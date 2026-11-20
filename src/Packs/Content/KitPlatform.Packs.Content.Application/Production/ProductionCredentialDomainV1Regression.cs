using System.Reflection;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-DOMAIN-01. Gate A ownership only.
/// No provider call. No org_settings copy. No secret value.
/// </summary>
public static class ProductionCredentialDomainV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_DOMAIN_01";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller();
        var store = new InMemoryProductionCredentialStore(caller);

        caller.Id = Guid.Empty;
        Ok(Throws(ProductionTenantBoundary.Required, () =>
            store.CreateAsync(ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "runway-a")).GetAwaiter().GetResult()),
            "T1 missing caller tenant fails closed");

        Ok(Throws(ProductionTenantBoundary.Required, () =>
            ProductionCredentialOwnership.Create(Guid.NewGuid(), Guid.Empty, "no-tenant")),
            "T1 credential requires TenantId");

        caller.Id = tenantA;
        Ok(Throws(ProductionCredentialOwnership.VersionCredentialRequired, () =>
            store.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
                Guid.NewGuid(), Guid.NewGuid(), tenantA, 1, "runway", "gen3")).GetAwaiter().GetResult()),
            "T2 version requires an existing credential");

        var credentialA = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "tenant-a-runway");
        store.CreateAsync(credentialA).GetAwaiter().GetResult();
        var versionA = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credentialA.CredentialId, tenantA, 1, "runway", "gen3");
        store.AddVersionAsync(versionA).GetAwaiter().GetResult();
        Ok(store.GetAsync(credentialA.CredentialId).GetAwaiter().GetResult()?.TenantId == tenantA,
            "T3 credential belongs to tenant A");
        Ok(store.GetVersionAsync(versionA.CredentialVersionId).GetAwaiter().GetResult()?.CredentialId == credentialA.CredentialId,
            "T3 version belongs to that credential");

        Ok(Throws(ProductionTenantBoundary.Forbidden, () =>
            store.CreateAsync(ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantB, "written-by-a")).GetAwaiter().GetResult()),
            "T4 caller A cannot create a credential for tenant B");

        caller.Id = tenantB;
        Ok(store.GetAsync(credentialA.CredentialId).GetAwaiter().GetResult() is null, "T4 tenant B cannot read tenant A credential");
        Ok(store.GetVersionAsync(versionA.CredentialVersionId).GetAwaiter().GetResult() is null, "T4 tenant B cannot read tenant A version");
        Ok(Throws(ProductionTenantBoundary.Immutable, () =>
            store.CreateAsync(credentialA with { TenantId = tenantB, DisplayName = "stolen" }).GetAwaiter().GetResult()),
            "T4 credential identity cannot be retaken by another tenant");

        caller.Id = tenantA;
        var sameCapability = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "tenant-a-runway-2");
        store.CreateAsync(sameCapability).GetAwaiter().GetResult();
        store.AddVersionAsync(ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), sameCapability.CredentialId, tenantA, 1, "runway", "gen3")).GetAwaiter().GetResult();
        Ok(sameCapability.CredentialId != credentialA.CredentialId, "T5 credential identity is not ProviderId or ModelId");
        Ok(versionA.ProviderId == "runway" && versionA.CredentialId != Guid.Parse("00000000-0000-0000-0000-000000000000"),
            "T5 provider metadata stays distinct from credential identity");

        Ok(Throws(ProductionCredentialOwnership.SecretForbidden, () =>
            ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "sk-test-secret")),
            "T6 plaintext secret label is rejected");
        Ok(!typeof(ProductionCredential).GetProperties().Any(IsSecretProperty)
           && !typeof(ProductionCredentialVersion).GetProperties().Any(IsSecretProperty),
            "T6 domain model has no secret property");

        Ok(!typeof(ProductionCredential).GetProperties().Any(p => p.Name.Contains("Workspace", StringComparison.Ordinal))
           && !typeof(ProductionCredentialVersion).GetProperties().Any(p => p.Name.Contains("Workspace", StringComparison.Ordinal)),
            "T7 no Workspace ownership");
        Ok(!typeof(ProductionCredential).GetProperties().Any(p => p.Name.Contains("Pack", StringComparison.Ordinal))
           && !typeof(ProductionCredentialVersion).GetProperties().Any(p => p.Name.Contains("Pack", StringComparison.Ordinal)),
            "T8 no Product Pack ownership");

        Ok(Throws(ProductionTenantBoundary.Immutable, () =>
            ProductionCredentialOwnership.RejectOwnerChange(credentialA.TenantId, tenantB)),
            "T3 ownership cannot change");
        Ok(Throws(ProductionCredentialOwnership.VersionOwnerImmutable, () =>
            ProductionCredentialOwnership.RejectVersionReassignment(credentialA.CredentialId, sameCapability.CredentialId)),
            "T2 version cannot move to another credential");

        var root = RepoRoot();
        var sql = File.ReadAllText(Path.Combine(root, "migrations", "377_pack_content_production_credential_domain.sql"));
        Ok(sql.Contains("UUID NOT NULL REFERENCES public.tenants (id)", StringComparison.Ordinal)
           && sql.Contains("fk_prod_credential_version_owner", StringComparison.Ordinal)
           && sql.Contains("PRODUCTION_TENANT_IMMUTABLE", StringComparison.Ordinal)
           && sql.Contains("PRODUCTION_CREDENTIAL_VERSION_OWNER_IMMUTABLE", StringComparison.Ordinal),
            "T3 schema enforces tenant ownership and version parent");
        Ok(sql.Contains("CHECK (status IN ('ACTIVE', 'SUPERSEDED', 'REVOKED'))", StringComparison.Ordinal)
           && sql.Contains("ck_prod_credential_status CHECK (status IN ('ACTIVE', 'REVOKED'))", StringComparison.Ordinal),
            "T3 version status is ACTIVE, SUPERSEDED, or REVOKED");
        var ddl = string.Join('\n', sql.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        Ok(!ddl.Contains("api_key", StringComparison.OrdinalIgnoreCase)
           && !ddl.Contains("ciphertext", StringComparison.OrdinalIgnoreCase)
           && !ddl.Contains("workspace_id", StringComparison.OrdinalIgnoreCase)
           && !ddl.Contains("product_pack", StringComparison.OrdinalIgnoreCase)
           && !ddl.Contains("org_settings", StringComparison.Ordinal)
           && !ddl.Contains("production_tasks", StringComparison.Ordinal)
           && !ddl.Contains("production_attempts", StringComparison.Ordinal)
           && !ddl.Contains("production_artifacts", StringComparison.Ordinal)
           && !ddl.Contains("production_bindings", StringComparison.Ordinal),
            "T9 schema does not add secrets or alter production records or org_settings");

        var video = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "ContentVideoConfig.cs"));
        Ok(video.Contains("ResolveSecret", StringComparison.Ordinal)
           && !video.Contains("ProductionCredential", StringComparison.Ordinal),
            "T10 provider resolution is untouched");
        var entities = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionEntities.cs"));
        Ok(entities.Contains("Guid? CredentialId", StringComparison.Ordinal)
           && entities.Contains("Guid? CredentialVersionId", StringComparison.Ordinal)
           && !entities.Contains("ApiKey", StringComparison.Ordinal)
           && !entities.Contains("secret_ciphertext", StringComparison.OrdinalIgnoreCase)
           && !entities.Contains("Plaintext", StringComparison.Ordinal),
            "T10 snapshot pins non-secret credential identity and stores no secret");

        return fail;
    }

    static bool IsSecretProperty(PropertyInfo property)
    {
        var name = property.Name;
        return name.Contains("Secret", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Token", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Password", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Cipher", StringComparison.OrdinalIgnoreCase);
    }

    static bool Throws(string code, Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex) when (ex.Message == code || ex.InnerException?.Message == code)
        {
            return true;
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
