using AttendanceSystem.Api.Infrastructure;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace AttendanceSystem.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected Guid CurrentUserId => User.UserId();

    protected IActionResult From<T>(ServiceResult<T> r)
    {
        if (r.Ok) return StatusCode(r.Status, r.Data);
        var pd = new ProblemDetails { Status = r.Status, Title = ReasonPhrases.GetReasonPhrase(r.Status), Detail = r.Error };
        if (r.Extra != null) foreach (var kv in r.Extra) pd.Extensions[kv.Key] = kv.Value;
        return new ObjectResult(pd) { StatusCode = r.Status };
    }

    protected async Task<IActionResult?> ValidateAsync<T>(IValidator<T> validator, T model, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(model, ct);
        if (result.IsValid) return null;
        return ValidationProblem(new ValidationProblemDetails(result.ToDictionary()));
    }
}
