using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Runway accepts 1000 characters for the whole promptText after every part is joined.
/// A part that does not fit is left off. The cap never slices a field or a JSON object.
/// </summary>
public static class RunwayPromptBudget
{
    public static RunwayPromptDecision Decide(string? story, string? acting, string? visual, string? shotContract)
    {
        var body = Join(story, acting, visual);
        var continuity = ProductionVisualContinuity.Slice(shotContract);
        var fallback = FitWholeFields(story, acting, visual, VideoVisualIngressV1Rules.RunwayPromptMax);
        return Finish(body, continuity, VideoVisualIngressV1Rules.RunwayPromptMax, fallback);
    }

    public static bool HasVisualJson(string? text)
    {
        var value = text ?? "";
        return value.Contains('{') && value.Contains("\"primary\"", StringComparison.Ordinal);
    }

    /// <summary>
    /// promptText the Runway client would put on the wire. An over-cap visual prompt
    /// becomes the intact approval text. It is not sliced inside the JSON.
    /// </summary>
    public static string CapVisualPrompt(string? joined, int cap)
    {
        var decision = FromJoined(joined, cap);
        return decision.Fits ? decision.PromptText : decision.ApprovalPrompt;
    }

    public static RunwayPromptDecision FromJoined(string? joined, int cap)
    {
        var limit = cap < 1 ? VideoVisualIngressV1Rules.RunwayPromptMax : cap;
        var source = (joined ?? "").Trim();
        var markerAt = source.IndexOf(ProductionVisualContinuity.Marker, StringComparison.Ordinal);
        var continuity = markerAt < 0 ? "" : ProductionVisualContinuity.Slice(source);
        var body = markerAt < 0 ? source : source[..markerAt].Trim();
        return Finish(body, continuity, limit, ExtractVisual(body));
    }

    static RunwayPromptDecision Finish(string body, string continuity, int cap, string wholeFieldFallback)
    {
        var joined = Join(body, continuity);
        if (joined.Length <= cap && Intact(joined))
            return new RunwayPromptDecision(true, joined, continuity.Length > 0, joined, null);

        var approval = body.Length <= cap && Intact(body)
            ? body
            : wholeFieldFallback.Length <= cap && Intact(wholeFieldFallback)
                ? wholeFieldFallback
                : "";
        var reason = ProductionFailureReasons.RunwayPromptOverCap
            + $" joined={joined.Length} cap={cap} continuity={continuity.Length} approval={approval.Length}. "
            + "Continuity was not attached because the cap would cut the prompt. "
            + "Approval prompt keeps complete fields: " + approval;
        return new RunwayPromptDecision(false, approval, false, approval, reason);
    }

    static string FitWholeFields(string? story, string? acting, string? visual, int cap)
    {
        var visualText = (visual ?? "").Trim();
        var fields = new[] { (story ?? "").Trim(), (acting ?? "").Trim(), visualText }
            .Where(part => part.Length > 0)
            .ToArray();
        var best = "";
        var bestScore = -1;
        var n = fields.Length;
        var limit = 1 << n;
        for (var mask = 1; mask < limit; mask++)
        {
            var parts = new List<string>();
            for (var i = 0; i < n; i++)
            {
                if ((mask & (1 << i)) != 0)
                    parts.Add(fields[i]);
            }
            var text = string.Join(" ", parts);
            if (text.Length > cap || !Intact(text))
                continue;
            var score = text.Length + (visualText.Length > 0 && text.Contains(visualText, StringComparison.Ordinal) ? 10_000 : 0);
            if (score > bestScore)
            {
                best = text;
                bestScore = score;
            }
        }
        return best;
    }

    static string ExtractVisual(string body)
    {
        var start = body.IndexOf('{');
        var end = body.LastIndexOf('}');
        if (start < 0 || end <= start)
            return "";
        var json = body[start..(end + 1)];
        return Intact(json) ? json : "";
    }

    public static bool Intact(string? text)
    {
        var value = text ?? "";
        var start = value.IndexOf('{');
        if (start < 0)
            return true;
        var end = value.LastIndexOf('}');
        if (end <= start)
            return false;
        if (value.IndexOf('{', end + 1) >= 0)
            return false;
        try
        {
            using var doc = JsonDocument.Parse(value[start..(end + 1)]);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    static string Join(params string?[] parts) =>
        string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
}

public sealed record RunwayPromptDecision(
    bool Fits,
    string PromptText,
    bool ContinuityIncluded,
    string ApprovalPrompt,
    string? Reason);
