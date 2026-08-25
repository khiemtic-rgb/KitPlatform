namespace KitPlatform.Packs.Pharmacy.Consultation;

public static class PharmacyConsultationSymptoms
{
    public const string Headache = "headache";
    public const string RunnyNose = "runny_nose";
    public const string NasalCongestion = "nasal_congestion";
    public const string Cough = "cough";
    public const string CoughDry = "cough_dry";
    public const string CoughPhlegm = "cough_phlegm";
    public const string SoreThroat = "sore_throat";
    public const string Fever = "fever";
    public const string AbdominalPain = "abdominal_pain";
    public const string Diarrhea = "diarrhea";
    public const string Nausea = "nausea";
    public const string Heartburn = "heartburn";
    public const string Allergy = "allergy";
    public const string ItchySkin = "itchy_skin";
    public const string Rash = "rash";
    public const string BodyAche = "body_ache";
    public const string Fatigue = "fatigue";
    public const string Dizziness = "dizziness";
    public const string Sneezing = "sneezing";
    public const string CommonCold = "common_cold";
    public const string ShortnessOfBreath = "shortness_of_breath";
    public const string Constipation = "constipation";
    public const string Bloating = "bloating";
    public const string Reflux = "reflux";
    public const string Vomiting = "vomiting";
    public const string Hives = "hives";
    public const string Swelling = "swelling";
    public const string InsectBite = "insect_bite";
    public const string Toothache = "toothache";
    public const string BackPain = "back_pain";
    public const string JointPain = "joint_pain";
    public const string Chills = "chills";
    public const string EyePain = "eye_pain";
    public const string RedEye = "red_eye";
    public const string ItchyEye = "itchy_eye";
    public const string WateryEye = "watery_eye";
    public const string EarPain = "ear_pain";
    public const string Insomnia = "insomnia";
    public const string Anxiety = "anxiety";
    public const string Other = "other";

    public static readonly IReadOnlyList<PharmacyConsultationSymptomGroupDto> Groups =
    [
        new(
            "respiratory",
            "Hô hấp",
            [
                new(Cough, "Ho"),
                new(CoughDry, "Ho khan"),
                new(CoughPhlegm, "Ho có đờm"),
                new(RunnyNose, "Sổ mũi"),
                new(NasalCongestion, "Nghẹt mũi"),
                new(SoreThroat, "Đau họng"),
            ]),
        new(
            "digestive",
            "Tiêu hóa",
            [
                new(AbdominalPain, "Đau bụng"),
                new(Diarrhea, "Tiêu chảy"),
                new(Nausea, "Buồn nôn"),
                new(Heartburn, "Ợ nóng"),
            ]),
        new(
            "skin_allergy",
            "Da / dị ứng",
            [
                new(Allergy, "Dị ứng"),
                new(ItchySkin, "Ngứa da"),
                new(Rash, "Phát ban"),
            ]),
        new(
            "pain_fever",
            "Đau / sốt",
            [
                new(Headache, "Đau đầu"),
                new(Fever, "Sốt"),
                new(BodyAche, "Đau cơ"),
                new(Fatigue, "Mệt"),
                new(Dizziness, "Chóng mặt"),
            ]),
        new(
            "other",
            "Khác",
            [
                new(Other, "Khác / chưa rõ"),
            ]),
    ];

    public static readonly IReadOnlyList<PharmacyConsultationSymptomOptionDto> QuickOptions =
        Groups.SelectMany(g => g.Items).ToList();
}

public sealed record PharmacyConsultationSymptomOptionDto(string Code, string Label);

public sealed record PharmacyConsultationSymptomGroupDto(
    string Code,
    string Label,
    IReadOnlyList<PharmacyConsultationSymptomOptionDto> Items);

public sealed record PharmacyConsultationSymptomCatalogDto(
    IReadOnlyList<PharmacyConsultationSymptomGroupDto> Groups,
    IReadOnlyList<PharmacyConsultationSymptomOptionDto> Flat,
    string CatalogProfile = "novixa_base",
    IReadOnlyDictionary<string, IReadOnlyList<string>>? AliasesByCode = null);
