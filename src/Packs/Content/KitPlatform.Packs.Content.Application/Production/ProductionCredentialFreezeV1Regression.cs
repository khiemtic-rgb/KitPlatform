using System.Reflection;
using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-FREEZE-01. Pins CredentialId and CredentialVersionId on the frozen snapshot.
/// No provider call. No latest-version rule. No historical backfill.
/// </summary>
public static class ProductionCredentialFreezeV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_FREEZE_01";
    const string Plain = "gate-d-plain-value";

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
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var inner = new ProductionCredentialResolver(identities, secrets, caller);
        var seen = new List<(Guid CredentialId, Guid VersionId)>();
        var resolver = new RecordingResolver(inner, seen);
        var store = new InMemoryProductionWorkStore();
        var engine = new ProductionWorkExecutionEngine(store, credentials: resolver);

        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "tenant-a-freeze");
        await identities.CreateAsync(credential);
        var version = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(version);
        await secrets.PutAsync(version.CredentialVersionId, Plain);
        var later = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 2, "runway", "gen4_turbo");
        await identities.AddVersionAsync(later);
        await secrets.PutAsync(later.CredentialVersionId, "gate-d-later-value");

        var reference = new ProductionCredentialReference(credential.CredentialId, version.CredentialVersionId);
        var frozen = await Freeze(engine, reference);
        Ok(frozen.CredentialId == credential.CredentialId, "T1 freeze stores CredentialId");
        Ok(frozen.CredentialVersionId == version.CredentialVersionId, "T1 freeze stores CredentialVersionId");
        Ok(frozen.CredentialId != Guid.Empty && frozen.CredentialVersionId != Guid.Empty, "T2 both identities are present");
        Ok(!frozen.ToString().Contains(Plain, StringComparison.Ordinal), "T4 snapshot text hides the secret");
        foreach (var property in typeof(ProductionTaskInputSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var name = property.Name;
            Ok(!name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("Cipher", StringComparison.OrdinalIgnoreCase)
               && !name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
               && name != "Key",
                "T4 property " + name + " is not secret material");
            var value = property.GetValue(frozen)?.ToString() ?? "";
            Ok(!value.Contains(Plain, StringComparison.Ordinal), "T5 " + name + " does not contain the secret");
        }

        var without = frozen with { CredentialId = null, CredentialVersionId = null };
        Ok(LegacyHash(without) == ShotExecutionCompiler.SnapshotHash(without),
            "T6 historical hash stays stable when credential ids are absent");
        Ok(ShotExecutionCompiler.SnapshotHash(frozen) != LegacyHash(frozen),
            "T6 credential identity participates in the snapshot hash");
        var otherVersion = frozen with { CredentialVersionId = later.CredentialVersionId };
        Ok(ShotExecutionCompiler.SnapshotHash(otherVersion) != frozen.SnapshotHash,
            "T6 CredentialVersion B hashes differently from A");
        var otherCredential = frozen with { CredentialId = Guid.NewGuid() };
        Ok(ShotExecutionCompiler.SnapshotHash(otherCredential) != frozen.SnapshotHash,
            "T6 CredentialId change changes the hash");

        var replaced = false;
        try
        {
            await store.InsertSnapshotAsync(frozen with
            {
                SnapshotId = Guid.NewGuid(),
                CredentialId = later.CredentialId == credential.CredentialId ? Guid.NewGuid() : later.CredentialId,
                CredentialVersionId = later.CredentialVersionId,
            });
        }
        catch (InvalidOperationException ex) when (ex.Message == "SNAPSHOT_IMMUTABLE")
        {
            replaced = true;
        }
        var loaded = await store.GetSnapshotByTaskAsync(frozen.TaskId);
        Ok(replaced && loaded?.CredentialId == credential.CredentialId
           && loaded.CredentialVersionId == version.CredentialVersionId
           && loaded.SnapshotHash == frozen.SnapshotHash,
            "T7 T8 frozen credential identity cannot be replaced");

        caller.Id = tenantB;
        var foreign = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantB, "tenant-b");
        await identities.CreateAsync(foreign);
        var foreignVersion = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), foreign.CredentialId, tenantB, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(foreignVersion);
        await secrets.PutAsync(foreignVersion.CredentialVersionId, "gate-d-foreign");
        caller.Id = tenantA;
        Ok(await Fails(engine, ProductionCredentialSecretProtector.SecretUnavailable,
                new ProductionCredentialReference(foreign.CredentialId, foreignVersion.CredentialVersionId)),
            "T9 wrong-tenant credential fails at freeze");

        var otherCredentialRow = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "other-credential");
        await identities.CreateAsync(otherCredentialRow);
        var otherOwnedVersion = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), otherCredentialRow.CredentialId, tenantA, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(otherOwnedVersion);
        await secrets.PutAsync(otherOwnedVersion.CredentialVersionId, "gate-d-other-credential");
        Ok(await Fails(engine, ProductionCredentialSecretProtector.SecretUnavailable,
                new ProductionCredentialReference(credential.CredentialId, otherOwnedVersion.CredentialVersionId)),
            "T10 wrong credential version fails at freeze");

        var revoked = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "revoked") with
        {
            Status = ProductionCredentialStatuses.Revoked,
        };
        await identities.CreateAsync(revoked with { CredentialId = revoked.CredentialId });
        var revokedVersion = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), revoked.CredentialId, tenantA, 1, "runway", "gen4_turbo");
        await identities.AddVersionAsync(revokedVersion);
        await secrets.PutAsync(revokedVersion.CredentialVersionId, "gate-d-revoked");
        Ok(await Fails(engine, ProductionCredentialResolver.Revoked,
                new ProductionCredentialReference(revoked.CredentialId, revokedVersion.CredentialVersionId)),
            "T11 inactive credential fails");

        var inactiveVersion = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 3, "runway", "gen4_turbo") with
        {
            Status = ProductionCredentialStatuses.Revoked,
        };
        await identities.AddVersionAsync(inactiveVersion);
        await secrets.PutAsync(inactiveVersion.CredentialVersionId, "gate-d-inactive-version");
        Ok(await Fails(engine, ProductionCredentialResolver.Revoked,
                new ProductionCredentialReference(credential.CredentialId, inactiveVersion.CredentialVersionId)),
            "T12 inactive credential version fails");

        Ok(await Fails(engine, ProductionCredentialResolver.ProviderMismatch,
                reference, "fal", "gen4_turbo"),
            "T13 provider mismatch fails");
        Ok(await Fails(engine, ProductionCredentialResolver.ModelMismatch,
                reference, "runway", "other-model"),
            "T14 model mismatch fails");

        var beforeMissing = await store.GetSnapshotByTaskAsync(
            (await engine.CreateTaskAsync((await engine.CreateRequestAsync(
                Scope(), ProductionActions.CreateVideo, "gate-d", "idem-missing-" + Guid.NewGuid().ToString("N"))).RequestId)).TaskId);
        Ok(await Fails(engine, ProductionCredentialResolver.Unresolved, null),
            "T15 missing credential reference fails closed");
        Ok(beforeMissing is null, "T15 missing reference does not create a snapshot");

        var brokenCaller = new SwitchCaller { Id = tenantA };
        var brokenStore = new InMemoryProductionWorkStore();
        var brokenEngine = new ProductionWorkExecutionEngine(
            brokenStore,
            credentials: new ProductionCredentialResolver(identities, new BrokenSecrets(), brokenCaller));
        var brokenRequest = await brokenEngine.CreateRequestAsync(
            Scope(), ProductionActions.CreateVideo, "gate-d", "idem-cipher-" + Guid.NewGuid().ToString("N"));
        var brokenTask = await brokenEngine.CreateTaskAsync(brokenRequest.RequestId);
        var cipherFailed = false;
        try
        {
            await brokenEngine.FreezeInputAsync(brokenTask.TaskId, Input(), "runway", "gen4_turbo", credential: reference);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.CiphertextInvalid
            && !ex.ToString().Contains(Plain, StringComparison.Ordinal))
        {
            cipherFailed = true;
        }
        Ok(cipherFailed && await brokenStore.GetSnapshotByTaskAsync(brokenTask.TaskId) is null,
            "T15 ciphertext that cannot be decrypted does not freeze");

        var historicalTask = await engine.CreateTaskAsync(
            (await engine.CreateRequestAsync(Scope(), ProductionActions.CreateVideo, "gate-d", "idem-hist-" + Guid.NewGuid().ToString("N"))).RequestId);
        var historical = without with
        {
            SnapshotId = Guid.NewGuid(),
            TaskId = historicalTask.TaskId,
            SnapshotHash = ShotExecutionCompiler.SnapshotHash(without with { TaskId = historicalTask.TaskId }),
        };
        historical = historical with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(historical) };
        await store.InsertSnapshotAsync(historical);
        var again = await Freeze(engine, reference);
        var historicalLoaded = await store.GetSnapshotByTaskAsync(historical.TaskId);
        Ok(historicalLoaded?.CredentialId is null && historicalLoaded?.CredentialVersionId is null
           && again.TaskId != historical.TaskId,
            "T16 historical snapshot is not backfilled");

        seen.Clear();
        var firstAttempt = await engine.StartAttemptAsync(frozen.TaskId, frozen.SnapshotHash);
        var secondAttempt = await engine.StartAttemptAsync(frozen.TaskId, frozen.SnapshotHash);
        Ok(firstAttempt.AttemptNumber == 1 && secondAttempt.AttemptNumber == 2, "T17 retry opens another attempt");
        Ok(seen.Count >= 2 && seen.All(call => call.CredentialId == credential.CredentialId && call.VersionId == version.CredentialVersionId),
            "T17 T18 retry resolves the frozen credential and version");
        Ok(seen.All(call => call.VersionId != later.CredentialVersionId),
            "T19 retry does not choose the later version");

        var transport = new RecordingRunwayTransport();
        var adapter = new RunwayMotionExecutionAdapter(transport);
        var started = await adapter.StartFrozenAsync(frozen, new MotionStartContext(null, null, firstAttempt.AttemptId), default);
        var retried = await adapter.StartFrozenAsync(loaded!, new MotionStartContext(null, null, secondAttempt.AttemptId), default);
        Ok(started.ProviderJobId.Length > 0 && retried.ProviderJobId == started.ProviderJobId
           && transport.Seen?.CredentialId == credential.CredentialId
           && transport.Seen.CredentialVersionId == version.CredentialVersionId
           && transport.HttpCallCount == 0,
            "T17 T21 retry passes the frozen reference and does not call a provider");

        var root = RepoRoot();
        var engineSource = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionWorkExecutionEngine.cs"));
        var adapterSource = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "Providers", "RunwayMotionExecutionAdapter.cs"));
        var actions = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCreateVideoService.Actions.cs"));
        var migration = File.ReadAllText(Path.Combine(root, "migrations", "379_pack_content_production_credential_freeze.sql"));
        Ok(!engineSource.Contains("org_settings", StringComparison.Ordinal)
           && !adapterSource.Contains("org_settings", StringComparison.Ordinal)
           && !actions.Contains("org_settings", StringComparison.Ordinal),
            "T20 freeze and retry do not read org_settings");
        Ok(!engineSource.Contains("GetEnvironmentVariable", StringComparison.Ordinal)
           && !adapterSource.Contains("RUNWAY_API_KEY", StringComparison.Ordinal)
           && !adapterSource.Contains("GetEnvironmentVariable", StringComparison.Ordinal),
            "T21 retry does not use environment provider keys");
        Ok(actions.Contains("GetSnapshotByTaskAsync", StringComparison.Ordinal)
           && actions.Contains("StartAttemptAsync", StringComparison.Ordinal),
            "T17 retry reads the frozen snapshot");
        Ok(!migration.Contains("UPDATE pack_content.production_task_input_snapshots", StringComparison.Ordinal),
            "T16 migration does not backfill historical snapshots");
        Ok(migration.Contains("credential_id", StringComparison.Ordinal)
           && migration.Contains("credential_version_id", StringComparison.Ordinal)
           && !migration.Contains("secret_ciphertext", StringComparison.Ordinal),
            "T5 schema adds only the non-secret references");

        Ok(ProductionCredentialDomainV1Regression.Run().Count == 0, "T22 gate A");
        Ok(ProductionCredentialSecretV1Regression.Run().Count == 0, "T23 gate B");
        Ok(ProductionCredentialResolutionV1Regression.Run().Count == 0, "T24 gate C");
        Ok(transport.HttpCallCount == 0, "T25 no provider jobs");
    }

    static async Task<ProductionTaskInputSnapshot> Freeze(
        ProductionWorkExecutionEngine engine, ProductionCredentialReference reference, string provider = "runway", string model = "gen4_turbo")
    {
        var request = await engine.CreateRequestAsync(
            Scope(), ProductionActions.CreateVideo, "gate-d", "idem-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        return await engine.FreezeInputAsync(task.TaskId, Input(), provider, model, credential: reference);
    }

    static async Task<bool> Fails(
        ProductionWorkExecutionEngine engine, string code, ProductionCredentialReference? reference,
        string provider = "runway", string model = "gen4_turbo")
    {
        try
        {
            await Freeze(engine, reference ?? new ProductionCredentialReference(Guid.Empty, Guid.Empty), provider, model);
            if (reference is null)
                await engine.FreezeInputAsync(
                    (await engine.CreateTaskAsync((await engine.CreateRequestAsync(
                        Scope(), ProductionActions.CreateVideo, "gate-d", "idem-empty-" + Guid.NewGuid().ToString("N"))).RequestId)).TaskId,
                    Input(), provider, model);
            return false;
        }
        catch (InvalidOperationException ex) when (ex.Message == code && !ex.ToString().Contains(Plain, StringComparison.Ordinal))
        {
            return true;
        }
    }

    static string LegacyHash(ProductionTaskInputSnapshot snap)
    {
        var parts = new List<string>
        {
            snap.StoryHash, snap.ActingHash, snap.VisualHash, snap.CompilerHash,
            snap.KeyframeHash, snap.VoiceHash, snap.MotionHash, snap.LipsyncSourceHash,
            snap.TimingHash, snap.ProviderId, snap.ModelId,
            snap.KeyframeArtifactId?.ToString("N") ?? "",
            snap.AspectRatio, snap.DurationSec.ToString("0.###"), snap.CompilerVersion,
        };
        if (snap.PerformanceSnapshotId is not null || !string.IsNullOrWhiteSpace(snap.PerformanceHash))
        {
            parts.Add(snap.PerformanceSnapshotId?.ToString("D") ?? "");
            parts.Add(snap.PerformanceHash ?? "");
        }
        return ShotExecutionCompiler.Hash(string.Join('|', parts));
    }

    static ProductionScope Scope() =>
        new(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH01");

    static ShotExecutionInput Input() =>
        new("Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.", "He stays in place.", "sofa 16:9", "CHAR-001", "kf-d", "5s", "", "hold");

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

    sealed class RecordingResolver : IProductionCredentialResolver
    {
        readonly IProductionCredentialResolver _inner;
        readonly List<(Guid CredentialId, Guid VersionId)> _seen;

        public RecordingResolver(IProductionCredentialResolver inner, List<(Guid CredentialId, Guid VersionId)> seen)
        {
            _inner = inner;
            _seen = seen;
        }

        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            _seen.Add((credentialId, credentialVersionId));
            return _inner.ResolveAsync(credentialId, credentialVersionId, providerId, modelId, ct);
        }
    }

    sealed class RecordingRunwayTransport : IRunwayTransport
    {
        public ProductionCredentialReference? Seen { get; private set; }
        public bool RequiresNetwork => false;
        public int HttpCallCount => 0;

        public Task<RunwayTransportCreateResult> CreateImageToVideoAsync(RunwayTransportCreateRequest request, CancellationToken ct = default)
        {
            Seen = request.Credential;
            return Task.FromResult(new RunwayTransportCreateResult("local-frozen-" + request.TaskId.ToString("N")));
        }

        public Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Succeeded));

        public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
    }

    sealed class BrokenSecrets : IProductionCredentialSecretStore
    {
        public Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default) =>
            throw new InvalidOperationException(ProductionCredentialSecretProtector.CiphertextInvalid);
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
