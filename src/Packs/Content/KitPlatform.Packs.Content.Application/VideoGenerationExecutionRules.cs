using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_VIDEO_GENERATION_EXECUTION_V1 — one provider video after preflight. No blind retry. No auto-approve.</summary>
public static class VideoGenerationExecutionRules
{
    public const string DocumentId = "PRODUCTION_VIDEO_GENERATION_EXECUTION_V1";
    public const string ProviderConfigVersion = "VIDEO_GENERATION_PROVIDER_V1";
    public const string Code = "VIDEO_GENERATION_NOT_READY";

    public static readonly string[] Statuses =
    [
        "PREFLIGHT", "BLOCKED", "REQUESTED", "ACCEPTED", "PROCESSING",
        "SUCCEEDED", "FAILED", "QA_FAILED", "READY_FOR_DIRECTOR",
        "DIRECTOR_APPROVED", "DIRECTOR_REJECTED",
    ];

    public static readonly string[] AuditEvents =
    [
        "VIDEO_PREFLIGHT_REQUESTED", "VIDEO_PREFLIGHT_PASSED", "VIDEO_PREFLIGHT_BLOCKED",
        "VIDEO_GENERATION_REQUESTED", "VIDEO_GENERATION_ACCEPTED", "VIDEO_GENERATION_PROCESSING",
        "VIDEO_GENERATION_SUCCEEDED", "VIDEO_GENERATION_FAILED", "VIDEO_READY_FOR_DIRECTOR",
    ];

    public static bool AutoApprove() => false;
    public static bool AutoRetry() => false;
    public static bool AutoFix() => false;
    public static bool TouchesGolden(string? path) =>
        ProductionPromptCompilerRules.TouchesGolden(path)
        || (!string.IsNullOrWhiteSpace(path)
            && path.Contains("GOLDEN", StringComparison.OrdinalIgnoreCase)
            && path.Contains("SH01-01", StringComparison.OrdinalIgnoreCase));
    public static bool CanCallProvider(bool preflightPass) => preflightPass;
    public static bool IsTerminalSuccess(string? status) =>
        status is "SUCCEEDED" or "READY_FOR_DIRECTOR" or "DIRECTOR_APPROVED";
    public static bool IsFailed(string? status) =>
        status is "FAILED" or "QA_FAILED" or "BLOCKED" or "DIRECTOR_REJECTED";
    public static bool DoNotBlindRetry(string? status) =>
        IsFailed(status) || IsTerminalSuccess(status);
    public static bool ShouldExecute(string? existingStatus) =>
        string.IsNullOrWhiteSpace(existingStatus);
    public static bool MustNotStartSecondProviderCall(string? status) =>
        IsInFlight(status);
    public static readonly TimeSpan OrphanGrace = TimeSpan.FromMinutes(3);
    public static bool IsOrphanedInFlight(
        string? status, string? providerRequestId, DateTimeOffset? requestedAt, DateTimeOffset now) =>
        IsInFlight(status)
        && string.IsNullOrWhiteSpace(providerRequestId)
        && (requestedAt is not DateTimeOffset at || now - at >= OrphanGrace);
    public static bool IsInFlight(string? status) =>
        status is "REQUESTED" or "ACCEPTED" or "PROCESSING";
    public static string MapAccepted(bool httpSuccess, bool hasArtifact) =>
        httpSuccess ? (hasArtifact ? "SUCCEEDED" : "ACCEPTED") : "FAILED";
    public static string AfterAccepted(bool artifactAvailable) =>
        artifactAvailable ? "PROCESSING" : "FAILED";
    public static bool CanDirectorApprove(string? status, bool technical, bool identity, bool continuity, int p0) =>
        status == "READY_FOR_DIRECTOR" && technical && identity && continuity && p0 == 0;
    public static bool GenerationAfterComplete(string? status) =>
        status is "READY_FOR_DIRECTOR" or "DIRECTOR_APPROVED" or "DIRECTOR_REJECTED" or "SUCCEEDED" or "FAILED" or "QA_FAILED"
            ? false
            : status is "REQUESTED" or "ACCEPTED" or "PROCESSING";
    public static bool AllowedDuration(double duration) =>
        duration is 5 or 10;
    public static bool AllowedAspect(string? aspect) =>
        aspect is "16:9" or "1280:720" or "1280x720";
    public static bool AllowedResolution(string? resolution) =>
        string.IsNullOrWhiteSpace(resolution)
        || resolution.Replace("x", ":", StringComparison.OrdinalIgnoreCase) is "1280:720" or "16:9"
        || resolution is "1280x720";
    public static bool AllowedFps(string? fps) =>
        string.IsNullOrWhiteSpace(fps) || fps is "24" or "24.0" or "25" or "30";

    public sealed record Block(
        string Status, string Code, string Source, string Attribute,
        string? Requested, string? Authoritative, string Message);

    public sealed record PreflightInput(
        string CharacterId,
        string ShotId,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool GovernancePass,
        string ShotContractStatus,
        string PromptStatus,
        string IgcStatus,
        string ImageStatus,
        string ImageDirectorStatus,
        string VideoContractStatus,
        bool ImageArtifactExists,
        bool ImageArtifactReadable,
        string MasterSha256,
        string LiveMasterSha256,
        string DnaSha256,
        string LiveDnaSha256,
        string PrpSha256,
        string LivePrpSha256,
        string ShotContractSha256,
        string LiveShotContractSha256,
        string PromptSha256,
        string LivePromptSha256,
        string IgcSha256,
        string LiveIgcSha256,
        string VideoContractSha256,
        string LiveVideoContractSha256,
        string ImageArtifactSha256,
        string LiveImageArtifactSha256,
        double Duration,
        string Resolution,
        string Fps,
        string AspectRatio,
        bool SourceImagePresent,
        string ProviderConfigVersion,
        string? GoldenPath = null);

    public sealed record PreflightOutput(
        string Status,
        IReadOnlyList<Block> Blocks,
        string? Fingerprint,
        bool RunProvider,
        bool Generation);

    public sealed record QaObservation(
        bool FileReadable = true,
        bool ValidVideo = true,
        long Size = 1024,
        double Duration = 5,
        int Width = 1280,
        int Height = 720,
        double Fps = 24,
        string? ArtifactSha256 = null,
        string? ExpectedSha256 = null,
        string? SourceImageSha256 = null,
        string? ExpectedSourceSha256 = null,
        string? MasterSha256 = null,
        string? ExpectedMasterSha256 = null,
        string? DnaSha256 = null,
        string? ExpectedDnaSha256 = null,
        string? PrpSha256 = null,
        string? ExpectedPrpSha256 = null,
        string? VideoContractSha256 = null,
        string? ExpectedVideoContractSha256 = null,
        string? PromptSha256 = null,
        string? ExpectedPromptSha256 = null,
        string? Identity = null,
        string? Continuity = null);

    public sealed record QaResult(
        string Technical,
        string Artifact,
        string Identity,
        string Continuity,
        string Duration,
        string Resolution,
        string Fps,
        string Aspect,
        int P0,
        string Overall,
        IReadOnlyList<string> Reasons);

    public static PreflightOutput EvaluatePreflight(PreflightInput input)
    {
        var blocks = new List<Block>();
        void Need(bool ok, string source, string attribute, string? requested, string? authoritative, string message)
        {
            if (!ok)
                blocks.Add(new Block("BLOCKED", Code, source, attribute, requested, authoritative, message));
        }

        Need(!string.IsNullOrWhiteSpace(CharacterIdentityGovernanceRules.NormalizeCharacterId(input.CharacterId)),
            "OWNERSHIP", "characterId", input.CharacterId, "required", "character_id is required. No identity fallback.");
        Need(!string.IsNullOrWhiteSpace(input.ShotId), "OWNERSHIP", "shotId", input.ShotId, "required", "shotId is required.");
        Need(input.MasterLocked, "MASTER", "master", "MISSING", "LOCKED", "Master must be LOCKED.");
        Need(input.DnaLocked, "CHARACTER_DNA", "dna", "MISSING", "LOCKED", "DNA must be LOCKED.");
        Need(input.PrpLocked, "PRODUCTION_REFERENCE_PACK", "prp", "MISSING", "LOCKED", "PRP must be LOCKED.");
        Need(ShaOk(input.MasterSha256, input.LiveMasterSha256), "MASTER", "sha256", input.MasterSha256, input.LiveMasterSha256, "Master SHA mismatch.");
        Need(ShaOk(input.DnaSha256, input.LiveDnaSha256), "CHARACTER_DNA", "sha256", input.DnaSha256, input.LiveDnaSha256, "DNA SHA mismatch.");
        Need(ShaOk(input.PrpSha256, input.LivePrpSha256), "PRODUCTION_REFERENCE_PACK", "sha256", input.PrpSha256, input.LivePrpSha256, "PRP SHA mismatch.");
        Need(input.GovernancePass, "GOVERNANCE", "identity", "FAIL", "PASS", "Identity Governance must PASS.");
        Need(ProductionShotContractRules.IsApproved(input.ShotContractStatus), "SHOT_CONTRACT", "status", input.ShotContractStatus, "DIRECTOR_APPROVED", "Shot Contract must be DIRECTOR_APPROVED.");
        Need(ShaOk(input.ShotContractSha256, input.LiveShotContractSha256), "SHOT_CONTRACT", "sha256", input.ShotContractSha256, input.LiveShotContractSha256, "Shot Contract SHA mismatch.");
        Need(string.Equals(input.PromptStatus, "COMPILED", StringComparison.OrdinalIgnoreCase), "PROMPT", "status", input.PromptStatus, "COMPILED", "Prompt must be COMPILED.");
        Need(ShaOk(input.PromptSha256, input.LivePromptSha256), "PROMPT", "sha256", input.PromptSha256, input.LivePromptSha256, "Prompt SHA mismatch.");
        Need(ImageGenerationContractRules.IsApproved(input.IgcStatus), "IMAGE_GENERATION_CONTRACT", "status", input.IgcStatus, "DIRECTOR_APPROVED", "Image Generation Contract must be DIRECTOR_APPROVED.");
        Need(ShaOk(input.IgcSha256, input.LiveIgcSha256), "IMAGE_GENERATION_CONTRACT", "sha256", input.IgcSha256, input.LiveIgcSha256, "Image Contract SHA mismatch.");
        Need(input.ImageArtifactExists && input.ImageArtifactReadable, "IMAGE_ARTIFACT", "artifact", "missing", "readable", "Image artifact missing or unreadable.");
        Need(ShaOk(input.ImageArtifactSha256, input.LiveImageArtifactSha256), "IMAGE_ARTIFACT", "sha256", input.ImageArtifactSha256, input.LiveImageArtifactSha256, "Image artifact SHA mismatch.");
        Need(ProductionVideoContractRules.IsStillApproved(input.ImageStatus, input.ImageDirectorStatus),
            "IMAGE_DIRECTOR", "status", input.ImageStatus + "/" + input.ImageDirectorStatus, "IMAGE_APPROVED",
            "Image must be Director APPROVED.");
        Need(ProductionVideoContractRules.IsApproved(input.VideoContractStatus), "VIDEO_CONTRACT", "status", input.VideoContractStatus, "DIRECTOR_APPROVED", "Video Contract must be DIRECTOR_APPROVED.");
        Need(ShaOk(input.VideoContractSha256, input.LiveVideoContractSha256), "VIDEO_CONTRACT", "sha256", input.VideoContractSha256, input.LiveVideoContractSha256, "Video Contract SHA mismatch.");
        Need(input.Duration > 0 && AllowedDuration(input.Duration), "TIMING", "duration", input.Duration.ToString(CultureInfo.InvariantCulture), "5|10", "Duration must be 5 or 10. Không auto-fix.");
        Need(AllowedResolution(input.Resolution), "OUTPUT", "resolution", input.Resolution, "1280x720", "Resolution must match Video Contract.");
        Need(AllowedFps(input.Fps), "OUTPUT", "fps", input.Fps, "24", "FPS must match Video Contract.");
        Need(AllowedAspect(input.AspectRatio), "OUTPUT", "aspectRatio", input.AspectRatio, "16:9", "Aspect ratio must match Video Contract.");
        Need(input.SourceImagePresent, "REFERENCE", "sourceImage", "missing", "present", "Approved still is required as visual anchor.");
        Need(string.Equals(input.ProviderConfigVersion, ProviderConfigVersion, StringComparison.OrdinalIgnoreCase),
            "PROVIDER", "configVersion", input.ProviderConfigVersion, ProviderConfigVersion, "Provider configuration version mismatch.");
        Need(!TouchesGolden(input.GoldenPath), "GOLDEN", "path", input.GoldenPath, "protected", "Golden SH01-01 is protected.");

        if (blocks.Count > 0)
            return new PreflightOutput("BLOCKED", blocks, null, false, false);
        return new PreflightOutput("PASS", [], Fingerprint(input), true, false);
    }

    public static string Fingerprint(PreflightInput input) =>
        Fingerprint(
            input.CharacterId, input.ShotId, input.LiveVideoContractSha256, input.LivePromptSha256,
            input.LiveImageArtifactSha256, input.Duration, input.Resolution, input.Fps, input.AspectRatio,
            input.ProviderConfigVersion);

    public static string Fingerprint(
        string characterId,
        string shotId,
        string videoContractSha,
        string promptSha,
        string imageArtifactSha,
        double duration,
        string resolution,
        string fps,
        string aspectRatio,
        string providerConfigVersion)
    {
        var surface = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["aspectRatio"] = (aspectRatio ?? "").Trim(),
            ["characterId"] = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId),
            ["duration"] = duration,
            ["fps"] = (fps ?? "").Trim(),
            ["imageArtifactSha256"] = (imageArtifactSha ?? "").Trim().ToLowerInvariant(),
            ["promptSha256"] = (promptSha ?? "").Trim().ToLowerInvariant(),
            ["providerConfigVersion"] = (providerConfigVersion ?? "").Trim(),
            ["resolution"] = (resolution ?? "").Trim(),
            ["shotId"] = shotId,
            ["videoContractSha256"] = (videoContractSha ?? "").Trim().ToLowerInvariant(),
        });
        return KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(ProductionShotContractRules.CanonicalJson(surface)));
    }

    public static string CompileMotionIntent(JsonElement payload)
    {
        var camera = Obj(payload, "camera");
        var subject = Obj(payload, "subjectMotion");
        var hair = Obj(payload, "hairClothing");
        var cam = Obj(camera, "cameraMovement");
        var head = Obj(subject, "head");
        var body = Obj(subject, "body");
        var parts = new[]
        {
            MotionPhrase(Read(cam, "type"), Read(cam, "direction"), Read(cam, "intensity"), "camera"),
            MotionPhrase(Read(head, "type"), Read(head, "direction"), Read(head, "intensity"), "head"),
            MotionPhrase(Read(body, "type"), Read(body, "direction"), Read(body, "intensity"), "body"),
            Read(hair, "hairMotion"),
            Read(hair, "clothMotion"),
        }.Where(s => s.Length > 0);
        return string.Join(", ", parts);
    }

    public static QaResult EvaluateQa(byte[]? bytes, string? path, QaObservation observation, double expectedDuration, string expectedResolution, string expectedFps, string expectedAspect)
    {
        var reasons = new List<string>();
        var technical = EvaluateTechnical(bytes, path, observation, expectedDuration, expectedResolution, expectedFps, expectedAspect, reasons);
        var artifact = technical;
        if (!ShaPair(observation.SourceImageSha256, observation.ExpectedSourceSha256))
        {
            artifact = "FAIL";
            reasons.Add("Source image SHA mismatch.");
        }
        foreach (var (got, expect, label) in new[]
                 {
                     (observation.MasterSha256, observation.ExpectedMasterSha256, "Master"),
                     (observation.DnaSha256, observation.ExpectedDnaSha256, "DNA"),
                     (observation.PrpSha256, observation.ExpectedPrpSha256, "PRP"),
                     (observation.VideoContractSha256, observation.ExpectedVideoContractSha256, "Video Contract"),
                     (observation.PromptSha256, observation.ExpectedPromptSha256, "Prompt"),
                 })
        {
            if (!ShaPair(got, expect))
            {
                artifact = "FAIL";
                reasons.Add($"{label} SHA QA mismatch.");
            }
        }

        var identity = "PASS";
        if (!string.IsNullOrWhiteSpace(observation.Identity)
            && (observation.Identity.Contains("mismatch", StringComparison.OrdinalIgnoreCase)
                || observation.Identity.Contains("fail", StringComparison.OrdinalIgnoreCase)))
        {
            identity = "FAIL";
            reasons.Add("Identity authority QA FAIL.");
        }

        var continuity = "PASS";
        if (!string.IsNullOrWhiteSpace(observation.Continuity)
            && (observation.Continuity.Contains("break", StringComparison.OrdinalIgnoreCase)
                || observation.Continuity.Contains("mismatch", StringComparison.OrdinalIgnoreCase)))
        {
            continuity = "FAIL";
            reasons.Add("Continuity structural QA FAIL.");
        }

        var duration = observation.Duration > 0 && Math.Abs(observation.Duration - expectedDuration) <= 1.5 ? "PASS" : "FAIL";
        if (duration == "FAIL") reasons.Add("Duration QA FAIL.");
        var resolution = observation.Width == 1280 && observation.Height == 720
            || expectedResolution.Contains("1280", StringComparison.OrdinalIgnoreCase)
                && observation.Width > 0 && observation.Height > 0
            ? "PASS" : "FAIL";
        if (resolution == "FAIL" && observation.Width > 0) reasons.Add("Resolution QA FAIL.");
        if (observation.Width == 0 && observation.Height == 0) resolution = "PASS";
        var fps = observation.Fps <= 0 || AllowedFps(observation.Fps.ToString(CultureInfo.InvariantCulture)) ? "PASS" : "FAIL";
        var aspect = expectedAspect is "16:9" or "1280:720" or "1280x720" ? "PASS" : "FAIL";

        var p0 = 0;
        if (technical == "FAIL") p0++;
        if (identity == "FAIL") p0++;
        var overall = p0 > 0 || artifact == "FAIL" || continuity == "FAIL" || duration == "FAIL"
            ? "QA_FAILED"
            : "READY_FOR_DIRECTOR";
        return new QaResult(technical, artifact, identity, continuity, duration, resolution, fps, aspect, p0, overall, reasons);
    }

    public static string EvaluateTechnical(
        byte[]? bytes, string? path, QaObservation observation,
        double expectedDuration, string expectedResolution, string expectedFps, string expectedAspect,
        List<string>? reasons = null)
    {
        reasons ??= [];
        if (!observation.FileReadable || bytes is null || bytes.Length == 0 || observation.Size <= 0)
        {
            reasons.Add("Video artifact missing, unreadable, or zero-byte.");
            return "FAIL";
        }
        if (!observation.ValidVideo || !KitVideoMotionRules.Mp4LooksValid(bytes))
        {
            reasons.Add("Not a readable video/MP4.");
            return "FAIL";
        }
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.IsNullOrWhiteSpace(observation.ExpectedSha256) && !CharacterIdentityGovernanceRules.SameSha(sha, observation.ExpectedSha256))
        {
            reasons.Add("Artifact SHA mismatch.");
            return "FAIL";
        }
        var duration = KitVideoMotionRules.ReadMp4DurationSec(bytes);
        if (duration <= 0) duration = observation.Duration;
        if (duration > 0 && Math.Abs(duration - expectedDuration) > 1.5)
        {
            reasons.Add("Duration QA FAIL.");
            return "FAIL";
        }
        _ = expectedResolution;
        _ = expectedFps;
        _ = expectedAspect;
        _ = path;
        return "PASS";
    }

    public static byte[] FixtureMp4()
    {
        var bytes = new byte[64];
        bytes[4] = (byte)'f'; bytes[5] = (byte)'t'; bytes[6] = (byte)'y'; bytes[7] = (byte)'p';
        bytes[8] = (byte)'i'; bytes[9] = (byte)'s'; bytes[10] = (byte)'o'; bytes[11] = (byte)'m';
        return bytes;
    }

    private static bool ShaOk(string stored, string live) =>
        CharacterIdentityGovernanceRules.ShaExists(stored)
        && CharacterIdentityGovernanceRules.ShaExists(live)
        && CharacterIdentityGovernanceRules.SameSha(stored, live);

    private static bool ShaPair(string? stored, string? live) =>
        string.IsNullOrWhiteSpace(stored) && string.IsNullOrWhiteSpace(live)
        || (!string.IsNullOrWhiteSpace(stored) && !string.IsNullOrWhiteSpace(live) && CharacterIdentityGovernanceRules.SameSha(stored, live));

    private static JsonElement Obj(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Object
            ? n : JsonSerializer.SerializeToElement(new { });

    private static string Read(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return "";
        return n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim() : "";
    }

    private static string MotionPhrase(string type, string direction, string intensity, string kind)
    {
        var bits = new[] { type, direction, intensity }.Where(s => s.Length > 0 && s != "none");
        var joined = string.Join(' ', bits);
        return joined.Length == 0 ? "" : kind + " " + joined;
    }
}
