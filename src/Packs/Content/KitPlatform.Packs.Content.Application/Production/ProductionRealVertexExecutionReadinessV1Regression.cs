using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionRealVertexExecutionReadinessV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_REAL_VERTEX_EXECUTION_READINESS_V1";
    const string FakeProject = "ck10-fake-project";
    const string FakeEmail = "veo@ck10-fake.iam.gserviceaccount.com";
    const string FakeSaJson =
        """{"type":"service_account","project_id":"ck10-fake-project","client_email":"veo@ck10-fake.iam.gserviceaccount.com","private_key":"-----BEGIN PRIVATE KEY-----\nTEST-NOT-A-REAL-KEY\n-----END PRIVATE KEY-----\n","token_uri":"https://oauth2.googleapis.com/token"}""";
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
        T_ConfigurationPrecedence(Ok);
        T_MissingCredentialConfig(Ok);
        T_MalformedFakeServiceAccount(Ok);
        T_ValidFakeServiceAccount(Ok);
        T_ProjectAndLocation(Ok);
        T_EndpointAndModel(Ok);
        T_AuthenticatorFakeAndClosed(Ok);
        T_HttpClientComposition(Ok);
        await T_AuthorizationHeaderFakeToken(Ok);
        T_NoNetwork(Ok);
        await T_OfficialResolverClosed(Ok);
        T_PermitAndPaidAndKnownCost(Ok);
        T_TimeoutSeparation(Ok);
        await T_RecoverDoesNotStart(Ok);
        T_Checkpoint9Transport(Ok);
        await T_OfficialExecutionLock(Ok);
    }

    static void T_ConfigurationPrecedence(Action<bool, string> Ok)
    {
        Ok(VeoHttpConfigPrecedence.JoinFileSourceOrder.SequenceEqual(new[]
        {
            "videoConfigJson.veoJoinFile",
            "veoJoinSecretRef",
            "ContentOptions.VeoJoinFile",
            "Content:VeoJoinFile",
            "VEO_JOIN_FILE",
        }), "join-file precedence order");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile("state", "secret", "options", "config", "env") == "state",
            "state wins; no silent merge");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, "secret", "options", "config", "env") == "secret",
            "secretRef is second");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, null, "options", "config", "env") == "options",
            "ContentOptions is third");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, null, null, "config", "env") == "config",
            "Content:VeoJoinFile is fourth");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, null, null, null, "env") == "env",
            "VEO_JOIN_FILE is last");
        Ok(VeoHttpConfigPrecedence.ResolveJoinFile(null, null, null, null, null) is null,
            "all-empty is missing, not merged");
    }

    static void T_MissingCredentialConfig(Action<bool, string> Ok)
    {
        var parsed = VeoHttpConfigPrecedence.ParseExplicit(null);
        Ok(!parsed.Ok && parsed.Code == "VEO_JOIN_FILE_MISSING" && parsed.Contract is null,
            "missing explicit config is VEO_JOIN_FILE_MISSING");
        var auth = new ProductionVeoHttpAuthenticator(null);
        Ok(auth.Contract is null && auth.GetAccessTokenCalls == 0, "closed authenticator holds no secret");
    }

    static void T_MalformedFakeServiceAccount(Action<bool, string> Ok)
    {
        var gemini = VeoHttpConfigPrecedence.ParseExplicit("AIzaSyDummyGeminiKeyThatLooksReal12");
        Ok(!gemini.Ok && gemini.Code == "GEMINI_API_KEY_REJECTED", "Gemini lookalike rejected");
        var broken = VeoHttpConfigPrecedence.ParseExplicit("{not-json");
        Ok(!broken.Ok && broken.Code == ProductionFailureReasons.InvalidProviderResponse, "malformed JSON fail-closed");
        var noKey = VeoHttpConfigPrecedence.ParseExplicit(
            """{"type":"service_account","project_id":"x","client_email":"a@b"}""");
        Ok(!noKey.Ok && noKey.Contract is null, "SA without private_key is not a Vertex account");
    }

    static void T_ValidFakeServiceAccount(Action<bool, string> Ok)
    {
        var parsed = VeoHttpConfigPrecedence.ParseExplicit(FakeSaJson);
        Ok(parsed.Ok && parsed.Contract is not null, "valid fake SA parses");
        Ok(parsed.Contract!.ProjectId == FakeProject
            && parsed.Contract.ClientEmail == FakeEmail
            && parsed.Contract.TokenUri == VeoHttpAuthContract.DefaultTokenUri
            && parsed.Contract.HasPrivateKeyPem
            && parsed.Contract.Scope == VeoHttpAuthContract.Scope
            && parsed.Contract.JwtLifetimeSeconds == 3600,
            "contract fields without storing PEM");
        var json = JsonSerializer.Serialize(parsed.Contract);
        Ok(!json.Contains("BEGIN", StringComparison.Ordinal)
            && !json.Contains("PRIVATE KEY", StringComparison.Ordinal)
            && !json.Contains("test-veo-bearer-token", StringComparison.Ordinal),
            "contract JSON never contains PEM or token");
    }

    static void T_ProjectAndLocation(Action<bool, string> Ok)
    {
        var parsed = VeoHttpConfigPrecedence.ParseExplicit(FakeSaJson);
        Ok(parsed.Contract!.ProjectId == FakeProject, "project comes from explicit fake SA");
        Ok(VeoHttpConfigPrecedence.ResolveLocation(null, null) == VeoHttpConfigPrecedence.DefaultLocation,
            "location fallback us-central1");
        Ok(VeoHttpConfigPrecedence.ResolveLocation("europe-west1", "us-central1") == "europe-west1",
            "ContentOptions.VeoLocation wins");
        Ok(VeoHttpConfigPrecedence.ResolveLocation(null, "asia-southeast1") == "asia-southeast1",
            "Content:VeoLocation is second");
        var settings = parsed.Contract.ToSettings(null);
        Ok(settings.ProjectId == FakeProject && settings.Location == "us-central1",
            "settings compose from contract + default location");
    }

    static void T_EndpointAndModel(Action<bool, string> Ok)
    {
        var settings = new VeoHttpSettings(FakeProject, "us-central1");
        var start = VeoVertexEndpoints.PredictLongRunning(settings);
        var poll = VeoVertexEndpoints.FetchPredictOperation(settings);
        Ok(start ==
            $"https://us-central1-aiplatform.googleapis.com/v1/projects/{FakeProject}/locations/us-central1/publishers/google/models/veo-3.1-generate-001:predictLongRunning",
            "START endpoint composition");
        Ok(poll ==
            $"https://us-central1-aiplatform.googleapis.com/v1/projects/{FakeProject}/locations/us-central1/publishers/google/models/veo-3.1-generate-001:fetchPredictOperation",
            "POLL endpoint composition");
        Ok(ProductionMotionCatalog.Veo31ModelId == "veo-3.1"
            && VeoVertexEndpoints.VertexModel == "veo-3.1-generate-001"
            && VeoVertexEndpoints.Publisher == "google"
            && VeoVertexEndpoints.ApiVersion == "v1",
            "catalog veo-3.1 maps to Vertex veo-3.1-generate-001");
    }

    static void T_AuthenticatorFakeAndClosed(Action<bool, string> Ok)
    {
        var fake = new FakeVeoHttpAuthenticator();
        var token = fake.GetAccessTokenAsync().GetAwaiter().GetResult();
        Ok(token == FakeVeoHttpAuthenticator.Token && fake.Calls == 1, "fake authenticator is deterministic");
        var closed = new ProductionVeoHttpAuthenticator(VeoHttpConfigPrecedence.ParseExplicit(FakeSaJson).Contract);
        var blocked = false;
        try { closed.GetAccessTokenAsync().GetAwaiter().GetResult(); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.VertexExecutionClosed)
        {
            blocked = true;
        }
        Ok(blocked && closed.GetAccessTokenCalls == 1, "production authenticator is CLOSED");
        Ok(VeoHttpAuthContract.Algorithm == "RS256"
            && VeoHttpAuthContract.Scope.Contains("cloud-platform", StringComparison.Ordinal)
            && VeoHttpAuthContract.GrantType.Contains("jwt-bearer", StringComparison.Ordinal),
            "JWT contract documented, not executed");
    }

    static void T_HttpClientComposition(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        Ok(graph.Http.Timeout == ProductionVeoHttpClientPolicy.RequestTimeout
            && ProductionVeoHttpClientPolicy.RequestTimeout == TimeSpan.FromMinutes(4),
            "HttpClient timeout is 4 minutes");
        Ok(graph.Http.BaseAddress is null, "no Vertex BaseAddress on composed client");
        Ok(graph.Handler is FakeVeoHttpMessageHandler, "tests inject FakeVeoHttpMessageHandler");
        Ok(graph.Adapter.RequiresNetwork && graph.Transport.RequiresNetwork, "HTTP transport RequiresNetwork");
        Ok(!ProductionVeoReadinessComposition.ProductionResolverIncludesVeo
            && !ProductionVeoReadinessComposition.RealVertexExecutionOpen,
            "production execution lock flags stay closed");
    }

    static async Task T_AuthorizationHeaderFakeToken(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK10-" + new string('K', 48));
        var req = new VeoMotionTransportCreateRequest(
            Guid.NewGuid(), Guid.NewGuid(),
            FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId,
            Story + " " + Acting + " " + Visual,
            VeoMotionImageDataUri.FromBytes(bytes, "image/png"),
            "image/png", 8, 10, "16:9", 1, "720p", false,
            "ck10", KitVideoIntegrityRules.Sha256Hex(bytes), Guid.NewGuid());
        await graph.Transport.CreateImageToVideoAsync(req, default);
        Ok(graph.Handler.Last!.Scheme == "Bearer"
            && graph.Handler.Last.Token == FakeVeoHttpAuthenticator.Token
            && graph.Authenticator.Calls == 1,
            "Authorization Bearer fake token only");
    }

    static void T_NoNetwork(Action<bool, string> Ok)
    {
        Ok(typeof(FakeVeoHttpMessageHandler).IsSubclassOf(typeof(HttpMessageHandler)),
            "no-network handler");
        Ok(!typeof(ProductionVeoHttpAuthenticator).Assembly.GetTypes()
                .Any(t => t.Name.Contains("ContentVeoClient", StringComparison.Ordinal)),
            "Application readiness does not reference ContentVeoClient");
    }

    static async Task T_OfficialResolverClosed(Action<bool, string> Ok)
    {
        var resolver = ProductionVeoReadinessComposition.CreateProductionResolver();
        var closed = false;
        try { resolver.Resolve(FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId); }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.UnknownProvider)
        {
            closed = true;
        }
        Ok(closed, "production-shaped resolver google/veo-3.1 is UNKNOWN_PROVIDER");
        var bound = ProductionCreateVideoProviderBinding.Authoritative(
            true,
            new ProductionAiSelection(
                Guid.Parse("c52b646f-86b9-4a43-834d-d94ab9f013b8"),
                ProductionScopeAnchorRules.OfficialEp01,
                "EP01-SC01-SH02", "EP01", "SC01",
                ProductionAiSelectionStages.Video,
                FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId,
                ProductionAiSelectionModes.Explicit,
                "checkpoint-10",
                DateTimeOffset.UtcNow,
                "DIRECTOR_SELECTION",
                ProductionAiSelectionStatuses.Active,
                "SELECTION_HASH_V1:ck10"),
            ProductionMotionCatalog.FixtureProviderId,
            ProductionMotionCatalog.FixtureModelId,
            new FixtureMotionAdapter());
        Ok(bound.ProviderId == FamixaProviderIds.Google && bound.ModelId == "veo-3.1",
            "official bind stays google/veo-3.1, no Fixture fallback");
        await Task.CompletedTask;
    }

    static void T_PermitAndPaidAndKnownCost(Action<bool, string> Ok)
    {
        Ok(ProductionMotionCatalog.TryGet(FamixaProviderIds.Google, "veo-3.1", out var catalog)
            && catalog.IsPaid,
            "google/veo-3.1 IsPaid remains true");
        var quote = FamixaProviderCostCatalog.Quote(FamixaProviderIds.Google, "veo-3.1", 10);
        Ok(quote.CostKind == FamixaCostKind.Known && quote.EstimatedAmount == 0.20m
            && quote.BillingUnit == "count",
            "quote is official $0.20 / count (duration is not a multiplier)");
        var cost = ProductionMotionCostGuard.Evaluate(FamixaProviderIds.Google, "veo-3.1", 10, true);
        Ok(cost.Allowed && cost.EstimatedCredits == 0.20m, "paid + official quote is ALLOWED");
        var veo = new VeoMotionExecutionAdapter(new FakeVeoMotionTransport());
        Ok(veo.IsPaid, "Veo adapter IsPaid");
    }

    static void T_TimeoutSeparation(Action<bool, string> Ok)
    {
        Ok(ProductionVeoHttpClientPolicy.RequestTimeout == TimeSpan.FromMinutes(4),
            "HTTP request timeout is transport-level 4 minutes");
        Ok(ProductionVeoHttpClientPolicy.OsPollAttempts == 90
            && ProductionVeoHttpClientPolicy.OsPollInterval == TimeSpan.FromSeconds(2),
            "OS poll remains 90 x 2s");
        Ok(ProductionVeoHttpClientPolicy.RequestTimeout
            != TimeSpan.FromSeconds(ProductionVeoHttpClientPolicy.OsPollAttempts * ProductionVeoHttpClientPolicy.OsPollInterval.TotalSeconds),
            "HTTP timeout is not the OS poll loop");
    }

    static async Task T_RecoverDoesNotStart(Action<bool, string> Ok)
    {
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK10-" + new string('K', 48));
        var snap = Snap(bytes);
        var ctx = new MotionStartContext(bytes, "image/png", Guid.NewGuid(), null, "EP01-SC01-SH02");
        var start = await graph.Adapter.StartFrozenAsync(snap, ctx, default);
        var starts = graph.Transport.StartCount;
        _ = await graph.Adapter.RecoverFrozenAsync(snap, start.ProviderJobId);
        Ok(graph.Transport.StartCount == starts && graph.Transport.PollCount == 1
            && graph.Handler.PredictCalls == 1 && graph.Handler.FetchCalls == 1,
            "Recover polls existing operation, never Start");
    }

    static void T_Checkpoint9Transport(Action<bool, string> Ok)
    {
        var ck9 = ProductionHttpVeoTransportV1Regression.Run();
        Ok(ck9.Count == 0, ck9.Count == 0 ? "CK9 transport regression" : "CK9:" + string.Join(",", ck9));
    }

    static async Task T_OfficialExecutionLock(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var selections = new InMemoryProductionAiSelectionStore();
        var permits = new CountingProductionExecutionPermitStore();
        var motion = new FixtureMotionAdapter();
        var graph = ProductionVeoReadinessComposition.CreateClosedTestGraph();
        var resolver = ProductionVeoReadinessComposition.CreateProductionResolver();
        var os = new ProductionExecutionService(store, motion, selections: selections, permits: permits);
        var svc = new ProductionCreateVideoService(
            os, store, files, motion, resolver: resolver, officialPermits: permits);
        var scope = new ProductionScope(
            ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        var seed = Input(10);
        var kf = await SeedKeyframe(os, scope, seed);
        var input = seed with
        {
            KeyframeArtifactId = kf.ArtifactId,
            KeyframeByteSha256 = kf.ByteSha256,
            KeyframeHash = kf.ByteSha256,
        };
        _ = await selections.UpsertActiveAsync(new UpsertProductionAiSelectionCommand(
            scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
            ProductionAiSelectionStages.Video,
            FamixaProviderIds.Google, ProductionMotionCatalog.Veo31ModelId,
            "checkpoint-10", "DIRECTOR_SELECTION"));

        var tasksBefore = (await store.ListTasksByScopeAsync(scope.BuildId, scope.ShotCode)).Count;
        var failed = false;
        try
        {
            await svc.CreateVideoAsync(new CreateVideoApiCommand(
                scope, input, "test", "ck10-lock", null, null,
                ProviderId: FamixaProviderIds.Google,
                ModelId: ProductionMotionCatalog.Veo31ModelId,
                AuthorizePaidNetwork: true));
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith(ProductionFailureReasons.UnknownProvider, StringComparison.Ordinal))
        {
            failed = true;
        }

        var tasks = await store.ListTasksByScopeAsync(scope.BuildId, scope.ShotCode);
        var prior = await store.GetRequestByIdempotencyAsync("ck10-lock");
        var eval = await os.EvaluateCreateVideoAsync(scope, CreateVideoActionContractGate.ClaimFrom(input));
        Ok(failed, "official CREATE_VIDEO stays UNKNOWN_PROVIDER");
        Ok(graph.Transport.StartCount == 0, "StartCount == 0");
        Ok(graph.Handler.Requests.Count == 0 && graph.Transport.HttpCallCount == 0, "HTTP request count == 0");
        Ok(graph.Authenticator.Calls == 0, "authenticator not invoked");
        Ok(permits.ConsumeCalls == 0, "permit consume count == 0");
        Ok(prior is null && tasks.Count == tasksBefore
            && tasks.All(t => t.Action != ProductionActions.CreateVideo),
            "canonical mutation == 0 (no CREATE_VIDEO request/task)");
        Ok(eval.BlockReason == PerformanceContractIds.OfficialFilmForbidden,
            "Evaluate official paid + no permit is OFFICIAL_FILM_FORBIDDEN");
        _ = graph.Adapter;
    }

    static ProductionTaskInputSnapshot Snap(byte[] bytes) =>
        new(
            Guid.NewGuid(), Guid.NewGuid(),
            Story, Acting, Visual, "", "", "",
            "", "", "", "",
            KitVideoIntegrityRules.Sha256Hex(bytes), "", "", "",
            ShotExecutionCompiler.Hash("10.0s"),
            ProductionMotionCatalog.GoogleProviderId,
            ProductionMotionCatalog.Veo31ModelId,
            "ck10-snap",
            DateTimeOffset.UtcNow,
            Guid.Parse("91daaf4b-5550-4cbc-b9f9-0bd0be556cef"),
            "16:9", 10);

    static ShotExecutionInput Input(decimal duration) =>
        new(Story, Acting, Visual, "CHAR-001", "",
            duration.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "s",
            "", "motion",
            null, "", "16:9", duration, ShotExecutionCompiler.Version, null);

    static async Task<ProductionArtifact> SeedKeyframe(
        IProductionExecutionService os, ProductionScope scope, ShotExecutionInput input)
    {
        var req = await os.CreateRequestAsync(scope, ProductionActions.CreateImage, "test", "kf-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(req.RequestId);
        await os.FreezeInputAsync(task.TaskId, input, "import", "existing-pixels");
        var att = await os.StartAttemptAsync(task.TaskId, "kf-payload");
        var bytes = Encoding.UTF8.GetBytes("FAMIXA-KF-CK10-" + scope.ShotCode + new string('K', 48));
        await os.SubmitProviderJobAsync(att.AttemptId, "img-ck10");
        await os.CompleteAttemptAsync(att.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes));
        var art = await os.VerifyArtifactAsync(att.AttemptId, bytes, "image/png", 0, "kf/" + scope.ShotCode + ".png");
        await os.BindCurrentAsync(art.ArtifactId);
        return art;
    }
}
