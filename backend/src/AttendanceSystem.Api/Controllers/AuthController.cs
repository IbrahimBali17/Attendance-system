using AttendanceSystem.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AttendanceSystem.Api.Controllers;

[Route("api/auth")]
public class AuthController(AuthService auth) : ApiControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest req, [FromServices] IValidator<LoginRequest> v, CancellationToken ct) =>
        await ValidateAsync(v, req, ct) ?? From(await auth.LoginAsync(req, ct));

    [HttpPost("refresh"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh(RefreshRequest req, [FromServices] IValidator<RefreshRequest> v, CancellationToken ct) =>
        await ValidateAsync(v, req, ct) ?? From(await auth.RefreshAsync(req, ct));

    [HttpPost("register-device"), Authorize]
    public async Task<IActionResult> RegisterDevice(RegisterDeviceRequest req, [FromServices] IValidator<RegisterDeviceRequest> v, CancellationToken ct)
    {
        var invalid = await ValidateAsync(v, req, ct);
        if (invalid != null) return invalid;
        var res = await auth.RegisterDeviceAsync(CurrentUserId, req.FcmToken, ct);
        return res.Ok ? NoContent() : From(res);
    }
}
