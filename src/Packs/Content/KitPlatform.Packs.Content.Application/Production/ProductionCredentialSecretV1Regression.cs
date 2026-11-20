using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-STORAGE-IMPLEMENTATION-01. Gate B ciphertext only.
/// No provider call. No org_settings copy.
/// </summary>
public static class ProductionCredentialSecretV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_SECRET_01";
    const string Plain = "gate-b-plain-value";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var keyA = NewKey();
        var keyB = NewKey();
        var ring = Ring("v1", ("v1", keyA));
        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "tenant-a-secret");
        identities.CreateAsync(credential).GetAwaiter().GetResult();
        var version = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 1, "runway", "gen3");
        identities.AddVersionAsync(version).GetAwaiter().GetResult();
        secrets.PutAsync(version.CredentialVersionId, Plain).GetAwaiter().GetResult();

        var envelope = secrets.EnvelopeForTests(version.CredentialVersionId);
        Ok(envelope is not null, "T1 envelope stored");
        Ok(envelope!.Ciphertext.Length > 0
           && !envelope.Ciphertext.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Plain)),
            "T2 ciphertext is not plaintext");
        Ok(!Convert.ToBase64String(envelope.Ciphertext).Contains(Plain, StringComparison.Ordinal),
            "T2 ciphertext encoding does not contain plaintext");
        Ok(secrets.OpenAsync(version.CredentialVersionId).GetAwaiter().GetResult() == Plain,
            "T3 correct key decrypts");
        Ok(Throws(ProductionCredentialSecretProtector.SecretImmutable, () =>
            secrets.PutAsync(version.CredentialVersionId, "gate-b-replaced").GetAwaiter().GetResult()),
            "T3 a stored secret is not silently replaced");

        var missing = new InMemoryProductionCredentialSecretStore(
            caller, identities, new MapProductionCredentialKeyRing(null, new Dictionary<string, byte[]>()));
        Ok(Throws(ProductionCredentialSecretProtector.KeyMissing, () =>
            missing.PutAsync(version.CredentialVersionId, Plain).GetAwaiter().GetResult()),
            "T4 missing key fails closed");
        Ok(Throws(ProductionCredentialSecretProtector.KeyInvalid, () =>
            ProductionCredentialSecretProtector.ParseKey("v1", Convert.ToBase64String(new byte[16]))),
            "T5 invalid key fails closed");

        var corrupt = new ProductionCredentialSecretEnvelope(
            envelope.Version, envelope.KeyId, envelope.Nonce, (byte[])envelope.Ciphertext.Clone(), (byte[])envelope.Tag.Clone());
        corrupt.Ciphertext[0] ^= 0xFF;
        Ok(Throws(ProductionCredentialSecretProtector.CiphertextInvalid, () =>
            ProductionCredentialSecretProtector.Decrypt(corrupt, new ProductionCredentialKey("v1", keyA))),
            "T6 corrupted ciphertext fails closed");
        Ok(Throws(ProductionCredentialSecretProtector.CiphertextInvalid, () =>
            ProductionCredentialSecretProtector.Decrypt(envelope, new ProductionCredentialKey("v1", keyB))),
            "T7 another key fails closed");

        Ok(envelope.KeyId == "v1" && envelope.Version == 1, "T8 envelope names the key version");
        Ok(!envelope.ToString().Contains(Plain, StringComparison.Ordinal)
           && !envelope.ToString().Contains(Convert.ToBase64String(keyA), StringComparison.Ordinal)
           && !new ProductionCredentialKey("v1", keyA).ToString().Contains(Convert.ToBase64String(keyA), StringComparison.Ordinal),
            "T8 envelope and key text do not expose the key or plaintext");

        Ok(ThrowsWithoutPlain(ProductionCredentialSecretProtector.CiphertextInvalid, () =>
            ProductionCredentialSecretProtector.Decrypt(corrupt, new ProductionCredentialKey("v1", keyA))),
            "T9 errors do not contain the secret");

        caller.Id = tenantB;
        Ok(Throws(ProductionCredentialSecretProtector.SecretUnavailable, () =>
            secrets.OpenAsync(version.CredentialVersionId).GetAwaiter().GetResult()),
            "T10 tenant B cannot open tenant A secret");

        caller.Id = tenantA;
        var version2 = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 2, "runway", "gen3");
        identities.AddVersionAsync(version2).GetAwaiter().GetResult();
        secrets.PutAsync(version2.CredentialVersionId, "gate-b-other-value").GetAwaiter().GetResult();
        Ok(secrets.OpenAsync(version.CredentialVersionId).GetAwaiter().GetResult() == Plain
           && secrets.OpenAsync(version2.CredentialVersionId).GetAwaiter().GetResult() == "gate-b-other-value",
            "T11 version boundary keeps each secret");

        var root = RepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "migrations", "378_pack_content_production_credential_secret.sql"));
        var ddl = string.Join('\n', sql.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        Ok(ddl.Contains("secret_ciphertext", StringComparison.Ordinal)
           && !ddl.Contains("secret_plaintext", StringComparison.Ordinal)
           && !ddl.Contains("org_settings", StringComparison.Ordinal)
           && !ddl.Contains("production_tasks", StringComparison.Ordinal)
           && !ddl.Contains("production_attempts", StringComparison.Ordinal)
           && !ddl.Contains("production_artifacts", StringComparison.Ordinal)
           && !ddl.Contains("production_bindings", StringComparison.Ordinal),
            "T12 migration stores ciphertext only on credential versions");

        var video = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "ContentVideoConfig.cs"));
        Ok(video.Contains("ResolveSecret", StringComparison.Ordinal)
           && !video.Contains("ProductionCredentialSecret", StringComparison.Ordinal),
            "T13 provider resolution is unchanged");
        var entities = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionEntities.cs"));
        Ok(entities.Contains("Guid? CredentialId", StringComparison.Ordinal)
           && entities.Contains("Guid? CredentialVersionId", StringComparison.Ordinal)
           && !entities.Contains("ApiKey", StringComparison.Ordinal)
           && !entities.Contains("secret_ciphertext", StringComparison.OrdinalIgnoreCase),
            "T14 snapshot pins non-secret credential identity and stores no secret");

        var protector = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCredentialSecret.cs"));
        var repository = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "ProductionCredentialSecretRepository.cs"));
        Ok(!protector.Contains("Log", StringComparison.Ordinal)
           && !repository.Contains("org_settings", StringComparison.Ordinal)
           && !repository.Contains("Log", StringComparison.Ordinal),
            "T9 storage path does not log or read legacy settings");

        Ok(Throws(ProductionCredentialSecretProtector.KeyMissing, () =>
            new MapProductionCredentialKeyRing("v2", new Dictionary<string, byte[]> { ["v2"] = keyB })
                .Require("v1")),
            "T8 missing key version fails closed without falling back");

        Ok(Throws(ProductionCredentialKeyBackupGate.BackupRequired, () =>
                ProductionCredentialKeyBackupGate.RequireOffMachine(null))
           && Throws(ProductionCredentialKeyBackupGate.BackupRequired, () =>
                ProductionCredentialKeyBackupGate.RequireOffMachine("false"))
           && Throws(ProductionCredentialKeyBackupGate.BackupRequired, () =>
                ProductionCredentialKeyBackupGate.RequireOffMachine("True")),
            "off-machine backup attestation fails closed");
        var attested = true;
        try
        {
            ProductionCredentialKeyBackupGate.RequireOffMachine("true");
        }
        catch (InvalidOperationException)
        {
            attested = false;
        }
        Ok(attested, "exact true is the only accepted backup attestation");

        var blockedGate = ProductionCredentialBackupGateStatus.FromConfirmation(null, "v1", null);
        Ok(blockedGate.WriteGate == ProductionCredentialBackupGateStatus.WriteBlocked
           && blockedGate.BlockCode == ProductionCredentialKeyBackupGate.BackupRequired
           && blockedGate.RestoreVerification == ProductionCredentialBackupGateStatus.RestoreUnverified
           && blockedGate.EncryptionKeyId == "v1",
            "missing backup confirmation blocks and does not claim restore");
        var wrongCaseGate = ProductionCredentialBackupGateStatus.FromConfirmation("True", "v1", null);
        Ok(wrongCaseGate.BlockCode == ProductionCredentialKeyBackupGate.BackupRequired,
            "backup confirmation rejects any value other than exact true");
        var openGate = ProductionCredentialBackupGateStatus.FromConfirmation("true", "v1", null);
        Ok(openGate.WriteGate == ProductionCredentialBackupGateStatus.WriteOpen
           && openGate.BlockCode is null
           && openGate.RestoreVerification == ProductionCredentialBackupGateStatus.RestoreUnverified,
            "exact confirmation opens the write gate without calling restore verified");
        var missingKeyGate = ProductionCredentialBackupGateStatus.FromConfirmation(
            "true", "v1", ProductionCredentialSecretProtector.KeyMissing);
        Ok(missingKeyGate.WriteGate == ProductionCredentialBackupGateStatus.WriteBlocked
           && missingKeyGate.BlockCode == ProductionCredentialSecretProtector.KeyMissing
           && missingKeyGate.EncryptionKeyId is null,
            "missing encryption key stays blocked and hides the key id");
        var visible = string.Join('|', openGate.WriteGate, openGate.RestoreVerification, openGate.EncryptionKeyId, openGate.BlockCode);
        Ok(!visible.Contains(Plain, StringComparison.Ordinal)
           && !visible.Contains("DataEncryptionKey", StringComparison.Ordinal),
            "backup gate view contains no secret material");
        var rotation = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "ProductionCredentialRotationRepository.cs"));
        Ok(repository.Contains("RequireBackupSatisfied()", StringComparison.Ordinal)
           && rotation.Contains("RequireBackupSatisfied()", StringComparison.Ordinal)
           && !rotation.Contains("RequireOffMachineBackup()", StringComparison.Ordinal),
            "credential registration still calls the backup gate");

        var di = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "ContentPackDependencyInjection.cs"));
        Ok(di.Contains("AddSingleton<IProductionCredentialKeyRing, ConfigurationProductionCredentialKeyRing>", StringComparison.Ordinal)
           && di.Contains("AddScoped<IProductionCredentialStore, ProductionCredentialRepository>", StringComparison.Ordinal)
           && di.Contains("AddScoped<IProductionCredentialSecretStore, ProductionCredentialSecretRepository>", StringComparison.Ordinal)
           && di.Contains("AddScoped<IProductionCredentialRotation, ProductionCredentialRotationRepository>", StringComparison.Ordinal)
           && di.Contains("AddScoped<IProductionCredentialRegistration, ProductionCredentialRegistrationService>", StringComparison.Ordinal)
           && di.Contains("AddScoped<IProductionCredentialResolver, ProductionCredentialResolver>", StringComparison.Ordinal)
           && di.Contains("AddScoped<IRunwayTransport, HttpRunwayTransport>", StringComparison.Ordinal)
           && !di.Contains("AddScoped<IRunwayTransport, DryRunRunwayTransport>", StringComparison.Ordinal),
            "DI keeps credential services and HTTP runway transport");

        var credentialManifest = File.ReadAllText(Path.Combine(
            root, "deploy", "ubuntu", "migration-files.production-credentials.txt"));
        var credentialFiles = credentialManifest.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
        Ok(credentialFiles.SequenceEqual(new[]
            {
                "377_pack_content_production_credential_domain.sql",
                "378_pack_content_production_credential_secret.sql",
                "379_pack_content_production_credential_freeze.sql",
            }),
            "credential manifest lists only 377, 378, and 379");
        var contentManifest = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "migration-files.content.txt"));
        Ok(!contentManifest.Contains("377_pack_content_production_credential_domain.sql", StringComparison.Ordinal)
           && !contentManifest.Contains("378_pack_content_production_credential_secret.sql", StringComparison.Ordinal)
           && !contentManifest.Contains("379_pack_content_production_credential_freeze.sql", StringComparison.Ordinal),
            "content manifest does not list credential migrations");
        var backup = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "backup-production-credential-key.sh"));
        Ok(backup.Contains("OFFMACHINE_DEST", StringComparison.Ordinal)
           && backup.Contains("OFFMACHINE_CONFIRMED", StringComparison.Ordinal)
           && backup.Contains("PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED", StringComparison.Ordinal)
           && backup.Contains("PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED", StringComparison.Ordinal)
           && backup.Contains("different directory", StringComparison.Ordinal)
           && backup.Contains("PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED", StringComparison.Ordinal)
           && backup.Contains("PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE", StringComparison.Ordinal)
           && !backup.Contains("exit 0", StringComparison.Ordinal),
            "backup script fails closed and does not treat a different directory as off-machine");
        var keyCheck = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "check-production-credential-key.sh"));
        Ok(keyCheck.Contains("PRODUCTION_CREDENTIAL_KEY_OK", StringComparison.Ordinal)
           && keyCheck.Contains("PRODUCTION_CREDENTIAL_KEY_INVALID", StringComparison.Ordinal)
           && keyCheck.Contains("PRODUCTION_CREDENTIAL_KEY_MISSING", StringComparison.Ordinal)
           && keyCheck.Contains("PRODUCTION_CREDENTIAL_KEY_RESTORE_OK", StringComparison.Ordinal)
           && keyCheck.Contains("PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH", StringComparison.Ordinal)
           && !keyCheck.Contains("echo \"$key_b64\"", StringComparison.Ordinal)
           && !keyCheck.Contains("echo \"$key_line\"", StringComparison.Ordinal),
            "key check validates size and restore without printing the key");
        var runner = File.ReadAllText(Path.Combine(root, "deploy", "ubuntu", "run-kit-migrations-production-credentials.sh"));
        var precondition = runner.IndexOf("PRODUCTION_CREDENTIAL_PRECONDITION_MISSING", StringComparison.Ordinal);
        var apply = runner.IndexOf("psql_run -v ON_ERROR_STOP=1 -f", StringComparison.Ordinal);
        Ok(precondition >= 0 && apply > precondition
           && runner.Contains("PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED", StringComparison.Ordinal)
           && runner.Contains("PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED", StringComparison.Ordinal)
           && runner.Contains("PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED", StringComparison.Ordinal)
           && runner.Contains("PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED", StringComparison.Ordinal)
           && runner.Contains("PRODUCTION_CREDENTIAL_MIGRATION_APPROVED", StringComparison.Ordinal)
           && runner.Contains("check-production-credential-key.sh", StringComparison.Ordinal),
            "migration runner stops before SQL when backup evidence or approval is missing");
        foreach (var relative in new[] { "src/KitPlatform.Api/appsettings.json", "src/KitPlatform.Api/appsettings.Development.json" })
        {
            var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                continue;
            Ok(!File.ReadAllText(path).Contains("\"OffMachineBackupConfirmed\"", StringComparison.Ordinal),
                "off-machine backup is not attested in " + relative);
        }

        return fail;
    }

    static byte[] NewKey()
    {
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    static MapProductionCredentialKeyRing Ring(string current, params (string Id, byte[] Key)[] keys) =>
        new(current, keys.ToDictionary(pair => pair.Id, pair => pair.Key));

    static bool Throws(string code, Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex) when (ex.Message == code)
        {
            return true;
        }
    }

    static bool ThrowsWithoutPlain(string code, Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex)
        {
            return ex.Message == code && !ex.ToString().Contains(Plain, StringComparison.Ordinal);
        }
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return @"E:\KitPlatform";
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
