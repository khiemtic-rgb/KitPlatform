using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PharmaCore.Application.CustomerApp;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class HttpCustomerOtpSender : ICustomerOtpSender
{
    private readonly HttpClient _http;
    private readonly CustomerAppSmsSettings _settings;
    private readonly ILogger<HttpCustomerOtpSender> _logger;

    public HttpCustomerOtpSender(
        HttpClient http,
        IOptions<CustomerAppSmsSettings> settings,
        ILogger<HttpCustomerOtpSender> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendOtpAsync(
        string phone,
        string tenantCode,
        string code,
        int expireMinutes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.HttpUrl))
            throw new InvalidOperationException("CustomerAppSms:HttpUrl chưa cấu hình.");

        var message = _settings.MessageTemplate
            .Replace("{code}", code, StringComparison.Ordinal)
            .Replace("{minutes}", expireMinutes.ToString(), StringComparison.Ordinal)
            .Replace("{phone}", phone, StringComparison.Ordinal)
            .Replace("{tenant}", tenantCode, StringComparison.Ordinal);

        using var request = new HttpRequestMessage(HttpMethod.Post, _settings.HttpUrl)
        {
            Content = JsonContent.Create(new
            {
                phone,
                tenantCode,
                message,
                code,
                expireMinutes,
            }),
        };

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            request.Headers.TryAddWithoutValidation(
                _settings.ApiKeyHeader,
                _settings.ApiKey);
        }

        var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "SMS gateway HTTP {Status} for {Phone}: {Body}",
                (int)response.StatusCode,
                phone,
                body);
            throw new InvalidOperationException("Không gửi được SMS OTP. Thử lại sau.");
        }

        _logger.LogInformation("OTP SMS sent via gateway to {Phone} (tenant {Tenant})", phone, tenantCode);
    }
}
