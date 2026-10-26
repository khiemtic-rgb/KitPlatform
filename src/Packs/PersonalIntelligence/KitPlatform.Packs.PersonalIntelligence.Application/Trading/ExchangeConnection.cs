using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public sealed class ExchangeConnectionException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public ExchangeConnectionException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

public sealed class ExchangeCredentialRequest
{
    public string? ExchangeCode { get; set; }
    public string? Environment { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? Passphrase { get; set; }
    public bool AcknowledgeUnverifiedPermissions { get; set; }
}

public sealed class ExchangeAssetDto
{
    public string Coin { get; set; } = "";
    public string? WalletBalance { get; set; }
    public string? Equity { get; set; }
    public string? UsdValue { get; set; }
    public string? AvailableToWithdraw { get; set; }
}

public sealed class ExchangeProbeDto
{
    public bool Accepted { get; set; }
    public string ExchangeCode { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Message { get; set; } = "";
    public bool ReadOnlyVerified { get; set; }
    public List<string> PermissionNames { get; set; } = [];
    public string? ExternalUserId { get; set; }
    public string? MarginMode { get; set; }
    public int? UnifiedMarginStatus { get; set; }
    public int? Uta { get; set; }
    public string? AccountType { get; set; }
    public string? TotalEquity { get; set; }
    public string? TotalWalletBalance { get; set; }
    public string? TotalAvailableBalance { get; set; }
    public string? BalanceError { get; set; }
    public string SourceEndpoints { get; set; } = "";
    public List<ExchangeAssetDto> Assets { get; set; } = [];
    public DateTimeOffset CheckedAt { get; set; }
}

public sealed class ExchangeConnectionDto
{
    public Guid ConnectionId { get; set; }
    public string ExchangeCode { get; set; } = "";
    public string Environment { get; set; } = "";
    public string Status { get; set; } = "";
    public string ApiKeyMask { get; set; } = "";
    public bool ReadOnlyVerified { get; set; }
    public List<string> PermissionNames { get; set; } = [];
    public string? ExternalUserId { get; set; }
    public string? MarginMode { get; set; }
    public int? UnifiedMarginStatus { get; set; }
    public int? Uta { get; set; }
    public DateTimeOffset? LastCheckedAt { get; set; }
    public string? LastError { get; set; }
    public string LastProbeStatus { get; set; } = "";
    public DateTimeOffset? DisconnectedAt { get; set; }
}

public sealed class ExchangeAccountViewDto
{
    public ExchangeConnectionDto Connection { get; set; } = new();
    public Guid? SnapshotId { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public string Freshness { get; set; } = "NONE";
    public string? SourceEndpoints { get; set; }
    public string? AccountType { get; set; }
    public string? MarginMode { get; set; }
    public int? UnifiedMarginStatus { get; set; }
    public string? TotalEquity { get; set; }
    public string? TotalWalletBalance { get; set; }
    public string? TotalAvailableBalance { get; set; }
    public string? BalanceError { get; set; }
    public List<ExchangeAssetDto> Assets { get; set; } = [];
}

public interface IExchangeConnectionService
{
    Task<ExchangeProbeDto> TestAsync(ExchangeCredentialRequest request, CancellationToken cancellationToken);
    Task<ExchangeAccountViewDto> SaveAsync(ExchangeCredentialRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExchangeConnectionDto>> ListAsync(CancellationToken cancellationToken);
    Task<ExchangeAccountViewDto> GetAccountAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<ExchangeAccountViewDto> RefreshAsync(Guid connectionId, CancellationToken cancellationToken);
    Task DisconnectAsync(Guid connectionId, CancellationToken cancellationToken);
}

public interface IExchangeConnector
{
    string ExchangeCode { get; }
    Task<ExchangeProbeDto> ReadAsync(string environment, string apiKey, string apiSecret, CancellationToken cancellationToken);
    Task EnsureReadOnlyAsync(string environment, string apiKey, string apiSecret, CancellationToken cancellationToken);
    Task<ExchangeExecutionPageDto> ListExecutionsAsync(
        string environment,
        string apiKey,
        string apiSecret,
        string category,
        long startTimeMs,
        long endTimeMs,
        int limit,
        string? cursor,
        CancellationToken cancellationToken);
}

public interface IExchangeConnectorLog
{
    void Info(string message);
}

public sealed class SilentExchangeConnectorLog : IExchangeConnectorLog
{
    public void Info(string message)
    {
    }
}

public sealed class ExchangeCredentialKey
{
    public string KeyId { get; }
    readonly byte[] _key;

    public ExchangeCredentialKey(string keyId, byte[] key)
    {
        KeyId = keyId;
        _key = key;
    }

    public ReadOnlySpan<byte> Material => _key;
}

public sealed class ExchangeCredentialEnvelope
{
    public string KeyId { get; }
    public byte[] Nonce { get; }
    public byte[] Ciphertext { get; }
    public byte[] Tag { get; }

    public ExchangeCredentialEnvelope(string keyId, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        KeyId = keyId;
        Nonce = nonce;
        Ciphertext = ciphertext;
        Tag = tag;
    }

    public override string ToString() => FormattableString.Invariant($"ExchangeCredentialEnvelope kid={KeyId}");
}

public interface IExchangeCredentialKeyRing
{
    ExchangeCredentialKey RequireCurrent();
}

public static class ExchangeCredentialProtector
{
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;

    public static ExchangeCredentialEnvelope Encrypt(string plaintext, ExchangeCredentialKey key)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            throw new ExchangeConnectionException(400, "secret_required", "API secret là bắt buộc.");
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(RequireMaterial(key), TagSize);
            aes.Encrypt(nonce, plain, cipher, tag);
        }
        catch (CryptographicException)
        {
            throw new ExchangeConnectionException(503, "key_invalid", "Khóa mã hóa không dùng được. Không lưu credential.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        return new ExchangeCredentialEnvelope(key.KeyId, nonce, cipher, tag);
    }

    public static string Decrypt(ExchangeCredentialEnvelope envelope, ExchangeCredentialKey key)
    {
        if (!string.Equals(envelope.KeyId, key.KeyId, StringComparison.Ordinal)
            || envelope.Nonce.Length != NonceSize
            || envelope.Tag.Length != TagSize
            || envelope.Ciphertext.Length == 0)
            throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");

        var plain = new byte[envelope.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(RequireMaterial(key), TagSize);
            aes.Decrypt(envelope.Nonce, envelope.Ciphertext, envelope.Tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            throw new ExchangeConnectionException(503, "ciphertext_invalid", "Không giải mã được credential đã lưu.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public static ExchangeCredentialKey Parse(string? keyId, string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(base64Key))
            throw new ExchangeConnectionException(503, "key_missing", "Chưa cấu hình khóa mã hóa bên ngoài database. Không lưu credential.");
        byte[] material;
        try
        {
            material = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException)
        {
            throw new ExchangeConnectionException(503, "key_invalid", "Khóa mã hóa không hợp lệ. Không lưu credential.");
        }

        if (material.Length != KeySize)
        {
            CryptographicOperations.ZeroMemory(material);
            throw new ExchangeConnectionException(503, "key_invalid", "Khóa mã hóa không hợp lệ. Không lưu credential.");
        }

        return new ExchangeCredentialKey(keyId.Trim(), material);
    }

    public static string Fingerprint(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();

    public static string Mask(string apiKey)
    {
        var trimmed = apiKey.Trim();
        if (trimmed.Length < 8)
            throw new ExchangeConnectionException(400, "invalid_key", "API key quá ngắn.");
        return "••••" + trimmed[^4..];
    }

    static ReadOnlySpan<byte> RequireMaterial(ExchangeCredentialKey key)
    {
        if (string.IsNullOrWhiteSpace(key.KeyId) || key.Material.Length != KeySize)
            throw new ExchangeConnectionException(503, "key_invalid", "Khóa mã hóa không hợp lệ. Không lưu credential.");
        return key.Material;
    }
}

public sealed class EnvironmentExchangeCredentialKeyRing : IExchangeCredentialKeyRing
{
    public const string KeyVariable = "PersonalIntelligence__ExchangeCredentialKey";
    public const string KeyIdVariable = "PersonalIntelligence__ExchangeCredentialKeyId";

    public ExchangeCredentialKey RequireCurrent()
    {
        var keyId = Environment.GetEnvironmentVariable(KeyIdVariable);
        if (string.IsNullOrWhiteSpace(keyId))
            keyId = "pi-exchange-v1";
        return ExchangeCredentialProtector.Parse(keyId, Environment.GetEnvironmentVariable(KeyVariable));
    }
}

public sealed class StaticExchangeCredentialKeyRing : IExchangeCredentialKeyRing
{
    readonly ExchangeCredentialKey _key;

    public StaticExchangeCredentialKeyRing(string keyId, string base64Key) =>
        _key = ExchangeCredentialProtector.Parse(keyId, base64Key);

    public ExchangeCredentialKey RequireCurrent() => _key;
}
