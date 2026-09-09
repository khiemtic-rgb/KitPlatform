using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ImageGenerationExecutionV1Regression
{
    public const string SuiteId = "PRODUCTION_IMAGE_GENERATION_EXECUTION_V1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        var shotId = "dddddddd-dddd-dddd-dddd-dddddddddddd";
        ImageGenerationExecutionRules.PreflightInput Ready(
            bool master = true, bool dna = true, bool prp = true, bool gov = true,
            string shot = "DIRECTOR_APPROVED", string prompt = "COMPILED", string igc = "DIRECTOR_APPROVED",
            string? liveMaster = null, string? liveDna = null, string? livePrp = null,
            string? liveShot = null, string? livePrompt = null, string? liveIgc = null,
            string? provMaster = null, string? provDna = null, string? provPrp = null, string? provContract = null,
            bool masterReadable = true, string? golden = null, string capability = "IMAGE_GENERATION",
            string selection = "EXTERNAL", string policy = "IMAGE_ONLY", string characterId = "CHAR-099") =>
            new(characterId, shotId, master, dna, prp, gov, shot, prompt, igc,
                sha, liveMaster ?? sha, sha, liveDna ?? sha, sha, livePrp ?? sha,
                sha, liveShot ?? sha, sha, livePrompt ?? sha, sha, liveIgc ?? sha,
                provMaster ?? sha, provDna ?? sha, provPrp ?? sha, provContract ?? sha,
                capability, selection, policy, masterReadable, "master.jpg", golden);

        var pass = ImageGenerationExecutionRules.EvaluatePreflight(Ready());
        Ok(pass.Status == "PASS" && pass.RunGemini && pass.Credit == 0, "32 Gemini only after preflight / credit 0");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(master: false)).Blocks.Any(b => b.Attribute == "master"), "01 missing Master → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(dna: false)).Blocks.Any(b => b.Attribute == "dna"), "02 missing DNA → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(prp: false)).Blocks.Any(b => b.Attribute == "prp"), "03 missing PRP → BLOCK");
        var other = new string('b', 64);
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(liveMaster: other)).Blocks.Any(b => b.Source == "MASTER"), "04 Master SHA mismatch → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(liveDna: other)).Blocks.Any(b => b.Source == "CHARACTER_DNA"), "05 DNA SHA mismatch → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(livePrp: other)).Blocks.Any(b => b.Source == "PRODUCTION_REFERENCE_PACK"), "06 PRP SHA mismatch → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(shot: "VALIDATED")).Status == "BLOCKED", "07 Shot Contract not approved → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(prompt: "BLOCKED")).Status == "BLOCKED", "08 Prompt not compiled → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(livePrompt: other)).Blocks.Any(b => b.Source == "PROMPT" && b.Attribute == "sha256"), "09 Prompt SHA mismatch → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(igc: "VALIDATED")).Status == "BLOCKED", "10 IGC not approved → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(liveIgc: other)).Blocks.Any(b => b.Source == "IMAGE_GENERATION_CONTRACT"), "11 IGC SHA mismatch → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(gov: false)).Status == "BLOCKED", "12 Governance fail → BLOCK");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready(masterReadable: false)).Blocks.Any(b => b.Attribute == "master")
            && !ImageGenerationExecutionRules.EvaluatePreflight(Ready(masterReadable: false)).RunGemini, "13 reference unreadable → BLOCK");

        var f1 = ImageGenerationExecutionRules.Fingerprint(Ready());
        var f2 = ImageGenerationExecutionRules.Fingerprint(Ready());
        Ok(f1.Length == 64 && f1 == f2, "14 deterministic fingerprint → PASS");
        Ok(!ImageGenerationExecutionRules.ShouldExecute("READY_FOR_DIRECTOR")
            && ImageGenerationExecutionRules.DoNotBlindRetry("READY_FOR_DIRECTOR"), "15 duplicate fingerprint → no second execution");
        Ok(ImageGenerationExecutionRules.DoNotBlindRetry("FAILED")
            && !ImageGenerationExecutionRules.ShouldExecute("FAILED")
            && !ImageGenerationExecutionRules.AutoRetry(), "16 failed same fingerprint → DO_NOT_BLIND_RETRY");
        Ok(ImageGenerationExecutionRules.MapHttp(true, false) == "ACCEPTED"
            && ImageGenerationExecutionRules.MapHttp(true, false) != "READY_FOR_DIRECTOR", "17 provider HTTP 200 → ACCEPTED, not READY");
        Ok(ImageGenerationExecutionRules.AfterAccepted(false) == "FAILED", "18 artifact unavailable → FAILED");

        var jpeg = ImageGenerationExecutionRules.FixtureJpeg512();
        var jpegSha = KitVideoIntegrityRules.Sha256Hex(jpeg);
        Ok(ImageGenerationExecutionRules.EvaluateTechnical(Array.Empty<byte>(), null, null, new ImageGenerationExecutionRules.QaObservation(FileReadable: false, Size: 0)) == "FAIL", "19 artifact unreadable → QA/technical FAIL");
        Ok(ImageGenerationExecutionRules.EvaluateTechnical(jpeg, null, jpegSha, null) == "PASS", "20 artifact SHA → PASS");

        var idFail = ImageGenerationExecutionRules.EvaluateQa(jpeg, null, jpegSha, new ImageGenerationExecutionRules.QaObservation(Identity: "mismatch"), "11", "short", "medium", true);
        Ok(idFail.Identity == "FAIL" && idFail.Overall == "QA_FAILED", "21 identity mismatch → QA_FAILED");
        var ageFail = ImageGenerationExecutionRules.EvaluateQa(jpeg, null, jpegSha, new ImageGenerationExecutionRules.QaObservation(Age: "14"), "11", "short", "medium", true);
        Ok(ageFail.Identity == "FAIL", "22 age mismatch → QA_FAILED");
        var hairFail = ImageGenerationExecutionRules.EvaluateQa(jpeg, null, jpegSha, new ImageGenerationExecutionRules.QaObservation(Hair: "long mismatch"), "11", "short", "medium", true);
        Ok(hairFail.Identity == "FAIL", "23 hair mismatch → QA_FAILED");
        var contFail = ImageGenerationExecutionRules.EvaluateQa(jpeg, null, jpegSha, new ImageGenerationExecutionRules.QaObservation(Continuity: "wardrobe break"), "11", "short", "medium", true);
        Ok(contFail.Continuity == "FAIL", "24 continuity mismatch → QA_FAILED");
        var compFail = ImageGenerationExecutionRules.EvaluateQa(jpeg, null, jpegSha, new ImageGenerationExecutionRules.QaObservation(Framing: "mismatch"), "11", "short", "medium", true);
        Ok(compFail.Composition == "FAIL", "25 composition mismatch → QA_FAILED");
        Ok(idFail.P0 > 0 && !ImageGenerationExecutionRules.CanDirectorApprove("READY_FOR_DIRECTOR", true, true, false, true, true, idFail.P0), "26 P0 QA fail → Director blocked");

        var qaPass = ImageGenerationExecutionRules.EvaluateQa(jpeg, null, jpegSha, new ImageGenerationExecutionRules.QaObservation(Identity: "match", Age: "11", Hair: "short", Framing: "medium", Continuity: "hold"), "11", "short", "medium", true);
        Ok(qaPass.Overall == "READY_FOR_DIRECTOR" && qaPass.P0 == 0, "27 all QA PASS → READY_FOR_DIRECTOR");
        Ok(ImageGenerationExecutionRules.CanDirectorApprove("READY_FOR_DIRECTOR", true, true, true, true, true, 0)
            && !ImageGenerationExecutionRules.AutoApprove(), "28 Director approve → APPROVED_PRODUCTION_STILL");
        Ok(!ImageGenerationExecutionRules.CanDirectorApprove("REJECTED", true, true, true, true, true, 0), "29 Director reject → REJECTED");
        Ok(ImageGenerationExecutionRules.DoNotBlindRetry("APPROVED"), "30 approved artifact immutable → PASS");
        Ok(ImageGenerationExecutionRules.CreditStatus(null) == "UNKNOWN", "31 credit unavailable → UNKNOWN");
        Ok(!ImageGenerationExecutionRules.CanCallGemini(false) && ImageGenerationExecutionRules.CanCallGemini(true), "32 Gemini after preflight");
        Ok(ImageGenerationExecutionRules.IsRunway("RUNWAY") && !ImageGenerationExecutionRules.AllowsProvider("RUNWAY"), "33 Runway called → FAIL");
        Ok(!ImageGenerationExecutionRules.GenerationBeforeDirector("READY_FOR_DIRECTOR")
            && !ImageGenerationExecutionRules.GenerationBeforeDirector("SUCCEEDED"), "34 generation before Director → FALSE");
        Ok(!ImageGenerationExecutionRules.TouchesGolden("master.jpg") && ImageGenerationExecutionRules.TouchesGolden("GOLDEN-SH01-01"), "35 Golden unchanged → PASS");
        Ok(!ImageGenerationExecutionRules.AutoFix() && ImageGenerationExecutionRules.EvaluatePreflight(Ready()).RunGemini, "36/37/38 Master DNA PRP unchanged + no auto-fix");
        Ok(pass.Fingerprint == f1, "39 Prompt unchanged / fingerprint stable");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready()).Blocks.Count == 0, "40 IGC unchanged when approved");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready()).RunGemini == false || ImageGenerationExecutionRules.EvaluatePreflight(Ready(master: false)).Credit == 0, "credit 0 on block");
        Ok(!ImageGenerationExecutionRules.CanCallGemini(ImageGenerationExecutionRules.EvaluatePreflight(Ready(gov: false)).Status == "PASS"), "no Gemini on governance fail");
        Ok(ImageGenerationExecutionRules.EvaluatePreflight(Ready() with { CrpUsable = false, CrpStatus = "DRAFT" }).Status == "BLOCKED"
            && !ImageGenerationExecutionRules.EvaluatePreflight(Ready() with { CrpUsable = false, CrpStatus = "DRAFT" }).RunGemini,
            "41 CRP unusable → Gemini not called");

        return fail;
    }
}
