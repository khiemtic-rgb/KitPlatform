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
    private readonly IContentPublishService _publish;
    private readonly IContentVideoService _videos;
    private readonly IContentOpsService _ops;
    private readonly IContentWorkQueueService _work;
    private readonly IContentFacebookConnectionService _facebook;
    private readonly IContentSeriesTurboService _seriesTurbo;
    private readonly IContentSeriesTakeProxyService _seriesTake;
    private readonly IContentSeriesAssembleService _seriesAssemble;
    private readonly IContentSeriesStillService _seriesStill;
    private readonly IContentSeriesScriptDraftService _seriesDraft;
    private readonly IContentSeriesPilotService _seriesPilot;

    public ContentController(
        IContentOrgSettingsService settings,
        IContentBrandService brands,
        IContentTopicService topics,
        IContentPackageService packages,
        IContentPublishService publish,
        IContentVideoService videos,
        IContentOpsService ops,
        IContentWorkQueueService work,
        IContentFacebookConnectionService facebook,
        IContentSeriesTurboService seriesTurbo,
        IContentSeriesTakeProxyService seriesTake,
        IContentSeriesAssembleService seriesAssemble,
        IContentSeriesStillService seriesStill,
        IContentSeriesScriptDraftService seriesDraft,
        IContentSeriesPilotService seriesPilot)
    {
        _settings = settings;
        _brands = brands;
        _topics = topics;
        _packages = packages;
        _publish = publish;
        _videos = videos;
        _ops = ops;
        _work = work;
        _facebook = facebook;
        _seriesTurbo = seriesTurbo;
        _seriesTake = seriesTake;
        _seriesAssemble = seriesAssemble;
        _seriesStill = seriesStill;
        _seriesDraft = seriesDraft;
        _seriesPilot = seriesPilot;
    }

    [HttpGet("ops")]
    public async Task<ActionResult<ContentOpsSnapshotDto>> GetOps(CancellationToken cancellationToken) =>
        Ok(await _ops.GetSnapshotAsync(cancellationToken));

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<ContentCalendarItemDto>>> GetCalendar(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _ops.ListCalendarAsync(from, to, brandId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("work")]
    public async Task<ActionResult<IReadOnlyList<ContentWorkJobDto>>> ListActiveWork(
        CancellationToken cancellationToken) =>
        Ok(await _work.ListActiveAsync(cancellationToken));

    [HttpGet("work/{id:guid}")]
    public async Task<ActionResult<ContentWorkJobDto>> GetWork(Guid id, CancellationToken cancellationToken)
    {
        var row = await _work.GetAsync(id, cancellationToken);
        return row is null ? NotFound() : Ok(row);
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

    [HttpPost("video/test")]
    public async Task<ActionResult<ContentVideoTestResultDto>> TestVideo(CancellationToken cancellationToken) =>
        Ok(await _settings.TestVideoAsync(cancellationToken));

    [HttpPost("series/turbo")]
    [RequestSizeLimit(16_000_000)]
    public async Task<ActionResult<ContentSeriesTurboTaskDto>> StartSeriesTurbo(
        [FromBody] ContentSeriesTurboStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesTurbo.StartAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/script-draft")]
    public async Task<ActionResult<ContentSeriesScriptDraftDto>> DraftSeriesScript(
        [FromBody] ContentSeriesScriptDraftRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesDraft.DraftAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("series/turbo")]
    public Task<ActionResult<ContentSeriesTurboTaskDto>> GetSeriesTurboQuery(
        [FromQuery] string taskId,
        CancellationToken cancellationToken) =>
        GetSeriesTurbo(taskId, cancellationToken);

    [HttpGet("series/turbo/{*taskId}")]
    public async Task<ActionResult<ContentSeriesTurboTaskDto>> GetSeriesTurbo(
        string taskId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesTurbo.GetAsync(taskId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/lipsync")]
    [RequestSizeLimit(16_000_000)]
    public async Task<ActionResult<ContentSeriesTurboTaskDto>> StartSeriesLipsync(
        [FromBody] ContentSeriesLipsyncStartRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesTurbo.StartLipsyncAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/assemble")]
    [RequestSizeLimit(40_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 40_000_000)]
    public async Task<IActionResult> AssembleSeriesCut(
        [FromBody] ContentSeriesAssembleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var cut = await _seriesAssemble.AssembleAsync(request, cancellationToken);
            return File(cut.Bytes, cut.ContentType, cut.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/take-probe")]
    public async Task<ActionResult<ContentSeriesTakeProbeDto>> ProbeSeriesTake(
        [FromBody] ContentSeriesTakeProxyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesTake.ProbeAsync(request.Url, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/take-proxy")]
    public async Task<IActionResult> ProxySeriesTake(
        [FromBody] ContentSeriesTakeProxyRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var take = await _seriesTake.FetchAsync(request.Url, cancellationToken);
            return File(take.Bytes, take.ContentType, take.FileName);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("series/pilot")]
    public async Task<ActionResult<ContentSeriesPilotDto>> GetSeriesPilot(
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesPilot.GetAsync(code ?? "FAMIXA", cancellationToken));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("series/pilot")]
    public async Task<ActionResult<ContentSeriesPilotDto>> PutSeriesPilot(
        [FromBody] UpsertContentSeriesPilotRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesPilot.UpsertAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("series/builds")]
    public async Task<ActionResult<IReadOnlyList<ContentSeriesBuildSummaryDto>>> ListSeriesBuilds(
        [FromQuery] string? code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesPilot.ListBuildsAsync(code ?? "FAMIXA", cancellationToken));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("series/builds/{id:guid}")]
    public async Task<ActionResult<ContentSeriesBuildDto>> GetSeriesBuild(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesPilot.GetBuildAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("series/builds")]
    public async Task<ActionResult<ContentSeriesBuildDto>> PutSeriesBuild(
        [FromBody] UpsertContentSeriesBuildRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesPilot.UpsertBuildAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("series/builds/{id:guid}")]
    public async Task<IActionResult> DeleteSeriesBuild(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _seriesPilot.DeleteBuildAsync(id, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("series/voices")]
    public async Task<ActionResult<IReadOnlyList<ContentSeriesVoiceDto>>> ListSeriesVoices(
        CancellationToken cancellationToken) =>
        Ok(await _seriesPilot.ListVoicesAsync(cancellationToken));

    [HttpPost("series/kf-note")]
    public async Task<ActionResult<ContentSeriesKfNoteDto>> RewriteSeriesKfNote(
        [FromBody] ContentSeriesKfNoteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesStill.RewriteNoteAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/still-qa")]
    public async Task<ActionResult<ContentSeriesStillQaDto>> QaSeriesStill(
        [FromBody] ContentSeriesStillQaRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesStill.QaAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/still")]
    public async Task<ActionResult<ContentSeriesStillDto>> GenerateSeriesStill(
        [FromBody] ContentSeriesStillRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _seriesStill.GenerateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/tts")]
    public async Task<IActionResult> PreviewSeriesTts(
        [FromBody] ContentSeriesTtsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await _seriesPilot.PreviewTtsAsync(
                request.VoiceId,
                request.Text,
                request.PublicOwnerId,
                request.VoiceName,
                request.VoiceSettings,
                cancellationToken,
                request.Accent);
            return File(bytes, "audio/mpeg");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("facebook/test")]
    public async Task<ActionResult<ContentFacebookTestResultDto>> TestFacebook(CancellationToken cancellationToken) =>
        Ok(await _settings.TestFacebookAsync(cancellationToken));

    [HttpGet("facebook/oauth/start")]
    public async Task<ActionResult<ContentFacebookStartDto>> StartFacebook(
        [FromQuery] Guid brandId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _facebook.StartAsync(brandId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("facebook/oauth/complete")]
    public async Task<ActionResult<ContentFacebookPendingDto>> CompleteFacebook(
        [FromBody] ContentFacebookCompleteRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _facebook.CompleteAsync(request.Code, request.State, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("facebook/oauth/pending/{sessionId}")]
    public async Task<ActionResult<ContentFacebookPendingDto>> GetFacebookPending(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var row = await _facebook.GetPendingAsync(sessionId, cancellationToken);
        return row is null ? NotFound(new { message = "Phiên Facebook hết hạn — Kết nối lại." }) : Ok(row);
    }

    [HttpPost("facebook/oauth/select")]
    public async Task<ActionResult<ContentChannelTargetDto>> SelectFacebookPage(
        [FromBody] ContentFacebookSelectRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _facebook.SelectPageAsync(request.SessionId, request.PageId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("channels/{id:guid}/facebook/verify")]
    public async Task<ActionResult<ContentFacebookVerifyDto>> VerifyFacebook(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _facebook.VerifyAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("channels/{id:guid}/facebook/disconnect")]
    public async Task<ActionResult<ContentChannelTargetDto>> DisconnectFacebook(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _facebook.DisconnectAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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

    [HttpGet("write-plans")]
    public async Task<ActionResult<IReadOnlyList<ContentWritePlanDto>>> ListWritePlans(
        [FromQuery] Guid? brandId,
        CancellationToken cancellationToken) =>
        Ok(await _brands.ListWritePlansAsync(brandId, cancellationToken));

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
        [FromQuery] bool? coresOnly,
        CancellationToken cancellationToken) =>
        Ok(await _packages.ListAsync(brandId, status, coresOnly ?? false, cancellationToken));

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
    public async Task<ActionResult<EnqueueWorkResultDto>> GeneratePackage(
        Guid id,
        [FromBody] GenerateContentRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _work.EnqueueGeneratePackageAsync(
                id, request ?? new GenerateContentRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/{id:guid}/adapt-multi")]
    public async Task<ActionResult<EnqueueWorkResultDto>> AdaptMulti(
        Guid id,
        [FromBody] AnalyzeAdaptRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _work.EnqueueBrandAdaptAsync(
                id, request ?? new AnalyzeAdaptRequest(), cancellationToken));
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

    [HttpGet("packages/{id:guid}/export")]
    public async Task<IActionResult> ExportPackage(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var (bytes, fileName) = await _packages.ExportManualPackAsync(id, cancellationToken);
            return File(bytes, "application/zip", fileName);
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

    [HttpPut("packages/{id:guid}/brief")]
    public async Task<ActionResult<ContentPackageDto>> UpdatePackageBrief(
        Guid id,
        [FromBody] ContentCreativeBriefDto brief,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _packages.UpdateBriefAsync(id, brief, cancellationToken);
            return row is null ? NotFound() : Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("packages/{id:guid}/performance")]
    public async Task<ActionResult<IReadOnlyList<ContentPerformanceDto>>> ListPackagePerformance(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _packages.ListPerformanceAsync(id, cancellationToken));

    [HttpPost("packages/{id:guid}/performance")]
    public async Task<ActionResult<ContentPerformanceDto>> IngestPackagePerformance(
        Guid id,
        [FromBody] IngestContentPerformanceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.IngestPerformanceAsync(id, request, cancellationToken));
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

    [HttpPost("packages/pool")]
    public async Task<ActionResult<CreatePoolIdeasResultDto>> CreatePool(
        [FromBody] CreatePoolIdeasRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.CreatePoolAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/pool/analyze")]
    public async Task<ActionResult<AnalyzePoolResultDto>> AnalyzePool(
        [FromBody] AnalyzePoolRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _work.EnqueueBrandAdaptBatchAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/pool/suggest")]
    public async Task<ActionResult<SuggestPoolIdeasResultDto>> SuggestPool(
        [FromBody] SuggestPoolIdeasRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.SuggestPoolIdeasAsync(request ?? new SuggestPoolIdeasRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("packages/pool/apply")]
    public async Task<ActionResult<ApplyPoolFitsResultDto>> ApplyPool(
        [FromBody] ApplyPoolFitsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _packages.ApplyPoolFitsAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

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

    [HttpPost("video/jobs/{id:guid}/mvp-pipeline")]
    public async Task<ActionResult<EnqueueWorkResultDto>> RunVideoMvpPipeline(
        Guid id,
        [FromBody] RunVideoMvpPipelineRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _work.EnqueueVideoMvpAsync(
                id, request ?? new RunVideoMvpPipelineRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video/jobs/{id:guid}/render")]
    public async Task<ActionResult<EnqueueWorkResultDto>> QueueVideoRender(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _work.EnqueueVideoRenderAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("webhooks/creatomate")]
    [AllowAnonymous]
    public async Task<IActionResult> CreatomateWebhook(CancellationToken cancellationToken)
    {
        using var doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString()
            : root.TryGetProperty("renderId", out var rid) ? rid.GetString() : null;
        var status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
        var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
        var snap = root.TryGetProperty("snapshot_url", out var s) ? s.GetString()
            : root.TryGetProperty("snapshotUrl", out var s2) ? s2.GetString() : null;
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { message = "Missing render id" });
        await _videos.ApplyCreatomateWebhookAsync(id, status, url, snap, cancellationToken);
        return Ok(new { received = true });
    }

    [HttpGet("video/media/{jobId:guid}/{fileName}")]
    [AllowAnonymous]
    public IActionResult GetVideoMedia(Guid jobId, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
            return BadRequest();
        var root = Path.GetFullPath(Path.Combine(
            Directory.GetCurrentDirectory(),
            "App_Data",
            "content-video",
            jobId.ToString("N")));
        var path = Path.GetFullPath(Path.Combine(root, fileName));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(path))
            return NotFound();
        var contentType = fileName.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ? "audio/mpeg" : "application/octet-stream";
        return PhysicalFile(path, contentType);
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
    public async Task<ActionResult<EnqueueWorkResultDto>> Generate(
        Guid id,
        [FromBody] GenerateContentRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _work.EnqueueGenerateTopicAsync(
                id, request ?? new GenerateContentRequest(), cancellationToken));
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
    public async Task<ActionResult<EnqueueWorkResultDto>> Publish(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await BindPublishRequestAsync(cancellationToken);
            return Ok(await _work.EnqueuePublishTopicAsync(id, request, cancellationToken));
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
