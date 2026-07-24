using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Care;

namespace KitPlatform.Api.Controllers.Care;

[ApiController]
[Route("api/care-os")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.CareOs)]
public sealed class CareOsController : ControllerBase
{
    private readonly ICareOsOverviewService _overview;
    private readonly ICareEventService _events;
    private readonly ICareCohortService _cohorts;
    private readonly ICareKpiService _kpis;

    public CareOsController(
        ICareOsOverviewService overview,
        ICareEventService events,
        ICareCohortService cohorts,
        ICareKpiService kpis)
    {
        _overview = overview;
        _events = events;
        _cohorts = cohorts;
        _kpis = kpis;
    }

    /// <summary>Solution shaping overview — readiness / non-goals / when KPIs can run.</summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(CareOsOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CareOsOverviewDto>> Overview(CancellationToken cancellationToken) =>
        Ok(await _overview.GetOverviewAsync(cancellationToken));

    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<CareEventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CareEventDto>>> ListEvents(
        [FromQuery] Guid? customerId,
        [FromQuery] string? eventType,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await _events.ListEventsAsync(customerId, eventType, limit, cancellationToken));

    [HttpPost("events")]
    [ProducesResponseType(typeof(CareEventDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CareEventDto>> CreateEvent(
        [FromBody] CreateCareEventRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _events.CreateEventAsync(request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("cohorts")]
    [ProducesResponseType(typeof(IReadOnlyList<CareCohortDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CareCohortDefinitionDto>>> ListCohorts(
        CancellationToken cancellationToken) =>
        Ok(await _cohorts.ListDefinitionsAsync(cancellationToken));

    [HttpGet("cohort-memberships")]
    [ProducesResponseType(typeof(IReadOnlyList<CareCohortMembershipDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CareCohortMembershipDto>>> ListMemberships(
        [FromQuery] Guid? cohortId,
        CancellationToken cancellationToken = default) =>
        Ok(await _cohorts.ListMembershipsAsync(cohortId, cancellationToken));

    [HttpPost("cohort-memberships")]
    [ProducesResponseType(typeof(CareCohortMembershipDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CareCohortMembershipDto>> AssignCohort(
        [FromBody] AssignCareCohortRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _cohorts.AssignAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("kpis")]
    [ProducesResponseType(typeof(IReadOnlyList<CareKpiDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CareKpiDefinitionDto>>> ListKpis(
        CancellationToken cancellationToken) =>
        Ok(await _kpis.ListDefinitionsAsync(cancellationToken));
}
