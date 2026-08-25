using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

internal static class ConsultationPreliminaryAssessment
{
    internal const string DisclaimerVi =
        "Nhận định dựa trên thông tin khách cung cấp, không thay thế chẩn đoán của người có chuyên môn.";

    private sealed record Cluster(string Code, string LabelVi, HashSet<string> SymptomCodes);

    private static readonly Cluster[] Clusters =
    [
        new(
            "upper_respiratory_mild",
            "cảm lạnh / nhiễm trùng hô hấp trên nhẹ",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "cough", "cough_dry", "cough_phlegm", "runny_nose", "sore_throat", "sneezing",
                "common_cold", "nasal_congestion", "flu_symptoms", "voice_hoarseness", "throat_clearing",
                "post_nasal_drip", "sinus_pressure",
            }),
        new(
            "allergic_rhinitis",
            "viêm mũi dị ứng / dị ứng đường mũi",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "allergy", "runny_nose", "sneezing", "itchy_eye", "hives", "nasal_congestion", "watery_eye",
            }),
        new(
            "digestive_acute",
            "rối loạn tiêu hóa cấp",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "diarrhea", "nausea", "vomiting", "abdominal_pain", "bloating", "dehydration_signs",
            }),
        new(
            "fever_pain",
            "sốt / đau cơ thể",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "fever", "headache", "body_ache", "chills", "fatigue",
            }),
        new(
            "gi_reflux",
            "khó tiêu / trào ngược dạ dày",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "heartburn", "reflux", "bloating", "nausea",
            }),
    ];

    private static readonly Dictionary<string, string> SymptomLabelsVi =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["cough"] = "Ho",
            ["cough_dry"] = "Ho khan",
            ["cough_phlegm"] = "Ho có đờm",
            ["runny_nose"] = "Sổ mũi",
            ["sore_throat"] = "Đau họng",
            ["sneezing"] = "Hắt hơi",
            ["common_cold"] = "Cảm lạnh",
            ["nasal_congestion"] = "Nghẹt mũi",
            ["flu_symptoms"] = "Triệu chứng cúm",
            ["fever"] = "Sốt",
            ["headache"] = "Đau đầu",
            ["diarrhea"] = "Tiêu chảy",
            ["allergy"] = "Dị ứng",
            ["abdominal_pain"] = "Đau bụng",
            ["voice_hoarseness"] = "Khàn tiếng",
        };

    internal static ConsultationPreliminaryAssessmentDto Build(
        PharmacyConsultationFactsDto facts,
        ConsultationSafetyRules.Evaluation safetyEval)
    {
        var normalized = ConsultationSafetyRules.NormalizeFacts(facts);
        var symptoms = normalized.Symptoms;
        var supporting = BuildSupportingLines(normalized);
        var missing = DetectMissingInfo(normalized, symptoms);

        if (safetyEval.SafetyLevel is "stop_sale" or "refer_medical"
            || normalized.RedFlags.Count > 0)
        {
            var flagSummary = safetyEval.Flags.Count > 0
                ? string.Join("; ", safetyEval.Flags.Select(f => f.Message))
                : "Có dấu hiệu cần được đánh giá y tế.";
            return new ConsultationPreliminaryAssessmentDto(
                "needs_evaluation",
                "Có dấu hiệu cần được đánh giá thêm",
                flagSummary,
                DisclaimerVi,
                supporting,
                [],
                missing,
                "Không tiếp tục dựa vào gợi ý OTC tự động; đề nghị dược sĩ đánh giá hoặc chuyển khám phù hợp.");
        }

        if (symptoms.Count == 0)
        {
            return new ConsultationPreliminaryAssessmentDto(
                "insufficient",
                "Chưa đủ thông tin để nhận định",
                "Cần hỏi thêm khách trước khi đưa ra nhận định sơ bộ.",
                DisclaimerVi,
                supporting,
                [],
                missing,
                null);
        }

        var scores = Clusters
            .Select(c => new { Cluster = c, Score = symptoms.Count(s => c.SymptomCodes.Contains(s)) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ToList();

        if (scores.Count == 0)
        {
            return new ConsultationPreliminaryAssessmentDto(
                missing.Count > 0 ? "insufficient" : "likely",
                missing.Count > 0 ? "Chưa đủ thông tin để nhận định" : "Có thể phù hợp với nhóm triệu chứng đã ghi nhận",
                BuildContextSummary(normalized, symptoms),
                DisclaimerVi,
                supporting,
                missing.Count > 0
                    ? []
                    :
                    [
                        new ConsultationPreliminaryHypothesisDto(
                            "general_symptoms",
                            "primary",
                            "nhóm triệu chứng khách mô tả",
                            "Phù hợp với các triệu chứng nhân viên đã ghi nhận — cần dược sĩ đối chiếu thêm.")
                    ],
                missing,
                null);
        }

        var primary = scores[0];
        var hypotheses = new List<ConsultationPreliminaryHypothesisDto>
        {
            new(
                primary.Cluster.Code,
                "primary",
                primary.Cluster.LabelVi,
                $"Phù hợp khá nhiều với {primary.Score} triệu chứng trong nhóm đã ghi nhận."),
        };

        if (scores.Count > 1
            && normalized.HasFever is false
            && scores[1].Cluster.Code == "allergic_rhinitis"
            && primary.Cluster.Code == "upper_respiratory_mild")
        {
            hypotheses.Add(new ConsultationPreliminaryHypothesisDto(
                scores[1].Cluster.Code,
                "alternate",
                scores[1].Cluster.LabelVi,
                "Có thể cân nhắc nếu hắt hơi / ngứa mũi nổi bật và không có sốt."));
        }

        var level = primary.Score >= 2 ? "likely" : "insufficient";
        var headline = level == "insufficient"
            ? "Chưa đủ thông tin để nhận định"
            : $"Có thể phù hợp với nhóm {primary.Cluster.LabelVi}";

        return new ConsultationPreliminaryAssessmentDto(
            level,
            headline,
            BuildContextSummary(normalized, symptoms),
            DisclaimerVi,
            supporting,
            hypotheses.Take(3).ToList(),
            missing,
            null);
    }

    private static List<string> BuildSupportingLines(PharmacyConsultationFactsDto facts)
    {
        var lines = new List<string>();
        foreach (var code in facts.Symptoms)
        {
            var label = SymptomLabelsVi.TryGetValue(code, out var vi) ? vi : code.Replace('_', ' ');
            lines.Add(label);
        }

        if (facts.HasFever == true) lines.Add("Có sốt");
        else if (facts.HasFever == false) lines.Add("Chưa ghi nhận sốt");

        if (facts.DurationDays is > 0)
            lines.Add($"Khởi phát khoảng {facts.DurationDays} ngày");

        if (facts.AgeYears is > 0)
            lines.Add($"Tuổi {facts.AgeYears}");

        if (!string.IsNullOrWhiteSpace(facts.Gender))
        {
            lines.Add(facts.Gender switch
            {
                "male" => "Nam",
                "female" => "Nữ",
                _ => facts.Gender,
            });
        }

        return lines;
    }

    private static string BuildContextSummary(
        PharmacyConsultationFactsDto facts,
        IReadOnlyList<string> symptoms)
    {
        var parts = new List<string>();
        if (symptoms.Count > 0)
        {
            var labels = symptoms
                .Take(4)
                .Select(s => SymptomLabelsVi.TryGetValue(s, out var vi) ? vi : s.Replace('_', ' '));
            parts.Add(string.Join(" + ", labels));
        }

        if (facts.DurationDays is > 0)
            parts.Add($"khoảng {facts.DurationDays} ngày");

        if (facts.HasFever == false)
            parts.Add("chưa ghi nhận sốt");
        else if (facts.HasFever == true)
            parts.Add("có sốt");

        return parts.Count > 0
            ? string.Join(", ", parts) + "."
            : "Thông tin triệu chứng đang được ghi nhận.";
    }

    private static List<string> DetectMissingInfo(
        PharmacyConsultationFactsDto facts,
        IReadOnlyList<string> symptoms)
    {
        var missing = new List<string>();
        var respiratory = symptoms.Any(s =>
            s is "cough" or "cough_dry" or "cough_phlegm" or "sore_throat" or "runny_nose" or "common_cold");

        if (facts.AgeYears is null && facts.AgeMonths is null)
            missing.Add("tuổi / độ tuổi");

        if (respiratory && facts.DurationDays is null)
            missing.Add("thời gian ho / triệu chứng");

        if (respiratory && facts.HasFever is null)
            missing.Add("có sốt hay không");

        if (symptoms.Contains("cough", StringComparer.OrdinalIgnoreCase)
            && !symptoms.Contains("cough_dry", StringComparer.OrdinalIgnoreCase)
            && !symptoms.Contains("cough_phlegm", StringComparer.OrdinalIgnoreCase))
        {
            missing.Add("ho khan hay có đờm");
        }

        if (respiratory
            && !facts.RedFlags.Contains("shortness_of_breath")
            && facts.Notes?.Contains("breathing:", StringComparison.OrdinalIgnoreCase) != true)
        {
            missing.Add("khó thở");
        }

        if (symptoms.Any(s => s is "diarrhea" or "vomiting" or "abdominal_pain") && facts.DurationDays is null)
            missing.Add("thời gian triệu chứng tiêu hóa");

        return missing.Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList();
    }
}
