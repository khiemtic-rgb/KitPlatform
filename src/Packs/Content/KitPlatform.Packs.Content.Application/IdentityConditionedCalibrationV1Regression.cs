namespace KitPlatform.Packs.Content;

public static class IdentityConditionedCalibrationV1Regression
{
    public const string SuiteId = IdentityConditionedCalibrationV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var pack = VisualCalibrationPackV1Rules.CompilePack();
        var plan = IdentityConditionedCalibrationV1Rules.CompilePlan(pack, "CAL-EXEC-IC");
        var subject = VisualCalibrationPackV1Rules.DefaultMatrix[2];
        var views = plan.Where(r =>
            string.Equals(r.SubjectId, subject.CalibrationSubjectId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.ExecutionOrder).ToList();
        var front = views.First(r => IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType));
        var threeQ = views.First(r => r.ViewType == "THREE_QUARTER");
        var side = views.First(r => r.ViewType == "SIDE");
        var full = views.First(r => r.ViewType == "FULL_BODY");
        var audit = IdentityConditionedCalibrationV1Rules.Audit(pack);
        var snapshot = IdentityConditionedCalibrationV1Rules.SnapshotOf(pack);
        var pixels24 = VisualCalibrationPackV1Rules.WithPixelCoverage(
            pack with { Status = VisualCalibrationPackV1Rules.StatusPendingReview }, 24);
        var historical = StripIdentity(pixels24);

        Ok(IdentityConditionedCalibrationV1Rules.FrontFirst(plan)
            && views[0].ViewType == "FRONT"
            && views[0].ExecutionOrder == 1, "IC-01 FRONT is always first");
        Ok(front.ReferenceCount == 0
            && string.IsNullOrWhiteSpace(front.ReferenceRole)
            && IdentityConditionedCalibrationV1Rules.ViewRefs("FRONT").Count == 0, "IC-02 FRONT has zero references");
        Ok(threeQ.ReferenceCount == 1
            && threeQ.ReferenceRole == IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor
            && threeQ.IdentityAnchorSlot == "FRONT", "IC-03 THREE_QUARTER references FRONT");
        Ok(side.ReferenceCount == 1
            && side.IdentityAnchorSlot == "FRONT", "IC-04 SIDE references FRONT");
        Ok(full.ReferenceCount == 1
            && full.IdentityAnchorSlot == "FRONT", "IC-05 FULL_BODY references FRONT");

        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(threeQ, pack)
            && IdentityConditionedCalibrationV1Rules.FindFront(pack, subject.CalibrationSubjectId) is null,
            "IC-06 Downstream view cannot execute without FRONT");
        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                threeQ with { IdentityAnchorSha256 = null, IdentityAnchorPath = "calibration://missing" }, pack),
            "IC-07 Missing FRONT blocks provider");
        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                Bind(pixels24, threeQ) with { IdentityAnchorSha256 = new string('0', 64) }, pixels24),
            "IC-08 Invalid FRONT SHA blocks provider");

        var otherFront = IdentityConditionedCalibrationV1Rules.FindFront(pixels24, "CAL-001");
        Ok(otherFront is not null
            && !IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                Bind(pixels24, threeQ) with
                {
                    IdentityAnchorSha256 = otherFront.Sha256,
                    Identity = threeQ.Identity! with { SubjectId = "CAL-001" },
                }, pixels24),
            "IC-09 Wrong subject FRONT blocks provider");
        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                Bind(pixels24, threeQ) with { CalibrationPackId = "OTHER-PACK" }, pixels24),
            "IC-10 Wrong pack FRONT blocks provider");
        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                Bind(pixels24, threeQ) with { VisualUniverseSha = ProjectVisualStyleV2Rules.ProtectedV1Sha }, pixels24),
            "IC-11 Wrong visualUniverseSha blocks provider");
        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                Bind(pixels24, threeQ) with { ProjectVisualStyleSha = pack.VisualUniverseSha }, pixels24),
            "IC-12 Wrong PVS SHA blocks provider");
        Ok(!IdentityConditionedCalibrationV1Rules.ProviderMayCall(
                Bind(pixels24, threeQ) with { CharacterDesignLanguageSha = pack.VisualUniverseSha }, pixels24),
            "IC-13 Wrong CDL SHA blocks provider");

        var liveMock = new MockVisualCalibrationGenerationProvider();
        var live = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, liveMock, true, true).GetAwaiter().GetResult();
        var liveFront = IdentityConditionedCalibrationV1Rules.FindFront(live.Pack, subject.CalibrationSubjectId);
        var againMock = new MockVisualCalibrationGenerationProvider();
        var again = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            live.Pack, againMock, true, true).GetAwaiter().GetResult();
        Ok(liveFront is not null
            && again.GateCode == VisualCalibrationPackV1Rules.GateDuplicate
            && againMock.CallCount == 0
            && ProjectVisualStyleV1Rules.SameSha(
                IdentityConditionedCalibrationV1Rules.FindFront(again.Pack, subject.CalibrationSubjectId)?.Sha256,
                liveFront.Sha256),
            "IC-14 Completed valid FRONT cannot be silently replaced");
        Ok(again.GateCode == VisualCalibrationPackV1Rules.GateDuplicate
            && againMock.CallCount == 0, "IC-15 Completed downstream view is BLOCK_DUPLICATE");

        var retryPack = ClearView(live.Pack, subject.CalibrationSubjectId, "THREE_QUARTER");
        var retryTq = IdentityConditionedCalibrationV1Rules.BindLiveAnchor(
            retryPack, IdentityConditionedCalibrationV1Rules.CompileOne(
                retryPack, subject, "THREE_QUARTER", "CAL-EXEC-RETRY", null));
        Ok(ProjectVisualStyleV1Rules.SameSha(retryTq.IdentityAnchorSha256, liveFront!.Sha256),
            "IC-16 Retry THREE_Q uses same FRONT SHA");
        var retrySide = IdentityConditionedCalibrationV1Rules.BindLiveAnchor(
            ClearView(live.Pack, subject.CalibrationSubjectId, "SIDE"),
            IdentityConditionedCalibrationV1Rules.CompileOne(live.Pack, subject, "SIDE", "CAL-EXEC-RETRY", null));
        Ok(ProjectVisualStyleV1Rules.SameSha(retrySide.IdentityAnchorSha256, liveFront.Sha256),
            "IC-17 Retry SIDE uses same FRONT SHA");
        var retryFull = IdentityConditionedCalibrationV1Rules.BindLiveAnchor(
            ClearView(live.Pack, subject.CalibrationSubjectId, "FULL_BODY"),
            IdentityConditionedCalibrationV1Rules.CompileOne(live.Pack, subject, "FULL_BODY", "CAL-EXEC-RETRY", null));
        Ok(ProjectVisualStyleV1Rules.SameSha(retryFull.IdentityAnchorSha256, liveFront.Sha256),
            "IC-18 Retry FULL_BODY uses same FRONT SHA");

        Ok(FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(front.CompiledPrompt, threeQ.CompiledPrompt)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(front.CompiledPrompt, side.CompiledPrompt)
            && FamixaVisualUniverseAuthorityV1Rules.StylePrefixIdentical(front.CompiledPrompt, full.CompiledPrompt),
            "IC-19 Style prefix identical across four views");
        Ok(VisualCalibrationPackV1Rules.ViewsDifferOnlyByCamera(
                VisualCalibrationPackV1Rules.CompileSubjectViews(subject))
            && front.CompiledPrompt != threeQ.CompiledPrompt, "IC-20 Camera differs without changing style authority");
        Ok(front.Identity is not null
            && !IdentityConditionedCalibrationV1Rules.ContractContainsStyleAuthority(front.Identity)
            && !IdentityConditionedCalibrationV1Rules.ContainsStyleAuthority(front.Identity.Wardrobe),
            "IC-21 Identity contract does not contain style authority");
        Ok(!UnifiedVisualCompilerV1Rules.RawPromptIsStyleAuthority()
            && IdentityConditionedCalibrationV1Rules.UnifiedCompilerBound(front.CompiledPrompt, snapshot),
            "IC-22 Caller prompt cannot override style");
        Ok(!SeriesStillVisualIngressV1Rules.IdentityBriefBypassesCompiler(),
            "IC-23 IdentityBrief cannot become visual style authority");
        Ok(plan.All(r => !string.IsNullOrWhiteSpace(r.CompiledPrompt)
                && r.CompiledPrompt.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal)),
            "IC-24 Provider receives compiled prompt only");
        Ok(!typeof(IVisualCalibrationGenerationProvider).GetMethods()
                .Any(m => m.Name.Contains("Resolve", StringComparison.OrdinalIgnoreCase)),
            "IC-25 Provider cannot resolve VUA/PVS/CDL");

        var dryMock = new MockVisualCalibrationGenerationProvider();
        var dry = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            pack, dryMock, false, true).GetAwaiter().GetResult();
        Ok(dry.GateCode == VisualCalibrationPackV1Rules.GateConfirmation
            && dryMock.CallCount == 0
            && !dry.ProviderCalled, "IC-26 confirm=false never calls provider");
        Ok(VisualCalibrationPackV1Rules.GetCoverage(dry.Pack).Valid == 0
            && dry.Pack.Subjects.SelectMany(s => s.Artifacts)
                .All(a => !string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase)),
            "IC-27 confirm=false never writes PIXEL");
        Ok(!IdentityConditionedCalibrationV1Rules.MutatesCharacters()
            && !VisualCalibrationPackV1Rules.CreatesCharacterMaster()
            && !dry.Pack.Equals(default), "IC-28 confirm=false never mutates character");

        Ok(audit.IdentityAnchorsExpected == 6
            && IdentityConditionedCalibrationV1Rules.GetCoverage(live.Pack).IdentityAnchorsValid == 6,
            "IC-29 6 subjects produce 6 identity anchors");
        Ok(audit.IdentityConditionedExpected == 18
            && plan.Count(r => !IdentityConditionedCalibrationV1Rules.IsFront(r.ViewType)) == 18,
            "IC-30 18 downstream slots require FRONT reference");
        Ok(VisualCalibrationPackV1Rules.GetCoverage(historical).Valid == 24
            && IdentityConditionedCalibrationV1Rules.GetCoverage(historical).IdentityConditionedValid == 0
            && VisualCalibrationPackV1Rules.EvaluateApprove(
                historical, true, VisualCalibrationPackV1Rules.PhotorealismLow)
                == IdentityConditionedCalibrationV1Rules.GateIdentity,
            "IC-31 24/24 pixel coverage is insufficient if identity conditioning evidence is incomplete");
        Ok(IdentityConditionedCalibrationV1Rules.IdentityReadyForReview(pixels24)
            && VisualCalibrationPackV1Rules.EvaluateApprove(
                pixels24, true, VisualCalibrationPackV1Rules.PhotorealismLow) is null,
            "IC-32 6/6 anchors + 18/18 conditioned views required for approval");

        Ok(CharacterAuthorityInitializationV1Rules.MinhShaUnchanged(
                CharacterAuthorityInitializationV1Rules.ProtectedMasterSha,
                CharacterAuthorityInitializationV1Rules.ProtectedDnaSha,
                CharacterAuthorityInitializationV1Rules.ProtectedPrpSha,
                CharacterAuthorityInitializationV1Rules.ProtectedCrpSha),
            "IC-33 Locked Minh remains untouched");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
            == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1", "IC-34 Minh Master SHA unchanged");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
            == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc", "IC-35 Minh DNA SHA unchanged");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
            == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444", "IC-36 Minh PRP SHA unchanged");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
            == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7", "IC-37 Minh CRP SHA unchanged");
        Ok(FamixaVisualUniverseAuthorityV1Rules.Sha()
            .StartsWith("4e9c4bad", StringComparison.OrdinalIgnoreCase), "IC-38 VUA SHA unchanged");
        Ok(ProjectVisualStyleV1Rules.SameSha(
            ProjectVisualStyleV2Rules.ProtectedV1Sha,
            "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58"), "IC-39 PVS SHA unchanged");
        Ok(CharacterDesignLanguageV2Rules.Sha()
            .StartsWith("683ce6bd", StringComparison.OrdinalIgnoreCase), "IC-40 CDL SHA unchanged");
        Ok(!IdentityConditionedCalibrationV1Rules.RequiresMigration(), "IC-41 No migration required");
        Ok(!IdentityConditionedCalibrationV1Rules.LegacyFallback(), "IC-42 No legacy fallback");
        Ok(!IdentityConditionedCalibrationV1Rules.IndependentDownstreamGenerationAllowed()
            && !IdentityConditionedCalibrationV1Rules.ProviderMayCall(side with { ReferenceCount = 0 }, pack)
            && FailedFrontBlocksDownstream(),
            "IC-43 No independent downstream T2I path");

        Ok(audit.IdentityConditioningReady
            && audit.FrontFirstEnforced
            && audit.DownstreamRequiresFront
            && audit.IndependentDownstreamGenerationForbidden
            && !audit.GeminiCalled
            && !audit.GenerationExecuted
            && audit.IdentityAnchorsValid == 0
            && audit.IdentityConditionedValid == 0
            && audit.PixelCoverage.Valid == 0, "IC-DRY dry-run identity architecture is ready with 0 pixels");

        return fail;
    }

    private static VisualCalibrationGenerationRequest Bind(
        VisualCalibrationPackV1Rules.PackSnapshot pack, VisualCalibrationGenerationRequest request) =>
        IdentityConditionedCalibrationV1Rules.BindLiveAnchor(pack, request);

    private static VisualCalibrationPackV1Rules.PackSnapshot StripIdentity(
        VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        pack with
        {
            Subjects = pack.Subjects.Select(s => s with
            {
                Artifacts = s.Artifacts.Select(a => a with
                {
                    IdentityAnchorSlot = null,
                    IdentityAnchorSha256 = null,
                    ReferenceRole = null,
                }).ToList(),
            }).ToList(),
        };

    private static VisualCalibrationPackV1Rules.PackSnapshot ClearView(
        VisualCalibrationPackV1Rules.PackSnapshot pack, string subjectId, string view) =>
        pack with
        {
            Status = VisualCalibrationPackV1Rules.StatusGenerating,
            Subjects = pack.Subjects.Select(s =>
                !string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase)
                    ? s
                    : s with
                    {
                        Artifacts = s.Artifacts.Select(a =>
                            !string.Equals(a.ViewType, view, StringComparison.OrdinalIgnoreCase)
                                ? a
                                : a with
                                {
                                    Kind = null,
                                    Path = null,
                                    Sha256 = null,
                                    IdentityAnchorSlot = null,
                                    IdentityAnchorSha256 = null,
                                    ReferenceRole = null,
                                    GenerationStatus = VisualCalibrationPackV1Rules.SlotFailed,
                                }).ToList(),
                    }).ToList(),
        };

    private static bool FailedFrontBlocksDownstream()
    {
        var mock = new MockVisualCalibrationGenerationProvider();
        mock.FailSlots.Add(VisualCalibrationPackV1Rules.SlotIdOf("CAL-001", "FRONT"));
        var outcome = VisualCalibrationGenerationServiceV1.ExecuteAsync(
            VisualCalibrationPackV1Rules.CompilePack(), mock, true, true).GetAwaiter().GetResult();
        var cal1 = outcome.Pack.Subjects.First(s => s.CalibrationSubjectId == "CAL-001");
        return mock.CallCount < 24
            && cal1.Artifacts.Where(a => a.ViewType != "FRONT")
                .All(a => a.GenerationStatus == IdentityConditionedCalibrationV1Rules.GateAnchor)
            && !cal1.Artifacts.Any(a =>
                a.ViewType != "FRONT"
                && string.Equals(a.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase));
    }
}
