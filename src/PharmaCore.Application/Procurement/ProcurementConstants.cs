namespace PharmaCore.Application.Procurement;

public static class SupplierStatuses
{
    public const short Active = 1;
    public const short Inactive = 2;
}

public static class PurchaseOrderStatuses
{
    public const short Draft = 1;
    public const short Approved = 2;
    public const short PartiallyReceived = 3;
    public const short Received = 4;
    public const short Closed = 5;
    public const short Cancelled = 6;
}

public static class GoodsReceiptStatuses
{
    public const short Draft = 1;
    public const short Completed = 2;
    public const short Cancelled = 3;
}

public static class PaymentMethods
{
    public const short Cash = 1;
    public const short BankTransfer = 2;
    public const short Other = 3;
}

public static class SupplierPaymentStatuses
{
    public const short Draft = 1;
    public const short Posted = 2;
    public const short Cancelled = 3;
}
