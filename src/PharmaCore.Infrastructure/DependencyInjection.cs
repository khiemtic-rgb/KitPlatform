using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PharmaCore.Application.Abstractions;
using PharmaCore.Application.Auth;
using PharmaCore.Application.Catalog;
using PharmaCore.Application.Customers;
using PharmaCore.Application.Inventory;
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
using PharmaCore.Infrastructure.Procurement;
using PharmaCore.Infrastructure.Sales;
using PharmaCore.Infrastructure.Data;
using PharmaCore.Infrastructure.Security;

namespace PharmaCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));

        services.AddSingleton<IDbConnectionFactory>(_ => new NpgsqlConnectionFactory(connectionString));
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();

        services.AddScoped<AuthRepository>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();

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
