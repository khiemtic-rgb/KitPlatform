using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

public static class CarePackDependencyInjection
{
    public static IServiceCollection AddCarePack(this IServiceCollection services)
    {
        services.AddScoped<ICareOsOverviewService, CareOsOverviewService>();
        services.AddScoped<CareEventRepository>();
        services.AddScoped<ICareEventService, CareEventService>();
        services.AddScoped<CareCohortRepository>();
        services.AddScoped<ICareCohortService, CareCohortService>();
        services.AddScoped<CareKpiRepository>();
        services.AddScoped<ICareKpiService, CareKpiService>();
        return services;
    }
}
