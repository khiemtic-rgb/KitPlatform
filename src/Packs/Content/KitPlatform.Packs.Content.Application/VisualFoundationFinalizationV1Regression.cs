namespace KitPlatform.Packs.Content;

public static class VisualFoundationFinalizationV1Regression
{
    public const string SuiteId = VisualFoundationFinalizationV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var report = VisualFoundationFinalizationV1Rules.Audit();
        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var plan = VisualCalibrationGenerationServiceV1.CompilePlan(pack, "CAL-EXEC-F-REG");
        var snapshot = VisualUniverseSnapshotV1Rules.FromAuthorities(
            FamixaVisualUniverseAuthorityV1Rules.ProjectId,
            FamixaVisualUniverseAuthorityV1Rules.ToDto(
                FamixaVisualUniverseAuthorityV1Rules.StatusDraft, false),
            ProjectVisualStyleV2Rules.ProtectedV1Sha);
        var still = UnifiedVisualCompilerV1Rules.Compile(snapshot);
        var video = VideoVisualIngressV1Rules.CompileVideo(
            snapshot, new SeriesStillVisualIngressV1Rules.SceneVisualContract(
                ProjectId: FamixaVisualUniverseAuthorityV1Rules.ProjectId,
                Action: "walk", Framing: "VIDEO"));

        Ok(report.DocumentId == VisualFoundationFinalizationV1Rules.DocumentId, "F-01 document");
        Ok(!VisualFoundationFinalizationV1Rules.CallsGemini()
            && !VisualFoundationFinalizationV1Rules.CreatesPixels()
            && !VisualFoundationFinalizationV1Rules.MutatesAuthority()
            && !VisualFoundationFinalizationV1Rules.MutatesCharacters()
            && !VisualFoundationFinalizationV1Rules.Approves()
            && !VisualFoundationFinalizationV1Rules.Locks()
            && !VisualFoundationFinalizationV1Rules.Promotes(),
            "F-02 audit does not generate/mutate/approve");
        Ok(!VisualFoundationFinalizationV1Rules.TechnicalPassIsVisualPass()
            && !VisualFoundationFinalizationV1Rules.VisualStylePassIsIdentityPass(),
            "F-03 TECHNICAL ≠ VISUAL ≠ IDENTITY");
        Ok(report.StyleAuthority == VisualFoundationFinalizationV1Rules.GatePass
            && report.Compiled24
            && plan.Count == 24, "F-04 style authority + 24 compile");
        Ok(report.IdentityReadiness == VisualFoundationFinalizationV1Rules.GatePass
            && !VisualFoundationFinalizationV1Rules.CalibrationGeneratesIndependently()
            && VisualFoundationFinalizationV1Rules.CalibrationUsesIdentityRefs()
            && IdentityConditionedCalibrationV1Rules.FrontFirst(plan),
            "F-05 calibration FRONT-first identity refs");
        Ok(plan.All(r => !VisualFoundationFinalizationV1Rules.CalibrationRequestHasIdentityAuthority(r)),
            "F-06 calibration requests do not carry Master SHA");
        Ok(report.CameraInvariantCompile == VisualFoundationFinalizationV1Rules.GatePass
            && VisualFoundationFinalizationV1Rules.CameraInvariantCompile(pack),
            "F-07 camera invariant holds at compile");
        Ok(report.CameraInvariantPixel == VisualFoundationFinalizationV1Rules.GateFail
            && !VisualFoundationFinalizationV1Rules.CameraMayRedesignIdentity(),
            "F-08 camera invariant still unproven at pixel; independent redesign forbidden");
        Ok(CharacterStudioIdentityLockV1Rules.ViewRefs("FRONT").SequenceEqual(["MASTER"])
            && CharacterStudioIdentityLockV1Rules.ViewRefs("FULL_BODY").SequenceEqual(["MASTER", "FRONT"])
            && report.ProductionIdentityRefPolicy,
            "F-09 production identity ref policy already exists");
        Ok(!VisualFoundationFinalizationV1Rules.MasterShaBindsProviderIdentity(),
            "F-10 Master SHA is not a provider identity bind");
        Ok(report.NoWatermark == VisualFoundationFinalizationV1Rules.GateFail
            && !VisualFoundationFinalizationV1Rules.WatermarkForbiddenInCalibrationNegatives(),
            "F-11 watermark/logo not in calibration negatives");
        Ok(SeriesStillVisualIngressV1Rules.ProviderMayCall(still)
            && video.Contract is not null
            && VideoVisualIngressV1Rules.ProviderMayCall(video.Contract)
            && CharacterFirstMasterVisualIngressV1Rules.IngressInvariant(),
            "F-12 unified ingress provider boundary");
        Ok(!SeriesStillVisualIngressV1Rules.IdentityBriefBypassesCompiler()
            && !UnifiedVisualCompilerV1Rules.RawPromptIsStyleAuthority()
            && report.ProviderBoundary == VisualFoundationFinalizationV1Rules.GatePass,
            "F-13 raw prompt / IdentityBrief cannot redefine style");
        Ok(CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha)
            && report.LockedCharacters == VisualFoundationFinalizationV1Rules.GatePass,
            "F-14 locked Minh SHA protection");
        Ok(report.AuthorityUnchanged
            && VisualFoundationFinalizationV1Rules.SameCapture(report.Before, report.After),
            "F-15 audit does not change authority SHAs");
        Ok(report.FoundationState == VisualFoundationFinalizationV1Rules.FoundationNotReady
            && !VisualFoundationFinalizationV1Rules.IsFoundationReady(
                true, true, false, false, true),
            "F-16 foundation not ready until identity pixels + watermark");
        Ok(report.NextStep == VisualFoundationFinalizationV1Rules.NextIdentityConditioning
            && !VisualFoundationFinalizationV1Rules.CreatesSecondStyleCompiler()
            && !VisualFoundationFinalizationV1Rules.ReplacesPvsOrCdlOrVua(),
            "F-17 next step is identity conditioning, not a new style compiler");
        Ok(report.PassSeparation == VisualFoundationFinalizationV1Rules.GatePass
            && report.VisualReadiness == VisualFoundationFinalizationV1Rules.GateFail,
            "F-18 visual PASS is Director-only; not implied by compile");

        return fail;
    }
}
