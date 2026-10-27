using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AttendanceSystem.Tests.Unit;

public class AbsenceAlertEngineTests
{
    private sealed class FakePush : IPushSender
    {
        public List<(string Token, string Title, string Body)> Sent { get; } = new();
        public Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default)
        {
            Sent.Add((deviceToken, title, body));
            return Task.CompletedTask;
        }
    }

    private static AppDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AbsenceAlertEngine NewEngine(AppDbContext db, FakePush push) =>
        new(db, push, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Alerts:MinSessions"] = "3" }).Build(),
            TimeProvider.System, NullLogger<AbsenceAlertEngine>.Instance);

    private static List<ClassSession> AddEndedSessions(AppDbContext db, Course course, Classroom room, int count)
    {
        var list = Enumerable.Range(0, count).Select(i => new ClassSession
        {
            CourseId = course.Id, ClassroomId = room.Id, Status = SessionStatus.Ended,
            StartedAt = DateTime.UtcNow.AddDays(-30 + i), EndedAt = DateTime.UtcNow.AddDays(-30 + i).AddHours(1)
        }).ToList();
        db.Sessions.AddRange(list);
        return list;
    }

    private static void Attend(AppDbContext db, User student, IEnumerable<ClassSession> sessions) =>
        db.AttendanceRecords.AddRange(sessions.Select(s => new AttendanceRecord
        {
            SessionId = s.Id, StudentId = student.Id, CheckedInAt = s.StartedAt.AddMinutes(2), Status = AttendanceStatus.Present
        }));

    [Fact]
    public async Task Alert_IsSentOncePerThresholdCrossing_AndRearmsAfterRecovery()
    {
        await using var db = NewDb();
        var prof = new User { Role = UserRole.Professor, FullName = "P", Email = "p@x", UniversityId = "P" };
        var student = new User { Role = UserRole.Student, FullName = "S", Email = "s@x", UniversityId = "S", FcmToken = "device-token" };
        var room = new Classroom { Name = "R" };
        var course = new Course { Code = "C1", Name = "Course", ProfessorId = prof.Id, AbsenceThresholdPercent = 75 };
        db.AddRange(prof, student, room, course);
        db.Enrollments.Add(new Enrollment { CourseId = course.Id, StudentId = student.Id });
        var first4 = AddEndedSessions(db, course, room, 4);
        Attend(db, student, first4.Take(1));                       // 1 of 4 = 25%
        await db.SaveChangesAsync();

        var push = new FakePush();
        var engine = NewEngine(db, push);

        (await engine.RunOnceAsync()).Should().Be(1);
        push.Sent.Should().ContainSingle().Which.Token.Should().Be("device-token");

        (await engine.RunOnceAsync()).Should().Be(0, "the same crossing must not alert twice");

        Attend(db, student, first4.Skip(1));                       // 4 of 4 = 100% -> recovered
        await db.SaveChangesAsync();
        (await engine.RunOnceAsync()).Should().Be(0);
        (await db.NotificationLogs.SingleAsync()).Resolved.Should().BeTrue();

        AddEndedSessions(db, course, room, 4);                     // 4 of 8 = 50% -> crosses again
        await db.SaveChangesAsync();
        (await engine.RunOnceAsync()).Should().Be(1);
        push.Sent.Should().HaveCount(2);
        db.NotificationLogs.Count().Should().Be(2);
    }

    [Fact]
    public async Task NoAlert_BeforeMinimumSessions()
    {
        await using var db = NewDb();
        var prof = new User { Role = UserRole.Professor, FullName = "P", Email = "p@x", UniversityId = "P" };
        var student = new User { Role = UserRole.Student, FullName = "S", Email = "s@x", UniversityId = "S" };
        var room = new Classroom { Name = "R" };
        var course = new Course { Code = "C1", Name = "Course", ProfessorId = prof.Id };
        db.AddRange(prof, student, room, course);
        db.Enrollments.Add(new Enrollment { CourseId = course.Id, StudentId = student.Id });
        AddEndedSessions(db, course, room, 2);
        await db.SaveChangesAsync();

        (await NewEngine(db, new FakePush()).RunOnceAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StudentWithoutDeviceToken_StillGetsInAppAlert_WithoutPush()
    {
        await using var db = NewDb();
        var prof = new User { Role = UserRole.Professor, FullName = "P", Email = "p@x", UniversityId = "P" };
        var student = new User { Role = UserRole.Student, FullName = "S", Email = "s@x", UniversityId = "S" };
        var room = new Classroom { Name = "R" };
        var course = new Course { Code = "C1", Name = "Course", ProfessorId = prof.Id };
        db.AddRange(prof, student, room, course);
        db.Enrollments.Add(new Enrollment { CourseId = course.Id, StudentId = student.Id });
        AddEndedSessions(db, course, room, 4);
        await db.SaveChangesAsync();

        var push = new FakePush();
        (await NewEngine(db, push).RunOnceAsync()).Should().Be(1);
        push.Sent.Should().BeEmpty();
        (await db.NotificationLogs.CountAsync()).Should().Be(1);
    }
}
