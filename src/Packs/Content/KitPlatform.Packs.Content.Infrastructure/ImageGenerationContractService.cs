using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ImageGenerationContractService : IImageGenerationContractService
{
    private readonly ImageGenerationContractRepository _repo;
    private readonly ProductionShotContractRepository _contracts;
    private readonly ProductionPromptCompilerRepository _prompts;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly ICharacterIdentityGovernanceService _governance;

    public ImageGenerationContractService(
        ImageGenerationContractRepository repo,
        ProductionShotContractRepository contracts,
        ProductionPromptCompilerRepository prompts,
        KitVideoProductionShotRepository shots,
        KitVideoCharacterDnaRepository dna,
        ICharacterIdentityGovernanceService governance)
    {
        _repo = repo;
        _contracts = contracts;
        _prompts = prompts;
        _shots = shots;
        _dna = dna;
        _governance = governance;
    }

    public IReadOnlyList<string> RunRegression() => ImageGenerationContractV1Regression.Run();

    public async Task<ImageGenerationContractDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(shotId, "director", writeGate: false, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        if (row is not null)
            return ToDto(row, ctx.Gov, ctx.Contract, ctx.Prompt, []);
        return Preview(ctx);
    }

    public Task<ImageGenerationContractDto> SaveAsync(Guid shotId, ImageGenerationContractWriteRequest? request, string actor, CancellationToken cancellationToken = default) =>
        PersistAsync(shotId, request, actor, validateOnly: false, cancellationToken);

    public Task<ImageGenerationContractDto> ValidateAsync(Guid shotId, ImageGenerationContractWriteRequest? request, string actor, CancellationToken cancellationToken = default) =>
        PersistAsync(shotId, request, actor, validateOnly: true, cancellationToken);

    public async Task<ImageGenerationContractDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_NOT_READY", "Chỉ Director được duyệt.", "DIRECTOR", "actor");
        var ctx = await LoadAsync(shotId, actor, writeGate: true, cancellationToken);
        if (ctx.Gate.Status != "PASS" || ctx.Contract is null || ctx.Prompt is null)
        {
            var first = ctx.Gate.Blocks.FirstOrDefault() ?? new ImageGenerationContractRules.Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY", "AUTHORITY", "contract", null, null, "IMAGE_GENERATION_CONTRACT_NOT_READY");
            throw Fail(first, ctx.Gov, ctx.Gate.Blocks.Select(Map).ToList());
        }
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_NOT_READY", "Chưa có Image Generation Contract.");
        var existingPayload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var overlay = ImageGenerationContractRules.ReadOverlay(existingPayload);
        var expectedSha = ImageGenerationContractRules.HashCanonical(
            ImageGenerationContractRules.WithPolicy(ctx.Payload, overlay.QualityPolicy, overlay.BackgroundPolicy, overlay.OutputFormat));
        if (!CharacterIdentityGovernanceRules.SameSha(row.ContractSha256, expectedSha))
            throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_NOT_READY", "Contract SHA mismatch on approve. Không auto-fix. Không overwrite payload.", "CONTRACT", "contractSha256", row.ContractSha256, expectedSha, ctx.Gov);
        if (ImageGenerationContractRules.IsApproved(row.ContractStatus))
            return ToDto(row, ctx.Gov, ctx.Contract, ctx.Prompt, []);
        await _repo.ApproveAsync(row.Id, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "IMAGE_GENERATION_CONTRACT_APPROVED", new { note, sha = row.ContractSha256, generate = false }, actor, cancellationToken);
        return await GetAsync(shotId, cancellationToken);
    }

    public async Task<ImageGenerationContractDto> RejectAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_NOT_READY", "Chưa có Image Generation Contract.");
        if (ImageGenerationContractRules.IsApproved(row.ContractStatus))
            throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_LOCKED", "IMAGE_GENERATION_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
        await _repo.RejectAsync(row.Id, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "IMAGE_GENERATION_CONTRACT_REJECTED", new { note, generate = false }, actor, cancellationToken);
        return await GetAsync(shotId, cancellationToken);
    }

    private async Task<ImageGenerationContractDto> PersistAsync(
        Guid shotId, ImageGenerationContractWriteRequest? request, string actor, bool validateOnly, CancellationToken ct)
    {
        var ctx = await LoadAsync(shotId, actor, writeGate: true, ct);
        if (ctx.Gate.Status != "PASS" || ctx.Contract is null || ctx.Prompt is null)
        {
            var first = ctx.Gate.Blocks.FirstOrDefault() ?? new ImageGenerationContractRules.Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY", "AUTHORITY", "contract", null, null, "IMAGE_GENERATION_CONTRACT_NOT_READY");
            await _repo.InsertEventAsync(null, shotId, "IMAGE_GENERATION_CONTRACT_BLOCKED", new { blocks = ctx.Gate.Blocks, generate = false }, actor, ct);
            throw Fail(first, ctx.Gov, ctx.Gate.Blocks.Select(Map).ToList());
        }

        var existing = await _repo.GetLatestAsync(shotId, ct);
        JsonElement? existingPayload = existing is null
            ? null
            : JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(existing.PayloadJson) ? "{}" : existing.PayloadJson);
        var overlay = ImageGenerationContractRules.ResolveOverlay(request?.QualityPolicy, request?.BackgroundPolicy, request?.OutputFormat, existingPayload);
        var payload = ImageGenerationContractRules.WithPolicy(ctx.Payload, overlay.QualityPolicy, overlay.BackgroundPolicy, overlay.OutputFormat);
        if (ProductionPromptCompilerRules.ContainsProviderSyntax(payload.GetRawText()))
            throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_INVALID", "Provider-specific token. Không persist.", "PROVIDER", "provider", null, "EXTERNAL", ctx.Gov);

        var sha = ImageGenerationContractRules.HashCanonical(payload);
        var canonical = ProductionShotContractRules.CanonicalJson(ImageGenerationContractRules.HashSurface(payload));
        var status = validateOnly ? "VALIDATED" : "DRAFT";

        if (existing is not null && ImageGenerationContractRules.IsApproved(existing.ContractStatus))
        {
            if (existing.ContractSha256 == sha)
                return ToDto(existing, ctx.Gov, ctx.Contract, ctx.Prompt, []);
            var next = NewRow(ctx, payload, sha, canonical, actor, ImageGenerationContractRules.NextVersion(existing.ContractVersion), status);
            next = await _repo.InsertAsync(next, ct);
            await _repo.MarkSupersededAsync(existing.Id, next.Id, ct);
            await _repo.InsertEventAsync(next.Id, shotId, "IMAGE_GENERATION_CONTRACT_V2_CREATED", new { previous = existing.ContractVersion, generate = false }, actor, ct);
            return ToDto(next, ctx.Gov, ctx.Contract, ctx.Prompt, []);
        }

        if (existing is null)
        {
            var row = NewRow(ctx, payload, sha, canonical, actor, "V1", status);
            row = await _repo.InsertAsync(row, ct);
            await _repo.InsertEventAsync(row.Id, shotId, validateOnly ? "IMAGE_GENERATION_CONTRACT_VALIDATED" : "IMAGE_GENERATION_CONTRACT_SAVED", new { sha, generate = false }, actor, ct);
            return ToDto(row, ctx.Gov, ctx.Contract, ctx.Prompt, []);
        }

        existing.PayloadJson = payload.GetRawText();
        existing.CanonicalJson = canonical;
        existing.ContractSha256 = sha;
        existing.ContractStatus = status;
        existing.ShotContractId = ctx.Contract.Id;
        existing.PromptId = ctx.Prompt.Id;
        existing.MasterId = ctx.Gov.MasterId;
        existing.MasterSha256 = ctx.Gov.MasterSha256 ?? "";
        existing.DnaId = ctx.Gov.DnaId;
        existing.DnaSha256 = ctx.Gov.DnaSha256 ?? "";
        existing.PrpId = ctx.Gov.PrpId;
        existing.PrpSha256 = ctx.Gov.PrpSha256 ?? "";
        existing.ShotContractSha256 = ctx.LiveContractSha;
        existing.PromptSha256 = ctx.Prompt.PromptSha256;
        existing.UpdatedBy = actor;
        existing.ValidatedAt = validateOnly ? DateTimeOffset.UtcNow : existing.ValidatedAt;
        existing.ValidatedBy = validateOnly ? actor : existing.ValidatedBy;
        existing = await _repo.UpdateDraftAsync(existing, ct);
        await _repo.InsertEventAsync(existing.Id, shotId, validateOnly ? "IMAGE_GENERATION_CONTRACT_VALIDATED" : "IMAGE_GENERATION_CONTRACT_SAVED", new { sha, generate = false }, actor, ct);
        return ToDto(existing, ctx.Gov, ctx.Contract, ctx.Prompt, []);
    }

    private sealed record Ctx(
        KitVideoProductionShotRepository.ShotRow Shot,
        ProductionShotContractRepository.ContractRow? Contract,
        ProductionPromptCompilerRepository.PromptRow? Prompt,
        CharacterIdentityGovernanceDto Gov,
        JsonElement ShotPayload,
        JsonElement Payload,
        string LiveContractSha,
        ImageGenerationContractRules.GateOutput Gate);

    private async Task<Ctx> LoadAsync(Guid shotId, string actor, bool writeGate, CancellationToken ct)
    {
        var shot = await _shots.GetByIdAsync(shotId, ct)
            ?? throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_NOT_READY", "Production Shot không tồn tại.", "SHOT", "shotId");
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

        CharacterIdentityGovernanceDto gov;
        if (contract is not null && writeGate)
        {
            gov = await _governance.ProductionGateAsync(
                shot.CharacterId,
                new CharacterIdentityGovernanceCheckRequest(
                    ProductionShotContractRules.ToGovernanceShotSpec(shotPayload),
                    ProductionShotContractRules.ToGovernancePrompt(shotPayload),
                    shot.Id),
                actor, ct);
        }
        else
            gov = await _governance.GetAsync(shot.CharacterId, shot.EraId, ct);
        if (gov.Generate)
            throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_INVALID", "generation=false.", "COMPILER", "generate", "true", "false", gov with { Generate = false });

        if (contract is not null && !ProductionShotContractRules.SameTenant(shot.CharacterId, contract.CharacterId, contract.SeriesId, "FAMIXA"))
            throw new ImageGenerationContractException("IMAGE_GENERATION_CONTRACT_NOT_READY", "Character/Shot/Contract không cùng tenant.", "OWNERSHIP", "characterId", contract.CharacterId, shot.CharacterId, gov);

        var dnaRow = await _dna.GetByVersionAsync(CharacterIdentityGovernanceRules.NormalizeCharacterId(shot.CharacterId), shot.EraId, "V1", ct);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dnaRow?.SpecJson) ? "{}" : dnaRow!.SpecJson);
        var forbidden = ProductionPromptCompilerRules.ExtractDnaForbidden(dnaSpec);
        var payload = ImageGenerationContractRules.BuildPayload(
            shot.CharacterId, shot.Id,
            gov.MasterId, gov.MasterSha256 ?? "",
            gov.DnaId, gov.DnaSha256 ?? "",
            gov.PrpId, gov.PrpSha256 ?? "",
            contract?.Id, liveContractSha,
            prompt?.Id, prompt?.PromptSha256 ?? "",
            shotPayload.ValueKind == JsonValueKind.Object ? shotPayload : JsonSerializer.SerializeToElement(new { }),
            forbidden);

        var govConflicts = gov.Conflicts.Select(c => CharacterIdentityGovernanceRules.Block(c.Code, c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message)).ToList();
        var gate = ImageGenerationContractRules.Evaluate(new ImageGenerationContractRules.GateInput(
            shot.CharacterId,
            gov.Master == "LOCKED", gov.Dna == "LOCKED", gov.Prp == "LOCKED",
            gov.Status == "PASS" && gov.ProductionAllowed, govConflicts,
            contract?.ContractStatus ?? "",
            prompt?.PromptStatus ?? "",
            contract?.MasterSha256 ?? gov.MasterSha256 ?? "", gov.MasterSha256 ?? "",
            contract?.DnaSha256 ?? gov.DnaSha256 ?? "", gov.DnaSha256 ?? "",
            contract?.PrpSha256 ?? gov.PrpSha256 ?? "", gov.PrpSha256 ?? "",
            contract?.ContractSha256 ?? "", liveContractSha,
            prompt?.PromptSha256 ?? "", prompt?.PromptSha256 ?? "",
            prompt?.MasterSha256 ?? "", prompt?.DnaSha256 ?? "", prompt?.PrpSha256 ?? "", prompt?.ContractSha256 ?? "",
            prompt?.PromptText, payload));

        return new Ctx(shot, contract, prompt, gov, shotPayload, payload, liveContractSha, gate);
    }

    private ImageGenerationContractDto Preview(Ctx ctx)
    {
        var blocks = ctx.Gate.Blocks.Select(Map).ToList();
        return new ImageGenerationContractDto(
            null, ctx.Shot.Id, ctx.Contract?.Id, ctx.Prompt?.Id, ctx.Contract?.SeriesId ?? "FAMIXA",
            ctx.Shot.CharacterId, ctx.Shot.EraId, "V1",
            blocks.Count == 0 ? "DRAFT" : "BLOCKED",
            ImageGenerationContractRules.DocumentId,
            ctx.Payload, "", ctx.Gate.ContractSha256 ?? "",
            ctx.Gov.MasterId, ctx.Gov.MasterSha256 ?? "", ctx.Gov.DnaId, ctx.Gov.DnaSha256 ?? "",
            ctx.Gov.PrpId, ctx.Gov.PrpSha256 ?? "", ctx.LiveContractSha, ctx.Prompt?.PromptSha256 ?? "",
            null, null, null, null, null, null, null, null,
            false, false,
            ctx.Gov.Master, ctx.Gov.Dna, ctx.Gov.Prp, ctx.Gov.GovernanceEngine,
            ctx.Contract?.ContractStatus ?? "MISSING", ctx.Prompt?.PromptStatus ?? "MISSING",
            ImageGenerationContractRules.IsApproved(ctx.Contract?.ContractStatus) ? "PENDING" : "PENDING",
            blocks, ctx.Gov);
    }

    private ImageGenerationContractRepository.Row NewRow(
        Ctx ctx, JsonElement payload, string sha, string canonical, string actor, string version, string status) => new()
    {
        Id = Guid.NewGuid(),
        ShotId = ctx.Shot.Id,
        ShotContractId = ctx.Contract!.Id,
        PromptId = ctx.Prompt!.Id,
        SeriesId = ctx.Contract!.SeriesId,
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
        PromptSha256 = ctx.Prompt!.PromptSha256,
        CreatedBy = actor,
        UpdatedBy = actor,
        ValidatedAt = status == "VALIDATED" ? DateTimeOffset.UtcNow : null,
        ValidatedBy = status == "VALIDATED" ? actor : null,
    };

    private static ImageGenerationContractDto ToDto(
        ImageGenerationContractRepository.Row row,
        CharacterIdentityGovernanceDto gov,
        ProductionShotContractRepository.ContractRow? contract,
        ProductionPromptCompilerRepository.PromptRow? prompt,
        IReadOnlyList<ImageGenerationContractBlockDto> blocks)
    {
        var payload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        return new ImageGenerationContractDto(
            row.Id, row.ShotId, row.ShotContractId, row.PromptId, row.SeriesId, row.CharacterId, row.EraId,
            row.ContractVersion, row.ContractStatus, row.DocumentId, payload, row.CanonicalJson, row.ContractSha256,
            row.MasterId, row.MasterSha256, row.DnaId, row.DnaSha256, row.PrpId, row.PrpSha256,
            row.ShotContractSha256, row.PromptSha256,
            row.CreatedAt, row.CreatedBy, row.UpdatedAt, row.UpdatedBy, row.ValidatedAt, row.ValidatedBy, row.ApprovedAt, row.ApprovedBy,
            ImageGenerationContractRules.IsApproved(row.ContractStatus), false,
            gov.Master, gov.Dna, gov.Prp, gov.GovernanceEngine,
            contract?.ContractStatus ?? "DIRECTOR_APPROVED",
            prompt?.PromptStatus ?? "COMPILED",
            ImageGenerationContractRules.IsApproved(row.ContractStatus) ? "APPROVED" : "PENDING",
            blocks, gov);
    }

    private static ImageGenerationContractBlockDto Map(ImageGenerationContractRules.Block b) =>
        new(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message);

    private static ImageGenerationContractBlockDto Block(string code, string source, string attribute, string? requested, string? authoritative, string message) =>
        new("BLOCKED", code, source, attribute, requested, authoritative, message);

    private static ImageGenerationContractException Fail(
        ImageGenerationContractRules.Block first,
        CharacterIdentityGovernanceDto? gov,
        IReadOnlyList<ImageGenerationContractBlockDto>? blocks = null) =>
        new(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative, gov, blocks ?? [Map(first)]);

    private static ImageGenerationContractException Fail(
        ImageGenerationContractBlockDto first,
        CharacterIdentityGovernanceDto? gov,
        IReadOnlyList<ImageGenerationContractBlockDto>? blocks = null) =>
        new(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative, gov, blocks ?? [first]);
}
