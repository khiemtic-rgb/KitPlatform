using System.Net.Http.Json;
using System.Text.Json;
using KitPlatform.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Gemini restyles KIT Sales copy. Facts stay locked — no new features, prices, or promises.
/// </summary>
internal sealed class KitSalesGeminiVoice
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";
    private static readonly string[] FallbackModels = ["gemini-3.6-flash", "gemini-flash-latest", "gemini-2.5-flash"];

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly KitSalesAiSettingsService _ai;
    private readonly ILogger<KitSalesGeminiVoice> _log;

    public KitSalesGeminiVoice(
        HttpClient http,
        KitSalesAiSettingsService ai,
        ILogger<KitSalesGeminiVoice> log)
    {
        _http = http;
        _ai = ai;
        _log = log;
    }

    public async Task<string> RestyleInboundAsync(
        string customerText,
        string lockedReply,
        bool escalate,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(lockedReply))
            return lockedReply;

        var settings = await ResolveAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            return lockedReply;

        try
        {
            var json = await GenerateAsync(
                settings,
                """
                Bạn viết lại tin nhắn Facebook cho nhân viên sales Novixa nói với chủ nhà thuốc Việt Nam.
                Giọng ngoài đời: ngắn, dễ nghe, xưng em, gọi anh/chị. Không văn công ty.
                CHỈ được dùng sự thật trong KHÓA. Không thêm tính năng, giá, cam kết, AI, phần mềm.
                Nếu escalate=true: giữ ý "chưa có trong tài liệu, người sẽ trả lời" — không bịa.
                Trả JSON: {"reply":"..."}
                """,
                $"""
                Tin khách: {customerText}
                escalate: {escalate}
                KHÓA (giữ nguyên ý): {lockedReply}
                """,
                cancellationToken);
            var parsed = JsonSerializer.Deserialize<InboundDraft>(json, JsonOpts);
            var reply = parsed?.Reply?.Trim();
            return string.IsNullOrWhiteSpace(reply) ? lockedReply : reply;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "KIT Sales Gemini inbound restyle failed — using locked reply");
            return lockedReply;
        }
    }

    public async Task<KitSalesOutboundVoice> RestyleOutboundAsync(
        string step,
        NovixaPainDef pain,
        string catalogDraft,
        CancellationToken cancellationToken)
    {
        var settings = await ResolveAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
            throw new InvalidOperationException(
                "Chưa có key Gemini. Vào KIT Sales → Cài đặt, dán Gemini API key.");

        var hookOnly = string.IsNullOrWhiteSpace(step) || step == "hook";
        var system = hookOnly
            ? """
              Bạn viết lại câu tiếp cận Facebook tới chủ nhà thuốc. Giọng ngoài đời, ngắn, như tin nhắn thật.
              Hỏi nỗi đau. CẤM nói giải pháp, Novixa, AI, phần mềm, giá, PHC, Health Check.
              Giữ đúng nỗi đau và ý hỏi. Không đổi chủ đề.
              Trả JSON: {"hook":"...","outreach":"...","question":"..."}
              """
            : """
              Bạn viết lại tin Facebook sales Novixa. Giọng ngoài đời, ngắn, xưng em, gọi anh/chị.
              CHỈ được dùng sự thật trong BẢN GỐC. Giữ nguyên số, giá, URL, tên bước.
              Không thêm tính năng, cam kết, hay lời mời mới.
              Trả JSON: {"draft":"..."}
              """;
        var user = hookOnly
            ? $"""
               Nỗi đau: {pain.Name}
               Mô tả: {pain.Description}
               Hook gốc: {pain.Hook}
               Câu tiếp cận gốc: {pain.Outreach}
               Câu hỏi gốc: {pain.Question}
               """
            : $"""
               Bước: {step}
               Nỗi đau: {pain.Name}
               BẢN GỐC (khóa): {catalogDraft}
               """;

        try
        {
            var json = await GenerateAsync(settings, system, user, cancellationToken);
            var parsed = JsonSerializer.Deserialize<OutboundDraft>(json, JsonOpts);
            if (hookOnly)
            {
                var outreach = First(parsed?.Outreach) ?? pain.Outreach;
                var question = First(parsed?.Question) ?? pain.Question;
                var hook = First(parsed?.Hook) ?? pain.Hook;
                return new KitSalesOutboundVoice(
                    hook, outreach, question, outreach + "\n\n" + question, true);
            }

            var draft = First(parsed?.Draft) ?? catalogDraft;
            return new KitSalesOutboundVoice(pain.Hook, pain.Outreach, pain.Question, draft, true);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "KIT Sales Gemini outbound restyle failed");
            throw new InvalidOperationException("Gemini không viết lại được. Thử lại sau.", ex);
        }
    }

    private async Task<ContentParkGeminiKeyResolver.ResolvedSettings> ResolveAsync(
        CancellationToken cancellationToken)
    {
        var (key, model) = await _ai.GetEffectiveAsync(cancellationToken);
        return new ContentParkGeminiKeyResolver.ResolvedSettings(key, model);
    }

    private async Task<string> GenerateAsync(
        ContentParkGeminiKeyResolver.ResolvedSettings settings,
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var key = settings.ApiKey
            ?? throw new InvalidOperationException(
                "Chưa có key Gemini. Vào KIT Sales → Cài đặt, dán Gemini API key.");

        var models = new List<string>();
        if (!string.IsNullOrWhiteSpace(settings.TextModel))
            models.Add(settings.TextModel.Trim());
        foreach (var model in FallbackModels)
        {
            if (!models.Contains(model, StringComparer.OrdinalIgnoreCase))
                models.Add(model);
        }

        Exception? last = null;
        foreach (var model in models)
        {
            try
            {
                return await CallAsync(key, model, system, user, cancellationToken);
            }
            catch (Exception ex)
            {
                last = ex;
                _log.LogWarning(ex, "KIT Sales Gemini model {Model} failed", model);
            }
        }

        throw last ?? new InvalidOperationException("Gemini không viết lại được.");
    }

    private async Task<string> CallAsync(
        string apiKey,
        string model,
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = system } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = user } } } },
            generationConfig = new { temperature = 0.55, responseMimeType = "application/json" },
        };
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/models/{model}:generateContent");
        req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        req.Content = JsonContent.Create(body);
        using var res = await _http.SendAsync(req, cancellationToken);
        var raw = await res.Content.ReadAsStringAsync(cancellationToken);
        if (!res.IsSuccessStatusCode)
        {
            var snippet = raw.Length > 240 ? raw[..240] : raw;
            throw new InvalidOperationException($"Gemini {model} ({(int)res.StatusCode}): {snippet}");
        }

        using var doc = JsonDocument.Parse(raw);
        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Gemini returned empty text");
        return text;
    }

    private static string? First(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class InboundDraft
    {
        public string? Reply { get; init; }
    }

    private sealed class OutboundDraft
    {
        public string? Hook { get; init; }
        public string? Outreach { get; init; }
        public string? Question { get; init; }
        public string? Draft { get; init; }
    }
}

internal sealed record KitSalesOutboundVoice(
    string Hook,
    string Outreach,
    string Question,
    string Draft,
    bool GeminiUsed);
