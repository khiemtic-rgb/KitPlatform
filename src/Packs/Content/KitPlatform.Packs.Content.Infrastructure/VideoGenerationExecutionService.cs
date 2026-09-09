using System.Globalization;
using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class VideoGenerationExecutionService : IVideoGenerationExecutionService
{
    private readonly VideoGenerationExecutionRepository _repo;
    private readonly ProductionVideoContractRepository _videos;
    private readonly ProductionShotContractRepository _contracts;
    private readonly ProductionPromptCompilerRepository _prompts;
    private readonly ImageGenerationContractRepository _igc;
    private readonly ImageGenerationExecutionRepository _stills;
    private readonly ImageGenerationDirectorReviewRepository _reviews;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly ICharacterIdentityGovernanceService _governance;
    private readonly IVideoGenerationProvider _provider;
    private readonly KitVideoArtifactStore _images;
    private readonly KitVideoVideoStore _artifacts;
    private readonly IVisualUniverseSnapshotResolver _snapshots;
    private readonly IUnifiedVisualCompiler _compiler;

    public VideoGenerationExecutionService(
        VideoGenerationExecutionRepository repo,
        ProductionVideoContractRepository videos,
        ProductionShotContractRepository contracts,
        ProductionPromptCompilerRepository prompts,
        ImageGenerationContractRepository igc,
        ImageGenerationExecutionRepository stills,
        ImageGenerationDirectorReviewRepository reviews,
        KitVideoProductionShotRepository shots,
        ICharacterIdentityGovernanceService governance,
        IVideoGenerationProvider provider,
        KitVideoArtifactStore images,
        KitVideoVideoStore artifacts,
        IVisualUniverseSnapshotResolver snapshots,
        IUnifiedVisualCompiler compiler)
    {
        _repo = repo;
        _videos = videos;
        _contracts = contracts;
        _prompts = prompts;
        _igc = igc;
        _stills = stills;
        _reviews = reviews;
        _shots = shots;
        _governance = governance;
        _provider = provider;
        _images = images;
        _artifacts = artifacts;
        _snapshots = snapshots;
        _compiler = compiler;
    }

    public IReadOnlyList<string> RunRegression() => VideoGenerationExecutionV1Regression.Run();

    public IReadOnlyList<(string Suite, IReadOnlyList<string> Failures)> RunParkRegressions() =>
    [
        (CharacterIdentityGovernanceV11Regression.SuiteId, CharacterIdentityGovernanceV11Regression.Run()),
        (ProductionShotContractV1Regression.SuiteId, ProductionShotContractV1Regression.Run()),
        (ProductionPromptCompilerV1Regression.SuiteId, ProductionPromptCompilerV1Regression.Run()),
        (ProductionContractPromptV2LifecycleRegression.SuiteId, ProductionContractPromptV2LifecycleRegression.Run()),
        (ImageGenerationContractV1Regression.SuiteId, ImageGenerationContractV1Regression.Run()),
        (ImageGenerationExecutionV1Regression.SuiteId, ImageGenerationExecutionV1Regression.Run()),
        (FirstRealProductionV1Regression.SuiteId, FirstRealProductionV1Regression.Run()),
        (ImageGenerationDirectorReviewV1Regression.SuiteId, ImageGenerationDirectorReviewV1Regression.Run()),
        (ProductionVideoContractV1Regression.SuiteId, ProductionVideoContractV1Regression.Run()),
        (VideoGenerationExecutionV1Regression.SuiteId, VideoGenerationExecutionV1Regression.Run()),
        (FamixaProviderLifecycleParityV1Regression.SuiteId, FamixaProviderLifecycleParityV1Regression.Run()),
        (FamixaProviderRoutingFoundationV1Regression.SuiteId, FamixaProviderRoutingFoundationV1Regression.Run()),
        (FamixaProviderRouterV1Regression.SuiteId, FamixaProviderRouterV1Regression.Run()),
        (FamixaRuntimeMaxCostV1Regression.SuiteId, FamixaRuntimeMaxCostV1Regression.Run()),
        (FamixaExecutionProvenanceV1Regression.SuiteId, FamixaExecutionProvenanceV1Regression.Run()),
        (ProductionOsArchitectureV1Regression.SuiteId, ProductionOsArchitectureV1Regression.Run()),
        (CharacterReferencePackV1Regression.SuiteId, CharacterReferencePackV1Regression.Run()),
        (CharacterReferenceCompletionV1Regression.SuiteId, CharacterReferenceCompletionV1Regression.Run()),
        (CharacterReferenceGenerationV1Regression.SuiteId, CharacterReferenceGenerationV1Regression.Run()),
        (CharacterProductionLibraryV1Regression.SuiteId, CharacterProductionLibraryV1Regression.Run()),
        (ProductionWorkflowHardeningV1Regression.SuiteId, ProductionWorkflowHardeningV1Regression.Run()),
        (ProductionProgressV1Regression.SuiteId, ProductionProgressV1Regression.Run()),
        (ProjectVisualStyleV1Regression.SuiteId, ProjectVisualStyleV1Regression.Run()),
        (CharacterStudioV1Regression.SuiteId, CharacterStudioV1Regression.Run()),
        (CharacterStudioUnifiedGenerationV1Regression.SuiteId, CharacterStudioUnifiedGenerationV1Regression.Run()),
        (CharacterAgeConsistencyV1Regression.SuiteId, CharacterAgeConsistencyV1Regression.Run()),
        (CharacterAgeGenerationIntegrationV1Regression.SuiteId, CharacterAgeGenerationIntegrationV1Regression.Run()),
        (CharacterAgeGateV1Regression.SuiteId, CharacterAgeGateV1Regression.Run()),
        (VisualUniverseSnapshotResolverV1Regression.SuiteId, VisualUniverseSnapshotResolverV1Regression.Run()),
        (UnifiedVisualContractV1Regression.SuiteId, UnifiedVisualContractV1Regression.Run()),
        (UnifiedVisualCompilerV1Regression.SuiteId, UnifiedVisualCompilerV1Regression.Run()),
        (CharacterFirstMasterVisualIngressV1Regression.SuiteId, CharacterFirstMasterVisualIngressV1Regression.Run()),
        (SeriesStillVisualIngressV1Regression.SuiteId, SeriesStillVisualIngressV1Regression.Run()),
        (VideoVisualIngressV1Regression.SuiteId, VideoVisualIngressV1Regression.Run()),
        (VideoAudioLipsyncPipelineV1Regression.SuiteId, VideoAudioLipsyncPipelineV1Regression.Run()),
        (VisualUniverseLockReadinessV1Regression.SuiteId, VisualUniverseLockReadinessV1Regression.Run()),
        (VisualFoundationFinalizationV1Regression.SuiteId, VisualFoundationFinalizationV1Regression.Run()),
        (IdentityConditionedCalibrationV1Regression.SuiteId, IdentityConditionedCalibrationV1Regression.Run()),
        (IdentityConditionedCalibrationLiveV1Regression.SuiteId, IdentityConditionedCalibrationLiveV1Regression.Run()),
        (IdentityConditionedCalibrationDirectorReviewV1Regression.SuiteId, IdentityConditionedCalibrationDirectorReviewV1Regression.Run()),
        (IdentityConditionedCalibrationDirectorReviewUiV1Regression.SuiteId, IdentityConditionedCalibrationDirectorReviewUiV1Regression.Run()),
    ];

    public async Task<VideoGenerationExecutionDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, "director", cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        return row is null ? Preview(ctx) : ToDto(row, ctx, []);
    }

    public async Task<VideoGenerationExecutionDto> PreflightAsync(Guid shotId, string actor, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        await _repo.InsertEventAsync(null, shotId,
            ctx.Preflight.Status == "PASS" ? "VIDEO_PREFLIGHT_PASSED" : "VIDEO_PREFLIGHT_BLOCKED",
            new { status = ctx.Preflight.Status, runProvider = ctx.Preflight.RunProvider, fingerprint = ctx.Preflight.Fingerprint, generate = false },
            actor, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        return row is null ? Preview(ctx) : ToDto(row, ctx, ctx.Preflight.Blocks.Select(Map).ToList());
    }

    public async Task<VideoGenerationExecutionDto> ExecuteAsync(Guid shotId, string actor, bool confirm = false, CancellationToken cancellationToken = default)
    {
        if (!confirm)
            throw new VideoGenerationExecutionException(
                VideoVisualIngressV1Rules.GateConfirm,
                "Director confirmation required. Video generation dry-run does not call the provider.",
                "VISUAL_UNIVERSE", "confirm");
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        var compiled = await CompileBoundAsync(ctx, cancellationToken);
        await _repo.InsertEventAsync(null, shotId, "VIDEO_PREFLIGHT_REQUESTED",
            new { status = ctx.Preflight.Status, generate = false }, actor, cancellationToken);
        if (ctx.Preflight.Status != "PASS" || !ctx.Preflight.RunProvider
            || ctx.Video is null || ctx.Contract is null || ctx.Prompt is null || ctx.Igc is null || ctx.Still is null)
        {
            await _repo.InsertEventAsync(null, shotId, "VIDEO_PREFLIGHT_BLOCKED",
                new { blocks = ctx.Preflight.Blocks, generate = false }, actor, cancellationToken);
            throw Fail(ctx);
        }

        var fingerprint = ctx.Preflight.Fingerprint!;
        var existing = await _repo.GetByFingerprintAsync(fingerprint, cancellationToken);
        if (existing is not null && (VideoGenerationExecutionRules.DoNotBlindRetry(existing.ExecutionStatus)
            || VideoGenerationExecutionRules.IsTerminalSuccess(existing.ExecutionStatus)))
        {
            await _repo.InsertEventAsync(existing.Id, shotId, "VIDEO_GENERATION_FAILED",
                new { code = "DO_NOT_BLIND_RETRY", fingerprint, status = existing.ExecutionStatus, generate = false }, actor, cancellationToken);
            return ToDto(existing, ctx, []);
        }
        if (existing is not null && VideoGenerationExecutionRules.IsInFlight(existing.ExecutionStatus))
        {
            if (!string.IsNullOrWhiteSpace(existing.ProviderRequestId))
                return await FinishAsync(existing, ctx, actor, await _provider.ResumeAsync(existing.ProviderRequestId, cancellationToken), cancellationToken);
            if (!VideoGenerationExecutionRules.IsOrphanedInFlight(
                    existing.ExecutionStatus, existing.ProviderRequestId, existing.RequestedAt, DateTimeOffset.UtcNow))
            {
                await _repo.InsertEventAsync(existing.Id, shotId, "VIDEO_GENERATION_REQUESTED",
                    new { fingerprint, generate = false, reason = "IN_FLIGHT_NO_SECOND_CALL" }, actor, cancellationToken);
                return ToDto(existing, ctx, []);
            }
            existing.RequestedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = actor;
            existing = await _repo.UpdateAsync(existing, cancellationToken);
            await _repo.InsertEventAsync(existing.Id, shotId, "VIDEO_GENERATION_REQUESTED",
                new { fingerprint, generate = true, reason = "ORPHAN_RECOVER" }, actor, cancellationToken);
        }

        var id = existing?.Id ?? Guid.NewGuid();
        var row = existing ?? new VideoGenerationExecutionRepository.Row
        {
            Id = id,
            ShotId = shotId,
            VideoContractId = ctx.Video.Id,
            StillExecutionId = ctx.Still.Id,
            ShotContractId = ctx.Contract.Id,
            PromptId = ctx.Prompt.Id,
            IgcId = ctx.Igc.Id,
            SeriesId = ctx.Video.SeriesId,
            CharacterId = ctx.Shot.CharacterId,
            EraId = ctx.Shot.EraId,
            ExecutionStatus = "REQUESTED",
            Provider = _provider.ProviderId,
            ProviderStatus = "REQUESTED",
            ProviderConfigVersion = VideoGenerationExecutionRules.ProviderConfigVersion,
            ExecutionFingerprint = fingerprint,
            IdempotencyKey = fingerprint,
            MasterSha256 = ctx.Gov.MasterSha256 ?? "",
            DnaSha256 = ctx.Gov.DnaSha256 ?? "",
            PrpSha256 = ctx.Gov.PrpSha256 ?? "",
            ShotContractSha256 = ctx.LiveContractSha,
            PromptSha256 = ctx.Prompt.PromptSha256,
            IgcSha256 = ctx.Igc.ContractSha256,
            VideoContractSha256 = ctx.Video.ContractSha256,
            StillArtifactSha256 = ctx.Still.ArtifactSha256,
            DurationSeconds = ctx.Duration,
            Resolution = ctx.Resolution,
            Fps = ctx.Fps,
            AspectRatio = ctx.Aspect,
            CreditStatus = "UNKNOWN",
            RequestedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
        };
        if (existing is null)
        {
            row = await _repo.InsertAsync(row, cancellationToken);
            if (row.Id != id)
                return ToDto(row, ctx, []);
        }

        await _repo.InsertEventAsync(row.Id, shotId, "VIDEO_GENERATION_REQUESTED", new { fingerprint, generate = true }, actor, cancellationToken);
        var request = VideoVisualIngressV1Rules.ToProviderRequest(
            compiled,
            ctx.StillBytes!,
            ctx.Still.ArtifactMime,
            ctx.Still.ArtifactSha256,
            ctx.Duration,
            ctx.Resolution,
            ctx.Fps,
            ctx.Aspect,
            VideoGenerationExecutionRules.ProviderConfigVersion);
        var result = await _provider.GenerateAsync(request, cancellationToken);
        return await FinishAsync(row, ctx, actor, result, cancellationToken);
    }

    public async Task<VideoGenerationExecutionDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Chỉ Director được duyệt.", "DIRECTOR", "actor");
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Chưa có execution.", "EXECUTION", "status");
        var qa = ReadQa(row.QaJson);
        if (!VideoGenerationExecutionRules.CanDirectorApprove(row.ExecutionStatus, qa.Technical == "PASS", qa.Identity == "PASS", qa.Continuity == "PASS", qa.P0))
            throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Chưa READY_FOR_DIRECTOR hoặc P0 > 0. Không auto-approve.", "DIRECTOR", "status", row.ExecutionStatus, "READY_FOR_DIRECTOR");
        row.ExecutionStatus = "DIRECTOR_APPROVED";
        row.ApprovedAt = DateTimeOffset.UtcNow;
        row.ApprovedBy = actor;
        row.UpdatedBy = actor;
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "VIDEO_DIRECTOR_APPROVED", new { note, sha = row.ArtifactSha256, generate = false }, actor, cancellationToken);
        return ToDto(row, ctx, []);
    }

    public async Task<VideoGenerationExecutionDto> RejectAsync(Guid shotId, string actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 3)
            throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Director phải nhập lý do reject.", "DIRECTOR", "rejectionReason");
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Chưa có execution.", "EXECUTION", "status");
        if (row.ExecutionStatus == "DIRECTOR_APPROVED")
            throw new VideoGenerationExecutionException("VIDEO_GENERATION_EXECUTION_LOCKED", "DIRECTOR_APPROVED artifact immutable.", "EXECUTION", "status");
        if (row.ExecutionStatus is not ("READY_FOR_DIRECTOR" or "QA_FAILED"))
            throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Chưa tới Director review.", "DIRECTOR", "status", row.ExecutionStatus, "READY_FOR_DIRECTOR");
        row.ExecutionStatus = "DIRECTOR_REJECTED";
        row.RejectedAt = DateTimeOffset.UtcNow;
        row.RejectedBy = actor;
        row.UpdatedBy = actor;
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "VIDEO_DIRECTOR_REJECTED", new { reason = reason.Trim(), generate = false }, actor, cancellationToken);
        return ToDto(row, ctx, []);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadArtifactAsync(Guid shotId, Guid executionId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(executionId, cancellationToken);
        if (row is null || row.ShotId != shotId) return null;
        var bytes = _artifacts.Read(row.ArtifactPath);
        return bytes is null ? null : (bytes, string.IsNullOrWhiteSpace(row.ArtifactMime) ? "video/mp4" : row.ArtifactMime);
    }

    private async Task<VideoGenerationExecutionDto> FinishAsync(
        VideoGenerationExecutionRepository.Row row, Ctx ctx, string actor, VideoGenerationProviderResult result, CancellationToken ct)
    {
        if (!result.Accepted)
        {
            row.ExecutionStatus = "FAILED";
            row.ProviderStatus = "FAILED";
            row.Provider = result.Provider;
            row.CompletedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = actor;
            row = await _repo.UpdateAsync(row, ct);
            await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_FAILED", new { provider = result.Provider, generate = true }, actor, ct);
            return ToDto(row, ctx, []);
        }

        row.ExecutionStatus = "ACCEPTED";
        row.ProviderStatus = "ACCEPTED";
        row.Provider = result.Provider;
        row.ProviderRequestId = result.ProviderRequestId;
        row.AcceptedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = actor;
        row = await _repo.UpdateAsync(row, ct);
        await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_ACCEPTED", new { id = result.ProviderRequestId, generate = true }, actor, ct);

        if (!result.Succeeded || result.Bytes is null || result.Bytes.Length == 0)
        {
            if (result.ProviderStatus == "PROCESSING")
            {
                row.ExecutionStatus = "PROCESSING";
                row.ProviderStatus = "PROCESSING";
                row = await _repo.UpdateAsync(row, ct);
                await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_PROCESSING", new { generate = true }, actor, ct);
                return ToDto(row, ctx, []);
            }
            row.ExecutionStatus = "FAILED";
            row.ProviderStatus = "FAILED";
            row.CompletedAt = DateTimeOffset.UtcNow;
            row = await _repo.UpdateAsync(row, ct);
            await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_FAILED", new { reason = "artifact unavailable", generate = true }, actor, ct);
            return ToDto(row, ctx, []);
        }

        row.ExecutionStatus = "PROCESSING";
        row.ProviderStatus = "PROCESSING";
        row = await _repo.UpdateAsync(row, ct);
        await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_PROCESSING", new { generate = true }, actor, ct);

        var path = _artifacts.PersistProductionExecution(row.ShotId, row.Id, result.Bytes);
        var sha = KitVideoIntegrityRules.Sha256Hex(result.Bytes);
        row.ArtifactPath = path;
        row.ArtifactSha256 = sha;
        row.ArtifactMime = result.Mime ?? "video/mp4";
        row.ExecutionStatus = "SUCCEEDED";
        row.ProviderStatus = "SUCCEEDED";
        row = await _repo.UpdateAsync(row, ct);
        await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_SUCCEEDED", new { sha, generate = true }, actor, ct);

        var duration = KitVideoMotionRules.ReadMp4DurationSec(result.Bytes);
        var qa = VideoGenerationExecutionRules.EvaluateQa(result.Bytes, path, new VideoGenerationExecutionRules.QaObservation(
            Size: result.Bytes.Length, Duration: duration > 0 ? duration : ctx.Duration,
            ArtifactSha256: sha, ExpectedSha256: sha,
            SourceImageSha256: ctx.Still!.ArtifactSha256, ExpectedSourceSha256: ctx.Still.ArtifactSha256,
            MasterSha256: ctx.Gov.MasterSha256, ExpectedMasterSha256: ctx.Gov.MasterSha256,
            DnaSha256: ctx.Gov.DnaSha256, ExpectedDnaSha256: ctx.Gov.DnaSha256,
            PrpSha256: ctx.Gov.PrpSha256, ExpectedPrpSha256: ctx.Gov.PrpSha256,
            VideoContractSha256: ctx.Video!.ContractSha256, ExpectedVideoContractSha256: ctx.Video.ContractSha256,
            PromptSha256: ctx.Prompt!.PromptSha256, ExpectedPromptSha256: ctx.Prompt.PromptSha256,
            Identity: "hold", Continuity: "hold"), ctx.Duration, ctx.Resolution, ctx.Fps, ctx.Aspect);
        row.QaJson = JsonSerializer.Serialize(qa);
        row.CompletedAt = DateTimeOffset.UtcNow;
        if (qa.Overall == "QA_FAILED")
        {
            row.ExecutionStatus = "QA_FAILED";
            row = await _repo.UpdateAsync(row, ct);
            await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_GENERATION_FAILED", new { qa, generate = false }, actor, ct);
            return ToDto(row, ctx, []);
        }
        row.ExecutionStatus = "READY_FOR_DIRECTOR";
        row = await _repo.UpdateAsync(row, ct);
        await _repo.InsertEventAsync(row.Id, row.ShotId, "VIDEO_READY_FOR_DIRECTOR", new { sha, autoApprove = false, generate = false }, actor, ct);
        return ToDto(row, ctx, []);
    }

    private sealed record Ctx(
        KitVideoProductionShotRepository.ShotRow Shot,
        ProductionShotContractRepository.ContractRow? Contract,
        ProductionPromptCompilerRepository.PromptRow? Prompt,
        ImageGenerationContractRepository.Row? Igc,
        ImageGenerationExecutionRepository.Row? Still,
        ImageGenerationDirectorReviewRepository.Row? Review,
        ProductionVideoContractRepository.Row? Video,
        CharacterIdentityGovernanceDto Gov,
        JsonElement VideoPayload,
        string LiveContractSha,
        byte[]? StillBytes,
        double Duration,
        string Resolution,
        string Fps,
        string Aspect,
        string MotionIntent,
        VideoGenerationExecutionRules.PreflightOutput Preflight);

    private async Task<UnifiedVisualContract> CompileBoundAsync(Ctx ctx, CancellationToken ct)
    {
        var (snapshot, snapGate) = await VideoVisualIngressV1Rules.ResolveSnapshotAsync(
            _snapshots, FamixaVisualUniverseAuthorityV1Rules.ProjectId, ct);
        if (snapGate is not null || snapshot is null)
            throw new VideoGenerationExecutionException(
                snapGate ?? VideoVisualIngressV1Rules.GateSnapshot,
                "Visual Universe Snapshot chưa sẵn sàng. Video không được tạo ngoài Visual Universe.",
                "VISUAL_UNIVERSE", "snapshot");
        var compiled = VideoVisualIngressV1Rules.CompileVideo(
            snapshot,
            VideoVisualIngressV1Rules.FromExecution(
                ctx.Shot.CharacterId, ctx.MotionIntent, ctx.Prompt?.PromptText, ctx.Duration, ctx.Aspect),
            _compiler);
        if (compiled.Gate is not null || compiled.Contract is null
            || !VideoVisualIngressV1Rules.ProviderMayCall(compiled.Contract))
            throw new VideoGenerationExecutionException(
                compiled.Gate ?? VideoVisualIngressV1Rules.GateCompile,
                "Unified Visual Compiler blocked Video. No legacy prompt fallback.",
                "VISUAL_UNIVERSE", "compiler");
        return compiled.Contract;
    }

    private async Task<Ctx> LoadAsync(Guid shotId, string actor, CancellationToken ct)
    {
        var shot = await _shots.GetByIdAsync(shotId, ct)
            ?? throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "Production Shot không tồn tại.", "SHOT", "shotId");
        var contract = await _contracts.GetLatestAsync(shotId, ct);
        var liveContractSha = contract is null ? "" : ProductionShotContractRules.HashCanonical(
            JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson));
        var prompt = contract is null ? null : await _prompts.GetCompiledByContractShaAsync(shotId, liveContractSha, ct)
            ?? await _prompts.GetLatestAsync(shotId, ct);
        var igc = await _igc.GetLatestApprovedAsync(shotId, ct) ?? await _igc.GetLatestAsync(shotId, ct);
        var still = await _stills.GetLatestAsync(shotId, ct);
        var review = still is null ? null : await _reviews.GetByExecutionAsync(still.Id, ct);
        var video = await _videos.GetLatestAsync(shotId, ct);
        JsonElement videoPayload = default;
        if (video is not null)
            videoPayload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(video.PayloadJson) ? "{}" : video.PayloadJson);
        var stillBytes = still is null ? null : _images.Read(still.ArtifactPath);
        var liveArt = stillBytes is { Length: > 0 } ? KitVideoIntegrityRules.Sha256Hex(stillBytes) : "";
        var gov = await _governance.ProductionGateAsync(
            shot.CharacterId,
            new CharacterIdentityGovernanceCheckRequest(
                contract is null ? null : ProductionShotContractRules.ToGovernanceShotSpec(
                    JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson)),
                null, shot.Id),
            actor, ct);
        if (gov.Generate)
            throw new VideoGenerationExecutionException(VideoGenerationExecutionRules.Code, "generation=false until execute.", "COMPILER", "generate", "true", "false");

        var output = Obj(videoPayload, "output");
        var timing = Obj(videoPayload, "timing");
        var duration = ReadNumber(output, "duration");
        if (duration <= 0) duration = ReadNumber(timing, "duration");
        var resolution = First(Read(output, "resolution"), "1280x720");
        var fps = First(Read(output, "fps"), First(Read(timing, "fps"), "24"));
        var aspect = First(Read(output, "aspectRatio"), "16:9");
        var motion = VideoGenerationExecutionRules.CompileMotionIntent(videoPayload);
        var reviewStatus = review?.ReviewStatus
            ?? (ImageGenerationDirectorReviewRules.IsImageApproved(still?.ExecutionStatus) ? "APPROVED"
                : ImageGenerationDirectorReviewRules.IsImageRejected(still?.ExecutionStatus) ? "REJECTED" : "PENDING");
        var liveVideoSha = video is null ? "" : video.ContractSha256;
        var preflight = VideoGenerationExecutionRules.EvaluatePreflight(new VideoGenerationExecutionRules.PreflightInput(
            shot.CharacterId, shot.Id.ToString(),
            gov.Master == "LOCKED", gov.Dna == "LOCKED", gov.Prp == "LOCKED",
            gov.Status == "PASS" && gov.ProductionAllowed,
            contract?.ContractStatus ?? "", prompt?.PromptStatus ?? "", igc?.ContractStatus ?? "",
            still?.ExecutionStatus ?? "", reviewStatus, video?.ContractStatus ?? "",
            stillBytes is { Length: > 0 }, stillBytes is { Length: > 0 },
            contract?.MasterSha256 ?? gov.MasterSha256 ?? "", gov.MasterSha256 ?? "",
            contract?.DnaSha256 ?? gov.DnaSha256 ?? "", gov.DnaSha256 ?? "",
            contract?.PrpSha256 ?? gov.PrpSha256 ?? "", gov.PrpSha256 ?? "",
            contract?.ContractSha256 ?? "", liveContractSha,
            prompt?.PromptSha256 ?? "", prompt?.PromptSha256 ?? "",
            igc?.ContractSha256 ?? "", igc?.ContractSha256 ?? "",
            video?.ContractSha256 ?? "", liveVideoSha,
            still?.ArtifactSha256 ?? "", liveArt,
            duration, resolution, fps, aspect, stillBytes is { Length: > 0 },
            VideoGenerationExecutionRules.ProviderConfigVersion));
        return new Ctx(shot, contract, prompt, igc, still, review, video, gov, videoPayload, liveContractSha, stillBytes,
            duration, resolution, fps, aspect, motion, preflight);
    }

    private static VideoGenerationExecutionDto Preview(Ctx ctx) => ToDto(null, ctx, ctx.Preflight.Blocks.Select(Map).ToList());

    private static VideoGenerationExecutionDto ToDto(
        VideoGenerationExecutionRepository.Row? row, Ctx ctx, IReadOnlyList<VideoGenerationExecutionBlockDto> extra)
    {
        var blocks = extra.Count > 0 ? extra : ctx.Preflight.Blocks.Select(Map).ToList();
        var pass = ctx.Preflight.Status == "PASS";
        var approval = row?.ExecutionStatus == "DIRECTOR_APPROVED" ? "APPROVED"
            : row?.ExecutionStatus == "DIRECTOR_REJECTED" ? "REJECTED" : "PENDING";
        var generation = row is not null && VideoGenerationExecutionRules.GenerationAfterComplete(row.ExecutionStatus);
        var url = row is null || string.IsNullOrWhiteSpace(row.ArtifactSha256)
            ? ""
            : $"/api/content/video-engine/shots/{ctx.Shot.Id}/video-generation-execution/{row.Id}/video";
        return new VideoGenerationExecutionDto(
            row?.Id, ctx.Shot.Id, row?.VideoContractId ?? ctx.Video?.Id, ctx.Still?.Id,
            ctx.Video?.SeriesId ?? "FAMIXA", ctx.Shot.CharacterId, ctx.Shot.CharacterName, ctx.Shot.EraId, ctx.Shot.ShotCode,
            VideoGenerationExecutionRules.DocumentId,
            row?.ExecutionStatus ?? (pass ? "PREFLIGHT" : "BLOCKED"),
            row?.Provider ?? "",
            row?.ProviderStatus ?? "",
            row?.ProviderRequestId,
            VideoGenerationExecutionRules.ProviderConfigVersion,
            row?.ExecutionFingerprint ?? ctx.Preflight.Fingerprint ?? "",
            row?.MasterSha256 ?? ctx.Gov.MasterSha256 ?? "",
            row?.DnaSha256 ?? ctx.Gov.DnaSha256 ?? "",
            row?.PrpSha256 ?? ctx.Gov.PrpSha256 ?? "",
            row?.ShotContractSha256 ?? ctx.LiveContractSha,
            row?.PromptSha256 ?? ctx.Prompt?.PromptSha256 ?? "",
            row?.IgcSha256 ?? ctx.Igc?.ContractSha256 ?? "",
            row?.VideoContractSha256 ?? ctx.Video?.ContractSha256 ?? "",
            row?.StillArtifactSha256 ?? ctx.Still?.ArtifactSha256 ?? "",
            row?.DurationSeconds ?? ctx.Duration,
            row?.Resolution ?? ctx.Resolution,
            row?.Fps ?? ctx.Fps,
            row?.AspectRatio ?? ctx.Aspect,
            row?.ArtifactPath, row?.ArtifactSha256 ?? "", row?.ArtifactMime ?? "",
            url, row is not null && !string.IsNullOrWhiteSpace(row.ArtifactPath),
            ReadQaOrNull(row?.QaJson),
            row?.CreditStatus ?? "UNKNOWN", row?.CreditValue,
            row?.RequestedAt, row?.AcceptedAt, row?.CompletedAt, row?.ApprovedAt, row?.ApprovedBy,
            pass, pass && VideoGenerationExecutionRules.CanCallProvider(true),
            generation,
            VideoGenerationExecutionRules.IsTerminalSuccess(row?.ExecutionStatus),
            pass && (row is null
                || VideoGenerationExecutionRules.ShouldExecute(row.ExecutionStatus)
                || VideoGenerationExecutionRules.IsOrphanedInFlight(
                    row.ExecutionStatus, row.ProviderRequestId, row.RequestedAt, DateTimeOffset.UtcNow)),
            row?.ExecutionStatus == "READY_FOR_DIRECTOR",
            row is not null && row.ExecutionStatus is "READY_FOR_DIRECTOR" or "QA_FAILED",
            ctx.Gov.Master, ctx.Gov.Dna, ctx.Gov.Prp, ctx.Gov.GovernanceEngine,
            ctx.Contract?.ContractStatus ?? "MISSING",
            ctx.Prompt?.PromptStatus ?? "MISSING",
            ctx.Igc?.ContractStatus ?? "MISSING",
            ctx.Review?.ReviewStatus ?? "PENDING",
            ctx.Video?.ContractStatus ?? "MISSING",
            approval,
            blocks);
    }

    private static VideoGenerationExecutionException Fail(Ctx ctx)
    {
        var first = ctx.Preflight.Blocks.FirstOrDefault()
            ?? new VideoGenerationExecutionRules.Block("BLOCKED", VideoGenerationExecutionRules.Code, "AUTHORITY", "preflight", "FAIL", "PASS", VideoGenerationExecutionRules.Code);
        return new VideoGenerationExecutionException(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative,
            ctx.Preflight.Blocks.Select(Map).ToList());
    }

    private static VideoGenerationExecutionBlockDto Map(VideoGenerationExecutionRules.Block b) =>
        new(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message);

    private static VideoGenerationExecutionQaDto ReadQa(string? json)
    {
        try
        {
            return JsonSerializer.Deserialize<VideoGenerationExecutionQaDto>(string.IsNullOrWhiteSpace(json) ? "{}" : json)
                ?? new VideoGenerationExecutionQaDto("", "", "", "", "", "", "", "", 0, "", []);
        }
        catch { return new VideoGenerationExecutionQaDto("", "", "", "", "", "", "", "", 0, "", []); }
    }

    private static VideoGenerationExecutionQaDto? ReadQaOrNull(string? json) =>
        string.IsNullOrWhiteSpace(json) || json == "{}" ? null : ReadQa(json);

    private static JsonElement Obj(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Object
            ? n : JsonSerializer.SerializeToElement(new { });

    private static string Read(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return "";
        return n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim()
            : n.ValueKind == JsonValueKind.Number ? n.ToString() : "";
    }

    private static double ReadNumber(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return 0;
        if (n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out var d))
            return d;
        return double.TryParse(n.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
    }

    private static string First(string a, string b) => string.IsNullOrWhiteSpace(a) ? b : a;
}
