using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core;
using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Api.Controllers.CustomerApp;

/// <summary>
/// Pharmacy customer Connect inbox (Option 1) — status only, no Clinic EMR.
/// </summary>
[ApiController]
[Route("api/customer-app/connect")]
[Authorize(Policy = CustomerAppPolicies.Authenticated)]
[RequirePlatformModule(PlatformModuleCodes.CustomerApp)]
public sealed class CustomerAppConnectController : ControllerBase
{
    private readonly ICustomerAppConnectService _connect;
    private readonly ICurrentCustomerAccessor _customer;

    public CustomerAppConnectController(
        ICustomerAppConnectService connect,
        ICurrentCustomerAccessor customer)
    {
        _connect = connect;
        _customer = customer;
    }

    [HttpGet("inbox")]
    [ProducesResponseType(typeof(CustomerConnectInboxDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox(CancellationToken cancellationToken) =>
        Ok(await _connect.GetInboxAsync(
            _customer.TenantId,
            _customer.CustomerId,
            cancellationToken));
}
