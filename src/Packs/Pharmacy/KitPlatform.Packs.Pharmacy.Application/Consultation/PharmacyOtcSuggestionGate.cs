namespace KitPlatform.Packs.Pharmacy.Consultation;

/// <summary>
/// Ordinary OTC product lists are hidden when the safety engine says stop or refer.
/// The engine decision is not overridden by an LLM.
/// </summary>
public static class PharmacyOtcSuggestionGate
{
    public static bool ShowsOrdinaryOtcList(string? safetyLevel) =>
        safetyLevel is not ("stop_sale" or "refer_medical" or "refer_pharmacist");
}
