using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/school-schedule")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsSchoolScheduleController : ControllerBase
{
    private readonly IFamilySchoolScheduleService _school;

    public FamilyOsSchoolScheduleController(IFamilySchoolScheduleService school) => _school = school;

    /// <summary>SCH-02 — schedule + derived phase/quiet for one child.</summary>
    [HttpGet("~/api/family-os/families/{familyId:guid}/members/{memberId:guid}/school-schedule")]
    [ProducesResponseType(typeof(FamilySchoolScheduleMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilySchoolScheduleMemberDto>> GetMember(
        Guid familyId,
        Guid memberId,
        [FromQuery] DateTimeOffset? asOf,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _school.GetMemberAsync(familyId, memberId, asOf, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>SCH-02 — batch quietNow / phase for all children (admin + push tooling).</summary>
    [HttpGet("quiet-map")]
    [ProducesResponseType(typeof(FamilySchoolQuietMapDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FamilySchoolQuietMapDto>> QuietMap(
        Guid familyId,
        [FromQuery] DateTimeOffset? asOf,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _school.GetQuietMapAsync(familyId, asOf, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
