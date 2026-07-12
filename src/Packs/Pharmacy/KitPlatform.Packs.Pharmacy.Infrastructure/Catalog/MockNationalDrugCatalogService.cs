using Microsoft.Extensions.Options;
using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal sealed class MockNationalDrugCatalogService : INationalDrugCatalogService
{
    private static readonly IReadOnlyList<NationalDrugFieldMapDto> FieldMap =
    [
        new("maThuoc", "Mã thuốc (CSDL QG)", "nationalDrugId", "Mã liên kết QG", "Khóa tham chiếu — không hiển thị POS"),
        new("soDangKy", "Số đăng ký lưu hành", "nationalRegistrationNumber", "Số ĐK lưu hành", "Lưu trên sản phẩm để đối soát"),
        new("tenThuoc", "Tên thuốc", "productName", "Tên sản phẩm", "Tên thương mại trên nhãn"),
        new("tenHoatChat", "Tên hoạt chất", "genericName", "Tên hoạt chất / generic", "Gộp với hàm lượng nếu có"),
        new("hamLuong", "Hàm lượng", "genericName", "Tên hoạt chất / generic", "Nối sau tên hoạt chất"),
        new("dangBaoChe", "Dạng bào chế", "description", "Mô tả", "Ghi chú tham chiếu QG"),
        new("quyCachDongGoi", "Quy cách đóng gói", "description", "Mô tả", "Ghi chú tham chiếu QG"),
        new("donViTinh", "Đơn vị tính", "saleUnitName", "ĐVT cơ sở", "ĐVT bán nhỏ nhất"),
        new("maVach", "Mã vạch / GTIN", "primaryBarcode", "Barcode chính", "Gợi ý — chỉnh tại tab Chi tiết"),
        new("loaiThuoc", "Loại thuốc", "drugType", "Loại thuốc", "OTC → 1, Kê đơn → 2"),
        new("tenNhaSanXuat", "Nhà sản xuất", "description", "Mô tả", "Ghi chú tham chiếu QG"),
        new("nuocSanXuat", "Nước sản xuất", "description", "Mô tả", "Ghi chú tham chiếu QG"),
    ];

    private static readonly IReadOnlyList<MockDrugRecord> Catalog =
    [
        new(
            "DRUG-VN-000001",
            "VD-1234-23",
            "Paracetamol 500mg",
            "Paracetamol",
            "500 mg",
            "Viên nén",
            "Hộp 10 vỉ x 10 viên",
            "Viên",
            "DHG Pharma",
            "Việt Nam",
            "OTC",
            "8930000000101",
            "N02BE01",
            "Uống",
            new DateOnly(2028, 6, 30)),
        new(
            "DRUG-VN-000002",
            "VD-5678-22",
            "Amoxicillin 500mg",
            "Amoxicillin",
            "500 mg",
            "Viên nang cứng",
            "Hộp 2 vỉ x 10 viên",
            "Viên",
            "Imexpharm",
            "Việt Nam",
            "RX",
            "8930000000200",
            "J01CA04",
            "Uống",
            new DateOnly(2027, 12, 31)),
        new(
            "DRUG-VN-000003",
            "VD-9012-21",
            "Vitamin C 1000mg",
            "Acid ascorbic (Vitamin C)",
            "1000 mg",
            "Viên sủi",
            "Tuýp 20 viên",
            "Viên",
            "Sanofi",
            "Pháp",
            "OTC",
            "8930000000309",
            "A11GA01",
            "Uống",
            new DateOnly(2029, 3, 15)),
        new(
            "DRUG-VN-000004",
            "VD-3456-24",
            "Paracetamol Extra",
            "Paracetamol + Caffeine",
            "500 mg + 65 mg",
            "Viên nén bao phim",
            "Hộp 2 vỉ x 10 viên",
            "Viên",
            "Stada",
            "Đức",
            "OTC",
            "8930000000408",
            "N02BE51",
            "Uống",
            new DateOnly(2028, 9, 1)),
        new(
            "DRUG-VN-000005",
            "VD-7788-20",
            "Omeprazole 20mg",
            "Omeprazole",
            "20 mg",
            "Viên nang cứng tan trong ruột",
            "Hộp 3 vỉ x 10 viên",
            "Viên",
            "Domesco",
            "Việt Nam",
            "RX",
            "8930000000507",
            "A02BC01",
            "Uống",
            new DateOnly(2027, 8, 20)),
        new(
            "DRUG-VN-000006",
            "VD-1122-19",
            "Salbutamol 2mg/5ml",
            "Salbutamol",
            "2 mg/5 ml",
            "Siro",
            "Chai 60 ml",
            "Chai",
            "Pharmedic",
            "Việt Nam",
            "RX",
            "8930000000606",
            "R03AC02",
            "Uống",
            new DateOnly(2026, 11, 30)),
        new(
            "DRUG-VN-000007",
            "VD-4455-24",
            "Calcium 500mg",
            "Calcium carbonate",
            "500 mg",
            "Viên nén",
            "Hộp 10 vỉ x 10 viên",
            "Viên",
            "Mekophar",
            "Việt Nam",
            "OTC",
            "8930000000705",
            "A12AA04",
            "Uống",
            new DateOnly(2028, 4, 30)),
        new(
            "DRUG-VN-000008",
            "VD-5566-23",
            "Captopril 25mg",
            "Captopril",
            "25 mg",
            "Viên nén",
            "Hộp 10 vỉ x 10 viên",
            "Viên",
            "Domesco",
            "Việt Nam",
            "RX",
            "8930000000804",
            "C09AA01",
            "Uống",
            new DateOnly(2027, 10, 15)),
        new(
            "DRUG-VN-000009",
            "VD-6677-22",
            "Cefixime 200mg",
            "Cefixime",
            "200 mg",
            "Viên nang cứng",
            "Hộp 1 vỉ x 10 viên",
            "Viên",
            "Imexpharm",
            "Việt Nam",
            "RX",
            "8930000000903",
            "J01DD08",
            "Uống",
            new DateOnly(2027, 5, 20)),
        new(
            "DRUG-VN-000010",
            "VD-7789-21",
            "Cetirizine 10mg",
            "Cetirizine",
            "10 mg",
            "Viên nén bao phim",
            "Hộp 1 vỉ x 10 viên",
            "Viên",
            "Stada",
            "Đức",
            "OTC",
            "8930000001009",
            "R06AE07",
            "Uống",
            new DateOnly(2028, 1, 12)),
    ];

    private readonly NationalDrugCatalogSettings _settings;

    public MockNationalDrugCatalogService(IOptions<NationalDrugCatalogSettings> settings) =>
        _settings = settings.Value;

    public Task<NationalDrugConnectionStatusDto> GetConnectionStatusAsync(CancellationToken cancellationToken = default)
    {
        var mode = NormalizeMode(_settings.Mode);
        var (label, isLive, message) = mode switch
        {
            "live" => ("Liên thông thật", true, "Đang kết nối CSDL Dược QG theo QĐ 522."),
            "sandbox" => ("Sandbox", false, "Môi trường thử nghiệm — cần tài khoản Sở Y tế."),
            _ => ("Mock (nội bộ)", false, "Dữ liệu mẫu nội bộ — chờ hồ sơ liên thông Novixa (QĐ 522)."),
        };

        return Task.FromResult(new NationalDrugConnectionStatusDto(mode, label, isLive, message));
    }

    public IReadOnlyList<NationalDrugFieldMapDto> GetFieldMap() => FieldMap;

    public Task<PagedNationalDrugListResult> SearchAsync(
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var q = search?.Trim();
        IEnumerable<MockDrugRecord> query = Catalog;
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(d =>
                d.DrugId.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.RegistrationNumber.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.ProductName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (d.ActiveIngredient?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.Barcode?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var list = query.OrderBy(d => d.ProductName).ToList();
        var total = list.Count;
        var items = list
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToListItem)
            .ToList();

        return Task.FromResult(new PagedNationalDrugListResult(items, total, page, pageSize));
    }

    public Task<NationalDrugDetailDto?> GetAsync(string drugId, CancellationToken cancellationToken = default)
    {
        var record = Catalog.FirstOrDefault(d =>
            string.Equals(d.DrugId, drugId?.Trim(), StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(record is null ? null : ToDetail(record));
    }

    public Task<NationalDrugProductPrefillDto?> BuildProductPrefillAsync(
        string drugId,
        CancellationToken cancellationToken = default)
    {
        var record = Catalog.FirstOrDefault(d =>
            string.Equals(d.DrugId, drugId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (record is null) return Task.FromResult<NationalDrugProductPrefillDto?>(null);

        var generic = BuildGenericName(record);
        var description = BuildDescription(record);

        return Task.FromResult<NationalDrugProductPrefillDto?>(new NationalDrugProductPrefillDto(
            record.DrugId,
            record.RegistrationNumber,
            record.ProductName,
            generic,
            MapDrugType(record.DrugCategoryCode),
            record.UnitName,
            description,
            record.Barcode));
    }

    private static string NormalizeMode(string? mode) =>
        (mode ?? "mock").Trim().ToLowerInvariant() switch
        {
            "live" => "live",
            "sandbox" => "sandbox",
            _ => "mock",
        };

    private static NationalDrugListItemDto ToListItem(MockDrugRecord d) =>
        new(
            d.DrugId,
            d.RegistrationNumber,
            d.ProductName,
            d.ActiveIngredient,
            d.Strength,
            d.DosageForm,
            d.UnitName,
            d.Manufacturer,
            MapDrugCategoryLabel(d.DrugCategoryCode));

    private static NationalDrugDetailDto ToDetail(MockDrugRecord d) =>
        new(
            d.DrugId,
            d.RegistrationNumber,
            d.ProductName,
            d.ActiveIngredient,
            d.Strength,
            d.DosageForm,
            d.Packaging,
            d.UnitName,
            d.Manufacturer,
            d.CountryOfOrigin,
            d.DrugCategoryCode,
            MapDrugCategoryLabel(d.DrugCategoryCode),
            d.Barcode,
            d.AtcCode,
            d.RouteOfAdministration,
            d.RegistrationExpiryDate);

    private static string? BuildGenericName(MockDrugRecord d)
    {
        if (string.IsNullOrWhiteSpace(d.ActiveIngredient)) return null;
        if (string.IsNullOrWhiteSpace(d.Strength)) return d.ActiveIngredient;
        return $"{d.ActiveIngredient} {d.Strength}".Trim();
    }

    private static string BuildDescription(MockDrugRecord d)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(d.DosageForm)) parts.Add($"Dạng bào chế: {d.DosageForm}");
        if (!string.IsNullOrWhiteSpace(d.Packaging)) parts.Add($"Quy cách: {d.Packaging}");
        if (!string.IsNullOrWhiteSpace(d.Manufacturer)) parts.Add($"NSX: {d.Manufacturer}");
        if (!string.IsNullOrWhiteSpace(d.CountryOfOrigin)) parts.Add($"Xuất xứ: {d.CountryOfOrigin}");
        if (!string.IsNullOrWhiteSpace(d.AtcCode)) parts.Add($"ATC: {d.AtcCode}");
        parts.Add($"Số ĐK: {d.RegistrationNumber}");
        return string.Join(" · ", parts);
    }

    private static short MapDrugType(string categoryCode) =>
        categoryCode.ToUpperInvariant() switch
        {
            "RX" => 2,
            "CONTROLLED" => 3,
            _ => 1,
        };

    private static string MapDrugCategoryLabel(string categoryCode) =>
        categoryCode.ToUpperInvariant() switch
        {
            "RX" => "Kê đơn",
            "CONTROLLED" => "Kiểm soát",
            _ => "OTC",
        };

    private sealed record MockDrugRecord(
        string DrugId,
        string RegistrationNumber,
        string ProductName,
        string? ActiveIngredient,
        string? Strength,
        string? DosageForm,
        string? Packaging,
        string UnitName,
        string? Manufacturer,
        string? CountryOfOrigin,
        string DrugCategoryCode,
        string? Barcode,
        string? AtcCode,
        string? RouteOfAdministration,
        DateOnly? RegistrationExpiryDate);
}
