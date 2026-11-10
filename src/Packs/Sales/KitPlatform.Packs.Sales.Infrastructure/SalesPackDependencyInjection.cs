using System.Net.Sockets;
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

        services.AddHttpClient("kit-sales-osm", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(25);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("KitPlatformSalesDiscovery/1.0 (KIT SALES; public pharmacy POI)");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        });
        services.AddHttpClient("kit-sales-facebook", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("kit-sales-gemini", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<KitSalesGeminiVoice>(c => c.Timeout = TimeSpan.FromSeconds(25));
        services.AddScoped<IKitSalesDeskService, KitSalesDeskService>();
        services.AddScoped<IKitSalesPainService, KitSalesPainService>();
        services.AddScoped<KitSalesFacebookSettingsService>();
        services.AddScoped<IKitSalesFacebookSettings>(sp => sp.GetRequiredService<KitSalesFacebookSettingsService>());
        services.AddScoped<KitSalesAiSettingsService>();
        services.AddScoped<IKitSalesAiSettings>(sp => sp.GetRequiredService<KitSalesAiSettingsService>());
        services.AddScoped<IKitSalesAiGate>(sp => sp.GetRequiredService<KitSalesAiSettingsService>());
        services.AddScoped<KitSalesAssessmentLookup>();
        services.AddScoped<IFacebookOutboundGate, FacebookOutboundGate>();
        services.AddScoped<IKitSalesAgentService, KitSalesAgentService>();
        services.AddScoped<IKitSalesAgentSweep, KitSalesAgentSweep>();
        services.AddScoped<IKitSalesProspectDiscovery, KitSalesProspectDiscovery>();
        services.AddScoped<IKitSalesChatService, KitSalesChatService>();
        services.AddScoped<IKitSalesReviewService, KitSalesReviewService>();
        return services;
    }
}
