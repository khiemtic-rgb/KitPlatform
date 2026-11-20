using System.Security.Cryptography;
using System.Text.Json;
using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// Backup-gate status is non-secret. A passing test is not evidence that a production key was backed up.
/// </summary>
public sealed class ProductionCredentialBackupGateTests
{
    [Fact]
    public void Matching_local_seal_opens_without_a_confirmation_flag()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var view = Ring(key, confirmation: null).DescribeWriteGate();

        Assert.Equal(ProductionCredentialBackupGateStatus.WriteOpen, view.WriteGate);
        Assert.Equal(ProductionCredentialBackupGateStatus.RestoreVerified, view.RestoreVerification);
        Assert.Null(view.BlockCode);
        Assert.Equal("v1", view.EncryptionKeyId);
        Assert.Equal(ProductionCredentialBackupEvidence.RestoreOk, view.EvidenceCode);
        Assert.DoesNotContain(Convert.ToBase64String(key), JsonSerializer.Serialize(view));
    }

    [Fact]
    public void Exact_true_without_a_matching_seal_stays_unverified()
    {
        var vault = NewVault();
        ProductionCredentialLocalSeal.Match("v1", RandomNumberGenerator.GetBytes(32), out _, vault);
        var view = Ring(RandomNumberGenerator.GetBytes(32), "true", vault).DescribeWriteGate();

        Assert.Equal(ProductionCredentialBackupGateStatus.WriteOpen, view.WriteGate);
        Assert.Equal(ProductionCredentialBackupGateStatus.RestoreUnverified, view.RestoreVerification);
        Assert.Null(view.BlockCode);
    }

    [Fact]
    public void Capitalized_true_without_a_matching_seal_stays_blocked()
    {
        var vault = NewVault();
        ProductionCredentialLocalSeal.Match("v1", RandomNumberGenerator.GetBytes(32), out _, vault);
        var view = Ring(RandomNumberGenerator.GetBytes(32), "True", vault).DescribeWriteGate();

        Assert.Equal(ProductionCredentialKeyBackupGate.BackupRequired, view.BlockCode);
        Assert.Equal(ProductionCredentialBackupGateStatus.WriteBlocked, view.WriteGate);
        Assert.Equal(ProductionCredentialBackupGateStatus.RestoreUnverified, view.RestoreVerification);
    }

    [Fact]
    public void Invalid_key_blocks_without_returning_key_material()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProductionCredentials:DataEncryptionKeyId"] = "v1",
            ["ProductionCredentials:DataEncryptionKey"] = "not-a-key",
            ["ProductionCredentials:OffMachineBackupConfirmed"] = "true",
        }).Build();

        var view = new ConfigurationProductionCredentialKeyRing(config).DescribeWriteGate();

        Assert.Equal(ProductionCredentialBackupGateStatus.WriteBlocked, view.WriteGate);
        Assert.Equal(ProductionCredentialSecretProtector.KeyInvalid, view.BlockCode);
        Assert.Null(view.EncryptionKeyId);
        Assert.DoesNotContain("not-a-key", JsonSerializer.Serialize(view));
    }

    [Fact]
    public void Mismatched_key_id_does_not_decrypt()
    {
        var keyA = RandomNumberGenerator.GetBytes(32);
        var keyB = RandomNumberGenerator.GetBytes(32);
        var envelope = ProductionCredentialSecretProtector.Encrypt(
            "tenant-secret", new ProductionCredentialKey("v1", keyA));

        var mismatch = Assert.Throws<InvalidOperationException>(() =>
            ProductionCredentialSecretProtector.Decrypt(envelope, new ProductionCredentialKey("v2", keyB)));
        Assert.Equal(ProductionCredentialSecretProtector.KeyMissing, mismatch.Message);

        var wrongMaterial = Assert.Throws<InvalidOperationException>(() =>
            ProductionCredentialSecretProtector.Decrypt(envelope, new ProductionCredentialKey("v1", keyB)));
        Assert.Equal(ProductionCredentialSecretProtector.CiphertextInvalid, wrongMaterial.Message);
        Assert.DoesNotContain("tenant-secret", mismatch.Message + wrongMaterial.Message);
    }

    [Fact]
    public void Backup_required_points_at_the_registered_security_flow_without_a_bypass()
    {
        var body = ProductionCredentialActionableErrors.Describe(
            ProductionCredentialKeyBackupGate.BackupRequired, "trace-1");

        Assert.Equal("security_gate", body.Category);
        Assert.Equal("content.productionCredentialSecurity", body.ActionTarget);
        Assert.Equal("Mở điều kiện bảo mật", body.ActionLabel);
        Assert.True(body.Retryable);
        Assert.DoesNotContain("OffMachine", body.Message);
        Assert.DoesNotContain("DataEncryptionKey", body.Message);
        Assert.Equal("trace-1", body.CorrelationId);
    }

    [Fact]
    public void Local_copy_of_another_key_id_does_not_confirm_restore()
    {
        var evidence = ProductionCredentialBackupEvidence.Evaluate("v1", "prod-v1", evidenceOnThisMachine: true, restoreMatched: false);

        Assert.Equal(ProductionCredentialBackupEvidence.KeyIdMismatch, evidence);
        var view = ProductionCredentialBackupGateStatus.FromConfirmation(null, "v1", null) with
        {
            EvidenceCode = evidence,
            EvidenceKeyId = "prod-v1",
        };
        Assert.Equal(ProductionCredentialBackupGateStatus.WriteBlocked, view.WriteGate);
        Assert.Equal(ProductionCredentialBackupGateStatus.RestoreUnverified, view.RestoreVerification);
    }

    [Fact]
    public void Proven_restore_of_the_current_key_on_this_machine_opens_the_gate()
    {
        var evidence = ProductionCredentialBackupEvidence.Evaluate("v1", "v1", evidenceOnThisMachine: true, restoreMatched: true);
        Assert.Equal(ProductionCredentialBackupEvidence.RestoreOk, evidence);
        var view = ProductionCredentialBackupGateStatus.WithEvidence(
            ProductionCredentialBackupGateStatus.FromConfirmation(null, "v1", null),
            evidence,
            "v1");
        Assert.Equal(ProductionCredentialBackupGateStatus.WriteOpen, view.WriteGate);
        Assert.Equal(ProductionCredentialBackupGateStatus.RestoreVerified, view.RestoreVerification);
        Assert.Null(view.BlockCode);
    }

    [Fact]
    public void Unopened_same_machine_copy_stays_blocked()
    {
        Assert.Equal(
            ProductionCredentialBackupEvidence.SameMachine,
            ProductionCredentialBackupEvidence.Evaluate("v1", "v1", evidenceOnThisMachine: true, restoreMatched: false));
    }

    [Fact]
    public void Unknown_credential_code_has_no_invented_action()
    {
        var body = ProductionCredentialActionableErrors.Describe("PRODUCTION_CREDENTIAL_KEY_MISSING", null);

        Assert.Equal("unknown", body.Category);
        Assert.Null(body.ActionTarget);
        Assert.Null(body.ActionLabel);
        Assert.False(body.Retryable);
    }

    static string NewVault()
    {
        var vault = Path.Combine(Path.GetTempPath(), "kpa-seal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vault);
        return vault;
    }

    static ConfigurationProductionCredentialKeyRing Ring(byte[] key, string? confirmation, string? vault = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ProductionCredentials:DataEncryptionKeyId"] = "v1",
            ["ProductionCredentials:DataEncryptionKey"] = Convert.ToBase64String(key),
            ["ProductionCredentials:LocalSealVault"] = vault ?? NewVault(),
        };
        if (confirmation is not null)
            values["ProductionCredentials:OffMachineBackupConfirmed"] = confirmation;
        return new ConfigurationProductionCredentialKeyRing(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }
}
