using System.Text;
using KitPlatform.Api.Controllers.Integration;
using KitPlatform.Application.Integration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class CdpWebhookControllerTests
{
    [Fact]
    public async Task Rejects_when_secret_is_missing()
    {
        var result = await Receive("   ", "{\"event\":1}", null);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Rejects_when_signature_does_not_match()
    {
        var result = await Receive("test-secret", "{\"event\":1}", "sha256=00");

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Accepts_a_matching_signature()
    {
        const string secret = "test-secret";
        const string body = "{\"event\":1}";
        var result = await Receive(secret, body, IntegrationWebhookSignature.Sign(secret, body));

        Assert.IsType<OkObjectResult>(result);
    }

    private static async Task<IActionResult> Receive(string? secret, string body, string? signature)
    {
        var controller = new CdpWebhookController(
            Options.Create(new IntegrationOutboxOptions { WebhookSecret = secret }),
            NullLogger<CdpWebhookController>.Instance);
        var http = new DefaultHttpContext();
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        if (signature is not null)
            http.Request.Headers[IntegrationWebhookSignature.HeaderName] = signature;
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        return await controller.Receive(CancellationToken.None);
    }
}
