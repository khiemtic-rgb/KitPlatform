using Microsoft.Extensions.DependencyInjection;

namespace KitPlatform.Packs.Sales.Infrastructure;

public static class SalesPackDependencyInjection
{
    public static IServiceCollection AddKitSalesPack(this IServiceCollection services)
    {
        services.AddScoped<IKitSalesDeskService, KitSalesDeskService>();
        return services;
    }
}
