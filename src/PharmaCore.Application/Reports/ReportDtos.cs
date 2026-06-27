namespace PharmaCore.Application.Reports;

public sealed record ReportCatalogItemDto(
    string Code,
    string Name,
    string Category,
    string Description,
    bool RequiresSales,
    bool RequiresProcurement,
    bool RequiresInventory);

public sealed record ReportColumnDto(
    string Key,
    string Title,
    string Format,
    string Align);

public sealed record ReportTableResultDto(
    string ReportCode,
    string Title,
    DateTime GeneratedAtUtc,
    IReadOnlyDictionary<string, string> FilterLabels,
    IReadOnlyList<ReportColumnDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyDictionary<string, object?>? Totals);

public sealed record ReportDateRangeQuery
{
    public DateTime FromUtc { get; init; }
    public DateTime ToUtc { get; init; }
}
