namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-EXECUTION-RETRY-IDEMPOTENCY-01.
/// One failed official request can have one concurrent provider start.
/// A later retry is allowed only after that attempt has failed.
/// </summary>
public static class ProductionExecutionRetryIdempotencyV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_EXECUTION_RETRY_IDEMPOTENCY_01";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            RunAsync(Ok).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var two = await ConcurrentRetriesAsync(2);
        Ok(two.Starts == 1 && two.InFlight >= 1 && two.Attempts == 2 && two.Snapshots == 1
            && two.SameHash && two.SameProvider,
            "two concurrent retries submit one provider start " + two.Note);
        var three = await ConcurrentRetriesAsync(3);
        Ok(three.Starts == 1 && three.InFlight >= 2 && three.Attempts == 2 && three.Snapshots == 1,
            "three concurrent retries submit one provider start");

        var sequential = await SequentialRetryAsync();
        Ok(sequential.Starts == 2
            && sequential.Attempts == 3
            && sequential.Snapshots == 1
            && sequential.SameHash
            && sequential.SameProvider
            && sequential.SecondAttemptNumber == 3,
            "a failed retry may start one later attempt on the same snapshot " + sequential.Note);

        var credential = await FrozenCredentialRetryAsync();
        Ok(credential.Starts == 0 && credential.SameVersion && credential.SameHash && credential.RetryShape,
            "sequential retry keeps the frozen credential pair");

        var root = RepoRoot();
        var service = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCreateVideoService.cs"));
        var actions = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCreateVideoService.Actions.cs"));
        var begin = service.IndexOf("BeginOfficialProviderStartAsync", StringComparison.Ordinal);
        var start = service.IndexOf("StartFrozenAsync", begin, StringComparison.Ordinal);
        Ok(begin >= 0 && start > begin, "CREATE_VIDEO claims the task before provider start");
        Ok(actions.Contains("BeginOfficialProviderStartAsync", StringComparison.Ordinal)
            && !actions.Contains("StartAttemptAsync(task.TaskId, snapHash ?? frozen.SnapshotHash", StringComparison.Ordinal),
            "failed voice and lipsync retry claims the task before provider execution");
    }

    static async Task<Outcome> ConcurrentRetriesAsync(int callers)
    {
        var harness = await FailedOfficialRequestAsync();
        var command = RetryCommand(harness);
        var results = await Task.WhenAll(Enumerable.Range(0, callers).Select(_ => InvokeAsync(harness.Service, command)));
        var snap = await harness.Store.GetSnapshotByTaskAsync(harness.Task.TaskId);
        var attempts = await harness.Store.ListAttemptsAsync(harness.Task.TaskId);
        var note = string.Join(" | ", results.Where(r => r is not null && r != ProductionFailureReasons.InFlightReplay).Distinct());
        return new Outcome(
            harness.Motion.Starts,
            results.Count(r => r == ProductionFailureReasons.InFlightReplay),
            attempts.Count,
            snap is null ? 0 : 1,
            snap?.SnapshotHash == harness.Frozen.SnapshotHash,
            attempts.All(a => a.ProviderId == harness.Frozen.ProviderId && a.ModelId == harness.Frozen.ModelId),
            attempts.Count == 0 ? 0 : attempts.Max(a => a.AttemptNumber),
            note);
    }

    static async Task<Outcome> SequentialRetryAsync()
    {
        var harness = await FailedOfficialRequestAsync();
        harness.Motion.FailStart = true;
        var first = await InvokeAsync(harness.Service, RetryCommand(harness));
        harness.Motion.FailStart = false;
        var second = await InvokeAsync(harness.Service, RetryCommand(harness));
        var snap = await harness.Store.GetSnapshotByTaskAsync(harness.Task.TaskId);
        var attempts = await harness.Store.ListAttemptsAsync(harness.Task.TaskId);
        return new Outcome(
            harness.Motion.Starts,
            0,
            attempts.Count,
            snap is null ? 0 : 1,
            snap?.SnapshotHash == harness.Frozen.SnapshotHash && first == "provider down" && second is null,
            attempts.All(a => a.ProviderId == harness.Frozen.ProviderId && a.ModelId == harness.Frozen.ModelId),
            attempts.Count == 0 ? 0 : attempts.Max(a => a.AttemptNumber),
            (first ?? "ok") + "/" + (second ?? "ok"));
    }

    static async Task<CredentialOutcome> FrozenCredentialRetryAsync()
    {
        var tenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
        var caller = new FixedCaller(tenant);
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var seen = new List<Guid>();
        var resolver = new RecordingResolver(new ProductionCredentialResolver(identities, secrets, caller), seen);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenant, "retry-idem");
        await identities.CreateAsync(credential);
        var version = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenant, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(version);
        await secrets.PutAsync(version.CredentialVersionId, "retry-idem-secret");
        var reference = new ProductionCredentialReference(credential.CredentialId, version.CredentialVersionId);
        var store = new InMemoryProductionWorkStore();
        var engine = ProductionWorkExecutionEngine.CreateOfficial(store, resolver);
        var scope = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        var request = await engine.CreateRequestAsync(scope, ProductionActions.CreateVideo, "retry", "idem-cred-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        var frozen = await engine.FreezeInputAsync(task.TaskId, Input(), "runway", "gen4_turbo", credential: reference);
        var failed = await engine.StartAttemptAsync(task.TaskId, frozen.SnapshotHash);
        await engine.RecordProviderFailedAsync(failed.AttemptId, "provider down");
        var latest = Guid.NewGuid();
        _ = latest;
        var retry = await engine.BeginOfficialProviderStartAsync(task.TaskId, frozen.SnapshotHash);
        await engine.RecordProviderFailedAsync(retry.AttemptId, "provider down");
        var again = await engine.BeginOfficialProviderStartAsync(task.TaskId, frozen.SnapshotHash);
        var snap = await store.GetSnapshotByTaskAsync(task.TaskId);
        var attempts = await store.ListAttemptsAsync(task.TaskId);
        return new CredentialOutcome(
            0,
            snap?.SnapshotHash == frozen.SnapshotHash
                && snap?.CredentialVersionId == version.CredentialVersionId
                && snap.CredentialVersionId != latest,
            seen.All(id => id == version.CredentialVersionId),
            attempts.Count == 3 && again.AttemptNumber == 3 && again.ProviderId == "runway" && again.ModelId == "gen4_turbo");
    }

    static async Task<Harness> FailedOfficialRequestAsync()
    {
        var store = new InMemoryProductionWorkStore();
        var motion = new CountingMotion();
        var os = new ProductionExecutionService(store, motion);
        var service = new ProductionCreateVideoService(os, store, new InMemoryProductionArtifactFileStore(), motion);
        var scope = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");
        var request = await os.CreateRequestAsync(scope, ProductionActions.CreateVideo, "retry", "idem-" + Guid.NewGuid().ToString("N"));
        var task = await os.CreateTaskAsync(request.RequestId);
        var frozen = await new ProductionWorkExecutionEngine(store).FreezeInputAsync(task.TaskId, Input(), motion.ProviderId, motion.ModelId);
        var attempt = await os.StartAttemptAsync(task.TaskId, frozen.SnapshotHash);
        await os.FailAttemptAsync(attempt.AttemptId, "provider down");
        return new Harness(store, service, motion, scope, request, task, frozen, Input());
    }

    static CreateVideoApiCommand RetryCommand(Harness harness) =>
        new(harness.Scope, harness.Input, "retry", harness.Request.IdempotencyKey, null, null,
            harness.Motion.ProviderId, harness.Motion.ModelId, RetryAttempt: true);

    static async Task<string?> InvokeAsync(ProductionCreateVideoService service, CreateVideoApiCommand command)
    {
        try
        {
            await service.CreateVideoAsync(command);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    static ShotExecutionInput Input() =>
        new("Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.", "He stays in place.", "sofa 16:9", "CHAR-001", "kf-retry", "5s", "", "hold");

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return @"E:\KitPlatform";
    }

    sealed record Harness(
        InMemoryProductionWorkStore Store,
        ProductionCreateVideoService Service,
        CountingMotion Motion,
        ProductionScope Scope,
        ProductionRequest Request,
        ProductionTask Task,
        ProductionTaskInputSnapshot Frozen,
        ShotExecutionInput Input);

    sealed record Outcome(int Starts, int InFlight, int Attempts, int Snapshots, bool SameHash, bool SameProvider, int SecondAttemptNumber, string Note);

    sealed record CredentialOutcome(int Starts, bool SameHash, bool SameVersion, bool RetryShape);

    sealed class FixedCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class RecordingResolver(IProductionCredentialResolver inner, List<Guid> seen) : IProductionCredentialResolver
    {
        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            seen.Add(credentialVersionId);
            return inner.ResolveAsync(credentialId, credentialVersionId, providerId, modelId, ct);
        }
    }

    sealed class CountingMotion : IMotionExecutionAdapter
    {
        readonly FixtureMotionAdapter _inner = new();
        public int Starts;
        public bool FailStart;
        public string ProviderId => _inner.ProviderId;
        public string ModelId => _inner.ModelId;
        public bool IsPaid => false;
        public bool RequiresNetwork => false;

        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => StartFrozenAsync(snapshot, null, ct);

        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, MotionStartContext? context, CancellationToken ct)
        {
            Starts++;
            if (FailStart)
                throw new InvalidOperationException("provider down");
            return ((IMotionExecutionAdapter)_inner).StartFrozenAsync(snapshot, context, ct);
        }

        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => _inner.PollFrozenAsync(snapshot, providerJobId, ct);

        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => _inner.RecoverFrozenAsync(snapshot, providerJobId, ct);

        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
            => _inner.FetchOutputAsync(snapshot, providerJobId, ct);

        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
            => _inner.ExecuteFrozenAsync(snapshot, ct);
    }
}
