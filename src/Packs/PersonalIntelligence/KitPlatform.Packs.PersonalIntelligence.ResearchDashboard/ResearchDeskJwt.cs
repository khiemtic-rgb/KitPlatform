using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.ResearchDashboard;

public enum DeskAccess
{
    Allowed,
    Unconfigured,
    Anonymous,
    Invalid,
    Forbidden,
}

/// <summary>
/// Checks the same HMAC JWT the platform API issues. The signing key comes from configuration, never from source.
/// </summary>
public static class ResearchDeskJwt
{
    public const string RunPermission = "personal_intelligence.trading.run";
    const string RoleClaim = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

    public static DeskAccess Authorize(HttpRequest request, IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32
            || string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
            return DeskAccess.Unconfigured;

        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.Ordinal))
            return DeskAccess.Anonymous;
        var token = header[prefix.Length..].Trim();
        if (!TryRead(token, secret, issuer, audience, out var payload))
            return DeskAccess.Invalid;
        return CanRun(payload) ? DeskAccess.Allowed : DeskAccess.Forbidden;
    }

    public static string Sign(string secret, string issuer, string audience, IReadOnlyList<(string Type, string Value)> claims, DateTimeOffset expires)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var body = new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = audience,
            ["exp"] = expires.ToUnixTimeSeconds(),
        };
        foreach (var group in claims.GroupBy(claim => claim.Type, StringComparer.Ordinal))
        {
            var values = group.Select(claim => claim.Value).ToArray();
            body[group.Key] = values.Length == 1 ? values[0] : values;
        }

        var payload = Base64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)));
        var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(header + "." + payload)));
        return header + "." + payload + "." + signature;
    }

    static bool TryRead(string token, string secret, string issuer, string audience, out JsonElement payload)
    {
        payload = default;
        var parts = token.Split('.');
        if (parts.Length != 3)
            return false;
        byte[] headerBytes;
        byte[] payloadBytes;
        byte[] signature;
        try
        {
            headerBytes = FromBase64Url(parts[0]);
            payloadBytes = FromBase64Url(parts[1]);
            signature = FromBase64Url(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        try
        {
            using (var header = JsonDocument.Parse(headerBytes))
            {
                if (!header.RootElement.TryGetProperty("alg", out var algorithm)
                    || !string.Equals(algorithm.GetString(), "HS256", StringComparison.Ordinal))
                    return false;
            }

            var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
            if (expected.Length != signature.Length || !CryptographicOperations.FixedTimeEquals(expected, signature))
                return false;

            using var document = JsonDocument.Parse(payloadBytes);
            payload = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return false;
        }
        if (!string.Equals(Text(payload, "iss"), issuer, StringComparison.Ordinal))
            return false;
        if (!AudienceMatches(payload, audience))
            return false;
        if (!payload.TryGetProperty("exp", out var exp) || exp.ValueKind != JsonValueKind.Number)
            return false;
        var expires = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
        return DateTimeOffset.UtcNow <= expires.AddMinutes(1);
    }

    static bool CanRun(JsonElement payload)
        => Has(payload, RoleClaim, "ADMIN")
            || Has(payload, "role", "ADMIN")
            || Has(payload, "permission", RunPermission);

    static bool AudienceMatches(JsonElement payload, string audience)
    {
        if (!payload.TryGetProperty("aud", out var value))
            return false;
        if (value.ValueKind == JsonValueKind.String)
            return string.Equals(value.GetString(), audience, StringComparison.Ordinal);
        return value.ValueKind == JsonValueKind.Array
            && value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == audience);
    }

    static bool Has(JsonElement payload, string name, string expected)
    {
        if (!payload.TryGetProperty(name, out var value))
            return false;
        if (value.ValueKind == JsonValueKind.String)
            return string.Equals(value.GetString(), expected, StringComparison.Ordinal);
        return value.ValueKind == JsonValueKind.Array
            && value.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString() == expected);
    }

    static string? Text(JsonElement payload, string name)
        => payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
