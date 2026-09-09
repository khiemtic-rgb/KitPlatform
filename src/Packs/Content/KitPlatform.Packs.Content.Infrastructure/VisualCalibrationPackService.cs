using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class VisualCalibrationPackService : IVisualCalibrationPackService
{
    private readonly KitVideoVisualSystemRepository _repo;
    private readonly IFamixaCharacterService _characters;
    private readonly IVisualUniverseAuthority _universe;
    private readonly ICharacterAuthorityInitializationV1Service _init;
    private readonly CharacterAuthorityStore _store;
    private readonly KitVideoMasterReferenceRepository _assets;
    private readonly KitVideoArtifactStore _artifacts;
    private readonly IVisualCalibrationGenerationProvider _provider;
    private static readonly SemaphoreSlim GenerateGate = new(1, 1);

    public VisualCalibrationPackService(
        KitVideoVisualSystemRepository repo,
        IFamixaCharacterService characters,
        IVisualUniverseAuthority universe,
        ICharacterAuthorityInitializationV1Service init,
        CharacterAuthorityStore store,
        KitVideoMasterReferenceRepository assets,
        KitVideoArtifactStore artifacts,
        IVisualCalibrationGenerationProvider provider)
    {
        _repo = repo;
        _characters = characters;
        _universe = universe;
        _init = init;
        _store = store;
        _assets = assets;
        _artifacts = artifacts;
        _provider = provider;
    }

    public IReadOnlyList<string> RunRegression() => VisualCalibrationPackV1Regression.Run();
    public IReadOnlyList<string> RunHardeningRegression() => VisualCalibrationPackV1HardeningRegression.Run();
    public IReadOnlyList<string> RunLiveGenerationReadinessRegression() =>
        VisualCalibrationPackV1LiveGenerationReadinessRegression.Run();
    public IReadOnlyList<string> RunLiveGenerationRegression() =>
        VisualCalibrationPackV1LiveGenerationRegression.Run();

    public async Task<VisualCalibrationPackDto> CreatePackAsync(
        VisualCalibrationRequestDto request, string actor, CancellationToken cancellationToken = default)
    {
        var compiled = VisualCalibrationPackV1Rules.CompilePack();
        if (!request.Confirm)
            return VisualCalibrationPackV1Rules.ToDto(compiled, VisualCalibrationPackV1Rules.GateConfirmation);
        var row = await _repo.GetInForceAsync(Project(), ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        if (row is null)
            return VisualCalibrationPackV1Rules.ToDto(compiled, VisualCalibrationPackV1Rules.GatePvsNotReady);
        var snap = compiled with { PackId = request.PackId ?? compiled.PackId };
        await _repo.UpdateRulesJsonAsync(
            row.Id, VisualCalibrationPackV1Rules.MergeSnapshot(row.RulesJson, snap), cancellationToken);
        _ = actor;
        return await WithImpact(VisualCalibrationPackV1Rules.ToDto(snap), cancellationToken);
    }

    public async Task<VisualCalibrationPackDto> GenerateAsync(
        string packId, VisualCalibrationRequestDto request, string actor, CancellationToken cancellationToken = default)
    {
        var compiled = VisualCalibrationPackV1Rules.CompilePack();
        if (!request.Confirm)
        {
            var rowDry = await _repo.GetInForceAsync(Project(), ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
            var existingDry = rowDry is null ? null : VisualCalibrationPackV1Rules.ReadSnapshot(rowDry.RulesJson);
            var workingDry = existingDry ?? compiled with
            {
                PackId = string.IsNullOrWhiteSpace(packId) ? compiled.PackId : packId.Trim(),
            };
            var live = IdentityConditionedCalibrationLiveV1Rules.Readiness(workingDry);
            return VisualCalibrationPackV1Rules.ToDto(workingDry, VisualCalibrationPackV1Rules.GateConfirmation) with
            {
                IdentityCoverage = IdentityConditionedCalibrationV1Rules.GetCoverage(workingDry),
                IdentityPlan = live.Plan.Select(r => IdentityConditionedCalibrationV1Rules.ToSlotPlan(r)).ToList(),
                CalibrationRunId = live.CalibrationRunId,
                LiveStatus = live.Status,
                ProviderCalled = false,
                GenerationExecuted = false,
                GeminiCalled = false,
                StaffMessage = "READY_FOR_DIRECTOR_CONFIRMATION. confirm=true and generate=true required. Provider was not called.",
            };
        }
        var gate = VisualCalibrationPackV1Rules.ValidateGenerate(
            compiled.VisualUniverseSha, compiled.ProjectVisualStyleSha, compiled.CharacterDesignLanguageSha,
            request.Confirm);
        if (gate is not null)
            return VisualCalibrationPackV1Rules.ToDto(compiled, gate);

        var row = await _repo.GetInForceAsync(Project(), ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        if (row is null)
            return VisualCalibrationPackV1Rules.ToDto(compiled, VisualCalibrationPackV1Rules.GatePvsNotReady);
        var existing = VisualCalibrationPackV1Rules.ReadSnapshot(row.RulesJson);
        var working = existing ?? compiled with
        {
            PackId = string.IsNullOrWhiteSpace(packId) ? compiled.PackId : packId.Trim(),
        };
        if (existing is not null
            && !VisualCalibrationPackV1Rules.MayGenerate(existing.Status)
            && VisualCalibrationPackV1Rules.GetCoverage(existing).Valid != 24)
            return VisualCalibrationPackV1Rules.ToDto(existing, VisualCalibrationPackV1Rules.GateInvalidState);

        await GenerateGate.WaitAsync(cancellationToken);
        VisualCalibrationGenerationOutcome outcome;
        try
        {
            outcome = await VisualCalibrationGenerationServiceV1.ExecuteAsync(
                working, _provider, request.Confirm, request.Generate, cancellationToken,
                async (snap, ct) =>
                {
                    await _repo.UpdateRulesJsonAsync(
                        row.Id, VisualCalibrationPackV1Rules.MergeSnapshot(row.RulesJson, snap), ct);
                });
        }
        finally
        {
            GenerateGate.Release();
        }
        if (outcome.GateCode == VisualCalibrationPackV1Rules.GateConfirmation)
            return VisualCalibrationPackV1Rules.ToDto(working, outcome.GateCode);

        await _repo.UpdateRulesJsonAsync(
            row.Id, VisualCalibrationPackV1Rules.MergeSnapshot(row.RulesJson, outcome.Pack), cancellationToken);
        _ = actor;
        var dto = await WithImpact(
            VisualCalibrationPackV1Rules.ToDto(outcome.Pack, outcome.GateCode, outcome.ProviderCalled),
            cancellationToken);
        return dto with
        {
            ProviderCalled = outcome.ProviderCalled,
            GenerationExecuted = outcome.GenerationExecuted,
            GeminiCalled = outcome.ProviderCalled,
            StaffMessage = outcome.GateCode == VisualCalibrationPackV1Rules.GateDuplicate
                ? "Calibration slot already has a valid PIXEL. Retry is a separate action."
                : outcome.GateCode == VisualCalibrationPackV1Rules.GateIncomplete
                    ? "Calibration generation is incomplete. Coverage is not 24/24. Status is not PENDING_REVIEW."
                    : request.Generate
                        ? null
                        : "Compiled 24 calibration requests. Provider was not called.",
        };
    }

    public async Task<VisualCalibrationPackV1Rules.PackSnapshot> GetSnapshotAsync(
        string packId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetInForceAsync(Project(), ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : VisualCalibrationPackV1Rules.ReadSnapshot(row.RulesJson);
        var pack = snap ?? VisualCalibrationPackV1Rules.CompilePack();
        if (!string.IsNullOrWhiteSpace(packId)
            && snap is not null
            && !string.Equals(snap.PackId, packId, StringComparison.OrdinalIgnoreCase)
            && packId != VisualCalibrationPackV1Rules.DefaultPackId)
            return pack;
        return pack;
    }

    public async Task<VisualCalibrationPackDto> GetAsync(string packId, CancellationToken cancellationToken = default)
    {
        var pack = await GetSnapshotAsync(packId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(packId)
            && packId != VisualCalibrationPackV1Rules.DefaultPackId
            && !string.Equals(pack.PackId, packId, StringComparison.OrdinalIgnoreCase))
            return VisualCalibrationPackV1Rules.ToDto(pack, VisualCalibrationPackV1Rules.GateInvalidState);
        return await WithImpact(VisualCalibrationPackV1Rules.ToDto(pack), cancellationToken);
    }

    public async Task<IReadOnlyList<VisualCalibrationArtifactDto>> GetArtifactsAsync(
        string packId, CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(packId, cancellationToken);
        return dto.Subjects.SelectMany(s => s.Artifacts).ToList();
    }

    public async Task<VisualCalibrationCoverageDto> GetCoverageAsync(
        string packId, CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(packId, cancellationToken);
        return dto.Coverage ?? new VisualCalibrationCoverageDto(24, 0, 0, 24);
    }

    public async Task<(byte[] Bytes, string Mime)?> ReadSlotImageAsync(
        string packId, string subjectId, string viewType, CancellationToken cancellationToken = default)
    {
        var dto = await GetAsync(packId, cancellationToken);
        var view = (viewType ?? "").Trim().ToUpperInvariant();
        var art = dto.Subjects
            .FirstOrDefault(s => string.Equals(s.CalibrationSubjectId, subjectId, StringComparison.OrdinalIgnoreCase))
            ?.Artifacts.FirstOrDefault(a => string.Equals(a.ViewType, view, StringComparison.OrdinalIgnoreCase));
        if (art is null || string.IsNullOrWhiteSpace(art.Path)
            || !string.Equals(art.Kind, VisualCalibrationPackV1Rules.KindPixel, StringComparison.OrdinalIgnoreCase))
            return null;
        var bytes = _artifacts.Read(art.Path);
        return bytes is { Length: > 32 }
            ? (bytes, KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg")
            : null;
    }

    public Task<VisualCalibrationPackDto> ApproveAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, CancellationToken cancellationToken = default) =>
        AdvanceAsync(packId, request, actor, "APPROVE", cancellationToken);

    public Task<VisualCalibrationPackDto> RejectAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, CancellationToken cancellationToken = default) =>
        AdvanceAsync(packId, request, actor, "REJECT", cancellationToken);

    public Task<VisualCalibrationPackDto> LockAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, CancellationToken cancellationToken = default) =>
        AdvanceAsync(packId, request, actor, "LOCK", cancellationToken);

    private async Task<VisualCalibrationPackDto> AdvanceAsync(
        string packId, VisualCalibrationRequestDto? request, string actor, string action,
        CancellationToken cancellationToken)
    {
        var row = await _repo.GetInForceAsync(Project(), ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? VisualCalibrationPackV1Rules.CompilePack()
            : VisualCalibrationPackV1Rules.ReadSnapshot(row.RulesJson) ?? VisualCalibrationPackV1Rules.CompilePack();
        if (!request?.Confirm ?? true)
            return VisualCalibrationPackV1Rules.ToDto(snap, VisualCalibrationPackV1Rules.GateConfirmation);

        if (action == "APPROVE")
        {
            var gate = VisualCalibrationPackV1Rules.EvaluateApprove(
                snap, request?.DirectorPass == true, request?.PhotorealismLevel ?? snap.PhotorealismLevel);
            if (gate is not null)
                return VisualCalibrationPackV1Rules.ToDto(snap, gate);
            snap = VisualCalibrationPackV1Rules.Approve(snap);
        }
        else if (action == "REJECT")
        {
            snap = VisualCalibrationPackV1Rules.Reject(snap, request?.RejectionReason);
        }
        else
        {
            var vua = await _universe.GetAsync(Project(), cancellationToken);
            var vuaLocked = vua.CurrentAuthority
                || FamixaVisualUniverseAuthorityV1Rules.IsAuthority(vua.Status);
            var gate = VisualCalibrationPackV1Rules.EvaluateLock(snap.Status, true, vuaLocked);
            if (gate is not null)
                return VisualCalibrationPackV1Rules.ToDto(snap, gate);
            snap = VisualCalibrationPackV1Rules.Lock(snap, actor);
            var promoted = await _universe.LockAsync(
                Project(), new VisualUniverseAuthorityRequestDto(Project(), true), actor, cancellationToken);
            if (promoted.CurrentAuthority && FamixaVisualUniverseAuthorityV1Rules.IsAuthority(promoted.Status))
            {
                snap = VisualCalibrationPackV1Rules.MarkAuthorityTransition(snap);
                await MaterializeStaleAsync(snap.VisualUniverseSha, cancellationToken);
            }
            else
            {
                var dto = await WithImpact(VisualCalibrationPackV1Rules.ToDto(
                    snap, promoted.GateCode ?? VisualCalibrationPackV1Rules.GateVuaPromotion), cancellationToken);
                if (row is not null)
                    await _repo.UpdateRulesJsonAsync(
                        row.Id, VisualCalibrationPackV1Rules.MergeSnapshot(row.RulesJson, snap), cancellationToken);
                return dto with
                {
                    StaffMessage = "Calibration pack LOCKED. Visual Universe Authority was not promoted — VUA must be APPROVED before official LockAsync succeeds.",
                    AuthorityTransitioned = false,
                    CurrentAuthority = false,
                };
            }
        }

        if (row is not null)
            await _repo.UpdateRulesJsonAsync(
                row.Id, VisualCalibrationPackV1Rules.MergeSnapshot(row.RulesJson, snap), cancellationToken);
        _ = packId;
        return await WithImpact(VisualCalibrationPackV1Rules.ToDto(snap), cancellationToken);
    }

    private async Task MaterializeStaleAsync(string currentAuthoritySha, CancellationToken ct)
    {
        IReadOnlyList<FamixaCharacterDto> rows;
        try { rows = await _characters.ListAsync(ct); }
        catch { return; }

        foreach (var row in rows.Where(r => !string.Equals(r.Visual, "voice", StringComparison.OrdinalIgnoreCase)))
        {
            var id = CharacterStudioV1Rules.NormalizeCharacterId(row.CharacterCode);
            var locked = await OfficialLockedAsync(id, row.Lifecycle, ct);
            if (!VisualCalibrationPackV1Rules.MayMaterializeStale(locked, true))
                continue;
            var asset = await _store.GetAssetAsync(id, ct);
            if (asset is null) continue;
            var patch = new JsonObject
            {
                ["visualUniverseStale"] = true,
                ["visualUniverseAuthoritySha"] = currentAuthoritySha,
            };
            await _assets.MergeAssetExtraAsync(asset.AssetId, patch.ToJsonString(), ct);
        }
    }

    private async Task<bool> OfficialLockedAsync(string characterId, string? lifecycle, CancellationToken ct)
    {
        if (string.Equals(lifecycle, "locked", StringComparison.OrdinalIgnoreCase))
            return true;
        try
        {
            var init = await _init.GetAsync(characterId, null, "ERA-01", ct);
            return init.AuthorityLocked;
        }
        catch
        {
            return false;
        }
    }

    private async Task<VisualCalibrationPackDto> WithImpact(VisualCalibrationPackDto dto, CancellationToken ct)
    {
        try
        {
            var rows = await _characters.ListAsync(ct);
            var people = rows.Where(r => !string.Equals(r.Visual, "voice", StringComparison.OrdinalIgnoreCase)).ToList();
            var impactRows = new List<VisualCalibrationImpactRowDto>();
            foreach (var row in people)
            {
                var id = CharacterStudioV1Rules.NormalizeCharacterId(row.CharacterCode);
                var locked = await OfficialLockedAsync(id, row.Lifecycle, ct);
                var bound = ProjectVisualStyleV2Rules.ProtectedV1Sha;
                var stale = dto.AuthorityTransitioned
                    && VisualCalibrationPackV1Rules.VisualUniverseStale(bound, dto.VisualUniverseSha)
                    && !locked;
                impactRows.Add(new VisualCalibrationImpactRowDto(
                    id, locked, FamixaVisualUniverseAuthorityV1Rules.MutationForbidden(locked),
                    stale, bound));
            }

            var lockedCount = impactRows.Count(r => r.OfficialLocked);
            return dto with
            {
                Impact = new VisualCalibrationImpactDto(
                    people.Count, lockedCount, impactRows.Count(r => r.VisualUniverseStale), impactRows),
            };
        }
        catch
        {
            return dto;
        }
    }

    private static string Project() => VisualCalibrationPackV1Rules.ProjectId;
}
