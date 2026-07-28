using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Api.Controllers.FamilyOs;

[ApiController]
[Route("api/family-os/families/{familyId:guid}/ai")]
[Authorize]
[RequirePlatformModule(PlatformModuleCodes.FamilyOs)]
public sealed class FamilyOsAiDigestController : ControllerBase
{
    private readonly IFamilyAiDigestService _digest;

    public FamilyOsAiDigestController(IFamilyAiDigestService digest) => _digest = digest;

    [HttpGet("wins-digest")]
    [ProducesResponseType(typeof(FamilyAiWinsDigestDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyAiWinsDigestDto>> WinsDigest(
        Guid familyId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _digest.GetWinsDigestAsync(familyId, from, to, limit, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpGet("letter")]
    [ProducesResponseType(typeof(FamilyAiLetterDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyAiLetterDto>> MonthlyLetter(
        Guid familyId,
        [FromQuery] string? month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DateOnly? monthDate = null;
            if (!string.IsNullOrWhiteSpace(month))
            {
                // Accept yyyy-MM or yyyy-MM-dd
                if (DateOnly.TryParse($"{month.Trim()}-01", out var m1))
                    monthDate = m1;
                else if (DateOnly.TryParse(month.Trim(), out var m2))
                    monthDate = new DateOnly(m2.Year, m2.Month, 1);
                else
                    return BadRequest(new { code = "validation_error", message = "month phải dạng yyyy-MM." });
            }

            return Ok(await _digest.GetMonthlyLetterAsync(familyId, monthDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Family Replay chữ — EOM narrative from Memory + ROP (no video).</summary>
    [HttpGet("replay")]
    [ProducesResponseType(typeof(FamilyReplayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FamilyReplayDto>> MonthlyReplay(
        Guid familyId,
        [FromQuery] string? month,
        CancellationToken cancellationToken = default)
    {
        try
        {
            DateOnly? monthDate = null;
            if (!string.IsNullOrWhiteSpace(month))
            {
                if (DateOnly.TryParse($"{month.Trim()}-01", out var m1))
                    monthDate = m1;
                else if (DateOnly.TryParse(month.Trim(), out var m2))
                    monthDate = new DateOnly(m2.Year, m2.Month, 1);
                else
                    return BadRequest(new { code = "validation_error", message = "month phải dạng yyyy-MM." });
            }

            return Ok(await _digest.GetMonthlyReplayAsync(familyId, monthDate, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
