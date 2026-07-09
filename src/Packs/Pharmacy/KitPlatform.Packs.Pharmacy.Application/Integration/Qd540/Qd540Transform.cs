using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Pharmacy.Integration.Qd540;

public static class Qd540Transform
{
    private static readonly TimeZoneInfo VietnamTimeZone = ResolveVietnamTimeZone();

    private static readonly Dictionary<string, string> CountryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VN"] = "Việt Nam",
        ["US"] = "Hoa Kỳ",
        ["IN"] = "Ấn Độ",
        ["CN"] = "Trung Quốc",
        ["KR"] = "Hàn Quốc",
        ["JP"] = "Nhật Bản",
        ["DE"] = "Đức",
        ["FR"] = "Pháp",
        ["GB"] = "Anh",
        ["IT"] = "Ý",
        ["TH"] = "Thái Lan",
        ["SG"] = "Singapore",
        ["AU"] = "Úc",
        ["CH"] = "Thụy Sĩ",
        ["BE"] = "Bỉ",
        ["PL"] = "Ba Lan",
        ["RU"] = "Nga",
        ["TW"] = "Đài Loan",
        ["ID"] = "Indonesia",
        ["MY"] = "Malaysia",
    };

    public static string? EncodeMaThuoc540(string? soDangKy, string? packaging)
    {
        if (string.IsNullOrWhiteSpace(soDangKy)) return null;
        var reg = NormalizeAlphaNum(soDangKy);
        if (string.IsNullOrWhiteSpace(packaging))
            return Truncate(reg, 50);
        var pack = NormalizeAlphaNum(packaging);
        return Truncate(reg + pack, 50);
    }

    public static int? FormatHanDung540(DateOnly? expiryDate) =>
        expiryDate is null ? null : int.Parse(expiryDate.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

    public static long FormatDateTime540(DateTime utcDateTime)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            VietnamTimeZone);
        return long.Parse(local.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));
    }

    public static decimal ToSmallestUnitQty(decimal qty, decimal conversionFactor) =>
        Math.Round(qty * conversionFactor, 3, MidpointRounding.AwayFromZero);

    public static long ToRetailPriceVnd(decimal unitPrice, decimal conversionFactor)
    {
        if (conversionFactor <= 0) conversionFactor = 1m;
        var perSmallest = unitPrice / conversionFactor;
        return (long)Math.Round(perSmallest, 0, MidpointRounding.AwayFromZero);
    }

    public static string MapCountryName(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return string.Empty;
        return CountryNames.TryGetValue(countryCode.Trim(), out var name)
            ? Truncate(name, 20)
            : Truncate(countryCode.Trim(), 20);
    }

    public static Qd540Table1RowDto? MapSourceRow(Qd540Table1SourceRow row, ICollection<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(row.RetailFacilityCode))
        {
            warnings.Add($"Thiếu Ma_co_so_ban_le cho chi nhánh (source {row.SourceId}).");
            return null;
        }

        if (row.ExpiryDate is null)
        {
            warnings.Add($"Thiếu hạn dùng lô {row.BatchNumber} (source {row.SourceId}).");
            return null;
        }

        var packaging = row.Packaging ?? row.BaseUnitName ?? string.Empty;
        var maThuoc = !string.IsNullOrWhiteSpace(row.NationalDrugId)
            ? Truncate(NormalizeAlphaNum(row.NationalDrugId), 50)
            : EncodeMaThuoc540(row.NationalRegistrationNumber, packaging);

        if (string.IsNullOrWhiteSpace(maThuoc))
        {
            warnings.Add($"Không derive được ma_thuoc cho SP {row.ProductName} (source {row.SourceId}).");
            return null;
        }

        if (string.IsNullOrWhiteSpace(row.NationalRegistrationNumber))
            warnings.Add($"Thiếu so_dang_ky cho SP {row.ProductName} (source {row.SourceId}).");

        var tenHoatChat = Truncate(
            (row.GenericName ?? row.FirstIngredientName ?? row.ProductName).Trim(),
            50);
        var nongDo = Truncate((row.FirstStrength ?? string.Empty).Trim(), 20);
        var nhaSx = Truncate((row.BrandName ?? row.ManufacturerAttr ?? string.Empty).Trim(), 100);
        var hanDung = FormatHanDung540(row.ExpiryDate);
        if (hanDung is null) return null;

        var qtySmallest = ToSmallestUnitQty(row.Quantity, row.ConversionFactor <= 0 ? 1m : row.ConversionFactor);
        var giaBanLe = ToRetailPriceVnd(row.UnitPrice, row.ConversionFactor <= 0 ? 1m : row.ConversionFactor);
        var eventTs = FormatDateTime540(row.EventAt);

        return new Qd540Table1RowDto(
            MaThuoc: maThuoc,
            TenThuoc: Truncate(row.ProductName.Trim(), 50),
            SoDangKy: Truncate((row.NationalRegistrationNumber ?? string.Empty).Trim(), 20),
            TenHoatChat: tenHoatChat,
            NongDoHamLuong: nongDo,
            NhaSanXuat: nhaSx,
            NuocSanXuat: MapCountryName(row.CountryCode),
            NhaNhapKhau: Truncate((row.ImporterName ?? string.Empty).Trim(), 100),
            QuyCachDongGoi: Truncate(packaging.Trim(), 20),
            DangBaoChe: Truncate((row.DosageForm ?? string.Empty).Trim(), 20),
            DonViDongGoiNn: Truncate((row.BaseUnitName ?? string.Empty).Trim(), 20),
            GiaBanLe: giaBanLe,
            SoLo: Truncate(row.BatchNumber.Trim(), 20),
            HanDung: hanDung.Value,
            SoLuongNhap: row.EventKind == Qd540EventKind.Grn ? qtySmallest : 0m,
            SoLuongBan: row.EventKind == Qd540EventKind.Sale ? qtySmallest : 0m,
            SoLuongTon: 0m,
            DonViBThuocChoCsbl: Truncate((row.SupplierName ?? string.Empty).Trim(), 100),
            SoHoaDonMThuoc: Truncate((row.SupplierInvoiceNumber ?? string.Empty).Trim(), 20),
            NgayNhap: row.EventKind == Qd540EventKind.Grn ? eventTs : null,
            NgayBan: row.EventKind == Qd540EventKind.Sale ? eventTs : null,
            MaCoSoBanLe: Truncate(row.RetailFacilityCode.Trim(), 12),
            MaCoSoBanBuon: Truncate((row.WholesaleFacilityCode ?? string.Empty).Trim(), 12));
    }

    public static string ToCsv(IReadOnlyList<Qd540Table1RowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',',
            "ma_thuoc", "ten_thuoc", "so_dang_ky", "ten_hoat_chat", "nong_do_ham_luong",
            "nha_san_xuat", "nuoc_san_xuat", "nha_nhap_khau", "quy_cach_dong_goi", "dang_bao_che",
            "don_vi_dong_goi_nn", "gia_ban_le", "so_lo", "han_dung", "so_luong_nhap", "so_luong_ban",
            "so_luong_ton", "don_vi_bthuoc_cho_csbl", "so_hoa_don_mthuoc", "ngay_nhap", "ngay_ban",
            "Ma_co_so_ban_le", "Ma_co_so_ban_buon"));

        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(r.MaThuoc), Csv(r.TenThuoc), Csv(r.SoDangKy), Csv(r.TenHoatChat), Csv(r.NongDoHamLuong),
                Csv(r.NhaSanXuat), Csv(r.NuocSanXuat), Csv(r.NhaNhapKhau), Csv(r.QuyCachDongGoi), Csv(r.DangBaoChe),
                Csv(r.DonViDongGoiNn), r.GiaBanLe.ToString(CultureInfo.InvariantCulture),
                Csv(r.SoLo), r.HanDung.ToString(CultureInfo.InvariantCulture),
                r.SoLuongNhap.ToString(CultureInfo.InvariantCulture),
                r.SoLuongBan.ToString(CultureInfo.InvariantCulture),
                r.SoLuongTon.ToString(CultureInfo.InvariantCulture),
                Csv(r.DonViBThuocChoCsbl), Csv(r.SoHoaDonMThuoc),
                r.NgayNhap?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                r.NgayBan?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Csv(r.MaCoSoBanLe), Csv(r.MaCoSoBanBuon)));
        }

        return sb.ToString();
    }

    private static string Csv(string? value)
    {
        var s = value ?? string.Empty;
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
            return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }

    private static string NormalizeAlphaNum(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string Truncate(string value, int maxLen) =>
        value.Length <= maxLen ? value : value[..maxLen];

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
    }
}
