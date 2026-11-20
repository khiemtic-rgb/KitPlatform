using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>
/// Agent tìm chủ đề từ bảng video thịnh hành. Chỉ tạo phiếu. Đưa vào pool khi người bấm.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content/topic-scout")]
public sealed class ContentTopicScoutController : ControllerBase
{
    private readonly ITopicScoutService _scout;

    public ContentTopicScoutController(ITopicScoutService scout) => _scout = scout;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TopicScoutProposalDto>>> List(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scout.ListAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("run")]
    public async Task<ActionResult<TopicScoutRunDto>> Run(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scout.RunAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<TopicScoutProposalDto>> Accept(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scout.AcceptAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult<TopicScoutProposalDto>> Dismiss(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _scout.DismissAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
