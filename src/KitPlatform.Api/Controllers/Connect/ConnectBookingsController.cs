using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/bookings")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectBookingsController : ControllerBase
{
    private readonly IConnectBookingService _bookings;

    public ConnectBookingsController(IConnectBookingService bookings) => _bookings = bookings;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectBookingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectBookingDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _bookings.ListAsync(status, cancellationToken));

    [HttpPost]
    [ProducesResponseType(typeof(ConnectBookingDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConnectBookingDto>> Create(
        [FromBody] CreateConnectBookingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _bookings.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/confirm")]
    public Task<ActionResult<ConnectBookingDto>> Confirm(Guid id, CancellationToken ct) =>
        Mutate(id, s => s.ConfirmAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<ConnectBookingDto>> Cancel(Guid id, CancellationToken ct) =>
        Mutate(id, s => s.CancelAsync(id, ct));

    [HttpPost("{id:guid}/complete")]
    public Task<ActionResult<ConnectBookingDto>> Complete(Guid id, CancellationToken ct) =>
        Mutate(id, s => s.CompleteAsync(id, ct));

    [HttpPost("{id:guid}/no-show")]
    public Task<ActionResult<ConnectBookingDto>> NoShow(Guid id, CancellationToken ct) =>
        Mutate(id, s => s.MarkNoShowAsync(id, ct));

    private async Task<ActionResult<ConnectBookingDto>> Mutate(
        Guid id,
        Func<IConnectBookingService, Task<ConnectBookingDto?>> action)
    {
        try
        {
            var item = await action(_bookings);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
