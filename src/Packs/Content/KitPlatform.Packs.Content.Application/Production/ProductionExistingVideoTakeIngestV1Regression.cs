using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionExistingVideoTakeIngestV1Regression
{
    public const string SuiteId = "FAMIXA_EXISTING_VIDEO_TAKE_INGEST_V1";

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
        var tenantA = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var tenantB = Guid.Parse("22222222-2222-4222-8222-222222222222");
        var caller = new SwitchTenant { Id = tenantA };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var motion = new GuardMotion();
        var sourceId = Guid.Parse("b22370a9-6ddd-4b04-a281-c7498cc926ae");
        var bytes = ProductionTestMp4.Encode("existing-video-take-ingest", 5);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var held = new UnattributedVideoTake(
            sourceId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime, sha, 5, "runway", "gen4_turbo", bytes);
        var source = new FakeSource(held);
        var os = new ProductionExecutionService(store, motion);
        var svc = new ProductionCreateVideoService(os, store, files, motion, historicalVideoTakes: source);
        var build = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        store.RegisterBuild(build, tenantA);
        var scope = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");

        var art = await svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(scope, sourceId, "test"), default);
        Ok(art.ArtifactId != sourceId, "A new artifact id");
        Ok(art.ArtifactId != Guid.Empty, "A artifact id assigned");
        Ok(art.Role == ProductionArtifactRoles.VideoTake, "B role VIDEO_TAKE");
        Ok(art.VerificationStatus == ProductionArtifactStatuses.Verified, "E VERIFIED");
        Ok(string.Equals(art.ByteSha256, sha, StringComparison.OrdinalIgnoreCase), "D SHA256 preserved");
        Ok(art.StorageKey.StartsWith(ProductionTenantBoundary.Prefix(tenantA) + "/os/", StringComparison.OrdinalIgnoreCase), "C tenant-scoped storage key");
        Ok(!art.StorageKey.Contains("b22370a9", StringComparison.OrdinalIgnoreCase), "C storage key is not the historical id");
        var bind = await store.GetActiveBindingAsync(build, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake));
        Ok(bind is not null && bind.ArtifactId == art.ArtifactId, "F CURRENT:VIDEO_TAKE");
        var task = await store.GetTaskAsync(art.TaskId);
        Ok(task is not null && task.Scope.BuildId == build && task.ProviderId == "import" && task.ModelId == "existing-video", "provider import/existing-video");
        var snap = task is null ? null : await store.GetSnapshotByTaskAsync(task.TaskId);
        Ok(snap is not null && snap.ShotContract.Contains(sourceId.ToString("D"), StringComparison.Ordinal), "provenance names source artifact");
        Ok(snap is not null && snap.ParentArtifactIds is null, "no parent artifact link invented");
        var preview = await svc.GetPreviewAsync(scope);
        Ok(!preview.IsEmpty && preview.ArtifactId == art.ArtifactId && preview.StorageKey == art.StorageKey, "preview resolves new artifact");
        var media = await svc.ReadPreviewMediaAsync(scope);
        Ok(media is not null && KitVideoIntegrityRules.Sha256Hex(media) == art.ByteSha256, "preview bytes match SHA");
        Ok(motion.Calls == 0, "J motion adapter not called");
        Ok(source.Reads == 1, "source read once");

        var again = await svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(scope, sourceId, "test"), default);
        Ok(again.ArtifactId == art.ArtifactId, "idempotent replay returns the same artifact");
        Ok(source.Reads == 1, "replay does not read the source again");
        var tasks = await store.ListTasksByScopeAsync(build, scope.ShotCode);
        Ok(tasks.Count == 1, "replay does not create a second task");

        caller.Id = tenantB;
        Ok(await store.GetArtifactAsync(art.ArtifactId) is null, "H tenant B cannot read the artifact");
        var hidden = await svc.GetPreviewAsync(scope);
        Ok(hidden.IsEmpty, "H tenant B preview is empty");
        Ok(await svc.ReadPreviewMediaAsync(scope) is null, "H tenant B cannot read preview bytes");
        caller.Id = tenantA;

        Ok(held.ArtifactId == sourceId && held.ByteSha256 == sha && held.Bytes.Length == bytes.Length, "I source record unchanged");

        var missing = new FakeSource(null);
        var svcMissing = new ProductionCreateVideoService(os, store, files, motion, historicalVideoTakes: missing);
        var otherBuild = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        store.RegisterBuild(otherBuild, tenantA);
        var other = new ProductionScope(otherBuild, "EP01", "SC01", "EP01-SC01-SH01");
        Ok(await Throws(ProductionFailureReasons.OutputArtifactNotFound, () =>
            svcMissing.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(other, Guid.NewGuid(), "test"), default)),
            "K missing source");

        var badSha = held with { ByteSha256 = new string('a', 64) };
        var svcBad = new ProductionCreateVideoService(os, store, files, motion, historicalVideoTakes: new FakeSource(badSha));
        Ok(await Throws(ProductionFailureReasons.FailedIdentity, () =>
            svcBad.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(other, sourceId, "test"), default)),
            "K sha mismatch rejected before a task");
        Ok((await store.ListTasksByScopeAsync(otherBuild, other.ShotCode)).Count == 0, "K sha mismatch writes nothing");

        var junk = new byte[64];
        var junkSha = KitVideoIntegrityRules.Sha256Hex(junk);
        var junkTake = held with { Bytes = junk, ByteSha256 = junkSha, ArtifactId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc") };
        var junkScope = new ProductionScope(otherBuild, "EP01", "SC01", "EP01-SC01-SH02");
        var svcJunk = new ProductionCreateVideoService(os, store, files, motion, historicalVideoTakes: new FakeSource(junkTake));
        Ok(await Throws(ProductionFailureReasons.FailedVerification, () =>
            svcJunk.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(junkScope, junkTake.ArtifactId, "test"), default)),
            "K undecodable bytes fail official verification");
        Ok(await store.GetActiveBindingAsync(otherBuild, junkScope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake)) is null,
            "K failed verification does not bind CURRENT:VIDEO_TAKE");

        var unregistered = new ProductionScope(Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd"), "EP01", "SC01", "EP01-SC01-SH01");
        Ok(await Throws(ProductionTenantBoundary.Unattributed, () =>
            svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(unregistered, sourceId, "test"), default)),
            "G NULL build tenant rejected");
        store.RegisterBuild(unregistered.BuildId, tenantB);
        Ok(await Throws(ProductionTenantBoundary.Forbidden, () =>
            svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(unregistered, sourceId, "test"), default)),
            "L other tenant build rejected");
        var wrongEpisode = new ProductionScope(otherBuild, "EP99", "SC01", "EP99-SC01-SH01");
        Ok(await Throws(ProductionFailureReasons.MissingPerformanceSnapshot, () =>
            svc.RegisterExistingVideoTakeAsync(new RegisterExistingVideoTakeCommand(wrongEpisode, sourceId, "test"), default)),
            "L non-EP01 target rejected before a write");
        Ok(motion.Calls == 0, "J still no provider call");
    }

    static async Task<bool> Throws(string message, Func<Task> work)
    {
        try
        {
            await work();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == message;
        }
    }

    sealed class SwitchTenant : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class FakeSource(UnattributedVideoTake? take) : IUnattributedVideoTakeSource
    {
        public int Reads;
        public Task<UnattributedVideoTake?> ReadAsync(Guid artifactId, CancellationToken ct = default)
        {
            Reads++;
            if (take is null || take.ArtifactId != artifactId) return Task.FromResult<UnattributedVideoTake?>(null);
            return Task.FromResult<UnattributedVideoTake?>(take);
        }
    }

    sealed class GuardMotion : IMotionExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "runway";
        public string ModelId => "gen4_turbo";
        public bool IsPaid => true;
        public bool RequiresNetwork => true;
        public Task<MotionStartResult> StartFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionStartResult("no"));
        }
        public Task<MotionPollResult> PollFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        }
        public Task<MotionPollResult> RecoverFrozenAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionPollResult(MotionPollStatuses.Failed, providerJobId));
        }
        public Task<MotionAdapterResult> FetchOutputAsync(ProductionTaskInputSnapshot snapshot, string providerJobId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionAdapterResult(providerJobId, [], "video/mp4", 0, "os/no"));
        }
        public Task<MotionAdapterResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new MotionAdapterResult("no", [], "video/mp4", 0, "os/no"));
        }
    }
}
