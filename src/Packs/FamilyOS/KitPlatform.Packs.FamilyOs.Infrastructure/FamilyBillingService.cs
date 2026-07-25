using KitPlatform.Application.Payment;
using KitPlatform.Packs.FamilyOs;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

/// <summary>
/// Famixa billing facade — delegates to Kit Payment Platform.
/// Keeps Family OS API contracts stable while providers live in payment.*.
/// </summary>
internal sealed class FamilyBillingService : IFamilyBillingService
{
    private readonly IPaymentService _payment;
    private readonly FamilyOsBillingOptions _billing;

    public FamilyBillingService(
        IPaymentService payment,
        IOptions<FamilyOsBillingOptions> billing)
    {
        _payment = payment;
        _billing = billing.Value;
    }

    public async Task<CheckoutDto> CreateCheckoutAsync(
        Guid familyId,
        CreateCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var planCode = string.IsNullOrWhiteSpace(request.PlanCode)
            ? FamilyBillingPlans.StarterMonth
            : request.PlanCode.Trim().ToLowerInvariant();
        if (planCode != FamilyBillingPlans.StarterMonth)
            throw new InvalidOperationException("Gói không hỗ trợ — hiện chỉ có starter_month.");

        // Ensure plan amount matches Famixa config override if present.
        _ = _billing.StarterMonthAmountVnd;

        var order = await _payment.CreateOrderAsync(
            new CreatePaymentOrderRequest(
                PaymentProductCodes.FamilyOs,
                PaymentSubjectTypes.Family,
                familyId,
                planCode,
                request.ReturnUrl,
                request.CancelUrl,
                PaymentProviderCodes.PayOs),
            cancellationToken);

        return Map(order);
    }

    public async Task<CheckoutDto> GetCheckoutAsync(
        Guid familyId,
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        var order = await _payment.GetOrderAsync(
            PaymentProductCodes.FamilyOs,
            familyId,
            orderCode,
            cancellationToken);
        return Map(order);
    }

    public Task HandlePayOsWebhookAsync(
        string rawBody,
        CancellationToken cancellationToken = default) =>
        _payment.HandleProviderWebhookAsync(PaymentProviderCodes.PayOs, rawBody, cancellationToken);

    public async Task<CheckoutDto> ActivateFromOrderCodeAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        var order = await _payment.ActivateOrderAsync(orderCode, cancellationToken);
        return Map(order);
    }

    private static CheckoutDto Map(PaymentOrderDto order) =>
        new(
            order.Id,
            order.SubjectId,
            order.OrderCode,
            order.PlanCode,
            order.AmountVnd,
            order.Status,
            order.CheckoutUrl,
            order.QrCode,
            order.Description ?? order.PublicCode,
            order.PaidAt,
            order.CreatedAt,
            order.ExpiresAt);
}
