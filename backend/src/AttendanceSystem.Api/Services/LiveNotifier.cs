using AttendanceSystem.Api.Hubs;
using AttendanceSystem.Api.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace AttendanceSystem.Api.Services;

public interface ILiveNotifier
{
    Task QrRotatedAsync(Guid sessionId, string token, CancellationToken ct = default);
    Task AttendanceUpdatedAsync(Guid sessionId, LiveEntryDto entry, int checkedIn, int enrolled, CancellationToken ct = default);
    Task SessionEndedAsync(Guid sessionId, CancellationToken ct = default);
}

public class SignalRLiveNotifier(IHubContext<AttendanceHub> hub) : ILiveNotifier
{
    private IClientProxy Group(Guid sessionId) => hub.Clients.Group(AttendanceHub.GroupName(sessionId));

    public Task QrRotatedAsync(Guid sessionId, string token, CancellationToken ct = default) =>
        Group(sessionId).SendAsync("QrRotated",
            new QrDto(sessionId, token, QrTokens.Payload(sessionId, token), QrTokens.RotationSeconds, (int)QrTokens.Ttl.TotalSeconds), ct);

    public Task AttendanceUpdatedAsync(Guid sessionId, LiveEntryDto entry, int checkedIn, int enrolled, CancellationToken ct = default) =>
        Group(sessionId).SendAsync("AttendanceUpdated", new { sessionId, entry, checkedIn, enrolled }, ct);

    public Task SessionEndedAsync(Guid sessionId, CancellationToken ct = default) =>
        Group(sessionId).SendAsync("SessionEnded", new { sessionId }, ct);
}
