using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

public sealed record GeminiStillReference(string CharacterId, string Name, string Mime, string Sha256, byte[] Bytes);

/// <summary>
/// A paid still may be current only when every named character's official locked master
/// was frozen into the prompt and sent to the image provider.
/// </summary>
public static class ProductionLockedStillReference
{
    public const string Missing = "LOCKED_CHARACTER_REFERENCE_MISSING";
    public const string Stale = "STALE_PICTURE_REFERENCE";
    public const string NotSent = "LOCKED_CHARACTER_REFERENCE_NOT_SENT";
    public const string Current = "CURRENT_PICTURE_VALID";

    public static bool InheritFamixaShotCanon(string? seriesCode)
    {
        var code = (seriesCode ?? "").Trim();
        return code.Length == 0
            || !code.StartsWith("NOVIXA", StringComparison.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<(string Id, string Name)> Characters(string? visual, string? shotCode = null)
    {
        var parsed = Parse(visual);
        if (parsed.Count > 0) return parsed;
        if (string.Equals(shotCode, ProductionEp01Sh01IngressCanon.Shot, StringComparison.Ordinal))
            return Parse(ProductionEp01Sh01IngressCanon.VisualContract);
        if (string.Equals(shotCode, ProductionEp01Sh02IngressCanon.Shot, StringComparison.Ordinal))
            return Parse(ProductionEp01Sh02IngressCanon.VisualContract);
        return parsed;
    }

    public static string Token(IEnumerable<(string Id, string Sha)> rows) =>
        string.Join(' ', rows
            .Select(row => $"{row.Id.Trim().ToUpperInvariant()}:{row.Sha.Trim().ToLowerInvariant()}")
            .Where(row => row.Length > 8)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(row => row, StringComparer.Ordinal));

    public static bool Bound(string? prompt, string token) =>
        !string.IsNullOrWhiteSpace(token)
        && (prompt ?? "").Contains(token, StringComparison.Ordinal);

    public static IReadOnlyList<(string Id, string Sha)> Frozen(string? prompt)
    {
        var found = new List<(string Id, string Sha)>();
        foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
            prompt ?? "", @"CHAR-\d+:[0-9a-f]{64}", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var parts = match.Value.Split(':');
            var row = (parts[0].ToUpperInvariant(), parts[1].ToLowerInvariant());
            if (!found.Exists(existing => existing.Id == row.Item1))
                found.Add(row);
        }
        return found;
    }

    static List<(string Id, string Name)> Parse(string? visual)
    {
        var found = new List<(string Id, string Name)>();
        var raw = (visual ?? "").Trim();
        if (!raw.StartsWith('{')) return found;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("primary", out var primary))
                Add(primary, found);
            if (root.TryGetProperty("secondary", out var secondary) && secondary.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in secondary.EnumerateArray())
                    Add(item, found);
            }
        }
        catch (JsonException)
        {
            return [];
        }
        return found;
    }

    static void Add(JsonElement node, List<(string Id, string Name)> found)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("id", out var idEl))
            return;
        var id = (idEl.GetString() ?? "").Trim().ToUpperInvariant();
        if (id.Length == 0) return;
        if (found.Exists(row => string.Equals(row.Id, id, StringComparison.Ordinal))) return;
        var name = node.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? "").Trim() : "";
        if (name.Length == 0) name = id;
        found.Add((id, name));
    }
}

public interface IProductionLockedMasterSource
{
    Task<GeminiStillReference?> ReadOfficialAsync(string characterId, string name, CancellationToken ct = default);
}
