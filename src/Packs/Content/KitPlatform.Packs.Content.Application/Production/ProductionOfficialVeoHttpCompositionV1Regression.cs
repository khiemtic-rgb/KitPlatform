using System.Net;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialVeoHttpCompositionV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_VEO_HTTP_COMPOSITION_V1";
    public const string ComposeProject = "ck16-compose-project";
    const string OperationName = "projects/ck16-compose-project/locations/us-central1/operations/op-ck16";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        await T_OfficialComposeReady(Ok);
        await T_EndpointAndModelMapping(Ok);
        await T_RequestContract(Ok);
        await T_DurationMapping(Ok);
        await T_KeyframeIntegrity(Ok);
        await T_WrongBuild(Ok);
        await T_WrongShot(Ok);
        await T_WrongProvider(Ok);
        await T_WrongModel(Ok);
        await T_WrongSelection(Ok);
        await T_FixtureRunwayUnselected(Ok);
        await T_MissingFrozen(Ok);
        await T_SnapshotMismatch(Ok);
        await T_WrongKeyframe(Ok);
        await T_WrongCostVariant(Ok);
        await T_LifecycleOffline(Ok);
        await T_OutputPolicy(Ok);
        await T_NoNetworkNoMutation(Ok);
        await T_PreflightRemovesHttpCompositionUnavailable(Ok);
        T_ProductionResolverClosed(Ok);
        T_Ck15StillGreen(Ok);
    }

    static async Task T_OfficialComposeReady(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        Ok(composed.Ready && composed.State == ProductionFailureReasons.HttpCompositionReady,
            "official google/veo-3.1 HTTP composition READY");
        Ok(composed.TransportRequest is not null && composed.Settings is not null
            && composed.VertexRequestJson is not null,
            "composed request / settings / JSON present");
        Ok(composed.TransportRequest!.ProviderId == RealVertexExecutionV1Lock.ProviderId
            && composed.TransportRequest.ModelId == RealVertexExecutionV1Lock.ModelId,
            "transport identity google / veo-3.1");
    }

    static async Task T_EndpointAndModelMapping(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        var settings = composed.Settings!;
        Ok(settings.ProjectId == ComposeProject
            && settings.Location == VeoHttpConfigPrecedence.DefaultLocation
            && settings.VertexModel == VeoVertexEndpoints.VertexModel
            && VeoVertexEndpoints.VertexModel == "veo-3.1-generate-001"
            && VeoVertexEndpoints.Publisher == "google"
            && VeoVertexEndpoints.ApiVersion == "v1",
            "veo-3.1 → veo-3.1-generate-001 / Vertex v1 / publisher google / us-central1");
        Ok(composed.StartUrl == VeoVertexEndpoints.PredictLongRunning(settings)
            && composed.PollUrl == VeoVertexEndpoints.FetchPredictOperation(settings),
            "START predictLongRunning / POLL fetchPredictOperation");
        Ok(composed.StartUrl!.Contains($"/v1/projects/{ComposeProject}/locations/us-central1/publishers/google/models/veo-3.1-generate-001:predictLongRunning", StringComparison.Ordinal)
            && composed.PollUrl!.Contains($"/v1/projects/{ComposeProject}/locations/us-central1/publishers/google/models/veo-3.1-generate-001:fetchPredictOperation", StringComparison.Ordinal),
            "endpoint derived from project/location/model");
        Ok(!composed.StartUrl.Contains("nt-xuanhoa", StringComparison.OrdinalIgnoreCase)
            && settings.ProjectId != "kit-platform"
            && settings.ProjectId != "novixa",
            "no user-specific project hard-code");
        var europe = OfficialVeoHttpCompositionV1.Compose(
            Request(frozen, sources, bytes) with { Location = "europe-west1" });
        Ok(europe.Ready && europe.Settings!.Location == "europe-west1"
            && europe.StartUrl!.Contains("europe-west1-aiplatform.googleapis.com", StringComparison.Ordinal),
            "location override stays composition-only");
    }

    static async Task T_RequestContract(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        var json = composed.VertexRequestJson!;
        using var doc = JsonDocument.Parse(json);
        var inst = doc.RootElement.GetProperty("instances")[0];
        var p = doc.RootElement.GetProperty("parameters");
        Ok(inst.GetProperty("prompt").GetString() == VeoMotionPrompt.FromFrozen(frozen)
            && inst.GetProperty("prompt").GetString() == frozen.Story + " " + frozen.ActingBeat + " " + frozen.VisualContract,
            "prompt = Story + ActingBeat + VisualContract");
        var img = inst.GetProperty("image");
        var wireBytes = Convert.FromBase64String(img.GetProperty("bytesBase64Encoded").GetString()!);
        Ok(wireBytes.SequenceEqual(bytes) && img.GetProperty("mimeType").GetString() == "image/png",
            "image bytes + MIME from frozen keyframe");
        Ok(p.GetProperty("sampleCount").GetInt32() == 1
            && p.GetProperty("aspectRatio").GetString() == "16:9"
            && p.GetProperty("durationSeconds").GetInt32() == 8
            && p.GetProperty("resolution").GetString() == "720p"
            && p.GetProperty("generateAudio").GetBoolean() == false,
            "sampleCount=1 / aspect 16:9 / durationSeconds=8 / 720p / generateAudio=false");
        Ok(!json.Contains("seed", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("lastFrame", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("http://", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("https://", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("gs://", StringComparison.Ordinal)
            && !json.Contains("gcsUri", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("outputGcs", StringComparison.OrdinalIgnoreCase)
            && !json.Contains("imageUri", StringComparison.OrdinalIgnoreCase),
            "no seed / lastFrame / image URL / GCS");
        Ok(json == HttpVeoMotionTransport.PreviewStartBody(composed.TransportRequest!),
            "composition reuses HttpVeoMotionTransport PreviewStartBody");
    }

    static async Task T_DurationMapping(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var before = frozen.DurationSec;
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        Ok(before == OfficialCreateVideoFreezeV1.OfficialDurationSec
            && frozen.DurationSec == 10
            && composed.TransportRequest!.OsDurationSec == 10
            && composed.TransportRequest.ProviderDurationSec == 8
            && VeoMotionDurationMapping.ToProviderDurationSec(frozen.DurationSec) == 8,
            "OS 10 stays on snapshot; provider wire is 8");
    }

    static async Task T_KeyframeIntegrity(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        var req = composed.TransportRequest!;
        Ok(req.KeyframeArtifactId == OfficialCreateVideoFreezeV1.OfficialKeyframeId
            && req.KeyframeHash == frozen.KeyframeHash
            && req.KeyframeHash == KitVideoIntegrityRules.Sha256Hex(bytes),
            "exact frozen KeyframeArtifactId + hash");
        using var doc = JsonDocument.Parse(composed.VertexRequestJson!);
        var wire = Convert.FromBase64String(
            doc.RootElement.GetProperty("instances")[0].GetProperty("image").GetProperty("bytesBase64Encoded").GetString()!);
        Ok(wire.SequenceEqual(bytes) && KitVideoIntegrityRules.Sha256Hex(wire) == frozen.KeyframeHash,
            "composed bytes match authoritative artifact hash");
    }

    static async Task T_WrongBuild(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Scope = new ProductionScope(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "EP01", "SC01", "EP01-SC01-SH02"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "wrong build");
    }

    static async Task T_WrongShot(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Scope = new ProductionScope(RealVertexExecutionV1Lock.BuildId, "EP01", "SC01", "EP01-SC01-SH01"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.UnauthorizedVertexTarget, "wrong shot");
    }

    static async Task T_WrongProvider(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Selection = OfficialSelection("runway", "veo-3.1"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "wrong provider");
    }

    static async Task T_WrongModel(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Selection = OfficialSelection("google", "veo-2"),
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.ProviderModelMismatch, "wrong model");
    }

    static async Task T_WrongSelection(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Selection = OfficialSelection() with { SelectionId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") },
        });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.SelectionConflict, "wrong selection");
    }

    static async Task T_FixtureRunwayUnselected(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        Ok(!OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Selection = OfficialSelection("fixture", "fixture-motion-v1"),
        }).Ready, "Fixture rejected");
        Ok(!OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with
        {
            Selection = OfficialSelection("runway", "gen4_turbo"),
        }).Ready, "Runway rejected");
        Ok(!OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with { Selection = null }).Ready
            && OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with { Selection = null }).BlockReason
                == ProductionFailureReasons.MissingProductionAiSelection,
            "unselected provider/model rejected");
    }

    static async Task T_MissingFrozen(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var r = OfficialVeoHttpCompositionV1.Compose(Request(null, sources, bytes) with { Frozen = null });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.InputNotFrozen, "missing frozen snapshot");
    }

    static async Task T_SnapshotMismatch(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var live = Live(sources) with { Story = sources.Story + " changed" };
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes) with { Live = live });
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "snapshot mismatch");
    }

    static async Task T_WrongKeyframe(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var other = Encoding.UTF8.GetBytes("FAMIXA-KF-CK16-OTHER-" + new string('X', 48));
        var r = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, other));
        Ok(!r.Ready && r.BlockReason == ProductionFailureReasons.InputSnapshotMismatch, "wrong keyframe rejected");
        var missing = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, null) with { KeyframeBytes = null });
        Ok(!missing.Ready && missing.BlockReason == ProductionFailureReasons.KeyframeBytesMissing,
            "missing keyframe bytes rejected");
        Ok(bytes.Length >= 32 && KitVideoIntegrityRules.Sha256Hex(bytes) == frozen.KeyframeHash,
            "authoritative frozen bytes unchanged");
    }

    static async Task T_WrongCostVariant(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var audio = OfficialVeoHttpCompositionV1.Compose(
            Request(frozen, sources, bytes) with { Variant = FamixaVeo31OfficialPricing.VideoAudio720p() });
        var fourK = OfficialVeoHttpCompositionV1.Compose(
            Request(frozen, sources, bytes) with { Variant = FamixaVeo31OfficialPricing.VideoOnly4k() });
        Ok(!audio.Ready && audio.BlockReason == ProductionFailureReasons.UnknownCost, "AUDIO variant rejected");
        Ok(!fourK.Ready && fourK.BlockReason == ProductionFailureReasons.UnknownCost, "4K variant rejected");
    }

    static async Task T_LifecycleOffline(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, """{"done":false}"""),
        };
        var auth = new FakeVeoHttpAuthenticator();
        var transport = new HttpVeoMotionTransport(new HttpClient(handler), auth, composed.Settings!);
        var adapter = new VeoMotionExecutionAdapter(transport);
        var start = await transport.CreateImageToVideoAsync(composed.TransportRequest!);
        Ok(VeoVertexOperationId.Decode(start.ProviderJobId) == OperationName, "operation identity encoded");
        Ok(handler.Last!.Url == composed.StartUrl && handler.PredictCalls == 1, "START predictLongRunning");
        Ok(handler.Last.Body == composed.VertexRequestJson, "START body == composed JSON");
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        Ok(poll.Status == FakeVeoMotionTransport.WireRunning && handler.FetchCalls == 1
            && handler.Last.Url == composed.PollUrl
            && handler.Last.Body == HttpVeoMotionTransport.PreviewPollBody(OperationName),
            "POLL fetchPredictOperation + operationName");
        var recover = await adapter.RecoverFrozenAsync(frozen, start.ProviderJobId);
        Ok(recover.ProviderJobId == start.ProviderJobId && handler.PredictCalls == 1 && handler.FetchCalls == 2,
            "Recover uses same Poll, never Start again");
        Ok(auth.Calls == 3 && handler.Requests.All(r => r.Scheme == "Bearer" && r.Token == FakeVeoHttpAuthenticator.Token),
            "lifecycle uses FakeVeoHttpAuthenticator only");
    }

    static async Task T_OutputPolicy(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        var mp4 = ProductionTestMp4.Encode("ck16-inline", 10);
        var inline = await OutputOf(composed, "{\"done\":true,\"response\":{\"videos\":[{\"bytesBase64Encoded\":\"" + Convert.ToBase64String(mp4) + "\"}]}}");
        Ok(inline.Poll.Status == FakeVeoMotionTransport.WireSucceeded && inline.Bytes.SequenceEqual(mp4),
            "inline output bytes → byte[]");
        var gcs = await OutputOf(composed, """{"done":true,"response":{"videos":[{"gcsUri":"gs://bucket/out.mp4"}]}}""");
        Ok(gcs.Poll.Status == FakeVeoMotionTransport.WireFailed
            && gcs.Poll.FailureReason == ProductionFailureReasons.ProviderOutputUnsupported,
            "GCS/URI-only → PROVIDER_OUTPUT_UNSUPPORTED");
        var missing = await OutputOf(composed, """{"done":true,"response":{"videos":[{}]}}""");
        Ok(missing.Poll.Status == FakeVeoMotionTransport.WireFailed
            && missing.Poll.FailureReason == ProductionFailureReasons.ProviderOutputMissing,
            "missing output → PROVIDER_OUTPUT_MISSING");
        var malformed = await OutputOf(composed, "not-json{");
        Ok(malformed.Poll.Status == FakeVeoMotionTransport.WireFailed
            && malformed.Poll.FailureReason == ProductionFailureReasons.InvalidProviderResponse,
            "malformed → INVALID_PROVIDER_RESPONSE");
        Ok(!inline.Handler.Last!.Body.Contains("/api/content/series/veo-video", StringComparison.Ordinal)
            && !inline.Handler.Last.Url.Contains("KitVideoVideoStore", StringComparison.Ordinal),
            "KitVideoVideoStore URL is not authoritative");
    }

    static async Task T_NoNetworkNoMutation(Action<bool, string> Ok)
    {
        var (store, sources, bytes, permits) = await Harness();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var closed = new ProductionVeoHttpAuthenticator();
        var first = await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin());
        var hash = first.Snapshot.SnapshotHash;
        var duration = first.Snapshot.DurationSec;
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(first.Snapshot, sources, bytes));
        var after = await OfficialCreateVideoFreezeV1.FindOfficialCreateVideoSnapshotAsync(store);
        Ok(composed.Ready, "compose succeeds offline");
        Ok(after!.SnapshotId == first.Snapshot.SnapshotId
            && after.SnapshotHash == hash
            && after.DurationSec == 10
            && duration == 10
            && await OfficialCreateVideoFreezeV1.CountOfficialCreateVideoSnapshotsAsync(store) == 1,
            "frozen snapshot unchanged (DurationSec=10)");
        Ok(graph.Handler.Requests.Count == 0 && graph.Transport.HttpCallCount == 0
            && graph.Authenticator.Calls == 0 && graph.Transport.StartCount == 0
            && RealVertexExecutionV1Lock.StartCount == 0,
            "no HTTP / OAuth / provider Start");
        Ok(permits.IssueCalls == 0 && permits.ConsumeCalls == 0, "permit issued/consumed = 0");
        Ok(closed.GetAccessTokenCalls == 0, "production authenticator not invoked");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, null, null, null, null) is null,
            "no credential / Join file read");
    }

    static async Task T_PreflightRemovesHttpCompositionUnavailable(Action<bool, string> Ok)
    {
        var (_, sources, bytes, _) = await Harness();
        var frozen = await Freeze(sources);
        var composed = OfficialVeoHttpCompositionV1.Compose(Request(frozen, sources, bytes));
        var auth = OfficialVeoProductionAuthenticatorV1.Compose(
            new OfficialVeoAuthenticatorCompositionRequest(RealVertexExecutionV1Lock.OfficialScope, OfficialSelection()));
        var report = CostOfficialPermitAuthorization.Evaluate(
            RealVertexExecutionPreflight.ProductionClosedDefaults(
                RealVertexExecutionV1Lock.OfficialScope, OfficialSelection())
            with
            {
                Frozen = frozen,
                LiveClaim = Live(sources),
                Adapter = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport()),
                KeyframeBytes = bytes,
                OfficialAuthenticator = auth,
                OfficialHttpComposition = composed,
            });
        Ok(composed.Ready && report.HttpCompositionPass
            && !report.Preflight.Blockers.Contains(ProductionFailureReasons.HttpCompositionUnavailable),
            "HTTP_COMPOSITION_UNAVAILABLE removed for official composition");
        Ok(!report.Preflight.Allowed
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.UnknownProvider)
            && report.Preflight.Blockers.Contains(ProductionFailureReasons.ExecutionAuthorizationRequired),
            "resolver / execution remain BLOCKED");
        var closed = RealVertexExecutionPreflight.ProductionClosedDefaults(
            RealVertexExecutionV1Lock.OfficialScope, OfficialSelection());
        Ok(!closed.HttpCompositionAvailable && closed.OfficialHttpComposition is null
            && !RealVertexExecutionPreflight.IsHttpCompositionAvailable(closed)
            && RealVertexExecutionPreflight.Evaluate(closed).Blockers.Contains(ProductionFailureReasons.UnknownProvider),
            "ClosedDefaults keep HTTP unavailable + UNKNOWN_PROVIDER");
    }

    static void T_ProductionResolverClosed(Action<bool, string> Ok)
    {
        Ok(!OfficialVeoHttpCompositionV1.ProductionResolverOpen
            && !ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "production resolver / execution remain CLOSED");
        var resolver = ProductionVeoReadinessComposition.CreateProductionResolver();
        var unknown = false;
        try { resolver.Resolve("google", "veo-3.1"); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionFailureReasons.UnknownProvider, StringComparison.Ordinal))
        {
            unknown = true;
        }
        Ok(unknown, "UNKNOWN_PROVIDER remains on production resolver");
    }

    static void T_Ck15StillGreen(Action<bool, string> Ok)
    {
        var ck15 = ProductionOfficialVeoProductionAuthenticatorV1Regression.Run();
        Ok(ck15.Count == 0, ck15.Count == 0 ? "CK15 still green" : "CK15:" + string.Join(",", ck15));
    }

    static async Task<(FakeVeoHttpMessageHandler Handler, VeoMotionTransportPollResult Poll, byte[] Bytes)>
        OutputOf(OfficialVeoHttpCompositionResult composed, string pollJson)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, pollJson),
        };
        var transport = new HttpVeoMotionTransport(new HttpClient(handler), new FakeVeoHttpAuthenticator(), composed.Settings!);
        var start = await transport.CreateImageToVideoAsync(composed.TransportRequest!);
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        byte[] output = [];
        if (poll.Status == FakeVeoMotionTransport.WireSucceeded)
            output = await transport.DownloadOutputAsync(start.ProviderJobId);
        return (handler, poll, output);
    }

    static OfficialVeoHttpCompositionRequest Request(
        ProductionTaskInputSnapshot? frozen,
        OfficialCreateVideoFreezeSources sources,
        byte[]? bytes) =>
        new(
            RealVertexExecutionV1Lock.OfficialScope,
            OfficialSelection(),
            frozen,
            Live(sources),
            bytes,
            "image/png",
            CostPolicyDecision.FromCatalog("google", "veo-3.1", 10),
            FamixaVeo31OfficialPricing.ProductionDefault,
            ComposeProject);

    static ProductionAiSelection OfficialSelection(string provider = "google", string model = "veo-3.1") =>
        new(
            OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode, "EP01", "SC01",
            ProductionAiSelectionStages.Video,
            provider, model,
            ProductionAiSelectionModes.Explicit,
            "checkpoint-16",
            DateTimeOffset.UnixEpoch,
            "DIRECTOR_SELECTION",
            ProductionAiSelectionStatuses.Active,
            "SELECTION_HASH_V1:ck16");

    static ShotExecutionInput Live(OfficialCreateVideoFreezeSources sources) =>
        new(
            sources.Story, sources.ActingBeat, sources.VisualContract, "CHAR-001",
            sources.KeyframeHash, sources.Timing, "", "",
            sources.KeyframeArtifactId, sources.KeyframeHash, "16:9", sources.DurationSec,
            ShotExecutionCompiler.Version, [sources.KeyframeArtifactId],
            PerformanceSnapshotId: sources.PerformanceSnapshotId,
            PerformanceHash: sources.PerformanceHash);

    static async Task<ProductionTaskInputSnapshot> Freeze(OfficialCreateVideoFreezeSources sources)
    {
        var store = new InMemoryProductionWorkStore();
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck16",
            "kf-16-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        await store.InsertArtifactAsync(new ProductionArtifact(
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            task.TaskId,
            Guid.NewGuid(),
            ProductionArtifactRoles.Keyframe,
            "image/png",
            sources.KeyframeBytes.Length,
            0,
            sources.KeyframeHash,
            "kf/EP01-SC01-SH02.png",
            null,
            DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified,
            sources.KeyframeHash));
        return (await OfficialCreateVideoFreezeV1.FreezeAsync(store, sources, OfficialCreateVideoFreezeCredentialFixture.Pin())).Snapshot;
    }

    static async Task<(InMemoryProductionWorkStore Store, OfficialCreateVideoFreezeSources Sources, byte[] Bytes, CountingProductionExecutionPermitStore Permits)>
        Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-16-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var engine = new ProductionWorkExecutionEngine(store);
        var req = await engine.CreateRequestAsync(
            RealVertexExecutionV1Lock.OfficialScope, ProductionActions.CreateImage, "ck16",
            "kf-16-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        await store.InsertArtifactAsync(new ProductionArtifact(
            OfficialCreateVideoFreezeV1.OfficialKeyframeId,
            task.TaskId,
            Guid.NewGuid(),
            ProductionArtifactRoles.Keyframe,
            "image/png",
            bytes.Length,
            0,
            hash,
            "kf/EP01-SC01-SH02.png",
            null,
            DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified,
            hash));
        return (store, OfficialCreateVideoFreezeV1.FromEp01Sh02Canon(bytes, hash), bytes,
            new CountingProductionExecutionPermitStore());
    }
}
