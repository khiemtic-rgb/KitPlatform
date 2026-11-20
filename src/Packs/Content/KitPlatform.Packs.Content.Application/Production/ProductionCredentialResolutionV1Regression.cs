using System.Security.Cryptography;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// PRODUCTION-CREDENTIAL-RESOLUTION-01. Explicit credential and version only.
/// No provider call. No latest-version rule.
/// </summary>
public static class ProductionCredentialResolutionV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_CREDENTIAL_RESOLUTION_01";
    const string Plain = "gate-c-plain-value";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var tenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var caller = new SwitchCaller { Id = tenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var identities = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, identities, ring);
        var resolver = new ProductionCredentialResolver(identities, secrets, caller);
        var credential = ProductionCredentialOwnership.Create(Guid.NewGuid(), tenantA, "tenant-a");
        identities.CreateAsync(credential).GetAwaiter().GetResult();
        var version = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 1, "runway", "gen4_turbo");
        identities.AddVersionAsync(version).GetAwaiter().GetResult();
        var other = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 2, "runway", "gen4_turbo");
        identities.AddVersionAsync(other).GetAwaiter().GetResult();
        secrets.PutAsync(version.CredentialVersionId, Plain).GetAwaiter().GetResult();
        secrets.PutAsync(other.CredentialVersionId, "gate-c-other-value").GetAwaiter().GetResult();

        var material = resolver.ResolveAsync(
            credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult();
        Ok(material.Secret == Plain && material.CredentialVersionId == version.CredentialVersionId,
            "T1 tenant A resolves its own version");
        Ok(!material.ToString().Contains(Plain, StringComparison.Ordinal), "T14 material text hides the secret");

        caller.Id = tenantB;
        Ok(Throws(ProductionCredentialSecretProtector.SecretUnavailable, () =>
            resolver.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "T2 tenant B cannot resolve tenant A");
        caller.Id = tenantA;
        Ok(Throws(ProductionCredentialResolver.Unresolved, () =>
            resolver.ResolveAsync(Guid.Empty, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "T3 missing credential id fails closed");
        caller.Id = Guid.Empty;
        Ok(Throws(ProductionTenantBoundary.Required, () =>
            resolver.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "T4 missing caller tenant fails closed");
        caller.Id = tenantA;

        var revoked = credential with { Status = ProductionCredentialStatuses.Revoked, DisplayName = "revoked-copy" };
        var revokedId = Guid.NewGuid();
        identities.CreateAsync(revoked with { CredentialId = revokedId }).GetAwaiter().GetResult();
        var revokedVersion = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), revokedId, tenantA, 1, "runway", "gen4_turbo");
        identities.AddVersionAsync(revokedVersion).GetAwaiter().GetResult();
        Ok(Throws(ProductionCredentialResolver.Revoked, () =>
            resolver.ResolveAsync(revokedId, revokedVersion.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "T5 revoked credential fails closed");

        var bare = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 3, "fal", "sync");
        identities.AddVersionAsync(bare).GetAwaiter().GetResult();
        Ok(Throws(ProductionCredentialSecretProtector.SecretUnavailable, () =>
            resolver.ResolveAsync(credential.CredentialId, bare.CredentialVersionId, "fal", "sync").GetAwaiter().GetResult()),
            "T6 missing secret fails closed");
        Ok(Throws(ProductionCredentialResolver.ProviderMismatch, () =>
            resolver.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "fal", "gen4_turbo").GetAwaiter().GetResult()),
            "T9 wrong provider fails closed");
        Ok(Throws(ProductionCredentialResolver.ModelMismatch, () =>
            resolver.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "runway", "other-model").GetAwaiter().GetResult()),
            "T10 wrong model fails closed");
        var openedOther = resolver.ResolveAsync(
            credential.CredentialId, other.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult();
        Ok(openedOther.Secret == "gate-c-other-value" && openedOther.Secret != Plain,
            "T11 explicit version is not replaced by another version");

        identities.SupersedeActiveVersions(credential.CredentialId);
        var superseded = resolver.ResolveAsync(
            credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult();
        Ok(superseded.CredentialVersionId == version.CredentialVersionId && superseded.Secret == Plain,
            "SUPERSEDED explicit pin still opens");
        Ok(Throws(ProductionCredentialResolver.Unresolved, () =>
            resolver.ResolveActiveAsync(tenantA, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "new execution does not select a SUPERSEDED version");

        var broken = new ProductionCredentialResolver(identities, new BrokenSecrets(), caller);
        Ok(Throws(ProductionCredentialSecretProtector.CiphertextInvalid, () =>
            broken.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "T8 invalid ciphertext fails closed");

        var newer = ProductionCredentialOwnership.CreateVersion(
            Guid.NewGuid(), credential.CredentialId, tenantA, 4, "runway", "gen4_turbo");
        identities.AddVersionAsync(newer).GetAwaiter().GetResult();
        secrets.PutAsync(newer.CredentialVersionId, "gate-c-newer-value").GetAwaiter().GetResult();
        identities.RevokeVersionAsync(version.CredentialVersionId).GetAwaiter().GetResult();
        var opens = 0;
        var spy = new ProductionCredentialResolver(identities, new CountingSecrets(secrets, () => opens++), caller);
        Ok(Throws(ProductionCredentialResolver.Revoked, () =>
                spy.ResolveAsync(credential.CredentialId, version.CredentialVersionId, "runway", "gen4_turbo").GetAwaiter().GetResult())
           && opens == 0,
            "REVOKED is rejected before decrypt");

        Ok(Throws(ProductionCredentialResolver.Unresolved, () =>
            ProductionCredentialResolver.RequireAsync(resolver, null, "runway", "gen4_turbo").GetAwaiter().GetResult()),
            "T3 missing reference fails closed");

        var root = RepoRoot();
        foreach (var file in new[]
        {
            "HttpRunwayTransport.cs",
            "HttpFalLipsyncTransport.cs",
            "HttpElevenLabsVoiceTransport.cs",
        })
        {
            var text = File.ReadAllText(Path.Combine(
                root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production", file));
            Ok(!text.Contains("ResolveAsync", StringComparison.Ordinal)
               && !text.Contains("org_settings", StringComparison.Ordinal)
               && !text.Contains("Content:Secrets", StringComparison.Ordinal)
               && text.Contains("ProductionCredentialResolver.RequireAsync", StringComparison.Ordinal),
                "T12 " + file + " does not resolve a legacy secret");
        }

        var veo = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production",
            "OfficialVeoPollRecoverMaterializationLiveV1.cs"));
        var veoStart = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure", "Production",
            "OfficialVeoRealVertexStartLiveV1.cs"));
        Ok(!veo.Contains("org_settings", StringComparison.Ordinal)
           && !veoStart.Contains("org_settings", StringComparison.Ordinal),
            "T12 official Veo live loaders do not read org_settings");

        var freeze = File.ReadAllText(Path.Combine(
            root, "src", "Packs", "Content", "KitPlatform.Packs.Content.Application", "Production", "ProductionEntities.cs"));
        Ok(freeze.Contains("Guid? CredentialId", StringComparison.Ordinal)
           && freeze.Contains("Guid? CredentialVersionId", StringComparison.Ordinal)
           && !freeze.Contains("ApiKey", StringComparison.Ordinal)
           && !freeze.Contains("secret_ciphertext", StringComparison.OrdinalIgnoreCase),
            "T16 freeze record stores non-secret credential identity only");
        Ok(ProductionCredentialDomainV1Regression.Run().Count == 0, "T16 gate A");
        Ok(ProductionCredentialSecretV1Regression.Run().Count == 0, "T17 gate B");
        return fail;
    }

    static bool Throws(string code, Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception ex) when (ex.Message == code && !ex.ToString().Contains(Plain, StringComparison.Ordinal))
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

    sealed class CountingSecrets(IProductionCredentialSecretStore inner, Action opened) : IProductionCredentialSecretStore
    {
        public Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default) =>
            inner.PutAsync(credentialVersionId, secret, ct);

        public Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default)
        {
            opened();
            return inner.OpenAsync(credentialVersionId, ct);
        }
    }

    sealed class BrokenSecrets : IProductionCredentialSecretStore
    {
        public Task PutAsync(Guid credentialVersionId, string secret, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<string> OpenAsync(Guid credentialVersionId, CancellationToken ct = default) =>
            throw new InvalidOperationException(ProductionCredentialSecretProtector.CiphertextInvalid);
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }
}
