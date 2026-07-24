using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Application.Healthcare;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Packs.Pharmacy.Care;
using KitPlatform.Packs.Pharmacy.Catalog;
using KitPlatform.Packs.Pharmacy.CustomerApp;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Infrastructure.Care;
using KitPlatform.Packs.Pharmacy.Infrastructure.CustomerApp;
using KitPlatform.Packs.Pharmacy.Infrastructure.Events;
using KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;
using KitPlatform.Packs.Pharmacy.Inventory;
using KitPlatform.Packs.Pharmacy.Knowledge;
using KitPlatform.Packs.Pharmacy.Integration.Qd540;
using KitPlatform.Packs.Pharmacy.Procurement;
using KitPlatform.Packs.Pharmacy.Rx;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Packs.Pharmacy.Infrastructure.Healthcare;
using KitPlatform.Packs.Pharmacy.Infrastructure.Integration.Qd540;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

public static class PharmacyPackDependencyInjection
{
    public static IServiceCollection AddPharmacyPack(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Care + knowledge (existing)
        services.AddScoped<CustomerActiveMedicationRepository>();
        services.AddScoped<ICustomerActiveMedicationService, CustomerActiveMedicationService>();
        services.AddScoped<IAiCareContextProvider, AiCareContextProvider>();
        services.AddScoped<ICareProductLookup, CareProductLookup>();
        services.AddScoped<IDrugKnowledgeQuery, DrugKnowledgeQuery>();
        services.AddScoped<IPlatformEventHandler, PharmacySalesOrderCompletedHandler>();
        services.AddScoped<IPlatformEventHandler, PharmacySalesReturnCompletedHandler>();
        services.AddScoped<PharmacyDispensingNoteRepository>();
        services.AddScoped<IPharmacyDispensingNoteService, PharmacyDispensingNoteService>();

        // Catalog
        services.AddScoped<CatalogRepository>();
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<ICatalogImportService, CatalogImportService>();
        services.AddScoped<IProductDuplicateMergeService, ProductDuplicateMergeService>();
        services.AddScoped<CategoryRepository>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<BrandRepository>();
        services.AddScoped<IBrandService, BrandService>();
        services.AddScoped<ActiveIngredientRepository>();
        services.AddScoped<IActiveIngredientService, ActiveIngredientService>();
        services.Configure<NationalDrugCatalogSettings>(configuration.GetSection(NationalDrugCatalogSettings.SectionName));
        services.AddScoped<INationalDrugCatalogService, MockNationalDrugCatalogService>();
        services.AddScoped<INationalDrugBulkLinkService, NationalDrugBulkLinkService>();

        // Inventory
        services.AddScoped<InventoryRepository>();
        services.AddScoped<BatchResolver>();
        services.AddScoped<IBatchResolver>(sp => sp.GetRequiredService<BatchResolver>());
        services.AddScoped<IInventoryEngine>(sp => sp.GetRequiredService<BatchResolver>());
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IInventoryImportService, InventoryImportService>();
        services.AddScoped<ILowStockSettingsService, LowStockSettingsService>();

        // Procurement
        services.AddScoped<ProcurementRepository>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<ISupplierImportService, SupplierImportService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<IGoodsReceiptService, GoodsReceiptService>();
        services.AddScoped<ISupplierPaymentService, SupplierPaymentService>();
        services.AddScoped<ISupplierPayablesService, SupplierPayablesService>();
        services.AddScoped<IProcurementVatTreatmentService, ProcurementVatTreatmentService>();

        // Sales / POS
        services.AddScoped<SalesRepository>();
        services.AddScoped<ISalesService, SalesService>();
        services.AddScoped<ICustomerReceivablesService, CustomerReceivablesService>();
        services.AddScoped<ICustomerPaymentService, CustomerPaymentService>();
        services.AddScoped<PrescriptionRepository>();
        services.AddScoped<IPrescriptionService, PrescriptionService>();
        services.AddScoped<IRxNotifyService, RxNotifyService>();
        services.AddScoped<PrescriberPortalRepository>();
        services.AddScoped<PrescriberPortalJwtTokenService>();
        services.AddScoped<IPrescriberPortalAuthService, PrescriberPortalAuthService>();
        services.AddScoped<IPrescriberLinkService, PrescriberLinkService>();
        services.AddScoped<PrescriberPortalPrescriptionRepository>();
        services.AddScoped<IPrescriberPortalPrescriptionService, PrescriberPortalPrescriptionService>();
        services.AddScoped<IPrescriberIdentityService, PrescriberIdentityHealthcareAdapter>();
        services.AddScoped<IPrescriberNetworkService, PrescriberNetworkHealthcareAdapter>();
        services.AddScoped<IPrescriptionIssuanceService, PrescriptionIssuanceHealthcareAdapter>();
        services.Configure<PrescriberPortalAuthSettings>(
            configuration.GetSection(PrescriberPortalAuthSettings.SectionName));

        // QĐ 540 integration (Bảng 1 export)
        services.AddScoped<Qd540Table1Repository>();
        services.AddScoped<IQd540Table1Service, Qd540Table1Service>();

        return services;
    }
}
