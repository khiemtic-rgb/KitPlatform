using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;

namespace KitPlatform.Api.Controllers.Content;

/// <summary>KIT Marketing Park — gated by module kit_content (tenant KIT_MKT / package marketing_park).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[Route("api/content")]
public sealed class ContentController : ControllerBase
{
    private readonly IContentOrgSettingsService _settings;
    private readonly IContentBrandService _brands;
    private readonly IContentTopicService _topics;
    private readonly IContentPackageService _packages;
    private readonly IContentGenerateService _generate;
    private readonly IContentPublishService _publish;
    private readonly IContentVideoService _videos;

    public ContentController(
        IContentOrgSettingsService settings,
        IContentBrandService brands,
        IContentTopicService topics,
        IContentPackageService packages,
        IContentGenerateService generate,
        IContentPublishService publish,
        IContentVideoService videos)
    {
        _settings = settings;
        _brands = brands;
        _topics = topics;
        _packages = packages;
        _generate = generate;
        _publish = publish;
        _videos = videos;
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

    [HttpGet("packages")]
    public async Task<ActionResult<IReadOnlyList<ContentPackageDto>>> ListPackages(
        [FromQuery] Guid? brandId,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _packages.ListAsync(brandId, status, cancellationToken));

    [HttpGet("packages/{id:guid}")]
    public async Task<ActionResult<ContentPackageDto>> GetPackage(Guid id, CancellationToken cancellationToken)
    {
        var row = await _packages.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpGet("packages/{id:guid}/detail")]
    public async Task<ActionResult<ContentPackageDetailDto>> GetPackageDetail(Guid id, CancellationToken cancellationToken)
    {
        var row = await _packages.GetDetailAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("packages")]
    public async Task<ActionResult<ContentPackageDto>> CreatePackage(
        [FromBody] UpsertContentPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.CreateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("packages/{id:guid}")]
    public async Task<ActionResult<ContentPackageDto>> UpdatePackage(
        Guid id,
        [FromBody] UpsertContentPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _packages.UpdateAsync(id, request, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/{id:guid}/generate")]
    public async Task<ActionResult<GenerateContentResultDto>> GeneratePackage(
        Guid id,
        [FromBody] GenerateContentRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.GenerateAllAsync(id, request ?? new GenerateContentRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/{id:guid}/adapt")]
    public async Task<ActionResult<ContentPackageDto>> AdaptPackage(
        Guid id,
        [FromBody] AdaptContentPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.AdaptAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/{id:guid}/approve")]
    public async Task<ActionResult<ContentPackageDto>> ApprovePackage(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var row = await _packages.ApproveAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/approve-batch")]
    public async Task<ActionResult<BatchApprovePackagesResultDto>> ApprovePackagesBatch(
        [FromBody] BatchApprovePackagesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _packages.ApproveBatchAsync(request, cancellationToken));

    [HttpGet("video/templates")]
    public async Task<ActionResult<IReadOnlyList<ContentVideoTemplateDto>>> ListVideoTemplates(
        [FromQuery] bool? activeOnly,
        CancellationToken cancellationToken) =>
        Ok(await _videos.ListTemplatesAsync(activeOnly ?? true, cancellationToken));

    [HttpGet("video/jobs")]
    public async Task<ActionResult<IReadOnlyList<ContentVideoJobDto>>> ListVideoJobs(
        [FromQuery] Guid? brandId,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _videos.ListJobsAsync(brandId, status, cancellationToken));

    [HttpGet("video/jobs/{id:guid}")]
    public async Task<ActionResult<ContentVideoJobDto>> GetVideoJob(Guid id, CancellationToken cancellationToken)
    {
        var row = await _videos.GetJobAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("video/jobs/from-package")]
    public async Task<ActionResult<ContentVideoJobDto>> CreateVideoJobFromPackage(
        [FromBody] CreateVideoJobFromPackageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videos.CreateFromPackageAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("video/jobs/{id:guid}/script")]
    public async Task<ActionResult<ContentVideoJobDto>> UpdateVideoJobScript(
        Guid id,
        [FromBody] UpdateVideoJobScriptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _videos.UpdateScriptAsync(id, request, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video/jobs/{id:guid}/storyboard")]
    public async Task<ActionResult<ContentVideoJobDto>> PrepareVideoStoryboard(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _videos.PrepareStoryboardAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("video/jobs/{id:guid}/render")]
    public async Task<ActionResult<ContentVideoJobDto>> QueueVideoRender(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _videos.QueueRenderAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video/jobs/{id:guid}/refresh")]
    public async Task<ActionResult<ContentVideoJobDto>> RefreshVideoRender(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _videos.RefreshRenderAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("video/jobs/{id:guid}/approve")]
    public async Task<ActionResult<ContentVideoJobDto>> ApproveVideoJob(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _videos.ApproveAsync(id, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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

    [HttpDelete("topics/{id:guid}")]
    public async Task<IActionResult> DeleteTopic(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _topics.DeleteAsync(id, cancellationToken);
        return ok ? NoContent() : NotFound();
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
    [RequestSizeLimit(40 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 40 * 1024 * 1024)]
    public async Task<ActionResult<PublishContentResultDto>> Publish(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await BindPublishRequestAsync(cancellationToken);
            return Ok(await _publish.PublishAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<PublishContentRequest> BindPublishRequestAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            byte[]? bytes = null;
            string? fileName = null;
            string? contentType = null;
            var file = form.Files.GetFile("image");
            if (file is { Length: > 0 })
            {
                await using var stream = file.OpenReadStream();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, cancellationToken);
                bytes = ms.ToArray();
                fileName = file.FileName;
                contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/jpeg" : file.ContentType;
            }

            DateTimeOffset? publishAt = null;
            if (DateTimeOffset.TryParse(form["publishAt"], out var pa))
                publishAt = pa;

            return new PublishContentRequest
            {
                IncludeManualExport = !bool.TryParse(form["includeManualExport"], out var ime) || ime,
                RunImmediately = !bool.TryParse(form["runImmediately"], out var ri) || ri,
                PublishAt = publishAt,
                ImageBase64 = bytes is { Length: > 0 } ? Convert.ToBase64String(bytes) : null,
                ImageFileName = fileName,
                ImageContentType = contentType,
            };
        }

        var json = await JsonSerializer.DeserializeAsync<PublishContentRequest>(
            Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
            cancellationToken);
        return json ?? new PublishContentRequest();
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<ContentPublishJobDto>>> ListJobs(
        [FromQuery] Guid? topicId,
        CancellationToken cancellationToken) =>
        Ok(await _publish.ListJobsAsync(topicId, cancellationToken));

    [HttpPost("jobs/{id:guid}/run")]
    [RequestSizeLimit(40 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 40 * 1024 * 1024)]
    public async Task<ActionResult<ContentPublishJobDto>> RunJob(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            PublishContentRequest? media = null;
            if (Request.HasFormContentType)
                media = await BindPublishRequestAsync(cancellationToken);

            var row = await _publish.RunJobAsync(id, media, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
