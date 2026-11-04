using Microsoft.Extensions.Caching.Memory;
using KitPlatform.Api.Authorization;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class LoginAttemptGuardTests
{
    private sealed class ManualTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static (LoginAttemptGuard Guard, ManualTime Time) Create()
    {
        var time = new ManualTime(DateTimeOffset.UtcNow);
        return (new LoginAttemptGuard(new MemoryCache(new MemoryCacheOptions()), time), time);
    }

    [Fact]
    public void Locks_account_and_ip_after_five_failures()
    {
        var (guard, _) = Create();
        var key = LoginAttemptGuard.AccountKey("DEMO_PHARMACY", "admin");
        for (var i = 0; i < LoginAttemptGuard.MaxFailuresPerAccountAndIp - 1; i++)
            guard.RecordFailure(key, "1.1.1.1");

        Assert.Null(guard.GetLockRemaining(key, "1.1.1.1"));
        guard.RecordFailure(key, "1.1.1.1");
        Assert.NotNull(guard.GetLockRemaining(key, "1.1.1.1"));
        Assert.Null(guard.GetLockRemaining(key, "2.2.2.2"));
    }

    [Fact]
    public void Lock_expires_after_lock_duration()
    {
        var (guard, time) = Create();
        var key = LoginAttemptGuard.AccountKey("DEMO_PHARMACY", "admin");
        for (var i = 0; i < LoginAttemptGuard.MaxFailuresPerAccountAndIp; i++)
            guard.RecordFailure(key, "1.1.1.1");

        time.Advance(LoginAttemptGuard.LockDuration + TimeSpan.FromSeconds(1));
        Assert.Null(guard.GetLockRemaining(key, "1.1.1.1"));
    }

    [Fact]
    public void Distributed_failures_lock_the_account_for_every_ip()
    {
        var (guard, _) = Create();
        var key = LoginAttemptGuard.AccountKey("NT_A", "admin");
        for (var i = 0; i < LoginAttemptGuard.MaxFailuresPerAccount; i++)
            guard.RecordFailure(key, $"10.0.0.{i}");

        Assert.NotNull(guard.GetLockRemaining(key, "10.9.9.9"));
    }

    [Fact]
    public void Success_clears_the_ip_counter()
    {
        var (guard, _) = Create();
        var key = LoginAttemptGuard.AccountKey("NT_A", "Admin ");
        for (var i = 0; i < LoginAttemptGuard.MaxFailuresPerAccountAndIp - 1; i++)
            guard.RecordFailure(key, "1.1.1.1");
        guard.RecordSuccess(key, "1.1.1.1");
        guard.RecordFailure(key, "1.1.1.1");

        Assert.Null(guard.GetLockRemaining(key, "1.1.1.1"));
        Assert.Equal(key, LoginAttemptGuard.AccountKey("nt_a", "admin"));
    }

    [Fact]
    public void Missing_ip_does_not_double_count_into_the_account_scope()
    {
        var (guard, _) = Create();
        var key = LoginAttemptGuard.AccountKey("NT_A", "admin");
        for (var i = 0; i < LoginAttemptGuard.MaxFailuresPerAccountAndIp - 1; i++)
            guard.RecordFailure(key, null);

        Assert.Null(guard.GetLockRemaining(key, null));
    }
}
