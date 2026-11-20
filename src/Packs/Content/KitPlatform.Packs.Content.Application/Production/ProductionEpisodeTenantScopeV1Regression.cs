namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-EPISODE-TENANT-SCOPE-01. Scope only. Does not assemble, compose, or bind.
/// </summary>
public static class ProductionEpisodeTenantScopeV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_EPISODE_TENANT_SCOPE_01";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { Prove(Ok); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static void Prove(Action<bool, string> Ok)
    {
        var historical = ProductionScopeAnchorRules.OfficialEp01;
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var buildA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccc01");
        var caller = new Caller();
        var store = new InMemoryProductionWorkStore(caller);
        store.RegisterBuild(buildA, tenantA);

        Ok(historical == Guid.Parse("4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f")
            && Throws(ProductionTenantBoundary.Unattributed, () => ProductionBuildTenantGate.Require(tenantA, null)),
            "T1 historical EP01 build stays unattributed");

        var episode = new ProductionScope(buildA, "EP01", "EP01", ProductionEpisodeAssemblyContract.EpisodeShotCode);
        Ok(ProductionEpisodeAssemblyContract.IsEpisodeScope(episode)
            && ProductionScopeAnchorRules.IsEp01EpisodeBuild(buildA, "EP01")
            && ProductionEpisodeAssemblyContract.ValidateInputs(
                buildA, "EP01", SampleInputs(), streamCompatible: true, legacyHint: null) is null,
            "T2 tenant-owned EP01 build is an episode scope");

        caller.Id = tenantA;
        Ok(!Throws(ProductionTenantBoundary.Forbidden, () => ProductionBuildTenantGate.Require(tenantA, tenantA))
            && store.GetArtifactAsync(ProductionEp01Sh01AssemblyCanon.MixArtifactId).GetAwaiter().GetResult() is null,
            "T3 tenant A can use its own EP01 build");

        caller.Id = tenantB;
        Ok(Throws(ProductionTenantBoundary.Forbidden, () => InsertOn(store, buildA, tenantB)),
            "T4 tenant B cannot use tenant A EP01 build");

        caller.Id = tenantA;
        Ok(Throws(ProductionTenantBoundary.Unattributed, () => InsertOn(store, historical, tenantA)),
            "T5 historical NULL EP01 build is closed to tenant A");

        var sh01 = new ProductionScope(buildA, "EP01", "SC01", "EP01-SC01-SH01");
        var sh02 = new ProductionScope(buildA, "EP01", "SC01", "EP01-SC01-SH02");
        Ok(!ProductionEp01Sh01AssemblyCanon.MatchesScope(sh01)
            && !ProductionEp01Sh02AssembleCanon.MatchesScope(sh02)
            && ProductionEp01Sh01AssemblyCanon.MixArtifactId == Guid.Parse("2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2")
            && ProductionEp01Sh02AssembleCanon.MixArtifactId == Guid.Parse("58297402-e252-45e8-8d33-c7414749a297"),
            "T6 new build does not inherit historical artifact ids");

        Ok(store.GetActiveBindingAsync(buildA, sh01.ShotCode, ProductionBindingRoles.Accepted).GetAwaiter().GetResult() is null
            && store.GetActiveBindingAsync(buildA, sh02.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly)).GetAwaiter().GetResult() is null
            && store.GetActiveBindingAsync(historical, "EP01-SC01-SH01", ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly)).GetAwaiter().GetResult() is null,
            "T7 new build does not inherit historical bindings");

        Ok(ProductionScopeAnchorRules.OfficialFilmScopeIds.Count == 4
            && ProductionScopeAnchorRules.OfficialFilmScopeIds.All(ProductionScopeAnchorRules.IsOfficialFilm)
            && ProductionScopeAnchorRules.IsOfficialFilm(historical)
            && !ProductionScopeAnchorRules.IsOfficialFilm(buildA),
            "T8 historical official-film ids stay official film");

        var arbitrary = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd09");
        Ok(!ProductionScopeAnchorRules.IsTenantOwnedEp01Build(arbitrary, "EP02")
            && !ProductionScopeAnchorRules.IsEp01EpisodeBuild(arbitrary, "EP99")
            && !ProductionEpisodeAssemblyContract.IsEpisodeScope(new ProductionScope(arbitrary, "EP02", "EP02", "EP02"))
            && !ProductionScopeAnchorRules.IsOfficialFilm(arbitrary)
            && !ProductionScopeAnchorRules.IsTenantOwnedEp01Build(ProductionScopeAnchorRules.OfficialEp99, "EP01")
            && !ProductionEpisodeAssemblyContract.IsEpisodeScope(
                new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP01", "EP01", "EP01")),
            "T9 non-EP01 build is not official EP01");

        Ok(Throws(ProductionTenantBoundary.Unattributed, () => ProductionBuildTenantGate.Require(tenantA, null))
            && Throws(ProductionTenantBoundary.Required, () => ProductionBuildTenantGate.Require(Guid.Empty, tenantA)),
            "T10 unowned build is rejected");

        Ok(Throws(ProductionTenantBoundary.Forbidden, () => ProductionBuildTenantGate.Require(tenantB, tenantA)),
            "T11 tenant mismatch fails closed");

        Ok(store.GetArtifactAsync(ProductionEp01Sh02AssembleCanon.MixArtifactId).GetAwaiter().GetResult() is null
            && store.GetActiveBindingAsync(buildA, "EP01", ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly)).GetAwaiter().GetResult() is null,
            "T12 no artifact or binding is created");

        var actions = File.ReadAllText(FindActions());
        var sh01Hist = new ProductionScope(historical, "EP01", "SC01", "EP01-SC01-SH01");
        var sh02Hist = new ProductionScope(historical, "EP01", "SC01", "EP01-SC01-SH02");
        Ok(ProductionMixContract.AppliesTo(sh01)
            && ProductionMixContract.AppliesTo(sh02)
            && !ProductionMixContract.AppliesTo(sh01Hist)
            && ProductionMixContract.AppliesTo(sh02Hist)
            && !ProductionPerformanceConsumption.ActionRequiresSnapshot(ProductionActions.CreateVideo, episode)
            && ProductionPerformanceConsumption.ActionRequiresSnapshot(
                ProductionActions.CreateVideo, new ProductionScope(arbitrary, "EP02", "EP02", "EP02")),
            "tenant EP01 uses MIX compose and does not require a performance snapshot");

        Ok(actions.Contains("IsTenantOwnedEp01Build", StringComparison.Ordinal)
            && actions.Contains("historicalOfficialFilm && ProductionEp01Sh01AssemblyCanon.MatchesScope", StringComparison.Ordinal)
            && actions.Contains("if (officialFilm)", StringComparison.Ordinal)
            && actions.Contains("AssembleOfficialAsync", StringComparison.Ordinal),
            "shot assembly official path includes tenant-owned EP01 and keeps historical parent locks");
    }

    static IReadOnlyList<ProductionEpisodeAssemblyInput> SampleInputs()
    {
        var sha = new string('a', 64);
        var sha2 = new string('b', 64);
        return
        [
            new(0, "EP01-SC01-SH01", ProductionArtifactRoles.Assembly, Guid.Parse("11111111-1111-1111-1111-111111111111"), sha, "tenant/sh01", 5.042m, 0, 0),
            new(1, "EP01-SC01-SH02", ProductionArtifactRoles.Assembly, Guid.Parse("22222222-2222-2222-2222-222222222222"), sha2, "tenant/sh02", 8m, 0, 0),
        ];
    }

    static void InsertOn(InMemoryProductionWorkStore store, Guid buildId, Guid actor)
    {
        _ = actor;
        var scope = new ProductionScope(buildId, "EP01", "EP01", "EP01");
        store.InsertRequestAsync(new ProductionRequest(
            Guid.NewGuid(), scope, ProductionActions.AssembleEpisode, ProductionArtifactRoles.Assembly,
            DateTimeOffset.UtcNow, "scope", ProductionRequestStatuses.Submitted, null, "scope-" + buildId.ToString("N"), [])).GetAwaiter().GetResult();
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

    static string FindActions()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "src", "Packs", "Content",
                "KitPlatform.Packs.Content.Application", "Production", "ProductionCreateVideoService.Actions.cs");
            if (File.Exists(path)) return path;
            dir = dir.Parent;
        }
        return Path.Combine("src", "Packs", "Content", "KitPlatform.Packs.Content.Application",
            "Production", "ProductionCreateVideoService.Actions.cs");
    }

    sealed class Caller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
