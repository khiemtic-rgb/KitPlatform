using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/org-links")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectOrgLinksController : ControllerBase
{
    private readonly IConnectOrgLinkService _links;

    public ConnectOrgLinksController(IConnectOrgLinkService links) => _links = links;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectOrgLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectOrgLinkDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _links.ListAsync(status, cancellationToken));

    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectOrgLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectOrgLinkDto>>> Pending(
        CancellationToken cancellationToken) =>
        Ok(await _links.ListPendingIncomingAsync(cancellationToken));

    [HttpGet("directory")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectDirectoryEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectDirectoryEntryDto>>> Directory(
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        Ok(await _links.SearchDirectoryAsync(q, cancellationToken));

    [HttpPost("invite")]
    [ProducesResponseType(typeof(ConnectOrgLinkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConnectOrgLinkDto>> Invite(
        [FromBody] InviteConnectOrgLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _links.InviteAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("request")]
    [ProducesResponseType(typeof(ConnectOrgLinkDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConnectOrgLinkDto>> RequestLink(
        [FromBody] RequestConnectOrgLinkRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _links.RequestAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/accept")]
    [ProducesResponseType(typeof(ConnectOrgLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectOrgLinkDto>> Accept(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var item = await _links.AcceptAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ConnectOrgLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectOrgLinkDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var item = await _links.ApproveAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ConnectOrgLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectOrgLinkDto>> Reject(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var item = await _links.RejectAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/revoke")]
    [ProducesResponseType(typeof(ConnectOrgLinkDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectOrgLinkDto>> Revoke(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var item = await _links.RevokeAsync(id, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
