using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Shot plan beside a library script (<c>*.anh.json</c>).
/// A matching plan supplies the Gemini prompt and the frame ratio.
/// No plan, or a line that does not match, keeps the caller's existing prompt.
/// </summary>
public static class NgvShotPlan
{
    public const string LegacyAspect = "9:16";

    public sealed record Shot(int From, int To, string First, string Prompt);

    public sealed record Choice(string Prompt, string AspectRatio, string? Note, int From, int To, bool UsedPlan);

    static readonly Regex ShortOnly = new(@"^(?:[-*]\s*)?\[\s*(?:chỉ\s*)?ngắn\s*\]\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex SpokenLabel = new(@"^(một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|thứ\s+\S+|bước\s*\d+|\d+|lưu ý|ví dụ|nhớ|quy tắc(\s*\d+)?|điều\s+\S+|nguồn|kết luận|tóm lại|chú ý|cảnh báo|câu hỏi|hỏi|đáp)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Normalize(string? text) => (text ?? "").Normalize(NormalizationForm.FormC).Trim();

    /// <summary>Same spoken sentence the film uses: drop a [ngắn] mark and a speaker label, keep "Bước 3:".</summary>
    public static string Spoken(string? text)
    {
        var line = ShortOnly.Replace(Normalize(text), "").Replace("**", "", StringComparison.Ordinal).Trim();
        var colon = line.IndexOf(':');
        if (colon is > 0 and <= 40)
        {
            var label = line[..colon].Trim();
            var words = label.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (label.Length > 0 && words.Length <= 3 && label.IndexOfAny([',', '.', '!', '?', '…']) < 0 && !SpokenLabel.IsMatch(label))
                return line[(colon + 1)..].Trim();
        }
        return line;
    }

    /// <summary>
    /// Line index to shot index. A line between two anchors, including a [ngắn] line the plan does not name,
    /// stays in the shot that starts at the previous anchor. Returns null when any shot's first line is missing.
    /// </summary>
    public static int[]? Map(IReadOnlyList<Shot> shots, IReadOnlyList<string> lines)
    {
        if (shots.Count == 0 || lines.Count == 0) return null;
        var anchors = new int[shots.Count];
        var cursor = 0;
        for (var shot = 0; shot < shots.Count; shot++)
        {
            var want = Spoken(shots[shot].First);
            if (want.Length == 0) return null;
            var found = -1;
            for (var i = cursor; i < lines.Count; i++)
            {
                if (Spoken(lines[i]) == want)
                {
                    found = i;
                    break;
                }
            }
            if (found < 0) return null;
            anchors[shot] = found;
            cursor = found + 1;
        }
        var map = new int[lines.Count];
        Array.Fill(map, -1);
        for (var shot = 0; shot < shots.Count; shot++)
        {
            var end = shot + 1 < shots.Count ? anchors[shot + 1] : lines.Count;
            for (var i = anchors[shot]; i < end; i++) map[i] = shot;
        }
        return map;
    }

    public static IReadOnlyList<Shot> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("shots", out var shots) || shots.ValueKind != JsonValueKind.Array)
            return Array.Empty<Shot>();
        var list = new List<Shot>();
        foreach (var item in shots.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("from", out var fromEl) || !item.TryGetProperty("to", out var toEl)) continue;
            if (!fromEl.TryGetInt32(out var from) || !toEl.TryGetInt32(out var to) || from < 0 || to < from || to > 500) continue;
            var first = item.TryGetProperty("first", out var firstEl) && firstEl.ValueKind == JsonValueKind.String ? firstEl.GetString() ?? "" : "";
            var prompt = item.TryGetProperty("prompt", out var promptEl) && promptEl.ValueKind == JsonValueKind.String ? promptEl.GetString() ?? "" : "";
            if (prompt.Trim().Length < 2) continue;
            list.Add(new Shot(from, to, first, prompt));
        }
        return list;
    }

    /// <summary>
    /// <paramref name="lines"/> is the spoken sentence list, index 0 first, including short-only lines.
    /// When every shot's first line is found, a [ngắn] line between anchors uses that shot's image.
    /// Otherwise the file index is used, and a mismatched first line keeps the legacy prompt.
    /// Wide frames ask for 16:9. Every other orientation asks for 9:16.
    /// </summary>
    public static Choice Resolve(IReadOnlyList<Shot>? shots, int index, IReadOnlyList<string>? lines, string? orientation, string legacyPrompt)
    {
        if (shots == null || shots.Count == 0)
            return new Choice(legacyPrompt, LegacyAspect, null, index, index, false);
        if (lines is { Count: > 0 })
        {
            var map = Map(shots, lines);
            if (map != null && index >= 0 && index < map.Length && map[index] >= 0)
            {
                var shotIndex = map[index];
                var from = 0;
                while (map[from] != shotIndex) from++;
                var to = map.Length - 1;
                while (map[to] != shotIndex) to--;
                return Planned(shots[shotIndex], orientation, from, to);
            }
        }
        Shot? shot = null;
        foreach (var item in shots)
        {
            if (item.From <= index && index <= item.To)
            {
                shot = item;
                break;
            }
        }
        if (shot == null)
            return new Choice(legacyPrompt, LegacyAspect, "Kế hoạch hình không có shot cho câu này.", index, index, false);
        var anchor = lines != null && shot.From < lines.Count ? lines[shot.From] ?? "" : "";
        if (!string.Equals(Spoken(shot.First), Spoken(anchor), StringComparison.Ordinal))
            return new Choice(legacyPrompt, LegacyAspect, $"Kế hoạch hình không khớp câu {shot.From + 1}. Đã dùng prompt cũ.", shot.From, shot.To, false);
        return Planned(shot, orientation, shot.From, shot.To);
    }

    static Choice Planned(Shot shot, string? orientation, int from, int to)
    {
        var aspect = string.Equals(orientation, "wide", StringComparison.OrdinalIgnoreCase) ? "16:9" : "9:16";
        return new Choice(FitRatio(shot.Prompt, aspect), aspect, null, from, to, true);
    }

    public static string FitRatio(string prompt, string aspect)
    {
        if (aspect == "9:16")
            return prompt.Replace("16:9", "9:16", StringComparison.Ordinal);
        return prompt.Replace("9:16", "16:9", StringComparison.Ordinal);
    }
}
