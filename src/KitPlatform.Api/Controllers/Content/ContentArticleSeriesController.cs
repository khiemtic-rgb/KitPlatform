using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content")]
public sealed class ContentArticleSeriesController : ControllerBase
{
    private readonly IContentArticleSeriesService _articleSeries;

    public ContentArticleSeriesController(IContentArticleSeriesService articleSeries) =>
        _articleSeries = articleSeries;

    public sealed record ReorderArticleEpisodesRequest(IReadOnlyList<Guid> EpisodeIds);

    [HttpGet("article-series")]
    public async Task<ActionResult<IReadOnlyList<ContentArticleSeriesDto>>> List(
        [FromQuery] Guid? brandId, [FromQuery] string? status, [FromQuery] Guid? corePackageId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken) =>
        Ok(await _articleSeries.ListAsync(brandId, status, corePackageId, from, to, cancellationToken));

    [HttpGet("article-series/{id:guid}")]
    public async Task<ActionResult<ContentArticleSeriesDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _articleSeries.GetDetailAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("article-series")]
    public async Task<ActionResult<ContentArticleSeriesDto>> Create(
        [FromBody] CreateContentArticleSeriesRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.CreateAsync(request, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("article-series/{id:guid}")]
    public async Task<ActionResult<ContentArticleSeriesDto>> Update(
        Guid id, [FromBody] UpdateContentArticleSeriesRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var row = await _articleSeries.UpdateAsync(id, request, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("article-series/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var ok = await _articleSeries.DeleteAsync(id, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("article-series/{id:guid}/approve")]
    public async Task<ActionResult<ContentArticleSeriesDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.ApproveAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("article-series/{id:guid}/blueprint/generate")]
    public async Task<ActionResult<ContentArticleSeriesDetailDto>> GenerateBlueprint(
        Guid id, [FromBody] GenerateArticleSeriesBlueprintRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.GenerateBlueprintAsync(id, request ?? new GenerateArticleSeriesBlueprintRequest(), cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("article-series/{id:guid}/blueprint/regenerate")]
    public async Task<ActionResult<ContentArticleSeriesDetailDto>> RegenerateBlueprint(
        Guid id, [FromBody] GenerateArticleSeriesBlueprintRequest? request, CancellationToken cancellationToken)
    {
        var body = (request ?? new GenerateArticleSeriesBlueprintRequest()) with { ReplaceExistingPlan = true };
        try { return Ok(await _articleSeries.GenerateBlueprintAsync(id, body, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("article-series/{id:guid}/episodes")]
    public async Task<ActionResult<IReadOnlyList<ContentArticleEpisodeDto>>> ListEpisodes(
        Guid id, CancellationToken cancellationToken) =>
        Ok(await _articleSeries.ListEpisodesAsync(id, cancellationToken));

    [HttpPost("article-series/{id:guid}/episodes")]
    public async Task<ActionResult<ContentArticleEpisodeDto>> AddEpisode(
        Guid id, [FromBody] UpsertContentArticleEpisodeRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.AddEpisodeAsync(id, request ?? new UpsertContentArticleEpisodeRequest(), cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut("article-series/{id:guid}/episodes/reorder")]
    public async Task<ActionResult<IReadOnlyList<ContentArticleEpisodeDto>>> ReorderEpisodes(
        Guid id, [FromBody] ReorderArticleEpisodesRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.ReorderEpisodesAsync(id, request.EpisodeIds ?? [], cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("article-series/{id:guid}/episodes/{episodeId:guid}")]
    public async Task<ActionResult<ContentArticleEpisodeDetailDto>> GetEpisode(
        Guid id, Guid episodeId, CancellationToken cancellationToken)
    {
        var row = await _articleSeries.GetEpisodeDetailAsync(id, episodeId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPut("article-series/{id:guid}/episodes/{episodeId:guid}")]
    public async Task<ActionResult<ContentArticleEpisodeDto>> UpdateEpisode(
        Guid id, Guid episodeId, [FromBody] UpsertContentArticleEpisodeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var row = await _articleSeries.UpdateEpisodeAsync(id, episodeId, request, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("article-series/{id:guid}/episodes/{episodeId:guid}")]
    public async Task<IActionResult> DeleteEpisode(Guid id, Guid episodeId, CancellationToken cancellationToken)
    {
        try
        {
            var ok = await _articleSeries.DeleteEpisodeAsync(id, episodeId, cancellationToken);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("article-series/{id:guid}/episodes/{episodeId:guid}/brief")]
    public async Task<ActionResult<ContentArticleEpisodeDto>> Brief(
        Guid id, Guid episodeId, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.GenerateBriefAsync(id, episodeId, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("article-series/{id:guid}/episodes/{episodeId:guid}/generate")]
    public async Task<ActionResult<GenerateArticleEpisodeResultDto>> Generate(
        Guid id, Guid episodeId, [FromBody] GenerateContentRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.GenerateContentAsync(id, episodeId, request, cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("article-series/{id:guid}/episodes/generate-next")]
    public async Task<ActionResult<GenerateArticleEpisodeBatchResultDto>> GenerateNext(
        Guid id, [FromBody] GenerateArticleEpisodeNextRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _articleSeries.GenerateNextAsync(id, request ?? new GenerateArticleEpisodeNextRequest(), cancellationToken)); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }
}
