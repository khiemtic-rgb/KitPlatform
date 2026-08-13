using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

/// <summary>Caches OAuth-style access token from POST /auth/login (API CSDL dược v2).</summary>
internal sealed class CsdlDuocTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptionsMonitor<NationalDrugCatalogSettings> _options;
    private readonly ILogger<CsdlDuocTokenProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public CsdlDuocTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptionsMonitor<NationalDrugCatalogSettings> options,
        ILogger<CsdlDuocTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow < _expiresAt)
            return _accessToken!;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_accessToken) && DateTimeOffset.UtcNow < _expiresAt)
                return _accessToken!;

            var settings = _options.CurrentValue;
            var username = settings.Username?.Trim();
            var password = settings.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "NationalDrugCatalog:Username/Password chưa cấu hình (sandbox/live).");
            }

            var passwordField = settings.PasswordIsBase64
                ? password.Trim()
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(password));

            var client = _httpClientFactory.CreateClient("csdl-duoc");
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = username,
                ["password"] = passwordField,
            });

            using var response = await client.PostAsync("auth/login", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CSDL dược login failed: {Status} {Body}",
                    (int)response.StatusCode,
                    Truncate(body, 300));
                throw new InvalidOperationException(
                    $"Đăng nhập CSDL dược thất bại HTTP {(int)response.StatusCode}.");
            }

            var token = System.Text.Json.JsonSerializer.Deserialize<CsdlDuocTokenResponse>(
                body,
                JsonOptions());
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
                throw new InvalidOperationException("CSDL dược login không trả access_token.");

            var skew = TimeSpan.FromMinutes(2);
            var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn));
            _accessToken = token.AccessToken;
            _expiresAt = DateTimeOffset.UtcNow.Add(lifetime) - skew;
            _logger.LogInformation(
                "CSDL dược token ok — expires_in={ExpiresIn}s mode={Mode}",
                token.ExpiresIn,
                NationalDrugCatalogSettings.NormalizeMode(settings.Mode));
            return _accessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate()
    {
        _accessToken = null;
        _expiresAt = DateTimeOffset.MinValue;
    }

    public async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken cancellationToken = default)
    {
        var settings = _options.CurrentValue;
        var client = _httpClientFactory.CreateClient("csdl-duoc");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken));
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _ = settings; // base address set at factory
        return client;
    }

    private static System.Text.Json.JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= max ? value
        : value[..max] + "…";
}
