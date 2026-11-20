using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// CK18-only opened authenticator. Uses the existing Veo Join account + JWT grant.
/// Does not log or persist the token or PEM.
/// </summary>
public sealed class OfficialVeoJoinFileHttpAuthenticator : IVeoHttpAuthenticator
{
    readonly FamixaVeoJoinCredentialsV1.Account _account;
    readonly HttpClient _http;

    public OfficialVeoJoinFileHttpAuthenticator(FamixaVeoJoinCredentialsV1.Account account, HttpClient? http = null)
    {
        _account = account;
        _http = http ?? new HttpClient { Timeout = ProductionVeoHttpClientPolicy.RequestTimeout };
    }

    public int Calls { get; private set; }
    public string ProjectId => _account.ProjectId;

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        Calls++;
        var jwt = SignJwt(_account);
        using var req = new HttpRequestMessage(HttpMethod.Post, _account.TokenUri);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = VeoHttpAuthContract.GrantType,
            ["assertion"] = jwt,
        });
        using var res = await _http.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var token = doc.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException(FamixaProviderErrorCodes.AuthFailed);
        return token;
    }

    static string SignJwt(FamixaVeoJoinCredentialsV1.Account account)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Base64Url(Encoding.UTF8.GetBytes("""{"alg":"RS256","typ":"JWT"}"""));
        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            iss = account.ClientEmail,
            scope = VeoHttpAuthContract.Scope,
            aud = account.TokenUri,
            iat = now,
            exp = now + VeoHttpAuthContract.JwtLifetimeSeconds,
        })));
        var unsigned = header + "." + payload;
        using var rsa = RSA.Create();
        rsa.ImportFromPem(account.PrivateKey);
        var sig = rsa.SignData(Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return unsigned + "." + Base64Url(sig);
    }

    static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
