using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ImageGenerationExecutionService : IImageGenerationExecutionService
{
    private readonly ImageGenerationExecutionRepository _repo;
    private readonly ImageGenerationContractRepository _igc;
    private readonly ProductionShotContractRepository _contracts;
    private readonly ProductionPromptCompilerRepository _prompts;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly KitVideoMasterLockRepository _masters;
    private readonly ICharacterIdentityGovernanceService _governance;
    private readonly IGeminiImageGenerationProvider _gemini;
    private readonly KitVideoArtifactStore _artifacts;
    private readonly ICharacterReferencePackService _crp;
    private readonly IVisualUniverseSnapshotResolver _snapshots;
    private readonly IUnifiedVisualCompiler _compiler;

    public ImageGenerationExecutionService(
        ImageGenerationExecutionRepository repo,
        ImageGenerationContractRepository igc,
        ProductionShotContractRepository contracts,
        ProductionPromptCompilerRepository prompts,
        KitVideoProductionShotRepository shots,
        KitVideoMasterLockRepository masters,
        ICharacterIdentityGovernanceService governance,
        IGeminiImageGenerationProvider gemini,
        KitVideoArtifactStore artifacts,
        ICharacterReferencePackService crp,
        IVisualUniverseSnapshotResolver snapshots,
        IUnifiedVisualCompiler compiler)
    {
        _repo = repo;
        _igc = igc;
        _contracts = contracts;
        _prompts = prompts;
        _shots = shots;
        _masters = masters;
        _governance = governance;
        _gemini = gemini;
        _artifacts = artifacts;
        _crp = crp;
        _snapshots = snapshots;
        _compiler = compiler;
    }

    public IReadOnlyList<string> RunRegression() => ImageGenerationExecutionV1Regression.Run();

    public async Task<ImageGenerationExecutionDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, "director", cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        return row is null ? Preview(ctx) : ToDto(row, ctx, []);
    }

    public async Task<ImageGenerationExecutionDto> GetByIdAsync(Guid shotId, Guid executionId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, "director", cancellationToken);
        var row = await _repo.GetByIdAsync(executionId, cancellationToken)
            ?? throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Execution không tồn tại.", "EXECUTION", "executionId");
        if (row.ShotId != shotId)
            throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Execution không thuộc shot.", "OWNERSHIP", "shotId");
        return ToDto(row, ctx, []);
    }

    public async Task<ImageGenerationExecutionDto> PreflightAsync(Guid shotId, string actor, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        await _repo.InsertEventAsync(null, shotId, "EXECUTION_PREFLIGHT", new
        {
            status = ctx.Preflight.Status,
            runGemini = ctx.Preflight.RunGemini,
            credit = ctx.Preflight.Credit,
            fingerprint = ctx.Preflight.Fingerprint,
            generate = false,
        }, actor, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        return row is null ? Preview(ctx) : ToDto(row, ctx, ctx.Preflight.Blocks.Select(Map).ToList());
    }

    public async Task<ImageGenerationExecutionDto> ExecuteAsync(Guid shotId, string actor, bool confirm, string? provider, CancellationToken cancellationToken = default)
    {
        var picked = (provider ?? "").Trim().ToUpperInvariant();
        if (!confirm)
            throw new ImageGenerationExecutionException(
                "FIRST_REAL_PRODUCTION_BLOCKED",
                "Cần xác nhận trước khi tạo hình.",
                "CONFIRM", "generationAllowed", "false", "true");
        if (picked.Length == 0)
            throw new ImageGenerationExecutionException(
                "NEEDS_PROVIDER_SELECTION",
                "Chưa chọn nhà cung cấp AI.",
                "PROVIDER", "selection", "", "GEMINI");
        if (picked is "RUNWAY" or "VEO" || !FirstRealProductionRules.AllowsImageProvider(picked))
            throw new ImageGenerationExecutionException(
                "PROVIDER_CAPABILITY_UNSUPPORTED",
                "Nhà cung cấp AI này chưa hỗ trợ tạo hình cho shot này.",
                "PROVIDER", "capability", picked, "GEMINI");
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        var referenceSha = string.IsNullOrWhiteSpace(ctx.CrpSha) ? (ctx.Gov.PrpSha256 ?? "") : ctx.CrpSha;
        var intent = FirstRealProductionRules.TryResolveIntent(
            ctx.Shot.CharacterId, ctx.ShotPayload,
            ctx.Gov.MasterSha256 ?? "", ctx.Gov.DnaSha256 ?? "", referenceSha, ctx.LiveContractSha);
        var existingFp = string.IsNullOrWhiteSpace(ctx.Preflight.Fingerprint)
            ? null
            : await _repo.GetByFingerprintAsync(ctx.Preflight.Fingerprint, cancellationToken);
        if (existingFp is not null && FirstRealProductionV2Rules.IsHistoricalStill(existingFp.Id.ToString()))
            existingFp = null;
        var duplicate = existingFp is not null
            && ImageGenerationExecutionRules.IsTerminalSuccess(existingFp.ExecutionStatus);
        var gate = FirstRealProductionV2Rules.Evaluate(new FirstRealProductionV2Rules.GateInput(
            true,
            FirstRealProductionV2Rules.IsShot001(ctx.Shot.ShotCode),
            ctx.Shot.ShotCode,
            string.Equals(ctx.Gov.Master, "LOCKED", StringComparison.OrdinalIgnoreCase),
            string.Equals(ctx.Gov.Dna, "LOCKED", StringComparison.OrdinalIgnoreCase),
            string.Equals(ctx.Gov.Prp, "LOCKED", StringComparison.OrdinalIgnoreCase),
            FirstRealProductionV2Rules.ShaMatch(string.IsNullOrWhiteSpace(ctx.Igc?.MasterSha256) ? ctx.Gov.MasterSha256 : ctx.Igc!.MasterSha256, ctx.Gov.MasterSha256),
            FirstRealProductionV2Rules.ShaMatch(string.IsNullOrWhiteSpace(ctx.Igc?.DnaSha256) ? ctx.Gov.DnaSha256 : ctx.Igc!.DnaSha256, ctx.Gov.DnaSha256),
            FirstRealProductionV2Rules.ShaMatch(string.IsNullOrWhiteSpace(ctx.Igc?.PrpSha256) ? ctx.Gov.PrpSha256 : ctx.Igc!.PrpSha256, ctx.Gov.PrpSha256),
            ctx.CrpUsable,
            ctx.CrpStatus,
            ctx.CrpCoverage,
            ctx.Contract is not null && ProductionShotContractRules.IsApproved(ctx.Contract.ContractStatus),
            intent is not null,
            picked,
            FirstRealProductionRules.ImageCapabilityReady(ProductionOsRules.GeminiImageProfile),
            null,
            confirm,
            duplicate,
            ctx.Shot.CharacterId,
            ctx.Shot.CharacterId));
        var mayCall = FirstRealProductionV2Rules.MayCallProvider(gate);
        await _repo.InsertEventAsync(null, shotId, FirstRealProductionV2Rules.DocumentId, new
        {
            documentId = FirstRealProductionRules.DocumentId,
            v2DocumentId = FirstRealProductionV2Rules.DocumentId,
            generationStarted = true,
            generationAllowed = gate.GenerationAllowed,
            providerSelected = gate.ProviderSelected,
            capabilityGate = gate.CapabilityReady ? "READY" : (gate.Code ?? "BLOCKED"),
            intentSha = intent is null ? null : ProductionOsRules.IntentSha(intent),
            masterSha = ctx.Gov.MasterSha256,
            dnaSha = ctx.Gov.DnaSha256,
            referenceSha,
            shotContractSha = ctx.LiveContractSha,
            executionFingerprint = ctx.Preflight.Fingerprint,
            mayCallProvider = mayCall,
            generate = false,
            geminiCalled = false,
            runwayCalled = false,
            veoCalled = false,
            generationCount = 1,
            autoRetry = false,
        }, actor, cancellationToken);
        if (!mayCall)
            throw new ImageGenerationExecutionException(
                gate.Code ?? "FIRST_REAL_PRODUCTION_BLOCKED",
                gate.StaffMessage,
                "FIRST_REAL_PRODUCTION",
                "generationAllowed",
                "false",
                "true");
        await _repo.InsertEventAsync(null, shotId, "EXECUTION_PREFLIGHT", new
        {
            status = ctx.Preflight.Status,
            runGemini = ctx.Preflight.RunGemini,
            generate = false,
        }, actor, cancellationToken);
        if (ctx.Preflight.Status != "PASS" || !ctx.Preflight.RunGemini || ctx.Igc is null || ctx.Contract is null || ctx.Prompt is null)
        {
            var first = ctx.Preflight.Blocks.FirstOrDefault()
                ?? new ImageGenerationExecutionRules.Block("BLOCKED", "IMAGE_GENERATION_PREFLIGHT_FAILED", "AUTHORITY", "preflight", "FAIL", "PASS", "IMAGE_GENERATION_PREFLIGHT_FAILED");
            throw Fail(first, ctx.Preflight.Blocks.Select(Map).ToList());
        }

        var fingerprint = ctx.Preflight.Fingerprint!;
        var existing = await _repo.GetByFingerprintAsync(fingerprint, cancellationToken);
        if (existing is not null && FirstRealProductionV2Rules.IsHistoricalStill(existing.Id.ToString()))
            existing = null;
        if (existing is not null)
        {
            await _repo.InsertEventAsync(existing.Id, shotId, "EXECUTION_FAILED", new
            {
                code = "BLOCK_DUPLICATE",
                fingerprint,
                status = existing.ExecutionStatus,
                generate = false,
                geminiCalled = false,
            }, actor, cancellationToken);
            throw new ImageGenerationExecutionException(
                "BLOCK_DUPLICATE",
                "Lần tạo hình này đã chạy. Không gọi lại nhà cung cấp.",
                "FIRST_REAL_PRODUCTION",
                "executionFingerprint",
                fingerprint,
                existing.ExecutionFingerprint);
        }

        if (!FirstRealProductionV2Rules.HasRequiredCrpRefs(ctx.References))
            throw new ImageGenerationExecutionException(
                "CRP_NOT_READY",
                FirstRealProductionRules.StaffCrpBlock,
                "CHARACTER_REFERENCE_PACK",
                "coverage",
                ctx.CrpCoverage.ToString(),
                "4");

        var id = Guid.NewGuid();
        var row = new ImageGenerationExecutionRepository.Row
        {
            Id = id,
            ShotId = shotId,
            IgcId = ctx.Igc.Id,
            ShotContractId = ctx.Contract.Id,
            PromptId = ctx.Prompt.Id,
            SeriesId = ctx.Igc.SeriesId,
            CharacterId = ctx.Shot.CharacterId,
            EraId = ctx.Shot.EraId,
            ExecutionStatus = "REQUESTED",
            Provider = ImageGenerationExecutionRules.Provider,
            ProviderStatus = "REQUESTED",
            ExecutionFingerprint = fingerprint,
            IdempotencyKey = fingerprint,
            MasterSha256 = ctx.Gov.MasterSha256 ?? "",
            DnaSha256 = ctx.Gov.DnaSha256 ?? "",
            PrpSha256 = ctx.Gov.PrpSha256 ?? "",
            ShotContractSha256 = ctx.LiveContractSha,
            PromptSha256 = ctx.Prompt.PromptSha256,
            IgcSha256 = ctx.Igc.ContractSha256,
            IgcVersion = ctx.Igc.ContractVersion,
            PromptVersion = ctx.Prompt.PromptVersion,
            ShotContractVersion = ctx.Contract.ContractVersion,
            CreditStatus = "UNKNOWN",
            RequestedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor,
            UpdatedBy = actor,
        };
        row = await _repo.InsertAsync(row, cancellationToken);
        if (row.Id != id)
            return ToDto(row, ctx, []);

        await _repo.InsertEventAsync(row.Id, shotId, "GEMINI_REQUESTED", new { fingerprint, generate = true }, actor, cancellationToken);

        var aspect = ReadReq(ctx.IgcPayload, "aspectRatio");
        if (string.IsNullOrWhiteSpace(aspect))
            aspect = ReadNested(ctx.ShotPayload, "production", "aspectRatio");
        var resolution = ReadReq(ctx.IgcPayload, "resolution");
        var (snapshot, snapGate) = await SeriesStillVisualIngressV1Rules.ResolveSnapshotAsync(
            _snapshots, FamixaVisualUniverseAuthorityV1Rules.ProjectId, cancellationToken);
        if (snapGate is not null || snapshot is null)
            throw new ImageGenerationExecutionException(
                snapGate ?? SeriesStillVisualIngressV1Rules.GateSnapshot,
                "Visual Universe Snapshot chưa sẵn sàng. Series Still / IGE không được tạo ngoài Visual Universe.",
                "VISUAL_UNIVERSE", "snapshot");
        var compiled = SeriesStillVisualIngressV1Rules.CompileStill(
            snapshot,
            SeriesStillVisualIngressV1Rules.FromIge(ctx.Shot.CharacterId, ctx.Prompt?.PromptText),
            _compiler);
        if (compiled.Gate is not null || compiled.Contract is null
            || !SeriesStillVisualIngressV1Rules.ProviderMayCall(compiled.Contract))
            throw new ImageGenerationExecutionException(
                compiled.Gate ?? SeriesStillVisualIngressV1Rules.GateCompile,
                "Unified Visual Compiler blocked Series Still / IGE. No legacy prompt fallback.",
                "VISUAL_UNIVERSE", "compiler");
        var request = SeriesStillVisualIngressV1Rules.ToProviderRequest(
            compiled.Contract,
            ctx.References,
            string.IsNullOrWhiteSpace(aspect) ? "16:9" : aspect,
            resolution);
        var result = await _gemini.GenerateAsync(request, cancellationToken);

        if (!result.Accepted)
        {
            row.ExecutionStatus = "FAILED";
            row.ProviderStatus = "FAILED";
            row.CreditStatus = ImageGenerationExecutionRules.CreditStatus(result.Credit);
            row.CreditValue = result.Credit;
            row.CompletedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = actor;
            row = await _repo.UpdateAsync(row, cancellationToken);
            await _repo.InsertEventAsync(row.Id, shotId, "EXECUTION_FAILED", new { provider = result.Provider, generate = true }, actor, cancellationToken);
            return ToDto(row, ctx, []);
        }

        row.ExecutionStatus = "ACCEPTED";
        row.ProviderStatus = "ACCEPTED";
        row.ProviderRequestId = result.ProviderRequestId;
        row.AcceptedAt = DateTimeOffset.UtcNow;
        row.CreditStatus = ImageGenerationExecutionRules.CreditStatus(result.Credit);
        row.CreditValue = result.Credit;
        row.UpdatedBy = actor;
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "GEMINI_ACCEPTED", new { http = "ACCEPTED", ready = false, generate = true }, actor, cancellationToken);

        if (!result.Succeeded || result.Bytes is null || result.Bytes.Length == 0)
        {
            row.ExecutionStatus = "FAILED";
            row.ProviderStatus = "FAILED";
            row.CompletedAt = DateTimeOffset.UtcNow;
            row = await _repo.UpdateAsync(row, cancellationToken);
            await _repo.InsertEventAsync(row.Id, shotId, "EXECUTION_FAILED", new { reason = "artifact unavailable", generate = true }, actor, cancellationToken);
            return ToDto(row, ctx, []);
        }

        row.ExecutionStatus = "PROCESSING";
        row.ProviderStatus = "PROCESSING";
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "GEMINI_PROCESSING", new { generate = true }, actor, cancellationToken);

        var persisted = _artifacts.PersistProductionExecution(shotId, row.Id, result.Bytes);
        var sha = KitVideoIntegrityRules.Sha256Hex(persisted.Bytes);
        row.ArtifactPath = persisted.Path;
        row.ArtifactSha256 = sha;
        row.ArtifactMime = result.Mime ?? KitVideoArtifactRules.DetectMime(persisted.Bytes) ?? "image/jpeg";
        row.ExecutionStatus = "SUCCEEDED";
        row.ProviderStatus = "SUCCEEDED";
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "GEMINI_SUCCEEDED", new { sha, generate = true }, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "ARTIFACT_DOWNLOADED", new { path = persisted.Path, generate = false }, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "ARTIFACT_HASHED", new { sha, generate = false }, actor, cancellationToken);

        await _repo.InsertEventAsync(row.Id, shotId, "QA_STARTED", new { generate = false }, actor, cancellationToken);
        var framing = ReadNested(ctx.ShotPayload, "composition", "framing");
        var qa = ImageGenerationExecutionRules.EvaluateQa(
            persisted.Bytes, persisted.Path, sha, null, null, null, framing, continuityRequired: true);
        row.QaJson = JsonSerializer.Serialize(qa);
        row.CompletedAt = DateTimeOffset.UtcNow;
        if (qa.Overall == "QA_FAILED")
        {
            row.ExecutionStatus = "QA_FAILED";
            row = await _repo.UpdateAsync(row, cancellationToken);
            await _repo.InsertEventAsync(row.Id, shotId, "QA_FAILED", new { qa, generate = false }, actor, cancellationToken);
            return ToDto(row, ctx, []);
        }

        row.ExecutionStatus = "READY_FOR_DIRECTOR";
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "QA_COMPLETED", new { qa, generate = false }, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "READY_FOR_DIRECTOR", new { sha, autoApprove = false, generate = false }, actor, cancellationToken);
        return ToDto(row, ctx, []);
    }

    public async Task<ImageGenerationExecutionDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Chỉ Director được duyệt.", "DIRECTOR", "actor");
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Chưa có execution.", "EXECUTION", "status");
        var qa = ReadQa(row.QaJson);
        if (!ImageGenerationExecutionRules.CanDirectorApprove(row.ExecutionStatus, qa.Technical == "PASS", qa.Character == "PASS", qa.Identity == "PASS", qa.Continuity == "PASS", qa.Composition == "PASS", qa.P0))
            throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Chưa READY_FOR_DIRECTOR hoặc P0 > 0. Không auto-approve.", "DIRECTOR", "status", row.ExecutionStatus, "READY_FOR_DIRECTOR");
        row.ExecutionStatus = "APPROVED";
        row.ApprovedAt = DateTimeOffset.UtcNow;
        row.ApprovedBy = actor;
        row.UpdatedBy = actor;
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "DIRECTOR_APPROVED", new { note, sha = row.ArtifactSha256, generate = false }, actor, cancellationToken);
        return ToDto(row, ctx, []);
    }

    public async Task<ImageGenerationExecutionDto> RejectAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, actor, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Chưa có execution.", "EXECUTION", "status");
        if (row.ExecutionStatus == "APPROVED")
            throw new ImageGenerationExecutionException("IMAGE_GENERATION_EXECUTION_LOCKED", "APPROVED artifact immutable.", "EXECUTION", "status");
        if (row.ExecutionStatus != "READY_FOR_DIRECTOR" && row.ExecutionStatus != "QA_FAILED")
            throw new ImageGenerationExecutionException("IMAGE_GENERATION_PREFLIGHT_FAILED", "Chưa tới Director review.", "DIRECTOR", "status", row.ExecutionStatus, "READY_FOR_DIRECTOR");
        row.ExecutionStatus = "REJECTED";
        row.RejectedAt = DateTimeOffset.UtcNow;
        row.RejectedBy = actor;
        row.UpdatedBy = actor;
        row = await _repo.UpdateAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "DIRECTOR_REJECTED", new { note, generate = false }, actor, cancellationToken);
        return ToDto(row, ctx, []);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadArtifactAsync(Guid shotId, Guid executionId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(executionId, cancellationToken);
        if (row is null || row.ShotId != shotId) return null;
        var bytes = _artifacts.Read(row.ArtifactPath);
        if (bytes is null) return null;
        return (bytes, string.IsNullOrWhiteSpace(row.ArtifactMime) ? "image/jpeg" : row.ArtifactMime);
    }

    private sealed record Ctx(
        KitVideoProductionShotRepository.ShotRow Shot,
        ImageGenerationContractRepository.Row? Igc,
        ProductionShotContractRepository.ContractRow? Contract,
        ProductionPromptCompilerRepository.PromptRow? Prompt,
        CharacterIdentityGovernanceDto Gov,
        JsonElement ShotPayload,
        JsonElement IgcPayload,
        string LiveContractSha,
        ImageGenerationExecutionRules.PreflightOutput Preflight,
        IReadOnlyList<ImageGenerationReferenceBytes> References,
        bool CrpUsable,
        string CrpStatus,
        string CrpSha,
        int CrpCoverage);

    private async Task<Ctx> LoadAsync(Guid shotId, string actor, CancellationToken ct)
    {
        var shot = await _shots.GetByIdAsync(shotId, ct)
            ?? throw new ImageGenerationExecutionException("SHOT_NOT_FOUND", "Không tìm thấy Shot 01.", "SHOT", "shotId");
        var igc = await _igc.GetLatestApprovedAsync(shotId, ct) ?? await _igc.GetLatestAsync(shotId, ct);
        var contract = await _contracts.GetLatestAsync(shotId, ct);
        var liveContractSha = contract is null ? "" : ProductionShotContractRules.HashCanonical(
            JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson));
        var prompt = contract is null
            ? null
            : await _prompts.GetCompiledByContractShaAsync(shotId, liveContractSha, ct)
              ?? await _prompts.GetLatestAsync(shotId, ct);

        JsonElement shotPayload = default;
        if (contract is not null)
            shotPayload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson);
        JsonElement igcPayload = default;
        if (igc is not null)
            igcPayload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(igc.PayloadJson) ? "{}" : igc.PayloadJson);

        var gov = contract is not null
            ? await _governance.ProductionGateAsync(
                shot.CharacterId,
                new CharacterIdentityGovernanceCheckRequest(
                    ProductionShotContractRules.ToGovernanceShotSpec(shotPayload),
                    ProductionShotContractRules.ToGovernancePrompt(shotPayload),
                    shot.Id),
                actor, ct)
            : await _governance.GetAsync(shot.CharacterId, shot.EraId, ct);

        var master = await _masters.GetByVersionAsync(
            CharacterIdentityGovernanceRules.NormalizeCharacterId(shot.CharacterId), shot.EraId, "V1", ct)
            ?? await _masters.GetActiveCanonAsync(CharacterIdentityGovernanceRules.NormalizeCharacterId(shot.CharacterId), shot.EraId, ct);
        var masterBytes = _artifacts.Read(master?.ArtifactPath);
        var masterReadable = masterBytes is { Length: > 0 }
            && !string.IsNullOrWhiteSpace(master?.Sha256)
            && CharacterIdentityGovernanceRules.SameSha(KitVideoIntegrityRules.Sha256Hex(masterBytes), master.Sha256)
            && !ImageGenerationExecutionRules.TouchesGolden(master.ArtifactPath);
        var refs = new List<ImageGenerationReferenceBytes>();
        if (masterReadable && masterBytes is not null && master is not null)
        {
            refs.Add(new ImageGenerationReferenceBytes(
                "MASTER_REFERENCE",
                KitVideoArtifactRules.DetectMime(masterBytes) ?? "image/jpeg",
                masterBytes,
                master.Sha256,
                "MASTER_REFERENCE"));
        }

        CharacterReferencePackGetDto? crp = null;
        try { crp = await _crp.GetAsync(shot.CharacterId, shot.EraId, ct); }
        catch (InvalidOperationException) { crp = null; }
        var packCanUse = crp?.Pack?.CanUse == true;
        var crpStatus = crp?.Pack?.Status ?? "";
        var crpCoverage = crp?.Pack?.RequiredReady ?? 0;
        var crpSha = crp?.Pack?.PackSha256 ?? "";
        var crpUsable = FirstRealProductionV2Rules.CrpReadyForProduction(crpStatus, packCanUse, crpCoverage);
        AppendLockedCrpRefs(refs, shot.CharacterId, crp);

        var (capability, selection) = ReadProviderPolicy(igcPayload);
        var liveIgcSha = igc is null ? "" : (string.IsNullOrWhiteSpace(igc.ContractSha256)
            ? ImageGenerationContractRules.HashCanonical(igcPayload)
            : igc.ContractSha256);
        var preflight = ImageGenerationExecutionRules.EvaluatePreflight(new ImageGenerationExecutionRules.PreflightInput(
            shot.CharacterId, shot.Id.ToString(),
            gov.Master == "LOCKED", gov.Dna == "LOCKED", gov.Prp == "LOCKED",
            gov.Status == "PASS" && gov.ProductionAllowed,
            contract?.ContractStatus ?? "",
            prompt?.PromptStatus ?? "",
            igc?.ContractStatus ?? "",
            igc?.MasterSha256 ?? gov.MasterSha256 ?? "", gov.MasterSha256 ?? "",
            igc?.DnaSha256 ?? gov.DnaSha256 ?? "", gov.DnaSha256 ?? "",
            igc?.PrpSha256 ?? gov.PrpSha256 ?? "", gov.PrpSha256 ?? "",
            igc?.ShotContractSha256 ?? contract?.ContractSha256 ?? "", liveContractSha,
            igc?.PromptSha256 ?? prompt?.PromptSha256 ?? "", prompt?.PromptSha256 ?? "",
            igc?.ContractSha256 ?? "", liveIgcSha,
            prompt?.MasterSha256 ?? "", prompt?.DnaSha256 ?? "", prompt?.PrpSha256 ?? "", prompt?.ContractSha256 ?? "",
            capability, selection, ImageGenerationExecutionRules.GenerationPolicy,
            masterReadable, master?.ArtifactPath, master?.ArtifactPath,
            crpUsable, crpStatus, crpSha, crpUsable));

        return new Ctx(shot, igc, contract, prompt, gov, shotPayload, igcPayload, liveContractSha, preflight, refs, crpUsable, crpStatus, crpSha, crpCoverage);
    }

    private ImageGenerationExecutionDto Preview(Ctx ctx)
    {
        var blocks = ctx.Preflight.Blocks.Select(Map).ToList();
        return new ImageGenerationExecutionDto(
            null, ctx.Shot.Id, ctx.Igc?.Id, ctx.Contract?.Id, ctx.Prompt?.Id,
            ctx.Igc?.SeriesId ?? "FAMIXA", ctx.Shot.CharacterId, ctx.Shot.EraId,
            ImageGenerationExecutionRules.DocumentId,
            ctx.Preflight.Status == "PASS" ? "PREFLIGHT" : "BLOCKED",
            ImageGenerationExecutionRules.Provider, "", null,
            ctx.Preflight.Fingerprint ?? "", ctx.Preflight.Fingerprint ?? "",
            ctx.Gov.MasterSha256 ?? "", ctx.Gov.DnaSha256 ?? "", ctx.Gov.PrpSha256 ?? "",
            ctx.LiveContractSha, ctx.Prompt?.PromptSha256 ?? "", ctx.Igc?.ContractSha256 ?? "",
            null, "", "", null, "UNKNOWN", null,
            null, null, null, null, null, null, null,
            ctx.Preflight.Status == "PASS", ctx.Preflight.RunGemini, false, false,
            ctx.Gov.Master, ctx.Gov.Dna, ctx.Gov.Prp, ctx.Gov.GovernanceEngine,
            ctx.Contract?.ContractStatus ?? "MISSING", ctx.Prompt?.PromptStatus ?? "MISSING",
            ctx.Igc?.ContractStatus ?? "MISSING", "PENDING", blocks,
            ctx.CrpUsable, ctx.CrpStatus,
            ctx.CrpUsable ? null : FirstRealProductionRules.StaffCrpBlock,
            false);
    }

    private ImageGenerationExecutionDto ToDto(
        ImageGenerationExecutionRepository.Row row,
        Ctx ctx,
        IReadOnlyList<ImageGenerationExecutionBlockDto> extra)
    {
        var qa = string.IsNullOrWhiteSpace(row.QaJson) || row.QaJson == "{}" ? null : ReadQa(row.QaJson);
        return new ImageGenerationExecutionDto(
            row.Id, row.ShotId, row.IgcId, row.ShotContractId, row.PromptId, row.SeriesId, row.CharacterId, row.EraId,
            row.DocumentId, row.ExecutionStatus, row.Provider, row.ProviderStatus, row.ProviderRequestId,
            row.ExecutionFingerprint, row.IdempotencyKey, row.MasterSha256, row.DnaSha256, row.PrpSha256,
            row.ShotContractSha256, row.PromptSha256, row.IgcSha256, row.ArtifactPath, row.ArtifactSha256, row.ArtifactMime,
            qa, row.CreditStatus, row.CreditValue, row.RequestedAt, row.AcceptedAt, row.CompletedAt,
            row.CreatedAt, row.CreatedBy, row.ApprovedAt, row.ApprovedBy,
            ctx.Preflight.Status == "PASS", false,
            !string.IsNullOrWhiteSpace(row.ArtifactSha256) || ImageGenerationExecutionRules.IsTerminalSuccess(row.ExecutionStatus),
            row.ExecutionStatus is "SUCCEEDED" or "QA_FAILED" or "READY_FOR_DIRECTOR" or "APPROVED" or "REJECTED" or "FAILED",
            ctx.Gov.Master, ctx.Gov.Dna, ctx.Gov.Prp, ctx.Gov.GovernanceEngine,
            ctx.Contract?.ContractStatus ?? "DIRECTOR_APPROVED",
            ctx.Prompt?.PromptStatus ?? "COMPILED",
            ctx.Igc?.ContractStatus ?? "DIRECTOR_APPROVED",
            row.ExecutionStatus is "APPROVED" or "IMAGE_APPROVED" ? "APPROVED"
                : row.ExecutionStatus is "REJECTED" or "IMAGE_REJECTED" ? "REJECTED" : "PENDING",
            extra, ctx.CrpUsable, ctx.CrpStatus,
            ctx.CrpUsable ? null : FirstRealProductionRules.StaffCrpBlock,
            false);
    }

    private static ImageGenerationExecutionQaDto ReadQa(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ImageGenerationExecutionQaDto>(json)
                ?? new ImageGenerationExecutionQaDto("", "", "", "", "", 0, "", []);
        }
        catch
        {
            return new ImageGenerationExecutionQaDto("", "", "", "", "", 0, "", []);
        }
    }

    private static (string Capability, string Selection) ReadProviderPolicy(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("providerPolicy", out var p) || p.ValueKind != JsonValueKind.Object)
            return ("IMAGE_GENERATION", "EXTERNAL");
        var cap = p.TryGetProperty("providerCapability", out var c) ? c.GetString() ?? "" : "";
        var sel = p.TryGetProperty("providerSelection", out var s) ? s.GetString() ?? "" : "";
        return (
            string.IsNullOrWhiteSpace(cap) ? "IMAGE_GENERATION" : cap,
            string.IsNullOrWhiteSpace(sel) ? "EXTERNAL" : sel);
    }

    private static string ReadReq(JsonElement payload, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("imageRequirements", out var r) || r.ValueKind != JsonValueKind.Object)
            return "";
        return r.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
    }

    private static string ReadNested(JsonElement payload, string obj, string name)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty(obj, out var o) || o.ValueKind != JsonValueKind.Object)
            return "";
        return o.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() ?? "" : "";
    }

    private static ImageGenerationExecutionBlockDto Map(ImageGenerationExecutionRules.Block b) =>
        new(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message);

    private void AppendLockedCrpRefs(
        List<ImageGenerationReferenceBytes> refs,
        string characterId,
        CharacterReferencePackGetDto? crp)
    {
        if (crp?.Pack?.Items is null) return;
        foreach (var type in CharacterReferencePackRules.RequiredTypes)
        {
            var item = crp.Pack.Items.FirstOrDefault(i =>
                string.Equals(CharacterReferencePackRules.NormalizeRefType(i.Type), type, StringComparison.OrdinalIgnoreCase));
            if (item is null || string.IsNullOrWhiteSpace(item.ArtifactPath)) continue;
            string? assetId = null;
            string? metaCharacter = null;
            if (item.Metadata.ValueKind == JsonValueKind.Object)
            {
                if (item.Metadata.TryGetProperty("assetId", out var a) && a.ValueKind == JsonValueKind.String)
                    assetId = a.GetString();
                if (item.Metadata.TryGetProperty("characterId", out var c) && c.ValueKind == JsonValueKind.String)
                    metaCharacter = c.GetString();
            }
            if (!FirstRealProductionV2Rules.AllowedCrpPath(characterId, item.ArtifactPath, assetId, metaCharacter, type))
                continue;
            var bytes = _artifacts.Read(item.ArtifactPath);
            if (bytes is null || bytes.Length == 0) continue;
            var sha = string.IsNullOrWhiteSpace(item.ArtifactSha256)
                ? KitVideoIntegrityRules.Sha256Hex(bytes)
                : item.ArtifactSha256;
            refs.Add(new ImageGenerationReferenceBytes(
                type,
                KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg",
                bytes,
                sha,
                type));
        }
    }

    private static ImageGenerationExecutionException Fail(
        ImageGenerationExecutionRules.Block first,
        IReadOnlyList<ImageGenerationExecutionBlockDto> blocks) =>
        new(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative, blocks);
}
