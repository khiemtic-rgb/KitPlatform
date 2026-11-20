using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Một câu sẽ gửi ElevenLabs: chữ đã kèm thẻ cảm xúc, giọng, và dấu vân tay để biết câu nào đã có giọng đúng.</summary>
public sealed record LinesheetTtsItem(
    string RowId,
    string Speaker,
    string VoiceId,
    string Tags,
    string Text,
    int Chars,
    string Fingerprint,
    int Take = 1);

/// <summary>
/// Kế hoạch tạo giọng ElevenLabs (Eleven v3) cho cả tập. Thuần, không gọi mạng.
/// Bài học khi thử tay (2026-11-04): câu ngắn gửi trơn không ra cảm xúc, nên mỗi câu kèm thẻ diễn xuất
/// lấy từ cột direction (hoặc cột tts_tags nếu ghi tay). Dấu chấm than làm v3 đổi cảm xúc giữa câu, nên đổi thành dấu chấm.
/// </summary>
public static class LinesheetTtsPlan
{
    public const string Model = "eleven_v3";

    /// <summary>Bộ chuyển mặc định từ ghi chú tiếng Việt sang thẻ v3 (quy tắc đầu tiên khớp thì dùng, tối đa 2 thẻ).</summary>
    /// <remarks>
    /// Sửa 2026-11-04 sau khi nghe EP01: "bình thản giả" từng ra [forced cheerfulness] làm Bố nghe vui khi mất việc,
    /// nên chuyển sang nhóm buồn [sad].
    /// Không dùng chữ "buồn" đứng một mình vì sẽ khớp nhầm "buồn cười". Thêm [sighs] cho "thở dài" và [shouting] cho "quát to".
    /// </remarks>
    public static readonly IReadOnlyList<LinesheetTtsTagRule> DefaultTagRules =
    [
        new() { DirectionContains = ["thì thầm", "rất nhỏ", "lẩm bẩm", "lẩm nhẩm"], Tag = "[whispers]" },
        new() { DirectionContains = ["buồn bã", "tiếc nuối", "cam chịu", "bình thản giả"], Tag = "[sad]" },
        new() { DirectionContains = ["thở dài"], Tag = "[sighs]" },
        new() { DirectionContains = ["nghẹn"], Tag = "[holding back tears]" },
        new() { DirectionContains = ["run"], Tag = "[shaky voice]" },
        new() { DirectionContains = ["gắt", "quát"], Tag = "[angry]" },
        new() { DirectionContains = ["quát to", "hét lên"], Tag = "[shouting]" },
        new() { DirectionContains = ["hoảng", "cuống"], Tag = "[panicked]" },
        new() { DirectionContains = ["hét", "phấn khích", "hào hứng"], Tag = "[excited]" },
        new() { DirectionContains = ["cười khẽ", "bật cười", "hơi cười"], Tag = "[light chuckle]" },
        new() { DirectionContains = ["cười gượng", "cố tươi", "đùa nhạt"], Tag = "[forced cheerfulness]" },
        new() { DirectionContains = ["dịu", "thương", "ấm"], Tag = "[softly]" },
        new() { DirectionContains = ["mệt", "đều đều"], Tag = "[tired]" },
        new() { DirectionContains = ["tự giễu", "trêu", "nửa đùa"], Tag = "[wry]" },
        new() { DirectionContains = ["dỗi", "cãi lại"], Tag = "[sulky]" },
        new() { DirectionContains = ["ngại", "áy náy", "khó xử", "lúng túng"], Tag = "[hesitant]" },
        new() { DirectionContains = ["chậm", "để lửng", "trầm"], Tag = "[slowly]" },
    ];

    static readonly Regex Exclaim = new("!+", RegexOptions.Compiled);
    static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>Thẻ của một câu: cột tts_tags nếu có; không thì tối đa 2 thẻ suy từ direction.</summary>
    public static string TagsFor(LinesheetRow row, LinesheetSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(row.TtsTags)) return row.TtsTags.Trim();
        var rules = settings.TtsTagRules.Count > 0 ? settings.TtsTagRules : DefaultTagRules;
        var tags = new List<string>();
        foreach (var rule in rules)
        {
            if (tags.Count >= 2) break;
            if (rule.Tag.Length == 0 || tags.Contains(rule.Tag)) continue;
            if (rule.DirectionContains.Any(k => ContainsWord(row.Direction, k)))
                tags.Add(rule.Tag);
        }

        return string.Join(" ", tags);
    }

    /// <summary>Khớp theo từ: "run" không khớp "Trung", "ấm" không khớp "Nấm".</summary>
    public static bool ContainsWord(string text, string phrase)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(phrase)) return false;
        var i = 0;
        while ((i = text.IndexOf(phrase, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var before = i == 0 || !char.IsLetterOrDigit(text[i - 1]);
            var end = i + phrase.Length;
            var after = end >= text.Length || !char.IsLetterOrDigit(text[end]);
            if (before && after) return true;
            i++;
        }

        return false;
    }

    public static string SpokenText(LinesheetRow row, string tags)
    {
        var body = Spaces.Replace(Exclaim.Replace(row.Vi.Trim(), "."), " ");
        return tags.Length > 0 ? tags + " " + body : body;
    }

    public static LinesheetVoiceDef? VoiceFor(string speaker, LinesheetSettings settings) =>
        settings.Voices.TryGetValue(speaker, out var v) && !string.IsNullOrWhiteSpace(v.VoiceId) ? v : null;

    /// <summary>Lần tạo 1 giữ đúng dấu vân tay cũ, để thêm tính năng lần tạo không bắt tạo lại các câu đã có.</summary>
    public static string Fingerprint(string voiceId, LinesheetVoiceDef v, string text, int take = 1) =>
        take <= 1
            ? LinesheetRenderPlan.Sha256Hex(JsonSerializer.Serialize(new
            {
                m = Model, voiceId, text, v.Stability, v.SimilarityBoost, v.Style, v.Speed,
            }))[..16]
            : LinesheetRenderPlan.Sha256Hex(JsonSerializer.Serialize(new
            {
                m = Model, voiceId, text, v.Stability, v.SimilarityBoost, v.Style, v.Speed, take,
            }))[..16];

    /// <summary>Lập kế hoạch cho các câu thoại; câu thiếu giọng của người nói được trả riêng ở <paramref name="missingVoices"/>.</summary>
    public static IReadOnlyList<LinesheetTtsItem> Plan(
        IReadOnlyList<LinesheetRow> rows,
        LinesheetSettings settings,
        out IReadOnlyList<string> missingVoices,
        IReadOnlyCollection<string>? onlyRowIds = null,
        IReadOnlyDictionary<string, int>? takes = null)
    {
        var items = new List<LinesheetTtsItem>();
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var r in rows.Where(r => r.IsLine))
        {
            if (onlyRowIds is { Count: > 0 } && !onlyRowIds.Contains(r.Id)) continue;
            var v = VoiceFor(r.Speaker, settings);
            if (v is null)
            {
                missing.Add(r.Speaker);
                continue;
            }

            var tags = TagsFor(r, settings);
            var text = SpokenText(r, tags);
            var take = takes is not null && takes.TryGetValue(r.Id, out var t) && t > 1 ? t : 1;
            items.Add(new LinesheetTtsItem(r.Id, r.Speaker, v.VoiceId.Trim(), tags, text, text.Length,
                Fingerprint(v.VoiceId.Trim(), v, text, take), take));
        }

        missingVoices = missing.ToList();
        return items;
    }
}
