using System.Linq;
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
    private readonly IFamixaCharacterService _characters;
    private readonly IKitVideoEngineService _videoEngine;
    private readonly IKitVideoJobService _videoJobs;
    private readonly IKitVideoAssetService _videoAssets;
    private readonly IKitVideoContinuityService _videoContinuity;
    private readonly IKitVideoVisionService _videoVision;
    private readonly IKitVideoPixelService _videoPixels;
    private readonly IKitVideoMotionService _videoMotion;
    private readonly IKitVideoVisualSystemService _visualSystem;
    private readonly IKitVideoMasterReferenceService _masterReference;
    private readonly IKitVideoIdentityTestService _identityTests;
    private readonly IKitVideoIdentityStressService _identityStress;
    private readonly IKitVideoMasterReviewService _masterReview;
    private readonly IKitVideoCharacterDnaService _characterDna;
    private readonly IKitVideoProductionReferencePackService _productionPack;
    private readonly ICharacterReferencePackService _characterReferencePack;
    private readonly IKitVideoProductionShotService _productionShot;
    private readonly ICharacterIdentityGovernanceService _identityGovernance;
    private readonly IProductionShotContractService _shotContract;
    private readonly IProductionPromptCompiler _promptCompiler;
    private readonly IImageGenerationContractService _imageGenerationContract;
    private readonly IImageGenerationExecutionService _imageGenerationExecution;
    private readonly IImageGenerationDirectorReviewService _imageDirectorReview;
    private readonly IProductionVideoContractService _videoContract;
    private readonly IVideoGenerationExecutionService _videoGeneration;
    private readonly IProductionOsService _productionOs;
    private readonly ICharacterProductionLibraryService _characterLibrary;
    private readonly IProductionProgressService _productionProgress;
    private readonly IFirstRealProductionService _firstRealProduction;
    private readonly ICharacterReferenceGenerationService _characterReferenceGeneration;
    private readonly ICharacterReferenceAutoGenerationV2Service _characterReferenceSet;
    private readonly ICharacterAuthorityInitializationV1Service _authorityInitialization;
    private readonly ICharacterAuthorityPipelineV1Service _authorityPipeline;
    private readonly ICharacterReferenceRegenerationV1Service _crpRegeneration;
    private readonly ICharacterStudioOrchestrator _characterStudio;
    private readonly ICharacterStudioGenerationService _characterStudioGeneration;
    private readonly IProjectVisualStyleAuthority _projectVisualStyle;
    private readonly IVisualUniverseAuthority _visualUniverse;
    private readonly IVisualCalibrationPackService _visualCalibration;
    private readonly IIdentityConditionedCalibrationDirectorReviewService _identityDirectorReview;

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
        IContentSeriesPilotService seriesPilot,
        IFamixaCharacterService characters,
        IKitVideoEngineService videoEngine,
        IKitVideoJobService videoJobs,
        IKitVideoAssetService videoAssets,
        IKitVideoContinuityService videoContinuity,
        IKitVideoVisionService videoVision,
        IKitVideoPixelService videoPixels,
        IKitVideoMotionService videoMotion,
        IKitVideoVisualSystemService visualSystem,
        IKitVideoMasterReferenceService masterReference,
        IKitVideoIdentityTestService identityTests,
        IKitVideoIdentityStressService identityStress,
        IKitVideoMasterReviewService masterReview,
        IKitVideoCharacterDnaService characterDna,
        IKitVideoProductionReferencePackService productionPack,
        ICharacterReferencePackService characterReferencePack,
        IKitVideoProductionShotService productionShot,
        ICharacterIdentityGovernanceService identityGovernance,
        IProductionShotContractService shotContract,
        IProductionPromptCompiler promptCompiler,
        IImageGenerationContractService imageGenerationContract,
        IImageGenerationExecutionService imageGenerationExecution,
        IImageGenerationDirectorReviewService imageDirectorReview,
        IProductionVideoContractService videoContract,
        IVideoGenerationExecutionService videoGeneration,
        IProductionOsService productionOs,
        ICharacterProductionLibraryService characterLibrary,
        IProductionProgressService productionProgress,
        IFirstRealProductionService firstRealProduction,
        ICharacterReferenceGenerationService characterReferenceGeneration,
        ICharacterReferenceAutoGenerationV2Service characterReferenceSet,
        ICharacterAuthorityInitializationV1Service authorityInitialization,
        ICharacterAuthorityPipelineV1Service authorityPipeline,
        ICharacterReferenceRegenerationV1Service crpRegeneration,
        ICharacterStudioOrchestrator characterStudio,
        ICharacterStudioGenerationService characterStudioGeneration,
        IProjectVisualStyleAuthority projectVisualStyle,
        IVisualUniverseAuthority visualUniverse,
        IVisualCalibrationPackService visualCalibration,
        IIdentityConditionedCalibrationDirectorReviewService identityDirectorReview)
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
        _characters = characters;
        _videoEngine = videoEngine;
        _videoJobs = videoJobs;
        _videoAssets = videoAssets;
        _videoContinuity = videoContinuity;
        _videoVision = videoVision;
        _videoPixels = videoPixels;
        _videoMotion = videoMotion;
        _visualSystem = visualSystem;
        _masterReference = masterReference;
        _identityTests = identityTests;
        _identityStress = identityStress;
        _masterReview = masterReview;
        _characterDna = characterDna;
        _productionPack = productionPack;
        _characterReferencePack = characterReferencePack;
        _productionShot = productionShot;
        _identityGovernance = identityGovernance;
        _shotContract = shotContract;
        _promptCompiler = promptCompiler;
        _imageGenerationContract = imageGenerationContract;
        _imageGenerationExecution = imageGenerationExecution;
        _imageDirectorReview = imageDirectorReview;
        _videoContract = videoContract;
        _videoGeneration = videoGeneration;
        _productionOs = productionOs;
        _characterLibrary = characterLibrary;
        _productionProgress = productionProgress;
        _firstRealProduction = firstRealProduction;
        _characterReferenceGeneration = characterReferenceGeneration;
        _characterReferenceSet = characterReferenceSet;
        _authorityInitialization = authorityInitialization;
        _authorityPipeline = authorityPipeline;
        _crpRegeneration = crpRegeneration;
        _characterStudio = characterStudio;
        _characterStudioGeneration = characterStudioGeneration;
        _projectVisualStyle = projectVisualStyle;
        _visualUniverse = visualUniverse;
        _visualCalibration = visualCalibration;
        _identityDirectorReview = identityDirectorReview;
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

    [HttpGet("series/characters")]
    public async Task<ActionResult<IReadOnlyList<FamixaCharacterDto>>> ListFamixaCharacters(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.ListAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("series/characters/{code}")]
    public async Task<ActionResult<FamixaCharacterDto>> GetFamixaCharacter(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.GetAsync(code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("series/characters")]
    public async Task<ActionResult<FamixaCharacterDto>> CreateFamixaCharacter(
        [FromBody] CreateFamixaCharacterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.CreateDraftAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpPut("series/characters/{code}/canon")]
    public async Task<ActionResult<FamixaCharacterDto>> PutFamixaCanon(
        string code,
        [FromBody] UpsertFamixaCharacterCanonRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.PutCanonAsync(code, request, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpPost("series/characters/{code}/approve")]
    public async Task<ActionResult<FamixaCharacterDto>> ApproveFamixaCharacter(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.ApproveAsync(code, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpPost("series/characters/{code}/lock")]
    public async Task<ActionResult<FamixaCharacterDto>> LockFamixaCharacter(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.LockAsync(code, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpPost("series/characters/{code}/unlock")]
    public async Task<ActionResult<FamixaCharacterDto>> UnlockFamixaCharacter(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.UnlockAsync(code, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpPut("series/characters/{code}/references")]
    public async Task<ActionResult<FamixaCharacterDto>> PutFamixaReferences(
        string code,
        [FromBody] IReadOnlyList<FamixaCharacterRefDto> references,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.PutReferencesAsync(code, references, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpPost("series/characters/guard")]
    public async Task<ActionResult<FamixaCharacterGuardDto>> GuardFamixaCharacters(
        [FromBody] FamixaCharacterGuardRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.GuardAsync(request, cancellationToken));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("series/characters/detect-duplicate")]
    public async Task<ActionResult<FamixaCharacterDuplicateDto>> DetectFamixaDuplicate(
        [FromBody] FamixaCharacterDuplicateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _characters.DetectDuplicateAsync(request, cancellationToken));

    [HttpPost("series/characters/{code}/versions")]
    public async Task<ActionResult<FamixaCharacterDto>> CreateFamixaVersion(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.CreateVersionAsync(code, User.Identity?.Name, "new-version", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterConflictOrBad(ex);
        }
    }

    [HttpGet("series/characters/{code}/versions")]
    public async Task<ActionResult<IReadOnlyList<FamixaCharacterVersionDto>>> ListFamixaVersions(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characters.ListVersionsAsync(code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("series/characters/{code}/audit")]
    public async Task<ActionResult<IReadOnlyList<FamixaCharacterAuditDto>>> ListFamixaAudit(
        string code,
        CancellationToken cancellationToken) =>
        Ok(await _characters.ListAuditAsync(code, cancellationToken));

    [HttpGet("video-engine/visual-system")]
    public async Task<ActionResult<KitVideoVisualSystemDto>> GetVisualSystem(
        [FromQuery] string projectCode = "FAMIXA",
        [FromQuery] string? systemCode = null,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await _visualSystem.GetAsync(projectCode, systemCode, version, cancellationToken);
            if (row is null) return NotFound();
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/visual-system/{id:guid}/lock")]
    public async Task<ActionResult<KitVideoVisualSystemDto>> LockVisualSystem(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _visualSystem.LockAsync(id, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("project-visual-style")]
    public async Task<ActionResult<ProjectVisualStyleDto>> GetProjectVisualStyle(
        [FromQuery] string projectId = "FAMIXA",
        CancellationToken cancellationToken = default) =>
        Ok(await _projectVisualStyle.GetActiveAsync(projectId, cancellationToken));

    [HttpGet("project-visual-style/presets")]
    public ActionResult<object> ProjectVisualStylePresets() =>
        Ok(new { documentId = ProjectVisualStyleV1Rules.DocumentId, items = _projectVisualStyle.Presets() });

    [HttpGet("project-visual-style/characters/{characterId}")]
    public async Task<ActionResult<ProjectVisualStyleCharacterBindDto>> ResolveProjectVisualStyle(
        string characterId,
        [FromQuery] string projectId = "FAMIXA",
        CancellationToken cancellationToken = default) =>
        Ok(await _projectVisualStyle.ResolveForCharacterAsync(projectId, characterId, cancellationToken));

    [HttpPost("project-visual-style/initialize")]
    public async Task<ActionResult<ProjectVisualStyleDto>> InitializeProjectVisualStyle(
        [FromBody] ProjectVisualStyleInitializeRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _projectVisualStyle.InitializeAsync(
                request ?? new ProjectVisualStyleInitializeRequest(),
                User.Identity?.Name ?? "director",
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("project-visual-style/versions")]
    public async Task<ActionResult<ProjectVisualStyleDto>> CreateProjectVisualStyleVersion(
        [FromBody] ProjectVisualStyleInitializeRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _projectVisualStyle.CreateVersionAsync(
                request ?? new ProjectVisualStyleInitializeRequest(),
                User.Identity?.Name ?? "director",
                cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("project-visual-style/regression")]
    public ActionResult<object> ProjectVisualStyleRegression()
    {
        var fail = _projectVisualStyle.RunRegression();
        return Ok(new
        {
            suite = ProjectVisualStyleV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            autoApprove = false,
            autoLock = false,
        });
    }

    [HttpGet("project-visual-style/revision/regression")]
    [HttpGet("project-visual-style/v2/regression")]
    [HttpGet("character-studio/project-visual-style/v2/regression")]
    public ActionResult<object> ProjectVisualStyleRevisionRegression()
    {
        var fail = _projectVisualStyle.RunRevisionRegression();
        return Ok(new
        {
            suite = ProjectVisualStyleV2Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            autoApprove = false,
            autoLock = false,
            providerCalled = false,
            generationExecuted = false,
        });
    }

    [HttpGet("project-visual-style/v2")]
    [HttpGet("character-studio/project-visual-style/v2")]
    public async Task<ActionResult<ProjectVisualStyleV2DefinitionDto>> GetProjectVisualStyleV2(
        [FromQuery] string projectId = "FAMIXA",
        CancellationToken cancellationToken = default) =>
        Ok(await _projectVisualStyle.GetV2Async(projectId, cancellationToken));

    [HttpGet("project-visual-style/{projectId}/revision/impact")]
    public async Task<ActionResult<ProjectVisualStyleImpactDto>> ProjectVisualStyleRevisionImpact(
        string projectId, CancellationToken cancellationToken = default) =>
        Ok(await _projectVisualStyle.ImpactAsync(projectId, cancellationToken));

    [HttpPost("project-visual-style/{projectId}/revision")]
    public async Task<ActionResult<ProjectVisualStyleRevisionResultDto>> RequestProjectVisualStyleRevision(
        string projectId,
        [FromBody] ProjectVisualStyleRevisionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _projectVisualStyle.RequestRevisionAsync(
                projectId, request ?? new ProjectVisualStyleRevisionRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("project-visual-style/{projectId}/revision/approve")]
    public async Task<ActionResult<ProjectVisualStyleRevisionResultDto>> ApproveProjectVisualStyleRevision(
        string projectId,
        [FromBody] ProjectVisualStyleRevisionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _projectVisualStyle.ApproveRevisionAsync(
                projectId, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("project-visual-style/{projectId}/revision/reject")]
    public async Task<ActionResult<ProjectVisualStyleRevisionResultDto>> RejectProjectVisualStyleRevision(
        string projectId,
        [FromBody] ProjectVisualStyleRevisionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _projectVisualStyle.RejectRevisionAsync(
                projectId, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("project-visual-style/{projectId}/revision/lock")]
    public async Task<ActionResult<ProjectVisualStyleRevisionResultDto>> LockProjectVisualStyleRevision(
        string projectId,
        [FromBody] ProjectVisualStyleRevisionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _projectVisualStyle.LockRevisionAsync(
                projectId, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/master-reference")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> GetMasterReference(
        [FromQuery] string projectCode = "FAMIXA",
        [FromQuery] string assetCode = "CHAR-001",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var row = await _masterReference.GetAsync(projectCode, assetCode, cancellationToken);
            if (row is null) return NotFound();
            return Ok(row);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/candidates")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> RegisterMasterCandidate(
        [FromBody] KitVideoMasterCandidateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReference.RegisterCandidateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/candidates/{id:guid}/qa")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> QaMasterCandidate(
        Guid id,
        [FromBody] KitVideoMasterQaRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReference.ApplyQaAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/approve")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> ApproveMasterReference(
        [FromQuery] string projectCode = "FAMIXA",
        [FromQuery] string assetCode = "CHAR-001",
        [FromQuery] Guid? candidateId = null,
        [FromQuery] string decision = "APPROVE",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _masterReference.ApproveAsync(projectCode, assetCode, candidateId, decision, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/lock")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> LockMasterReference(
        [FromQuery] string projectCode = "FAMIXA",
        [FromQuery] string assetCode = "CHAR-001",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _masterReference.LockAsync(projectCode, assetCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/compile")]
    public async Task<ActionResult<KitVideoMasterCompileDto>> CompileMasterReference(
        [FromQuery] string role = "FRONT",
        [FromQuery] bool dnaApproved = false,
        [FromQuery] bool styleReady = false)
    {
        return Ok(await _masterReference.CompileAsync(role, dnaApproved, styleReady));
    }

    [HttpPost("video-engine/master-reference/generate")]
    public async Task<ActionResult<KitVideoMasterGenerateDto>> GenerateMasterReference(
        [FromBody] KitVideoMasterGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReference.GenerateCandidateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/candidates/{id:guid}/analyze")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> AnalyzeMasterCandidate(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReference.AnalyzeAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/master-reference/candidates/{id:guid}/image")]
    public async Task<IActionResult> MasterCandidateImage(Guid id, CancellationToken cancellationToken)
    {
        var file = await _masterReference.ReadCandidateImageAsync(id, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime);
    }

    [HttpPost("video-engine/master-reference/compare")]
    [HttpPost("video-engine/characters/{characterCode}/eras/{era}/candidates/compare")]
    public async Task<ActionResult<KitVideoMasterCompareDto>> CompareMasterCandidates(
        string? characterCode = null,
        string? era = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            KitVideoMasterReferenceRules.EnsureProjectAsset("FAMIXA", characterCode is null or "CHAR-001" ? "CHAR-001" : characterCode);
            if (!string.IsNullOrWhiteSpace(era) && !era.Equals("ERA-01", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "SELECTION: ERA-01 only." });
            return Ok(await _masterReference.CompareAsync("FAMIXA", "CHAR-001", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/select")]
    [HttpPost("video-engine/characters/{characterCode}/eras/{era}/candidates/{id:guid}/select")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> SelectMasterCandidate(
        Guid? id,
        [FromQuery] Guid? candidateId,
        string? characterCode = null,
        string? era = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pick = id ?? candidateId ?? throw new InvalidOperationException("candidateId required.");
            return Ok(await _masterReference.SelectAsync("FAMIXA", "CHAR-001", pick, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-reference/candidates/{id:guid}/front-runner")]
    public async Task<ActionResult<KitVideoMasterReferenceDto>> MarkMasterFrontRunner(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReference.MarkFrontRunnerAsync("FAMIXA", "CHAR-001", id, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/master-reference/resolve")]
    [HttpGet("video-engine/characters/{characterCode}/eras/{era}/master-reference")]
    public async Task<ActionResult<KitVideoMasterResolveDto>> ResolveMasterReference(
        string? characterCode = null,
        string? era = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _masterReference.ResolveAsync("FAMIXA", "CHAR-001", cancellationToken));

    [HttpGet("video-engine/master-reference/events")]
    public async Task<ActionResult<IReadOnlyList<KitVideoMasterEventDto>>> MasterReferenceEvents(
        CancellationToken cancellationToken) =>
        Ok(await _masterReference.ListEventsAsync("FAMIXA", "CHAR-001", cancellationToken));

    [HttpGet("video-engine/identity-tests")]
    public async Task<ActionResult<IReadOnlyList<KitVideoIdentityTestDto>>> ListIdentityTests(
        [FromQuery] Guid? candidateId,
        CancellationToken cancellationToken) =>
        Ok(await _identityTests.ListAsync(candidateId, cancellationToken));

    [HttpPost("video-engine/identity-tests")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> CreateIdentityTest(
        [FromBody] KitVideoIdentityTestCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityTests.CreateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/identity-tests/{id:guid}")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> GetIdentityTest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityTests.GetAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/identity-tests/{id:guid}/artifacts")]
    public async Task<ActionResult<IReadOnlyList<KitVideoIdentityTestArtifactDto>>> IdentityTestArtifacts(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await _identityTests.ListArtifactsAsync(id, cancellationToken));

    [HttpGet("video-engine/identity-tests/{id:guid}/summary")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> IdentityTestSummary(Guid id, CancellationToken cancellationToken) =>
        Ok(await _identityTests.SummaryAsync(id, cancellationToken));

    [HttpPost("video-engine/identity-tests/{id:guid}/run")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> RunIdentityTest(
        Guid id,
        [FromBody] KitVideoIdentityTestRunRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityTests.RunAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity-tests/{id:guid}/analyze")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> AnalyzeIdentityTest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityTests.AnalyzeAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity-tests/{id:guid}/compare")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> CompareIdentityTest(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityTests.CompareAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity-tests/{id:guid}/director-decision")]
    public async Task<ActionResult<KitVideoIdentityTestDto>> IdentityTestDirectorDecision(
        Guid id,
        [FromBody] KitVideoIdentityTestDecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityTests.DecideAsync(id, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/identity-tests/artifacts/{artifactId:guid}/image")]
    public async Task<IActionResult> IdentityTestArtifactImage(Guid artifactId, CancellationToken cancellationToken)
    {
        var file = await _identityTests.ReadArtifactImageAsync(artifactId, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime);
    }

    [HttpGet("video-engine/identity/stress-test")]
    public async Task<ActionResult<IReadOnlyList<KitVideoIdentityStressDto>>> ListIdentityStress(
        [FromQuery] Guid? candidateId,
        CancellationToken cancellationToken) =>
        Ok(await _identityStress.ListAsync(candidateId, cancellationToken));

    [HttpPost("video-engine/identity/stress-test")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> CreateIdentityStress(
        [FromBody] KitVideoIdentityStressCreateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.CreateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/identity/stress-test/{id:guid}")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> GetIdentityStress(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.GetAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity/stress-test/{id:guid}/run")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> RunIdentityStress(
        Guid id,
        [FromBody] KitVideoIdentityStressRunRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.RunAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity/stress-test/{id:guid}/analyze")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> AnalyzeIdentityStress(
        Guid id,
        [FromQuery] string? caseCode,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.AnalyzeAsync(id, caseCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity/stress-test/{id:guid}/director-pass")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> IdentityStressDirectorPass(
        Guid id,
        [FromBody] KitVideoIdentityStressDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.DecideAsync(id, new KitVideoIdentityStressDecisionRequest("PASS", request?.Reason), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity/stress-test/{id:guid}/reject")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> IdentityStressReject(
        Guid id,
        [FromBody] KitVideoIdentityStressDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.DecideAsync(id, new KitVideoIdentityStressDecisionRequest("REJECT", request?.Reason), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity/stress-test/{id:guid}/repair")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> IdentityStressRepair(
        Guid id,
        [FromBody] KitVideoIdentityStressRepairRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.RepairAsync(id, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/identity/stress-test/{id:guid}/promote")]
    public async Task<ActionResult<KitVideoIdentityStressDto>> IdentityStressPromote(
        Guid id,
        [FromBody] KitVideoIdentityStressDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _identityStress.DecideAsync(id, new KitVideoIdentityStressDecisionRequest("PROMOTE_TO_MASTER_REVIEW", request?.Reason), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/identity/stress-test/artifacts/{artifactId:guid}/image")]
    public async Task<IActionResult> IdentityStressArtifactImage(Guid artifactId, CancellationToken cancellationToken)
    {
        var file = await _identityStress.ReadArtifactImageAsync(artifactId, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime);
    }

    [HttpGet("video-engine/master-review")]
    public async Task<ActionResult<IReadOnlyList<KitVideoMasterReviewDto>>> ListMasterReview(CancellationToken cancellationToken) =>
        Ok(await _masterReview.ListAsync(cancellationToken));

    [HttpPost("video-engine/master-review")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> OpenMasterReview(
        [FromBody] KitVideoMasterReviewOpenRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.OpenAsync(request ?? new KitVideoMasterReviewOpenRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpGet("video-engine/master-review/{id:guid}")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> GetMasterReview(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.GetAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-review/{id:guid}/director-pass")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewDirectorPass(
        Guid id,
        [FromBody] KitVideoMasterReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.DecideAsync(id, new KitVideoMasterReviewDecisionRequest("PASS", request?.Note), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpPost("video-engine/master-review/{id:guid}/conditional")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewConditional(
        Guid id,
        [FromBody] KitVideoMasterReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.DecideAsync(id, new KitVideoMasterReviewDecisionRequest("CONDITIONAL", request?.Note), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-review/{id:guid}/reject")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewReject(
        Guid id,
        [FromBody] KitVideoMasterReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.DecideAsync(id, new KitVideoMasterReviewDecisionRequest("REJECT", request?.Note), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpPost("video-engine/master-review/{id:guid}/select")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewSelect(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.SelectMasterAsync(id, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-review/{id:guid}/approve")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewApprove(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.ApproveAsync(id, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/master-review/{id:guid}/lock")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewLock(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _masterReview.LockAsync(id, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpGet("video-engine/master-reference/canon")]
    public async Task<ActionResult<KitVideoMasterLockDto?>> ResolveMasterCanon(
        [FromQuery] string characterId = "CHAR-001",
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        Ok(await _masterReview.ResolveCanonAsync(characterId, eraId, cancellationToken));

    [HttpPut("video-engine/master-reference/{id:guid}")]
    [HttpDelete("video-engine/master-reference/{id:guid}")]
    public async Task<ActionResult> RejectMasterMutation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _masterReview.RejectMasterMutationAsync(id, Request.Method == "DELETE" ? "DELETE" : "UPDATE", cancellationToken);
            return Conflict(new { message = "MASTER_LOCKED" });
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpGet("/api/characters/{characterId}/master-review/{candidateId:guid}")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> GetMasterReviewByCandidate(
        string characterId, Guid candidateId, CancellationToken cancellationToken)
    {
        try
        {
            _ = characterId;
            return Ok(await _masterReview.GetByCandidateAsync(candidateId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpPost("/api/characters/{characterId}/master-review/{candidateId:guid}/director-pass")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewDirectorPassByCandidate(
        string characterId,
        Guid candidateId,
        [FromBody] KitVideoMasterReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _masterReview.GetByCandidateAsync(candidateId, cancellationToken);
            _ = characterId;
            return Ok(await _masterReview.DecideAsync(row.Id, new KitVideoMasterReviewDecisionRequest("PASS", request?.Note), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpPost("/api/characters/{characterId}/master-review/{candidateId:guid}/reject")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewRejectByCandidate(
        string characterId,
        Guid candidateId,
        [FromBody] KitVideoMasterReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var row = await _masterReview.GetByCandidateAsync(candidateId, cancellationToken);
            _ = characterId;
            return Ok(await _masterReview.DecideAsync(row.Id, new KitVideoMasterReviewDecisionRequest("REJECT", request?.Note), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return MasterReviewError(ex);
        }
    }

    [HttpGet("/api/characters/{characterId}/master-reference")]
    public async Task<ActionResult<KitVideoMasterLockDto?>> GetCharacterMasterReference(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        Ok(await _masterReview.ResolveCanonAsync(characterId, eraId, cancellationToken));

    [HttpPost("/api/characters/{characterId}/eras/{eraId}/master-review/pass")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReviewPassAlias(
        string characterId,
        string eraId,
        [FromBody] KitVideoMasterReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var rows = await _masterReview.ListAsync(cancellationToken);
        var row = rows.FirstOrDefault(r =>
            r.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)
            && r.EraId.Equals(eraId, StringComparison.OrdinalIgnoreCase));
        if (row is null) return NotFound(new { message = "MASTER_REVIEW: chưa mở review." });
        return await MasterReviewDirectorPass(row.Id, request, cancellationToken);
    }

    [HttpPost("/api/characters/{characterId}/eras/{eraId}/master-reference/lock")]
    public async Task<ActionResult<KitVideoMasterReviewDto>> MasterReferenceLockAlias(
        string characterId,
        string eraId,
        CancellationToken cancellationToken)
    {
        var rows = await _masterReview.ListAsync(cancellationToken);
        var row = rows.FirstOrDefault(r =>
            r.CharacterId.Equals(characterId, StringComparison.OrdinalIgnoreCase)
            && r.EraId.Equals(eraId, StringComparison.OrdinalIgnoreCase));
        if (row is null) return NotFound(new { message = "MASTER_REVIEW: chưa mở review." });
        return await MasterReviewLock(row.Id, cancellationToken);
    }

    private ActionResult MasterReviewError(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("MASTER_REFERENCE_GATE_NOT_SATISFIED", StringComparison.Ordinal))
            return BadRequest(new { message = "MASTER_REFERENCE_GATE_NOT_SATISFIED", detail = ex.Message });
        if (ex.Message.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = ex.Message });
        return Conflict(new { message = ex.Message });
    }

    [HttpGet("video-engine/character-dna")]
    [HttpGet("/api/characters/{characterId}/dna")]
    public async Task<ActionResult<KitVideoCharacterDnaGetDto>> GetCharacterDna(
        string? characterId = "CHAR-001",
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.GetAsync(characterId ?? "CHAR-001", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpGet("video-engine/character-dna/{dnaVersion}")]
    [HttpGet("/api/characters/{characterId}/dna/{dnaVersion}")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> GetCharacterDnaVersion(
        string? characterId,
        string dnaVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _characterDna.GetVersionAsync(characterId ?? "CHAR-001", dnaVersion, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPost("video-engine/character-dna")]
    [HttpPost("/api/characters/{characterId}/dna")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> CreateCharacterDna(
        string? characterId = "CHAR-001",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.CreateAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPost("video-engine/character-dna/approve")]
    [HttpPost("/api/characters/{characterId}/dna/approve")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> ApproveCharacterDna(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoCharacterDnaNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.ApproveAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPost("video-engine/character-dna/reject")]
    [HttpPost("/api/characters/{characterId}/dna/reject")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> RejectCharacterDna(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoCharacterDnaNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.RejectAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPost("video-engine/character-dna/analyze")]
    [HttpPost("/api/characters/{characterId}/dna/analyze")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> AnalyzeCharacterDna(
        string? characterId = "CHAR-001",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.AnalyzeAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPost("video-engine/character-dna/edit")]
    [HttpPost("/api/characters/{characterId}/dna/edit")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> EditCharacterDna(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoCharacterDnaEditRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.EditAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request ?? new KitVideoCharacterDnaEditRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPost("video-engine/character-dna/return")]
    [HttpPost("/api/characters/{characterId}/dna/return")]
    public async Task<ActionResult<KitVideoCharacterDnaDto>> ReturnCharacterDna(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoCharacterDnaNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterDna.ReturnToEditAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    [HttpPut("video-engine/character-dna/{id:guid}")]
    [HttpDelete("video-engine/character-dna/{id:guid}")]
    public async Task<ActionResult> RejectCharacterDnaMutation(Guid id, CancellationToken cancellationToken)
    {
        _ = id;
        try
        {
            await _characterDna.RejectMutationAsync("CHAR-001", Request.Method == "DELETE" ? "DELETE" : "UPDATE", cancellationToken);
            return Conflict(new { message = "DNA_LOCKED" });
        }
        catch (InvalidOperationException ex)
        {
            return CharacterDnaError(ex);
        }
    }

    private ActionResult CharacterDnaError(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("DNA_GATE_NOT_SATISFIED", StringComparison.Ordinal)
            || ex.Message.StartsWith("DNA_INVALID", StringComparison.Ordinal))
            return BadRequest(new { message = ex.Message.StartsWith("DNA_INVALID", StringComparison.Ordinal) ? "DNA_INVALID" : "DNA_GATE_NOT_SATISFIED", detail = ex.Message });
        if (ex.Message.StartsWith("DNA_LOCKED", StringComparison.Ordinal))
            return Conflict(new { message = "DNA_LOCKED", detail = ex.Message });
        if (ex.Message.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = ex.Message });
        return Conflict(new { message = ex.Message });
    }

    [HttpGet("video-engine/character-reference-pack")]
    public async Task<ActionResult<CharacterReferencePackGetDto>> GetCharacterReferencePack(
        [FromQuery] string? characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferencePack.GetAsync(characterId ?? "", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpGet("characters/{characterId}/reference-pack")]
    [HttpGet("video-engine/characters/{characterId}/reference-pack")]
    public Task<ActionResult<CharacterReferencePackGetDto>> GetCharacterReferencePackByRoute(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        GetCharacterReferencePack(characterId, eraId, cancellationToken);

    [HttpPost("characters/{characterId}/reference-pack")]
    [HttpPost("video-engine/character-reference-pack")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack")]
    public async Task<ActionResult<CharacterReferencePackDto>> CreateCharacterReferencePack(
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferencePack.CreateAsync(
                characterId ?? characterIdQuery ?? "", User.Identity?.Name ?? "director", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpPut("video-engine/character-reference-pack/{packId:guid}")]
    [HttpPut("video-engine/characters/{characterId}/reference-pack/{packId:guid}")]
    public async Task<ActionResult<CharacterReferencePackDto>> UpsertCharacterReferenceItem(
        Guid packId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromBody] CharacterReferenceItemRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferencePack.UpsertItemAsync(
                characterId ?? characterIdQuery ?? "", packId, request ?? new CharacterReferenceItemRequest(""),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/items/{type}/attach")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/items/{type}/attach")]
    public async Task<ActionResult<CharacterReferencePackDto>> AttachCharacterReferenceItem(
        Guid packId,
        string type,
        [FromForm] IFormFile file,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms, cancellationToken);
            return Ok(await _characterReferencePack.AttachItemAsync(
                characterId ?? characterIdQuery ?? "", packId, type, ms.ToArray(), file.FileName,
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/items/{type}/register")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/items/{type}/register")]
    public async Task<ActionResult<CharacterReferencePackDto>> RegisterCharacterReferenceItem(
        Guid packId,
        string type,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromBody] CharacterReferenceItemRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferencePack.RegisterExistingAsync(
                characterId ?? characterIdQuery ?? "", packId, type, request?.ArtifactPath ?? "",
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpGet("video-engine/character-reference-pack/{packId:guid}/items/{itemId:guid}/image")]
    [HttpGet("video-engine/characters/{characterId}/reference-pack/{packId:guid}/items/{itemId:guid}/image")]
    public async Task<IActionResult> CharacterReferenceItemImage(
        Guid packId,
        Guid itemId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _characterReferencePack.ReadItemImageAsync(
                characterId ?? characterIdQuery ?? "", packId, itemId, cancellationToken);
            if (file is null) return NotFound();
            return File(file.Value.Bytes, file.Value.Mime);
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/validate")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/validate")]
    public Task<ActionResult<CharacterReferencePackDto>> ValidateCharacterReferencePack(
        Guid packId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default) =>
        CharacterReferencePackAction(characterId ?? characterIdQuery, packId, "validate", null, cancellationToken);

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/approve")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/approve")]
    public Task<ActionResult<CharacterReferencePackDto>> ApproveCharacterReferencePack(
        Guid packId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromBody] CharacterReferencePackNoteRequest? request = null,
        CancellationToken cancellationToken = default) =>
        CharacterReferencePackAction(characterId ?? characterIdQuery, packId, "approve", request?.Note, cancellationToken);

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/reject")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/reject")]
    public Task<ActionResult<CharacterReferencePackDto>> RejectCharacterReferencePack(
        Guid packId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromBody] CharacterReferencePackNoteRequest? request = null,
        CancellationToken cancellationToken = default) =>
        CharacterReferencePackAction(characterId ?? characterIdQuery, packId, "reject", request?.Note, cancellationToken);

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/lock")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/lock")]
    public Task<ActionResult<CharacterReferencePackDto>> LockCharacterReferencePack(
        Guid packId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default) =>
        CharacterReferencePackAction(characterId ?? characterIdQuery, packId, "lock", null, cancellationToken);

    [HttpPost("video-engine/character-reference-pack/{packId:guid}/supersede")]
    [HttpPost("video-engine/characters/{characterId}/reference-pack/{packId:guid}/supersede")]
    public Task<ActionResult<CharacterReferencePackDto>> SupersedeCharacterReferencePack(
        Guid packId,
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default) =>
        CharacterReferencePackAction(characterId ?? characterIdQuery, packId, "supersede", null, cancellationToken);

    [HttpGet("video-engine/character-library")]
    public async Task<ActionResult<CharacterLibraryListDto>> ListCharacterLibrary(
        [FromQuery] string? q = null,
        [FromQuery] string? filter = null,
        [FromQuery] string? sort = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterLibrary.ListAsync(q, filter, sort, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, generate = false });
        }
    }

    [HttpGet("video-engine/character-library/regression")]
    public ActionResult<object> CharacterProductionLibraryRegression()
    {
        var failures = _characterLibrary.RunRegression();
        return Ok(new
        {
            suite = CharacterProductionLibraryRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/production-progress")]
    public async Task<ActionResult<ProductionProgressListDto>> ListProductionProgress(
        [FromQuery] string seriesCode = "FAMIXA",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionProgress.ListAsync(seriesCode, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, generate = false });
        }
    }

    [HttpGet("video-engine/series/builds/{id:guid}/production-progress")]
    public async Task<ActionResult<ProductionProgressDto>> GetProductionProgress(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionProgress.GetBuildAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("NOT_FOUND", StringComparison.Ordinal))
                return NotFound(new { message = ex.Message, generate = false });
            return BadRequest(new { message = ex.Message, generate = false });
        }
    }

    [HttpGet("video-engine/production-progress/regression")]
    public ActionResult<object> ProductionProgressRegression()
    {
        var failures = _productionProgress.RunRegression();
        return Ok(new
        {
            suite = ProductionProgressRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/character-library/{characterId}")]
    public async Task<ActionResult<CharacterLibraryDetailDto>> GetCharacterLibrary(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterLibrary.GetAsync(characterId, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("không thuộc", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = ex.Message, generate = false });
            return NotFound(new { message = ex.Message, generate = false });
        }
    }

    [HttpGet("video-engine/character-reference-pack/existing-full-body")]
    public async Task<ActionResult<CharacterReferenceExistingFullBodyDto>> FindExistingCharacterReferenceFullBody(
        [FromQuery] string? characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferencePack.FindExistingFullBodyAsync(characterId ?? "", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    [HttpGet("video-engine/character-reference-pack/regression")]
    [HttpGet("video-engine/characters/reference-pack/regression")]
    public ActionResult<object> CharacterReferencePackRegression(string? characterId = null)
    {
        _ = characterId;
        var failures = _characterReferencePack.RunRegression();
        return Ok(new
        {
            suite = CharacterReferencePackRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/character-reference-approval-lock/regression")]
    public ActionResult<object> CharacterReferenceApprovalLockRegression()
    {
        var failures = _characterReferencePack.RunApprovalLockRegression();
        return Ok(new
        {
            suite = CharacterReferenceApprovalLockRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = failures.Count,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/character-reference-completion/regression")]
    public ActionResult<object> CharacterReferenceCompletionRegression()
    {
        var failures = _characterReferencePack.RunCompletionRegression();
        return Ok(new
        {
            suite = CharacterReferenceCompletionRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/characters/{characterId}/reference-generation-set")]
    public async Task<ActionResult<CharacterReferenceSetDto>> GetCharacterReferenceSet(
        string characterId,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceSet.GetAsync(characterId, provider, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-generation-set/prepare")]
    public async Task<ActionResult<CharacterReferenceSetDto>> PrepareCharacterReferenceSet(
        string characterId,
        [FromBody] CharacterReferenceSetRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceSet.PrepareAsync(
                characterId, request ?? new CharacterReferenceSetRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-generation-set/execute")]
    public async Task<ActionResult<CharacterReferenceSetDto>> ExecuteCharacterReferenceSet(
        string characterId,
        [FromBody] CharacterReferenceSetRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceSet.ExecuteAsync(
                characterId, request ?? new CharacterReferenceSetRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/character-reference-auto-generation-v2/regression")]
    public ActionResult<object> CharacterReferenceAutoGenerationV2Regression()
    {
        var fail = _characterReferenceSet.RunRegression();
        return Ok(new
        {
            suite = CharacterReferenceAutoGenerationV2Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
        });
    }

    [HttpGet("video-engine/characters/{characterId}/authority-initialization")]
    public async Task<ActionResult<CharacterAuthorityInitializationDto>> GetAuthorityInitialization(
        string characterId,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _authorityInitialization.GetAsync(characterId, provider, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/master/prepare")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> PrepareAuthorityMaster(
        string characterId, [FromBody] CharacterAuthorityInitializationRequestDto? request, CancellationToken cancellationToken) =>
        AuthorityAction(characterId, request, (id, body, actor, ct) =>
            _authorityInitialization.PrepareMasterAsync(id, body, actor, ct), cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/master/execute")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> ExecuteAuthorityMaster(
        string characterId, [FromBody] CharacterAuthorityInitializationRequestDto? request, CancellationToken cancellationToken) =>
        AuthorityAction(characterId, request, (id, body, actor, ct) =>
            _authorityInitialization.ExecuteMasterAsync(id, body, actor, ct), cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/master/approve")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> ApproveAuthorityMaster(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.ApproveMasterAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/master/reject")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> RejectAuthorityMaster(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.RejectMasterAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/master/lock")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> LockAuthorityMaster(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.LockMasterAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/dna/prepare")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> PrepareAuthorityDna(
        string characterId, [FromBody] CharacterAuthorityInitializationRequestDto? request, CancellationToken cancellationToken) =>
        AuthorityAction(characterId, request, (id, body, actor, ct) =>
            _authorityInitialization.PrepareDnaAsync(id, body, actor, ct), cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/dna/execute")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> ExecuteAuthorityDna(
        string characterId, [FromBody] CharacterAuthorityInitializationRequestDto? request, CancellationToken cancellationToken) =>
        AuthorityAction(characterId, request, (id, body, actor, ct) =>
            _authorityInitialization.ExecuteDnaAsync(id, body, actor, ct), cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/dna/approve")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> ApproveAuthorityDna(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.ApproveDnaAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/dna/reject")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> RejectAuthorityDna(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.RejectDnaAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/dna/lock")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> LockAuthorityDna(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.LockDnaAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/prp/prepare")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> PrepareAuthorityPrp(
        string characterId, [FromBody] CharacterAuthorityInitializationRequestDto? request, CancellationToken cancellationToken) =>
        AuthorityAction(characterId, request, (id, body, actor, ct) =>
            _authorityInitialization.PreparePrpAsync(id, body, actor, ct), cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/prp/execute")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> ExecuteAuthorityPrp(
        string characterId, [FromBody] CharacterAuthorityInitializationRequestDto? request, CancellationToken cancellationToken) =>
        AuthorityAction(characterId, request, (id, body, actor, ct) =>
            _authorityInitialization.ExecutePrpAsync(id, body, actor, ct), cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/prp/approve")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> ApproveAuthorityPrp(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.ApprovePrpAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/prp/reject")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> RejectAuthorityPrp(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.RejectPrpAsync, cancellationToken);

    [HttpPost("video-engine/characters/{characterId}/authority-initialization/prp/lock")]
    public Task<ActionResult<CharacterAuthorityInitializationDto>> LockAuthorityPrp(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AuthorityMutate(characterId, eraId, _authorityInitialization.LockPrpAsync, cancellationToken);

    [HttpGet("video-engine/characters/{characterId}/authority-initialization/master/image")]
    public async Task<IActionResult> AuthorityMasterImage(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _authorityInitialization.ReadMasterImageAsync(characterId, eraId, cancellationToken);
            if (file is null) return NotFound();
            return File(file.Value.Bytes, file.Value.Mime);
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/character-authority-initialization-v1/regression")]
    public ActionResult<object> CharacterAuthorityInitializationV1Regression()
    {
        var fail = _authorityInitialization.RunRegression();
        return Ok(new
        {
            suite = CharacterAuthorityInitializationV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
        });
    }

    [HttpGet("video-engine/characters/{characterId}/authority-pipeline")]
    public async Task<ActionResult<CharacterAuthorityPipelineDto>> GetCharacterAuthorityPipeline(
        string characterId,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _authorityPipeline.GetAsync(characterId, provider, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/authority-pipeline/crp/approve")]
    public async Task<ActionResult<CharacterAuthorityPipelineDto>> ApproveCharacterAuthorityPipelineCrp(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _authorityPipeline.ApproveCrpAsync(
                characterId, User.Identity?.Name ?? "director", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/authority-pipeline/crp/lock")]
    public async Task<ActionResult<CharacterAuthorityPipelineDto>> LockCharacterAuthorityPipelineCrp(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _authorityPipeline.LockCrpAsync(
                characterId, User.Identity?.Name ?? "director", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/authority-pipeline/crp/reject")]
    public async Task<ActionResult<CharacterReferenceRegenerationDto>> RejectCharacterAuthorityPipelineCrp(
        string characterId,
        [FromBody] CharacterReferenceRejectRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _crpRegeneration.RejectAsync(
                characterId, request ?? new CharacterReferenceRejectRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/authority-pipeline/crp/regenerate/prepare")]
    public async Task<ActionResult<CharacterReferenceRegenerationDto>> PrepareCharacterReferenceRegeneration(
        string characterId,
        [FromBody] CharacterReferenceRegenerateRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _crpRegeneration.PrepareAsync(
                characterId, request ?? new CharacterReferenceRegenerateRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/authority-pipeline/crp/regenerate")]
    public async Task<ActionResult<CharacterReferenceRegenerationDto>> ExecuteCharacterReferenceRegeneration(
        string characterId,
        [FromBody] CharacterReferenceRegenerateRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _crpRegeneration.ExecuteAsync(
                characterId, request ?? new CharacterReferenceRegenerateRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/characters/{characterId}/authority-pipeline/crp/history")]
    public async Task<ActionResult<CharacterReferenceRegenerationDto>> CharacterReferenceRegenerationHistory(
        string characterId, [FromQuery] string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _crpRegeneration.HistoryAsync(characterId, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/characters/{characterId}/authority-pipeline/crp")]
    public async Task<ActionResult<CharacterReferenceRegenerationDto>> GetCharacterReferenceRegeneration(
        string characterId,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _crpRegeneration.GetAsync(characterId, provider, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/character-reference-regeneration-v1/regression")]
    public ActionResult<object> CharacterReferenceRegenerationV1Regression()
    {
        var fail = _crpRegeneration.RunRegression();
        return Ok(new
        {
            suite = CharacterReferenceRegenerationV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/styles")]
    public ActionResult<CharacterStudioStyleListDto> CharacterStudioStyles() =>
        Ok(_characterStudio.Styles());

    [HttpGet("character-studio/regression")]
    public ActionResult<object> CharacterStudioRegression()
    {
        var fail = _characterStudio.RunRegression();
        return Ok(new
        {
            suite = CharacterStudioV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/identity-lock/regression")]
    public ActionResult<object> CharacterStudioIdentityLockRegression()
    {
        var fail = _characterStudio.RunIdentityLockRegression();
        return Ok(new
        {
            suite = CharacterStudioIdentityLockV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/characters")]
    public async Task<ActionResult<CharacterStudioListDto>> ListCharacterStudio(
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _characterStudio.ListAsync(cancellationToken)); }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> CreateCharacterStudio(
        [FromBody] CharacterStudioCreateRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.CreateAsync(
                request ?? new CharacterStudioCreateRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpGet("character-studio/characters/{characterId}")]
    [HttpGet("character-studio/characters/{characterId}/status")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> GetCharacterStudio(
        string characterId,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _characterStudio.GetAsync(characterId, provider, eraId, cancellationToken)); }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpGet("character-studio/characters/{characterId}/references")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> GetCharacterStudioReferences(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try { return Ok(await _characterStudio.GetAsync(characterId, null, eraId, cancellationToken)); }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpGet("character-studio/characters/{characterId}/references/{type}/image")]
    public async Task<IActionResult> CharacterStudioReferenceImage(
        string characterId, string type, [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var hit = await _characterStudio.ReadReferenceAsync(characterId, type, eraId, cancellationToken);
        if (hit is null) return NotFound();
        return File(hit.Value.Bytes, hit.Value.Mime);
    }

    [HttpGet("character-studio/characters/{characterId}/master/image")]
    public async Task<IActionResult> CharacterStudioMasterImage(
        string characterId, [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var hit = await _characterStudio.ReadMasterAsync(characterId, eraId, cancellationToken);
        if (hit is null) return NotFound();
        return File(hit.Value.Bytes, hit.Value.Mime);
    }

    [HttpGet("character-studio/characters/{characterId}/master/revision/previous/image")]
    public async Task<IActionResult> CharacterStudioMasterRevisionPreviousImage(
        string characterId, [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var hit = await _characterStudio.ReadPreviousMasterAsync(characterId, eraId, cancellationToken);
        if (hit is null) return NotFound();
        return File(hit.Value.Bytes, hit.Value.Mime);
    }

    [HttpGet("character-studio/characters/{characterId}/master/revision/candidate/image")]
    public async Task<IActionResult> CharacterStudioMasterRevisionCandidateImage(
        string characterId, [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var hit = await _characterStudio.ReadCandidateMasterAsync(characterId, eraId, cancellationToken);
        if (hit is null) return NotFound();
        return File(hit.Value.Bytes, hit.Value.Mime);
    }

    [HttpPost("character-studio/{characterId}/master/revision")]
    [HttpPost("character-studio/characters/{characterId}/master/revision")]
    public async Task<ActionResult<CharacterMasterRevisionDto>> RequestCharacterStudioMasterRevision(
        string characterId,
        [FromBody] CharacterMasterRevisionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.RequestMasterRevisionAsync(
                characterId, request ?? new CharacterMasterRevisionRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/{characterId}/master/revision/approve")]
    [HttpPost("character-studio/characters/{characterId}/master/revision/approve")]
    public async Task<ActionResult<CharacterMasterRevisionDto>> ApproveCharacterStudioMasterRevision(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        [FromBody] CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.ApproveMasterRevisionAsync(
                characterId, User.Identity?.Name ?? "director",
                (review ?? new CharacterMasterRevisionReviewRequestDto()) with
                {
                    EraId = string.IsNullOrWhiteSpace(review?.EraId) ? eraId : review.EraId,
                },
                cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/{characterId}/master/revision/reject")]
    [HttpPost("character-studio/characters/{characterId}/master/revision/reject")]
    public async Task<ActionResult<CharacterMasterRevisionDto>> RejectCharacterStudioMasterRevision(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        [FromBody] CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.RejectMasterRevisionAsync(
                characterId, User.Identity?.Name ?? "director",
                (review ?? new CharacterMasterRevisionReviewRequestDto()) with
                {
                    EraId = string.IsNullOrWhiteSpace(review?.EraId) ? eraId : review.EraId,
                },
                cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/{characterId}/master/revision/lock")]
    [HttpPost("character-studio/characters/{characterId}/master/revision/lock")]
    public async Task<ActionResult<CharacterMasterRevisionDto>> LockCharacterStudioMasterRevision(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        [FromBody] CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.LockMasterRevisionAsync(
                characterId, User.Identity?.Name ?? "director",
                (review ?? new CharacterMasterRevisionReviewRequestDto()) with
                {
                    EraId = string.IsNullOrWhiteSpace(review?.EraId) ? eraId : review.EraId,
                },
                cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpGet("character-studio/unified-generation/regression")]
    public ActionResult<object> CharacterStudioUnifiedGenerationRegression()
    {
        var fail = _characterStudioGeneration.RunRegression();
        return Ok(new
        {
            suite = CharacterStudioUnifiedGenerationV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/age-consistency/regression")]
    public ActionResult<object> CharacterAgeConsistencyRegression()
    {
        var fail = _characterStudioGeneration.RunAgeConsistencyRegression();
        return Ok(new
        {
            suite = CharacterAgeConsistencyV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/age-gate/regression")]
    public ActionResult<object> CharacterAgeGateRegression()
    {
        var fail = _characterStudioGeneration.RunAgeGateRegression();
        return Ok(new
        {
            suite = CharacterAgeGateV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/master-revision/regression")]
    public ActionResult<object> CharacterMasterRevisionRegression()
    {
        var fail = _characterStudioGeneration.RunMasterRevisionRegression();
        return Ok(new
        {
            suite = CharacterMasterRevisionV1Rules.SuiteId,
            directorReviewSuite = CharacterMasterRevisionDirectorReviewV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            tests = fail.Count == 0 ? Array.Empty<string>() : fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/master-revision/director-review/regression")]
    public ActionResult<object> CharacterMasterRevisionDirectorReviewRegression()
    {
        var fail = _characterStudioGeneration.RunMasterRevisionDirectorReviewRegression();
        return Ok(new
        {
            suite = CharacterMasterRevisionDirectorReviewV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            tests = fail.Count == 0 ? Array.Empty<string>() : fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/character-design-language")]
    public ActionResult<CharacterDesignLanguageDefinitionDto> GetCharacterDesignLanguage() =>
        Ok(CharacterDesignLanguageV1Rules.ToDefinitionDto());

    [HttpPost("character-studio/character-design-language/compile-preview")]
    public ActionResult<CharacterDesignLanguageCompileDto> CompileCharacterDesignLanguagePreview(
        [FromBody] CharacterDesignLanguageCompilePreviewRequestDto? request)
    {
        var age = request?.ChronologicalAge > 0 ? request.ChronologicalAge : 11;
        var compiled = CharacterDesignLanguageV1Rules.Compile(age, request?.Gender, request?.AgeAppearanceProfile);
        return Ok(CharacterDesignLanguageV1Rules.ToCompileDto(compiled));
    }

    [HttpGet("character-studio/character-design-language/regression")]
    public ActionResult<object> CharacterDesignLanguageRegression()
    {
        var fail = _characterStudioGeneration.RunCharacterDesignLanguageRegression();
        return Ok(new
        {
            suite = CharacterDesignLanguageV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            tests = fail.Count == 0 ? Array.Empty<string>() : fail,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            generationExecuted = false,
            autoApprove = false,
            autoLock = false,
            persisted = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/character-design-language/v2")]
    public ActionResult<CharacterDesignLanguageV2DefinitionDto> GetCharacterDesignLanguageV2() =>
        Ok(CharacterDesignLanguageV2Rules.ToDefinitionDto());

    [HttpGet("character-studio/character-design-language/v2/regression")]
    public ActionResult<object> CharacterDesignLanguageV2RegressionEndpoint()
    {
        var fail = _characterStudioGeneration.RunCharacterDesignLanguageV2Regression();
        return Ok(new
        {
            suite = CharacterDesignLanguageV2Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            tests = fail.Count == 0 ? Array.Empty<string>() : fail,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            generationExecuted = false,
            autoApprove = false,
            autoLock = false,
            persisted = false,
            production = false,
            video = false,
            sha256 = CharacterDesignLanguageV2Rules.Sha(),
            currentAuthority = false,
        });
    }

    [HttpGet("character-studio/project-visual-universe")]
    public async Task<ActionResult<VisualUniverseAuthorityDto>> GetVisualUniverse(
        [FromQuery] string? projectId = null, CancellationToken cancellationToken = default) =>
        Ok(await _visualUniverse.GetAsync(projectId ?? "FAMIXA", cancellationToken));

    [HttpPost("character-studio/project-visual-universe/revision")]
    public async Task<ActionResult<VisualUniverseAuthorityDto>> RequestVisualUniverseRevision(
        [FromBody] VisualUniverseAuthorityRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualUniverse.RequestRevisionAsync(
                request?.ProjectId ?? "FAMIXA", request ?? new VisualUniverseAuthorityRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("character-studio/project-visual-universe/calibration")]
    public async Task<ActionResult<VisualUniverseCalibrationDto>> GetVisualUniverseCalibration(
        [FromQuery] string? projectId = null, CancellationToken cancellationToken = default) =>
        Ok(await _visualUniverse.GetCalibrationAsync(projectId ?? "FAMIXA", cancellationToken));

    [HttpPost("character-studio/project-visual-universe/calibration")]
    public async Task<ActionResult<VisualUniverseCalibrationDto>> RequestVisualUniverseCalibration(
        [FromBody] VisualUniverseAuthorityRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualUniverse.RequestCalibrationAsync(
                request?.ProjectId ?? "FAMIXA", request ?? new VisualUniverseAuthorityRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("character-studio/project-visual-universe/approve")]
    public async Task<ActionResult<VisualUniverseAuthorityDto>> ApproveVisualUniverse(
        [FromBody] VisualUniverseAuthorityRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualUniverse.ApproveAsync(
                request?.ProjectId ?? "FAMIXA", request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("character-studio/project-visual-universe/reject")]
    public async Task<ActionResult<VisualUniverseAuthorityDto>> RejectVisualUniverse(
        [FromBody] VisualUniverseAuthorityRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualUniverse.RejectAsync(
                request?.ProjectId ?? "FAMIXA", request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("character-studio/project-visual-universe/lock")]
    public async Task<ActionResult<VisualUniverseAuthorityDto>> LockVisualUniverse(
        [FromBody] VisualUniverseAuthorityRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualUniverse.LockAsync(
                request?.ProjectId ?? "FAMIXA", request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("character-studio/project-visual-universe/regression")]
    public ActionResult<object> VisualUniverseAuthorityRegression()
    {
        var fail = _characterStudioGeneration.RunVisualUniverseAuthorityRegression();
        return Ok(new
        {
            suite = FamixaVisualUniverseAuthorityV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            autoApprove = false,
            autoLock = false,
            sha256 = FamixaVisualUniverseAuthorityV1Rules.Sha(),
            currentAuthority = false,
        });
    }

    [HttpGet("character-studio/visual-universe-snapshot/regression")]
    public ActionResult<object> VisualUniverseSnapshotRegression()
    {
        var fail = VisualUniverseSnapshotResolverV1Regression.Run();
        return Ok(new
        {
            suite = VisualUniverseSnapshotResolverV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("character-studio/unified-visual-contract/regression")]
    public ActionResult<object> UnifiedVisualContractRegression()
    {
        var fail = UnifiedVisualContractV1Regression.Run();
        return Ok(new
        {
            suite = UnifiedVisualContractV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("character-studio/unified-visual-compiler/regression")]
    public ActionResult<object> UnifiedVisualCompilerRegression()
    {
        var fail = UnifiedVisualCompilerV1Regression.Run();
        return Ok(new
        {
            suite = UnifiedVisualCompilerV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("project-visual-mode")]
    [HttpGet("project-visual-mode/{projectId}")]
    public ActionResult<ProjectVisualModeContract> GetProjectVisualMode(string? projectId = null)
    {
        _ = projectId;
        return Ok(ProjectVisualModeAuthorityV1Rules.FamixaCurrent());
    }

    [HttpGet("project-visual-mode/regression")]
    public ActionResult<object> ProjectVisualModeRegression()
    {
        var fail = ProjectVisualModeAuthorityV1Regression.Run();
        return Ok(new
        {
            suite = ProjectVisualModeAuthorityV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            autoApprove = false,
            autoLock = false,
        });
    }

    [HttpGet("series-still-visual-ingress/regression")]
    public ActionResult<object> SeriesStillVisualIngressRegression()
    {
        var fail = SeriesStillVisualIngressV1Regression.Run();
        return Ok(new
        {
            suite = SeriesStillVisualIngressV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("identity-conditioned-calibration")]
    public ActionResult<IdentityConditionedCalibrationReport> IdentityConditionedCalibration() =>
        Ok(IdentityConditionedCalibrationV1Rules.Audit());

    [HttpGet("identity-conditioned-calibration/live")]
    public ActionResult<IdentityConditionedLiveReadiness> IdentityConditionedCalibrationLive() =>
        Ok(IdentityConditionedCalibrationLiveV1Rules.Readiness());

    [HttpGet("identity-conditioned-calibration/live/regression")]
    public ActionResult<object> IdentityConditionedCalibrationLiveRegression()
    {
        var fail = IdentityConditionedCalibrationLiveV1Regression.Run();
        return Ok(new
        {
            suite = IdentityConditionedCalibrationLiveV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            liveStatus = IdentityConditionedCalibrationLiveV1Rules.ReadyForDirectorConfirmation,
        });
    }

    [HttpGet("identity-conditioned-calibration/regression")]
    public ActionResult<object> IdentityConditionedCalibrationRegression()
    {
        var fail = IdentityConditionedCalibrationV1Regression.Run();
        return Ok(new
        {
            suite = IdentityConditionedCalibrationV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("identity-conditioned-calibration/director-review")]
    public async Task<ActionResult<IdentityConditionedCalibrationPackReview>> IdentityConditionedCalibrationDirectorReview(
        CancellationToken cancellationToken) =>
        Ok(await _identityDirectorReview.GetAsync(null, cancellationToken));

    [HttpGet("identity-conditioned-calibration/director-review/ui/regression")]
    public ActionResult<object> IdentityConditionedCalibrationDirectorReviewUiRegression()
    {
        var fail = IdentityConditionedCalibrationDirectorReviewUiV1Regression.Run();
        return Ok(new
        {
            suite = IdentityConditionedCalibrationDirectorReviewUiV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            visualPass = false,
        });
    }

    [HttpGet("identity-conditioned-calibration/director-review/regression")]
    public ActionResult<object> IdentityConditionedCalibrationDirectorReviewRegression()
    {
        var fail = _identityDirectorReview.RunRegression();
        return Ok(new
        {
            suite = IdentityConditionedCalibrationDirectorReviewV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            visualPass = false,
        });
    }

    [HttpGet("identity-conditioned-calibration/director-review/{runId}")]
    public async Task<ActionResult<IdentityConditionedCalibrationPackReview>> IdentityConditionedCalibrationDirectorReviewByRun(
        string runId, CancellationToken cancellationToken) =>
        Ok(await _identityDirectorReview.GetAsync(runId, cancellationToken));

    [HttpGet("identity-conditioned-calibration/director-review/{runId}/subject/{subjectId}")]
    public async Task<ActionResult<IdentityConditionedCalibrationSubjectReview>> IdentityConditionedCalibrationDirectorReviewSubject(
        string runId, string subjectId, CancellationToken cancellationToken)
    {
        var row = await _identityDirectorReview.GetSubjectAsync(runId, subjectId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("identity-conditioned-calibration/director-review/{runId}/subject/{subjectId}")]
    public async Task<ActionResult<IdentityConditionedCalibrationPackReview>> SaveIdentityConditionedCalibrationDirectorReview(
        string runId,
        string subjectId,
        [FromBody] IdentityConditionedCalibrationDirectorReviewSaveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _identityDirectorReview.SaveSubjectAsync(
            runId, subjectId, request, User.Identity?.Name ?? "director", cancellationToken));

    [HttpPost("identity-conditioned-calibration/director-review/{runId}/visual-pass")]
    public async Task<ActionResult<IdentityConditionedCalibrationPackReview>> MarkIdentityConditionedCalibrationVisualPass(
        string runId,
        [FromBody] VisualCalibrationRequestDto? request,
        CancellationToken cancellationToken) =>
        Ok(await _identityDirectorReview.MarkVisualPassAsync(
            runId, request?.Confirm == true, User.Identity?.Name ?? "director", cancellationToken));

    [HttpGet("visual-foundation/finalization")]
    public ActionResult<VisualFoundationFinalizationReport> VisualFoundationFinalization() =>
        Ok(VisualFoundationFinalizationV1Rules.Audit());

    [HttpGet("visual-foundation/finalization/regression")]
    public ActionResult<object> VisualFoundationFinalizationRegression()
    {
        var fail = VisualFoundationFinalizationV1Regression.Run();
        return Ok(new
        {
            suite = VisualFoundationFinalizationV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("visual-universe/lock-readiness")]
    public async Task<ActionResult<VisualUniverseLockReadinessReport>> VisualUniverseLockReadiness(
        CancellationToken cancellationToken)
    {
        var live = await _visualUniverse.GetAsync("FAMIXA", cancellationToken);
        var report = VisualUniverseLockReadinessV1Rules.Audit(live);
        return Ok(report);
    }

    [HttpGet("visual-universe/lock-readiness/regression")]
    public ActionResult<object> VisualUniverseLockReadinessRegression()
    {
        var fail = VisualUniverseLockReadinessV1Regression.Run();
        return Ok(new
        {
            suite = VisualUniverseLockReadinessV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("video-audio-lipsync-pipeline/regression")]
    public ActionResult<object> VideoAudioLipsyncPipelineRegression()
    {
        var fail = VideoAudioLipsyncPipelineV1Regression.Run();
        return Ok(new
        {
            suite = VideoAudioLipsyncPipelineV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
            elevenLabsCalled = false,
            falCalled = false,
            runwayCalled = false,
        });
    }

    [HttpGet("video-visual-ingress/regression")]
    public ActionResult<object> VideoVisualIngressRegression()
    {
        var fail = VideoVisualIngressV1Regression.Run();
        return Ok(new
        {
            suite = VideoVisualIngressV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("character-studio/first-master-visual-ingress/regression")]
    public ActionResult<object> FirstMasterVisualIngressRegression()
    {
        var fail = _characterStudioGeneration.RunFirstMasterVisualIngressRegression();
        return Ok(new
        {
            suite = CharacterFirstMasterVisualIngressV1Regression.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            generate = false,
            geminiCalled = false,
            providerCalled = false,
        });
    }

    [HttpGet("visual-calibration/regression")]
    public ActionResult<object> VisualCalibrationRegression()
    {
        var fail = _visualCalibration.RunRegression();
        return Ok(new
        {
            suite = VisualCalibrationPackV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            providerCalled = false,
            generationExecuted = false,
            geminiCalled = false,
            autoApprove = false,
            autoLock = false,
        });
    }

    [HttpGet("visual-calibration/hardening/regression")]
    public ActionResult<object> VisualCalibrationHardeningRegression()
    {
        var fail = _visualCalibration.RunHardeningRegression();
        return Ok(new
        {
            suite = VisualCalibrationPackV1Rules.HardeningSuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            providerCalled = false,
            generationExecuted = false,
            geminiCalled = false,
        });
    }

    [HttpGet("visual-calibration/live-generation-readiness/regression")]
    public ActionResult<object> VisualCalibrationLiveGenerationReadinessRegression()
    {
        var fail = _visualCalibration.RunLiveGenerationReadinessRegression();
        return Ok(new
        {
            suite = VisualCalibrationPackV1Rules.LiveGenerationSuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            providerCalled = false,
            generationExecuted = false,
            geminiCalled = false,
        });
    }

    [HttpGet("visual-calibration/live-generation/regression")]
    public ActionResult<object> VisualCalibrationLiveGenerationRegression()
    {
        var fail = _visualCalibration.RunLiveGenerationRegression();
        return Ok(new
        {
            suite = VisualCalibrationPackV1Rules.LiveGenerationOfficialSuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            providerCalled = false,
            generationExecuted = false,
            geminiCalled = false,
            autoApprove = false,
            autoLock = false,
        });
    }

    [HttpPost("visual-calibration/packs")]
    public async Task<ActionResult<VisualCalibrationPackDto>> CreateVisualCalibrationPack(
        [FromBody] VisualCalibrationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualCalibration.CreatePackAsync(
                request ?? new VisualCalibrationRequestDto(), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("visual-calibration/{packId}/generate")]
    public async Task<ActionResult<VisualCalibrationPackDto>> GenerateVisualCalibration(
        string packId,
        [FromBody] VisualCalibrationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualCalibration.GenerateAsync(
                packId, request ?? new VisualCalibrationRequestDto(), User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("visual-calibration/{packId}")]
    public async Task<ActionResult<VisualCalibrationPackDto>> GetVisualCalibration(
        string packId, CancellationToken cancellationToken = default) =>
        Ok(await _visualCalibration.GetAsync(packId, cancellationToken));

    [HttpGet("visual-calibration/{packId}/artifacts")]
    public async Task<ActionResult<IReadOnlyList<VisualCalibrationArtifactDto>>> GetVisualCalibrationArtifacts(
        string packId, CancellationToken cancellationToken = default) =>
        Ok(await _visualCalibration.GetArtifactsAsync(packId, cancellationToken));

    [HttpGet("visual-calibration/{packId}/coverage")]
    public async Task<ActionResult<VisualCalibrationCoverageDto>> GetVisualCalibrationCoverage(
        string packId, CancellationToken cancellationToken = default) =>
        Ok(await _visualCalibration.GetCoverageAsync(packId, cancellationToken));

    [HttpGet("visual-calibration/{packId}/slots/{subjectId}/{viewType}/image")]
    public async Task<IActionResult> VisualCalibrationSlotImage(
        string packId, string subjectId, string viewType, CancellationToken cancellationToken = default)
    {
        var hit = await _visualCalibration.ReadSlotImageAsync(packId, subjectId, viewType, cancellationToken);
        if (hit is null) return NotFound();
        return File(hit.Value.Bytes, hit.Value.Mime);
    }

    [HttpPost("visual-calibration/{packId}/approve")]
    public async Task<ActionResult<VisualCalibrationPackDto>> ApproveVisualCalibration(
        string packId,
        [FromBody] VisualCalibrationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualCalibration.ApproveAsync(
                packId, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("visual-calibration/{packId}/reject")]
    public async Task<ActionResult<VisualCalibrationPackDto>> RejectVisualCalibration(
        string packId,
        [FromBody] VisualCalibrationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualCalibration.RejectAsync(
                packId, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("visual-calibration/{packId}/lock")]
    public async Task<ActionResult<VisualCalibrationPackDto>> LockVisualCalibration(
        string packId,
        [FromBody] VisualCalibrationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _visualCalibration.LockAsync(
                packId, request, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("character-studio/appearance-profile/regression")]
    public ActionResult<object> CharacterAppearanceProfileRegression()
    {
        var fail = _characterStudioGeneration.RunAppearanceProfileRegression();
        return Ok(new
        {
            suite = CharacterAppearanceProfileV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            pass = fail.Count == 0,
            tests = fail.Count == 0 ? Array.Empty<string>() : fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpGet("character-studio/age-generation-integration/regression")]
    public ActionResult<object> CharacterAgeGenerationIntegrationRegression()
    {
        var fail = _characterStudioGeneration.RunAgeGenerationIntegrationRegression();
        return Ok(new
        {
            suite = CharacterAgeGenerationIntegrationV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    [HttpPost("character-studio/{characterId}/age-consistency")]
    [HttpPost("character-studio/characters/{characterId}/age-consistency")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> ReviewCharacterAgeConsistency(
        string characterId,
        [FromBody] CharacterAgeConsistencyReviewRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.ReviewAgeConsistencyAsync(
                characterId,
                request ?? new CharacterAgeConsistencyReviewRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/{characterId}/appearance-consistency")]
    [HttpPost("character-studio/characters/{characterId}/appearance-consistency")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> ReviewCharacterAppearanceConsistency(
        string characterId,
        [FromBody] CharacterAppearanceConsistencyReviewRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.ReviewAppearanceConsistencyAsync(
                characterId,
                request ?? new CharacterAppearanceConsistencyReviewRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpGet("character-studio/{characterId}/age-consistency")]
    [HttpGet("character-studio/characters/{characterId}/age-consistency")]
    public async Task<ActionResult<CharacterAgeConsistencyDto>> GetCharacterAgeConsistency(
        string characterId,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.GetAgeConsistencyAsync(
                characterId, provider, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/{characterId}/reference-generation")]
    [HttpPost("character-studio/characters/{characterId}/reference-generation")]
    public async Task<ActionResult<CharacterStudioReferenceGenerationResultDto>> CharacterStudioReferenceGeneration(
        string characterId,
        [FromBody] CharacterStudioReferenceGenerationRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudioGeneration.GenerateCharacterReferenceSet(
                characterId,
                request ?? new CharacterStudioReferenceGenerationRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters/{characterId}/generate")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> GenerateCharacterStudio(
        string characterId,
        [FromBody] CharacterStudioActionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.GenerateAsync(
                characterId, request ?? new CharacterStudioActionRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters/{characterId}/score-identity")]
    [HttpPost("character-studio/{characterId}/score-identity")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> ScoreCharacterStudioIdentity(
        string characterId,
        [FromBody] CharacterStudioActionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.ScoreIdentityAsync(
                characterId, request ?? new CharacterStudioActionRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters/{characterId}/regenerate")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> RegenerateCharacterStudio(
        string characterId,
        [FromBody] CharacterStudioActionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.RegenerateAsync(
                characterId, request ?? new CharacterStudioActionRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters/{characterId}/approve")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> ApproveCharacterStudio(
        string characterId, [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.ApproveAsync(
                characterId, User.Identity?.Name ?? "director", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters/{characterId}/reject")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> RejectCharacterStudio(
        string characterId,
        [FromBody] CharacterStudioActionRequestDto? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.RejectAsync(
                characterId, request ?? new CharacterStudioActionRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpPost("character-studio/characters/{characterId}/lock")]
    public async Task<ActionResult<CharacterStudioCharacterDto>> LockCharacterStudio(
        string characterId, [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterStudio.LockAsync(
                characterId, User.Identity?.Name ?? "director", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex) { return CharacterReferenceGenerationError(ex); }
    }

    [HttpGet("video-engine/character-authority-pipeline-v1/regression")]
    public ActionResult<object> CharacterAuthorityPipelineV1Regression()
    {
        var fail = _authorityPipeline.RunRegression();
        return Ok(new
        {
            suite = CharacterAuthorityPipelineV1Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoApprove = false,
            autoLock = false,
            production = false,
            video = false,
        });
    }

    private async Task<ActionResult<CharacterAuthorityInitializationDto>> AuthorityAction(
        string characterId,
        CharacterAuthorityInitializationRequestDto? request,
        Func<string, CharacterAuthorityInitializationRequestDto, string, CancellationToken, Task<CharacterAuthorityInitializationDto>> run,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await run(characterId, request ?? new CharacterAuthorityInitializationRequestDto(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    private async Task<ActionResult<CharacterAuthorityInitializationDto>> AuthorityMutate(
        string characterId,
        string eraId,
        Func<string, string, string, CancellationToken, Task<CharacterAuthorityInitializationDto>> run,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await run(characterId, User.Identity?.Name ?? "director", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/characters/{characterId}/reference-generation")]
    [HttpGet("video-engine/character-reference-generation")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> GetCharacterReferenceGeneration(
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromQuery] string? referenceType = null,
        [FromQuery] string? provider = null,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceGeneration.GetAsync(
                characterId ?? characterIdQuery ?? "", referenceType, provider, eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-generation/prepare")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> PrepareCharacterReferenceGeneration(
        string characterId,
        [FromBody] CharacterReferenceGenerationRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceGeneration.PrepareAsync(
                characterId, request ?? new CharacterReferenceGenerationRequest(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-generation/execute")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> ExecuteCharacterReferenceGeneration(
        string characterId,
        [FromBody] CharacterReferenceGenerationRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceGeneration.ExecuteAsync(
                characterId, request ?? new CharacterReferenceGenerationRequest(),
                User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/characters/{characterId}/reference-generation/{executionId:guid}")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> GetCharacterReferenceGenerationExecution(
        string characterId, Guid executionId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceGeneration.GetExecutionAsync(characterId, executionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/characters/{characterId}/reference-generation/{executionId:guid}/image")]
    public async Task<IActionResult> CharacterReferenceGenerationImage(
        string characterId, Guid executionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var file = await _characterReferenceGeneration.ReadCandidateImageAsync(characterId, executionId, cancellationToken);
            if (file is null) return NotFound();
            return File(file.Value.Bytes, file.Value.Mime);
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-generation/{executionId:guid}/accept")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> AcceptCharacterReferenceGeneration(
        string characterId, Guid executionId, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceGeneration.AcceptAsync(
                characterId, executionId, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-generation/{executionId:guid}/reject")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> RejectCharacterReferenceGeneration(
        string characterId, Guid executionId,
        [FromBody] CharacterReferenceGenerationRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _characterReferenceGeneration.RejectAsync(
                characterId, executionId, User.Identity?.Name ?? "director", request?.Reason, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-pack/register")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> RegisterCharacterReferenceGeneration(
        string characterId,
        [FromBody] CharacterReferenceGenerationRequest? request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (request?.ExecutionId is not Guid executionId || executionId == Guid.Empty)
                return BadRequest(new { message = "Thiếu lần tạo ảnh.", generate = false });
            return Ok(await _characterReferenceGeneration.RegisterAsync(
                characterId, executionId, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpPost("video-engine/characters/{characterId}/reference-pack/validate")]
    public async Task<ActionResult<CharacterReferenceGenerationDto>> ValidateCharacterReferenceGenerationPack(
        string characterId,
        [FromBody] CharacterReferenceGenerationRequest? request,
        [FromQuery] Guid? packId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var id = request?.PackId ?? packId;
            if (id is null || id == Guid.Empty)
                return BadRequest(new { message = "Thiếu bộ ảnh chuẩn.", generate = false });
            return Ok(await _characterReferenceGeneration.ValidatePackAsync(
                characterId, id.Value, User.Identity?.Name ?? "director", request?.ExecutionId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferenceGenerationError(ex);
        }
    }

    [HttpGet("video-engine/character-reference-generation/regression")]
    public ActionResult<object> CharacterReferenceGenerationRegression()
    {
        var failures = _characterReferenceGeneration.RunRegression();
        return Ok(new
        {
            suite = CharacterReferenceGenerationRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = failures.Count,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    private ActionResult CharacterReferenceGenerationError(InvalidOperationException ex)
    {
        var staff = ex.Message.StartsWith("REFERENCE_GENERATION_BLOCKED:", StringComparison.Ordinal)
            ? ex.Message["REFERENCE_GENERATION_BLOCKED:".Length..].Trim()
            : ex.Message;
        return Conflict(new
        {
            message = staff,
            detail = ex.Message,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    private async Task<ActionResult<CharacterReferencePackDto>> CharacterReferencePackAction(
        string? characterId, Guid packId, string action, string? note, CancellationToken cancellationToken)
    {
        try
        {
            var actor = User.Identity?.Name ?? "director";
            var id = characterId ?? "";
            return Ok(action switch
            {
                "validate" => await _characterReferencePack.ValidateAsync(id, packId, actor, cancellationToken),
                "approve" => await _characterReferencePack.ApproveAsync(id, packId, actor, note, cancellationToken),
                "reject" => await _characterReferencePack.RejectAsync(id, packId, actor, note, cancellationToken),
                "lock" => await _characterReferencePack.LockAsync(id, packId, actor, cancellationToken),
                "supersede" => await _characterReferencePack.SupersedeAsync(id, packId, actor, cancellationToken),
                _ => throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: unknown action."),
            });
        }
        catch (InvalidOperationException ex)
        {
            return CharacterReferencePackError(ex);
        }
    }

    private ActionResult CharacterReferencePackError(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("REFERENCE_PACK_IDENTITY_CONFLICT", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_PACK_DNA_CONFLICT", StringComparison.Ordinal))
            return BadRequest(new { message = "REFERENCE_PACK_IDENTITY_CONFLICT", detail = ex.Message, generate = false });
        if (ex.Message.StartsWith("REFERENCE_ASSET_MISSING", StringComparison.Ordinal))
            return BadRequest(new { message = "REFERENCE_ASSET_MISSING", detail = ex.Message, generate = false });
        if (ex.Message.StartsWith("REFERENCE_PACK_NOT_READY", StringComparison.Ordinal))
            return BadRequest(new { message = "REFERENCE_PACK_NOT_READY", detail = ex.Message, generate = false });
        if (ex.Message.StartsWith("REFERENCE_PACK_INCOMPLETE", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_MASTER_MISMATCH", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_DNA_MISMATCH", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_PRP_MISMATCH", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_IDENTITY_MISMATCH", StringComparison.Ordinal)
            || ex.Message.StartsWith("REFERENCE_ASSET_INVALID", StringComparison.Ordinal)
            || ex.Message.StartsWith("DIRECTOR_APPROVAL_REQUIRED", StringComparison.Ordinal)
            || ex.Message.StartsWith("INVALID_REFERENCE_PACK_STATE", StringComparison.Ordinal))
            return BadRequest(new { message = ex.Message.Split(':')[0], detail = ex.Message, generate = false });
        if (ex.Message.StartsWith("REFERENCE_ALREADY_LOCKED", StringComparison.Ordinal)
            || ex.Message.StartsWith("CRP_LOCKED", StringComparison.Ordinal))
            return Conflict(new { message = ex.Message.Split(':')[0], detail = ex.Message, generate = false });
        if (ex.Message.StartsWith("CRP_GATE_NOT_SATISFIED", StringComparison.Ordinal)
            || ex.Message.StartsWith("CRP_INVALID", StringComparison.Ordinal))
            return BadRequest(new { message = "CRP_GATE_NOT_SATISFIED", detail = ex.Message });
        if (ex.Message.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = ex.Message });
        return Conflict(new { message = ex.Message });
    }

    [HttpGet("video-engine/production-reference-pack")]
    public async Task<ActionResult<KitVideoProductionReferencePackGetDto>> GetProductionReferencePack(
        [FromQuery] string? characterId = "CHAR-001",
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.GetAsync(characterId ?? "CHAR-001", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpGet("/api/characters/{characterId}/production-reference-pack")]
    public Task<ActionResult<KitVideoProductionReferencePackGetDto>> GetProductionReferencePackAlias(
        string characterId,
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        GetProductionReferencePack(characterId, eraId, cancellationToken);

    [HttpGet("video-engine/production-reference-pack/version/{packVersion}")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> GetProductionReferencePackVersion(
        [FromQuery] string? characterId,
        string packVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionPack.GetVersionAsync(characterId ?? "CHAR-001", packVersion, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpGet("/api/characters/{characterId}/production-reference-pack/{packVersion}")]
    public Task<ActionResult<KitVideoProductionReferencePackDto>> GetProductionReferencePackVersionAlias(
        string characterId,
        string packVersion,
        CancellationToken cancellationToken) =>
        GetProductionReferencePackVersion(characterId, packVersion, cancellationToken);

    [HttpPost("video-engine/production-reference-pack")]
    [HttpPost("/api/characters/{characterId}/production-reference-pack")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> CreateProductionReferencePack(
        string? characterId = "CHAR-001",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.CreateAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpPost("video-engine/production-reference-pack/approve")]
    [HttpPost("/api/characters/{characterId}/production-reference-pack/approve")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> ApproveProductionReferencePack(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoProductionReferencePackNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.ApproveAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpPost("video-engine/production-reference-pack/reject")]
    [HttpPost("/api/characters/{characterId}/production-reference-pack/reject")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> RejectProductionReferencePack(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoProductionReferencePackNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.RejectAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpPost("video-engine/production-reference-pack/analyze")]
    [HttpPost("/api/characters/{characterId}/production-reference-pack/analyze")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> AnalyzeProductionReferencePack(
        string? characterId = "CHAR-001",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.AnalyzeAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpPost("video-engine/production-reference-pack/edit")]
    [HttpPost("/api/characters/{characterId}/production-reference-pack/edit")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> EditProductionReferencePack(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoProductionReferencePackEditRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.EditAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request ?? new KitVideoProductionReferencePackEditRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpPost("video-engine/production-reference-pack/return")]
    [HttpPost("/api/characters/{characterId}/production-reference-pack/return")]
    public async Task<ActionResult<KitVideoProductionReferencePackDto>> ReturnProductionReferencePack(
        string? characterId = "CHAR-001",
        [FromBody] KitVideoProductionReferencePackNoteRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionPack.ReturnToEditAsync(characterId ?? "CHAR-001", User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    [HttpPut("video-engine/production-reference-pack/{id:guid}")]
    [HttpDelete("video-engine/production-reference-pack/{id:guid}")]
    public async Task<ActionResult> RejectProductionReferencePackMutation(Guid id, CancellationToken cancellationToken)
    {
        _ = id;
        try
        {
            await _productionPack.RejectMutationAsync("CHAR-001", Request.Method == "DELETE" ? "DELETE" : "UPDATE", cancellationToken);
            return Conflict(new { message = "PRP_LOCKED" });
        }
        catch (InvalidOperationException ex)
        {
            return ProductionPackError(ex);
        }
    }

    private ActionResult ProductionPackError(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("PRP_GATE_NOT_SATISFIED", StringComparison.Ordinal)
            || ex.Message.StartsWith("PRP_INVALID", StringComparison.Ordinal))
            return BadRequest(new { message = ex.Message.StartsWith("PRP_INVALID", StringComparison.Ordinal) ? "PRP_INVALID" : "PRP_GATE_NOT_SATISFIED", detail = ex.Message });
        if (ex.Message.StartsWith("PRP_LOCKED", StringComparison.Ordinal))
            return Conflict(new { message = "PRP_LOCKED", detail = ex.Message });
        if (ex.Message.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = ex.Message });
        return Conflict(new { message = ex.Message });
    }

    [HttpGet("video-engine/production-shots")]
    [HttpGet("/api/characters/{characterId}/production-shots")]
    public async Task<ActionResult<KitVideoProductionShotGetDto>> GetProductionShots(
        string? characterId = "CHAR-001",
        [FromQuery] string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionShot.GetAsync(characterId ?? "CHAR-001", eraId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpGet("video-engine/production-shots/{id:guid}")]
    public async Task<ActionResult<KitVideoProductionShotDto>> GetProductionShot(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.GetOneAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots")]
    [HttpPost("/api/characters/{characterId}/production-shots")]
    public async Task<ActionResult<KitVideoProductionShotDto>> CreateProductionShot(
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await _productionShot.CreateAsync(characterId ?? characterIdQuery ?? "", User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/analyze")]
    public async Task<ActionResult<KitVideoProductionShotDto>> AnalyzeProductionShot(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.AnalyzeAsync(id, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/identity-check")]
    public async Task<ActionResult<KitVideoProductionShotDto>> IdentityCheckProductionShot(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.IdentityCheckAsync(id, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/edit")]
    public async Task<ActionResult<KitVideoProductionShotDto>> EditProductionShot(
        Guid id,
        [FromBody] KitVideoProductionShotEditRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.EditAsync(id, User.Identity?.Name ?? "director", request ?? new KitVideoProductionShotEditRequest(), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/approve")]
    public async Task<ActionResult<KitVideoProductionShotDto>> ApproveProductionShot(
        Guid id,
        [FromBody] KitVideoProductionShotNoteRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.ApproveAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/reject")]
    public async Task<ActionResult<KitVideoProductionShotDto>> RejectProductionShot(
        Guid id,
        [FromBody] KitVideoProductionShotNoteRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.RejectAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/return")]
    public async Task<ActionResult<KitVideoProductionShotDto>> ReturnProductionShot(
        Guid id,
        [FromBody] KitVideoProductionShotNoteRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _productionShot.ReturnToEditAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpPut("video-engine/production-shots/{id:guid}")]
    [HttpDelete("video-engine/production-shots/{id:guid}")]
    public async Task<ActionResult> RejectProductionShotMutation(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _productionShot.RejectMutationAsync(id, Request.Method == "DELETE" ? "DELETE" : "UPDATE", cancellationToken);
            return Conflict(new { message = "SHOT_LOCKED" });
        }
        catch (InvalidOperationException ex)
        {
            return ProductionShotError(ex);
        }
    }

    [HttpGet("video-engine/identity-governance")]
    [HttpGet("/api/characters/{characterId}/identity-governance")]
    public async Task<ActionResult<CharacterIdentityGovernanceDto>> GetIdentityGovernance(
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _identityGovernance.GetAsync(characterId ?? characterIdQuery ?? "", "ERA-01", cancellationToken));

    [HttpPost("video-engine/identity-governance/check")]
    [HttpPost("/api/characters/{characterId}/identity-governance/check")]
    public async Task<ActionResult<CharacterIdentityGovernanceDto>> CheckIdentityGovernance(
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        [FromBody] CharacterIdentityGovernanceCheckRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var row = await _identityGovernance.CheckAsync(
            characterId ?? characterIdQuery ?? "",
            request ?? new CharacterIdentityGovernanceCheckRequest(),
            User.Identity?.Name ?? "director",
            cancellationToken);
        return Ok(row);
    }

    [HttpGet("video-engine/identity-governance/audit")]
    [HttpGet("/api/characters/{characterId}/identity-governance/audit")]
    public async Task<ActionResult<IReadOnlyList<CharacterIdentityGovernanceAuditDto>>> ListIdentityGovernanceAudit(
        [FromRoute] string? characterId = null,
        [FromQuery(Name = "characterId")] string? characterIdQuery = null,
        CancellationToken cancellationToken = default) =>
        Ok(await _identityGovernance.ListAuditAsync(characterId ?? characterIdQuery ?? "", "ERA-01", cancellationToken));

    [HttpPost("video-engine/identity-gate")]
    [HttpPost("/api/production/identity-gate")]
    public async Task<ActionResult<CharacterIdentityGovernanceDto>> ProductionIdentityGate(
        [FromQuery] string? characterId,
        [FromBody] CharacterIdentityGovernanceCheckRequest? request,
        CancellationToken cancellationToken)
    {
        var row = await _identityGovernance.ProductionGateAsync(
            characterId ?? "", request, User.Identity?.Name ?? "director", cancellationToken);
        if (!row.ProductionAllowed)
            return BadRequest(row);
        return Ok(row);
    }

    [HttpGet("video-engine/identity-governance/regression")]
    public ActionResult<object> IdentityGovernanceRegression()
    {
        var failures = _identityGovernance.RunRegression();
        return Ok(new
        {
            suite = CharacterIdentityGovernanceV11Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
        });
    }

    [HttpGet("video-engine/production-shots/{id:guid}/contract")]
    [HttpGet("/api/production/shots/{id:guid}/contract")]
    public async Task<ActionResult<ProductionShotContractDto>> GetProductionShotContract(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _shotContract.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return ProductionShotError(ex); }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/contract")]
    [HttpPut("video-engine/production-shots/{id:guid}/contract")]
    [HttpPost("/api/production/shots/{id:guid}/contract")]
    [HttpPut("/api/production/shots/{id:guid}/contract")]
    public async Task<ActionResult<ProductionShotContractDto>> SaveProductionShotContract(
        Guid id, [FromBody] ProductionShotContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var payload = request?.Payload is { ValueKind: JsonValueKind.Object } p ? p : default;
            if (payload.ValueKind != JsonValueKind.Object)
                return BadRequest(new { message = "SHOT_CONTRACT_INVALID", detail = "payload required", generate = false });
            return Ok(await _shotContract.SaveAsync(id, payload, User.Identity?.Name ?? "director", cancellationToken));
        }
        catch (InvalidOperationException ex) { return ProductionShotError(ex); }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/contract/validate")]
    [HttpPost("/api/production/shots/{id:guid}/contract/validate")]
    public async Task<ActionResult<ProductionShotContractDto>> ValidateProductionShotContract(
        Guid id, [FromBody] ProductionShotContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _shotContract.ValidateAsync(id, request?.Payload, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return ProductionShotError(ex); }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/contract/approve")]
    [HttpPost("/api/production/shots/{id:guid}/contract/approve")]
    public async Task<ActionResult<ProductionShotContractDto>> ApproveProductionShotContract(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _shotContract.ApproveAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ProductionShotError(ex); }
    }

    [HttpPost("video-engine/production-shots/{id:guid}/contract/reject")]
    [HttpPost("/api/production/shots/{id:guid}/contract/reject")]
    public async Task<ActionResult<ProductionShotContractDto>> RejectProductionShotContract(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _shotContract.RejectAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ProductionShotError(ex); }
    }

    [HttpGet("video-engine/production-shot-contract/regression")]
    public ActionResult<object> ProductionShotContractRegression()
    {
        var failures = _shotContract.RunRegression();
        return Ok(new
        {
            suite = ProductionShotContractV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
        });
    }

    [HttpGet("video-engine/shots/{id:guid}/prompt")]
    [HttpGet("/api/production/shots/{id:guid}/prompt")]
    public async Task<ActionResult<ProductionPromptCompilerDto>> GetProductionPrompt(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _promptCompiler.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return ProductionPromptError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/prompt/compile")]
    [HttpPost("/api/production/shots/{id:guid}/prompt/compile")]
    public async Task<ActionResult<ProductionPromptCompilerDto>> CompileProductionPrompt(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _promptCompiler.CompileAsync(id, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return ProductionPromptError(ex); }
    }

    [HttpGet("video-engine/contract-prompt-v2-lifecycle/regression")]
    public ActionResult<object> ContractPromptV2LifecycleRegression()
    {
        var failures = ProductionContractPromptV2LifecycleRegression.Run();
        return Ok(new
        {
            suite = ProductionContractPromptV2LifecycleRegression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
        });
    }

    [HttpGet("video-engine/shots/{id:guid}/prompt/regression")]
    [HttpGet("video-engine/production-prompt-compiler/regression")]
    public ActionResult<object> ProductionPromptCompilerRegression()
    {
        var failures = _promptCompiler.RunRegression();
        return Ok(new
        {
            suite = ProductionPromptCompilerV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
        });
    }

    [HttpGet("video-engine/shots/{id:guid}/image-generation-contract")]
    [HttpGet("/api/production/shots/{id:guid}/image-generation-contract")]
    public async Task<ActionResult<ImageGenerationContractDto>> GetImageGenerationContract(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationContract.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-contract")]
    [HttpPut("video-engine/shots/{id:guid}/image-generation-contract")]
    [HttpPost("/api/production/shots/{id:guid}/image-generation-contract")]
    [HttpPut("/api/production/shots/{id:guid}/image-generation-contract")]
    public async Task<ActionResult<ImageGenerationContractDto>> SaveImageGenerationContract(
        Guid id, [FromBody] ImageGenerationContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationContract.SaveAsync(id, request, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-contract/validate")]
    [HttpPost("/api/production/shots/{id:guid}/image-generation-contract/validate")]
    public async Task<ActionResult<ImageGenerationContractDto>> ValidateImageGenerationContract(
        Guid id, [FromBody] ImageGenerationContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationContract.ValidateAsync(id, request, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-contract/approve")]
    [HttpPost("/api/production/shots/{id:guid}/image-generation-contract/approve")]
    public async Task<ActionResult<ImageGenerationContractDto>> ApproveImageGenerationContract(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationContract.ApproveAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-contract/reject")]
    [HttpPost("/api/production/shots/{id:guid}/image-generation-contract/reject")]
    public async Task<ActionResult<ImageGenerationContractDto>> RejectImageGenerationContract(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationContract.RejectAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationContractError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/image-generation-execution")]
    [HttpGet("/api/production/shots/{id:guid}/image-generation-execution")]
    public async Task<ActionResult<ImageGenerationExecutionDto>> GetImageGenerationExecution(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationExecution.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationExecutionError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/image-generation-execution/{executionId:guid}")]
    [HttpGet("/api/production/shots/{id:guid}/image-generation-execution/{executionId:guid}")]
    public async Task<ActionResult<ImageGenerationExecutionDto>> GetImageGenerationExecutionById(Guid id, Guid executionId, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationExecution.GetByIdAsync(id, executionId, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationExecutionError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/image-generation-execution/{executionId:guid}/image")]
    public async Task<IActionResult> GetImageGenerationExecutionArtifact(Guid id, Guid executionId, CancellationToken cancellationToken)
    {
        var file = await _imageGenerationExecution.ReadArtifactAsync(id, executionId, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime);
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-execution/preflight")]
    [HttpPost("/api/production/shots/{id:guid}/image-generation-execution/preflight")]
    public async Task<ActionResult<ImageGenerationExecutionDto>> PreflightImageGenerationExecution(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationExecution.PreflightAsync(id, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationExecutionError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-execution/execute")]
    [HttpPost("/api/production/shots/{id:guid}/image-generation-execution/execute")]
    public async Task<ActionResult<ImageGenerationExecutionDto>> ExecuteImageGenerationExecution(
        Guid id,
        [FromBody] FirstRealProductionExecuteRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _imageGenerationExecution.ExecuteAsync(
                id, User.Identity?.Name ?? "director", request?.Confirm ?? false, request?.Provider, cancellationToken));
        }
        catch (InvalidOperationException ex) { return ImageGenerationExecutionError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/first-real-production")]
    [HttpGet("video-engine/first-real-production")]
    public async Task<ActionResult<FirstRealProductionDto>> GetFirstRealProduction(
        Guid? id,
        [FromQuery] Guid? shotId,
        [FromQuery] string? provider,
        CancellationToken cancellationToken)
    {
        try
        {
            var target = id ?? shotId ?? Guid.Empty;
            if (target == Guid.Empty)
                return BadRequest(new { message = "Thiếu shotId.", generate = false });
            return Ok(await _firstRealProduction.GetAsync(target, provider, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, generate = false, geminiCalled = false });
        }
    }

    [HttpGet("video-engine/first-real-production/regression")]
    public ActionResult<object> FirstRealProductionRegression()
    {
        var fail = _firstRealProduction.RunRegression();
        return Ok(new
        {
            suite = FirstRealProductionRules.SuiteId,
            fail = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/first-real-production-v2/regression")]
    public ActionResult<object> FirstRealProductionV2Regression()
    {
        var fail = _firstRealProduction.RunV2Regression();
        return Ok(new
        {
            suite = FirstRealProductionV2Rules.SuiteId,
            status = fail.Count == 0 ? "PASS" : "FAIL",
            fail = fail.Count,
            p0 = fail.Count,
            failures = fail,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            autoRetry = false,
            autoApprove = false,
        });
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-execution/approve")]
    public async Task<ActionResult<ImageGenerationExecutionDto>> ApproveImageGenerationExecution(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationExecution.ApproveAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationExecutionError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-generation-execution/reject")]
    public async Task<ActionResult<ImageGenerationExecutionDto>> RejectImageGenerationExecution(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageGenerationExecution.RejectAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageGenerationExecutionError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/image-director-review")]
    [HttpGet("/api/production/shots/{id:guid}/image-director-review")]
    public async Task<ActionResult<ImageGenerationDirectorReviewDto>> GetImageDirectorReview(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageDirectorReview.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageDirectorReviewError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-director-review")]
    [HttpPost("/api/production/shots/{id:guid}/image-director-review")]
    public async Task<ActionResult<ImageGenerationDirectorReviewDto>> OpenImageDirectorReview(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageDirectorReview.OpenAsync(id, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageDirectorReviewError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-director-review/approve")]
    [HttpPost("/api/production/shots/{id:guid}/image-director-review/approve")]
    public async Task<ActionResult<ImageGenerationDirectorReviewDto>> ApproveImageDirectorReview(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageDirectorReview.ApproveAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageDirectorReviewError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/image-director-review/reject")]
    [HttpPost("/api/production/shots/{id:guid}/image-director-review/reject")]
    public async Task<ActionResult<ImageGenerationDirectorReviewDto>> RejectImageDirectorReview(
        Guid id, [FromBody] ImageGenerationDirectorReviewRejectRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _imageDirectorReview.RejectAsync(id, User.Identity?.Name ?? "director", request?.Reason ?? request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return ImageDirectorReviewError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/video-contract")]
    [HttpGet("/api/production/shots/{id:guid}/video-contract")]
    public async Task<ActionResult<ProductionVideoContractDto>> GetProductionVideoContract(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoContract.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-contract")]
    [HttpPost("/api/production/shots/{id:guid}/video-contract")]
    public async Task<ActionResult<ProductionVideoContractDto>> SaveProductionVideoContract(
        Guid id, [FromBody] ProductionVideoContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoContract.SaveAsync(id, request, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoContractError(ex); }
    }

    [HttpPut("video-engine/shots/{id:guid}/video-contract/{contractId:guid}")]
    [HttpPut("/api/production/shots/{id:guid}/video-contract/{contractId:guid}")]
    public async Task<ActionResult<ProductionVideoContractDto>> UpdateProductionVideoContract(
        Guid id, Guid contractId, [FromBody] ProductionVideoContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoContract.UpdateAsync(id, contractId, request, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-contract/validate")]
    [HttpPost("video-engine/shots/{id:guid}/video-contract/{contractId:guid}/validate")]
    [HttpPost("/api/production/shots/{id:guid}/video-contract/validate")]
    [HttpPost("/api/production/shots/{id:guid}/video-contract/{contractId:guid}/validate")]
    public async Task<ActionResult<ProductionVideoContractDto>> ValidateProductionVideoContract(
        Guid id, Guid? contractId, [FromBody] ProductionVideoContractWriteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoContract.ValidateAsync(id, contractId ?? request?.ContractId, request, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-contract/{contractId:guid}/approve")]
    [HttpPost("/api/production/shots/{id:guid}/video-contract/{contractId:guid}/approve")]
    public async Task<ActionResult<ProductionVideoContractDto>> ApproveProductionVideoContract(
        Guid id, Guid contractId, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoContract.ApproveAsync(id, contractId, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoContractError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-contract/{contractId:guid}/reject")]
    [HttpPost("/api/production/shots/{id:guid}/video-contract/{contractId:guid}/reject")]
    public async Task<ActionResult<ProductionVideoContractDto>> RejectProductionVideoContract(
        Guid id, Guid contractId, [FromBody] ImageGenerationDirectorReviewRejectRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoContract.RejectAsync(id, contractId, User.Identity?.Name ?? "director", request?.Reason ?? request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoContractError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/video-generation-execution")]
    [HttpGet("/api/production/shots/{id:guid}/video-generation-execution")]
    public async Task<ActionResult<VideoGenerationExecutionDto>> GetVideoGenerationExecution(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoGeneration.GetAsync(id, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoGenerationError(ex); }
    }

    [HttpGet("video-engine/shots/{id:guid}/video-generation-execution/{executionId:guid}/video")]
    public async Task<IActionResult> GetVideoGenerationExecutionArtifact(Guid id, Guid executionId, CancellationToken cancellationToken)
    {
        var file = await _videoGeneration.ReadArtifactAsync(id, executionId, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime, enableRangeProcessing: true);
    }

    [HttpPost("video-engine/shots/{id:guid}/video-generation-execution/preflight")]
    [HttpPost("/api/production/shots/{id:guid}/video-generation-execution/preflight")]
    public async Task<ActionResult<VideoGenerationExecutionDto>> PreflightVideoGenerationExecution(Guid id, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoGeneration.PreflightAsync(id, User.Identity?.Name ?? "director", cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoGenerationError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-generation-execution/execute")]
    [HttpPost("/api/production/shots/{id:guid}/video-generation-execution/execute")]
    public async Task<ActionResult<VideoGenerationExecutionDto>> ExecuteVideoGenerationExecution(
        Guid id, [FromQuery] bool confirm = false, CancellationToken cancellationToken = default)
    {
        try { return Ok(await _videoGeneration.ExecuteAsync(id, User.Identity?.Name ?? "director", confirm, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoGenerationError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-generation-execution/approve")]
    public async Task<ActionResult<VideoGenerationExecutionDto>> ApproveVideoGenerationExecution(
        Guid id, [FromBody] KitVideoProductionShotNoteRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoGeneration.ApproveAsync(id, User.Identity?.Name ?? "director", request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoGenerationError(ex); }
    }

    [HttpPost("video-engine/shots/{id:guid}/video-generation-execution/reject")]
    public async Task<ActionResult<VideoGenerationExecutionDto>> RejectVideoGenerationExecution(
        Guid id, [FromBody] ImageGenerationDirectorReviewRejectRequest? request, CancellationToken cancellationToken)
    {
        try { return Ok(await _videoGeneration.RejectAsync(id, User.Identity?.Name ?? "director", request?.Reason ?? request?.Note, cancellationToken)); }
        catch (InvalidOperationException ex) { return VideoGenerationError(ex); }
    }

    [HttpGet("video-engine/video-generation-execution/regression")]
    public ActionResult<object> VideoGenerationExecutionRegression()
    {
        var failures = _videoGeneration.RunRegression();
        return Ok(new
        {
            suite = VideoGenerationExecutionV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
        });
    }

    [HttpGet("video-engine/production-os/catalog")]
    public ActionResult<object> ProductionOsCatalog()
    {
        return Ok(new
        {
            architecture = ProductionOsRules.ArchitectureId,
            generate = false,
            autoSelect = ProductionOsRules.AutoSelectProvider(),
            providers = _productionOs.Catalog().Select(p => new
            {
                providerId = p.ProviderId,
                kind = p.Kind,
                capabilities = p.Capabilities,
            }),
        });
    }

    [HttpGet("video-engine/production-os/regression")]
    public ActionResult<object> ProductionOsArchitectureRegression()
    {
        var failures = _productionOs.RunRegression();
        return Ok(new
        {
            architecture = ProductionOsRules.ArchitectureId,
            suite = ProductionOsRules.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
        });
    }

    [HttpGet("video-engine/video-generation-execution/park-regression")]
    public ActionResult<object> VideoGenerationParkRegression()
    {
        var suites = _videoGeneration.RunParkRegressions();
        var fail = suites.Sum(s => s.Failures.Count);
        return Ok(new
        {
            status = fail == 0 ? "PASS" : "FAIL",
            fail,
            p0 = 0,
            generate = false,
            suites = suites.Select(s => new { suite = s.Suite, status = s.Failures.Count == 0 ? "PASS" : "FAIL", fail = s.Failures.Count, failures = s.Failures }),
        });
    }

    private ActionResult VideoGenerationError(InvalidOperationException ex)
    {
        if (ex is VideoGenerationExecutionException ve)
        {
            return BadRequest(new
            {
                status = "BLOCKED",
                code = ve.Code,
                gate = ve.Attribute,
                source = ve.BlockSource,
                requested = ve.Requested,
                authoritative = ve.Authoritative,
                message = ve.Message,
                generation = false,
                generate = false,
                blocks = ve.Blocks,
            });
        }
        return VideoContractError(ex);
    }

    [HttpGet("video-engine/video-contract/regression")]
    public ActionResult<object> ProductionVideoContractRegression()
    {
        var failures = _videoContract.RunRegression();
        return Ok(new
        {
            suite = ProductionVideoContractV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
        });
    }

    private ActionResult VideoContractError(InvalidOperationException ex)
    {
        if (ex is ProductionVideoContractException ve)
        {
            return BadRequest(new
            {
                status = "BLOCKED",
                code = ve.Code,
                source = ve.BlockSource,
                attribute = ve.Attribute,
                requested = ve.Requested,
                authoritative = ve.Authoritative,
                message = ve.Message,
                generation = false,
                generate = false,
                blocks = ve.Blocks,
            });
        }
        return ImageDirectorReviewError(ex);
    }

    [HttpGet("video-engine/image-director-review/regression")]
    public ActionResult<object> ImageDirectorReviewRegression()
    {
        var failures = _imageDirectorReview.RunRegression();
        return Ok(new
        {
            suite = ImageGenerationDirectorReviewV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
        });
    }

    private ActionResult ImageDirectorReviewError(InvalidOperationException ex)
    {
        if (ex is ImageGenerationDirectorReviewException ie)
        {
            return BadRequest(new
            {
                status = "BLOCKED",
                code = ie.Code,
                source = ie.BlockSource,
                attribute = ie.Attribute,
                requested = ie.Requested,
                authoritative = ie.Authoritative,
                message = ie.Message,
                generation = false,
                generate = false,
                blocks = ie.Blocks,
            });
        }
        return ImageGenerationExecutionError(ex);
    }

    [HttpGet("video-engine/image-generation-execution/regression")]
    public ActionResult<object> ImageGenerationExecutionRegression()
    {
        var failures = _imageGenerationExecution.RunRegression();
        return Ok(new
        {
            suite = ImageGenerationExecutionV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
        });
    }

    private ActionResult ImageGenerationExecutionError(InvalidOperationException ex)
    {
        if (ex is ImageGenerationExecutionException ie)
        {
            return BadRequest(new
            {
                status = "BLOCKED",
                code = ie.Code,
                source = ie.BlockSource,
                attribute = ie.Attribute,
                requested = ie.Requested,
                authoritative = ie.Authoritative,
                message = ie.Message,
                generation = false,
                geminiCalled = false,
                runwayCalled = false,
                veoCalled = false,
                blocks = ie.Blocks,
            });
        }
        return ImageGenerationContractError(ex);
    }

    [HttpGet("video-engine/image-generation-contract/regression")]
    public ActionResult<object> ImageGenerationContractRegression()
    {
        var failures = _imageGenerationContract.RunRegression();
        return Ok(new
        {
            suite = ImageGenerationContractV1Regression.SuiteId,
            status = failures.Count == 0 ? "PASS" : "FAIL",
            fail = failures.Count,
            p0 = 0,
            failures,
            generate = false,
        });
    }

    private ActionResult ImageGenerationContractError(InvalidOperationException ex)
    {
        if (ex is ImageGenerationContractException ie)
        {
            var body = new
            {
                status = "BLOCKED",
                code = ie.Code,
                source = ie.BlockSource,
                attribute = ie.Attribute,
                requested = ie.Requested,
                authoritative = ie.Authoritative,
                message = ie.Message,
                generate = false,
                blocks = ie.Blocks,
                governance = ie.Governance,
            };
            if (ie.Code is "IMAGE_GENERATION_CONTRACT_LOCKED")
                return Conflict(body);
            return BadRequest(body);
        }
        return ProductionPromptError(ex);
    }

    private ActionResult ProductionPromptError(InvalidOperationException ex)
    {
        if (ex is ProductionPromptCompilerException pe)
        {
            var first = pe.Blocks.FirstOrDefault();
            return BadRequest(new
            {
                status = "BLOCKED",
                code = pe.Code,
                source = pe.BlockSource,
                attribute = pe.Attribute,
                requested = pe.Requested,
                authoritative = pe.Authoritative,
                message = pe.Message,
                generate = false,
                blocks = pe.Blocks,
                governance = pe.Governance,
                detail = first?.Message ?? pe.Message,
            });
        }
        return ProductionShotError(ex);
    }

    private ActionResult ProductionShotError(InvalidOperationException ex)
    {
        if (ex is ProductionShotContractException ce)
        {
            var body = new
            {
                message = ce.Code,
                detail = ce.Message,
                generate = false,
                issues = ce.Issues,
                governance = ce.Governance,
            };
            if (ce.Code is "SHOT_CONTRACT_LOCKED")
                return Conflict(body);
            return BadRequest(body);
        }
        if (ex is CharacterIdentityGovernanceBlockedException ge)
            return BadRequest(new
            {
                message = "SHOT_GATE_NOT_SATISFIED",
                detail = ge.Message,
                generate = false,
                governance = ge.Result,
            });
        if (ex.Message.StartsWith("SHOT_GATE_NOT_SATISFIED", StringComparison.Ordinal)
            || ex.Message.StartsWith("SHOT_INVALID", StringComparison.Ordinal))
            return BadRequest(new { message = ex.Message.StartsWith("SHOT_INVALID", StringComparison.Ordinal) ? "SHOT_INVALID" : "SHOT_GATE_NOT_SATISFIED", detail = ex.Message });
        if (ex.Message.StartsWith("SHOT_LOCKED", StringComparison.Ordinal))
            return Conflict(new { message = "SHOT_LOCKED", detail = ex.Message });
        if (ex.Message.Contains("không tồn tại", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = ex.Message });
        return Conflict(new { message = ex.Message });
    }

    [HttpPost("/api/video/characters/{characterId}/identity-stress-tests")]
    public Task<ActionResult<KitVideoIdentityStressDto>> CreateIdentityStressAlias(
        string characterId,
        [FromBody] KitVideoIdentityStressCreateRequest request,
        CancellationToken cancellationToken) =>
        CreateIdentityStress(request with { CharacterId = characterId }, cancellationToken);

    [HttpPost("/api/video/identity-stress-tests/{id:guid}/run")]
    public Task<ActionResult<KitVideoIdentityStressDto>> RunIdentityStressAlias(
        Guid id,
        [FromBody] KitVideoIdentityStressRunRequest request,
        CancellationToken cancellationToken) =>
        RunIdentityStress(id, request, cancellationToken);

    [HttpGet("/api/video/identity-stress-tests/{id:guid}")]
    public Task<ActionResult<KitVideoIdentityStressDto>> GetIdentityStressAlias(Guid id, CancellationToken cancellationToken) =>
        GetIdentityStress(id, cancellationToken);

    [HttpGet("/api/video/identity-stress-tests/{id:guid}/cases")]
    public async Task<ActionResult<IReadOnlyList<KitVideoIdentityStressArtifactDto>>> IdentityStressCasesAlias(
        Guid id,
        CancellationToken cancellationToken)
    {
        var row = await _identityStress.GetAsync(id, cancellationToken);
        return Ok(row.Artifacts);
    }

    [HttpPost("/api/video/identity-stress-tests/{id:guid}/director-approve")]
    public Task<ActionResult<KitVideoIdentityStressDto>> IdentityStressApproveAlias(
        Guid id,
        [FromBody] KitVideoIdentityStressDecisionRequest? request,
        CancellationToken cancellationToken) =>
        IdentityStressDirectorPass(id, request, cancellationToken);

    [HttpPost("/api/video/identity-stress-tests/{id:guid}/director-reject")]
    public Task<ActionResult<KitVideoIdentityStressDto>> IdentityStressRejectAlias(
        Guid id,
        [FromBody] KitVideoIdentityStressDecisionRequest? request,
        CancellationToken cancellationToken) =>
        IdentityStressReject(id, request, cancellationToken);

    [HttpGet("video-engine/projects")]
    public async Task<ActionResult<IReadOnlyList<KitVideoProjectDto>>> ListVideoProjects(
        CancellationToken cancellationToken) =>
        Ok(await _videoEngine.ListProjectsAsync(cancellationToken));

    [HttpGet("video-engine/projects/{code}")]
    public async Task<ActionResult<KitVideoProjectDto>> GetVideoProject(
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoEngine.GetProjectAsync(code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/productions")]
    public async Task<ActionResult<IReadOnlyList<KitVideoProductionDto>>> ListVideoProductions(
        [FromQuery] string? project,
        CancellationToken cancellationToken) =>
        Ok(await _videoEngine.ListProductionsAsync(project, cancellationToken));

    [HttpGet("video-engine/productions/{id:guid}")]
    public async Task<ActionResult<KitVideoProductionDto>> GetVideoProduction(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoEngine.GetProductionAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/productions")]
    public async Task<ActionResult<KitVideoProductionDto>> EnsureVideoProduction(
        [FromBody] CreateKitVideoProductionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoEngine.EnsureProductionAsync(request, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/productions/{id:guid}/transition")]
    public async Task<ActionResult<KitVideoProductionDto>> TransitionVideoProduction(
        Guid id,
        [FromBody] TransitionKitVideoProductionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoEngine.TransitionProductionAsync(id, request, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("video-engine/productions/{id:guid}/shots/{shotCode}")]
    public async Task<ActionResult<KitVideoProductionDto>> UpsertVideoShot(
        Guid id,
        string shotCode,
        [FromBody] UpsertKitVideoShotStateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoEngine.UpsertShotAsync(id, shotCode, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/jobs")]
    public async Task<ActionResult<IReadOnlyList<KitVideoGenerationJobDto>>> ListEngineJobs(
        [FromQuery] Guid productionId,
        CancellationToken cancellationToken) =>
        Ok(await _videoJobs.ListAsync(productionId, cancellationToken));

    [HttpGet("video-engine/jobs/{id:guid}")]
    public async Task<ActionResult<KitVideoGenerationJobDto>> GetEngineJob(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoJobs.GetAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/jobs")]
    public async Task<ActionResult<KitVideoGenerationJobDto>> EnsureEngineJob(
        [FromBody] CreateKitVideoJobRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoJobs.EnsureAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/jobs/{id:guid}/confirm")]
    public async Task<ActionResult<KitVideoGenerationJobDto>> ConfirmEngineJob(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoJobs.ConfirmAsync(id, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/jobs/{id:guid}/complete")]
    public async Task<ActionResult<KitVideoGenerationJobDto>> CompleteEngineJob(
        Guid id,
        [FromBody] CompleteKitVideoJobRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoJobs.CompleteAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/jobs/{id:guid}/retry")]
    public async Task<ActionResult<KitVideoGenerationJobDto>> RetryEngineJob(
        Guid id,
        [FromBody] RetryKitVideoJobRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoJobs.RetryAsync(id, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpGet("video-engine/assets")]
    public async Task<ActionResult<IReadOnlyList<KitVideoAssetDto>>> ListEngineAssets(
        [FromQuery] string? project,
        CancellationToken cancellationToken) =>
        Ok(await _videoAssets.ListAsync(project, cancellationToken));

    [HttpGet("video-engine/assets/{project}/{code}")]
    public async Task<ActionResult<KitVideoAssetDto>> GetEngineAsset(
        string project,
        string code,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoAssets.GetAsync(project, code, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/assets/{project}/{code}/versions")]
    public async Task<ActionResult<KitVideoAssetDto>> CreateEngineAssetVersion(
        string project,
        string code,
        [FromBody] CreateKitVideoAssetVersionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoAssets.CreateVersionAsync(project, code, request, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return AssetConflictOrBad(ex);
        }
    }

    [HttpPut("video-engine/assets/{project}/{code}/canon")]
    public async Task<ActionResult<KitVideoAssetDto>> PutEngineAssetCanon(
        string project,
        string code,
        [FromBody] UpsertKitVideoAssetCanonRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoAssets.PutCanonAsync(project, code, request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return AssetConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/assets/{project}/{code}/lifecycle")]
    public async Task<ActionResult<KitVideoAssetDto>> SetEngineAssetLifecycle(
        string project,
        string code,
        [FromBody] TransitionKitVideoProductionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoAssets.SetLifecycleAsync(project, code, request.ToState, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return AssetConflictOrBad(ex);
        }
    }

    [HttpGet("video-engine/assets/{project}/{code}/usage")]
    public async Task<ActionResult<KitVideoAssetUsageDto>> GetEngineAssetUsage(
        string project,
        string code,
        CancellationToken cancellationToken) =>
        Ok(await _videoAssets.UsageAsync(project, code, cancellationToken));

    [HttpPost("video-engine/resolve")]
    public async Task<ActionResult<KitVideoShotPackageDto>> ResolveEngineShot(
        [FromBody] KitVideoResolveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _videoAssets.ResolveAsync(request, cancellationToken));

    [HttpPost("video-engine/preflight")]
    public async Task<ActionResult<KitVideoPreflightDto>> PreflightEngineShot(
        [FromBody] KitVideoResolveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _videoAssets.PreflightAsync(request, cancellationToken));

    [HttpPost("video-engine/compile-image-prompt")]
    public async Task<ActionResult<KitVideoCompiledPromptDto>> CompileEngineImagePrompt(
        [FromBody] KitVideoResolveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _videoAssets.CompileAsync(request, cancellationToken));

    [HttpPost("video-engine/keyframe-qa")]
    public async Task<ActionResult<KitVideoKeyframeQa>> EngineKeyframeQa(
        [FromBody] KitVideoKeyframeQaInput request,
        CancellationToken cancellationToken) =>
        Ok(await _videoAssets.QaAsync(request, cancellationToken));

    [HttpPost("video-engine/scene-masters")]
    public async Task<ActionResult<KitVideoSceneMasterDto>> UpsertEngineSceneMaster(
        [FromBody] UpsertKitVideoSceneMasterRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoAssets.UpsertSceneMasterAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return AssetConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/snapshots")]
    public async Task<ActionResult> ApproveEngineSnapshot(
        [FromBody] ApproveKitVideoSnapshotRequest request,
        CancellationToken cancellationToken)
    {
        await _videoAssets.ApproveSnapshotAsync(request, cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("video-engine/story")]
    public async Task<ActionResult<KitVideoStoryGraphDto>> GetEngineStory(
        [FromQuery] Guid productionId,
        CancellationToken cancellationToken)
    {
        var row = await _videoContinuity.GetAsync(productionId, cancellationToken);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("video-engine/story/compile")]
    public async Task<ActionResult<KitVideoStoryGraphDto>> CompileEngineStory(
        [FromBody] KitVideoStoryCompileRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _videoContinuity.CompileAsync(request, cancellationToken));

    [HttpPost("video-engine/story/validate")]
    public ActionResult<KitVideoContinuityResult> ValidateEngineContinuity(
        [FromBody] KitVideoContinuityValidateRequest request) =>
        Ok(_videoContinuity.Validate(request));

    [HttpPost("video-engine/story/gate")]
    public ActionResult<KitVideoStoryGateResult> GateEngineStory(
        [FromBody] KitVideoStoryGateRequest request) =>
        Ok(_videoContinuity.Gate(request));

    [HttpPost("video-engine/story/override")]
    public async Task<ActionResult<KitVideoContinuityOverrideDto>> OverrideEngineContinuity(
        [FromBody] KitVideoContinuityOverrideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoContinuity.OverrideAsync(request, User.Identity?.Name, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/story/impact")]
    public ActionResult<KitVideoScriptImpactResult> EngineScriptImpact(
        [FromBody] KitVideoScriptImpactRequest request) =>
        Ok(_videoContinuity.Impact(request));

    [HttpPost("video-engine/visual/compile")]
    public ActionResult<KitVideoGenerationRequestDto> CompileEngineVisual(
        [FromBody] KitVideoVisualCompileRequest request)
    {
        try
        {
            return Ok(_videoVision.Compile(request));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/visual/qa")]
    public ActionResult<KitVideoVisionQaDto> EngineVisionQa(
        [FromBody] KitVideoVisionEvaluateRequest request) =>
        Ok(_videoVision.Evaluate(request));

    [HttpPost("video-engine/visual/diagnose")]
    public ActionResult<KitVideoRepairDto> EngineVisionDiagnose(
        [FromBody] KitVideoVisionQaDto qa) =>
        Ok(_videoVision.Diagnose(qa));

    [HttpPost("video-engine/visual/revision")]
    public ActionResult<object> EngineVisionRevision(
        [FromBody] KitVideoDirectorFeedbackRequest request)
    {
        try
        {
            return Ok(new { instruction = _videoVision.Revision(request.Note) });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("video-engine/visual/attempts")]
    public async Task<ActionResult<IReadOnlyList<KitVideoKeyframeAttemptDto>>> ListEngineAttempts(
        [FromQuery] Guid productionId,
        [FromQuery] string shotCode,
        CancellationToken cancellationToken) =>
        Ok(await _videoVision.ListAttemptsAsync(productionId, shotCode, cancellationToken));

    [HttpPost("video-engine/visual/attempts")]
    public async Task<ActionResult<KitVideoKeyframeAttemptDto>> RecordEngineAttempt(
        [FromBody] KitVideoRecordAttemptRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoVision.RecordAttemptAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("video-engine/visual/decide")]
    public async Task<ActionResult<KitVideoPixelGenerateDto>> DecideEngineAttempt(
        [FromBody] KitVideoDirectorDecideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoPixels.DecideAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/visual/i2v-package")]
    public ActionResult<KitVideoI2vReadyPackageDto> EngineI2vPackage(
        [FromBody] KitVideoKeyframeAttemptDto attempt) =>
        Ok(_videoVision.I2vPackage(attempt));

    [HttpGet("video-engine/visual/provider")]
    public async Task<ActionResult<KitVideoProviderStatusDto>> EngineVisualProvider(
        CancellationToken cancellationToken) =>
        Ok(await _videoPixels.ProviderAsync(cancellationToken));

    [HttpPost("video-engine/visual/generate")]
    public async Task<ActionResult<KitVideoPixelGenerateDto>> EngineVisualGenerate(
        [FromBody] KitVideoPixelGenerateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoPixels.GenerateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/visual/analyze")]
    public async Task<ActionResult<KitVideoPixelGenerateDto>> EngineVisualAnalyze(
        [FromBody] KitVideoPixelAnalyzeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoPixels.AnalyzeBytesAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/visual/revalidate")]
    public async Task<ActionResult<KitVideoPixelGenerateDto>> EngineVisualRevalidate(
        [FromBody] KitVideoPixelRevalidateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoPixels.RevalidateAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpGet("video-engine/visual/i2v-ready")]
    public async Task<ActionResult<KitVideoI2vReadyPackageDto>> EngineVisualI2vReady(
        [FromQuery] Guid attemptId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoPixels.I2vPackageAsync(attemptId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpGet("video-engine/visual/attempts/{attemptId:guid}/image")]
    public async Task<IActionResult> EngineVisualAttemptImage(Guid attemptId, CancellationToken cancellationToken)
    {
        var file = await _videoPixels.ReadArtifactAsync(attemptId, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime);
    }

    [HttpPost("video-engine/motion/preflight")]
    public async Task<ActionResult<KitVideoMotionTakeDto>> EngineMotionPreflight(
        [FromBody] KitVideoMotionSubmitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoMotion.PreflightAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/motion/submit")]
    public async Task<ActionResult<KitVideoMotionTakeDto>> EngineMotionSubmit(
        [FromBody] KitVideoMotionSubmitRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoMotion.SubmitAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/motion/{takeId:guid}/poll")]
    public async Task<ActionResult<KitVideoMotionTakeDto>> EngineMotionPoll(
        Guid takeId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoMotion.PollAsync(takeId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpGet("video-engine/motion/latest")]
    public async Task<ActionResult<KitVideoMotionTakeDto>> EngineMotionLatest(
        [FromQuery] Guid attemptId,
        CancellationToken cancellationToken)
    {
        var row = await _videoMotion.GetLatestByKeyframeAsync(attemptId, cancellationToken);
        if (row is null) return NotFound();
        return Ok(row);
    }

    [HttpGet("video-engine/motion/{takeId:guid}")]
    public async Task<ActionResult<KitVideoMotionTakeDto>> EngineMotionGet(
        Guid takeId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoMotion.GetAsync(takeId, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpPost("video-engine/motion/{takeId:guid}/decide")]
    public async Task<ActionResult<KitVideoMotionTakeDto>> EngineMotionDecide(
        Guid takeId,
        [FromBody] KitVideoTakeDecideRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _videoMotion.DecideAsync(takeId, request.Decision, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return JobConflictOrBad(ex);
        }
    }

    [HttpGet("video-engine/motion/{takeId:guid}/video")]
    public async Task<IActionResult> EngineMotionVideo(Guid takeId, CancellationToken cancellationToken)
    {
        var file = await _videoMotion.ReadVideoAsync(takeId, cancellationToken);
        if (file is null) return NotFound();
        return File(file.Value.Bytes, file.Value.Mime);
    }

    private ActionResult JobConflictOrBad(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("HOLD", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("CREDIT_GATE", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("IDEMPOTENCY", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("DO_NOT_BLIND_RETRY", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("REVIEW_REQUIRED", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("NOT_READY", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("ARTIFACT_FAILED", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("GOLDEN_ONLY", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("PREFLIGHT", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("PHASE_06_GOLDEN_ONLY", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("INVALIDATED", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = ex.Message });
        }
        return BadRequest(new { message = ex.Message });
    }

    private ActionResult AssetConflictOrBad(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("LOCKED", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("MISSING", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("APPROVE trước", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("I2V", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = ex.Message });
        }
        return BadRequest(new { message = ex.Message });
    }

    private ActionResult CharacterConflictOrBad(InvalidOperationException ex)
    {
        if (ex.Message.StartsWith("LOCKED", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("REQUEST CREATION", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("ARCHIVED", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("DUPLICATE", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("QA FAIL", StringComparison.OrdinalIgnoreCase)
            || ex.Message.StartsWith("APPROVE trước", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { message = ex.Message });
        }
        return BadRequest(new { message = ex.Message });
    }

    [HttpGet("series/voices")]
    public async Task<ActionResult<IReadOnlyList<ContentSeriesVoiceDto>>> ListSeriesVoices(
        CancellationToken cancellationToken) =>
        Ok(await _seriesPilot.ListVoicesAsync(cancellationToken));

    [HttpGet("series/voices/{voiceId}/preview")]
    public async Task<IActionResult> PreviewSeriesVoiceLibrary(
        string voiceId,
        CancellationToken cancellationToken)
    {
        var bytes = await _seriesPilot.GetLibraryPreviewAsync(voiceId, cancellationToken);
        if (bytes is null || bytes.Length == 0)
            return NotFound(new { message = "Giọng này chưa có file mẫu thư viện." });
        return File(bytes, "audio/mpeg");
    }

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
            var preview = await _seriesPilot.PreviewTtsAsync(
                request.VoiceId,
                request.Text,
                request.PublicOwnerId,
                request.VoiceName,
                request.VoiceSettings,
                cancellationToken,
                request.Accent);
            if (!string.IsNullOrWhiteSpace(preview.DecisionId))
                Response.Headers["X-Famixa-Decision-Id"] = preview.DecisionId;
            if (!string.IsNullOrWhiteSpace(preview.ProviderId))
                Response.Headers["X-Famixa-Provider-Id"] = preview.ProviderId;
            if (!string.IsNullOrWhiteSpace(preview.ModelId))
                Response.Headers["X-Famixa-Model-Id"] = preview.ModelId;
            return File(preview.Bytes, "audio/mpeg");
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
