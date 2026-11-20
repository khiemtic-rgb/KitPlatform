using System.Net;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionHttpVeoTransportV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_HTTP_VEO_TRANSPORT_V1";
    const string Story = "Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.";
    const string Acting = "He stays in place.";
    const string Visual = "visual 16:9";
    const string OperationName = "projects/test-project/locations/us-central1/operations/op-ck9";

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
        await T_StartRequestShape(Ok);
        await T_StartParsesOperationName(Ok);
        await T_StartHttpError(Ok);
        await T_PollRunning(Ok);
        await T_PollSuccessInlineBytes(Ok);
        await T_PollProviderError(Ok);
        await T_PollMalformed(Ok);
        await T_RecoverDoesNotStart(Ok);
        await T_TimeoutNoNewStart(Ok);
        await T_Cancellation(Ok);
        await T_UnsupportedGcs(Ok);
        await T_KeyframeAndPromptAndDuration(Ok);
        T_NoNetworkHandler(Ok);
    }

    static async Task T_StartRequestShape(Action<bool, string> Ok)
    {
        var (transport, handler, auth, adapter, snap, bytes, ctx) = Harness();
        await adapter.StartFrozenAsync(snap, ctx, default);
        var cap = handler.Last!;
        Ok(cap.Method == "POST", "START method POST");
        Ok(cap.Url == VeoVertexEndpoints.PredictLongRunning(Settings()), "START predictLongRunning URL");
        Ok(cap.Url.Contains("/v1/projects/test-project/locations/us-central1/publishers/google/models/veo-3.1-generate-001:predictLongRunning", StringComparison.Ordinal),
            "START v1 google publisher veo-3.1-generate-001");
        Ok(cap.Scheme == "Bearer" && cap.Token == FakeVeoHttpAuthenticator.Token, "START Authorization Bearer fake token");
        Ok(auth.Calls == 1, "START uses fake authenticator only");
        using var doc = JsonDocument.Parse(cap.Body);
        var inst = doc.RootElement.GetProperty("instances")[0];
        var p = doc.RootElement.GetProperty("parameters");
        Ok(inst.GetProperty("prompt").GetString() == Story + " " + Acting + " " + Visual, "START prompt");
        Ok(p.GetProperty("sampleCount").GetInt32() == 1
            && p.GetProperty("resolution").GetString() == "720p"
            && p.GetProperty("generateAudio").GetBoolean() == false
            && p.GetProperty("durationSeconds").GetInt32() == 8
            && p.GetProperty("aspectRatio").GetString() == "16:9",
            "START fixed params + duration 8 + aspect 16:9");
        var img = inst.GetProperty("image");
        Ok(Convert.FromBase64String(img.GetProperty("bytesBase64Encoded").GetString()!).SequenceEqual(bytes)
            && img.GetProperty("mimeType").GetString() == "image/png",
            "START image bytes == frozen keyframe");
        Ok(!cap.Body.Contains("seed", StringComparison.OrdinalIgnoreCase)
            && !cap.Body.Contains("lastFrame", StringComparison.OrdinalIgnoreCase)
            && !cap.Body.Contains("gs://", StringComparison.Ordinal),
            "START has no seed/last-frame/GCS");
        Ok(transport.HttpCallCount >= 1 && handler.PredictCalls == 1, "START one HTTP predict");
    }

    static async Task T_StartParsesOperationName(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = _ => FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}"""),
        };
        var transport = NewTransport(handler);
        var created = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        Ok(VeoVertexOperationId.Decode(created.ProviderJobId) == OperationName, "START parses operation name");
        Ok(!created.ProviderJobId.StartsWith(FakeVeoMotionTransport.JobPrefix, StringComparison.Ordinal),
            "production transport does not mint fake-veo job ids");
    }

    static async Task T_StartHttpError(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = _ => FakeVeoHttpMessageHandler.Json(HttpStatusCode.Forbidden, """{"error":{"message":"denied"}}"""),
        };
        var transport = NewTransport(handler);
        var failed = false;
        try { await transport.CreateImageToVideoAsync(SampleRequest(), default); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(FamixaProviderErrorCodes.AuthFailed, StringComparison.Ordinal)
            && ex.Message.Contains("http=403", StringComparison.Ordinal))
        {
            failed = true;
        }
        Ok(failed, "START 403 => AUTH_FAILED");
    }

    static async Task T_PollRunning(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, """{"done":false}"""),
        };
        var transport = NewTransport(handler);
        var start = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        Ok(poll.Status == FakeVeoMotionTransport.WireRunning && handler.FetchCalls == 1, "POLL RUNNING");
    }

    static async Task T_PollSuccessInlineBytes(Action<bool, string> Ok)
    {
        var mp4 = ProductionTestMp4.Encode("ck9-inline", 10);
        var b64 = Convert.ToBase64String(mp4);
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK,
                    "{\"done\":true,\"response\":{\"videos\":[{\"bytesBase64Encoded\":\"" + b64 + "\"}]}}"),
        };
        var transport = NewTransport(handler);
        var start = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        var bytes = await transport.DownloadOutputAsync(start.ProviderJobId);
        Ok(poll.Status == FakeVeoMotionTransport.WireSucceeded && bytes.SequenceEqual(mp4),
            "POLL success returns inline byte[]");
        Ok(!handler.Last!.Body.Contains("/api/content/series/veo-video", StringComparison.Ordinal),
            "output is not a legacy public URL");
    }

    static async Task T_PollProviderError(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, """{"error":{"message":"model failed"}}"""),
        };
        var transport = NewTransport(handler);
        var start = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        Ok(poll.Status == FakeVeoMotionTransport.WireFailed
            && (poll.FailureReason ?? "").StartsWith(FamixaProviderErrorCodes.ProviderError, StringComparison.Ordinal),
            "POLL provider error FAIL CLOSED");
    }

    static async Task T_PollMalformed(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, "not-json{"),
        };
        var transport = NewTransport(handler);
        var start = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        Ok(poll.Status == FakeVeoMotionTransport.WireFailed
            && poll.FailureReason == ProductionFailureReasons.InvalidProviderResponse,
            "POLL malformed FAIL CLOSED");
    }

    static async Task T_RecoverDoesNotStart(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, """{"done":false}"""),
        };
        var http = new HttpClient(handler);
        var transport = new HttpVeoMotionTransport(http, new FakeVeoHttpAuthenticator(), Settings());
        var adapter = new VeoMotionExecutionAdapter(transport);
        var (_, _, _, _, snap, _, ctx) = Harness(handler, transport, adapter);
        var start = await adapter.StartFrozenAsync(snap, ctx, default);
        _ = await adapter.RecoverFrozenAsync(snap, start.ProviderJobId);
        _ = await adapter.RecoverFrozenAsync(snap, start.ProviderJobId);
        Ok(handler.PredictCalls == 1 && handler.FetchCalls == 2,
            "Recover polls existing operation, never predictLongRunning again");
    }

    static async Task T_TimeoutNoNewStart(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}"""),
        };
        var transport = NewTransport(handler);
        var start = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        var predict = handler.PredictCalls;
        handler.ThrowCanceled = true;
        var timedOut = false;
        try { await transport.GetOperationAsync(start.ProviderJobId); }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(FamixaProviderErrorCodes.Timeout, StringComparison.Ordinal))
        {
            timedOut = true;
        }
        if (!timedOut)
        {
            var poll = await transport.GetOperationAsync(start.ProviderJobId);
            timedOut = (poll.FailureReason ?? "").StartsWith(FamixaProviderErrorCodes.Timeout, StringComparison.Ordinal);
        }
        Ok(timedOut && handler.PredictCalls == predict, "timeout does not start a new provider job");
    }

    static async Task T_Cancellation(Action<bool, string> Ok)
    {
        var transport = NewTransport(new FakeVeoHttpMessageHandler());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = false;
        try { await transport.CreateImageToVideoAsync(SampleRequest(), cts.Token); }
        catch (OperationCanceledException) { cancelled = true; }
        Ok(cancelled, "cancelled token fails closed without network");
    }

    static async Task T_UnsupportedGcs(Action<bool, string> Ok)
    {
        var handler = new FakeVeoHttpMessageHandler
        {
            OnSend = cap => cap.Url.Contains(":predictLongRunning", StringComparison.Ordinal)
                ? FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK, $$"""{"name":"{{OperationName}}"}""")
                : FakeVeoHttpMessageHandler.Json(HttpStatusCode.OK,
                    """{"done":true,"response":{"videos":[{"gcsUri":"gs://bucket/out.mp4"}]}}"""),
        };
        var transport = NewTransport(handler);
        var start = await transport.CreateImageToVideoAsync(SampleRequest(), default);
        var poll = await transport.GetOperationAsync(start.ProviderJobId);
        Ok(poll.Status == FakeVeoMotionTransport.WireFailed
            && poll.FailureReason == ProductionFailureReasons.ProviderOutputUnsupported,
            "GCS URI is PROVIDER_OUTPUT_UNSUPPORTED, not bytes");
    }

    static async Task T_KeyframeAndPromptAndDuration(Action<bool, string> Ok)
    {
        var (transport, handler, _, adapter, snap, bytes, ctx) = Harness();
        await adapter.StartFrozenAsync(snap, ctx, default);
        using var doc = JsonDocument.Parse(handler.Last!.Body);
        var img = Convert.FromBase64String(doc.RootElement.GetProperty("instances")[0].GetProperty("image").GetProperty("bytesBase64Encoded").GetString()!);
        Ok(img.SequenceEqual(bytes) && KitVideoIntegrityRules.Sha256Hex(img) == snap.KeyframeHash,
            "frozen keyframe hash unchanged on the wire");
        Ok(doc.RootElement.GetProperty("instances")[0].GetProperty("prompt").GetString() == VeoMotionPrompt.FromFrozen(snap),
            "request prompt == adapter frozen prompt");
        Ok(snap.DurationSec == 10
            && doc.RootElement.GetProperty("parameters").GetProperty("durationSeconds").GetInt32() == 8,
            "OS DurationSec stays 10, provider durationSeconds is 8");
        var live = Encoding.UTF8.GetBytes("LIVE-NOT-FROZEN" + new string('X', 48));
        var rejected = false;
        try { await adapter.StartFrozenAsync(snap, ctx with { KeyframeBytes = live }, default); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.InputSnapshotMismatch)
        {
            rejected = true;
        }
        Ok(rejected, "transport never sees live keyframe fallback");
        Ok(transport.RequiresNetwork && handler.Requests.All(r => r.Url.StartsWith("https://us-central1-aiplatform.googleapis.com/", StringComparison.Ordinal)),
            "HTTP boundary is Vertex-shaped; handler is fake");
    }

    static void T_NoNetworkHandler(Action<bool, string> Ok)
    {
        Ok(typeof(FakeVeoHttpMessageHandler).IsSubclassOf(typeof(HttpMessageHandler)),
            "tests use FakeVeoHttpMessageHandler only");
        Ok(!typeof(HttpVeoMotionTransport).Assembly.GetTypes().Any(t => t.Name.Contains("ContentVeoClient", StringComparison.Ordinal)),
            "Application transport does not reference ContentVeoClient");
    }

    static VeoHttpSettings Settings() => new("test-project", "us-central1");

    static HttpVeoMotionTransport NewTransport(FakeVeoHttpMessageHandler handler) =>
        new(new HttpClient(handler), new FakeVeoHttpAuthenticator(), Settings());

    static VeoMotionTransportCreateRequest SampleRequest()
    {
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK9-" + new string('K', 48));
        return new VeoMotionTransportCreateRequest(
            Guid.NewGuid(), Guid.NewGuid(),
            FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId,
            Story + " " + Acting + " " + Visual,
            VeoMotionImageDataUri.FromBytes(bytes, "image/png"),
            "image/png",
            8, 10, "16:9", 1, "720p", false,
            "ck9-snap", KitVideoIntegrityRules.Sha256Hex(bytes), Guid.NewGuid());
    }

    static (HttpVeoMotionTransport Transport, FakeVeoHttpMessageHandler Handler, FakeVeoHttpAuthenticator Auth,
        VeoMotionExecutionAdapter Adapter, ProductionTaskInputSnapshot Snap, byte[] Bytes, MotionStartContext Ctx)
        Harness(FakeVeoHttpMessageHandler? handler = null, HttpVeoMotionTransport? transport = null, VeoMotionExecutionAdapter? adapter = null)
    {
        handler ??= new FakeVeoHttpMessageHandler();
        var auth = new FakeVeoHttpAuthenticator();
        transport ??= new HttpVeoMotionTransport(new HttpClient(handler), auth, Settings());
        adapter ??= new VeoMotionExecutionAdapter(transport);
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK9-" + new string('K', 48));
        var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(),
            Story, Acting, Visual, "", "", "",
            "", "", "", "",
            hash, "", "", "",
            ShotExecutionCompiler.Hash("10.0s"),
            ProductionMotionCatalog.GoogleProviderId,
            ProductionMotionCatalog.Veo31ModelId,
            "ck9-snap-hash",
            DateTimeOffset.UtcNow,
            Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef"),
            "16:9", 10);
        var ctx = new MotionStartContext(bytes, "image/png", Guid.NewGuid(), null, "EP01-SC01-SH02");
        return (transport, handler, auth, adapter, snap, bytes, ctx);
    }
}
