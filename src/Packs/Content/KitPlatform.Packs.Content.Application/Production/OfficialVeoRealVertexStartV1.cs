using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoStartRequest(
    OfficialVeoExecutionAuthorizationRequest AuthorizationRequest,
    OfficialVeoExecutionAuthorizationResult ExpectedAuthorization,
    IProductionWorkStore Store,
    IProductionExecutionPermitStore Permits,
    OfficialVeoStartLedger Ledger,
    IVeoMotionTransport Transport,
    IVeoHttpAuthenticator Authenticator);

public sealed record OfficialVeoStartProvenance(
    Guid AttemptId,
    string AuthorizationHash,
    Guid BuildId,
    string ShotId,
    string Action,
    string ProviderId,
    string ModelId,
    Guid SelectionId,
    Guid SnapshotId,
    string SnapshotHash,
    Guid PermitId,
    string PermitHash,
    string CostVariant,
    decimal? CostAmount,
    string? CostCurrency,
    string? CostUnit,
    decimal OsDurationSec,
    int ProviderDurationSec,
    Guid? KeyframeArtifactId,
    string KeyframeHash,
    string StartUrl,
    string PollUrl,
    string VertexModel,
    string Project,
    string Location,
    string RequestPayloadHash,
    DateTimeOffset Timestamp,
    string ExecutionState);

public sealed record OfficialVeoStartResult(
    bool Started,
    bool Replayed,
    string Outcome,
    string? BlockReason,
    string AuthorizationHash,
    Guid? AttemptId,
    Guid PermitId,
    string PermitStateBefore,
    string PermitStateAfter,
    string ProviderId,
    string ModelId,
    string VertexModel,
    string? Project,
    string? Location,
    string? StartUrl,
    string? PollUrl,
    string? RequestPayloadHash,
    decimal OsDurationSec,
    int ProviderDurationSec,
    string CostVariant,
    decimal? CostAmount,
    string? AuthenticatorResult,
    string? OauthResult,
    int StartAttemptCount,
    int ProviderJobCount,
    string? ProviderOperationName,
    string? EncodedOperationId,
    int? HttpStatus,
    int RetryCount,
    int AutomaticRetryConfiguration,
    OfficialVeoStartProvenance? Provenance,
    IReadOnlyList<string> RedactionCheck);

/// <summary>
/// Official CREATE_VIDEO one-shot Vertex Start. CK18.
/// Preflight has zero side effects. Exactly one predictLongRunning. No automatic retry.
/// </summary>
public static class OfficialVeoRealVertexStartV1
{
    public const int AutomaticRetryCount = 0;
    public const string CostVariantId = "VIDEO_ONLY/720p/audio=false/sampleCount=1";

    public static async Task<OfficialVeoStartResult> StartAsync(
        OfficialVeoStartRequest request,
        CancellationToken ct = default)
    {
        var permitBefore = request.AuthorizationRequest.Permit;
        var permitStateBefore = permitBefore is null
            ? "MISSING"
            : permitBefore.ConsumedAt is null
                ? permitBefore.Status
                : ProductionExecutionPermitStatuses.Consumed;

        OfficialVeoExecutionAuthorizationResult authz;
        try
        {
            authz = OfficialVeoExecutionAuthorizationV1.Evaluate(
                request.AuthorizationRequest with { Existing = request.ExpectedAuthorization });
        }
        catch (InvalidOperationException ex)
        {
            return Blocked(request, permitStateBefore, permitStateBefore, ProductionFailureReasons.ExecutionAuthorizationRequired, CodeOf(ex.Message));
        }

        if (!authz.Authorized
            || request.ExpectedAuthorization is not { Authorized: true }
            || !string.Equals(authz.AuthorizationHash, request.ExpectedAuthorization.AuthorizationHash, StringComparison.Ordinal))
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                ProductionFailureReasons.ExecutionAuthorizationRequired,
                authz.BlockReason ?? ProductionFailureReasons.ExecutionAuthorizationRequired,
                authz.AuthorizationHash);
        }

        var frozen = request.AuthorizationRequest.Frozen;
        var live = request.AuthorizationRequest.Live;
        var permit = request.AuthorizationRequest.Permit;
        var http = authz.HttpComposition;
        var bytes = request.AuthorizationRequest.KeyframeBytes;
        if (frozen is null || live is null || permit is null || http is not { Ready: true, TransportRequest: not null, VertexRequestJson: not null })
            return Blocked(request, permitStateBefore, permitStateBefore, ProductionFailureReasons.ExecutionAuthorizationRequired, authz.BlockReason);

        if (permit.ConsumedAt is not null
            || !string.Equals(permit.Status, ProductionExecutionPermitStatuses.Issued, StringComparison.Ordinal))
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                PerformanceContractIds.OfficialFilmForbidden,
                PerformanceContractIds.OfficialFilmForbidden,
                authz.AuthorizationHash);
        }

        try
        {
            ProductionFrozenCreateVideoMatch.Assert(
                frozen,
                live,
                new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()));
        }
        catch (InvalidOperationException ex)
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                ProductionFailureReasons.InputSnapshotMismatch,
                CodeOf(ex.Message),
                authz.AuthorizationHash);
        }

        if (bytes is null || bytes.Length < 32
            || !string.Equals(frozen.KeyframeHash, KitVideoIntegrityRules.Sha256Hex(bytes), StringComparison.OrdinalIgnoreCase)
            || frozen.DurationSec != OfficialCreateVideoFreezeV1.OfficialDurationSec
            || http.TransportRequest.ProviderDurationSec != 8
            || http.TransportRequest.OsDurationSec != 10)
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                ProductionFailureReasons.InputSnapshotMismatch,
                ProductionFailureReasons.InputSnapshotMismatch,
                authz.AuthorizationHash);
        }

        if (request.Transport is not HttpVeoMotionTransport)
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                ProductionFailureReasons.UnknownProvider,
                ProductionFailureReasons.UnknownProvider,
                authz.AuthorizationHash);
        }

        var body = http.VertexRequestJson;
        if (!IsExactCk16Body(body, frozen, bytes))
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                ProductionFailureReasons.InvalidProviderResponse,
                ProductionFailureReasons.InvalidProviderResponse,
                authz.AuthorizationHash);
        }

        var payloadHash = PayloadHash(body);
        var claim = request.Ledger.TryClaim(authz.AuthorizationHash, permit.PermitId, payloadHash);
        if (claim.Replay)
            return ReplayOf(request, authz, claim.Claim, permitStateBefore, payloadHash);
        if (!claim.Won)
        {
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                claim.BlockReason ?? ProductionFailureReasons.DuplicateProviderStart,
                claim.BlockReason ?? ProductionFailureReasons.DuplicateProviderStart,
                authz.AuthorizationHash);
        }

        var engine = new ProductionWorkExecutionEngine(request.Store);
        ProductionAttempt attempt;
        try
        {
            attempt = await engine.StartAttemptAsync(frozen.TaskId, AttemptColumnHash(payloadHash), ct);
        }
        catch (Exception)
        {
            request.Ledger.Complete(
                authz.AuthorizationHash,
                permit.PermitId,
                OfficialVeoStartOutcomes.NotSent,
                null,
                null,
                null,
                ProductionFailureReasons.StartNotSent);
            return Blocked(
                request,
                permitStateBefore,
                permitStateBefore,
                OfficialVeoStartOutcomes.NotSent,
                ProductionFailureReasons.StartNotSent,
                authz.AuthorizationHash,
                payloadHash: payloadHash);
        }
        request.Ledger.BindAttempt(authz.AuthorizationHash, permit.PermitId, attempt.AttemptId);

        var provenance = ProvenanceOf(authz, frozen, permit, http, attempt.AttemptId, payloadHash, OfficialVeoStartOutcomes.InFlight);
        await request.Store.InsertEventAsync(new ProductionTaskEvent(
            Guid.NewGuid(),
            ProductionEventTypes.AttemptStarted,
            DateTimeOffset.UtcNow,
            null,
            frozen.TaskId,
            attempt.AttemptId,
            null,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RedactedJson(provenance)), ct);

        var httpBefore = request.Transport.HttpCallCount;
        string outcome;
        string? operation = null;
        string? encoded = null;
        int? httpStatus = null;
        string? error = null;
        var oauth = "NOT_CALLED";
        try
        {
            if (request.Authenticator is FakeVeoHttpAuthenticator)
                oauth = "FAKE";
            var created = await request.Transport.CreateImageToVideoAsync(http.TransportRequest, ct);
            encoded = created.ProviderJobId;
            operation = SafeDecode(created.ProviderJobId);
            httpStatus = 200;
            outcome = OfficialVeoStartOutcomes.Started;
            if (request.Authenticator is FakeVeoHttpAuthenticator fake)
                oauth = fake.Calls > 0 ? "FAKE_ACQUIRED" : "FAKE";
            else if (request.Authenticator is ProductionVeoHttpAuthenticator)
                oauth = "PRODUCTION_CLOSED";
            await engine.SubmitProviderJobAsync(attempt.AttemptId, created.ProviderJobId, ct);
        }
        catch (InvalidOperationException ex)
        {
            error = CodeOf(ex.Message);
            httpStatus = StatusOf(ex.Message);
            outcome = Classify(ex, request.Transport.HttpCallCount, httpBefore);
        }
        catch (HttpRequestException)
        {
            error = FamixaProviderErrorCodes.ProviderError;
            outcome = request.Transport.HttpCallCount > httpBefore
                ? OfficialVeoStartOutcomes.Unknown
                : OfficialVeoStartOutcomes.NotSent;
        }
        catch (TaskCanceledException)
        {
            error = FamixaProviderErrorCodes.Timeout;
            outcome = request.Transport.HttpCallCount > httpBefore
                ? OfficialVeoStartOutcomes.Unknown
                : OfficialVeoStartOutcomes.NotSent;
        }

        var attempted = request.Transport.HttpCallCount > httpBefore;
        var permitStateAfter = permitStateBefore;
        if (attempted)
        {
            var consumed = await request.Permits.TryConsumeAsync(
                RealVertexExecutionV1Lock.BuildId,
                RealVertexExecutionV1Lock.ShotCode,
                ProductionActions.CreateVideo,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                ct);
            permitStateAfter = consumed
                ? ProductionExecutionPermitStatuses.Consumed
                : permitStateBefore;
            if (!consumed && outcome == OfficialVeoStartOutcomes.Started)
            {
                request.Ledger.Complete(
                    authz.AuthorizationHash, permit.PermitId, OfficialVeoStartOutcomes.Failed,
                    operation, encoded, httpStatus, ProductionFailureReasons.PermitConflict);
                return Blocked(
                    request,
                    permitStateBefore,
                    permitStateAfter,
                    ProductionFailureReasons.PermitConflict,
                    ProductionFailureReasons.PermitConflict,
                    authz.AuthorizationHash,
                    attempt.AttemptId,
                    payloadHash);
            }
        }

        if (outcome == OfficialVeoStartOutcomes.Unknown)
            error ??= ProductionFailureReasons.StartOutcomeUnknown;

        var completed = request.Ledger.Complete(
            authz.AuthorizationHash,
            permit.PermitId,
            outcome == OfficialVeoStartOutcomes.Started ? OfficialVeoStartOutcomes.Running : outcome,
            operation,
            encoded,
            httpStatus,
            error);

        if (outcome != OfficialVeoStartOutcomes.Started)
        {
            await engine.RecordProviderFailedAsync(
                attempt.AttemptId,
                error ?? ProductionFailureReasons.StartOutcomeUnknown,
                ct);
        }

        var finalProv = provenance with { ExecutionState = completed.Outcome, AttemptId = attempt.AttemptId };
        return ResultOf(
            started: outcome == OfficialVeoStartOutcomes.Started,
            replayed: false,
            outcome: completed.Outcome,
            block: outcome == OfficialVeoStartOutcomes.Started ? null : (error ?? outcome),
            authz,
            http,
            frozen,
            permit,
            permitStateBefore,
            permitStateAfter,
            payloadHash,
            attempt.AttemptId,
            completed,
            oauth,
            startCount: attempted ? 1 : 0,
            jobCount: outcome == OfficialVeoStartOutcomes.Started ? 1 : 0,
            finalProv);
    }

    static OfficialVeoStartResult ReplayOf(
        OfficialVeoStartRequest request,
        OfficialVeoExecutionAuthorizationResult authz,
        OfficialVeoStartClaim claim,
        string permitStateBefore,
        string payloadHash)
    {
        var http = authz.HttpComposition!;
        var frozen = request.AuthorizationRequest.Frozen!;
        var permit = request.AuthorizationRequest.Permit!;
        var after = permit.ConsumedAt is null && permit.Status == ProductionExecutionPermitStatuses.Issued
            ? permitStateBefore
            : ProductionExecutionPermitStatuses.Consumed;
        return ResultOf(
            started: false,
            replayed: true,
            outcome: OfficialVeoStartOutcomes.Replay,
            block: ProductionFailureReasons.StartReplay,
            authz,
            http,
            frozen,
            permit,
            permitStateBefore,
            after,
            payloadHash,
            claim.AttemptId == Guid.Empty ? null : claim.AttemptId,
            claim,
            "NOT_CALLED",
            startCount: 0,
            jobCount: 0,
            ProvenanceOf(authz, frozen, permit, http, claim.AttemptId, payloadHash, OfficialVeoStartOutcomes.Replay));
    }

    static OfficialVeoStartResult Blocked(
        OfficialVeoStartRequest request,
        string before,
        string after,
        string state,
        string? reason,
        string? hash = null,
        Guid? attemptId = null,
        string? payloadHash = null) =>
        new(
            false,
            false,
            state,
            reason,
            hash ?? request.ExpectedAuthorization.AuthorizationHash,
            attemptId,
            request.AuthorizationRequest.Permit?.PermitId ?? Guid.Empty,
            before,
            after,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            VeoVertexEndpoints.VertexModel,
            request.ExpectedAuthorization.HttpComposition?.Settings?.ProjectId,
            request.ExpectedAuthorization.HttpComposition?.Settings?.Location,
            request.ExpectedAuthorization.HttpComposition?.StartUrl,
            request.ExpectedAuthorization.HttpComposition?.PollUrl,
            payloadHash,
            request.AuthorizationRequest.Frozen?.DurationSec ?? 0,
            request.ExpectedAuthorization.HttpComposition?.TransportRequest?.ProviderDurationSec ?? 0,
            CostVariantId,
            FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd,
            request.ExpectedAuthorization.Authenticator?.State,
            "NOT_CALLED",
            0,
            0,
            null,
            null,
            null,
            AutomaticRetryCount,
            ProductionVeoHttpClientPolicy.StartAutomaticRetryCount,
            null,
            RedactionCheck(null));

    static OfficialVeoStartResult ResultOf(
        bool started,
        bool replayed,
        string outcome,
        string? block,
        OfficialVeoExecutionAuthorizationResult authz,
        OfficialVeoHttpCompositionResult http,
        ProductionTaskInputSnapshot frozen,
        ProductionExecutionPermit permit,
        string before,
        string after,
        string payloadHash,
        Guid? attemptId,
        OfficialVeoStartClaim claim,
        string oauth,
        int startCount,
        int jobCount,
        OfficialVeoStartProvenance provenance) =>
        new(
            started,
            replayed,
            outcome,
            block,
            authz.AuthorizationHash,
            attemptId,
            permit.PermitId,
            before,
            after,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            VeoVertexEndpoints.VertexModel,
            http.Settings?.ProjectId,
            http.Settings?.Location,
            http.StartUrl,
            http.PollUrl,
            payloadHash,
            frozen.DurationSec,
            http.TransportRequest?.ProviderDurationSec ?? 0,
            CostVariantId,
            FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd,
            authz.Authenticator?.State,
            oauth,
            startCount,
            jobCount,
            claim.ProviderOperationName,
            claim.EncodedOperationId,
            claim.HttpStatus,
            AutomaticRetryCount,
            ProductionVeoHttpClientPolicy.StartAutomaticRetryCount,
            provenance,
            RedactionCheck(RedactedJson(provenance)));

    static OfficialVeoStartProvenance ProvenanceOf(
        OfficialVeoExecutionAuthorizationResult authz,
        ProductionTaskInputSnapshot frozen,
        ProductionExecutionPermit permit,
        OfficialVeoHttpCompositionResult http,
        Guid attemptId,
        string payloadHash,
        string state) =>
        new(
            attemptId,
            authz.AuthorizationHash,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId,
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            frozen.SnapshotId,
            frozen.SnapshotHash,
            permit.PermitId,
            permit.PermitHash,
            CostVariantId,
            FamixaVeo31OfficialPricing.VideoOnly720Or1080Usd,
            FamixaVeo31OfficialPricing.Currency,
            FamixaVeo31OfficialPricing.Unit,
            frozen.DurationSec,
            http.TransportRequest?.ProviderDurationSec ?? 8,
            frozen.KeyframeArtifactId,
            frozen.KeyframeHash,
            http.StartUrl ?? "",
            http.PollUrl ?? "",
            VeoVertexEndpoints.VertexModel,
            http.Settings?.ProjectId ?? "",
            http.Settings?.Location ?? "",
            payloadHash,
            DateTimeOffset.UtcNow,
            state);

    static bool IsExactCk16Body(string body, ProductionTaskInputSnapshot frozen, byte[] bytes)
    {
        if (body.Contains("seed", StringComparison.OrdinalIgnoreCase)
            || body.Contains("lastFrame", StringComparison.OrdinalIgnoreCase)
            || body.Contains("gs://", StringComparison.Ordinal)
            || body.Contains("gcsUri", StringComparison.OrdinalIgnoreCase)
            || body.Contains("http://", StringComparison.OrdinalIgnoreCase)
            || body.Contains("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        using var doc = JsonDocument.Parse(body);
        var inst = doc.RootElement.GetProperty("instances")[0];
        var p = doc.RootElement.GetProperty("parameters");
        var img = inst.GetProperty("image");
        var wire = Convert.FromBase64String(img.GetProperty("bytesBase64Encoded").GetString()!);
        return inst.GetProperty("prompt").GetString() == VeoMotionPrompt.FromFrozen(frozen)
            && wire.SequenceEqual(bytes)
            && img.GetProperty("mimeType").GetString() == "image/png"
            && p.GetProperty("sampleCount").GetInt32() == 1
            && p.GetProperty("aspectRatio").GetString() == "16:9"
            && p.GetProperty("durationSeconds").GetInt32() == 8
            && p.GetProperty("resolution").GetString() == "720p"
            && p.GetProperty("generateAudio").GetBoolean() == false;
    }

    static string PayloadHash(string body) =>
        "REQ_HASH_V1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant();

    /// <summary>
    /// production_attempts.request_payload_hash is VARCHAR(64). Persist the SHA-256 hex only.
    /// Ledger / provenance keep the REQ_HASH_V1: prefix.
    /// </summary>
    static string AttemptColumnHash(string payloadHash)
    {
        const string prefix = "REQ_HASH_V1:";
        if (payloadHash.StartsWith(prefix, StringComparison.Ordinal))
            return payloadHash[prefix.Length..];
        return payloadHash.Length <= 64 ? payloadHash : payloadHash[^64..];
    }

    static string Classify(InvalidOperationException ex, int httpAfter, int httpBefore)
    {
        if (httpAfter <= httpBefore)
            return OfficialVeoStartOutcomes.NotSent;
        var code = CodeOf(ex.Message);
        if (code == FamixaProviderErrorCodes.Timeout)
            return OfficialVeoStartOutcomes.Unknown;
        return OfficialVeoStartOutcomes.Failed;
    }

    static string? SafeDecode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded)) return null;
        try { return VeoVertexOperationId.Decode(encoded); }
        catch { return null; }
    }

    static int? StatusOf(string message)
    {
        const string marker = "http=";
        var i = message.IndexOf(marker, StringComparison.Ordinal);
        if (i < 0) return null;
        var raw = message[(i + marker.Length)..].Split(' ', '·')[0];
        return int.TryParse(raw, out var n) ? n : null;
    }

    static string RedactedJson(OfficialVeoStartProvenance provenance)
    {
        var json = JsonSerializer.Serialize(provenance);
        return json
            .Replace("Bearer", "REDACTED", StringComparison.OrdinalIgnoreCase)
            .Replace("private_key", "REDACTED", StringComparison.OrdinalIgnoreCase)
            .Replace("access_token", "REDACTED", StringComparison.OrdinalIgnoreCase)
            .Replace("BEGIN PRIVATE KEY", "REDACTED", StringComparison.OrdinalIgnoreCase);
    }

    static IReadOnlyList<string> RedactionCheck(string? json)
    {
        var raw = json ?? "";
        var hits = new List<string>();
        if (raw.Contains("Bearer ", StringComparison.OrdinalIgnoreCase)) hits.Add("authorization-header");
        if (raw.Contains("BEGIN PRIVATE", StringComparison.OrdinalIgnoreCase)) hits.Add("private-key");
        if (raw.Contains("access_token", StringComparison.OrdinalIgnoreCase)) hits.Add("access-token");
        if (raw.Contains("private_key", StringComparison.OrdinalIgnoreCase)) hits.Add("private_key");
        return hits;
    }

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
