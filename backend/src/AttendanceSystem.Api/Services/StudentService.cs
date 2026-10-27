using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Services;

public class StudentService(AppDbContext db)
{
    private async Task<(List<Course> Courses, Dictionary<Guid, int> Totals, Dictionary<Guid, int> Attended)> LoadAsync(Guid studentId, Guid? courseId, CancellationToken ct)
    {
        var courses = await db.Courses.Include(c => c.Professor)
            .Where(c => db.Enrollments.Any(e => e.CourseId == c.Id && e.StudentId == studentId) && (courseId == null || c.Id == courseId))
            .ToListAsync(ct);
        var ids = courses.Select(c => c.Id).ToList();
        var totals = await db.Sessions.Where(s => ids.Contains(s.CourseId) && s.Status == SessionStatus.Ended)
            .GroupBy(s => s.CourseId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var attended = await db.AttendanceRecords
            .Where(r => r.StudentId == studentId && ids.Contains(r.Session.CourseId) && r.Session.Status == SessionStatus.Ended)
            .GroupBy(r => r.Session.CourseId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return (courses, totals, attended);
    }

    public async Task<List<StudentCourseDto>> GetCoursesAsync(Guid studentId, CancellationToken ct)
    {
        var (courses, totals, attended) = await LoadAsync(studentId, null, ct);
        return courses.OrderBy(c => c.Code).Select(c =>
        {
            var t = totals.GetValueOrDefault(c.Id);
            var a = attended.GetValueOrDefault(c.Id);
            return new StudentCourseDto(c.Id, c.Code, c.Name, c.Professor.FullName, t, a, AttendanceRules.Percent(a, t), c.AbsenceThresholdPercent);
        }).ToList();
    }

    public async Task<List<ActiveSessionDto>> GetActiveSessionsAsync(Guid studentId, CancellationToken ct)
    {
        var courseIds = await db.Enrollments.Where(e => e.StudentId == studentId).Select(e => e.CourseId).ToListAsync(ct);
        var sessions = await db.Sessions.Include(s => s.Course).Include(s => s.Classroom)
            .Where(s => courseIds.Contains(s.CourseId) && s.Status == SessionStatus.Active).ToListAsync(ct);
        return sessions.Select(s => new ActiveSessionDto(s.Id, s.CourseId, s.Course.Name, s.Classroom.Name, s.StartedAt)).ToList();
    }

    public async Task<List<HistoryCourseDto>> GetHistoryAsync(Guid studentId, Guid? courseId, CancellationToken ct)
    {
        var (courses, totals, attended) = await LoadAsync(studentId, courseId, ct);
        var ids = courses.Select(c => c.Id).ToList();
        var records = await db.AttendanceRecords.Include(r => r.Session)
            .Where(r => r.StudentId == studentId && ids.Contains(r.Session.CourseId))
            .OrderByDescending(r => r.CheckedInAt).ToListAsync(ct);
        return courses.OrderBy(c => c.Code).Select(c =>
        {
            var t = totals.GetValueOrDefault(c.Id);
            var a = attended.GetValueOrDefault(c.Id);
            var list = records.Where(r => r.Session.CourseId == c.Id)
                .Select(r => new AttendanceRecordDto(r.Id, r.SessionId, c.Name, r.CheckedInAt, r.Status.ToString(), r.DistanceMeters)).ToList();
            return new HistoryCourseDto(c.Id, c.Name, t, a, AttendanceRules.Percent(a, t), list);
        }).ToList();
    }

    public async Task<List<NotificationDto>> GetNotificationsAsync(Guid studentId, CancellationToken ct)
    {
        var logs = await db.NotificationLogs.Where(n => n.StudentId == studentId)
            .OrderByDescending(n => n.SentAt).Take(50).ToListAsync(ct);
        return logs.Select(n => new NotificationDto(n.Id, n.Type, n.Message, n.SentAt)).ToList();
    }
}
