using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Infrastructure;

internal static class ContentPlainText
{
    public static string FromMarkdown(string markdown, bool singleLine = false)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var s = markdown.Replace("\r\n", "\n", StringComparison.Ordinal);

        s = Regex.Replace(s, @"```[\s\S]*?```", "");
        s = Regex.Replace(s, @"!\[([^\]]*)\]\([^)]+\)", "$1");
        s = Regex.Replace(s, @"\[([^\]]+)\]\(([^)]+)\)", "$1");
        s = Regex.Replace(s, @"\*\*\*(.+?)\*\*\*", "$1");
        s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
        s = Regex.Replace(s, @"__(.+?)__", "$1");
        s = Regex.Replace(s, @"(?<!\w)\*(?!\*)(.+?)(?<!\*)\*(?!\w)", "$1");
        s = Regex.Replace(s, @"(?<!\w)_(?!_)(.+?)(?<!_)_(?!\w)", "$1");
        // A heading needs a space after the hashes; "#NguoiGiuVon" is a hashtag.
        s = Regex.Replace(s, @"^#{1,6}[ \t]+", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^>\s?", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^\s*[-*+]\s+", "• ", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);
        s = Regex.Replace(s, @"^---+\s*$", "", RegexOptions.Multiline);
        s = s.Replace("`", "", StringComparison.Ordinal);
        s = s.Replace("**", "", StringComparison.Ordinal);
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        if (singleLine)
            s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }
}
