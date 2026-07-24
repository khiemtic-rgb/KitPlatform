using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

public static class FamilyOsPackDependencyInjection
{
    public static IServiceCollection AddFamilyOsPack(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddScoped<IFamilyOsOverviewService, FamilyOsOverviewService>();
        services.AddScoped<FamilyGraphRepository>();
        services.AddScoped<IFamilyGraphService, FamilyGraphService>();
        services.AddScoped<FamilyRoutineRepository>();
        services.AddScoped<IFamilyRoutineService, FamilyRoutineService>();
        services.AddScoped<FamilyDayFlowRepository>();
        services.AddScoped<IFamilyDayFlowService, FamilyDayFlowService>();
        services.AddScoped<FamilyAgreementRepository>();
        services.AddScoped<FamilyAccountabilityOptionRepository>();
        services.AddScoped<IFamilyAgreementService, FamilyAgreementService>();
        services.AddScoped<FamilyConsequenceRepository>();
        services.AddScoped<IFamilyConsequenceService, FamilyConsequenceService>();
        services.AddScoped<FamilyAccountabilityGlanceRepository>();
        services.AddScoped<IFamilyAccountabilityGlanceService, FamilyAccountabilityGlanceService>();
        services.AddScoped<FamilyCoachInsightRepository>();
        services.AddScoped<IFamilyCoachInsightService, FamilyCoachInsightService>();
        services.AddScoped<FamilyOsParentPushRepository>();
        services.AddScoped<IFamilyOsParentPushService, FamilyOsParentPushService>();
        services.AddScoped<FamilyValueRepository>();
        services.AddScoped<IFamilyValueService, FamilyValueService>();
        services.AddScoped<FamilyTeamUnlockRepository>();
        services.AddScoped<IFamilyTeamUnlockService, FamilyTeamUnlockService>();
        services.AddScoped<FamilyGratitudeRepository>();
        services.AddScoped<IFamilyGratitudeService, FamilyGratitudeService>();
        services.AddScoped<FamilyStarLedgerRepository>();
        services.AddScoped<FamilyStarSettingsRepository>();
        services.AddScoped<IFamilyStarService, FamilyStarService>();
        services.AddScoped<IFamilyStarSettingsService, FamilyStarSettingsService>();
        services.AddScoped<FamilyRewardRepository>();
        services.AddScoped<IFamilyRewardService, FamilyRewardService>();
        services.AddScoped<FamilyMoodRepository>();
        services.AddScoped<IFamilyMoodService, FamilyMoodService>();

        if (configuration is not null)
        {
            services.Configure<FamilyOsReminderOptions>(
                configuration.GetSection(FamilyOsReminderSettings.SectionName));
        }
        else
        {
            services.Configure<FamilyOsReminderOptions>(_ => { });
        }

        services.AddHostedService<FamilyOsParentReminderWorker>();
        return services;
    }
}
