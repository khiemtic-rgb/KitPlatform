using System.Text.Json;
using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class Ck18LiveVertexStartOnlyTests
{
    [Fact]
    public async Task Live_official_vertex_start_when_explicitly_armed()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("CK18_LIVE"), "1", StringComparison.Ordinal))
            return;

        var cs = ContentLocalConnection.Require();
        var root = Environment.GetEnvironmentVariable("CK18B_VIDEO_ROOT")
            ?? @"E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v";
        OfficialVeoRealVertexStartLiveResult? result = null;
        try
        {
            result = await OfficialVeoRealVertexStartLiveV1.StartDevelopmentAsync(cs, root);
        }
        catch (Exception ex)
        {
            File.WriteAllText(
                Path.Combine(Path.GetTempPath(), "ck18-live-start-result.txt"),
                "VERDICT=NOT_SENT — START NOT SENT\nUNCAUGHT=" + ex.GetType().Name + "\nMESSAGE=" + ex.Message);
            throw;
        }

        var start = result.Start;
        File.WriteAllText(
            Path.Combine(Path.GetTempPath(), "ck18-live-start-result.txt"),
            string.Join(Environment.NewLine, [
                "VERDICT=" + result.Verdict,
                "HANDOFF=" + result.Handoff,
                "CONTRACT_HASH=" + result.ContractAuthorizationHash,
                "CONTRACT_MATCH=" + result.ContractHashMatched,
                "STARTED=" + start.Started,
                "OUTCOME=" + start.Outcome,
                "BLOCK=" + start.BlockReason,
                "ATTEMPT=" + start.AttemptId,
                "REQ_HASH=" + start.RequestPayloadHash,
                "PROJECT=" + result.AuthoritativeProject,
                "LOCATION=" + result.Location,
                "START_URL=" + start.StartUrl,
                "POLL_URL=" + start.PollUrl,
                "OAUTH=" + result.OauthCount,
                "NETWORK=" + result.NetworkCount,
                "START_COUNT=" + start.StartAttemptCount,
                "JOB_COUNT=" + start.ProviderJobCount,
                "HTTP_STATUS=" + start.HttpStatus,
                "OPERATION=" + start.ProviderOperationName,
                "ENCODED=" + start.EncodedOperationId,
                "PERMIT_BEFORE=" + start.PermitStateBefore,
                "PERMIT_AFTER=" + start.PermitStateAfter,
                "OS_DUR=" + start.OsDurationSec,
                "WIRE_DUR=" + start.ProviderDurationSec,
                "RETRY=" + start.RetryCount,
                "REDACTION=" + start.RedactionCheck.Count,
                "AUTH_HASH=" + start.AuthorizationHash,
            ]));
        Assert.True(result.ContractHashMatched, "CK18B AUTH_HASH must match before Start");
        Assert.True(
            start.Started
            || start.Outcome is OfficialVeoStartOutcomes.Unknown
                or OfficialVeoStartOutcomes.NotSent
                or ProductionFailureReasons.InvalidProviderResponse
            || result.Verdict is "START_OUTCOME_UNKNOWN"
                or "NOT_SENT — START NOT SENT"
                or "INVALID_PROVIDER_RESPONSE"
                or "PERMIT_CONSUMPTION_PENDING",
            result.Verdict + " · " + start.BlockReason);
    }
}
