using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// ADR-04 Gate B envelope. AES-256-GCM.
/// Key size 32 bytes. Nonce 12 bytes, random per encryption. Tag 16 bytes.
/// Envelope version 1. KeyId is a non-secret key version, not the key.
/// Ciphertext is not a credential identity.
/// </summary>
public sealed class ProductionCredentialSecretEnvelope
{
    public const int EnvelopeVersion = 1;
    public const int KeySize = 32;
    public const int NonceSize = 12;
    public const int TagSize = 16;

    public int Version { get; }
    public string KeyId { get; }
    public byte[] Nonce { get; }
    public byte[] Ciphertext { get; }
    public byte[] Tag { get; }

    public ProductionCredentialSecretEnvelope(int version, string keyId, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        Version = version;
        KeyId = keyId;
        Nonce = nonce;
        Ciphertext = ciphertext;
        Tag = tag;
    }

    public override string ToString() =>
        FormattableString.Invariant($"ProductionCredentialSecretEnvelope v{Version} kid={KeyId}");
}

public sealed class ProductionCredentialKey
{
    public string KeyId { get; }
    readonly byte[] _key;

    public ProductionCredentialKey(string keyId, byte[] key)
    {
        KeyId = keyId;
        _key = key;
    }

    public ReadOnlySpan<byte> Material => _key;

    public override string ToString() =>
        FormattableString.Invariant($"ProductionCredentialKey kid={KeyId}");
}

/// <summary>
/// Off-machine backup attestation. Missing, blank, or any value other than the exact token true fails closed.
/// A backup on the same machine does not satisfy this gate.
/// </summary>
public static class ProductionCredentialKeyBackupGate
{
    public const string BackupRequired = "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED";
    public const string ConfirmationName = "ProductionCredentials:OffMachineBackupConfirmed";

    public static void RequireOffMachine(string? confirmation)
    {
        if (!string.Equals(confirmation, "true", StringComparison.Ordinal))
            throw new InvalidOperationException(BackupRequired);
    }
}

/// <summary>
/// Non-secret view of the write gate. Restore verification is never inferred from the confirmation flag.
/// </summary>
public sealed record ProductionCredentialBackupGateView(
    string WriteGate,
    string RestoreVerification,
    string? EncryptionKeyId,
    string? BlockCode,
    string? EvidenceCode = null,
    string? EvidenceKeyId = null);

public static class ProductionCredentialBackupEvidence
{
    public const string Absent = "ABSENT";
    public const string KeyIdMismatch = "KEY_ID_MISMATCH";
    public const string SameMachine = "SAME_MACHINE";
    public const string RestoreMismatch = "RESTORE_MISMATCH";
    public const string RestoreOk = "RESTORE_OK";

    public static string Evaluate(
        string? currentKeyId,
        string? evidenceKeyId,
        bool evidenceOnThisMachine,
        bool restoreMatched)
    {
        if (string.IsNullOrWhiteSpace(evidenceKeyId))
            return Absent;
        if (!string.Equals(currentKeyId?.Trim(), evidenceKeyId.Trim(), StringComparison.Ordinal))
            return KeyIdMismatch;
        if (restoreMatched)
            return RestoreOk;
        return evidenceOnThisMachine ? SameMachine : RestoreMismatch;
    }
}

public static class ProductionCredentialBackupGateStatus
{
    public const string WriteBlocked = "BLOCKED";
    public const string WriteOpen = "OPEN";
    public const string RestoreUnverified = "UNVERIFIED";
    public const string RestoreVerified = "VERIFIED";

    public static ProductionCredentialBackupGateView WithEvidence(
        ProductionCredentialBackupGateView view,
        string evidenceCode,
        string? evidenceKeyId)
    {
        var shownKeyId = evidenceCode == ProductionCredentialBackupEvidence.Absent ? null : evidenceKeyId;
        var next = view with { EvidenceCode = evidenceCode, EvidenceKeyId = shownKeyId };
        var backupBlocked = next.BlockCode is null
            || next.BlockCode == ProductionCredentialKeyBackupGate.BackupRequired;
        if (evidenceCode == ProductionCredentialBackupEvidence.RestoreOk && backupBlocked)
        {
            return next with
            {
                WriteGate = WriteOpen,
                RestoreVerification = RestoreVerified,
                BlockCode = null,
            };
        }
        return next;
    }

    public static ProductionCredentialBackupGateView FromConfirmation(
        string? confirmation,
        string? encryptionKeyId,
        string? keyError)
    {
        if (!string.IsNullOrWhiteSpace(keyError))
        {
            return new ProductionCredentialBackupGateView(
                WriteBlocked, RestoreUnverified, null, keyError);
        }

        if (!string.Equals(confirmation, "true", StringComparison.Ordinal))
        {
            return new ProductionCredentialBackupGateView(
                WriteBlocked,
                RestoreUnverified,
                encryptionKeyId,
                ProductionCredentialKeyBackupGate.BackupRequired);
        }

        return new ProductionCredentialBackupGateView(
            WriteOpen, RestoreUnverified, encryptionKeyId, null);
    }
}

public sealed record ProductionCredentialActionableError(
    string Code,
    string Message,
    string Category,
    string? ActionType,
    string? ActionLabel,
    string? ActionTarget,
    bool Retryable,
    string? CorrelationId);

public static class ProductionCredentialActionableErrors
{
    public const string SecurityTarget = "content.productionCredentialSecurity";
    public const string SecurityGate = "security_gate";

    public static ProductionCredentialActionableError Describe(string code, string? correlationId)
    {
        if (code == ProductionCredentialKeyBackupGate.BackupRequired)
        {
            return new ProductionCredentialActionableError(
                code,
                "Chưa đủ điều kiện bảo mật để lưu thông tin xác thực. Nội dung vừa nhập chưa được lưu.",
                SecurityGate,
                "open_security_condition",
                "Mở điều kiện bảo mật",
                SecurityTarget,
                true,
                correlationId);
        }

        return new ProductionCredentialActionableError(code, code, "unknown", null, null, null, false, correlationId);
    }
}

public interface IProductionCredentialKeyRing
{
    ProductionCredentialKey RequireCurrent();
    ProductionCredentialKey Require(string keyId);
    ProductionCredentialBackupGateView DescribeWriteGate();
}

public static class ProductionCredentialSecretProtector
{
    public const string KeyMissing = "PRODUCTION_CREDENTIAL_KEY_MISSING";
    public const string KeyInvalid = "PRODUCTION_CREDENTIAL_KEY_INVALID";
    public const string CiphertextInvalid = "PRODUCTION_CREDENTIAL_CIPHERTEXT_INVALID";
    public const string SecretUnavailable = "PRODUCTION_CREDENTIAL_SECRET_UNAVAILABLE";
    public const string SecretRequired = "PRODUCTION_CREDENTIAL_SECRET_REQUIRED";
    public const string SecretImmutable = "PRODUCTION_CREDENTIAL_SECRET_IMMUTABLE";

    public static ProductionCredentialSecretEnvelope Encrypt(string plaintext, ProductionCredentialKey key)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
            throw new InvalidOperationException(SecretRequired);
        var material = RequireKey(key);
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = new byte[ProductionCredentialSecretEnvelope.NonceSize];
        RandomNumberGenerator.Fill(nonce);
        var cipher = new byte[plain.Length];
        var tag = new byte[ProductionCredentialSecretEnvelope.TagSize];
        try
        {
            using var aes = new AesGcm(material, ProductionCredentialSecretEnvelope.TagSize);
            aes.Encrypt(nonce, plain, cipher, tag);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(KeyInvalid);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }

        return new ProductionCredentialSecretEnvelope(
            ProductionCredentialSecretEnvelope.EnvelopeVersion, key.KeyId, nonce, cipher, tag);
    }

    public static string Decrypt(ProductionCredentialSecretEnvelope envelope, ProductionCredentialKey key)
    {
        if (envelope.Version != ProductionCredentialSecretEnvelope.EnvelopeVersion)
            throw new InvalidOperationException(CiphertextInvalid);
        if (!string.Equals(envelope.KeyId, key.KeyId, StringComparison.Ordinal))
            throw new InvalidOperationException(KeyMissing);
        if (envelope.Nonce.Length != ProductionCredentialSecretEnvelope.NonceSize
            || envelope.Tag.Length != ProductionCredentialSecretEnvelope.TagSize
            || envelope.Ciphertext.Length == 0)
            throw new InvalidOperationException(CiphertextInvalid);

        var material = RequireKey(key);
        var plain = new byte[envelope.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(material, ProductionCredentialSecretEnvelope.TagSize);
            aes.Decrypt(envelope.Nonce, envelope.Ciphertext, envelope.Tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            throw new InvalidOperationException(CiphertextInvalid);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    public static ProductionCredentialKey ParseKey(string? keyId, string? base64Key)
    {
        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(base64Key))
            throw new InvalidOperationException(KeyMissing);
        byte[] material;
        try
        {
            material = Convert.FromBase64String(base64Key.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(KeyInvalid);
        }

        if (material.Length != ProductionCredentialSecretEnvelope.KeySize)
        {
            CryptographicOperations.ZeroMemory(material);
            throw new InvalidOperationException(KeyInvalid);
        }

        return new ProductionCredentialKey(keyId.Trim(), material);
    }

    static ReadOnlySpan<byte> RequireKey(ProductionCredentialKey key)
    {
        if (string.IsNullOrWhiteSpace(key.KeyId) || key.Material.Length != ProductionCredentialSecretEnvelope.KeySize)
            throw new InvalidOperationException(KeyInvalid);
        return key.Material;
    }
}

/// <summary>
/// Gate B storage primitive. Encrypts before retain. Does not resolve a provider.
/// Caller tenant must own the credential version. Wrong tenant fails closed.
/// </summary>
public interface IProductionCredentialSecretStore
{
    Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default);
    Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default);
}

public sealed class InMemoryProductionCredentialSecretStore : IProductionCredentialSecretStore
{
    readonly IProductionCallerTenant _caller;
    readonly InMemoryProductionCredentialStore _identities;
    readonly IProductionCredentialKeyRing _keys;
    readonly Dictionary<Guid, ProductionCredentialSecretEnvelope> _envelopes = new();

    public InMemoryProductionCredentialSecretStore(
        IProductionCallerTenant caller,
        InMemoryProductionCredentialStore identities,
        IProductionCredentialKeyRing keys)
    {
        _caller = caller;
        _identities = identities;
        _keys = keys;
    }

    public ProductionCredentialSecretEnvelope? EnvelopeForTests(Guid credentialVersionId) =>
        _envelopes.TryGetValue(credentialVersionId, out var envelope) ? envelope : null;

    public void RetainPrepared(Guid credentialVersionId, ProductionCredentialSecretEnvelope envelope)
    {
        var version = OwnedVersion(credentialVersionId, CancellationToken.None).GetAwaiter().GetResult();
        if (envelope.Ciphertext.Length == 0 || envelope.Nonce.Length == 0 || envelope.Tag.Length == 0)
            throw new InvalidOperationException(ProductionCredentialSecretProtector.CiphertextInvalid);
        if (_envelopes.ContainsKey(version.CredentialVersionId))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretImmutable);
        _envelopes[version.CredentialVersionId] = envelope;
    }

    public void DiscardPrepared(Guid credentialVersionId) => _envelopes.Remove(credentialVersionId);

    public async Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default)
    {
        var version = await OwnedVersion(credentialVersionId, ct);
        if (_envelopes.ContainsKey(version.CredentialVersionId))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretImmutable);
        var envelope = ProductionCredentialSecretProtector.Encrypt(secret, _keys.RequireCurrent());
        _envelopes[version.CredentialVersionId] = envelope;
    }

    public async Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default)
    {
        var version = await OwnedVersion(credentialVersionId, ct);
        if (!_envelopes.TryGetValue(version.CredentialVersionId, out var envelope))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
        var key = _keys.Require(envelope.KeyId);
        return ProductionCredentialSecretProtector.Decrypt(envelope, key);
    }

    async Task<ProductionCredentialVersion> OwnedVersion(Guid credentialVersionId, CancellationToken ct)
    {
        _caller.RequireTenant();
        var version = await _identities.GetVersionAsync(credentialVersionId, ct);
        if (version is null)
            throw new InvalidOperationException(ProductionCredentialSecretProtector.SecretUnavailable);
        return version;
    }
}

public sealed class MapProductionCredentialKeyRing : IProductionCredentialKeyRing
{
    readonly Dictionary<string, ProductionCredentialKey> _keys;
    readonly string? _currentId;

    public MapProductionCredentialKeyRing(string? currentId, IReadOnlyDictionary<string, byte[]> keys)
    {
        _currentId = string.IsNullOrWhiteSpace(currentId) ? null : currentId.Trim();
        _keys = new Dictionary<string, ProductionCredentialKey>(StringComparer.Ordinal);
        foreach (var pair in keys)
            _keys[pair.Key] = new ProductionCredentialKey(pair.Key, pair.Value);
    }

    public ProductionCredentialKey RequireCurrent()
    {
        if (_currentId is null || !_keys.TryGetValue(_currentId, out var key))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.KeyMissing);
        return key;
    }

    public ProductionCredentialKey Require(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId) || !_keys.TryGetValue(keyId, out var key))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.KeyMissing);
        return key;
    }

    public ProductionCredentialBackupGateView DescribeWriteGate()
    {
        try
        {
            return ProductionCredentialBackupGateStatus.FromConfirmation(null, RequireCurrent().KeyId, null);
        }
        catch (InvalidOperationException ex) when (
            ex.Message is ProductionCredentialSecretProtector.KeyMissing
                or ProductionCredentialSecretProtector.KeyInvalid)
        {
            return ProductionCredentialBackupGateStatus.FromConfirmation(null, null, ex.Message);
        }
    }
}
