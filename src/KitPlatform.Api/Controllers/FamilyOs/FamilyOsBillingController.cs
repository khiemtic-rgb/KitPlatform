using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os")]
public sealed class FamilyOsBillingController : ControllerBase
{
    private readonly IFamilyBillingService _billing;

    public FamilyOsBillingController(IFamilyBillingService billing) =>
        _billing = billing;

    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpPost("families/{familyId:guid}/billing/checkout")]
    [ProducesResponseType(typeof(CheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CheckoutDto>> CreateCheckout(
        Guid familyId,
        [FromBody] CreateCheckoutRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.CreateCheckoutAsync(
                familyId,
                request ?? new CreateCheckoutRequest(),
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [Authorize]
    [RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
    [HttpGet("families/{familyId:guid}/billing/checkout/{orderCode:long}")]
    [ProducesResponseType(typeof(CheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CheckoutDto>> GetCheckout(
        Guid familyId,
        long orderCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _billing.GetCheckoutAsync(familyId, orderCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>PayOS payment webhook — anonymous; verifies HMAC when checksum key is configured.</summary>
    [AllowAnonymous]
    [HttpPost("billing/payos-webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PayOsWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: false);
        var body = await reader.ReadToEndAsync(cancellationToken);
        try
        {
            await _billing.HandlePayOsWebhookAsync(body, cancellationToken);
            return Ok(new { received = true });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Contains("chữ ký", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("signature", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { code = "invalid_signature", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Ops-only — delegates to Kit Payment. Requires payment.ops.activate.</summary>
    [Authorize(Policy = PaymentPolicies.OpsActivate)]
    [HttpPost("billing/activate-order")]
    [ProducesResponseType(typeof(CheckoutDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CheckoutDto>> ActivateOrder(
        [FromBody] ActivateOrderRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.OrderCode <= 0)
            return BadRequest(new { code = "validation_error", message = "orderCode là bắt buộc." });
        try
        {
            return Ok(await _billing.ActivateFromOrderCodeAsync(request.OrderCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
