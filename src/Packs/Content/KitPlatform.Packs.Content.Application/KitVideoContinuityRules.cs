using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public interface IContinuityValidator
{
    KitVideoContinuityResult Validate(JsonElement shot, JsonElement? previousSnapshot);
}

public sealed record KitVideoContinuityResult(string Status, IReadOnlyList<string> Notes);

public sealed record KitVideoStoryGateResult(bool Ok, bool AllowKf, IReadOnlyList<string> Reasons);

public sealed record KitVideoScriptImpactResult(
    bool RebuildAll,
    IReadOnlyList<string> AffectedScenes,
    IReadOnlyList<string> AffectedBeats,
    IReadOnlyList<string> AffectedShots,
    IReadOnlyList<string> AffectedContinuity);

public static class KitVideoContinuityRules
{
    public const string Version = "KIT-VIDEO-CONTINUITY-V1";

    private static readonly Regex Concrete = new(
        @"(^|[^\p{L}])(chạy|bước|đi|đưa|giơ|cầm|đặt|ngồi|đứng|mở|nhìn|nói|khoe|raises?|walks?|runs?|shows?|holds?|puts?|sits?|stands?|approaches?)(?=$|[^\p{L}])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Vague = new(
        @"(^|[^\p{L}])(emotional|cảm xúc|buồn|vui vẻ)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Move = new(
        @"(chạy|bước|đi|walks?|runs?|approaches?|từ .+ đến|from .+ to)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsConcreteAction(string? action)
    {
        var t = (action ?? "").Trim();
        if (t.Length == 0) return false;
        if (Vague.IsMatch(t) && !Concrete.IsMatch(t)) return false;
        return Concrete.IsMatch(t);
    }

    public static string ShotStatusFromAction(string? action) =>
        IsConcreteAction(action) ? "READY" : "HOLD";

    public static KitVideoContinuityResult Validate(JsonElement shot, JsonElement? previous)
    {
        var notes = new List<string>();
        if (previous is null || previous.Value.ValueKind != JsonValueKind.Object)
            return new KitVideoContinuityResult("CONTINUITY_PASS", notes);

        var action = Str(shot, "action");
        var chars = Obj(shot, "characterState");
        var prevChars = Obj(previous.Value, "characters");
        if (chars.ValueKind == JsonValueKind.Object && prevChars.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in chars.EnumerateObject())
            {
                if (!prevChars.TryGetProperty(prop.Name, out var before)) continue;
                if (Str(prop.Value, "wardrobe") != Str(before, "wardrobe"))
                    notes.Add($"WARDROBE_DRIFT:{prop.Name}");
                if (Str(prop.Value, "position") != Str(before, "position") && !Move.IsMatch(action))
                    notes.Add($"TELEPORT:{prop.Name}");
                var look = Str(prop.Value, "facing");
                var prevLook = Str(before, "facing");
                if (!string.IsNullOrEmpty(prevLook) && !string.IsNullOrEmpty(look) && look != prevLook
                    && !Regex.IsMatch(action, @"quay|turns?|looks? (left|right)", RegexOptions.IgnoreCase))
                    notes.Add($"LOOK_FLIP:{prop.Name}");
            }
        }

        var loc = Str(Obj(shot, "locationState"), "location");
        var prevLoc = Str(Obj(previous.Value, "location"), "location");
        if (!string.IsNullOrEmpty(prevLoc) && loc != prevLoc
            && !Regex.IsMatch(action, @"đến|khác|leaves?|cut to", RegexOptions.IgnoreCase))
            notes.Add("LOCATION_DRIFT");

        var fail = notes.Exists(n => n.StartsWith("TELEPORT", StringComparison.Ordinal)
            || n == "LOCATION_DRIFT"
            || n.StartsWith("WARDROBE", StringComparison.Ordinal));
        return new KitVideoContinuityResult(
            fail ? "CONTINUITY_FAIL" : notes.Count > 0 ? "CONTINUITY_WARNING" : "CONTINUITY_PASS",
            notes);
    }

    public static KitVideoStoryGateResult PreGenerationGate(JsonElement shot, IReadOnlyList<string>? detected)
    {
        var reasons = new List<string>();
        var action = Str(shot, "action");
        var status = Str(shot, "status");
        var continuity = Str(shot, "continuity");
        if (status == "HOLD" || !IsConcreteAction(action)) reasons.Add("Action missing");
        var characters = Arr(shot, "characters");
        if (characters.Count == 0) reasons.Add("Required Character missing");
        if (Regex.IsMatch(action, @"mẹ|mother|linh|CHAR-003", RegexOptions.IgnoreCase)
            && !characters.Contains("CHAR-003", StringComparer.OrdinalIgnoreCase))
            reasons.Add("Required Character missing");
        var props = Arr(shot, "requiredProps");
        var propState = Obj(shot, "propState");
        var paperState = propState.ValueKind == JsonValueKind.Object
            && propState.TryGetProperty("PROP-001", out var st)
            ? st.GetString()
            : "not_present";
        if (Regex.IsMatch(action, @"bài|paper|cầm|holds?|đưa|shows?", RegexOptions.IgnoreCase)
            && (paperState == "not_present" || !props.Contains("PROP-001", StringComparer.OrdinalIgnoreCase)))
            reasons.Add("Required Prop missing");
        if (continuity == "CONTINUITY_FAIL") reasons.Add("Continuity FAIL");
        if (detected is { Count: > 0 } && detected.Count != characters.Count)
        {
            var extra = detected.Where(d => !characters.Contains(d, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (extra.Length > 0) reasons.Add($"Extra Character: {string.Join(',', extra)}");
            var missing = characters.Where(c => !detected.Contains(c, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (missing.Length > 0) reasons.Add($"Missing Character: {string.Join(',', missing)}");
        }

        var allowKf = reasons.Count == 0 && status == "READY" && continuity != "CONTINUITY_FAIL";
        return new KitVideoStoryGateResult(allowKf, allowKf, reasons);
    }

    public static void EnsureOverride(string? reason, string? approvedBy)
    {
        if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(approvedBy))
            throw new InvalidOperationException("OverrideReason + ApprovedBy bắt buộc.");
    }

    public static bool AllowsKeyframe(string status, string continuity) =>
        status == "READY" && continuity != "CONTINUITY_FAIL";

    private static string Str(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v)
            ? v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()
            : "";

    private static JsonElement Obj(JsonElement el, string name) =>
        el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) ? v : default;

    private static List<string> Arr(JsonElement el, string name)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array)
            return [];
        return v.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }
}

public sealed class KitVideoContinuityValidator : IContinuityValidator
{
    public KitVideoContinuityResult Validate(JsonElement shot, JsonElement? previousSnapshot) =>
        KitVideoContinuityRules.Validate(shot, previousSnapshot);
}
