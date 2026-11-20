using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content;

/// <summary>AUTHORITY-STABILIZATION-01D. Official production progress is OS bindings only.</summary>
public static class ProductionProgressAuthorityV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_PROGRESS_AUTHORITY_01D";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var ready = new ProductionProgressRules.CharacterFact("CHAR-001", "Minh", true, []);
        var stamped = new ProductionProgressRules.ShotFact(
            "EP01-SC01-SH01", "SC01", 1, ["CHAR-001"], true, true, true, true, true, true);
        var cleared = ProductionProgressRules.ApplyOfficialOsProgress(stamped, []);
        var script = Resolve(cleared, scriptLocked: true);
        Ok(script.Stages.First(s => s.Id == "script").Done, "T1 script progress stays on graph lock");
        Ok(script.VideoMade == 0 && script.VideoApproved == 0 && script.ImageMade == 0, "T2/T5/T8 empty OS clears graph-shaped flags");

        var vgeShaped = ProductionProgressRules.ApplyOfficialOsProgress(stamped, []);
        Ok(!vgeShaped.HasWorkflowVideo && !vgeShaped.VideoDirectorApproved, "T3/T4/T7 VGE-shaped flags do not survive an OS read");

        var current = ProductionProgressRules.ApplyOfficialOsProgress(stamped, [
            new ProductionProgressRules.OsBindingFact(
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
                ProductionArtifactRoles.VideoTake,
                ProductionArtifactStatuses.Verified),
        ]);
        var currentProgress = Resolve(current, scriptLocked: true);
        Ok(currentProgress.VideoMade == 1 && currentProgress.VideoApproved == 0, "T6/T9 CURRENT video is made and not approved");
        Ok(current.VideoDirectorApproved == false, "T9 verification does not approve");

        var failed = ProductionProgressRules.ApplyOfficialOsProgress(stamped, [
            new ProductionProgressRules.OsBindingFact(
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
                ProductionArtifactRoles.VideoTake,
                ProductionArtifactStatuses.FailedVerification),
        ]);
        Ok(!failed.HasWorkflowVideo && !failed.VideoDirectorApproved && !failed.Finalized, "T9 failed verification is not production progress");

        var accepted = ProductionProgressRules.ApplyOfficialOsProgress(stamped, [
            new ProductionProgressRules.OsBindingFact(
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.VideoTake),
                ProductionArtifactRoles.VideoTake,
                ProductionArtifactStatuses.Verified),
        ]);
        var acceptedProgress = Resolve(accepted, scriptLocked: true);
        Ok(acceptedProgress.VideoApproved == 1 && accepted.Finalized == false, "T6/T10 ACCEPTED video is not mix-complete");
        Ok(script.Stages.First(s => s.Id == "script").Done && acceptedProgress.Stages.First(s => s.Id == "script").Done, "T10 script stage is independent of the binding");

        var mix = ProductionProgressRules.ApplyOfficialOsProgress(accepted, [
            new ProductionProgressRules.OsBindingFact(
                ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.VideoTake),
                ProductionArtifactRoles.VideoTake,
                ProductionArtifactStatuses.Verified),
            new ProductionProgressRules.OsBindingFact(
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix),
                ProductionArtifactRoles.Mix,
                ProductionArtifactStatuses.Verified),
        ]);
        Ok(mix.Finalized && mix.VideoDirectorApproved, "T10 mix binding is finish, accept binding stays distinct");

        var assemblyOnly = ProductionProgressRules.ApplyOfficialOsProgress(stamped, [
            new ProductionProgressRules.OsBindingFact(
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
                ProductionArtifactRoles.Assembly,
                ProductionArtifactStatuses.Verified),
        ]);
        Ok(assemblyOnly.HasWorkflowVideo && !assemblyOnly.VideoDirectorApproved && !assemblyOnly.Finalized,
            "CURRENT:ASSEMBLY counts as shot video and does not approve or finish");
        return fail;
    }

    static ProductionProgressRules.EpisodeProgress Resolve(ProductionProgressRules.ShotFact shot, bool scriptLocked)
    {
        var scene = new ProductionProgressRules.SceneFact("SC01", "Scene", ["CHAR-001"], [shot]);
        var ready = new ProductionProgressRules.CharacterFact("CHAR-001", "Minh", true, []);
        return ProductionProgressRules.Resolve(new ProductionProgressRules.EpisodeFact(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "FAMIXA", "EP01", "EP01", scriptLocked, [scene], [ready]));
    }
}
