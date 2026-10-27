using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.PersonalIntelligence.Intent;

namespace KitPlatform.Api.Controllers.PersonalIntelligence;

[ApiController]
[Route("api/personal-intelligence/paper-tasks")]
[Authorize(Policy = PersonalIntelligencePolicies.TaskRead)]
[RequirePersonalIntelligenceModule]
public sealed class PaperTaskController : ControllerBase
{
    readonly IPaperTaskStore _tasks;

    public PaperTaskController(IPaperTaskStore tasks) => _tasks = tasks;

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
