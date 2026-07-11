using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal static class CustomerOtpSenderRegistration
{
    public static IServiceCollection AddCustomerOtpSender(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.Configure<CustomerAppSmsSettings>(configuration.GetSection(CustomerAppSmsSettings.SectionName));

        if (environment.IsProduction())
        {
            var smsUrl = configuration.GetSection(CustomerAppSmsSettings.SectionName)["HttpUrl"];
            var smsProvider = configuration.GetSection(CustomerAppSmsSettings.SectionName)["Provider"] ?? "Http";
            if (!smsProvider.Equals("Log", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(smsUrl))
            {
                throw new InvalidOperationException(
                    "Production requires CustomerAppSms:HttpUrl when Provider is Http.");
            }
        }

        var provider = configuration.GetSection(CustomerAppSmsSettings.SectionName)["Provider"] ?? "Http";
        var useLog = environment.IsDevelopment()
            || provider.Equals("Log", StringComparison.OrdinalIgnoreCase);

        if (useLog)
        {
            services.AddSingleton<ICustomerOtpSender, LogCustomerOtpSender>();
            services.AddSingleton<ISmsTextSender, LogSmsTextSender>();
            return services;
        }

        services.AddHttpClient<ICustomerOtpSender, HttpCustomerOtpSender>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestVersion = new Version(1, 1);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
        });

        services.AddHttpClient<ISmsTextSender, HttpSmsTextSender>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestVersion = new Version(1, 1);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(5),
        });

        return services;
    }
}
