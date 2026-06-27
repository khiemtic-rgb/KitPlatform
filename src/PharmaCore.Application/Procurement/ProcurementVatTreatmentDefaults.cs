namespace PharmaCore.Application.Procurement;

public static class ProcurementVatTreatmentDefaults
{
    public static readonly HashSet<string> BuiltInCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "kct",
        "vat_0",
        "vat_5",
        "vat_8",
        "vat_10",
    };

    public static bool IsBuiltIn(string treatmentCode) =>
        BuiltInCodes.Contains(treatmentCode.Trim());
}
