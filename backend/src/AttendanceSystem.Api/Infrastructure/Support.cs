using System.Security.Claims;
using AttendanceSystem.Api.Data;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AttendanceSystem.Api.Infrastructure;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "attendance-api";
    public string Audience { get; set; } = "attendance-clients";
    public int AccessMinutes { get; set; } = 60;
    public int RefreshDays { get; set; } = 14;
}

public static class ClaimsExtensions
{
    public static Guid UserId(this ClaimsPrincipal p) =>
        Guid.Parse(p.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException("Missing subject claim."));
}

public sealed record ServiceResult<T>(int Status, T? Data = default, string? Error = null, Dictionary<string, object?>? Extra = null)
{
    public bool Ok => Status is >= 200 and < 300;
}

public static class Result
{
    public static ServiceResult<T> Success<T>(T data, int status = 200) => new(status, data);
    public static ServiceResult<T> Fail<T>(int status, string error, Dictionary<string, object?>? extra = null) =>
        new(status, default, error, extra);
}

public static class PostgresConnection
{
    /// <summary>Supports ConnectionStrings:Postgres or a Railway-style DATABASE_URL (postgres://user:pass@host:port/db).</summary>
    public static string Resolve(IConfiguration cfg)
    {
        var url = cfg["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("postgres://") || url.StartsWith("postgresql://")))
        {
            var u = new Uri(url);
            var ui = u.UserInfo.Split(':', 2);
            var pass = ui.Length > 1 ? Uri.UnescapeDataString(ui[1]) : "";
            return $"Host={u.Host};Port={(u.Port > 0 ? u.Port : 5432)};Database={u.AbsolutePath.TrimStart('/')};" +
                   $"Username={Uri.UnescapeDataString(ui[0])};Password={pass};SSL Mode=Prefer;Trust Server Certificate=true";
        }
        return cfg.GetConnectionString("Postgres")
               ?? throw new InvalidOperationException("ConnectionStrings:Postgres (or DATABASE_URL) is not configured.");
    }
}

public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> log, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { /* client went away */ }
        catch (Exception ex)
        {
            log.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path);
            if (ctx.Response.HasStarted) throw;
            ctx.Response.Clear();
            ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
            ctx.Response.ContentType = "application/problem+json";
            await ctx.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Status = 500,
                Title = "An unexpected error occurred.",
                Detail = env.IsDevelopment() ? ex.ToString() : "Please try again later.",
                Extensions = { ["traceId"] = ctx.TraceIdentifier }
            });
        }
    }
}

public class DependenciesHealthCheck(AppDbContext db, IQrTokenStore qr) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            if (!await db.Database.CanConnectAsync(ct)) return HealthCheckResult.Unhealthy("Database unreachable.");
            if (!await qr.PingAsync(ct)) return HealthCheckResult.Unhealthy("Redis unreachable.");
            return HealthCheckResult.Healthy("Database and Redis are reachable.");
        }
        catch (Exception ex) { return HealthCheckResult.Unhealthy("Dependency check failed.", ex); }
    }
}
