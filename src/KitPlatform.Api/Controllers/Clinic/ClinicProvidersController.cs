using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Api.Controllers.Clinic;

[ApiController]
[Route("api/clinic/providers")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.ClinicEmrLite)]
[Authorize(Policy = ClinicPolicies.Read)]
public sealed class ClinicProvidersController : ControllerBase
{
    private readonly IClinicProviderService _providers;

    public ClinicProvidersController(IClinicProviderService providers) => _providers = providers;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClinicProviderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClinicProviderDto>>> List(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default) =>
        Ok(await _providers.ListAsync(includeInactive, cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ClinicProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicProviderDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var item = await _providers.GetAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicProviderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicProviderDto>> Create(
        [FromBody] CreateClinicProviderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await _providers.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClinicProviderDto>> Update(
        Guid id,
        [FromBody] UpdateClinicProviderRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _providers.UpdateAsync(id, request, cancellationToken);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("from-connect")]
    [Authorize(Policy = ClinicPolicies.Write)]
    [ProducesResponseType(typeof(ClinicProviderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClinicProviderDto>> UpsertFromConnect(
        [FromBody] UpsertClinicProviderFromConnectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _providers.UpsertFromConnectAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
