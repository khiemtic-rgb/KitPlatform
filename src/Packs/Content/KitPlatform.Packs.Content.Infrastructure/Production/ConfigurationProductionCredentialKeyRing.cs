using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Loads the data-encryption key from process configuration.
/// Production name: ProductionCredentials__DataEncryptionKey
/// Production key id: ProductionCredentials__DataEncryptionKeyId
/// Previous keys, for master-key decrypt only: ProductionCredentials__Keys__{keyId}
/// The key is not read from JWT, the database password, or provider API keys.
/// Absence fails closed when a secret is stored or opened. Startup does not invent a key.
/// </summary>
internal sealed class ConfigurationProductionCredentialKeyRing : IProductionCredentialKeyRing
{
    public const string CurrentKeyName = "ProductionCredentials:DataEncryptionKey";
    public const string CurrentKeyIdName = "ProductionCredentials:DataEncryptionKeyId";
    public const string PreviousKeyPrefix = "ProductionCredentials:Keys:";
    public const string OffMachineBackupConfirmedName = ProductionCredentialKeyBackupGate.ConfirmationName;

    readonly IConfiguration _configuration;

    public ConfigurationProductionCredentialKeyRing(IConfiguration configuration) =>
        _configuration = configuration;

    public void RequireOffMachineBackup() =>
        ProductionCredentialKeyBackupGate.RequireOffMachine(_configuration[OffMachineBackupConfirmedName]);

    public void RequireBackupSatisfied()
    {
        var view = DescribeWriteGate();
        if (view.WriteGate != ProductionCredentialBackupGateStatus.WriteOpen)
            throw new InvalidOperationException(ProductionCredentialKeyBackupGate.BackupRequired);
    }

    public ProductionCredentialKey RequireCurrent()
    {
        var keyId = _configuration[CurrentKeyIdName];
        var material = _configuration[CurrentKeyName];
        return ProductionCredentialSecretProtector.ParseKey(keyId, material);
    }

    public ProductionCredentialKey Require(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
            throw new InvalidOperationException(ProductionCredentialSecretProtector.KeyMissing);

        var currentId = _configuration[CurrentKeyIdName];
        if (string.Equals(currentId?.Trim(), keyId, StringComparison.Ordinal))
            return RequireCurrent();

        var previous = _configuration[PreviousKeyPrefix + keyId];
        return ProductionCredentialSecretProtector.ParseKey(keyId, previous);
    }

    public ProductionCredentialBackupGateView DescribeWriteGate()
    {
        string? keyId = null;
        string? keyError = null;
        try
        {
            keyId = RequireCurrent().KeyId;
        }
        catch (InvalidOperationException ex) when (
            ex.Message is ProductionCredentialSecretProtector.KeyMissing
                or ProductionCredentialSecretProtector.KeyInvalid)
        {
            keyError = ex.Message;
        }

        string? evidenceKeyId = null;
        var evidence = ProductionCredentialBackupEvidence.Absent;
        if (keyError is null && keyId is not null)
        {
            try
            {
                var current = RequireCurrent();
                var vault = _configuration["ProductionCredentials:LocalSealVault"];
                evidence = ProductionCredentialLocalSeal.Match(
                    current.KeyId,
                    current.Material,
                    out evidenceKeyId,
                    string.IsNullOrWhiteSpace(vault) ? null : vault);
            }
            catch (IOException)
            {
                evidence = ProductionCredentialBackupEvidence.Absent;
            }
            catch (CryptographicException)
            {
                evidence = ProductionCredentialBackupEvidence.RestoreMismatch;
            }
            catch (PlatformNotSupportedException)
            {
                evidence = ProductionCredentialBackupEvidence.Absent;
            }
        }
        else
        {
            evidenceKeyId = ProductionCredentialLocalSeal.ReadLegacyKeyId();
            evidence = ProductionCredentialBackupEvidence.Evaluate(keyId, evidenceKeyId, evidenceOnThisMachine: true, restoreMatched: false);
        }

        return ProductionCredentialBackupGateStatus.WithEvidence(
            ProductionCredentialBackupGateStatus.FromConfirmation(
                _configuration[OffMachineBackupConfirmedName],
                keyId,
                keyError),
            evidence,
            evidenceKeyId);
    }

    static (string? KeyId, bool OnThisMachine) ReadLocalEvidence()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var path = Path.Combine(root, "KitPlatform", "production-credential-backup", "production-credential-key.manifest.txt");
        if (!File.Exists(path))
            return (null, false);
        string? keyId = null;
        string? machine = null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("keyId=", StringComparison.Ordinal))
                keyId = line["keyId=".Length..].Trim();
            else if (line.StartsWith("machine=", StringComparison.Ordinal))
                machine = line["machine=".Length..].Trim();
        }
        var onThisMachine = !string.IsNullOrWhiteSpace(machine)
            && string.Equals(machine, Environment.MachineName, StringComparison.OrdinalIgnoreCase);
        return (keyId, onThisMachine);
    }
}
