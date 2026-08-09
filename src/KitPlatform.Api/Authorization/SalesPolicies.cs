namespace KitPlatform.Api.Authorization;

public static class SalesPolicies
{
    public const string Read = "SalesRead";
    public const string Write = "SalesWrite";
    public const string Pos = "SalesPos";
    public const string Customers = "SalesCustomers";
    /// <summary>
    /// Quầy hoặc bàn KH: tạo CRM / xác nhận member (sales.pos | sales.customers | sales.write).
    /// </summary>
    public const string PosOrCustomers = "SalesPosOrCustomers";
    public const string Settings = "SalesSettings";
    /// <summary>AC5 — cancel draft sales orders (sales.cancel or ADMIN).</summary>
    public const string Cancel = "SalesCancel";
    /// <summary>Merge duplicate customers (sales.customers.merge or ADMIN) — not implied by sales.customers.</summary>
    public const string CustomerMerge = "SalesCustomerMerge";
    /// <summary>Báo cáo sửa giá + đồng bộ giá bán catalog (sales.price.manage or ADMIN).</summary>
    public const string PriceManage = "SalesPriceManage";
}
