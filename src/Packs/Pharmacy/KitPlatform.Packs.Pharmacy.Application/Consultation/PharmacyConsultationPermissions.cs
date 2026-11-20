namespace KitPlatform.Packs.Pharmacy.Consultation;

public static class PharmacyConsultationPermissions
{
    /// <summary>
    /// Pharmacist-in-charge: may view the OTC list for a case that needs a pharmacist
    /// (pregnancy, breastfeeding, infant). Sales staff never see it.
    /// </summary>
    public const string Pharmacist = "pharmacy.consultation.pharmacist";
}
