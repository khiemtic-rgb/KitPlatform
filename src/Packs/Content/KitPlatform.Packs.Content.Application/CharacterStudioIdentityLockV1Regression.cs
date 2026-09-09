using System.Linq;

namespace KitPlatform.Packs.Content;

public static class CharacterStudioIdentityLockV1Regression
{
    public const string SuiteId = CharacterStudioIdentityLockV1Rules.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var style = ProjectVisualStyleV1Rules.PresetOf("3D_STYLIZED_REALISM")!;
        var styleSha = ProjectVisualStyleV1Rules.Sha(style);
        var stylePrompt = ProjectVisualStyleV1Rules.BuildPrompt(style);
        var sha = new string('b', 64);
        var minhMaster = CharacterAuthorityInitializationV1Rules.ProtectedMasterSha;
        var minhDna = CharacterAuthorityInitializationV1Rules.ProtectedDnaSha;
        var minhPrp = CharacterAuthorityInitializationV1Rules.ProtectedPrpSha;
        var minhCrp = CharacterAuthorityInitializationV1Rules.ProtectedCrpSha;
        var age = CharacterAgeConsistencyV1Rules.FromCanonicalAge(38);
        var identity = CharacterStudioV1Rules.IdentityBrief(
            "CHAR-099", "Lan", 38, "male", "STYLE_3D_STYLIZED_REALISM", "adult", "Bố");
        var request = CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
            "CHAR-099", "ERA-01", styleSha, stylePrompt, identity, sha, sha, sha, sha, age, "male");

        var prompts = CharacterStudioIdentityLockV1Rules.GenerationOrder.ToDictionary(
            v => v,
            v => CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(request, v),
            StringComparer.OrdinalIgnoreCase);
        var sideChained = CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(
            request, "SIDE", CharacterStudioIdentityLockV1Rules.ViewRefs("SIDE"));

        Ok(prompts.Values.All(CharacterStudioIdentityLockV1Rules.PromptHasStudioLock)
            && prompts.Values.All(p => !CharacterStudioIdentityLockV1Rules.PromptHasAuthoritySha(p))
            && CharacterAgeGenerationIntegrationV1Rules.ViewsDifferOnlyByCamera(prompts)
            && sideChained.Contains("FRONT", StringComparison.Ordinal),
            "01 A studio lock on every view, no Master/DNA/PRP SHA, camera is the only shared-ref delta");

        Ok(prompts["SIDE"].Contains("90-degree", StringComparison.OrdinalIgnoreCase)
            && prompts["SIDE"].Contains("not three-quarter", StringComparison.OrdinalIgnoreCase)
            && prompts["FRONT"].Contains("0 degrees", StringComparison.OrdinalIgnoreCase),
            "02 A camera grid: FRONT 0°, SIDE true profile");

        var uneval = CharacterStudioV1Rules.ScoreSlot("FRONT", true, false, false);
        var declared = CharacterStudioV1Rules.ScoreSlot(
            "FRONT", true, false, false, CharacterStudioIdentityLockV1Rules.DeclaredPassScores("FRONT"));
        Ok(CharacterStudioIdentityLockV1Rules.IsUnevaluated(uneval.Verdict)
            && CharacterStudioIdentityLockV1Rules.IsPass(declared.Verdict)
            && uneval.Overall == 0,
            "03 A no default 96 — missing Vision is NOT_EVALUATED");

        Ok(CharacterStudioIdentityLockV1Rules.ViewRefs("FRONT").SequenceEqual(["MASTER"])
            && CharacterStudioIdentityLockV1Rules.ViewRefs("THREE_QUARTER").SequenceEqual(["MASTER", "FRONT"])
            && CharacterStudioIdentityLockV1Rules.ViewRefs("SIDE").SequenceEqual(["MASTER", "FRONT"])
            && CharacterStudioIdentityLockV1Rules.ViewRefs("FULL_BODY").SequenceEqual(["MASTER", "FRONT"])
            && CharacterStudioV1Rules.ChainDependencies("THREE_QUARTER").Contains("FRONT")
            && !CharacterStudioV1Rules.ChainDependencies("SIDE").Contains("FULL_BODY"),
            "04 B FRONT-first; later views lock to Master+FRONT");

        Ok(CharacterStudioIdentityLockV1Rules.GenerationOrder[0] == "FRONT"
            && CharacterStudioIdentityLockV1Rules.GenerationOrder.SequenceEqual(
                ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"]),
            "05 B generation order starts with FRONT");

        var parsed = CharacterStudioIdentityLockV1Rules.ParseScores(
            """{"face":91,"hair":90,"age":88,"gender":96,"skin":92,"structure":90,"proportion":90,"clothing":93,"style":91,"crossView":90}""");
        var visionSlot = CharacterStudioV1Rules.ScoreSlot("SIDE", true, false, false, parsed);
        Ok(parsed is not null && parsed["face"] == 91
            && CharacterStudioIdentityLockV1Rules.IsPass(visionSlot.Verdict)
            && visionSlot.Face == 91,
            "06 C Vision JSON maps to slot scores");

        var ageFail = CharacterStudioV1Rules.ScoreSlot("FRONT", true, false, false,
            new Dictionary<string, int>(CharacterStudioIdentityLockV1Rules.DeclaredPassScores("FRONT"))
            {
                ["age"] = 20,
                ["face"] = 20,
            });
        Ok(CharacterStudioIdentityLockV1Rules.IsFail(ageFail.Verdict)
            && CharacterStudioV1Rules.FailedSlots([ageFail]).SequenceEqual(["FRONT"])
            && CharacterStudioV1Rules.FailedSlots([uneval]).Count == 0,
            "07 C FAIL only from Vision; NOT_EVALUATED is not a repair target");

        Ok(CharacterStudioIdentityLockV1Rules.VisionUserPrompt("SIDE", 35, 41)
                .Contains("true 90-degree", StringComparison.OrdinalIgnoreCase)
            && !CharacterStudioIdentityLockV1Rules.VisionUserPrompt("FRONT", 35, 41)
                .Contains("Nam", StringComparison.Ordinal)
            && !CharacterStudioIdentityLockV1Rules.VisionUserPrompt("FRONT", 35, 41)
                .Contains("Minh", StringComparison.Ordinal),
            "08 C Vision prompt is view+age only, no character name");

        var mock = new MockCharacterStudioIdentityJudge();
        var scored = mock.ScoreViewAsync("FRONT", [1, 2, 3], [4, 5, 6], 35, 41, CancellationToken.None)
            .GetAwaiter().GetResult();
        Ok(mock.Calls == 1 && scored is not null && scored["face"] >= 90
            && !CharacterStudioIdentityLockV1Rules.AutoApprove()
            && !CharacterStudioIdentityLockV1Rules.AutoLock()
            && !CharacterStudioIdentityLockV1Rules.CallsGemini()
            && !CharacterStudioIdentityLockV1Rules.GeneratesVideo()
            && !CharacterStudioIdentityLockV1Rules.UsesCharacterName()
            && !CharacterStudioIdentityLockV1Rules.WritesOfficialMinhTables(),
            "09 mock judge / no auto / no video / no name / no official Minh write");

        Ok(CharacterStudioV1Rules.ProtectedMinhUnchanged(minhMaster, minhDna, minhPrp, minhCrp),
            "10 Minh authority SHA unchanged");

        var ready = CharacterStudioV1Rules.RequiredViews
            .Select(t => CharacterStudioV1Rules.ScoreSlot(
                t, true, false, false, CharacterStudioIdentityLockV1Rules.DeclaredPassScores(t)))
            .ToList();
        Ok(CharacterStudioIdentityLockV1Rules.SlotsReadyForDirector(ready)
            && CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(false, ready)
            && !CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(false, [uneval])
            && CharacterStudioIdentityLockV1Rules.SlotsNeedVision([uneval])
            && !CharacterStudioIdentityLockV1Rules.AutoApprove(),
            "11 Director may approve only after Vision PASS; no auto-approve");

        Ok(CharacterStudioIdentityLockV1Rules.MayScoreExisting(false, 4, [uneval])
            && !CharacterStudioIdentityLockV1Rules.MayScoreExisting(true, 4, [uneval])
            && !CharacterStudioIdentityLockV1Rules.MayScoreExisting(false, 3, [uneval])
            && !CharacterStudioIdentityLockV1Rules.MayScoreExisting(false, 4, ready)
            && !CharacterStudioIdentityLockV1Rules.GeneratesPixelsOnScore()
            && CharacterStudioIdentityLockV1Rules.StaffScoreExisting == "Chấm với Master",
            "12 score existing 4 views without generating pixels");

        Ok(CharacterStudioIdentityLockV1Rules.StaffVisionPending.Contains("Chấm với Master", StringComparison.Ordinal)
            && !CharacterStudioIdentityLockV1Rules.StaffVisionPending.Contains("Tạo lại bộ ảnh", StringComparison.Ordinal)
            && !CharacterStudioIdentityLockV1Rules.AutoApprove(),
            "13 pending Vision asks to score, not regenerate");

        return fail;
    }
}
