using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionVideoContractService : IProductionVideoContractService
{
    private readonly ProductionVideoContractRepository _repo;
    private readonly ProductionShotContractRepository _contracts;
    private readonly ImageGenerationExecutionRepository _executions;
    private readonly ImageGenerationDirectorReviewRepository _reviews;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly ICharacterIdentityGovernanceService _governance;
    private readonly KitVideoArtifactStore _artifacts;

    public ProductionVideoContractService(
        ProductionVideoContractRepository repo,
        ProductionShotContractRepository contracts,
        ImageGenerationExecutionRepository executions,
        ImageGenerationDirectorReviewRepository reviews,
        KitVideoProductionShotRepository shots,
        ICharacterIdentityGovernanceService governance,
        KitVideoArtifactStore artifacts)
    {
        _repo = repo;
        _contracts = contracts;
        _executions = executions;
        _reviews = reviews;
        _shots = shots;
        _governance = governance;
        _artifacts = artifacts;
    }

    public IReadOnlyList<string> RunRegression() => ProductionVideoContractV1Regression.Run();

    public async Task<ProductionVideoContractDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, "director", writeGate: false, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        return row is null ? Preview(ctx) : ToDto(row, ctx);
    }

    public Task<ProductionVideoContractDto> SaveAsync(Guid shotId, ProductionVideoContractWriteRequest? request, string actor, CancellationToken cancellationToken = default) =>
        PersistAsync(shotId, request?.ContractId, request, actor, validateOnly: false, cancellationToken);

    public Task<ProductionVideoContractDto> UpdateAsync(Guid shotId, Guid contractId, ProductionVideoContractWriteRequest? request, string actor, CancellationToken cancellationToken = default) =>
        PersistAsync(shotId, contractId, request, actor, validateOnly: false, cancellationToken);

    public Task<ProductionVideoContractDto> ValidateAsync(Guid shotId, Guid? contractId, ProductionVideoContractWriteRequest? request, string actor, CancellationToken cancellationToken = default) =>
        PersistAsync(shotId, contractId ?? request?.ContractId, request, actor, validateOnly: true, cancellationToken);

    public async Task<ProductionVideoContractDto> ApproveAsync(Guid shotId, Guid contractId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Chỉ Director được duyệt.", "DIRECTOR", "actor");
        var ctx = await LoadAsync(shotId, actor, writeGate: true, cancellationToken);
        if (ctx.Gate.Status != "PASS" || ctx.Execution is null)
            throw Fail(ctx);
        var row = await _repo.GetByIdAsync(contractId, cancellationToken)
            ?? throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Chưa có Video Contract.");
        if (row.ShotId != shotId)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Contract không thuộc shot này.", "OWNERSHIP", "shotId");
        if (row.ContractStatus != "VALIDATED")
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Chỉ VALIDATED được APPROVE.", "CONTRACT", "status", row.ContractStatus, "VALIDATED");
        if (!CharacterIdentityGovernanceRules.SameSha(row.ContractSha256, ctx.Gate.ContractSha256 ?? ""))
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Contract SHA mismatch on approve. Không auto-fix.", "CONTRACT", "contractSha256", row.ContractSha256, ctx.Gate.ContractSha256);
        if (ProductionVideoContractRules.IsApproved(row.ContractStatus))
            return ToDto(row, ctx);
        await _repo.ApproveAsync(row.Id, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "VIDEO_CONTRACT_DIRECTOR_APPROVED", new { note, sha = row.ContractSha256, generate = false }, actor, cancellationToken);
        return await GetAsync(shotId, cancellationToken);
    }

    public async Task<ProductionVideoContractDto> RejectAsync(Guid shotId, Guid contractId, string actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (!ProductionVideoContractRules.RejectReasonRequired(reason))
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Director phải nhập lý do reject.", "DIRECTOR", "rejectionReason");
        var row = await _repo.GetByIdAsync(contractId, cancellationToken)
            ?? throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Chưa có Video Contract.");
        if (row.ShotId != shotId)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Contract không thuộc shot này.", "OWNERSHIP", "shotId");
        if (ProductionVideoContractRules.IsApproved(row.ContractStatus))
            throw new ProductionVideoContractException("VIDEO_CONTRACT_LOCKED", "VIDEO_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
        await _repo.RejectAsync(row.Id, actor, reason!.Trim(), cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "VIDEO_CONTRACT_DIRECTOR_REJECTED", new { reason = reason.Trim(), generate = false }, actor, cancellationToken);
        return await GetAsync(shotId, cancellationToken);
    }

    private async Task<ProductionVideoContractDto> PersistAsync(
        Guid shotId, Guid? contractId, ProductionVideoContractWriteRequest? request, string actor, bool validateOnly, CancellationToken ct)
    {
        var ctx = await LoadAsync(shotId, actor, writeGate: true, ct);
        if (ctx.Gate.Status != "PASS" || ctx.Contract is null || ctx.Execution is null)
        {
            await _repo.InsertEventAsync(null, shotId, "VIDEO_CONTRACT_BLOCKED", new { blocks = ctx.Gate.Blocks, generate = false }, actor, ct);
            throw Fail(ctx);
        }

        var overlay = ToOverlay(request, ctx.ExistingPayload);
        var payload = ProductionVideoContractRules.BuildPayload(
            ctx.Shot.CharacterId, ctx.Shot.Id,
            ctx.Gov.MasterId, ctx.Gov.MasterSha256 ?? "",
            ctx.Gov.DnaId, ctx.Gov.DnaSha256 ?? "",
            ctx.Gov.PrpId, ctx.Gov.PrpSha256 ?? "",
            ctx.Contract.Id, ctx.LiveContractSha,
            ctx.Execution.Id, ctx.Execution.ArtifactSha256,
            ctx.ShotPayload, overlay);
        var gate = ProductionVideoContractRules.Evaluate(WithPayload(ctx, payload));
        if (gate.Status != "PASS")
            throw Fail(ctx with { Gate = gate, Payload = payload });

        var sha = ProductionVideoContractRules.HashCanonical(payload);
        var canonical = ProductionShotContractRules.CanonicalJson(ProductionVideoContractRules.HashSurface(payload));
        var status = validateOnly ? "VALIDATED" : "DRAFT";
        var existing = contractId is { } id ? await _repo.GetByIdAsync(id, ct) : await _repo.GetLatestAsync(shotId, ct);
        if (existing is not null && existing.ShotId != shotId)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Contract không thuộc shot này.", "OWNERSHIP", "shotId");

        if (existing is not null && ProductionVideoContractRules.IsApproved(existing.ContractStatus))
        {
            if (existing.ContractSha256 == sha && existing.StillExecutionId == ctx.Execution.Id)
                return ToDto(existing, ctx with { Payload = payload, Gate = gate });
            var next = NewRow(ctx, payload, sha, canonical, actor, ProductionVideoContractRules.NextVersion(existing.ContractVersion), status);
            next = await _repo.InsertAsync(next, ct);
            await _repo.MarkSupersededAsync(existing.Id, next.Id, ct);
            await _repo.InsertEventAsync(next.Id, shotId, "VIDEO_CONTRACT_V2_CREATED", new { previous = existing.ContractVersion, generate = false }, actor, ct);
            return ToDto(next, ctx with { Payload = payload, Gate = gate });
        }

        if (existing is null)
        {
            var row = NewRow(ctx, payload, sha, canonical, actor, "V1", status);
            row = await _repo.InsertAsync(row, ct);
            await _repo.InsertEventAsync(row.Id, shotId, validateOnly ? "VIDEO_CONTRACT_VALIDATED" : "VIDEO_CONTRACT_SAVED", new { sha, generate = false }, actor, ct);
            return ToDto(row, ctx with { Payload = payload, Gate = gate });
        }

        existing.PayloadJson = payload.GetRawText();
        existing.CanonicalJson = canonical;
        existing.ContractSha256 = sha;
        existing.ContractStatus = status;
        existing.ShotContractId = ctx.Contract.Id;
        existing.StillExecutionId = ctx.Execution.Id;
        existing.MasterId = ctx.Gov.MasterId;
        existing.MasterSha256 = ctx.Gov.MasterSha256 ?? "";
        existing.DnaId = ctx.Gov.DnaId;
        existing.DnaSha256 = ctx.Gov.DnaSha256 ?? "";
        existing.PrpId = ctx.Gov.PrpId;
        existing.PrpSha256 = ctx.Gov.PrpSha256 ?? "";
        existing.ShotContractSha256 = ctx.LiveContractSha;
        existing.StillArtifactSha256 = ctx.Execution.ArtifactSha256;
        existing.UpdatedBy = actor;
        existing.ValidatedAt = validateOnly ? DateTimeOffset.UtcNow : existing.ValidatedAt;
        existing.ValidatedBy = validateOnly ? actor : existing.ValidatedBy;
        existing = await _repo.UpdateDraftAsync(existing, ct);
        await _repo.InsertEventAsync(existing.Id, shotId, validateOnly ? "VIDEO_CONTRACT_VALIDATED" : "VIDEO_CONTRACT_SAVED", new { sha, generate = false }, actor, ct);
        return ToDto(existing, ctx with { Payload = payload, Gate = gate });
    }

    private sealed record Ctx(
        KitVideoProductionShotRepository.ShotRow Shot,
        ProductionShotContractRepository.ContractRow? Contract,
        ImageGenerationExecutionRepository.Row? Execution,
        ImageGenerationDirectorReviewRepository.Row? Review,
        CharacterIdentityGovernanceDto Gov,
        JsonElement ShotPayload,
        JsonElement Payload,
        JsonElement? ExistingPayload,
        string LiveContractSha,
        byte[]? ArtifactBytes,
        ProductionVideoContractRules.GateOutput Gate);

    private async Task<Ctx> LoadAsync(Guid shotId, string actor, bool writeGate, CancellationToken ct)
    {
        var shot = await _shots.GetByIdAsync(shotId, ct)
            ?? throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "Production Shot không tồn tại.", "SHOT", "shotId");
        var contract = await _contracts.GetLatestAsync(shotId, ct);
        var liveContractSha = contract is null ? "" : ProductionShotContractRules.HashCanonical(
            JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson));
        JsonElement shotPayload = default;
        if (contract is not null)
            shotPayload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson);

        var execution = await _executions.GetLatestAsync(shotId, ct);
        var review = execution is null ? null : await _reviews.GetByExecutionAsync(execution.Id, ct);
        var bytes = execution is null ? null : _artifacts.Read(execution.ArtifactPath);
        var liveArt = bytes is { Length: > 0 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";

        var gov = contract is not null && writeGate
            ? await _governance.ProductionGateAsync(
                shot.CharacterId,
                new CharacterIdentityGovernanceCheckRequest(
                    ProductionShotContractRules.ToGovernanceShotSpec(shotPayload),
                    ProductionShotContractRules.ToGovernancePrompt(shotPayload),
                    shot.Id),
                actor, ct)
            : await _governance.GetAsync(shot.CharacterId, shot.EraId, ct);
        if (gov.Generate)
            throw new ProductionVideoContractException("VIDEO_CONTRACT_NOT_READY", "generation=false.", "COMPILER", "generate", "true", "false");

        var existing = await _repo.GetLatestAsync(shotId, ct);
        JsonElement? existingPayload = existing is null
            ? null
            : JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(existing.PayloadJson) ? "{}" : existing.PayloadJson);
        var overlay = existingPayload is { ValueKind: JsonValueKind.Object } el
            ? ProductionVideoContractRules.ReadOverlay(el)
            : new ProductionVideoContractRules.WriteOverlay();
        var payload = ProductionVideoContractRules.BuildPayload(
            shot.CharacterId, shot.Id,
            gov.MasterId, gov.MasterSha256 ?? "",
            gov.DnaId, gov.DnaSha256 ?? "",
            gov.PrpId, gov.PrpSha256 ?? "",
            contract?.Id, liveContractSha,
            execution?.Id ?? Guid.Empty, execution?.ArtifactSha256 ?? "",
            shotPayload.ValueKind == JsonValueKind.Object ? shotPayload : JsonSerializer.SerializeToElement(new { }),
            overlay);

        var reviewStatus = review?.ReviewStatus
            ?? (ImageGenerationDirectorReviewRules.IsImageApproved(execution?.ExecutionStatus) ? "APPROVED"
                : ImageGenerationDirectorReviewRules.IsImageRejected(execution?.ExecutionStatus) ? "REJECTED" : "PENDING");
        var govConflicts = gov.Conflicts.Select(c => CharacterIdentityGovernanceRules.Block(c.Code, c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message)).ToList();
        var gate = ProductionVideoContractRules.Evaluate(new ProductionVideoContractRules.GateInput(
            shot.CharacterId,
            gov.Master == "LOCKED", gov.Dna == "LOCKED", gov.Prp == "LOCKED",
            gov.Status == "PASS" && gov.ProductionAllowed, govConflicts,
            contract?.ContractStatus ?? "",
            execution?.ExecutionStatus ?? "",
            reviewStatus,
            bytes is { Length: > 0 }, bytes is { Length: > 0 },
            contract?.MasterSha256 ?? gov.MasterSha256 ?? "", gov.MasterSha256 ?? "",
            contract?.DnaSha256 ?? gov.DnaSha256 ?? "", gov.DnaSha256 ?? "",
            contract?.PrpSha256 ?? gov.PrpSha256 ?? "", gov.PrpSha256 ?? "",
            contract?.ContractSha256 ?? "", liveContractSha,
            execution?.ArtifactSha256 ?? "", liveArt,
            existing?.StillExecutionId, execution?.Id,
            payload));
        return new Ctx(shot, contract, execution, review, gov, shotPayload, payload, existingPayload, liveContractSha, bytes, gate);
    }

    private static ProductionVideoContractRules.GateInput WithPayload(Ctx ctx, JsonElement payload) =>
        new(ctx.Shot.CharacterId,
            ctx.Gov.Master == "LOCKED", ctx.Gov.Dna == "LOCKED", ctx.Gov.Prp == "LOCKED",
            ctx.Gov.Status == "PASS" && ctx.Gov.ProductionAllowed,
            ctx.Gov.Conflicts.Select(c => CharacterIdentityGovernanceRules.Block(c.Code, c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message)).ToList(),
            ctx.Contract?.ContractStatus ?? "",
            ctx.Execution?.ExecutionStatus ?? "",
            ctx.Review?.ReviewStatus ?? "PENDING",
            ctx.ArtifactBytes is { Length: > 0 }, ctx.ArtifactBytes is { Length: > 0 },
            ctx.Contract?.MasterSha256 ?? ctx.Gov.MasterSha256 ?? "", ctx.Gov.MasterSha256 ?? "",
            ctx.Contract?.DnaSha256 ?? ctx.Gov.DnaSha256 ?? "", ctx.Gov.DnaSha256 ?? "",
            ctx.Contract?.PrpSha256 ?? ctx.Gov.PrpSha256 ?? "", ctx.Gov.PrpSha256 ?? "",
            ctx.Contract?.ContractSha256 ?? "", ctx.LiveContractSha,
            ctx.Execution?.ArtifactSha256 ?? "", ctx.Execution?.ArtifactSha256 ?? "",
            ctx.Execution?.Id, ctx.Execution?.Id, payload);

    private static ProductionVideoContractRules.WriteOverlay ToOverlay(ProductionVideoContractWriteRequest? request, JsonElement? existing)
    {
        JsonElement? cam = string.IsNullOrWhiteSpace(request?.CameraMovementType)
            ? null
            : ProductionVideoContractRules.Motion(request!.CameraMovementType!, request.CameraDirection ?? "forward", request.CameraIntensity ?? "low");
        JsonElement? head = string.IsNullOrWhiteSpace(request?.HeadMovementType)
            ? null
            : ProductionVideoContractRules.Motion(request!.HeadMovementType!, request.HeadDirection ?? "right", request.HeadIntensity ?? "low");
        return ProductionVideoContractRules.ResolveOverlay(
            new ProductionVideoContractRules.WriteOverlay(
                request?.DurationSeconds, cam, head,
                request?.StartingExpression, request?.EndingExpression,
                request?.HairMotion, request?.ClothMotion),
            existing);
    }

    private ProductionVideoContractRepository.Row NewRow(Ctx ctx, JsonElement payload, string sha, string canonical, string actor, string version, string status) => new()
    {
        Id = Guid.NewGuid(),
        ShotId = ctx.Shot.Id,
        ShotContractId = ctx.Contract!.Id,
        StillExecutionId = ctx.Execution!.Id,
        SeriesId = ctx.Contract.SeriesId,
        CharacterId = ctx.Shot.CharacterId,
        EraId = ctx.Shot.EraId,
        ContractVersion = version,
        ContractStatus = status,
        PayloadJson = payload.GetRawText(),
        CanonicalJson = canonical,
        ContractSha256 = sha,
        MasterId = ctx.Gov.MasterId,
        MasterSha256 = ctx.Gov.MasterSha256 ?? "",
        DnaId = ctx.Gov.DnaId,
        DnaSha256 = ctx.Gov.DnaSha256 ?? "",
        PrpId = ctx.Gov.PrpId,
        PrpSha256 = ctx.Gov.PrpSha256 ?? "",
        ShotContractSha256 = ctx.LiveContractSha,
        StillArtifactSha256 = ctx.Execution.ArtifactSha256,
        CreatedBy = actor,
        UpdatedBy = actor,
        ValidatedAt = status == "VALIDATED" ? DateTimeOffset.UtcNow : null,
        ValidatedBy = status == "VALIDATED" ? actor : null,
    };

    private static ProductionVideoContractDto Preview(Ctx ctx) =>
        ToDto(null, ctx);

    private static ProductionVideoContractDto ToDto(ProductionVideoContractRepository.Row? row, Ctx ctx)
    {
        var payload = row is null
            ? ctx.Payload
            : JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var approval = ProductionVideoContractRules.IsApproved(row?.ContractStatus) ? "APPROVED"
            : row?.ContractStatus == "REJECTED" ? "REJECTED" : "PENDING";
        var stillApproved = ProductionVideoContractRules.IsStillApproved(ctx.Execution?.ExecutionStatus, ctx.Review?.ReviewStatus
            ?? (ImageGenerationDirectorReviewRules.IsImageApproved(ctx.Execution?.ExecutionStatus) ? "APPROVED" : "PENDING"));
        var url = ctx.Execution is null
            ? ""
            : $"/api/content/video-engine/shots/{ctx.Shot.Id}/image-generation-execution/{ctx.Execution.Id}/image";
        var pass = ctx.Gate.Status == "PASS";
        return new ProductionVideoContractDto(
            row?.Id, ctx.Shot.Id, row?.ShotContractId ?? ctx.Contract?.Id, ctx.Execution?.Id,
            ctx.Contract?.SeriesId ?? "FAMIXA", ctx.Shot.CharacterId, ctx.Shot.CharacterName, ctx.Shot.EraId, ctx.Shot.ShotCode,
            row?.ContractVersion ?? "V1",
            row?.ContractStatus ?? (pass ? "DRAFT" : "NOT_READY"),
            ProductionVideoContractRules.DocumentId,
            payload,
            row?.CanonicalJson ?? "",
            row?.ContractSha256 ?? ctx.Gate.ContractSha256 ?? "",
            row?.MasterId ?? ctx.Gov.MasterId,
            row?.MasterSha256 ?? ctx.Gov.MasterSha256 ?? "",
            row?.DnaId ?? ctx.Gov.DnaId,
            row?.DnaSha256 ?? ctx.Gov.DnaSha256 ?? "",
            row?.PrpId ?? ctx.Gov.PrpId,
            row?.PrpSha256 ?? ctx.Gov.PrpSha256 ?? "",
            row?.ShotContractSha256 ?? ctx.LiveContractSha,
            row?.StillArtifactSha256 ?? ctx.Execution?.ArtifactSha256 ?? "",
            ctx.Execution?.ExecutionStatus ?? "MISSING",
            ctx.Review?.ReviewStatus ?? "PENDING",
            stillApproved,
            url,
            ctx.ArtifactBytes is { Length: > 0 },
            row?.RejectionReason,
            row?.CreatedAt, row?.CreatedBy, row?.ValidatedAt, row?.ValidatedBy, row?.ApprovedAt, row?.ApprovedBy,
            ProductionVideoContractRules.IsApproved(row?.ContractStatus),
            false,
            pass && (row is null || ProductionVideoContractRules.CanEdit(row.ContractStatus)),
            pass && (row is null || ProductionVideoContractRules.CanEdit(row.ContractStatus)),
            pass && row?.ContractStatus == "VALIDATED",
            row is not null && ProductionVideoContractRules.CanEdit(row.ContractStatus),
            ctx.Gov.Master, ctx.Gov.Dna, ctx.Gov.Prp, ctx.Gov.GovernanceEngine,
            ctx.Contract?.ContractStatus ?? "MISSING",
            approval,
            ctx.Gate.Blocks.Select(b => new ImageGenerationContractBlockDto(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message)).ToList());
    }

    private static ProductionVideoContractException Fail(Ctx ctx)
    {
        var first = ctx.Gate.Blocks.FirstOrDefault()
            ?? new ProductionVideoContractRules.Block("BLOCKED", "VIDEO_CONTRACT_NOT_READY", "AUTHORITY", "contract", null, null, "VIDEO_CONTRACT_NOT_READY");
        return new ProductionVideoContractException(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative,
            ctx.Gate.Blocks.Select(b => new ImageGenerationContractBlockDto(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message)).ToList());
    }
}
