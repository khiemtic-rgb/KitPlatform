namespace KitPlatform.Packs.Sales;

public sealed record DirectoryAccount(
    string SourceKey,
    string Name,
    string? Province,
    string DocNumber,
    string DocDate,
    string SourceUrl,
    int PdfPage);

public sealed record AccountImportItem(string Action, string SourceKey, string Name, string? ReviewReason);

public sealed record AccountImportPlan(IReadOnlyList<AccountImportItem> Items, int LeadsCreated);

public static class SalesAccountImport
{
    public const int MaxRows = 500;

    public static void Guard(IReadOnlyList<DirectoryAccount> rows)
    {
        if (rows.Count > MaxRows)
            throw new InvalidOperationException("IMPORT_TOO_LARGE");
        if (rows.Any(row => string.IsNullOrWhiteSpace(row.SourceKey) || string.IsNullOrWhiteSpace(row.Name)))
            throw new InvalidOperationException("IMPORT_ROW_INVALID");
    }

    public static AccountImportPlan Plan(
        IReadOnlyList<DirectoryAccount> rows,
        IReadOnlySet<string> existingSourceKeys)
    {
        Guard(rows);
        var seen = new HashSet<string>(existingSourceKeys, StringComparer.Ordinal);
        var accepted = new List<DirectoryAccount>();
        var items = new List<AccountImportItem>();
        foreach (var row in rows)
        {
            if (!seen.Add(row.SourceKey))
            {
                items.Add(new AccountImportItem("skip", row.SourceKey, row.Name, null));
                continue;
            }

            accepted.Add(row);
        }

        var folded = accepted
            .GroupBy(row => Fold(row.Name))
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var row in accepted)
        {
            items.Add(new AccountImportItem("insert_account", row.SourceKey, row.Name, null));
            if (folded[Fold(row.Name)] > 1)
                items.Add(new AccountImportItem("open_review", row.SourceKey, row.Name, "duplicate_name"));
        }

        return new AccountImportPlan(items, 0);
    }

    public static string Fold(string name) => name.Trim().ToLowerInvariant();
}
