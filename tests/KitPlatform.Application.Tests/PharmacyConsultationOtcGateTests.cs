using KitPlatform.Packs.Pharmacy.Consultation;
using KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;
using Xunit;

namespace KitPlatform.Application.Tests;

public sealed class PharmacyConsultationOtcGateTests
{
    [Fact]
    public void Adult_cough_and_sore_throat_stays_suggestable()
    {
        var facts = Facts(ageYears: 34, symptoms: ["cough", "sore_throat"], hasFever: false);
        var eval = ConsultationSafetyRules.Evaluate(facts);
        var assessment = ConsultationPreliminaryAssessment.Build(facts, eval);
        var matches = ConsultationOtcRules.ResolveMatches(facts.Symptoms);

        Assert.Equal("none", eval.SafetyLevel);
        Assert.False(ConsultationSafetyRules.BlocksAutomaticOtcSuggestions(eval.SafetyLevel));
        Assert.Equal("likely", assessment.Level);
        Assert.Contains(matches, m => m.CategoryCodes.Contains("HO_HAP"));
        Assert.Contains(matches, m => m.CategoryCodes.Contains("GIAM_DAU"));
    }

    [Fact]
    public void Seizure_stops_sale_and_blocks_otc()
    {
        var facts = Facts(ageYears: 34, symptoms: ["cough"], redFlags: ["seizure"]);
        var eval = ConsultationSafetyRules.Evaluate(facts);

        Assert.Equal("stop_sale", eval.SafetyLevel);
        Assert.True(ConsultationSafetyRules.BlocksAutomaticOtcSuggestions(eval.SafetyLevel));
        Assert.Contains("cấp cứu", ConsultationSafetyRules.AutomaticOtcBlockMessage(eval), StringComparison.Ordinal);
    }

    [Fact]
    public void Infant_fever_refers_medical_and_blocks_otc()
    {
        var facts = Facts(ageYears: 1, symptoms: ["fever"], hasFever: true);
        var eval = ConsultationSafetyRules.Evaluate(facts);

        Assert.Equal("refer_medical", eval.SafetyLevel);
        Assert.True(ConsultationSafetyRules.BlocksAutomaticOtcSuggestions(eval.SafetyLevel));
        Assert.Contains("sốt", ConsultationSafetyRules.AutomaticOtcBlockMessage(eval), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pharmacist_referral_still_allows_suggestions()
    {
        var facts = Facts(ageYears: 30, symptoms: ["cough"], isPregnant: true);
        var eval = ConsultationSafetyRules.Evaluate(facts);

        Assert.Equal("refer_pharmacist", eval.SafetyLevel);
        Assert.False(ConsultationSafetyRules.BlocksAutomaticOtcSuggestions(eval.SafetyLevel));
    }

    private static PharmacyConsultationFactsDto Facts(
        int ageYears,
        string[] symptoms,
        bool? hasFever = null,
        bool? isPregnant = null,
        string[]? redFlags = null) =>
        new(
            ageYears,
            null,
            "female",
            symptoms,
            2,
            hasFever,
            isPregnant,
            false,
            redFlags ?? [],
            null);
}
