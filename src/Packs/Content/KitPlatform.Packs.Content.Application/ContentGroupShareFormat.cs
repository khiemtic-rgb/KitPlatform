using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>Normalize group copy so Facebook shows a clean member post, not a draft dump.</summary>
public static class ContentGroupShareFormat
{
    public static string Normalize(string? raw)
    {
        var t = (raw ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (t.Length == 0) return "";

        t = Regex.Replace(t, @"\*\*(.+?)\*\*", "$1");
        t = Regex.Replace(t, @"^#{1,6}\s+", "", RegexOptions.Multiline);

        var hashes = new List<string>();
        var blocks = new List<string>();
        foreach (var rawLine in t.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                if (blocks.Count > 0 && blocks[^1].Length > 0)
                    blocks.Add("");
                continue;
            }

            if (IsHashtagLine(trimmed))
            {
                foreach (Match tag in Regex.Matches(trimmed, @"#[\p{L}\p{Nd}_]+"))
                    hashes.Add(tag.Value);
                continue;
            }

            if (Regex.IsMatch(trimmed, @"^[-*]\s+"))
                trimmed = "• " + Regex.Replace(trimmed, @"^[-*]\s+", "");
            else if (trimmed.StartsWith('•') && !trimmed.StartsWith("• ", StringComparison.Ordinal))
                trimmed = "• " + trimmed.TrimStart('•', ' ');

            blocks.Add(trimmed);
        }

        while (blocks.Count > 0 && blocks[^1].Length == 0)
            blocks.RemoveAt(blocks.Count - 1);

        var sb = new StringBuilder();
        foreach (var line in blocks)
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(line);
        }

        if (hashes.Count > 0)
        {
            var uniq = hashes.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToList();
            if (sb.Length > 0) sb.Append("\n\n");
            sb.Append(string.Join(' ', uniq));
        }

        return sb.ToString().Trim();
    }

    private static bool IsHashtagLine(string line)
    {
        var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 && words.All(w => w.StartsWith('#'));
    }
}
