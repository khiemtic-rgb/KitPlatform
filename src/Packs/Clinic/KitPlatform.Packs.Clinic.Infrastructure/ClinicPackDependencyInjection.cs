using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.Clinic;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Packs.Clinic.Infrastructure;

public static class ClinicPackDependencyInjection
{
    public static IServiceCollection AddClinicPack(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ClinicCksSettings>(configuration.GetSection(ClinicCksSettings.SectionName));
        services.AddScoped<ClinicAppointmentRepository>();
        services.AddScoped<IClinicAppointmentService, ClinicAppointmentService>();
        services.AddScoped<ClinicVisitRepository>();
        services.AddScoped<EncounterSessionRepository>();
        services.AddScoped<IEncounterMediaProvider, NullEncounterMediaProvider>();
        services.AddScoped<IClinicVisitService, ClinicVisitService>();
        services.AddScoped<ClinicProviderRepository>();
        services.AddScoped<IClinicProviderService, ClinicProviderService>();
        services.AddScoped<IClinicTenantSettingsService, ClinicTenantSettingsService>();
        services.AddScoped<ClinicPrescriptionRepository>();
        services.AddScoped<IClinicPrescriptionService, ClinicPrescriptionService>();
        services.AddScoped<IClinicDaySummaryService, ClinicDaySummaryService>();
        services.AddScoped<IConnectClinicCalendarBridge, ConnectClinicCalendarBridge>();
        services.AddScoped<CrmLeadRepository>();
        services.AddScoped<ICrmLeadService, CrmLeadService>();

        var provider = configuration.GetSection(ClinicCksSettings.SectionName)["Provider"]
            ?? ClinicSignatureProviders.Mock;
        if (string.Equals(provider, ClinicSignatureProviders.UsbCa, StringComparison.OrdinalIgnoreCase))
        {
            // Hard-CKS sprint: register CaUsbClinicPrescriptionSigner here.
            services.AddScoped<IClinicPrescriptionSigner, MockClinicPrescriptionSigner>();
        }
        else
        {
            services.AddScoped<IClinicPrescriptionSigner, MockClinicPrescriptionSigner>();
        }

        return services;
    }
}
