using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.TestPack;

namespace KitPlatform.Packs.TestPack.Infrastructure;

public static class TestPackDependencyInjection
{
    public static IServiceCollection AddTestPack(this IServiceCollection services)
    {
        services.AddScoped<ITestPackProbe, SqlTestPackProbe>();
        return services;
    }
}
