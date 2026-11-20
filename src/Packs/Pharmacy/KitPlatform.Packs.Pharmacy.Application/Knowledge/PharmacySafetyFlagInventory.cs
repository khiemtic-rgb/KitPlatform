namespace KitPlatform.Packs.Pharmacy.Knowledge;

public sealed record PharmacySafetyFlagInventoryItem(
    string FlagCode,
    string SafetyLevel,
    string MessageVi,
    string SourceRef);

/// <summary>
/// Existing coded safety warnings queued for human review.
/// None of these rows are approved by this catalog.
/// </summary>
public static class PharmacySafetyFlagInventory
{
    public const string SourceRef = "src/Packs/Pharmacy/KitPlatform.Packs.Pharmacy.Infrastructure/Consultation/ConsultationSafetyRules.cs";

    public static IReadOnlyList<PharmacySafetyFlagInventoryItem> Items { get; } =
    [
        Item("infant_under_2", "refer_pharmacist", "Trẻ dưới 2 tuổi — không tự tư vấn OTC tại quầy; chuyển dược sĩ."),
        Item("pregnant", "refer_pharmacist", "Khách mang thai — chỉ dược sĩ tư vấn và quyết định sản phẩm."),
        Item("breastfeeding", "refer_pharmacist", "Khách đang cho con bú — chỉ dược sĩ tư vấn và quyết định sản phẩm."),
        Item("difficulty_breathing", "refer_medical", "Khó thở — không bán OTC; hướng cấp cứu / bác sĩ ngay."),
        Item("shortness_of_breath", "refer_medical", "Khó thở — không bán OTC; hướng cấp cứu / bác sĩ ngay."),
        Item("chest_pain", "refer_medical", "Đau ngực — không bán OTC; hướng cấp cứu / bác sĩ ngay."),
        Item("unconscious", "stop_sale", "Ngất / co giật — dừng bán, gọi cấp cứu."),
        Item("seizure", "stop_sale", "Ngất / co giật — dừng bán, gọi cấp cứu."),
        Item("severe_bleeding", "refer_medical", "Chảy máu nặng — không bán OTC; hướng cấp cứu."),
        Item("swallowing_difficulty", "refer_pharmacist", "Khó nuốt — chuyển dược sĩ, không tự chọn thuốc."),
        Item("high_fever_infant", "refer_medical", "Trẻ nhỏ sốt cao — hướng khám ngay."),
        Item("infant_fever", "refer_medical", "Trẻ nhỏ có sốt — không tự tư vấn OTC; hướng khám."),
        Item("allergy_symptom", "caution", "Triệu chứng dị ứng — hỏi thêm tiền sử dị ứng / phản ứng thuốc trước khi gợi ý."),
    ];

    private static PharmacySafetyFlagInventoryItem Item(string code, string level, string message) =>
        new(code, level, message, SourceRef);
}
