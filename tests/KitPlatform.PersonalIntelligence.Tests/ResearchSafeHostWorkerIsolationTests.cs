using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KitPlatform.Api.Authorization;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchSafeHostWorkerIsolationTests
{
    [Fact]
    public void Unset_flag_keeps_background_writers()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddHostedService<ProbeWriter>();

        Assert.False(ResearchSafeHostWorkerIsolation.IsEnabled(configuration));
        ResearchSafeHostWorkerIsolation.Apply(configuration, services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void Explicit_false_keeps_background_writers()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ResearchSafeHostWorkerIsolation.ConfigurationKey] = "false",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddHostedService<ProbeWriter>();

        Assert.False(ResearchSafeHostWorkerIsolation.IsEnabled(configuration));
        ResearchSafeHostWorkerIsolation.Apply(configuration, services);

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
    }

    [Fact]
    public void Suppress_flag_removes_every_registered_hosted_service()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [ResearchSafeHostWorkerIsolation.ConfigurationKey] = "true",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddHostedService<ProbeWriter>();
        services.AddHostedService<SecondProbeWriter>();
        services.AddSingleton<string>("kept");

        Assert.True(ResearchSafeHostWorkerIsolation.IsEnabled(configuration));
        ResearchSafeHostWorkerIsolation.Apply(configuration, services);

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(string));
    }

    sealed class ProbeWriter : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    sealed class SecondProbeWriter : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
