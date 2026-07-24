using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Auth;
using KitPlatform.Application.Customers;
using KitPlatform.Application.Dashboard;
using KitPlatform.Application.Loyalty;
using KitPlatform.Application.Identity;
using KitPlatform.Application.Integration;
using KitPlatform.Application.Platform;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Application.Reports;
using KitPlatform.Application.Notify;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Application.CustomerApp;
using KitPlatform.Infrastructure.Notify;
using KitPlatform.Infrastructure.Auth;
using KitPlatform.Infrastructure.Core.Engines;
using KitPlatform.Infrastructure.Configuration;
using KitPlatform.Infrastructure.Customers;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Infrastructure.Identity;
using KitPlatform.Infrastructure.Integration;
using KitPlatform.Infrastructure.Kernel.Workspace;
using KitPlatform.Infrastructure.Loyalty;
using KitPlatform.Infrastructure.Platform;
using KitPlatform.Infrastructure.Platform.Events;
using KitPlatform.Infrastructure.Reports;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.CustomerApp;
using KitPlatform.Infrastructure.Security;

namespace KitPlatform.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.Configure<PlatformSettings>(configuration.GetSection(PlatformSettings.SectionName));
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<CustomerAppAuthSettings>(configuration.GetSection(CustomerAppAuthSettings.SectionName));
        services.Configure<CustomerAppPushOptions>(configuration.GetSection(CustomerAppPushSettings.SectionName));

        // Background workers need a non-scoped factory (no HTTP/tenant session).
        services.AddSingleton(new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<IDbConnectionFactory>(sp =>
            new TenantScopedNpgsqlConnectionFactory(
                connectionString,
                sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IWorkspaceResolver, WorkspaceResolver>();
        services.AddScoped<BranchAccessRepository>();
        services.AddScoped<IBranchAccessService, BranchAccessService>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<ICurrentCustomerAccessor, CurrentCustomerAccessor>();
        services.AddScoped<ICurrentPrescriberAccessor, CurrentPrescriberAccessor>();

        services.AddScoped<AuthRepository>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddCustomerOtpSender(configuration, environment);

        services.AddScoped<CustomerAppAuthRepository>();
        services.AddSingleton<CustomerAppJwtTokenService>();
        services.AddScoped<ICustomerAppAuthService, CustomerAppAuthService>();
        services.AddScoped<ICustomerPilotOtpAdminService, CustomerPilotOtpAdminService>();

        services.AddScoped<CustomerLoyaltyRepository>();
        services.AddScoped<ICustomerLoyaltyService, CustomerLoyaltyService>();

        services.AddScoped<CustomerReminderRepository>();
        services.AddScoped<ICustomerReminderService, CustomerReminderService>();
        services.AddScoped<CustomerFamilyRepository>();
        services.AddScoped<ICustomerFamilyService, CustomerFamilyService>();
        services.AddScoped<CustomerHealthRepository>();
        services.AddScoped<ICustomerHealthService, CustomerHealthService>();
        services.AddScoped<CustomerCareReminderRepository>();
        services.AddScoped<ICustomerCareReminderService, CustomerCareReminderService>();
        services.AddScoped<CustomerRepurchaseRepository>();
        services.AddScoped<ICustomerRepurchaseService, CustomerRepurchaseService>();
        services.AddScoped<ICustomerMedicationAdherenceService, CustomerMedicationAdherenceService>();
        services.AddScoped<ICustomerAiHealthService, CustomerAiHealthService>();
        services.AddScoped<CustomerAppBrandingRepository>();
        services.AddScoped<ICustomerAppBrandingService, CustomerAppBrandingService>();

        services.AddScoped<CustomerNotificationRepository>();
        services.AddScoped<ICustomerNotificationService, CustomerNotificationService>();
        services.AddScoped<CustomerCatalogRepository>();
        services.AddScoped<ICustomerCatalogService, CustomerCatalogService>();

        services.AddScoped<CustomerAppConsentRepository>();
        services.AddScoped<ICustomerAppConsentService, CustomerAppConsentService>();

        services.AddScoped<CustomerPushRepository>();
        services.AddScoped<CustomerEngagementRepository>();
        services.AddScoped<ICustomerNotificationTextService, CustomerNotificationTextService>();
        services.AddScoped<ICustomerPushService, CustomerPushService>();
        services.AddScoped<CustomerEngagementEventRepository>();
        services.AddScoped<ICustomerEngagementEventService, CustomerEngagementEventService>();
        services.AddScoped<CustomerEngagementAnalyticsRepository>();
        services.AddScoped<ICustomerEngagementAnalyticsService, CustomerEngagementAnalyticsService>();
        services.AddHostedService<MedicationReminderPushWorker>();

        services.AddSingleton<IChatEventHub, ChatEventHub>();
        services.AddSingleton<IDraftOrderEventHub, DraftOrderEventHub>();
        services.AddScoped<CustomerChatRepository>();
        services.AddScoped<ICustomerChatService, CustomerChatService>();

        services.AddScoped<CustomerDraftOrderRepository>();
        services.AddScoped<ICustomerDraftOrderService, CustomerDraftOrderService>();

        services.AddScoped<CustomerPurchaseRepository>();
        services.AddScoped<ICustomerPurchaseService, CustomerPurchaseService>();
        services.AddScoped<ICustomerAppOverviewService, CustomerAppOverviewService>();
        services.AddScoped<ICustomerAppConnectService, CustomerAppConnectService>();
        services.AddScoped<CustomerReceivablesRepository>();
        services.AddScoped<ICustomerAppReceivablesService, CustomerAppReceivablesService>();

        services.AddScoped<CustomerAddressRepository>();
        services.AddScoped<ICustomerAddressService, CustomerAddressService>();

        services.AddScoped<CustomerReservationRepository>();
        services.AddScoped<ICustomerReservationService, CustomerReservationService>();

        services.AddScoped<LoyaltyAdminRepository>();
        services.AddScoped<LoyaltyPosService>();
        services.AddScoped<VoucherRepository>();
        services.AddScoped<VoucherPosService>();
        services.AddScoped<IVoucherAdminService, VoucherAdminService>();
        services.AddScoped<ILoyaltyAdminService, LoyaltyAdminService>();

        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
        services.AddScoped<WorkspacePackProvisioner>();
        services.AddScoped<ITenantPlatformSettings, TenantPlatformSettingsService>();
        services.AddScoped<IIntegrationOutboxWriter, IntegrationOutboxWriter>();
        services.Configure<IntegrationOutboxOptions>(configuration.GetSection(IntegrationOutboxOptions.SectionName));
        services.AddHttpClient(
            IntegrationOutboxHttpClient.Name,
            (sp, client) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<IntegrationOutboxOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(Math.Max(5, options.WebhookTimeoutSeconds));
            });
        services.AddSingleton<IIntegrationOutboxPublisher, HttpIntegrationOutboxPublisher>();
        services.AddScoped<IIntegrationOutboxQuery, IntegrationOutboxQuery>();
        services.AddHostedService<IntegrationOutboxWorker>();
        services.AddScoped<IPlatformEventWriter, PlatformEventWriter>();
        services.AddScoped<IPlatformEventDispatcher, InProcessPlatformEventDispatcher>();
        services.AddScoped<IPlatformEventHandler, LoggingPlatformEventHandler>();
        services.Configure<PlatformEventOptions>(configuration.GetSection(PlatformEventOptions.SectionName));
        services.AddHostedService<PlatformEventWorker>();
        services.Configure<Notify.NotifyQueueOptions>(configuration.GetSection(Notify.NotifyQueueOptions.SectionName));
        services.AddScoped<INotifyQueueDispatcher, NotifyQueueDispatcher>();
        services.AddHostedService<Notify.NotifyQueueWorker>();

        services.AddScoped<DashboardRepository>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<Success.OwnerCockpitRepository>();
        services.AddScoped<Application.Success.IOwnerCockpitService, Success.OwnerCockpitService>();
        services.AddScoped<Success.ShiftChecklistRepository>();
        services.AddScoped<Application.Success.IShiftChecklistService, Success.ShiftChecklistService>();
        services.AddScoped<Success.LossPreventionRepository>();
        services.AddScoped<Application.Success.ILossPreventionService, Success.LossPreventionService>();
        services.AddScoped<Learning.LearningRepository>();
        services.AddScoped<Application.Learning.ILearningCatalogService, Learning.LearningCatalogService>();
        services.AddScoped<Application.Learning.ILearningProgressService, Learning.LearningProgressService>();

        services.AddScoped<ReportsRepository>();
        services.AddScoped<IReportsService, ReportsService>();

        services.AddScoped<CustomerConsentRepository>();
        services.AddScoped<ICustomerConsentService, CustomerConsentService>();
        services.AddScoped<CustomerAdminRepository>();
        services.AddScoped<CustomerGroupRepository>();
        services.AddScoped<ICustomerAdminService, CustomerAdminService>();
        services.AddScoped<ICustomerMergeService, CustomerMergeService>();
        services.AddScoped<ICustomerGroupService, CustomerGroupService>();
        services.AddScoped<ICustomerImportService, CustomerImportService>();
        services.AddScoped<IdentityAdminRepository>();
        services.AddScoped<IIdentityAdminService, IdentityAdminService>();

        services.AddScoped<PlatformTenantRepository>();
        services.AddScoped<IPlatformTenantService, PlatformTenantService>();

        services.AddScoped<AuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IAuditLogQuery, AuditLogQueryService>();
        // Core Engines (EA) — wrap existing pilot logic; additive, no behavior change.
        services.AddScoped<IPricingEngine, PricingEngine>();
        services.AddScoped<IPermissionEngine, PermissionEngine>();
        services.AddScoped<IAuditEngine, AuditEngine>();
        services.AddScoped<INotificationEngine, NotificationEngine>();
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddScoped<AiConversationRepository>();
        services.AddScoped<IAiConversationRepository>(sp => sp.GetRequiredService<AiConversationRepository>());
        services.AddScoped<IAiOrchestrator, AiOrchestrator>();

        return services;
    }
}
