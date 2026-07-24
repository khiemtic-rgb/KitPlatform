using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/overview")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsOverviewController : ControllerBase
{
    private readonly IFamilyOsOverviewService _overview;

    public FamilyOsOverviewController(IFamilyOsOverviewService overview) => _overview = overview;

    [HttpGet]
    [ProducesResponseType(typeof(FamilyOsOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyOsOverviewDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _overview.GetOverviewAsync(cancellationToken));
}
