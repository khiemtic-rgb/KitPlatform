namespace KitPlatform.Packs.Content;

public static class VisualUniverseLockReadinessV1Regression
{
    public const string SuiteId = VisualUniverseLockReadinessV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var report = VisualUniverseLockReadinessV1Rules.Audit();
        var liveDraft = FamixaVisualUniverseAuthorityV1Rules.ToDto(
            FamixaVisualUniverseAuthorityV1Rules.StatusDraft, false);

        Ok(report.CurrentAuthority.Status is FamixaVisualUniverseAuthorityV1Rules.StatusDraft
            or FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationPending
            or FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationReady
            or FamixaVisualUniverseAuthorityV1Rules.StatusPendingReview
            or FamixaVisualUniverseAuthorityV1Rules.StatusApproved
            or FamixaVisualUniverseAuthorityV1Rules.StatusLocked
            or FamixaVisualUniverseAuthorityV1Rules.StatusRejected
            or FamixaVisualUniverseAuthorityV1Rules.StatusSuperseded
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(report.CurrentAuthority.Sha),
            "A-01 Current authority resolved");
        Ok(FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(report.Candidate.Sha)
            && report.Candidate.Sha == FamixaVisualUniverseAuthorityV1Rules.Sha()
            && report.Candidate.CandidateIsAuthority == FamixaVisualUniverseAuthorityV1Rules.IsAuthority(report.Candidate.Status),
            "A-02 Candidate resolved");
        Ok(report.Pvs.SnapshotBindsV1
            && report.Pvs.V1Status == "ACTIVE"
            && report.Pvs.V2Status == "DRAFT"
            && report.Pvs.V2Unused,
            "A-03 PVS resolved");
        Ok(report.Cdl.Sha == CharacterDesignLanguageV2Rules.Sha()
            && report.Cdl.Version == CharacterDesignLanguageV2Rules.Version,
            "A-04 CDL resolved");
        Ok(report.CalibrationMatrix.Total == 24
            && report.CalibrationMatrix.ExpectedShape,
            "A-05 Calibration resolved");
        Ok(report.Snapshot.Resolver == "VisualUniverseSnapshotResolver"
            && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(report.Snapshot.VisualUniverseSha),
            "A-06 Snapshot resolved");
        Ok(report.Compiler.LiveMasterStillVideo == "IUnifiedVisualCompiler"
            && report.Master.Compiler == "IUnifiedVisualCompiler",
            "A-07 Unified compiler resolved");
        Ok(report.Master.Compiled && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(report.Master.CompiledPromptSha),
            "A-08 Master compiled");
        Ok(report.Still.Compiled && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(report.Still.CompiledPromptSha),
            "A-09 Still compiled");
        Ok(report.Video.Compiled && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(report.Video.CompiledPromptSha),
            "A-10 Video compiled");
        Ok(report.MasterStillVideoConsistency.Same
            && report.Master.StyleLayer == report.Still.StyleLayer
            && report.Still.StyleLayer == report.Video.StyleLayer,
            "A-11 Master/Still/Video canonical style equality");
        Ok(report.CalibrationMatrix.Total == 24
            && report.CalibrationMatrix.Slots.Select(s => s.SlotId).Distinct().Count() == 24,
            "A-12 24 calibration slots");
        Ok(report.CalibrationMatrix.IdenticalAuthorityBindings
            && report.CalibrationMatrix.Valid == 24
            && report.CalibrationMatrix.Mismatch == 0,
            "A-13 24 identical authority bindings");
        Ok(report.AgeAppearance.ChildKeepsStyle
            && report.AgeAppearance.OlderKeepsStyle
            && report.AgeAppearance.ChildOlderPrefixIdentical,
            "A-14 Age cannot override style");
        Ok(report.AgeAppearance.StereotypeTokensPresent
            && report.AgeAppearance.OlderKeepsStyle,
            "A-15 Appearance cannot override style");
        Ok(report.PromptAuthority.StyleLayerUnchanged
            && report.PromptAuthority.PrefixIdentical,
            "A-16 IdentityBrief cannot override style");
        Ok(report.PromptAuthority.SceneLacksPhotoreal
            && report.PromptAuthority.SurfacesSharePrefix,
            "A-17 raw prompt cannot override style");
        Ok(report.PhotorealismCeiling.InjectionCannotRaise
            && report.PhotorealismCeiling.SnapshotCeiling == "LOW",
            "A-18 photorealism injection blocked");
        Ok(!CharacterFirstMasterVisualIngressV1Rules.LegacyStyleFallback()
            && !SeriesStillVisualIngressV1Rules.LegacyStyleFallback()
            && !VideoVisualIngressV1Rules.LegacyStyleFallback(),
            "A-19 legacy fallback absent");
        Ok(CharacterFirstMasterVisualIngressV1Rules.CompileMaster(null, "CHAR-099", null, null, null, null).Gate
            == CharacterFirstMasterVisualIngressV1Rules.GateSnapshot,
            "A-20 missing snapshot blocked");
        Ok(CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(
                CharacterFirstMasterVisualIngressV1Rules.DefaultSnapshot() with { CdlSha = "" })
            == CharacterFirstMasterVisualIngressV1Rules.GateCdlSha,
            "A-21 missing SHA blocked");
        Ok(VideoVisualIngressV1Rules.ProviderMayCall(
                VideoVisualIngressV1Rules.ToProviderRequest(
                    VideoVisualIngressV1Rules.CompileVideo(
                        CharacterFirstMasterVisualIngressV1Rules.DefaultSnapshot(),
                        new SeriesStillVisualIngressV1Rules.SceneVisualContract(
                            CharacterId: "CHAR-099", Motion: "blink", Framing: "VIDEO")).Contract!,
                    [1], "image/jpeg", new string('a', 64), 5, "1280x720", "24", "16:9", "V1")),
            "A-22 provider receives compiled contract");
        Ok(VideoVisualIngressV1Rules.ProviderCannotDefineStyle()
            && SeriesStillVisualIngressV1Rules.ProviderCannotDefineStyle()
            && CharacterFirstMasterVisualIngressV1Rules.ProviderCannotDefineStyle(),
            "A-23 provider cannot resolve style");
        Ok(report.LockImpact.MinhMutationForbidden
            && report.LockImpact.MinhWouldNotStale
            && report.ShaProtection,
            "A-24 Minh protected");
        Ok(!VisualUniverseLockReadinessV1Rules.MutatesAuthority()
            && report.ShaBefore.Vua == report.ShaAfter.Vua
            && report.ShaBefore.PvsV1 == report.ShaAfter.PvsV1
            && report.ShaBefore.Cdl == report.ShaAfter.Cdl,
            "A-25 no authority mutation");
        Ok(!VisualUniverseLockReadinessV1Rules.MutatesDatabase()
            && report.Safety.DatabaseMutation == 0,
            "A-26 no database mutation");
        Ok(report.Safety.Gemini == 0 && report.Safety.Runway == 0 && report.Safety.Fal == 0
            && !VisualUniverseLockReadinessV1Rules.CallsGemini(),
            "A-27 no provider call");
        Ok(report.Safety.Generation == 0 && report.Safety.VideoGeneration == 0
            && report.Safety.PixelArtifactsCreated == 0
            && VisualCalibrationGenerationServiceV1.DryRun(VisualCalibrationPackV1Rules.CompilePack()).ProviderCalled == false,
            "A-28 no generation");
        Ok(!report.LockImpact.MayLockNow
            || FamixaVisualUniverseAuthorityV1Rules.MayLock(liveDraft.Status) == report.LockImpact.MayLockNow,
            "A-29 lock impact preview");
        Ok(report.PromptPreview.Count == 3
            && report.PromptPreview.All(p => p.StartsWithCanonical && p.CanonicalSection.Length > 80)
            && report.PromptPreview.Any(p => p.SubjectId == "CAL-001")
            && report.PromptPreview.Any(p => p.SubjectId == "CAL-005"),
            "A-30 actual prompt inspection");
        Ok(report.OverallStatus == VisualUniverseLockReadinessV1Rules.ReadyWithRisks
            && report.DirectorDecision == VisualUniverseLockReadinessV1Rules.RecommendGenerate,
            "A-31 audit verdict is READY_WITH_RISKS / GENERATE_CALIBRATION");
        Ok(report.Pvs.Verdict == VisualUniverseLockReadinessV1Rules.PassWithRisk
            && report.Cdl.Verdict == VisualUniverseLockReadinessV1Rules.Pass
            && report.FamixaLook.Score == VisualUniverseLockReadinessV1Rules.LookPartial
            && !report.Contemporary.PvsV1BlocksRural
            && report.Contemporary.CdlBlocksRuralDefault,
            "A-32 risk fields remain explicit (no silent fix)");
        Ok(VideoVisualIngressV1Regression.Run().Count == 0
            && SeriesStillVisualIngressV1Regression.Run().Count == 0
            && CharacterFirstMasterVisualIngressV1Regression.Run().Count == 0,
            "A-33 existing ingress suites remain PASS");

        return fail;
    }
}
