namespace KitPlatform.Application.Core;

/// <summary>
/// Platform module codes — sync with <c>platform_module_registry.module_code</c> (migration 051).
/// Solution packs enable subsets per tenant via <c>settings.platform.enabled_modules</c>.
/// </summary>
public static class PlatformModuleCodes
{
    public const string Inventory = "inventory";
    public const string Procurement = "procurement";
    public const string Sales = "sales";
    public const string Loyalty = "loyalty";
    public const string CustomerApp = "customer_app";
    public const string Medication = "medication";
    public const string HealthWallet = "health_wallet";
    public const string Reservations = "reservations";
    public const string Reports = "reports";
    public const string Clinic = "clinic";
    public const string Lab = "lab";
    public const string Spa = "spa";
    public const string Assessment = "assessment";
    public const string PharmacySurvey = "pharmacy_survey";
    public const string ClinicAppointments = "clinic_appointments";
    public const string ClinicEmrLite = "clinic_emr_lite";
    public const string CrmLeads = "crm_leads";
    public const string ERrx = "e_rx";
    public const string PrescriberNetwork = "prescriber_network";
    public const string PrescriberPortal = "prescriber_portal";
    public const string Telehealth = "telehealth";
    /// <summary>Novixa Connect — collaboration workspace (not e-Rx issuer).</summary>
    public const string NovixaConnect = "novixa_connect";

    public static IReadOnlyList<string> All { get; } =
    [
        Inventory,
        Procurement,
        Sales,
        Loyalty,
        CustomerApp,
        Medication,
        HealthWallet,
        Reservations,
        Reports,
        Clinic,
        ClinicAppointments,
        ClinicEmrLite,
        CrmLeads,
        Lab,
        Spa,
        Assessment,
        PharmacySurvey,
        ERrx,
        PrescriberNetwork,
        PrescriberPortal,
        Telehealth,
        NovixaConnect,
    ];
}
