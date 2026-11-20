namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-OFFICIAL-STATE-BINDING-01.
/// A verified artifact becomes official only through an authorized binding.
/// Preview still requires the physical bytes to match that artifact.
/// </summary>
public static class ProductionOfficialStateBindingV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OFFICIAL_STATE_BINDING_01";

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
        var missing = await SeedAsync();
        var missingBound = false;
        try
        {
            await missing.Engine.BindAsync(Guid.NewGuid(), ProductionBindingRoles.Current);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ARTIFACT_MISSING")
        {
            missingBound = true;
        }
        Ok(missingBound && await ActiveCount(missing.Store, missing.Scope, ProductionBindingRoles.Current) == 0,
            "missing artifact does not bind");

        var unverified = await SeedAsync();
        var raw = Bytes("unverified");
        var unverifiedArt = await PlantAsync(unverified, raw, ProductionArtifactStatuses.Unverified);
        var unverifiedBound = false;
        try
        {
            await unverified.Engine.BindAsync(unverifiedArt.ArtifactId, ProductionBindingRoles.Current);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.FailedIdentity)
        {
            unverifiedBound = true;
        }
        Ok(unverifiedBound && await ActiveCount(unverified.Store, unverified.Scope, ProductionBindingRoles.Current) == 0,
            "unverified artifact does not bind");

        var plain = await SeedAsync();
        var plainBytes = Bytes("verified-unbound");
        var plainArt = await PlantAsync(plain, plainBytes, ProductionArtifactStatuses.Verified);
        var previewBefore = await plain.Service.GetPreviewAsync(plain.Scope);
        Ok(previewBefore.IsEmpty && previewBefore.ArtifactId is null, "verified unbound artifact is not official preview");

        var bound = await plain.Engine.BindAsync(plainArt.ArtifactId, ProductionBindingRoles.Current);
        var again = await plain.Engine.BindAsync(plainArt.ArtifactId, ProductionBindingRoles.Current);
        Ok(again.BindingId == bound.BindingId
            && await ActiveCount(plain.Store, plain.Scope, ProductionBindingRoles.Current) == 1,
            "binding the same artifact again is idempotent");
        var preview = await plain.Service.GetPreviewAsync(plain.Scope);
        Ok(preview.ArtifactId == plainArt.ArtifactId && !preview.IsEmpty, "preview follows the official binding and matching bytes");

        plain.Files.PlantExternalBytes(plainArt.StorageKey, Bytes("tampered"));
        var mismatched = await plain.Service.GetPreviewAsync(plain.Scope);
        var stillBound = await plain.Store.GetActiveBindingAsync(plain.Scope.BuildId, plain.Scope.ShotCode, ProductionBindingRoles.Current);
        Ok(mismatched.IsEmpty && stillBound?.ArtifactId == plainArt.ArtifactId,
            "SHA mismatch fails preview closed and does not invent a new binding");

        var replaced = await SeedAsync();
        var firstArt = await PlantAsync(replaced, Bytes("first"), ProductionArtifactStatuses.Verified);
        var second = await AnotherTaskAsync(replaced);
        var secondArt = await PlantOnAsync(replaced, second.Task, second.Attempt, Bytes("second"), ProductionArtifactStatuses.Verified);
        await replaced.Engine.BindAsync(firstArt.ArtifactId, ProductionBindingRoles.Current);
        await replaced.Engine.BindAsync(secondArt.ArtifactId, ProductionBindingRoles.Current);
        var active = await replaced.Store.ListActiveBindingsAsync(replaced.Scope.BuildId, replaced.Scope.ShotCode);
        var current = active.Where(b => b.Role == ProductionBindingRoles.Current).ToArray();
        Ok(current.Length == 1 && current[0].ArtifactId == secondArt.ArtifactId,
            "replacement leaves one CURRENT binding");

        var raced = await SeedAsync();
        var left = await PlantAsync(raced, Bytes("left"), ProductionArtifactStatuses.Verified);
        var rightTask = await AnotherTaskAsync(raced);
        var right = await PlantOnAsync(raced, rightTask.Task, rightTask.Attempt, Bytes("right"), ProductionArtifactStatuses.Verified);
        var races = await Task.WhenAll(
            InvokeBind(raced.Engine, left.ArtifactId),
            InvokeBind(raced.Engine, right.ArtifactId));
        var afterRace = (await raced.Store.ListActiveBindingsAsync(raced.Scope.BuildId, raced.Scope.ShotCode))
            .Count(b => b.Role == ProductionBindingRoles.Current);
        Ok(afterRace == 1 && races.Count(r => r is null) >= 1, "concurrent CURRENT binds leave one official binding");

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaa11");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbb22");
        var caller = new SwitchCaller { Id = tenantA };
        var isolated = await SeedAsync(caller);
        var owned = await PlantAsync(isolated, Bytes("tenant-a"), ProductionArtifactStatuses.Verified);
        await isolated.Engine.BindAsync(owned.ArtifactId, ProductionBindingRoles.Current);
        caller.Id = tenantB;
        var foreignVisible = await isolated.Store.GetActiveBindingAsync(isolated.Scope.BuildId, isolated.Scope.ShotCode, ProductionBindingRoles.Current);
        var foreignPreview = await isolated.Service.GetPreviewAsync(isolated.Scope);
        var cross = false;
        try
        {
            await isolated.Engine.BindAsync(owned.ArtifactId, ProductionBindingRoles.Visible);
        }
        catch (InvalidOperationException ex) when (ex.Message is "ARTIFACT_MISSING" or "TASK_MISSING"
            || ex.Message == ProductionTenantBoundary.Forbidden)
        {
            cross = true;
        }
        Ok(foreignVisible is null && foreignPreview.IsEmpty && cross, "tenant B cannot read or bind tenant A");

        Ok(!Legacy.LegacySeriesGraphAdapter.MayDriveNewBinding("acceptedTake.url"),
            "acceptedTake cannot create an official binding");
        var root = RepoRoot();
        var turbo = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "ContentSeriesTurboService.cs"));
        var vge = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "VideoGenerationExecutionService.cs"));
        var previewSource = File.ReadAllText(Path.Combine(root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "OfficialPreviewIntegrityV1.cs"));
        Ok(!turbo.Contains("production_bindings", StringComparison.Ordinal)
            && !vge.Contains("production_bindings", StringComparison.Ordinal)
            && !previewSource.Contains("acceptedTake", StringComparison.Ordinal),
            "legacy execution and preview do not write an official binding");
        Ok(OfficialPreviewIntegrityV1Regression.Run().Count == 0, "official preview integrity stays closed");
    }

    static async Task<string?> InvokeBind(ProductionWorkExecutionEngine engine, Guid artifactId)
    {
        try
        {
            await engine.BindAsync(artifactId, ProductionBindingRoles.Current);
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    static async Task<int> ActiveCount(InMemoryProductionWorkStore store, ProductionScope scope, string role)
    {
        var rows = await store.ListActiveBindingsAsync(scope.BuildId, scope.ShotCode);
        return rows.Count(b => b.Role == role);
    }

    static async Task<Harness> SeedAsync(IProductionCallerTenant? caller = null)
    {
        var store = new InMemoryProductionWorkStore(caller);
        if (caller is not null)
            store.RegisterBuild(ProductionScopeAnchorRules.OfficialEp01, caller.RequireTenant());
        var files = new InMemoryProductionArtifactFileStore(caller);
        var engine = new ProductionWorkExecutionEngine(store);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var service = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter());
        var scope = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH91");
        var request = await engine.CreateRequestAsync(scope, ProductionActions.CreateVideo, "bind", "idem-bind-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        await engine.FreezeInputAsync(task.TaskId, Input(), "fixture", "fixture-motion-v1");
        var attempt = await engine.StartAttemptAsync(task.TaskId, "bind-payload");
        return new Harness(store, files, engine, service, scope, task, attempt);
    }

    static async Task<(ProductionTask Task, ProductionAttempt Attempt)> AnotherTaskAsync(Harness harness)
    {
        var request = await harness.Engine.CreateRequestAsync(
            harness.Scope, ProductionActions.CreateVideo, "bind", "idem-bind-" + Guid.NewGuid().ToString("N"));
        var task = await harness.Engine.CreateTaskAsync(request.RequestId);
        await harness.Engine.FreezeInputAsync(task.TaskId, Input(), "fixture", "fixture-motion-v1");
        var attempt = await harness.Engine.StartAttemptAsync(task.TaskId, "bind-payload");
        return (task, attempt);
    }

    static Task<ProductionArtifact> PlantAsync(Harness harness, byte[] bytes, string status)
        => PlantOnAsync(harness, harness.Task, harness.Attempt, bytes, status);

    static async Task<ProductionArtifact> PlantOnAsync(
        Harness harness, ProductionTask task, ProductionAttempt attempt, byte[] bytes, string status)
    {
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var key = harness.Files.Write("os-bind", attempt.AttemptId.ToString("N"), bytes);
        var artifact = new ProductionArtifact(
            Guid.NewGuid(), task.TaskId, attempt.AttemptId, ProductionArtifactRoles.VideoTake,
            "video/mp4", bytes.Length, 5, sha, key, null, DateTimeOffset.UtcNow, status, sha);
        await harness.Store.InsertArtifactAsync(artifact);
        return artifact;
    }

    static byte[] Bytes(string label) => System.Text.Encoding.UTF8.GetBytes("FAMIXA-BIND-" + label + "-" + new string('B', 40));

    static ShotExecutionInput Input() =>
        new("Minh ngập ngừng, đặt cặp xuống ghế rồi chậm chạp kéo khóa.", "He stays in place.", "sofa 16:9", "CHAR-001", "kf-bind", "5s", "", "hold");

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
        InMemoryProductionArtifactFileStore Files,
        ProductionWorkExecutionEngine Engine,
        ProductionCreateVideoService Service,
        ProductionScope Scope,
        ProductionTask Task,
        ProductionAttempt Attempt);

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
