using AttendanceSystem.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendanceSystem.Api.Controllers;

[Authorize(Roles = "Student")]
public class StudentController(StudentService students) : ApiControllerBase
{
    [HttpGet("api/student/courses")]
    public async Task<IActionResult> Courses(CancellationToken ct) => Ok(await students.GetCoursesAsync(CurrentUserId, ct));

    [HttpGet("api/student/active-session")]
    public async Task<IActionResult> ActiveSession(CancellationToken ct) =>
        Ok(new { sessions = await students.GetActiveSessionsAsync(CurrentUserId, ct) });

    [HttpGet("api/student/attendance-history")]
    public async Task<IActionResult> History([FromQuery] Guid? courseId, CancellationToken ct) =>
        Ok(await students.GetHistoryAsync(CurrentUserId, courseId, ct));

    [HttpGet("api/student/notifications")]
    public async Task<IActionResult> Notifications(CancellationToken ct) => Ok(await students.GetNotificationsAsync(CurrentUserId, ct));

    [HttpPost("api/attendance/check-in"), EnableRateLimiting("checkin")]
    public async Task<IActionResult> CheckIn(CheckInRequest req, [FromServices] IValidator<CheckInRequest> v, [FromServices] AttendanceService attendance, CancellationToken ct) =>
        await ValidateAsync(v, req, ct) ?? From(await attendance.CheckInAsync(CurrentUserId, req, ct));
}
