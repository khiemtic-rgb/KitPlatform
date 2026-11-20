using System.Security.Cryptography;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// FAMIXA-FILM-PRODUCTION-35. Rotation leaves one ACTIVE version. No provider call and no live credential.
/// </summary>
public sealed class P35CredentialRotation
{
    static readonly Guid TenantA = Guid.Parse("4028941b-49b3-4ba2-8815-778daef4a2d3");
    const string SecretV1 = "p35-v1-secret-not-a-provider-key";
    const string SecretV2 = "p35-v2-secret-not-a-provider-key";
    const string SecretV3 = "p35-v3-secret-not-a-provider-key";
    static readonly ProductionScope Scope = new(
        Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb"), "EP01", "SC01", "EP01-SC01-SH01");

    [Fact]
    public async Task Rotation_supersedes_the_previous_version_and_new_freezes_use_the_active_one()
    {
        var harness = Open();
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", SecretV1, "runway"));
        var v1 = await harness.Store.GetVersionAsync(registered.CredentialVersionId);
        Assert.Equal(ProductionCredentialStatuses.Active, v1!.Status);

        var store = new InMemoryProductionWorkStore();
        var engine = ProductionWorkExecutionEngine.CreateOfficial(store, harness.Resolver);
        var frozen = await Freeze(engine);
        Assert.Equal(registered.CredentialVersionId, frozen.CredentialVersionId);

        var v2 = await harness.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
            registered.CredentialId, "runway", "gen4_turbo", SecretV2));
        var v1After = await harness.Store.GetVersionAsync(registered.CredentialVersionId);
        Assert.Equal(ProductionCredentialStatuses.Superseded, v1After!.Status);
        Assert.Equal(ProductionCredentialStatuses.Active, v2.Status);
        Assert.Equal(1, await ActiveCount(harness, registered.CredentialId));
        Assert.Equal(registered.CredentialVersionId, (await store.GetSnapshotByTaskAsync(frozen.TaskId))!.CredentialVersionId);

        var active = await harness.Resolver.ResolveActiveAsync(TenantA, "runway", "gen4_turbo");
        Assert.Equal(v2.CredentialVersionId, active.CredentialVersionId);
        Assert.Equal(SecretV2, active.Secret);
        Assert.DoesNotContain(SecretV2, active.ToString(), StringComparison.Ordinal);

        var historical = await harness.Resolver.ResolveAsync(
            registered.CredentialId, registered.CredentialVersionId, "runway", "gen4_turbo");
        Assert.Equal(registered.CredentialVersionId, historical.CredentialVersionId);
        Assert.Equal(SecretV1, historical.Secret);

        var next = await Freeze(engine);
        Assert.Equal(v2.CredentialVersionId, next.CredentialVersionId);
        Assert.DoesNotContain(SecretV1, next.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(SecretV2, next.ToString(), StringComparison.Ordinal);

        var v3 = await harness.Registration.AddVersionAsync(new AddProductionCredentialVersionCommand(
            registered.CredentialId, "runway", "gen4_turbo", SecretV3));
        Assert.Equal(ProductionCredentialStatuses.Superseded, (await harness.Store.GetVersionAsync(registered.CredentialVersionId))!.Status);
        Assert.Equal(ProductionCredentialStatuses.Superseded, (await harness.Store.GetVersionAsync(v2.CredentialVersionId))!.Status);
        Assert.Equal(ProductionCredentialStatuses.Active, v3.Status);
        Assert.Equal(1, await ActiveCount(harness, registered.CredentialId));
        Assert.Equal(v3.CredentialVersionId, (await harness.Resolver.ResolveActiveAsync(TenantA, "runway", "gen4_turbo")).CredentialVersionId);
        Assert.Equal(SecretV1, (await harness.Resolver.ResolveAsync(
            registered.CredentialId, registered.CredentialVersionId, "runway", "gen4_turbo")).Secret);
    }

    [Fact]
    public async Task A_failed_rotation_does_not_leave_the_previous_version_revoked()
    {
        var harness = Open();
        var registered = await harness.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "runway", "gen4_turbo", SecretV1, "runway"));
        var failed = false;
        try
        {
            await harness.Rotation.RotateAsync(
                registered.CredentialId, registered.CredentialVersionId, "runway", "gen4_turbo", SecretV2);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionCredentialOwnership.VersionExists)
        {
            failed = true;
        }
        Assert.True(failed);
        Assert.Equal(ProductionCredentialStatuses.Active, (await harness.Store.GetVersionAsync(registered.CredentialVersionId))!.Status);
        Assert.Equal(1, await ActiveCount(harness, registered.CredentialId));
    }

    [Fact]
    public void Prior_rotation_regression_still_passes()
    {
        var failed = ProductionCredentialRotationV1Regression.Run();
        Assert.True(failed.Count == 0, string.Join("; ", failed));
    }

    [Fact]
    public void Official_path_closure_still_passes()
    {
        var failed = ProductionCredentialOfficialPathClosureV1Regression.Run();
        Assert.True(failed.Count == 0, string.Join("; ", failed));
    }

    static async Task<int> ActiveCount(Harness harness, Guid credentialId)
    {
        var rows = await harness.Registration.ListAsync();
        return rows.Count(row => row.CredentialId == credentialId && row.Status == ProductionCredentialStatuses.Active);
    }

    static async Task<ProductionTaskInputSnapshot> Freeze(
        ProductionWorkExecutionEngine engine, ProductionCredentialReference? credential = null)
    {
        var request = await engine.CreateRequestAsync(
            Scope, ProductionActions.CreateVideo, "p35", "p35-" + Guid.NewGuid().ToString("N"));
        var task = await engine.CreateTaskAsync(request.RequestId);
        return await engine.FreezeInputAsync(
            task.TaskId,
            new ShotExecutionInput("story", "acting", "visual", "", "kf", "5", "", ""),
            "runway",
            "gen4_turbo",
            credential: credential);
    }

    static Harness Open()
    {
        var caller = new SwitchCaller { Id = TenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var store = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, store, ring);
        var rotation = new InMemoryProductionCredentialRotation(store, secrets, ring);
        return new Harness(
            store,
            rotation,
            new ProductionCredentialResolver(store, secrets, caller),
            new ProductionCredentialRegistrationService(caller, store, rotation));
    }

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed record Harness(
        InMemoryProductionCredentialStore Store,
        InMemoryProductionCredentialRotation Rotation,
        ProductionCredentialResolver Resolver,
        ProductionCredentialRegistrationService Registration);
}
