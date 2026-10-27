using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Services;

public class AttendanceService(AppDbContext db, IQrTokenStore qr, ILiveNotifier notifier, TimeProvider time)
{
    /// <summary>
    /// Dual-layer check-in. Order: session 404, enrolment 403, GPS 400, ended 409, QR 410, distance 403, duplicate 409.
    /// </summary>
    public async Task<ServiceResult<AttendanceRecordDto>> CheckInAsync(Guid studentId, CheckInRequest req, CancellationToken ct)
    {
        var session = await db.Sessions.Include(s => s.Classroom).Include(s => s.Course)
            .FirstOrDefaultAsync(s => s.Id == req.SessionId, ct);
        if (session is null) return Result.Fail<AttendanceRecordDto>(404, "Session not found.");

        var enrolled = await db.Enrollments.AnyAsync(e => e.CourseId == session.CourseId && e.StudentId == studentId, ct);
        if (!enrolled) return Result.Fail<AttendanceRecordDto>(403, "You are not enrolled in this course.");

        if (!GeoCalculator.IsValidCoordinate(req.Latitude, req.Longitude))
            return Result.Fail<AttendanceRecordDto>(400, "Valid GPS coordinates are required.");

        if (session.Status != SessionStatus.Active)
            return Result.Fail<AttendanceRecordDto>(409, "This session has ended.");

        if (string.IsNullOrWhiteSpace(req.Token) || !await qr.IsValidAsync(session.Id, req.Token, ct))
            return Result.Fail<AttendanceRecordDto>(410, "QR code expired or invalid. Scan the latest code.");

        var refLat = session.ProfessorLatitude ?? session.Classroom.Latitude;
        var refLon = session.ProfessorLongitude ?? session.Classroom.Longitude;
        var distance = GeoCalculator.DistanceMeters(req.Latitude!.Value, req.Longitude!.Value, refLat, refLon);
        if (!GeoCalculator.IsWithinRadius(distance, session.Classroom.RadiusMeters))
            return Result.Fail<AttendanceRecordDto>(403,
                $"You are {distance:F0} m from the classroom; you must be within {session.Classroom.RadiusMeters:F0} m.",
                new Dictionary<string, object?> { ["distanceMeters"] = Math.Round(distance, 1), ["radiusMeters"] = session.Classroom.RadiusMeters });

        if (await db.AttendanceRecords.AnyAsync(r => r.SessionId == session.Id && r.StudentId == studentId, ct))
            return Result.Fail<AttendanceRecordDto>(409, "You have already checked in to this session.");

        var now = time.GetUtcNow().UtcDateTime;
        var record = new AttendanceRecord
        {
            SessionId = session.Id, StudentId = studentId, CheckedInAt = now,
            Latitude = req.Latitude.Value, Longitude = req.Longitude.Value,
            DistanceMeters = Math.Round(distance, 1),
            Status = AttendanceRules.DetermineStatus(now, session.StartedAt, session.Course.LateAfterMinutes)
        };
        db.AttendanceRecords.Add(record);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Result.Fail<AttendanceRecordDto>(409, "You have already checked in to this session."); }

        var student = await db.Users.FirstAsync(u => u.Id == studentId, ct);
        var checkedIn = await db.AttendanceRecords.CountAsync(r => r.SessionId == session.Id, ct);
        var enrolledCount = await db.Enrollments.CountAsync(e => e.CourseId == session.CourseId, ct);
        await notifier.AttendanceUpdatedAsync(session.Id,
            new LiveEntryDto(student.Id, student.FullName, student.UniversityId, record.CheckedInAt, record.Status.ToString()),
            checkedIn, enrolledCount, ct);

        return Result.Success(new AttendanceRecordDto(record.Id, session.Id, session.Course.Name, record.CheckedInAt,
            record.Status.ToString(), record.DistanceMeters), 201);
    }
}
