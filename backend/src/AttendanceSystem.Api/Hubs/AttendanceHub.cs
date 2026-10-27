using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Api.Hubs;

/// <summary>Professors join a session group to receive QrRotated, AttendanceUpdated and SessionEnded events.</summary>
[Authorize(Roles = "Professor")]
public class AttendanceHub(AppDbContext db) : Hub
{
    public static string GroupName(Guid sessionId) => $"session-{sessionId}";

    public async Task JoinSession(Guid sessionId)
    {
        var professorId = Context.User!.UserId();
        var owns = await db.Sessions.AnyAsync(s => s.Id == sessionId && s.Course.ProfessorId == professorId);
        if (!owns) throw new HubException("Session not found or not yours.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(sessionId));
    }

    public Task LeaveSession(Guid sessionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(sessionId));
}
