using KitPlatform.Application.CustomerApp;
using Xunit;

namespace KitPlatform.Platform.Tests;

public class CustomerCareCardRulesTests
{
    private static CustomerCareCardProductRow Product(string name, string? generic = null, string? category = null, string? ingredients = null) =>
        new(name, generic, category, ingredients);

    [Fact]
    public void Amlodipine_elderly_in_january_gets_hypertension_cold_card()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 1, 15),
            new DateOnly(1954, 3, 1),
            [Product("Amlodipin 5mg")]));

        Assert.NotNull(card);
        Assert.Equal(CustomerCareCardGroups.Hypertension, card.Group);
        Assert.Equal(CustomerCareCardClimate.Cold, card.Climate);
        Assert.True(card.Elderly);
        Assert.Equal("hypertension_elderly_cold", card.Code);
        Assert.Contains("huyết áp", card.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("không tự", card.Body, StringComparison.Ordinal);
        Assert.Equal("/chat", card.CtaPath);
        Assert.DoesNotContain("chẩn đoán", card.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Không chẩn đoán", card.Disclaimer, StringComparison.Ordinal);
    }

    [Fact]
    public void Joint_product_in_nồm_month_uses_humid_overlay()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 3, 10),
            new DateOnly(1988, 1, 1),
            [Product("Viên xương khớp Glucosamine")]));

        Assert.NotNull(card);
        Assert.Equal(CustomerCareCardGroups.Joint, card.Group);
        Assert.Equal(CustomerCareCardClimate.Humid, card.Climate);
        Assert.False(card.Elderly);
        Assert.Contains("khớp", card.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ventolin_in_december_is_respiratory_cold()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 12, 2),
            null,
            [Product("Ventolin Inhaler", ingredients: "Salbutamol")]));

        Assert.NotNull(card);
        Assert.Equal(CustomerCareCardGroups.Respiratory, card.Group);
        Assert.Equal(CustomerCareCardClimate.Cold, card.Climate);
        Assert.Contains("không tự thêm kháng sinh", card.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Metformin_elderly_in_july_is_diabetes_heat()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 7, 8),
            new DateOnly(1950, 6, 1),
            [Product("Metformin 850mg")]));

        Assert.NotNull(card);
        Assert.Equal(CustomerCareCardGroups.Diabetes, card.Group);
        Assert.Equal(CustomerCareCardClimate.Hot, card.Climate);
        Assert.True(card.Elderly);
        Assert.Contains("đường huyết", card.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vitamin_or_painkiller_alone_does_not_create_a_card()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 1, 15),
            new DateOnly(1950, 1, 1),
            [
                Product("Vitamin C 500mg"),
                Product("Paracetamol 500mg"),
                Product("Ibuprofen 400mg"),
                Product("Phenylephrine cảm cúm"),
            ]));

        Assert.Null(card);
    }

    [Fact]
    public void No_products_means_no_weather_essay()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 1, 15),
            new DateOnly(1940, 1, 1),
            []));

        Assert.Null(card);
    }

    [Fact]
    public void Detects_vietnamese_diacritics_on_huyết_áp()
    {
        var groups = CustomerCareCardRules.DetectGroups([Product("Thuốc huyết áp Coversyl")]);
        Assert.Contains(groups, g => g.Group == CustomerCareCardGroups.Hypertension);
    }

    [Fact]
    public void Climate_relevant_group_wins_over_other_chronic_meds()
    {
        var card = CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            new DateOnly(2026, 7, 1),
            new DateOnly(1960, 1, 1),
            [
                Product("Viên xương khớp"),
                Product("Amlodipin 5mg"),
            ]));

        Assert.NotNull(card);
        Assert.Equal(CustomerCareCardGroups.Hypertension, card.Group);
        Assert.Equal(CustomerCareCardClimate.Hot, card.Climate);
    }

    [Fact]
    public void Fold_strips_vietnamese_marks()
    {
        Assert.Equal("huyet ap", CustomerCareCardRules.Fold("Huyết áp"));
        Assert.Equal("tieu duong", CustomerCareCardRules.Fold("tiểu đường"));
    }
}
