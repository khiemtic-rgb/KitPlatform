namespace KitPlatform.Application.Payment;

public static class PaymentProductCodes
{
    public const string FamilyOs = "family_os";
}

public static class PaymentSubjectTypes
{
    public const string Family = "family";
    public const string Tenant = "tenant";
}

public static class PaymentProviderCodes
{
    public const string PayOs = "payos";
    public const string VnPay = "vnpay";
    public const string MoMo = "momo";
    public const string ZaloPay = "zalopay";
    public const string Manual = "manual";
}

public static class PaymentOrderStatuses
{
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Canceled = "canceled";
    public const string Expired = "expired";
}

public static class PaymentSubscriptionStatuses
{
    public const string Trial = "trial";
    public const string Active = "active";
    public const string PastDue = "past_due";
    public const string Expired = "expired";
    public const string Canceled = "canceled";
}

public static class PaymentSettings
{
    public const string SectionName = "Payment";
    public const string PayOsSectionName = "Payment:PayOS";
}

public sealed class PaymentPayOsOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ChecksumKey { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string CancelUrl { get; set; } = "";
}

public sealed record PaymentPlanDto(
    string ProductCode,
    string PlanCode,
    string DisplayName,
    int AmountVnd,
    string Currency,
    int IntervalDays,
    int TrialDays);

/// <summary>Ops-only partial update for a sellable plan (null = keep current value).</summary>
public sealed record UpdatePaymentPlanRequest(
    int? AmountVnd = null,
    int? TrialDays = null,
    string? DisplayName = null,
    bool? IsActive = null);

/// <summary>Checkout method shown in shared KIT Pay UI (gateway or bank transfer).</summary>
public sealed record PaymentMethodDto(
    string ProviderCode,
    string DisplayName,
    string Description,
    bool Available,
    string? UnavailableReason = null);

public sealed record PaymentSubscriptionDto(
    Guid Id,
    Guid TenantId,
    string ProductCode,
    string SubjectType,
    Guid SubjectId,
    string PlanCode,
    string Status,
    DateTimeOffset? TrialEndsAt,
    DateTimeOffset? CurrentPeriodEnd,
    bool AutoRenew,
    bool IsEntitled);

public sealed record CreatePaymentOrderRequest(
    string ProductCode,
    string SubjectType,
    Guid SubjectId,
    string? PlanCode = null,
    string? ReturnUrl = null,
    string? CancelUrl = null,
    string? PreferredProvider = null);

public sealed record PaymentOrderDto(
    Guid Id,
    Guid TenantId,
    string ProductCode,
    string SubjectType,
    Guid SubjectId,
    long OrderCode,
    string PublicCode,
    string PlanCode,
    int AmountVnd,
    string Currency,
    string Status,
    string? ProviderCode,
    string? CheckoutUrl,
    string? QrCode,
    string? Description,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt);

public sealed record ActivatePaymentOrderRequest(long OrderCode);

/// <summary>Provider-agnostic checkout result from an adapter.</summary>
public sealed record ProviderCheckoutResult(
    string? CheckoutUrl,
    string? QrCode,
    string? ProviderPaymentId,
    DateTimeOffset? ExpiresAt);

public sealed record ProviderWebhookResult(
    bool IsSuccess,
    long OrderCode,
    string? ProviderTxnId,
    string RawBody);

/// <summary>
/// Adapter for a payment gateway. Add MoMo/VNPay/ZaloPay/Stripe without changing app code.
/// </summary>
public interface IPaymentProvider
{
    string ProviderCode { get; }

    bool IsReady { get; }

    Task<ProviderCheckoutResult> CreateCheckoutAsync(
        long orderCode,
        string publicCode,
        int amountVnd,
        string description,
        string returnUrl,
        string cancelUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verify webhook signature (fail closed) and extract order code.
    /// Throws InvalidOperationException on bad signature / missing config.
    /// </summary>
    ProviderWebhookResult ParseAndVerifyWebhook(string rawBody);
}

/// <summary>
/// Product-specific fulfillment after a payment order is paid
/// (extend subscription, provision seats, etc.).
/// </summary>
public interface IPaymentProductHandler
{
    string ProductCode { get; }

    Task OnOrderPaidAsync(
        PaymentOrderPaidContext context,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentOrderPaidContext(
    Guid TenantId,
    Guid OrderId,
    long OrderCode,
    string PublicCode,
    string ProductCode,
    string SubjectType,
    Guid SubjectId,
    string PlanCode,
    int AmountVnd,
    int IntervalDays,
    DateTimeOffset PeriodEnd,
    Guid SubscriptionId);

public interface IPaymentService
{
    Task<PaymentPlanDto?> GetPlanAsync(
        string productCode,
        string planCode,
        CancellationToken cancellationToken = default);

    /// <summary>Active plans for a product (shared checkout catalog).</summary>
    Task<IReadOnlyList<PaymentPlanDto>> ListPlansAsync(
        string productCode,
        CancellationToken cancellationToken = default);

    /// <summary>Ops-only: adjust price / trial length of a plan at runtime.</summary>
    Task<PaymentPlanDto> UpdatePlanAsync(
        string productCode,
        string planCode,
        UpdatePaymentPlanRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Payment methods for shared checkout (available + coming soon).</summary>
    Task<IReadOnlyList<PaymentMethodDto>> ListMethodsAsync(
        CancellationToken cancellationToken = default);

    Task<PaymentSubscriptionDto?> GetSubscriptionAsync(
        string productCode,
        string subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default);

    Task<PaymentOrderDto> CreateOrderAsync(
        CreatePaymentOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentOrderDto> GetOrderAsync(
        string productCode,
        Guid subjectId,
        long orderCode,
        CancellationToken cancellationToken = default);

    Task HandleProviderWebhookAsync(
        string providerCode,
        string rawBody,
        CancellationToken cancellationToken = default);

    /// <summary>Ops-only manual activation (bank transfer confirmed).</summary>
    Task<PaymentOrderDto> ActivateOrderAsync(
        long orderCode,
        CancellationToken cancellationToken = default);
}
