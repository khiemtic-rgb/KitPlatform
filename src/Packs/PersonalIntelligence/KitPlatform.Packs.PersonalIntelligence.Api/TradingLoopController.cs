using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/trading")]
public sealed class TradingLoopController : ControllerBase
{
    private readonly ITradingOperationalLoop _loop;

    public TradingLoopController(ITradingOperationalLoop loop) => _loop = loop;

    [HttpPost("loops")]
    [Authorize(Policy = PersonalIntelligencePolicies.Run)]
    [RequirePersonalIntelligenceModule]
    public async Task<ActionResult<TradingLoopResult>> Run(
        [FromBody] TradingLoopRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(request.ApprovedBy))
            {
                var allowed = User.IsInRole("ADMIN")
                    || CoreAuthority.HasPermission(User, PersonalIntelligencePolicies.ApprovePermission);
                if (!allowed)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new
                    {
                        message = "Paper approval requires the personal_intelligence.trading.approve permission.",
                    });
                }

                request.ApprovedBy = CoreAuthority.RequireUserId(User).ToString("D");
            }

            return Ok(await _loop.RunFixtureAsync(request, cancellationToken));
        }
        catch (TradingOperationConflictException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
