namespace AttendanceSystem.Api.Domain;

public enum UserRole { Student = 0, Professor = 1 }
public enum SessionStatus { Active = 0, Ended = 1 }
public enum AttendanceStatus { Present = 0, Late = 1 }

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UniversityId { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public string? FcmToken { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid ProfessorId { get; set; }
    public User Professor { get; set; } = null!;
    public string Semester { get; set; } = "";
    public double AbsenceThresholdPercent { get; set; } = 75;
    public int LateAfterMinutes { get; set; } = 10;
}

public class Enrollment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
}

public class Classroom
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; } = 30;
}

public class ClassSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public Guid ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Active;
    /// <summary>Optional override of the classroom position (professor's live position).</summary>
    public double? ProfessorLatitude { get; set; }
    public double? ProfessorLongitude { get; set; }
}

public class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ClassSession Session { get; set; } = null!;
    public Guid StudentId { get; set; }
    public User Student { get; set; } = null!;
    public DateTime CheckedInAt { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double DistanceMeters { get; set; }
    public AttendanceStatus Status { get; set; }
}

public class NotificationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StudentId { get; set; }
    public Guid CourseId { get; set; }
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTime SentAt { get; set; }
    /// <summary>True once the student recovered above the threshold (re-arms the alert).</summary>
    public bool Resolved { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}
