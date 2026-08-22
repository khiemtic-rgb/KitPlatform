using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Optional TTS for Video MVP V1. No-op when API key missing.</summary>
internal sealed class ContentElevenLabsClient
{
    private readonly HttpClient _http;
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public ContentElevenLabsClient(
        HttpClient http,
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _http = http;
        _repo = repo;
        _options = options;
        _configuration = configuration;
        if (_http.BaseAddress is null)
            _http.BaseAddress = new Uri("https://api.elevenlabs.io/");
    }

    public async Task<ContentVideoResolved> ResolveAsync(CancellationToken cancellationToken)
    {
        var row = await _repo.GetOrgSettingsAsync(cancellationToken);
        return ContentVideoConfigParser.Resolve(
            ContentVideoConfigParser.Parse(row.VideoConfigJson),
            _options.Value,
            _configuration);
    }

    public async Task<bool> IsConfiguredAsync(CancellationToken cancellationToken) =>
        (await ResolveAsync(cancellationToken)).ElevenLabsConfigured;

    public async Task<(bool Ok, string Message)> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        if (!resolved.ElevenLabsConfigured)
            return (false, "Chưa có ElevenLabs API key — đặt Secret ref hoặc dán key (chỉ ghi).");

        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/user");
        req.Headers.Add("xi-api-key", resolved.ElevenLabsApiKey);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = body.Length > 300 ? body[..300] + "…" : body;
            return (false, $"ElevenLabs {(int)res.StatusCode}: {snippet}");
        }
        return (true, $"ElevenLabs kết nối OK · voice {resolved.VoiceId}");
    }

    public Task<byte[]> SynthesizeMp3Async(string text, CancellationToken cancellationToken) =>
        SynthesizeMp3Async(text, voiceId: null, publicOwnerId: null, voiceName: null, voiceSettings: null, cancellationToken);

    public Task<byte[]> SynthesizeMp3Async(string text, string? voiceId, CancellationToken cancellationToken) =>
        SynthesizeMp3Async(text, voiceId, publicOwnerId: null, voiceName: null, voiceSettings: null, cancellationToken);

    public Task<byte[]> SynthesizeMp3Async(
        string text,
        string? voiceId,
        string? publicOwnerId,
        string? voiceName,
        CancellationToken cancellationToken) =>
        SynthesizeMp3Async(text, voiceId, publicOwnerId, voiceName, voiceSettings: null, cancellationToken);

    public async Task<byte[]> SynthesizeMp3Async(
        string text,
        string? voiceId,
        string? publicOwnerId,
        string? voiceName,
        ContentSeriesTtsVoiceSettings? voiceSettings,
        CancellationToken cancellationToken,
        string? accent = null)
    {
        var resolved = await ResolveAsync(cancellationToken);
        var key = resolved.ElevenLabsApiKey
                  ?? throw new InvalidOperationException("Chưa cấu hình ElevenLabsApiKey.");
        var voice = FirstNonEmpty(voiceId) ?? resolved.VoiceId;
        if (string.IsNullOrWhiteSpace(voice))
            throw new InvalidOperationException("Thiếu Voice ID ElevenLabs.");
        voice = await EnsureLibraryVoiceAsync(key, voice, publicOwnerId, voiceName, cancellationToken);
        var spoken = StripSpokenDirection(text);
        if (spoken.Length > 5000) spoken = spoken[..5000];
        var stability = Clamp01(voiceSettings?.Stability, 0.5);
        var similarity = Clamp01(voiceSettings?.SimilarityBoost, 0.75);
        var style = Clamp01(voiceSettings?.Style, 0.0);
        var speed = voiceSettings?.Speed is >= 0.7 and <= 1.2 ? voiceSettings.Speed.Value : 1.0;
        var lockNorth = LooksNorthernLabel($"{accent} {voiceName}");
        var settings = new
        {
            stability,
            similarity_boost = similarity,
            style,
            speed,
            use_speaker_boost = true,
        };
        object body = lockNorth
            ? new
            {
                text = spoken,
                model_id = "eleven_v3",
                apply_text_normalization = "off",
                voice_settings = settings,
            }
            : new
            {
                text = spoken,
                model_id = "eleven_v3",
                language_code = "vi",
                apply_text_normalization = "on",
                voice_settings = settings,
            };

        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1/text-to-speech/{Uri.EscapeDataString(voice)}");
        req.Headers.Add("xi-api-key", key);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg"));
        req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req, cancellationToken);
        var bytes = await res.Content.ReadAsByteArrayAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = System.Text.Encoding.UTF8.GetString(bytes);
            if (snippet.Contains("voice_clone_not_permitted", StringComparison.OrdinalIgnoreCase)
                || snippet.Contains("Instant voice cloning", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Giọng Instant Clone — gói ElevenLabs hiện tại không cho TTS. Chọn giọng thư viện tiếng Việt miền Bắc, không dùng giọng clone.");
            }
            if (snippet.Length > 300) snippet = snippet[..300] + "…";
            throw new InvalidOperationException($"ElevenLabs TTS failed ({(int)res.StatusCode}): {snippet}");
        }

        if (bytes.Length < 100)
            throw new InvalidOperationException("ElevenLabs trả audio rỗng.");
        return bytes;
    }

    public async Task<IReadOnlyList<ContentSeriesVoiceDto>> ListVoicesAsync(CancellationToken cancellationToken)
    {
        var resolved = await ResolveAsync(cancellationToken);
        if (!resolved.ElevenLabsConfigured || string.IsNullOrWhiteSpace(resolved.ElevenLabsApiKey))
            return [];

        var key = resolved.ElevenLabsApiKey;
        var byId = new Dictionary<string, ContentSeriesVoiceDto>(StringComparer.Ordinal);
        foreach (var row in await ReadAccountVoicesAsync(key, cancellationToken))
            byId[row.VoiceId] = row;
        foreach (var row in await ReadVietnameseLibraryAsync(key, cancellationToken))
        {
            if (byId.TryGetValue(row.VoiceId, out var existing))
            {
                byId[row.VoiceId] = existing with
                {
                    Vietnamese = existing.Vietnamese || row.Vietnamese,
                    PublicOwnerId = existing.PublicOwnerId ?? row.PublicOwnerId,
                    Category = existing.Category ?? row.Category,
                    Gender = existing.Gender ?? row.Gender,
                    Age = existing.Age ?? row.Age,
                    Accent = existing.Accent ?? row.Accent,
                };
                continue;
            }
            byId[row.VoiceId] = row;
        }

        var northern = byId.Values
            .Where(v => !v.Cloned)
            .Where(KeepNorthernVietnamese)
            .OrderByDescending(v => LooksNorthern(v))
            .ThenBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Take(120)
            .ToList();
        if (northern.Count > 0) return northern;
        return byId.Values
            .Where(v => !v.Cloned && v.Vietnamese && !LooksSouthernOrCentral(v))
            .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();
    }

    private async Task<List<ContentSeriesVoiceDto>> ReadAccountVoicesAsync(string key, CancellationToken cancellationToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "v1/voices");
        req.Headers.Add("xi-api-key", key);
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode) return [];
        return ParseVoiceArray(body, library: false);
    }

    private async Task<List<ContentSeriesVoiceDto>> ReadVietnameseLibraryAsync(string key, CancellationToken cancellationToken)
    {
        var rows = new List<ContentSeriesVoiceDto>();
        var queries = new[]
        {
            "language=vi&accent=northern&page_size=100&sort=usage_character_count_1y",
            "language=vi&accent=hanoi&page_size=100&sort=usage_character_count_1y",
            "language=vi&accent=northern&gender=male&page_size=100&sort=usage_character_count_1y",
            "language=vi&accent=northern&age=middle_aged&page_size=100",
            "language=vi&search=Hanoi&page_size=100",
            "language=vi&search=H%C3%A0%20N%E1%BB%99i&page_size=100",
            "language=vi&search=Hanoi%20man&page_size=100",
            "language=vi&search=narrator&page_size=100",
            "language=vi&page_size=100&sort=usage_character_count_1y",
        };
        foreach (var query in queries)
        {
            var pages = query.Contains("accent=", StringComparison.Ordinal) || query.Contains("search=", StringComparison.Ordinal)
                ? 2
                : 3;
            for (var page = 0; page < pages; page++)
            {
                var url = $"v1/shared-voices?{query}&page={page}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Add("xi-api-key", key);
                using var res = await _http.SendAsync(req, cancellationToken);
                var body = await res.Content.ReadAsStringAsync(cancellationToken);
                if (!res.IsSuccessStatusCode) break;
                var chunk = ParseVoiceArray(body, library: true);
                rows.AddRange(chunk);
                var more = false;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    more = doc.RootElement.TryGetProperty("has_more", out var hm) && hm.ValueKind == JsonValueKind.True;
                }
                catch (JsonException)
                {
                    more = false;
                }
                if (!more || chunk.Count == 0) break;
            }
        }
        return rows;
    }

    private static List<ContentSeriesVoiceDto> ParseVoiceArray(string json, bool library)
    {
        var rows = new List<ContentSeriesVoiceDto>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("voices", out var voices) || voices.ValueKind != JsonValueKind.Array)
                return rows;
            foreach (var v in voices.EnumerateArray())
            {
                var id = ReadString(v, "voice_id");
                var name = ReadString(v, "name");
                if (string.IsNullOrWhiteSpace(id)) continue;
                var category = ReadString(v, "category");
                var cloned = string.Equals(category, "cloned", StringComparison.OrdinalIgnoreCase);
                var vietnamese = library || LooksVietnamese(v);
                var owner = ReadString(v, "public_owner_id");
                var gender = FirstNonEmpty(ReadString(v, "gender")) ?? FirstNonEmpty(ReadLabel(v, "gender"));
                var age = FirstNonEmpty(ReadString(v, "age")) ?? FirstNonEmpty(ReadLabel(v, "age"));
                var accent = FirstNonEmpty(ReadString(v, "accent")) ?? FirstNonEmpty(ReadLabel(v, "accent"));
                var label = string.IsNullOrWhiteSpace(name) ? id.Trim() : name.Trim();
                rows.Add(new ContentSeriesVoiceDto(
                    id.Trim(),
                    label,
                    string.IsNullOrWhiteSpace(category) ? null : category,
                    cloned,
                    vietnamese,
                    string.IsNullOrWhiteSpace(owner) ? null : owner,
                    gender,
                    age,
                    accent));
            }
        }
        catch (JsonException)
        {
            return rows;
        }
        return rows;
    }

    private async Task<string> EnsureLibraryVoiceAsync(
        string key,
        string voiceId,
        string? publicOwnerId,
        string? voiceName,
        CancellationToken cancellationToken)
    {
        var owner = FirstNonEmpty(publicOwnerId);
        if (owner is null) return voiceId;
        using var req = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1/voices/add/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(voiceId)}");
        req.Headers.Add("xi-api-key", key);
        req.Content = JsonContent.Create(new { new_name = FirstNonEmpty(voiceName) ?? voiceId });
        using var res = await _http.SendAsync(req, cancellationToken);
        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        if (res.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var added = ReadString(doc.RootElement, "voice_id");
                if (!string.IsNullOrWhiteSpace(added)) return added.Trim();
            }
            catch (JsonException)
            {
                return voiceId;
            }
        }
        return voiceId;
    }

    private static bool LooksVietnamese(JsonElement v)
    {
        if (IsViToken(ReadString(v, "language")) || IsViToken(ReadString(v, "locale")) || IsViToken(ReadString(v, "accent")))
            return true;
        if (v.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in labels.EnumerateObject())
            {
                if (IsViToken(p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : null))
                    return true;
            }
        }
        if (v.TryGetProperty("verified_languages", out var verified) && verified.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in verified.EnumerateArray())
            {
                if (IsViToken(ReadString(item, "language")) || IsViToken(ReadString(item, "locale")))
                    return true;
            }
        }
        return IsViToken(ReadString(v, "name")) || IsViToken(ReadString(v, "description"));
    }

    private static bool IsViToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var s = raw.Trim();
        return s.Equals("vi", StringComparison.OrdinalIgnoreCase)
               || s.Equals("vie", StringComparison.OrdinalIgnoreCase)
               || s.StartsWith("vi-", StringComparison.OrdinalIgnoreCase)
               || s.Contains("vietnam", StringComparison.OrdinalIgnoreCase)
               || s.Contains("tiếng việt", StringComparison.OrdinalIgnoreCase)
               || s.Contains("tieng viet", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? ReadLabel(JsonElement v, string key)
    {
        if (!v.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Object)
            return null;
        return ReadString(labels, key);
    }

    private static bool KeepNorthernVietnamese(ContentSeriesVoiceDto v) =>
        v.Vietnamese && LooksNorthern(v) && !LooksSouthernOrCentral(v);

    private static bool LooksNorthern(ContentSeriesVoiceDto v) =>
        LooksNorthernLabel(FoldVoice(v));

    private static bool LooksNorthernLabel(string? raw) =>
        ContainsAny(Fold(raw), "northern", "north viet", "hanoi", "ha noi", "ha-noi", "mien bac", "giong bac", "bac ky", "hai phong", "nam dinh", "thai nguyen");

    private static bool LooksSouthernOrCentral(ContentSeriesVoiceDto v) =>
        ContainsAny(
            FoldVoice(v),
            "southern",
            "south viet",
            "south",
            "saigon",
            "sai gon",
            "ho chi minh",
            "hcm",
            "mien nam",
            "nam ky",
            "giong nam",
            "mekong",
            "can tho",
            "vung tau",
            "central",
            "mien trung",
            "hue",
            "da nang",
            "nha trang");

    private static string FoldVoice(ContentSeriesVoiceDto v) =>
        Fold($"{v.Accent} {v.Name} {v.Gender} {v.Age}");

    private static bool ContainsAny(string hay, params string[] needles) =>
        needles.Any(n => hay.Contains(n, StringComparison.Ordinal));

    private static string Fold(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var n = raw.Replace('đ', 'd').Replace('Đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(n.Length);
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static double Clamp01(double? value, double fallback)
    {
        if (value is null || double.IsNaN(value.Value)) return fallback;
        if (value.Value < 0) return 0;
        if (value.Value > 1) return 1;
        return value.Value;
    }

    private static string? FirstNonEmpty(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    /// <summary>Drop English v3 audio tags — with language_code=vi ElevenLabs reads them aloud.</summary>
    internal static string StripSpokenDirection(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return "";
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\[[^\]]+\]\s*", "");
        return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
    }
}
