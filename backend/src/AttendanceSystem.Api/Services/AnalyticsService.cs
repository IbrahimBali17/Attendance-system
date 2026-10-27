using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Services;

public class AnalyticsService(AppDbContext db)
{
    public async Task<ServiceResult<CourseAnalyticsDto>> GetAsync(Guid professorId, Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.FirstOrDefaultAsync(c => c.Id == courseId, ct);
        if (course is null) return Result.Fail<CourseAnalyticsDto>(404, "Course not found.");
        if (course.ProfessorId != professorId) return Result.Fail<CourseAnalyticsDto>(403, "This is not your course.");

        var students = await db.Enrollments.Where(e => e.CourseId == courseId).Select(e => e.Student)
            .OrderBy(s => s.UniversityId).ToListAsync(ct);
        var sessions = await db.Sessions.Where(s => s.CourseId == courseId && s.Status == SessionStatus.Ended)
            .OrderBy(s => s.StartedAt).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var records = await db.AttendanceRecords.Where(r => sessionIds.Contains(r.SessionId)).ToListAsync(ct);

        var enrolled = students.Count;
        var sessionRates = sessions.Select(s =>
        {
            var rs = records.Where(r => r.SessionId == s.Id).ToList();
            var late = rs.Count(r => r.Status == AttendanceStatus.Late);
            var present = rs.Count - late;
            var rate = enrolled == 0 ? 0 : Math.Round(rs.Count * 100.0 / enrolled, 1);
            return new SessionRateDto(s.Id, s.StartedAt, present, late, Math.Max(0, enrolled - rs.Count), rate);
        }).ToList();

        var stats = students.Select(st =>
        {
            var mine = records.Where(r => r.StudentId == st.Id).ToList();
            var pct = AttendanceRules.Percent(mine.Count, sessions.Count);
            return new StudentStatDto(st.Id, st.UniversityId, st.FullName, mine.Count,
                mine.Count(r => r.Status == AttendanceStatus.Late), sessions.Count, pct,
                sessions.Count > 0 && AttendanceRules.IsBelowThreshold(pct, course.AbsenceThresholdPercent));
        }).ToList();

        var distribution = new List<DistributionBucketDto>
        {
            new("90-100%", stats.Count(s => s.Percent >= 90)),
            new("75-89%", stats.Count(s => s.Percent is >= 75 and < 90)),
            new("50-74%", stats.Count(s => s.Percent is >= 50 and < 75)),
            new("< 50%", stats.Count(s => s.Percent < 50)),
        };

        return Result.Success(new CourseAnalyticsDto(course.Id, course.Code, course.Name, enrolled, sessions.Count,
            sessionRates.Count == 0 ? 0 : Math.Round(sessionRates.Average(s => s.Rate), 1),
            course.AbsenceThresholdPercent, sessionRates, stats,
            stats.Where(s => s.AtRisk).OrderBy(s => s.Percent).ToList(), distribution));
    }
}
