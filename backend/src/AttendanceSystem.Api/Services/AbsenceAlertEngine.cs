using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Services;

/// <summary>
/// Sends one push + in-app alert per threshold crossing. When the student recovers above the threshold the
/// open alert is marked Resolved, which re-arms it for a future crossing.
/// </summary>
public class AbsenceAlertEngine(AppDbContext db, IPushSender push, IConfiguration cfg, TimeProvider time, ILogger<AbsenceAlertEngine> log)
{
    public const string AlertType = "AbsenceAlert";

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var minSessions = cfg.GetValue("Alerts:MinSessions", 3);
        var sent = 0;
        var courses = await db.Courses.AsNoTracking().ToListAsync(ct);

        foreach (var course in courses)
        {
            var endedIds = await db.Sessions.Where(s => s.CourseId == course.Id && s.Status == SessionStatus.Ended)
                .Select(s => s.Id).ToListAsync(ct);
            if (endedIds.Count < minSessions) continue;

            var attended = await db.AttendanceRecords.Where(r => endedIds.Contains(r.SessionId))
                .GroupBy(r => r.StudentId).Select(g => new { StudentId = g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.StudentId, x => x.N, ct);
            var students = await db.Enrollments.Where(e => e.CourseId == course.Id).Select(e => e.Student).ToListAsync(ct);
            var openLogs = await db.NotificationLogs
                .Where(n => n.CourseId == course.Id && n.Type == AlertType && !n.Resolved).ToListAsync(ct);

            foreach (var st in students)
            {
                var pct = AttendanceRules.Percent(attended.GetValueOrDefault(st.Id), endedIds.Count);
                var open = openLogs.FirstOrDefault(n => n.StudentId == st.Id);
                if (AttendanceRules.IsBelowThreshold(pct, course.AbsenceThresholdPercent))
                {
                    if (open != null) continue;
                    var message = $"Your attendance in {course.Code} is {pct:0.#}%, below the required {course.AbsenceThresholdPercent:0.#}%.";
                    db.NotificationLogs.Add(new NotificationLog
                    {
                        StudentId = st.Id, CourseId = course.Id, Type = AlertType,
                        Message = message, SentAt = time.GetUtcNow().UtcDateTime
                    });
                    if (!string.IsNullOrEmpty(st.FcmToken)) await push.SendAsync(st.FcmToken, "Attendance warning", message, ct);
                    sent++;
                }
                else if (open != null)
                {
                    open.Resolved = true;
                }
            }
            await db.SaveChangesAsync(ct);
        }
        log.LogInformation("Absence alert run finished: {Sent} new alert(s).", sent);
        return sent;
    }
}
