using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/referrals")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectReferralsController : ControllerBase
{
    private readonly IConnectReferralService _referrals;

    public ConnectReferralsController(IConnectReferralService referrals) => _referrals = referrals;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectReferralDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectReferralDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _referrals.ListAsync(status, cancellationToken));

    [HttpGet("inbox")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectReferralDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectReferralDto>>> Inbox(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _referrals.ListInboxAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConnectReferralDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConnectReferralDto>> Create(
        [FromBody] CreateConnectReferralRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _referrals.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<ConnectReferralDto>> Accept(Guid id, CancellationToken ct) =>
        Mutate(id, (s, _) => s.AcceptAsync(id, ct), ct);

    [HttpPost("{id:guid}/reject")]
    public Task<ActionResult<ConnectReferralDto>> Reject(Guid id, CancellationToken ct) =>
        Mutate(id, (s, _) => s.RejectAsync(id, ct), ct);

    [HttpPost("{id:guid}/complete")]
    public Task<ActionResult<ConnectReferralDto>> Complete(Guid id, CancellationToken ct) =>
        Mutate(id, (s, _) => s.CompleteAsync(id, ct), ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<ConnectReferralDto>> Cancel(Guid id, CancellationToken ct) =>
        Mutate(id, (s, _) => s.CancelAsync(id, ct), ct);

    private async Task<ActionResult<ConnectReferralDto>> Mutate(
        Guid id,
        Func<IConnectReferralService, CancellationToken, Task<ConnectReferralDto?>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await action(_referrals, cancellationToken);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
