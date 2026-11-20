using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionVeoMotionAdapterV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_VEO_MOTION_ADAPTER_V1";

    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
    const string Acting = "He stays in place.";
    const string Visual = "visual 16:9";

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
        T_IdentityAndNoFixtureFallback(Ok);
        await T_StartKeyframeDataUri(Ok);
        await T_PromptFromFrozen(Ok);
        await T_DurationMappingPreservesOs(Ok);
        await T_AspectFromFrozen(Ok);
        await T_FixedParams(Ok);
        await T_PollRunningThenSucceeded(Ok);
        await T_RecoverDoesNotDuplicateStart(Ok);
        await T_OutputBytes(Ok);
        await T_MissingKeyframeFailsClosed(Ok);
        await T_InvalidOutputFailsClosed(Ok);
        await T_ProviderFailureFailsClosed(Ok);
        await T_NoLiveReresolution(Ok);
    }

    static void T_IdentityAndNoFixtureFallback(Action<bool, string> Ok)
    {
        var fake = new FakeVeoMotionTransport();
        var veo = new VeoMotionExecutionAdapter(fake);
        Ok(veo.ProviderId == FamixaProviderIds.Google && veo.ModelId == "veo-3.1", "adapter identity google/veo-3.1");
        Ok(veo.IsPaid && !veo.RequiresNetwork && fake.HttpCallCount == 0, "fake transport unpaid-network and no HTTP");
        var resolver = new ProductionMotionAdapterResolver([new FixtureMotionAdapter(), veo]);
        Ok(resolver.Resolve("google", "veo-3.1") is VeoMotionExecutionAdapter, "resolver returns Veo adapter");
        Ok(resolver.Resolve("fixture", "fixture-motion-v1") is FixtureMotionAdapter, "fixture still fixture");
        Ok(resolver.Resolve("google", "veo-3.1").ProviderId != ProductionMotionCatalog.FixtureProviderId,
            "google/veo-3.1 does not fallback Fixture");
    }

    static async Task T_StartKeyframeDataUri(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, bytes, ctx) = Harness();
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        Ok(start.ProviderJobId.StartsWith(FakeVeoMotionTransport.JobPrefix, StringComparison.Ordinal), "start returns fake operation id");
        var req = fake.LastCreateRequest!;
        Ok(req.ImageDataUri.StartsWith("data:image/png;base64,", StringComparison.Ordinal), "keyframe is data URI");
        var b64 = req.ImageDataUri["data:image/png;base64,".Length..];
        Ok(Convert.FromBase64String(b64).SequenceEqual(bytes), "data URI bytes == frozen keyframe bytes");
        Ok(req.KeyframeArtifactId == snap.KeyframeArtifactId && req.KeyframeHash == snap.KeyframeHash,
            "request pins frozen KeyframeArtifactId/Hash");
        Ok(fake.HttpCallCount == 0, "start has no network");
    }

    static async Task T_PromptFromFrozen(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, ctx) = Harness();
        await adapter.StartFrozenAsync(snap, ctx, default);
        Ok(fake.LastCreateRequest!.Prompt == Story + " " + Acting + " " + Visual,
            "prompt is frozen Story + ActingBeat + VisualContract");
        Ok(!fake.LastCreateRequest.Prompt.Contains("previewUrl", StringComparison.Ordinal)
            && !fake.LastCreateRequest.Prompt.Contains("acceptedTake", StringComparison.Ordinal),
            "prompt has no live URL/take");
    }

    static async Task T_DurationMappingPreservesOs(Action<bool, string> Ok)
    {
        Ok(VeoMotionDurationMapping.ToProviderDurationSec(10) == 8, "OS 10 → provider 8");
        Ok(VeoMotionDurationMapping.ToProviderDurationSec(8) == 8, "OS 8 → provider 8");
        Ok(VeoMotionDurationMapping.ToProviderDurationSec(6) == 6, "OS 6 → provider 6");
        Ok(VeoMotionDurationMapping.ToProviderDurationSec(5) == 4, "OS 5 → provider 4");
        var (adapter, fake, snap, _, ctx) = Harness(duration: 10);
        await adapter.StartFrozenAsync(snap, ctx, default);
        Ok(snap.DurationSec == 10, "frozen OS DurationSec stays 10");
        Ok(fake.LastCreateRequest!.OsDurationSec == 10 && fake.LastCreateRequest.ProviderDurationSec == 8,
            "request keeps OS 10 and maps provider 8");
    }

    static async Task T_AspectFromFrozen(Action<bool, string> Ok)
    {
        var wide = Harness(aspect: "16:9");
        await wide.Adapter.StartFrozenAsync(wide.Snap, wide.Ctx, default);
        Ok(wide.Fake.LastCreateRequest!.AspectRatio == "16:9", "frozen 16:9 preserved");
        var tall = Harness(aspect: "9:16");
        await tall.Adapter.StartFrozenAsync(tall.Snap, tall.Ctx, default);
        Ok(tall.Fake.LastCreateRequest!.AspectRatio == "9:16", "frozen 9:16 preserved");
    }

    static async Task T_FixedParams(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, ctx) = Harness();
        await adapter.StartFrozenAsync(snap, ctx, default);
        var req = fake.LastCreateRequest!;
        Ok(req.SampleCount == 1 && req.Resolution == "720p" && req.GenerateAudio == false,
            "legacy fixed params at provider boundary");
        Ok(req.ProviderId == FamixaProviderIds.Google && req.ModelId == "veo-3.1",
            "request identity google/veo-3.1");
    }

    static async Task T_PollRunningThenSucceeded(Action<bool, string> Ok)
    {
        var (adapter, _, snap, _, ctx) = Harness();
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        var p1 = await adapter.PollFrozenAsync(snap, start.ProviderJobId);
        var p2 = await adapter.PollFrozenAsync(snap, start.ProviderJobId);
        var p3 = await adapter.PollFrozenAsync(snap, start.ProviderJobId);
        Ok(p1.Status == MotionPollStatuses.Processing
            && p2.Status == MotionPollStatuses.Processing
            && p3.Status == MotionPollStatuses.Succeeded
            && p3.ProviderJobId == start.ProviderJobId,
            "poll RUNNING → RUNNING → SUCCEEDED");
    }

    static async Task T_RecoverDoesNotDuplicateStart(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, ctx) = Harness();
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        _ = await adapter.RecoverFrozenAsync(snap, start.ProviderJobId);
        _ = await adapter.RecoverFrozenAsync(snap, start.ProviderJobId);
        Ok(fake.CreateCalls == 1 && fake.GetOperationCalls == 2,
            "Recover/Poll do not create a duplicate provider job");
    }

    static async Task T_OutputBytes(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, ctx) = Harness();
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        for (var i = 0; i < 3; i++)
            await adapter.PollFrozenAsync(snap, start.ProviderJobId);
        var output = await adapter.FetchOutputAsync(snap, start.ProviderJobId);
        Ok(output.Bytes.Length >= 32 && output.ProviderJobId == start.ProviderJobId, "lifecycle receives output bytes");
        Ok(fake.DownloadCalls == 1 && fake.HttpCallCount == 0, "bytes come from fake transport");
        Ok(output.DurationSec == 10, "output duration stays OS 10, not provider 8");
    }

    static async Task T_MissingKeyframeFailsClosed(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, _) = Harness();
        var failed = false;
        try { await adapter.StartFrozenAsync(snap, new MotionStartContext(null, "image/png"), default); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.MissingKeyframe)
        {
            failed = true;
        }
        Ok(failed && fake.CreateCalls == 0, "missing keyframe FAIL CLOSED before transport");
    }

    static async Task T_InvalidOutputFailsClosed(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, ctx) = Harness();
        fake.Scenario = FakeVeoScenario.InvalidOutput;
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        var failed = false;
        try { await adapter.FetchOutputAsync(snap, start.ProviderJobId); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedIdentity)
        {
            failed = true;
        }
        Ok(failed, "invalid output bytes FAIL CLOSED");
    }

    static async Task T_ProviderFailureFailsClosed(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, _, ctx) = Harness();
        fake.Scenario = FakeVeoScenario.Failed;
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        var poll = await adapter.PollFrozenAsync(snap, start.ProviderJobId);
        Ok(poll.Status == MotionPollStatuses.Failed
            && poll.FailureReason == ProductionFailureReasons.ProviderUnavailable
            && FamixaProviderErrors.Classify(poll.FailureReason) == FamixaProviderErrorCodes.Unsupported,
            "provider failure FAIL CLOSED + classified");
        var unknown = await adapter.PollFrozenAsync(snap, "not-a-veo-operation");
        Ok(unknown.Status == MotionPollStatuses.Failed
            && unknown.FailureReason == ProductionFailureReasons.UnknownProvider,
            "unknown operation FAIL CLOSED");
    }

    static async Task T_NoLiveReresolution(Action<bool, string> Ok)
    {
        var (adapter, fake, snap, bytes, ctx) = Harness();
        await adapter.StartFrozenAsync(snap, ctx, default);
        var firstUri = fake.LastCreateRequest!.ImageDataUri;
        var firstPrompt = fake.LastCreateRequest.Prompt;
        var liveBytes = Encoding.UTF8.GetBytes("LIVE-KEYFRAME-NOT-FROZEN" + new string('L', 48));
        var liveCtx = ctx with { KeyframeBytes = liveBytes };
        var hashFailed = false;
        try { await adapter.StartFrozenAsync(snap, liveCtx, default); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.InputSnapshotMismatch)
        {
            hashFailed = true;
        }
        Ok(hashFailed, "live keyframe bytes that miss frozen hash FAIL CLOSED");
        await adapter.StartFrozenAsync(snap, ctx, default);
        Ok(fake.LastCreateRequest!.ImageDataUri == firstUri
            && fake.LastCreateRequest.Prompt == firstPrompt
            && !Convert.FromBase64String(fake.LastCreateRequest.ImageDataUri.Split(',')[1]).SequenceEqual(liveBytes),
            "replay uses frozen bytes/prompt, not live keyframe");
    }

    static (VeoMotionExecutionAdapter Adapter, FakeVeoMotionTransport Fake, ProductionTaskInputSnapshot Snap, byte[] Bytes, MotionStartContext Ctx)
        Harness(decimal duration = 10, string aspect = "16:9")
    {
        var fake = new FakeVeoMotionTransport();
        var adapter = new VeoMotionExecutionAdapter(fake);
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK7-" + new string('K', 48));
        var kfId = Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef");
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(),
            Story, Acting, Visual, "", "", "",
            "", "", "", "",
            hash, "", "", "",
            ShotExecutionCompiler.Hash(duration.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s"),
            ProductionMotionCatalog.GoogleProviderId,
            ProductionMotionCatalog.Veo31ModelId,
            "ck7-snap-hash",
            DateTimeOffset.UtcNow,
            kfId, aspect, duration);
        var ctx = new MotionStartContext(bytes, "image/png", Guid.NewGuid(), null, "EP01-SC01-SH02");
        return (adapter, fake, snap, bytes, ctx);
    }
}
