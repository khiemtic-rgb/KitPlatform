using System.Net.Http.Json;
using System.Text.Json;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesFacebookMessengerClient
{
    private const string Graph = "https://graph.facebook.com/v21.0/me/messages";
    private readonly IHttpClientFactory _http;
    private readonly KitSalesFacebookSettingsService _settings;

    public KitSalesFacebookMessengerClient(IHttpClientFactory http, KitSalesFacebookSettingsService settings)
    {
        _http = http;
        _settings = settings;
    }

    public async Task<bool> IsSendReadyAsync(CancellationToken cancellationToken)
    {
        var options = await _settings.GetEffectiveAsync(cancellationToken);
        return options.Enabled && !string.IsNullOrWhiteSpace(options.PageAccessToken);
    }

    public async Task SendTextAsync(string recipientPsid, string text, CancellationToken cancellationToken)
    {
        var options = await _settings.GetEffectiveAsync(cancellationToken);
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.PageAccessToken))
            throw new InvalidOperationException("Chưa cấu hình Page Access Token (Cài đặt KIT Sales).");

        var client = _http.CreateClient("kit-sales-facebook");
        using var req = new HttpRequestMessage(HttpMethod.Post, Graph);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + options.PageAccessToken);
        req.Content = JsonContent.Create(new
        {
            recipient = new { id = recipientPsid },
            messaging_type = "RESPONSE",
            message = new { text },
        });

        using var res = await client.SendAsync(req, cancellationToken);
        if (res.IsSuccessStatusCode)
            return;

        var body = await res.Content.ReadAsStringAsync(cancellationToken);
        string? err = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var msg))
                err = msg.GetString();
        }
        catch (JsonException)
        {
            // keep raw body
        }

        throw new InvalidOperationException(err ?? $"Messenger trả {(int)res.StatusCode}.");
    }
}
