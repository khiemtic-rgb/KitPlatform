using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/status-events")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectStatusEventsController : ControllerBase
{
    private readonly IConnectStatusEventService _events;

    public ConnectStatusEventsController(IConnectStatusEventService events) => _events = events;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectStatusEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectStatusEventDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _events.ListAsync(status, cancellationToken));

    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectStatusEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ConnectStatusEventDto>>> Pending(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _events.ListPendingAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(ConnectStatusEventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConnectStatusEventDto>> Create(
        [FromBody] CreateConnectStatusEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var item = await _events.CreateManualReadyAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id = item.Id }, item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/consume")]
    public Task<ActionResult<ConnectStatusEventDto>> Consume(Guid id, CancellationToken ct) =>
        Mutate(id, s => s.ConsumeAsync(id, ct));

    [HttpPost("{id:guid}/dismiss")]
    public Task<ActionResult<ConnectStatusEventDto>> Dismiss(Guid id, CancellationToken ct) =>
        Mutate(id, s => s.DismissAsync(id, ct));

    private async Task<ActionResult<ConnectStatusEventDto>> Mutate(
        Guid id,
        Func<IConnectStatusEventService, Task<ConnectStatusEventDto?>> action)
    {
        try
        {
            var item = await action(_events);
            return item is null ? NotFound() : Ok(item);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
