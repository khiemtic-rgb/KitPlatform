using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Api.Controllers.PersonalIntelligence;

namespace KitPlatform.Api.Authorization;

public sealed class ResearchOnlyControllerConvention : IApplicationModelConvention
{
    readonly bool _exposeExchangeRead;

    public ResearchOnlyControllerConvention(bool exposeExchangeRead = false) => _exposeExchangeRead = exposeExchangeRead;

    public void Apply(ApplicationModel application)
    {
        var packAssembly = typeof(ResearchPublicationController).Assembly;
        var hidden = application.Controllers
            .Where(controller => controller.ControllerType.Assembly == packAssembly
                && controller.ControllerType != typeof(ResearchPublicationController)
                && controller.ControllerType != typeof(AgentTaskController)
                && controller.ControllerType != typeof(PaperTaskController)
                && controller.ControllerType != typeof(BusinessIntentController)
                && !(_exposeExchangeRead && controller.ControllerType == typeof(TradingExchangeController)))
            .ToList();
        foreach (var controller in hidden)
            application.Controllers.Remove(controller);
    }
}

/// <summary>
/// Removes every <see cref="Microsoft.Extensions.Hosting.IHostedService"/> already registered.
/// Production leaves <see cref="ConfigurationKey"/> unset, so <see cref="Apply"/> does nothing.
/// </summary>
public static class ResearchSafeHostWorkerIsolation
{
    public const string ConfigurationKey = "ResearchSafeHost:SuppressBackgroundWriters";

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue(ConfigurationKey, false);

    public static void Apply(IConfiguration configuration, IServiceCollection services)
    {
        if (!IsEnabled(configuration))
            return;

        foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)).ToList())
            services.Remove(descriptor);
    }
}
