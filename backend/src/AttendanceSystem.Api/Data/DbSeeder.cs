using AttendanceSystem.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Data;

/// <summary>Demo data: 1 professor, 60 students, 1 classroom, 2 courses, 8 past sessions per course.</summary>
public static class DbSeeder
{
    public const string ProfessorEmail = "prof@hu.edu.jo";
    public const string ProfessorPassword = "Prof@12345";
    public const string StudentPassword = "Student@123";

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var prof = new User
        {
            UniversityId = "P0001", FullName = "Dr. Demo Professor", Email = ProfessorEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ProfessorPassword, 10), Role = UserRole.Professor
        };
        var studentHash = BCrypt.Net.BCrypt.HashPassword(StudentPassword, 10);
        var students = Enumerable.Range(1, 60).Select(i => new User
        {
            UniversityId = $"2026{i:0000}", FullName = $"Student {i:000}",
            Email = $"student{i:000}@hu.edu.jo", PasswordHash = studentHash, Role = UserRole.Student
        }).ToList();

        // Replace with the real coordinates of your classroom before production use.
        var room = new Classroom { Name = "IT Building - Hall 101", Latitude = 32.1030, Longitude = 36.1820, RadiusMeters = 30 };

        var c1 = new Course { Code = "CIS301", Name = "Advanced Database Systems", ProfessorId = prof.Id, Semester = "Fall 2026" };
        var c2 = new Course { Code = "CIS320", Name = "Mobile Application Development", ProfessorId = prof.Id, Semester = "Fall 2026" };

        db.Users.Add(prof);
        db.Users.AddRange(students);
        db.Classrooms.Add(room);
        db.Courses.AddRange(c1, c2);
        await db.SaveChangesAsync(ct);

        var enrollments = students.Select(s => new Enrollment { CourseId = c1.Id, StudentId = s.Id }).ToList();
        enrollments.AddRange(students.Take(40).Select(s => new Enrollment { CourseId = c2.Id, StudentId = s.Id }));
        db.Enrollments.AddRange(enrollments);

        var rng = new Random(42);
        var today = DateTime.UtcNow.Date;
        foreach (var course in new[] { c1, c2 })
        {
            var enrolled = enrollments.Where(e => e.CourseId == course.Id).Select(e => e.StudentId).ToList();
            for (var w = 8; w >= 1; w--)
            {
                var start = today.AddDays(-7 * w).AddHours(9);
                var session = new ClassSession
                {
                    CourseId = course.Id, ClassroomId = room.Id, StartedAt = start,
                    EndedAt = start.AddHours(1), Status = SessionStatus.Ended
                };
                db.Sessions.Add(session);
                for (var i = 0; i < enrolled.Count; i++)
                {
                    var p = i % 15 == 0 ? 0.35 : i % 7 == 0 ? 0.6 : 0.92;   // a few at-risk students
                    if (rng.NextDouble() > p) continue;
                    var late = rng.NextDouble() < 0.15;
                    db.AttendanceRecords.Add(new AttendanceRecord
                    {
                        SessionId = session.Id, StudentId = enrolled[i],
                        CheckedInAt = start.AddMinutes(late ? 12 + rng.Next(10) : rng.Next(1, 8)),
                        Latitude = room.Latitude, Longitude = room.Longitude,
                        DistanceMeters = Math.Round(rng.NextDouble() * 20, 1),
                        Status = late ? AttendanceStatus.Late : AttendanceStatus.Present
                    });
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
