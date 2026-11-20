using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// Official build 4ef164d6 is unattributed. These probes construct no caller, so tenant
/// enforcement denies them before a repository read. Do not inject a tenant or backfill
/// tenant_id to reach mix or accept.
/// </summary>
public sealed class ProductionOfficialSh02CreateMixV1LiveTests
{
    [Fact]
    public async Task Live_official_sh02_mix_compose_segments_once()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => OfficialSh02MixLiveV1.ExecuteOnceAsync());
        Assert.Equal(ProductionTenantBoundary.Required, ex.Message);
    }

    [Fact]
    public async Task Live_official_sh02_mix_replay_idempotency_does_not_compose()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => OfficialSh02MixLiveV1.ReplayIdempotencyAsync());
        Assert.Equal(ProductionTenantBoundary.Required, ex.Message);
    }
}
