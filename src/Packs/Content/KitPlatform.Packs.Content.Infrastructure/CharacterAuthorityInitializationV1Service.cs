using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterAuthorityInitializationV1Service : ICharacterAuthorityInitializationV1Service
{
    private readonly IFamixaCharacterService _characters;
    private readonly CharacterAuthorityStore _store;
    private readonly KitVideoMasterReferenceRepository _candidates;
    private readonly KitVideoMasterLockRepository _locks;
    private readonly KitVideoCharacterDnaRepository _dna;
    private readonly KitVideoProductionReferencePackRepository _prp;
    private readonly KitVideoArtifactStore _artifacts;
    private readonly ICharacterAuthorityGenerationProvider _provider;

    public CharacterAuthorityInitializationV1Service(
        IFamixaCharacterService characters,
        CharacterAuthorityStore store,
        KitVideoMasterReferenceRepository candidates,
        KitVideoMasterLockRepository locks,
        KitVideoCharacterDnaRepository dna,
        KitVideoProductionReferencePackRepository prp,
        KitVideoArtifactStore artifacts,
        ICharacterAuthorityGenerationProvider provider)
    {
        _characters = characters;
        _store = store;
        _candidates = candidates;
        _locks = locks;
        _dna = dna;
        _prp = prp;
        _artifacts = artifacts;
        _provider = provider;
    }

    public IReadOnlyList<string> RunRegression() => CharacterAuthorityInitializationV1Regression.Run();

    public Task<CharacterAuthorityInitializationDto> GetAsync(
        string characterId, string? provider, string eraId = "ERA-01",
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, new CharacterAuthorityInitializationRequestDto(false, provider, eraId),
            CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionGet, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> PrepareMasterAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, request with { Confirm = false },
            CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionPrepare, cancellationToken);

    public async Task<CharacterAuthorityInitializationDto> ExecuteMasterAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, request, CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionExecute, cancellationToken);
        var gate = CharacterAuthorityInitializationV1Rules.Evaluate(ctx.Input);
        if (!CharacterAuthorityInitializationV1Rules.MayCallProvider(gate))
            return ToDto(ctx, gate, false, false);

        var result = await _provider.GenerateMasterAsync(
            new CharacterAuthorityGenerationRequest(
                ctx.CharacterId, ctx.EraId, "MASTER", ctx.CanonicalText, "3:4", ctx.IdentityVersion),
            cancellationToken);
        var gemini = string.Equals(_provider.ProviderId, "GEMINI", StringComparison.OrdinalIgnoreCase);

        if (!result.Succeeded || result.Bytes is not { Length: > 32 }
            || CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(null, result.ProviderRequestId))
        {
            if (ctx.Asset is not null)
            {
                await _candidates.InsertEventAsync(ctx.Asset.AssetId, ctx.Asset.VersionId, null,
                    "AUTHORITY_MASTER_FAILED", actor, "", "",
                    JsonSerializer.Serialize(new
                    {
                        document = CharacterAuthorityInitializationV1Rules.DocumentId,
                        error = result.ErrorCode ?? "GENERATION_FAILED",
                        auto_retry = false,
                    }), cancellationToken);
            }
            return ToDto(ctx, gate with
            {
                Status = "FAILED",
                Code = "GENERATION_FAILED",
                StaffMessage = CharacterAuthorityInitializationV1Rules.StaffFail,
                GenerationAllowed = false,
                MayCallProvider = false,
            }, false, true, gemini);
        }

        var attempt = (await _candidates.ListCandidatesAsync(ctx.Asset!.VersionId, cancellationToken)).Count + 1;
        var candidateCode = $"{ctx.CharacterId}-{ctx.EraId}-AUTH-MASTER-{attempt:000}";
        var persist = _artifacts.PersistMaster(ctx.CharacterId, ctx.EraId, candidateCode, result.Bytes);
        if (!persist.Check.Ok)
        {
            return ToDto(ctx, gate with
            {
                Status = "FAILED",
                Code = "GENERATION_FAILED",
                StaffMessage = CharacterAuthorityInitializationV1Rules.StaffFail,
                MayCallProvider = false,
            }, false, true, gemini);
        }

        var sha = KitVideoIntegrityRules.Sha256Hex(persist.Jpeg);
        var candidateId = Guid.NewGuid();
        await _candidates.InsertCandidateAsync(new KitVideoMasterReferenceRepository.CandidateRow
        {
            Id = candidateId,
            VersionId = ctx.Asset!.VersionId,
            CandidateCode = candidateCode,
            Role = "AUTHORITY_MASTER",
            GenerationAttempt = 1,
            ArtifactPath = persist.Path,
            Sha256 = sha,
            VisualStyleVersion = "V1",
            CharacterId = ctx.CharacterId,
            EraId = ctx.EraId,
            ImageType = "CHARACTER_CANDIDATE",
            Status = "REVIEW",
            QaStatus = "PENDING",
            QaJson = "{}",
            ExtraJson = JsonSerializer.Serialize(new
            {
                document = CharacterAuthorityInitializationV1Rules.DocumentId,
                authorityType = "MASTER",
                characterId = ctx.CharacterId,
                era = ctx.EraId,
                identityVersion = ctx.IdentityVersion,
                provider = _provider.ProviderId,
                providerRequestId = result.ProviderRequestId,
                artifactSha = sha,
            }),
            Fingerprint = ctx.Fingerprint ?? "",
        }, cancellationToken);
        await WriteStoreAsync(ctx, snap => snap with
        {
            MasterStatus = CharacterAuthorityInitializationV1Rules.ReadyForDirector,
            MasterSha = sha,
            MasterPath = persist.Path,
            MasterCandidateId = candidateId,
            MasterFingerprint = ctx.Fingerprint,
            Provider = _provider.ProviderId,
            ProviderRequestId = result.ProviderRequestId,
        }, cancellationToken);
        await _candidates.InsertEventAsync(ctx.Asset.AssetId, ctx.Asset.VersionId, candidateId,
            "AUTHORITY_MASTER_READY_FOR_DIRECTOR", actor, persist.Path, sha,
            JsonSerializer.Serialize(new
            {
                document = CharacterAuthorityInitializationV1Rules.DocumentId,
                auto_approve = false,
                auto_lock = false,
            }), cancellationToken);

        var fresh = await LoadAsync(characterId, request with { Confirm = false },
            CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionGet, cancellationToken);
        return ToDto(fresh, gate, true, true, gemini);
    }

    public Task<CharacterAuthorityInitializationDto> ApproveMasterAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionApprove, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> RejectMasterAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionReject, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> LockMasterAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionLock, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> PrepareDnaAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, request with { Confirm = false },
            CharacterAuthorityInitializationV1Rules.StageDna,
            CharacterAuthorityInitializationV1Rules.ActionPrepare, cancellationToken);

    public async Task<CharacterAuthorityInitializationDto> ExecuteDnaAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, request, CharacterAuthorityInitializationV1Rules.StageDna,
            CharacterAuthorityInitializationV1Rules.ActionExecute, cancellationToken);
        var outcome = CharacterAuthorityInitializationV1Application.CompileDna(ctx.Input);
        if (outcome.Gate.Status == "BLOCKED")
            return ToDto(ctx, outcome.Gate, false, false);

        var spec = CharacterAuthorityInitializationV1Rules.CompileDnaSpec(
            ctx.CharacterId, ctx.CharacterName, ctx.Role, ctx.EraId,
            ctx.MasterSha ?? "", ctx.IdentityVersion, ctx.Gender, ctx.Age);
        var sha = CharacterAuthorityInitializationV1Rules.SpecSha(spec);
        await WriteStoreAsync(ctx, snap => snap with
        {
            DnaStatus = CharacterAuthorityInitializationV1Rules.ReadyForDirector,
            DnaSha = sha,
            DnaSpec = JsonSerializer.Serialize(spec),
            DnaMasterSha = ctx.MasterSha,
        }, cancellationToken);
        var fresh = await LoadAsync(characterId, request with { Confirm = false },
            CharacterAuthorityInitializationV1Rules.StageDna,
            CharacterAuthorityInitializationV1Rules.ActionGet, cancellationToken);
        return ToDto(fresh, outcome.Gate, true, false);
    }

    public Task<CharacterAuthorityInitializationDto> ApproveDnaAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StageDna,
            CharacterAuthorityInitializationV1Rules.ActionApprove, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> RejectDnaAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StageDna,
            CharacterAuthorityInitializationV1Rules.ActionReject, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> LockDnaAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StageDna,
            CharacterAuthorityInitializationV1Rules.ActionLock, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> PreparePrpAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default) =>
        PreviewAsync(characterId, request with { Confirm = false },
            CharacterAuthorityInitializationV1Rules.StagePrp,
            CharacterAuthorityInitializationV1Rules.ActionPrepare, cancellationToken);

    public async Task<CharacterAuthorityInitializationDto> ExecutePrpAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, request, CharacterAuthorityInitializationV1Rules.StagePrp,
            CharacterAuthorityInitializationV1Rules.ActionExecute, cancellationToken);
        var outcome = CharacterAuthorityInitializationV1Application.CompilePrp(ctx.Input);
        if (outcome.Gate.Status == "BLOCKED")
            return ToDto(ctx, outcome.Gate, false, false);

        var spec = CharacterAuthorityInitializationV1Rules.CompilePrpSpec(
            ctx.CharacterId, ctx.CharacterName, ctx.EraId, ctx.MasterSha ?? "", ctx.DnaSha ?? "");
        var json = JsonSerializer.Serialize(spec);
        if (CharacterReferencePackRules.ContainsProvider(json))
            return ToDto(ctx, outcome.Gate with
            {
                Status = "BLOCKED",
                Code = "AUTHORITY_CONFLICT",
                StaffMessage = CharacterAuthorityInitializationV1Rules.StaffIdentity,
            }, false, false);
        var sha = CharacterAuthorityInitializationV1Rules.SpecSha(spec);
        await WriteStoreAsync(ctx, snap => snap with
        {
            PrpStatus = CharacterAuthorityInitializationV1Rules.ReadyForDirector,
            PrpSha = sha,
            PrpSpec = json,
            PrpMasterSha = ctx.MasterSha,
            PrpDnaSha = ctx.DnaSha,
        }, cancellationToken);
        var fresh = await LoadAsync(characterId, request with { Confirm = false },
            CharacterAuthorityInitializationV1Rules.StagePrp,
            CharacterAuthorityInitializationV1Rules.ActionGet, cancellationToken);
        return ToDto(fresh, outcome.Gate, true, false);
    }

    public Task<CharacterAuthorityInitializationDto> ApprovePrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StagePrp,
            CharacterAuthorityInitializationV1Rules.ActionApprove, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> RejectPrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StagePrp,
            CharacterAuthorityInitializationV1Rules.ActionReject, actor, cancellationToken);

    public Task<CharacterAuthorityInitializationDto> LockPrpAsync(
        string characterId, string actor, string eraId = "ERA-01", CancellationToken cancellationToken = default) =>
        AdvanceAsync(characterId, eraId, CharacterAuthorityInitializationV1Rules.StagePrp,
            CharacterAuthorityInitializationV1Rules.ActionLock, actor, cancellationToken);

    public async Task<(byte[] Bytes, string Mime)?> ReadMasterImageAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, new CharacterAuthorityInitializationRequestDto(false, null, eraId),
            CharacterAuthorityInitializationV1Rules.StageMaster,
            CharacterAuthorityInitializationV1Rules.ActionGet, cancellationToken);
        if (string.IsNullOrWhiteSpace(ctx.MasterPath)
            || CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(ctx.MasterCandidateId?.ToString(), ctx.MasterPath))
            return null;
        var bytes = _artifacts.Read(ctx.MasterPath);
        return bytes is { Length: > 32 } ? (bytes, "image/jpeg") : null;
    }

    private async Task<CharacterAuthorityInitializationDto> PreviewAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string stage, string action,
        CancellationToken ct)
    {
        var ctx = await LoadAsync(characterId, request, stage, action, ct);
        return ToDto(ctx, CharacterAuthorityInitializationV1Rules.Evaluate(ctx.Input), false, false);
    }

    private async Task<CharacterAuthorityInitializationDto> AdvanceAsync(
        string characterId, string eraId, string stage, string action, string actor, CancellationToken ct)
    {
        var request = new CharacterAuthorityInitializationRequestDto(false, null, eraId);
        var ctx = await LoadAsync(characterId, request, stage, action, ct);
        var gate = CharacterAuthorityInitializationV1Rules.Evaluate(ctx.Input);
        if (gate.Status == "BLOCKED")
            return ToDto(ctx, gate, false, false);

        var next = action switch
        {
            CharacterAuthorityInitializationV1Rules.ActionApprove => CharacterAuthorityInitializationV1Rules.Approved,
            CharacterAuthorityInitializationV1Rules.ActionLock => CharacterAuthorityInitializationV1Rules.Locked,
            CharacterAuthorityInitializationV1Rules.ActionReject => CharacterAuthorityInitializationV1Rules.Rejected,
            _ => throw new InvalidOperationException("AUTHORITY_CONFLICT"),
        };

        if (stage == CharacterAuthorityInitializationV1Rules.StageMaster
            && action == CharacterAuthorityInitializationV1Rules.ActionLock
            && CharacterAuthorityPipelineV1Rules.RejectPhotorealisticExternal(ctx.MasterPath))
            return ToDto(ctx, gate with
            {
                Status = "BLOCKED",
                Code = "INVALID_REFERENCE",
                StaffMessage = CharacterAuthorityPipelineV1Rules.StaffPhotoreal,
            }, false, false);

        await WriteStoreAsync(ctx, snap =>
        {
            var updated = stage switch
            {
                CharacterAuthorityInitializationV1Rules.StageMaster => snap with { MasterStatus = next },
                CharacterAuthorityInitializationV1Rules.StageDna => snap with { DnaStatus = next },
                _ => snap with { PrpStatus = next },
            };
            if (stage == CharacterAuthorityInitializationV1Rules.StageMaster
                && next == CharacterAuthorityInitializationV1Rules.Locked)
            {
                var dnaSpec = CharacterAuthorityInitializationV1Rules.CompileDnaSpec(
                    ctx.CharacterId, ctx.CharacterName, ctx.Role, ctx.EraId,
                    ctx.MasterSha ?? "", ctx.IdentityVersion, ctx.Gender, ctx.Age);
                var dnaJson = JsonSerializer.Serialize(dnaSpec);
                var dnaSha = CharacterAuthorityInitializationV1Rules.SpecSha(dnaSpec);
                var prpSpec = CharacterAuthorityInitializationV1Rules.CompilePrpSpec(
                    ctx.CharacterId, ctx.CharacterName, ctx.EraId, ctx.MasterSha ?? "", dnaSha);
                var prpJson = JsonSerializer.Serialize(prpSpec);
                var prpSha = CharacterAuthorityInitializationV1Rules.SpecSha(prpSpec);
                updated = updated with
                {
                    DnaStatus = CharacterAuthorityInitializationV1Rules.Locked,
                    DnaSha = dnaSha,
                    DnaSpec = dnaJson,
                    DnaMasterSha = ctx.MasterSha,
                    PrpStatus = CharacterAuthorityInitializationV1Rules.Locked,
                    PrpSha = prpSha,
                    PrpSpec = prpJson,
                    PrpMasterSha = ctx.MasterSha,
                    PrpDnaSha = dnaSha,
                };
            }
            return updated;
        }, ct);

        if (stage == CharacterAuthorityInitializationV1Rules.StageMaster && ctx.MasterCandidateId is Guid cid)
        {
            if (next == CharacterAuthorityInitializationV1Rules.Locked
                || next == CharacterAuthorityInitializationV1Rules.Approved
                || next == CharacterAuthorityInitializationV1Rules.Rejected)
                await _candidates.SetCandidateStatusAsync(cid, next, ct);
        }

        if (ctx.Asset is not null)
        {
            await _candidates.InsertEventAsync(ctx.Asset.AssetId, ctx.Asset.VersionId, ctx.MasterCandidateId,
                $"AUTHORITY_{stage}_{next}", actor, ctx.MasterPath ?? "", ctx.MasterSha ?? "",
                JsonSerializer.Serialize(new
                {
                    document = CharacterAuthorityInitializationV1Rules.DocumentId,
                    auto_approve = false,
                    auto_lock = false,
                }), ct);
        }

        var fresh = await LoadAsync(characterId, request, stage, CharacterAuthorityInitializationV1Rules.ActionGet, ct);
        return ToDto(fresh, gate, false, false);
    }

    private sealed record Ctx(
        string CharacterId,
        string CharacterName,
        string Role,
        string EraId,
        string IdentityVersion,
        string? Gender,
        string? Age,
        string CanonicalText,
        bool IdentityReady,
        bool AuthorityLocked,
        string? Provider,
        string MasterStatus,
        string? MasterSha,
        string? MasterPath,
        Guid? MasterCandidateId,
        string DnaStatus,
        string? DnaSha,
        string PrpStatus,
        string? PrpSha,
        string? Fingerprint,
        string CandidateCode,
        KitVideoMasterReferenceRepository.AssetRow? Asset,
        CharacterAuthorityStore.Snapshot? Store,
        CharacterAuthorityInitializationV1Rules.GateInput Input);

    private async Task<Ctx> LoadAsync(
        string characterId, CharacterAuthorityInitializationRequestDto request, string stage, string action,
        CancellationToken ct)
    {
        var id = (characterId ?? "").Trim().ToUpperInvariant();
        var era = string.IsNullOrWhiteSpace(request.EraId) ? "ERA-01" : request.EraId.Trim().ToUpperInvariant();
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? null : request.Provider.Trim().ToUpperInvariant();

        FamixaCharacterDto? person = null;
        if (id.Length > 0)
        {
            try { person = await _characters.GetAsync(id, ct); }
            catch (InvalidOperationException) { person = null; }
        }

        var identityReady = person is not null
            && CharacterAuthorityInitializationV1Rules.IdentityReady(person.Name, person.Role, person.Visual, person.Lifecycle);
        var identityVersion = person?.Version ?? person?.CurrentVersionId?.ToString() ?? "";
        var gender = person is null ? null : CanonText(person.Canon, "identity", "gender");
        var age = person is null ? null : CanonText(person.Canon, "identity", "currentAge");
        var bio = person is null ? null : CanonText(person.Canon, "identity", "biography");

        string masterStatus = CharacterAuthorityInitializationV1Rules.Missing;
        string dnaStatus = CharacterAuthorityInitializationV1Rules.Missing;
        string prpStatus = CharacterAuthorityInitializationV1Rules.Missing;
        string? masterSha = null;
        string? masterPath = null;
        Guid? masterCandidateId = null;
        string? dnaSha = null;
        string? prpSha = null;
        string? existingFp = null;
        CharacterAuthorityStore.Snapshot? store = null;
        KitVideoMasterReferenceRepository.AssetRow? asset = null;

        KitVideoMasterLockRepository.MasterRow? officialMaster = null;
        KitVideoCharacterDnaRepository.DnaRow? officialDna = null;
        KitVideoProductionReferencePackRepository.PackRow? officialPrp = null;
        if (id.Length > 0)
        {
            officialMaster = await _locks.GetByVersionAsync(id, era, "V1", ct);
            officialDna = await _dna.GetByVersionAsync(id, era, "V1", ct);
            officialPrp = await _prp.GetByVersionAsync(id, era, "V1", ct);
            asset = await _store.GetAssetAsync(id, ct);
            store = await _store.ReadAsync(id, era, ct);
        }

        var officialLocked = officialMaster is not null && CharacterReferencePackRules.MasterLocked(officialMaster.Status)
            && officialDna is not null && CharacterReferencePackRules.DnaLocked(officialDna.Status)
            && officialPrp is not null && string.Equals(officialPrp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase);

        if (officialMaster is not null && CharacterReferencePackRules.MasterLocked(officialMaster.Status))
        {
            masterStatus = CharacterAuthorityInitializationV1Rules.Locked;
            masterSha = officialMaster.Sha256;
            masterPath = officialMaster.ArtifactPath;
        }
        else if (store is not null)
        {
            masterStatus = store.MasterStatus;
            masterSha = store.MasterSha;
            masterPath = store.MasterPath;
            masterCandidateId = store.MasterCandidateId;
            existingFp = store.MasterFingerprint;
        }

        if (officialDna is not null && CharacterReferencePackRules.DnaLocked(officialDna.Status))
        {
            dnaStatus = CharacterAuthorityInitializationV1Rules.Locked;
            dnaSha = KitVideoIntegrityRules.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(officialDna.SpecJson ?? "{}"));
        }
        else if (store is not null)
        {
            dnaStatus = store.DnaStatus;
            dnaSha = store.DnaSha;
        }

        if (officialPrp is not null && string.Equals(officialPrp.Status, "LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            prpStatus = CharacterAuthorityInitializationV1Rules.Locked;
            prpSha = officialPrp.PrpSha256;
            if (string.IsNullOrWhiteSpace(prpSha))
                prpSha = KitVideoIntegrityRules.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(officialPrp.SpecJson ?? "{}"));
        }
        else if (store is not null)
        {
            prpStatus = store.PrpStatus;
            prpSha = store.PrpSha;
        }

        var authorityLocked = officialLocked
            || CharacterAuthorityInitializationV1Rules.IsAuthorityLocked(masterStatus, dnaStatus, prpStatus);

        var fingerprint = identityReady
            ? CharacterAuthorityInitializationV1Rules.ExecutionFingerprint(id, era, identityVersion, "MASTER")
            : null;
        var dup = fingerprint is not null && asset is not null
            && await _candidates.CountSuccessfulFingerprintAsync(asset.VersionId, fingerprint, ct) > 0
            && CharacterAuthorityInitializationV1Rules.DuplicatePolicy(fingerprint, existingFp ?? fingerprint)
               == "BLOCK_DUPLICATE"
            && string.Equals(action, CharacterAuthorityInitializationV1Rules.ActionExecute, StringComparison.OrdinalIgnoreCase)
            && string.Equals(stage, CharacterAuthorityInitializationV1Rules.StageMaster, StringComparison.OrdinalIgnoreCase)
            && (CharacterAuthorityInitializationV1Rules.IsReadyForDirector(masterStatus)
                || CharacterAuthorityInitializationV1Rules.IsApproved(masterStatus)
                || CharacterAuthorityInitializationV1Rules.IsLocked(masterStatus));

        var capabilityReady = provider is null || CharacterAuthorityInitializationV1Rules.CapabilityAllows(provider);
        var historical = CharacterAuthorityInitializationV1Rules.RejectHistoricalStill(
            masterCandidateId?.ToString(), masterPath);
        var input = new CharacterAuthorityInitializationV1Rules.GateInput(
            person is not null,
            identityReady,
            authorityLocked,
            stage,
            action,
            masterStatus,
            dnaStatus,
            prpStatus,
            provider,
            capabilityReady,
            capabilityReady ? null : "PROVIDER_CAPABILITY_UNSUPPORTED",
            request.Confirm,
            dup,
            historical && string.Equals(action, CharacterAuthorityInitializationV1Rules.ActionExecute, StringComparison.OrdinalIgnoreCase));

        return new Ctx(
            id,
            person?.Name ?? store?.CharacterName ?? "",
            person?.Role ?? store?.Role ?? "",
            era,
            identityVersion,
            gender,
            age,
            CharacterAuthorityInitializationV1Rules.CanonicalMasterBrief(
                id, person?.Name ?? id, person?.Role ?? "", era, gender, age, bio),
            identityReady,
            authorityLocked,
            provider,
            masterStatus,
            masterSha,
            masterPath,
            masterCandidateId,
            dnaStatus,
            dnaSha,
            prpStatus,
            prpSha,
            fingerprint,
            $"{id}-{era}-AUTH-MASTER-001",
            asset,
            store,
            input);
    }

    private async Task WriteStoreAsync(Ctx ctx, Func<CharacterAuthorityStore.Snapshot, CharacterAuthorityStore.Snapshot> patch, CancellationToken ct)
    {
        if (ctx.AuthorityLocked)
            throw new InvalidOperationException("AUTHORITY_CONFLICT: nhân vật đã khóa. Không ghi đè.");
        if (ctx.Asset is null)
            throw new InvalidOperationException("CHARACTER_IDENTITY_NOT_READY: chưa có video asset.");
        var baseSnap = ctx.Store ?? new CharacterAuthorityStore.Snapshot(
            ctx.Asset.AssetId, ctx.Asset.VersionId, ctx.CharacterId, ctx.EraId,
            ctx.CharacterName, ctx.Role, ctx.IdentityVersion,
            ctx.MasterStatus, ctx.MasterSha, ctx.MasterPath, ctx.MasterCandidateId, ctx.Fingerprint,
            ctx.Provider, null,
            ctx.DnaStatus, ctx.DnaSha, null, ctx.MasterSha,
            ctx.PrpStatus, ctx.PrpSha, null, ctx.MasterSha, ctx.DnaSha);
        var next = patch(baseSnap with
        {
            AssetId = ctx.Asset.AssetId,
            VersionId = ctx.Asset.VersionId,
            CharacterId = ctx.CharacterId,
            EraId = ctx.EraId,
            CharacterName = ctx.CharacterName,
            Role = ctx.Role,
            IdentityVersion = ctx.IdentityVersion,
        });
        await _store.WriteAsync(next, ct);
    }

    private CharacterAuthorityInitializationDto ToDto(
        Ctx ctx, CharacterAuthorityInitializationV1Rules.Gate gate, bool generate, bool called, bool gemini = false)
    {
        var authority = CharacterAuthorityInitializationV1Rules.AuthorityReady(ctx.MasterStatus, ctx.DnaStatus, ctx.PrpStatus);
        var master = Stage(
            CharacterAuthorityInitializationV1Rules.StageMaster, ctx.MasterStatus, ctx.MasterSha, ctx.MasterCandidateId,
            ctx, gate);
        var dna = Stage(CharacterAuthorityInitializationV1Rules.StageDna, ctx.DnaStatus, ctx.DnaSha, null, ctx, gate);
        var prp = Stage(CharacterAuthorityInitializationV1Rules.StagePrp, ctx.PrpStatus, ctx.PrpSha, null, ctx, gate);
        var next = authority
            ? CharacterAuthorityInitializationV1Rules.StaffReady
            : CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus)
                ? (CharacterAuthorityInitializationV1Rules.IsLocked(ctx.DnaStatus)
                    ? CharacterAuthorityInitializationV1Rules.StaffNeedPrp
                    : CharacterAuthorityInitializationV1Rules.StaffNeedDna)
                : CharacterAuthorityInitializationV1Rules.StaffNeedMaster;
        return new CharacterAuthorityInitializationDto(
            CharacterAuthorityInitializationV1Rules.DocumentId,
            ctx.CharacterId,
            ctx.CharacterName,
            ctx.Role,
            ctx.EraId,
            ctx.IdentityVersion,
            ctx.IdentityReady,
            ctx.AuthorityLocked,
            gate.Status,
            gate.Code,
            gate.StaffMessage,
            next,
            authority,
            gate.ProviderSelected,
            gate.CapabilityReady,
            gate.GenerationAllowed,
            gate.MayCallProvider,
            gate.Code == "CONFIRMATION_REQUIRED",
            generate,
            called,
            called && gemini,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            ctx.Provider,
            ctx.Provider is null ? null : CharacterAuthorityInitializationV1Rules.SetCapability(ctx.Provider),
            master,
            dna,
            prp,
            ctx.Fingerprint,
            ctx.MasterCandidateId is null ? null : $"/api/content/video-engine/characters/{ctx.CharacterId}/authority-initialization/master/image",
            new
            {
                masterSha = ctx.MasterSha,
                dnaSha = ctx.DnaSha,
                prpSha = ctx.PrpSha,
                fingerprint = ctx.Fingerprint,
                authorityLocked = ctx.AuthorityLocked,
            });
    }

    private static CharacterAuthorityStageDto Stage(
        string stage, string status, string? sha, Guid? artifactId, Ctx ctx,
        CharacterAuthorityInitializationV1Rules.Gate gate)
    {
        var lockedPrev = stage switch
        {
            CharacterAuthorityInitializationV1Rules.StageDna => CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus),
            CharacterAuthorityInitializationV1Rules.StagePrp => CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus)
                && CharacterAuthorityInitializationV1Rules.IsLocked(ctx.DnaStatus),
            _ => true,
        };
        var canExecute = lockedPrev
            && (status == CharacterAuthorityInitializationV1Rules.Missing
                || status == CharacterAuthorityInitializationV1Rules.Rejected
                || status == CharacterAuthorityInitializationV1Rules.Failed)
            && !ctx.AuthorityLocked
            && ctx.IdentityReady;
        return new CharacterAuthorityStageDto(
            stage,
            status,
            CharacterAuthorityInitializationV1Rules.StaffMessage(stage, status),
            canExecute,
            canExecute && gate.GenerationAllowed,
            CharacterAuthorityInitializationV1Rules.IsReadyForDirector(status) && !ctx.AuthorityLocked,
            CharacterAuthorityInitializationV1Rules.IsReadyForDirector(status) && !ctx.AuthorityLocked,
            CharacterAuthorityInitializationV1Rules.IsApproved(status) && !ctx.AuthorityLocked,
            sha,
            artifactId);
    }

    private static string? CanonText(JsonElement canon, string parent, string child)
    {
        if (canon.ValueKind != JsonValueKind.Object || !canon.TryGetProperty(parent, out var p)
            || p.ValueKind != JsonValueKind.Object || !p.TryGetProperty(child, out var c))
            return null;
        return c.ValueKind switch
        {
            JsonValueKind.String => c.GetString(),
            JsonValueKind.Number => c.GetRawText(),
            _ => null,
        };
    }
}
