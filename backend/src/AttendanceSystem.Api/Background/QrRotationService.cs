using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Domain;
using AttendanceSystem.Api.Infrastructure;
using AttendanceSystem.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Background;

/// <summary>Issues a fresh QR token for every active session every 28 s (tokens live 30 s) and pushes it to the dashboard.</summary>
public class QrRotationService(IServiceScopeFactory scopes, IQrTokenStore qr, ILiveNotifier notifier, ILogger<QrRotationService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(QrTokens.RotationSeconds));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var ids = await db.Sessions.Where(s => s.Status == SessionStatus.Active).Select(s => s.Id).ToListAsync(stoppingToken);
                foreach (var id in ids)
                {
                    var token = await qr.RotateAsync(id, stoppingToken);
                    await notifier.QrRotatedAsync(id, token, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "QR rotation tick failed."); }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
