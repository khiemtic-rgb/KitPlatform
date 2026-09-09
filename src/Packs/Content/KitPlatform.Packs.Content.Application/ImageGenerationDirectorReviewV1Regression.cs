using System.Linq;

namespace KitPlatform.Packs.Content;

public static class ImageGenerationDirectorReviewV1Regression
{
    public const string SuiteId = "PRODUCTION_IMAGE_DIRECTOR_REVIEW_V1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        ImageGenerationDirectorReviewRules.ReviewInput Ready(
            string exec = "READY_FOR_DIRECTOR",
            string approval = "PENDING",
            bool master = true, bool dna = true, bool prp = true,
            string shot = "DIRECTOR_APPROVED", string prompt = "COMPILED", string igc = "DIRECTOR_APPROVED",
            string tech = "PASS", string character = "PASS", string identity = "PASS",
            string cont = "PASS", string comp = "PASS", int p0 = 0,
            bool artifact = true, bool readable = true,
            string? liveMaster = null, string? liveDna = null, string? livePrp = null,
            string? liveShot = null, string? livePrompt = null, string? liveIgc = null, string? liveArt = null) =>
            new(exec, approval, master, dna, prp, shot, prompt, igc, tech, character, identity, cont, comp, p0,
                artifact, readable,
                sha, liveMaster ?? sha, sha, liveDna ?? sha, sha, livePrp ?? sha,
                sha, liveShot ?? sha, sha, livePrompt ?? sha, sha, liveIgc ?? sha,
                sha, liveArt ?? sha);

        var pass = ImageGenerationDirectorReviewRules.Evaluate(Ready());
        Ok(pass.CanApprove && pass.CanReject && pass.Status == "READY" && !pass.Generation, "01 happy path READY → can APPROVE");
        Ok(ImageGenerationDirectorReviewRules.IsImageApproved("IMAGE_APPROVED"), "01b IMAGE_APPROVED terminal");
        Ok(ImageGenerationDirectorReviewRules.IsImageRejected("IMAGE_REJECTED"), "02 REJECT maps IMAGE_REJECTED");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(exec: "PROCESSING")).CanApprove, "03 PROCESSING → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(exec: "SUCCEEDED")).CanApprove, "04 SUCCEEDED without READY → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(exec: "DRAFT")).CanApprove, "05 DRAFT → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(tech: "FAIL")).Blocks.Any(b => b.Attribute == "technical"), "06 Technical QA FAIL → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(character: "FAIL")).Blocks.Any(b => b.Attribute == "character"), "07 Character QA FAIL → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(identity: "FAIL")).Blocks.Any(b => b.Attribute == "identity"), "08 Identity QA FAIL → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(cont: "FAIL")).Blocks.Any(b => b.Attribute == "continuity"), "09 Continuity QA FAIL → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(comp: "FAIL")).Blocks.Any(b => b.Attribute == "composition"), "10 Composition QA FAIL → BLOCK");
        var other = new string('b', 64);
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(liveMaster: other)).Blocks.Any(b => b.Code == "IMAGE_DIRECTOR_REVIEW_BLOCKED" && b.Source == "MASTER"), "11 Master SHA mismatch → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(liveDna: other)).Blocks.Any(b => b.Source == "CHARACTER_DNA"), "12 DNA SHA mismatch → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(livePrp: other)).Blocks.Any(b => b.Source == "PRODUCTION_REFERENCE_PACK"), "13 PRP SHA mismatch → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(liveShot: other)).Blocks.Any(b => b.Source == "SHOT_CONTRACT"), "14 Shot Contract SHA mismatch → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(livePrompt: other)).Blocks.Any(b => b.Source == "PROMPT"), "15 Prompt SHA mismatch → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(liveIgc: other)).Blocks.Any(b => b.Source == "IMAGE_GENERATION_CONTRACT"), "16 IGC SHA mismatch → BLOCK");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(liveArt: other)).Blocks.Any(b => b.Source == "ARTIFACT"), "17 Artifact SHA mismatch → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(artifact: false)).CanApprove, "18 artifact missing → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(readable: false)).CanApprove, "19 artifact unreadable → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(approval: "APPROVED")).CanApprove, "20 already approved → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.AutoApprove() && !ImageGenerationDirectorReviewRules.AutoFix(), "21 no auto-approve / no auto-fix");
        Ok(!ImageGenerationDirectorReviewRules.CallsProvider() && !ImageGenerationDirectorReviewRules.AllowsGemini() && !ImageGenerationDirectorReviewRules.AllowsRunway(), "22 no provider");
        Ok(pass.Generation == false, "23 generation = FALSE");
        Ok(!ImageGenerationDirectorReviewRules.TouchesGolden("production-execution/still.png")
            && ImageGenerationDirectorReviewRules.TouchesGolden("GOLDEN-SH01-01"), "24 Golden protected");
        Ok(ImageGenerationDirectorReviewRules.RejectReasonRequired("Face identity chưa đạt.")
            && !ImageGenerationDirectorReviewRules.RejectReasonRequired("  "), "25 reject reason required");
        Ok(ImageGenerationDirectorReviewRules.Evaluate(Ready(p0: 1)).Blocks.Any(b => b.Attribute == "p0"), "26 P0 → BLOCK");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(exec: "IMAGE_APPROVED", approval: "APPROVED")).CanApprove, "27 IMAGE_APPROVED → no re-approve");
        Ok(!ImageGenerationDirectorReviewRules.AllowsArtifactMutation("APPROVED")
            && !ImageGenerationDirectorReviewRules.AllowsArtifactMutation("REJECTED"), "28 APPROVED/REJECTED artifact mutation BLOCK");
        var mismatch = ImageGenerationDirectorReviewRules.Evaluate(Ready(liveMaster: other));
        Ok(!mismatch.CanApprove && mismatch.CanReject, "29 SHA mismatch blocks APPROVE, Director may still REJECT");
        Ok(!ImageGenerationDirectorReviewRules.Evaluate(Ready(exec: "IMAGE_REJECTED", approval: "REJECTED")).CanApprove, "31 IMAGE_REJECTED → no approve");
        Ok(ImageGenerationDirectorReviewRules.AllowsArtifactMutation("PENDING")
            && !ImageGenerationDirectorReviewRules.AllowsArtifactMutation("SUPERSEDED"), "32 only PENDING may change review SHA");
        return fail;
    }
}
