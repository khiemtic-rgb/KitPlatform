namespace KitPlatform.Packs.Pharmacy.Catalog;

public sealed class NationalDrugCatalogSettings
{
    public const string SectionName = "NationalDrugCatalog";

    /// <summary>mock | sandbox | live</summary>
    public string Mode { get; init; } = "mock";

    /// <summary>
    /// Base URL API v2 (không có slash cuối). Để trống thì suy ra theo Mode:
    /// sandbox → https://api-sandbox.csdlduoc.com.vn/v2 ; live → https://api.csdlduoc.com.vn/v2
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>Tên đăng nhập cơ sở do TTYQG cấp.</summary>
    public string? Username { get; init; }

    /// <summary>Mật khẩu dạng plain — client sẽ Base64 theo đặc tả API 1.1.</summary>
    public string? Password { get; init; }

    /// <summary>Nếu đã lưu sẵn Base64 thì đặt true (không encode lại).</summary>
    public bool PasswordIsBase64 { get; init; }

    public int TimeoutSeconds { get; init; } = 45;

    /// <summary>
    /// API list hiện không filter theo tên — khi có search text, quét tối đa N trang (page_size≤50)
    /// rồi lọc nội bộ. Tra cứu chính xác nên dùng mã thuốc / SĐK.
    /// </summary>
    public int MaxSearchScanPages { get; init; } = 40;

    /// <summary>
    /// Khi true + Mode sandbox/live có credentials: đẩy phiếu xuất bán lẻ (stock-out)
    /// sau khi đơn bán hoàn tất. Mặc định false — bật chủ động khi UAT.
    /// </summary>
    public bool EnableStockOutSync { get; init; }

    /// <summary>Số GCN đủ điều kiện KD thuốc (practice_license_code) — chuỗi nhà thuốc.</summary>
    public string? PracticeLicenseCode { get; init; }

    /// <summary>
    /// unit_id CSDL dược dùng khi không lấy được từ packagings của thuốc (vd U31 = Viên).
    /// </summary>
    public string DefaultCsdlUnitId { get; init; } = "U31";

    public bool CanSyncStockOut =>
        EnableStockOutSync
        && UsesRemoteApi
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password);

    public string ResolveBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(BaseUrl))
            return BaseUrl.Trim().TrimEnd('/');

        return NormalizeMode(Mode) switch
        {
            "live" => "https://api.csdlduoc.com.vn/v2",
            "sandbox" => "https://api-sandbox.csdlduoc.com.vn/v2",
            _ => "https://api-sandbox.csdlduoc.com.vn/v2",
        };
    }

    public static string NormalizeMode(string? mode) =>
        (mode ?? "mock").Trim().ToLowerInvariant() switch
        {
            "live" => "live",
            "sandbox" => "sandbox",
            _ => "mock",
        };

    public bool UsesRemoteApi =>
        NormalizeMode(Mode) is "sandbox" or "live";
}
