namespace AttendanceSystem.Api.Services;

public record LoginRequest(string Login, string Password);
public record RefreshRequest(string RefreshToken);
public record RegisterDeviceRequest(string FcmToken);
public record UserDto(Guid Id, string UniversityId, string FullName, string Email, string Role);
public record AuthResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt, UserDto User);

public record CheckInRequest(Guid SessionId, string? Token, double? Latitude, double? Longitude);
public record AttendanceRecordDto(Guid Id, Guid SessionId, string CourseName, DateTime CheckedInAt, string Status, double DistanceMeters);

public record StartSessionRequest(Guid CourseId, Guid ClassroomId, double? Latitude, double? Longitude);
public record SessionDto(Guid Id, Guid CourseId, string CourseName, string CourseCode, string Classroom, DateTime StartedAt, DateTime? EndedAt, string Status);
public record QrDto(Guid SessionId, string Token, string Payload, int RotatesInSeconds, int ExpiresInSeconds);
public record LiveEntryDto(Guid StudentId, string FullName, string UniversityId, DateTime CheckedInAt, string Status);
public record LiveSessionDto(SessionDto Session, int Enrolled, int CheckedIn, List<LiveEntryDto> Entries);

public record ClassroomDto(Guid Id, string Name, double Latitude, double Longitude, double RadiusMeters);
public record ProfessorCourseDto(Guid Id, string Code, string Name, string Semester, int Enrolled, double ThresholdPercent, Guid? ActiveSessionId);
public record ProfessorOverviewDto(List<ProfessorCourseDto> Courses, List<ClassroomDto> Classrooms);

public record StudentCourseDto(Guid Id, string Code, string Name, string Professor, int TotalSessions, int Attended, double Percent, double ThresholdPercent);
public record ActiveSessionDto(Guid SessionId, Guid CourseId, string CourseName, string Classroom, DateTime StartedAt);
public record HistoryCourseDto(Guid CourseId, string CourseName, int TotalSessions, int Attended, double Percent, List<AttendanceRecordDto> Records);
public record NotificationDto(Guid Id, string Type, string Message, DateTime SentAt);

public record SessionRateDto(Guid SessionId, DateTime StartedAt, int Present, int Late, int Absent, double Rate);
public record StudentStatDto(Guid StudentId, string UniversityId, string FullName, int Attended, int Late, int Total, double Percent, bool AtRisk);
public record DistributionBucketDto(string Label, int Count);
public record CourseAnalyticsDto(Guid CourseId, string Code, string CourseName, int Enrolled, int EndedSessions, double AverageRate,
    double ThresholdPercent, List<SessionRateDto> Sessions, List<StudentStatDto> Students, List<StudentStatDto> AtRisk,
    List<DistributionBucketDto> Distribution);
