using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

public sealed class ConsultationNegationAndScreeningTests
{
    private static PharmacyConsultationFactsDto Local(string text, params string[] quick)
    {
        var facts = ConsultationTextParser.EnrichFromNaturalLanguage(
            PharmacyConsultationGeminiClient.FactsFromQuickOnly(quick),
            text,
            quick);
        return ConsultationTextParser.ApplyTextSymptoms(facts, text, quick, null);
    }

    [Fact]
    public void Denied_fever_is_not_a_symptom()
    {
        var facts = Local("Người lớn ho khan 2 ngày, không sốt");

        Assert.Contains("cough_dry", facts.Symptoms);
        Assert.DoesNotContain("fever", facts.Symptoms);
        Assert.False(facts.HasFever);
    }

    [Fact]
    public void Stated_fever_with_denied_cough()
    {
        var facts = Local("Bé 6 tuổi sốt cao, không ho");

        Assert.Contains("fever", facts.Symptoms);
        Assert.DoesNotContain("cough", facts.Symptoms);
        Assert.True(facts.HasFever);
    }

    [Fact]
    public void Resolved_fever_with_ongoing_cough()
    {
        var facts = Local("hết sốt nhưng còn ho có đờm");

        Assert.Contains("cough_phlegm", facts.Symptoms);
        Assert.DoesNotContain("fever", facts.Symptoms);
        Assert.False(facts.HasFever);
    }

    [Fact]
    public void Chained_denial_covers_both_symptoms()
    {
        var facts = Local("không sốt hay đau họng, sổ mũi");

        Assert.DoesNotContain("fever", facts.Symptoms);
        Assert.DoesNotContain("sore_throat", facts.Symptoms);
        Assert.Contains("runny_nose", facts.Symptoms);
    }

    [Fact]
    public void Ho_k_is_shorthand_not_denial()
    {
        var facts = Local("ho k 3 ngày");

        Assert.Contains("cough_dry", facts.Symptoms);
    }

    [Fact]
    public void Denied_diarrhea_keeps_abdominal_pain()
    {
        var facts = Local("chưa bị tiêu chảy, đau bụng");

        Assert.DoesNotContain("diarrhea", facts.Symptoms);
        Assert.Contains("abdominal_pain", facts.Symptoms);
    }

    [Fact]
    public void Staff_picked_fever_wins_over_text_denial()
    {
        var facts = Local("không sốt", "fever");

        Assert.Contains("fever", facts.Symptoms);
        Assert.True(facts.HasFever);
    }

    [Fact]
    public void Model_fever_is_dropped_when_text_denies_it()
    {
        var model = new PharmacyConsultationFactsDto(
            35, null, null, ["cough_dry", "fever"], 2, true, null, null, [], null);
        var enriched = ConsultationTextParser.EnrichFromNaturalLanguage(model, "ho khan 2 ngày, không sốt", []);
        var facts = ConsultationTextParser.ApplyTextSymptoms(enriched, "ho khan 2 ngày, không sốt", [], null);

        Assert.DoesNotContain("fever", facts.Symptoms);
        Assert.False(facts.HasFever);
    }

    [Fact]
    public void Red_flag_phrase_is_never_masked()
    {
        var scan = ConsultationTextParser.ScanNegations("không khó thở");

        Assert.Contains("kho tho", scan.MaskedText);
        Assert.Empty(scan.NegatedCodes);
    }

    [Fact]
    public void Fever_symptom_is_removed_when_screening_says_no_fever()
    {
        var facts = ConsultationSafetyRules.NormalizeFacts(new PharmacyConsultationFactsDto(
            35, null, null, ["fever", "cough_dry"], null, false, null, null, [], null));

        Assert.Equal(["cough_dry"], facts.Symptoms);
    }

    private static PharmacyConsultationProductSuggestionDto Product(string name, string? generic) =>
        new(Guid.NewGuid(), name, name, generic, name, Guid.NewGuid(), "Viên", 1000, 5, "r", "keyword");

    [Fact]
    public void Decongestant_requires_blood_pressure_screening()
    {
        var eval = new ConsultationSafetyRules.Evaluation("none", []);
        var result = ConsultationProductScreening.Apply(
            [Product("Decolgen Forte", null), Product("Tiffy", "Paracetamol; Phenylephrin HCl"), Product("Prospan", "Cao lá thường xuân")],
            eval);

        Assert.Equal("caution", result.Evaluation.SafetyLevel);
        var flag = Assert.Single(result.Evaluation.Flags, f => f.Code == "screen_decongestant");
        Assert.Contains("tăng huyết áp", flag.Message);
        Assert.Contains("Decolgen Forte", flag.Message);
        Assert.DoesNotContain("Prospan", flag.Message);
        Assert.Contains(result.Evaluation.Flags, f => f.Code == "screen_paracetamol");
        Assert.Equal(3, result.Suggestions.Count);
    }

    [Fact]
    public void Screening_never_lowers_an_existing_level()
    {
        var eval = new ConsultationSafetyRules.Evaluation(
            "refer_pharmacist",
            [new PharmacyConsultationSafetyFlagDto("pregnant", "refer_pharmacist", "x")]);
        var result = ConsultationProductScreening.Apply([Product("Ibuprofen 400", "Ibuprofen")], eval);

        Assert.Equal("refer_pharmacist", result.Evaluation.SafetyLevel);
        Assert.Contains(result.Evaluation.Flags, f => f.Code == "screen_nsaid");
    }

    [Fact]
    public void No_screening_flag_without_a_matching_product()
    {
        var eval = new ConsultationSafetyRules.Evaluation("none", []);
        var result = ConsultationProductScreening.Apply([Product("Prospan", "Cao lá thường xuân")], eval);

        Assert.Equal("none", result.Evaluation.SafetyLevel);
        Assert.Empty(result.Evaluation.Flags);
    }
}
