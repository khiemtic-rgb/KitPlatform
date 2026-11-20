using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class Ck20LiveVideoTakeAcceptanceTests
{
    [Fact]
    public async Task Live_official_video_take_acceptance_when_explicitly_armed()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CK20_LIVE"), "1", StringComparison.Ordinal))
            return;

        var cs = ContentLocalConnection.Require();
        var root = Environment.GetEnvironmentVariable("CK18B_VIDEO_ROOT")
            ?? @"E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v";
        OfficialVeoVideoTakeAcceptanceLiveResult? result = null;
        try
        {
            result = await OfficialVeoVideoTakeAcceptanceLiveV1.AcceptDevelopmentAsync(cs, root);
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "ck20-live-accept-result.txt"),
                "VERDICT=BLOCKED\nUNCAUGHT=" + ex.GetType().Name + "\nMESSAGE=" + ex.Message);
            throw;
        }

        var first = result.First;
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "ck20-live-accept-result.txt"),
            string.Join(Environment.NewLine, [
                "VERDICT=" + result.Verdict,
                "HANDOFF=" + result.Handoff,
                "ACCEPTED=" + first.Accepted,
                "REPLAYED=" + result.Second.Replayed,
                "OUTCOME=" + first.Outcome,
                "BLOCK=" + first.BlockReason,
                "ARTIFACT=" + first.Take?.ArtifactId,
                "SHA=" + first.Take?.ByteSha256,
                "BYTES=" + first.Take?.ByteLength,
                "MIME=" + first.Take?.MimeType,
                "VERIFY=" + first.Take?.VerificationStatus,
                "ACCEPT=" + first.Take?.AcceptanceStatus,
                "LOOKUP=" + result.Lookup?.ArtifactId,
                "START=" + result.StartCount,
                "POLL=" + result.PollCount,
                "TASK=" + first.Take?.TaskId,
                "ATTEMPT=" + first.Take?.AttemptId,
                "SNAPSHOT=" + first.Take?.SnapshotId,
                "OPERATION=" + first.Take?.OperationId,
            ]));
        Assert.Equal(0, result.StartCount);
        Assert.Equal(0, result.PollCount);
        Assert.True(first.Accepted || !string.IsNullOrWhiteSpace(first.BlockReason), result.Verdict);
    }
}
