using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ImageGenerationDirectorReviewService : IImageGenerationDirectorReviewService
{
    private readonly ImageGenerationDirectorReviewRepository _repo;
    private readonly ImageGenerationExecutionRepository _executions;
    private readonly ImageGenerationContractRepository _igc;
    private readonly ProductionShotContractRepository _contracts;
    private readonly ProductionPromptCompilerRepository _prompts;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly ICharacterIdentityGovernanceService _governance;
    private readonly KitVideoArtifactStore _artifacts;

    public ImageGenerationDirectorReviewService(
        ImageGenerationDirectorReviewRepository repo,
        ImageGenerationExecutionRepository executions,
        ImageGenerationContractRepository igc,
        ProductionShotContractRepository contracts,
        ProductionPromptCompilerRepository prompts,
        KitVideoProductionShotRepository shots,
        ICharacterIdentityGovernanceService governance,
        KitVideoArtifactStore artifacts)
    {
        _repo = repo;
        _executions = executions;
        _igc = igc;
        _contracts = contracts;
        _prompts = prompts;
        _shots = shots;
        _governance = governance;
        _artifacts = artifacts;
    }

    public IReadOnlyList<string> RunRegression() => ImageGenerationDirectorReviewV1Regression.Run();

    public async Task<ImageGenerationDirectorReviewDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, cancellationToken);
        var review = ctx.Execution is null ? null : await _repo.GetByExecutionAsync(ctx.Execution.Id, cancellationToken);
        return ToDto(ctx, review);
    }

    public async Task<ImageGenerationDirectorReviewDto> OpenAsync(Guid shotId, string actor, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, cancellationToken);
        if (ctx.Execution is null)
            throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Chưa có Image Generation Execution.", "EXECUTION", "execution");
        var review = await _repo.GetByExecutionAsync(ctx.Execution.Id, cancellationToken);
        if (review is null)
        {
            review = await _repo.InsertAsync(NewPending(ctx, actor), cancellationToken);
            await _repo.InsertEventAsync(review.Id, ctx.Execution.Id, shotId, "IMAGE_DIRECTOR_REVIEW_REQUESTED",
                new { sha = ctx.Execution.ArtifactSha256, generate = false }, actor, cancellationToken);
        }
        return ToDto(ctx, review);
    }

    public async Task<ImageGenerationDirectorReviewDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Chỉ Director được duyệt.", "DIRECTOR", "actor");
        var opened = await OpenAsync(shotId, actor, cancellationToken);
        var ctx = await LoadAsync(shotId, cancellationToken);
        if (ctx.Gate.Status != "READY" || !ctx.Gate.CanApprove || ctx.Execution is null)
        {
            var first = ctx.Gate.Blocks.FirstOrDefault()
                ?? new ImageGenerationDirectorReviewRules.Block("BLOCKED", "IMAGE_DIRECTOR_REVIEW_NOT_READY", "DIRECTOR", "review", null, null, "IMAGE_DIRECTOR_REVIEW_NOT_READY");
            throw Fail(first, ctx.Gate.Blocks);
        }
        var review = await _repo.GetByExecutionAsync(ctx.Execution.Id, cancellationToken)
            ?? throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Chưa mở Director Review.");
        review = await _repo.DecideAsync(review.Id, "APPROVED", "APPROVED", actor, null, cancellationToken);
        await _executions.MarkDirectorDecisionAsync(ctx.Execution.Id, "IMAGE_APPROVED", actor, approve: true, cancellationToken);
        await _repo.InsertEventAsync(review.Id, ctx.Execution.Id, shotId, "IMAGE_DIRECTOR_APPROVED",
            new { note, sha = ctx.Execution.ArtifactSha256, generate = false }, actor, cancellationToken);
        ctx = await LoadAsync(shotId, cancellationToken);
        return ToDto(ctx, review);
    }

    public async Task<ImageGenerationDirectorReviewDto> RejectAsync(Guid shotId, string actor, string? reason, CancellationToken cancellationToken = default)
    {
        if (!ImageGenerationDirectorReviewRules.RejectReasonRequired(reason))
            throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Director phải nhập lý do reject.", "DIRECTOR", "rejectionReason");
        var ctx = await LoadAsync(shotId, cancellationToken);
        if (ctx.Execution is null)
            throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Chưa có execution.", "EXECUTION", "execution");
        if (ctx.Execution.ExecutionStatus != "READY_FOR_DIRECTOR")
            throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Reject chỉ khi READY_FOR_DIRECTOR.", "EXECUTION", "status", ctx.Execution.ExecutionStatus, "READY_FOR_DIRECTOR");
        await OpenAsync(shotId, actor, cancellationToken);
        var review = await _repo.GetByExecutionAsync(ctx.Execution.Id, cancellationToken)
            ?? throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Chưa mở Director Review.");
        review = await _repo.DecideAsync(review.Id, "REJECTED", "REJECTED", actor, reason!.Trim(), cancellationToken);
        await _executions.MarkDirectorDecisionAsync(ctx.Execution.Id, "IMAGE_REJECTED", actor, approve: false, cancellationToken);
        await _repo.InsertEventAsync(review.Id, ctx.Execution.Id, shotId, "IMAGE_DIRECTOR_REJECTED",
            new { reason = reason.Trim(), sha = ctx.Execution.ArtifactSha256, generate = false }, actor, cancellationToken);
        ctx = await LoadAsync(shotId, cancellationToken);
        return ToDto(ctx, review);
    }

    private sealed record Ctx(
        KitVideoProductionShotRepository.ShotRow Shot,
        ImageGenerationExecutionRepository.Row? Execution,
        ImageGenerationContractRepository.Row? Igc,
        ProductionShotContractRepository.ContractRow? Contract,
        ProductionPromptCompilerRepository.PromptRow? Prompt,
        CharacterIdentityGovernanceDto Gov,
        string LiveContractSha,
        string LiveIgcSha,
        ImageGenerationExecutionQaDto? Qa,
        byte[]? ArtifactBytes,
        ImageGenerationDirectorReviewRules.ReviewOutput Gate);

    private async Task<Ctx> LoadAsync(Guid shotId, CancellationToken ct)
    {
        var shot = await _shots.GetByIdAsync(shotId, ct)
            ?? throw new ImageGenerationDirectorReviewException("IMAGE_DIRECTOR_REVIEW_NOT_READY", "Production Shot không tồn tại.", "SHOT", "shotId");
        var execution = await _executions.GetLatestAsync(shotId, ct);
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
        var gov = contract is not null
            ? await _governance.ProductionGateAsync(
                shot.CharacterId,
                new CharacterIdentityGovernanceCheckRequest(
                    ProductionShotContractRules.ToGovernanceShotSpec(shotPayload),
                    ProductionShotContractRules.ToGovernancePrompt(shotPayload),
                    shot.Id),
                "director", ct)
            : await _governance.GetAsync(shot.CharacterId, shot.EraId, ct);
        var liveIgcSha = igc is null ? "" : (string.IsNullOrWhiteSpace(igc.ContractSha256)
            ? ImageGenerationContractRules.HashCanonical(JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(igc.PayloadJson) ? "{}" : igc.PayloadJson))
            : igc.ContractSha256);
        var bytes = execution is null ? null : _artifacts.Read(execution.ArtifactPath);
        var liveArt = bytes is { Length: > 0 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
        var qa = ReadQa(execution?.QaJson);
        var gate = ImageGenerationDirectorReviewRules.Evaluate(new ImageGenerationDirectorReviewRules.ReviewInput(
            execution?.ExecutionStatus ?? "",
            ImageGenerationDirectorReviewRules.IsImageApproved(execution?.ExecutionStatus) ? "APPROVED"
                : ImageGenerationDirectorReviewRules.IsImageRejected(execution?.ExecutionStatus) ? "REJECTED" : "PENDING",
            gov.Master == "LOCKED", gov.Dna == "LOCKED", gov.Prp == "LOCKED",
            contract?.ContractStatus ?? "",
            prompt?.PromptStatus ?? "",
            igc?.ContractStatus ?? "",
            qa?.Technical ?? "", qa?.Character ?? "", qa?.Identity ?? "", qa?.Continuity ?? "", qa?.Composition ?? "",
            qa?.P0 ?? 1,
            bytes is { Length: > 0 }, bytes is { Length: > 0 },
            execution?.MasterSha256 ?? "", gov.MasterSha256 ?? "",
            execution?.DnaSha256 ?? "", gov.DnaSha256 ?? "",
            execution?.PrpSha256 ?? "", gov.PrpSha256 ?? "",
            execution?.ShotContractSha256 ?? "", liveContractSha,
            execution?.PromptSha256 ?? "", prompt?.PromptSha256 ?? "",
            execution?.IgcSha256 ?? "", liveIgcSha,
            execution?.ArtifactSha256 ?? "", liveArt));
        return new Ctx(shot, execution, igc, contract, prompt, gov, liveContractSha, liveIgcSha, qa, bytes, gate);
    }

    private ImageGenerationDirectorReviewRepository.Row NewPending(Ctx ctx, string actor) => new()
    {
        Id = Guid.NewGuid(),
        ShotId = ctx.Shot.Id,
        ExecutionId = ctx.Execution!.Id,
        CharacterId = ctx.Shot.CharacterId,
        EraId = ctx.Shot.EraId,
        SeriesId = ctx.Igc?.SeriesId ?? "FAMIXA",
        MasterSha256 = ctx.Execution.MasterSha256,
        DnaSha256 = ctx.Execution.DnaSha256,
        PrpSha256 = ctx.Execution.PrpSha256,
        ShotContractSha256 = ctx.Execution.ShotContractSha256,
        PromptSha256 = ctx.Execution.PromptSha256,
        ImageGenerationContractSha256 = ctx.Execution.IgcSha256,
        ArtifactSha256 = ctx.Execution.ArtifactSha256,
        CreatedBy = actor,
        UpdatedBy = actor,
    };

    private static ImageGenerationDirectorReviewDto ToDto(Ctx ctx, ImageGenerationDirectorReviewRepository.Row? review)
    {
        var approval = review?.DirectorApproval
            ?? (ImageGenerationDirectorReviewRules.IsImageApproved(ctx.Execution?.ExecutionStatus) ? "APPROVED"
                : ImageGenerationDirectorReviewRules.IsImageRejected(ctx.Execution?.ExecutionStatus) ? "REJECTED"
                : "PENDING");
        var url = ctx.Execution is null
            ? ""
            : $"/api/content/video-engine/shots/{ctx.Shot.Id}/image-generation-execution/{ctx.Execution.Id}/image";
        return new ImageGenerationDirectorReviewDto(
            review?.Id, ctx.Shot.Id, ctx.Execution?.Id, ctx.Igc?.SeriesId ?? "FAMIXA",
            ctx.Shot.CharacterId, ctx.Shot.CharacterName, ctx.Shot.EraId, ctx.Shot.ShotCode,
            review?.ReviewVersion ?? "V1",
            review?.ReviewStatus ?? (ctx.Gate.Status == "READY" ? "PENDING" : "PENDING"),
            ImageGenerationDirectorReviewRules.DocumentId,
            approval, review?.DirectorId, review?.DirectorAt, review?.RejectionReason,
            ctx.Gov.Master, ctx.Gov.Dna, ctx.Gov.Prp, ctx.Gov.GovernanceEngine,
            ctx.Contract?.ContractStatus ?? "MISSING",
            ctx.Prompt?.PromptStatus ?? "MISSING",
            ctx.Igc?.ContractStatus ?? "MISSING",
            ctx.Execution?.ExecutionStatus ?? "MISSING",
            ctx.Execution?.ProviderStatus ?? "",
            ctx.Qa,
            ctx.Execution?.MasterSha256 ?? ctx.Gov.MasterSha256 ?? "",
            ctx.Execution?.DnaSha256 ?? ctx.Gov.DnaSha256 ?? "",
            ctx.Execution?.PrpSha256 ?? ctx.Gov.PrpSha256 ?? "",
            ctx.Execution?.ShotContractSha256 ?? ctx.LiveContractSha,
            ctx.Execution?.PromptSha256 ?? ctx.Prompt?.PromptSha256 ?? "",
            ctx.Execution?.IgcSha256 ?? ctx.LiveIgcSha,
            ctx.Execution?.ArtifactSha256 ?? "",
            ctx.Execution?.ArtifactPath,
            url,
            ctx.ArtifactBytes is { Length: > 0 },
            ctx.Gate.CanApprove && approval == "PENDING",
            ctx.Gate.CanReject && approval == "PENDING",
            false,
            review?.ReviewStatus is "APPROVED" or "REJECTED",
            ctx.Gate.Blocks.Select(b => new ImageGenerationExecutionBlockDto(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message)).ToList());
    }

    private static ImageGenerationExecutionQaDto? ReadQa(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            string S(params string[] names)
            {
                foreach (var n in names)
                    if (r.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String)
                        return v.GetString() ?? "";
                return "";
            }
            var p0 = 0;
            if (r.TryGetProperty("P0", out var p) || r.TryGetProperty("p0", out p))
                p0 = p.TryGetInt32(out var i) ? i : 0;
            var reasons = new List<string>();
            if (r.TryGetProperty("Reasons", out var rs) || r.TryGetProperty("reasons", out rs))
            {
                if (rs.ValueKind == JsonValueKind.Array)
                    reasons.AddRange(rs.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0));
            }
            return new ImageGenerationExecutionQaDto(
                S("Technical", "technical"), S("Character", "character"), S("Identity", "identity"),
                S("Continuity", "continuity"), S("Composition", "composition"), p0, S("Overall", "overall"), reasons);
        }
        catch { return null; }
    }

    private static ImageGenerationDirectorReviewException Fail(
        ImageGenerationDirectorReviewRules.Block first,
        IReadOnlyList<ImageGenerationDirectorReviewRules.Block> blocks) =>
        new(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative,
            blocks.Select(b => new ImageGenerationExecutionBlockDto(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message)).ToList());
}
