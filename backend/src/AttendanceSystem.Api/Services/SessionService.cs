using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Services;

public class SessionService(AppDbContext db, IQrTokenStore qr, ILiveNotifier notifier, TimeProvider time)
{
    public static SessionDto ToDto(ClassSession s) =>
        new(s.Id, s.CourseId, s.Course.Name, s.Course.Code, s.Classroom.Name, s.StartedAt, s.EndedAt, s.Status.ToString());

    public async Task<ProfessorOverviewDto> GetOverviewAsync(Guid professorId, CancellationToken ct)
    {
        var courses = await db.Courses.Where(c => c.ProfessorId == professorId).OrderBy(c => c.Code).ToListAsync(ct);
        var ids = courses.Select(c => c.Id).ToList();
        var enrolled = await db.Enrollments.Where(e => ids.Contains(e.CourseId))
            .GroupBy(e => e.CourseId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var active = await db.Sessions.Where(s => ids.Contains(s.CourseId) && s.Status == SessionStatus.Active)
            .Select(s => new { s.CourseId, s.Id }).ToListAsync(ct);
        var rooms = await db.Classrooms.OrderBy(c => c.Name).ToListAsync(ct);
        return new ProfessorOverviewDto(
            courses.Select(c => new ProfessorCourseDto(c.Id, c.Code, c.Name, c.Semester, enrolled.GetValueOrDefault(c.Id),
                c.AbsenceThresholdPercent, active.FirstOrDefault(a => a.CourseId == c.Id)?.Id)).ToList(),
            rooms.Select(r => new ClassroomDto(r.Id, r.Name, r.Latitude, r.Longitude, r.RadiusMeters)).ToList());
    }

    public async Task<ServiceResult<SessionDto>> StartAsync(Guid professorId, StartSessionRequest req, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == req.CourseId, ct);
        if (course is null) return Result.Fail<SessionDto>(404, "Course not found.");
        if (course.ProfessorId != professorId) return Result.Fail<SessionDto>(403, "This is not your course.");
        var room = await db.Classrooms.FirstOrDefaultAsync(c => c.Id == req.ClassroomId, ct);
        if (room is null) return Result.Fail<SessionDto>(404, "Classroom not found.");

        if ((req.Latitude is null) != (req.Longitude is null) ||
            (req.Latitude is not null && !GeoCalculator.IsValidCoordinate(req.Latitude, req.Longitude)))
            return Result.Fail<SessionDto>(400, "Provide both a valid latitude and longitude, or neither.");

        if (await db.Sessions.AnyAsync(s => s.CourseId == course.Id && s.Status == SessionStatus.Active, ct))
            return Result.Fail<SessionDto>(409, "This course already has an active session.");

        var session = new ClassSession
        {
            CourseId = course.Id, ClassroomId = room.Id, StartedAt = time.GetUtcNow().UtcDateTime,
            ProfessorLatitude = req.Latitude, ProfessorLongitude = req.Longitude
        };
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
        await qr.RotateAsync(session.Id, ct);
        session.Course = course;
        session.Classroom = room;
        return Result.Success(ToDto(session), 201);
    }

    public async Task<ServiceResult<SessionDto>> EndAsync(Guid professorId, Guid sessionId, CancellationToken ct)
    {
        var s = await LoadOwnedAsync(professorId, sessionId, ct);
        if (s.Error != null) return Result.Fail<SessionDto>(s.Status, s.Error);
        var session = s.Session!;
        if (session.Status == SessionStatus.Active)
        {
            session.Status = SessionStatus.Ended;
            session.EndedAt = time.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
            await qr.RemoveAsync(session.Id, ct);
            await notifier.SessionEndedAsync(session.Id, ct);
        }
        return Result.Success(ToDto(session));
    }

    public async Task<ServiceResult<LiveSessionDto>> GetLiveAsync(Guid professorId, Guid sessionId, CancellationToken ct)
    {
        var s = await LoadOwnedAsync(professorId, sessionId, ct);
        if (s.Error != null) return Result.Fail<LiveSessionDto>(s.Status, s.Error);
        var session = s.Session!;
        var records = await db.AttendanceRecords.Include(r => r.Student)
            .Where(r => r.SessionId == sessionId).OrderBy(r => r.CheckedInAt).ToListAsync(ct);
        var enrolled = await db.Enrollments.CountAsync(e => e.CourseId == session.CourseId, ct);
        var entries = records.Select(r => new LiveEntryDto(r.StudentId, r.Student.FullName, r.Student.UniversityId,
            r.CheckedInAt, r.Status.ToString())).ToList();
        return Result.Success(new LiveSessionDto(ToDto(session), enrolled, entries.Count, entries));
    }

    public async Task<ServiceResult<QrDto>> GetQrAsync(Guid professorId, Guid sessionId, CancellationToken ct)
    {
        var s = await LoadOwnedAsync(professorId, sessionId, ct);
        if (s.Error != null) return Result.Fail<QrDto>(s.Status, s.Error);
        if (s.Session!.Status != SessionStatus.Active) return Result.Fail<QrDto>(409, "This session has ended.");
        var token = await qr.GetCurrentAsync(sessionId, ct) ?? await qr.RotateAsync(sessionId, ct);
        return Result.Success(new QrDto(sessionId, token, QrTokens.Payload(sessionId, token),
            QrTokens.RotationSeconds, (int)QrTokens.Ttl.TotalSeconds));
    }

    private async Task<(int Status, string? Error, ClassSession? Session)> LoadOwnedAsync(Guid professorId, Guid sessionId, CancellationToken ct)
    {
        var session = await db.Sessions.Include(x => x.Course).Include(x => x.Classroom).FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (session is null) return (404, "Session not found.", null);
        if (session.Course.ProfessorId != professorId) return (403, "This is not your session.", null);
        return (200, null, session);
    }
}
