using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

public static class SalesPackDependencyInjection
{
    public static IServiceCollection AddKitSalesPack(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
            services.Configure<KitSalesFacebookOptions>(configuration.GetSection(KitSalesFacebookOptions.Section));

        services.AddHttpClient("kit-sales-facebook", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("kit-sales-gemini", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<KitSalesGeminiVoice>(c => c.Timeout = TimeSpan.FromSeconds(25));
        services.AddScoped<IKitSalesDeskService, KitSalesDeskService>();
        services.AddScoped<IKitSalesPainService, KitSalesPainService>();
        services.AddScoped<KitSalesFacebookSettingsService>();
        services.AddScoped<IKitSalesFacebookSettings>(sp => sp.GetRequiredService<KitSalesFacebookSettingsService>());
        services.AddScoped<KitSalesAiSettingsService>();
        services.AddScoped<IKitSalesAiSettings>(sp => sp.GetRequiredService<KitSalesAiSettingsService>());
        services.AddScoped<KitSalesFacebookMessengerClient>();
        services.AddScoped<IKitSalesChatService, KitSalesChatService>();
        return services;
    }
}
