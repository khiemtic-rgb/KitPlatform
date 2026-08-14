using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

/// <summary>Caches OAuth-style access tokens per username+baseUrl from POST /auth/login.</summary>
internal sealed class CsdlDuocTokenProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CsdlDuocTokenProvider> _logger;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    public CsdlDuocTokenProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<CsdlDuocTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync(
        CsdlDuocEffectiveCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(credentials);
        if (_cache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow < hit.ExpiresAt)
            return hit.AccessToken;

        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(key, out hit) && DateTimeOffset.UtcNow < hit.ExpiresAt)
                return hit.AccessToken;

            var username = credentials.Username?.Trim();
            var password = credentials.Password;
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Chưa có Username/Password CSDL dược (tenant hoặc platform).");
            }

            var passwordField = credentials.PasswordIsBase64
                ? password.Trim()
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(password));

            var client = _httpClientFactory.CreateClient("csdl-duoc");
            client.BaseAddress = new Uri(credentials.BaseUrl.TrimEnd('/') + "/");

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
                    "CSDL dược login failed ({Source}/{User}): {Status} {Body}",
                    credentials.Source,
                    username,
                    (int)response.StatusCode,
                    Truncate(body, 300));
                throw new InvalidOperationException(
                    $"Đăng nhập CSDL dược thất bại HTTP {(int)response.StatusCode}.");
            }

            var token = JsonSerializer.Deserialize<CsdlDuocTokenResponse>(body, JsonOptions());
            if (string.IsNullOrWhiteSpace(token?.AccessToken))
                throw new InvalidOperationException("CSDL dược login không trả access_token.");

            var skew = TimeSpan.FromMinutes(2);
            var lifetime = TimeSpan.FromSeconds(Math.Max(60, token.ExpiresIn));
            var entry = new CacheEntry(token.AccessToken, DateTimeOffset.UtcNow.Add(lifetime) - skew);
            _cache[key] = entry;
            _logger.LogInformation(
                "CSDL dược token ok — source={Source} user={User} mode={Mode} expires_in={ExpiresIn}s",
                credentials.Source,
                username,
                credentials.Mode,
                token.ExpiresIn);
            return entry.AccessToken;
        }
        finally
        {
            gate.Release();
        }
    }

    public void Invalidate(string? username)
    {
        if (string.IsNullOrWhiteSpace(username)) return;
        var prefix = username.Trim() + "|";
        foreach (var key in _cache.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                _cache.TryRemove(key, out _);
        }
    }

    public async Task<HttpClient> CreateAuthorizedClientAsync(
        CsdlDuocEffectiveCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient("csdl-duoc");
        client.BaseAddress = new Uri(credentials.BaseUrl.TrimEnd('/') + "/");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(credentials, cancellationToken));
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string CacheKey(CsdlDuocEffectiveCredentials c) =>
        $"{c.Username?.Trim()}|{c.BaseUrl.TrimEnd('/')}";

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };

    private static string Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= max ? value
        : value[..max] + "…";

    private sealed record CacheEntry(string AccessToken, DateTimeOffset ExpiresAt);
}
