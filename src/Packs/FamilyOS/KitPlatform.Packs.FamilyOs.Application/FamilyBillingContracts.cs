namespace KitPlatform.Packs.FamilyOs;

public static class FamilyBillingPlans
{
    public const string StarterMonth = "starter_month";
    public const int DefaultStarterMonthAmountVnd = 99_000;
}

public static class FamilyBillingCheckoutStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Canceled = "canceled";
    public const string Expired = "expired";
}

public static class FamilyOsPayOsSettings
{
    public const string SectionName = "FamilyOs:PayOS";
}

public static class FamilyOsBillingSettings
{
    public const string SectionName = "FamilyOs:Billing";
}

public sealed class FamilyOsPayOsOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
}

public sealed class FamilyOsBillingOptions
{
    public int StarterMonthAmountVnd { get; set; } = FamilyBillingPlans.DefaultStarterMonthAmountVnd;
}

public sealed record CreateCheckoutRequest(
    string? PlanCode = null,
    string? ReturnUrl = null,
    string? CancelUrl = null);

public sealed record CheckoutDto(
    Guid Id,
    Guid FamilyId,
    long OrderCode,
    string PlanCode,
    int AmountVnd,
    string Status,
    string? CheckoutUrl,
    string? QrCode,
    string? Description,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ActivateOrderRequest(long OrderCode);

public interface IFamilyBillingService
{
    Task<CheckoutDto> CreateCheckoutAsync(
        Guid familyId,
        CreateCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<CheckoutDto> GetCheckoutAsync(
        Guid familyId,
        long orderCode,
        CancellationToken cancellationToken = default);

    Task HandlePayOsWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default);

    Task<CheckoutDto> ActivateFromOrderCodeAsync(
        long orderCode,
        CancellationToken cancellationToken = default);
}
