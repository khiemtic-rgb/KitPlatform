namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Supersede keeps the previous ACTIVE selection row and installs one new ACTIVE row.
/// Upsert still refuses a different hash.
/// </summary>
public static class ProductionAiSelectionSupersedeV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_AI_SELECTION_SUPERSEDE_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try { RunCore(Ok); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static void RunCore(Action<bool, string> Ok)
    {
        var build = Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb");
        var store = new InMemoryProductionAiSelectionStore();
        var veo = store.UpsertActiveAsync(Command(build, "google", "veo-3.1", "veo selection")).GetAwaiter().GetResult();
        Ok(veo.Status == ProductionAiSelectionStatuses.Active && veo.ProviderId == "google", "T1 veo is active");

        var conflict = false;
        try
        {
            store.UpsertActiveAsync(Command(build, "runway", "gen4_turbo", "direct replace")).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.SelectionConflict)
        {
            conflict = true;
        }
        Ok(conflict, "T2 upsert of a different provider still conflicts");
        Ok(store.GetActiveAsync(build, "EP01-SC01-SH02", "VIDEO").GetAwaiter().GetResult()?.SelectionId == veo.SelectionId,
            "T2 veo row stays active after the refused upsert");

        var wrong = false;
        try
        {
            store.SupersedeActiveAsync(new SupersedeProductionAiSelectionCommand(
                Guid.NewGuid(), Command(build, "runway", "gen4_turbo", "wrong id"))).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.SelectionConflict)
        {
            wrong = true;
        }
        Ok(wrong, "T3 unexpected selection id conflicts");

        var blank = false;
        try
        {
            store.SupersedeActiveAsync(new SupersedeProductionAiSelectionCommand(
                veo.SelectionId, Command(build, "runway", "gen4_turbo", "  "))).GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.SelectionConflict)
        {
            blank = true;
        }
        Ok(blank, "T4 blank reason does not supersede");

        var done = store.SupersedeActiveAsync(new SupersedeProductionAiSelectionCommand(
            veo.SelectionId, Command(build, "runway", "gen4_turbo", "SH02 runway gen4_turbo"))).GetAwaiter().GetResult();
        Ok(done.Superseded.SelectionId == veo.SelectionId
            && done.Superseded.Status == ProductionAiSelectionStatuses.Superseded
            && done.Superseded.ProviderId == "google"
            && done.Superseded.ModelId == "veo-3.1"
            && done.Superseded.SelectionHash == veo.SelectionHash
            && done.Superseded.Reason == veo.Reason,
            "T5 veo history keeps id, provider, hash, and reason");
        Ok(done.Active.Status == ProductionAiSelectionStatuses.Active
            && done.Active.ProviderId == "runway"
            && done.Active.ModelId == "gen4_turbo"
            && done.Active.SelectionId != veo.SelectionId
            && done.Active.Reason.Contains("supersedes=" + veo.SelectionId.ToString("D"), StringComparison.Ordinal),
            "T5 runway is the only new active row and names the previous id");
        Ok(store.GetActiveAsync(build, "EP01-SC01-SH02", "VIDEO").GetAwaiter().GetResult()?.SelectionId == done.Active.SelectionId,
            "T5 active read returns runway");

        var again = store.SupersedeActiveAsync(new SupersedeProductionAiSelectionCommand(
            done.Active.SelectionId, Command(build, "runway", "gen4_turbo", "repeat"))).GetAwaiter().GetResult();
        Ok(again.Active.SelectionId == done.Active.SelectionId
            && again.Superseded.Status == ProductionAiSelectionStatuses.Active,
            "T6 same hash does not add another row");

        var root = RepoRoot();
        Ok(root is not null, "T7 repository file is readable");
        if (root is null) return;
        var repo = File.ReadAllText(root);
        Ok(repo.Contains("SET status = 'SUPERSEDED'", StringComparison.Ordinal)
            && !repo.Contains("DELETE FROM pack_content.production_ai_selection", StringComparison.Ordinal),
            "T7 repository supersedes and does not delete selection history");
    }

    static UpsertProductionAiSelectionCommand Command(Guid build, string provider, string model, string reason) =>
        new(build, "EP01-SC01-SH02", "EP01", "SC01", "VIDEO", provider, model, "regression", reason);

    static string? RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName,
                "src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionAiSelectionRepository.cs");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }
}
