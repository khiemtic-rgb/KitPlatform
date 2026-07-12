using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Clinic;

namespace KitPlatform.Api.Controllers.Clinic;

[ApiController]
[Route("api/clinic/day-summary")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.ClinicEmrLite)]
public sealed class ClinicDaySummaryController : ControllerBase
{
    private readonly IClinicDaySummaryService _summary;

    public ClinicDaySummaryController(IClinicDaySummaryService summary) => _summary = summary;

    [HttpGet]
    [ProducesResponseType(typeof(ClinicDaySummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClinicDaySummaryDto>> Get(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken) =>
        Ok(await _summary.GetAsync(date, cancellationToken));
}
