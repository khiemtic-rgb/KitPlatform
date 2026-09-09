namespace KitPlatform.Packs.Content;

public static class KitVideoAssetRules
{
    public const string Version = "KIT-VIDEO-ASSET-V1";

    public static readonly string[] Kinds =
        ["CHARACTER", "LOCATION", "PRODUCT", "PROP", "VEHICLE", "ANIMAL", "OTHER", "WARDROBE"];

    public static readonly string[] Lifecycle =
        ["DRAFT", "CREATING", "REVIEW", "APPROVED", "LOCKED", "ARCHIVED"];

    public static bool CanUseInProduction(string lifecycle)
    {
        var s = (lifecycle ?? "").Trim().ToUpperInvariant();
        return s is "APPROVED" or "LOCKED";
    }

    public static void EnsureNotLocked(string lifecycle)
    {
        if (string.Equals(lifecycle, "LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("LOCKED: tạo Version mới. Không overwrite Canon.");
    }

    public static string NextVersion(string current)
    {
        var digits = new string((current ?? "V1").Where(char.IsDigit).ToArray());
        var n = int.TryParse(digits, out var v) ? v : 1;
        return $"V{n + 1}";
    }

    public static string? InheritWardrobe(string? previous, string? scriptChange) =>
        string.IsNullOrWhiteSpace(scriptChange) ? previous : scriptChange;

    public static KitVideoReferenceQa EvaluateReference(KitVideoReferenceQaInput input)
    {
        var reasons = new List<string>();
        if (input.Readable == false) reasons.Add("Not readable.");
        if (input.Width is < 64 || input.Height is < 64) reasons.Add("Resolution too low.");
        if (input.FaceVisible == false) reasons.Add("Face not visible.");
        if (input.Occlusion == true) reasons.Add("Severe occlusion.");
        if (input.Watermark == true) reasons.Add("Watermark.");
        if (input.UnexpectedText == true) reasons.Add("Unexpected text.");
        if (input.AspectOk == false) reasons.Add("Wrong aspect.");
        return new KitVideoReferenceQa(reasons.Count == 0, reasons.Count == 0 ? "PASS" : "BLOCK", reasons);
    }

    public static KitVideoKeyframeQa EvaluateKeyframe(KitVideoKeyframeQaInput input)
    {
        var reasons = new List<string>();
        var required = input.RequiredCharacters ?? [];
        var detected = input.DetectedCharacters ?? [];
        if (detected.Count != required.Count)
            reasons.Add($"Detected Characters = {detected.Count}, required {required.Count}.");
        foreach (var c in required)
        {
            if (!detected.Contains(c, StringComparer.OrdinalIgnoreCase))
                reasons.Add($"Missing character {c}.");
        }
        foreach (var p in input.RequiredProps ?? [])
        {
            if (!(input.DetectedProps ?? []).Contains(p, StringComparer.OrdinalIgnoreCase))
                reasons.Add($"Missing detail {p}.");
        }
        if ((input.ActionNeeds ?? []).Contains("interaction", StringComparer.OrdinalIgnoreCase) && detected.Count < 2)
            reasons.Add("Action interaction not visible.");
        if (input.HalfFace == true) reasons.Add("half face");
        if (input.CutOffHead == true) reasons.Add("cut-off head");
        if (input.MissingHands == true) reasons.Add("missing hands");
        if (input.PropOutsideFrame == true) reasons.Add("important prop outside frame");
        var ok = reasons.Count == 0;
        return new KitVideoKeyframeQa(ok, ok ? "PASS" : "FAIL", reasons, ok);
    }

    public static string CompileImagePrompt(KitVideoCompileRequest req)
    {
        if (!string.IsNullOrWhiteSpace(req.I2vImageSource)
            && !req.I2vImageSource.Equals("APPROVED_KEYFRAME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("I2V must use APPROVED KEYFRAME — not character crop/sheet.");

        var lines = new List<string> { $"ProjectStyle: {req.ProjectStyle ?? "project visual"}" };
        foreach (var layer in req.Layers ?? [])
        {
            if (string.IsNullOrWhiteSpace(layer)) continue;
            if (!string.IsNullOrWhiteSpace(req.Dialogue) && layer.Contains(req.Dialogue, StringComparison.Ordinal))
                continue;
            lines.Add(layer);
        }
        if (!string.IsNullOrWhiteSpace(req.Action)) lines.Add($"ShotAction {req.Action}");
        if (!string.IsNullOrWhiteSpace(req.Camera)) lines.Add($"Camera {req.Camera}");
        if (!string.IsNullOrWhiteSpace(req.Speaker)) lines.Add($"Speaker {req.Speaker}");
        if (!string.IsNullOrWhiteSpace(req.Emotion)) lines.Add($"Emotion {req.Emotion}");
        var prompt = string.Join('\n', lines);
        if (!string.IsNullOrWhiteSpace(req.Dialogue) && prompt.Contains(req.Dialogue, StringComparison.Ordinal))
            throw new InvalidOperationException("Prompt không chứa thoại.");
        return prompt;
    }

    public static string ImpactMessage(int usageCount) =>
        usageCount > 0
            ? $"Asset này đang được sử dụng trong {usageCount} Shot. Không được âm thầm thay đổi toàn bộ production."
            : "Asset chưa gắn shot.";
}

public sealed record KitVideoReferenceQaInput(
    bool? Readable = null,
    int? Width = null,
    int? Height = null,
    bool? FaceVisible = null,
    bool? Occlusion = null,
    bool? Watermark = null,
    bool? UnexpectedText = null,
    bool? AspectOk = null);

public sealed record KitVideoReferenceQa(bool Ok, string Status, IReadOnlyList<string> Reasons);

public sealed record KitVideoKeyframeQaInput(
    IReadOnlyList<string> RequiredCharacters,
    IReadOnlyList<string> DetectedCharacters,
    IReadOnlyList<string>? RequiredProps = null,
    IReadOnlyList<string>? DetectedProps = null,
    IReadOnlyList<string>? ActionNeeds = null,
    bool? HalfFace = null,
    bool? CutOffHead = null,
    bool? MissingHands = null,
    bool? PropOutsideFrame = null);

public sealed record KitVideoKeyframeQa(bool Ok, string Status, IReadOnlyList<string> Reasons, bool AllowI2v);

public sealed record KitVideoCompileRequest(
    string? ProjectStyle = null,
    IReadOnlyList<string>? Layers = null,
    string? Action = null,
    string? Camera = null,
    string? Speaker = null,
    string? Emotion = null,
    string? Dialogue = null,
    string? I2vImageSource = null);

public interface IAssetResolver
{
    KitVideoShotPackageDto Resolve(KitVideoResolveRequest request, IReadOnlyList<KitVideoAssetDto> catalog);
}

public interface IImagePromptCompiler
{
    KitVideoCompiledPromptDto Compile(KitVideoShotPackageDto package, KitVideoShotSpecDto spec, string? projectStyle);
}
