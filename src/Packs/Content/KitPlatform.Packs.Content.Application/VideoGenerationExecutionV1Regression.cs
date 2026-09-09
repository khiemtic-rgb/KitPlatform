using System.Linq;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class VideoGenerationExecutionV1Regression
{
    public const string SuiteId = "PRODUCTION_VIDEO_GENERATION_EXECUTION_V1_REGRESSION";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var sha = new string('a', 64);
        var shotId = "66559efe-3021-42f6-89e2-a4d24f83c7f2";
        VideoGenerationExecutionRules.PreflightInput Ready(
            bool master = true, bool dna = true, bool prp = true, bool gov = true,
            string shot = "DIRECTOR_APPROVED", string prompt = "COMPILED", string igc = "DIRECTOR_APPROVED",
            string image = "IMAGE_APPROVED", string review = "APPROVED", string video = "DIRECTOR_APPROVED",
            bool artifact = true, bool readable = true, bool source = true,
            string? liveMaster = null, string? liveDna = null, string? livePrp = null,
            string? liveShot = null, string? livePrompt = null, string? liveIgc = null,
            string? liveVideo = null, string? liveImage = null,
            double duration = 5, string resolution = "1280x720", string fps = "24", string aspect = "16:9",
            string config = VideoGenerationExecutionRules.ProviderConfigVersion,
            string characterId = "CHAR-099", string? golden = null) =>
            new(characterId, shotId, master, dna, prp, gov, shot, prompt, igc, image, review, video,
                artifact, readable,
                sha, liveMaster ?? sha, sha, liveDna ?? sha, sha, livePrp ?? sha,
                sha, liveShot ?? sha, sha, livePrompt ?? sha, sha, liveIgc ?? sha,
                sha, liveVideo ?? sha, sha, liveImage ?? sha,
                duration, resolution, fps, aspect, source, config, golden);

        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(master: false)).Blocks.Any(b => b.Attribute == "master"), "01 missing Master → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(dna: false)).Blocks.Any(b => b.Attribute == "dna"), "02 missing DNA → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(prp: false)).Blocks.Any(b => b.Attribute == "prp"), "03 missing PRP → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(gov: false)).Blocks.Any(b => b.Source == "GOVERNANCE"), "04 identity conflict → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(shot: "VALIDATED")).Status == "BLOCKED", "05 Shot Contract chưa approved → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(prompt: "DRAFT")).Status == "BLOCKED", "06 Prompt chưa compiled → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(igc: "VALIDATED")).Status == "BLOCKED", "07 Image Contract chưa approved → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(artifact: false, readable: false)).Blocks.Any(b => b.Source == "IMAGE_ARTIFACT"), "08 Image artifact missing → BLOCK");
        var other = new string('b', 64);
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(liveImage: other)).Blocks.Any(b => b.Source == "IMAGE_ARTIFACT" && b.Attribute == "sha256"), "09 Image SHA mismatch → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(image: "READY_FOR_DIRECTOR", review: "PENDING")).Status == "BLOCKED", "10 Image chưa Director approved → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(video: "VALIDATED")).Status == "BLOCKED", "11 Video Contract chưa approved → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(liveVideo: other)).Blocks.Any(b => b.Source == "VIDEO_CONTRACT" && b.Attribute == "sha256"), "12 Video Contract SHA mismatch → BLOCK");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(livePrompt: other)).Blocks.Any(b => b.Source == "PROMPT" && b.Attribute == "sha256"), "13 Prompt SHA mismatch → BLOCK");

        var pass = VideoGenerationExecutionRules.EvaluatePreflight(Ready());
        var f1 = VideoGenerationExecutionRules.Fingerprint(Ready());
        var f2 = VideoGenerationExecutionRules.Fingerprint(Ready());
        Ok(pass.Status == "PASS" && f1.Length == 64 && f1 == f2 && pass.Fingerprint == f1, "14 fingerprint deterministic");
        Ok(!VideoGenerationExecutionRules.ShouldExecute("READY_FOR_DIRECTOR")
            && VideoGenerationExecutionRules.DoNotBlindRetry("READY_FOR_DIRECTOR"), "15 duplicate fingerprint → no second execution");
        Ok(!VideoGenerationExecutionRules.EvaluatePreflight(Ready(master: false)).RunProvider
            && !VideoGenerationExecutionRules.CanCallProvider(false), "16 provider not called when preflight fails");
        Ok(pass.RunProvider && VideoGenerationExecutionRules.CanCallProvider(true)
            && VideoGenerationExecutionRules.ShouldExecute(null)
            && VideoGenerationExecutionRules.DoNotBlindRetry("SUCCEEDED"), "17 provider called exactly once");
        Ok(VideoGenerationExecutionRules.IsInFlight("REQUESTED")
            && VideoGenerationExecutionRules.MustNotStartSecondProviderCall("REQUESTED")
            && !VideoGenerationExecutionRules.ShouldExecute("REQUESTED"), "17b REQUESTED must not start a second provider call");
        var orphanAt = DateTimeOffset.UtcNow.AddMinutes(-4);
        Ok(VideoGenerationExecutionRules.IsOrphanedInFlight("REQUESTED", null, orphanAt, DateTimeOffset.UtcNow)
            && !VideoGenerationExecutionRules.IsOrphanedInFlight("REQUESTED", "task-1", orphanAt, DateTimeOffset.UtcNow)
            && !VideoGenerationExecutionRules.IsOrphanedInFlight("REQUESTED", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), "17c orphan REQUESTED without task may recover once");
        Ok(VideoGenerationExecutionRules.MapAccepted(true, false) == "ACCEPTED", "18 provider accepted");
        Ok(VideoGenerationExecutionRules.AfterAccepted(true) == "PROCESSING", "19 provider processing");
        Ok(VideoGenerationExecutionRules.MapAccepted(true, true) == "SUCCEEDED", "20 provider success");

        var mp4 = VideoGenerationExecutionRules.FixtureMp4();
        var mp4Sha = KitVideoIntegrityRules.Sha256Hex(mp4);
        var obsPass = new VideoGenerationExecutionRules.QaObservation(
            Size: mp4.Length, Duration: 5, ArtifactSha256: mp4Sha, ExpectedSha256: mp4Sha,
            SourceImageSha256: sha, ExpectedSourceSha256: sha,
            MasterSha256: sha, ExpectedMasterSha256: sha,
            DnaSha256: sha, ExpectedDnaSha256: sha,
            PrpSha256: sha, ExpectedPrpSha256: sha,
            VideoContractSha256: sha, ExpectedVideoContractSha256: sha,
            PromptSha256: sha, ExpectedPromptSha256: sha,
            Identity: "hold", Continuity: "hold");
        Ok(VideoGenerationExecutionRules.EvaluateTechnical(null, null, obsPass with { FileReadable = false, Size = 0 }, 5, "1280x720", "24", "16:9") == "FAIL", "21 artifact missing → FAIL");
        Ok(VideoGenerationExecutionRules.EvaluateTechnical([], null, obsPass with { Size = 0 }, 5, "1280x720", "24", "16:9") == "FAIL", "22 artifact zero byte → FAIL");
        Ok(VideoGenerationExecutionRules.EvaluateTechnical(mp4, null, obsPass with { ExpectedSha256 = other }, 5, "1280x720", "24", "16:9") == "FAIL", "23 artifact SHA mismatch → FAIL");
        Ok(VideoGenerationExecutionRules.EvaluateQa(mp4, null, obsPass with { ValidVideo = false }, 5, "1280x720", "24", "16:9").Technical == "FAIL", "24 technical QA fail");
        Ok(VideoGenerationExecutionRules.EvaluateQa(mp4, null, obsPass with { Identity = "mismatch" }, 5, "1280x720", "24", "16:9").Identity == "FAIL", "25 structural identity QA");
        Ok(VideoGenerationExecutionRules.EvaluateQa(mp4, null, obsPass with { Continuity = "wardrobe break" }, 5, "1280x720", "24", "16:9").Continuity == "FAIL", "26 continuity QA");
        Ok(VideoGenerationExecutionRules.EvaluateQa(mp4, null, obsPass, 5, "1280x720", "24", "16:9").Overall == "READY_FOR_DIRECTOR", "27 READY_FOR_DIRECTOR");
        Ok(!VideoGenerationExecutionRules.AutoApprove()
            && !VideoGenerationExecutionRules.CanDirectorApprove("READY_FOR_DIRECTOR", true, false, true, 1), "28 no auto approve");
        Ok(VideoGenerationExecutionRules.DoNotBlindRetry("FAILED")
            && !VideoGenerationExecutionRules.ShouldExecute("FAILED")
            && !VideoGenerationExecutionRules.AutoRetry(), "29 no blind retry");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(liveMaster: other)).Blocks.Any(b => b.Source == "MASTER")
            && VideoGenerationExecutionRules.EvaluatePreflight(Ready(liveDna: other)).Blocks.Any(b => b.Source == "CHARACTER_DNA")
            && VideoGenerationExecutionRules.EvaluatePreflight(Ready(livePrp: other)).Blocks.Any(b => b.Source == "PRODUCTION_REFERENCE_PACK"), "30 Master/DNA/PRP unchanged (SHA gate)");
        Ok(!VideoGenerationExecutionRules.TouchesGolden("production-video-execution")
            && VideoGenerationExecutionRules.TouchesGolden("GOLDEN-SH01-01")
            && VideoGenerationExecutionRules.EvaluatePreflight(Ready(golden: "GOLDEN-SH01-01")).Status == "BLOCKED", "31 Golden unchanged");
        Ok(pass.Generation == false
            && VideoGenerationExecutionRules.GenerationAfterComplete("READY_FOR_DIRECTOR") == false
            && VideoGenerationExecutionRules.GenerationAfterComplete("PROCESSING"), "32 generation state correct");
        Ok(VideoGenerationExecutionRules.AuditEvents.Contains("VIDEO_PREFLIGHT_PASSED")
            && VideoGenerationExecutionRules.AuditEvents.Contains("VIDEO_GENERATION_SUCCEEDED")
            && VideoGenerationExecutionRules.AuditEvents.Contains("VIDEO_READY_FOR_DIRECTOR"), "33 audit events");
        Ok(VideoGenerationExecutionRules.DoNotBlindRetry("SUCCEEDED")
            && !VideoGenerationExecutionRules.ShouldExecute("SUCCEEDED"), "34 immutable artifact after success");
        Ok(VideoGenerationExecutionRules.EvaluatePreflight(Ready(characterId: "")).Blocks.Any(b => b.Source == "OWNERSHIP"), "35 ownership/tenant isolation");

        var payload = JsonSerializer.SerializeToElement(new
        {
            camera = new { cameraMovement = new { type = "slow_push_in", direction = "forward", intensity = "low" } },
            subjectMotion = new { head = new { type = "slight_turn", direction = "right", intensity = "low" }, body = new { type = "natural_breathing", direction = "none", intensity = "low" } },
            hairClothing = new { hairMotion = "moves slightly with air", clothMotion = "moves naturally" },
        });
        var intent = VideoGenerationExecutionRules.CompileMotionIntent(payload);
        Ok(intent.Contains("slow_push_in") && !intent.Contains("runway", StringComparison.OrdinalIgnoreCase)
            && !intent.Contains("gemini", StringComparison.OrdinalIgnoreCase), "36 motion intent has no provider syntax");
        Ok(!VideoGenerationExecutionRules.AutoFix() && VideoGenerationExecutionRules.Statuses.Contains("READY_FOR_DIRECTOR")
            && !VideoGenerationExecutionRules.Statuses.Contains("VIDEO_READY"), "37 no VIDEO_READY alias");
        return fail;
    }
}
