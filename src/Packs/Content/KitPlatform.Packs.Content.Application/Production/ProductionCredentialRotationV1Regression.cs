using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-ROTATION-01. A new version does not rewrite a frozen execution.
/// Revoked versions fail closed. They are not replaced by another version.
/// </summary>
public static class ProductionCredentialRotationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_ROTATION_01";
    const string SecretV1 = "gate-e-version-one";
    const string SecretV2 = "gate-e-version-two";

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
        var rotation = new InMemoryProductionCredentialRotation(identities, secrets, ring);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "tenant-a-rotation");
        await identities.CreateAsync(credential);

        var version1Id = Guid.NewGuid();
        var created1 = await rotation.RotateAsync(
            credential.CredentialId, version1Id, "runway", "gen4_turbo", SecretV1);
        Ok(created1.CredentialVersionId == version1Id && created1.Version == 1
           && created1.Status == ProductionCredentialStatuses.Active,
            "T1 tenant creates V1");
        var v1Envelope = Copy(secrets.EnvelopeForTests(version1Id));
        var v1Before = await identities.GetVersionAsync(version1Id);

        var version2Id = Guid.NewGuid();
        var created2 = await rotation.RotateAsync(
            credential.CredentialId, version2Id, "runway", "gen4_turbo", SecretV2);
        var v1After = await identities.GetVersionAsync(version1Id);
        Ok(created2.CredentialVersionId == version2Id && created2.Version == 2,
            "T2 tenant creates V2");
        Ok(created1.CredentialVersionId != created2.CredentialVersionId
           && created1.Version != created2.Version,
            "T3 V1 and V2 have different immutable version identities");
        Ok(v1Envelope is not null && Same(v1Envelope, secrets.EnvelopeForTests(version1Id)),
            "T4 V1 ciphertext remains unchanged");
        Ok(v1Before is not null && v1After is not null
           && v1After.CredentialId == v1Before.CredentialId
           && v1After.TenantId == v1Before.TenantId
           && v1After.Version == v1Before.Version
           && v1After.ProviderId == v1Before.ProviderId
           && v1After.ModelId == v1Before.ModelId
           && v1After.Status == ProductionCredentialStatuses.Superseded
           && created2.Status == ProductionCredentialStatuses.Active,
            "T5 V1 is superseded and V2 is the only active version");
        Ok(created2.CredentialId == credential.CredentialId && created1.CredentialId == credential.CredentialId,
            "T6 V2 belongs to the same credential");
        Ok(created2.TenantId == tenantA && created1.TenantId == tenantA,
            "T7 V2 belongs to the same tenant");

        caller.Id = tenantB;
        var foreignId = Guid.NewGuid();
        Ok(await Throws(ProductionCredentialOwnership.VersionCredentialRequired, () =>
                rotation.RotateAsync(credential.CredentialId, foreignId, "runway", "gen4_turbo", "foreign")),
            "T8 tenant B cannot rotate tenant A credential");
        Ok(await identities.GetVersionAsync(foreignId) is null,
            "T9 tenant B cannot create a version under tenant A credential");
        Ok(await Throws(ProductionCredentialSecretProtector.SecretUnavailable, () =>
                rotation.RevokeVersionAsync(version2Id)),
            "T8 tenant B cannot revoke a tenant A version");
        caller.Id = tenantA;
        Ok((await identities.GetVersionAsync(version2Id))!.Status == ProductionCredentialStatuses.Active,
            "T8 tenant B revoke leaves the tenant A version unchanged");
        Ok(await secrets.OpenAsync(version1Id) == SecretV1 && await secrets.OpenAsync(version2Id) == SecretV2,
            "T9 owning tenant still opens its own versions");

        var seen = new List<(Guid CredentialId, Guid VersionId)>();
        var resolver = new RecordingResolver(new ProductionCredentialResolver(identities, secrets, caller), seen);
        var work = new InMemoryProductionWorkStore();
        var engine = new ProductionWorkExecutionEngine(work, credentials: resolver);
        var reference1 = new ProductionCredentialReference(credential.CredentialId, version1Id);
        var frozen = await Freeze(engine, reference1);
        var hash = frozen.SnapshotHash;
        Ok(frozen.CredentialId == credential.CredentialId && frozen.CredentialVersionId == version1Id,
            "T12 existing execution is frozen at V1");

        seen.Clear();
        var retried = await engine.StartAttemptAsync(frozen.TaskId, frozen.SnapshotHash);
        var loaded = await work.GetSnapshotByTaskAsync(frozen.TaskId);
        Ok(retried.AttemptNumber == 1
           && seen.Count == 1
           && seen[0].CredentialId == credential.CredentialId
           && seen[0].VersionId == version1Id
           && seen[0].VersionId != version2Id
           && loaded?.CredentialVersionId == version1Id
           && loaded.SnapshotHash == hash,
            "T13 T14 retry uses V1 and does not change the frozen hash");

        var reference2 = new ProductionCredentialReference(credential.CredentialId, version2Id);
        var newer = await Freeze(engine, reference2);
        Ok(newer.CredentialId == credential.CredentialId
           && newer.CredentialVersionId == version2Id
           && newer.SnapshotHash != hash
           && (await work.GetSnapshotByTaskAsync(frozen.TaskId))!.CredentialVersionId == version1Id
           && (await work.GetSnapshotByTaskAsync(frozen.TaskId))!.SnapshotHash == hash,
            "T11 new execution explicitly selects V2 while execution A stays on V1");

        var historicalTask = await engine.CreateTaskAsync(
            (await engine.CreateRequestAsync(Scope(), ProductionActions.CreateVideo, "gate-e", "idem-hist-" + Guid.NewGuid().ToString("N"))).RequestId);
        var historical = frozen with
        {
            SnapshotId = Guid.NewGuid(),
            TaskId = historicalTask.TaskId,
            CredentialId = null,
            CredentialVersionId = null,
        };
        historical = historical with { SnapshotHash = ShotExecutionCompiler.SnapshotHash(historical) };
        await work.InsertSnapshotAsync(historical);
        var historicalLoaded = await work.GetSnapshotByTaskAsync(historicalTask.TaskId);
        Ok(historicalLoaded is { CredentialId: null, CredentialVersionId: null }
           && historicalLoaded.SnapshotHash == historical.SnapshotHash,
            "T15 historical snapshot is not updated");

        var transport = new RecordingRunwayTransport();
        var started = await new RunwayMotionExecutionAdapter(transport).StartFrozenAsync(
            loaded!, new MotionStartContext(null, null, retried.AttemptId), default);
        Ok(started.ProviderJobId.Length > 0
           && transport.Seen?.CredentialVersionId == version1Id
           && transport.HttpCallCount == 0,
            "T24 provider boundary receives the frozen version and executes no job");

        seen.Clear();
        await rotation.RevokeVersionAsync(version2Id);
        Ok(await Throws(ProductionCredentialResolver.Revoked, () =>
                Freeze(engine, reference2)),
            "T10 revoked current version cannot be selected for a new execution");
        seen.Clear();
        var kept = await engine.StartAttemptAsync(frozen.TaskId, frozen.SnapshotHash);
        var afterV2Revoke = await work.GetSnapshotByTaskAsync(frozen.TaskId);
        Ok(kept.AttemptNumber > 1
           && seen.Count >= 1
           && seen.All(call => call.VersionId == version1Id)
           && afterV2Revoke?.CredentialVersionId == version1Id
           && afterV2Revoke.SnapshotHash == hash,
            "T13 revoking V2 leaves the frozen V1 retry on V1");
        await rotation.RevokeVersionAsync(version1Id);
        seen.Clear();
        var revokedRetry = false;
        try
        {
            await engine.StartAttemptAsync(frozen.TaskId, frozen.SnapshotHash);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialResolver.Revoked
            && !ex.ToString().Contains(SecretV1, StringComparison.Ordinal)
            && !ex.ToString().Contains(SecretV2, StringComparison.Ordinal))
        {
            revokedRetry = true;
        }
        var afterRevoke = await work.GetSnapshotByTaskAsync(frozen.TaskId);
        Ok(revokedRetry
           && afterRevoke?.CredentialVersionId == version1Id
           && afterRevoke.SnapshotHash == hash
           && seen.All(call => call.VersionId != version2Id),
            "T13 revoking the frozen version fails closed and does not retarget the snapshot");

        var missed = Guid.NewGuid();
        Ok(await Throws(ProductionCredentialSecretProtector.KeyMissing, () =>
                new InMemoryProductionCredentialRotation(
                    identities, secrets, new MapProductionCredentialKeyRing(null, new Dictionary<string, byte[]>()))
                    .RotateAsync(credential.CredentialId, missed, "runway", "gen4_turbo", SecretV2)),
            "T19 encryption failure creates no version");
        Ok(await identities.GetVersionAsync(missed) is null, "T19 no partial version remains");
        Ok(!created2.ToString().Contains(SecretV2, StringComparison.Ordinal)
           && !(secrets.EnvelopeForTests(version2Id)?.ToString().Contains(SecretV2, StringComparison.Ordinal) ?? false),
            "T19 plaintext secret is not stored on the version identity");

        var root = RepoRoot();
        var rotationSource = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCredentialRotation.cs"));
        var repositorySource = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "ProductionCredentialRotationRepository.cs"));
        var combined = rotationSource + repositorySource;
        Ok(!combined.Contains("GetLatestCredentialVersion", StringComparison.Ordinal)
           && !combined.Contains("ORDER BY version", StringComparison.OrdinalIgnoreCase)
           && !combined.Contains("org_settings", StringComparison.Ordinal)
           && !combined.Contains("GetEnvironmentVariable", StringComparison.Ordinal)
           && !combined.Contains("production_task_input_snapshots", StringComparison.Ordinal),
            "T16 T17 T18 rotation does not select a latest version, legacy settings, or rewrite a snapshot");

        Ok(ProductionCredentialDomainV1Regression.Run().Count == 0, "T20 gate A");
        Ok(ProductionCredentialSecretV1Regression.Run().Count == 0, "T21 gate B");
        Ok(ProductionCredentialResolutionV1Regression.Run().Count == 0, "T22 gate C");
        Ok(ProductionCredentialFreezeV1Regression.Run().Count == 0, "T23 gate D");
        Ok(ProductionCredentialFreezeGapV1Regression.Run().Count == 0, "T23 gate D gap closure");
    }

    static async Task<ProductionTaskInputSnapshot> Freeze(
        ProductionWorkExecutionEngine engine, ProductionCredentialReference reference)
    {
        var request = await engine.CreateRequestAsync(
            Scope(), ProductionActions.CreateVideo, "gate-e", "idem-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        return await engine.FreezeInputAsync(task.TaskId, Input(), "runway", "gen4_turbo", credential: reference);
    }

    static async Task<bool> Throws(string code, Func<Task> action)
    {
        try
        {
            await action();
            return false;
        }
        catch (Exception ex) when (ex.Message == code && !ex.ToString().Contains(SecretV1, StringComparison.Ordinal))
        {
            return true;
        }
    }

    static CopiedEnvelope? Copy(ProductionCredentialSecretEnvelope? envelope) =>
        envelope is null
            ? null
            : new CopiedEnvelope(envelope.KeyId, envelope.Nonce.ToArray(), envelope.Ciphertext.ToArray(), envelope.Tag.ToArray());

    static bool Same(CopiedEnvelope before, ProductionCredentialSecretEnvelope? after) =>
        after is not null
        && before.KeyId == after.KeyId
        && before.Nonce.AsSpan().SequenceEqual(after.Nonce)
        && before.Ciphertext.AsSpan().SequenceEqual(after.Ciphertext)
        && before.Tag.AsSpan().SequenceEqual(after.Tag);

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

    sealed record CopiedEnvelope(string KeyId, byte[] Nonce, byte[] Ciphertext, byte[] Tag);

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class RecordingResolver(IProductionCredentialResolver inner, List<(Guid CredentialId, Guid VersionId)> seen)
        : IProductionCredentialResolver
    {
        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
        {
            seen.Add((credentialId, credentialVersionId));
            return inner.ResolveAsync(credentialId, credentialVersionId, providerId, modelId, ct);
        }
    }

    sealed class RecordingRunwayTransport : IRunwayTransport
    {
        public ProductionCredentialReference? Seen { get; private set; }
        public bool RequiresNetwork => false;
        public int HttpCallCount => 0;

        public Task<RunwayTransportCreateResult> CreateImageToVideoAsync(
            RunwayTransportCreateRequest request, CancellationToken ct = default)
        {
            Seen = request.Credential;
            return Task.FromResult(new RunwayTransportCreateResult("local-frozen-" + request.TaskId.ToString("N")));
        }

        public Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(new RunwayTransportTaskResult(providerJobId, MotionPollStatuses.Succeeded));

        public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());
    }
}
