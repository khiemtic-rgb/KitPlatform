using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

public static class LocalOsPackDependencyInjection
{
    public static IServiceCollection AddLocalOsPack(this IServiceCollection services)
    {
        services.AddScoped<ILocalOsListingService, LocalOsListingService>();
        services.AddScoped<ILocalOsReportService, LocalOsReportService>();
        services.AddScoped<ILocalOsPublisherService, LocalOsPublisherService>();
        services.AddScoped<ILocalOsIngestService, LocalOsIngestService>();
        services.AddScoped<ILocalOsSourceService, LocalOsSourceService>();
        services.AddScoped<ILocalOsWatchService, LocalOsWatchService>();
        services.AddHttpClient<ILocalOsRewriteService, LocalOsRewriteService>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(45);
        });
        services.AddHostedService<LocalOsWatchWorker>();
        return services;
    }
}
