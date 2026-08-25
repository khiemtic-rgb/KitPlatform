namespace KitPlatform.Packs.Pharmacy.Consultation;

/// <summary>
/// Novixa base catalog + Xuân Hòa counter overlay (import/xuan-hoa product + customer notes).
/// </summary>
public static class PharmacyConsultationSymptomCatalogBuilder
{
    public const string TenantCodeXuanHoa = "NT_XUANHOA";

    public static PharmacyConsultationSymptomCatalogDto Build(string? tenantCode)
    {
        if (IsXuanHoaProfile(tenantCode))
            return BuildXuanHoa();

        return new PharmacyConsultationSymptomCatalogDto(
            PharmacyConsultationSymptoms.Groups,
            PharmacyConsultationSymptoms.QuickOptions,
            "novixa_base");
    }

    public static bool IsXuanHoaProfile(string? tenantCode) =>
        string.Equals(tenantCode?.Trim(), TenantCodeXuanHoa, StringComparison.OrdinalIgnoreCase);

    private static PharmacyConsultationSymptomCatalogDto BuildXuanHoa()
    {
        var groups = new List<PharmacyConsultationSymptomGroupDto>(PharmacyConsultationSymptoms.Groups.Count + 2);

        foreach (var group in PharmacyConsultationSymptoms.Groups)
        {
            groups.Add(group.Code switch
            {
                "respiratory" => group with
                {
                    Items = MergeItems(group.Items,
                        new(PharmacyConsultationSymptoms.Sneezing, "Hắt hơi"),
                        new(PharmacyConsultationSymptoms.CommonCold, "Cảm lạnh / cảm cúm"),
                        new(PharmacyConsultationSymptoms.ShortnessOfBreath, "Khó thở")),
                },
                "digestive" => group with
                {
                    Items = MergeItems(group.Items,
                        new(PharmacyConsultationSymptoms.Constipation, "Táo bón"),
                        new(PharmacyConsultationSymptoms.Bloating, "Đầy bụng"),
                        new(PharmacyConsultationSymptoms.Reflux, "Ợ chua / trào ngược"),
                        new(PharmacyConsultationSymptoms.Vomiting, "Nôn / ói")),
                },
                "skin_allergy" => group with
                {
                    Items = MergeItems(group.Items,
                        new(PharmacyConsultationSymptoms.Hives, "Nổi mề đay / mẩn đỏ"),
                        new(PharmacyConsultationSymptoms.Swelling, "Sưng / phù nề"),
                        new(PharmacyConsultationSymptoms.InsectBite, "Côn trùng cắn")),
                },
                "pain_fever" => group with
                {
                    Items = MergeItems(group.Items,
                        new(PharmacyConsultationSymptoms.Toothache, "Đau răng"),
                        new(PharmacyConsultationSymptoms.BackPain, "Đau lưng"),
                        new(PharmacyConsultationSymptoms.JointPain, "Đau khớp"),
                        new(PharmacyConsultationSymptoms.Chills, "Rét run")),
                },
                _ => group,
            });
        }

        groups.Add(new PharmacyConsultationSymptomGroupDto(
            "eye_ear",
            "Mắt / tai",
            [
                new(PharmacyConsultationSymptoms.EyePain, "Đau mắt"),
                new(PharmacyConsultationSymptoms.RedEye, "Đỏ mắt"),
                new(PharmacyConsultationSymptoms.ItchyEye, "Ngứa mắt"),
                new(PharmacyConsultationSymptoms.WateryEye, "Chảy nước mắt"),
                new(PharmacyConsultationSymptoms.EarPain, "Đau tai"),
            ]));

        groups.Add(new PharmacyConsultationSymptomGroupDto(
            "sleep_stress",
            "Ngủ / thần kinh",
            [
                new(PharmacyConsultationSymptoms.Insomnia, "Khó ngủ / mất ngủ"),
                new(PharmacyConsultationSymptoms.Anxiety, "Lo âu / căng thẳng"),
            ]));

        var flat = groups.SelectMany(g => g.Items).ToList();
        return new PharmacyConsultationSymptomCatalogDto(groups, flat, "xuan_hoa");
    }

    private static IReadOnlyList<PharmacyConsultationSymptomOptionDto> MergeItems(
        IReadOnlyList<PharmacyConsultationSymptomOptionDto> existing,
        params PharmacyConsultationSymptomOptionDto[] extra)
    {
        var map = new Dictionary<string, PharmacyConsultationSymptomOptionDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in existing)
            map[item.Code] = item;
        foreach (var item in extra)
            map[item.Code] = item;
        return map.Values.ToList();
    }
}
