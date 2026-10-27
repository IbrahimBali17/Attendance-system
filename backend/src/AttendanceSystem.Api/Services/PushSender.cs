using System.Net.Http.Json;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;

namespace AttendanceSystem.Api.Services;

public interface IPushSender
{
    Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default);
}

/// <summary>Sends via Firebase Cloud Messaging; Expo push tokens (ExponentPushToken[...]) go through Expo's push API.</summary>
public class PushSender : IPushSender
{
    private readonly HttpClient _http;
    private readonly ILogger<PushSender> _log;
    private static readonly object Gate = new();

    public PushSender(HttpClient http, IConfiguration cfg, ILogger<PushSender> log)
    {
        _http = http;
        _log = log;
        var b64 = cfg["Firebase:CredentialsBase64"];
        if (string.IsNullOrWhiteSpace(b64)) return;
        lock (Gate)
        {
            if (FirebaseApp.DefaultInstance != null) return;
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromJson(json) });
        }
    }

    public async Task SendAsync(string deviceToken, string title, string body, CancellationToken ct = default)
    {
        try
        {
            if (deviceToken.StartsWith("ExponentPushToken") || deviceToken.StartsWith("ExpoPushToken"))
            {
                var res = await _http.PostAsJsonAsync("https://exp.host/--/api/v2/push/send",
                    new { to = deviceToken, title, body, sound = "default" }, ct);
                res.EnsureSuccessStatusCode();
                return;
            }
            if (FirebaseApp.DefaultInstance == null)
            {
                _log.LogWarning("Firebase is not configured; push '{Title}' was not delivered.", title);
                return;
            }
            await FirebaseMessaging.DefaultInstance.SendAsync(new Message
            {
                Token = deviceToken,
                Notification = new Notification { Title = title, Body = body }
            }, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Push delivery failed (push is best-effort, alert is still stored in-app).");
        }
    }
}
