using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class VisualUniverseAuthority : IVisualUniverseAuthority
{
    private readonly KitVideoVisualSystemRepository _repo;
    private readonly ICharacterGenerationProvider _provider;
    private readonly KitVideoArtifactStore _artifacts;

    public VisualUniverseAuthority(
        KitVideoVisualSystemRepository repo,
        ICharacterGenerationProvider provider,
        KitVideoArtifactStore artifacts)
    {
        _repo = repo;
        _provider = provider;
        _artifacts = artifacts;
    }

    public IReadOnlyList<string> RunRegression() => FamixaVisualUniverseAuthorityV1Regression.Run();

    public async Task<VisualUniverseAuthorityDto> GetAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var project = Normalize(projectId);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : FamixaVisualUniverseAuthorityV1Rules.ReadSnapshot(row.RulesJson);
        return ToDto(project, snap, false, false);
    }

    public async Task<VisualUniverseAuthorityDto> RequestRevisionAsync(
        string projectId, VisualUniverseAuthorityRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var project = Normalize(string.IsNullOrWhiteSpace(request.ProjectId) ? projectId : request.ProjectId);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : FamixaVisualUniverseAuthorityV1Rules.ReadSnapshot(row.RulesJson);
        var gate = FamixaVisualUniverseAuthorityV1Rules.EvaluateCreate(snap?.Status, request.Confirm, row is not null);
        if (gate is not null)
            return ToDto(project, snap, false, false, gate);
        var next = new FamixaVisualUniverseAuthorityV1Rules.AuthoritySnapshot(
            FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationPending,
            FamixaVisualUniverseAuthorityV1Rules.Version,
            FamixaVisualUniverseAuthorityV1Rules.Sha(),
            row is null ? ProjectVisualStyleV2Rules.ProtectedV1Sha : null,
            FamixaVisualUniverseAuthorityV1Rules.DesignLanguageSha(),
            FamixaVisualUniverseAuthorityV1Rules.StyleReferencePackSha(),
            FamixaVisualUniverseAuthorityV1Rules.StyleCalibrationPackSha(),
            FamixaVisualUniverseAuthorityV1Rules.Fingerprint(ProjectVisualStyleV2Rules.ProtectedV1Sha),
            actor, null, null, null, request.Notes,
            FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationPending,
            CompileSlots(null));
        await _repo.UpdateRulesJsonAsync(row!.Id, FamixaVisualUniverseAuthorityV1Rules.MergeSnapshot(row.RulesJson, next), cancellationToken);
        return ToDto(project, next, false, false);
    }

    public async Task<VisualUniverseCalibrationDto> GetCalibrationAsync(
        string projectId, CancellationToken cancellationToken = default)
    {
        var row = await _repo.GetInForceAsync(Normalize(projectId), ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : FamixaVisualUniverseAuthorityV1Rules.ReadSnapshot(row.RulesJson);
        return FamixaVisualUniverseAuthorityV1Rules.ToCalibrationDto(snap?.Slots, snap?.CalibrationStatus ?? snap?.Status);
    }

    public async Task<VisualUniverseCalibrationDto> RequestCalibrationAsync(
        string projectId, VisualUniverseAuthorityRequestDto request, string actor,
        CancellationToken cancellationToken = default)
    {
        var project = Normalize(string.IsNullOrWhiteSpace(request.ProjectId) ? projectId : request.ProjectId);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : FamixaVisualUniverseAuthorityV1Rules.ReadSnapshot(row.RulesJson);
        var gate = FamixaVisualUniverseAuthorityV1Rules.EvaluateCalibrate(snap?.Status, request.Confirm);
        if (gate is not null || row is null || snap is null)
            return FamixaVisualUniverseAuthorityV1Rules.ToCalibrationDto(snap?.Slots, snap?.Status)
                with { ProviderCalled = false, GeminiCalled = false, GenerationExecuted = false };

        var slots = CompileSlots(snap.Slots).ToList();
        var called = false;
        if (request.Generate)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var generated = await _provider.GenerateMasterAsync(
                    new CharacterAuthorityGenerationRequest(
                        "VUA", "CAL", slots[i].Code, slots[i].Prompt, "3:4", "V1"),
                    cancellationToken);
                called = generated.ProviderCalled;
                if (!generated.Succeeded || generated.Bytes is not { Length: > 32 })
                    break;
                var persist = _artifacts.PersistMaster("VUA", "CAL", $"VUA-CAL-{slots[i].Code}", generated.Bytes);
                if (!persist.Check.Ok) break;
                slots[i] = slots[i] with
                {
                    Path = persist.Path,
                    Sha256 = KitVideoIntegrityRules.Sha256Hex(persist.Jpeg),
                };
            }
        }

        var complete = slots.Count == 4 && slots.All(s => !string.IsNullOrWhiteSpace(s.Path));
        var next = snap with
        {
            Status = complete
                ? FamixaVisualUniverseAuthorityV1Rules.StatusPendingReview
                : FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationPending,
            CalibrationStatus = complete
                ? FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationReady
                : FamixaVisualUniverseAuthorityV1Rules.StatusCalibrationPending,
            RequestedBy = actor,
            Slots = slots,
            Notes = request.Notes,
        };
        await _repo.UpdateRulesJsonAsync(row.Id, FamixaVisualUniverseAuthorityV1Rules.MergeSnapshot(row.RulesJson, next), cancellationToken);
        return FamixaVisualUniverseAuthorityV1Rules.ToCalibrationDto(slots, next.Status) with
        {
            ProviderCalled = called,
            GeminiCalled = called,
            GenerationExecuted = called,
        };
    }

    public Task<VisualUniverseAuthorityDto> ApproveAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor,
        CancellationToken cancellationToken = default) =>
        AdvanceAsync(projectId, request, actor, "APPROVE", cancellationToken);

    public Task<VisualUniverseAuthorityDto> RejectAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor,
        CancellationToken cancellationToken = default) =>
        AdvanceAsync(projectId, request, actor, "REJECT", cancellationToken);

    public Task<VisualUniverseAuthorityDto> LockAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor,
        CancellationToken cancellationToken = default) =>
        AdvanceAsync(projectId, request, actor, "LOCK", cancellationToken);

    private async Task<VisualUniverseAuthorityDto> AdvanceAsync(
        string projectId, VisualUniverseAuthorityRequestDto? request, string actor, string action,
        CancellationToken cancellationToken)
    {
        var project = Normalize(string.IsNullOrWhiteSpace(request?.ProjectId) ? projectId : request!.ProjectId);
        var row = await _repo.GetInForceAsync(project, ProjectVisualStyleV1Rules.SystemCode, cancellationToken);
        var snap = row is null ? null : FamixaVisualUniverseAuthorityV1Rules.ReadSnapshot(row.RulesJson);
        var gate = FamixaVisualUniverseAuthorityV1Rules.EvaluateAdvance(snap?.Status, action);
        if (gate is not null || row is null || snap is null)
            return ToDto(project, snap, false, false, gate);

        var status = action switch
        {
            "APPROVE" => FamixaVisualUniverseAuthorityV1Rules.StatusApproved,
            "REJECT" => FamixaVisualUniverseAuthorityV1Rules.StatusRejected,
            "LOCK" => FamixaVisualUniverseAuthorityV1Rules.StatusLocked,
            _ => snap.Status,
        };
        var next = snap with
        {
            Status = status,
            ReviewedBy = actor,
            ReviewDecision = action,
            RejectionReason = request?.RejectionReason,
            Notes = request?.Notes,
        };
        await _repo.UpdateRulesJsonAsync(row.Id, FamixaVisualUniverseAuthorityV1Rules.MergeSnapshot(row.RulesJson, next), cancellationToken);
        return ToDto(project, next, false, false);
    }

    private static IReadOnlyList<FamixaVisualUniverseAuthorityV1Rules.CalibrationSlotSnapshot> CompileSlots(
        IReadOnlyList<FamixaVisualUniverseAuthorityV1Rules.CalibrationSlotSnapshot>? existing) =>
        FamixaVisualUniverseAuthorityV1Rules.CalibrationArchetypes.Select(e =>
        {
            var prev = existing?.FirstOrDefault(s => s.Code == e.Code);
            return new FamixaVisualUniverseAuthorityV1Rules.CalibrationSlotSnapshot(
                e.Code, e.Label, prev?.Path, prev?.Sha256,
                FamixaVisualUniverseAuthorityV1Rules.CompileCalibrationPrompt(e));
        }).ToList();

    private static VisualUniverseAuthorityDto ToDto(
        string project,
        FamixaVisualUniverseAuthorityV1Rules.AuthoritySnapshot? snap,
        bool providerCalled,
        bool geminiCalled,
        string? gate = null)
    {
        var status = snap?.Status ?? FamixaVisualUniverseAuthorityV1Rules.StatusDraft;
        var dto = FamixaVisualUniverseAuthorityV1Rules.ToDto(status, FamixaVisualUniverseAuthorityV1Rules.IsAuthority(status));
        var cal = FamixaVisualUniverseAuthorityV1Rules.ToCalibrationDto(snap?.Slots, snap?.CalibrationStatus ?? status);
        return dto with
        {
            ProjectId = project,
            Calibration = cal.Slots,
            ProviderCalled = providerCalled,
            GeminiCalled = geminiCalled,
            Persisted = snap is not null,
            GateCode = gate,
            StaffMessage = gate switch
            {
                FamixaVisualUniverseAuthorityV1Rules.GateConfirmation => "Cần xác nhận tạo Visual Universe Revision.",
                FamixaVisualUniverseAuthorityV1Rules.GateNotReady => "Project Visual Style chưa sẵn sàng.",
                FamixaVisualUniverseAuthorityV1Rules.GateOpenRevision => "Đang có Visual Universe Revision chưa khóa.",
                FamixaVisualUniverseAuthorityV1Rules.GateNotApproved => "Cần duyệt Visual Universe trước khi khóa.",
                FamixaVisualUniverseAuthorityV1Rules.GateInvalidState => "Visual Universe không ở trạng thái hợp lệ.",
                _ => FamixaVisualUniverseAuthorityV1Rules.IsAuthority(status)
                    ? "Visual Universe đã khóa. Character mới phải kế thừa authority này."
                    : "PVS V1 vẫn là production authority cho đến khi Director khóa Visual Universe.",
            },
        };
    }

    private static string Normalize(string? projectId)
    {
        var t = (projectId ?? "").Trim().ToUpperInvariant();
        return string.IsNullOrWhiteSpace(t) ? ProjectVisualStyleV1Rules.DefaultProject : t;
    }
}
