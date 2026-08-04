using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Application.Payment;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

public static class FamilyOsPackDependencyInjection
{
    public static IServiceCollection AddFamilyOsPack(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddScoped<IFamilyCommercialService, FamilyCommercialService>();
        services.AddScoped<IFamilyBillingService, FamilyBillingService>();
        services.AddScoped<IPaymentProductHandler, FamilyOsPaymentProductHandler>();
        services.AddScoped<IFamilyOsOverviewService, FamilyOsOverviewService>();
        services.AddScoped<FamilyGraphRepository>();
        services.AddScoped<IFamilyGraphService, FamilyGraphService>();
        services.AddScoped<FamilyRoutineRepository>();
        services.AddScoped<IFamilyRoutineService, FamilyRoutineService>();
        services.AddScoped<FamilyCalendarPeriodRepository>();
        services.AddScoped<IFamilyCalendarPeriodService, FamilyCalendarPeriodService>();
        services.AddScoped<FamilyDayFlowRepository>();
        services.AddScoped<IFamilyDayFlowService, FamilyDayFlowService>();
        services.AddScoped<FamilyEvidenceUploadRepository>();
        services.AddScoped<IFamilyEvidenceUploadService, FamilyEvidenceUploadService>();
        services.AddScoped<IFamilyMomentUploadService, FamilyMomentUploadService>();
        services.AddScoped<IFamilyMorningNoteService, FamilyMorningNoteService>();
        services.AddScoped<FamilyBehaviorRepository>();
        services.AddScoped<IFamilyBehaviorService, FamilyBehaviorService>();
        services.AddScoped<FamilyAgreementRepository>();
        services.AddScoped<FamilyAccountabilityOptionRepository>();
        services.AddScoped<IFamilyAgreementService, FamilyAgreementService>();
        services.AddScoped<FamilyConsequenceRepository>();
        services.AddScoped<IFamilyConsequenceService, FamilyConsequenceService>();
        services.AddScoped<FamilyAccountabilityGlanceRepository>();
        services.AddScoped<IFamilyAccountabilityGlanceService, FamilyAccountabilityGlanceService>();
        services.AddScoped<FamilyCoachInsightRepository>();
        services.AddScoped<IFamilyCoachInsightService, FamilyCoachInsightService>();
        services.AddScoped<FamilyInsightRepository>();
        services.AddScoped<IFamilyInsightService, FamilyInsightService>();
        services.AddScoped<FamilyParentSuccessRepository>();
        services.AddScoped<IFamilyParentSuccessService, FamilyParentSuccessService>();
        services.AddScoped<IFamilyAiDigestService, FamilyAiDigestService>();
        services.AddScoped<FamilyOsParentPushRepository>();
        services.AddScoped<IFamilyOsParentPushService, FamilyOsParentPushService>();
        services.AddScoped<FamilyValueRepository>();
        services.AddScoped<IFamilyValueService, FamilyValueService>();
        services.AddScoped<FamilyBlueprintRepository>();
        services.AddScoped<IFamilyBlueprintService, FamilyBlueprintService>();
        services.AddScoped<FamilyTeamUnlockRepository>();
        services.AddScoped<IFamilyTeamUnlockService, FamilyTeamUnlockService>();
        services.AddScoped<FamilyTeamNudgeRepository>();
        services.AddScoped<IFamilyTeamNudgeService, FamilyTeamNudgeService>();
        services.AddScoped<IFamilyCooperationScoreService, FamilyCooperationScoreService>();
        services.AddScoped<IFamilyRitualService, FamilyRitualService>();
        services.AddScoped<FamilyGratitudeRepository>();
        services.AddScoped<IFamilyGratitudeService, FamilyGratitudeService>();
        services.AddScoped<FamilyParentVoiceRepository>();
        services.AddScoped<FamilyRelationshipTriggerStateRepository>();
        services.AddScoped<IFamilyRelationshipService, FamilyRelationshipService>();
        services.AddScoped<FamilyStarLedgerRepository>();
        services.AddScoped<FamilyStarSettingsRepository>();
        services.AddScoped<FamilyCurrencySettingsRepository>();
        services.AddScoped<FamilyBadgeRepository>();
        services.AddScoped<IFamilyStarService, FamilyStarService>();
        services.AddScoped<IFamilyStarSettingsService, FamilyStarSettingsService>();
        services.AddScoped<IFamilyCurrencySettingsService, FamilyCurrencySettingsService>();
        services.AddScoped<IFamilyBadgeService, FamilyBadgeService>();
        services.AddScoped<FamilyRewardRepository>();
        services.AddScoped<IFamilyRewardService, FamilyRewardService>();
        services.AddScoped<FamilyMoodRepository>();
        services.AddScoped<IFamilyMoodService, FamilyMoodService>();
        services.AddScoped<FamilyParentGoalRepository>();
        services.AddScoped<IFamilyParentGoalService, FamilyParentGoalService>();
        services.AddScoped<FamilyChallengeRepository>();
        services.AddScoped<IFamilyChallengeService, FamilyChallengeService>();
        services.AddScoped<FamilyMemoryRepository>();
        services.AddScoped<IFamilyMemoryService, FamilyMemoryService>();
        services.AddScoped<FamilyChildRequestRepository>();
        services.AddScoped<FamilyAiProposalRepository>();
        services.AddScoped<FamilyScreenWalletRepository>();
        services.AddScoped<IFamilyScoreService, FamilyScoreService>();
        services.AddScoped<IFamilyScreenWalletService, FamilyScreenWalletService>();
        services.AddScoped<IFamilyModeService, FamilyModeService>();
        services.AddScoped<IFamilyChildRequestService, FamilyChildRequestService>();
        services.AddScoped<IFamilyAiProposalService, FamilyAiProposalService>();
        services.AddScoped<IFamilyDecisionInboxService, FamilyDecisionInboxService>();

        if (configuration is not null)
        {
            services.Configure<FamilyOsReminderOptions>(
                configuration.GetSection(FamilyOsReminderSettings.SectionName));
            services.Configure<FamilyOsPayOsOptions>(
                configuration.GetSection(FamilyOsPayOsSettings.SectionName));
            services.Configure<FamilyOsBillingOptions>(
                configuration.GetSection(FamilyOsBillingSettings.SectionName));
        }
        else
        {
            services.Configure<FamilyOsReminderOptions>(_ => { });
            services.Configure<FamilyOsPayOsOptions>(_ => { });
            services.Configure<FamilyOsBillingOptions>(_ => { });
        }

        services.AddHostedService<FamilyOsParentReminderWorker>();
        return services;
    }
}
