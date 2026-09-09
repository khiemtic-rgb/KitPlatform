using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterStudioOrchestrator : ICharacterStudioOrchestrator
{
    private readonly IFamixaCharacterService _characters;
    private readonly ICharacterAuthorityInitializationV1Service _init;
    private readonly ICharacterAuthorityPipelineV1Service _pipeline;
    private readonly ICharacterReferenceRegenerationV1Service _regen;
    private readonly ICharacterReferencePackService _officialCrp;
    private readonly CharacterAuthorityStore _store;
    private readonly KitVideoMasterReferenceRepository _assets;
    private readonly KitVideoArtifactStore _artifacts;
    private readonly ICharacterGenerationProvider _provider;
    private readonly ICharacterReferenceSelector _selector;
    private readonly IProjectVisualStyleAuthority _visualStyle;
    private readonly IVisualUniverseAuthority _visualUniverse;
    private readonly ICharacterAgeConsistencyEvaluator _ageEvaluator;
    private readonly ICharacterStudioIdentityJudge _identityJudge;
    private readonly IVisualUniverseSnapshotResolver _visualUniverseSnapshot;
    private readonly IUnifiedVisualCompiler _unifiedVisualCompiler;

    public CharacterStudioOrchestrator(
        IFamixaCharacterService characters,
        ICharacterAuthorityInitializationV1Service init,
        ICharacterAuthorityPipelineV1Service pipeline,
        ICharacterReferenceRegenerationV1Service regen,
        ICharacterReferencePackService officialCrp,
        CharacterAuthorityStore store,
        KitVideoMasterReferenceRepository assets,
        KitVideoArtifactStore artifacts,
        ICharacterGenerationProvider provider,
        ICharacterReferenceSelector selector,
        IProjectVisualStyleAuthority visualStyle,
        IVisualUniverseAuthority visualUniverse,
        ICharacterAgeConsistencyEvaluator ageEvaluator,
        ICharacterStudioIdentityJudge identityJudge,
        IVisualUniverseSnapshotResolver visualUniverseSnapshot,
        IUnifiedVisualCompiler unifiedVisualCompiler)
    {
        _characters = characters;
        _init = init;
        _pipeline = pipeline;
        _regen = regen;
        _officialCrp = officialCrp;
        _store = store;
        _assets = assets;
        _artifacts = artifacts;
        _provider = provider;
        _selector = selector;
        _visualStyle = visualStyle;
        _visualUniverse = visualUniverse;
        _ageEvaluator = ageEvaluator;
        _identityJudge = identityJudge;
        _visualUniverseSnapshot = visualUniverseSnapshot;
        _unifiedVisualCompiler = unifiedVisualCompiler;
    }

    public IReadOnlyList<string> RunRegression() => CharacterStudioV1Regression.Run();

    public IReadOnlyList<string> RunIdentityLockRegression() =>
        CharacterStudioIdentityLockV1Regression.Run();

    public IReadOnlyList<string> RunAgeConsistencyRegression() =>
        CharacterAgeConsistencyV1Regression.Run();

    public IReadOnlyList<string> RunAgeGenerationIntegrationRegression() =>
        CharacterAgeGenerationIntegrationV1Regression.Run();

    public IReadOnlyList<string> RunAgeGateRegression() =>
        CharacterAgeGateV1Regression.Run();

    public IReadOnlyList<string> RunAppearanceProfileRegression() =>
        CharacterAppearanceProfileV1Regression.Run();

    public IReadOnlyList<string> RunCharacterDesignLanguageRegression() =>
        CharacterDesignLanguageV1Regression.Run();

    public IReadOnlyList<string> RunCharacterDesignLanguageV2Regression() =>
        CharacterDesignLanguageV2Regression.Run();

    public IReadOnlyList<string> RunVisualUniverseAuthorityRegression() =>
        FamixaVisualUniverseAuthorityV1Regression.Run();

    public IReadOnlyList<string> RunMasterRevisionRegression() =>
        CharacterMasterRevisionV1Regression.Run()
            .Concat(CharacterMasterRevisionDirectorReviewV1Regression.Run())
            .ToList();

    public IReadOnlyList<string> RunMasterRevisionDirectorReviewRegression() =>
        CharacterMasterRevisionDirectorReviewV1Regression.Run();

    public IReadOnlyList<string> RunFirstMasterVisualIngressRegression() =>
        CharacterFirstMasterVisualIngressV1Regression.Run();

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> ReviewGates = new();

    public async Task<CharacterMasterRevisionDto> RequestMasterRevisionAsync(
        string characterId, CharacterMasterRevisionRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var era = string.IsNullOrWhiteSpace(request.EraId) ? CharacterStudioV1Rules.DefaultEra : request.EraId.Trim().ToUpperInvariant();
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "" : request.Provider.Trim().ToUpperInvariant();
        var ctx = await LoadAsync(characterId, era, provider, cancellationToken);
        var source = ToRevisionSource(ctx, request);
        var code = CharacterMasterRevisionV1Rules.Evaluate(source);
        if (code is not null)
            return ToRevisionDto(ctx, source, code, false, false, null);
        var (snapshot, snapGate) = await TrySnapshotAsync(cancellationToken);
        if (snapGate is not null || snapshot is null)
            return ToRevisionDto(ctx, source, snapGate ?? CharacterFirstMasterVisualIngressV1Rules.GateSnapshot, false, false, null);
        var compiled = CharacterFirstMasterVisualIngressV1Rules.CompileRevision(
            snapshot, source, _unifiedVisualCompiler);
        if (compiled.Gate is not null || compiled.Contract is null)
            return ToRevisionDto(ctx, source, compiled.Gate ?? CharacterFirstMasterVisualIngressV1Rules.GateCompile, false, false, null);
        if (ctx.Asset is null)
            return ToRevisionDto(ctx, source, CharacterMasterRevisionV1Rules.GateMasterNotReady, false, false, null);

        var contract = CharacterMasterRevisionV1Rules.BuildGenerationRequest(source, compiled.Contract);
        var providerReq = CharacterMasterRevisionV1Rules.ToProviderRequest(contract, ctx.EraId, ctx.IdentityVersion);
        var generated = await _provider.GenerateMasterAsync(providerReq, cancellationToken);
        if (!generated.Succeeded || generated.Bytes is not { Length: > 32 })
        {
            await _store.WriteRevisionAsync(ctx.Asset.AssetId, RevisionFrom(ctx, source, contract,
                CharacterMasterRevisionV1Rules.StatusRequested, actor, null, null, null), cancellationToken);
            return ToRevisionDto(ctx, source, generated.ErrorCode ?? "MASTER_REVISION_FAILED",
                generated.ProviderCalled, false, generated.ProviderRequestId);
        }

        var version = CharacterMasterRevisionV1Rules.NextVersion(ctx.Revision?.CandidateMasterVersion ?? "V1");
        var persist = _artifacts.PersistMaster(ctx.CharacterId, ctx.EraId,
            $"{ctx.CharacterId}-{ctx.EraId}-STUDIO-MASTER-{version}", generated.Bytes);
        if (!persist.Check.Ok)
            return ToRevisionDto(ctx, source, "MASTER_REVISION_FAILED", generated.ProviderCalled, false, generated.ProviderRequestId);
        var candidateSha = KitVideoIntegrityRules.Sha256Hex(persist.Jpeg);
        var snap = RevisionFrom(ctx, source, contract, CharacterMasterRevisionV1Rules.StatusPendingReview,
            actor, persist.Path, candidateSha, version, generated.ProviderRequestId);
        await _store.WriteRevisionAsync(ctx.Asset.AssetId, snap, cancellationToken);
        ctx = await LoadAsync(characterId, era, provider, cancellationToken);
        return ToRevisionDto(ctx, source, null, generated.ProviderCalled, true, generated.ProviderRequestId);
    }

    public Task<CharacterMasterRevisionDto> ApproveMasterRevisionAsync(
        string characterId, string actor, CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default) =>
        AdvanceRevisionAsync(characterId, actor, review, CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove, cancellationToken);

    public Task<CharacterMasterRevisionDto> RejectMasterRevisionAsync(
        string characterId, string actor, CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default) =>
        AdvanceRevisionAsync(characterId, actor, review, CharacterMasterRevisionDirectorReviewV1Rules.ActionReject, cancellationToken);

    public Task<CharacterMasterRevisionDto> LockMasterRevisionAsync(
        string characterId, string actor, CharacterMasterRevisionReviewRequestDto? review = null,
        CancellationToken cancellationToken = default) =>
        AdvanceRevisionAsync(characterId, actor, review, CharacterMasterRevisionDirectorReviewV1Rules.ActionLock, cancellationToken);

    public async Task<CharacterAgeConsistencyDto> GetAgeConsistencyAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var row = await GetAsync(characterId, provider, eraId, cancellationToken);
        var target = CharacterAgeConsistencyV1Rules.FromCanonicalAge(
            row.Age ?? 0, row.AgeExpressionMinYears, row.AgeExpressionMaxYears);
        var profile = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(target, row.Gender);
        var gate = _ageEvaluator.Evaluate(new AgeConsistencyEvaluationRequest(
            row.CharacterId,
            row.Age ?? target.ChronologicalAge,
            row.AgeExpressionMinYears ?? target.TargetAppearanceAgeMin,
            row.AgeExpressionMaxYears ?? target.TargetAppearanceAgeMax,
            profile,
            row.Coverage,
            null,
            row.AgeProfile?.ConsistencyStatus ?? row.AgeConsistencyStatus,
            row.AgeConsistencyScore,
            row.AgeProfile?.EvaluatedAgeMin,
            row.AgeProfile?.EvaluatedAgeMax,
            row.AgeConsistencyReason,
            row.AgeProfile?.Evaluator,
            row.AgeProfile?.EvaluatedAt));
        return new CharacterAgeConsistencyDto(
            CharacterAgeGateV1Rules.DocumentId,
            row.CharacterId,
            target.CanonicalAge,
            row.AgeExpressionMinYears ?? target.ExpressionMinAge,
            row.AgeExpressionMaxYears ?? target.ExpressionMaxAge,
            row.AgeLifeStage ?? target.LifeStage,
            gate.Status,
            gate.Score ?? row.AgeConsistencyScore,
            gate.Reason ?? row.AgeConsistencyReason,
            row.AgeArtifacts ?? [],
            row.Age ?? target.ChronologicalAge,
            row.AgeExpressionMinYears ?? target.TargetAppearanceAgeMin,
            row.AgeExpressionMaxYears ?? target.TargetAppearanceAgeMax,
            profile,
            gate.Pass,
            gate.EvaluatedAgeMin,
            gate.EvaluatedAgeMax,
            gate.Evaluator,
            gate.EvaluatedAt,
            gate.Code);
    }

    public CharacterStudioStyleListDto Styles() =>
        new(CharacterStudioV1Rules.DocumentId, CharacterStudioV1Rules.StylePresets);

    public async Task<CharacterStudioListDto> ListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _characters.ListAsync(cancellationToken);
        var items = new List<CharacterStudioCharacterDto>();
        foreach (var row in rows.Where(r => !string.Equals(r.Visual, "voice", StringComparison.OrdinalIgnoreCase)))
            items.Add(await GetAsync(row.CharacterCode, null, "ERA-01", cancellationToken));
        return new CharacterStudioListDto(CharacterStudioV1Rules.DocumentId, items, items.Count);
    }

    public async Task<CharacterStudioCharacterDto> GetAsync(
        string characterId, string? provider = null, string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, eraId, provider, cancellationToken);
        return ToDto(ctx, CharacterStudioV1Rules.Evaluate(ctx.Input with { Action = "GET" }), false, false);
    }

    public async Task<CharacterStudioCharacterDto> CreateAsync(
        CharacterStudioCreateRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var name = (request.Name ?? "").Trim();
        var age = request.Age ?? 0;
        var gender = (request.Gender ?? "").Trim();
        var project = string.IsNullOrWhiteSpace(request.ProjectId) ? CharacterStudioV1Rules.ProjectFamixa : request.ProjectId!.Trim().ToUpperInvariant();
        var projectStyle = await _visualStyle.GetActiveAsync(project, cancellationToken);
        var styleId = projectStyle.Ready
            ? ProjectVisualStyleV1Rules.StudioKeyOf(projectStyle.StyleKey ?? "3D_STYLIZED_REALISM")
            : "";
        var description = (request.Description ?? "").Trim();
        if (!CharacterStudioV1Rules.ProfileValid(name, age, gender, styleId, description))
            throw new InvalidOperationException("PROFILE_INVALID: " + CharacterStudioV1Rules.StaffNeedProfile);
        if (request.Generate && !projectStyle.Ready)
            throw new InvalidOperationException(ProjectVisualStyleV1Rules.GateNotReady + ": " + ProjectVisualStyleV1Rules.StaffNotConfigured);

        var role = string.IsNullOrWhiteSpace(request.Role) ? "character" : request.Role.Trim();
        var created = await _characters.CreateDraftAsync(new CreateFamixaCharacterRequest(
            request.CharacterId,
            name,
            role,
            gender,
            age,
            "CORE",
            "frame",
            description,
            null,
            true,
            false), cancellationToken);

        var era = string.IsNullOrWhiteSpace(request.EraId) ? CharacterStudioV1Rules.DefaultEra : request.EraId!.Trim().ToUpperInvariant();
        await _assets.EnsureCharacterAssetAsync("FAMIXA", created.CharacterCode, created.Name, era, cancellationToken);
        await WriteStudioMetaAsync(
            created.CharacterCode, created.Name, era, styleId, description, age, gender,
            projectStyle, role, request.Personality, request.ExtraDescription, ct: cancellationToken);

        if (request.Generate && request.Confirm)
        {
            return await GenerateAsync(created.CharacterCode, new CharacterStudioActionRequestDto(
                true, request.Provider, era), actor, cancellationToken);
        }

        return await GetAsync(created.CharacterCode, request.Provider, era, cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> GenerateAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var era = Era(request.EraId);
        var ctx = await LoadAsync(characterId, era, request.Provider, cancellationToken);
        var gate = CharacterStudioV1Rules.Evaluate(ctx.Input with
        {
            Action = "GENERATE",
            Confirm = request.Confirm,
            Provider = ctx.Provider,
            CrpStale = CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(
                ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage),
            DuplicateFingerprint = ctx.Input.DuplicateFingerprint
                && !CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(
                    ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage),
        });
        if (gate.Code is not null)
            return ToDto(ctx, gate, false, false);
        var ageGate = AgeGenerationGate(ctx);
        if (ageGate is not null)
            return ToDto(ctx, ageGate, false, false);
        if (!CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus))
        {
            var (_, snapGate) = await TrySnapshotAsync(cancellationToken);
            if (snapGate is not null)
                return ToDto(ctx, IngressGate(snapGate), false, false);
        }
        return await RunPipelineAsync(ctx, request, actor, null, cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> RegenerateAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var era = Era(request.EraId);
        var ctx = await LoadAsync(characterId, era, request.Provider, cancellationToken);
        var gate = CharacterStudioV1Rules.Evaluate(ctx.Input with
        {
            Action = "REGENERATE",
            Confirm = request.Confirm,
            Provider = ctx.Provider,
            CrpRejected = ctx.CrpRejected
                || CharacterStudioV1Rules.IsPendingCrp(ctx.Input.StudioState)
                || CharacterStudioV1Rules.IsFailed(ctx.Input.StudioState),
            DuplicateFingerprint = false,
            CrpStale = CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(
                ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage),
        });
        if (gate.Code is not null)
            return ToDto(ctx, gate, false, false);
        var ageGate = AgeGenerationGate(ctx);
        if (ageGate is not null)
            return ToDto(ctx, ageGate, false, false);
        var slots = CharacterStudioV1Rules.RegenerationSlots(ctx.RejectReasonCode, request.RegenerateAll);
        return await RunPipelineAsync(ctx, request, actor, slots, cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> ScoreIdentityAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var era = Era(request.EraId);
        var ctx = await LoadAsync(characterId, era, request.Provider, cancellationToken);
        var scored = SlotScoresOf(ctx);
        if (!CharacterStudioIdentityLockV1Rules.MayScoreExisting(ctx.OfficialLocked, ctx.Coverage, scored))
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                ctx.Input.StudioState, CharacterStudioIdentityLockV1Rules.GateScoreNotReady,
                ctx.OfficialLocked
                    ? "Nhân vật đã khóa."
                    : ctx.Coverage < 4
                        ? "Chưa đủ 4 ảnh để chấm."
                        : "Bộ ảnh đã được chấm.",
                false, false, false, false, false, false, false), false, false);
        }
        if (!request.Confirm)
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                ctx.Input.StudioState, "CONFIRMATION_REQUIRED",
                CharacterStudioIdentityLockV1Rules.StaffScoreConfirm,
                false, false, false, false, false, false, false), false, false);
        }

        var masterPath = CharacterMasterRevisionV1Rules.LiveAuthorityPath(
            ctx.Revision?.Status, ctx.Revision?.CandidateMasterPath, ctx.MasterPath);
        var masterBytes = string.IsNullOrWhiteSpace(masterPath) ? null : _artifacts.Read(masterPath);
        if (masterBytes is not { Length: > 32 }
            && ctx.MasterPath is not null
            && !CharacterMasterRevisionV1Rules.LiveAuthorityRequiresCandidateBytes(ctx.Revision?.Status))
            masterBytes = _artifacts.Read(ctx.MasterPath);
        if (masterBytes is not { Length: > 32 })
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                CharacterStudioV1Rules.Failed,
                CharacterMasterRevisionV1Rules.GateAuthorityUnreadable,
                CharacterMasterRevisionV1Rules.StaffAuthorityUnreadable,
                false, false, false, false, false, false, false), false, false);
        }

        var produced = new Dictionary<string, (byte[] Bytes, string Path, string Sha)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ctx.Items)
        {
            var bytes = _artifacts.Read(item.Path);
            if (bytes is { Length: > 32 })
                produced[item.Type] = (bytes, item.Path, item.Sha256);
        }
        if (produced.Count < 4)
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                ctx.Input.StudioState, CharacterStudioIdentityLockV1Rules.GateScoreNotReady,
                "Thiếu file ảnh hiện tại. Không tạo ảnh mới.",
                false, false, false, false, false, false, false), false, false);
        }

        var vision = await ScoreAgainstMasterAsync(ctx, produced, masterBytes, cancellationToken);
        if (vision.Count == 0)
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                ctx.Input.StudioState, CharacterStudioIdentityLockV1Rules.GateScoreFailed,
                CharacterStudioIdentityLockV1Rules.StaffScoreFailed,
                false, false, false, false, false, false, false), false, false);
        }

        await PersistIdentityScoresAsync(ctx, produced, vision, cancellationToken);
        var fresh = await LoadAsync(characterId, era, request.Provider, cancellationToken);
        return ToDto(fresh, CharacterStudioV1Rules.Evaluate(fresh.Input with { Action = "GET" }), false, false);
    }

    public async Task<CharacterStudioCharacterDto> ApproveAsync(
        string characterId, string actor, string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, eraId, null, cancellationToken);
        if (!ctx.OfficialLocked
            && !CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(false, SlotScoresOf(ctx)))
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                "BLOCKED", CharacterStudioIdentityLockV1Rules.GateVisionNotReady,
                CharacterStudioIdentityLockV1Rules.StaffVisionPending,
                false, false, false, false, false, false, false), false, false);
        }
        await _pipeline.ApproveCrpAsync(characterId, actor, eraId, cancellationToken);
        return await GetAsync(characterId, null, eraId, cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> RejectAsync(
        string characterId, CharacterStudioActionRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        if (!CharacterStudioV1Rules.RejectReasonValid(request.RejectReasonCode, request.RejectReasonText))
            throw new InvalidOperationException("REJECT_REASON_REQUIRED: " + CharacterStudioV1Rules.StaffNeedReason);
        await _regen.RejectAsync(characterId, new CharacterReferenceRejectRequestDto(
            CharacterStudioV1Rules.MapLegacyReject(request.RejectReasonCode),
            request.RejectReasonText,
            request.EraId), actor, cancellationToken);
        return await GetAsync(characterId, request.Provider, Era(request.EraId), cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> LockAsync(
        string characterId, string actor, string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(characterId, null, eraId, cancellationToken);
        if (!CharacterAgeGateV1Rules.MayBecomeReady(
                current.OfficialLocked, current.AgeConsistencyStatus)
            || !CharacterAppearanceConsistencyV1Rules.MayBecomeReady(
                current.OfficialLocked, current.AppearanceConsistencyStatus))
            throw new InvalidOperationException(
                CharacterAgeConsistencyV1Rules.GateReadyBlocked + ": Age Consistency và Appearance Consistency phải PASS trước khi khóa nhân vật.");
        await _pipeline.LockCrpAsync(characterId, actor, eraId, cancellationToken);
        return await GetAsync(characterId, null, eraId, cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> ReviewAgeConsistencyAsync(
        string characterId,
        CharacterAgeConsistencyReviewRequestDto request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var era = CharacterStudioV1Rules.DefaultEra;
        var ctx = await LoadAsync(characterId, era, null, cancellationToken);
        var result = CharacterAgeConsistencyV1Rules.NormalizeReviewResult(request.Result);
        var gate = CharacterAgeConsistencyV1Rules.ReviewGate(result, ctx.Coverage, ctx.OfficialLocked);
        if (gate is not null)
            throw new InvalidOperationException(gate + ": Age Consistency chưa thể đánh giá.");
        if (result == CharacterAgeConsistencyV1Rules.StatusPass
            && !ctx.OfficialLocked
            && !CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(false, SlotScoresOf(ctx)))
            throw new InvalidOperationException(
                CharacterStudioIdentityLockV1Rules.GateVisionNotReady + ": "
                + CharacterStudioIdentityLockV1Rules.StaffVisionPending);
        if (result == CharacterAgeConsistencyV1Rules.StatusFail)
        {
            var note = string.IsNullOrWhiteSpace(request.Note)
                ? "Tuổi biểu hiện không phù hợp với tuổi nhân vật."
                : request.Note!.Trim();
            return await RejectAsync(characterId, new CharacterStudioActionRequestDto(
                false, ctx.Provider, era,
                request.ReasonCode ?? CharacterAgeConsistencyV1Rules.RejectAgeMismatch,
                note), actor, cancellationToken);
        }

        await PersistAgeReviewAsync(ctx, CharacterAgeConsistencyV1Rules.StatusPass, null, request.Note, actor, cancellationToken);
        return await GetAsync(characterId, ctx.Provider, era, cancellationToken);
    }

    public async Task<CharacterStudioCharacterDto> ReviewAppearanceConsistencyAsync(
        string characterId,
        CharacterAppearanceConsistencyReviewRequestDto request,
        string actor,
        CancellationToken cancellationToken = default)
    {
        var era = CharacterStudioV1Rules.DefaultEra;
        var ctx = await LoadAsync(characterId, era, null, cancellationToken);
        var result = CharacterAppearanceConsistencyV1Rules.NormalizeReviewResult(request.Result);
        var gate = CharacterAppearanceConsistencyV1Rules.ReviewGate(result, ctx.Coverage, ctx.OfficialLocked);
        if (gate is not null)
            throw new InvalidOperationException(gate + ": Appearance Consistency chưa thể đánh giá.");
        if (result == CharacterAppearanceConsistencyV1Rules.StatusPass
            && !ctx.OfficialLocked
            && !CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(false, SlotScoresOf(ctx)))
            throw new InvalidOperationException(
                CharacterStudioIdentityLockV1Rules.GateVisionNotReady + ": "
                + CharacterStudioIdentityLockV1Rules.StaffVisionPending);
        if (result == CharacterAppearanceConsistencyV1Rules.StatusFail)
        {
            var note = string.IsNullOrWhiteSpace(request.Note)
                ? "Ngoại hình không nhất quán với Appearance Profile."
                : request.Note!.Trim();
            await PersistAppearanceReviewAsync(
                ctx,
                CharacterAppearanceConsistencyV1Rules.StatusFail,
                request.ReasonCode ?? CharacterAppearanceConsistencyV1Rules.GateStereotype,
                note,
                actor,
                cancellationToken);
            return await GetAsync(characterId, ctx.Provider, era, cancellationToken);
        }

        await PersistAppearanceReviewAsync(
            ctx, CharacterAppearanceConsistencyV1Rules.StatusPass, null, request.Note, actor, cancellationToken);
        return await GetAsync(characterId, ctx.Provider, era, cancellationToken);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadMasterAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, eraId, null, cancellationToken);
        if (ctx.OfficialLocked)
            return await _init.ReadMasterImageAsync(characterId, eraId, cancellationToken);
        var path = CharacterMasterRevisionV1Rules.LiveAuthorityPath(
            ctx.Revision?.Status, ctx.Revision?.CandidateMasterPath, ctx.MasterPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            if (CharacterMasterRevisionV1Rules.LiveAuthorityRequiresCandidateBytes(ctx.Revision?.Status))
                return null;
            return await _init.ReadMasterImageAsync(characterId, eraId, cancellationToken);
        }
        var bytes = _artifacts.Read(path);
        return bytes is { Length: > 32 }
            ? (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg")
            : null;
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadCandidateMasterAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, eraId, null, cancellationToken);
        if (string.IsNullOrWhiteSpace(ctx.Revision?.CandidateMasterPath)) return null;
        var bytes = _artifacts.Read(ctx.Revision.CandidateMasterPath);
        return bytes is { Length: > 32 }
            ? (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg")
            : null;
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadPreviousMasterAsync(
        string characterId, string eraId = "ERA-01", CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, eraId, null, cancellationToken);
        var path = ctx.Revision?.CurrentMasterPath;
        if (string.IsNullOrWhiteSpace(path)
            || string.Equals(path, ctx.Revision?.CandidateMasterPath, StringComparison.OrdinalIgnoreCase))
            path = ctx.Store?.MasterPath ?? ctx.MasterPath;
        if (string.IsNullOrWhiteSpace(path)
            || string.Equals(path, ctx.Revision?.CandidateMasterPath, StringComparison.OrdinalIgnoreCase))
            return await _init.ReadMasterImageAsync(characterId, eraId, cancellationToken);
        var bytes = _artifacts.Read(path);
        return bytes is { Length: > 32 }
            ? (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg")
            : null;
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadReferenceAsync(
        string characterId, string type, string eraId = "ERA-01",
        CancellationToken cancellationToken = default)
    {
        var ctx = await LoadAsync(characterId, eraId, null, cancellationToken);
        var want = CharacterReferencePackRules.NormalizeRefType(type);
        var item = ctx.Items.FirstOrDefault(i =>
            string.Equals(i.Type, want, StringComparison.OrdinalIgnoreCase));
        if (item is null || string.IsNullOrWhiteSpace(item.Path)) return null;
        var bytes = _artifacts.Read(item.Path);
        if (bytes is not { Length: > 32 }) return null;
        return (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/png");
    }

    private async Task<CharacterStudioCharacterDto> RunPipelineAsync(
        Ctx ctx,
        CharacterStudioActionRequestDto request,
        string actor,
        IReadOnlyList<string>? onlySlots,
        CancellationToken ct)
    {
        if (ctx.OfficialLocked)
            return ToDto(ctx, CharacterStudioV1Rules.Evaluate(ctx.Input with { Action = "GENERATE", OfficialLocked = true }), false, false);
        if (ctx.Asset is null)
            throw new InvalidOperationException("CHARACTER_IDENTITY_NOT_READY: chưa có video asset.");

        var providerId = ctx.Provider ?? "GEMINI";
        var called = false;
        var masterPath = CharacterMasterRevisionV1Rules.LiveAuthorityPath(
            ctx.Revision?.Status, ctx.Revision?.CandidateMasterPath, ctx.MasterPath);
        byte[]? masterBytes = string.IsNullOrWhiteSpace(masterPath) ? null : _artifacts.Read(masterPath);
        if (CharacterMasterRevisionV1Rules.LiveAuthorityRequiresCandidateBytes(ctx.Revision?.Status)
            && masterBytes is not { Length: > 32 })
        {
            return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                CharacterStudioV1Rules.Failed,
                CharacterMasterRevisionV1Rules.GateAuthorityUnreadable,
                CharacterMasterRevisionV1Rules.StaffAuthorityUnreadable,
                false, false, false, false, false, false, false), true, false);
        }

        if (!CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus))
        {
            var (snapshot, snapGate) = await TrySnapshotAsync(ct);
            if (snapGate is not null || snapshot is null)
                return ToDto(ctx, IngressGate(snapGate ?? CharacterFirstMasterVisualIngressV1Rules.GateSnapshot), false, false);
            var compiled = CharacterFirstMasterVisualIngressV1Rules.CompileMaster(
                snapshot, ctx.CharacterId, ctx.AgeTarget, ctx.Appearance, ctx.Gender, ctx.Description,
                CharacterFirstMasterVisualIngressV1Rules.MasterView, _unifiedVisualCompiler);
            if (compiled.Gate is not null || compiled.Contract is null)
                return ToDto(ctx, IngressGate(compiled.Gate ?? CharacterFirstMasterVisualIngressV1Rules.GateCompile), false, false);
            var master = await _provider.GenerateMasterAsync(
                CharacterFirstMasterVisualIngressV1Rules.ToProviderRequest(
                    compiled.Contract, ctx.CharacterId, ctx.EraId,
                    CharacterFirstMasterVisualIngressV1Rules.GenerationTypeMaster, ctx.IdentityVersion),
                ct);
            called = master.ProviderCalled;
            if (!master.Succeeded || master.Bytes is not { Length: > 32 }
                || CharacterStudioV1Rules.RejectHistoricalStill(master.ProviderRequestId))
                return Fail(ctx, "MASTER_GENERATION_FAILED", called);

            var persist = _artifacts.PersistMaster(ctx.CharacterId, ctx.EraId,
                $"{ctx.CharacterId}-{ctx.EraId}-STUDIO-MASTER-001", master.Bytes);
            if (!persist.Check.Ok) return Fail(ctx, "MASTER_GENERATION_FAILED", called);
            masterBytes = persist.Jpeg;
            var masterSha = KitVideoIntegrityRules.Sha256Hex(persist.Jpeg);
            await WriteAuthorityAsync(ctx, snap => snap with
            {
                MasterStatus = CharacterAuthorityInitializationV1Rules.Locked,
                MasterSha = masterSha,
                MasterPath = persist.Path,
                Provider = providerId,
                ProviderRequestId = master.ProviderRequestId,
                MasterFingerprint = ctx.Fingerprint,
            }, ct);
            ctx = await LoadAsync(ctx.CharacterId, ctx.EraId, providerId, ct);
        }

        if (!CharacterAuthorityInitializationV1Rules.IsLocked(ctx.DnaStatus)
            && CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus))
        {
            var spec = CharacterAuthorityInitializationV1Rules.CompileDnaSpec(
                ctx.CharacterId, ctx.Name, ctx.Role, ctx.EraId, ctx.MasterSha ?? "", ctx.IdentityVersion,
                ctx.Gender, ctx.Age, ctx.ProjectVisualStyleId, ctx.ProjectVisualStyleSha);
            var dnaSha = CharacterAuthorityInitializationV1Rules.SpecSha(spec);
            await WriteAuthorityAsync(ctx, snap => snap with
            {
                DnaStatus = CharacterAuthorityInitializationV1Rules.Locked,
                DnaSha = dnaSha,
                DnaSpec = JsonSerializer.Serialize(spec),
                DnaMasterSha = ctx.MasterSha,
            }, ct);
            ctx = await LoadAsync(ctx.CharacterId, ctx.EraId, providerId, ct);
        }

        if (!CharacterAuthorityInitializationV1Rules.IsLocked(ctx.PrpStatus)
            && CharacterAuthorityInitializationV1Rules.IsLocked(ctx.DnaStatus))
        {
            var spec = CharacterAuthorityInitializationV1Rules.CompilePrpSpec(
                ctx.CharacterId, ctx.Name, ctx.EraId, ctx.MasterSha ?? "", ctx.DnaSha ?? "",
                ctx.ProjectVisualStyleId, ctx.ProjectVisualStyleSha,
                CharacterAgeConsistencyV1Rules.CompileAgeAppearanceProfile(ctx.AgeTarget, ctx.Gender));
            var prpSha = CharacterAuthorityInitializationV1Rules.SpecSha(spec);
            await WriteAuthorityAsync(ctx, snap => snap with
            {
                PrpStatus = CharacterAuthorityInitializationV1Rules.Locked,
                PrpSha = prpSha,
                PrpSpec = JsonSerializer.Serialize(spec),
                PrpMasterSha = ctx.MasterSha,
                PrpDnaSha = ctx.DnaSha,
            }, ct);
            ctx = await LoadAsync(ctx.CharacterId, ctx.EraId, providerId, ct);
        }

        if (masterBytes is not { Length: > 32 }
            && ctx.MasterPath is not null
            && !CharacterMasterRevisionV1Rules.LiveAuthorityRequiresCandidateBytes(ctx.Revision?.Status))
            masterBytes = _artifacts.Read(ctx.MasterPath);
        if (masterBytes is not { Length: > 32 })
        {
            if (CharacterMasterRevisionV1Rules.LiveAuthorityRequiresCandidateBytes(ctx.Revision?.Status))
                return ToDto(ctx, new CharacterStudioV1Rules.Gate(
                    CharacterStudioV1Rules.Failed,
                    CharacterMasterRevisionV1Rules.GateAuthorityUnreadable,
                    CharacterMasterRevisionV1Rules.StaffAuthorityUnreadable,
                    false, false, false, false, false, false, false), true, called);
            return Fail(ctx, "MASTER_GENERATION_FAILED", called);
        }

        if (CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage))
            onlySlots = CharacterStudioV1Rules.RequiredViews;

        var keep = new Dictionary<string, CharacterAuthorityStore.CrpItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ctx.Items)
            keep[item.Type] = item;

        var produced = new Dictionary<string, (byte[] Bytes, string Path, string Sha)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ctx.Items)
        {
            var bytes = _artifacts.Read(item.Path);
            if (bytes is { Length: > 32 })
                produced[item.Type] = (bytes, item.Path, item.Sha256);
        }

        var targets = (onlySlots is { Count: > 0 } ? onlySlots : CharacterStudioV1Rules.RequiredViews).ToList();
        var replaceExisting = onlySlots is { Count: > 0 };
        if (replaceExisting)
        {
            foreach (var view in targets)
            {
                keep.Remove(view);
                produced.Remove(view);
            }
        }
        var setId = Guid.NewGuid();
        var calls = 0;
        var missing = CharacterStudioIdentityLockV1Rules.GenerationOrder
            .Where(view => targets.Contains(view, StringComparer.OrdinalIgnoreCase) && !produced.ContainsKey(view))
            .ToList();
        var frontMissing = missing.Contains("FRONT", StringComparer.OrdinalIgnoreCase);
        var laterMissing = missing
            .Where(v => !string.Equals(v, "FRONT", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (frontMissing)
        {
            var frontFail = await GenerateViewBatchAsync(
                ctx, ["FRONT"], masterBytes, produced, keep, setId, ct);
            called = true;
            calls++;
            if (frontFail is not null) return Fail(ctx, frontFail, called);
        }
        if (laterMissing.Count > 0)
        {
            if (calls >= CharacterStudioV1Rules.MaxProviderCallsPerJob)
                return Fail(ctx, "REFERENCE_GENERATION_FAILED", called);
            var laterFail = await GenerateViewBatchAsync(
                ctx, laterMissing, masterBytes, produced, keep, setId, ct);
            called = true;
            calls++;
            if (laterFail is not null) return Fail(ctx, laterFail, called);
        }

        await PersistGeneratedCrpAsync(
            ctx, keep, produced, null, setId, providerId, replaceExisting, onlySlots,
            CancellationToken.None);

        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> vision =
            new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            vision = await ScoreAgainstMasterAsync(ctx, produced, masterBytes, ct);
        }
        catch (OperationCanceledException)
        {
            var canceled = await LoadAsync(ctx.CharacterId, ctx.EraId, providerId, CancellationToken.None);
            return ToDto(canceled, CharacterStudioV1Rules.Evaluate(canceled.Input with { Action = "GET" }), true, called,
                logicalCount: CharacterStudioUnifiedGenerationV1Rules.LogicalGenerationCount(calls),
                executionId: setId.ToString(),
                setId: setId.ToString());
        }
        var scores = Score(produced, ctx.MasterSha, vision);
        var failed = CharacterStudioV1Rules.FailedSlots(scores).ToList();
        var attempt = 0;
        while (failed.Count > 0 && CharacterStudioV1Rules.MayRepairSlot("CONTENT_INCONSISTENCY", attempt))
        {
            attempt++;
            foreach (var view in failed.ToList())
            {
                if (calls >= CharacterStudioV1Rules.MaxProviderCallsPerJob) break;
                var repairFail = await GenerateViewBatchAsync(
                    ctx, [view], masterBytes, produced, keep, Guid.NewGuid(), ct);
                called = true;
                calls++;
                if (repairFail is not null) continue;
            }
            vision = await ScoreAgainstMasterAsync(ctx, produced, masterBytes, ct);
            scores = Score(produced, ctx.MasterSha, vision);
            failed = CharacterStudioV1Rules.FailedSlots(scores).ToList();
        }

        var consistency = CharacterStudioUnifiedGenerationV1Rules.EvaluateConsistency(scores);
        await PersistGeneratedCrpAsync(
            ctx, keep, produced, vision, setId, providerId, replaceExisting, onlySlots,
            CancellationToken.None);
        var fresh = await LoadAsync(ctx.CharacterId, ctx.EraId, providerId, CancellationToken.None);
        return ToDto(fresh, CharacterStudioV1Rules.Evaluate(fresh.Input with { Action = "GET" }), true, called,
            logicalCount: CharacterStudioUnifiedGenerationV1Rules.LogicalGenerationCount(calls),
            consistencyPass: consistency.Pass,
            executionId: setId.ToString(),
            setId: setId.ToString());
    }

    private async Task PersistIdentityScoresAsync(
        Ctx ctx,
        Dictionary<string, (byte[] Bytes, string Path, string Sha)> produced,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> vision,
        CancellationToken ct)
    {
        if (ctx.Asset is null) return;
        var scores = Score(produced, ctx.MasterSha, vision);
        var items = ctx.Items.ToList();
        if (items.Count == 0) return;
        var consistency = CharacterStudioUnifiedGenerationV1Rules.EvaluateConsistency(scores);
        var anyFail = scores.Any(s => CharacterStudioIdentityLockV1Rules.IsFail(s.Verdict));
        var persistStatus = anyFail
            ? CharacterStudioV1Rules.Rejected
            : CharacterReferenceRegenerationV1Rules.PendingReview;
        await _store.WriteCrpAsync(ctx.Asset.AssetId, new CharacterAuthorityStore.CrpSnapshot(
            ctx.CharacterId, ctx.EraId, persistStatus, items.Count, false,
            ctx.Crp?.Sha256 ?? KitVideoIntegrityRules.Sha256Hex(
                System.Text.Encoding.UTF8.GetBytes(string.Join("|", items.Select(i => i.Sha256).OrderBy(x => x)))),
            items, ctx.Crp?.Version ?? "V1", ctx.Crp?.SetId ?? Guid.NewGuid().ToString(),
            ctx.Provider ?? "GEMINI", ctx.Fingerprint ?? ctx.Crp?.Fingerprint,
            ctx.MasterSha, ctx.DnaSha, ctx.PrpSha,
            anyFail ? "CONSISTENCY_FAILED" : ctx.RejectReasonCode,
            anyFail ? string.Join(", ", consistency.Failed) : ctx.RejectReasonText,
            ctx.Crp?.RejectedBy, ctx.Crp?.RejectedAt, ctx.Crp?.History ?? [], vision), ct);
    }

    private async Task PersistGeneratedCrpAsync(
        Ctx ctx,
        Dictionary<string, CharacterAuthorityStore.CrpItem> keep,
        Dictionary<string, (byte[] Bytes, string Path, string Sha)> produced,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? vision,
        Guid setId,
        string providerId,
        bool replaceExisting,
        IReadOnlyList<string>? onlySlots,
        CancellationToken ct)
    {
        var scores = Score(produced, ctx.MasterSha, vision);
        var items = CharacterStudioV1Rules.RequiredViews
            .Select(t => keep.TryGetValue(t, out var item) ? item : null)
            .Where(i => i is not null)
            .Cast<CharacterAuthorityStore.CrpItem>()
            .ToList();
        if (items.Count == 0) return;
        var coverage = items.Count;
        var history = (ctx.Crp?.History ?? []).ToList();
        var redoSet = replaceExisting
            || ctx.CrpRejected
            || CharacterStudioV1Rules.IsPendingCrp(ctx.Input.StudioState)
            || CharacterStudioV1Rules.IsFailed(ctx.Input.StudioState)
            || CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage);
        if (ctx.Crp is not null && onlySlots is { Count: 4 } or null && redoSet)
        {
            history.Add(new CharacterAuthorityStore.CrpHistoryEntry(
                ctx.Crp.Version, ctx.Crp.Status, ctx.Crp.Sha256, ctx.Crp.Items,
                ctx.Crp.RejectReasonCode, ctx.Crp.RejectReasonText, ctx.Crp.RejectedBy, ctx.Crp.RejectedAt,
                ctx.Crp.Provider, ctx.Crp.SetId, ctx.Crp.Fingerprint));
        }

        var version = onlySlots is { Count: > 0 and < 4 }
            ? (ctx.Crp?.Version ?? "V1")
            : redoSet ? CharacterStudioV1Rules.NextVersion(ctx.Crp?.Version) : (ctx.Crp?.Version ?? "V1");
        var crpSha = KitVideoIntegrityRules.Sha256Hex(
            System.Text.Encoding.UTF8.GetBytes(string.Join("|", items.Select(i => i.Sha256).OrderBy(x => x))));
        var identitySha = ctx.IdentitySha ?? CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            ctx.CharacterId, ctx.Name,
            int.TryParse(ctx.Age, out var ageNum) ? ageNum : 0,
            ctx.Gender ?? "", ctx.Role, ctx.Personality, ctx.Description ?? "",
            ctx.ProjectVisualStyleId, ctx.ProjectVisualStyleSha);
        var fingerprint = CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
            CharacterStudioV1Rules.ProjectFamixa, ctx.CharacterId, ctx.ProjectVisualStyleSha,
            identitySha, ctx.MasterSha ?? "", ctx.DnaSha ?? "", ctx.PrpSha ?? "",
            null, CharacterAgeConsistencyV1Rules.AgeProfileSha(ctx.AgeTarget));
        var consistency = CharacterStudioUnifiedGenerationV1Rules.EvaluateConsistency(scores);
        var anyFail = scores.Any(s => CharacterStudioIdentityLockV1Rules.IsFail(s.Verdict));
        var persistStatus = anyFail
            ? CharacterStudioV1Rules.Rejected
            : CharacterReferenceRegenerationV1Rules.PendingReview;
        await _store.WriteCrpAsync(ctx.Asset!.AssetId, new CharacterAuthorityStore.CrpSnapshot(
            ctx.CharacterId, ctx.EraId,
            persistStatus,
            coverage, false, crpSha, items, version, setId.ToString(), providerId, fingerprint,
            ctx.MasterSha, ctx.DnaSha, ctx.PrpSha,
            anyFail ? "CONSISTENCY_FAILED" : null,
            anyFail ? string.Join(", ", consistency.Failed) : null,
            null, null, history, vision), ct);
        var projectStyle = await _visualStyle.GetActiveAsync(CharacterStudioV1Rules.ProjectFamixa, ct);
        await WriteStudioMetaAsync(
            ctx.CharacterId, ctx.Name, ctx.EraId, ctx.StyleId ?? "", ctx.Description ?? "",
            int.TryParse(ctx.Age, out var persistAge) ? persistAge : 0, ctx.Gender ?? "",
            projectStyle, ctx.Role, ctx.Personality, ctx.ExtraDescription, identitySha, setId.ToString(), ct);
    }

    private async Task<string?> GenerateViewBatchAsync(
        Ctx ctx,
        IReadOnlyList<string> views,
        byte[] masterBytes,
        Dictionary<string, (byte[] Bytes, string Path, string Sha)> produced,
        Dictionary<string, CharacterAuthorityStore.CrpItem> keep,
        Guid setId,
        CancellationToken ct)
    {
        var reqs = views.Select(view =>
        {
            var refs = BuildChainRefs(view, masterBytes, produced);
            var labels = CharacterStudioIdentityLockV1Rules.ViewRefs(view);
            return new CharacterReferenceViewRequest(
                view,
                CharacterAgeGenerationIntegrationV1Rules.ComposeViewPrompt(
                    StudioGenerationRequest(ctx), view, labels),
                view == "FULL_BODY" ? "3:4" : "1:1",
                refs);
        }).ToList();
        var result = await _provider.GenerateViewsAsync(StudioViewSet(ctx, reqs), ct);
        if (CharacterStudioV1Rules.RejectHistoricalStill(result.ProviderRequestId))
            return "HISTORICAL_STILL_REJECTED";
        foreach (var view in views)
        {
            var hit = result.Views.FirstOrDefault(v =>
                string.Equals(v.ReferenceType, view, StringComparison.OrdinalIgnoreCase));
            if (!result.Succeeded || hit is not { Succeeded: true, Bytes.Length: > 32 })
                return "REFERENCE_GENERATION_FAILED";
            var persist = _artifacts.PersistReferenceCandidate(
                ctx.CharacterId, ctx.EraId, ctx.Asset!.AssetId, view, setId, hit.Bytes!);
            var sha = KitVideoIntegrityRules.Sha256Hex(persist.Bytes);
            produced[view] = (persist.Bytes, persist.Path, sha);
            keep[view] = new CharacterAuthorityStore.CrpItem(view, persist.Path, sha);
        }
        return null;
    }

    private async Task<Dictionary<string, IReadOnlyDictionary<string, int>>> ScoreAgainstMasterAsync(
        Ctx ctx,
        Dictionary<string, (byte[] Bytes, string Path, string Sha)> produced,
        byte[] masterBytes,
        CancellationToken ct)
    {
        var vision = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        if (masterBytes is not { Length: > 32 }) return vision;
        foreach (var view in CharacterStudioV1Rules.RequiredViews)
        {
            if (!produced.TryGetValue(view, out var row) || row.Bytes is not { Length: > 32 })
                continue;
            var scored = await _identityJudge.ScoreViewAsync(
                view, row.Bytes, masterBytes,
                ctx.AgeTarget.TargetAppearanceAgeMin, ctx.AgeTarget.TargetAppearanceAgeMax, ct);
            if (scored is not null) vision[view] = scored;
        }
        return vision;
    }

    private static List<CharacterStudioV1Rules.SlotScore> Score(
        Dictionary<string, (byte[] Bytes, string Path, string Sha)> produced,
        string? masterSha,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? vision = null) =>
        CharacterStudioV1Rules.RequiredViews.Select(t =>
        {
            var present = produced.TryGetValue(t, out var row);
            var historical = present && CharacterStudioV1Rules.RejectHistoricalStill(null, row.Path);
            var crop = t == "FULL_BODY" && present
                && CharacterAuthorityInitializationV1Rules.SameSha(masterSha, row.Sha);
            IReadOnlyDictionary<string, int>? slot = null;
            vision?.TryGetValue(t, out slot);
            return CharacterStudioV1Rules.ScoreSlot(t, present, historical, crop, slot);
        }).ToList();

    private static List<ImageGenerationReferenceBytes> BuildChainRefs(
        string view,
        byte[] masterBytes,
        Dictionary<string, (byte[] Bytes, string Path, string Sha)> produced)
    {
        var refs = new List<ImageGenerationReferenceBytes>
        {
            new("MASTER_REFERENCE", "image/png", masterBytes, KitVideoIntegrityRules.Sha256Hex(masterBytes), "MASTER"),
        };
        foreach (var label in CharacterStudioV1Rules.ChainDependencies(view).Where(x => x != "MASTER"))
        {
            if (!produced.TryGetValue(label, out var row)) continue;
            refs.Add(new($"{label}_REFERENCE", "image/png", row.Bytes, row.Sha, label));
        }
        return refs;
    }

    private CharacterStudioCharacterDto Fail(Ctx ctx, string code, bool called) =>
        ToDto(ctx, new CharacterStudioV1Rules.Gate(
            CharacterStudioV1Rules.Failed, code, CharacterStudioV1Rules.StaffFail,
            false, false, false, false, false, false, false), true, called);

    private async Task<(VisualUniverseSnapshot? Snapshot, string? Gate)> TrySnapshotAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _visualUniverseSnapshot.GetCurrentAsync(
                CharacterStudioV1Rules.ProjectFamixa, cancellationToken);
            return (snapshot, CharacterFirstMasterVisualIngressV1Rules.ValidateSnapshot(snapshot));
        }
        catch
        {
            return (null, CharacterFirstMasterVisualIngressV1Rules.GateSnapshot);
        }
    }

    private static CharacterStudioV1Rules.Gate IngressGate(string code) =>
        new("BLOCKED", code, code switch
        {
            CharacterFirstMasterVisualIngressV1Rules.GateSnapshot =>
                "Visual Universe Snapshot chưa sẵn sàng. Master không được tạo ngoài Visual Universe.",
            CharacterFirstMasterVisualIngressV1Rules.GateVuaSha =>
                "Visual Universe SHA missing. Master generation is blocked.",
            CharacterFirstMasterVisualIngressV1Rules.GatePvsSha =>
                "Project Visual Style SHA missing. Master generation is blocked.",
            CharacterFirstMasterVisualIngressV1Rules.GateCdlSha =>
                "Character Design Language SHA missing. Master generation is blocked.",
            CharacterFirstMasterVisualIngressV1Rules.GateCompile =>
                "Unified Visual Compiler failed. Master generation is blocked.",
            _ => "Visual Universe ingress blocked Master generation.",
        }, false, false, false, false, false, false, false);

    private async Task WriteAuthorityAsync(
        Ctx ctx, Func<CharacterAuthorityStore.Snapshot, CharacterAuthorityStore.Snapshot> patch, CancellationToken ct)
    {
        if (ctx.OfficialLocked)
            throw new InvalidOperationException("AUTHORITY_LOCKED: " + CharacterStudioV1Rules.StaffLocked);
        var baseSnap = ctx.Store ?? new CharacterAuthorityStore.Snapshot(
            ctx.Asset!.AssetId, ctx.Asset.VersionId, ctx.CharacterId, ctx.EraId,
            ctx.Name, ctx.Role, ctx.IdentityVersion,
            ctx.MasterStatus, ctx.MasterSha, ctx.MasterPath, null, ctx.Fingerprint,
            ctx.Provider, null,
            ctx.DnaStatus, ctx.DnaSha, null, ctx.MasterSha,
            ctx.PrpStatus, ctx.PrpSha, null, ctx.MasterSha, ctx.DnaSha);
        await _store.WriteAsync(patch(baseSnap), ct);
    }

    private async Task WriteStudioMetaAsync(
        string characterId, string name, string era, string styleId, string description, int age, string gender,
        ProjectVisualStyleDto projectStyle,
        string? role = null, string? personality = null, string? extraDescription = null,
        string? identitySha = null, string? generationSetId = null,
        CancellationToken ct = default)
    {
        var asset = await _store.GetAssetAsync(characterId, ct)
            ?? await _assets.EnsureCharacterAssetAsync("FAMIXA", characterId, name, era, ct);
        var inherit = projectStyle.Ready
            ? ProjectVisualStyleV1Rules.InheritLockedByProject
            : ProjectVisualStyleV1Rules.NotConfigured;
        var identity = CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
            characterId, name, age, gender, role, personality, description,
            projectStyle.Id?.ToString(), projectStyle.Sha);
        var doc = new JsonObject
        {
            ["characterStudio"] = new JsonObject
            {
                ["document"] = CharacterStudioV1Rules.DocumentId,
                ["pipeline"] = CharacterStudioUnifiedGenerationV1Rules.DocumentId,
                ["styleId"] = styleId,
                ["description"] = description,
                ["extraDescription"] = extraDescription ?? "",
                ["age"] = age,
                ["gender"] = gender,
                ["role"] = role ?? "",
                ["personality"] = personality ?? "",
                ["era"] = era,
                ["styleSource"] = "PROJECT",
                ["identitySha256"] = identitySha ?? identity,
                ["generationSetId"] = generationSetId,
                ["ageExpression"] = JsonSerializer.SerializeToNode(
                    CharacterAgeConsistencyV1Rules.CompileAgeExpression(
                        CharacterAgeConsistencyV1Rules.FromCanonicalAge(age))),
                ["ageConsistency"] = new JsonObject
                {
                    ["status"] = CharacterAgeConsistencyV1Rules.StatusNotEvaluated,
                    ["document"] = CharacterAgeConsistencyV1Rules.DocumentId,
                },
            },
            ["projectVisualStyle"] = new JsonObject
            {
                ["id"] = projectStyle.Id?.ToString(),
                ["sha"] = projectStyle.Sha,
                ["styleKey"] = projectStyle.StyleKey,
                ["inheritStatus"] = inherit,
                ["authority"] = ProjectVisualStyleV1Rules.StaffAuthority,
            },
        };
        await _assets.MergeAssetExtraAsync(asset.AssetId, doc.ToJsonString(), ct);
    }

    private sealed record Ctx(
        string CharacterId,
        string Name,
        string Role,
        string EraId,
        string IdentityVersion,
        string? Gender,
        string? Age,
        string? StyleId,
        string? Description,
        string IdentityBrief,
        string? Provider,
        bool OfficialLocked,
        bool AuthorityLocked,
        string MasterStatus,
        string? MasterSha,
        string? MasterPath,
        string DnaStatus,
        string? DnaSha,
        string PrpStatus,
        string? PrpSha,
        string? CrpStatus,
        int Coverage,
        bool CrpRejected,
        string? RejectReasonCode,
        string? RejectReasonText,
        string? Fingerprint,
        string? CrpSha,
        IReadOnlyList<CharacterAuthorityStore.CrpItem> Items,
        CharacterAuthorityStore.Snapshot? Store,
        CharacterAuthorityStore.CrpSnapshot? Crp,
        KitVideoMasterReferenceRepository.AssetRow? Asset,
        CharacterStudioV1Rules.GateInput Input,
        IReadOnlyList<CharacterReferenceHistoryItemDto> History,
        string? ProjectVisualStyleId,
        string? ProjectVisualStyleSha,
        string? ProjectVisualStyleKey,
        string? ProjectVisualStyleName,
        string VisualStyleInherit,
        string VisualStyleGate,
        string? Personality,
        string? ExtraDescription,
        string? IdentitySha,
        AgeExpressionTarget AgeTarget,
        AgeSetResult AgeSet,
        CharacterAppearanceProfile Appearance,
        string? AppearanceStoredStatus,
        CharacterAuthorityStore.RevisionSnapshot? Revision,
        string? InitMasterSha = null);

    private async Task<Ctx> LoadAsync(string characterId, string era, string? provider, CancellationToken ct)
    {
        var id = CharacterStudioV1Rules.NormalizeCharacterId(characterId);
        FamixaCharacterDto? person = null;
        try { person = await _characters.GetAsync(id, ct); }
        catch (InvalidOperationException) { person = null; }

        var init = person is null
            ? null
            : await _init.GetAsync(id, provider, era, ct);
        CharacterReferenceRegenerationDto? regen = null;
        try { regen = await _regen.GetAsync(id, provider, era, ct); }
        catch (InvalidOperationException) { regen = null; }

        CharacterReferencePackGetDto? official = null;
        try { official = await _officialCrp.GetAsync(id, era, ct); }
        catch (InvalidOperationException) { official = null; }

        var asset = await _store.GetAssetAsync(id, ct);
        var store = await _store.ReadAsync(id, era, ct);
        var crp = await _store.ReadCrpAsync(id, era, ct);
        var extra = Parse(asset?.ExtraJson);
        var studio = extra["characterStudio"] as JsonObject;
        var projectStyle = await _visualStyle.GetActiveAsync(CharacterStudioV1Rules.ProjectFamixa, ct);
        var bind = await _visualStyle.ResolveForCharacterAsync(CharacterStudioV1Rules.ProjectFamixa, id, ct);
        var styleId = projectStyle.Ready
            ? ProjectVisualStyleV1Rules.StudioKeyOf(projectStyle.StyleKey ?? "3D_STYLIZED_REALISM")
            : Text(studio, "styleId") ?? "";
        var description = Text(studio, "description")
            ?? CanonText(person?.Canon, "identity", "biography")
            ?? "";
        var extraDescription = Text(studio, "extraDescription");
        var personality = Text(studio, "personality")
            ?? CanonText(person?.Canon, "personality", "summary");
        var gender = Text(studio, "gender")
            ?? CanonText(person?.Canon, "identity", "gender");
        var age = Text(studio, "age")
            ?? CanonText(person?.Canon, "identity", "currentAge");
        var role = Text(studio, "role")
            ?? person?.Role
            ?? store?.Role
            ?? "";

        var packLocked = official?.Pack is { Status: var packStatus, CanUse: true }
            && string.Equals(packStatus, "LOCKED", StringComparison.OrdinalIgnoreCase);
        var workspaceLocked = crp is { CanUse: true }
            && CharacterStudioV1Rules.IsLocked(crp.Status);
        var masterLocked = CharacterAuthorityInitializationV1Rules.IsLocked(
            init?.Master.Status ?? CharacterAuthorityInitializationV1Rules.Missing);
        var officialLocked = (packLocked && init?.AuthorityLocked == true)
            || (workspaceLocked && masterLocked);

        var masterStatus = init?.Master.Status ?? CharacterAuthorityInitializationV1Rules.Missing;
        var dnaStatus = init?.Dna.Status ?? CharacterAuthorityInitializationV1Rules.Missing;
        var prpStatus = init?.Prp.Status ?? CharacterAuthorityInitializationV1Rules.Missing;
        var crpStatus = crp?.Status ?? regen?.Status ?? official?.Pack?.Status;
        var items = crp?.Items ?? [];
        if (items.Count == 0 && official?.Pack?.Items is { Count: > 0 } packItems)
        {
            items = packItems
                .Where(i => !string.IsNullOrWhiteSpace(i.Type) && !string.IsNullOrWhiteSpace(i.ArtifactPath))
                .Select(i => new CharacterAuthorityStore.CrpItem(i.Type, i.ArtifactPath, i.ArtifactSha256))
                .ToList();
        }

        var coverage = items.Count > 0 ? items.Count : regen?.Coverage ?? official?.Pack?.RequiredReady ?? 0;
        var rejected = CharacterStudioV1Rules.IsRejected(crpStatus) || regen?.Status == CharacterReferenceRegenerationV1Rules.Rejected;
        var authorityLocked = CharacterAuthorityInitializationV1Rules.IsAuthorityLocked(masterStatus, dnaStatus, prpStatus);
        var profileReady = person is not null
            && CharacterAuthorityInitializationV1Rules.IdentityReady(person.Name, person.Role, person.Visual, person.Lifecycle);
        var snap = new CharacterStudioV1Rules.StudioSnapshot(
            person is not null, profileReady, asset is not null, officialLocked,
            masterStatus, dnaStatus, prpStatus, crpStatus, coverage,
            CharacterReferenceRegenerationV1Rules.IsApproved(crpStatus)
                || CharacterReferenceRegenerationV1Rules.IsApproved(regen?.Status),
            officialLocked || CharacterStudioV1Rules.IsLocked(crpStatus) || regen?.Locked == true,
            rejected, coverage >= 4);
        var state = CharacterStudioV1Rules.ResolveState(snap);
        var chosen = string.IsNullOrWhiteSpace(provider) ? "GEMINI" : provider.Trim().ToUpperInvariant();
        var ageNum = int.TryParse(age, out var parsed) ? parsed : 11;
        var name = person?.Name ?? store?.CharacterName ?? id;
        var identitySha = Text(studio, "identitySha256")
            ?? CharacterStudioUnifiedGenerationV1Rules.IdentitySha(
                id, name, ageNum, gender ?? "", role, personality, description,
                projectStyle.Id?.ToString(), projectStyle.Sha);
        var initMasterSha = init?.Master.Sha256;
        var masterSha = initMasterSha ?? store?.MasterSha;
        var dnaSha = init?.Dna.Sha256 ?? store?.DnaSha;
        var prpSha = init?.Prp.Sha256 ?? store?.PrpSha;
        var ageTarget = ResolveAgeTarget(studio, ageNum);
        var revision = await _store.ReadRevisionAsync(id, ct);
        var masterPath = CharacterMasterRevisionV1Rules.LiveAuthorityPath(
            revision?.Status, revision?.CandidateMasterPath, store?.MasterPath);
        masterSha = CharacterMasterRevisionV1Rules.LiveAuthoritySha(
            revision?.Status, revision?.CandidateMasterSha, masterSha);
        var crpStale = CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(crp?.MasterSha, masterSha, coverage);
        var liveFp = CharacterReferencePackRules.ShaExists(masterSha)
            && CharacterReferencePackRules.ShaExists(dnaSha)
            && CharacterReferencePackRules.ShaExists(prpSha)
            ? CharacterStudioUnifiedGenerationV1Rules.Fingerprint(
                CharacterStudioV1Rules.ProjectFamixa, id, projectStyle.Sha, identitySha,
                masterSha!, dnaSha!, prpSha!, null,
                CharacterAgeConsistencyV1Rules.AgeProfileSha(ageTarget))
            : null;
        var duplicate = liveFp is not null
            && !rejected
            && !crpStale
            && state != CharacterStudioV1Rules.Failed
            && CharacterStudioUnifiedGenerationV1Rules.DuplicatePolicy(
                liveFp, crp?.Fingerprint, false) == CharacterStudioUnifiedGenerationV1Rules.GateDuplicate;
        var ageSet = ResolveAgeSet(
            ageTarget, studio,
            crp?.RejectReasonCode ?? regen?.RejectReasonCode,
            crp?.RejectReasonText ?? regen?.RejectReasonText,
            coverage);
        var input = new CharacterStudioV1Rules.GateInput(
            person is not null, profileReady, officialLocked, officialLocked,
            "GET", state, chosen, CharacterStudioV1Rules.CapabilityAllows(chosen),
            false, duplicate, false, coverage >= 4 && !rejected, rejected, null,
            projectStyle.Ready, projectStyle.Sha,
            string.Equals(ageSet.Status, CharacterAgeConsistencyV1Rules.StatusFail, StringComparison.OrdinalIgnoreCase),
            crpStale);

        var identity = CharacterStudioV1Rules.IdentityBrief(
            id, name, ageNum, gender ?? "", styleId, description, role, personality);
        var history = regen?.History ?? [];
        var appearance = CharacterAppearanceProfileV1Rules.Compile(
            new CharacterAppearanceProfileV1Rules.AppearanceSource(
                ageNum, gender, role, personality, description, extraDescription, projectStyle.Sha));
        var appearanceStored = Text(studio?["appearanceConsistency"] as JsonObject, "status");

        return new Ctx(
            id,
            name,
            role,
            era,
            person?.Version ?? "",
            gender,
            age,
            styleId,
            description,
            identity,
            chosen,
            officialLocked,
            authorityLocked,
            masterStatus,
            masterSha,
            masterPath,
            dnaStatus,
            dnaSha,
            prpStatus,
            prpSha,
            crpStatus,
            coverage,
            rejected,
            crp?.RejectReasonCode ?? regen?.RejectReasonCode,
            crp?.RejectReasonText ?? regen?.RejectReasonText,
            crp?.Fingerprint ?? liveFp ?? regen?.Fingerprint,
            crp?.Sha256 ?? regen?.CrpSha256 ?? official?.Pack?.PackSha256,
            items,
            store,
            crp,
            asset,
            input,
            history,
            projectStyle.Id?.ToString() ?? bind.ProjectVisualStyleId,
            projectStyle.Sha ?? bind.ProjectVisualStyleSha,
            projectStyle.StyleKey ?? bind.StyleKey,
            projectStyle.StyleName,
            bind.InheritStatus,
            projectStyle.Ready ? ProjectVisualStyleV1Rules.GateValid : ProjectVisualStyleV1Rules.GateNotReady,
            personality,
            extraDescription,
            identitySha,
            ageTarget,
            ageSet,
            appearance,
            appearanceStored,
            revision,
            initMasterSha);
    }

    private CharacterStudioCharacterDto ToDto(
        Ctx ctx, CharacterStudioV1Rules.Gate gate, bool generate, bool called,
        int logicalCount = 0, bool? consistencyPass = null,
        string? executionId = null, string? setId = null)
    {
        var dnaStatus = ctx.Revision is { Status: CharacterMasterRevisionV1Rules.StatusLocked }
            && !string.IsNullOrWhiteSpace(ctx.Revision.DnaStatus)
            ? ctx.Revision.DnaStatus
            : ctx.DnaStatus;
        var prpStatus = ctx.Revision is { Status: CharacterMasterRevisionV1Rules.StatusLocked }
            && !string.IsNullOrWhiteSpace(ctx.Revision.PrpStatus)
            ? ctx.Revision.PrpStatus
            : ctx.PrpStatus;
        var crpStatus = CharacterMasterRevisionV1Rules.OverlayCrpStale(
            ctx.Revision?.Status, ctx.Revision?.CrpStatus, ctx.Crp?.MasterSha, ctx.MasterSha)
            ? CharacterMasterRevisionV1Rules.StaleCrp
            : ctx.CrpStatus;
        var state = gate.Status == "BLOCKED" ? CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
            ctx.Input.CharacterExists, ctx.Input.ProfileReady, ctx.Asset is not null, ctx.OfficialLocked,
            ctx.MasterStatus, dnaStatus, prpStatus, crpStatus, ctx.Coverage,
            false, ctx.OfficialLocked, ctx.CrpRejected, ctx.Coverage >= 4)) : (gate.Status == CharacterStudioV1Rules.Failed ? CharacterStudioV1Rules.Failed : CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
            ctx.Input.CharacterExists, ctx.Input.ProfileReady, ctx.Asset is not null, ctx.OfficialLocked,
            ctx.MasterStatus, dnaStatus, prpStatus, crpStatus, ctx.Coverage,
            false, CharacterStudioV1Rules.IsLocked(ctx.CrpStatus) || ctx.OfficialLocked, ctx.CrpRejected, ctx.Coverage >= 4)));
        if (gate.Status == "BLOCKED")
            state = CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                ctx.Input.CharacterExists, ctx.Input.ProfileReady, ctx.Asset is not null, ctx.OfficialLocked,
                ctx.MasterStatus, dnaStatus, prpStatus, crpStatus, ctx.Coverage,
                false, ctx.OfficialLocked || CharacterStudioV1Rules.IsLocked(ctx.CrpStatus), ctx.CrpRejected, ctx.Coverage >= 4));

        var style = CharacterStudioV1Rules.StyleOf(ctx.StyleId);
        var scored = SlotScoresOf(ctx);
        var identityReady = CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(ctx.OfficialLocked, scored);
        var slots = CharacterStudioV1Rules.RequiredViews.Select(t =>
        {
            var item = ctx.Items.FirstOrDefault(i => string.Equals(i.Type, t, StringComparison.OrdinalIgnoreCase));
            var score = scored.FirstOrDefault(s => string.Equals(s.Type, t, StringComparison.OrdinalIgnoreCase))
                ?? CharacterStudioIdentityLockV1Rules.UnevaluatedSlot(t);
            var verdict = item is null ? "MISSING"
                : ctx.CrpRejected ? CharacterStudioV1Rules.StaffReject
                : score.Verdict;
            return new CharacterStudioSlotDto(
                t,
                t switch
                {
                    "FRONT" => "Trước mặt",
                    "THREE_QUARTER" => "3/4",
                    "SIDE" => "Nghiêng",
                    "FULL_BODY" => "Toàn thân",
                    _ => t,
                },
                verdict,
                item is null || ctx.CrpRejected ? 0 : score.Overall,
                item is not null,
                item?.Path,
                item?.Sha256);
        }).ToList();

        if (CharacterMasterRevisionV1Rules.IsOpenRevision(ctx.Revision?.Status))
            state = ctx.Revision!.Status;
        var progress = Progress(state, ctx, scored);
        var staff = gate.StaffMessage;
        if (!ctx.OfficialLocked && ctx.Coverage >= 4
            && gate.Code != CharacterStudioIdentityLockV1Rules.GateScoreFailed
            && gate.Code != CharacterStudioIdentityLockV1Rules.GateScoreNotReady)
        {
            if (CharacterStudioIdentityLockV1Rules.SlotsNeedVision(scored))
                staff = CharacterStudioIdentityLockV1Rules.StaffVisionPending;
            else if (scored.Any(s => CharacterStudioIdentityLockV1Rules.IsFail(s.Verdict)))
                staff = CharacterStudioIdentityLockV1Rules.StaffVisionFail;
        }
        var next = CharacterStudioUnifiedGenerationV1Rules.NextAction(
            state, ctx.VisualStyleGate != ProjectVisualStyleV1Rules.GateNotReady);
        if (!ctx.OfficialLocked && ctx.Coverage >= 4
            && CharacterStudioIdentityLockV1Rules.SlotsNeedVision(scored))
            next = CharacterStudioIdentityLockV1Rules.StaffScoreExisting;
        else if (!ctx.OfficialLocked && ctx.Coverage >= 4
            && scored.Any(s => CharacterStudioIdentityLockV1Rules.IsFail(s.Verdict)))
            next = CharacterStudioUnifiedGenerationV1Rules.StaffRegenerate;
        var mayScoreIdentity = CharacterStudioIdentityLockV1Rules.MayScoreExisting(
            ctx.OfficialLocked, ctx.Coverage, scored);
        var ageDto = ToAgeDto(ctx);
        if (!ctx.OfficialLocked
            && CharacterStudioIdentityLockV1Rules.SlotsNeedVision(scored)
            && string.Equals(ageDto.ConsistencyStatus, CharacterAgeConsistencyV1Rules.StatusPass, StringComparison.OrdinalIgnoreCase))
            ageDto = ageDto with
            {
                ConsistencyStatus = CharacterAgeConsistencyV1Rules.StatusNotEvaluated,
                Pass = false,
                Reason = CharacterStudioIdentityLockV1Rules.StaffVisionPending,
            };
        var suggested = _selector.Select(new CharacterStudioV1Rules.ReferenceSelectRequest("CLOSE_UP", "MCU", null, null, null));
        return new CharacterStudioCharacterDto(
            CharacterStudioV1Rules.DocumentId,
            ctx.CharacterId,
            ctx.Name,
            ctx.EraId,
            ctx.StyleId ?? "",
            style?.Label ?? "",
            int.TryParse(ctx.Age, out var age) ? age : null,
            ctx.Gender,
            ctx.Description,
            state,
            CharacterStudioUnifiedGenerationV1Rules.PublicHeadline(
                state, ctx.Coverage, 4, ctx.OfficialLocked || CharacterStudioV1Rules.IsLocked(ctx.CrpStatus)),
            staff,
            next,
            gate.Code,
            ctx.Coverage,
            4,
            state == CharacterStudioV1Rules.CharacterReady,
            ctx.OfficialLocked,
            ctx.AuthorityLocked,
            gate.MayCreate,
            gate.MayGenerate || (!ctx.OfficialLocked && ctx.Coverage < 4 && state is
                CharacterStudioV1Rules.ProfileReady or CharacterStudioV1Rules.Draft or CharacterStudioV1Rules.Failed
                or CharacterStudioV1Rules.MasterReady or CharacterStudioV1Rules.DnaReady or CharacterStudioV1Rules.PrpReady),
            (gate.MayApprove || CharacterStudioV1Rules.IsPendingCrp(state)) && identityReady,
            gate.MayReject || CharacterStudioV1Rules.IsPendingCrp(state)
                || CharacterStudioIdentityLockV1Rules.SlotsNeedVision(scored),
            !ctx.OfficialLocked
                && (gate.MayLock
                    || state == CharacterStudioV1Rules.CrpApproved
                    || CharacterReferenceRegenerationV1Rules.IsApproved(ctx.CrpStatus)
                    || CharacterStudioV1Rules.IsLocked(ctx.CrpStatus)
                    || state == CharacterStudioV1Rules.CrpLocked
                    || state == CharacterStudioV1Rules.CharacterReady)
                && CharacterAgeGateV1Rules.MayBecomeReady(ctx.OfficialLocked, StudioAgeGate(ctx).Status)
                && CharacterAppearanceConsistencyV1Rules.MayBecomeReady(
                    ctx.OfficialLocked, StudioAppearance(ctx).Status),
            CharacterStudioV1Rules.MayRegenerate(
                state, ctx.OfficialLocked, ctx.CrpRejected, AgeProgressFailed(ctx, state),
                CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage)
                    || CharacterStudioIdentityLockV1Rules.SlotsNeedVision(scored)
                    || scored.Any(s => CharacterStudioIdentityLockV1Rules.IsFail(s.Verdict))),
            gate.Code == "CONFIRMATION_REQUIRED",
            generate,
            called,
            called && string.Equals(_provider.ProviderId, "GEMINI", StringComparison.OrdinalIgnoreCase),
            false,
            false,
            false,
            false,
            ctx.Provider,
            ctx.Crp?.Version ?? "V1",
            ctx.MasterSha,
            ctx.DnaSha,
            ctx.PrpSha,
            ctx.CrpSha,
            ctx.Fingerprint,
            ctx.RejectReasonCode,
            ctx.RejectReasonText,
            slots,
            progress,
            ctx.History,
            suggested,
            new
            {
                document = CharacterStudioV1Rules.DocumentId,
                pipeline = CharacterStudioV1Rules.PipelineVersion,
                provider = ctx.Provider,
                fingerprint = ctx.Fingerprint,
                projectVisualStyleId = ctx.ProjectVisualStyleId,
                projectVisualStyleSha = ctx.ProjectVisualStyleSha,
                visualStyleCanonical = ctx.ProjectVisualStyleKey,
            },
            ctx.ProjectVisualStyleId,
            ctx.ProjectVisualStyleSha,
            ctx.ProjectVisualStyleKey,
            ctx.ProjectVisualStyleName,
            ctx.VisualStyleInherit,
            ctx.VisualStyleGate,
            ctx.Role,
            ctx.Personality,
            ctx.ExtraDescription,
            ctx.IdentitySha,
            executionId ?? ctx.Crp?.SetId,
            setId ?? ctx.Crp?.SetId,
            ctx.Items.Select(i => i.Sha256).ToList(),
            logicalCount,
            consistencyPass,
            ctx.Store?.ProviderRequestId,
            gate.Code == CharacterStudioUnifiedGenerationV1Rules.GateDuplicate,
            ctx.AgeTarget.ExpressionMinAge,
            ctx.AgeTarget.ExpressionMaxAge,
            ctx.AgeTarget.LifeStage,
            ageDto.ConsistencyStatus,
            ageDto.Score ?? ctx.AgeSet.Score,
            ageDto.Reason ?? CharacterAgeConsistencyV1Rules.PublicAgeReason(ctx.AgeSet, ctx.AgeTarget),
            ctx.AgeSet.Artifacts,
            ageDto,
            ToAppearanceDto(ctx),
            StudioAppearance(ctx).Status,
            ToRevisionCard(ctx),
            CharacterMasterRevisionV1Rules.MayRequest(
                ctx.OfficialLocked, CharacterReferencePackRules.ShaExists(ctx.MasterSha))
                && !CharacterMasterRevisionV1Rules.IsOpenRevision(ctx.Revision?.Status),
            CharacterStyleConformanceV1Rules.StatusWithoutEvaluator(),
            FamixaVisualUniverseAuthorityV1Rules.IsAuthority(FamixaVisualUniverseAuthorityV1Rules.StatusDraft)
                ? "PASS" : "NOT_LOCKED",
            FamixaVisualUniverseAuthorityV1Rules.Sha(),
            FamixaVisualUniverseAuthorityV1Rules.Version,
            false,
            ProjectVisualStyleV2Rules.ProtectedV1Sha,
            CharacterDesignLanguageV2Rules.Sha(),
            UnifiedVisualCompilerV1Rules.PromptVersion,
            FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(ctx.ProjectVisualStyleSha)
                && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(FamixaVisualUniverseAuthorityV1Rules.Sha())
                && FamixaVisualUniverseAuthorityV1Rules.LookLikeSha(CharacterDesignLanguageV2Rules.Sha())
                ? "READY" : "BLOCKED",
            mayScoreIdentity);
    }

    private static IReadOnlyList<CharacterStudioV1Rules.SlotScore> SlotScoresOf(Ctx ctx) =>
        CharacterStudioV1Rules.RequiredViews.Select(t =>
        {
            var item = ctx.Items.FirstOrDefault(i =>
                string.Equals(i.Type, t, StringComparison.OrdinalIgnoreCase));
            IReadOnlyDictionary<string, int>? persisted = null;
            ctx.Crp?.SlotScores?.TryGetValue(t, out persisted);
            if (persisted is null && ctx.OfficialLocked && item is not null)
                persisted = CharacterStudioIdentityLockV1Rules.DeclaredPassScores(t);
            return CharacterStudioV1Rules.ScoreSlot(t, item is not null, false, false, persisted);
        }).ToList();

    private static IReadOnlyList<CharacterStudioProgressDto> Progress(
        string state, Ctx ctx, IReadOnlyList<CharacterStudioV1Rules.SlotScore> scored)
    {
        var rejected = CharacterStudioV1Rules.IsRejected(state);
        var identityReady = CharacterStudioIdentityLockV1Rules.MayApproveAfterVision(ctx.OfficialLocked, scored);
        bool ViewDone(string type) =>
            ctx.OfficialLocked
                ? ctx.Items.Any(i => string.Equals(i.Type, type, StringComparison.OrdinalIgnoreCase))
                : scored.Any(s =>
                    string.Equals(s.Type, type, StringComparison.OrdinalIgnoreCase)
                    && CharacterStudioIdentityLockV1Rules.IsPass(s.Verdict));
        bool Done(params string[] after) => after.Contains(state)
            || state is CharacterStudioV1Rules.CrpPendingReview or CharacterStudioV1Rules.CrpApproved
                or CharacterStudioV1Rules.CrpLocked or CharacterStudioV1Rules.CharacterReady
                or CharacterStudioV1Rules.Rejected;
        var steps = new (string Id, string Label, bool DoneFlag)[]
        {
            ("identity", "Hồ sơ nhân vật", ctx.Input.ProfileReady),
            ("prepare", CharacterStudioUnifiedGenerationV1Rules.StaffPreparing,
                CharacterAuthorityInitializationV1Rules.IsLocked(ctx.MasterStatus)
                && CharacterAuthorityInitializationV1Rules.IsLocked(ctx.DnaStatus)
                && CharacterAuthorityInitializationV1Rules.IsLocked(ctx.PrpStatus)
                || Done(CharacterStudioV1Rules.PrpReady)),
            ("set", CharacterStudioUnifiedGenerationV1Rules.StaffGenerating, ctx.Coverage >= 4),
            ("front", "Trước mặt", ViewDone("FRONT")),
            ("three", "3/4", ViewDone("THREE_QUARTER")),
            ("side", "Nghiêng", ViewDone("SIDE")),
            ("body", "Toàn thân", ViewDone("FULL_BODY")),
            ("check", ctx.Coverage >= 4 && !rejected && identityReady
                ? CharacterStudioUnifiedGenerationV1Rules.StaffSetReady
                : "Kiểm tra đồng nhất",
                identityReady && !rejected && state != CharacterStudioV1Rules.Failed),
            ("face", "Face consistency",
                identityReady && !rejected && state != CharacterStudioV1Rules.Failed),
            ("style", "Visual style",
                identityReady && !rejected && state != CharacterStudioV1Rules.Failed),
            ("age", "Age consistency", identityReady && AgeProgressDone(ctx, state)),
            ("appearance", "Appearance consistency", identityReady && AppearanceProgressDone(ctx, state)),
            ("review", CharacterStudioUnifiedGenerationV1Rules.ReviewStepLabel(
                    state, ctx.OfficialLocked || CharacterStudioV1Rules.IsLocked(state)),
                identityReady && (state is CharacterStudioV1Rules.CrpPendingReview or CharacterStudioV1Rules.CrpApproved
                    or CharacterStudioV1Rules.CrpLocked or CharacterStudioV1Rules.CharacterReady)),
        };
        return steps.Select((s, i) => new CharacterStudioProgressDto(
            s.Id, s.Label, s.DoneFlag,
            !s.DoneFlag && !rejected && steps.Take(i).All(x => x.DoneFlag),
            (rejected && s.Id == "check")
                || (s.Id == "age" && AgeProgressFailed(ctx, state))
                || (s.Id == "appearance" && AppearanceProgressFailed(ctx, state))
                || (state == CharacterStudioV1Rules.Failed && !s.DoneFlag && steps.Take(i).All(x => x.DoneFlag)))).ToList();
    }

    private static AgeConsistencyResult StudioAgeGate(Ctx ctx)
    {
        var profile = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(ctx.AgeTarget, ctx.Gender);
        return CharacterAgeGateV1Rules.Resolve(
            ctx.AgeTarget, profile, ctx.Coverage, ctx.AgeSet.Status, ctx.AgeSet.Score,
            null, null, ctx.AgeSet.Reason);
    }

    private static CharacterStudioAgeDto ToAgeDto(Ctx ctx)
    {
        var gate = StudioAgeGate(ctx);
        var profile = CharacterAgeConsistencyV1Rules.AgeAppearanceProfileText(ctx.AgeTarget, ctx.Gender);
        return new CharacterStudioAgeDto(
            ctx.AgeTarget.ChronologicalAge,
            ctx.AgeTarget.TargetAppearanceAgeMin,
            ctx.AgeTarget.TargetAppearanceAgeMax,
            gate.Status,
            ctx.AgeTarget.LifeStage,
            profile,
            gate.Pass,
            gate.Score,
            gate.EvaluatedAgeMin,
            gate.EvaluatedAgeMax,
            gate.Reason,
            gate.Evaluator,
            gate.EvaluatedAt,
            gate.Code);
    }

    private static CharacterAgeGenerationIntegrationV1Rules.AgeAwareGenerationRequest StudioGenerationRequest(Ctx ctx)
    {
        var preset = ProjectVisualStyleV1Rules.PresetOf(ctx.ProjectVisualStyleKey ?? ctx.StyleId);
        var stylePrompt = ProjectVisualStyleV2Rules.CompileAuthorityPrompt(ctx.ProjectVisualStyleSha, preset);
        return CharacterAgeGenerationIntegrationV1Rules.BuildRequest(
            ctx.CharacterId, ctx.EraId, ctx.ProjectVisualStyleSha, stylePrompt,
            ctx.IdentityBrief, ctx.IdentitySha,
            ctx.MasterSha ?? "", ctx.DnaSha ?? "", ctx.PrpSha ?? "",
            ctx.AgeTarget, ctx.Gender, ctx.Appearance);
    }

    private static CharacterReferenceSetRequest StudioViewSet(
        Ctx ctx, IReadOnlyList<CharacterReferenceViewRequest> views) =>
        CharacterAgeGenerationIntegrationV1Rules.ToProviderRequest(StudioGenerationRequest(ctx), views);

    private static CharacterStudioV1Rules.Gate? AgeGenerationGate(Ctx ctx)
    {
        var code = CharacterAgeGenerationIntegrationV1Rules.ValidateRequest(StudioGenerationRequest(ctx))
            ?? CharacterAgeConsistencyV1Rules.ValidateProfile(
                ctx.AgeTarget.ChronologicalAge,
                ctx.AgeTarget.TargetAppearanceAgeMin,
                ctx.AgeTarget.TargetAppearanceAgeMax)
            ?? CharacterAppearanceProfileV1Rules.Validate(ctx.Appearance);
        if (code is null) return null;
        var staff = code == CharacterAgeConsistencyV1Rules.GateInvalid
            ? "Khoảng tuổi biểu hiện không hợp lệ."
            : code == CharacterAppearanceProfileV1Rules.GateNotReady
                || code == CharacterAppearanceProfileV1Rules.GateShaMissing
                || code == CharacterAppearanceProfileV1Rules.GateInvalid
                ? "Cần Character Appearance Profile trước khi tạo bộ ảnh."
                : "Cần tuổi nhân vật và khoảng tuổi biểu hiện mục tiêu.";
        return new CharacterStudioV1Rules.Gate("BLOCKED", code, staff, false, false, false, false, false, false, false);
    }

    private async Task PersistAgeReviewAsync(
        Ctx ctx, string status, string? reasonCode, string? note, string actor, CancellationToken ct)
    {
        if (ctx.OfficialLocked || ctx.Asset is null) return;
        var extra = Parse(ctx.Asset.ExtraJson);
        var studio = extra["characterStudio"] as JsonObject ?? [];
        var audit = studio["ageAudit"] as JsonArray ?? [];
        audit.Add(new JsonObject
        {
            ["action"] = "AGE_CONSISTENCY_" + status,
            ["actor"] = actor,
            ["reasonCode"] = reasonCode,
            ["note"] = note,
            ["at"] = DateTimeOffset.UtcNow.ToString("O"),
        });
        studio["ageConsistency"] = new JsonObject
        {
            ["document"] = CharacterAgeConsistencyV1Rules.DocumentId,
            ["status"] = status,
            ["reasonCode"] = reasonCode,
            ["note"] = note,
            ["reviewedBy"] = actor,
        };
        studio["ageAudit"] = audit;
        extra["characterStudio"] = studio;
        await _assets.MergeAssetExtraAsync(ctx.Asset.AssetId, extra.ToJsonString(), ct);
    }

    private async Task PersistAppearanceReviewAsync(
        Ctx ctx, string status, string? reasonCode, string? note, string actor, CancellationToken ct)
    {
        if (ctx.OfficialLocked)
            throw new InvalidOperationException("AUTHORITY_LOCKED: Nhân vật đã khóa.");
        if (ctx.Asset is null)
            throw new InvalidOperationException(
                CharacterAppearanceConsistencyV1Rules.GateReviewNotReady
                + ": Chưa có asset nhân vật để lưu kết quả chấm.");
        var extra = Parse(ctx.Asset.ExtraJson);
        var studio = extra["characterStudio"] as JsonObject ?? [];
        var audit = studio["appearanceAudit"] as JsonArray ?? [];
        audit.Add(new JsonObject
        {
            ["action"] = "APPEARANCE_CONSISTENCY_" + status,
            ["actor"] = actor,
            ["reasonCode"] = reasonCode,
            ["note"] = note,
            ["at"] = DateTimeOffset.UtcNow.ToString("O"),
        });
        studio["appearanceConsistency"] = new JsonObject
        {
            ["document"] = CharacterAppearanceConsistencyV1Rules.DocumentId,
            ["status"] = status,
            ["reasonCode"] = reasonCode,
            ["note"] = note,
            ["reviewedBy"] = actor,
        };
        studio["appearanceAudit"] = audit;
        extra["characterStudio"] = studio;
        await _assets.MergeAssetExtraAsync(ctx.Asset.AssetId, extra.ToJsonString(), ct);
    }

    private static bool AgeProgressDone(Ctx ctx, string state) =>
        ctx.OfficialLocked
        || state is CharacterStudioV1Rules.CharacterReady or CharacterStudioV1Rules.CrpLocked
        || ctx.AgeSet.Status == CharacterAgeConsistencyV1Rules.StatusPass;

    private static bool AgeProgressFailed(Ctx ctx, string state) =>
        !ctx.OfficialLocked
        && state is not CharacterStudioV1Rules.CharacterReady and not CharacterStudioV1Rules.CrpLocked
        && ctx.AgeSet.Status == CharacterAgeConsistencyV1Rules.StatusFail;

    private static CharacterAppearanceConsistencyResult StudioAppearance(Ctx ctx) =>
        CharacterAppearanceConsistencyV1Rules.Resolve(
            ctx.Appearance, ctx.Coverage, ctx.AppearanceStoredStatus, ctx.OfficialLocked);

    private async Task<CharacterMasterRevisionDto> AdvanceRevisionAsync(
        string characterId,
        string actor,
        CharacterMasterRevisionReviewRequestDto? review,
        string action,
        CancellationToken ct)
    {
        var era = string.IsNullOrWhiteSpace(review?.EraId)
            ? CharacterStudioV1Rules.DefaultEra
            : review!.EraId.Trim().ToUpperInvariant();
        var gate = ReviewGates.GetOrAdd(
            CharacterStudioV1Rules.NormalizeCharacterId(characterId),
            _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var ctx = await LoadAsync(characterId, era, null, ct);
            if (ctx.OfficialLocked)
                throw new InvalidOperationException(CharacterMasterRevisionV1Rules.GateLockedSilent);
            if (ctx.Asset is null || ctx.Revision is null
                || !CharacterReferencePackRules.ShaExists(ctx.Revision.CandidateMasterSha))
                throw new InvalidOperationException(
                    CharacterMasterRevisionDirectorReviewV1Rules.GateCandidateNotFound);
            var liveAuthority = LiveAuthoritySha(ctx);
            var code = CharacterMasterRevisionDirectorReviewV1Rules.Evaluate(
                new CharacterMasterRevisionDirectorReviewV1Rules.ReviewCommand(
                    ctx.CharacterId,
                    ctx.Revision.CharacterId,
                    ctx.Revision.CandidateMasterSha,
                    ctx.Revision.CurrentMasterSha,
                    liveAuthority,
                    ctx.Revision.Status,
                    action,
                    actor,
                    review?.Notes,
                    review?.RejectionReason));
            if (code is not null)
                throw new InvalidOperationException(code + ": " + RevisionStaff(code));
            var state = new CharacterMasterRevisionV1Rules.AuthorityState(
                ctx.Revision.CurrentMasterSha ?? ctx.MasterSha,
                ctx.Revision.CandidateMasterSha,
                ctx.Revision.Status,
                ctx.Revision.DnaStatus,
                ctx.Revision.PrpStatus,
                ctx.Revision.CrpStatus);
            var next = CharacterMasterRevisionDirectorReviewV1Rules.Apply(state, action);
            if (next.Status == state.Status)
                throw new InvalidOperationException(
                    CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState);
            var decision = action switch
            {
                CharacterMasterRevisionDirectorReviewV1Rules.ActionApprove =>
                    CharacterMasterRevisionDirectorReviewV1Rules.DecisionApprove,
                CharacterMasterRevisionDirectorReviewV1Rules.ActionReject =>
                    CharacterMasterRevisionDirectorReviewV1Rules.DecisionReject,
                _ => CharacterMasterRevisionDirectorReviewV1Rules.DecisionLock,
            };
            var audit = CharacterMasterRevisionDirectorReviewV1Rules.Audit(
                ctx.CharacterId, ctx.Revision.CandidateMasterId, state.Status, next.Status, actor,
                liveAuthority, ctx.Revision.CandidateMasterSha, decision,
                review?.Notes ?? review?.RejectionReason);
            var snap = ctx.Revision with
            {
                Status = next.Status,
                CurrentMasterSha = next.CurrentMasterSha,
                DnaStatus = next.DnaStatus,
                PrpStatus = next.PrpStatus,
                CrpStatus = next.CrpStatus,
                RequestedBy = actor,
                ReviewedBy = actor,
                ReviewedAt = audit.Timestamp,
                ReviewDecision = decision,
                RejectionReason = action == CharacterMasterRevisionDirectorReviewV1Rules.ActionReject
                    ? review?.RejectionReason
                    : ctx.Revision.RejectionReason,
                ReviewNotes = review?.Notes ?? ctx.Revision.ReviewNotes,
                AuditJson = AppendAudit(ctx.Revision.AuditJson, audit),
            };
            if (!await _store.WriteRevisionIfStatusAsync(
                    ctx.CharacterId, ctx.Asset.AssetId, state.Status, snap, ct))
                throw new InvalidOperationException(
                    CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState);
            if (action == CharacterMasterRevisionDirectorReviewV1Rules.ActionLock
                && ctx.Store is not null
                && CharacterReferencePackRules.ShaExists(ctx.Revision.CandidateMasterSha))
            {
                await _store.WriteAsync(ctx.Store with
                {
                    MasterSha = ctx.Revision.CandidateMasterSha,
                    MasterPath = ctx.Revision.CandidateMasterPath ?? ctx.Store.MasterPath,
                }, ct);
            }
            ctx = await LoadAsync(characterId, era, null, ct);
            return ToRevisionDto(ctx, ToRevisionSource(ctx, new CharacterMasterRevisionRequestDto()),
                null, false, false, ctx.Revision?.ProviderRequestId);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string AppendAudit(
        string? existing, CharacterMasterRevisionDirectorReviewV1Rules.ReviewAudit audit)
    {
        JsonArray arr;
        try
        {
            arr = JsonNode.Parse(string.IsNullOrWhiteSpace(existing) ? "[]" : existing) as JsonArray
                ?? [];
        }
        catch (JsonException)
        {
            arr = [];
        }
        arr.Add(new JsonObject
        {
            ["characterId"] = audit.CharacterId,
            ["candidateMasterId"] = audit.CandidateMasterId,
            ["previousStatus"] = audit.PreviousStatus,
            ["newStatus"] = audit.NewStatus,
            ["actor"] = audit.Actor,
            ["timestamp"] = audit.Timestamp,
            ["currentAuthoritySha"] = audit.CurrentAuthoritySha,
            ["candidateSha"] = audit.CandidateSha,
            ["decision"] = audit.Decision,
            ["notes"] = audit.Notes,
        });
        return arr.ToJsonString();
    }

    private static CharacterMasterRevisionV1Rules.RevisionSource ToRevisionSource(
        Ctx ctx, CharacterMasterRevisionRequestDto request)
    {
        var lastOk = ctx.Revision is not null
            && ctx.Revision.Status is CharacterMasterRevisionV1Rules.StatusPendingReview
                or CharacterMasterRevisionV1Rules.StatusApproved
                or CharacterMasterRevisionV1Rules.StatusLocked
            ? ctx.Revision.Fingerprint
            : null;
        return new CharacterMasterRevisionV1Rules.RevisionSource(
            ctx.Input.CharacterExists,
            ctx.CharacterId,
            CharacterReferencePackRules.ShaExists(ctx.MasterSha),
            ctx.MasterPath,
            ctx.MasterSha,
            ctx.Appearance,
            ctx.AgeTarget,
            ctx.IdentitySha,
            ctx.IdentityBrief,
            ctx.ProjectVisualStyleSha,
            ctx.VisualStyleGate == ProjectVisualStyleV1Rules.GateValid,
            request.RevisionReason,
            request.RevisionNotes,
            request.Confirm,
            request.Provider,
            ctx.OfficialLocked,
            ctx.Input.StudioState == CharacterStudioV1Rules.CharacterReady,
            lastOk,
            ctx.Revision?.CandidateMasterSha,
            ctx.Revision?.Status);
    }

    private CharacterMasterRevisionDto ToRevisionDto(
        Ctx ctx,
        CharacterMasterRevisionV1Rules.RevisionSource source,
        string? code,
        bool providerCalled,
        bool executed,
        string? providerRequestId)
    {
        var gate = new CharacterStudioV1Rules.Gate(
            code is null ? CharacterStudioV1Rules.ResolveState(new CharacterStudioV1Rules.StudioSnapshot(
                ctx.Input.CharacterExists, ctx.Input.ProfileReady, ctx.Asset is not null, ctx.OfficialLocked,
                ctx.MasterStatus, ctx.DnaStatus, ctx.PrpStatus, ctx.CrpStatus, ctx.Coverage,
                false, ctx.OfficialLocked, ctx.CrpRejected, ctx.Coverage >= 4)) : "BLOCKED",
            code, RevisionStaff(code), false, false, false, false, false, false, false);
        var row = ToDto(ctx, gate, false, providerCalled);
        return new CharacterMasterRevisionDto(
            CharacterMasterRevisionV1Rules.DocumentId,
            ctx.Revision?.RevisionId,
            ctx.CharacterId,
            ctx.Revision?.CurrentMasterId ?? ctx.MasterPath,
            ctx.Revision?.CurrentMasterSha ?? ctx.MasterSha,
            ctx.Revision?.CandidateMasterId,
            ctx.Revision?.CandidateMasterSha,
            ctx.Revision?.Status ?? (code is null
                ? CharacterMasterRevisionV1Rules.StatusRequested
                : CharacterMasterRevisionV1Rules.StatusRequested),
            ctx.Revision?.RevisionReason ?? source.RevisionReason,
            ctx.Revision?.RevisionNotes ?? source.RevisionNotes,
            ctx.Provider,
            executed,
            providerCalled,
            providerCalled,
            providerRequestId ?? ctx.Revision?.ProviderRequestId,
            ctx.Revision?.Fingerprint ?? (code is null || code == CharacterMasterRevisionV1Rules.GateConfirmation
                ? CharacterMasterRevisionV1Rules.Fingerprint(source)
                : null),
            code,
            RevisionStaff(code),
            ctx.Appearance.ProfileSha,
            CharacterAgeConsistencyV1Rules.AgeProfileSha(ctx.AgeTarget),
            ctx.ProjectVisualStyleSha,
            ctx.IdentitySha,
            ctx.Revision?.GenerationContractSha,
            ctx.Revision?.CurrentMasterVersion ?? "V1",
            ctx.Revision?.CandidateMasterVersion,
            row,
            ctx.Revision?.ReviewedBy,
            ctx.Revision?.ReviewedAt,
            ctx.Revision?.ReviewDecision,
            ctx.Revision?.RejectionReason,
            ctx.Revision?.ReviewNotes);
    }

    private static string? LiveAuthoritySha(Ctx ctx)
    {
        if (CharacterReferencePackRules.ShaExists(ctx.Revision?.CurrentMasterSha))
            return ctx.Revision!.CurrentMasterSha;
        if (ctx.Revision?.Status == CharacterMasterRevisionV1Rules.StatusLocked
            && CharacterReferencePackRules.ShaExists(ctx.Revision.CandidateMasterSha))
            return ctx.Revision.CandidateMasterSha;
        return ctx.Store?.MasterSha ?? ctx.MasterSha;
    }

    private static CharacterStudioMasterRevisionCardDto ToRevisionCard(Ctx ctx)
    {
        var status = ctx.Revision?.Status;
        var open = CharacterMasterRevisionV1Rules.IsOpenRevision(status);
        var pending = status == CharacterMasterRevisionV1Rules.StatusPendingReview;
        var approved = status == CharacterMasterRevisionV1Rules.StatusApproved;
        var locked = status == CharacterMasterRevisionV1Rules.StatusLocked;
        return new CharacterStudioMasterRevisionCardDto(
            status ?? "",
            ctx.Revision?.RevisionReason,
            ctx.Revision?.CurrentMasterSha ?? ctx.MasterSha,
            ctx.Revision?.CandidateMasterSha,
            ctx.Revision?.CandidateMasterVersion,
            CharacterMasterRevisionV1Rules.MayRequest(
                ctx.OfficialLocked, CharacterReferencePackRules.ShaExists(ctx.MasterSha)) && !open,
            pending && !ctx.OfficialLocked,
            pending && !ctx.OfficialLocked,
            approved && !ctx.OfficialLocked,
            null,
            ctx.Revision?.Fingerprint,
            CharacterMasterRevisionDirectorReviewV1Rules.IsCurrentAuthority(status, false),
            CharacterMasterRevisionDirectorReviewV1Rules.IsCurrentAuthority(status, true),
            locked ? (ctx.Revision?.CandidateMasterVersion ?? "V2") : (ctx.Revision?.CurrentMasterVersion ?? "V1"),
            $"{ctx.AgeTarget.TargetAppearanceAgeMin}–{ctx.AgeTarget.TargetAppearanceAgeMax}",
            CharacterAppearanceProfileV1Rules.PublicFacialMaturity(ctx.Appearance.ChronologicalAge),
            ctx.Revision?.RejectionReason,
            ctx.Revision?.ReviewDecision,
            ctx.Revision?.ReviewedBy,
            ctx.Revision?.ReviewNotes,
            CharacterMasterRevisionV1Rules.HistoricalAuthoritySha(
                status,
                ctx.Revision?.CandidateMasterSha,
                ctx.Revision?.CurrentMasterSha,
                ctx.Store?.MasterSha,
                ctx.InitMasterSha),
            ctx.Revision?.CurrentMasterVersion ?? "V1",
            CharacterMasterRevisionV1Rules.CrpStaleForCurrentMaster(ctx.Crp?.MasterSha, ctx.MasterSha, ctx.Coverage),
            string.Equals(
                ctx.Revision is { Status: CharacterMasterRevisionV1Rules.StatusLocked }
                    ? ctx.Revision.DnaStatus : ctx.DnaStatus,
                CharacterMasterRevisionV1Rules.StaleDna, StringComparison.OrdinalIgnoreCase),
            string.Equals(
                ctx.Revision is { Status: CharacterMasterRevisionV1Rules.StatusLocked }
                    ? ctx.Revision.PrpStatus : ctx.PrpStatus,
                CharacterMasterRevisionV1Rules.StalePrp, StringComparison.OrdinalIgnoreCase));
    }

    private static CharacterAuthorityStore.RevisionSnapshot RevisionFrom(
        Ctx ctx,
        CharacterMasterRevisionV1Rules.RevisionSource source,
        CharacterMasterGenerationRequest contract,
        string status,
        string actor,
        string? candidatePath,
        string? candidateSha,
        string? candidateVersion,
        string? providerRequestId = null) =>
        new(
            ctx.CharacterId,
            status,
            Guid.NewGuid().ToString("N"),
            ctx.MasterPath,
            ctx.MasterSha,
            ctx.MasterPath,
            "V1",
            candidatePath,
            candidateSha,
            candidatePath,
            candidateVersion,
            CharacterMasterRevisionV1Rules.NormalizeReason(source.RevisionReason),
            source.RevisionNotes,
            ctx.Appearance.ProfileSha,
            CharacterAgeConsistencyV1Rules.AgeProfileSha(ctx.AgeTarget),
            ctx.ProjectVisualStyleSha,
            ctx.IdentitySha,
            contract.Fingerprint,
            contract.GenerationContractSha,
            ctx.Provider,
            providerRequestId,
            actor,
            DateTimeOffset.UtcNow.ToString("O"),
            null,
            null,
            null);

    private static string RevisionStaff(string? code) => code switch
    {
        null => "Master Revision đã sẵn sàng để Director xem.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateInvalidState =>
            "Trạng thái Master Revision không cho phép thao tác này.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateNotApproved =>
            "Chỉ khóa Master Revision sau khi Director duyệt.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateCandidateNotFound =>
            "Không có Master Revision candidate.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateCharacterMismatch =>
            "Candidate không thuộc nhân vật này.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateAuthorityChanged =>
            "Current Master Authority đã đổi. Không khóa candidate cũ.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateUnauthorized =>
            "Tài khoản này không được duyệt Master Revision.",
        CharacterMasterRevisionDirectorReviewV1Rules.GateRejectReason =>
            "Cần nhập lý do khi đánh giá Không đạt.",
        CharacterMasterRevisionV1Rules.GateConfirmation =>
            "Bạn đang tạo Master Revision mới. Master hiện tại sẽ được giữ nguyên. Một Master candidate mới sẽ được tạo để Director xem xét.",
        CharacterMasterRevisionV1Rules.GateLockedSilent =>
            "Nhân vật LOCKED không được sửa Master im lặng.",
        FamixaVisualUniverseAuthorityV1Rules.GateAuthorityNotLocked =>
            "Visual Universe Authority chưa khóa. Master Revision vẫn tạo candidate theo PVS + CDL hiện tại.",
        FamixaVisualUniverseAuthorityV1Rules.GateCalibrationNotApproved =>
            "Style Calibration chưa duyệt. Master Revision không bị chặn khi VUA còn DRAFT.",
        CharacterMasterRevisionV1Rules.GateDuplicate =>
            "Revision này đã tạo thành công. Không gọi lại nhà cung cấp.",
        CharacterMasterRevisionV1Rules.GateProviderUnsupported =>
            "Nhà cung cấp không hỗ trợ tạo Master.",
        CharacterAppearanceProfileV1Rules.GateNotReady =>
            "Cần Character Appearance Profile trước khi tạo Master Revision.",
        CharacterAgeConsistencyV1Rules.GateNotReady =>
            "Cần Age Appearance Profile trước khi tạo Master Revision.",
        ProjectVisualStyleV1Rules.GateNotReady =>
            "Cần Project Visual Style Authority trước khi tạo Master Revision.",
        _ => "Chưa thể tạo Master Revision.",
    };

    private static CharacterStudioAppearanceDto ToAppearanceDto(Ctx ctx)
    {
        var gate = StudioAppearance(ctx);
        return new CharacterStudioAppearanceDto(
            ctx.Appearance.ChronologicalAge,
            ctx.Appearance.TargetAppearanceAgeMin,
            ctx.Appearance.TargetAppearanceAgeMax,
            CharacterAppearanceProfileV1Rules.PublicFacialMaturity(ctx.Appearance.ChronologicalAge),
            ctx.Appearance.LifestyleProfile,
            ctx.Appearance.EnergyProfile,
            ctx.Appearance.GroomingProfile,
            gate.Status,
            ctx.Appearance.ProfileSha,
            ctx.Appearance.Role,
            ctx.Appearance.ClothingProfile,
            gate.Code);
    }

    private static bool AppearanceProgressDone(Ctx ctx, string state) =>
        ctx.OfficialLocked
        || state is CharacterStudioV1Rules.CharacterReady or CharacterStudioV1Rules.CrpLocked
        || StudioAppearance(ctx).Status == CharacterAppearanceConsistencyV1Rules.StatusPass;

    private static bool AppearanceProgressFailed(Ctx ctx, string state) =>
        !ctx.OfficialLocked
        && state is not CharacterStudioV1Rules.CharacterReady and not CharacterStudioV1Rules.CrpLocked
        && StudioAppearance(ctx).Status == CharacterAppearanceConsistencyV1Rules.StatusFail;

    private static AgeExpressionTarget ResolveAgeTarget(JsonObject? studio, int ageYears)
    {
        var expr = studio?["ageExpression"] as JsonObject;
        var provenance = Text(expr, "overrideProvenance");
        var explicitOverride = !string.IsNullOrWhiteSpace(provenance);
        return CharacterAgeConsistencyV1Rules.FromCanonicalAge(
            ageYears,
            explicitOverride ? IntOf(expr, "targetAppearanceAgeMin") ?? IntOf(expr, "expressionMinAge") : null,
            explicitOverride ? IntOf(expr, "targetAppearanceAgeMax") ?? IntOf(expr, "expressionMaxAge") : null,
            provenance);
    }

    private static AgeSetResult ResolveAgeSet(
        AgeExpressionTarget target, JsonObject? studio,
        string? rejectCode, string? rejectText, int coverage)
    {
        var artifacts = new List<AgeArtifactInput>();
        if (studio?["ageConsistency"] is JsonObject gate && gate["artifacts"] is JsonArray arr)
        {
            foreach (var item in arr.OfType<JsonObject>())
            {
                var type = Text(item, "type");
                if (string.IsNullOrWhiteSpace(type)) continue;
                artifacts.Add(new AgeArtifactInput(type, IntOf(item, "apparentAge")));
            }
        }

        if (artifacts.Count == 0 && coverage >= 4)
        {
            artifacts.AddRange(CharacterStudioV1Rules.RequiredViews.Select(t => new AgeArtifactInput(t, null)));
        }

        var set = CharacterAgeConsistencyV1Rules.EvaluateSet(target, artifacts);
        var stored = Text(studio?["ageConsistency"] as JsonObject, "status");
        if (string.Equals(stored, CharacterAgeConsistencyV1Rules.StatusPass, StringComparison.OrdinalIgnoreCase)
            && set.Status != CharacterAgeConsistencyV1Rules.StatusFail)
        {
            set = set with { Status = CharacterAgeConsistencyV1Rules.StatusPass };
        }

        if (string.Equals(rejectCode, CharacterAgeConsistencyV1Rules.RejectAgeMismatch, StringComparison.OrdinalIgnoreCase)
            && set.Status != CharacterAgeConsistencyV1Rules.StatusFail)
        {
            return set with
            {
                Status = CharacterAgeConsistencyV1Rules.StatusFail,
                Reason = string.IsNullOrWhiteSpace(rejectText)
                    ? "Tuổi biểu hiện không phù hợp với tuổi nhân vật."
                    : rejectText,
            };
        }

        return set;
    }

    private static int? IntOf(JsonObject? node, string key)
    {
        if (node is null || !node.TryGetPropertyValue(key, out var value) || value is null) return null;
        if (value is JsonValue jv && jv.TryGetValue<int>(out var n)) return n;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static string NextAction(string state, CharacterStudioV1Rules.Gate gate) =>
        CharacterStudioUnifiedGenerationV1Rules.NextAction(
            state, gate.Code != ProjectVisualStyleV1Rules.GateNotReady);

    private static string Era(string? era) =>
        string.IsNullOrWhiteSpace(era) ? CharacterStudioV1Rules.DefaultEra : era.Trim().ToUpperInvariant();

    private static JsonObject Parse(string? raw)
    {
        try { return JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject ?? []; }
        catch (JsonException) { return []; }
    }

    private static string? Text(JsonObject? node, string key)
    {
        if (node is null || !node.TryGetPropertyValue(key, out var value) || value is null) return null;
        if (value is JsonValue jv && jv.TryGetValue<int>(out var n)) return n.ToString();
        var t = value.GetValue<string?>();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static string? CanonText(JsonElement? canon, string obj, string key)
    {
        if (canon is not { ValueKind: JsonValueKind.Object } root) return null;
        if (!root.TryGetProperty(obj, out var child) || child.ValueKind != JsonValueKind.Object) return null;
        if (!child.TryGetProperty(key, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }
}
