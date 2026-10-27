using System.Net.Http.Headers;
using System.Net.Http.Json;
using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AttendanceSystem.Tests.Integration;

/// <summary>Seeds 1 professor, 1 classroom (30 m), 1 course, 2 enrolled students and 1 non-enrolled student.</summary>
public class Scenario : IDisposable
{
    public const double RoomLat = 32.1030, RoomLon = 36.1820;
    public const string Password = "Passw0rd!";
    public ApiFactory Factory { get; } = new();
    public Guid CourseId { get; private set; }
    public Guid RoomId { get; private set; }

    public static double OffsetNorth(double meters) => RoomLat + meters / 111_194.93;

    public async Task InitAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        var hash = BCrypt.Net.BCrypt.HashPassword(Password, 4);
        var prof = new User { UniversityId = "P1", FullName = "Prof", Email = "prof@test.dev", PasswordHash = hash, Role = UserRole.Professor };
        var s1 = new User { UniversityId = "S1", FullName = "Student One", Email = "s1@test.dev", PasswordHash = hash, Role = UserRole.Student };
        var s2 = new User { UniversityId = "S2", FullName = "Student Two", Email = "s2@test.dev", PasswordHash = hash, Role = UserRole.Student };
        var s3 = new User { UniversityId = "S3", FullName = "Outsider", Email = "s3@test.dev", PasswordHash = hash, Role = UserRole.Student };
        var room = new Classroom { Name = "Room", Latitude = RoomLat, Longitude = RoomLon, RadiusMeters = 30 };
        var course = new Course { Code = "T101", Name = "Testing", ProfessorId = prof.Id, Semester = "Fall 2026", LateAfterMinutes = 10 };
        db.Users.AddRange(prof, s1, s2, s3);
        db.Classrooms.Add(room);
        db.Courses.Add(course);
        db.Enrollments.AddRange(new Enrollment { CourseId = course.Id, StudentId = s1.Id }, new Enrollment { CourseId = course.Id, StudentId = s2.Id });
        await db.SaveChangesAsync();
        CourseId = course.Id;
        RoomId = room.Id;
    }

    public async Task<HttpClient> LoginAsync(string email)
    {
        var client = Factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { login = email, password = Password });
        res.EnsureSuccessStatusCode();
        var auth = (await res.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public async Task<SessionDto> StartSessionAsync(HttpClient professor)
    {
        var res = await professor.PostAsJsonAsync("/api/sessions", new { courseId = CourseId, classroomId = RoomId });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<SessionDto>())!;
    }

    public async Task<string> CurrentTokenAsync(HttpClient professor, Guid sessionId)
    {
        var res = await professor.GetAsync($"/api/sessions/{sessionId}/qr");
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<QrDto>())!.Token;
    }

    public Task<HttpResponseMessage> CheckInAsync(HttpClient student, Guid sessionId, string? token, double? lat, double? lon) =>
        student.PostAsJsonAsync("/api/attendance/check-in", new { sessionId, token, latitude = lat, longitude = lon });

    public void Dispose() => Factory.Dispose();
}
