namespace KitPlatform.Packs.Connect;

/// <summary>SKU nhà thuốc đối tác — dùng khi clinic kê đơn cho bệnh nhân từ Connect.</summary>
public sealed record ConnectPartnerProductDto(
    Guid ProductId,
    string ProductCode,
    string ProductName,
    string? GenericName,
    string? DefaultUnitName,
    decimal StockAvailableQty);

public interface IConnectPartnerCatalogService
{
    /// <summary>
    /// Tìm thuốc active của NT partner khi có org_link active.
    /// Caller phải là clinic (hoặc bất kỳ tenant đã link) — chỉ đọc catalog NT.
    /// </summary>
    Task<IReadOnlyList<ConnectPartnerProductDto>> SearchPharmacyProductsAsync(
        Guid pharmacyTenantId,
        string? query,
        CancellationToken cancellationToken = default);
}
