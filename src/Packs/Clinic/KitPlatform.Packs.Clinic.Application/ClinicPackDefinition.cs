namespace KitPlatform.Packs.Clinic;

/// <summary>Clinic + CRM Pack 2 pilot — metadata only; tables in <c>pack_clinic</c> / <c>pack_crm</c>.</summary>
public static class ClinicPackDefinition
{
    public const string PackCode = "clinic_crm";
    public const string DisplayName = "Novixa Clinic (ClinicOS)";

    /// <summary>
    /// Clinic-only defaults — no pharmacy retail SKUs (sales/inventory/…).
    /// Aligns with migration 126 strip for clinic vertical tenants.
    /// </summary>
    public static IReadOnlyList<string> DefaultEnabledModules { get; } =
    [
        "clinic_appointments",
        "clinic_emr_lite",
        "novixa_connect",
    ];

    public static IReadOnlyList<string> PackModuleCodes { get; } =
    [
        "clinic_appointments",
        "clinic_emr_lite",
        "crm_leads",
    ];
}
