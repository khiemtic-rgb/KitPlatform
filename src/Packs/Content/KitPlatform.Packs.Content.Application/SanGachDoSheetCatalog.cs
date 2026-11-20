namespace KitPlatform.Packs.Content.Application;

/// <summary>
/// Đọc tên file mẫu trong Bo_Chuan. Không copy ảnh vào git. Không dựng mesh.
/// </summary>
public static class SanGachDoSheetCatalog
{
    public const string FilmTitle = "Sân gạch đỏ";

    static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Bin"] = "Bin",
        ["Bo"] = "Bố",
        ["Di"] = "Dì",
        ["Duc"] = "Đức",
        ["Hoa"] = "Hoa",
        ["Hue"] = "Huệ",
        ["Hung"] = "Hùng",
        ["Huong"] = "Hương",
        ["Lan"] = "Lan",
        ["Minh"] = "Minh",
        ["Thao"] = "Thảo",
        ["Tuan"] = "Tuấn",
        ["DAOCU_mu_la_co"] = "Mũ lá cọ",
    };

    static readonly string[] SheetOrder = ["goc", "mat", "ngoi", "anh"];

    public sealed record Character(string Id, string Name, string Kind, IReadOnlyList<string> Sheets);

    public static IReadOnlyList<Character> Read(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return [];

        var grouped = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(root, "*.png"))
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrWhiteSpace(stem)) continue;
            var (id, kind) = Split(stem);
            if (!IsSafeToken(id) || !IsSafeToken(kind)) continue;
            if (!grouped.TryGetValue(id, out var sheets))
            {
                sheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                grouped[id] = sheets;
            }
            sheets.Add(kind);
        }

        return grouped
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => new Character(
                p.Key,
                Names.TryGetValue(p.Key, out var name) ? name : p.Key,
                p.Key.StartsWith("DAOCU_", StringComparison.OrdinalIgnoreCase) ? "prop" : "person",
                SheetOrder.Where(p.Value.Contains).ToArray()))
            .ToArray();
    }

    public static string? SheetPath(string root, string id, string kind)
    {
        if (!IsSafeToken(id) || !IsSafeToken(kind) || string.IsNullOrWhiteSpace(root))
            return null;
        var file = kind.Equals("anh", StringComparison.OrdinalIgnoreCase)
            ? id + ".png"
            : id + "_" + kind.ToLowerInvariant() + ".png";
        var full = Path.GetFullPath(Path.Combine(root, file));
        var baseFull = Path.GetFullPath(root);
        if (!baseFull.EndsWith(Path.DirectorySeparatorChar))
            baseFull += Path.DirectorySeparatorChar;
        if (!full.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
            return null;
        return full;
    }

    static (string Id, string Kind) Split(string stem)
    {
        foreach (var kind in new[] { "goc", "mat", "ngoi" })
        {
            var suffix = "_" + kind;
            if (stem.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && stem.Length > suffix.Length)
                return (stem[..^suffix.Length], kind);
        }
        return (stem, "anh");
    }

    static bool IsSafeToken(string value) =>
        value.Length is > 0 and <= 64 && value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
}
