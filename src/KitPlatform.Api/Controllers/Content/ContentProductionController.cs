using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using KitPlatform.Api.Authorization;
using KitPlatform.Application.Core;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Api.Controllers.Content;

[ApiController]
[Authorize(Roles = "ADMIN")]
[RequirePlatformModule(PlatformModuleCodes.KitContent)]
[ServiceFilter(typeof(ProductionTenantActionFilter))]
[Route("api/content/production")]
public sealed class ContentProductionController : ControllerBase
{
    readonly IProductionCreateVideoService _createVideo;
    readonly IProductionTestFoundationService _tests;
    readonly IProductionTestKeyframeService _testKeyframes;
    readonly IPerformanceContractService _performance;
    readonly ProductionRunwayHttpAudit _runwayAudit;
    readonly ProductionElevenLabsHttpAudit _elevenLabsAudit;
    readonly ProductionFalHttpAudit _falAudit;
    readonly ProductionFfmpegMixAudit _ffmpegMixAudit;
    readonly IProductionReadinessAuthority _readiness;
    readonly IProductionCredentialRegistration _credentials;
    readonly IProductionCredentialStore _credentialStore;
    readonly IProductionTenantAiSettings _tenantAi;
    readonly IProductionCredentialKeyRing _keys;
    readonly ILogger<ContentProductionController> _log;
    readonly IContentSeriesPilotService _series;

    public ContentProductionController(
        IProductionCreateVideoService createVideo,
        IProductionTestFoundationService tests,
        IProductionTestKeyframeService testKeyframes,
        IPerformanceContractService performance,
        ProductionRunwayHttpAudit runwayAudit,
        ProductionElevenLabsHttpAudit elevenLabsAudit,
        ProductionFalHttpAudit falAudit,
        ProductionFfmpegMixAudit ffmpegMixAudit,
        IProductionReadinessAuthority readiness,
        IProductionCredentialRegistration credentials,
        IProductionCredentialStore credentialStore,
        IProductionTenantAiSettings tenantAi,
        IProductionCredentialKeyRing keys,
        ILogger<ContentProductionController> log,
        IContentSeriesPilotService series)
    {
        _keys = keys;
        _log = log;
        _series = series;
        _tenantAi = tenantAi;
        _createVideo = createVideo;
        _tests = tests;
        _testKeyframes = testKeyframes;
        _performance = performance;
        _runwayAudit = runwayAudit;
        _elevenLabsAudit = elevenLabsAudit;
        _falAudit = falAudit;
        _ffmpegMixAudit = ffmpegMixAudit;
        _readiness = readiness;
        _credentials = credentials;
        _credentialStore = credentialStore;
    }

    [HttpGet("runway-http-audit")]
    public ActionResult<object> GetRunwayHttpAudit() =>
        Ok(new
        {
            startCount = _runwayAudit.StartCount,
            pollCount = _runwayAudit.PollCount,
            downloadCount = _runwayAudit.DownloadCount,
            lastProviderJobId = _runwayAudit.LastProviderJobId,
            total = _runwayAudit.StartCount + _runwayAudit.PollCount + _runwayAudit.DownloadCount,
        });

    [HttpGet("elevenlabs-http-audit")]
    public ActionResult<object> GetElevenLabsHttpAudit() =>
        Ok(new
        {
            startCount = _elevenLabsAudit.StartCount,
            lastProviderJobId = _elevenLabsAudit.LastProviderJobId,
        });

    [HttpGet("fal-http-audit")]
    public ActionResult<object> GetFalHttpAudit() =>
        Ok(new
        {
            startCount = _falAudit.StartCount,
            lastProviderJobId = _falAudit.LastProviderJobId,
        });

    [HttpGet("ffmpeg-mix-audit")]
    public ActionResult<object> GetFfmpegMixAudit() =>
        Ok(new
        {
            executionCount = _ffmpegMixAudit.ExecutionCount,
            composeExecutionCount = _ffmpegMixAudit.ComposeExecutionCount,
            lastCommand = _ffmpegMixAudit.LastCommand,
            lastComposeCommand = _ffmpegMixAudit.LastComposeCommand,
        });

    [HttpPost("recover-existing-lipsync")]
    public async Task<ActionResult<ProductionCreateVideoResultDto>> RecoverExistingLipsync(
        [FromBody] RecoverExistingLipsyncRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var slice = await _createVideo.RecoverOfficialExistingLipsyncAsync(body.AttemptId, body.ProviderJobId, ct);
            return Ok(ToDto(slice, slice.Task.InputSnapshotId ?? Guid.Empty));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("close-empty-lipsync-request")]
    public async Task<ActionResult<object>> CloseEmptyLipsyncRequest(
        [FromBody] CloseEmptyLipsyncRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var request = await _createVideo.CloseEmptyOfficialLipsyncRequestAsync(body.IdempotencyKey, ct);
            return Ok(new
            {
                requestId = request.RequestId,
                requestStatus = request.Status,
                idempotencyKey = request.IdempotencyKey,
                userAction = request.UserAction,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("performance-drafts")]
    public ActionResult<object> CreatePerformanceDraft([FromBody] PerformanceDraftRequestDto body)
    {
        try
        {
            var draft = _performance.CreateDraft(ToSource(body));
            var verdict = _performance.ValidateDraft(draft, body.ExtraJson);
            return Ok(new
            {
                documentId = draft.DocumentId,
                schemaVersion = draft.SchemaVersion,
                buildId = draft.BuildId,
                shotCode = draft.ShotCode,
                payloadJson = PerformanceContractCanon.CanonicalJson(draft),
                voiceReady = verdict.VoiceReady,
                ok = verdict.Ok,
                issues = verdict.Issues.Select(i => i.Code).ToArray(),
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("performance-snapshots")]
    public async Task<ActionResult<PerformanceSnapshotDto>> FreezePerformanceSnapshot(
        [FromBody] PerformanceDraftRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var source = ToSource(body);
            var draft = _performance.CreateDraft(source);
            var snap = await _performance.FreezeAsync(
                draft, User.Identity?.Name ?? "admin", PerformanceContractRules.SourceOf(source, draft), body.ExtraJson, ct);
            var verdict = _performance.ValidateDraft(snap.Draft);
            return Ok(ToPerformanceDto(snap, verdict));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("director-timing")]
    public async Task<ActionResult<PerformanceSnapshotDto>> MaterializeDirectorTiming(
        [FromBody] ProductionDirectorTimingRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var verdict = await _createVideo.EvaluateCreateVideoAsync(scope, null, ct);
            if (verdict.KeyframeArtifactId is null)
                return BadRequest(new { message = ProductionFailureReasons.MissingKeyframe });
            var contractActing = ShortProductionQualityPipelineV1.Ep01Contract(scope.ShotCode)?.ActingBeat;
            var acting = string.IsNullOrWhiteSpace(contractActing) ? verdict.ActingBeat : contractActing;
            if (string.Equals(scope.ShotCode, "EP01-SC01-SH04", StringComparison.Ordinal))
                acting = "Exactly two people. No third person, no ghost, no duplicate body. Both faces stay toward each other. Nobody turns their back to the camera. Heads do not spin or snap. " + acting;
            var snap = await _readiness.MaterializeDirectorTimingAsync(new MaterializeDirectorTimingCommand(
                scope, body.DurationSec, verdict.Story, acting, verdict.VisualContract,
                verdict.KeyframeArtifactId.Value, verdict.KeyframeByteSha256, User.Identity?.Name ?? "admin",
                body.SpeechStartSec, body.SpeechEndSec), ct);
            return Ok(ToPerformanceDto(snap, _performance.ValidateDraft(
                snap.Draft,
                ProductionDirectorTiming.ExtraJson(body.DurationSec, "director", body.SpeechStartSec, body.SpeechEndSec))));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("voice-production-ingress")]
    public async Task<ActionResult<VoiceProductionIngressDto>> MaterializeVoiceProductionIngress(
        [FromBody] VoiceProductionIngressRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var command = new MaterializeVoiceProductionIngressCommand(scope, User.Identity?.Name ?? "admin");
            var row = ProductionEp01Sh02VoiceIngressCanon.MatchesScope(scope)
                ? await _readiness.MaterializeSh02VoiceProductionIngressAsync(command, ct)
                : await _readiness.MaterializeVoiceProductionIngressAsync(command, ct);
            var line = row.Snapshot.Draft.Lines[0];
            return Ok(new VoiceProductionIngressDto(
                row.Snapshot.PerformanceSnapshotId,
                row.Snapshot.PerformanceHash,
                row.ParentSnapshotId,
                line.LineId,
                line.TextIdentity.Text,
                line.CharacterId,
                ProductionVoiceSelectionCanon.VoiceIdOf(row.Selection),
                row.Selection.ProviderId,
                row.Selection.ModelId,
                row.Selection.SelectionId,
                row.Selection.SelectionHash,
                row.Permit.PermitId,
                row.Permit.Status,
                row.Permit.PermitHash));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("lipsync-production-ingress")]
    public async Task<ActionResult<ProductionExecutionPermitDto>> MaterializeLipsyncProductionIngress(
        [FromBody] VoiceProductionIngressRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var row = await _readiness.MaterializeSh02LipsyncProductionIngressAsync(
                new MaterializeLipsyncProductionIngressCommand(scope, User.Identity?.Name ?? "admin"), ct);
            return Ok(ToPermitDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("performance-snapshots/{id:guid}")]
    public async Task<ActionResult<PerformanceSnapshotDto>> GetPerformanceSnapshot(Guid id, CancellationToken ct)
    {
        try
        {
            var snap = await _performance.GetAsync(id, ct);
            return Ok(ToPerformanceDto(snap, _performance.ValidateDraft(snap.Draft)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("test-runs")]
    public async Task<ActionResult<ProductionTestRunDto>> CreateTestRun(
        [FromBody] ProductionCreateTestRunRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var created = await _tests.CreateAsync(body.Name, User.Identity?.Name ?? "admin", ct);
            return Ok(ToTestDto(created));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("test-runs/{id:guid}")]
    public async Task<ActionResult<ProductionTestRunDto>> GetTestRun(Guid id, CancellationToken ct)
    {
        var row = await _tests.GetAsync(id, ct);
        if (row is null) return NotFound();
        return Ok(ToTestDto(row));
    }

    [HttpPost("test-runs/{id:guid}/keyframes")]
    public async Task<ActionResult<ProductionTestKeyframeDto>> RegisterTestKeyframe(
        Guid id,
        [FromBody] ProductionRegisterTestKeyframeRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var run = await _tests.GetAsync(id, ct);
            if (run is null) return NotFound();
            var row = await _testKeyframes.RegisterAsync(new RegisterTestKeyframeCommand(
                id, run.Scope.ScopeId, body.ShotCode, body.FixtureId, User.Identity?.Name ?? "admin"), ct);
            return Ok(new ProductionTestKeyframeDto(
                row.FixtureId, row.ArtifactId, row.ScopeId, row.ShotCode, row.Role, row.Mime,
                row.ByteSha256, row.ByteLength, row.Width, row.Height, row.StorageKey,
                row.VerificationStatus, row.Source, row.BindingId, row.Reused));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("keyframes")]
    public async Task<ActionResult<ProductionCreateVideoResultDto>> RegisterKeyframe(
        [FromBody] ProductionRegisterKeyframeRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var (bytes, mime) = ReadDataUrl(body.KeyframeDataUrl);
            var art = await _createVideo.RegisterKeyframeAsync(
                new RegisterKeyframeCommand(
                    scope, bytes, mime, body.Story, User.Identity?.Name ?? "admin",
                    body.ActingBeat ?? "", body.VisualContract ?? "", body.Timing ?? "",
                    body.DurationSec, body.PictureRevisionId, body.IngressProvenanceJson), ct);
            return Ok(new ProductionCreateVideoResultDto(
                Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, ProductionActions.CreateImage,
                "import", "existing-pixels", null, art.ArtifactId, art.ByteSha256, art.StorageKey,
                null, null, "empty"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("video-takes/recover")]
    public async Task<ActionResult<ProductionCreateVideoResultDto>> RecoverExistingVideoTake(
        [FromBody] ProductionRecoverVideoTakeRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var art = await _createVideo.RegisterExistingVideoTakeAsync(
                new RegisterExistingVideoTakeCommand(scope, body.SourceArtifactId, User.Identity?.Name ?? "admin"), ct);
            return Ok(new ProductionCreateVideoResultDto(
                Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, ProductionActions.CreateVideo,
                "import", "existing-video", null, art.ArtifactId, art.ByteSha256, art.StorageKey,
                null, null, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("voices")]
    public async Task<ActionResult<ProductionCreateVideoResultDto>> RegisterVoice(
        [FromBody] ProductionRegisterVoiceRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var build = await _series.GetBuildAsync(body.BuildId, ct);
            NovixaVoiceBytesGate.Require(build.Id, build.SeriesCode, build.Graph.GetRawText(), body.ShotCode);
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var (bytes, mime) = ReadDataUrl(body.VoiceDataUrl);
            var art = await _createVideo.RegisterVoiceBytesAsync(
                new RegisterVoiceBytesCommand(scope, bytes, mime, body.Story, User.Identity?.Name ?? "admin"), ct);
            return Ok(new ProductionCreateVideoResultDto(
                Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, ProductionActions.CreateVoice,
                "import", "existing-audio", null, art.ArtifactId, art.ByteSha256, art.StorageKey,
                null, null, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("voices/recover")]
    public async Task<ActionResult<ProductionCreateVideoResultDto>> RecoverExistingVoice(
        [FromBody] ProductionRecoverVoiceRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var art = await _createVideo.RegisterExistingVoiceAsync(
                new RegisterExistingVoiceCommand(scope, body.SourceArtifactId, User.Identity?.Name ?? "admin"), ct);
            return Ok(new ProductionCreateVideoResultDto(
                Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, ProductionActions.CreateVoice,
                "import", "existing-voice", null, art.ArtifactId, art.ByteSha256, art.StorageKey,
                null, null, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("requests/prepare")]
    public async Task<ActionResult<ProductionCreateVideoPrepareDto>> PrepareCreateVideo(
        [FromBody] ProductionCreateVideoRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            var input = ToShotInput(body, body.KeyframeArtifactId is { } id ? [id] : null);
            var prepared = await _createVideo.PrepareCreateVideoAsync(new CreateVideoApiCommand(
                scope, input, User.Identity?.Name ?? "admin", body.IdempotencyKey, null, null,
                body.ProviderId, body.ModelId, false, body.AuthorizePaidNetwork), ct);
            var p = prepared.Preflight;
            return Ok(new ProductionCreateVideoPrepareDto(
                prepared.Request.RequestId, prepared.Task.TaskId, prepared.Snapshot.SnapshotId,
                prepared.Snapshot.SnapshotHash, prepared.Attempt.AttemptId, p.ProviderId, p.ModelId,
                p.ProviderDuration, p.EstimatedCost, p.MaxCost, p.KeyframeArtifactId, p.KeyframeSha,
                p.KeyframeStatus, p.Confirmation, p.Network, p.FilmMutation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("requests")]
    public async Task<ActionResult<ProductionCreateVideoResultDto>> CreateVideo(
        [FromBody] ProductionCreateVideoRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var action = string.IsNullOrWhiteSpace(body.Action) ? ProductionActions.CreateVideo : body.Action.Trim();
            var scope = Scope(body.BuildId, body.EpisodeCode, body.SceneCode, body.ShotCode);
            if (string.Equals(action, ProductionActions.AssembleEpisode, StringComparison.Ordinal))
            {
                var episode = await _createVideo.AssembleEpisodeOsAsync(ActionCommand(scope, body, User.Identity?.Name ?? "admin"), ct);
                var episodeOut = episode.Output;
                return Ok(new ProductionCreateVideoResultDto(
                    episode.Request.RequestId, episode.Task.TaskId,
                    episode.Task.InputSnapshotId ?? Guid.Empty, episode.Attempt.AttemptId,
                    ProductionActions.AssembleEpisode, episode.Attempt.ProviderId, episode.Attempt.ModelId,
                    episode.Attempt.ProviderJobId ?? episode.Assembly.AssemblyId.ToString("D"),
                    episodeOut?.ArtifactId ?? Guid.Empty,
                    episodeOut?.ByteSha256 ?? "",
                    episodeOut?.StorageKey ?? "",
                    null, null, episodeOut is null ? "none" : "visible"));
            }
            if (string.Equals(action, ProductionActions.Assemble, StringComparison.Ordinal))
            {
                var assembled = await _createVideo.AssembleOsAsync(ActionCommand(scope, body, User.Identity?.Name ?? "admin"), ct);
                var output = assembled.Output;
                return Ok(new ProductionCreateVideoResultDto(
                    assembled.Request.RequestId, assembled.Task.TaskId,
                    assembled.Task.InputSnapshotId ?? Guid.Empty, assembled.Attempt.AttemptId,
                    ProductionActions.Assemble, assembled.Attempt.ProviderId, assembled.Attempt.ModelId,
                    assembled.Attempt.ProviderJobId ?? assembled.Assembly.AssemblyId.ToString("D"),
                    output?.ArtifactId ?? assembled.AcceptedArtifactIds.FirstOrDefault(),
                    output?.ByteSha256 ?? "",
                    output?.StorageKey ?? ("assembly/" + assembled.Assembly.AssemblyId.ToString("N")),
                    null, null, output is null ? "none" : "visible"));
            }
            if (!string.Equals(action, ProductionActions.CreateVideo, StringComparison.Ordinal))
            {
                var slice = await ExecuteOsActionAsync(action, scope, body, ct);
                return Ok(ToDto(slice, slice.Task.InputSnapshotId ?? Guid.Empty));
            }
            if (!string.Equals(ProductionActions.CreateVideo, "CREATE_VIDEO", StringComparison.Ordinal))
                return BadRequest(new { message = ProductionFailureReasons.ActionRemapForbidden });
            byte[]? kfBytes = null;
            string? kfMime = null;
            if (!string.IsNullOrWhiteSpace(body.KeyframeDataUrl))
                (kfBytes, kfMime) = ReadDataUrl(body.KeyframeDataUrl);
            var input = ToShotInput(body, body.KeyframeArtifactId is { } id ? [id] : null);
            var result = await _createVideo.CreateVideoAsync(new CreateVideoApiCommand(
                scope, input, User.Identity?.Name ?? "admin", body.IdempotencyKey, kfBytes, kfMime,
                body.ProviderId, body.ModelId, false, body.AuthorizePaidNetwork, false, body.PreparedAttemptId), ct);
            var snap = result.Task.InputSnapshotId ?? Guid.Empty;
            return Ok(ToDto(result, snap));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("requests/{id:guid}")]
    public async Task<ActionResult<ProductionRequestDto>> GetRequest(Guid id, CancellationToken ct)
    {
        var row = await _createVideo.GetRequestAsync(id, ct);
        if (row is null) return NotFound();
        return Ok(new ProductionRequestDto(
            row.RequestId, row.UserAction, row.RequiredOutputType, row.Status,
            row.Scope.BuildId, row.Scope.ShotCode, row.IdempotencyKey));
    }

    [HttpGet("strategy/suggest")]
    public ActionResult<NovixaShortProductionStrategyV1.Plan> SuggestShortStrategy(
        [FromQuery] bool hasDialogue = false,
        [FromQuery] bool speaksOnCamera = false,
        [FromQuery] bool needsPictureMotion = false,
        [FromQuery] bool wordLevelMouth = false) =>
        Ok(NovixaShortProductionStrategyV1.Suggest(new NovixaShortProductionStrategyV1.Facts(
            hasDialogue, speaksOnCamera, needsPictureMotion, wordLevelMouth)));

    [HttpGet("tasks/{id:guid}")]
    public async Task<ActionResult<ProductionTaskDto>> GetTask(Guid id, CancellationToken ct)
    {
        var row = await _createVideo.GetTaskAsync(id, ct);
        if (row is null) return NotFound();
        return Ok(new ProductionTaskDto(
            row.TaskId, row.RequestId, row.Action, row.Status, row.ProviderId, row.ModelId, row.InputSnapshotId));
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/actions")]
    public async Task<ActionResult<ProductionShotActionsDto>> GetActions(
        Guid buildId, string shotCode, [FromQuery] string? episodeCode, [FromQuery] string? sceneCode,
        [FromQuery] string? story, [FromQuery] string? actingBeat, [FromQuery] string? visualContract,
        [FromQuery] string? timing, [FromQuery] Guid? keyframeArtifactId,
        [FromQuery] Guid? performanceSnapshotId, [FromQuery] string? performanceHash,
        [FromQuery] string? providerId, [FromQuery] string? modelId,
        [FromQuery] string? pictureStrategy, [FromQuery] string? speechDelivery,
        [FromQuery] bool strategyConfirmed = false, [FromQuery] int directSpeakerCount = 0,
        CancellationToken ct = default)
    {
        var scope = Scope(buildId, episodeCode, sceneCode, shotCode);
        var state = await _createVideo.GetShotStateAsync(scope, Claim(
            story, actingBeat, visualContract, timing, keyframeArtifactId,
            performanceSnapshotId, performanceHash, providerId, modelId,
            pictureStrategy, speechDelivery, strategyConfirmed, directSpeakerCount), ct);
        var motion = state.Nodes.FirstOrDefault(n => n.Node == ShotNodeIds.Motion);
        var currentState = motion?.Status == ShotNodeStatuses.Accepted ? "ACCEPTED"
            : state.VisibleArtifactId is not null && motion?.ArtifactId == state.VisibleArtifactId ? "VISIBLE"
            : motion?.ArtifactId is not null ? "CURRENT"
            : "EMPTY";
        return Ok(new ProductionShotActionsDto(
            buildId, shotCode, currentState,
            state.CurrentArtifactId, state.VisibleArtifactId, state.AcceptedArtifactId, state.Actions,
            state.VideoArtifactId, state.VoiceArtifactId, state.LipsyncArtifactId));
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/state")]
    public async Task<ActionResult<ShotProductionState>> GetState(
        Guid buildId, string shotCode, [FromQuery] string? episodeCode, [FromQuery] string? sceneCode,
        [FromQuery] string? story, [FromQuery] string? actingBeat, [FromQuery] string? visualContract,
        [FromQuery] string? timing, [FromQuery] Guid? keyframeArtifactId,
        [FromQuery] Guid? performanceSnapshotId, [FromQuery] string? performanceHash,
        [FromQuery] string? providerId, [FromQuery] string? modelId,
        [FromQuery] string? pictureStrategy, [FromQuery] string? speechDelivery,
        [FromQuery] bool strategyConfirmed = false, [FromQuery] int directSpeakerCount = 0,
        CancellationToken ct = default)
        => Ok(await _createVideo.GetShotStateAsync(
            Scope(buildId, episodeCode, sceneCode, shotCode),
            Claim(story, actingBeat, visualContract, timing, keyframeArtifactId,
                performanceSnapshotId, performanceHash, providerId, modelId,
                pictureStrategy, speechDelivery, strategyConfirmed, directSpeakerCount), ct));

    [HttpPost("ai-selections/supersede")]
    public async Task<ActionResult<ProductionAiSelectionSupersedeDto>> SupersedeAiSelection(
        [FromBody] ProductionAiSelectionSupersedeRequestDto body,
        CancellationToken ct)
    {
        try
        {
            _tenantAi.RequireSelectableOverride(body.BuildId, body.Stage, body.ProviderId, body.ModelId);
            var result = await _readiness.SupersedeSelectionAsync(new SupersedeProductionAiSelectionCommand(
                body.ExpectedSelectionId,
                new UpsertProductionAiSelectionCommand(
                    body.BuildId, body.ShotCode, body.EpisodeCode, body.SceneCode, body.Stage,
                    body.ProviderId, body.ModelId, User.Identity?.Name ?? "admin", body.Reason ?? "")), ct);
            return Ok(new ProductionAiSelectionSupersedeDto(
                ToSelectionDto(result.Superseded), ToSelectionDto(result.Active)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("ai-selections")]
    public async Task<ActionResult<ProductionAiSelectionDto>> UpsertAiSelection(
        [FromBody] ProductionAiSelectionRequestDto body,
        CancellationToken ct)
    {
        try
        {
            _tenantAi.RequireSelectableOverride(body.BuildId, body.Stage, body.ProviderId, body.ModelId);
            var row = await _readiness.UpsertSelectionAsync(new UpsertProductionAiSelectionCommand(
                body.BuildId, body.ShotCode, body.EpisodeCode, body.SceneCode, body.Stage,
                body.ProviderId, body.ModelId, User.Identity?.Name ?? "admin", body.Reason ?? ""), ct);
            return Ok(ToSelectionDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("ai-selections")]
    public async Task<ActionResult<ProductionAiSelectionDto>> GetAiSelection(
        [FromQuery] Guid buildId, [FromQuery] string shotCode, [FromQuery] string? stage, CancellationToken ct)
    {
        var row = await _readiness.GetSelectionAsync(
            buildId, shotCode, string.IsNullOrWhiteSpace(stage) ? ProductionAiSelectionStages.Video : stage.Trim(), ct);
        if (row is null) return NotFound();
        return Ok(ToSelectionDto(row));
    }

    [HttpPost("execution-permits")]
    public async Task<ActionResult<ProductionExecutionPermitDto>> IssueExecutionPermit(
        [FromBody] ProductionExecutionPermitRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var row = await _readiness.IssuePermitAsync(new IssueProductionExecutionPermitCommand(
                body.BuildId, body.ShotCode, body.EpisodeCode, body.SceneCode, body.Action,
                body.ProviderId, body.ModelId, User.Identity?.Name ?? "admin", body.Reason ?? ""), ct);
            return Ok(ToPermitDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("execution-permits")]
    public async Task<ActionResult<ProductionExecutionPermitDto>> GetExecutionPermit(
        [FromQuery] Guid buildId, [FromQuery] string shotCode, [FromQuery] string? action,
        [FromQuery] string? providerId, [FromQuery] string? modelId, CancellationToken ct)
    {
        var act = string.IsNullOrWhiteSpace(action) ? ProductionActions.CreateVideo : action.Trim();
        var provider = string.IsNullOrWhiteSpace(providerId) ? ProductionMotionCatalog.RunwayProviderId : providerId.Trim();
        var model = string.IsNullOrWhiteSpace(modelId) ? ProductionMotionCatalog.RunwayGen4Turbo : modelId.Trim();
        var row = await _readiness.GetIssuedPermitAsync(buildId, shotCode, act, provider, model, ct)
            ?? await _readiness.GetLatestPermitAsync(buildId, shotCode, act, provider, model, ct);
        if (row is null) return NotFound();
        return Ok(ToPermitDto(row));
    }

    [HttpGet("ai-catalog")]
    public async Task<ActionResult<ProductionAiCatalogDto>> GetAiCatalog(CancellationToken ct)
    {
        var video = new List<ProductionAiCatalogModelDto>();
        var voice = new List<ProductionAiCatalogModelDto>();
        var lipSync = new List<ProductionAiCatalogModelDto>();

        foreach (var model in ProductionMotionCatalog.All.Where(m => m.ProviderId != ProductionMotionCatalog.FixtureProviderId))
        {
            var status = await ResolveCredentialStatusAsync(model.ProviderId, model.ModelId, ct);
            video.Add(new ProductionAiCatalogModelDto(
                model.ProviderId, model.ModelId, model.DisplayName, model.Capability.ToString(),
                model.Action, model.Enabled, model.IsPaid, status));
        }

        foreach (var model in ProductionVoiceCatalog.All.Where(m => m.ProviderId != ProductionVoiceCatalog.FixtureProviderId))
        {
            var status = await ResolveCredentialStatusAsync(model.ProviderId, model.ModelId, ct);
            voice.Add(new ProductionAiCatalogModelDto(
                model.ProviderId, model.ModelId, model.DisplayName, model.Capability.ToString(),
                model.Action, model.Enabled, model.IsPaid, status));
        }

        foreach (var model in ProductionLipsyncCatalog.All.Where(m => m.ProviderId != ProductionLipsyncCatalog.FixtureProviderId))
        {
            var status = await ResolveCredentialStatusAsync(model.ProviderId, model.ModelId, ct);
            lipSync.Add(new ProductionAiCatalogModelDto(
                model.ProviderId, model.ModelId, model.DisplayName, model.Capability.ToString(),
                model.Action, model.Enabled, model.IsPaid, status));
        }

        var image = new List<ProductionAiCatalogModelDto>();
        foreach (var model in ProductionImageCatalog.All)
        {
            var status = await ResolveCredentialStatusAsync(model.ProviderId, model.ModelId, ct);
            image.Add(new ProductionAiCatalogModelDto(
                model.ProviderId, model.ModelId, model.DisplayName, model.Capability.ToString(),
                model.Action, model.Enabled, model.IsPaid, status));
        }

        return Ok(new ProductionAiCatalogDto(video, voice, lipSync, image));
    }

    [HttpGet("tenant-ai-config")]
    public async Task<ActionResult<ProductionTenantAiSettingsDto>> GetTenantAiConfig(CancellationToken ct)
    {
        var views = await _tenantAi.GetAsync(ct);
        return Ok(new ProductionTenantAiSettingsDto(views.Select(ToTenantAiDto).ToList()));
    }

    [HttpPut("tenant-ai-config/{capability}")]
    public async Task<ActionResult<ProductionTenantAiConfigDto>> SaveTenantAiConfig(
        string capability,
        [FromBody] SaveProductionTenantAiConfigRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var row = await _tenantAi.SaveAsync(new SaveProductionTenantAiConfigCommand(
                capability, body.ProviderId, body.ModelId, body.CredentialId, body.Enabled,
                User.Identity?.Name ?? "admin"), ct);
            return Ok(ToTenantAiConfigDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("tenant-ai-config/{capability}")]
    public async Task<ActionResult> ClearTenantAiConfig(string capability, CancellationToken ct)
    {
        try
        {
            await _tenantAi.ClearAsync(capability, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("credentials/backup-gate")]
    public ActionResult<ProductionCredentialBackupGateView> GetCredentialBackupGate() =>
        Ok(_keys.DescribeWriteGate());

    [HttpGet("credentials")]
    public async Task<ActionResult<IReadOnlyList<ProductionCredentialCatalogDto>>> ListCredentials(CancellationToken ct)
    {
        var rows = await _credentials.ListAsync(ct);
        return Ok(rows.Select(ToCatalogDto).ToList());
    }

    [HttpPost("credentials")]
    public async Task<ActionResult<ProductionCredentialRegistrationDto>> RegisterCredential(
        [FromBody] RegisterProductionCredentialRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var row = await _credentials.RegisterAsync(new RegisterProductionCredentialCommand(
                body.ProviderId, body.ModelId, body.Secret, body.DisplayName), ct);
            return Ok(ToCredentialDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return CredentialRejected(ex);
        }
    }

    [HttpPost("credentials/rotate")]
    public async Task<ActionResult<ProductionCredentialRotationDto>> RotateCredential(
        [FromBody] RotateProductionCredentialRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var row = await _credentials.RotateActiveAsync(new RotateProductionCredentialCommand(
                body.ProviderId, body.ModelId, body.Secret), ct);
            return Ok(new ProductionCredentialRotationDto(row.ProviderId, row.ModelId, row.Status, row.Version));
        }
        catch (InvalidOperationException ex)
        {
            return CredentialRejected(ex);
        }
    }

    BadRequestObjectResult CredentialRejected(InvalidOperationException ex)
    {
        if (ex.Message == ProductionCredentialKeyBackupGate.BackupRequired)
            _log.LogInformation("Credential write rejected because the encryption-key backup gate is closed.");
        return new BadRequestObjectResult(
            ProductionCredentialActionableErrors.Describe(ex.Message, HttpContext.TraceIdentifier));
    }

    [HttpPost("credentials/{credentialId:guid}/versions")]
    public async Task<ActionResult<ProductionCredentialRegistrationDto>> AddCredentialVersion(
        Guid credentialId,
        [FromBody] AddProductionCredentialVersionRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var row = await _credentials.AddVersionAsync(new AddProductionCredentialVersionCommand(
                credentialId, body.ProviderId, body.ModelId, body.Secret), ct);
            return Ok(ToCredentialDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("credentials/{credentialVersionId:guid}/revoke")]
    public async Task<ActionResult<ProductionCredentialRegistrationDto>> RevokeCredentialVersion(
        Guid credentialVersionId, CancellationToken ct)
    {
        try
        {
            var row = await _credentials.RevokeVersionAsync(credentialVersionId, ct);
            return Ok(ToCredentialDto(row));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/create-video-input-preview")]
    public async Task<ActionResult<CreateVideoInputPreviewDto>> GetCreateVideoInputPreview(
        Guid buildId, string shotCode, [FromQuery] string? episodeCode, [FromQuery] string? sceneCode,
        CancellationToken ct)
    {
        var scope = Scope(buildId, episodeCode, sceneCode, shotCode);
        var verdict = await _createVideo.EvaluateCreateVideoAsync(scope, null, ct);
        var selection = await _readiness.GetSelectionAsync(buildId, shotCode, ProductionAiSelectionStages.Video, ct);
        var permit = await _readiness.GetIssuedPermitAsync(
            buildId, shotCode, ProductionActions.CreateVideo,
            selection?.ProviderId ?? verdict.ProviderId,
            selection?.ModelId ?? verdict.ModelId, ct);
        var preview = _readiness.Preview(scope, verdict, selection, permit);
        return Ok(new CreateVideoInputPreviewDto(
            preview.BuildId, preview.EpisodeCode, preview.SceneCode, preview.ShotCode,
            preview.KeyframeArtifactId, preview.KeyframeSha256, preview.Story, preview.ActingBeat,
            preview.VisualContract, preview.Timing, preview.DurationSec, preview.TimingStatus,
            preview.PerformanceSnapshotId, preview.PerformanceHash, preview.ProviderId, preview.ModelId,
            preview.SelectionId, preview.SelectionMode, preview.PermitId, preview.PermitStatus,
            preview.PermitScope, preview.CreateVideoAvailable, preview.BlockReason,
            preview.SpeechStartSec, preview.SpeechEndSec));
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/preview")]
    public async Task<ActionResult<ProductionPreviewDto>> GetPreview(Guid buildId, string shotCode, CancellationToken ct)
    {
        var preview = await _createVideo.GetPreviewAsync(Scope(buildId, "", "", shotCode), ct);
        if (preview.IsEmpty)
            return Ok(new ProductionPreviewDto(true, preview.Reason ?? ProductionFailureReasons.VisibleMissing, null, null, null, null));
        return Ok(new ProductionPreviewDto(
            false, null, preview.ArtifactId, preview.ByteSha256, preview.StorageKey,
            $"/api/content/production/shots/{buildId}/{Uri.EscapeDataString(shotCode)}/preview/media",
            preview.Role, preview.MimeType, preview.DurationSec));
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/authoritative-video")]
    public async Task<ActionResult<AuthoritativeVideoTakeDto>> GetAuthoritativeVideo(Guid buildId, string shotCode, CancellationToken ct)
    {
        var take = await _createVideo.GetAuthoritativeVideoTakeAsync(Scope(buildId, "", "", shotCode), ct);
        if (take is null)
            return Ok(new AuthoritativeVideoTakeDto(
                true, ProductionFailureReasons.OutputArtifactNotFound, null, null, null, null, null, null, null,
                null, null, null, null, null, null));
        return Ok(new AuthoritativeVideoTakeDto(
            false, null, take.ArtifactId, take.ArtifactRole, take.AcceptanceStatus, take.VerificationStatus,
            take.ByteSha256, take.MimeType, take.ByteLength, take.TaskId, take.AttemptId, take.SnapshotId,
            take.ProviderId, take.ModelId,
            $"/api/content/production/shots/{buildId}/{Uri.EscapeDataString(shotCode)}/preview/media"));
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/voice")]
    public async Task<ActionResult<ProductionVoiceArtifactDto>> GetCurrentVoice(
        Guid buildId, string shotCode, [FromQuery] string? lineId, CancellationToken ct)
    {
        try
        {
            var voice = await _createVideo.GetCurrentVoiceAsync(Scope(buildId, "", "", shotCode), lineId ?? "", ct);
            if (voice.IsEmpty)
                return Ok(new ProductionVoiceArtifactDto(
                    true, voice.Reason ?? ProductionFailureReasons.MissingDependency, null, null, null, null, null, null,
                    null, null, null, null, null, null, lineId));
            return Ok(new ProductionVoiceArtifactDto(
                false, null, voice.ArtifactId, voice.TaskId, voice.AttemptId, voice.InputSnapshotHash,
                voice.ProviderId, voice.ModelId, voice.ByteSha256, voice.StorageKey,
                $"/api/content/production/shots/{buildId}/{Uri.EscapeDataString(shotCode)}/voice/media?lineId={Uri.EscapeDataString(voice.LineId ?? lineId ?? "")}",
                voice.Role, voice.MimeType, voice.DurationSec, voice.LineId));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/voice/media")]
    public async Task<IActionResult> GetCurrentVoiceMedia(
        Guid buildId, string shotCode, [FromQuery] string? lineId, CancellationToken ct)
    {
        try
        {
            var voice = await _createVideo.GetCurrentVoiceAsync(Scope(buildId, "", "", shotCode), lineId ?? "", ct);
            if (voice.IsEmpty || string.IsNullOrWhiteSpace(voice.StorageKey))
                return NotFound(new { message = voice.Reason ?? ProductionFailureReasons.MissingDependency });
            var bytes = await _createVideo.ReadCurrentVoiceMediaAsync(Scope(buildId, "", "", shotCode), lineId ?? "", ct);
            if (bytes is null || bytes.Length < 32)
                return NotFound(new { message = ProductionFailureReasons.MissingDependency });
            return File(bytes, string.IsNullOrWhiteSpace(voice.MimeType) ? "application/octet-stream" : voice.MimeType);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/keyframe")]
    public async Task<ActionResult<ProductionPreviewDto>> GetCurrentKeyframe(
        Guid buildId, string shotCode, CancellationToken ct)
    {
        var frame = await _createVideo.GetCurrentKeyframeAsync(Scope(buildId, "", "", shotCode), ct);
        if (frame.IsEmpty)
            return Ok(new ProductionPreviewDto(true, frame.Reason ?? ProductionFailureReasons.MissingKeyframe, null, null, null, null));
        return Ok(new ProductionPreviewDto(
            false, null, frame.ArtifactId, frame.ByteSha256, null,
            $"/api/content/production/shots/{buildId}/{Uri.EscapeDataString(shotCode)}/keyframe/media",
            frame.Role, frame.MimeType, frame.DurationSec));
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/keyframe/media")]
    public async Task<IActionResult> GetCurrentKeyframeMedia(Guid buildId, string shotCode, CancellationToken ct)
    {
        var scope = Scope(buildId, "", "", shotCode);
        var frame = await _createVideo.GetCurrentKeyframeAsync(scope, ct);
        if (frame.IsEmpty)
            return NotFound(new { message = frame.Reason ?? ProductionFailureReasons.MissingKeyframe });
        var bytes = await _createVideo.ReadCurrentKeyframeMediaAsync(scope, ct);
        if (bytes is null || bytes.Length < 32)
            return NotFound(new { message = ProductionFailureReasons.FailedVerification });
        return File(bytes, string.IsNullOrWhiteSpace(frame.MimeType) ? "image/png" : frame.MimeType);
    }

    [HttpGet("shots/{buildId:guid}/{shotCode}/preview/media")]
    public async Task<IActionResult> GetPreviewMedia(Guid buildId, string shotCode, CancellationToken ct)
    {
        var preview = await _createVideo.GetPreviewAsync(Scope(buildId, "", "", shotCode), ct);
        if (preview.IsEmpty || string.IsNullOrWhiteSpace(preview.StorageKey))
            return NotFound(new { message = preview.Reason ?? ProductionFailureReasons.VisibleMissing });
        var bytes = await _createVideo.ReadPreviewMediaAsync(Scope(buildId, "", "", shotCode), ct);
        if (bytes is null || bytes.Length < 32)
            return NotFound(new { message = ProductionFailureReasons.VisibleMissing });
        return File(bytes, string.IsNullOrWhiteSpace(preview.MimeType) ? "application/octet-stream" : preview.MimeType);
    }

    static ProductionAiSelectionDto ToSelectionDto(ProductionAiSelection row) =>
        new(row.SelectionId, row.BuildId, row.ShotCode, row.EpisodeCode, row.SceneCode, row.Stage,
            row.ProviderId, row.ModelId, row.SelectionMode, row.SelectedBy, row.SelectedAt,
            row.Reason, row.Status, row.SelectionHash);

    static ProductionExecutionPermitDto ToPermitDto(ProductionExecutionPermit row) =>
        new(row.PermitId, row.BuildId, row.ShotCode, row.EpisodeCode, row.SceneCode, row.Action,
            row.ProviderId, row.ModelId, row.Status, row.IssuedBy, row.IssuedAt, row.ConsumedAt,
            row.Reason, row.PermitHash);

    static ProductionCredentialRegistrationDto ToCredentialDto(ProductionCredentialRegistrationResult row) =>
        new(row.CredentialId, row.CredentialVersionId, row.ProviderId, row.ModelId, row.Status, row.Version);

    static ProductionTenantAiConfigDto ToTenantAiConfigDto(ProductionTenantAiConfig row) =>
        new(row.ProviderId, row.ModelId, row.CredentialId, row.Enabled, row.UpdatedBy, row.UpdatedAt);

    static ProductionTenantAiCapabilityDto ToTenantAiDto(ProductionTenantAiCapabilityView view) =>
        new(
            view.Capability,
            view.Config is null ? null : ToTenantAiConfigDto(view.Config),
            view.Catalog.Select(e => new ProductionTenantAiCatalogEntryDto(
                e.ProviderId, e.ModelId, e.DisplayName, e.IsPaid, e.RequiresCredential)).ToList(),
            view.Credentials.Select(c => new ProductionTenantAiCredentialOptionDto(
                c.CredentialId, c.DisplayName, c.ProviderId, c.ModelId, c.Version)).ToList());

    static ProductionCredentialCatalogDto ToCatalogDto(ProductionCredentialCatalogEntry row) =>
        new(row.CredentialId, row.CredentialVersionId, row.ProviderId, row.ModelId,
            row.Status, row.IdentityStatus, row.Version, row.CreatedAt);

    static ProductionTestRunDto ToTestDto(ProductionTestFoundation row) =>
        new(
            row.Run.TestRunId,
            row.Run.Name,
            row.Run.Status,
            row.Run.CreatedAt,
            row.Anchor.ScopeId,
            row.Anchor.Kind,
            row.Anchor.SeriesBuildId,
            row.Shot.TestShotId,
            row.Shot.EpisodeCode,
            row.Shot.SceneCode,
            row.Shot.ShotCode);

    async Task<CreateVideoSliceResult> ExecuteOsActionAsync(
        string action, ProductionScope scope, ProductionCreateVideoRequestDto body, CancellationToken ct)
    {
        var command = ActionCommand(scope, body, User.Identity?.Name ?? "admin");
        return action switch
        {
            ProductionActions.CreateImage => await _createVideo.CreateImageAsync(command, ct),
            ProductionActions.CreateVoice => await _createVideo.CreateVoiceAsync(command, ct),
            ProductionActions.CreateLipsync => await _createVideo.CreateLipsyncAsync(command, ct),
            ProductionActions.MixVideo => await _createVideo.MixVideoAsync(command, ct),
            ProductionActions.Accept => await _createVideo.AcceptOsAsync(command, ct),
            ProductionActions.Regenerate => await _createVideo.RegenerateOsAsync(command, ct),
            _ => throw new InvalidOperationException(ProductionFailureReasons.ActionRemapForbidden),
        };
    }

    static ProductionActionApiCommand ActionCommand(ProductionScope scope, ProductionCreateVideoRequestDto body, string createdBy)
    {
        byte[]? bytes = null;
        string? mime = null;
        if (!string.IsNullOrWhiteSpace(body.KeyframeDataUrl))
            (bytes, mime) = ReadDataUrl(body.KeyframeDataUrl);
        var parents = new List<Guid>();
        if (body.KeyframeArtifactId is { } kf) parents.Add(kf);
        if (body.MotionArtifactId is { } mot) parents.Add(mot);
        if (body.VoiceArtifactId is { } vox) parents.Add(vox);
        if (body.LipsyncArtifactId is { } lip) parents.Add(lip);
        if (body.AcceptArtifactId is { } acc) parents.Add(acc);
        var shots = body.AssemblyArtifactIds is { Length: > 0 }
            ? body.AssemblyArtifactIds.Select((id, i) => (
                body.AssemblyShotCodes is { } codes && i < codes.Length ? codes[i] : scope.ShotCode,
                id,
                body.DurationSec > 0 ? body.DurationSec : 5m)).ToArray()
            : null;
        var input = ToShotInput(body, parents.Count > 0 ? parents : null);
        return new ProductionActionApiCommand(
            scope, input, createdBy, body.IdempotencyKey, bytes, mime, shots, body.AuthorizePaidNetwork,
            ImageSize: body.ImageSize);
    }

    static ShotExecutionInput ToShotInput(ProductionCreateVideoRequestDto body, IReadOnlyList<Guid>? parents) =>
        new(
            body.Story, body.ActingBeat, body.VisualContract, "",
            "", body.Timing, "", "",
            body.KeyframeArtifactId, "", body.AspectRatio, body.DurationSec,
            ShotExecutionCompiler.Version, parents,
            body.LineId ?? "", body.SpeakerCharacterId ?? "", body.VoiceId ?? "",
            body.MotionArtifactId, body.VoiceArtifactId, body.LipsyncArtifactId, body.AcceptArtifactId,
            body.PerformanceSnapshotId, body.PerformanceHash,
            PictureStrategy: body.PictureStrategy ?? "",
            SpeechDelivery: body.SpeechDelivery ?? "",
            StrategyConfirmed: body.StrategyConfirmed,
            DirectSpeakerCount: body.DirectSpeakerCount);

    static PerformanceProjectionSource ToSource(PerformanceDraftRequestDto body) =>
        new(
            body.BuildId,
            body.ShotCode,
            body.EpisodeCode,
            body.SceneCode,
            body.CharacterIds ?? [],
            body.DialogueSegmentIds ?? [],
            (body.Lines ?? []).Select(l => new LineProjectionSource(
                l.LineId, l.CharacterId, l.Text, l.Emotion, l.Intensity, l.Pace, l.Volume,
                l.PauseSec, l.Label, l.Emphasis, l.Locked)).ToArray(),
            body.ActingBeat is null ? null : new ActingBeatSource(
                body.ActingBeat.BeforeAction, body.ActingBeat.BeforeBody, body.ActingBeat.BeforeProp,
                body.ActingBeat.BeforeGaze, body.ActingBeat.BeforeRoom, body.ActingBeat.DuringEmotion,
                body.ActingBeat.DuringIntensity, body.ActingBeat.AfterAction, body.ActingBeat.AfterGaze),
            body.GazeTarget,
            body.OffFrameCharacterIds,
            body.VoiceOnlyCharacterIds,
            body.PrimaryCharacterIds,
            body.SecondaryCharacterIds,
            null,
            body.TimingHash,
            body.VisualAuthorityId,
            body.VisualContractHash,
            body.StoryTextHash,
            body.BeatId);

    static PerformanceSnapshotDto ToPerformanceDto(PerformanceSnapshot snap, PerformanceValidation verdict) =>
        new(
            snap.PerformanceSnapshotId,
            snap.PerformanceHash,
            snap.FrozenAt,
            snap.CreatedBy,
            snap.Draft.BuildId,
            snap.Draft.ShotCode,
            snap.Draft.EpisodeCode,
            snap.Draft.SceneCode,
            snap.PayloadJson,
            verdict.VoiceReady,
            verdict.Issues.Select(i => i.Code).ToArray(),
            snap.Source.ProductionDurationSec,
            snap.Source.TimingSource,
            snap.Source.DecisionType,
            snap.Draft.TimingRef.TimingHash,
            snap.Source.KeyframeArtifactId,
            snap.Source.SpeechStartSec,
            snap.Source.SpeechEndSec);

    static ProductionScope Scope(Guid buildId, string? episode, string? scene, string shot)
    {
        var episodeCode = (episode ?? "").Trim();
        var shotCode = (shot ?? "").Trim();
        if (episodeCode.Length == 0
            && shotCode.StartsWith("EP01-", StringComparison.Ordinal)
            && !ProductionScopeAnchorRules.IsOfficialFilm(buildId))
            episodeCode = "EP01";
        return new ProductionScope(buildId, episodeCode, (scene ?? "").Trim(), shotCode);
    }

    static CreateVideoReadinessClaim Claim(
        string? story, string? actingBeat, string? visualContract, string? timing, Guid? keyframeArtifactId,
        Guid? performanceSnapshotId = null, string? performanceHash = null,
        string? providerId = null, string? modelId = null,
        string? pictureStrategy = null, string? speechDelivery = null,
        bool strategyConfirmed = false, int directSpeakerCount = 0) =>
        new(story, actingBeat, visualContract, timing, null, keyframeArtifactId,
            ProviderId: providerId, ModelId: modelId,
            PerformanceSnapshotId: performanceSnapshotId, PerformanceHash: performanceHash,
            PictureStrategy: pictureStrategy, SpeechDelivery: speechDelivery,
            StrategyConfirmed: strategyConfirmed, DirectSpeakerCount: directSpeakerCount);

    static ProductionCreateVideoResultDto ToDto(CreateVideoSliceResult result, Guid snapshotId) =>
        new(
            result.Request.RequestId,
            result.Task.TaskId,
            snapshotId,
            result.Attempt.AttemptId,
            result.Request.UserAction,
            result.Attempt.ProviderId,
            result.Attempt.ModelId,
            result.Attempt.ProviderJobId,
            result.Artifact.ArtifactId,
            result.Artifact.ByteSha256,
            result.Artifact.StorageKey,
            result.Current?.BindingId,
            result.Visible?.BindingId,
            "visible");

    async Task<string> ResolveCredentialStatusAsync(string providerId, string modelId, CancellationToken ct)
    {
        var matches = await _credentialStore.ListEligibleAsync(providerId, modelId, ct);
        return matches.Count switch
        {
            0 => "NOT_CONFIGURED",
            1 => "ACTIVE",
            _ => "AMBIGUOUS"
        };
    }

    static (byte[] Bytes, string Mime) ReadDataUrl(string? dataUrl)
    {
        var raw = (dataUrl ?? "").Trim();
        var comma = raw.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (comma < 0) throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var meta = raw[..comma];
        var mime = "application/octet-stream";
        var slash = meta.IndexOf(':');
        var plus = meta.IndexOf(';');
        if (slash >= 0 && plus > slash) mime = meta[(slash + 1)..plus];
        var bytes = Convert.FromBase64String(raw[(comma + 7)..]);
        if (bytes.Length < 32) throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return (bytes, mime);
    }
}
