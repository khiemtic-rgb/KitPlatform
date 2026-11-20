using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class Ck19LivePollOnlyTests
{
    [Fact]
    public async Task Live_official_vertex_poll_when_explicitly_armed()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CK19_LIVE"), "1", StringComparison.Ordinal))
            return;

        var cs = ContentLocalConnection.Require();
        var root = Environment.GetEnvironmentVariable("CK18B_VIDEO_ROOT")
            ?? @"E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v";
        OfficialVeoPollRecoverMaterializationLiveResult? result = null;
        try
        {
            result = await OfficialVeoPollRecoverMaterializationLiveV1.PollDevelopmentAsync(cs, root);
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "ck19-live-poll-result.txt"),
                "VERDICT=BLOCKED\nUNCAUGHT=" + ex.GetType().Name + "\nMESSAGE=" + ex.Message);
            throw;
        }

        var first = result.First;
        var second = result.Second;
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "ck19-live-poll-result.txt"),
            string.Join(Environment.NewLine, [
                "VERDICT=" + result.Verdict,
                "HANDOFF=" + result.Handoff,
                "FIRST_OUTCOME=" + first.Outcome,
                "FIRST_STATE=" + first.ExecutionState,
                "FIRST_BLOCK=" + first.BlockReason,
                "FIRST_SUCCEEDED=" + first.Succeeded,
                "REPLAYED=" + second.Replayed,
                "ATTEMPT=" + first.AttemptId,
                "TASK=" + first.TaskId,
                "SNAPSHOT=" + first.SnapshotId,
                "SNAPSHOT_HASH=" + first.SnapshotHash,
                "PERMIT=" + first.PermitId,
                "AUTH_HASH=" + first.AuthorizationHash,
                "OPERATION=" + first.ProviderOperationName,
                "ENCODED=" + first.EncodedOperationId,
                "POLL_URL=" + first.PollUrl,
                "POLL_COUNT=" + first.PollCount,
                "START_COUNT=" + first.StartCount,
                "JOB_COUNT=" + first.ProviderJobCount,
                "HTTP=" + string.Join(",", first.PollHttpStatuses),
                "TERMINAL=" + first.ProviderTerminalStatus,
                "ARTIFACT=" + first.ArtifactId,
                "SHA=" + first.ArtifactSha256,
                "BYTES=" + first.ArtifactBytes,
                "MIME=" + first.MimeType,
                "SECOND_ARTIFACT=" + second.ArtifactId,
                "OAUTH=" + result.OauthCount,
                "NETWORK=" + result.NetworkCount,
                "PROJECT=" + result.AuthoritativeProject,
                "LOCATION=" + result.Location,
                "REDACTION=" + first.RedactionCheck.Count,
            ]));
        Assert.True(first.StartCount == 0 && second.StartCount == 0, "CK19 must not Start");
        Assert.True(
            first.Succeeded
            || first.Outcome is OfficialVeoPollStates.PollTimeout
                or OfficialVeoPollStates.ProviderFailed
                or ProductionFailureReasons.ProviderOutputMissing
                or ProductionFailureReasons.ProviderOutputUnsupported
                or ProductionFailureReasons.InvalidProviderResponse,
            result.Verdict + " · " + first.BlockReason);
    }
}
