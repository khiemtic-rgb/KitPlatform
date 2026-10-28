using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.PersonalIntelligence.Intent;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/paper-tasks")]
[Authorize(Policy = PersonalIntelligencePolicies.TaskRead)]
[RequirePersonalIntelligenceModule]
public sealed class PaperTaskController : ControllerBase
{
    readonly IPaperTaskStore _tasks;
    readonly ITradingDesk _desk;

    public PaperTaskController(IPaperTaskStore tasks, ITradingDesk desk)
    {
        _tasks = tasks;
        _desk = desk;
    }

    [HttpPost("decisions/{decisionId:guid}/paper")]
    [Authorize(Policy = PersonalIntelligencePolicies.Approve)]
    public async Task<ActionResult<PaperApprovalDto>> ApprovePaper(Guid decisionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.ApprovePaperAsync(decisionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("decisions/{decisionId:guid}/reject")]
    [Authorize(Policy = PersonalIntelligencePolicies.Approve)]
    public async Task<ActionResult<PaperRejectionDto>> RejectPaper(Guid decisionId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _desk.RejectPaperAsync(decisionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaperTaskSnapshot>>> List(CancellationToken cancellationToken)
    {
        if (!CoreAuthority.TryResolveCaller(User, out _))
            return Unauthorized();
        return Ok(await _tasks.ListAsync(cancellationToken));
    }

    [HttpGet("{taskId:guid}")]
    public async Task<ActionResult<PaperTaskSnapshot>> Get(Guid taskId, CancellationToken cancellationToken)
    {
        if (!CoreAuthority.TryResolveCaller(User, out _))
            return Unauthorized();
        var task = await _tasks.VerifyAsync(taskId, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }
}
