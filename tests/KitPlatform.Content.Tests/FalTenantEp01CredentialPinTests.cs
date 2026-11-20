using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class FalTenantEp01CredentialPinTests
{
    static readonly Guid TenantA = Guid.Parse("11111111-1111-4111-8111-111111111111");
    static readonly Guid TenantB = Guid.Parse("22222222-2222-4222-8222-222222222222");
    static readonly Guid Build = Guid.Parse("2615b2da-da9e-486b-9372-8927784df40f");
    static readonly ProductionScope Sh01 = new(Build, "EP01", "SC01", "EP01-SC01-SH01");
    const string Secret = "fal-pin-02-test-secret";

    [Fact]
    public async Task Tenant_ep01_fal_freeze_pins_the_single_active_tenant_credential_without_secret()
    {
        var h = Open();
        var registered = await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", Secret, null));

        var snap = await FreezeAsync(h);

        Assert.Equal(registered.CredentialId, snap.CredentialId);
        Assert.Equal(registered.CredentialVersionId, snap.CredentialVersionId);
        Assert.Equal("fal", snap.ProviderId);
        Assert.Equal("sync-lipsync-v3", snap.ModelId);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(snap), StringComparison.Ordinal);
        Assert.DoesNotContain(typeof(ProductionTaskInputSnapshot).GetProperties(),
            p => p.Name is "Secret" or "Ciphertext" or "Nonce" or "Tag");

        var attempt = await h.Engine.StartAttemptAsync(snap.TaskId, snap.SnapshotHash);
        Assert.Equal("fal", attempt.ProviderId);
    }

    [Fact]
    public async Task Tenant_ep01_fal_without_credential_is_unresolved_before_start()
    {
        var h = Open();
        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => FreezeAsync(h));
        Assert.Equal(ProductionCredentialResolver.Unresolved, failed.Message);
    }

    [Fact]
    public async Task Tenant_ep01_fal_with_two_active_credentials_is_ambiguous_before_start()
    {
        var h = Open();
        await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand("fal", "sync-lipsync-v3", Secret, "a"));
        await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand("fal", "sync-lipsync-v3", Secret, "b"));
        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => FreezeAsync(h));
        Assert.Equal(ProductionCredentialResolver.Ambiguous, failed.Message);
    }

    [Fact]
    public async Task Tenant_ep01_fal_cannot_use_another_tenants_credential()
    {
        var h = Open();
        h.Caller.Id = TenantB;
        var foreign = await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", "tenant-b-secret", null));
        h.Caller.Id = TenantA;

        var implicitPin = await Assert.ThrowsAsync<InvalidOperationException>(() => FreezeAsync(h));
        Assert.Equal(ProductionCredentialResolver.Unresolved, implicitPin.Message);

        await Assert.ThrowsAsync<InvalidOperationException>(() => FreezeAsync(
            h, new ProductionCredentialReference(foreign.CredentialId, foreign.CredentialVersionId)));
    }

    [Fact]
    public async Task Start_attempt_revalidates_the_exact_pinned_fal_version()
    {
        var h = Open();
        await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand("fal", "sync-lipsync-v3", Secret, null));
        var superseded = await FreezeAsync(h);
        await h.Registration.RotateActiveAsync(new RotateProductionCredentialCommand("fal", "sync-lipsync-v3", "rotated-secret"));
        var kept = await h.Engine.StartAttemptAsync(superseded.TaskId, superseded.SnapshotHash);
        Assert.Equal("fal", kept.ProviderId);

        var current = await FreezeAsync(h);
        Assert.NotEqual(superseded.CredentialVersionId, current.CredentialVersionId);
        await h.Rotation.RevokeVersionAsync(current.CredentialVersionId!.Value);
        var revoked = await Assert.ThrowsAsync<InvalidOperationException>(
            () => h.Engine.StartAttemptAsync(current.TaskId, current.SnapshotHash));
        Assert.Equal(ProductionCredentialResolver.Revoked, revoked.Message);
    }

    [Fact]
    public async Task Fal_transport_uses_only_the_frozen_credential_secret()
    {
        var h = Open();
        var registered = await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", Secret, null));
        var handler = new RecordingHandler();
        var permits = await ConsumedPermitAsync();
        var sandbox = new RecordingSandboxPermit();
        var transport = HttpTransport(handler, h.Resolver, permits, sandbox);

        await Assert.ThrowsAsync<HttpRequestException>(() => transport.ExecuteOnceAsync(Request(
            new ProductionCredentialReference(registered.CredentialId, registered.CredentialVersionId))));

        Assert.NotEmpty(handler.Authorizations);
        Assert.All(handler.Authorizations, a => Assert.Equal($"Key {Secret}", a));
        Assert.Equal(0, sandbox.Calls);
        var rows = await permits.ListByTargetAsync(Build, Sh01.ShotCode, ProductionActions.CreateLipsync, "fal", "sync-lipsync-v3");
        Assert.Single(rows);
        Assert.Equal(ProductionExecutionPermitStatuses.Consumed, rows[0].Status);
    }

    [Fact]
    public async Task Fal_transport_without_consumed_permit_fails_before_http_and_never_uses_sandbox_permit()
    {
        var h = Open();
        var registered = await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand(
            "fal", "sync-lipsync-v3", Secret, null));
        var credential = new ProductionCredentialReference(registered.CredentialId, registered.CredentialVersionId);
        var handler = new RecordingHandler();
        var sandbox = new RecordingSandboxPermit();

        var none = HttpTransport(handler, h.Resolver, new InMemoryProductionExecutionPermitStore(), sandbox);
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => none.ExecuteOnceAsync(Request(credential)));
        Assert.Equal(ProductionFailureReasons.PaidNetworkForbidden, missing.Message);

        var issuedOnly = new InMemoryProductionExecutionPermitStore();
        await issuedOnly.IssueAsync(Permit(Build, Sh01.ShotCode));
        var unconsumed = HttpTransport(handler, h.Resolver, issuedOnly, sandbox);
        var notConsumed = await Assert.ThrowsAsync<InvalidOperationException>(() => unconsumed.ExecuteOnceAsync(Request(credential)));
        Assert.Equal(ProductionFailureReasons.PaidNetworkForbidden, notConsumed.Message);
        Assert.NotNull(await issuedOnly.GetIssuedAsync(Build, Sh01.ShotCode, ProductionActions.CreateLipsync, "fal", "sync-lipsync-v3"));

        var otherShot = new InMemoryProductionExecutionPermitStore();
        await otherShot.IssueAsync(Permit(Build, "EP01-SC01-SH02"));
        await otherShot.TryConsumeAsync(Build, "EP01-SC01-SH02", ProductionActions.CreateLipsync, "fal", "sync-lipsync-v3");
        var wrongShot = HttpTransport(handler, h.Resolver, otherShot, sandbox);
        await Assert.ThrowsAsync<InvalidOperationException>(() => wrongShot.ExecuteOnceAsync(Request(credential)));

        Assert.Empty(handler.Authorizations);
        Assert.Equal(0, sandbox.Calls);
        Assert.Equal(0, none.StartCount + unconsumed.StartCount + wrongShot.StartCount);
    }

    [Fact]
    public async Task Fal_transport_without_frozen_credential_is_unresolved_and_never_reaches_legacy_keys()
    {
        var h = Open();
        await h.Registration.RegisterAsync(new RegisterProductionCredentialCommand("fal", "sync-lipsync-v3", Secret, null));
        var handler = new RecordingHandler();
        var transport = HttpTransport(handler, h.Resolver, await ConsumedPermitAsync(), new RecordingSandboxPermit());

        var failed = await Assert.ThrowsAsync<InvalidOperationException>(() => transport.ExecuteOnceAsync(Request(null)));

        Assert.Equal(ProductionCredentialResolver.Unresolved, failed.Message);
        Assert.Empty(handler.Authorizations);
        Assert.Equal(0, transport.StartCount);
    }

    static async Task<ProductionTaskInputSnapshot> FreezeAsync(Harness h, ProductionCredentialReference? credential = null)
    {
        var request = await h.Engine.CreateRequestAsync(Sh01, ProductionActions.CreateLipsync, "test", Guid.NewGuid().ToString("N"));
        var task = await h.Engine.CreateTaskAsync(request.RequestId);
        return await h.Engine.FreezeInputAsync(
            task.TaskId, Input(), ProductionLipsyncCatalog.FalProviderId, ProductionLipsyncCatalog.SyncLipsyncV3,
            default, credential);
    }

    static ShotExecutionInput Input() =>
        new("Recovered line", "beat", "visual", "", "", "5", "", "",
            null, "", "16:9", 5, ShotExecutionCompiler.Version, null,
            "line-SC01-CHAR-003-1", "CHAR-003", "");

    static FalLipsyncTransportRequest Request(ProductionCredentialReference? credential)
    {
        Assert.True(ProductionLipsyncSpeakerTarget.TryResolveTenantSh01Linh(out var pin));
        var video = ProductionTestMp4.Encode("fal-pin-02-video", 5);
        var voice = new byte[64];
        voice[0] = 0x49; voice[1] = 0x44; voice[2] = 0x33;
        return new FalLipsyncTransportRequest(
            Guid.NewGuid(), Guid.NewGuid(), video, voice, pin, 5, "hash", Sh01, Sh01.ShotCode, null, null, credential);
    }

    static IssueProductionExecutionPermitCommand Permit(Guid build, string shot) =>
        new(build, shot, "EP01", "SC01", ProductionActions.CreateLipsync, "fal", "sync-lipsync-v3", "test", "fal-permit-02");

    static async Task<InMemoryProductionExecutionPermitStore> ConsumedPermitAsync()
    {
        var permits = new InMemoryProductionExecutionPermitStore();
        await permits.IssueAsync(Permit(Build, Sh01.ShotCode));
        Assert.True(await permits.TryConsumeAsync(Build, Sh01.ShotCode, ProductionActions.CreateLipsync, "fal", "sync-lipsync-v3"));
        return permits;
    }

    static IFalLipsyncTransport HttpTransport(
        HttpMessageHandler handler,
        IProductionCredentialResolver resolver,
        IProductionExecutionPermitStore permits,
        IProductionPaidNetworkPermit sandbox)
    {
        var assembly = Assembly.Load("KitPlatform.Packs.Content.Infrastructure");
        var clientType = assembly.GetType("KitPlatform.Packs.Content.Infrastructure.ContentFalClient", throwOnError: true)!;
        var transportType = assembly.GetType("KitPlatform.Packs.Content.Infrastructure.HttpFalLipsyncTransport", throwOnError: true)!;
        var client = Activator.CreateInstance(
            clientType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [new HttpClient(handler) { BaseAddress = new Uri("https://fal.invalid/") }, null, null, null, null], null)!;
        return (IFalLipsyncTransport)Activator.CreateInstance(
            transportType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            [client, sandbox, permits, new ProductionFalHttpAudit(), resolver], null)!;
    }

    static Harness Open()
    {
        var caller = new SwitchCaller { Id = TenantA };
        var key = new byte[ProductionCredentialSecretEnvelope.KeySize];
        RandomNumberGenerator.Fill(key);
        var ring = new MapProductionCredentialKeyRing("v1", new Dictionary<string, byte[]> { ["v1"] = key });
        var credentials = new InMemoryProductionCredentialStore(caller);
        var secrets = new InMemoryProductionCredentialSecretStore(caller, credentials, ring);
        var rotation = new InMemoryProductionCredentialRotation(credentials, secrets, ring);
        var resolver = new ProductionCredentialResolver(credentials, secrets, caller);
        var work = new InMemoryProductionWorkStore(caller);
        work.RegisterBuild(Build, TenantA);
        return new Harness(
            caller,
            ProductionWorkExecutionEngine.CreateOfficial(work, resolver),
            resolver,
            rotation,
            new ProductionCredentialRegistrationService(caller, credentials, rotation));
    }

    sealed record Harness(
        SwitchCaller Caller,
        ProductionWorkExecutionEngine Engine,
        IProductionCredentialResolver Resolver,
        IProductionCredentialRotation Rotation,
        IProductionCredentialRegistration Registration);

    sealed class SwitchCaller : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class RecordingSandboxPermit : IProductionPaidNetworkPermit
    {
        public int Calls { get; private set; }
        public int StartCount => 0;

        public bool IsEligible(ProductionScope scope, string shotCode, string providerId, string modelId)
        {
            Calls++;
            return false;
        }

        public bool TryConsumeStart(ProductionScope scope, string shotCode, string providerId, string modelId)
        {
            Calls++;
            return false;
        }
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Authorizations { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization is AuthenticationHeaderValue auth
                ? $"{auth.Scheme} {auth.Parameter}"
                : "");
            throw new HttpRequestException("FAL_PIN_02_NO_NETWORK");
        }
    }
}
