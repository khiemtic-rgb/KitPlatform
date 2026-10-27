using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class OkxSigner
{
    public static string Timestamp(DateTimeOffset now) =>
        now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    public static string Sign(string secret, string timestamp, string method, string requestPath)
    {
        var payload = timestamp + method.ToUpperInvariant() + requestPath;
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
