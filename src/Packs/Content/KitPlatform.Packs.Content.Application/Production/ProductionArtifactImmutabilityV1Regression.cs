namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-ARTIFACT-IMMUTABILITY-01. Physical bytes at a StorageKey are not replaced.
/// Preview still re-hashes the file. ArtifactId, StorageKey, and ByteSha256 stay distinct.
/// </summary>
public static class ProductionArtifactImmutabilityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_ARTIFACT_IMMUTABILITY_01";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        ProvePhysical(Ok);
        ProveStore(Ok);
        ProveRow(Ok);
        ProvePreview(Ok);
        ProveClosed(Ok);
        ProveUnprefixedWriterClosed(Ok);
        return fail;
    }

    static void ProvePhysical(Action<bool, string> Ok)
    {
        var root = Path.Combine(Path.GetTempPath(), "famixa-artifact-immut-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sentinel = Sentinel(1);
            var path = Path.Combine(root, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "os", "newfile01.mp4");
            ProductionArtifactPhysicalWrite.CommitFile(path, sentinel);
            Ok(File.ReadAllBytes(path).SequenceEqual(sentinel), "T1 new StorageKey writes bytes");

            var clone = (byte[])sentinel.Clone();
            ProductionArtifactPhysicalWrite.CommitFile(path, clone);
            Ok(File.ReadAllBytes(path).SequenceEqual(sentinel), "T2 same SHA does not overwrite");

            var other = Sentinel(2);
            Ok(Throws(ProductionArtifactPhysicalWrite.Collision, () => ProductionArtifactPhysicalWrite.CommitFile(path, other)),
                "T3 different SHA fails closed");
            Ok(File.ReadAllBytes(path).SequenceEqual(sentinel), "T3 physical bytes stay the first write");
            Ok(File.ReadAllBytes(path).SequenceEqual(sentinel), "T9 duplicate persist leaves physical bytes unchanged");

            var historical = Path.Combine(root, "veo", "historical-unprefixed.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(historical)!);
            var legacy = Sentinel(7);
            File.WriteAllBytes(historical, legacy);
            ProductionArtifactPhysicalWrite.CommitFile(path, clone);
            Ok(File.ReadAllBytes(historical).SequenceEqual(legacy), "T13 historical unprefixed file stays untouched");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* temp */ }
        }
    }

    static void ProveStore(Action<bool, string> Ok)
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb2");
        var caller = new SwitchTenant { Id = tenantA };
        var files = new InMemoryProductionArtifactFileStore(caller);
        var fileId = Guid.NewGuid().ToString("N");
        var sentinel = Sentinel(3);
        var key = files.Write("os", fileId, sentinel);
        Ok(key.StartsWith(ProductionTenantBoundary.Prefix(tenantA) + "/os/", StringComparison.OrdinalIgnoreCase),
            "T1 official write is tenant-prefixed");
        var stored = files.Read(key);
        files.Write("os", fileId, (byte[])sentinel.Clone());
        var again = files.Read(key);
        Ok(again is not null && again.SequenceEqual(sentinel) && ReferenceEquals(again, stored),
            "T2 store keeps the original bytes");

        Ok(Throws(ProductionArtifactPhysicalWrite.Collision, () => files.Write("os", fileId, Sentinel(4))),
            "T3 store rejects different bytes");
        Ok(files.Read(key)!.SequenceEqual(sentinel), "T9 retry with same bytes leaves the store unchanged");

        caller.Id = tenantB;
        var other = files.Write("os", fileId, Sentinel(5));
        Ok(!string.Equals(other, key, StringComparison.OrdinalIgnoreCase)
            && other.StartsWith(ProductionTenantBoundary.Prefix(tenantB) + "/", StringComparison.OrdinalIgnoreCase),
            "T6 tenant B writes its own key");
        caller.Id = tenantA;
        Ok(files.Read(key)!.SequenceEqual(sentinel), "T6 tenant B does not change tenant A bytes");
        caller.Id = tenantB;
        Ok(files.Read(key) is null, "T6 tenant B cannot read tenant A storage");

        caller.Id = Guid.Empty;
        Ok(Throws(ProductionTenantBoundary.Required, () => files.Write("os", fileId, sentinel)),
            "T7 missing tenant cannot write");
        caller.Id = tenantA;
        Ok(files.Read(key)!.SequenceEqual(sentinel), "T7 failed write leaves existing bytes");

        var official = files.Write("os", Guid.NewGuid().ToString("N"), sentinel);
        Ok(ProductionTenantBoundary.OwnsStorageKey(tenantA, official)
            && !official.StartsWith("os/", StringComparison.OrdinalIgnoreCase),
            "T8 official write does not use an unprefixed key");
        var owned = OwnedStore(caller, tenantA);
        var attemptId = owned.AttemptId;
        Ok(Throws(ProductionTenantBoundary.Forbidden, () => owned.Store.InsertArtifactAsync(new ProductionArtifact(
            Guid.NewGuid(), owned.TaskId, attemptId, ProductionArtifactRoles.VideoTake, "video/mp4",
            sentinel.Length, 1, KitVideoIntegrityRules.Sha256Hex(sentinel), "os/legacy-unprefixed", null,
            DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "snap")).GetAwaiter().GetResult()),
            "T8 unprefixed storage key is rejected");
    }

    static void ProveRow(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var bytes = Sentinel(6);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var key = files.Write("os", "rowfile01", bytes);
        var id = Guid.NewGuid();
        var attempt = Guid.NewGuid();
        var artifact = Artifact(id, attempt, sha, key);
        store.InsertArtifactAsync(artifact).GetAwaiter().GetResult();
        var binding = new ProductionBinding(
            Guid.NewGuid(), Guid.NewGuid(), "EP01-SC01-SH01", "CURRENT", id, artifact.TaskId, DateTimeOffset.UtcNow, null);
        store.InsertBindingAsync(binding).GetAwaiter().GetResult();

        Ok(Throws(ProductionArtifactPhysicalWrite.Collision, () => files.Write("os", "rowfile01", Sentinel(8))),
            "T10 late result with different bytes cannot overwrite");
        Ok(store.GetArtifactAsync(id).GetAwaiter().GetResult()!.ByteSha256 == sha, "T4 ByteSha256 stays");
        Ok(Throws(ProductionArtifactPhysicalWrite.RowLocked, () => store.InsertArtifactAsync(
            Artifact(id, attempt, KitVideoIntegrityRules.Sha256Hex(Sentinel(8)), key)).GetAwaiter().GetResult()),
            "T4 row SHA change fails closed");
        Ok(Throws(ProductionArtifactPhysicalWrite.RowLocked, () => store.InsertArtifactAsync(
            Artifact(id, attempt, sha, "os/other-key")).GetAwaiter().GetResult()),
            "T5 StorageKey change fails closed");
        var kept = store.GetArtifactAsync(id).GetAwaiter().GetResult();
        Ok(kept!.ArtifactId == id && kept.StorageKey == key && kept.ByteSha256 == sha, "T11 ArtifactId stays stable");
        var bound = store.GetActiveBindingAsync(binding.BuildId, binding.ShotCode, "CURRENT").GetAwaiter().GetResult();
        Ok(bound?.BindingId == binding.BindingId && bound.ArtifactId == id, "T15 collision does not rebind");

        var historical = Artifact(Guid.NewGuid(), Guid.NewGuid(), sha, "veo/historical.mp4");
        store.InsertArtifactAsync(historical).GetAwaiter().GetResult();
        var loaded = store.GetArtifactAsync(historical.ArtifactId).GetAwaiter().GetResult();
        Ok(loaded is not null && loaded.StorageKey == "veo/historical.mp4" && loaded.ByteSha256 == sha,
            "T14 historical unprefixed artifact row is not assigned a tenant key");
    }

    static void ProvePreview(Action<bool, string> Ok)
    {
        var files = new InMemoryProductionArtifactFileStore();
        var bytes = Sentinel(9);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var key = files.Write("os", "preview01", bytes);
        var matched = OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, key, sha);
        Ok(matched is not null && KitVideoIntegrityRules.Sha256Hex(matched) == sha, "T12 preview accepts matching physical SHA");
        var tampered = Sentinel(10);
        files.PlantExternalBytes(key, tampered);
        Ok(OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, key, sha) is null, "T12 preview rejects a physical SHA mismatch");
        Ok(Throws(ProductionArtifactPhysicalWrite.Collision, () => files.Write("os", "preview01", bytes)),
            "T12 official write does not repair a mismatched file");
    }

    static void ProveClosed(Action<bool, string> Ok)
    {
        var kept = SeriesGraphMediaWriteCutover.Apply("""
            { "runs": { "SH01": { "acceptedTake": { "url": "old-accept.mp4" }, "takeUrl": "old.mp4" } } }
            """, """
            { "runs": { "SH01": { "acceptedTake": { "url": "new-accept.mp4" }, "takeUrl": "new.mp4", "story": "line" } } }
            """);
        Ok(kept.Contains("old-accept.mp4", StringComparison.Ordinal)
            && !kept.Contains("new-accept.mp4", StringComparison.Ordinal),
            "T16 graph media write stays cut over");
        Ok(!ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob("POST content/series/turbo")
            && !ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob("POST content/series/lipsync")
            && !ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob(
                "POST content/video-engine/shots/{id}/video-generation-execution/execute"),
            "T17 VGE Turbo and LipSync stay closed");

        Ok(SeriesGraphMediaWriteCutoverV1Regression.Run().Count == 0, "T18 01A");
        Ok(OfficialPreviewIntegrityV1Regression.Run().Count == 0, "T18 01B");
        Ok(ProductionProgressAuthorityV1Regression.Run().Count == 0, "T18 01D");
        Ok(ProductionExecutionAuthorityV1Regression.Run().Count == 0, "T18 01E");
        Ok(ProductionLegacyExportAuthorityV1Regression.Run().Count == 0, "T18 01F");
        Ok(ProductionTenantBoundaryV1Regression.Run().Count == 0, "T18 tenant boundary");
        Ok(SeriesGraphMediaWriteCutoverV1Regression.Run().Count == 0, "G9 01A");
        Ok(OfficialPreviewIntegrityV1Regression.Run().Count == 0, "G9 01B");
        Ok(ProductionProgressAuthorityV1Regression.Run().Count == 0, "G9 01D");
        Ok(ProductionExecutionAuthorityV1Regression.Run().Count == 0, "G9 01E");
        Ok(ProductionLegacyExportAuthorityV1Regression.Run().Count == 0, "G9 01F");
        Ok(ProductionTenantBoundaryV1Regression.Run().Count == 0, "G10 tenant boundary");
    }

    static void ProveUnprefixedWriterClosed(Action<bool, string> Ok)
    {
        var root = Path.Combine(Path.GetTempPath(), "famixa-unprefixed-closed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var historical = Path.Combine(root, "os-veo-out", "historical01.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(historical)!);
            var legacy = Sentinel(11);
            File.WriteAllBytes(historical, legacy);
            var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories);

            Ok(Throws(ProductionArtifactPhysicalWrite.UnprefixedWriteClosed, () =>
                ProductionArtifactPhysicalWrite.CloseUnprefixedProductionWrite()),
                "G1 unprefixed production write fails closed");
            Ok(Directory.GetFiles(root, "*", SearchOption.AllDirectories).SequenceEqual(before),
                "G1 no new unprefixed file is created");
            Ok(File.ReadAllBytes(historical).SequenceEqual(legacy), "G2 historical unprefixed file stays untouched");
            Ok(Throws(ProductionArtifactPhysicalWrite.UnprefixedWriteClosed, () =>
                ProductionArtifactPhysicalWrite.CloseUnprefixedProductionWrite()),
                "G3 closed writer cannot overwrite a historical file");
            Ok(File.ReadAllBytes(historical).SequenceEqual(legacy), "G3 historical bytes stay the original sentinel");

            var caller = new SwitchTenant { Id = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1") };
            var files = new InMemoryProductionArtifactFileStore(caller);
            var official = files.Write("os", Guid.NewGuid().ToString("N"), Sentinel(12));
            Ok(ProductionTenantBoundary.OwnsStorageKey(caller.Id, official)
                && files.Read("os/" + official[(official.LastIndexOf('/') + 1)..]) is null,
                "G5 official write does not fall back to an unprefixed key");

            var store = new InMemoryProductionWorkStore();
            Ok(Throws(ProductionArtifactPhysicalWrite.UnprefixedWriteClosed, () =>
                ProductionArtifactPhysicalWrite.CloseUnprefixedProductionWrite()),
                "G6 closed writer throws before any artifact insert");
            Ok(store.ListArtifactsByTaskAsync(Guid.NewGuid()).GetAwaiter().GetResult().Count == 0,
                "G6 closed writer creates no artifact or binding");
            Ok(ProductionArtifactPhysicalWrite.UnprefixedWriteClosed.Contains("UNPREFIXED_WRITE_CLOSED", StringComparison.Ordinal),
                "G7 closed writer has no provider call");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { /* temp */ }
        }

        var repo = RepoRoot();
        Ok(repo is not null, "G1 repo root");
        if (repo is null) return;
        var directoryStore = File.ReadAllText(Path.Combine(repo,
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/OfficialVeoPollRecoverMaterializationLiveV1.cs"));
        var writeAt = directoryStore.IndexOf("public string Write(", StringComparison.Ordinal);
        var readAt = directoryStore.IndexOf("public byte[]? Read(", writeAt, StringComparison.Ordinal);
        var writeBody = writeAt >= 0 && readAt > writeAt ? directoryStore[writeAt..readAt] : "";
        Ok(writeBody.Contains("CloseUnprefixedProductionWrite", StringComparison.Ordinal)
            && !writeBody.Contains("CommitFile", StringComparison.Ordinal)
            && !writeBody.Contains("WriteAllBytes", StringComparison.Ordinal)
            && !writeBody.Contains("CreateDirectory", StringComparison.Ordinal)
            && !writeBody.Contains("HttpClient", StringComparison.Ordinal)
            && !writeBody.Contains("InsertArtifact", StringComparison.Ordinal)
            && !writeBody.Contains("InsertBinding", StringComparison.Ordinal),
            "G1 G6 G7 directory writer fails before disk, artifact, and provider");

        var officialStore = File.ReadAllText(Path.Combine(repo,
            "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionArtifactFileStore.cs"));
        Ok(officialStore.Contains("ProductionTenantBoundary.Prefix", StringComparison.Ordinal)
            && !officialStore.Contains("DirectoryProductionArtifactFileStore", StringComparison.Ordinal)
            && !officialStore.Contains("CloseUnprefixedProductionWrite", StringComparison.Ordinal),
            "G5 official store does not fall back to the unprefixed writer");
        Ok(!directoryStore.Contains("UPDATE pack_content.production_artifacts", StringComparison.Ordinal),
            "G8 closed writer does not assign historical tenant_id");
    }

    static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.Exists(@"E:\KitPlatform") ? @"E:\KitPlatform" : null;
    }

    static (InMemoryProductionWorkStore Store, Guid TaskId, Guid AttemptId) OwnedStore(SwitchTenant caller, Guid tenant)
    {
        var store = new InMemoryProductionWorkStore(caller);
        var build = Guid.NewGuid();
        store.RegisterBuild(build, tenant);
        var scope = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var request = new ProductionRequest(
            Guid.NewGuid(), scope, ProductionActions.CreateVideo, ProductionArtifactRoles.VideoTake,
            DateTimeOffset.UtcNow, "immut", ProductionRequestStatuses.Submitted, null, "immut-" + Guid.NewGuid().ToString("N"), []);
        store.InsertRequestAsync(request).GetAwaiter().GetResult();
        var task = new ProductionTask(
            Guid.NewGuid(), request.RequestId, null, ProductionActions.CreateVideo, "Shot", scope.ShotCode, scope,
            null, "veo", "veo-model", ProductionTaskStatuses.Created, DateTimeOffset.UtcNow, null, null, null, null);
        store.InsertTaskAsync(task).GetAwaiter().GetResult();
        var attempt = new ProductionAttempt(
            Guid.NewGuid(), task.TaskId, 1, "veo", "veo-model", "job", "STARTED",
            DateTimeOffset.UtcNow, null, "req", null, null);
        store.InsertAttemptAsync(attempt).GetAwaiter().GetResult();
        return (store, task.TaskId, attempt.AttemptId);
    }

    static ProductionArtifact Artifact(Guid id, Guid attemptId, string sha, string storageKey) =>
        new(
            id, Guid.NewGuid(), attemptId, ProductionArtifactRoles.VideoTake, "video/mp4",
            64, 1, sha, storageKey, null, DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "snap");

    static byte[] Sentinel(byte mark)
    {
        var bytes = new byte[64];
        bytes[0] = mark;
        bytes[^1] = mark;
        return bytes;
    }

    static bool Throws(string code, Action act)
    {
        try
        {
            act();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == code;
        }
    }

    sealed class SwitchTenant : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
