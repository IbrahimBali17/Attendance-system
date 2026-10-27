using AttendanceSystem.Api.Services;

namespace AttendanceSystem.Api.Background;

/// <summary>Runs the absence-alert engine every 15 minutes (Alerts:IntervalMinutes).</summary>
public class AbsenceAlertService(IServiceScopeFactory scopes, IConfiguration cfg, ILogger<AbsenceAlertService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = Math.Max(1, cfg.GetValue("Alerts:IntervalMinutes", 15));
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
        try
        {
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<AbsenceAlertEngine>().RunOnceAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
                catch (Exception ex) { log.LogError(ex, "Absence alert run failed."); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { }
    }
}
