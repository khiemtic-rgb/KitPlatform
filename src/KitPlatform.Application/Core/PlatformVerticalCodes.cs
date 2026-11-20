namespace KitPlatform.Application.Core;

/// <summary>Sync with <c>ck_tenants_business_vertical</c> (051 + 192a for <c>family</c>).</summary>
public static class PlatformVerticalCodes
{
    public const string Pharmacy = "pharmacy";
    public const string PharmacyChain = "pharmacy_chain";
    public const string SupplementStore = "supplement_store";
    public const string MedicalEquipmentStore = "medical_equipment_store";
    public const string Clinic = "clinic";
    public const string Lab = "lab";
    public const string MedicalSpa = "medical_spa";
    public const string Hybrid = "hybrid";
    /// <summary>FamilyOS consumer household tenant.</summary>
    public const string Family = "family";

    /// <summary>
    /// Marketing Park / Video Studio. Stored in settings JSON.
    /// <c>tenants.business_vertical</c> stays <see cref="Hybrid"/> because
    /// <c>ck_tenants_business_vertical</c> does not list this value. KIT_MKT uses the same pair.
    /// </summary>
    public const string Marketing = "marketing";

    public static IReadOnlyList<string> All { get; } =
    [
        Pharmacy,
        PharmacyChain,
        SupplementStore,
        MedicalEquipmentStore,
        Clinic,
        Lab,
        MedicalSpa,
        Hybrid,
        Family,
        Marketing,
    ];

    /// <summary>
    /// Value for <c>tenants.business_vertical</c>. Settings JSON may use <see cref="Family"/>
    /// before migration 192a widens <c>ck_tenants_business_vertical</c>; keep the existing
    /// column (usually <see cref="Hybrid"/>) until the column is already <c>family</c>.
    /// </summary>
    public static string ToColumnValue(string settingsVertical, string? currentColumnValue)
    {
        var settings = settingsVertical?.Trim() ?? Pharmacy;
        var column = currentColumnValue?.Trim();
        if (settings.Equals(Family, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(column, Family, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(column) ? Hybrid : column;
        }

        if (settings.Equals(Marketing, StringComparison.OrdinalIgnoreCase))
            return Hybrid;

        return settings;
    }
}
