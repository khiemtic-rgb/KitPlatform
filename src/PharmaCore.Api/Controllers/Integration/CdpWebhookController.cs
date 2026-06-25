using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PharmaCore.Application.Integration;

namespace PharmaCore.Api.Controllers.Integration;

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
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { message = "Empty body." });

        var secret = _options.WebhookSecret?.Trim();
        if (!string.IsNullOrEmpty(secret))
        {
            var header = Request.Headers[IntegrationWebhookSignature.HeaderName].ToString();
            if (!IntegrationWebhookSignature.TryValidate(secret, body, header))
                return Unauthorized(new { message = "Invalid webhook signature." });
        }

        _logger.LogInformation("CDP webhook received ({Length} bytes): {Payload}", body.Length, body);
        return Ok(new { received = true });
    }
}
