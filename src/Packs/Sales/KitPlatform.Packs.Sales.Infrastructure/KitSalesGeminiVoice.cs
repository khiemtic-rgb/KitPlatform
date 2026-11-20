using System.Net.Http.Json;
using System.Text.Json;
using KitPlatform.Packs.Sales;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.Sales.Infrastructure;

/// <summary>
/// Gemini restyles KIT Sales copy. Facts stay locked — no new features, prices, or promises.
/// One configured model, no fallback chain; every call passes the weekly budget gate first.
/// </summary>
internal sealed class KitSalesGeminiVoice
{
    private const string ApiBase = "https://generativelanguage.googleapis.com/v1beta";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly IKitSalesAiGate _gate;
    private readonly ILogger<KitSalesGeminiVoice> _log;

    public KitSalesGeminiVoice(
        HttpClient http,
        IKitSalesAiGate gate,
        ILogger<KitSalesGeminiVoice> log)
    {
        _http = http;
        _gate = gate;
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

        try
        {
            var json = await GenerateAsync(
                "inbound_restyle",
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
            var json = await GenerateAsync("outbound_restyle", system, user, cancellationToken);
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
        catch (KitSalesAiDeniedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "KIT Sales Gemini outbound restyle failed");
            throw new InvalidOperationException("Gemini không viết lại được. Thử lại sau.", ex);
        }
    }

    private async Task<string> GenerateAsync(
        string purpose,
        string system,
        string user,
        CancellationToken cancellationToken)
    {
        var ticket = await _gate.AcquireAsync(purpose, SalesAiBudget.Reserve(system, user), cancellationToken);
        if (!ticket.Granted)
            throw new KitSalesAiDeniedException(ticket.DenyReason ?? "denied");

        var usage = new KitSalesAiUsage(false, null, null, null, "not_completed");
        try
        {
            var (text, reported) = await CallAsync(ticket.ApiKey!, ticket.Model, system, user, cancellationToken);
            usage = reported;
            return text;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "KIT Sales Gemini model {Model} failed; no fallback model is tried", ticket.Model);
            usage = usage with { Ok = false, Error = ex.GetType().Name };
            throw;
        }
        finally
        {
            try
            {
                await _gate.SettleAsync(ticket, usage, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "KIT Sales AI usage settle failed; reserve stays charged");
            }
        }
    }

    private async Task<(string Text, KitSalesAiUsage Usage)> CallAsync(
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
        return (text, ReadUsage(doc.RootElement));
    }

    private static KitSalesAiUsage ReadUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usageMetadata", out var meta) || meta.ValueKind != JsonValueKind.Object)
            return new KitSalesAiUsage(true, null, null, null, null);
        return new KitSalesAiUsage(
            true,
            Count(meta, "promptTokenCount"),
            Count(meta, "candidatesTokenCount"),
            Count(meta, "totalTokenCount"),
            null);
    }

    private static long? Count(JsonElement meta, string name) =>
        meta.TryGetProperty(name, out var value) && value.TryGetInt64(out var n) ? n : null;

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

internal sealed class KitSalesAiDeniedException(string reason)
    : InvalidOperationException(reason switch
    {
        "no_key" => "Chưa có key Gemini. Vào KIT Sales → Cài đặt, dán Gemini API key.",
        "budget_zero" => "AI KIT Sales đang tắt (trần token tuần = 0).",
        "budget_exhausted" => "Đã hết trần token AI tuần này.",
        _ => "AI KIT Sales bị từ chối.",
    })
{
    public string Reason { get; } = reason;
}

internal sealed record KitSalesOutboundVoice(
    string Hook,
    string Outreach,
    string Question,
    string Draft,
    bool GeminiUsed);
