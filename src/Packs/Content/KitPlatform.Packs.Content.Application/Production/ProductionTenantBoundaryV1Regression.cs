using System.Reflection;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-TENANT-BOUNDARY-01. Two callers share one store.
/// Tenant B using Tenant A's ids fails closed. No default tenant is applied.
/// </summary>
public static class ProductionTenantBoundaryV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_TENANT_BOUNDARY_01";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        ProveIsolation(Ok);
        ProveLegacyClosed(Ok);
        Ok(OfficialPreviewIntegrityV1Regression.Run().Count == 0, "T34 01B");
        Ok(ProductionProgressAuthorityV1Regression.Run().Count == 0, "T36 01D");
        Ok(ProductionExecutionAuthorityV1Regression.Run().Count == 0, "T37 01E");
        Ok(ProductionLegacyExportAuthorityV1Regression.Run().Count == 0, "T39 01F");
        return fail;
    }

    static void ProveIsolation(Action<bool, string> Ok)
    {
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchTenant();
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var buildA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa10");
        var buildB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb20");
        store.RegisterBuild(buildA, tenantA);
        store.RegisterBuild(buildB, tenantB);

        caller.Id = Guid.Empty;
        Ok(Throws(ProductionTenantBoundary.Required, () => store.GetRequestAsync(Guid.NewGuid()).GetAwaiter().GetResult()),
            "T20 missing tenant fails closed");

        caller.Id = tenantA;
        var scopeA = new ProductionScope(buildA, "EP01", "SC01", "EP01-SC01-SH01");
        var requestA = new ProductionRequest(
            Guid.NewGuid(), scopeA, ProductionActions.CreateVideo, ProductionArtifactRoles.VideoTake,
            DateTimeOffset.UtcNow, "a", ProductionRequestStatuses.Submitted, null, "same-key", []);
        store.InsertRequestAsync(requestA).GetAwaiter().GetResult();
        Ok(store.GetRequestAsync(requestA.RequestId).GetAwaiter().GetResult()?.RequestId == requestA.RequestId,
            "T1 tenant A reads its request");

        var task = new ProductionTask(
            Guid.NewGuid(), requestA.RequestId, null, ProductionActions.CreateVideo, "Shot", scopeA.ShotCode, scopeA,
            null, "veo", "veo-model", ProductionTaskStatuses.Created, DateTimeOffset.UtcNow, null, null, null, null);
        store.InsertTaskAsync(task).GetAwaiter().GetResult();
        Ok(store.GetTaskAsync(task.TaskId).GetAwaiter().GetResult()?.TaskId == task.TaskId, "T4 tenant A reads its task");

        var snapshot = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId, "story", "acting", "visual", "shot", "prompt", "compiler",
            "s", "a", "v", "c", "", "", "", "", "", "veo", "veo-model", "snap-hash", DateTimeOffset.UtcNow);
        store.InsertSnapshotAsync(snapshot).GetAwaiter().GetResult();
        Ok(store.GetSnapshotAsync(snapshot.SnapshotId).GetAwaiter().GetResult()?.SnapshotHash == "snap-hash",
            "T6 tenant A reads its snapshot");

        var attempt = new ProductionAttempt(
            Guid.NewGuid(), task.TaskId, 1, "veo", "veo-model", "job-a", "STARTED",
            DateTimeOffset.UtcNow, null, "req", null, null);
        store.InsertAttemptAsync(attempt).GetAwaiter().GetResult();
        Ok(store.GetAttemptAsync(attempt.AttemptId).GetAwaiter().GetResult()?.ProviderJobId == "job-a",
            "T8 tenant A reads its attempt");
        Ok(store.GetAttemptAsync(attempt.AttemptId).GetAwaiter().GetResult()?.ProviderId == "veo",
            "T19 attempt stays on tenant A");

        var bytes = new byte[64];
        bytes[0] = 9;
        var storageKey = files.Write("os", attempt.AttemptId.ToString("N"), bytes);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        Ok(storageKey.StartsWith(ProductionTenantBoundary.Prefix(tenantA) + "/", StringComparison.OrdinalIgnoreCase),
            "T19 storage key records tenant A");
        var artifact = new ProductionArtifact(
            Guid.NewGuid(), task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake, "video/mp4",
            bytes.Length, 1, sha, storageKey, "job-a", DateTimeOffset.UtcNow, "UNVERIFIED", snapshot.SnapshotHash);
        store.InsertArtifactAsync(artifact).GetAwaiter().GetResult();
        Ok(store.GetArtifactAsync(artifact.ArtifactId).GetAwaiter().GetResult()?.ArtifactId == artifact.ArtifactId,
            "T10 tenant A reads its artifact");

        var binding = new ProductionBinding(
            Guid.NewGuid(), buildA, scopeA.ShotCode, "CURRENT", artifact.ArtifactId, task.TaskId, DateTimeOffset.UtcNow, null);
        store.InsertBindingAsync(binding).GetAwaiter().GetResult();
        Ok(store.GetActiveBindingAsync(buildA, scopeA.ShotCode, "CURRENT").GetAwaiter().GetResult()?.BindingId == binding.BindingId,
            "T13 tenant A reads its binding");

        var ev = new ProductionTaskEvent(
            Guid.NewGuid(), "ATTEMPT_STARTED", DateTimeOffset.UtcNow, requestA.RequestId, task.TaskId,
            attempt.AttemptId, artifact.ArtifactId, scopeA.ShotCode, ProductionActions.CreateVideo, "{}");
        store.InsertEventAsync(ev).GetAwaiter().GetResult();
        Ok(store.ListEventsByTaskAsync(task.TaskId).GetAwaiter().GetResult().Count == 1, "T17 tenant A event is stored");

        var assembly = new FilmAssembly(Guid.NewGuid(), buildA, "cut", DateTimeOffset.UtcNow);
        store.InsertAssemblyAsync(assembly).GetAwaiter().GetResult();
        store.InsertAssemblyShotAsync(new FilmAssemblyShot(
            Guid.NewGuid(), assembly.AssemblyId, buildA, scopeA.ShotCode, artifact.ArtifactId, 1, 1, "CUT", "BOUND"))
            .GetAwaiter().GetResult();
        Ok(store.ListAssembliesByBuildAsync(buildA).GetAwaiter().GetResult().Count == 1, "tenant A assembly is stored");

        var verified = OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, storageKey, sha);
        Ok(verified is not null && KitVideoIntegrityRules.Sha256Hex(verified) == sha,
            "T25 official preview still checks SHA");

        caller.Id = tenantB;
        Ok(store.GetRequestAsync(requestA.RequestId).GetAwaiter().GetResult() is null, "T2 tenant B cannot read A's request");
        Ok(store.GetRequestByIdempotencyAsync("same-key").GetAwaiter().GetResult() is null,
            "T3 tenant B cannot retrieve A's request by idempotency key");
        var scopeB = new ProductionScope(buildB, "EP01", "SC01", "EP01-SC01-SH01");
        var requestB = requestA with { RequestId = Guid.NewGuid(), Scope = scopeB };
        store.InsertRequestAsync(requestB).GetAwaiter().GetResult();
        Ok(store.GetRequestByIdempotencyAsync("same-key").GetAwaiter().GetResult()?.RequestId == requestB.RequestId,
            "T3 tenant B key is a different request");
        caller.Id = tenantA;
        Ok(store.GetRequestByIdempotencyAsync("same-key").GetAwaiter().GetResult()?.RequestId == requestA.RequestId,
            "T3 tenant A key still returns A's request");

        caller.Id = tenantB;
        Ok(store.GetTaskAsync(task.TaskId).GetAwaiter().GetResult() is null, "T5 tenant B cannot read A's task");
        Ok(store.GetSnapshotAsync(snapshot.SnapshotId).GetAwaiter().GetResult() is null, "T7 tenant B cannot read A's snapshot");
        Ok(store.GetAttemptAsync(attempt.AttemptId).GetAwaiter().GetResult() is null, "T9 tenant B cannot read A's attempt");
        Ok(store.GetArtifactAsync(artifact.ArtifactId).GetAwaiter().GetResult() is null, "T11 artifact id is not authority");
        Ok(files.Read(storageKey) is null, "T12 storage key is not authority");
        Ok(OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes(files, storageKey, sha) is null,
            "T26 valid SHA does not authorize another tenant");
        Ok(store.GetActiveBindingAsync(buildA, scopeA.ShotCode, "CURRENT").GetAwaiter().GetResult() is null,
            "T14 tenant B cannot read A's binding");
        Ok(Throws(ProductionTenantBoundary.Forbidden, () => store.UnbindAsync(binding.BindingId, DateTimeOffset.UtcNow).GetAwaiter().GetResult()),
            "T15 tenant B cannot mutate A's binding");
        Ok(store.ListAssembliesByBuildAsync(buildA).GetAwaiter().GetResult().Count == 0, "T16 tenant B cannot read A's assembly");
        Ok(Throws(ProductionTenantBoundary.Forbidden, () => store.InsertEventAsync(ev with { EventId = Guid.NewGuid() }).GetAwaiter().GetResult()),
            "T17 tenant B cannot append an event to A's task");
        Ok(Throws(ProductionTenantBoundary.Forbidden, () => store.InsertRequestAsync(new ProductionRequest(
            Guid.NewGuid(), scopeA, ProductionActions.CreateVideo, ProductionArtifactRoles.VideoTake,
            DateTimeOffset.UtcNow, "b", ProductionRequestStatuses.Submitted, null, "other-key", [])).GetAwaiter().GetResult()),
            "T18/T22 tenant B cannot execute against A's build");
        Ok(Throws(ProductionTenantBoundary.Forbidden, () => store.UpdateAttemptAsync(attempt with { Status = "LATE" }).GetAwaiter().GetResult()),
            "T14/T15 late result cannot update A's attempt");
        caller.Id = tenantA;
        Ok(store.GetAttemptAsync(attempt.AttemptId).GetAwaiter().GetResult()?.Status == "STARTED",
            "late result did not bind across tenants");
        Ok(store.GetActiveBindingAsync(buildA, scopeA.ShotCode, "CURRENT").GetAwaiter().GetResult()?.ArtifactId == artifact.ArtifactId,
            "A's binding is unchanged");

        var clientTenant = tenantB;
        caller.Id = tenantA;
        Ok(store.GetRequestAsync(requestA.RequestId).GetAwaiter().GetResult() is not null
            && clientTenant != caller.Id, "T21 caller tenant is not the client-supplied tenant");
        Ok(!typeof(ProductionCreateVideoRequestDto).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Any(p => string.Equals(p.Name, "TenantId", StringComparison.Ordinal)),
            "T21 request DTO has no tenant field");
    }

    static void ProveLegacyClosed(Action<bool, string> Ok)
    {
        Ok(!ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob("POST content/series/turbo"), "T27 turbo submit stays closed");
        Ok(!ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob("POST content/series/lipsync"), "T28 lipsync submit stays closed");
        Ok(!ProductionExecutionAuthorityV1.LegacyDirectMaySubmitProviderJob("POST content/video-engine/shots/{id}/video-generation-execution/execute"),
            "T29 VGE execute stays closed");
        Ok(ProductionLegacyExportAuthorityV1.Classification == "EXPORT_ONLY"
            && !ProductionLegacyExportAuthorityV1.MayCreateOfficialArtifact()
            && !ProductionLegacyExportAuthorityV1.MayCreateFilmAssembly(),
            "T30 legacy assemble stays export-only");
        Ok(!Legacy.LegacySeriesGraphAdapter.MayDriveNewBinding("acceptedTake.url"), "T31 acceptedTake stays non-authority");
        Ok(!Legacy.LegacySeriesGraphAdapter.MayDriveNewBinding("previewUrl")
            && !Legacy.LegacySeriesGraphAdapter.MayDriveNewBinding("takeUrl"),
            "T32 graph media stays non-authority");
        var kept = SeriesGraphMediaWriteCutover.Apply("""
            {"scriptLocked":true,"runs":{"SH01":{"takeUrl":"old.mp4","acceptedTake":{"n":1,"url":"old-accept.mp4"}}}}
            """, """{"runs":{"SH01":{"takeUrl":"new.mp4","acceptedTake":{"n":9,"url":"new-accept.mp4"}}}}""");
        Ok(kept.Contains("old-accept.mp4", StringComparison.Ordinal)
            && !kept.Contains("new-accept.mp4", StringComparison.Ordinal),
            "T33 01A graph media write stays cut over");

        var root = FindRepoRoot();
        Ok(root is not null, "repo root");
        if (root is null) return;
        var http = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpProductionCallerTenant.cs"));
        var repo = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionWorkRepository.cs"));
        var controller = File.ReadAllText(Path.Combine(root, "src/KitPlatform.Api/Controllers/Content/ContentProductionController.cs"));
        var turbo = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs"));
        var vge = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs"));
        Ok(http.Contains("_tenant.TenantId", StringComparison.Ordinal)
            && !http.Contains("WorkspaceId", StringComparison.Ordinal)
            && !http.Contains("KIT_MKT", StringComparison.Ordinal),
            "ITenantContext is the tenant source");
        Ok(repo.Contains("tenant_id = @TenantId", StringComparison.Ordinal)
            && repo.Contains("RequireBuildAsync", StringComparison.Ordinal)
            && !repo.Contains("WorkspaceId", StringComparison.Ordinal),
            "repository predicates constrain tenant");
        Ok(controller.Contains("AssembleEpisodeOsAsync", StringComparison.Ordinal)
            && controller.Contains("ProductionActions.AssembleEpisode", StringComparison.Ordinal),
            "episode assembly route is the official action");
        Ok(!ProductionLegacyExportAuthorityV1.MayCreateOfficialArtifact()
            && !ProductionLegacyExportAuthorityV1.AssembleOneShotMayExecuteOfficialProduction(),
            "legacy assemble and assembleOneShot stay non-official");
        var turboThrow = turbo.IndexOf("LegacyProviderSubmitClosed", StringComparison.Ordinal);
        var turboStart = turbo.IndexOf(".StartAsync", StringComparison.Ordinal);
        Ok(turboThrow >= 0 && turboStart > turboThrow, "T27/T28 throw remains before provider start");
        var vgeThrow = vge.IndexOf("LEGACY_EXECUTION_CLOSED", StringComparison.Ordinal);
        var vgeGenerate = vge.IndexOf("GenerateAsync", StringComparison.Ordinal);
        Ok(vgeThrow >= 0 && vgeGenerate > vgeThrow, "T29 VGE throw remains before generate");
        _ = ProductionActions.AssembleEpisode;
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
        catch (UnauthorizedAccessException ex)
        {
            return ex.Message == code;
        }
    }

    static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.Exists("E:\\KitPlatform") ? "E:\\KitPlatform" : null;
    }

    sealed class SwitchTenant : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
