using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaCore.Application.Integration;

namespace PharmaCore.Infrastructure.Integration;

internal sealed class HttpIntegrationOutboxPublisher : IIntegrationOutboxPublisher
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IntegrationOutboxOptions _options;
    private readonly ILogger<HttpIntegrationOutboxPublisher> _logger;

    public HttpIntegrationOutboxPublisher(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOutboxOptions> options,
        ILogger<HttpIntegrationOutboxPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task PublishAsync(string payloadJson, CancellationToken cancellationToken = default)
    {
        var webhookUrl = _options.WebhookUrl?.Trim();
        if (string.IsNullOrEmpty(webhookUrl))
        {
            _logger.LogInformation("Outbox event (log-only): {Payload}", payloadJson);
            return;
        }

        var client = _httpClientFactory.CreateClient(IntegrationOutboxHttpClient.Name);
        using var content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        var secret = _options.WebhookSecret?.Trim();
        if (!string.IsNullOrEmpty(secret))
        {
            var signature = IntegrationWebhookSignature.Sign(secret, payloadJson);
            content.Headers.Add(IntegrationWebhookSignature.HeaderName, signature);
        }

        using var response = await client.PostAsync(webhookUrl, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Outbox webhook returned {(int)response.StatusCode}: {Truncate(body, 500)}");
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}

internal static class IntegrationOutboxHttpClient
{
    public const string Name = "IntegrationOutbox";
}
