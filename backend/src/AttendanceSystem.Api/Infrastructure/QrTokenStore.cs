using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using StackExchange.Redis;

namespace AttendanceSystem.Api.Infrastructure;

public static class QrTokens
{
    public static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    public const int RotationSeconds = 28;

    public static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Payload(Guid sessionId, string token) =>
        JsonSerializer.Serialize(new { sessionId, token });
}

/// <summary>Ephemeral QR tokens: each token lives 30 s; a new one is issued every 28 s.</summary>
public interface IQrTokenStore
{
    Task<string> RotateAsync(Guid sessionId, CancellationToken ct = default);
    Task<string?> GetCurrentAsync(Guid sessionId, CancellationToken ct = default);
    Task<bool> IsValidAsync(Guid sessionId, string token, CancellationToken ct = default);
    Task RemoveAsync(Guid sessionId, CancellationToken ct = default);
    Task<bool> PingAsync(CancellationToken ct = default);
}

public class RedisQrTokenStore(IConnectionMultiplexer redis) : IQrTokenStore
{
    private IDatabase Db => redis.GetDatabase();
    private static string Cur(Guid s) => $"qr:{s}:current";
    private static string Tok(Guid s, string t) => $"qr:{s}:tok:{t}";

    public static ConfigurationOptions ParseOptions(string cs)
    {
        if (cs.StartsWith("redis://") || cs.StartsWith("rediss://"))
        {
            var u = new Uri(cs);
            var opts = new ConfigurationOptions { Ssl = u.Scheme == "rediss", AbortOnConnectFail = false };
            opts.EndPoints.Add(u.Host, u.Port > 0 ? u.Port : 6379);
            var ui = u.UserInfo.Split(':', 2);
            if (ui.Length > 1) { opts.User = Uri.UnescapeDataString(ui[0]); opts.Password = Uri.UnescapeDataString(ui[1]); }
            else if (ui[0].Length > 0) opts.Password = Uri.UnescapeDataString(ui[0]);
            return opts;
        }
        var parsed = ConfigurationOptions.Parse(cs);
        parsed.AbortOnConnectFail = false;
        return parsed;
    }

    public async Task<string> RotateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var token = QrTokens.NewToken();
        await Db.StringSetAsync(Tok(sessionId, token), "1", QrTokens.Ttl);
        await Db.StringSetAsync(Cur(sessionId), token, QrTokens.Ttl);
        return token;
    }

    public async Task<string?> GetCurrentAsync(Guid sessionId, CancellationToken ct = default)
    {
        var v = await Db.StringGetAsync(Cur(sessionId));
        return v.IsNullOrEmpty ? null : v.ToString();
    }

    public Task<bool> IsValidAsync(Guid sessionId, string token, CancellationToken ct = default) =>
        Db.KeyExistsAsync(Tok(sessionId, token));

    public async Task RemoveAsync(Guid sessionId, CancellationToken ct = default)
    {
        var current = await GetCurrentAsync(sessionId, ct);
        if (current != null) await Db.KeyDeleteAsync(Tok(sessionId, current));
        await Db.KeyDeleteAsync(Cur(sessionId));
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        await Db.PingAsync();
        return true;
    }
}

/// <summary>Single-node fallback used in tests and when no Redis connection string is configured.</summary>
public class InMemoryQrTokenStore(TimeProvider time) : IQrTokenStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();
    private readonly ConcurrentDictionary<Guid, (string Token, DateTimeOffset Expires)> _current = new();

    public Task<string> RotateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var token = QrTokens.NewToken();
        var expires = time.GetUtcNow().Add(QrTokens.Ttl);
        _tokens[$"{sessionId}:{token}"] = expires;
        _current[sessionId] = (token, expires);
        Sweep();
        return Task.FromResult(token);
    }

    public Task<string?> GetCurrentAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (_current.TryGetValue(sessionId, out var c) && c.Expires > time.GetUtcNow()) return Task.FromResult<string?>(c.Token);
        return Task.FromResult<string?>(null);
    }

    public Task<bool> IsValidAsync(Guid sessionId, string token, CancellationToken ct = default) =>
        Task.FromResult(_tokens.TryGetValue($"{sessionId}:{token}", out var exp) && exp > time.GetUtcNow());

    public Task RemoveAsync(Guid sessionId, CancellationToken ct = default)
    {
        foreach (var k in _tokens.Keys.Where(k => k.StartsWith(sessionId.ToString()))) _tokens.TryRemove(k, out _);
        _current.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    public Task<bool> PingAsync(CancellationToken ct = default) => Task.FromResult(true);

    private void Sweep()
    {
        var now = time.GetUtcNow();
        foreach (var kv in _tokens.Where(kv => kv.Value <= now)) _tokens.TryRemove(kv.Key, out _);
    }
}
