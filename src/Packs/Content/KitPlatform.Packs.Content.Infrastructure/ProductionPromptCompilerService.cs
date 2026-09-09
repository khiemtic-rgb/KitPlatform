using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionPromptCompilerService : IProductionPromptCompiler
{
    private readonly ProductionPromptCompilerRepository _repo;
    private readonly ProductionShotContractRepository _contracts;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly ICharacterIdentityGovernanceService _governance;

    public ProductionPromptCompilerService(
        ProductionPromptCompilerRepository repo,
        ProductionShotContractRepository contracts,
        KitVideoProductionShotRepository shots,
        KitVideoCharacterDnaRepository dna,
        ICharacterIdentityGovernanceService governance)
    {
        _repo = repo;
        _contracts = contracts;
        _shots = shots;
        _dna = dna;
        _governance = governance;
    }

    public IReadOnlyList<string> RunRegression() => ProductionPromptCompilerV1Regression.Run();

    public async Task<ProductionPromptCompilerDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var shot = await RequireShot(shotId, cancellationToken);
        var contract = await _contracts.GetLatestAsync(shotId, cancellationToken);
        var gov = await _governance.GetAsync(shot.CharacterId, shot.EraId, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        if (row is { PromptStatus: "COMPILED" or "SUPERSEDED" })
            return ToDto(row, gov, []);
        return NotReadyDto(shot, contract, gov, PreviewBlocks(contract, gov));
    }

    public async Task<ProductionPromptCompilerDto> CompileAsync(Guid shotId, string actor, CancellationToken cancellationToken = default)
    {
        var shot = await RequireShot(shotId, cancellationToken);
        var contract = await _contracts.GetLatestAsync(shotId, cancellationToken);
        await _repo.InsertEventAsync(null, shotId, contract?.Id, "PROMPT_COMPILE_REQUESTED",
            new { character = shot.CharacterId, shot = shot.ShotCode, generate = false }, actor, cancellationToken);

        if (contract is null)
        {
            var missing = Block("PROMPT_COMPILER_NOT_READY", "CONTRACT", "contract", null, "DIRECTOR_APPROVED", "PROMPT_COMPILER_NOT_READY: chưa có Production Shot Contract.");
            await AuditBlocked(shotId, null, missing, actor, cancellationToken);
            throw Fail(missing, null);
        }

        if (!ProductionShotContractRules.SameTenant(shot.CharacterId, contract.CharacterId, contract.SeriesId, "FAMIXA"))
        {
            var own = Block("PROMPT_COMPILER_NOT_READY", "OWNERSHIP", "characterId", contract.CharacterId, shot.CharacterId, "Character/Shot/Contract không cùng tenant.");
            await AuditBlocked(shotId, contract.Id, own, actor, cancellationToken);
            throw Fail(own, null);
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(contract.PayloadJson) ? "{}" : contract.PayloadJson);
        var liveContractSha = ProductionShotContractRules.HashCanonical(payload);
        var spec = ProductionShotContractRules.ToGovernanceShotSpec(payload);
        var promptSurface = ProductionShotContractRules.ToGovernancePrompt(payload);
        var gov = await _governance.ProductionGateAsync(
            shot.CharacterId,
            new CharacterIdentityGovernanceCheckRequest(spec, promptSurface, shot.Id),
            actor,
            cancellationToken);
        if (gov.Generate)
            throw new ProductionPromptCompilerException("PROMPT_COMPILER_BLOCKED", "generation=false.", "COMPILER", "generate", "true", "false", gov with { Generate = false });

        var dnaRow = await _dna.GetByVersionAsync(
            CharacterIdentityGovernanceRules.NormalizeCharacterId(shot.CharacterId), shot.EraId, "V1", cancellationToken);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dnaRow?.SpecJson) ? "{}" : dnaRow!.SpecJson);
        var authorityForbidden = ProductionPromptCompilerRules.ExtractDnaForbidden(dnaSpec);
        var govConflicts = gov.Conflicts.Select(c => CharacterIdentityGovernanceRules.Block(c.Code, c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message)).ToList();

        var input = new ProductionPromptCompilerRules.CompileInput(
            contract.ContractStatus,
            payload,
            contract.ContractSha256,
            liveContractSha,
            contract.MasterSha256,
            gov.MasterSha256 ?? "",
            contract.DnaSha256,
            gov.DnaSha256 ?? "",
            contract.PrpSha256,
            gov.PrpSha256 ?? "",
            gov.Master == "LOCKED",
            gov.Dna == "LOCKED",
            gov.Prp == "LOCKED",
            gov.Status == "PASS" && gov.ProductionAllowed,
            govConflicts,
            shot.CharacterId,
            contract.Id,
            shot.Id,
            contract.ContractVersion,
            gov.MasterId,
            gov.DnaId,
            gov.PrpId,
            authorityForbidden);

        var result = ProductionPromptCompilerRules.Evaluate(input);
        if (result.Status != "COMPILED" || string.IsNullOrWhiteSpace(result.Prompt))
        {
            var mapped = result.Blocks.Select(b => new ProductionPromptCompilerBlockDto(b.Status, b.Code, b.Source, b.Attribute, b.Requested, b.Authoritative, b.Message)).ToList();
            var first = mapped.FirstOrDefault() ?? Block("PROMPT_COMPILER_NOT_READY", "COMPILER", "status", result.Status, "COMPILED", "PROMPT_COMPILER_NOT_READY");
            await AuditBlocked(shotId, contract.Id, first, actor, cancellationToken, mapped, contract, result);
            throw Fail(first, gov, mapped);
        }

        var existing = await _repo.GetCompiledByContractShaAsync(shotId, liveContractSha, cancellationToken);
        if (existing is not null)
            return ToDto(existing, gov, []);

        var latest = await _repo.GetLatestAsync(shotId, cancellationToken);
        var version = latest is { PromptStatus: "COMPILED" }
            ? ProductionPromptCompilerRules.NextVersion(latest.PromptVersion)
            : "V1";
        var row = new ProductionPromptCompilerRepository.PromptRow
        {
            Id = Guid.NewGuid(),
            ShotId = shot.Id,
            ContractId = contract.Id,
            SeriesId = contract.SeriesId,
            CharacterId = shot.CharacterId,
            EraId = shot.EraId,
            ContractVersion = contract.ContractVersion,
            PromptVersion = version,
            PromptStatus = "COMPILED",
            PromptText = result.Prompt,
            NegativeJson = JsonSerializer.Serialize(result.NegativeConstraints),
            PromptSha256 = result.PromptSha256 ?? "",
            ContractSha256 = liveContractSha,
            MasterId = gov.MasterId,
            MasterSha256 = gov.MasterSha256 ?? "",
            DnaId = gov.DnaId,
            DnaSha256 = gov.DnaSha256 ?? "",
            PrpId = gov.PrpId,
            PrpSha256 = gov.PrpSha256 ?? "",
            CreatedBy = actor,
        };
        row = await _repo.InsertAsync(row, cancellationToken);
        if (latest is { PromptStatus: "COMPILED" } && latest.Id != row.Id)
            await _repo.MarkSupersededAsync(latest.Id, row.Id, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, contract.Id, "PROMPT_COMPILED", new
        {
            character = shot.CharacterId,
            shot = shot.ShotCode,
            contract = contract.Id,
            contract_sha = liveContractSha,
            master_sha = row.MasterSha256,
            dna_sha = row.DnaSha256,
            prp_sha = row.PrpSha256,
            prompt_sha = row.PromptSha256,
            status = "COMPILED",
            generate = false,
        }, actor, cancellationToken);
        return ToDto(row, gov, []);
    }

    private async Task<KitVideoProductionShotRepository.ShotRow> RequireShot(Guid shotId, CancellationToken ct) =>
        await _shots.GetByIdAsync(shotId, ct)
        ?? throw new ProductionPromptCompilerException("PROMPT_COMPILER_NOT_READY", "PROMPT_COMPILER_NOT_READY: Production Shot không tồn tại.", "SHOT", "shotId");

    private static IReadOnlyList<ProductionPromptCompilerBlockDto> PreviewBlocks(
        ProductionShotContractRepository.ContractRow? contract, CharacterIdentityGovernanceDto gov)
    {
        if (contract is null)
            return [Block("PROMPT_COMPILER_NOT_READY", "CONTRACT", "contract", null, "DIRECTOR_APPROVED", "Chưa có Production Shot Contract.")];
        if (!ProductionPromptCompilerRules.IsApproved(contract.ContractStatus))
            return [Block("PROMPT_COMPILER_NOT_READY", "CONTRACT", "status", contract.ContractStatus, "DIRECTOR_APPROVED", "Contract phải DIRECTOR_APPROVED trước khi compile.")];
        if (gov.Status != "PASS")
            return [Block("PROMPT_COMPILER_IDENTITY_CONFLICT", "GOVERNANCE", "identity", gov.RequestedValue, gov.AuthoritativeValue, gov.Message ?? "Identity Governance FAIL.")];
        return [];
    }

    private ProductionPromptCompilerDto NotReadyDto(
        KitVideoProductionShotRepository.ShotRow shot,
        ProductionShotContractRepository.ContractRow? contract,
        CharacterIdentityGovernanceDto gov,
        IReadOnlyList<ProductionPromptCompilerBlockDto> blocks) =>
        new(
            null, shot.Id, contract?.Id, contract?.SeriesId ?? "FAMIXA", shot.CharacterId, shot.EraId,
            contract?.ContractVersion ?? "V1", "V1", blocks.Count == 0 ? "NOT_READY" : "BLOCKED",
            ProductionPromptCompilerRules.DocumentId, null, [], "",
            contract?.ContractSha256 ?? "", gov.MasterId, gov.MasterSha256 ?? "", gov.DnaId, gov.DnaSha256 ?? "",
            gov.PrpId, gov.PrpSha256 ?? "", null, null, false, false,
            gov.Master, gov.Dna, gov.Prp, gov.GovernanceEngine,
            contract?.ContractStatus ?? "MISSING",
            ProductionPromptCompilerRules.IsApproved(contract?.ContractStatus) ? "APPROVED" : "PENDING",
            Provenance(shot, contract, gov, null),
            blocks, gov);

    private static ProductionPromptCompilerDto ToDto(
        ProductionPromptCompilerRepository.PromptRow row,
        CharacterIdentityGovernanceDto gov,
        IReadOnlyList<ProductionPromptCompilerBlockDto> blocks)
    {
        var negatives = JsonSerializer.Deserialize<List<string>>(string.IsNullOrWhiteSpace(row.NegativeJson) ? "[]" : row.NegativeJson) ?? [];
        return new ProductionPromptCompilerDto(
            row.Id, row.ShotId, row.ContractId, row.SeriesId, row.CharacterId, row.EraId,
            row.ContractVersion, row.PromptVersion, row.PromptStatus, row.DocumentId,
            row.PromptText, negatives, row.PromptSha256, row.ContractSha256,
            row.MasterId, row.MasterSha256, row.DnaId, row.DnaSha256, row.PrpId, row.PrpSha256,
            row.CreatedAt, row.CreatedBy, row.PromptStatus == "COMPILED", false,
            gov.Master, gov.Dna, gov.Prp, gov.GovernanceEngine,
            "DIRECTOR_APPROVED",
            "APPROVED",
            new ProductionPromptProvenanceDto(
                row.MasterSha256, row.DnaSha256, row.PrpSha256, row.ContractSha256, row.PromptSha256,
                row.PromptVersion, row.ContractVersion, row.ContractId, row.ShotId, row.CharacterId),
            blocks, gov);
    }

    private static ProductionPromptProvenanceDto Provenance(
        KitVideoProductionShotRepository.ShotRow shot,
        ProductionShotContractRepository.ContractRow? contract,
        CharacterIdentityGovernanceDto gov,
        ProductionPromptCompilerRepository.PromptRow? prompt) =>
        new(gov.MasterSha256, gov.DnaSha256, gov.PrpSha256, contract?.ContractSha256, prompt?.PromptSha256,
            prompt?.PromptVersion, contract?.ContractVersion, contract?.Id, shot.Id, shot.CharacterId);

    private async Task AuditBlocked(
        Guid shotId,
        Guid? contractId,
        ProductionPromptCompilerBlockDto first,
        string actor,
        CancellationToken ct,
        IReadOnlyList<ProductionPromptCompilerBlockDto>? all = null,
        ProductionShotContractRepository.ContractRow? contract = null,
        ProductionPromptCompilerRules.CompileOutput? result = null)
    {
        await _repo.InsertEventAsync(null, shotId, contractId, "PROMPT_COMPILE_BLOCKED", new
        {
            character = contract?.CharacterId,
            shot = shotId,
            contract = contractId,
            contract_sha = contract?.ContractSha256,
            master_sha = contract?.MasterSha256,
            dna_sha = contract?.DnaSha256,
            prp_sha = contract?.PrpSha256,
            prompt_sha = result?.PromptSha256,
            status = "BLOCKED",
            reason = first.Message,
            code = first.Code,
            blocks = all ?? [first],
            generate = false,
        }, actor, ct);
    }

    private static ProductionPromptCompilerBlockDto Block(string code, string source, string attribute, string? requested, string? authoritative, string message) =>
        new("BLOCKED", code, source, attribute, requested, authoritative, message);

    private static ProductionPromptCompilerException Fail(
        ProductionPromptCompilerBlockDto first,
        CharacterIdentityGovernanceDto? gov,
        IReadOnlyList<ProductionPromptCompilerBlockDto>? blocks = null) =>
        new(first.Code, first.Message, first.Source, first.Attribute, first.Requested, first.Authoritative, gov, blocks ?? [first]);
}
