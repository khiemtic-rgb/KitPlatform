using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Payment;

namespace KitPlatform.Api.Controllers.Payment;

/// <summary>Kit Payment Platform — provider webhooks and ops activation.</summary>
[ApiController]
[Route("api/payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentService _payment;

    public PaymentController(IPaymentService payment) => _payment = payment;

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
