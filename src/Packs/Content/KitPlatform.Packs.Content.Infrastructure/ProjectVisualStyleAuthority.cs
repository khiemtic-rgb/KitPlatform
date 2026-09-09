using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProjectVisualStyleAuthority : IProjectVisualStyleAuthority
{
    private readonly KitVideoVisualSystemRepository _repo;
    private readonly CharacterAuthorityStore _store;
    private readonly KitVideoMasterReferenceRepository _assets;
    private readonly IFamixaCharacterService _characters;
    private readonly ICharacterAuthorityInitializationV1Service _init;
    private readonly ICharacterReferencePackService _officialCrp;

    public ProjectVisualStyleAuthority(
        KitVideoVisualSystemRepository repo,
        CharacterAuthorityStore store,
        KitVideoMasterReferenceRepository assets,
        IFamixaCharacterService characters,
        ICharacterAuthorityInitializationV1Service init,
        ICharacterReferencePackService officialCrp)
    {
        _repo = repo;
        _store = store;
        _assets = assets;
        _characters = characters;
        _init = init;
        _officialCrp = officialCrp;
    }

    public IReadOnlyList<string> RunRegression() => ProjectVisualStyleV1Regression.Run();

    public IReadOnlyList<string> RunRevisionRegression() => ProjectVisualStyleV2Regression.Run();

    public async Task<ProjectVisualStyleV2DefinitionDto> GetV2Async(
        string projectId, CancellationToken cancellationToken = default)
    {
        var current = await GetActiveAsync(projectId, cancellationToken);
        var snapStatus = current.Revision?.Status;
        var status = string.IsNullOrWhiteSpace(snapStatus)
            ? ProjectVisualStyleV2Rules.StatusDraft
            : snapStatus;
        return new ProjectVisualStyleV2DefinitionDto(
            ProjectVisualStyleV2Rules.DocumentId,
            ProjectVisualStyleV2Rules.Version,
            ProjectVisualStyleV2Rules.StyleName,
            ProjectVisualStyleV2Rules.DisplayName,
            ProjectVisualStyleV2Rules.StyleIntent,
            ProjectVisualStyleV2Rules.CanonicalStyleDescription,
            ProjectVisualStyleV2Rules.StylizationLevel,
            ProjectVisualStyleV2Rules.PhotorealismLevel,
            ProjectVisualStyleV2Rules.CharacterReadability,
            ProjectVisualStyleV2Rules.Sha(),
            status,
            ProjectVisualStyleV2Rules.Canonical(),
            ProjectVisualStyleV2Rules.BuildPrompt(),
            ProjectVisualStyleV2Rules.NegativeStyleBlock,
            current.Version,
            current.Sha,
            ProjectVisualStyleV2Rules.CurrentRemainsAuthority(status == ProjectVisualStyleV2Rules.StatusDraft ? null : status),
            ProjectVisualStyleV2Rules.CandidateIsAuthority(status),
            false,
            false,
            current.Revision);
    }

    public IReadOnlyList<ProjectVisualStylePresetDto> Presets() =>
        ProjectVisualStyleV1Rules.Presets
            .Select(p => new ProjectVisualStylePresetDto(p.StyleKey, p.StyleName, p.Description, p.PreviewBullets))
            .ToList();

    public async Task<ProjectVisualStyleDto> GetActiveAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(projectId);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        return row is null ? Empty(project) : ToDto(row, project);
    }

    public async Task<ProjectVisualStyleDto?> GetLockedAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(projectId);
        var rows = await _repo.ListBySystemAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var locked = rows
            .Where(r => string.Equals(r.Status, "locked", StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Version, StringComparer.OrdinalIgnoreCase)
            .LastOrDefault();
        return locked is null ? null : ToDto(locked, project);
    }

    public async Task<ProjectVisualStyleDto> ValidateAsync(string projectId, CancellationToken cancellationToken = default) =>
        await GetActiveAsync(projectId, cancellationToken);

    public async Task<string?> GetCanonicalAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var dto = await GetActiveAsync(projectId, cancellationToken);
        return dto.Ready ? dto.Canonical : null;
    }

    public async Task<string?> GetShaAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var dto = await GetActiveAsync(projectId, cancellationToken);
        return dto.Ready ? dto.Sha : null;
    }

    public async Task<ProjectVisualStyleCharacterBindDto> ResolveForCharacterAsync(
        string projectId, string characterId, CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(projectId);
        var style = await GetActiveAsync(project, cancellationToken);
        var id = CharacterStudioV1Rules.NormalizeCharacterId(characterId);
        var storedKey = await ReadStoredStyleKeyAsync(id, cancellationToken);
        var inherit = style.Ready
            ? ProjectVisualStyleV1Rules.InheritStatus(storedKey, style.StyleKey ?? "")
            : ProjectVisualStyleV1Rules.NotConfigured;
        return new ProjectVisualStyleCharacterBindDto(
            id, inherit, style.Id?.ToString(), style.Sha, style.StyleKey);
    }

    public async Task<ProjectVisualStyleDto> InitializeAsync(
        ProjectVisualStyleInitializeRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(request.ProjectId);
        var existing = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        if (existing is not null)
            return ToDto(existing, project);

        if (!request.Confirm)
            throw new InvalidOperationException("CONFIRMATION_REQUIRED: cần xác nhận phong cách hình ảnh của dự án.");

        var preset = ProjectVisualStyleV1Rules.PresetOf(request.PresetKey)
            ?? throw new InvalidOperationException("STYLE_PRESET_INVALID: preset không hợp lệ.");
        var projectGuid = await _repo.GetProjectIdAsync(project, cancellationToken)
            ?? throw new InvalidOperationException("PROJECT_NOT_FOUND: " + project);

        var id = Guid.NewGuid();
        var sha = ProjectVisualStyleV1Rules.Sha(preset);
        var status = request.Activate ? ProjectVisualStyleV1Rules.Active : ProjectVisualStyleV1Rules.Draft;
        var doc = ProjectVisualStyleV1Rules.PersistDocument(id, project, preset, "V1", status, sha, actor);
        await _repo.InsertAsync(
            id, projectGuid, ProjectVisualStyleV1Rules.SystemCode, "V1",
            ProjectVisualStyleV1Rules.DbStatusOf(status),
            JsonSerializer.Serialize(doc),
            cancellationToken);

        await BindCompatibleCharactersAsync(project, id.ToString(), sha, preset.StyleKey, cancellationToken);
        return await GetActiveAsync(project, cancellationToken);
    }

    public async Task<ProjectVisualStyleDto> CreateVersionAsync(
        ProjectVisualStyleInitializeRequest request, string actor, CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(request.ProjectId);
        var current = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        if (current is not null
            && string.Equals(current.Status, "locked", StringComparison.OrdinalIgnoreCase)
            && string.Equals(current.Version, request.PresetKey, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProjectVisualStyleV1Rules.GateLocked + ": không sửa style đã khóa.");

        if (!request.Confirm)
            throw new InvalidOperationException("CONFIRMATION_REQUIRED: cần xác nhận phiên bản phong cách mới.");

        var preset = ProjectVisualStyleV1Rules.PresetOf(request.PresetKey)
            ?? throw new InvalidOperationException("STYLE_PRESET_INVALID: preset không hợp lệ.");
        var projectGuid = await _repo.GetProjectIdAsync(project, cancellationToken)
            ?? throw new InvalidOperationException("PROJECT_NOT_FOUND: " + project);

        var nextVersion = ProjectVisualStyleV1Rules.NextVersion(current?.Version);
        var id = Guid.NewGuid();
        var sha = ProjectVisualStyleV1Rules.Sha(preset);
        var status = request.Activate ? ProjectVisualStyleV1Rules.Active : ProjectVisualStyleV1Rules.Draft;
        var doc = ProjectVisualStyleV1Rules.PersistDocument(id, project, preset, nextVersion, status, sha, actor);
        await _repo.InsertAsync(
            id, projectGuid, ProjectVisualStyleV1Rules.SystemCode, nextVersion,
            ProjectVisualStyleV1Rules.DbStatusOf(status),
            JsonSerializer.Serialize(doc),
            cancellationToken);
        if (current is not null && request.Activate
            && !string.Equals(current.Status, "locked", StringComparison.OrdinalIgnoreCase))
            await _repo.SupersedeAsync(current.Id, id, cancellationToken);

        return await GetActiveAsync(project, cancellationToken);
    }

    public async Task<ProjectVisualStyleRevisionResultDto> RequestRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(string.IsNullOrWhiteSpace(request.ProjectId) ? projectId : request.ProjectId);
        var current = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var dto = current is null ? Empty(project) : ToDto(current, project);
        var existing = current is null ? null : ProjectVisualStyleV2Rules.ReadRevision(current.RulesJson);
        var gate = ProjectVisualStyleV2Rules.EvaluateCreate(new ProjectVisualStyleV2Rules.RevisionSource(
            dto.Ready, dto.Version, dto.Sha, existing?.Status, request.Confirm, actor));
        if (gate is not null)
            return RevisionResult(project, existing?.Status ?? "", gate, dto, existing, await ImpactOfAsync(project, existing, dto, cancellationToken));

        var snap = new ProjectVisualStyleV2Rules.RevisionSnapshot(
            ProjectVisualStyleV2Rules.StatusPendingReview,
            ProjectVisualStyleV2Rules.Version,
            ProjectVisualStyleV2Rules.Sha(),
            dto.Sha,
            dto.Version,
            ProjectVisualStyleV2Rules.Fingerprint(dto.Sha),
            actor,
            null, null, null, request.Notes);
        await _repo.UpdateRulesJsonAsync(current!.Id, ProjectVisualStyleV2Rules.MergeRevision(current.RulesJson, snap), cancellationToken);
        var next = await GetActiveAsync(project, cancellationToken);
        return RevisionResult(project, snap.Status, null, next, snap, await ImpactOfAsync(project, snap, next, cancellationToken));
    }

    public async Task<ProjectVisualStyleRevisionResultDto> ApproveRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor,
        CancellationToken cancellationToken = default) =>
        await AdvanceRevisionAsync(projectId, request, actor, "APPROVE", cancellationToken);

    public async Task<ProjectVisualStyleRevisionResultDto> RejectRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor,
        CancellationToken cancellationToken = default) =>
        await AdvanceRevisionAsync(projectId, request, actor, "REJECT", cancellationToken);

    public async Task<ProjectVisualStyleRevisionResultDto> LockRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor,
        CancellationToken cancellationToken = default) =>
        await AdvanceRevisionAsync(projectId, request, actor, "LOCK", cancellationToken);

    public async Task<ProjectVisualStyleImpactDto> ImpactAsync(
        string projectId, CancellationToken cancellationToken = default)
    {
        var project = NormalizeProject(projectId);
        var current = await GetActiveAsync(project, cancellationToken);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : ProjectVisualStyleV2Rules.ReadRevision(row.RulesJson);
        return await ImpactOfAsync(project, snap, current, cancellationToken);
    }

    private async Task<ProjectVisualStyleRevisionResultDto> AdvanceRevisionAsync(
        string projectId, ProjectVisualStyleRevisionRequestDto? request, string actor, string action,
        CancellationToken cancellationToken)
    {
        var project = NormalizeProject(string.IsNullOrWhiteSpace(request?.ProjectId) ? projectId : request!.ProjectId);
        var current = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken)
            ?? throw new InvalidOperationException(ProjectVisualStyleV2Rules.GateNotReady + ": " + ProjectVisualStyleV1Rules.StaffNotConfigured);
        var dto = ToDto(current, project);
        var snap = ProjectVisualStyleV2Rules.ReadRevision(current.RulesJson);
        var gate = ProjectVisualStyleV2Rules.EvaluateAdvance(snap?.Status, action);
        if (gate is not null)
            return RevisionResult(project, snap?.Status ?? "", gate, dto, snap, await ImpactOfAsync(project, snap, dto, cancellationToken));

        if (action == "LOCK")
        {
            var projectGuid = await _repo.GetProjectIdAsync(project, cancellationToken)
                ?? throw new InvalidOperationException("PROJECT_NOT_FOUND: " + project);
            var id = Guid.NewGuid();
            var sha = ProjectVisualStyleV2Rules.Sha();
            var locked = snap! with
            {
                Status = ProjectVisualStyleV2Rules.StatusLocked,
                ReviewedBy = actor,
                ReviewDecision = "LOCKED",
                Notes = request?.Notes,
            };
            await _repo.UpdateRulesJsonAsync(current.Id, ProjectVisualStyleV2Rules.MergeRevision(current.RulesJson, locked), cancellationToken);
            await _repo.InsertAsync(
                id, projectGuid, ProjectVisualStyleV1Rules.SystemCode, ProjectVisualStyleV2Rules.Version,
                ProjectVisualStyleV1Rules.DbStatusOf(ProjectVisualStyleV1Rules.Locked),
                JsonSerializer.Serialize(ProjectVisualStyleV2Rules.PersistV2Document(
                    id, project, ProjectVisualStyleV1Rules.Locked, sha, actor)),
                cancellationToken);
            await MarkUnlockedStaleAsync(dto.Sha, sha, cancellationToken);
            var next = await GetActiveAsync(project, cancellationToken);
            return RevisionResult(project, locked.Status, null, next, locked, await ImpactOfAsync(project, locked, next, cancellationToken));
        }

        var nextStatus = action == "APPROVE"
            ? ProjectVisualStyleV2Rules.StatusApproved
            : ProjectVisualStyleV2Rules.StatusRejected;
        var advanced = snap! with
        {
            Status = nextStatus,
            ReviewedBy = actor,
            ReviewDecision = nextStatus,
            RejectionReason = action == "REJECT" ? request?.RejectionReason : snap.RejectionReason,
            Notes = request?.Notes,
        };
        await _repo.UpdateRulesJsonAsync(current.Id, ProjectVisualStyleV2Rules.MergeRevision(current.RulesJson, advanced), cancellationToken);
        var after = await GetActiveAsync(project, cancellationToken);
        return RevisionResult(project, advanced.Status, null, after, advanced, await ImpactOfAsync(project, advanced, after, cancellationToken));
    }

    private async Task MarkUnlockedStaleAsync(string? previousSha, string nextSha, CancellationToken ct)
    {
        IReadOnlyList<FamixaCharacterDto> rows;
        try { rows = await _characters.ListAsync(ct); }
        catch (InvalidOperationException) { return; }

        foreach (var row in rows.Where(r => !string.Equals(r.Visual, "voice", StringComparison.OrdinalIgnoreCase)))
        {
            var id = CharacterStudioV1Rules.NormalizeCharacterId(row.CharacterCode);
            if (await OfficialLockedAsync(id, ct))
                continue;
            var asset = await _store.GetAssetAsync(id, ct);
            if (asset is null) continue;
            var patch = new JsonObject
            {
                ["projectVisualStyle"] = new JsonObject
                {
                    ["previousSha"] = previousSha,
                    ["sha"] = nextSha,
                    ["staleAfterStyleLock"] = true,
                    ["authority"] = ProjectVisualStyleV1Rules.StaffAuthority,
                },
            };
            await _assets.MergeAssetExtraAsync(asset.AssetId, patch.ToJsonString(), ct);
        }
    }

    private async Task<ProjectVisualStyleImpactDto> ImpactOfAsync(
        string project,
        ProjectVisualStyleV2Rules.RevisionSnapshot? snap,
        ProjectVisualStyleDto current,
        CancellationToken ct)
    {
        IReadOnlyList<FamixaCharacterDto> rows;
        try { rows = await _characters.ListAsync(ct); }
        catch (InvalidOperationException) { rows = []; }

        var nextSha = snap?.CandidateSha ?? ProjectVisualStyleV2Rules.Sha();
        var previousSha = current.Sha;
        var items = new List<ProjectVisualStyleImpactCharacterDto>();
        foreach (var row in rows.Where(r => !string.Equals(r.Visual, "voice", StringComparison.OrdinalIgnoreCase)))
        {
            var id = CharacterStudioV1Rules.NormalizeCharacterId(row.CharacterCode);
            var locked = await OfficialLockedAsync(id, ct);
            var store = await _store.ReadAsync(id, "ERA-01", ct);
            var crp = await _store.ReadCrpAsync(id, "ERA-01", ct);
            CharacterReferencePackDto? official = null;
            try { official = (await _officialCrp.GetAsync(id, "ERA-01", ct)).Pack; }
            catch (InvalidOperationException) { }

            var master = official?.MasterSha256 ?? store?.MasterSha;
            var dna = official?.DnaSha256 ?? store?.DnaSha;
            var prp = official?.PrpSha256 ?? store?.PrpSha;
            var crpSha = official?.PackSha256 ?? crp?.Sha256;
            items.Add(new ProjectVisualStyleImpactCharacterDto(
                id, row.Name, locked, ProjectVisualStyleV2Rules.MutationForbidden(locked),
                master, dna, prp, crpSha,
                ProjectVisualStyleV2Rules.ArtifactStaleAfterLock(master, previousSha, previousSha, nextSha),
                ProjectVisualStyleV2Rules.ArtifactStaleAfterLock(dna, previousSha, previousSha, nextSha),
                ProjectVisualStyleV2Rules.ArtifactStaleAfterLock(prp, previousSha, previousSha, nextSha),
                ProjectVisualStyleV2Rules.ArtifactStaleAfterLock(crpSha, previousSha, previousSha, nextSha)));
        }

        return new ProjectVisualStyleImpactDto(
            ProjectVisualStyleV2Rules.DocumentId, project,
            current.Version, current.Sha,
            snap?.Version, snap?.CandidateSha, snap?.Status,
            items,
            items.Count(i => i.OfficialLocked),
            items.Count(i => i.MasterStale || i.DnaStale || i.PrpStale || i.CrpStale));
    }

    private static ProjectVisualStyleRevisionResultDto RevisionResult(
        string project, string status, string? gate, ProjectVisualStyleDto style,
        ProjectVisualStyleV2Rules.RevisionSnapshot? snap, ProjectVisualStyleImpactDto impact)
    {
        var staff = gate switch
        {
            ProjectVisualStyleV2Rules.GateConfirmation => "Cần xác nhận tạo Visual Style Revision.",
            ProjectVisualStyleV2Rules.GateOpenRevision => "Đang có Visual Style Revision chưa khóa.",
            ProjectVisualStyleV2Rules.GateNotApproved => "Cần duyệt Visual Style trước khi khóa.",
            ProjectVisualStyleV2Rules.GateInvalidState => "Visual Style Revision không ở trạng thái hợp lệ.",
            ProjectVisualStyleV2Rules.GateNotReady => ProjectVisualStyleV1Rules.StaffNotConfigured,
            _ => status == ProjectVisualStyleV2Rules.StatusPendingReview ? "Candidate PVS V2 chờ duyệt."
                : status == ProjectVisualStyleV2Rules.StatusApproved ? "Đã duyệt. Khóa thì V2 mới thành authority."
                : status == ProjectVisualStyleV2Rules.StatusRejected ? "Không đạt. PVS V1 vẫn là authority."
                : status == ProjectVisualStyleV2Rules.StatusLocked ? "PVS V2 đã khóa làm authority."
                : "Project Visual Style.",
        };
        return new ProjectVisualStyleRevisionResultDto(
            ProjectVisualStyleV2Rules.DocumentId, project, status, gate, staff,
            gate == ProjectVisualStyleV2Rules.GateConfirmation, false, false, false, false,
            style.Sha, snap?.CandidateSha, style.Version, snap?.Version,
            ProjectVisualStyleV2Rules.CurrentRemainsAuthority(status),
            ProjectVisualStyleV2Rules.CandidateIsAuthority(status),
            style, style.Revision, impact);
    }

    private async Task BindCompatibleCharactersAsync(
        string project, string styleId, string sha, string styleKey, CancellationToken ct)
    {
        IReadOnlyList<FamixaCharacterDto> rows;
        try { rows = await _characters.ListAsync(ct); }
        catch (InvalidOperationException) { return; }

        foreach (var row in rows.Where(r => !string.Equals(r.Visual, "voice", StringComparison.OrdinalIgnoreCase)))
        {
            var id = CharacterStudioV1Rules.NormalizeCharacterId(row.CharacterCode);
            if (await OfficialLockedAsync(id, ct))
                continue;

            var asset = await _store.GetAssetAsync(id, ct);
            if (asset is null) continue;

            var stored = await ReadStoredStyleKeyAsync(id, ct);
            var inherit = ProjectVisualStyleV1Rules.InheritStatus(stored, styleKey);
            var patch = new JsonObject
            {
                ["projectVisualStyle"] = new JsonObject
                {
                    ["id"] = styleId,
                    ["sha"] = sha,
                    ["styleKey"] = styleKey,
                    ["inheritStatus"] = inherit,
                    ["authority"] = ProjectVisualStyleV1Rules.StaffAuthority,
                },
            };
            await _assets.MergeAssetExtraAsync(asset.AssetId, patch.ToJsonString(), ct);
        }
    }

    private async Task<bool> OfficialLockedAsync(string characterId, CancellationToken ct)
    {
        try
        {
            var init = await _init.GetAsync(characterId, null, "ERA-01", ct);
            var official = await _officialCrp.GetAsync(characterId, "ERA-01", ct);
            return init.AuthorityLocked
                && official.Pack is { CanUse: true, Status: var st }
                && string.Equals(st, "LOCKED", StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private async Task<string?> ReadStoredStyleKeyAsync(string characterId, CancellationToken ct)
    {
        var asset = await _store.GetAssetAsync(characterId, ct);
        if (asset is null || string.IsNullOrWhiteSpace(asset.ExtraJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(asset.ExtraJson);
            if (doc.RootElement.TryGetProperty("projectVisualStyle", out var pvs)
                && pvs.ValueKind == JsonValueKind.Object
                && pvs.TryGetProperty("styleKey", out var key))
                return key.GetString();
            if (doc.RootElement.TryGetProperty("characterStudio", out var studio)
                && studio.ValueKind == JsonValueKind.Object
                && studio.TryGetProperty("styleId", out var styleId))
                return styleId.GetString();
        }
        catch (JsonException)
        {
            return null;
        }
        return null;
    }

    private static string NormalizeProject(string? projectId)
    {
        var t = (projectId ?? "").Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(t) ? ProjectVisualStyleV1Rules.DefaultProject : t;
    }

    private ProjectVisualStyleDto Empty(string project) =>
        new(
            ProjectVisualStyleV1Rules.DocumentId, null, project,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            "V1", ProjectVisualStyleV1Rules.NotConfigured, null, ProjectVisualStyleV1Rules.StaffAuthority,
            null, null, null, null, [], null, null, false,
            ProjectVisualStyleV1Rules.GateNotReady, ProjectVisualStyleV1Rules.StaffNotConfigured, Presets());

    private ProjectVisualStyleDto ToDto(KitVideoVisualSystemRepository.Row row, string project)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.RulesJson) ? "{}" : row.RulesJson);
        var rules = doc.RootElement;
        var style = ProjectVisualStyleV1Rules.FromDocument(rules);
        var logical = ProjectVisualStyleV1Rules.LogicalStatusOf(
            row.Status,
            rules.TryGetProperty("status", out var st) ? st.GetString() : null);
        var sha = rules.TryGetProperty("sha", out var shaEl) ? shaEl.GetString() : ProjectVisualStyleV1Rules.Sha(style);
        var ready = ProjectVisualStyleV1Rules.ValidateReady(logical, sha) == ProjectVisualStyleV1Rules.GateValid;
        var isV2 = string.Equals(row.Version, ProjectVisualStyleV2Rules.Version, StringComparison.OrdinalIgnoreCase)
            || ProjectVisualStyleV1Rules.SameSha(sha, ProjectVisualStyleV2Rules.Sha());
        var preview = isV2 ? ProjectVisualStyleV2Rules.Preview : ProjectVisualStyleV1Rules.PresetOf(style.StyleKey)?.PreviewBullets ?? [];
        var snap = ProjectVisualStyleV2Rules.ReadRevision(row.RulesJson);
        return new ProjectVisualStyleDto(
            isV2 ? ProjectVisualStyleV2Rules.DocumentId : ProjectVisualStyleV1Rules.DocumentId,
            row.Id,
            project,
            style.StyleKey,
            isV2 ? ProjectVisualStyleV2Rules.StyleName : style.StyleName,
            isV2 ? ProjectVisualStyleV2Rules.StyleIntent : style.Description,
            style.RenderingStyle,
            style.CharacterStyle,
            style.EnvironmentStyle,
            style.LightingStyle,
            style.ColorStyle,
            style.CameraStyle,
            style.TextureStyle,
            style.RealismLevel,
            style.AgeRepresentationRule,
            style.AnatomyRule,
            style.ConsistencyRules,
            isV2 ? ProjectVisualStyleV2Rules.NegativeStyleBlock : style.NegativeRules,
            row.Version,
            logical,
            sha,
            ProjectVisualStyleV1Rules.StaffAuthority,
            null,
            null,
            row.LockedAt,
            row.LockedBy,
            preview,
            isV2 ? ProjectVisualStyleV2Rules.BuildPrompt() : ProjectVisualStyleV1Rules.BuildPrompt(style),
            isV2 ? ProjectVisualStyleV2Rules.Canonical() : ProjectVisualStyleV1Rules.Canonical(style),
            ready,
            ready ? ProjectVisualStyleV1Rules.GateValid : ProjectVisualStyleV1Rules.GateNotReady,
            ready ? (isV2 ? ProjectVisualStyleV2Rules.StyleName : style.StyleName) : ProjectVisualStyleV1Rules.StaffNotConfigured,
            Presets(),
            ToRevisionCard(snap, sha, row.Version));
    }

    private static ProjectVisualStyleRevisionCardDto ToRevisionCard(
        ProjectVisualStyleV2Rules.RevisionSnapshot? snap, string? currentSha, string currentVersion)
    {
        var status = snap?.Status ?? "";
        return new ProjectVisualStyleRevisionCardDto(
            status,
            snap?.Version ?? ProjectVisualStyleV2Rules.Version,
            ProjectVisualStyleV2Rules.StyleName,
            ProjectVisualStyleV2Rules.StyleIntent,
            ProjectVisualStyleV2Rules.StylePromptBlock,
            ProjectVisualStyleV2Rules.NegativeStyleBlock,
            ProjectVisualStyleV2Rules.HumanRealismBoundary,
            ProjectVisualStyleV2Rules.FaceStyle,
            ProjectVisualStyleV2Rules.EyeStyle,
            ProjectVisualStyleV2Rules.SkinStyle,
            ProjectVisualStyleV2Rules.HairStyle,
            ProjectVisualStyleV2Rules.BodyStyle,
            ProjectVisualStyleV2Rules.LightingStyle,
            ProjectVisualStyleV2Rules.MaterialStyle,
            ProjectVisualStyleV2Rules.BackgroundStyle,
            ProjectVisualStyleV2Rules.CameraStyle,
            snap?.CandidateSha ?? ProjectVisualStyleV2Rules.Sha(),
            snap?.CurrentSha ?? currentSha,
            snap?.CurrentVersion ?? currentVersion,
            ProjectVisualStyleV2Rules.MayRequest(status),
            ProjectVisualStyleV2Rules.MayApprove(status),
            ProjectVisualStyleV2Rules.MayReject(status),
            ProjectVisualStyleV2Rules.MayLock(status),
            ProjectVisualStyleV2Rules.CurrentRemainsAuthority(status),
            ProjectVisualStyleV2Rules.CandidateIsAuthority(status),
            snap?.Fingerprint,
            null,
            ProjectVisualStyleV2Rules.CanonicalStyleDescription,
            ProjectVisualStyleV2Rules.StylizationLevel,
            ProjectVisualStyleV2Rules.PhotorealismLevel,
            ProjectVisualStyleV2Rules.CharacterReadability,
            ProjectVisualStyleV2Rules.ClothingStyle,
            ProjectVisualStyleV2Rules.EnvironmentStyle,
            ProjectVisualStyleV2Rules.CharacterDesignLanguage);
    }
}
