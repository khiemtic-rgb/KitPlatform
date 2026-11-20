using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Integration;

namespace KitPlatform.Api.Controllers.Integration;

/// <summary>Endpoint nhận webhook CDP (dev: trỏ IntegrationOutbox:WebhookUrl về đây).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/integration/cdp-webhook")]
public sealed class CdpWebhookController : ControllerBase
{
    private readonly IntegrationOutboxOptions _options;
    private readonly ILogger<CdpWebhookController> _logger;

    public CdpWebhookController(IOptions<IntegrationOutboxOptions> options, ILogger<CdpWebhookController> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var secret = _options.WebhookSecret?.Trim();
        if (string.IsNullOrEmpty(secret))
            return Unauthorized(new { message = "Webhook secret is not configured." });

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { message = "Empty body." });

        var header = Request.Headers[IntegrationWebhookSignature.HeaderName].ToString();
        if (!IntegrationWebhookSignature.TryValidate(secret, body, header))
            return Unauthorized(new { message = "Invalid webhook signature." });

        _logger.LogInformation("CDP webhook received ({Length} bytes).", body.Length);
        return Ok(new { received = true });
    }
}
