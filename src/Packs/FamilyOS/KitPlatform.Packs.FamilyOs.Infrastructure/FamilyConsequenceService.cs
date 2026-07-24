using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyConsequenceService : IFamilyConsequenceService
{
    private readonly FamilyConsequenceRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyConsequenceService(
        FamilyConsequenceRepository repo,
        FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task SuggestFromSkipAsync(
        Guid familyId,
        SkipConsequenceSuggestRequest request,
        CancellationToken cancellationToken = default)
    {
        // Ốm / không khỏe — không gợi ý hậu quả (khớp copy Agreement demo)
        if (string.Equals(request.SkipReason, FamilySkipReasons.Sick, StringComparison.OrdinalIgnoreCase))
            return;

        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            return;

        var rules = await _repo.ListAcceptedAccountabilityRulesAsync(familyId, cancellationToken);
        foreach (var rule in rules)
        {
            var (templateId, appliesToMemberId, consequenceCode) =
                FamilyConsequenceRepository.ParseTerms(rule.TermsJson, rule.TargetId);

            if (string.IsNullOrWhiteSpace(consequenceCode))
                continue;

            var templateMatches = !templateId.HasValue
                || (request.TemplateId.HasValue && templateId.Value == request.TemplateId.Value);
            var memberMatches = !appliesToMemberId.HasValue
                || (request.MemberId.HasValue && appliesToMemberId.Value == request.MemberId.Value);

            // Need at least one pin (template or member) so rules aren't global
            if (!templateId.HasValue && !appliesToMemberId.HasValue)
                continue;

            if (!templateMatches || !memberMatches)
                continue;

            var label = await _repo.ResolveConsequenceLabelAsync(
                familyId, consequenceCode, cancellationToken)
                ?? FamilyAccountabilityDefaults.All
                    .FirstOrDefault(i =>
                        i.Kind == FamilyAccountabilityOptionKinds.Consequence &&
                        i.Code == consequenceCode)?.LabelVi
                ?? consequenceCode;

            await _repo.InsertPendingIfAbsentAsync(
                familyId,
                request.DayFlowId,
                request.CommitmentId,
                rule.Id,
                request.MemberId,
                request.FlowDate,
                consequenceCode,
                label,
                request.SkipReason,
                request.CommitmentTitle,
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<FamilyConsequenceEventDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        string? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter = status.Trim().ToLowerInvariant();
            if (!FamilyConsequenceEventStatuses.All.Contains(filter))
                throw new InvalidOperationException(
                    "status phải là pending_confirm | applied | waived.");
        }

        var rows = await _repo.ListAsync(familyId, flowDate, filter, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyConsequenceEventDto> DecideAsync(
        Guid familyId,
        Guid eventId,
        DecideConsequenceEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!FamilyConsequenceEventStatuses.Decide.Contains(status))
            throw new InvalidOperationException("status quyết định phải là applied | waived.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        if (members.All(m => m.Id != request.DecidedBy))
            throw new InvalidOperationException("decidedBy không thuộc gia đình này.");

        var existing = await _repo.GetAsync(familyId, eventId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy hậu quả đề xuất.");

        if (existing.Status != FamilyConsequenceEventStatuses.PendingConfirm)
            throw new InvalidOperationException("Hậu quả đã được quyết định.");

        var note = string.IsNullOrWhiteSpace(request.DecisionNote)
            ? null
            : request.DecisionNote.Trim();

        var updated = await _repo.DecideAsync(
            familyId, eventId, status, request.DecidedBy, note, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được hậu quả.");

        var dto = (await ListAsync(familyId, updated.FlowDate, null, cancellationToken))
            .First(e => e.Id == updated.Id);

        if (status == FamilyConsequenceEventStatuses.Applied)
        {
            var guide = FamilySoftLockGuides.ForConsequence(dto.ConsequenceCode);
            if (guide is not null)
                return dto with { SoftLockGuide = guide };
        }

        return dto;
    }

    private static FamilyConsequenceEventDto Map(FamilyConsequenceRepository.EventRow row)
    {
        SoftLockGuideDto? guide = null;
        if (row.Status == FamilyConsequenceEventStatuses.Applied)
            guide = FamilySoftLockGuides.ForConsequence(row.ConsequenceCode);

        return new FamilyConsequenceEventDto(
            row.Id,
            row.FamilyId,
            row.DayFlowId,
            row.CommitmentId,
            row.AgreementId,
            row.MemberId,
            row.MemberName,
            row.FlowDate,
            row.ConsequenceCode,
            row.LabelVi,
            row.TriggerSkipReason,
            row.CommitmentTitle,
            row.Status,
            row.DecidedBy,
            row.DecidedAt,
            row.DecisionNote,
            row.CreatedAt,
            guide);
    }
}
