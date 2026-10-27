using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KitPlatform.Packs.PersonalIntelligence.Research;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Research;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Intelligence;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure;

/// <summary>
/// Personal Intelligence Pack dependency injection
/// </summary>
public static class PersonalIntelligencePackDependencyInjection
{
    public static IServiceCollection AddPersonalIntelligenceResearch(
        this IServiceCollection services)
    {
        services.AddSingleton<IResearchPublicationReader, FileResearchPublicationReader>();
        return services;
    }

    public static IServiceCollection AddPersonalIntelligencePack(
        this IServiceCollection services,
        bool startBackfillWorker = true)
    {
        // Trading Ledger V1 repositories
        services.AddScoped<IPersonRepository, PersonRepository>();
        services.AddScoped<IExchangeRepository, ExchangeRepository>();
        services.AddScoped<ITradingAccountRepository, TradingAccountRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();
        services.AddScoped<IFeeRepository, FeeRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IPortfolioSnapshotRepository, PortfolioSnapshotRepository>();
        services.AddScoped<IIntelligenceJournal, IntelligenceJournal>();
        services.AddScoped<IPaperExecutionStore, PaperExecutionStore>();
        services.AddScoped<ITradingOperationalLoop, TradingOperationalLoop>();
        services.AddScoped<ITradingDesk, TradingDeskStore>();
        services.AddScoped<IPaperDecisionRecorder, PaperDecisionRecorder>();
        services.AddScoped<IPaperTaskStore, PaperTaskStore>();
        services.AddScoped<IBusinessIntentSession, BusinessIntentSession>();
        services.AddScoped<IBusinessCapabilityHandler, ResearchPublicationCapabilityHandler>();
        services.AddScoped<IBusinessCapabilityHandler, TradingDeskSummaryCapabilityHandler>();
        services.AddScoped<IBusinessCapabilityHandler, PaperEvaluationHandler>();
        services.AddSingleton<IResearchPublicationReader, FileResearchPublicationReader>();
        services.TryAddSingleton<IExchangeConnectorLog, SilentExchangeConnectorLog>();
        services.TryAddSingleton<IExchangeCredentialKeyRing, EnvironmentExchangeCredentialKeyRing>();
        services.TryAddSingleton(sp => new BybitExchangeConnector(
            sp.GetService<HttpMessageHandler>() ?? new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) },
            sp.GetRequiredService<IExchangeConnectorLog>()));
        services.TryAddSingleton<IExchangeConnector>(sp => sp.GetRequiredService<BybitExchangeConnector>());
        services.AddSingleton<IExchangeAdapter>(sp => sp.GetRequiredService<BybitExchangeConnector>());
        services.AddSingleton<IExchangeAdapter>(sp => new BinanceExchangeConnector(
            sp.GetService<HttpMessageHandler>() ?? new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5), AllowAutoRedirect = false },
            sp.GetRequiredService<IExchangeConnectorLog>()));
        services.AddSingleton<IExchangeAdapter>(sp => new OkxExchangeConnector(
            sp.GetService<HttpMessageHandler>() ?? new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(5) },
            sp.GetRequiredService<IExchangeConnectorLog>()));
        services.AddSingleton<IExchangeAdapterRegistry, ExchangeAdapterRegistry>();
        services.AddScoped<IExchangeConnectionService, ExchangeConnectionService>();
        services.AddScoped<IExchangeExecutionSync, ExchangeExecutionSyncService>();
        services.AddScoped<IExchangeBackfill, ExchangeBackfillService>();
        if (startBackfillWorker)
            services.AddHostedService<ExchangeBackfillWorker>();

        return services;
    }
}
