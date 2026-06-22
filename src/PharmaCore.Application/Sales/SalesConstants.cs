namespace PharmaCore.Application.Sales;

public static class SalesOrderStatuses
{
    public const short Draft = 1;
    public const short Completed = 2;
    public const short Cancelled = 3;
    public const short Refunded = 4;
}

public static class SalesDiscountTypes
{
    public const short Percent = 1;
    public const short Fixed = 2;
}

public static class SalesDiscountPermissions
{
    public const string Apply = "sales.discount";
    public const string Unlimited = "sales.discount.unlimited";
}

public static class SalesDiscountLimits
{
    public const decimal StaffMaxPercent = 10m;
}

public static class SalesPriceTypes
{
    public const short Retail = 1;
}

public static class SalesPaymentMethods
{
    public const short Cash = 1;
    public const short Card = 2;
    public const short Transfer = 3;
    public const short EWallet = 4;
}

public static class SalesReturnStatuses
{
    public const short Completed = 2;
}

public static class SalesShiftStatuses
{
    public const short Open = 1;
    public const short Closed = 2;
}
