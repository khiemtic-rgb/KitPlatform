using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

internal sealed class LocalOsRewriteService : ILocalOsRewriteService
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly string[] Models = ["gemini-flash-latest", "gemini-2.0-flash", "gemini-2.5-flash"];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly IDbConnectionFactory _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LocalOsRewriteService> _logger;

    public LocalOsRewriteService(
        HttpClient http,
        IDbConnectionFactory db,
        IConfiguration configuration,
        ILogger<LocalOsRewriteService> logger)
    {
        _http = http;
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<RewriteLocalListingResult> RewriteAsync(
        RewriteLocalListingRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = (request.Text ?? "").Trim();
        if (text.Length < 8)
            throw new InvalidOperationException("Dán nội dung bài trước, rồi bấm viết lại.");

        var kind = request.Kind is "event" or "room" ? request.Kind : "job";
        var key = await ResolveKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                "Chưa thấy key Gemini của Nội dung. Vào Nội dung → Cấu hình AI, hoặc đặt GEMINI_API_KEY.");

        Exception? last = null;
        foreach (var model in Models)
        {
            try
            {
                var raw = await GenerateJsonAsync(key, model, kind, text, cancellationToken);
                var parsed = JsonSerializer.Deserialize<AiDraft>(raw, JsonOpts)
                    ?? throw new InvalidOperationException("AI trả JSON rỗng.");
                return Guard(kind, text, parsed);
            }
            catch (Exception ex)
            {
                last = ex;
                _logger.LogWarning(ex, "Local OS rewrite model {Model} failed", model);
            }
        }

        throw last ?? new InvalidOperationException("AI không viết lại được. Thử lại sau.");
    }

    private async Task<string?> ResolveKeyAsync(CancellationToken cancellationToken)
    {
        var fromContent = await ReadContentGeminiKeyAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(fromContent))
            return fromContent;

        foreach (var name in new[]
        {
            "Content:GeminiApiKey",
            "GEMINI_API_KEY",
            "GOOGLE_API_KEY",
            "GOOGLE_GENERATIVE_AI_API_KEY",
        })
        {
            var v = name.Contains(':', StringComparison.Ordinal)
                ? _configuration[name]
                : _configuration[name] ?? Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        return FirstNonEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
            ?? FirstNonEmpty(Environment.GetEnvironmentVariable("GOOGLE_API_KEY"));
    }

    private async Task<string?> ReadContentGeminiKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var json = await conn.ExecuteScalarAsync<string?>(
                new CommandDefinition(
                    """
                    SELECT COALESCE(ai_config_json, '{}'::jsonb)::text
                    FROM pack_content.org_settings
                    ORDER BY updated_at DESC
                    LIMIT 1
                    """,
                    cancellationToken: cancellationToken));
            if (string.IsNullOrWhiteSpace(json))
                return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("geminiApiKey", out var keyEl))
            {
                var stored = keyEl.GetString();
                if (!string.IsNullOrWhiteSpace(stored))
                    return stored.Trim();
            }

            var secretRef = "GEMINI_API_KEY";
            if (root.TryGetProperty("geminiApiKeySecretRef", out var refEl)
                && !string.IsNullOrWhiteSpace(refEl.GetString()))
                secretRef = refEl.GetString()!.Trim();

            return FirstNonEmpty(_configuration[secretRef])
                ?? FirstNonEmpty(Environment.GetEnvironmentVariable(secretRef))
                ?? FirstNonEmpty(_configuration[$"Content:Secrets:{secretRef}"]);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Local OS rewrite: no Content Gemini config");
            return null;
        }
    }

    private async Task<string> GenerateJsonAsync(
        string apiKey,
        string model,
        string kind,
        string text,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            systemInstruction = new
            {
                parts = new[] { new { text = SystemPrompt(kind) } },
            },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text } } },
            },
            generationConfig = new
            {
                temperature = 0.15,
                responseMimeType = "application/json",
            },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/models/{model}:generateContent");
        req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        req.Content = JsonContent.Create(body);

        using var res = await _http.SendAsync(req, cancellationToken);
        var raw = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = raw.Length > 280 ? raw[..280] : raw;
            throw new InvalidOperationException($"Gemini {model} ({(int)res.StatusCode}): {snippet}");
        }

        using var doc = JsonDocument.Parse(raw);
        var textOut = ExtractText(doc.RootElement);
        if (string.IsNullOrWhiteSpace(textOut))
            throw new InvalidOperationException("Gemini returned empty text");
        return textOut;
    }

    private static string SystemPrompt(string kind) =>
        $"""
        Bạn viết lại tin rao vặt tiếng Việt cho trang Thái Nguyên Life (admin duyệt tay).
        Loại tin: {kind}.

        Bắt buộc:
        - Viết lại thành văn rõ, trung tính, như tin đã biên tập — không còn giọng chat nhóm.
        - Mở viết tắt: nv→nhân viên, sv→sinh viên, ko/kg→không, dc/đc→được, vs→với, lh→liên hệ, cf→cà phê, pt/parttime→bán thời gian, ft/fulltime→toàn thời gian, lcb→lương cơ bản, ib/inbox→nhắn tin, mn/ae→bỏ (hô hào).
        - Xóa cảm thán, emoji, in hoa la liệt, "ơi/nhé/nha/ạ/luôn/siêu/hot/gấp!!!/hehe".
        - Không bịa lương, số điện thoại, địa chỉ, giờ, quyền lợi, số lượng. Không có trong bài gốc thì để null / bỏ.
        - Giữ nguyên số liệu đã có (lương, SĐT, giờ, chỗ).
        - Phòng (room): không ghi giá trong body; salary để null.
        - Việc (job): body gồm tiêu đề rồi các dòng Thu nhập / Thời gian / Địa điểm / Yêu cầu / Liên hệ nếu có.
        - Tiêu đề ≤ 120 ký tự, không chấm than.

        Trả JSON đúng schema: title, body, place, phone, salary (null nếu không có trong bài gốc).
        """;

    private static RewriteLocalListingResult Guard(string kind, string source, AiDraft draft)
    {
        var title = Clip(OneLine(draft.Title), 120);
        if (string.IsNullOrWhiteSpace(title))
            title = LocalOsTextExtract.GuessTitle(source);

        var body = (draft.Body ?? "").Trim();
        if (body.Length > 2000)
            body = body[..2000].TrimEnd();
        if (body.Length < 8)
            body = title;

        var sourcePhones = LocalOsTextExtract.PhonesIn(source);
        var outPhones = LocalOsTextExtract.PhonesIn($"{draft.Phone} {body}");
        var phone = outPhones.FirstOrDefault(sourcePhones.Contains) ?? sourcePhones.FirstOrDefault();
        if (phone is not null && !body.Contains(phone, StringComparison.Ordinal))
            body = $"{body.TrimEnd()}\nLiên hệ: {phone}";

        var salary = kind == "room" ? null : KeepPay(source, draft.Salary);
        var place = OneLine(draft.Place);
        if (place is { Length: > 80 })
            place = place[..80].TrimEnd();

        return new RewriteLocalListingResult(title, body, place, phone, salary, "ai", null);
    }

    private static string? KeepPay(string source, string? proposed)
    {
        var fromSource = LocalOsTextExtract.GuessSalary(source);
        var clean = OneLine(proposed);
        if (string.IsNullOrWhiteSpace(clean))
            return fromSource;
        var sourceDigits = DigitBlob(source);
        var outDigits = DigitBlob(clean);
        if (outDigits.Length > 0 && sourceDigits.Length > 0 && !sourceDigits.Contains(outDigits) && !outDigits.Contains(sourceDigits[..Math.Min(3, sourceDigits.Length)]))
            return fromSource;
        return clean;
    }

    private static string DigitBlob(string s) => Regex.Replace(s, @"\D", "");

    private static string OneLine(string? s) =>
        Regex.Replace((s ?? "").Replace('\n', ' '), @"\s+", " ").Trim();

    private static string Clip(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd();

    private static string? FirstNonEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? ExtractText(JsonElement data)
    {
        if (!data.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            return null;
        if (!candidates[0].TryGetProperty("content", out var content))
            return null;
        if (!content.TryGetProperty("parts", out var parts))
            return null;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                var s = text.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
        }

        return null;
    }

    private sealed class AiDraft
    {
        public string? Title { get; set; }
        public string? Body { get; set; }
        public string? Place { get; set; }
        public string? Phone { get; set; }
        public string? Salary { get; set; }
    }
}
