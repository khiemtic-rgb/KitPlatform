using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/parent-success")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsParentSuccessController : ControllerBase
{
    private readonly IFamilyParentSuccessService _success;

    public FamilyOsParentSuccessController(IFamilyParentSuccessService success) =>
        _success = success;

    /// <summary>P0c — Return on Parenting + Growth Report (30 or 90 days).</summary>
    [HttpGet("rop")]
    [ProducesResponseType(typeof(ParentSuccessRopDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ParentSuccessRopDto>> GetRop(
        Guid familyId,
        [FromQuery] int days = 30,
        [FromQuery] DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (days is not (30 or 60 or 90))
                return BadRequest(new { code = "validation_error", message = "days phải là 30, 60 hoặc 90." });

            return Ok(await _success.GetRopAsync(familyId, days, asOf, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>P2 — evening 3Q check-in for the day.</summary>
    [HttpGet("evening-checkin")]
    [ProducesResponseType(typeof(ParentSuccessCheckinDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult<ParentSuccessCheckinDto>> GetEveningCheckin(
        Guid familyId,
        [FromQuery] Guid memberId,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (memberId == Guid.Empty)
                return BadRequest(new { code = "validation_error", message = "memberId là bắt buộc." });

            var row = await _success.GetEveningCheckinAsync(familyId, memberId, date, cancellationToken);
            if (row is null) return NoContent();
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPost("evening-checkin")]
    [ProducesResponseType(typeof(ParentSuccessCheckinDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ParentSuccessCheckinDto>> UpsertEveningCheckin(
        Guid familyId,
        [FromBody] UpsertParentSuccessCheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _success.UpsertEveningCheckinAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>P2 — 3 light parent achievements (soft recognition, not gamification hero).</summary>
    [HttpGet("achievements")]
    [ProducesResponseType(typeof(ParentAchievementsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ParentAchievementsDto>> ListAchievements(
        Guid familyId,
        [FromQuery] DateOnly? asOf = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _success.ListAchievementsAsync(familyId, asOf, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>P3 — Trust Flywheel: parent tapped Đã thử on a Famixa tip.</summary>
    [HttpPost("coach-acted")]
    [ProducesResponseType(typeof(ParentCoachActedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ParentCoachActedDto>> RecordCoachActed(
        Guid familyId,
        [FromBody] ParentCoachActedRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _success.RecordCoachActedAsync(familyId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("coach-acted")]
    [ProducesResponseType(typeof(ParentCoachActedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ParentCoachActedDto>> ListCoachActed(
        Guid familyId,
        [FromQuery] Guid memberId,
        [FromQuery] DateOnly? date = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (memberId == Guid.Empty)
                return BadRequest(new { code = "validation_error", message = "memberId là bắt buộc." });

            return Ok(await _success.ListCoachActedTodayAsync(familyId, memberId, date, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
