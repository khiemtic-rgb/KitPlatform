using KitPlatform.Packs.Pharmacy.Catalog;

namespace KitPlatform.Packs.Pharmacy.Infrastructure;

internal static class NationalDrugCatalogFieldMap
{
    public static readonly IReadOnlyList<NationalDrugFieldMapDto> Items =
    [
        new("id", "Mã thuốc (CSDL dược)", "nationalDrugId", "Mã liên kết QG", "drug_id — khóa tham chiếu"),
        new("registration_number", "Số GPLH / SĐK", "nationalRegistrationNumber", "Số ĐK lưu hành", "Đối soát trên sản phẩm"),
        new("old_registration_number", "Số GPLH cũ", "description", "Mô tả", "Tham chiếu lịch sử SĐK"),
        new("name", "Tên thuốc", "productName", "Tên sản phẩm", "Tên thương mại"),
        new("active_pharmaceutical_ingredient", "Hoạt chất", "genericName", "Tên hoạt chất / generic", "Gộp với hàm lượng nếu có"),
        new("strength", "Hàm lượng", "genericName", "Tên hoạt chất / generic", "Nối sau tên hoạt chất"),
        new("packagings[].unit_name", "Đơn vị tính", "saleUnitName", "ĐVT cơ sở", "Lấy ĐVT nhỏ nhất từ packagings"),
        new("packagings[].gtin", "GTIN", "primaryBarcode", "Barcode chính", "Gợi ý — chỉnh tại tab Chi tiết"),
        new("prescription_status", "Thuốc kê đơn", "drugType", "Loại thuốc", "0=OTC→1, 1=RX→2"),
        new("special_control_type", "Kiểm soát đặc biệt", "drugType", "Loại thuốc", ">0 → CONTROLLED (3)"),
        new("manufacturer.name", "Nhà sản xuất", "description", "Mô tả", "Ghi chú tham chiếu QG"),
        new("manufacturer.country", "Nước sản xuất", "description", "Mô tả", "Mã quốc gia ISO"),
    ];
}
