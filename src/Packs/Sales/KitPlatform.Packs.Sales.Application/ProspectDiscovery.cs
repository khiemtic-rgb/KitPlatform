using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Sales;

public sealed record DiscoverySource(
    string Code,
    string Status,
    string Reason,
    int MaxResults,
    TimeSpan MinInterval);

public sealed record PublicProspect(
    string ExternalId,
    string? Name,
    string? Amenity,
    string? Province,
    string? Address,
    string? Phone,
    string? Website,
    string SourceUrl,
    bool InsideIcpBounds,
    bool HasPrimaryName = false,
    double? Latitude = null,
    double? Longitude = null);

public sealed record ProspectStatement(string Kind, string Text);

public sealed record DiscoveryDecision(
    string Action,
    string Reason,
    int Priority,
    IReadOnlyList<ProspectStatement> Statements);

public sealed record DiscoveryMemory(
    IReadOnlySet<string> ExternalIds,
    IReadOnlySet<string> FoldedNames);

public static class NovixaSourceRegistry
{
    public static readonly DiscoverySource OsmOverpass = new(
        "osm_overpass",
        "enabled",
        "POI công khai OpenStreetMap, khung tỉnh Thái Nguyên, tối đa 15 điểm mỗi ngày.",
        15,
        TimeSpan.FromHours(24));

    public static readonly IReadOnlyList<DiscoverySource> All =
    [
        OsmOverpass,
        new("facebook", "blocked", "Không thu thập Facebook.", 0, TimeSpan.Zero),
        new("google_maps", "blocked", "Chưa có API được cấp và không phát sinh chi phí.", 0, TimeSpan.Zero),
        new("zalo", "blocked", "Chưa có tích hợp chính thức.", 0, TimeSpan.Zero),
        new("website_crawl", "blocked", "Chưa có nguồn website được cấp quyền.", 0, TimeSpan.Zero),
    ];

    public static DiscoverySource? Find(string code) =>
        All.FirstOrDefault(source => source.Code == code);

    public static bool AllowsCollection(string code) =>
        Find(code)?.Status == "enabled";

    public static readonly string[] OverpassInterpreters =
    [
        "https://overpass.openstreetmap.fr/api/interpreter",
        "https://overpass.osm.ch/api/interpreter",
        "https://overpass-api.de/api/interpreter",
        "https://overpass.kumi.systems/api/interpreter",
    ];
}

public static class DiscoverySchedule
{
    public static readonly TimeSpan SourceBackoff = TimeSpan.FromMinutes(5);

    public static string Gate(
        DateTimeOffset? lastSuccess,
        DateTimeOffset? lastFailure,
        DateTimeOffset now,
        TimeSpan successInterval)
    {
        if (lastSuccess is not null && now - lastSuccess.Value < successInterval)
            return "rate_limited";
        if (lastFailure is not null && now - lastFailure.Value < SourceBackoff)
            return "backoff";
        return "open";
    }
}

public static class ProspectNames
{
    public static string Fold(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var replaced = value.Replace('đ', 'd').Replace('Đ', 'D');
        var decomposed = replaced.Normalize(NormalizationForm.FormD);
        var buffer = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                buffer.Append(char.ToLowerInvariant(character));
        }

        return string.Join(
            ' ',
            buffer.ToString().Split([' ', ',', '.', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries));
    }

    public static string FoldBusiness(string? name)
    {
        var folded = Fold(name);
        foreach (var prefix in new[] { "nha thuoc", "quay thuoc", "pharmacy" })
        {
            if (folded == prefix)
                return "";
            if (folded.StartsWith(prefix + " ", StringComparison.Ordinal))
                return folded[(prefix.Length + 1)..];
        }

        return folded;
    }

    public static bool AcceptableBusinessName(string? name, bool hasPrimaryName)
    {
        var folded = Fold(name);
        if (FoldBusiness(name).Length == 0)
            return false;
        if (hasPrimaryName)
            return true;
        return folded.Contains("nha thuoc", StringComparison.Ordinal)
            || folded.Contains("pharmacy", StringComparison.Ordinal)
            || folded.Contains("quay thuoc", StringComparison.Ordinal);
    }
}

public static class ProspectDiscoveryPlanner
{
    public const int PriorityCap = 40;

    private static readonly string[] Chains =
    [
        "long chau",
        "pharmacity",
        "an khang",
        "trung son",
        "medicare",
        "guardian",
    ];

    public static DiscoveryDecision Decide(PublicProspect item, DiscoveryMemory memory)
    {
        var statements = Statements(item);
        var folded = ProspectNames.FoldBusiness(item.Name);
        if (folded.Length == 0)
            return new DiscoveryDecision("reject", "missing_name", 0, statements);
        if (!ProspectNames.AcceptableBusinessName(item.Name, item.HasPrimaryName))
            return new DiscoveryDecision("reject", "name_not_business", 0, statements);
        if (!string.Equals(item.Amenity, "pharmacy", StringComparison.OrdinalIgnoreCase))
            return new DiscoveryDecision("reject", "not_pharmacy", 0, statements);
        if (Chains.Any(chain => folded.Contains(chain, StringComparison.Ordinal)))
            return new DiscoveryDecision("reject", "chain", 0, statements);
        if (!InsideIcp(item))
            return new DiscoveryDecision("reject", "outside_province", 0, statements);
        if (memory.ExternalIds.Contains(item.ExternalId) || memory.FoldedNames.Contains(folded))
            return new DiscoveryDecision("skip_duplicate", "duplicate", 0, statements);

        return new DiscoveryDecision("create", "icp_pharmacy", Priority(item), statements);
    }

    private static bool InsideIcp(PublicProspect item)
    {
        var province = ProspectNames.Fold(item.Province);
        if (province.Length > 0)
            return province.Contains("thai nguyen", StringComparison.Ordinal);
        return item.InsideIcpBounds;
    }

    private static int Priority(PublicProspect item)
    {
        var score = 20;
        if (!string.IsNullOrWhiteSpace(item.Phone))
            score += 10;
        if (!string.IsNullOrWhiteSpace(item.Website))
            score += 5;
        if (!string.IsNullOrWhiteSpace(item.Address))
            score += 5;
        return Math.Min(score, PriorityCap);
    }

    private static List<ProspectStatement> Statements(PublicProspect item)
    {
        var statements = new List<ProspectStatement>();
        if (!string.IsNullOrWhiteSpace(item.Name))
            statements.Add(new("fact", "Tên công khai: " + item.Name.Trim()));
        if (string.Equals(item.Amenity, "pharmacy", StringComparison.OrdinalIgnoreCase))
            statements.Add(new("fact", "Nhãn amenity=pharmacy."));
        if (!string.IsNullOrWhiteSpace(item.Province))
            statements.Add(new("fact", "Địa bàn ghi trên nguồn: " + item.Province.Trim()));
        if (!string.IsNullOrWhiteSpace(item.Address))
            statements.Add(new("fact", "Địa chỉ công khai: " + item.Address.Trim()));
        if (!string.IsNullOrWhiteSpace(item.Phone))
            statements.Add(new("fact", "Điện thoại công khai có trên nguồn."));
        if (!string.IsNullOrWhiteSpace(item.Website))
            statements.Add(new("fact", "Website công khai có trên nguồn."));
        if (item.Latitude is double latitude && item.Longitude is double longitude)
            statements.Add(new("fact", string.Create(CultureInfo.InvariantCulture, $"Vị trí: {latitude:0.#####}, {longitude:0.#####}")));
        if (!string.IsNullOrWhiteSpace(item.SourceUrl))
            statements.Add(new("evidence", "Nguồn OpenStreetMap: " + item.SourceUrl));
        if (item.InsideIcpBounds && string.IsNullOrWhiteSpace(item.Province))
            statements.Add(new("evidence", "Điểm nằm trong khung truy vấn Thái Nguyên."));
        statements.Add(new("unknown", "Nhu cầu phần mềm chưa có bằng chứng."));
        return statements;
    }
}

public static class OverpassAttempt
{
    public static string Classify(string? transport, int? statusCode, string? body, int maxResults, out int count)
    {
        count = 0;
        if (transport is "timeout" or "dns" or "tls")
            return transport;
        if (statusCode is null or < 200 or >= 300)
            return "http_" + (statusCode?.ToString(CultureInfo.InvariantCulture) ?? "none");
        if (!OsmOverpassParser.TryParse(body ?? "", maxResults, out var prospects, out var error))
            return error ?? "parse";
        count = prospects.Count;
        return count == 0 ? "empty" : "ok";
    }
}

public static class OsmOverpassParser
{
    public static bool TryParse(string json, int maxResults, out IReadOnlyList<PublicProspect> prospects, out string? error)
    {
        prospects = [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
            {
                error = "overpass_shape";
                return false;
            }

            var list = new List<PublicProspect>();
            foreach (var element in elements.EnumerateArray())
            {
                if (list.Count >= maxResults)
                    break;
                var prospect = Read(element);
                if (prospect is not null)
                    list.Add(prospect);
            }

            prospects = list;
            error = null;
            return true;
        }
        catch (JsonException)
        {
            error = "overpass_json";
            return false;
        }
    }

    private static PublicProspect? Read(JsonElement element)
    {
        var type = Text(element, "type");
        if (type is not ("node" or "way"))
            return null;
        if (!element.TryGetProperty("id", out var idElement))
            return null;
        var id = idElement.ValueKind == JsonValueKind.Number
            ? idElement.GetInt64().ToString(CultureInfo.InvariantCulture)
            : idElement.GetString();
        if (string.IsNullOrWhiteSpace(id))
            return null;
        var tags = element.TryGetProperty("tags", out var tagElement) && tagElement.ValueKind == JsonValueKind.Object
            ? tagElement
            : default;
        var externalId = type + "/" + id;
        var primaryName = Tag(tags, "name");
        return new PublicProspect(
            externalId,
            primaryName ?? Tag(tags, "name:vi") ?? Tag(tags, "name:en"),
            Tag(tags, "amenity"),
            Tag(tags, "addr:province") ?? Tag(tags, "addr:city"),
            Address(tags),
            Tag(tags, "phone") ?? Tag(tags, "contact:phone"),
            Tag(tags, "website") ?? Tag(tags, "contact:website"),
            "https://www.openstreetmap.org/" + externalId,
            true,
            !string.IsNullOrWhiteSpace(primaryName),
            Number(element, "lat") ?? Number(element, "center", "lat"),
            Number(element, "lon") ?? Number(element, "center", "lon"));
    }

    private static string? Address(JsonElement tags)
    {
        var full = Tag(tags, "addr:full");
        if (!string.IsNullOrWhiteSpace(full))
            return full.Trim();
        var street = Tag(tags, "addr:street");
        var number = Tag(tags, "addr:housenumber");
        var joined = string.Join(' ', new[] { number, street }.Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) ? number : null;

    private static double? Number(JsonElement element, string parent, string name) =>
        element.TryGetProperty(parent, out var child) && child.ValueKind == JsonValueKind.Object
            ? Number(child, name)
            : null;

    private static string? Tag(JsonElement tags, string name) =>
        tags.ValueKind == JsonValueKind.Object && tags.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() : null;
}
