using System.Linq;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterIdentityGovernanceService : ICharacterIdentityGovernanceService
{
    private readonly CharacterIdentityGovernanceRepository _audit;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly KitVideoProductionShotRepository _shots;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly KitVideoArtifactStore _store;

    public CharacterIdentityGovernanceService(
        CharacterIdentityGovernanceRepository audit,
        KitVideoMasterLockRepository locks,
        KitVideoCharacterDnaRepository dna,
        KitVideoProductionReferencePackRepository prp,
        KitVideoProductionShotRepository shots,
        KitVideoMasterReferenceRepository candidates,
        KitVideoArtifactStore store)
    {
        _audit = audit;
        _locks = locks;
        _dna = dna;
        _prp = prp;
        _shots = shots;
        _candidates = candidates;
        _store = store;
    }

    public Task<CharacterIdentityGovernanceDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        EvaluateAsync(characterId, eraId, null, null, null, "director", writeAudit: false, cancellationToken);

    public async Task<CharacterIdentityGovernanceDto> CheckAsync(
        string characterId, CharacterIdentityGovernanceCheckRequest request, string actor, CancellationToken cancellationToken = default)
    {
        JsonElement? shot = request.ShotSpec is { ValueKind: JsonValueKind.Object } s ? s : null;
        Guid? shotId = request.ShotId;
        if (shot is null && shotId is { } id)
        {
            var row = await _shots.GetByIdAsync(id, cancellationToken);
            if (row is not null)
                shot = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
        }
        return await EvaluateAsync(characterId, "ERA-01", shot, request.UserPrompt, shotId, actor, writeAudit: true, cancellationToken);
    }

    public async Task<IReadOnlyList<CharacterIdentityGovernanceAuditDto>> ListAuditAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var id = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId);
        var rows = await _audit.ListAsync(id, eraId, cancellationToken);
        return rows.Select(r => new CharacterIdentityGovernanceAuditDto(
            r.Id, r.CharacterId, r.EraId, r.MasterId, r.DnaId, r.PrpId, r.ShotId,
            r.Gate, r.Result, r.Code, r.Source, r.Attribute, r.RequestedValue, r.AuthoritativeValue,
            r.Reason, r.Actor, r.CreatedAt)).ToList();
    }

    public Task<CharacterIdentityGovernanceDto> ProductionGateAsync(
        string characterId, CharacterIdentityGovernanceCheckRequest? request, string actor, CancellationToken cancellationToken = default) =>
        CheckAsync(characterId, request ?? new CharacterIdentityGovernanceCheckRequest(), actor, cancellationToken);

    public IReadOnlyList<string> RunRegression() => CharacterIdentityGovernanceV11Regression.Run();

    private async Task<CharacterIdentityGovernanceDto> EvaluateAsync(
        string characterId, string eraId, JsonElement? shotSpec, string? userPrompt, Guid? shotId,
        string actor, bool writeAudit, CancellationToken ct)
    {
        var id = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId);
        var master = string.IsNullOrWhiteSpace(id) ? null : await _locks.GetByVersionAsync(id, eraId, "V1", ct);
        var dna = string.IsNullOrWhiteSpace(id) ? null : await _dna.GetByVersionAsync(id, eraId, "V1", ct);
        var pack = string.IsNullOrWhiteSpace(id) ? null : await _prp.GetByVersionAsync(id, eraId, "V1", ct);
        if (master is not null
            && !string.IsNullOrWhiteSpace(master.CharacterId)
            && CharacterIdentityGovernanceRules.NormalizeCharacterId(master.CharacterId) != id)
            master = null;
        if (dna is not null
            && !string.IsNullOrWhiteSpace(dna.CharacterId)
            && CharacterIdentityGovernanceRules.NormalizeCharacterId(dna.CharacterId) != id)
            dna = null;
        if (pack is not null
            && !string.IsNullOrWhiteSpace(pack.CharacterId)
            && CharacterIdentityGovernanceRules.NormalizeCharacterId(pack.CharacterId) != id)
            pack = null;
        var masterLocked = master is not null && CharacterIdentityGovernanceRules.IsLocked(master.Status);
        var dnaLocked = dna is not null && CharacterIdentityGovernanceRules.IsLocked(dna.Status);
        var prpLocked = pack is not null && CharacterIdentityGovernanceRules.IsLocked(pack.Status);
        var liveMasterSha = master is null ? "" : LiveSha(master.ArtifactPath);
        var masterShaMatch = master is not null && CharacterIdentityGovernanceRules.SameSha(master.Sha256, liveMasterSha);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dna?.SpecJson) ? "{}" : dna!.SpecJson);
        var dnaSha = dna is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(dna.SpecJson ?? "{}"));
        var dnaShaMatch = CharacterIdentityGovernanceRules.ShaExists(dnaSha)
            && CharacterIdentityGovernanceRules.SameSha(dna?.MasterSha256, master?.Sha256);
        var livePrpSha = pack is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(pack.SpecJson ?? "{}"));
        var prpSha = pack?.PrpSha256 ?? livePrpSha;
        var prpShaMatch = prpLocked
            && CharacterIdentityGovernanceRules.ShaExists(livePrpSha)
            && (string.IsNullOrWhiteSpace(pack?.PrpSha256) || CharacterIdentityGovernanceRules.SameSha(pack.PrpSha256, livePrpSha))
            && (string.IsNullOrWhiteSpace(pack?.MasterSha256) || CharacterIdentityGovernanceRules.SameSha(pack.MasterSha256, master?.Sha256))
            && (string.IsNullOrWhiteSpace(pack?.DnaSha256) || CharacterIdentityGovernanceRules.SameSha(pack.DnaSha256, dnaSha));
        var prpSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(pack?.SpecJson) ? "{}" : pack!.SpecJson);
        var identityTestedPass = master is not null && CharacterIdentityGovernanceRules.ResultPass(master.IdentityTestResult);
        var stressRulesDefined = dnaSpec.ValueKind == JsonValueKind.Object
            && dnaSpec.TryGetProperty("stressRules", out var sr) && sr.ValueKind == JsonValueKind.Object;
        var stressState = CharacterIdentityGovernanceRules.StressState(master?.StressTestResult, stressRulesDefined);
        var p0 = 0;
        if (master is not null)
        {
            var cand = await _candidates.GetCandidateAsync(master.SourceCandidateId, ct);
            if (cand is not null) p0 = ReadP0(cand.QaJson);
        }
        JsonElement? previous = null;
        if (!string.IsNullOrWhiteSpace(id))
        {
            var shots = await _shots.ListAsync(id, eraId, ct);
            var prior = shotId is { } sid
                ? shots.LastOrDefault(s => s.Id != sid)
                : shots.LastOrDefault();
            if (prior is not null)
                previous = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(prior.SpecJson) ? "{}" : prior.SpecJson);
        }
        var authority = new CharacterIdentityGovernanceRules.AuthorityState(
            id, masterLocked, dnaLocked, prpLocked, masterShaMatch, dnaShaMatch, prpShaMatch,
            master?.Status ?? "MISSING", dna?.Status ?? "MISSING", pack?.Status ?? "MISSING",
            master?.Id, dna?.Id, pack?.Id);
        var conflicts = CharacterIdentityGovernanceRules.Evaluate(dnaSpec, prpSpec, shotSpec, previous, userPrompt, authority);
        var continuityPass = !conflicts.Any(c => c.Message.Contains("Continuity", StringComparison.OrdinalIgnoreCase));
        var regressionPass = masterLocked && dnaLocked && prpLocked && masterShaMatch && dnaShaMatch && prpShaMatch
            && !CharacterIdentityGovernanceRules.AutoFix()
            && !CharacterIdentityGovernanceRules.AutoApprove()
            && !CharacterIdentityGovernanceRules.AutoLock()
            && !CharacterIdentityGovernanceRules.CreatesPixels("SPEC")
            && !CharacterIdentityGovernanceRules.TouchesGolden(master?.ArtifactPath)
            && p0 == 0
            && !conflicts.Any(c => c.Code is "DNA_SHOT_CONFLICT" or "DNA_FORBIDDEN_ATTRIBUTE" or "IDENTITY_CONFLICT" or "DNA_CONFLICT" or "PRP_CONFLICT");
        var gates = CharacterIdentityGovernanceRules.EvaluateGates(
            authority, identityTestedPass, stressState, continuityPass, regressionPass, conflicts);
        var overall = CharacterIdentityGovernanceRules.AllPass(gates);
        var ready = CharacterIdentityGovernanceRules.ProductionReady(gates, p0);
        if (writeAudit)
            await WriteGateAuditAsync(id, eraId, master?.Id, dna?.Id, pack?.Id, shotId, actor, gates, conflicts, ready, ct);

        var first = conflicts.FirstOrDefault();
        return new CharacterIdentityGovernanceDto(
            overall ? "PASS" : "BLOCKED",
            string.IsNullOrWhiteSpace(id) ? (characterId ?? "") : id,
            masterLocked ? "LOCKED" : (master?.Status ?? "MISSING"),
            dnaLocked ? "LOCKED" : (dna?.Status ?? "MISSING"),
            prpLocked ? "LOCKED" : (pack?.Status ?? "MISSING"),
            identityTestedPass && !conflicts.Any(c => c.Code is "IDENTITY_CONFLICT" or "DNA_SHOT_CONFLICT" or "DNA_FORBIDDEN_ATTRIBUTE") ? "PASS" : "FAIL",
            CharacterIdentityGovernanceRules.StressGatePass(stressState) ? "PASS" : "FAIL",
            continuityPass ? "PASS" : "FAIL",
            regressionPass ? "PASS" : "FAIL",
            p0,
            ready,
            false,
            first?.Code,
            first?.Source,
            first?.Attribute,
            first?.RequestedValue,
            first?.AuthoritativeValue,
            first?.Message,
            master?.Id,
            master?.Sha256 ?? liveMasterSha,
            dna?.Id,
            dnaSha,
            pack?.Id,
            prpSha,
            shotId,
            gates.Select(g => new KitVideoDnaCheckItemDto(g.Code, g.Label, g.Pass, g.Reason)).ToList(),
            conflicts.Select(c => new CharacterIdentityConflictDto(c.Status, c.Code, c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message)).ToList(),
            new CharacterIdentityPromptContractDto(
                ready, id, master?.Id, master?.Sha256, dna?.Id, dnaSha, pack?.Id, prpSha, shotId,
                ready ? "PROVENANCE_ONLY" : "BLOCKED",
                "Prompt cannot override DNA. Model has no Identity Authority."),
            false, false, false,
            "PENDING",
            overall ? "PASS" : "BLOCKED",
            stressState);
    }

    private async Task WriteGateAuditAsync(
        string characterId, string eraId, Guid? masterId, Guid? dnaId, Guid? prpId, Guid? shotId,
        string actor, IReadOnlyList<CharacterIdentityGovernanceRules.GateItem> gates,
        IReadOnlyList<CharacterIdentityGovernanceRules.Conflict> conflicts, bool ready, CancellationToken ct)
    {
        var rows = gates.Select(g =>
        {
            var related = conflicts.FirstOrDefault(c => GateMatches(g.Code, c));
            return new CharacterIdentityGovernanceRepository.AuditRow
            {
                Id = Guid.NewGuid(),
                CharacterId = characterId,
                EraId = eraId,
                MasterId = masterId,
                DnaId = dnaId,
                PrpId = prpId,
                ShotId = shotId,
                Gate = g.Code,
                Result = g.Pass ? "PASS" : "BLOCKED",
                Code = related?.Code ?? (g.Pass ? null : g.Reason),
                Source = related?.Source,
                Attribute = related?.Attribute,
                RequestedValue = related?.RequestedValue,
                AuthoritativeValue = related?.AuthoritativeValue,
                Reason = related?.Message ?? (g.Pass ? "PASS" : g.Reason ?? "BLOCKED"),
                Actor = actor,
            };
        }).ToList();
        await _audit.InsertManyAsync(rows, new
        {
            character_id = characterId,
            hierarchy = CharacterIdentityGovernanceRules.Hierarchy,
            no_auto_fix = true,
            production_allowed = ready,
            generate = false,
            director_approval = "PENDING",
        }, ct);
    }

    private static bool GateMatches(string gate, CharacterIdentityGovernanceRules.Conflict c) => gate switch
    {
        "MASTER_GATE" => c.Source == "MASTER",
        "DNA_GATE" => c.Source == "CHARACTER_DNA" && c.Code is "DNA_CONFLICT" or "SHA_MISMATCH",
        "PRP_GATE" => c.Source == "PRODUCTION_REFERENCE_PACK" || c.Code == "PRP_CONFLICT",
        "IDENTITY_GATE" => c.Code is "IDENTITY_CONFLICT" or "DNA_SHOT_CONFLICT" or "DNA_FORBIDDEN_ATTRIBUTE",
        "SHOT_GATE" => c.Source == "SHOT_SPECIFICATION",
        "PROMPT_GATE" => c.Source == "USER_PROMPT",
        "CONTINUITY_GATE" => c.Message.Contains("Continuity", StringComparison.OrdinalIgnoreCase),
        _ => false,
    };

    private string LiveSha(string artifactPath)
    {
        var bytes = _store.Read(artifactPath);
        return bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
    }

    private static int ReadP0(string? qaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(qaJson) ? "{}" : qaJson);
            if (!doc.RootElement.TryGetProperty("p0", out var arr) || arr.ValueKind != JsonValueKind.Array) return 0;
            return arr.EnumerateArray().Count();
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
