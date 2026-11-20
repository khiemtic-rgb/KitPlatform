using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class FamixaVeoLiveWiringTests
{
    [Fact]
    public void Production_os_wires_the_real_veo_client_and_does_not_call_a_provider()
    {
        var services = new ServiceCollection();
        services.AddContentPack();
        services.AddScoped<IProductionCredentialResolver, UnusedCredentialResolver>();
        services.AddSingleton<IDbConnectionFactory, UnusedDb>();
        services.AddSingleton<ITenantContext, UnusedTenant>();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var authenticator = Assert.IsType<ProductionVeoHttpAuthenticator>(
            provider.GetRequiredService<IVeoHttpAuthenticator>());
        Assert.True(authenticator.TokenExchangeConfigured);
        Assert.Equal(0, authenticator.GetAccessTokenCalls);
        Assert.Null(authenticator.AcceptedCredentialId);
        Assert.IsType<HttpVeoJwtBearerTokenClient>(provider.GetRequiredService<IVeoJwtBearerTokenClient>());

        var registered = services
            .Where(d => d.ServiceType == typeof(IProductionCredentialResolver))
            .Select(d => d.ImplementationType)
            .ToArray();
        Assert.Contains(typeof(ProductionCredentialResolver), registered);

        using var scope = provider.CreateScope();
        var transport = Assert.IsType<HttpVeoMotionTransport>(
            scope.ServiceProvider.GetRequiredService<IVeoMotionTransport>());
        Assert.Equal(0, transport.HttpCallCount);
        Assert.True(transport.RequiresNetwork);
        var adapters = scope.ServiceProvider.GetServices<IMotionExecutionAdapter>().ToArray();
        var veo = Assert.Single(adapters, a => a is VeoMotionExecutionAdapter);
        Assert.Equal(ProductionMotionCatalog.GoogleProviderId, veo.ProviderId);
        Assert.Equal(ProductionMotionCatalog.Veo31ModelId, veo.ModelId);
        Assert.True(veo.RequiresNetwork);
        Assert.IsType<FixtureMotionAdapter>(adapters[^1]);
        Assert.False(adapters[^1].RequiresNetwork);
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(FakeVeoHttpAuthenticator));
        Assert.DoesNotContain(services, d => d.ImplementationType == typeof(FakeVeoMotionTransport));
        Assert.False(ProductionVeoReadinessComposition.RealVertexExecutionOpen);
        Assert.False(OfficialVeoProductionAuthenticatorV1.ProductionDiRegistered);
    }

    sealed class UnusedCredentialResolver : IProductionCredentialResolver
    {
        public Task<ProductionCredentialMaterial> ResolveAsync(
            Guid credentialId, Guid credentialVersionId, string providerId, string modelId, CancellationToken ct = default)
            => throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
    }

    sealed class UnusedDb : IDbConnectionFactory
    {
        public Task<Npgsql.NpgsqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("DB_NOT_USED");
    }

    sealed class UnusedTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => false;
        public Guid? WorkspaceId => null;
    }
}
