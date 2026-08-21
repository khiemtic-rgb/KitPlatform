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
        $$"""
        Bạn là biên tập viên tin rao vặt tiếng Việt cho Thái Nguyên Life. Loại tin: {{kind}}.
        Tách bài chat nhóm thành tin đã biên tập + điền từng trường. Không viết văn lan man.

        Bắt buộc:
        - Giọng trung tính, rõ, như tin tuyển dụng đã sửa. Không giọng chat.
        - Mở viết tắt: nv→nhân viên, sv→sinh viên, ko/kg→không, dc/đc→được, vs→với, lh→liên hệ, cf→cà phê, pt/parttime→bán thời gian, ft/fulltime→toàn thời gian, lcb→lương cơ bản, ib/inbox→nhắn tin. Bỏ ae/mn/ơi/nhé/ạ/hehe/emoji/gấp!!!
        - Không bịa. Số điện thoại, lương, giờ, địa chỉ, tên người không có trong bài gốc thì null.
        - Giữ nguyên số liệu (SĐT, lương, giờ). Chuẩn hóa SĐT thành 0xxxxxxxxx.
        - title ≤ 70 ký tự, dạng "Tuyển … — tên quán/công ty" hoặc "Cho thuê phòng …". Không chấm than.
        - place chỉ địa chỉ ngắn (đường / phường / mốc), ≤ 60 ký tự.
        - contactName: anh/chị/cô + tên, hoặc họ tên nếu bài ghi. Không lấy tên quán.
        - workingTime: ca/giờ (vd. 17h–22h, T2–CN).
        - requirements: tuổi, giới tính, kinh nghiệm — một câu.
        - organizationName: tên quán / nhà hàng / công ty.
        - employmentType: part_time | full_time | internship | weekend | null.
        - body: 1 câu mở (không lặp nguyên title), rồi các dòng nhãn có dữ liệu: Thu nhập / Thời gian / Địa điểm / Yêu cầu / Liên hệ. Liên hệ ghi "tên — SĐT" nếu có cả hai. Không dán lại bài gốc.
        - Phòng (room): salary null; không ghi giá trong body.

        Ví dụ việc:
        Gốc: "Ae ơi quán Vert cần nv pt pha chế 18-22k/h đối diện 341 PBC lh chị Hoa 0766408636"
        JSON: {"title":"Tuyển barista và nhân viên phục vụ — Vert","body":"Quán Vert tuyển barista và nhân viên phục vụ, bán thời gian.\n\nThu nhập: 18.000–22.000đ/giờ.\nĐịa điểm: đối diện 341 Phan Bội Châu, Thái Nguyên.\nLiên hệ: chị Hoa — 0766408636.","place":"Đối diện 341 Phan Bội Châu, Thái Nguyên","phone":"0766408636","salary":"18.000–22.000đ/giờ","contactName":"chị Hoa","workingTime":null,"requirements":null,"organizationName":"Vert","employmentType":"part_time"}

        Trả JSON đúng schema: title, body, place, phone, salary, contactName, workingTime, requirements, organizationName, employmentType.
        """;

    private static RewriteLocalListingResult Guard(string kind, string source, AiDraft draft)
    {
        var title = Clip(OneLine(draft.Title), 80);
        if (string.IsNullOrWhiteSpace(title) || title.Length > 72)
            title = LocalOsTextExtract.GuessShortTitle(kind, source);

        var sourcePhones = LocalOsTextExtract.PhonesIn(source);
        var outPhones = LocalOsTextExtract.PhonesIn($"{draft.Phone} {draft.Body}");
        var phone = outPhones.FirstOrDefault(sourcePhones.Contains) ?? sourcePhones.FirstOrDefault();

        var salary = kind == "room" ? null : KeepPay(source, draft.Salary);
        var place = LocalOsTextExtract.KeepIfFromSource(source, draft.Place)
            ?? LocalOsTextExtract.GuessStreetPlace(source)
            ?? LocalOsTextExtract.GuessPlace(source);
        var contactName = LocalOsTextExtract.KeepIfFromSource(source, draft.ContactName)
            ?? LocalOsTextExtract.GuessContactName(source);
        var workingTime = LocalOsTextExtract.KeepIfFromSource(source, draft.WorkingTime)
            ?? LocalOsTextExtract.GuessWorkingTime(source);
        var requirements = LocalOsTextExtract.KeepIfFromSource(source, draft.Requirements)
            ?? LocalOsTextExtract.GuessRequirements(source);
        var organization = LocalOsTextExtract.KeepIfFromSource(source, draft.OrganizationName)
            ?? LocalOsTextExtract.GuessOrganizationName(source);
        var employment = NormalizeEmployment(draft.EmploymentType)
            ?? LocalOsTextExtract.GuessEmploymentType(source);

        var body = (draft.Body ?? "").Trim();
        if (body.Length > 2000)
            body = body[..2000].TrimEnd();
        var dump = LocalOsTextExtract.LooksLikeChatDump(title, place, body, source)
            || body.Length < 8
            || !HasLabeledLines(body, kind);
        if (dump)
        {
            if (string.IsNullOrWhiteSpace(title) || LocalOsTextExtract.LooksLikeChatDump(title, place, body, source))
                title = LocalOsTextExtract.GuessShortTitle(kind, source);
            body = LocalOsTextExtract.StructuredBody(
                kind, title, organization, place, phone, contactName, salary, workingTime, requirements);
        }
        else
        {
            if (kind == "room")
                body = StripRoomPrices(body);
            var contact = LocalOsTextExtract.FormatContact(contactName, phone);
            if (contact is not null && (phone is null || !body.Contains(phone, StringComparison.Ordinal))
                && !body.Contains(contact, StringComparison.OrdinalIgnoreCase))
                body = $"{body.TrimEnd()}\nLiên hệ: {contact}";
        }

        return new RewriteLocalListingResult(
            title, body, place, phone, salary, contactName, workingTime, requirements,
            organization, employment, "ai", null);
    }

    private static bool HasLabeledLines(string body, string kind)
    {
        if (kind == "room")
            return Regex.IsMatch(body, @"địa chỉ\s*:", RegexOptions.IgnoreCase)
                || Regex.IsMatch(body, @"diện tích\s*:", RegexOptions.IgnoreCase);
        return Regex.IsMatch(body, @"thu nhập\s*:|thời gian\s*:|địa điểm\s*:|liên hệ\s*:", RegexOptions.IgnoreCase);
    }

    private static string? NormalizeEmployment(string? raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return s is "part_time" or "full_time" or "internship" or "weekend" ? s : null;
    }

    private static string StripRoomPrices(string body)
    {
        var kept = body.Split('\n')
            .Where(l => !Regex.IsMatch(l, @"\d+[.,]?\d*\s*(?:triệu|tr)\b", RegexOptions.IgnoreCase)
                        || Regex.IsMatch(l, @"m2|m²", RegexOptions.IgnoreCase));
        var next = string.Join('\n', kept).Trim();
        return next.Length >= 8 ? next : body;
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
        public string? ContactName { get; set; }
        public string? WorkingTime { get; set; }
        public string? Requirements { get; set; }
        public string? OrganizationName { get; set; }
        public string? EmploymentType { get; set; }
    }
}
