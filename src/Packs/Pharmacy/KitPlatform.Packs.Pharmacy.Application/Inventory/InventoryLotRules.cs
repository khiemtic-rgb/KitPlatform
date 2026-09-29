namespace KitPlatform.Packs.Pharmacy.Inventory;

public sealed record InventoryLotIdentity(
    bool Exists,
    bool HasConflict,
    string BatchNumber,
    DateOnly? ManufactureDate,
    DateOnly? ExpiryDate);

public static class InventoryLotRules
{
    public static string NormalizeBatchNumber(string? batchNumber)
    {
        if (string.IsNullOrWhiteSpace(batchNumber))
            return string.Empty;
        return string.Join(' ', batchNumber.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();
    }

    public static bool SameLot(string? left, string? right)
    {
        var a = NormalizeBatchNumber(left);
        return a.Length > 0 && a == NormalizeBatchNumber(right);
    }

    public static void EnsureDocumentLotsConsistent(
        IEnumerable<(Guid ProductId, string BatchNumber, DateOnly? ManufactureDate, DateOnly? ExpiryDate)> lines)
    {
        foreach (var group in lines.GroupBy(line => (line.ProductId, NormalizeBatchNumber(line.BatchNumber))))
        {
            if (group.Key.Item2.Length == 0)
                continue;
            var mfgs = group.Select(x => x.ManufactureDate).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            var exps = group.Select(x => x.ExpiryDate).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            if (mfgs.Count > 1 || exps.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Cùng số lô {group.Key.Item2} trên phiếu phải cùng một NSX và một HSD.");
            }
        }
    }

    public static (DateOnly? ManufactureDate, DateOnly ExpiryDate) Resolve(
        string batchNumber,
        DateOnly? incomingManufacture,
        DateOnly? incomingExpiry,
        InventoryLotIdentity? existing)
    {
        var lot = NormalizeBatchNumber(batchNumber);
        if (lot.Length == 0)
            throw new InvalidOperationException("Số lô không được để trống.");

        if (existing is { HasConflict: true })
        {
            throw new InvalidOperationException(
                $"Số lô {lot} đang có nhiều NSX/HSD trên các kho. Sửa tồn kho trước khi nhập thêm.");
        }

        var manufacture = MatchDate(lot, "NSX", incomingManufacture, existing?.ManufactureDate);
        var expiry = MatchDate(lot, "HSD", incomingExpiry, existing?.ExpiryDate);
        if (expiry is null)
            throw new InvalidOperationException($"Số lô {lot} bắt buộc có hạn dùng.");
        return (manufacture, expiry.Value);
    }

    private static DateOnly? MatchDate(string lot, string field, DateOnly? incoming, DateOnly? existing)
    {
        if (existing is DateOnly have && incoming is DateOnly next && have != next)
        {
            throw new InvalidOperationException(
                $"Số lô {lot} đã có {field} {have:dd/MM/yyyy}. Không được nhập {next:dd/MM/yyyy}.");
        }

        return incoming ?? existing;
    }
}
