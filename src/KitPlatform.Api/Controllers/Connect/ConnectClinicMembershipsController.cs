using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/clinics/memberships")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectClinicMembershipsController : ControllerBase
{
    private readonly IConnectDoctorMembershipService _memberships;

    public ConnectClinicMembershipsController(IConnectDoctorMembershipService memberships) =>
        _memberships = memberships;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectDoctorMembershipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectDoctorMembershipDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _memberships.ListForClinicAsync(status, cancellationToken));

    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectDoctorMembershipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectDoctorMembershipDto>>> Pending(
        CancellationToken cancellationToken) =>
        Ok(await _memberships.ListPendingForClinicAsync(cancellationToken));

    [HttpPost("invite")]
    [ProducesResponseType(typeof(ConnectDoctorMembershipDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConnectDoctorMembershipDto>> Invite(
        [FromBody] InviteDoctorMembershipRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _memberships.InviteAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>C2.0 pilot: clinic confirms doctor accepted invite offline (until doctor OTP).</summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(typeof(ConnectDoctorMembershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectDoctorMembershipDto>> Confirm(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _memberships.ConfirmInviteAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ConnectDoctorMembershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectDoctorMembershipDto>> Approve(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _memberships.ApproveAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ConnectDoctorMembershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectDoctorMembershipDto>> Reject(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _memberships.RejectAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType(typeof(ConnectDoctorMembershipDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectDoctorMembershipDto>> Revoke(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _memberships.RevokeAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

[ApiController]
[Route("api/connect/partners")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectPartnerDoctorsController : ControllerBase
{
    private readonly IConnectDoctorMembershipService _memberships;
    private readonly IConnectPartnerCatalogService _partnerCatalog;

    public ConnectPartnerDoctorsController(
        IConnectDoctorMembershipService memberships,
        IConnectPartnerCatalogService partnerCatalog)
    {
        _memberships = memberships;
        _partnerCatalog = partnerCatalog;
    }

    [HttpGet("{partnerTenantId:guid}/doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectDoctorDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ConnectDoctorDto>>> ListPartnerDoctors(
        Guid partnerTenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _memberships.ListPartnerClinicDoctorsAsync(partnerTenantId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Danh mục thuốc của NT partner (org_link active) — clinic kê đơn theo tồn/SKU NT nguồn.</summary>
    [HttpGet("{partnerTenantId:guid}/products")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectPartnerProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ConnectPartnerProductDto>>> SearchPartnerProducts(
        Guid partnerTenantId,
        [FromQuery] string? q,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _partnerCatalog.SearchPharmacyProductsAsync(partnerTenantId, q, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
