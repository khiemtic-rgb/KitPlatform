using Microsoft.Extensions.Caching.Memory;

namespace KitPlatform.Api.Authorization;

/// <summary>
/// In-memory failed-login throttle. Counters are per API process; a multi-instance deploy needs a shared store.
/// </summary>
public sealed class LoginAttemptGuard
{
    public const int MaxFailuresPerAccountAndIp = 5;
    public const int MaxFailuresPerAccount = 20;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    private const string AllIps = "all";

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    public LoginAttemptGuard(IMemoryCache cache, TimeProvider? time = null)
    {
        _cache = cache;
        _time = time ?? TimeProvider.System;
    }

    public static string AccountKey(string? tenantCode, string? username) =>
        $"{(tenantCode ?? "").Trim().ToUpperInvariant()}|{(username ?? "").Trim().ToLowerInvariant()}";

    /// <summary>Remaining lock time, or null when the caller may try.</summary>
    public TimeSpan? GetLockRemaining(string accountKey, string? ip)
    {
        var now = _time.GetUtcNow();
        var perIp = Remaining(LockKey(accountKey, IpScope(ip)), now);
        var perAccount = Remaining(LockKey(accountKey, AllIps), now);
        if (perIp is null)
            return perAccount;
        if (perAccount is null)
            return perIp;
        return perIp > perAccount ? perIp : perAccount;
    }

    public void RecordFailure(string accountKey, string? ip)
    {
        var now = _time.GetUtcNow();
        lock (_gate)
        {
            if (Increment(accountKey, IpScope(ip), now) >= MaxFailuresPerAccountAndIp)
                Lock(accountKey, IpScope(ip), now);
            if (Increment(accountKey, AllIps, now) >= MaxFailuresPerAccount)
                Lock(accountKey, AllIps, now);
        }
    }

    public void RecordSuccess(string accountKey, string? ip)
    {
        lock (_gate)
        {
            _cache.Remove(FailKey(accountKey, IpScope(ip)));
            _cache.Remove(LockKey(accountKey, IpScope(ip)));
        }
    }

    private TimeSpan? Remaining(string lockKey, DateTimeOffset now)
    {
        if (!_cache.TryGetValue(lockKey, out DateTimeOffset until))
            return null;
        var left = until - now;
        return left > TimeSpan.Zero ? left : null;
    }

    private int Increment(string accountKey, string scope, DateTimeOffset now)
    {
        var key = FailKey(accountKey, scope);
        var entry = _cache.TryGetValue(key, out FailureWindow? existing) && existing is not null && existing.ExpiresAt > now
            ? existing with { Count = existing.Count + 1 }
            : new FailureWindow(1, now + Window);
        _cache.Set(key, entry, entry.ExpiresAt);
        return entry.Count;
    }

    private void Lock(string accountKey, string scope, DateTimeOffset now)
    {
        var until = now + LockDuration;
        _cache.Set(LockKey(accountKey, scope), until, until);
        _cache.Remove(FailKey(accountKey, scope));
    }

    private static string IpScope(string? ip) => "ip:" + (string.IsNullOrWhiteSpace(ip) ? "unknown" : ip);

    private static string FailKey(string accountKey, string scope) => $"login-fail:{accountKey}:{scope}";

    private static string LockKey(string accountKey, string scope) => $"login-lock:{accountKey}:{scope}";

    private sealed record FailureWindow(int Count, DateTimeOffset ExpiresAt);
}
