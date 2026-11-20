using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-LEGACY-MIGRATION-01.
/// Only an explicitly proven tenant-owned credential can be copied into the credential domain.
/// </summary>
public static class ProductionCredentialLegacyMigrationV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_LEGACY_MIGRATION_01";
    const string Secret = "gate-f-owned-secret";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        try
        {
            RunAsync(Ok).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }

        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var inventory = ProductionCredentialLegacyCatalog.Inventory();
        Ok(inventory.Count > 0 && ProductionCredentialLegacyCatalog.EligibleCount() == 0
           && inventory.All(row => row.Class != ProductionCredentialLegacyClass.ProvablyTenantOwned),
            "T0 no inventoried legacy source is provably tenant-owned");

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var store = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, store, ring);
        var rotation = new InMemoryProductionCredentialRotation(store, secrets, ring);
        var migration = new InMemoryProductionCredentialLegacyMigration(store, rotation, caller);
        var resolver = new ProductionCredentialResolver(store, secrets, caller);
        var credentialId = Guid.Parse("380f0000-0000-7000-8000-0000000000f1");
        var versionId = Guid.Parse("380f0000-0000-7000-8000-0000000000f2");
        var owned = new ProductionCredentialLegacyEvidence(
            ProductionCredentialLegacyClass.ProvablyTenantOwned,
            tenantA, credentialId, versionId, "runway", "gen4_turbo", "synthetic-proven");

        var created = await migration.MigrateAsync(owned, Secret);
        var loaded = await store.GetAsync(credentialId);
        var version = await store.GetVersionAsync(versionId);
        Ok(loaded?.TenantId == tenantA && loaded.CredentialId == credentialId,
            "T1 T2 migrated credential belongs to the proven tenant");
        Ok(version?.CredentialId == credentialId && version.TenantId == tenantA && version.CredentialVersionId == versionId,
            "T3 migrated version keeps credential and tenant");
        var envelope = secrets.EnvelopeForTests(versionId);
        Ok(envelope is { Ciphertext.Length: > 0 }
           && !System.Text.Encoding.UTF8.GetString(envelope.Ciphertext).Contains(Secret, StringComparison.Ordinal)
           && created.ToString().IndexOf(Secret, StringComparison.Ordinal) < 0,
            "T4 T12 secret is stored only as ciphertext");

        var material = await resolver.ResolveAsync(credentialId, versionId, "runway", "gen4_turbo");
        Ok(material.Secret == Secret && material.CredentialVersionId == versionId
           && material.ToString().IndexOf(Secret, StringComparison.Ordinal) < 0,
            "T5 owning tenant can resolve the migrated credential");

        caller.Id = tenantB;
        var denied = false;
        try
        {
            await resolver.ResolveAsync(credentialId, versionId, "runway", "gen4_turbo");
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.SecretUnavailable
            && ex.ToString().IndexOf(Secret, StringComparison.Ordinal) < 0)
        {
            denied = true;
        }
        Ok(denied, "T6 other tenant cannot resolve the migrated credential");
        caller.Id = tenantA;

        var again = await migration.MigrateAsync(owned, Secret);
        Ok(again.CredentialVersionId == versionId && store.CredentialCount == 1 && store.VersionCount == 1,
            "T7 rerun does not create a duplicate credential");

        await Refuse(migration, store, ProductionCredentialLegacyClass.MultiTenantShared, "T8 shared credential is not migrated");
        await Refuse(migration, store, ProductionCredentialLegacyClass.UnscopedUnknown, "T9 unscoped credential is not migrated");
        await Refuse(migration, store, ProductionCredentialLegacyClass.NonProduction, "T10 non-production credential is not migrated");
        await Refuse(migration, store, ProductionCredentialLegacyClass.AlreadyMigrated, "T10b already-migrated evidence creates nothing");
        Ok(store.CredentialCount == 1 && store.VersionCount == 1, "T8 T9 T10 refusal leaves the proven credential unchanged");

        var broken = new MapProductionCredentialKeyRing("missing", new Dictionary<string, byte[]>());
        var brokenSecrets = new InMemoryProductionCredentialSecretStore(caller, store, broken);
        var brokenRotation = new InMemoryProductionCredentialRotation(store, brokenSecrets, broken);
        var brokenMigration = new InMemoryProductionCredentialLegacyMigration(store, brokenRotation, caller);
        var partialId = Guid.Parse("380f0000-0000-7000-8000-0000000000f3");
        var partial = owned with { CredentialId = partialId, CredentialVersionId = Guid.Parse("380f0000-0000-7000-8000-0000000000f4") };
        var keyMissing = false;
        try
        {
            await brokenMigration.MigrateAsync(partial, Secret);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialSecretProtector.KeyMissing)
        {
            keyMissing = true;
        }
        Ok(keyMissing && await store.GetAsync(partialId) is null,
            "T12 failed encryption leaves no credential");

        var root = RepoRoot();
        var migrationSource = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCredentialLegacyMigration.cs"));
        var repositorySource = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", "ProductionCredentialLegacyMigrationRepository.cs"));
        var engine = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionWorkExecutionEngine.cs"));
        var resolution = File.ReadAllText(Path.Combine(root,
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionCredentialResolution.cs"));
        Ok(!migrationSource.Contains("FROM pack_content.org_settings", StringComparison.Ordinal)
           && !repositorySource.Contains("org_settings", StringComparison.Ordinal)
           && !repositorySource.Contains("GetEnvironmentVariable", StringComparison.Ordinal)
           && !migrationSource.Contains("production_task_input_snapshots", StringComparison.Ordinal)
           && !engine.Contains("org_settings", StringComparison.Ordinal)
           && !resolution.Contains("org_settings", StringComparison.Ordinal)
           && !resolution.Contains("ContentVideoConfig", StringComparison.Ordinal),
            "T13 T14 official production and migration do not read org_settings or rewrite snapshots");

        static async Task Refuse(
            InMemoryProductionCredentialLegacyMigration migration,
            InMemoryProductionCredentialStore store,
            ProductionCredentialLegacyClass classification,
            string name)
        {
            var before = store.CredentialCount;
            var evidence = new ProductionCredentialLegacyEvidence(
                classification,
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1"),
                Guid.NewGuid(),
                Guid.NewGuid(),
                "runway",
                "gen4_turbo",
                "refused");
            var refused = false;
            try
            {
                await migration.MigrateAsync(evidence, "gate-f-refused-secret");
            }
            catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialLegacyCatalog.NotEligible
                && ex.ToString().IndexOf("gate-f-refused-secret", StringComparison.Ordinal) < 0)
            {
                refused = true;
            }
            if (!refused || store.CredentialCount != before)
                throw new InvalidOperationException(name);
        }
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.Api.csproj"))
                || File.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api", "KitPlatform.Api.csproj")))
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
