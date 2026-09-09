namespace KitPlatform.Packs.Content;

public static class IdentityConditionedCalibrationDirectorReviewV1Regression
{
    public const string SuiteId = IdentityConditionedCalibrationDirectorReviewV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var R = IdentityConditionedCalibrationDirectorReviewV1Rules.Pending;
        var P = IdentityConditionedCalibrationDirectorReviewV1Rules.Pass;
        var F = IdentityConditionedCalibrationDirectorReviewV1Rules.Fail;
        var RR = IdentityConditionedCalibrationDirectorReviewV1Rules.ReviewRequired;

        var pack = IdentityConditionedCalibrationDirectorReviewV1Rules.CompleteRun();
        var empty = VisualCalibrationPackV1Rules.CompilePack() with { CalibrationRunId = "CAL-RUN-EMPTY" };
        var workspace = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack);
        var emptyWs = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(empty);
        var subject = VisualCalibrationPackV1Rules.DefaultMatrix[2];
        var front = workspace.Subjects
            .First(s => s.SubjectId == subject.CalibrationSubjectId)
            .Views.First(v => v.View == "FRONT");
        var threeQ = workspace.Subjects
            .First(s => s.SubjectId == subject.CalibrationSubjectId)
            .Views.First(v => v.View == "THREE_QUARTER");
        var side = workspace.Subjects
            .First(s => s.SubjectId == subject.CalibrationSubjectId)
            .Views.First(v => v.View == "SIDE");
        var full = workspace.Subjects
            .First(s => s.SubjectId == subject.CalibrationSubjectId)
            .Views.First(v => v.View == "FULL_BODY");

        Ok(workspace.ViewsFound == 24 && workspace.ViewsExpected == 24
            && workspace.Subjects.SelectMany(s => s.Views).Count() == 24, "DR-01 24-image matrix discovered correctly");
        Ok(workspace.SubjectsFound == 6 && workspace.SubjectsExpected == 6
            && workspace.Subjects.Select(s => s.SubjectId).Distinct().Count() == 6, "DR-02 6 subjects discovered");
        Ok(workspace.Subjects.All(s => s.Views.Count == 4)
            && workspace.Subjects.All(s =>
                s.Views.Select(v => v.View).SequenceEqual(IdentityConditionedCalibrationV1Rules.GenerationOrder)),
            "DR-03 4 views per subject");
        Ok(front.IsIdentityAnchor
            && front.ReferenceRole is "NONE" or null
            && IdentityConditionedCalibrationV1Rules.IsFront(front.View), "DR-04 FRONT recognized as identity anchor");
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.ReferenceCountOf(
                IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(pack, subject.CalibrationSubjectId, "FRONT"),
                "FRONT") == 0, "DR-05 FRONT has zero references");
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.ReferenceCountOf(
                IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(pack, subject.CalibrationSubjectId, "THREE_QUARTER"),
                "THREE_QUARTER") == 1
            && threeQ.ReferenceArtifactSha256 == front.ArtifactSha256, "DR-06 THREE_QUARTER requires FRONT reference");
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.ReferenceCountOf(
                IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(pack, subject.CalibrationSubjectId, "SIDE"),
                "SIDE") == 1
            && side.ReferenceArtifactSha256 == front.ArtifactSha256, "DR-07 SIDE requires FRONT reference");
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.ReferenceCountOf(
                IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(pack, subject.CalibrationSubjectId, "FULL_BODY"),
                "FULL_BODY") == 1
            && full.ReferenceArtifactSha256 == front.ArtifactSha256, "DR-08 FULL_BODY requires FRONT reference");
        Ok(threeQ.ReferenceRole == IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor
            && side.ReferenceRole == IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor
            && full.ReferenceRole == IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor,
            "DR-09 Downstream reference role is IDENTITY_ANCHOR");
        Ok(threeQ.SubjectId == front.SubjectId
            && side.SubjectId == front.SubjectId
            && full.SubjectId == front.SubjectId, "DR-10 Downstream reference subject matches subject");

        var historicalPixels = StripIdentity(pack);
        var isolated = IdentityConditionedCalibrationLiveV1Rules.Isolate(historicalPixels);
        var historicalWs = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(isolated);
        Ok(IdentityConditionedCalibrationLiveV1Rules.HasIndependentHistoricalPixels(historicalPixels)
            && historicalWs.Historical.Count == 24
            && historicalWs.IdentityConditionedValid == 0
            && historicalWs.FrontAnchorsValid == 0
            && historicalWs.Historical.All(h => h.Label == IdentityConditionedCalibrationDirectorReviewV1Rules.HistoricalLabel),
            "DR-11 Historical independent artifacts are excluded");

        var missing = IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(
            empty, subject, "FRONT", null);
        Ok(missing.TechnicalStatus == F && missing.ArtifactIntegrity == F, "DR-12 Missing artifact = technical FAIL");

        var badSha = IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(
            pack, subject.CalibrationSubjectId, "FRONT")! with { Sha256 = "not-a-sha" };
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(pack, subject, "FRONT", badSha)
            .TechnicalStatus == F, "DR-13 Invalid SHA = technical FAIL");

        var badRef = IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(
            pack, subject.CalibrationSubjectId, "THREE_QUARTER")! with
        {
            IdentityAnchorSha256 = VisualCalibrationPackV1Rules.TestPixelSha("CAL-001", "FRONT"),
            ReferenceRole = "NONE",
        };
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(pack, subject, "THREE_QUARTER", badRef)
            .TechnicalStatus == F
            && IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(pack, subject, "THREE_QUARTER", badRef)
                .ReferenceBindingValid == F, "DR-14 Invalid reference = technical FAIL");

        var noVua = IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(
            pack, subject.CalibrationSubjectId, "FRONT")! with { VisualUniverseSha = null };
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(pack, subject, "FRONT", noVua)
            .AuthorityBindingValid == F, "DR-15 Missing VUA SHA = technical FAIL");

        var noPvs = IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(
            pack, subject.CalibrationSubjectId, "FRONT")! with { ProjectVisualStyleSha = null };
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(pack, subject, "FRONT", noPvs)
            .AuthorityBindingValid == F, "DR-16 Missing PVS SHA = technical FAIL");

        var noCdl = IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(
            pack, subject.CalibrationSubjectId, "FRONT")! with { CharacterDesignLanguageSha = null };
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(pack, subject, "FRONT", noCdl)
            .AuthorityBindingValid == F, "DR-17 Missing CDL SHA = technical FAIL");

        var techFail = IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(empty, subject, "FRONT", null);
        var allPassGates = new IdentityConditionedCalibrationVisualGates(P, P, P, P, P, P, P, P, P, P);
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.MayDirectorPass(techFail, allPassGates, P),
            "DR-18 Director PASS blocked when technical gates fail");

        var techOk = IdentityConditionedCalibrationDirectorReviewV1Rules.EvaluateTechnical(
            pack, subject, "FRONT",
            IdentityConditionedCalibrationDirectorReviewV1Rules.CurrentArtifact(pack, subject.CalibrationSubjectId, "FRONT"));
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.MayDirectorPass(techOk, allPassGates, P)
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.MayDirectorPass(
                techOk, IdentityConditionedCalibrationDirectorReviewV1Rules.PendingGates(), P),
            "DR-19 Director PASS allowed only after all required visual gates are explicitly PASS");

        var pendingWs = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack);
        Ok(pendingWs.PackDecision == IdentityConditionedCalibrationDirectorReviewV1Rules.PackNotReviewed
            && !pendingWs.VisualPass
            && pendingWs.Subjects.All(s => s.Decision == R && s.SubjectReviewStatus == R),
            "DR-20 PENDING cannot become PASS automatically");

        var failedDecision = IdentityConditionedCalibrationDirectorReviewV1Rules.ApplyDecision(
            techOk,
            new IdentityConditionedCalibrationDirectorDecision(
                "REV-FAIL", pack.CalibrationRunId!, pack.PackId, subject.CalibrationSubjectId,
                allPassGates, F, "identity drift",
                IdentityConditionedCalibrationDirectorReviewV1Rules.ReasonIdentityDrift,
                DateTimeOffset.UtcNow, "director"),
            null);
        var keptFail = IdentityConditionedCalibrationDirectorReviewV1Rules.ApplyDecision(
            techOk,
            new IdentityConditionedCalibrationDirectorDecision(
                "REV-FAIL", pack.CalibrationRunId!, pack.PackId, subject.CalibrationSubjectId,
                allPassGates, R, null, null, DateTimeOffset.UtcNow, "director"),
            failedDecision);
        Ok(failedDecision.Decision == F && keptFail.Decision == F, "DR-21 FAIL remains FAIL until Director changes it");

        var reviewReq = PassAllBut(pack, subject.CalibrationSubjectId, gates =>
            gates with { IdentityGate = RR }, RR);
        Ok(reviewReq.PackDecision == RR && !reviewReq.VisualPass, "DR-22 REVIEW_REQUIRED blocks pack PASS");

        var oneFail = PassAllBut(pack, subject.CalibrationSubjectId, gates =>
            gates with { IdentityGate = F }, F);
        Ok(oneFail.PackDecision == F && oneFail.SubjectPass == 5 && !oneFail.VisualPass,
            "DR-23 One failed subject blocks pack PASS");

        var missingViewPack = ClearView(pack, subject.CalibrationSubjectId, "SIDE");
        var missingWs = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
            missingViewPack, AllSubjectPass(pack));
        Ok(missingWs.ViewsFound == 24
            && missingWs.TechnicalIntegrityValid < 24
            && missingWs.PackDecision != P
            && !missingWs.VisualPass, "DR-24 One missing view blocks pack PASS");

        var cross = PassAllBut(pack, subject.CalibrationSubjectId, gates =>
            gates with { CrossCharacterGate = F }, F);
        Ok(cross.PackDecision == F && !cross.VisualPass, "DR-25 Cross-character collision blocks pack PASS");

        var photo = PassAllBut(pack, subject.CalibrationSubjectId, gates =>
            gates with { PhotorealismGate = F }, F);
        Ok(photo.PackDecision == F && !photo.VisualPass, "DR-26 Photorealism leakage blocks pack PASS");

        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.RegeneratesOnFail()
            && failedDecision.Decision == F, "DR-27 No regeneration on FAIL");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.CallsGemini()
            && !workspace.GeminiCalled, "DR-28 No Gemini call");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.CreatesPixels()
            && workspace.PixelArtifactCreated == 0, "DR-29 No image provider call");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.CreatesVideo()
            && workspace.VideoArtifactCreated == 0, "DR-30 No video provider call");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.MutatesAuthority()
            && FamixaVisualUniverseAuthorityV1Rules.Sha()
                == "4e9c4bad9ee0d241d1896846a574828865dcdd9e96e18751c251b3c77ea76726",
            "DR-31 No VUA mutation");
        Ok(ProjectVisualStyleV2Rules.ProtectedV1Sha
            == "d48e4884f6ac3315c887dfd139aae86510822d15ec8cc8705e8980629547de58",
            "DR-32 No PVS mutation");
        Ok(CharacterDesignLanguageV2Rules.Sha()
            == "683ce6bd64b1588c38db325cf4ce724d4f66be48b581b7f044ab25a065c88153",
            "DR-33 No CDL mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedMasterSha
            == "be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1",
            "DR-34 No Minh mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedDnaSha
            == "75ececad8899211ce31107232fe0288c11a9e113c5bc0e7c0a6c9f749d72f4dc",
            "DR-35 No DNA mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedPrpSha
            == "5e61ad240aaebaa13dcd91463a41ef9f9c0498fabefe86b7e8b1a1ad973a9444",
            "DR-36 No PRP mutation");
        Ok(CharacterAuthorityInitializationV1Rules.ProtectedCrpSha
            == "82543a4a4331e32a79a865fc3881c17e8bc3dc74c5dec51c52c2deab1a70c2b7",
            "DR-37 No CRP mutation");
        Ok(!IdentityConditionedCalibrationDirectorReviewV1Rules.RequiresMigration()
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.MutatesDatabase(),
            "DR-38 No migration");
        Ok(historicalWs.Historical.Count == 24
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.CountsHistorical()
            && IdentityConditionedCalibrationLiveV1Rules.HasIndependentHistoricalPixels(historicalPixels),
            "DR-39 Historical artifacts remain untouched");
        Ok(workspace.CalibrationRunId == pack.CalibrationRunId
            && workspace.Subjects.SelectMany(s => s.Views).All(v => v.CalibrationRunId == pack.CalibrationRunId),
            "DR-40 Review is bound to CalibrationRunId");

        var mixed = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
            pack, AllSubjectPass(pack), false, "CAL-RUN-OTHER");
        Ok(mixed.GateCode == IdentityConditionedCalibrationDirectorReviewV1Rules.GateRunMix
            && mixed.PackDecision == IdentityConditionedCalibrationDirectorReviewV1Rules.PackNotReviewed
            && mixed.SubjectPass == 0, "DR-41 Review cannot mix runs");

        Ok(workspace.TechnicalIntegrityValid == 24
            && workspace.FrontAnchorsValid == 6
            && workspace.IdentityConditionedValid == 18
            && !workspace.VisualPass
            && workspace.PackDecision != P, "DR-42 Technical PASS does not imply visual PASS");

        var ready = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack, AllSubjectPass(pack));
        Ok(ready.TechnicalIntegrityValid == 24 && ready.TechnicalIntegrityExpected == 24,
            "DR-43 Pack PASS requires 24/24 technical artifacts");
        Ok(ready.FrontAnchorsValid == 6 && ready.FrontAnchorsExpected == 6,
            "DR-44 Pack PASS requires 6/6 anchors");
        Ok(ready.IdentityConditionedValid == 18 && ready.IdentityConditionedExpected == 18,
            "DR-45 Pack PASS requires 18/18 conditioned views");
        Ok(ready.SubjectPass == 6 && ready.PackDecision == P && !ready.VisualPass,
            "DR-46 Pack PASS requires 6/6 subject PASS");
        Ok(ready.Subjects.All(s => s.Gates.AllPass()), "DR-47 All required visual gates must PASS");
        Ok(ready.ReviewRequired == 0, "DR-48 No unresolved REVIEW_REQUIRED");
        Ok(ready.Fail == 0, "DR-49 No unresolved FAIL");

        var marked = IdentityConditionedCalibrationDirectorReviewV1Rules.MarkVisualPass(ready, true);
        var blocked = IdentityConditionedCalibrationDirectorReviewV1Rules.MarkVisualPass(workspace, true);
        Ok(!workspace.VisualPass
            && !ready.VisualPass
            && marked.VisualPass
            && !blocked.VisualPass
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.AutoPassFromTechnical()
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.FoundationComplete()
            && IdentityConditionedCalibrationDirectorReviewV1Rules.AuthorityShaUnchanged(),
            "DR-50 VisualPass remains false until explicit Director PASS");

        var hist = StripIdentity(pack) with
        {
            CalibrationRunId = null,
            GenerationExecutionId = "CAL-EXEC-STABLE-REVIEW",
        };
        var boundA = IdentityConditionedCalibrationDirectorReviewV1Rules.BindReviewRun(hist);
        var boundB = IdentityConditionedCalibrationDirectorReviewV1Rules.BindReviewRun(hist);
        Ok(boundA.CalibrationRunId == "CAL-EXEC-STABLE-REVIEW"
            && boundB.CalibrationRunId == boundA.CalibrationRunId
            && boundA.HistoricalPixelCount >= 0,
            "DR-51 BindReviewRun keeps a stable store key so Save Review can persist");

        var directorReady = IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(
            hist, AllSubjectPass(pack with { CalibrationRunId = "CAL-EXEC-STABLE-REVIEW" }));
        Ok(IdentityConditionedCalibrationDirectorReviewV1Rules.CanRecordDirectorVisualPass(directorReady)
            && !IdentityConditionedCalibrationDirectorReviewV1Rules.CanMarkVisualPass(directorReady),
            "DR-52 Director 6/6 PASS can record Visual Pass without isolated technical 24/24");

        return fail;
    }

    private static IReadOnlyDictionary<string, IdentityConditionedCalibrationDirectorDecision> AllSubjectPass(
        VisualCalibrationPackV1Rules.PackSnapshot pack)
    {
        var map = new Dictionary<string, IdentityConditionedCalibrationDirectorDecision>(StringComparer.OrdinalIgnoreCase);
        foreach (var subject in VisualCalibrationPackV1Rules.DefaultMatrix)
            map[subject.CalibrationSubjectId] = IdentityConditionedCalibrationDirectorReviewV1Rules.AllPassDecision(
                subject.CalibrationSubjectId, pack.CalibrationRunId!, pack.PackId);
        return map;
    }

    private static IdentityConditionedCalibrationPackReview PassAllBut(
        VisualCalibrationPackV1Rules.PackSnapshot pack,
        string subjectId,
        Func<IdentityConditionedCalibrationVisualGates, IdentityConditionedCalibrationVisualGates> mutate,
        string decision)
    {
        var map = AllSubjectPass(pack).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var current = map[subjectId];
        map[subjectId] = current with { Gates = mutate(current.Gates), Decision = decision };
        return IdentityConditionedCalibrationDirectorReviewV1Rules.BuildPackReview(pack, map);
    }

    private static VisualCalibrationPackV1Rules.PackSnapshot StripIdentity(
        VisualCalibrationPackV1Rules.PackSnapshot pack) =>
        pack with
        {
            CalibrationRunId = null,
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
                                    ExecutionId = null,
                                    CompiledPromptSha = null,
                                }).ToList(),
                    }).ToList(),
        };
}
