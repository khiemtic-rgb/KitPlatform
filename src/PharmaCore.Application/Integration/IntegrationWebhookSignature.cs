using System.Security.Cryptography;
using System.Text;

namespace PharmaCore.Application.Integration;

public static class IntegrationWebhookSignature
{
    public const string HeaderName = "X-Integration-Signature";
    private const string Prefix = "sha256=";

    public static string Sign(string secret, string payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var data = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(key, data);
        return Prefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static bool TryValidate(string secret, string payload, string? headerValue)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return false;

        var expected = Sign(secret, payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(headerValue.Trim()));
    }
}
