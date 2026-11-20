using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// Official build 4ef164d6 is unattributed. These probes construct no caller, so tenant
/// enforcement denies them before a repository read. Do not inject a tenant or backfill
/// tenant_id to reach assemble.
/// </summary>
public sealed class ProductionOfficialSh02AssembleV1LiveTests
{
    [Fact]
    public async Task Live_official_sh02_assemble_remux_once()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => OfficialSh02AssembleLiveV1.ExecuteOnceAsync());
        Assert.Equal(ProductionTenantBoundary.Required, ex.Message);
    }

    [Fact]
    public async Task Live_official_sh02_assemble_replay_does_not_remux()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => OfficialSh02AssembleLiveV1.ReplayIdempotencyAsync());
        Assert.Equal(ProductionTenantBoundary.Required, ex.Message);
    }
}
