using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace PharmaCore.Application.Catalog;

public static class ProductNameNormalizer
{
    private static readonly Regex MultiSpace = new(@"\s+", RegexOptions.Compiled);

    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var collapsed = MultiSpace.Replace(name.Trim(), " ");
        var unaccented = RemoveDiacritics(collapsed);
        return unaccented.ToLowerInvariant();
    }

    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
