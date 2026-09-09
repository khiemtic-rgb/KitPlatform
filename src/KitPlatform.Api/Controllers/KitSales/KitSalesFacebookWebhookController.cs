using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Packs.Sales;
using KitPlatform.Packs.Sales.Infrastructure;

namespace KitPlatform.Api.Controllers.KitSales;

[ApiController]
[AllowAnonymous]
[Route("api/kit-sales/facebook/webhook")]
public sealed class KitSalesFacebookWebhookController : ControllerBase
{
    private readonly IKitSalesChatService _chat;
    private readonly KitSalesFacebookSettingsService _settings;
    private readonly ILogger<KitSalesFacebookWebhookController> _log;

    public KitSalesFacebookWebhookController(
        IKitSalesChatService chat,
        KitSalesFacebookSettingsService settings,
        ILogger<KitSalesFacebookWebhookController> log)
    {
        _chat = chat;
        _settings = settings;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        CancellationToken cancellationToken)
    {
        var options = await _settings.GetEffectiveAsync(cancellationToken);
        if (mode == "subscribe"
            && !string.IsNullOrWhiteSpace(options.VerifyToken)
            && token == options.VerifyToken
            && !string.IsNullOrWhiteSpace(challenge))
        {
            return Content(challenge, "text/plain");
        }

        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var raw = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var options = await _settings.GetEffectiveAsync(cancellationToken);
        if (!SignatureOk(raw, options))
            return Unauthorized();

        if (!options.Enabled)
            return Ok();

        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
                return Ok();

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("messaging", out var messaging))
                    continue;
                foreach (var item in messaging.EnumerateArray())
                    await HandleItemAsync(item, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "KIT Sales Facebook webhook parse failed");
        }

        return Ok();
    }

    private async Task HandleItemAsync(JsonElement item, CancellationToken cancellationToken)
    {
        if (item.TryGetProperty("message", out var message)
            && message.TryGetProperty("is_echo", out var echo)
            && echo.ValueKind == JsonValueKind.True)
            return;

        if (!item.TryGetProperty("sender", out var sender)
            || !sender.TryGetProperty("id", out var senderId))
            return;

        var psid = senderId.GetString();
        if (string.IsNullOrWhiteSpace(psid))
            return;

        var text = message.ValueKind == JsonValueKind.Object
            && message.TryGetProperty("text", out var textEl)
                ? textEl.GetString()
                : null;
        if (string.IsNullOrWhiteSpace(text))
            return;

        await _chat.HandleFacebookInboundAsync(psid, text, cancellationToken);
    }

    private bool SignatureOk(string raw, KitSalesFacebookOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AppSecret))
            return !options.Enabled;

        var header = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            return false;

        var expected = header["sha256=".Length..];
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.AppSecret), Encoding.UTF8.GetBytes(raw));
        var actual = Convert.ToHexString(hash);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(actual.ToLowerInvariant()),
            Encoding.ASCII.GetBytes(expected.ToLowerInvariant()));
    }
}
