using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoMotionRules
{
    public const string Version = "KIT-VIDEO-MOTION-V1";
    public const bool AllowGoldenShotOnly = true;
    public const string GoldenProject = "FAMIXA";
    public const string GoldenShot = "SH01-01";
    public const string Model = "gen4_turbo";
    public const string Ratio = "1280:720";
    public const int MaxAttempts = 3;
    public const int PromptMax = 900;

    public static readonly string[] VisualFailCodes =
    [
        "INTERNAL.BAD_OUTPUT", "INTERNAL.BAD_OUTPUT.CODE01", "SAFETY.OUTPUT",
    ];

    public static void EnsureGoldenShot(string? shotCode) =>
        EnsureGoldenLock(GoldenProject, shotCode);

    public static void EnsureGoldenLock(string? projectCode, string? shotCode)
    {
        var project = (projectCode ?? "").Trim().ToUpperInvariant();
        var shot = (shotCode ?? "").Trim().ToUpperInvariant();
        if (project.Length > 0 && project != GoldenProject)
            throw new InvalidOperationException("PHASE_06_GOLDEN_ONLY: chỉ FAMIXA / SH01-01.");
        if (shot != GoldenShot)
            throw new InvalidOperationException("PHASE_06_GOLDEN_ONLY: chỉ FAMIXA / SH01-01.");
    }

    public static KitVideoMotionCompileDto Compile(JsonElement contract)
    {
        EnsureGoldenShot(Str(contract, "shotCode"));
        var duration = ReadDuration(contract);
        var model = Str(contract, "model");
        if (string.IsNullOrWhiteSpace(model)) model = Model;
        if (!model.Equals(Model, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PREFLIGHT: model phải là gen4_turbo.");

        var action = Str(contract, "action");
        var camera = Str(contract, "camera");
        var motion = Str(contract, "motion");
        if (string.IsNullOrWhiteSpace(action))
            throw new InvalidOperationException("PREFLIGHT: motion contract thiếu action.");

        var prompt = string.Join(" ", new[] { motion.Trim(), camera.Trim() }.Where(s => s.Length > 0));
        if (string.IsNullOrWhiteSpace(prompt))
            throw new InvalidOperationException("PREFLIGHT: compiler không được bịa motion ngoài MOTION_CONTRACT.");
        if (prompt.Length > PromptMax) prompt = prompt[..PromptMax];
        EnsureMotionOnly(prompt, action);
        return new KitVideoMotionCompileDto(GoldenShot, prompt, Model, duration, Ratio, Version);
    }

    public static void EnsureMotionOnly(string prompt, string? action = null)
    {
        var blob = $"{prompt}\n{action}";
        if (Regex.IsMatch(blob, @"[“""][^”""]{2,}[”""]") ||
            Regex.IsMatch(blob, @"Mẹ ơi|con được|says?:|dialogue|thoại", RegexOptions.IgnoreCase))
            throw new InvalidOperationException("PREFLIGHT: Runway prompt không chứa thoại.");
        if (Regex.IsMatch(blob,
                @"visual contract|character sheet|reference sheet|pack_content|lipsync|elevenlabs|tts|subtitle|watermark|do not |don't |no text|SELECT |INSERT |\{|\}",
                RegexOptions.IgnoreCase))
            throw new InvalidOperationException("PREFLIGHT: Runway chỉ nhận motion + camera. Không gửi script / JSON / sheet.");
    }

    public static string Fingerprint(string sourceHash, string prompt, string model, int duration) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{sourceHash}|{prompt}|{model}|{duration}")))
            .ToLowerInvariant();

    public static void EnsureNotBlindRetry(string previousFingerprint, string nextFingerprint, string? retryReason)
    {
        if (string.IsNullOrWhiteSpace(previousFingerprint)) return;
        if (!string.Equals(previousFingerprint, nextFingerprint, StringComparison.OrdinalIgnoreCase)) return;
        throw new InvalidOperationException(
            "DO_NOT_BLIND_RETRY: cùng keyframe + cùng motion đã FAIL. Cần MOTION_PROMPT_REVISION hoặc KEYFRAME_REPAIR.");
    }

    public static void EnsureRetryReason(string? reason)
    {
        var r = (reason ?? "").Trim().ToUpperInvariant();
        if (r is "" or "MOTION_PROMPT_REVISION" or "KEYFRAME_REPAIR") return;
        throw new InvalidOperationException("Retry phải có reason MOTION_PROMPT_REVISION hoặc KEYFRAME_REPAIR.");
    }

    public static string ClassifyFailure(int? httpStatus, string? failureCode, string? error)
    {
        var code = (failureCode ?? "").Trim().ToUpperInvariant();
        if (httpStatus == 429 || code.Contains("429") || (error ?? "").Contains("quota", StringComparison.OrdinalIgnoreCase))
            return "RATE_LIMITED";
        if (httpStatus is >= 400 and < 500 && httpStatus != 429)
            return "INVALID_INPUT";
        if (code.StartsWith("SAFETY", StringComparison.Ordinal))
            return "SAFETY_BLOCKED";
        if (code.Contains("BAD_OUTPUT", StringComparison.Ordinal))
            return "GENERATION_FAILED";
        return "RUNWAY_FAILED";
    }

    public static bool IsRunwayAccepted(int httpStatus, string? taskId) =>
        httpStatus is 200 or 201 && !string.IsNullOrWhiteSpace(taskId);

    public static bool IsVideoReady(string providerStatus, string? outputUrl, bool downloaded, bool readable) =>
        providerStatus.Equals("SUCCEEDED", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(outputUrl)
        && downloaded
        && readable;

    public static KitVideoVideoQaDto EvaluateDeclared(JsonElement motion, JsonElement obs)
    {
        var p0 = new List<string>();
        var p1 = new List<string>();
        var p2 = new List<string>();
        var expected = ArrStr(motion, "characters");
        if (expected.Count == 0) expected = ["CHAR-001", "CHAR-003"];
        var detected = ArrStr(obs, "characters");
        if (detected.Count != expected.Count)
            p0.Add($"Character count: Expected {expected.Count} Detected {detected.Count}");
        foreach (var code in expected.Where(c => !detected.Contains(c, StringComparer.OrdinalIgnoreCase)))
            p0.Add($"Missing person: {code}");
        foreach (var extra in detected.Where(d => !expected.Contains(d, StringComparer.OrdinalIgnoreCase)))
            p0.Add($"Extra person: {extra}");
        if (obs.TryGetProperty("identityBreak", out var ib) && ib.ValueKind == JsonValueKind.True)
            p0.Add("Identity break");
        if (obs.TryGetProperty("deformation", out var def) && def.ValueKind == JsonValueKind.True)
            p0.Add("Major deformation");
        if (obs.TryGetProperty("wrongLocation", out var loc) && loc.ValueKind == JsonValueKind.True)
            p0.Add("Wrong location");
        if (obs.TryGetProperty("wrongWardrobe", out var wd) && wd.ValueKind == JsonValueKind.True)
            p0.Add("Wrong wardrobe");
        if (obs.TryGetProperty("corrupt", out var cor) && cor.ValueKind == JsonValueKind.True)
            p0.Add("Corrupt video");
        if (obs.TryGetProperty("actionOccurred", out var act) && act.ValueKind == JsonValueKind.False)
            p0.Add("Requested action did not happen");
        if (obs.TryGetProperty("forbiddenAction", out var forb) && forb.ValueKind == JsonValueKind.True)
            p0.Add("Forbidden action");
        if (obs.TryGetProperty("minorPropDrift", out var drift) && drift.ValueKind == JsonValueKind.True)
            p1.Add("Minor prop drift");
        if (obs.TryGetProperty("slightUnnatural", out var un) && un.ValueKind == JsonValueKind.True)
            p2.Add("Slight unnatural movement");
        var status = p0.Count > 0 ? "FAIL" : p1.Count > 0 ? "REVIEW_REQUIRED" : "PASS";
        return new KitVideoVideoQaDto(
            status,
            new Dictionary<string, int>
            {
                ["technical"] = 100,
                ["character"] = p0.Any(x => x.Contains("person", StringComparison.OrdinalIgnoreCase) || x.Contains("Identity") || x.Contains("Character")) ? 0 : 100,
                ["continuity"] = p0.Any(x => x.Contains("location", StringComparison.OrdinalIgnoreCase) || x.Contains("wardrobe", StringComparison.OrdinalIgnoreCase)) ? 0 : 100,
                ["motion"] = p0.Any(x => x.Contains("action", StringComparison.OrdinalIgnoreCase) || x.Contains("deformation", StringComparison.OrdinalIgnoreCase)) ? 0 : 100,
            },
            p0, p1, p0.Concat(p1).Concat(p2).ToList(), status != "FAIL",
            null, null, p1, p2,
            "PASS",
            p0.Any(x => x.Contains("person", StringComparison.OrdinalIgnoreCase) || x.Contains("Identity")) ? "FAIL" : "PASS",
            p0.Any(x => x.Contains("location", StringComparison.OrdinalIgnoreCase) || x.Contains("wardrobe", StringComparison.OrdinalIgnoreCase)) ? "FAIL" : "PASS",
            p0.Any(x => x.Contains("action", StringComparison.OrdinalIgnoreCase)) ? "FAIL" : "PASS");
    }

    private static List<string> ArrStr(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            return [];
        return el.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    public static string Diagnose(string? failureCode, string? error)
    {
        var code = (failureCode ?? "").Trim().ToUpperInvariant();
        var blob = $"{code} {error}";
        if (blob.Contains("BAD_OUTPUT", StringComparison.OrdinalIgnoreCase))
            return "INTERNAL.BAD_OUTPUT — không gửi lại cùng fingerprint. Sửa motion prompt hoặc repair keyframe.";
        if (blob.Contains("SAFETY", StringComparison.OrdinalIgnoreCase))
            return "SAFETY — đổi motion, không blind retry.";
        if (blob.Contains("429", StringComparison.OrdinalIgnoreCase) || blob.Contains("quota", StringComparison.OrdinalIgnoreCase))
            return "RATE_LIMIT — chờ, không đổi input.";
        return string.IsNullOrWhiteSpace(error) ? "Runway FAILED — diagnose trước khi retry." : error!;
    }

    public static KitVideoMotionPreflightDto Preflight(
        string shotCode,
        bool i2vReady,
        IReadOnlyList<string>? i2vBlocked,
        string? imageType,
        string liveHash,
        string storedHash,
        string qaHash,
        string approvedHash,
        byte[]? jpeg,
        KitVideoArtifactValidation? imageCheck,
        KitVideoMotionCompileDto compiled,
        bool keyConfigured)
    {
        var blocked = new List<string>();
        try { EnsureGoldenShot(shotCode); }
        catch (InvalidOperationException ex) { blocked.Add(ex.Message); }
        if (!i2vReady) blocked.Add("I2V_READY = FALSE");
        if (i2vBlocked is { Count: > 0 }) blocked.AddRange(i2vBlocked);
        if (!string.Equals(imageType, KitVideoIntegrityRules.ProductionStill, StringComparison.OrdinalIgnoreCase))
            blocked.Add("Image type is not PRODUCTION_STILL");
        if (string.IsNullOrWhiteSpace(liveHash) || liveHash != storedHash || liveHash != qaHash || liveHash != approvedHash)
            blocked.Add("SHA256 keyframe không khớp file / QA / approve");
        if (jpeg is null || jpeg.Length < 32) blocked.Add("Approved keyframe không đọc được");
        else if (jpeg.Length < 3 || jpeg[0] != 0xFF || jpeg[1] != 0xD8 || jpeg[2] != 0xFF)
            blocked.Add("Artifact không phải JPEG");
        if (imageCheck is not { Ok: true })
            blocked.Add(imageCheck is null ? "Keyframe chưa validate 1280×720" : string.Join(" | ", imageCheck.Reasons));
        if (compiled.DurationSec is not (5 or 10)) blocked.Add("duration phải 5 hoặc 10");
        if (!compiled.Model.Equals(Model, StringComparison.OrdinalIgnoreCase)) blocked.Add("model phải gen4_turbo");
        if (compiled.Ratio != Ratio) blocked.Add("ratio phải 1280:720");
        if (string.IsNullOrWhiteSpace(compiled.Prompt)) blocked.Add("motion contract invalid");
        if (!keyConfigured) blocked.Add("Runway API key chưa cấu hình");
        return new KitVideoMotionPreflightDto(blocked.Count == 0, blocked, compiled);
    }

    public static bool CanAdvance(string from, string to)
    {
        var f = from.Trim().ToUpperInvariant();
        var t = to.Trim().ToUpperInvariant();
        return (f, t) switch
        {
            ("READY", "SUBMITTING") => true,
            ("SUBMITTING", "SUBMITTED") => true,
            ("SUBMITTING", "FAILED") => true,
            ("SUBMITTED", "PROCESSING") => true,
            ("PROCESSING", "SUCCEEDED") => true,
            ("PROCESSING", "FAILED") => true,
            ("SUBMITTED", "RUNWAY_ACCEPTED") => true,
            ("SUCCEEDED", "DOWNLOADING") => true,
            ("DOWNLOADING", "ARTIFACT_VERIFY") => true,
            ("DOWNLOADING", "FAILED") => true,
            ("ARTIFACT_VERIFY", "VIDEO_QA") => true,
            ("ARTIFACT_VERIFY", "FAILED") => true,
            ("VIDEO_QA", "READY_FOR_DIRECTOR") => true,
            ("VIDEO_QA", "VIDEO_QA_FAIL") => true,
            ("VIDEO_QA", "FAILED") => true,
            ("READY_FOR_DIRECTOR", "APPROVED_TAKE") => true,
            ("READY_FOR_DIRECTOR", "REJECTED") => true,
            ("READY_FOR_DIRECTOR", "INVALIDATED") => true,
            ("FAILED", "DIAGNOSE") => true,
            _ => f == t,
        };
    }

    public static KitVideoVideoQaDto EvaluateTechnical(
        byte[]? video,
        string? providerStatus,
        string? outputUrl,
        int expectedDuration,
        string liveVideoHash,
        string storedVideoHash,
        string sourceHash)
    {
        var p0 = new List<string>();
        if (!string.Equals(providerStatus, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            p0.Add("Provider status ≠ SUCCEEDED");
        if (string.IsNullOrWhiteSpace(outputUrl)) p0.Add("Output URL missing");
        if (video is null || video.Length < 32) p0.Add("Video file missing");
        else if (!Mp4LooksValid(video)) p0.Add("Video not a readable MP4");
        var duration = video is null ? 0 : ReadMp4DurationSec(video);
        if (duration <= 0) p0.Add("Duration unreadable");
        else if (Math.Abs(duration - expectedDuration) > 1.5)
            p0.Add($"Duration {duration:0.#}s required {expectedDuration}s");
        if (string.IsNullOrWhiteSpace(liveVideoHash) || liveVideoHash != storedVideoHash)
            p0.Add("Video fingerprint mismatch");
        if (string.IsNullOrWhiteSpace(sourceHash)) p0.Add("Source keyframe hash missing");
        var status = p0.Count == 0 ? "PASS" : "FAIL";
        return new KitVideoVideoQaDto(
            status,
            new Dictionary<string, int> { ["technical"] = p0.Count == 0 ? 100 : 0 },
            p0,
            [],
            p0,
            status == "PASS",
            liveVideoHash,
            sourceHash);
    }

    public static bool Mp4LooksValid(byte[] bytes)
    {
        if (bytes.Length < 12) return false;
        return bytes[4] == (byte)'f' && bytes[5] == (byte)'t' && bytes[6] == (byte)'y' && bytes[7] == (byte)'p';
    }

    public static double ReadMp4DurationSec(byte[] bytes)
    {
        for (var i = 0; i + 8 < bytes.Length && i < Math.Min(bytes.Length, 2_000_000);)
        {
            var size = ReadBe32(bytes, i);
            if (size < 8) break;
            if (i + 8 <= bytes.Length && bytes[i + 4] == (byte)'m' && bytes[i + 5] == (byte)'o'
                && bytes[i + 6] == (byte)'o' && bytes[i + 7] == (byte)'v')
            {
                return ReadMvhd(bytes, i + 8, i + size);
            }
            i += size;
        }
        return 0;
    }

    private static double ReadMvhd(byte[] b, int start, int end)
    {
        for (var i = start; i + 24 < end && i + 24 < b.Length; i++)
        {
            if (b[i] != (byte)'m' || b[i + 1] != (byte)'v' || b[i + 2] != (byte)'h' || b[i + 3] != (byte)'d')
                continue;
            var version = b[i + 4];
            if (version == 1 && i + 36 < b.Length)
            {
                var timescale = ReadBe32(b, i + 20);
                var duration = ReadBe32(b, i + 28);
                return timescale > 0 ? duration / (double)timescale : 0;
            }
            if (i + 24 < b.Length)
            {
                var timescale = ReadBe32(b, i + 16);
                var duration = ReadBe32(b, i + 20);
                return timescale > 0 ? duration / (double)timescale : 0;
            }
        }
        return 0;
    }

    private static int ReadBe32(byte[] b, int o) =>
        o + 4 > b.Length ? 0 : (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    private static int ReadDuration(JsonElement contract)
    {
        if (contract.ValueKind != JsonValueKind.Object) return 5;
        if (contract.TryGetProperty("durationSec", out var d) && d.TryGetInt32(out var n))
            return n;
        return 5;
    }

    private static string Str(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? ""
            : "";
}

public sealed record KitVideoMotionCompileDto(
    string ShotCode,
    string Prompt,
    string Model,
    int DurationSec,
    string Ratio,
    string Compiler);

public sealed record KitVideoMotionPreflightDto(
    bool Ok,
    IReadOnlyList<string> Blocked,
    KitVideoMotionCompileDto Compiled);

public sealed record KitVideoVideoQaDto(
    string Status,
    IReadOnlyDictionary<string, int> Scores,
    IReadOnlyList<string> P0Fail,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Reasons,
    bool AllowApprove,
    string? VideoHash = null,
    string? SourceArtifactHash = null,
    IReadOnlyList<string>? P1 = null,
    IReadOnlyList<string>? P2 = null,
    string Technical = "",
    string Character = "",
    string Continuity = "",
    string Motion = "");

public sealed record KitVideoRunwayRequestDto(
    string Model,
    string InputImage,
    int Duration,
    string Ratio,
    string PromptText,
    string SourceArtifactHash);

public interface IRunwayRequestCompiler
{
    KitVideoRunwayRequestDto Compile(KitVideoI2vReadyPackageDto pack, JsonElement motionContract);
}

public interface IVideoOutputQA
{
    KitVideoVideoQaDto Evaluate(JsonElement motionContract, JsonElement observation);
}
