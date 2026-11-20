using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Exchanges one signed JWT for an access token. The caller supplies the assertion.
/// This type does not read environment, Join files, or credential stores.
/// </summary>
public interface IVeoJwtBearerTokenClient
{
    Task<string> ExchangeAsync(string tokenUri, string assertion, CancellationToken ct);
}

public static class VeoFrozenCredentialCodes
{
    public const string ContractMismatch = "VEO_SA_CONTRACT_MISMATCH";
}

/// <summary>
/// RS256 JWT for a service-account assertion. The PEM is an argument, never a file or env read.
/// </summary>
public static class VeoJwtBearer
{
    public static string Sign(string clientEmail, string serviceAccountJsonOrPem, string tokenUri, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(tokenUri))
            throw new InvalidOperationException(VeoHttpServiceAccountCodes.ClientEmailMissing);
        var pem = PrivateKeyPem(serviceAccountJsonOrPem);
        if (!pem.Contains("BEGIN PRIVATE KEY", StringComparison.Ordinal)
            && !pem.Contains("BEGIN RSA PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException(VeoHttpServiceAccountCodes.PrivateKeyInvalid);

        try
        {
            var iat = now.ToUnixTimeSeconds();
            var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
            var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                iss = clientEmail.Trim(),
                scope = VeoHttpAuthContract.Scope,
                aud = tokenUri.Trim(),
                iat,
                exp = iat + VeoHttpAuthContract.JwtLifetimeSeconds,
            })));
            var unsigned = header + "." + payload;
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            var sig = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return unsigned + "." + Base64Url(sig);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException(VeoHttpServiceAccountCodes.PrivateKeyInvalid);
        }
    }

    public static string PrivateKeyPem(string serviceAccountJsonOrPem)
    {
        var raw = (serviceAccountJsonOrPem ?? "").Trim();
        if (raw.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("private_key", out var key)
                    || key.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(key.GetString()))
                    throw new InvalidOperationException(VeoHttpServiceAccountCodes.PrivateKeyMissing);
                raw = key.GetString()!;
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(VeoHttpServiceAccountCodes.PrivateKeyInvalid);
            }
        }

        return raw.Replace("\\n", "\n", StringComparison.Ordinal).Trim();
    }

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// HTTPS token exchange only. Does not log the assertion or the access token.
/// </summary>
public sealed class HttpVeoJwtBearerTokenClient : IVeoJwtBearerTokenClient
{
    readonly HttpClient _http;

    public HttpVeoJwtBearerTokenClient(HttpClient http) =>
        _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<string> ExchangeAsync(string tokenUri, string assertion, CancellationToken ct)
    {
        if (!Uri.TryCreate((tokenUri ?? "").Trim(), UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(VeoHttpServiceAccountCodes.TokenUriInvalid);
        if (string.IsNullOrWhiteSpace(assertion))
            throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);

        using var req = new HttpRequestMessage(HttpMethod.Post, uri);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = VeoHttpAuthContract.GrantType,
            ["assertion"] = assertion,
        });

        HttpResponseMessage res;
        try
        {
            res = await _http.SendAsync(req, ct);
        }
        catch (HttpRequestException)
        {
            throw new InvalidOperationException(FamixaProviderErrorCodes.Timeout);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(FamixaProviderErrorCodes.Timeout);
        }

        using (res)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);
            try
            {
                using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
                var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(token))
                    throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);
                return token.Trim();
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(ProductionFailureReasons.InvalidProviderResponse);
            }
        }
    }
}
