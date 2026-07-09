namespace KitPlatform.Packs.Pharmacy.Integration.Qd540;

public sealed record Qd540Table1RowDto(
    string MaThuoc,
    string TenThuoc,
    string SoDangKy,
    string TenHoatChat,
    string NongDoHamLuong,
    string NhaSanXuat,
    string NuocSanXuat,
    string NhaNhapKhau,
    string QuyCachDongGoi,
    string DangBaoChe,
    string DonViDongGoiNn,
    long GiaBanLe,
    string SoLo,
    int HanDung,
    decimal SoLuongNhap,
    decimal SoLuongBan,
    decimal SoLuongTon,
    string DonViBThuocChoCsbl,
    string SoHoaDonMThuoc,
    long? NgayNhap,
    long? NgayBan,
    string MaCoSoBanLe,
    string MaCoSoBanBuon);

public sealed record Qd540Table1Query(
    DateOnly From,
    DateOnly To,
    Guid? BranchId);

public sealed record Qd540Table1ExportResult(
    IReadOnlyList<Qd540Table1RowDto> Rows,
    IReadOnlyList<string> Warnings,
    int SkippedRows);

public enum Qd540EventKind
{
    Grn,
    Sale,
}

public sealed record Qd540Table1SourceRow(
    Qd540EventKind EventKind,
    Guid SourceId,
    Guid BranchId,
    string? RetailFacilityCode,
    string? WholesaleFacilityCode,
    string? NationalDrugId,
    string? NationalRegistrationNumber,
    string ProductName,
    string? GenericName,
    string? FirstIngredientName,
    string? FirstStrength,
    string? BrandName,
    string? ManufacturerAttr,
    string? CountryCode,
    string? ImporterName,
    string? Packaging,
    string? DosageForm,
    string? BaseUnitName,
    decimal ConversionFactor,
    decimal Quantity,
    decimal UnitPrice,
    string BatchNumber,
    DateOnly? ExpiryDate,
    string? SupplierName,
    string? SupplierInvoiceNumber,
    DateTime EventAt);
