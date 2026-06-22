namespace PharmaCore.Application.Integration;

public static class IntegrationOutboxEventTypes
{
    public const string OrderCompleted = "order.completed";
    public const string OrderCancelled = "order.cancelled";
    public const string SalesReturnCompleted = "sales_return.completed";
    public const string CustomerConsentUpdated = "customer.consent.updated";
}

public static class IntegrationOutboxAggregateTypes
{
    public const string SalesOrder = "sales_order";
    public const string SalesReturn = "sales_return";
    public const string CustomerConsent = "customer_consent";
    public const string Customer = "customer";
}
