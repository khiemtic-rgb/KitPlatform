using System.Linq;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoProductionReferencePackService : IKitVideoProductionReferencePackService
{
    private readonly KitVideoProductionReferencePackRepository _repo;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly KitVideoArtifactStore _store;
    private readonly IKitVideoCharacterDnaService _dnaService;

    public KitVideoProductionReferencePackService(
        KitVideoProductionReferencePackRepository repo,
        KitVideoCharacterDnaRepository dna,
        KitVideoMasterLockRepository locks,
        KitVideoMasterReferenceRepository candidates,
        KitVideoArtifactStore store,
        IKitVideoCharacterDnaService dnaService)
    {
        _repo = repo;
        _dna = dna;
        _locks = locks;
        _candidates = candidates;
        _store = store;
        _dnaService = dnaService;
    }

    public async Task<KitVideoProductionReferencePackGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        if (!KitVideoProductionReferencePackRules.Accepts(characterId, eraId))
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        var ctx = await ContextAsync(cancellationToken);
        var pack = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        var dnaBundle = await _dnaService.GetAsync("CHAR-001", "ERA-01", cancellationToken);
        var checks = ctx.Checks;
        var canCreate = KitVideoProductionReferencePackRules.GatePass(checks) && pack is null;
        var selfCheck = pack is null
            ? checks
            : KitVideoProductionReferencePackRules.EvaluateDirectorReview(
                checks, DeriveSpec(pack, ctx), ctx.DnaSpec);
        return new KitVideoProductionReferencePackGetDto(
            pack is null ? null : ToDto(pack, ctx),
            dnaBundle.Master,
            dnaBundle.Dna,
            canCreate,
            canCreate ? null : FirstFail(selfCheck),
            ctx.IdentityPass,
            ctx.StressPass,
            ctx.P0,
            ToCheckDtos(selfCheck),
            KitVideoProductionReferencePackRules.GatePass(selfCheck));
    }

    public async Task<KitVideoProductionReferencePackDto> GetVersionAsync(string characterId, string packVersion, CancellationToken cancellationToken = default)
    {
        if (!KitVideoProductionReferencePackRules.Accepts(characterId, "ERA-01"))
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        var version = NormalizeVersion(packVersion);
        var row = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", version, cancellationToken)
            ?? await _repo.GetByCodeAsync(packVersion, cancellationToken)
            ?? throw new InvalidOperationException("Production Reference Pack không tồn tại.");
        var ctx = await ContextAsync(cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionReferencePackDto> CreateAsync(string characterId, string actor, CancellationToken cancellationToken = default)
    {
        if (!KitVideoProductionReferencePackRules.Accepts(characterId, "ERA-01"))
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        if (KitVideoProductionReferencePackRules.CreatesPixels("CREATE"))
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: không tạo ảnh production.");
        var ctx = await ContextAsync(cancellationToken);
        var existing = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        if (existing is not null)
            return ToDto(existing, ctx);
        KitVideoProductionReferencePackRules.EnsureCanCreate(ctx.Checks);
        if (ctx.Master is null || ctx.Dna is null)
            throw new InvalidOperationException("PRP_GATE_NOT_SATISFIED: thiếu Master hoặc DNA.");
        var spec = KitVideoProductionReferencePackRules.BuildV1Spec(
            ctx.Master.Sha256, ctx.DnaSha, ctx.Master.Id, ctx.Dna.Id, ctx.DnaSpec, ctx.IdentityPass, ctx.StressPass, ctx.P0);
        KitVideoProductionReferencePackRules.EnsureInheritance(spec, ctx.DnaSpec);
        var row = new KitVideoProductionReferencePackRepository.PackRow
        {
            Id = Guid.NewGuid(),
            MasterReferenceId = ctx.Master.Id,
            MasterCode = ctx.Master.MasterCode,
            MasterSha256 = ctx.Master.Sha256,
            CharacterDnaId = ctx.Dna.Id,
            DnaCode = ctx.Dna.DnaCode,
            DnaSha256 = ctx.DnaSha,
            SpecJson = spec.GetRawText(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        row = await _repo.InsertDraftAsync(row, cancellationToken);
        var payload = AuditPayload(row, actor);
        await _repo.InsertEventAsync(row.Id, "PRP_CREATED", payload, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "PRP_REVIEW_STARTED", payload, actor, cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionReferencePackDto> ApproveAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        KitVideoProductionReferencePackRules.EnsureDirector(actor);
        var row = await RequirePack(cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        if (row.Status == "LOCKED")
            return ToDto(row, ctx);
        var spec = JsonSerializer.Deserialize<JsonElement>(row.SpecJson);
        KitVideoProductionReferencePackRules.EnsureCanApprove(row.Status, spec, ctx.Checks, ctx.DnaSpec);
        var prpSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(row.SpecJson ?? "{}"));
        row = await _repo.ApproveAndLockAsync(row.Id, actor, note ?? "", prpSha, cancellationToken);
        var payload = AuditPayload(row, actor, prpSha);
        await _repo.InsertEventAsync(row.Id, "PRP_APPROVED", payload, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "PRP_LOCKED", payload, actor, cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionReferencePackDto> RejectAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        KitVideoProductionReferencePackRules.EnsureDirector(actor);
        var row = await RequirePack(cancellationToken);
        if (row.Status == "LOCKED")
            throw new InvalidOperationException("PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.");
        row = await _repo.RejectAsync(row.Id, note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "PRP_REJECTED", AuditPayload(row, actor), actor, cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionReferencePackDto> AnalyzeAsync(string characterId, string actor, CancellationToken cancellationToken = default)
    {
        var row = await RequirePack(cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        if (row.Status is "DRAFT" or "REJECTED" && ctx.Master is not null && ctx.Dna is not null)
        {
            var original = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
            var merged = KitVideoProductionReferencePackRules.AttachMissingProductionRules(
                original, ctx.Master.Sha256, ctx.DnaSha, ctx.Master.Id, ctx.Dna.Id);
            KitVideoProductionReferencePackRules.EnsureNoIdentityMutation(original, merged);
            if (!string.Equals(original.GetRawText(), merged.GetRawText(), StringComparison.Ordinal))
                row = await _repo.UpdateSpecAsync(row.Id, merged.GetRawText(), row.Note, cancellationToken);
        }
        await _repo.InsertEventAsync(row.Id, "PRP_REVIEW_STARTED", AuditPayload(row, actor), actor, cancellationToken);
        var dto = ToDto(row, ctx);
        var analysis =
            $"DIRECTOR GATE: {(dto.DirectorGatePass || row.Status == "LOCKED" ? "PASS" : "FAIL")} · PRP {row.Status} · Master {(ctx.MasterLocked ? "LOCKED" : "—")} · DNA {(ctx.DnaLocked ? "LOCKED" : "—")} · MASTER SHA {(ctx.MasterShaValid ? "MATCH" : "FAIL")} · DNA SHA {(ctx.DnaShaValid ? "MATCH" : "FAIL")} · Identity {(ctx.IdentityPass ? "PASS" : "FAIL")} · Stress {(ctx.StressPass ? "PASS" : "FAIL")} · P0 {ctx.P0} · {dto.Blocked ?? "READY"}";
        return dto with { Analysis = analysis };
    }

    public async Task<KitVideoProductionReferencePackDto> EditAsync(string characterId, string actor, KitVideoProductionReferencePackEditRequest request, CancellationToken cancellationToken = default)
    {
        var row = await RequirePack(cancellationToken);
        KitVideoProductionReferencePackRules.EnsureImmutable("EDIT", row.Status);
        if (row.Status == "LOCKED")
            throw new InvalidOperationException("PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.");
        var original = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
        var specJson = request.Spec is { ValueKind: JsonValueKind.Object } spec
            ? spec.GetRawText()
            : row.SpecJson;
        if (request.Spec is { ValueKind: JsonValueKind.Object } edited)
            KitVideoProductionReferencePackRules.EnsureNoIdentityMutation(original, edited);
        row = await _repo.UpdateSpecAsync(row.Id, specJson, request.Note ?? "", cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task<KitVideoProductionReferencePackDto> ReturnToEditAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var row = await RequirePack(cancellationToken);
        if (row.Status == "LOCKED")
            throw new InvalidOperationException("PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.");
        row = await _repo.SetStatusAsync(row.Id, "DRAFT", note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "PRP_REVIEW_STARTED", AuditPayload(row, actor), actor, cancellationToken);
        var ctx = await ContextAsync(cancellationToken);
        return ToDto(row, ctx);
    }

    public async Task RejectMutationAsync(string characterId, string action, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        KitVideoProductionReferencePackRules.EnsureImmutable(action, row?.Status ?? "LOCKED");
        throw new InvalidOperationException("PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.");
    }

    private async Task<KitVideoProductionReferencePackRepository.PackRow> RequirePack(CancellationToken ct) =>
        await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct)
            ?? throw new InvalidOperationException("Production Reference Pack không tồn tại.");

    private sealed record Context(
        KitVideoMasterLockRepository.MasterRow? Master,
        KitVideoCharacterDnaRepository.DnaRow? Dna,
        JsonElement DnaSpec,
        string DnaSha,
        bool MasterLocked,
        bool DnaLocked,
        bool MasterShaValid,
        bool DnaShaValid,
        bool IdentityPass,
        bool StressPass,
        int P0,
        IReadOnlyList<KitVideoProductionReferencePackRules.CheckItem> Checks);

    private async Task<Context> ContextAsync(CancellationToken ct)
    {
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        var dna = await _dna.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        var masterLocked = master is not null && KitVideoCharacterDnaRules.IsLockedMaster(master.Status, master.MasterCode);
        var dnaLocked = dna is not null && string.Equals(dna.Status, "LOCKED", StringComparison.OrdinalIgnoreCase);
        var liveMasterSha = master is null ? "" : LiveSha(master.ArtifactPath);
        var masterShaValid = master is not null && KitVideoCharacterDnaRules.SameSha(master.Sha256, liveMasterSha);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dna?.SpecJson) ? "{}" : dna!.SpecJson);
        var dnaSha = dna is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(dna.SpecJson ?? "{}"));
        var dnaShaValid = KitVideoProductionReferencePackRules.ShaExists(dnaSha)
            && (dna is null || KitVideoCharacterDnaRules.SameSha(dna.MasterSha256, master?.Sha256));
        var identityPass = master is not null && KitVideoCharacterDnaRules.ResultPass(master.IdentityTestResult);
        var stressPass = master is not null && KitVideoCharacterDnaRules.ResultPass(master.StressTestResult);
        var p0 = 1;
        if (master is not null)
        {
            var cand = await _candidates.GetCandidateAsync(master.SourceCandidateId, ct);
            if (cand is not null)
            {
                p0 = ReadP0(cand.QaJson);
                if (!identityPass) identityPass = cand.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase) && p0 == 0;
            }
            else p0 = 0;
        }
        var directorApproval = masterLocked && dnaLocked;
        var checks = KitVideoProductionReferencePackRules.EvaluateCreateGate(
            masterLocked, dnaLocked, masterShaValid, dnaShaValid, identityPass, stressPass, p0, directorApproval);
        return new Context(master, dna, dnaSpec, dnaSha, masterLocked, dnaLocked, masterShaValid, dnaShaValid,
            identityPass, stressPass, p0, checks);
    }

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

    private static KitVideoProductionReferencePackDto ToDto(
        KitVideoProductionReferencePackRepository.PackRow row,
        Context ctx)
    {
        var spec = DeriveSpec(row, ctx);
        var review = KitVideoProductionReferencePackRules.EvaluateDirectorReview(ctx.Checks, spec, ctx.DnaSpec);
        string? blocked = null;
        var canApprove = false;
        try
        {
            KitVideoProductionReferencePackRules.EnsureCanApprove(row.Status, spec, ctx.Checks, ctx.DnaSpec);
            canApprove = row.Status is "DRAFT" or "REJECTED";
        }
        catch (InvalidOperationException ex)
        {
            blocked = ex.Message;
        }
        var directorGate = KitVideoProductionReferencePackRules.GatePass(review);
        var gatePass = KitVideoProductionReferencePackRules.GatePass(ctx.Checks);
        var liveSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(row.SpecJson ?? "{}"));
        return new KitVideoProductionReferencePackDto(
            row.Id, row.PackCode, row.CharacterId, row.CharacterName, row.EraId,
            row.MasterReferenceId, row.MasterCode, row.MasterSha256, row.CharacterDnaId, row.DnaCode, row.DnaSha256,
            row.PackVersion, row.Status, row.DocumentId, row.CreatedAt, row.ApprovedAt, row.ApprovedBy,
            row.LockedAt, row.LockedBy, row.Note, spec,
            canApprove, row.Status is "DRAFT" or "REJECTED", row.Status == "LOCKED",
            blocked, row.Status is "DRAFT" or "REJECTED", row.Status == "REJECTED",
            null, ToCheckDtos(review), gatePass, ctx.IdentityPass, ctx.StressPass, ctx.P0,
            directorGate && row.Status is "DRAFT" or "LOCKED",
            row.PrpSha256 ?? (row.Status == "LOCKED" ? liveSha : null),
            directorGate);
    }

    private static IReadOnlyList<KitVideoDnaCheckItemDto> ToCheckDtos(
        IReadOnlyList<KitVideoProductionReferencePackRules.CheckItem> items) =>
        items.Select(x => new KitVideoDnaCheckItemDto(x.Code, x.Label, x.Pass, x.Reason)).ToList();

    private static JsonElement DeriveSpec(KitVideoProductionReferencePackRepository.PackRow row, Context ctx)
    {
        var spec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
        if (ctx.Master is null || ctx.Dna is null) return spec;
        var derived = KitVideoProductionReferencePackRules.AttachMissingProductionRules(
            spec, ctx.Master.Sha256, ctx.DnaSha, ctx.Master.Id, ctx.Dna.Id);
        KitVideoProductionReferencePackRules.EnsureNoIdentityMutation(spec, derived);
        return derived;
    }

    private static string? FirstFail(IReadOnlyList<KitVideoProductionReferencePackRules.CheckItem> items)
    {
        var fail = items.FirstOrDefault(x => !x.Pass);
        if (fail is null) return null;
        var prefix = fail.Code is "master_sha" or "dna_sha" ? "PRP_INVALID" : "PRP_GATE_NOT_SATISFIED";
        return $"{prefix}: {fail.Reason}";
    }

    private static object AuditPayload(KitVideoProductionReferencePackRepository.PackRow row, string? actor, string? prpSha256 = null) => new
    {
        character_id = row.CharacterId,
        pack_id = row.Id,
        pack_version = row.PackVersion,
        master_id = row.MasterReferenceId,
        master_sha256 = row.MasterSha256,
        dna_id = row.CharacterDnaId,
        dna_sha256 = row.DnaSha256,
        prp_sha256 = prpSha256 ?? row.PrpSha256,
        approved_by = actor,
        timestamp = DateTimeOffset.UtcNow,
    };

    private static string NormalizeVersion(string? raw)
    {
        var v = (raw ?? "").Trim();
        if (v.Equals(KitVideoProductionReferencePackRules.PackCode, StringComparison.OrdinalIgnoreCase)) return "V1";
        if (v.StartsWith("PROD-REF-", StringComparison.OrdinalIgnoreCase)) return v[9..];
        return v.Length == 0 ? "V1" : v.ToUpperInvariant();
    }
}
