namespace AttendanceSystem.Api.Domain;

public static class AttendanceRules
{
    public static AttendanceStatus DetermineStatus(DateTime nowUtc, DateTime startedAtUtc, int lateAfterMinutes) =>
        nowUtc > startedAtUtc.AddMinutes(lateAfterMinutes) ? AttendanceStatus.Late : AttendanceStatus.Present;

    /// <summary>Attendance percentage; a course with no finished sessions counts as 100%.</summary>
    public static double Percent(int attended, int total) =>
        total <= 0 ? 100 : Math.Round(attended * 100.0 / total, 1);

    public static bool IsBelowThreshold(double percent, double threshold) => percent < threshold;
}
