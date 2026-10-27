using AttendanceSystem.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.Api.Controllers;

[Authorize(Roles = "Professor")]
public class ProfessorController(SessionService sessions, AnalyticsService analytics) : ApiControllerBase
{
    [HttpGet("api/professor/courses")]
    public async Task<IActionResult> Courses(CancellationToken ct) => Ok(await sessions.GetOverviewAsync(CurrentUserId, ct));

    [HttpPost("api/sessions")]
    public async Task<IActionResult> Start(StartSessionRequest req, [FromServices] IValidator<StartSessionRequest> v, CancellationToken ct) =>
        await ValidateAsync(v, req, ct) ?? From(await sessions.StartAsync(CurrentUserId, req, ct));

    [HttpPost("api/sessions/{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, CancellationToken ct) => From(await sessions.EndAsync(CurrentUserId, id, ct));

    [HttpGet("api/sessions/{id:guid}/live")]
    public async Task<IActionResult> Live(Guid id, CancellationToken ct) => From(await sessions.GetLiveAsync(CurrentUserId, id, ct));

    [HttpGet("api/sessions/{id:guid}/qr")]
    public async Task<IActionResult> Qr(Guid id, CancellationToken ct) => From(await sessions.GetQrAsync(CurrentUserId, id, ct));

    [HttpGet("api/courses/{id:guid}/analytics")]
    public async Task<IActionResult> Analytics(Guid id, CancellationToken ct) => From(await analytics.GetAsync(CurrentUserId, id, ct));

    [HttpGet("api/courses/{id:guid}/report/excel")]
    public async Task<IActionResult> Excel(Guid id, CancellationToken ct)
    {
        var res = await analytics.GetAsync(CurrentUserId, id, ct);
        if (!res.Ok) return From(res);
        return File(ExcelReportBuilder.Build(res.Data!),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{res.Data!.Code}-attendance.xlsx");
    }

    [HttpGet("api/courses/{id:guid}/report/word")]
    public async Task<IActionResult> Word(Guid id, CancellationToken ct)
    {
        var res = await analytics.GetAsync(CurrentUserId, id, ct);
        if (!res.Ok) return From(res);
        return File(WordReportBuilder.Build(res.Data!),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{res.Data!.Code}-attendance.docx");
    }
}
