using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[ServiceFilter(typeof(ProductionTenantActionFilter))]
[Route("api/content")]
public sealed class ContentFilmTypeController : ControllerBase
{
    private readonly IContentFilmTypeService _filmTypes;

    public ContentFilmTypeController(IContentFilmTypeService filmTypes) => _filmTypes = filmTypes;

    [HttpGet("film-types")]
    public async Task<ActionResult<IReadOnlyList<FilmTypeDto>>> List(
        [FromQuery] Guid brandId, CancellationToken cancellationToken)
    {
        if (brandId == Guid.Empty) return BadRequest(new { message = "Chọn một brand." });
        try
        {
            return Ok(await _filmTypes.ListAsync(brandId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("film-types")]
    public async Task<ActionResult<FilmTypeDto>> Create(
        [FromBody] CreateFilmTypeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _filmTypes.CreateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("film-types/{filmTypeId:guid}")]
    public async Task<ActionResult<FilmTypeDto>> Update(
        Guid filmTypeId, [FromBody] UpdateFilmTypeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _filmTypes.UpdateAsync(filmTypeId, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("film-types/{filmTypeId:guid}/workflow")]
    public async Task<IActionResult> ChangeWorkflow(
        Guid filmTypeId, [FromBody] UpdateFilmTypeWorkflowRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _filmTypes.ChangeWorkflowAsync(filmTypeId, request.WorkflowKey, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-projects/{projectId:guid}/film-type")]
    public async Task<IActionResult> Bind(
        Guid projectId, [FromBody] BindProjectFilmTypeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _filmTypes.BindProjectAsync(projectId, request.FilmTypeId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("topics/{topicId:guid}/format")]
    public async Task<IActionResult> SetFormat(
        Guid topicId, [FromBody] SetContentFormatRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _filmTypes.SetTopicFormatAsync(topicId, request.Format, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("video-engine/productions/{productionId:guid}/content")]
    public async Task<IActionResult> Link(
        Guid productionId, [FromBody] LinkProductionContentRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _filmTypes.LinkProductionAsync(productionId, request.TopicId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/productions/{productionId:guid}/select-for-publish")]
    public async Task<IActionResult> Select(Guid productionId, CancellationToken cancellationToken)
    {
        try
        {
            await _filmTypes.SelectProductionForPublishAsync(productionId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
