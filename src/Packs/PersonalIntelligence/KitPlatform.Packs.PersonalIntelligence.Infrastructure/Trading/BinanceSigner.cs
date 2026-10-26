using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;

public static class BinanceSigner
{
    public const string RecvWindow = "5000";

    public static string Sign(string secret, string query)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(query));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
