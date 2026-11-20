using System.Globalization;
using System.Text;
using KitPlatform.Packs.Pharmacy.Consultation;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Consultation;

/// <summary>
/// Screening questions staff must ask before handing over a suggested product
/// whose active ingredient carries a common contraindication. Cautions only:
/// this never adds products, it only flags the ones already suggested.
/// </summary>
internal static class ConsultationProductScreening
{
    internal sealed record Group(string FlagCode, string[] Markers, string Question);

    internal sealed record Result(
        ConsultationSafetyRules.Evaluation Evaluation,
        List<PharmacyConsultationProductSuggestionDto> Suggestions);

    internal static readonly Group[] Groups =
    [
        new(
            "screen_decongestant",
            ["phenylephrin", "pseudoephedrin", "ephedrin", "decolgen", "tiffy", "actifed", "clarinase"],
            "Có thuốc co mạch (phenylephrin/pseudoephedrin). Hỏi: tăng huyết áp, bệnh tim, cường giáp, tăng nhãn áp, "
            + "phì đại tiền liệt tuyến, đang dùng thuốc chống trầm cảm. Có một trong các bệnh này thì không bán, chuyển dược sĩ."),
        new(
            "screen_nsaid",
            ["ibuprofen", "brufen", "diclofenac", "meloxicam", "naproxen", "piroxicam", "celecoxib", "aspirin", "acetylsalicylic"],
            "Có thuốc giảm đau chống viêm (NSAID). Hỏi: viêm loét dạ dày, bệnh thận, hen, đang dùng thuốc chống đông, "
            + "mang thai. Có thì không bán, chuyển dược sĩ."),
        new(
            "screen_paracetamol",
            ["paracetamol", "acetaminophen", "panadol", "efferalgan", "hapacol", "tatanol", "decolgen"],
            "Có paracetamol. Hỏi: bệnh gan, uống rượu nhiều, đang dùng thuốc khác cũng chứa paracetamol. "
            + "Không bán trùng hoạt chất."),
        new(
            "screen_sedating_antihistamine",
            ["clorpheniramin", "chlorpheniramin", "diphenhydramin", "promethazin"],
            "Có kháng histamin gây buồn ngủ. Hỏi: lái xe hoặc vận hành máy, người cao tuổi, tăng nhãn áp, "
            + "phì đại tiền liệt tuyến."),
        new(
            "screen_domperidone",
            ["domperidon", "motilium"],
            "Có domperidon. Hỏi: bệnh tim, loạn nhịp, đang dùng thuốc tim mạch. Không dùng kéo dài."),
        new(
            "screen_loperamide",
            ["loperamid"],
            "Có loperamid. Hỏi: tiêu chảy có máu, sốt cao, trẻ dưới 6 tuổi. Có thì không bán, chuyển dược sĩ."),
    ];

    internal static Result Apply(
        IReadOnlyList<PharmacyConsultationProductSuggestionDto> suggestions,
        ConsultationSafetyRules.Evaluation eval)
    {
        var flags = eval.Flags.ToList();
        var level = eval.SafetyLevel;
        foreach (var group in Groups)
        {
            var names = suggestions
                .Where(item => Matches(group, item))
                .Select(item => item.ProductName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();
            if (names.Count == 0 || flags.Any(f => f.Code == group.FlagCode))
                continue;

            flags.Add(new PharmacyConsultationSafetyFlagDto(
                group.FlagCode,
                "caution",
                $"{group.Question} ({string.Join(", ", names)})"));
            if (ConsultationSafetyRules.LevelRank(level) < ConsultationSafetyRules.LevelRank("caution"))
                level = "caution";
        }

        return new Result(new ConsultationSafetyRules.Evaluation(level, flags), suggestions.ToList());
    }

    internal static bool Matches(Group group, PharmacyConsultationProductSuggestionDto item)
    {
        var haystack = Fold($"{item.ProductName} {item.GenericName}");
        return group.Markers.Any(marker => haystack.Contains(marker, StringComparison.Ordinal));
    }

    private static string Fold(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }
        return builder.ToString();
    }
}
