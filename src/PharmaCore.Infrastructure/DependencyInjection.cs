using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Auth;
using PharmaCore.Application.Catalog;
using PharmaCore.Application.Customers;
using PharmaCore.Application.Inventory;
using PharmaCore.Application.Loyalty;
using PharmaCore.Application.Integration;
using PharmaCore.Application.Procurement;
using PharmaCore.Application.Sales;
using PharmaCore.Application.Configuration;
using PharmaCore.Infrastructure.Auth;
using PharmaCore.Infrastructure.Catalog;
using PharmaCore.Infrastructure.Configuration;
using PharmaCore.Infrastructure.Customers;
using PharmaCore.Infrastructure.Integration;
using PharmaCore.Infrastructure.Inventory;
using PharmaCore.Infrastructure.Loyalty;
using PharmaCore.Infrastructure.Procurement;
using PharmaCore.Infrastructure.Sales;
using PharmaCore.Infrastructure.Data;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Infrastructure.CustomerApp;
using PharmaCore.Infrastructure.Security;

namespace PharmaCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<CustomerAppAuthSettings>(configuration.GetSection(CustomerAppAuthSettings.SectionName));
        services.Configure<CustomerAppPushOptions>(configuration.GetSection(CustomerAppPushSettings.SectionName));

        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<ICurrentCustomerAccessor, CurrentCustomerAccessor>();

        services.AddScoped<AuthRepository>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddCustomerOtpSender(configuration, environment);

        services.AddScoped<CustomerAppAuthRepository>();
        services.AddSingleton<CustomerAppJwtTokenService>();
        services.AddScoped<ICustomerAppAuthService, CustomerAppAuthService>();

        services.AddScoped<CustomerLoyaltyRepository>();
        services.AddScoped<ICustomerLoyaltyService, CustomerLoyaltyService>();

        services.AddScoped<CustomerReminderRepository>();
        services.AddScoped<ICustomerReminderService, CustomerReminderService>();

        services.AddScoped<CustomerCatalogRepository>();
        services.AddScoped<ICustomerCatalogService, CustomerCatalogService>();

        services.AddScoped<CustomerAppConsentRepository>();
        services.AddScoped<ICustomerAppConsentService, CustomerAppConsentService>();

        services.AddScoped<CustomerPushRepository>();
        services.AddScoped<ICustomerPushService, CustomerPushService>();
        services.AddHostedService<MedicationReminderPushWorker>();

        services.AddSingleton<IChatEventHub, ChatEventHub>();
        services.AddSingleton<IDraftOrderEventHub, DraftOrderEventHub>();
        services.AddScoped<CustomerChatRepository>();
        services.AddScoped<ICustomerChatService, CustomerChatService>();

        services.AddScoped<CustomerDraftOrderRepository>();
        services.AddScoped<ICustomerDraftOrderService, CustomerDraftOrderService>();

        services.AddScoped<CustomerPurchaseRepository>();
        services.AddScoped<ICustomerPurchaseService, CustomerPurchaseService>();

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

        services.AddScoped<CatalogRepository>();
        services.AddScoped<ICatalogService, CatalogService>();

        services.AddScoped<CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();

        services.AddScoped<BrandRepository>();
        services.AddScoped<IBrandService, BrandService>();

        services.AddScoped<ActiveIngredientRepository>();
        services.AddScoped<IActiveIngredientService, ActiveIngredientService>();

        services.AddScoped<InventoryRepository>();
        services.AddScoped<IBatchResolver, BatchResolver>();
        services.AddScoped<ITenantSettingsService, TenantSettingsService>();
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
        services.AddScoped<IInventoryService, InventoryService>();

        services.AddScoped<CustomerConsentRepository>();
        services.AddScoped<ICustomerConsentService, CustomerConsentService>();

        services.AddScoped<ProcurementRepository>();
        services.AddScoped<AuditLogRepository>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();

        services.AddScoped<SalesRepository>();
        services.AddScoped<ISalesService, SalesService>();

        return services;
    }
}
