using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Connect;

namespace KitPlatform.Api.Controllers.Connect;

[ApiController]
[Route("api/connect/rx-handoffs")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.NovixaConnect)]
public sealed class ConnectRxHandoffsController : ControllerBase
{
    private readonly IConnectRxHandoffService _handoffs;

    public ConnectRxHandoffsController(IConnectRxHandoffService handoffs) => _handoffs = handoffs;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectRxHandoffDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectRxHandoffDto>>> List(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _handoffs.ListAsync(status, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ConnectRxHandoffDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectRxHandoffDto>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await _handoffs.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }
}
