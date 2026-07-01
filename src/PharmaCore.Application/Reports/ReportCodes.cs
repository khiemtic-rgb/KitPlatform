namespace PharmaCore.Application.Reports;

public static class ReportCodes
{
    public const string SalesRevenueByPeriod = "SALES-01";
    public const string SalesRevenueByPaymentMethod = "SALES-02";
    public const string SalesShifts = "SALES-03";
    public const string SalesRevenueByCategory = "SALES-04";
    public const string ProcurementGrnValue = "PROC-01";
    public const string ProcurementPayablesSnapshot = "PROC-03";
    public const string InventoryStockSnapshot = "INV-01";
    public const string InventoryNearExpiry = "INV-02";
}

public static class ReportGroupBy
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Supplier = "supplier";
}

public static class ReportColumnFormats
{
    public const string Text = "text";
    public const string Money = "money";
    public const string Qty = "qty";
    public const string Date = "date";
    public const string Integer = "integer";
}
