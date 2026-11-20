using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class Ck21aLiveOfficialVideoAuthorityTests
{
    [Fact]
    public async Task Live_official_video_authority_bridge_is_read_only_when_armed()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CK21A_LIVE"), "1", StringComparison.Ordinal))
            return;

        var cs = ContentLocalConnection.Require();
        OfficialVideoAuthorityBridgeLiveResult? result = null;
        try
        {
            result = await OfficialVideoAuthorityBridgeLiveV1.ReadDevelopmentAsync(cs);
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "ck21a-live-authority-result.txt"),
                "VERDICT=BLOCKED\nUNCAUGHT=" + ex.GetType().Name + "\nMESSAGE=" + ex.Message);
            throw;
        }

        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "ck21a-live-authority-result.txt"),
            string.Join(Environment.NewLine, [
                "VERDICT=" + result.Verdict,
                "HANDOFF=" + result.Handoff,
                "BUILD=" + RealVertexExecutionV1Lock.BuildId,
                "SHOT=" + RealVertexExecutionV1Lock.ShotCode,
                "TAKE=" + result.Take?.ArtifactId,
                "ROLE=" + result.Take?.ArtifactRole,
                "ACCEPT=" + result.Take?.AcceptanceStatus,
                "SHA=" + result.Take?.ByteSha256,
                "BYTES=" + result.Take?.ByteLength,
                "MIME=" + result.Take?.MimeType,
                "PROVIDER=" + result.Take?.ProviderId,
                "MODEL=" + result.Take?.ModelId,
                "MOTION=" + result.Motion.Hit?.ArtifactId,
                "MOTION_ROLE=" + result.Motion.Hit?.BindingRole,
                "PREVIEW=" + result.Preview.ArtifactId,
                "PREVIEW_ROLE=" + result.Preview.Role,
                "VIDEO=" + result.State.VideoArtifactId,
                "ACCEPTED=" + result.State.AcceptedArtifactId,
                "CURRENT=" + result.State.CurrentArtifactId,
                "ARTS=" + result.ArtifactCount,
                "BINDINGS=" + string.Join(";", result.BindingRoles),
            ]));
        Assert.Equal(OfficialVideoAuthorityBridgeLiveV1.Ck21aPass, result.Handoff);
        Assert.Equal(OfficialVideoAuthorityBridgeLiveV1.ExpectedArtifactId, result.Take?.ArtifactId);
        Assert.NotEqual(OfficialVideoAuthorityBridgeLiveV1.KeyframeId, result.Preview.ArtifactId);
        Assert.NotEqual(OfficialVideoAuthorityBridgeLiveV1.KeyframeId, result.State.VideoArtifactId);
    }
}
