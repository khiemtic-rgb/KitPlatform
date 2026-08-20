using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentSeriesPilotService : IContentSeriesPilotService
{
    public const string DefaultSeriesCode = "FAMIXA";
    private const int MaxGraphChars = 1_500_000;

    private static readonly HashSet<string> BinaryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "imageDataUrl",
        "keyframeDataUrl",
        "canonImageDataUrl",
    };

    private readonly ContentRepository _repo;
    private readonly ContentElevenLabsClient _elevenLabs;

    public ContentSeriesPilotService(ContentRepository repo, ContentElevenLabsClient elevenLabs)
    {
        _repo = repo;
        _elevenLabs = elevenLabs;
    }

    public async Task<ContentSeriesPilotDto> GetAsync(string seriesCode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(seriesCode);
        var row = await _repo.GetSeriesPilotAsync(code, cancellationToken);
        if (row is null)
        {
            using var empty = JsonDocument.Parse("{}");
            return new ContentSeriesPilotDto(code, empty.RootElement.Clone(), DateTimeOffset.MinValue);
        }
        return ToDto(row);
    }

    public async Task<ContentSeriesPilotDto> UpsertAsync(
        UpsertContentSeriesPilotRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.SeriesCode);
        var raw = request.Graph.ValueKind is JsonValueKind.Object or JsonValueKind.Array
            ? request.Graph.GetRawText()
            : "{}";
        var stripped = StripBinaries(raw);
        if (stripped.Length > MaxGraphChars)
            throw new InvalidOperationException("Graph Series quá lớn — bỏ packDraft cũ hoặc ảnh data URL rồi lưu lại.");
        var row = await _repo.UpsertSeriesPilotAsync(code, stripped, cancellationToken);
        return ToDto(row);
    }

    public Task<IReadOnlyList<ContentSeriesVoiceDto>> ListVoicesAsync(CancellationToken cancellationToken = default) =>
        _elevenLabs.ListVoicesAsync(cancellationToken);

    public async Task<byte[]> PreviewTtsAsync(
        string voiceId,
        string text,
        string? publicOwnerId = null,
        string? voiceName = null,
        ContentSeriesTtsVoiceSettings? voiceSettings = null,
        CancellationToken cancellationToken = default)
    {
        var spoken = (text ?? "").Trim();
        if (spoken.Length < 1)
            throw new InvalidOperationException("Thiếu câu thoại để nghe thử.");
        if (LooksLikeScreenplayDump(spoken))
            throw new InvalidOperationException("TTS chỉ nhận Voice Script (thoại CHAR). Không gửi heading/cảnh/action/CUT TO.");
        var voice = (voiceId ?? "").Trim();
        if (voice.Length is < 8 or > 64)
            throw new InvalidOperationException("Voice ID ElevenLabs không hợp lệ.");
        foreach (var ch in voice)
        {
            if (ch is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-')
                continue;
            throw new InvalidOperationException("Voice ID ElevenLabs không hợp lệ.");
        }
        if (!await _elevenLabs.IsConfiguredAsync(cancellationToken))
            throw new InvalidOperationException("Chưa có key ElevenLabs — Cấu hình AI.");
        return await _elevenLabs.SynthesizeMp3Async(spoken, voice, publicOwnerId, voiceName, voiceSettings, cancellationToken);
    }

    private static bool LooksLikeScreenplayDump(string spoken)
    {
        var lines = spoken.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length >= 4) return true;
        if (spoken.Contains("VIDEO ID:", StringComparison.OrdinalIgnoreCase)) return true;
        if (spoken.Contains("07. SCRIPT", StringComparison.OrdinalIgnoreCase)) return true;
        if (Regex.IsMatch(spoken, @"(?:^|\n)(?:SC|SCENE)\s*0*\d+\b", RegexOptions.IgnoreCase)
            && spoken.Length > 40)
            return true;
        if (Regex.IsMatch(spoken, @"\n(?:MINH|NAM|LINH|BỐ|MẸ)\s*:", RegexOptions.IgnoreCase)
            && spoken.Length > 60)
            return true;
        return false;
    }

    private static ContentSeriesPilotDto ToDto(ContentRepository.SeriesPilotRow row)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.GraphJson) ? "{}" : row.GraphJson);
        return new ContentSeriesPilotDto(row.SeriesCode, doc.RootElement.Clone(), row.UpdatedAt);
    }

    internal static string NormalizeCode(string? raw)
    {
        var code = (raw ?? "").Trim().ToUpperInvariant().Replace('-', '_');
        if (string.IsNullOrWhiteSpace(code)) return DefaultSeriesCode;
        if (code.Length > 64) code = code[..64];
        var sb = new StringBuilder(code.Length);
        foreach (var ch in code)
        {
            if (ch is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_')
                sb.Append(ch);
        }
        var clean = sb.ToString();
        if (clean.Length < 2 || clean[0] is < 'A' or > 'Z') return DefaultSeriesCode;
        return clean;
    }

    internal static string StripBinaries(string json)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("Graph Series không phải JSON hợp lệ.");
        }
        StripNode(node);
        return node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "{}";
    }

    private static void StripNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var drop = obj
                .Where(p => BinaryKeys.Contains(p.Key) || LooksLikeDataUrl(p.Value))
                .Select(p => p.Key)
                .ToList();
            foreach (var key in drop)
                obj.Remove(key);
            foreach (var prop in obj.ToList())
                StripNode(prop.Value);
            return;
        }
        if (node is JsonArray arr)
        {
            foreach (var item in arr)
                StripNode(item);
        }
    }

    private static bool LooksLikeDataUrl(JsonNode? node) =>
        node is JsonValue v
        && v.TryGetValue<string>(out var s)
        && s.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        && s.Contains(";base64,", StringComparison.OrdinalIgnoreCase);
}