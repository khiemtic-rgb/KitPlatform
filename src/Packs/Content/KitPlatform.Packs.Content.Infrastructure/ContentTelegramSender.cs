using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Gọi Telegram Bot API. Token nằm trong đường dẫn URL nên: không log URL, không đưa exception thô ra ngoài,
/// named client "content-telegram" đã tắt logger của HttpClientFactory (xem DI).
/// </summary>
internal sealed class ContentTelegramSender
{
    public const string ClientName = "content-telegram";

    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ContentTelegramOptions> _options;
    private readonly ContentTelegramStore _store;

    public ContentTelegramSender(
        IHttpClientFactory http,
        IConfiguration configuration,
        IOptions<ContentTelegramOptions> options,
        ContentTelegramStore store)
    {
        _http = http;
        _configuration = configuration;
        _options = options;
        _store = store;
    }

    /// <summary>Thứ tự: storedSecret trong org_settings → biến môi trường tên botTokenSecretRef → Content:Telegram:BotToken.</summary>
    public async Task<string?> ResolveTokenAsync(CancellationToken ct)
    {
        var cfg = await _store.GetBotConfigAsync(ct);
        return ResolveToken(cfg);
    }

    public string? ResolveToken(ContentTelegramBotConfig cfg)
    {
        if (ContentTelegramDefaults.IsValidToken(cfg.StoredSecret)) return cfg.StoredSecret;

        if (ContentTelegramDefaults.IsValidSecretRef(cfg.SecretRef))
        {
            var env = _configuration[cfg.SecretRef]?.Trim();
            if (ContentTelegramDefaults.IsValidToken(env)) return env;
        }

        var opt = _options.Value.BotToken?.Trim();
        return ContentTelegramDefaults.IsValidToken(opt) ? opt : null;
    }

    public Task<ContentTelegramSendResult> SendHtmlAsync(string token, string chatId, string html, bool silent, CancellationToken ct) =>
        PostAsync(token, "sendMessage", new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = ContentTelegramText.CapHtml(html),
            ["parse_mode"] = "HTML",
            ["disable_web_page_preview"] = false,
            ["disable_notification"] = silent,
        }, ct);

    public Task<ContentTelegramSendResult> SendPlainAsync(string token, string chatId, string text, CancellationToken ct) =>
        PostAsync(token, "sendMessage", new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = text,
        }, ct);

    public async Task<ContentTelegramSendResult> GetMeAsync(string token, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient(ClientName);
            using var res = await client.GetAsync(Path(token, "getMe"), ct);
            return await ReadAsync(res, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ContentTelegramSendResult.Network("Lỗi mạng (" + ex.GetType().Name + ").");
        }
    }

    private async Task<ContentTelegramSendResult> PostAsync(
        string token, string method, Dictionary<string, object?> body, CancellationToken ct)
    {
        try
        {
            var client = _http.CreateClient(ClientName);
            using var res = await client.PostAsJsonAsync(Path(token, method), body, ct);
            return await ReadAsync(res, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Không dùng ex.Message: có thể lẫn URL chứa token.
            return ContentTelegramSendResult.Network("Lỗi mạng (" + ex.GetType().Name + ").");
        }
    }

    private static string Path(string token, string method)
    {
        if (!ContentTelegramDefaults.IsValidToken(token))
            throw new InvalidOperationException("Token bot không đúng dạng.");
        return $"bot{token}/{method}";
    }

    private static async Task<ContentTelegramSendResult> ReadAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var code = (int)res.StatusCode;
        string? description = null;
        int? retryAfter = null;
        var ok = false;
        try
        {
            var text = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            if (root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String)
                description = ContentTelegramText.Cut(ContentTelegramText.Redact(d.GetString()), 200);
            if (root.TryGetProperty("parameters", out var p) && p.ValueKind == JsonValueKind.Object
                && p.TryGetProperty("retry_after", out var ra) && ra.TryGetInt32(out var sec))
                retryAfter = sec;
        }
        catch (JsonException)
        {
        }

        if (res.IsSuccessStatusCode && ok) return ContentTelegramSendResult.Success();
        return new ContentTelegramSendResult(false, code, retryAfter, description ?? $"HTTP {code}");
    }
}
