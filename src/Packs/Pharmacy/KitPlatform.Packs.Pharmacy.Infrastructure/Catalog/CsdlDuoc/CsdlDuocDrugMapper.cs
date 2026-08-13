using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Catalog.CsdlDuoc;

internal static class CsdlDuocDrugMapper
{
    public static NationalDrugListItemDto ToListItem(CsdlDuocDrugDto d)
    {
        var (code, label) = MapCategory(d);
        var pack = d.Packagings?.FirstOrDefault();
        return new NationalDrugListItemDto(
            DrugId: d.Id ?? string.Empty,
            RegistrationNumber: d.RegistrationNumber ?? d.Id ?? string.Empty,
            ProductName: d.Name ?? string.Empty,
            ActiveIngredient: d.ActivePharmaceuticalIngredient,
            Strength: d.Strength,
            DosageForm: null,
            UnitName: pack?.UnitName,
            Manufacturer: d.Manufacturer?.Name,
            DrugCategoryLabel: label);
    }

    public static NationalDrugDetailDto ToDetail(CsdlDuocDrugDto d)
    {
        var (code, label) = MapCategory(d);
        var pack = d.Packagings?.FirstOrDefault();
        var packaging = d.Packagings is { Count: > 0 }
            ? string.Join(", ", d.Packagings
                .Select(p => p.UnitName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            : null;
        var route = d.Routes?.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.Name))?.Name;
        DateOnly? expiry = d.ExpiryDate is { } dt
            ? DateOnly.FromDateTime(dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime())
            : null;

        return new NationalDrugDetailDto(
            DrugId: d.Id ?? string.Empty,
            RegistrationNumber: d.RegistrationNumber ?? d.Id ?? string.Empty,
            ProductName: d.Name ?? string.Empty,
            ActiveIngredient: d.ActivePharmaceuticalIngredient,
            Strength: d.Strength,
            DosageForm: null,
            Packaging: string.IsNullOrWhiteSpace(packaging) ? null : packaging,
            UnitName: pack?.UnitName,
            Manufacturer: d.Manufacturer?.Name,
            CountryOfOrigin: d.Manufacturer?.Country,
            DrugCategoryCode: code,
            DrugCategoryLabel: label,
            Barcode: pack?.Gtin,
            AtcCode: null,
            RouteOfAdministration: route,
            RegistrationExpiryDate: expiry);
    }

    public static NationalDrugProductPrefillDto ToPrefill(CsdlDuocDrugDto d)
    {
        var detail = ToDetail(d);
        var generic = BuildGenericName(detail.ActiveIngredient, detail.Strength);
        var description = BuildDescription(detail, d.OldRegistrationNumber);
        return new NationalDrugProductPrefillDto(
            detail.DrugId,
            detail.RegistrationNumber,
            detail.ProductName,
            generic,
            MapDrugType(detail.DrugCategoryCode),
            detail.UnitName ?? "Viên",
            description,
            detail.Barcode);
    }

    public static bool MatchesSearch(CsdlDuocDrugDto d, string q) =>
        Contains(d.Id, q)
        || Contains(d.RegistrationNumber, q)
        || Contains(d.OldRegistrationNumber, q)
        || Contains(d.Name, q)
        || Contains(d.ActivePharmaceuticalIngredient, q)
        || (d.Packagings?.Any(p => Contains(p.Gtin, q)) ?? false);

    private static bool Contains(string? haystack, string needle) =>
        !string.IsNullOrWhiteSpace(haystack)
        && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static (string Code, string Label) MapCategory(CsdlDuocDrugDto d)
    {
        if ((d.SpecialControlType ?? 0) > 0)
            return ("CONTROLLED", "Kiểm soát");
        if ((d.PrescriptionStatus ?? 0) == 1)
            return ("RX", "Kê đơn");
        return ("OTC", "OTC");
    }

    private static short MapDrugType(string categoryCode) =>
        categoryCode.ToUpperInvariant() switch
        {
            "RX" => 2,
            "CONTROLLED" => 3,
            _ => 1,
        };

    private static string? BuildGenericName(string? ingredient, string? strength)
    {
        if (string.IsNullOrWhiteSpace(ingredient)) return null;
        if (string.IsNullOrWhiteSpace(strength)) return ingredient;
        return $"{ingredient} {strength}".Trim();
    }

    private static string BuildDescription(NationalDrugDetailDto d, string? oldReg)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(d.Packaging)) parts.Add($"ĐVT/QC: {d.Packaging}");
        if (!string.IsNullOrWhiteSpace(d.Manufacturer)) parts.Add($"NSX: {d.Manufacturer}");
        if (!string.IsNullOrWhiteSpace(d.CountryOfOrigin)) parts.Add($"Xuất xứ: {d.CountryOfOrigin}");
        if (!string.IsNullOrWhiteSpace(d.RouteOfAdministration)) parts.Add($"Đường dùng: {d.RouteOfAdministration}");
        parts.Add($"Số ĐK: {d.RegistrationNumber}");
        if (!string.IsNullOrWhiteSpace(oldReg)) parts.Add($"SĐK cũ: {oldReg}");
        return string.Join(" · ", parts);
    }
}
