using System.Linq;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionShotContractService : IProductionShotContractService
{
    private readonly ProductionShotContractRepository _repo;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly ICharacterIdentityGovernanceService _governance;

    public ProductionShotContractService(
        ProductionShotContractRepository repo,
        KitVideoProductionShotRepository shots,
        ICharacterIdentityGovernanceService governance)
    {
        _repo = repo;
        _shots = shots;
        _governance = governance;
    }

    public IReadOnlyList<string> RunRegression() => ProductionShotContractV1Regression.Run();

    public async Task<ProductionShotContractDto> GetAsync(Guid shotId, CancellationToken cancellationToken = default)
    {
        var shot = await RequireShot(shotId, cancellationToken);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken);
        var gov = await _governance.GetAsync(shot.CharacterId, shot.EraId, cancellationToken);
        if (row is null)
            return EmptyDraft(shot, gov);
        return await ToDto(row, gov, cancellationToken);
    }

    public Task<ProductionShotContractDto> SaveAsync(Guid shotId, JsonElement payload, string actor, CancellationToken cancellationToken = default) =>
        PersistAsync(shotId, payload, actor, validateOnly: false, cancellationToken);

    public async Task<ProductionShotContractDto> ValidateAsync(Guid shotId, JsonElement? payload, string actor, CancellationToken cancellationToken = default)
    {
        if (payload is { ValueKind: JsonValueKind.Object } body)
            return await PersistAsync(shotId, body, actor, validateOnly: true, cancellationToken);
        var existing = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID: chưa có contract.");
        return await PersistAsync(shotId, JsonSerializer.Deserialize<JsonElement>(existing.PayloadJson), actor, validateOnly: true, cancellationToken);
    }

    public async Task<ProductionShotContractDto> ApproveAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actor) || actor.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID: chỉ Director được duyệt.");
        var validated = await ValidateAsync(shotId, null, actor, cancellationToken);
        if (validated.ContractValidation != "PASS" || validated.GovernanceEngine != "PASS")
            throw new ProductionShotContractException(
                validated.GovernanceEngine != "PASS" ? "SHOT_CONTRACT_IDENTITY_CONFLICT" : "SHOT_CONTRACT_INVALID",
                validated.GovernanceEngine != "PASS" ? "SHOT_CONTRACT_IDENTITY_CONFLICT" : "SHOT_CONTRACT_INVALID",
                validated.Governance,
                []);
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID: chưa có contract.");
        if (ProductionShotContractRules.IsApproved(row.ContractStatus))
            return validated;
        var payload = JsonSerializer.Deserialize<JsonElement>(row.PayloadJson);
        var sha = ProductionShotContractRules.HashCanonical(payload);
        var canonical = ProductionShotContractRules.CanonicalJson(ProductionShotContractRules.HashSurface(payload));
        await _repo.ApproveAsync(row.Id, actor, sha, canonical, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "CONTRACT_APPROVED", new { note, sha, generate = false }, actor, cancellationToken);
        return await GetAsync(shotId, cancellationToken);
    }

    public async Task<ProductionShotContractDto> RejectAsync(Guid shotId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetLatestAsync(shotId, cancellationToken)
            ?? throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID: chưa có contract.");
        if (ProductionShotContractRules.IsApproved(row.ContractStatus))
            throw new ProductionShotContractException("SHOT_CONTRACT_LOCKED", "SHOT_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.");
        await _repo.RejectAsync(row.Id, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, shotId, "CONTRACT_REJECTED", new { note, generate = false }, actor, cancellationToken);
        return await GetAsync(shotId, cancellationToken);
    }

    private async Task<ProductionShotContractDto> PersistAsync(
        Guid shotId, JsonElement payload, string actor, bool validateOnly, CancellationToken ct)
    {
        var shot = await RequireShot(shotId, ct);
        var requestedCharacter = ReadString(payload, "characterId");
        var requestedSeries = ReadString(payload, "seriesId");
        if (requestedCharacter.Length > 0 && !ProductionShotContractRules.SameTenant(shot.CharacterId, requestedCharacter, requestedSeries.Length > 0 ? requestedSeries : "FAMIXA", "FAMIXA"))
            throw new ProductionShotContractException("SHOT_CONTRACT_OWNERSHIP", "SHOT_CONTRACT_OWNERSHIP: Character/Shot/Contract không cùng tenant.");
        var gov = await GateAsync(shot, payload, actor, ct);
        if (gov.Status != "PASS" || !gov.ProductionAllowed)
            throw new ProductionShotContractException("SHOT_CONTRACT_IDENTITY_CONFLICT", "SHOT_CONTRACT_IDENTITY_CONFLICT", gov);
        if (HasIdentityRefs(payload) && ShaMismatch(payload, gov))
            throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "CONTRACT_INVALID: identity SHA mismatch. Không auto-fix.", gov);
        StampIdentity(ref payload, shot, gov);
        var issues = ProductionShotContractRules.Validate(payload);
        if (issues.Count > 0)
            throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID", gov, issues);

        var existing = await _repo.GetLatestAsync(shotId, ct);
        var sha = ProductionShotContractRules.HashCanonical(payload);
        var canonical = ProductionShotContractRules.CanonicalJson(ProductionShotContractRules.HashSurface(payload));
        var status = validateOnly ? "VALIDATED" : "DRAFT";
        if (existing is not null && ProductionShotContractRules.IsApproved(existing.ContractStatus))
        {
            if (existing.ContractSha256 == sha)
                return await ToDto(existing, gov, ct);
            var next = NewRow(shot, payload, gov, actor, ProductionShotContractRules.NextVersion(existing.ContractVersion), status, sha, canonical);
            next = await _repo.InsertAsync(next, ct);
            await _repo.MarkSupersededAsync(existing.Id, next.Id, ct);
            await _repo.InsertEventAsync(next.Id, shotId, "CONTRACT_V2_CREATED", new { previous = existing.ContractVersion, generate = false }, actor, ct);
            return await ToDto(next, gov, ct);
        }

        if (existing is null)
        {
            var row = NewRow(shot, payload, gov, actor, "V1", status, sha, canonical);
            row = await _repo.InsertAsync(row, ct);
            await _repo.InsertEventAsync(row.Id, shotId, validateOnly ? "CONTRACT_VALIDATED" : "CONTRACT_SAVED", new { sha, generate = false }, actor, ct);
            return await ToDto(row, gov, ct);
        }

        existing.PayloadJson = payload.GetRawText();
        existing.CanonicalJson = canonical;
        existing.ContractSha256 = sha;
        existing.ContractStatus = status;
        existing.SeriesId = ReadString(payload, "seriesId");
        existing.CharacterId = shot.CharacterId;
        existing.EraId = shot.EraId;
        existing.MasterId = gov.MasterId;
        existing.MasterSha256 = gov.MasterSha256 ?? "";
        existing.DnaId = gov.DnaId;
        existing.DnaSha256 = gov.DnaSha256 ?? "";
        existing.PrpId = gov.PrpId;
        existing.PrpSha256 = gov.PrpSha256 ?? "";
        existing.UpdatedBy = actor;
        existing.ValidatedAt = validateOnly ? DateTimeOffset.UtcNow : existing.ValidatedAt;
        existing.ValidatedBy = validateOnly ? actor : existing.ValidatedBy;
        existing = await _repo.UpdateDraftAsync(existing, ct);
        await _repo.InsertEventAsync(existing.Id, shotId, validateOnly ? "CONTRACT_VALIDATED" : "CONTRACT_SAVED", new { sha, generate = false }, actor, ct);
        return await ToDto(existing, gov, ct);
    }

    private async Task<CharacterIdentityGovernanceDto> GateAsync(
        KitVideoProductionShotRepository.ShotRow shot, JsonElement payload, string actor, CancellationToken ct)
    {
        var spec = ProductionShotContractRules.ToGovernanceShotSpec(payload);
        var prompt = ProductionShotContractRules.ToGovernancePrompt(payload);
        var gov = await _governance.ProductionGateAsync(
            shot.CharacterId,
            new CharacterIdentityGovernanceCheckRequest(spec, prompt, shot.Id),
            actor,
            ct);
        if (gov.Generate)
            throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID: generate=false.", gov with { Generate = false });
        return gov;
    }

    private static void StampIdentity(ref JsonElement payload, KitVideoProductionShotRepository.ShotRow shot, CharacterIdentityGovernanceDto gov)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText()) ?? new();
        dict["shotId"] = JsonSerializer.SerializeToElement(shot.ShotCode);
        dict["characterId"] = JsonSerializer.SerializeToElement(shot.CharacterId);
        dict["eraId"] = JsonSerializer.SerializeToElement(shot.EraId);
        if (!dict.ContainsKey("seriesId") || string.IsNullOrWhiteSpace(dict["seriesId"].GetString()))
            dict["seriesId"] = JsonSerializer.SerializeToElement("FAMIXA");
        dict["identity"] = JsonSerializer.SerializeToElement(new
        {
            masterId = gov.MasterId,
            masterSha256 = gov.MasterSha256,
            dnaId = gov.DnaId,
            dnaSha256 = gov.DnaSha256,
            prpId = gov.PrpId,
            prpSha256 = gov.PrpSha256,
        });
        payload = JsonSerializer.SerializeToElement(dict);
    }

    private static bool HasIdentityRefs(JsonElement payload)
    {
        if (!payload.TryGetProperty("identity", out var ident) || ident.ValueKind != JsonValueKind.Object)
            return false;
        return new[] { "masterSha256", "dnaSha256", "prpSha256", "masterId", "dnaId", "prpId" }
            .Any(k => ReadString(ident, k).Length > 0);
    }

    private static bool ShaMismatch(JsonElement payload, CharacterIdentityGovernanceDto gov)
    {
        if (!payload.TryGetProperty("identity", out var ident) || ident.ValueKind != JsonValueKind.Object)
            return true;
        return !CharacterIdentityGovernanceRules.SameSha(ReadString(ident, "masterSha256"), gov.MasterSha256)
            || !CharacterIdentityGovernanceRules.SameSha(ReadString(ident, "dnaSha256"), gov.DnaSha256)
            || !CharacterIdentityGovernanceRules.SameSha(ReadString(ident, "prpSha256"), gov.PrpSha256);
    }

    private async Task<KitVideoProductionShotRepository.ShotRow> RequireShot(Guid shotId, CancellationToken ct) =>
        await _shots.GetByIdAsync(shotId, ct)
        ?? throw new ProductionShotContractException("SHOT_CONTRACT_INVALID", "SHOT_CONTRACT_INVALID: Production Shot không tồn tại.");

    private ProductionShotContractRepository.ContractRow NewRow(
        KitVideoProductionShotRepository.ShotRow shot,
        JsonElement payload,
        CharacterIdentityGovernanceDto gov,
        string actor,
        string version,
        string status,
        string sha,
        string canonical) => new()
    {
        Id = Guid.NewGuid(),
        ShotId = shot.Id,
        SeriesId = ReadString(payload, "seriesId"),
        CharacterId = shot.CharacterId,
        EraId = shot.EraId,
        ContractVersion = version,
        ContractStatus = status,
        PayloadJson = payload.GetRawText(),
        CanonicalJson = canonical,
        ContractSha256 = sha,
        MasterId = gov.MasterId,
        MasterSha256 = gov.MasterSha256 ?? "",
        DnaId = gov.DnaId,
        DnaSha256 = gov.DnaSha256 ?? "",
        PrpId = gov.PrpId,
        PrpSha256 = gov.PrpSha256 ?? "",
        CreatedBy = actor,
        UpdatedBy = actor,
        ValidatedAt = status == "VALIDATED" ? DateTimeOffset.UtcNow : null,
        ValidatedBy = status == "VALIDATED" ? actor : null,
    };

    private ProductionShotContractDto EmptyDraft(KitVideoProductionShotRepository.ShotRow shot, CharacterIdentityGovernanceDto gov)
    {
        var payload = ProductionShotContractRules.BuildValidFixture(
            shot.CharacterId, "FAMIXA", shot.EraId,
            gov.MasterId ?? Guid.Empty, gov.MasterSha256 ?? "",
            gov.DnaId ?? Guid.Empty, gov.DnaSha256 ?? "",
            gov.PrpId ?? Guid.Empty, gov.PrpSha256 ?? "",
            shot.ShotCode);
        var issues = ProductionShotContractRules.Validate(payload);
        return new ProductionShotContractDto(
            null, shot.Id, "FAMIXA", shot.CharacterId, shot.EraId, "V1", "DRAFT",
            ProductionShotContractRules.DocumentId, payload, "", "",
            gov.MasterId, gov.MasterSha256 ?? "", gov.DnaId, gov.DnaSha256 ?? "", gov.PrpId, gov.PrpSha256 ?? "",
            null, null, null, null, null, null, null, null,
            false, false,
            gov.GovernanceEngine, issues.Count == 0 && gov.Status == "PASS" ? "PENDING" : "BLOCKED", "PENDING",
            gov.Master, gov.Dna, gov.Prp,
            issues.Select(i => new KitVideoDnaCheckItemDto(i.Code, i.Attribute, false, i.Message)).ToList(),
            gov);
    }

    private Task<ProductionShotContractDto> ToDto(
        ProductionShotContractRepository.ContractRow row, CharacterIdentityGovernanceDto gov, CancellationToken ct)
    {
        var payload = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
        var issues = ProductionShotContractRules.Validate(payload);
        var validation = issues.Count == 0 && gov.Status == "PASS" ? "PASS" : "BLOCKED";
        return Task.FromResult(new ProductionShotContractDto(
            row.Id, row.ShotId, row.SeriesId, row.CharacterId, row.EraId, row.ContractVersion, row.ContractStatus,
            row.DocumentId, payload, row.CanonicalJson, row.ContractSha256,
            row.MasterId, row.MasterSha256, row.DnaId, row.DnaSha256, row.PrpId, row.PrpSha256,
            row.CreatedAt, row.CreatedBy, row.UpdatedAt, row.UpdatedBy, row.ValidatedAt, row.ValidatedBy, row.ApprovedAt, row.ApprovedBy,
            ProductionShotContractRules.IsApproved(row.ContractStatus),
            false,
            gov.GovernanceEngine,
            validation,
            ProductionShotContractRules.IsApproved(row.ContractStatus) ? "APPROVED" : "PENDING",
            gov.Master, gov.Dna, gov.Prp,
            issues.Select(i => new KitVideoDnaCheckItemDto(i.Code, i.Attribute, false, i.Message)).ToList(),
            gov));
    }

    private static string ReadString(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString() ?? ""
            : "";
}
