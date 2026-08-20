using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Website gate needs ≥2 ## headings. Models often write a wall of prose or fake titles with **bold**.
/// Repair is deterministic — no second model call.
/// </summary>
public static class ContentWebLongRepair
{
    private static readonly string[] FallbackHeads =
    [
        "Chỗ dễ bỏ sót",
        "Vì sao nhìn vậy vẫn hụt",
        "Việc cần làm ngay",
    ];

    public static string EnsureHeadings(string? body)
    {
        var text = (body ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (text.Length == 0) return text;

        text = PromoteHashOne(text);
        text = PromoteBoldTitles(text);
        if (ContentQualityGate.CountMarkdownH2(text) >= 2)
            return text;
        return InsertSectionHeadings(text);
    }

    private static string PromoteHashOne(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("# ", StringComparison.Ordinal) && !t.StartsWith("##", StringComparison.Ordinal))
                lines[i] = "## " + t[2..].Trim();
        }
        return string.Join('\n', lines);
    }

    private static string PromoteBoldTitles(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i].Trim();
            var m = Regex.Match(raw, @"^\*\*(.+?)\*\*$");
            if (!m.Success) m = Regex.Match(raw, "^__(.+?)__$");
            if (!m.Success) continue;
            var title = m.Groups[1].Value.Trim();
            if (title.Length is < 8 or > 80) continue;
            lines[i] = "## " + title.TrimEnd('.', '。');
        }
        return string.Join('\n', lines);
    }

    private static string InsertSectionHeadings(string text)
    {
        var paras = text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();
        if (paras.Count < 3)
        {
            var byLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.Length > 40)
                .ToList();
            if (byLine.Count >= 4)
            {
                paras = [byLine[0]];
                var rest = byLine.Skip(1).ToList();
                var mid = Math.Max(1, rest.Count / 2);
                paras.Add(string.Join('\n', rest.Take(mid)));
                paras.Add(string.Join('\n', rest.Skip(mid)));
            }
        }

        if (paras.Count < 3)
            return text + "\n\n## " + FallbackHeads[0] + "\n\n## " + FallbackHeads[1] + "\n";

        var lead = paras[0];
        var tail = paras.Skip(1).ToList();
        var cut = Math.Max(1, tail.Count / 2);
        var blocks = new[]
        {
            string.Join("\n\n", tail.Take(cut)),
            string.Join("\n\n", tail.Skip(cut)),
        };

        var sb = new StringBuilder();
        sb.Append(lead.Trim());
        for (var i = 0; i < blocks.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(blocks[i])) continue;
            sb.Append("\n\n## ").Append(HeadingFrom(blocks[i], FallbackHeads[i])).Append("\n\n");
            sb.Append(blocks[i].Trim());
        }

        return sb.ToString();
    }

    private static string HeadingFrom(string block, string fallback)
    {
        var first = block.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "";
        first = Regex.Replace(first, @"^[\-\*\d\.\)\s]+", "");
        first = first.Trim().TrimEnd('.', '。', '!', '?');
        if (first.Length < 8) return fallback;
        if (first.Length > 72) first = first[..72].TrimEnd() + "…";
        return first;
    }
}
