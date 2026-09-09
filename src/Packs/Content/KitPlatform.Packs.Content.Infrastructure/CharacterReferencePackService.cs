using System.Linq;
using System.Text;
using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterReferencePackService : ICharacterReferencePackService
{
    private readonly CharacterReferencePackRepository _repo;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly KitVideoArtifactStore _store;
    private readonly IKitVideoAssetService _assets;
    private readonly CharacterAuthorityStore _authority;

    public CharacterReferencePackService(
        CharacterReferencePackRepository repo,
        KitVideoCharacterDnaRepository dna,
        KitVideoMasterLockRepository locks,
        KitVideoProductionReferencePackRepository prp,
        KitVideoArtifactStore store,
        IKitVideoAssetService assets,
        CharacterAuthorityStore authority)
    {
        _repo = repo;
        _dna = dna;
        _locks = locks;
        _prp = prp;
        _store = store;
        _assets = assets;
        _authority = authority;
    }

    public async Task<CharacterReferencePackGetDto> GetAsync(string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var ctx = await ContextAsync(id, eraId, cancellationToken);
        var pack = Latest(await _repo.ListByCharacterAsync(id, eraId, cancellationToken));
        if (pack is not null) pack = await EnsureSpecAsync(pack, ctx, cancellationToken);
        var dto = pack is null ? null : await ToDto(pack, ctx, cancellationToken);
        if (dto is null)
        {
            var workspace = await _authority.ReadCrpAsync(id, eraId, cancellationToken);
            if (workspace is { Items.Count: > 0 })
                dto = await ToWorkspaceDto(workspace, ctx, cancellationToken);
        }
        var canCreate = CharacterReferencePackRules.CanCreateOfficialPack(
            CharacterReferencePackRules.GatePass(ctx.Authority), pack is null && dto is null, ctx.OfficialMasterRow);
        var missing = dto?.MissingTypes ?? CharacterReferencePackRules.RequiredTypes.ToList();
        var masterCandidate = ctx.Master?.SourceCandidateId is { } sid && sid != Guid.Empty
            ? sid
            : ctx.OfficialMasterRow ? null : ctx.Master?.Id;
        return new CharacterReferencePackGetDto(
            dto, canCreate, canCreate ? null : (dto?.Blocked ?? FirstFail(ctx.Authority)),
            ctx.MasterLocked, ctx.DnaLocked, ctx.CharacterName, dto?.Authority ?? ToChecks(ctx.Authority),
            masterCandidate, CharacterReferencePackRules.DisplayAge(ctx.DnaSpec),
            CharacterReferencePackRules.DisplaySummary(ctx.DnaSpec),
            ctx.PrpLocked, ctx.PrpSha, dto?.CanUse ?? false, missing);
    }

    public async Task<CharacterReferencePackDto> CreateAsync(string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        if (CharacterReferencePackRules.CreatesPixels("CREATE"))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: không generate ảnh.");
        var ctx = await ContextAsync(id, eraId, cancellationToken);
        var existing = Latest(await _repo.ListByCharacterAsync(id, eraId, cancellationToken));
        if (existing is not null)
        {
            existing = await EnsureSpecAsync(existing, ctx, cancellationToken);
            return await ToDto(existing, ctx, cancellationToken);
        }
        CharacterReferencePackRules.EnsureCanCreate(ctx.Authority);
        if (!ctx.OfficialMasterRow)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: " + CharacterReferencePackRules.StaffOfficialCreateBlocked);
        if (ctx.Master is null || ctx.Dna is null)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: thiếu Master hoặc DNA.");
        var spec = CharacterReferencePackRules.DeriveSpec(
            id, ctx.CharacterName, eraId,
            ctx.Master.Id.ToString("N"), ctx.Master.Sha256, ctx.Master.Status,
            ctx.Dna.Id.ToString("N"), ctx.DnaSha, ctx.Dna.Status, ctx.DnaSpec,
            ctx.Prp?.Id.ToString("N") ?? "", ctx.PrpSha, ctx.Prp?.Status ?? "");
        var row = new CharacterReferencePackRepository.PackRow
        {
            Id = Guid.NewGuid(),
            PackCode = CharacterReferencePackRules.PackCode(id, eraId, "V1"),
            CharacterId = id,
            CharacterName = ctx.CharacterName,
            EraId = eraId,
            PackVersion = "V1",
            MasterReferenceId = ctx.Master.Id,
            MasterSha256 = ctx.Master.Sha256,
            CharacterDnaId = ctx.Dna.Id,
            DnaSha256 = ctx.DnaSha,
            CreatedBy = actor,
            ExtraJson = spec.GetRawText(),
        };
        row = await _repo.InsertDraftAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "REFERENCE_PACK_CREATED", Audit(row, actor), actor, cancellationToken);
        return await ToDto(row, ctx, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> UpsertItemAsync(string characterId, Guid packId, CharacterReferenceItemRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        CharacterReferencePackRules.EnsureImmutable("EDIT", pack.Status);
        if (CharacterReferencePackRules.TouchesGolden(request.ArtifactPath))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: không dùng Golden SH01-01.");
        var type = CharacterReferencePackRules.NormalizeRefType(request.Type);
        if (type.Length == 0) throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: thiếu loại reference.");
        if (CharacterReferencePackRules.ContainsProvider(request.Metadata?.GetRawText()))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Reference Pack không lưu provider prompt.");
        var ctx = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
        var required = CharacterReferencePackRules.RequiredTypes.Contains(type);
        var item = new CharacterReferencePackRepository.ItemRow
        {
            Id = Guid.NewGuid(),
            PackId = pack.Id,
            RefType = type,
            Required = required,
            ArtifactPath = request.ArtifactPath?.Trim() ?? "",
            ArtifactSha256 = request.ArtifactSha256?.Trim() ?? "",
            Status = string.IsNullOrWhiteSpace(request.ArtifactPath) ? "MISSING" : "DRAFT",
            MetadataJson = ProvenanceMetadata(request.Metadata, pack.CharacterId, ctx),
        };
        CharacterReferenceApprovalLockRules.EnsureMutable("EDIT", pack.Status);
        await _repo.UpsertItemAsync(item, cancellationToken);
        await _repo.SetStatusAsync(pack.Id, "DRAFT", actor, pack.Notes, null, cancellationToken);
        return await ToDto(await _repo.GetByIdAsync(pack.Id, cancellationToken) ?? pack, ctx, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> AttachItemAsync(
        string characterId, Guid packId, string type, byte[] bytes, string? fileName, string actor, CancellationToken cancellationToken = default)
    {
        if (bytes is null || bytes.Length < 32)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Artifact missing");
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        CharacterReferencePackRules.EnsureImmutable("EDIT", pack.Status);
        CharacterReferenceApprovalLockRules.EnsureMutable("EDIT", pack.Status);
        var kind = CharacterReferencePackRules.NormalizeRefType(type);
        if (kind.Length == 0) throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: thiếu loại reference.");
        if (CharacterReferencePackRules.TouchesGolden(fileName) || CharacterReferencePackRules.CreatesPixels(kind))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: không generate / không dùng Golden.");
        var incomingSha = KitVideoIntegrityRules.Sha256Hex(bytes);
        var existing = (await _repo.ListItemsAsync(pack.Id, cancellationToken))
            .FirstOrDefault(x => string.Equals(x.RefType, kind, StringComparison.OrdinalIgnoreCase));
        if (existing is not null
            && CharacterReferencePackRules.SameSha(existing.ArtifactSha256, incomingSha))
        {
            var ctxSame = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
            return await ToDto(pack, ctxSame, cancellationToken);
        }
        var stored = _store.PersistReference(pack.CharacterId, pack.EraId, pack.Id, kind, bytes);
        if (CharacterReferencePackRules.TouchesGolden(stored.Path))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: không dùng Golden SH01-01.");
        return await UpsertItemAsync(pack.CharacterId, pack.Id, new CharacterReferenceItemRequest(
            kind, stored.Path, KitVideoIntegrityRules.Sha256Hex(stored.Bytes)), actor, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> RegisterExistingAsync(
        string characterId, Guid packId, string type, string sourcePath, string actor, CancellationToken cancellationToken = default)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var kind = CharacterReferencePackRules.NormalizeRefType(type);
        if (kind.Length == 0) throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: thiếu loại reference.");
        if (!CharacterReferencePackRules.PathMatchesView(sourcePath, kind)
            || !CharacterReferencePackRules.AllowedRegisterPath(sourcePath, id))
        {
            throw new InvalidOperationException(
                $"{CharacterReferencePackRules.AssetMissingCode}: {CharacterReferencePackRules.StaffMissingReason(kind)} Không tạo ảnh mới.");
        }
        var bytes = await File.ReadAllBytesAsync(Path.GetFullPath(sourcePath), cancellationToken);
        if (CharacterReferencePackRules.CreatesPixels("REGISTER") || CharacterReferencePackRules.TouchesGolden(sourcePath))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: không generate / không dùng Golden.");
        return await AttachItemAsync(id, packId, kind, bytes, Path.GetFileName(sourcePath), actor, cancellationToken);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadItemImageAsync(
        string characterId, Guid packId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var official = await TryOfficialOwned(characterId, packId, cancellationToken);
        if (official is not null)
        {
            var items = await _repo.ListItemsAsync(packId, cancellationToken);
            var item = items.FirstOrDefault(x => x.Id == itemId);
            if (item is null || string.IsNullOrWhiteSpace(item.ArtifactPath)) return null;
            var bytes = _store.Read(item.ArtifactPath);
            if (bytes is null || bytes.Length < 32) return null;
            var mime = KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg";
            return (bytes, mime);
        }
        return await ReadWorkspaceItemImageAsync(characterId, packId, itemId, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> ValidateAsync(string characterId, Guid packId, string actor, CancellationToken cancellationToken = default)
    {
        if (await TryOfficialOwned(characterId, packId, cancellationToken) is null)
            return await RequireWorkspaceDto(characterId, packId, cancellationToken);
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        var ctx = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_VALIDATION_REQUESTED", Audit(pack, actor), actor, cancellationToken);
        var eval = await Evaluate(pack, ctx, cancellationToken);
        var spec = ReadSpec(pack);
        var specConflicts = CharacterReferencePackRules.EvaluateSpecConflicts(ctx.DnaSpec, spec);
        var identity = eval.Identity.Concat(specConflicts).ToList();
        try
        {
            CharacterReferencePackRules.EnsureValidatedOrThrow(
                PackAuthority(pack, ctx), eval.Coverage, eval.Artifacts, identity);
        }
        catch (InvalidOperationException)
        {
            await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_VALIDATION_FAILED", Audit(pack, actor), actor, cancellationToken);
            throw;
        }
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_VALIDATED", Audit(pack, actor), actor, cancellationToken);
        if (pack.Status is "DRAFT" or "REVIEW" or "REJECTED")
            pack = await _repo.SetStatusAsync(pack.Id, "VALIDATED", actor, pack.Notes, null, cancellationToken);
        return await ToDto(pack, ctx, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> ApproveAsync(string characterId, Guid packId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        CharacterReferenceApprovalLockRules.EnsureDirector(actor);
        if (await TryOfficialOwned(characterId, packId, cancellationToken) is null)
            return await MutateWorkspaceAsync(characterId, packId, "DIRECTOR_APPROVED", canUse: false, actor, cancellationToken);
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        var ctx = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
        var eval = await Evaluate(pack, ctx, cancellationToken);
        CharacterReferenceApprovalLockRules.EnsureApproveReady(
            pack.Status, PackAuthority(pack, ctx), eval.Coverage, eval.Artifacts, eval.Identity,
            pack.CharacterId, eval.Entries);
        if (!CharacterReferenceApprovalLockRules.ShouldWriteApprove(pack.Status))
            return await ToDto(pack, ctx, cancellationToken);
        var masterSha = pack.MasterSha256;
        var dnaSha = pack.DnaSha256;
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_APPROVAL_REQUESTED", Audit(pack, actor), actor, cancellationToken);
        pack = await _repo.SetStatusAsync(pack.Id, CharacterReferenceApprovalLockRules.ApprovedStatus, actor, note ?? pack.Notes, null, cancellationToken);
        if (!CharacterReferenceApprovalLockRules.AuthorityUnchanged(masterSha, pack.MasterSha256)
            || !CharacterReferenceApprovalLockRules.AuthorityUnchanged(dnaSha, pack.DnaSha256))
            throw new InvalidOperationException("REFERENCE_MASTER_MISMATCH: Approval không được đổi authority SHA.");
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_APPROVED", Audit(pack, actor), actor, cancellationToken);
        return await ToDto(pack, ctx, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> RejectAsync(string characterId, Guid packId, string actor, string? note, CancellationToken cancellationToken = default)
    {
        CharacterReferencePackRules.EnsureDirector(actor);
        CharacterReferencePackRules.EnsureRejectReason(note);
        if (await TryOfficialOwned(characterId, packId, cancellationToken) is null)
            return await MutateWorkspaceAsync(characterId, packId, "REJECTED", canUse: false, actor, cancellationToken);
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        CharacterReferencePackRules.EnsureImmutable("REJECT", pack.Status);
        pack = await _repo.SetStatusAsync(pack.Id, "REJECTED", actor, note!.Trim(), null, cancellationToken);
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_REJECTED", Audit(pack, actor), actor, cancellationToken);
        var ctx = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
        return await ToDto(pack, ctx, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> LockAsync(string characterId, Guid packId, string actor, CancellationToken cancellationToken = default)
    {
        CharacterReferenceApprovalLockRules.EnsureDirector(actor);
        if (await TryOfficialOwned(characterId, packId, cancellationToken) is null)
            return await MutateWorkspaceAsync(characterId, packId, "LOCKED", canUse: true, actor, cancellationToken);
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        var ctx = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
        var eval = await Evaluate(pack, ctx, cancellationToken);
        CharacterReferenceApprovalLockRules.EnsureLockReady(
            pack.Status, PackAuthority(pack, ctx), eval.Coverage, eval.Artifacts, eval.Identity,
            pack.CharacterId, eval.Entries);
        if (!CharacterReferenceApprovalLockRules.ShouldWriteLock(pack.Status))
            return await ToDto(pack, ctx, cancellationToken);
        var masterSha = pack.MasterSha256;
        var dnaSha = pack.DnaSha256;
        var sha = CharacterReferencePackRules.PackSha(
            pack.CharacterId, pack.PackVersion, pack.MasterReferenceId.ToString("N"),
            pack.MasterSha256, pack.CharacterDnaId.ToString("N"), pack.DnaSha256, eval.Entries, ReadSpec(pack));
        pack = await _repo.SetStatusAsync(pack.Id, CharacterReferenceApprovalLockRules.LockedStatus, actor, pack.Notes, sha, cancellationToken);
        if (!CharacterReferenceApprovalLockRules.AuthorityUnchanged(masterSha, pack.MasterSha256)
            || !CharacterReferenceApprovalLockRules.AuthorityUnchanged(dnaSha, pack.DnaSha256))
            throw new InvalidOperationException("REFERENCE_MASTER_MISMATCH: Lock không được đổi authority SHA.");
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_LOCKED", Audit(pack, actor, sha), actor, cancellationToken);
        return await ToDto(pack, ctx, cancellationToken);
    }

    public async Task<CharacterReferencePackDto> SupersedeAsync(string characterId, Guid packId, string actor, CancellationToken cancellationToken = default)
    {
        var pack = await RequireOwned(characterId, packId, cancellationToken);
        if (pack.Status != "LOCKED")
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: chỉ supersede pack đã khóa.");
        var ctx = await ContextAsync(pack.CharacterId, pack.EraId, cancellationToken);
        CharacterReferencePackRules.EnsureCanCreate(ctx.Authority);
        await _repo.MarkSupersededAsync(pack.Id, cancellationToken);
        await _repo.InsertEventAsync(pack.Id, "REFERENCE_PACK_SUPERSEDED", Audit(pack, actor), actor, cancellationToken);
        var next = CharacterReferencePackRules.NextVersion(pack.PackVersion);
        var row = new CharacterReferencePackRepository.PackRow
        {
            Id = Guid.NewGuid(),
            PackCode = CharacterReferencePackRules.PackCode(pack.CharacterId, pack.EraId, next),
            CharacterId = pack.CharacterId,
            CharacterName = pack.CharacterName,
            EraId = pack.EraId,
            PackVersion = next,
            MasterReferenceId = pack.MasterReferenceId,
            MasterSha256 = ctx.Master?.Sha256 ?? pack.MasterSha256,
            CharacterDnaId = pack.CharacterDnaId,
            DnaSha256 = ctx.DnaSha,
            CreatedBy = actor,
            SupersedesId = pack.Id,
            ExtraJson = ctx.Master is null || ctx.Dna is null
                ? pack.ExtraJson
                : CharacterReferencePackRules.DeriveSpec(
                    pack.CharacterId, pack.CharacterName, pack.EraId,
                    ctx.Master.Id.ToString("N"), ctx.Master.Sha256, ctx.Master.Status,
                    ctx.Dna.Id.ToString("N"), ctx.DnaSha, ctx.Dna.Status, ctx.DnaSpec,
                    ctx.Prp?.Id.ToString("N") ?? "", ctx.PrpSha, ctx.Prp?.Status ?? "").GetRawText(),
        };
        row = await _repo.InsertDraftAsync(row, cancellationToken);
        await _repo.InsertEventAsync(row.Id, "REFERENCE_PACK_CREATED", Audit(row, actor), actor, cancellationToken);
        return await ToDto(row, ctx, cancellationToken);
    }

    public IReadOnlyList<string> RunRegression() => CharacterReferencePackV1Regression.Run();

    public IReadOnlyList<string> RunCompletionRegression() => CharacterReferenceCompletionV1Regression.Run();

    public IReadOnlyList<string> RunApprovalLockRegression() => CharacterReferenceApprovalLockV1Regression.Run();

    public async Task<CharacterReferenceExistingFullBodyDto> FindExistingFullBodyAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var candidates = new List<CharacterReferenceCompletionRules.Candidate>();
        IReadOnlyList<KitVideoAssetDto> assets;
        try { assets = await _assets.ListAsync("FAMIXA", cancellationToken); }
        catch { assets = []; }
        foreach (var asset in assets.Where(a => CharacterReferencePackRules.SameTenant(a.AssetCode, id)))
        {
            foreach (var reference in asset.References)
            {
                candidates.Add(new CharacterReferenceCompletionRules.Candidate(
                    asset.Id.ToString("D"), asset.AssetCode, asset.Era, reference.Kind, reference.Path));
            }
        }
        foreach (var path in EnumerateLocalImages(id, eraId))
        {
            candidates.Add(new CharacterReferenceCompletionRules.Candidate(
                Path.GetFileNameWithoutExtension(path), id, eraId,
                CharacterReferencePackRules.PathMatchesView(path, "FULL_BODY") ? "FULL_BODY" : "UNKNOWN",
                path));
        }
        var hit = CharacterReferenceCompletionRules.SelectFullBody(id, candidates);
        if (hit is null)
        {
            return new CharacterReferenceExistingFullBodyDto(
                id, eraId, false, false, null, id, "FULL_BODY", null,
                CharacterReferenceCompletionRules.StaffMissingFullBody);
        }
        return new CharacterReferenceExistingFullBodyDto(
            id, eraId, true, true, hit.AssetId, hit.CharacterId, "FULL_BODY", hit.Path,
            CharacterReferenceCompletionRules.StaffComplete);
    }

    private static IEnumerable<string> EnumerateLocalImages(string characterId, string eraId)
    {
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "App_Data"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "App_Data")),
        };
        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Directory.Exists(root)) continue;
            foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!file.Contains(characterId, StringComparison.OrdinalIgnoreCase)) continue;
                if (!file.Contains(eraId, StringComparison.OrdinalIgnoreCase)) continue;
                var ext = Path.GetExtension(file);
                if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) continue;
                yield return file;
            }
        }
    }

    private async Task<CharacterReferencePackRepository.PackRow> RequireOwned(string characterId, Guid packId, CancellationToken ct)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var pack = await _repo.GetByIdAsync(packId, ct)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
        if (!CharacterReferencePackRules.SameTenant(id, pack.CharacterId))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Character ownership conflict");
        return pack;
    }

    private async Task<CharacterReferencePackRepository.PackRow?> TryOfficialOwned(
        string characterId, Guid packId, CancellationToken ct)
    {
        var pack = await _repo.GetByIdAsync(packId, ct);
        if (pack is null) return null;
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        if (!CharacterReferencePackRules.SameTenant(id, pack.CharacterId))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Character ownership conflict");
        return pack;
    }

    private async Task<CharacterReferencePackDto> RequireWorkspaceDto(
        string characterId, Guid packId, CancellationToken ct)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var ctx = await ContextAsync(id, "ERA-01", ct);
        var crp = await _authority.ReadCrpAsync(id, ctx.Master?.EraId ?? "ERA-01", ct)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
        var snap = await _authority.ReadAsync(id, crp.EraId, ct)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
        if (packId != snap.AssetId && packId != Guid.Empty)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Character ownership conflict");
        return await ToWorkspaceDto(crp, ctx, ct);
    }

    private async Task<(byte[] Bytes, string Mime)?> ReadWorkspaceItemImageAsync(
        string characterId, Guid packId, Guid itemId, CancellationToken ct)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var crp = await _authority.ReadCrpAsync(id, "ERA-01", ct);
        if (crp is null) return null;
        var snap = await _authority.ReadAsync(id, crp.EraId, ct);
        if (snap is not null && packId != snap.AssetId && packId != Guid.Empty) return null;
        var item = crp.Items.FirstOrDefault(i =>
            CharacterReferencePackRules.WorkspaceItemId(id, i.Type, i.Sha256) == itemId);
        if (item is null || string.IsNullOrWhiteSpace(item.Path)) return null;
        var bytes = _store.Read(item.Path);
        if (bytes is null || bytes.Length < 32) return null;
        return (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg");
    }

    private async Task<CharacterReferencePackDto> MutateWorkspaceAsync(
        string characterId, Guid packId, string status, bool canUse, string actor, CancellationToken ct)
    {
        var id = CharacterReferencePackRules.NormalizeCharacterId(characterId);
        var ctx = await ContextAsync(id, "ERA-01", ct);
        var snap = await _authority.ReadAsync(id, ctx.Master?.EraId ?? "ERA-01", ct)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
        var crp = await _authority.ReadCrpAsync(id, snap.EraId, ct)
            ?? throw new InvalidOperationException("Character Reference Pack không tồn tại.");
        if (packId != snap.AssetId && packId != Guid.Empty)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Character ownership conflict");
        if (string.Equals(crp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CRP_LOCKED: bộ ảnh chuẩn đã khóa.");
        if (status == "LOCKED" && crp.Status is not ("APPROVED" or "DIRECTOR_APPROVED"))
            throw new InvalidOperationException("DIRECTOR_APPROVAL_REQUIRED: Director chưa duyệt. Không khóa từ DRAFT.");
        if (status == "DIRECTOR_APPROVED" && crp.Coverage < 4 && crp.Items.Count < 4)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: bộ tham chiếu chưa đủ điều kiện duyệt.");
        _ = actor;
        await _authority.WriteCrpAsync(snap.AssetId, crp with { Status = status, CanUse = canUse }, ct);
        var next = await _authority.ReadCrpAsync(id, snap.EraId, ct) ?? crp with { Status = status, CanUse = canUse };
        return await ToWorkspaceDto(next, ctx, ct);
    }

    private async Task<CharacterReferencePackDto> ToWorkspaceDto(
        CharacterAuthorityStore.CrpSnapshot crp, Context ctx, CancellationToken ct)
    {
        var snap = await _authority.ReadAsync(ctx.CharacterId, crp.EraId, ct);
        var packId = snap?.AssetId ?? Guid.Empty;
        var emptyMeta = JsonSerializer.SerializeToElement(new { });
        var entries = CharacterReferencePackRules.RequiredTypes.Select(type =>
        {
            var item = crp.Items.FirstOrDefault(i => string.Equals(i.Type, type, StringComparison.OrdinalIgnoreCase));
            var path = item?.Path ?? "";
            var sha = item?.Sha256 ?? "";
            var live = string.IsNullOrWhiteSpace(path) ? "" : LiveSha(path);
            return new CharacterReferencePackRules.RefEntry(
                type, path, sha, string.IsNullOrWhiteSpace(live) ? null : live, emptyMeta, true);
        }).ToList();
        var coverage = CharacterReferencePackRules.EvaluateCoverage(entries);
        var artifacts = CharacterReferencePackRules.EvaluateArtifacts(entries);
        var identity = CharacterReferencePackRules.EvaluateIdentity(ctx.DnaSpec, entries);
        var coverageReady = CharacterReferencePackRules.GatePass(coverage);
        var artifactPass = CharacterReferencePackRules.GatePass(artifacts);
        var identityPass = !CharacterReferencePackRules.IdentityBlocks(identity);
        var provenance = CharacterReferencePackRules.EvaluateItemProvenance(
            ctx.Master?.Sha256 ?? "", ctx.DnaSha, ctx.PrpSha, entries);
        var ready = CharacterReferencePackRules.ReadyForDirector(ctx.Authority, coverage, artifacts, identity)
            && CharacterReferencePackRules.GatePass(provenance)
            && !CharacterReferenceRegenerationV1Rules.IsRejected(crp.Status);
        var shaMatch = ctx.Authority.Where(x => x.Code is "master_sha" or "dna_sha" or "prp_sha").All(x => x.Pass);
        var canUse = CharacterReferencePackRules.CanUse(
            ctx.MasterLocked, ctx.DnaLocked, ctx.PrpLocked, shaMatch, crp.Status, coverageReady && artifactPass, identityPass);
        var missing = CharacterReferencePackRules.MissingTypes(coverage);
        var items = crp.Items.Select(i => new CharacterReferenceItemDto(
            CharacterReferencePackRules.WorkspaceItemId(ctx.CharacterId, i.Type, i.Sha256),
            i.Type,
            CharacterReferencePackRules.RequiredTypes.Contains(i.Type, StringComparer.OrdinalIgnoreCase),
            i.Path,
            i.Sha256,
            crp.Status,
            emptyMeta)).ToList();
        string? blocked = ready ? null : FirstFail(ctx.Authority.Concat(coverage).Concat(artifacts).Concat(provenance).ToList())
            ?? (identityPass ? null : "Ảnh tham chiếu đang khác với đặc điểm nhân vật đã khóa.");
        return new CharacterReferencePackDto(
            packId,
            CharacterReferencePackRules.PackCode(ctx.CharacterId, crp.EraId, "V1"),
            ctx.CharacterId,
            ctx.CharacterName,
            crp.EraId,
            "V1",
            crp.Status,
            CharacterReferencePackRules.StatusVi(crp.Status),
            ctx.Master?.Id ?? Guid.Empty,
            ctx.Master?.Sha256 ?? "",
            ctx.MasterLocked,
            ctx.Dna?.Id ?? Guid.Empty,
            ctx.DnaSha,
            ctx.DnaLocked,
            coverage.Count(x => x.Pass),
            coverage.Count,
            coverageReady,
            identityPass,
            artifactPass,
            ready,
            canUse && string.Equals(crp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase) && ready,
            string.Equals(crp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase),
            blocked,
            crp.Sha256,
            "",
            DateTimeOffset.UtcNow,
            null,
            null,
            null,
            null,
            items,
            ToChecks(coverage),
            identity.Select(i => new CharacterReferenceIdentityDto(i.Attribute, i.Verdict, i.LockedValue, i.ReferenceValue, i.Message)).ToList(),
            ToChecks(ctx.Authority),
            null,
            null,
            null,
            ctx.Prp?.Id,
            ctx.PrpSha,
            ctx.PrpLocked,
            canUse && string.Equals(crp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase),
            missing,
            missing.Count == 0 ? null : CharacterReferencePackRules.AssetMissingCode);
    }

    private sealed record Context(
        string CharacterId,
        string CharacterName,
        KitVideoMasterLockRepository.MasterRow? Master,
        KitVideoCharacterDnaRepository.DnaRow? Dna,
        KitVideoProductionReferencePackRepository.PackRow? Prp,
        JsonElement DnaSpec,
        string DnaSha,
        string PrpSha,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool OfficialMasterRow,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> Authority);

    private sealed record Eval(
        IReadOnlyList<CharacterReferencePackRules.RefEntry> Entries,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> Coverage,
        IReadOnlyList<CharacterReferencePackRules.CheckItem> Artifacts,
        IReadOnlyList<CharacterReferencePackRules.IdentityFinding> Identity);

    private async Task<Context> ContextAsync(string characterId, string eraId, CancellationToken ct)
    {
        var master = await _locks.GetByVersionAsync(characterId, eraId, "V1", ct);
        var dna = await _dna.GetByVersionAsync(characterId, eraId, "V1", ct);
        var prp = await _prp.GetByVersionAsync(characterId, eraId, "V1", ct);
        var officialMasterRow = master is not null;
        if (master is null && dna is null && prp is null)
        {
            var snap = await _authority.ReadAsync(characterId, eraId, ct);
            master = _authority.ToOfficialMaster(snap);
            dna = _authority.ToOfficialDna(snap);
            prp = _authority.ToOfficialPrp(snap);
        }
        var masterLocked = master is not null && CharacterReferencePackRules.MasterLocked(master.Status);
        var dnaLocked = dna is not null && CharacterReferencePackRules.DnaLocked(dna.Status);
        var prpLocked = prp is not null && string.Equals(prp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase);
        var liveMasterSha = master is null ? "" : LiveSha(master.ArtifactPath);
        var masterShaMatch = master is not null && CharacterReferencePackRules.SameSha(master.Sha256, liveMasterSha);
        var dnaSpec = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(dna?.SpecJson) ? "{}" : dna!.SpecJson);
        var dnaSha = dna is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(dna.SpecJson ?? "{}"));
        var dnaShaMatch = CharacterReferencePackRules.ShaExists(dnaSha)
            && (dna is null || CharacterReferencePackRules.SameSha(dna.MasterSha256, master?.Sha256));
        var livePrpSha = prp is null ? "" : KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(prp.SpecJson ?? "{}"));
        var prpSha = prp?.PrpSha256 ?? livePrpSha;
        var prpShaMatch = prpLocked
            && CharacterReferencePackRules.ShaExists(prpSha)
            && (string.IsNullOrWhiteSpace(prp?.PrpSha256) || CharacterReferencePackRules.SameSha(prp.PrpSha256, livePrpSha) || CharacterReferencePackRules.SameSha(prp.PrpSha256, prpSha))
            && (prp is null || CharacterReferencePackRules.SameSha(prp.MasterSha256, master?.Sha256))
            && (prp is null || CharacterReferencePackRules.SameSha(prp.DnaSha256, dnaSha));
        var ownership = (master is null || CharacterReferencePackRules.SameTenant(characterId, master.CharacterId))
            && (dna is null || CharacterReferencePackRules.SameTenant(characterId, dna.CharacterId))
            && (prp is null || CharacterReferencePackRules.SameTenant(characterId, prp.CharacterId));
        var authority = CharacterReferencePackRules.EvaluateAuthorityGate(
            master is not null, masterLocked, masterShaMatch,
            dna is not null, dnaLocked, dnaShaMatch, ownership,
            prp is not null, prpLocked, prpShaMatch);
        var name = master?.CharacterName ?? dna?.CharacterName ?? "";
        return new Context(characterId, name, master, dna, prp, dnaSpec, dnaSha, prpSha, masterLocked, dnaLocked, prpLocked, officialMasterRow, authority);
    }

    private async Task<Eval> Evaluate(CharacterReferencePackRepository.PackRow pack, Context ctx, CancellationToken ct)
    {
        var items = await _repo.ListItemsAsync(pack.Id, ct);
        var byType = items.ToDictionary(x => x.RefType, StringComparer.OrdinalIgnoreCase);
        var extraTypes = items.Select(x => x.RefType).Where(t =>
            !CharacterReferencePackRules.RequiredTypes.Contains(t)
            && !CharacterReferencePackRules.OptionalTypes.Contains(t));
        var entries = CharacterReferencePackRules.RequiredTypes
            .Concat(CharacterReferencePackRules.OptionalTypes)
            .Concat(extraTypes)
            .Distinct()
            .Select(type =>
            {
                byType.TryGetValue(type, out var row);
                var path = row?.ArtifactPath ?? "";
                var sha = row?.ArtifactSha256 ?? "";
                var live = string.IsNullOrWhiteSpace(path) ? null : LiveSha(path);
                var meta = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row?.MetadataJson) ? "{}" : row!.MetadataJson);
                return new CharacterReferencePackRules.RefEntry(type, path, sha, string.IsNullOrWhiteSpace(live) ? null : live, meta, CharacterReferencePackRules.RequiredTypes.Contains(type));
            })
            .ToList();
        return new Eval(
            entries,
            CharacterReferencePackRules.EvaluateCoverage(entries),
            CharacterReferencePackRules.EvaluateArtifacts(entries.Where(e => e.Required || !string.IsNullOrWhiteSpace(e.ArtifactPath)).ToList()),
            CharacterReferencePackRules.EvaluateIdentity(ctx.DnaSpec, entries));
    }

    private async Task<CharacterReferencePackDto> ToDto(CharacterReferencePackRepository.PackRow pack, Context ctx, CancellationToken ct)
    {
        var eval = await Evaluate(pack, ctx, ct);
        var items = await _repo.ListItemsAsync(pack.Id, ct);
        var authority = PackAuthority(pack, ctx);
        var coverageReady = CharacterReferencePackRules.GatePass(eval.Coverage);
        var artifactPass = CharacterReferencePackRules.GatePass(eval.Artifacts);
        var spec = ReadSpec(pack);
        var specConflicts = CharacterReferencePackRules.EvaluateSpecConflicts(ctx.DnaSpec, spec);
        var identity = eval.Identity.Concat(specConflicts).ToList();
        var identityPass = !CharacterReferencePackRules.IdentityBlocks(identity);
        var provenance = CharacterReferencePackRules.EvaluateItemProvenance(
            ctx.Master?.Sha256 ?? "", ctx.DnaSha, ctx.PrpSha, eval.Entries);
        var duplicates = CharacterReferencePackRules.EvaluateDuplicateOrientation(items.Select(i => i.RefType));
        var ready = CharacterReferencePackRules.ReadyForDirector(authority, eval.Coverage, eval.Artifacts, identity)
            && CharacterReferencePackRules.GatePass(provenance)
            && duplicates.Count == 0;
        string? blocked = ready ? null : FirstFail(authority.Concat(eval.Coverage).Concat(eval.Artifacts).Concat(provenance).Concat(duplicates).ToList())
            ?? (identityPass ? null : "Ảnh tham chiếu đang khác với đặc điểm nhân vật đã khóa.");
        var missing = CharacterReferencePackRules.MissingTypes(eval.Coverage);
        var shaMatch = authority.Where(x => x.Code is "master_sha" or "dna_sha" or "prp_sha").All(x => x.Pass);
        var canUse = CharacterReferencePackRules.CanUse(
            ctx.MasterLocked, ctx.DnaLocked, ctx.PrpLocked, shaMatch, pack.Status, coverageReady && artifactPass, identityPass);
        var productionReady = canUse && pack.Status == "LOCKED" && ready;
        var assetMissing = missing.Count == 0 ? null : CharacterReferencePackRules.AssetMissingCode;
        var gates = CharacterReferencePackRules.EvaluateValidationGates(
            authority, eval.Coverage, eval.Artifacts, identity,
            CharacterReferencePackRules.ShaExists(pack.MasterSha256)
                && CharacterReferencePackRules.ShaExists(pack.DnaSha256)
                && CharacterReferencePackRules.ShaExists(ctx.PrpSha)
                && CharacterReferencePackRules.GatePass(provenance),
            CharacterReferencePackRules.VersionNumber(pack.PackVersion) > 0);
        var conflicts = identity.Where(i => i.Verdict == "FAIL")
            .Select(i => new CharacterReferenceIdentityDto(i.Attribute, i.Verdict, i.LockedValue, i.ReferenceValue, i.Message))
            .ToList();
        return new CharacterReferencePackDto(
            pack.Id, pack.PackCode, pack.CharacterId, pack.CharacterName, pack.EraId, pack.PackVersion,
            pack.Status, CharacterReferencePackRules.StatusVi(pack.Status),
            pack.MasterReferenceId, pack.MasterSha256, ctx.MasterLocked,
            pack.CharacterDnaId, pack.DnaSha256, ctx.DnaLocked,
            eval.Coverage.Count(x => x.Pass), eval.Coverage.Count, coverageReady, identityPass, artifactPass,
            ready, productionReady, pack.Status == "LOCKED", blocked, pack.PackSha256, pack.Notes,
            pack.CreatedAt, pack.ApprovedAt, pack.ApprovedBy, pack.LockedAt, pack.LockedBy,
            items.Select(ToItem).ToList(), ToChecks(eval.Coverage),
            identity.Select(i => new CharacterReferenceIdentityDto(i.Attribute, i.Verdict, i.LockedValue, i.ReferenceValue, i.Message)).ToList(),
            ToChecks(authority), spec, ToChecks(gates), conflicts,
            ctx.Prp?.Id, ctx.PrpSha, ctx.PrpLocked, canUse, missing, assetMissing);
    }

    private static IReadOnlyList<CharacterReferencePackRules.CheckItem> PackAuthority(
        CharacterReferencePackRepository.PackRow pack, Context ctx) =>
        CharacterReferencePackRules.EvaluateAuthorityGate(
            ctx.Master is not null,
            ctx.MasterLocked,
            CharacterReferencePackRules.SameSha(pack.MasterSha256, ctx.Master?.Sha256)
                && ctx.Authority.Any(x => x.Code == "master_sha" && x.Pass),
            ctx.Dna is not null,
            ctx.DnaLocked,
            CharacterReferencePackRules.SameSha(pack.DnaSha256, ctx.DnaSha)
                && ctx.Authority.Any(x => x.Code == "dna_sha" && x.Pass),
            CharacterReferencePackRules.SameTenant(pack.CharacterId, ctx.CharacterId),
            ctx.Prp is not null,
            ctx.PrpLocked,
            CharacterReferencePackRules.ShaExists(ctx.PrpSha)
                && ctx.Authority.Any(x => x.Code == "prp_sha" && x.Pass));

    private static CharacterReferencePackRepository.PackRow? Latest(IReadOnlyList<CharacterReferencePackRepository.PackRow> rows) =>
        rows.Where(r => r.Status != "SUPERSEDED")
            .OrderByDescending(r => CharacterReferencePackRules.VersionNumber(r.PackVersion))
            .ThenByDescending(r => r.CreatedAt)
            .FirstOrDefault()
        ?? rows.OrderByDescending(r => CharacterReferencePackRules.VersionNumber(r.PackVersion)).FirstOrDefault();

    private async Task<CharacterReferencePackRepository.PackRow> EnsureSpecAsync(
        CharacterReferencePackRepository.PackRow pack, Context ctx, CancellationToken ct)
    {
        if (pack.Status is "LOCKED" or "APPROVED" or "DIRECTOR_APPROVED" or "SUPERSEDED") return pack;
        if (ctx.Master is null || ctx.Dna is null) return pack;
        var current = ReadSpec(pack);
        var hasIdentity = current.ValueKind == JsonValueKind.Object && current.TryGetProperty("identity", out _);
        var hasPrp = current.ValueKind == JsonValueKind.Object && current.TryGetProperty("prp", out _);
        if (hasIdentity && hasPrp) return pack;
        var spec = CharacterReferencePackRules.DeriveSpec(
            pack.CharacterId, ctx.CharacterName, pack.EraId,
            ctx.Master.Id.ToString("N"), ctx.Master.Sha256, ctx.Master.Status,
            ctx.Dna.Id.ToString("N"), ctx.DnaSha, ctx.Dna.Status, ctx.DnaSpec,
            ctx.Prp?.Id.ToString("N") ?? "", ctx.PrpSha, ctx.Prp?.Status ?? "");
        await _repo.SetExtraJsonAsync(pack.Id, spec.GetRawText(), ct);
        return await _repo.GetByIdAsync(pack.Id, ct) ?? pack;
    }

    private static JsonElement ReadSpec(CharacterReferencePackRepository.PackRow pack)
    {
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(pack.ExtraJson) ? "{}" : pack.ExtraJson);
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new { });
        }
    }

    private static CharacterReferenceItemDto ToItem(CharacterReferencePackRepository.ItemRow row)
    {
        var meta = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(row.MetadataJson) ? "{}" : row.MetadataJson);
        return new CharacterReferenceItemDto(row.Id, row.RefType, row.Required, row.ArtifactPath, row.ArtifactSha256, row.Status, meta);
    }

    private static IReadOnlyList<KitVideoDnaCheckItemDto> ToChecks(IReadOnlyList<CharacterReferencePackRules.CheckItem> items) =>
        items.Select(x => new KitVideoDnaCheckItemDto(x.Code, x.Label, x.Pass, x.Reason)).ToList();

    private static string? FirstFail(IReadOnlyList<CharacterReferencePackRules.CheckItem> items)
    {
        var fail = items.FirstOrDefault(x => !x.Pass);
        return fail is null ? null : $"CRP_GATE_NOT_SATISFIED: {fail.Reason}";
    }

    private string LiveSha(string path)
    {
        var bytes = _store.Read(path);
        return bytes is { Length: > 32 } ? KitVideoIntegrityRules.Sha256Hex(bytes) : "";
    }

    private static string ProvenanceMetadata(JsonElement? incoming, string characterId, Context ctx)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (incoming is { ValueKind: JsonValueKind.Object } src)
        {
            foreach (var p in src.EnumerateObject())
                payload[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
        }
        void Set(string key, string value)
        {
            if (payload.TryGetValue(key, out var cur) && cur is string s && s.Trim().Length > 0) return;
            payload[key] = value;
        }
        Set("characterId", characterId);
        Set("masterSha256", ctx.Master?.Sha256 ?? "");
        Set("dnaSha256", ctx.DnaSha);
        Set("prpSha256", ctx.PrpSha);
        return JsonSerializer.Serialize(payload);
    }

    private static object Audit(CharacterReferencePackRepository.PackRow row, string? actor, string? sha = null) => new
    {
        character_id = row.CharacterId,
        pack_id = row.Id,
        pack_version = row.PackVersion,
        master_id = row.MasterReferenceId,
        master_sha256 = row.MasterSha256,
        dna_id = row.CharacterDnaId,
        dna_sha256 = row.DnaSha256,
        pack_sha256 = sha ?? row.PackSha256,
        actor,
    };
}
