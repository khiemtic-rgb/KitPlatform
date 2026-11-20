namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Tenant workspace may read CURRENT:VIDEO_TAKE. Historical official film stays ACCEPTED-only.
/// No provider, no new media, no pilot rewrite.
/// </summary>
public static class ProductionTenantWorkspaceAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_FILM_PRODUCTION_06";
    static readonly Guid TenantBuildId = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
    const string Shot = "EP01-SC01-SH01";

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
        await T_HistoricalAcceptedStillWins(Ok);
        await T_HistoricalCurrentDoesNotBecomeAuthoritative(Ok);
        await T_TenantCurrentVerifiedIsInput(Ok);
        await T_TenantAcceptedStillWins(Ok);
        await T_TenantRejectsUnverifiedWrongRoleAndOtherBuild(Ok);
        T_SelectionAndTenantSql(Ok);
        T_NoProvider(Ok);
    }

    static async Task T_HistoricalAcceptedStillWins(Action<bool, string> Ok)
    {
        var planted = await Plant(ProductionScopeAnchorRules.OfficialEp01, Shot, ProductionArtifactRoles.VideoTake,
            ProductionArtifactStatuses.Verified, current: true, accepted: true);
        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(
            planted.Store, ProductionScopeAnchorRules.OfficialEp01, Shot);
        Ok(take is not null && take.ArtifactId == planted.AcceptedId
            && take.AcceptanceStatus == OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus,
            "historical ACCEPTED:VIDEO_TAKE");
    }

    static async Task T_HistoricalCurrentDoesNotBecomeAuthoritative(Action<bool, string> Ok)
    {
        var planted = await Plant(ProductionScopeAnchorRules.OfficialEp01, Shot, ProductionArtifactRoles.VideoTake,
            ProductionArtifactStatuses.Verified, current: true, accepted: false);
        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(
            planted.Store, ProductionScopeAnchorRules.OfficialEp01, Shot);
        Ok(take is null, "historical CURRENT is not authoritative");
    }

    static async Task T_TenantCurrentVerifiedIsInput(Action<bool, string> Ok)
    {
        var planted = await Plant(TenantBuildId, Shot, ProductionArtifactRoles.VideoTake,
            ProductionArtifactStatuses.Verified, current: true, accepted: false);
        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(planted.Store, TenantBuildId, Shot);
        Ok(take is not null
            && take.ArtifactId == planted.CurrentId
            && take.ArtifactRole == ProductionArtifactRoles.VideoTake
            && take.AcceptanceStatus == OfficialVideoAuthorityBridgeV1.CurrentVideoTakeStatus
            && take.VerificationStatus == ProductionArtifactStatuses.Verified
            && take.BuildId == TenantBuildId
            && take.ProviderId == "import"
            && take.ModelId == "existing-video",
            "tenant CURRENT:VIDEO_TAKE");
        Ok(take!.AcceptanceStatus != OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus, "tenant status is not ACCEPTED");
    }

    static async Task T_TenantAcceptedStillWins(Action<bool, string> Ok)
    {
        var planted = await Plant(TenantBuildId, Shot, ProductionArtifactRoles.VideoTake,
            ProductionArtifactStatuses.Verified, current: true, accepted: true);
        var take = await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(planted.Store, TenantBuildId, Shot);
        Ok(take is not null && take.ArtifactId == planted.AcceptedId
            && take.AcceptanceStatus == OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus,
            "tenant ACCEPTED still wins");
    }

    static async Task T_TenantRejectsUnverifiedWrongRoleAndOtherBuild(Action<bool, string> Ok)
    {
        var unverified = await Plant(TenantBuildId, Shot, ProductionArtifactRoles.VideoTake,
            ProductionArtifactStatuses.Unverified, current: true, accepted: false);
        Ok(await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(unverified.Store, TenantBuildId, Shot) is null,
            "unverified CURRENT rejected");

        var keyframe = await Plant(TenantBuildId, Shot, ProductionArtifactRoles.Keyframe,
            ProductionArtifactStatuses.Verified, current: true, accepted: false);
        Ok(await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(keyframe.Store, TenantBuildId, Shot) is null,
            "non VIDEO_TAKE rejected");

        var other = await Plant(TenantBuildId, Shot, ProductionArtifactRoles.VideoTake,
            ProductionArtifactStatuses.Verified, current: true, accepted: false, taskBuildId: Guid.NewGuid());
        Ok(await OfficialVideoAuthorityBridgeV1.GetAuthoritativeVideoTakeAsync(other.Store, TenantBuildId, Shot) is null,
            "other build rejected");
    }

    static void T_SelectionAndTenantSql(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        Ok(root is not null, "repo root");
        if (root is null) return;
        var pilot = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesPilotService.cs"));
        var repo = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentRepository.cs"));
        var work = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionWorkRepository.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialVideoAuthorityBridgeV1.cs"));
        Ok(pilot.Contains("ClaimSeriesPilotActiveBuildIfUnsetAsync", StringComparison.Ordinal), "upsert does not steal active build");
        Ok(pilot.Contains("SetSeriesPilotActiveBuildAsync(row.SeriesCode, null", StringComparison.Ordinal), "delete still clears pointer");
        Ok(repo.Contains("active_build_id IS NULL OR active_build_id = @BuildId", StringComparison.Ordinal), "claim predicate");
        Ok(repo.Contains("WHERE id = @Id AND tenant_id = @TenantId", StringComparison.Ordinal), "build read is tenant scoped");
        Ok(work.Contains("WHERE artifact_id = @Id AND tenant_id = @TenantId", StringComparison.Ordinal), "artifact read is tenant scoped");
        Ok(bridge.Contains("OfficialVeoVideoTakeAcceptanceV1.GetAuthoritativeVideoTakeAsync", StringComparison.Ordinal), "historical lookup remains");
        Ok(bridge.Contains("IsOfficialFilm", StringComparison.Ordinal), "official film excluded from CURRENT fallback");
    }

    static void T_NoProvider(Action<bool, string> Ok)
    {
        var root = FindRepoRoot();
        if (root is null)
        {
            Ok(false, "repo root");
            return;
        }
        var bridge = File.ReadAllText(Path.Combine(root, "src/Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialVideoAuthorityBridgeV1.cs"));
        var start = bridge.IndexOf("static async Task<AuthoritativeVideoTake?> ResolveTenantCurrentVideoTakeAsync", StringComparison.Ordinal);
        var end = start >= 0
            ? bridge.IndexOf("public static Task<OfficialVideoAuthorityResolution> ResolveAsync", start, StringComparison.Ordinal)
            : -1;
        var body = start >= 0 && end > start ? bridge[start..end] : "";
        Ok(body.Length > 40, "CURRENT fallback method located");
        Ok(!body.Contains("HttpClient", StringComparison.Ordinal)
            && !body.Contains("Runway", StringComparison.Ordinal)
            && !body.Contains("Veo", StringComparison.Ordinal)
            && !body.Contains("Fal", StringComparison.Ordinal),
            "CURRENT fallback makes no provider call");
    }

    static async Task<Planted> Plant(
        Guid bindingBuildId,
        string shot,
        string role,
        string status,
        bool current,
        bool accepted,
        Guid? taskBuildId = null)
    {
        var store = new InMemoryProductionWorkStore();
        var engine = new ProductionWorkExecutionEngine(store);
        var scopeBuild = taskBuildId ?? bindingBuildId;
        var scope = new ProductionScope(scopeBuild, "EP01", "SC01", shot);
        var req = await engine.CreateRequestAsync(scope, ProductionActions.CreateVideo, "p06", "p06-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(req.RequestId);
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), task.TaskId,
            "story", "acting", "visual", "motion",
            "compiled", "compiled",
            "s", "a", "v", "c", "kh", "vh", "mh", "", "th",
            "import", "existing-video", "snap-p06", DateTimeOffset.UtcNow,
            null, "16:9", 5, "p06", null);
        await store.InsertSnapshotAsync(snap);
        var attempt = new ProductionAttempt(
            Guid.NewGuid(), task.TaskId, 1, "import", "existing-video", null,
            ProductionAttemptStatuses.ProviderSucceeded, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "req", "res", null);
        await store.InsertAttemptAsync(attempt);
        var currentId = Guid.NewGuid();
        var art = new ProductionArtifact(
            currentId, task.TaskId, attempt.AttemptId, role,
            "video/mp4", 12, 5, new string('a', 64), "tenant/os/p06", null,
            DateTimeOffset.UtcNow, status, snap.SnapshotHash);
        await store.InsertArtifactAsync(art);
        if (current)
        {
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), bindingBuildId, shot,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
                currentId, task.TaskId, DateTimeOffset.UtcNow, null));
        }
        var acceptedId = Guid.NewGuid();
        if (accepted)
        {
            var acceptedArt = art with { ArtifactId = acceptedId };
            await store.InsertArtifactAsync(acceptedArt);
            await store.InsertBindingAsync(new ProductionBinding(
                Guid.NewGuid(), bindingBuildId, shot,
                OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
                acceptedId, task.TaskId, DateTimeOffset.UtcNow, null));
        }
        return new Planted(store, currentId, acceptedId);
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

    sealed record Planted(InMemoryProductionWorkStore Store, Guid CurrentId, Guid AcceptedId);
}
