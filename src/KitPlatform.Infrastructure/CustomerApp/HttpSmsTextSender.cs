using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class HttpSmsTextSender : ISmsTextSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _http;
    private readonly CustomerAppSmsSettings _settings;
    private readonly ILogger<HttpSmsTextSender> _logger;

    public HttpSmsTextSender(
        HttpClient http,
        IOptions<CustomerAppSmsSettings> settings,
        ILogger<HttpSmsTextSender> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendTextAsync(
        string phone,
        string tenantCode,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.HttpUrl))
            throw new InvalidOperationException("CustomerAppSms:HttpUrl chưa cấu hình.");

        var json = JsonSerializer.Serialize(new
        {
            phone,
            tenantCode,
            message,
        }, JsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.HttpUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
            Version = new Version(1, 1),
        };
        request.Headers.ExpectContinue = false;

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(_settings.ApiKeyHeader, _settings.ApiKey);
        }

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "SMS text gateway HTTP {Status} for {Phone}: {Body}",
                (int)response.StatusCode,
                phone,
                body);
            throw new InvalidOperationException("Không gửi được SMS.");
        }

        _logger.LogInformation("SMS text sent via gateway to {Phone} (tenant {Tenant})", phone, tenantCode);
    }
}
