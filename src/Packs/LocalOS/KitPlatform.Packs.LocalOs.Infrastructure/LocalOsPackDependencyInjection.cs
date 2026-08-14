using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.LocalOs;

namespace KitPlatform.Packs.LocalOs.Infrastructure;

public static class LocalOsPackDependencyInjection
{
    public static IServiceCollection AddLocalOsPack(this IServiceCollection services)
    {
        services.AddScoped<ILocalOsListingService, LocalOsListingService>();
        services.AddScoped<ILocalOsPublisherService, LocalOsPublisherService>();
        return services;
    }
}
