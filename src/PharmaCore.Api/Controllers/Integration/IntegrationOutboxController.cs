using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCore.Application.Integration;

namespace PharmaCore.Api.Controllers.Integration;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/integration/outbox")]
public sealed class IntegrationOutboxController : ControllerBase
{
    private readonly IIntegrationOutboxQuery _outbox;

    public IntegrationOutboxController(IIntegrationOutboxQuery outbox) => _outbox = outbox;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationOutboxItemDto>>> List(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _outbox.ListRecentAsync(limit, cancellationToken));
}
