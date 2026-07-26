using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Payment;

namespace KitPlatform.Api.Controllers.Payment;

/// <summary>
/// Kit Payment Platform — canonical checkout API for all products (Famixa / Novixa / KEMS).
/// Product apps deep-link to a shared Checkout UI; that UI calls these endpoints.
/// </summary>
[ApiController]
[Route("api/payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _payment;

    public PaymentController(IPaymentService payment) => _payment = payment;

    /// <summary>Catalog of active plans for a product (shared checkout).</summary>
    [Authorize]
    [HttpGet("plans")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<PaymentPlanDto>>> ListPlans(
        [FromQuery] string productCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productCode))
            return BadRequest(new { code = "validation_error", message = "productCode là bắt buộc." });
        try
        {
            return Ok(await _payment.ListPlansAsync(productCode.Trim(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Ops-only — adjust plan price / trial length at runtime (Admin → Billing).</summary>
    [Authorize(Policy = PaymentPolicies.OpsActivate)]
    [HttpPut("plans/{productCode}/{planCode}")]
    [ProducesResponseType(typeof(PaymentPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaymentPlanDto>> UpdatePlan(
        string productCode,
        string planCode,
        [FromBody] UpdatePaymentPlanRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { code = "validation_error", message = "Body là bắt buộc." });
        try
        {
            return Ok(await _payment.UpdatePlanAsync(productCode, planCode, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Payment methods for shared checkout (VietQR, bank transfer, MoMo…).</summary>
    [Authorize]
    [HttpGet("methods")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentMethodDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentMethodDto>>> ListMethods(
        CancellationToken cancellationToken) =>
        Ok(await _payment.ListMethodsAsync(cancellationToken));

    /// <summary>Current subscription for a product subject (family / tenant / …).</summary>
    [Authorize]
    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(PaymentSubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentSubscriptionDto>> GetSubscription(
        [FromQuery] string productCode,
        [FromQuery] string subjectType,
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productCode) || string.IsNullOrWhiteSpace(subjectType) || subjectId == Guid.Empty)
            return BadRequest(new { code = "validation_error", message = "productCode, subjectType, subjectId là bắt buộc." });
        try
        {
            var sub = await _payment.GetSubscriptionAsync(
                productCode.Trim(), subjectType.Trim(), subjectId, cancellationToken);
            return sub is null ? NotFound() : Ok(sub);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Canonical create-order — all products use this instead of product-specific checkout endpoints.
    /// </summary>
    [Authorize]
    [HttpPost("orders")]
    [ProducesResponseType(typeof(PaymentOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentOrderDto>> CreateOrder(
        [FromBody] CreatePaymentOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { code = "validation_error", message = "Body là bắt buộc." });
        try
        {
            return Ok(await _payment.CreateOrderAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Poll order status after checkout (QR / bank transfer / provider return).</summary>
    [Authorize]
    [HttpGet("orders/{orderCode:long}")]
    [ProducesResponseType(typeof(PaymentOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PaymentOrderDto>> GetOrder(
        long orderCode,
        [FromQuery] string productCode,
        [FromQuery] Guid subjectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(productCode) || subjectId == Guid.Empty || orderCode <= 0)
            return BadRequest(new
            {
                code = "validation_error",
                message = "orderCode, productCode, subjectId là bắt buộc.",
            });
        try
        {
            return Ok(await _payment.GetOrderAsync(
                productCode.Trim(), subjectId, orderCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Canonical PayOS webhook (VietQR). Signature verification is fail-closed.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("payment-webhook")]
    [HttpPost("webhooks/payos")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PayOsWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        var body = await reader.ReadToEndAsync(cancellationToken);
        try
        {
            await _payment.HandleProviderWebhookAsync(
                PaymentProviderCodes.PayOs, body, cancellationToken);
            return Ok(new { received = true });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("chữ ký", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("ChecksumKey", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { code = "invalid_signature", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Ops-only manual activation when bank transfer is confirmed offline.</summary>
    [Authorize(Policy = PaymentPolicies.OpsActivate)]
    [HttpPost("orders/activate")]
    [ProducesResponseType(typeof(PaymentOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaymentOrderDto>> ActivateOrder(
        [FromBody] ActivatePaymentOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.OrderCode <= 0)
            return BadRequest(new { code = "validation_error", message = "orderCode là bắt buộc." });
        try
        {
            return Ok(await _payment.ActivateOrderAsync(request.OrderCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
