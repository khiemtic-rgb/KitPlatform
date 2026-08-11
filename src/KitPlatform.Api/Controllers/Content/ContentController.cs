using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>KIT Content Park — platform ADMIN ops (isolated from Pharmacy tenant packs).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/content")]
public sealed class ContentController : ControllerBase
{
    private readonly IContentOrgSettingsService _settings;
    private readonly IContentBrandService _brands;
    private readonly IContentTopicService _topics;
    private readonly IContentGenerateService _generate;
    private readonly IContentPublishService _publish;

    public ContentController(
        IContentOrgSettingsService settings,
        IContentBrandService brands,
        IContentTopicService topics,
        IContentGenerateService generate,
        IContentPublishService publish)
    {
        _settings = settings;
        _brands = brands;
        _topics = topics;
        _generate = generate;
        _publish = publish;
    }

    [HttpGet("settings")]
    public async Task<ActionResult<ContentOrgSettingsDto>> GetSettings(CancellationToken cancellationToken) =>
        Ok(await _settings.GetAsync(cancellationToken));

    [HttpPut("settings")]
    public async Task<ActionResult<ContentOrgSettingsDto>> UpdateSettings(
        [FromBody] UpdateContentOrgSettingsRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _settings.UpdateAsync(request, cancellationToken));

    [HttpGet("budget")]
    public async Task<ActionResult<ContentBudgetSnapshotDto>> GetBudget(CancellationToken cancellationToken) =>
        Ok(await _settings.GetBudgetSnapshotAsync(cancellationToken));

    [HttpPost("ai/test")]
    public async Task<ActionResult<ContentAiTestResultDto>> TestAi(CancellationToken cancellationToken) =>
        Ok(await _settings.TestAiAsync(cancellationToken));

    [HttpGet("brands")]
    public async Task<ActionResult<IReadOnlyList<ContentBrandDto>>> ListBrands(
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken) =>
        Ok(await _brands.ListAsync(activeOnly, cancellationToken));

    [HttpGet("brands/{id:guid}")]
    public async Task<ActionResult<ContentBrandDto>> GetBrand(Guid id, CancellationToken cancellationToken)
    {
        var row = await _brands.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("brands")]
    public async Task<ActionResult<ContentBrandDto>> CreateBrand(
        [FromBody] UpsertContentBrandRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _brands.CreateAsync(request, cancellationToken));

    [HttpPut("brands/{id:guid}")]
    public async Task<ActionResult<ContentBrandDto>> UpdateBrand(
        Guid id,
        [FromBody] UpsertContentBrandRequest request,
        CancellationToken cancellationToken)
    {
        var row = await _brands.UpdateAsync(id, request, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("brands/{brandId:guid}/sites")]
    public async Task<ActionResult<IReadOnlyList<ContentSiteTargetDto>>> ListSites(
        Guid brandId,
        CancellationToken cancellationToken) =>
        Ok(await _brands.ListSitesAsync(brandId, cancellationToken));

    [HttpPut("brands/{brandId:guid}/sites")]
    public async Task<ActionResult<ContentSiteTargetDto>> UpsertSite(
        Guid brandId,
        [FromBody] UpsertContentSiteTargetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _brands.UpsertSiteAsync(brandId, request, cancellationToken));

    [HttpGet("brands/{brandId:guid}/channels")]
    public async Task<ActionResult<IReadOnlyList<ContentChannelTargetDto>>> ListChannels(
        Guid brandId,
        CancellationToken cancellationToken) =>
        Ok(await _brands.ListChannelsAsync(brandId, cancellationToken));

    [HttpPut("brands/{brandId:guid}/channels")]
    public async Task<ActionResult<ContentChannelTargetDto>> UpsertChannel(
        Guid brandId,
        [FromBody] UpsertContentChannelTargetRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _brands.UpsertChannelAsync(brandId, request, cancellationToken));

    [HttpGet("topics")]
    public async Task<ActionResult<IReadOnlyList<ContentTopicDto>>> ListTopics(
        [FromQuery] Guid? brandId,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _topics.ListAsync(brandId, status, cancellationToken));

    [HttpGet("topics/{id:guid}")]
    public async Task<ActionResult<ContentTopicDto>> GetTopic(Guid id, CancellationToken cancellationToken)
    {
        var row = await _topics.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("topics/{id:guid}/detail")]
    public async Task<ActionResult<ContentTopicDetailDto>> GetTopicDetail(Guid id, CancellationToken cancellationToken)
    {
        var row = await _topics.GetDetailAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("topics")]
    public async Task<ActionResult<ContentTopicDto>> CreateTopic(
        [FromBody] UpsertContentTopicRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _topics.CreateAsync(request, cancellationToken));

    [HttpPut("topics/{id:guid}")]
    public async Task<ActionResult<ContentTopicDto>> UpdateTopic(
        Guid id,
        [FromBody] UpsertContentTopicRequest request,
        CancellationToken cancellationToken)
    {
        var row = await _topics.UpdateAsync(id, request, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("topics/{id:guid}/generate")]
    public async Task<ActionResult<GenerateContentResultDto>> Generate(
        Guid id,
        [FromBody] GenerateContentRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _generate.GenerateAsync(id, request ?? new GenerateContentRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("topics/{id:guid}/approve")]
    public async Task<ActionResult<ContentTopicDto>> Approve(Guid id, CancellationToken cancellationToken)
    {
        var row = await _topics.ApproveAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("topics/{topicId:guid}/assets/{assetId:guid}/select")]
    public async Task<IActionResult> SelectAsset(Guid topicId, Guid assetId, CancellationToken cancellationToken)
    {
        var ok = await _topics.SelectAssetAsync(topicId, assetId, cancellationToken);
        return ok ? NoContent() : NotFound();
    }

    [HttpGet("assets/{id:guid}/file")]
    public async Task<IActionResult> GetAssetFile(Guid id, CancellationToken cancellationToken)
    {
        var file = await _publish.GetAssetFileAsync(id, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
    }

    [HttpPost("topics/{id:guid}/publish")]
    public async Task<ActionResult<PublishContentResultDto>> Publish(
        Guid id,
        [FromBody] PublishContentRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _publish.PublishAsync(id, request ?? new PublishContentRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<ContentPublishJobDto>>> ListJobs(
        [FromQuery] Guid? topicId,
        CancellationToken cancellationToken) =>
        Ok(await _publish.ListJobsAsync(topicId, cancellationToken));

    [HttpPost("jobs/{id:guid}/run")]
    public async Task<ActionResult<ContentPublishJobDto>> RunJob(Guid id, CancellationToken cancellationToken)
    {
        var row = await _publish.RunJobAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }
}
