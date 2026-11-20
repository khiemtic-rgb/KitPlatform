using System.Security.Cryptography;
using System.Text;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Seals the active encryption key with the current Windows user and proves it opens again.
/// A different key id is not accepted. The key material is not written to the repository or to logs.
/// </summary>
internal static class ProductionCredentialLocalSeal
{
    const string IdPrefix = "ProductionCredentials__DataEncryptionKeyId=";
    const string KeyPrefix = "ProductionCredentials__DataEncryptionKey=";

    public static string Match(string keyId, ReadOnlySpan<byte> material, out string? evidenceKeyId, string? vaultRoot = null)
    {
        evidenceKeyId = null;
        if (!TryToken(keyId, out var token))
            return ProductionCredentialBackupEvidence.Absent;

        var vault = string.IsNullOrWhiteSpace(vaultRoot) ? VaultRoot() : vaultRoot;
        Directory.CreateDirectory(vault);
        var sealedPath = Path.Combine(vault, "production-credential-key." + token + ".dpapi");
        var manifestPath = Path.Combine(vault, "production-credential-key." + token + ".manifest.txt");
        if (!File.Exists(sealedPath))
            WriteSeal(keyId, material, sealedPath, manifestPath);

        if (!TryOpen(sealedPath, out var openedId, out var opened))
        {
            evidenceKeyId = ReadManifestKeyId(manifestPath) ?? ReadLegacyKeyId(vault);
            return string.IsNullOrWhiteSpace(evidenceKeyId)
                ? ProductionCredentialBackupEvidence.Absent
                : ProductionCredentialBackupEvidence.RestoreMismatch;
        }

        try
        {
            evidenceKeyId = openedId;
            if (!string.Equals(openedId, keyId, StringComparison.Ordinal))
                return ProductionCredentialBackupEvidence.KeyIdMismatch;
            if (opened.Length != material.Length || !CryptographicOperations.FixedTimeEquals(opened, material))
                return ProductionCredentialBackupEvidence.RestoreMismatch;
            return ProductionCredentialBackupEvidence.RestoreOk;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(opened);
        }
    }

    public static string? ReadLegacyKeyId(string? vault = null)
    {
        vault ??= VaultRoot();
        return ReadManifestKeyId(Path.Combine(vault, "production-credential-key.manifest.txt"));
    }

    static void WriteSeal(string keyId, ReadOnlySpan<byte> material, string sealedPath, string manifestPath)
    {
        var text = IdPrefix + keyId + "\n" + KeyPrefix + Convert.ToBase64String(material) + "\n";
        var payload = Encoding.UTF8.GetBytes(text);
        byte[] sealedBytes;
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Production credential seal requires Windows DPAPI.");
        try
        {
            sealedBytes = ProtectedData.Protect(payload, null, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }

        File.WriteAllBytes(sealedPath, sealedBytes);
        var manifest = "format=production-credential-key-dpapi-v1\n"
            + "keyId=" + keyId + "\n"
            + "materialBytes=" + material.Length + "\n"
            + "scope=CurrentUser\n"
            + "machine=" + Environment.MachineName + "\n";
        File.WriteAllText(manifestPath, manifest);
        CryptographicOperations.ZeroMemory(sealedBytes);
    }

    static bool TryOpen(string sealedPath, out string keyId, out byte[] material)
    {
        keyId = "";
        material = [];
        if (!File.Exists(sealedPath))
            return false;
        var blob = File.ReadAllBytes(sealedPath);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Production credential seal requires Windows DPAPI.");
        byte[] payload;
        try
        {
            payload = ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser);
        }
        catch (CryptographicException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(blob);
        }

        try
        {
            var text = Encoding.UTF8.GetString(payload);
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (lines.Length != 2 || !lines[0].StartsWith(IdPrefix, StringComparison.Ordinal) || !lines[1].StartsWith(KeyPrefix, StringComparison.Ordinal))
                return false;
            keyId = lines[0][IdPrefix.Length..];
            material = Convert.FromBase64String(lines[1][KeyPrefix.Length..]);
            return material.Length == ProductionCredentialSecretEnvelope.KeySize;
        }
        catch (FormatException)
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(payload);
        }
    }

    static string? ReadManifestKeyId(string path)
    {
        if (!File.Exists(path))
            return null;
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("keyId=", StringComparison.Ordinal))
                return line["keyId=".Length..].Trim();
        }
        return null;
    }

    static bool TryToken(string keyId, out string token)
    {
        token = "";
        if (string.IsNullOrWhiteSpace(keyId) || keyId.Length > 64)
            return false;
        foreach (var character in keyId)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_')
                return false;
        }
        token = keyId;
        return true;
    }

    static string VaultRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KitPlatform", "production-credential-backup");
}
