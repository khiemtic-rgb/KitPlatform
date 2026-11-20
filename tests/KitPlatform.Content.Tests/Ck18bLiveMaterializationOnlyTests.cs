using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class Ck18bLiveMaterializationOnlyTests
{
    [Fact]
    public async Task Live_official_contract_materialization_when_explicitly_armed()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CK18B_LIVE"), "1", StringComparison.Ordinal))
            return;

        var cs = ContentLocalConnection.Require();
        var root = Environment.GetEnvironmentVariable("CK18B_VIDEO_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
                "src", "KitPlatform.Api", "App_Data", "kit-video-i2v"));
        var first = await OfficialVeoLiveOfficialContractMaterializationLiveV1.MaterializeDevelopmentAsync(cs, root);
        Assert.True(first.Materialized, first.Verdict + " · " + first.BlockReason);
        Assert.Equal(OfficialVeoLiveOfficialContractMaterializationV1.VerdictPass, first.Verdict);
        Assert.Equal(OfficialVeoLiveOfficialContractMaterializationV1.ReadyForCk18RealStart, first.Handoff);
        var second = await OfficialVeoLiveOfficialContractMaterializationLiveV1.MaterializeDevelopmentAsync(cs, root);
        Assert.True(second.SnapshotReused && second.PermitReused);
        Assert.Equal(first.SnapshotId, second.SnapshotId);
        Assert.Equal(first.PermitId, second.PermitId);
        Assert.Equal(first.AuthorizationHash, second.AuthorizationHash);
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "ck18b-live-result.txt"),
            string.Join(Environment.NewLine, [
                "SNAPSHOT_ID=" + first.SnapshotId,
                "SNAPSHOT_HASH=" + first.SnapshotHash,
                "PERMIT_ID=" + first.PermitId,
                "PERMIT_HASH=" + first.PermitHash,
                "AUTH_HASH=" + first.AuthorizationHash,
                "AUTH_STATE=" + first.AuthorizationState,
                "VERDICT=" + first.Verdict,
                "HANDOFF=" + first.Handoff,
                "FIELDS=" + string.Join(",", first.FrozenFieldResults),
                "REUSED=" + second.SnapshotReused + "/" + second.PermitReused,
                "SNAP_CREATED=" + first.SnapshotCreated + " REUSED=" + first.SnapshotReused,
                "PERMIT_CREATED=" + first.PermitCreated + " REUSED=" + first.PermitReused,
            ]));
    }
}
