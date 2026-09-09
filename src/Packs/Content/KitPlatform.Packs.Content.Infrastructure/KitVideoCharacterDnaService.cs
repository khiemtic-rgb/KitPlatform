using System.Linq;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoCharacterDnaService : IKitVideoCharacterDnaService
{
    private readonly KitVideoCharacterDnaRepository _repo;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly KitVideoArtifactStore _store;

    public KitVideoCharacterDnaService(
        KitVideoCharacterDnaRepository repo,
        KitVideoMasterLockRepository locks,
        KitVideoMasterReferenceRepository candidates,
        KitVideoArtifactStore store)
    {
        _repo = repo;
        _locks = locks;
        _candidates = candidates;
        _store = store;
    }

    public async Task<KitVideoCharacterDnaGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        if (!KitVideoCharacterDnaRules.Accepts(characterId, eraId))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        var dna = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        var gate = await ProductionAsync(master, cancellationToken);
        var (canCreate, blocked) = CreateGate(master, dna?.Status == "LOCKED", gate.ShaValid);
        return new KitVideoCharacterDnaGetDto(
            dna is null ? null : await ToDto(dna, master, gate, cancellationToken),
            master is null ? null : ToMaster(master),
            canCreate && dna is null,
            blocked,
            gate.IdentityPass,
            gate.StressPass,
            gate.P0);
    }

    public async Task<KitVideoCharacterDnaDto> GetVersionAsync(string characterId, string dnaVersion, CancellationToken cancellationToken = default)
    {
        if (!KitVideoCharacterDnaRules.Accepts(characterId, "ERA-01"))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        var version = NormalizeVersion(dnaVersion);
        var row = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", version, cancellationToken)
            ?? await _repo.GetByCodeAsync(dnaVersion, cancellationToken)
            ?? throw new InvalidOperationException("Character DNA không tồn tại.");
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        return await ToDto(row, master, await ProductionAsync(master, cancellationToken), cancellationToken);
    }

    public async Task<KitVideoCharacterDnaDto> CreateAsync(string characterId, string actor, CancellationToken cancellationToken = default)
    {
        if (!KitVideoCharacterDnaRules.Accepts(characterId, "ERA-01"))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        if (KitVideoCharacterDnaRules.CreatesPixels("CREATE"))
            throw new InvalidOperationException("DNA: không tạo ảnh mới.");
        var existing = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        var master = await RequireMaster(cancellationToken);
        if (existing is not null)
            return await ToDto(existing, master, await ProductionAsync(master, cancellationToken), cancellationToken);
        var liveSha = LiveSha(master.ArtifactPath);
        KitVideoCharacterDnaRules.EnsureCanCreate(true, true, master.MasterCode, KitVideoCharacterDnaRules.SameSha(master.Sha256, liveSha), false);
        var spec = KitVideoCharacterDnaRules.BuildV1Spec(master.Sha256);
        var row = new KitVideoCharacterDnaRepository.DnaRow
        {
            Id = Guid.NewGuid(),
            MasterReferenceId = master.Id,
            MasterCode = master.MasterCode,
            MasterSha256 = master.Sha256,
            SpecJson = spec.GetRawText(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        row = await _repo.InsertDraftAsync(row, cancellationToken);
        var payload = AuditPayload(row, actor);
        await _repo.InsertEventAsync(row.Id, "DNA_CREATED", payload, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "DNA_REVIEW_STARTED", payload, actor, cancellationToken);
        return await ToDto(row, master, await ProductionAsync(master, cancellationToken), cancellationToken);
    }

    public async Task<KitVideoCharacterDnaDto> ApproveAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        KitVideoCharacterDnaRules.EnsureDirector(actor);
        var row = await RequireDna(characterId, cancellationToken);
        var master = await RequireMaster(cancellationToken);
        var gate = await ProductionAsync(master, cancellationToken);
        if (row.Status == "LOCKED")
            return await ToDto(row, master, gate, cancellationToken);
        var spec = JsonSerializer.Deserialize<JsonElement>(row.SpecJson);
        var shaOk = KitVideoCharacterDnaRules.SameSha(master.Sha256, row.MasterSha256) && gate.ShaValid;
        KitVideoCharacterDnaRules.EnsureCanApprove(row.Status, spec, true, true, shaOk, gate.IdentityPass, gate.StressPass, gate.P0);
        var dnaSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(row.SpecJson ?? "{}"));
        row = await _repo.ApproveAndLockAsync(row.Id, actor, note ?? "", dnaSha, cancellationToken);
        var payload = AuditPayload(row, actor, dnaSha);
        await _repo.InsertEventAsync(row.Id, "DNA_APPROVED", payload, actor, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "DNA_LOCKED", payload, actor, cancellationToken);
        return await ToDto(row, master, gate, cancellationToken);
    }

    public async Task<KitVideoCharacterDnaDto> RejectAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        KitVideoCharacterDnaRules.EnsureDirector(actor);
        var row = await RequireDna(characterId, cancellationToken);
        if (row.Status == "LOCKED")
            throw new InvalidOperationException("DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.");
        row = await _repo.RejectAsync(row.Id, note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "DNA_REJECTED", AuditPayload(row, actor), actor, cancellationToken);
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        return await ToDto(row, master, await ProductionAsync(master, cancellationToken), cancellationToken);
    }

    public async Task<KitVideoCharacterDnaDto> AnalyzeAsync(string characterId, string actor, CancellationToken cancellationToken = default)
    {
        var row = await RequireDna(characterId, cancellationToken);
        var master = await RequireMaster(cancellationToken);
        var gate = await ProductionAsync(master, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "DNA_REVIEW_STARTED", AuditPayload(row, actor), actor, cancellationToken);
        var dto = await ToDto(row, master, gate, cancellationToken);
        var analysis =
            $"DNA GATE: {(dto.GatePass || row.Status == "LOCKED" ? "PASS" : "FAIL")} · Master {(gate.MasterLocked ? "LOCKED" : "—")} · SHA {(gate.ShaValid ? "MATCH" : "FAIL")} · Identity {(gate.IdentityPass ? "PASS" : "FAIL")} · Stress {(gate.StressPass ? "PASS" : "FAIL")} · P0 {gate.P0} · {dto.Blocked ?? "READY"}";
        return dto with { Analysis = analysis };
    }

    public async Task<KitVideoCharacterDnaDto> EditAsync(string characterId, string actor, KitVideoCharacterDnaEditRequest request, CancellationToken cancellationToken = default)
    {
        var row = await RequireDna(characterId, cancellationToken);
        KitVideoCharacterDnaRules.EnsureImmutable("EDIT", row.Status);
        if (row.Status == "LOCKED")
            throw new InvalidOperationException("DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.");
        var specJson = request.Spec is { ValueKind: JsonValueKind.Object } spec ? spec.GetRawText() : row.SpecJson;
        row = await _repo.UpdateSpecAsync(row.Id, specJson, request.Note ?? "", cancellationToken);
        var master = await RequireMaster(cancellationToken);
        return await ToDto(row, master, await ProductionAsync(master, cancellationToken), cancellationToken);
    }

    public async Task<KitVideoCharacterDnaDto> ReturnToEditAsync(string characterId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        var row = await RequireDna(characterId, cancellationToken);
        if (row.Status == "LOCKED")
            throw new InvalidOperationException("DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.");
        row = await _repo.SetStatusAsync(row.Id, "DRAFT", note ?? "", cancellationToken);
        await _repo.InsertEventAsync(row.Id, "DNA_REVIEW_STARTED", AuditPayload(row, actor), actor, cancellationToken);
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        return await ToDto(row, master, await ProductionAsync(master, cancellationToken), cancellationToken);
    }

    public async Task RejectMutationAsync(string characterId, string action, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", cancellationToken);
        KitVideoCharacterDnaRules.EnsureImmutable(action, row?.Status ?? "LOCKED");
        throw new InvalidOperationException("DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.");
    }

    private async Task<KitVideoCharacterDnaRepository.DnaRow> RequireDna(string characterId, CancellationToken ct)
    {
        if (!KitVideoCharacterDnaRules.Accepts(characterId, "ERA-01"))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: chỉ CHAR-001 / ERA-01.");
        return await _repo.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct)
            ?? throw new InvalidOperationException("Character DNA không tồn tại.");
    }

    private async Task<KitVideoMasterLockRepository.MasterRow> RequireMaster(CancellationToken ct)
    {
        var master = await _locks.GetByVersionAsync("CHAR-001", "ERA-01", "V1", ct);
        if (master is null)
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: master exists thất bại.");
        if (!KitVideoCharacterDnaRules.IsLockedMaster(master.Status, master.MasterCode))
            throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: Master chưa LOCKED. Không tạo APPROVED DNA.");
        return master;
    }

    private string LiveSha(string artifactPath)
    {
        var bytes = _store.Read(artifactPath);
        return bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
    }

    private sealed record ProductionGate(bool MasterLocked, bool ShaValid, bool IdentityPass, int IdentityHave, bool StressPass, int StressHave, int P0);

    private async Task<ProductionGate> ProductionAsync(KitVideoMasterLockRepository.MasterRow? master, CancellationToken ct)
    {
        if (master is null)
            return new ProductionGate(false, false, false, 0, false, 0, 1);
        var shaValid = KitVideoCharacterDnaRules.SameSha(master.Sha256, LiveSha(master.ArtifactPath));
        var identityPass = KitVideoCharacterDnaRules.ResultPass(master.IdentityTestResult);
        var stressPass = KitVideoCharacterDnaRules.ResultPass(master.StressTestResult);
        var identityHave = master.IdentityTestResult.StartsWith("7/", StringComparison.Ordinal) ? 7 : 0;
        var stressHave = master.StressTestResult.StartsWith("10/", StringComparison.Ordinal) ? 10 : 0;
        var p0 = 1;
        var cand = await _candidates.GetCandidateAsync(master.SourceCandidateId, ct);
        if (cand is not null)
        {
            p0 = ReadP0(cand.QaJson);
            if (!identityPass) identityPass = cand.QaStatus.Equals("PASS", StringComparison.OrdinalIgnoreCase) && p0 == 0;
        }
        return new ProductionGate(
            KitVideoCharacterDnaRules.IsLockedMaster(master.Status, master.MasterCode),
            shaValid, identityPass, identityHave, stressPass, stressHave, p0);
    }

    private (bool CanCreate, string? Blocked) CreateGate(KitVideoMasterLockRepository.MasterRow? master, bool lockedDna, bool shaValid)
    {
        if (lockedDna)
            return (false, "DNA_LOCKED: V1 đã khóa.");
        if (master is null)
            return (false, "DNA_GATE_NOT_SATISFIED: master exists thất bại.");
        if (!KitVideoCharacterDnaRules.IsLockedMaster(master.Status, master.MasterCode))
            return (false, "DNA_GATE_NOT_SATISFIED: Master chưa LOCKED. Không tạo APPROVED DNA.");
        if (!shaValid)
            return (false, "DNA_INVALID: Master SHA không khớp. DNA không tự cập nhật.");
        return (true, null);
    }

    private async Task<KitVideoCharacterDnaDto> ToDto(
        KitVideoCharacterDnaRepository.DnaRow row,
        KitVideoMasterLockRepository.MasterRow? master,
        ProductionGate gate,
        CancellationToken ct)
    {
        _ = ct;
        var spec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.SpecJson) ? "{}" : row.SpecJson);
        string? blocked = null;
        var canApprove = false;
        try
        {
            if (master is null) throw new InvalidOperationException("DNA_GATE_NOT_SATISFIED: master exists thất bại.");
            var shaOk = KitVideoCharacterDnaRules.SameSha(master.Sha256, row.MasterSha256) && gate.ShaValid;
            KitVideoCharacterDnaRules.EnsureCanApprove(
                row.Status, spec, true, gate.MasterLocked, shaOk, gate.IdentityPass, gate.StressPass, gate.P0);
            canApprove = row.Status is "DRAFT" or "REJECTED";
        }
        catch (InvalidOperationException ex)
        {
            blocked = ex.Message;
        }
        var dnaSha = KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(row.SpecJson ?? "{}"));
        var shaMatch = master is not null && KitVideoCharacterDnaRules.SameSha(master.Sha256, row.MasterSha256) && gate.ShaValid;
        var checks = KitVideoCharacterDnaRules.EvaluateDirectorGate(
            spec, master is not null, gate.MasterLocked, shaMatch, gate.IdentityPass, gate.StressPass, gate.P0);
        var gatePass = KitVideoCharacterDnaRules.GatePass(checks);
        return new KitVideoCharacterDnaDto(
            row.Id, row.DnaCode, row.CharacterId, row.CharacterName, row.EraId,
            row.MasterReferenceId, row.MasterCode, row.MasterSha256, row.DnaVersion, row.Status,
            row.DocumentId, row.CreatedAt, row.ApprovedAt, row.ApprovedBy, row.LockedAt, row.LockedBy,
            row.Note, spec, canApprove, row.Status is "DRAFT" or "REJECTED", row.Status == "LOCKED",
            blocked, master is null ? null : ToMaster(master),
            dnaSha, gate.IdentityPass, gate.IdentityHave, gate.StressPass, gate.StressHave, gate.P0,
            row.Status is "DRAFT" or "REJECTED", row.Status == "REJECTED",
            null,
            checks.Select(x => new KitVideoDnaCheckItemDto(x.Code, x.Label, x.Pass, x.Reason)).ToList(),
            gatePass);
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

    private static KitVideoMasterLockDto ToMaster(KitVideoMasterLockRepository.MasterRow m) =>
        new(
            m.Id, m.MasterCode, "CHAR-001", m.CharacterName, m.EraId, m.SourceCandidateId, m.SourceCandidateCode,
            m.SourceVariation, m.ArtifactPath, m.Sha256, m.VisionFingerprint, m.DnaVersion, m.IdentityTestResult,
            m.StressTestResult, m.MasterReviewResult, m.LockedBy, m.LockedAt, m.LockReason, m.Version, m.Status,
            m.CanonPointerId, true);

    private static object AuditPayload(KitVideoCharacterDnaRepository.DnaRow row, string? actor, string? dnaSha256 = null) => new
    {
        character_id = row.CharacterId,
        dna_id = row.Id,
        dna_version = row.DnaVersion,
        master_id = row.MasterReferenceId,
        master_sha256 = row.MasterSha256,
        dna_sha256 = dnaSha256,
        approved_by = actor,
        timestamp = DateTimeOffset.UtcNow,
    };

    private static string NormalizeVersion(string? raw)
    {
        var v = (raw ?? "").Trim();
        if (v.Equals(KitVideoCharacterDnaRules.DnaCode, StringComparison.OrdinalIgnoreCase)) return "V1";
        if (v.StartsWith("DNA-", StringComparison.OrdinalIgnoreCase)) return v[4..];
        return v.Length == 0 ? "V1" : v.ToUpperInvariant();
    }
}
