using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoIntegrityRules
{
    public const string ProductionStill = "PRODUCTION_STILL";

    public static readonly string[] NonStillTypes =
    [
        "CHARACTER_SHEET", "COLLAGE", "MULTI_PANEL", "REFERENCE_BOARD",
        "TEXT_HEAVY_IMAGE", "INVALID_COMPOSITION", "UNKNOWN",
    ];

    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    public static string RequiredImageType(JsonElement contract)
    {
        if (contract.ValueKind == JsonValueKind.Object
            && contract.TryGetProperty("productionImage", out var p)
            && p.ValueKind == JsonValueKind.Object
            && p.TryGetProperty("imageType", out var t))
        {
            var v = t.GetString();
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim().ToUpperInvariant();
        }
        return ProductionStill;
    }

    public static string InferImageType(string? declared, string? rawJson)
    {
        var d = (declared ?? "").Trim().ToUpperInvariant().Replace(' ', '_');
        var blob = $"{declared} {rawJson}";
        if (Regex.IsMatch(blob, @"character[_\s-]*sheet|master reference|wardrobe grid|expression sheet|model sheet|turnaround|locked badge|famixa logo", RegexOptions.IgnoreCase))
            return "CHARACTER_SHEET";
        if (Regex.IsMatch(blob, @"reference board", RegexOptions.IgnoreCase) || d == "REFERENCE_BOARD")
            return "REFERENCE_BOARD";
        if (Regex.IsMatch(blob, @"storyboard", RegexOptions.IgnoreCase))
            return "INVALID_COMPOSITION";
        if (Regex.IsMatch(blob, @"collage", RegexOptions.IgnoreCase) || d == "COLLAGE")
            return "COLLAGE";
        if (Regex.IsMatch(blob, @"multi[-_ ]panel|\bpanels\b", RegexOptions.IgnoreCase) || d == "MULTI_PANEL")
            return "MULTI_PANEL";
        if (Regex.IsMatch(blob, @"text[-_ ]heavy|unrequested text|watermark|logo overlay", RegexOptions.IgnoreCase) || d == "TEXT_HEAVY_IMAGE")
            return "TEXT_HEAVY_IMAGE";
        if (d is "CHARACTER_SHEET" or "COLLAGE" or "MULTI_PANEL" or "REFERENCE_BOARD" or "TEXT_HEAVY_IMAGE" or "INVALID_COMPOSITION")
            return d;
        if (d == ProductionStill || Regex.IsMatch(blob, @"production[_\s-]*still|cinematic still|single frame|single scene", RegexOptions.IgnoreCase))
            return ProductionStill;
        if (string.IsNullOrWhiteSpace(d) || d == "UNKNOWN") return "UNKNOWN";
        return d;
    }

    public static KitVideoVisionQaDto ImageTypeGate(string required, string actual)
    {
        var req = string.IsNullOrWhiteSpace(required) ? ProductionStill : required.ToUpperInvariant();
        var got = string.IsNullOrWhiteSpace(actual) ? "UNKNOWN" : actual.ToUpperInvariant();
        if (req == ProductionStill && got == ProductionStill)
            return new KitVideoVisionQaDto("PASS", new Dictionary<string, int> { ["imageType"] = 100 }, [], [], [], true, true, null, got);
        return new KitVideoVisionQaDto(
            "FAIL",
            new Dictionary<string, int> { ["imageType"] = 0 },
            [$"IMAGE_TYPE: {got} required {req}"],
            [],
            [$"IMAGE_TYPE: {got} required {req}"],
            false,
            false,
            null,
            got);
    }

    public static KitVideoI2vReadyPackageDto Gate(
        string shotCode,
        string attemptId,
        string attemptStatus,
        string? qaStatus,
        IReadOnlyList<string>? p0Fail,
        byte[]? artifact,
        string liveHash,
        string storedArtifactHash,
        string qaHash,
        string approvedHash,
        string imageType)
    {
        var blocked = new List<string>();
        if (artifact is null || artifact.Length < 32)
            blocked.Add("Artifact missing");
        if (string.IsNullOrWhiteSpace(liveHash) || string.IsNullOrWhiteSpace(storedArtifactHash) || liveHash != storedArtifactHash)
            blocked.Add("Artifact hash mismatch");
        if (string.IsNullOrWhiteSpace(qaStatus) || string.IsNullOrWhiteSpace(qaHash))
            blocked.Add("QA result missing");
        if (!string.IsNullOrWhiteSpace(qaHash) && qaHash != liveHash)
            blocked.Add("QA hash does not belong to artifact");
        if (!string.Equals(qaStatus, "PASS", StringComparison.OrdinalIgnoreCase))
            blocked.Add("Vision QA not PASS");
        if (p0Fail is { Count: > 0 })
            blocked.Add("P0 FAIL");
        if (!string.Equals(imageType, ProductionStill, StringComparison.OrdinalIgnoreCase))
            blocked.Add($"Image type {imageType} is not PRODUCTION_STILL");
        if (!string.Equals(attemptStatus, "APPROVED", StringComparison.OrdinalIgnoreCase))
            blocked.Add("Director not APPROVED");
        if (string.IsNullOrWhiteSpace(approvedHash) || approvedHash != liveHash)
            blocked.Add("Approved hash mismatch");
        return new KitVideoI2vReadyPackageDto(
            shotCode,
            attemptId,
            "APPROVED_KEYFRAME",
            blocked.Count == 0,
            false,
            blocked,
            liveHash,
            imageType);
    }
}
