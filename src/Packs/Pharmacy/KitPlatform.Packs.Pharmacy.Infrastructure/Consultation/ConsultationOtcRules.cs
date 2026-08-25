using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal static class ConsultationOtcRules
{
    internal sealed record RuleMatch(
        string SymptomCode,
        string Reason,
        IReadOnlyList<string> CategoryCodes,
        IReadOnlyList<string> Keywords);

    private static readonly IReadOnlyDictionary<string, RuleMatch> BySymptom =
        new Dictionary<string, RuleMatch>(StringComparer.OrdinalIgnoreCase)
        {
            [PharmacyConsultationSymptoms.CoughDry] = new(
                PharmacyConsultationSymptoms.CoughDry,
                "Ho khan — long đờm / giảm ho OTC",
                ["HO_HAP"],
                ["acc", "acetylcysteine", "prospan", "ivy", "ho khan", "long dom", "long đờm"]),
            [PharmacyConsultationSymptoms.CoughPhlegm] = new(
                PharmacyConsultationSymptoms.CoughPhlegm,
                "Ho đờm — long đờm OTC",
                ["HO_HAP"],
                ["acc", "acetylcysteine", "prospan", "ivy", "long dom", "long đờm"]),
            [PharmacyConsultationSymptoms.Cough] = new(
                PharmacyConsultationSymptoms.Cough,
                "Ho — giảm ho / cảm cúm OTC",
                ["HO_HAP"],
                ["prospan", "decolgen", "cam cum", "ho"]),
            [PharmacyConsultationSymptoms.RunnyNose] = new(
                PharmacyConsultationSymptoms.RunnyNose,
                "Sổ mũi — thuốc cảm cúm OTC",
                ["HO_HAP"],
                ["decolgen", "cam cum", "so mui", "sổ mũi", "phenylephrine"]),
            [PharmacyConsultationSymptoms.NasalCongestion] = new(
                PharmacyConsultationSymptoms.NasalCongestion,
                "Nghẹt mũi — thuốc cảm cúm OTC",
                ["HO_HAP"],
                ["decolgen", "cam cum", "nghet mui", "nghẹt mũi"]),
            [PharmacyConsultationSymptoms.SoreThroat] = new(
                PharmacyConsultationSymptoms.SoreThroat,
                "Đau họng — giảm đau / cảm cúm OTC",
                ["HO_HAP", "GIAM_DAU"],
                ["decolgen", "cam cum", "dau hong", "đau họng", "paracetamol"]),
            [PharmacyConsultationSymptoms.Sneezing] = new(
                PharmacyConsultationSymptoms.Sneezing,
                "Hắt hơi — cảm cúm OTC",
                ["HO_HAP"],
                ["decolgen", "cam cum", "hat hoi", "hắt hơi"]),
            [PharmacyConsultationSymptoms.CommonCold] = new(
                PharmacyConsultationSymptoms.CommonCold,
                "Cảm lạnh — thuốc cảm cúm OTC",
                ["HO_HAP"],
                ["decolgen", "cam cum", "cam lanh", "cảm lạnh"]),
            [PharmacyConsultationSymptoms.Fever] = new(
                PharmacyConsultationSymptoms.Fever,
                "Sốt — hạ sốt paracetamol OTC",
                ["GIAM_DAU"],
                ["paracetamol", "panadol", "efferalgan", "tatanol", "ha sot", "hạ sốt"]),
            [PharmacyConsultationSymptoms.Headache] = new(
                PharmacyConsultationSymptoms.Headache,
                "Đau đầu — giảm đau OTC",
                ["GIAM_DAU"],
                ["paracetamol", "panadol", "efferalgan", "ibuprofen", "brufen", "dau dau", "đau đầu"]),
            [PharmacyConsultationSymptoms.BodyAche] = new(
                PharmacyConsultationSymptoms.BodyAche,
                "Đau nhức — giảm đau OTC",
                ["GIAM_DAU", "NGAO_DUOC"],
                ["paracetamol", "panadol", "salonpas", "dau nhuc", "đau nhức"]),
            [PharmacyConsultationSymptoms.Chills] = new(
                PharmacyConsultationSymptoms.Chills,
                "Rét run — hạ sốt / cảm cúm OTC",
                ["GIAM_DAU", "HO_HAP"],
                ["paracetamol", "decolgen", "cam cum"]),
            [PharmacyConsultationSymptoms.Diarrhea] = new(
                PharmacyConsultationSymptoms.Diarrhea,
                "Tiêu chảy — men vi sinh / smecta OTC",
                ["DA_DAY"],
                ["smecta", "diosmectite", "gastropulgite", "tieu chay", "tiêu chảy"]),
            [PharmacyConsultationSymptoms.Nausea] = new(
                PharmacyConsultationSymptoms.Nausea,
                "Buồn nôn — chống nôn OTC (cần DS nếu mang thai)",
                ["DA_DAY"],
                ["motilium", "domperidone", "buon non", "buồn nôn"]),
            [PharmacyConsultationSymptoms.Vomiting] = new(
                PharmacyConsultationSymptoms.Vomiting,
                "Nôn — chống nôn / bù nước OTC",
                ["DA_DAY"],
                ["motilium", "domperidone", "non", "nôn", "smecta"]),
            [PharmacyConsultationSymptoms.Heartburn] = new(
                PharmacyConsultationSymptoms.Heartburn,
                "Ợ nóng — kháng acid OTC",
                ["DA_DAY"],
                ["omeprazole", "antacid", "o nong", "ợ nóng"]),
            [PharmacyConsultationSymptoms.Reflux] = new(
                PharmacyConsultationSymptoms.Reflux,
                "Trào ngược — PPI OTC",
                ["DA_DAY"],
                ["omeprazole", "trao nguoc", "trào ngược"]),
            [PharmacyConsultationSymptoms.Constipation] = new(
                PharmacyConsultationSymptoms.Constipation,
                "Táo bón — nhuận tràng OTC",
                ["DA_DAY"],
                ["constipation", "tao bon", "táo bón", "fiber"]),
            [PharmacyConsultationSymptoms.Bloating] = new(
                PharmacyConsultationSymptoms.Bloating,
                "Đầy bụng — tiêu hóa OTC",
                ["DA_DAY"],
                ["simethicone", "day bung", "đầy bụng", "motilium"]),
            [PharmacyConsultationSymptoms.AbdominalPain] = new(
                PharmacyConsultationSymptoms.AbdominalPain,
                "Đau bụng — cần hỏi thêm / DS nếu dữ dội",
                ["DA_DAY"],
                ["smecta", "dau bung", "đau bụng"]),
            [PharmacyConsultationSymptoms.Allergy] = new(
                PharmacyConsultationSymptoms.Allergy,
                "Dị ứng — kháng histamin OTC (hỏi tiền sử)",
                ["VITAMIN"],
                ["loratadine", "cetirizine", "clarityne", "zyrtec", "di ung", "dị ứng"]),
            [PharmacyConsultationSymptoms.ItchySkin] = new(
                PharmacyConsultationSymptoms.ItchySkin,
                "Ngứa da — kháng histamin / bôi OTC",
                ["VITAMIN", "NGAO_DUOC"],
                ["loratadine", "cetirizine", "ngua", "ngứa"]),
            [PharmacyConsultationSymptoms.Rash] = new(
                PharmacyConsultationSymptoms.Rash,
                "Phát ban — kháng histamin OTC",
                ["VITAMIN"],
                ["loratadine", "cetirizine", "phat ban", "phát ban"]),
            [PharmacyConsultationSymptoms.Hives] = new(
                PharmacyConsultationSymptoms.Hives,
                "Mề đay — kháng histamin OTC",
                ["VITAMIN"],
                ["loratadine", "cetirizine", "me day", "mề đay"]),
            [PharmacyConsultationSymptoms.BackPain] = new(
                PharmacyConsultationSymptoms.BackPain,
                "Đau lưng — giảm đau / dán OTC",
                ["GIAM_DAU", "NGAO_DUOC"],
                ["paracetamol", "salonpas", "dau lung", "đau lưng"]),
            [PharmacyConsultationSymptoms.JointPain] = new(
                PharmacyConsultationSymptoms.JointPain,
                "Đau khớp — giảm đau OTC",
                ["GIAM_DAU", "NGAO_DUOC"],
                ["ibuprofen", "brufen", "salonpas", "dau khop", "đau khớp"]),
            [PharmacyConsultationSymptoms.Toothache] = new(
                PharmacyConsultationSymptoms.Toothache,
                "Đau răng — giảm đau OTC (nên khám nha)",
                ["GIAM_DAU"],
                ["paracetamol", "panadol", "dau rang", "đau răng"]),
            [PharmacyConsultationSymptoms.Fatigue] = new(
                PharmacyConsultationSymptoms.Fatigue,
                "Mệt mỏi — vitamin / bổ sung OTC",
                ["VITAMIN"],
                ["berocca", "redoxon", "vitamin", "met moi", "mệt mỏi"]),
            [PharmacyConsultationSymptoms.Dizziness] = new(
                PharmacyConsultationSymptoms.Dizziness,
                "Chóng mặt — cần hỏi thêm / DS",
                ["VITAMIN"],
                ["berocca", "vitamin", "chong mat", "chóng mặt"]),
            [PharmacyConsultationSymptoms.Insomnia] = new(
                PharmacyConsultationSymptoms.Insomnia,
                "Mất ngủ — OTC an thần nhẹ (cần DS)",
                ["VITAMIN"],
                ["melatonin", "mat ngu", "mất ngủ"]),
            [PharmacyConsultationSymptoms.Other] = new(
                PharmacyConsultationSymptoms.Other,
                "Triệu chứng khác — gợi ý OTC phổ biến",
                ["VITAMIN", "GIAM_DAU", "HO_HAP"],
                []),
        };

    internal static IReadOnlyList<RuleMatch> ResolveMatches(IReadOnlyList<string> symptoms)
    {
        var matches = new List<RuleMatch>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var symptom in symptoms)
        {
            if (string.IsNullOrWhiteSpace(symptom))
                continue;
            var code = symptom.Trim().ToLowerInvariant();
            if (!seen.Add(code))
                continue;
            if (BySymptom.TryGetValue(code, out var match))
                matches.Add(match);
        }

        return matches;
    }

    internal static IReadOnlyList<string> GetExcludeKeywords(PharmacyConsultationFactsDto facts)
    {
        var excludes = new List<string>();
        var infant = facts.AgeYears is <= 2
                       || (facts.AgeYears is null or 0 && facts.AgeMonths is <= 24);
        if (infant || facts.IsPregnant == true || facts.IsBreastfeeding == true)
        {
            excludes.AddRange(["ibuprofen", "brufen", "aspirin", "nsaid", "codeine"]);
        }

        if (facts.IsPregnant == true)
            excludes.AddRange(["domperidone", "motilium"]);

        return excludes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static string? PickReason(
        IReadOnlyList<RuleMatch> matches,
        string? categoryCode,
        string productName,
        string? genericName)
    {
        var haystack = $"{productName} {genericName}".ToLowerInvariant();
        foreach (var match in matches)
        {
            if (categoryCode is not null
                && match.CategoryCodes.Any(c => string.Equals(c, categoryCode, StringComparison.OrdinalIgnoreCase)))
            {
                return match.Reason;
            }

            if (match.Keywords.Any(k => haystack.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return match.Reason;
        }

        return matches.FirstOrDefault()?.Reason;
    }
}
