using System.Text.Json;
using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal static class ConsultationSafetyRules
{
    internal sealed record Evaluation(
        string SafetyLevel,
        IReadOnlyList<PharmacyConsultationSafetyFlagDto> Flags);

    internal static Evaluation Evaluate(PharmacyConsultationFactsDto facts)
    {
        var flags = new List<PharmacyConsultationSafetyFlagDto>();
        var maxLevel = 0;

        void Add(string code, string level, string message)
        {
            flags.Add(new PharmacyConsultationSafetyFlagDto(code, level, message));
            maxLevel = Math.Max(maxLevel, LevelRank(level));
        }

        var ageYears = facts.AgeYears;
        var ageMonths = facts.AgeMonths;
        var infant = ageYears is <= 2 || (ageYears is null or 0 && ageMonths is <= 24);

        if (infant)
        {
            Add(
                "infant_under_2",
                "refer_pharmacist",
                "Trẻ dưới 2 tuổi — không tự tư vấn OTC tại quầy; chuyển dược sĩ.");
        }

        if (facts.IsPregnant == true)
        {
            Add(
                "pregnant",
                "refer_pharmacist",
                "Khách mang thai — chỉ dược sĩ tư vấn và quyết định sản phẩm.");
        }

        if (facts.IsBreastfeeding == true)
        {
            Add(
                "breastfeeding",
                "refer_pharmacist",
                "Khách đang cho con bú — chỉ dược sĩ tư vấn và quyết định sản phẩm.");
        }

        foreach (var flag in facts.RedFlags ?? [])
        {
            switch (flag.Trim().ToLowerInvariant())
            {
                case "difficulty_breathing":
                case "shortness_of_breath":
                    Add(flag, "refer_medical", "Khó thở — không bán OTC; hướng cấp cứu / bác sĩ ngay.");
                    break;
                case "chest_pain":
                    Add(flag, "refer_medical", "Đau ngực — không bán OTC; hướng cấp cứu / bác sĩ ngay.");
                    break;
                case "unconscious":
                case "seizure":
                    Add(flag, "stop_sale", "Ngất / co giật — dừng bán, gọi cấp cứu.");
                    break;
                case "severe_bleeding":
                    Add(flag, "refer_medical", "Chảy máu nặng — không bán OTC; hướng cấp cứu.");
                    break;
                case "swallowing_difficulty":
                    Add(flag, "refer_pharmacist", "Khó nuốt — chuyển dược sĩ, không tự chọn thuốc.");
                    break;
                case "high_fever_infant":
                    Add(flag, "refer_medical", "Trẻ nhỏ sốt cao — hướng khám ngay.");
                    break;
                default:
                    Add(flag, "caution", "Dấu hiệu cần lưu ý — xác nhận lại với dược sĩ.");
                    break;
            }
        }

        if (facts.HasFever == true && infant)
        {
            Add(
                "infant_fever",
                "refer_medical",
                "Trẻ nhỏ có sốt — không tự tư vấn OTC; hướng khám.");
        }

        if (facts.Symptoms.Contains(PharmacyConsultationSymptoms.Allergy, StringComparer.OrdinalIgnoreCase)
            && flags.All(f => f.Code != "pregnant"))
        {
            Add(
                "allergy_symptom",
                "caution",
                "Triệu chứng dị ứng — hỏi thêm tiền sử dị ứng / phản ứng thuốc trước khi gợi ý.");
        }

        if (facts.Symptoms.Contains(PharmacyConsultationSymptoms.ShortnessOfBreath, StringComparer.OrdinalIgnoreCase)
            && flags.All(f => f.Code is not ("difficulty_breathing" or "shortness_of_breath")))
        {
            Add(
                "shortness_of_breath",
                "refer_medical",
                "Khó thở — không bán OTC; hướng cấp cứu / bác sĩ ngay.");
        }

        var level = maxLevel switch
        {
            >= 4 => "stop_sale",
            3 => "refer_medical",
            2 => "refer_pharmacist",
            1 => "caution",
            _ => "none",
        };

        return new Evaluation(level, flags);
    }

    internal static Evaluation MergeSymptomRiskFlags(
        Evaluation baseEval,
        IReadOnlyList<SymptomTaxonomyRepository.SymptomRiskRow> symptomRisks)
    {
        if (symptomRisks.Count == 0)
            return baseEval;

        var flags = baseEval.Flags.ToList();
        var maxLevel = LevelRank(baseEval.SafetyLevel);

        foreach (var risk in symptomRisks)
        {
            if (flags.Any(f => string.Equals(f.Code, risk.FlagCode, StringComparison.OrdinalIgnoreCase)))
                continue;

            flags.Add(new PharmacyConsultationSafetyFlagDto(
                risk.FlagCode,
                risk.SafetyLevel,
                risk.MessageVi));
            maxLevel = Math.Max(maxLevel, LevelRank(risk.SafetyLevel));
        }

        var level = maxLevel switch
        {
            >= 4 => "stop_sale",
            3 => "refer_medical",
            2 => "refer_pharmacist",
            1 => "caution",
            _ => "none",
        };

        return new Evaluation(level, flags);
    }

    internal static PharmacyConsultationFactsDto NormalizeFacts(PharmacyConsultationFactsDto facts)
    {
        var symptoms = (facts.Symptoms ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var redFlags = (facts.RedFlags ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return facts with
        {
            Gender = string.IsNullOrWhiteSpace(facts.Gender)
                ? null
                : facts.Gender.Trim().ToLowerInvariant(),
            Symptoms = symptoms,
            RedFlags = redFlags,
            Notes = string.IsNullOrWhiteSpace(facts.Notes) ? null : facts.Notes.Trim(),
        };
    }

    internal static string SerializeFacts(PharmacyConsultationFactsDto facts) =>
        JsonSerializer.Serialize(NormalizeFacts(facts));

    internal static PharmacyConsultationFactsDto DeserializeFacts(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PharmacyConsultationFactsDto>(json)
                   ?? EmptyFacts();
        }
        catch
        {
            return EmptyFacts();
        }
    }

    internal static PharmacyConsultationFactsDto EmptyFacts() =>
        new(null, null, null, [], null, null, null, null, [], null);

    private static int LevelRank(string level) => level switch
    {
        "stop_sale" => 4,
        "refer_medical" => 3,
        "refer_pharmacist" => 2,
        "caution" => 1,
        _ => 0,
    };
}
