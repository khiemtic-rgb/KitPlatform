using System.Linq;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoProductionShotService : IKitVideoProductionShotService
{
    private readonly KitVideoProductionShotRepository _repo;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly KitVideoArtifactStore _store;
    private readonly IKitVideoCharacterDnaService _dnaService;
    private readonly IKitVideoProductionReferencePackService _prpService;
    private readonly ICharacterIdentityGovernanceService _governance;

    public KitVideoProductionShotService(
        KitVideoProductionShotRepository repo,
        KitVideoProductionReferencePackRepository prp,
        KitVideoCharacterDnaRepository dna,
        KitVideoMasterLockRepository locks,
        KitVideoMasterReferenceRepository candidates,
        KitVideoArtifactStore store,
        IKitVideoCharacterDnaService dnaService,
        IKitVideoProductionReferencePackService prpService,
        ICharacterIdentityGovernanceService governance)
    {
        _repo = repo;
        _prp = prp;
        _dna = dna;
        _locks = locks;
        _candidates = candidates;
        _store = store;
        _dnaService = dnaService;
        _prpService = prpService;
        _governance = governance;
    }

    public async Task<KitVideoProductionShotGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        if (!KitVideoProductionShotRules.Accepts(characterId, eraId))
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        var ctx = await ContextAsync(cancellationToken);
        var rows = await _repo.ListAsync("CHAR-001", "ERA-01", cancellationToken);
        var shots = rows.Select(r => ToDto(r, ctx)).ToList();
        var dnaBundle = await _dnaService.GetAsync("CHAR-001", "ERA-01", cancellationToken);
        var prpBundle = await _prpService.GetAsync("CHAR-001", "ERA-01", cancellationToken);
        var canCreate = KitVideoProductionShotRules.GatePass(ctx.Source);
        return new KitVideoProductionShotGetDto(
            shots,
            dnaBundle.Master,
            dnaBundle.Dna,
            prpBundle.Pack,
            canCreate,
            canCreate ? null : FirstFail(ctx.Source),
            ctx.IdentityPass,
            ctx.StressPass,
            ToCheckDtos(ctx.Source),
            canCreate);
    }

    public async Task<KitVideoProductionShotDto> GetOneAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Production Shot không tồn tại.");
        return ToDto(row, await ContextAsync(cancellationToken));
    }

    public async Task<KitVideoProductionShotDto> CreateAsync(string characterId, string actor, CancellationToken cancellationToken = default)
    {
        if (KitVideoProductionShotRules.CreatesPixels("CREATE"))
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: không tạo ảnh / video.");
        await RequireGovernanceAsync(characterId, null, null, null, actor, cancellationToken);
        if (!KitVideoProductionShotRules.Accepts(characterId, "ERA-01"))
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        var ctx = await ContextAsync(cancellationToken);
        KitVideoProductionShotRules.EnsureCanCreate(ctx.Source);
        if (ctx.Master is null || ctx.Dna is null || ctx.Pack is null)
            throw new InvalidOperationException("SHOT_GATE_NOT_SATISFIED: thiếu Master, DNA hoặc PRP.");
        var seq = await _repo.NextSeqAsync("CHAR-001", "ERA-01", cancellationToken);
        var previous = (await _repo.ListAsync("CHAR-001", "ERA-01", cancellationToken)).LastOrDefault();
        var spec = KitVideoProductionShotRules.BuildDraftSpec(
            seq, ctx.Master.Sha256, ctx.DnaSha, ctx.PrpSha, ctx.Master.Id, ctx.Dna.Id, ctx.Pack.Id, ctx.DnaSpec,
            previous?.Id.ToString());
        KitVideoProductionShotRules.EnsureInheritance(spec, ctx.DnaSpec);
        await RequireGovernanceAsync(characterId, spec, null, null, actor, cancellationToken);
        var row = new KitVideoProductionShotRepository.ShotRow
        {
            Id = Guid.NewGuid(),
            ShotCode = KitVideoProductionShotRules.ShotCode(seq),
            ShotSeq = seq,
            MasterReferenceId = ctx.Master.Id,
            MasterCode = ctx.Master.MasterCode,
            MasterSha256 = ctx.Master.Sha256,
            CharacterDnaId = ctx.Dna.Id,
            DnaCode = ctx.Dna.DnaCode,
            DnaSha256 = ctx.DnaSha,
            ProductionPackId = ctx.Pack.Id,
            PrpCode = ctx.Pack.PackCode,
            PrpSha256 = ctx.PrpSha,
            SpecJson = spec.GetRawText(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor,
        };
        row = await _repo.InsertDraftAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "SHOT_CREATED", AuditPayload(row, actor), actor, cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionShotDto> AnalyzeAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        var spec = JsonSerializer.Deserialize<JsonElement>(row.SpecJson);
        var risk = KitVideoProductionShotRules.AssessRisk(spec);
        await _repo.InsertEventAsync(row.Id, "SHOT_ANALYZED", AuditPayload(row, actor), actor, cancellationToken);
        var dto = ToDto(row, ctx);
        return dto with
        {
            Analysis =
                $"DIRECTOR GATE: {(dto.DirectorGatePass || row.ShotStatus == "LOCKED" ? "PASS" : "FAIL")} · SHOT {row.ShotStatus} · RISK {risk} · Master {(ctx.MasterLocked ? "LOCKED" : "—")} · DNA {(ctx.DnaLocked ? "LOCKED" : "—")} · PRP {(ctx.PrpLocked ? "LOCKED" : "—")} · ALL SHA {(ctx.AllShaMatch ? "MATCH" : "FAIL")} · {dto.Blocked ?? "READY"}",
        };
    }

    public async Task<KitVideoProductionShotDto> IdentityCheckAsync(Guid id, string actor, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        KitVideoProductionShotRules.EnsureImmutable("EDIT", row.ShotStatus);
        var ctx = await ContextAsync(cancellationToken);
        var spec = JsonSerializer.Deserialize<JsonElement>(row.SpecJson);
        var risk = KitVideoProductionShotRules.AssessRisk(spec);
        var inheritOk = true;
        try { KitVideoProductionShotRules.EnsureInheritance(spec, ctx.DnaSpec); }
        catch (InvalidOperationException) { inheritOk = false; }
        var pass = inheritOk && KitVideoProductionShotRules.GatePass(ctx.Source) && KitVideoProductionShotRules.IsComplete(spec);
        var next = KitVideoProductionShotRules.MarkIdentityCheck(spec, pass, risk);
        var status = pass ? "DIRECTOR_REVIEW" : "IDENTITY_CHECK";
        row = await _repo.UpdateSpecAsync(row.Id, next.GetRawText(), status, row.Note, cancellationToken);
        await _repo.InsertEventAsync(row.Id, pass ? "SHOT_IDENTITY_PASS" : "SHOT_IDENTITY_FAIL", AuditPayload(row, actor), actor, cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionShotDto> EditAsync(Guid id, string actor, KitVideoProductionShotEditRequest request, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        KitVideoProductionShotRules.EnsureImmutable("EDIT", row.ShotStatus);
        var original = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
        var specJson = request.Spec is { ValueKind: JsonValueKind.Object } spec
            ? spec.GetRawText()
            : row.SpecJson;
        var editedEl = request.Spec is { ValueKind: JsonValueKind.Object } edited ? edited : original;
        await RequireGovernanceAsync(row.CharacterId, editedEl, request.Note, row.Id, actor, cancellationToken);
        if (request.Spec is { ValueKind: JsonValueKind.Object } editedSpec)
            KitVideoProductionShotRules.EnsureNoIdentityMutation(original, editedSpec);
        row = await _repo.UpdateSpecAsync(row.Id, specJson, row.ShotStatus == "LOCKED" ? row.ShotStatus : "DRAFT", request.Note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "SHOT_EDITED", AuditPayload(row, actor), actor, cancellationToken);
        return ToDto(row, await ContextAsync(cancellationToken));
    }

    public async Task<KitVideoProductionShotDto> ApproveAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default)
    {
        KitVideoProductionShotRules.EnsureDirector(actor);
        var row = await Require(id, cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        if (row.ShotStatus == "LOCKED")
            return ToDto(row, ctx);
        var spec = JsonSerializer.Deserialize<JsonElement>(row.SpecJson);
        await RequireGovernanceAsync(row.CharacterId, spec, null, row.Id, actor, cancellationToken);
        var checkPass = KitVideoProductionShotRules.IdentityCheckRecorded(spec);
        KitVideoProductionShotRules.EnsureCanApprove(row.ShotStatus, spec, ctx.DnaSpec, ctx.Source, checkPass);
        var shotSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(row.SpecJson ?? "{}"));
        row = await _repo.ApproveAndLockAsync(row.Id, actor, note ?? "", shotSha, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "SHOT_APPROVED", AuditPayload(row, actor, shotSha), actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "SHOT_LOCKED", AuditPayload(row, actor, shotSha), actor, cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionShotDto> RejectAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default)
    {
        KitVideoProductionShotRules.EnsureDirector(actor);
        var row = await Require(id, cancellationToken);
        if (row.ShotStatus == "LOCKED")
            throw new InvalidOperationException("SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.");
        row = await _repo.SetStatusAsync(row.Id, "REJECTED", note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "SHOT_REJECTED", AuditPayload(row, actor), actor, cancellationToken);
        return ToDto(row, await ContextAsync(cancellationToken));
    }

    public async Task<KitVideoProductionShotDto> ReturnToEditAsync(Guid id, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var row = await Require(id, cancellationToken);
        if (row.ShotStatus == "LOCKED")
            throw new InvalidOperationException("SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.");
        row = await _repo.SetStatusAsync(row.Id, "DRAFT", note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "SHOT_RETURNED", AuditPayload(row, actor), actor, cancellationToken);
        return ToDto(row, await ContextAsync(cancellationToken));
    }

    public async Task RejectMutationAsync(Guid id, string action, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByIdAsync(id, cancellationToken);
        KitVideoProductionShotRules.EnsureImmutable(action, row?.ShotStatus ?? "LOCKED");
        throw new InvalidOperationException("SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.");
    }

    private async Task RequireGovernanceAsync(
        string characterId, JsonElement? shotSpec, string? userPrompt, Guid? shotId, string actor, CancellationToken ct)
    {
        var request = new CharacterIdentityGovernanceCheckRequest(shotSpec, userPrompt, shotId);
        var gov = await _governance.ProductionGateAsync(characterId, request, actor, ct);
        if (gov.Generate)
            throw new CharacterIdentityGovernanceBlockedException(gov with { Generate = false, ProductionAllowed = false, Status = "BLOCKED" });
        if (gov.Status != "PASS" || !gov.ProductionAllowed)
            throw new CharacterIdentityGovernanceBlockedException(gov);
    }

    private async Task<KitVideoProductionShotRepository.ShotRow> Require(Guid id, CancellationToken ct) =>
        await _repo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Production Shot không tồn tại.");

    private sealed record Context(
        KitVideoMasterLockRepository.MasterRow? Master,
        KitVideoCharacterDnaRepository.DnaRow? Dna,
        KitVideoProductionReferencePackRepository.PackRow? Pack,
        JsonElement DnaSpec,
        string DnaSha,
        string PrpSha,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool AllShaMatch,
        bool IdentityPass,
        bool StressPass,
        IReadOnlyList<KitVideoProductionShotRules.CheckItem> Source);

    private async Task<Context> ContextAsync(CancellationToken ct)
    {
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        var dna = await _dna.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        var pack = await _prp.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        var masterLocked = master is not null && KitVideoCharacterDnaRules.IsLockedMaster(master.Status, master.MasterCode);
        var dnaLocked = dna is not null && string.Equals(dna.Status, "LOCKED", StringComparison.OrdinalIgnoreCase);
        var prpLocked = pack is not null && string.Equals(pack.Status, "LOCKED", StringComparison.OrdinalIgnoreCase);
        var liveMasterSha = master is null ? "" : LiveSha(master.ArtifactPath);
        var masterShaValid = master is not null && KitVideoCharacterDnaRules.SameSha(master.Sha256, liveMasterSha);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dna?.SpecJson) ? "{}" : dna!.SpecJson);
        var dnaSha = dna is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(dna.SpecJson ?? "{}"));
        var dnaShaValid = KitVideoProductionShotRules.ShaExists(dnaSha)
            && (dna is null || KitVideoCharacterDnaRules.SameSha(dna.MasterSha256, master?.Sha256));
        var prpSha = pack is null ? "" : (pack.PrpSha256 ?? KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(pack.SpecJson ?? "{}")));
        var prpShaValid = prpLocked && KitVideoProductionShotRules.ShaExists(prpSha);
        var identityPass = master is not null && KitVideoCharacterDnaRules.ResultPass(master.IdentityTestResult);
        var stressPass = master is not null && KitVideoCharacterDnaRules.ResultPass(master.StressTestResult);
        if (master is not null && !identityPass)
        {
            var cand = await _candidates.GetCandidateAsync(master.SourceCandidateId, ct);
            if (cand is not null && cand.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase))
                identityPass = true;
        }
        var source = KitVideoProductionShotRules.EvaluateSourceGate(
            masterLocked, dnaLocked, prpLocked, masterShaValid, dnaShaValid, prpShaValid, identityPass, stressPass);
        return new Context(master, dna, pack, dnaSpec, dnaSha, prpSha, masterLocked, dnaLocked, prpLocked,
            masterShaValid && dnaShaValid && prpShaValid, identityPass, stressPass, source);
    }

    private string LiveSha(string artifactPath)
    {
        var bytes = _store.Read(artifactPath);
        return bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
    }

    private static KitVideoProductionShotDto ToDto(KitVideoProductionShotRepository.ShotRow row, Context ctx)
    {
        var spec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
        var checkPass = KitVideoProductionShotRules.IdentityCheckRecorded(spec);
        var review = KitVideoProductionShotRules.EvaluateDirectorReview(ctx.Source, spec, ctx.DnaSpec, checkPass);
        string? blocked = null;
        var canApprove = false;
        try
        {
            KitVideoProductionShotRules.EnsureCanApprove(row.ShotStatus, spec, ctx.DnaSpec, ctx.Source, checkPass);
            canApprove = row.ShotStatus is "DIRECTOR_REVIEW" or "IDENTITY_CHECK";
        }
        catch (InvalidOperationException ex)
        {
            blocked = ex.Message;
        }
        var directorGate = KitVideoProductionShotRules.GatePass(review);
        var liveSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(row.SpecJson ?? "{}"));
        var editable = row.ShotStatus is "DRAFT" or "IDENTITY_CHECK" or "DIRECTOR_REVIEW" or "REJECTED";
        return new KitVideoProductionShotDto(
            row.Id, row.ShotCode, row.CharacterId, row.CharacterName, row.EraId, row.ShotSeq, row.ShotVersion,
            row.ShotStatus, row.DocumentId, row.MasterReferenceId, row.MasterCode, row.MasterSha256,
            row.CharacterDnaId, row.DnaCode, row.DnaSha256, row.ProductionPackId, row.PrpCode, row.PrpSha256,
            row.CreatedAt, row.CreatedBy, row.ApprovedAt, row.ApprovedBy, row.LockedAt, row.LockedBy, row.Note, spec,
            canApprove, editable, row.ShotStatus == "LOCKED", blocked, editable, row.ShotStatus == "REJECTED",
            null, ToCheckDtos(review), KitVideoProductionShotRules.GatePass(ctx.Source), directorGate,
            checkPass, ctx.IdentityPass, ctx.StressPass, directorGate && row.ShotStatus is "DIRECTOR_REVIEW" or "LOCKED",
            row.ShotSha256 ?? (row.ShotStatus == "LOCKED" ? liveSha : null));
    }

    private static IReadOnlyList<KitVideoDnaCheckItemDto> ToCheckDtos(
        IReadOnlyList<KitVideoProductionShotRules.CheckItem> items) =>
        items.Select(x => new KitVideoDnaCheckItemDto(x.Code, x.Label, x.Pass, x.Reason)).ToList();

    private static string? FirstFail(IReadOnlyList<KitVideoProductionShotRules.CheckItem> items)
    {
        var fail = items.FirstOrDefault(x => !x.Pass);
        if (fail is null) return null;
        var prefix = fail.Code is "master_sha" or "dna_sha" or "prp_sha" ? "SHOT_INVALID" : "SHOT_GATE_NOT_SATISFIED";
        return $"{prefix}: {fail.Reason}";
    }

    private static object AuditPayload(KitVideoProductionShotRepository.ShotRow row, string? actor, string? shotSha = null) => new
    {
        character_id = row.CharacterId,
        shot_id = row.Id,
        shot_code = row.ShotCode,
        shot_version = row.ShotVersion,
        master_id = row.MasterReferenceId,
        master_sha256 = row.MasterSha256,
        dna_id = row.CharacterDnaId,
        dna_sha256 = row.DnaSha256,
        prp_id = row.ProductionPackId,
        prp_sha256 = row.PrpSha256,
        shot_sha256 = shotSha ?? row.ShotSha256,
        approved_by = actor,
        timestamp = DateTimeOffset.UtcNow,
    };
}
