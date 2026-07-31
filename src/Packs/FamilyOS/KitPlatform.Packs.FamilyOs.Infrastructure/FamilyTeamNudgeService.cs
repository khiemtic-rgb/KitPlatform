using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyTeamNudgeService : IFamilyTeamNudgeService
{
    private const int MaxSentPerFromPerDay = 3;

    private readonly FamilyTeamNudgeRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyTeamUnlockService _team;
    private readonly IFamilyMemoryService _memories;
    private readonly ITenantContext _tenant;

    public FamilyTeamNudgeService(
        FamilyTeamNudgeRepository repo,
        FamilyGraphRepository families,
        IFamilyTeamUnlockService team,
        IFamilyMemoryService memories,
        ITenantContext tenant)
    {
        _repo = repo;
        _families = families;
        _team = team;
        _memories = memories;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FamilyTeamNudgeDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        Guid? forMemberId = null,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _repo.ListAsync(familyId, flowDate, forMemberId, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<FamilyTeamNudgeCandidateDto>> ListFromCandidatesAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;
        var team = await _team.GetTeamDayAsync(familyId, date, cancellationToken);
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var children = members
            .Where(m => m.RoleCode.Equals("child", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return children.Select(ch =>
        {
            var stage = FamilyTeamRoleMatrix.StageFromDateOfBirth(ch.DateOfBirth, date);
            var slice = team.Children.FirstOrDefault(s => s.MemberId == ch.Id);
            var missionsComplete = slice is null
                ? false
                : slice.Total > 0 && slice.Open == 0;
            var canInvite = FamilyTeamRoleMatrix.CanInvite(stage, missionsComplete);
            return new FamilyTeamNudgeCandidateDto(
                ch.Id, ch.DisplayName, stage, canInvite, missionsComplete);
        }).ToList();
    }

    public async Task<FamilyTeamNudgeDto> CreateAsync(
        Guid familyId,
        FamilyTeamNudgeCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = request.FlowDate ?? today;
        var template = (request.TemplateCode ?? "").Trim().ToLowerInvariant();
        if (!FamilyTeamNudgeTemplates.All.Contains(template))
            throw new InvalidOperationException("templateCode không hợp lệ.");

        if (request.FromMemberId == request.ToMemberId)
            throw new InvalidOperationException("Không thể nhắc chính mình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var from = members.FirstOrDefault(m => m.Id == request.FromMemberId)
            ?? throw new InvalidOperationException("fromMemberId không thuộc gia đình.");
        var to = members.FirstOrDefault(m => m.Id == request.ToMemberId)
            ?? throw new InvalidOperationException("toMemberId không thuộc gia đình.");

        if (!from.RoleCode.Equals("child", StringComparison.OrdinalIgnoreCase)
            || !to.RoleCode.Equals("child", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ anh/chị em (child) mới gửi/nhận nudge.");

        var candidates = await ListFromCandidatesAsync(familyId, date, cancellationToken);
        var fromCand = candidates.FirstOrDefault(c => c.MemberId == from.Id);
        if (fromCand is null || !fromCand.CanInvite)
            throw new InvalidOperationException(
                "Con này chưa đủ điều kiện mời nhắc (cần xong mission hoặc đủ tuổi).");

        var toStage = FamilyTeamRoleMatrix.StageFromDateOfBirth(to.DateOfBirth, date);
        if (!FamilyTeamRoleMatrix.PreferAsInviter(fromCand.StageCode, toStage)
            && !fromCand.MissionsComplete)
            throw new InvalidOperationException("Ưu tiên anh/chị đã xong hoặc lớn hơn làm người nhắc.");

        var message = FamilyTeamNudgeTemplates.MessageVi(
            template, ShortName(from.DisplayName), ShortName(to.DisplayName));

        var row = await _repo.InsertDraftAsync(
            familyId,
            date,
            from.Id,
            to.Id,
            request.CommitmentId,
            template,
            message,
            cancellationToken);

        return Map(row);
    }

    public async Task<FamilyTeamNudgeDto> SendAsync(
        Guid familyId,
        Guid nudgeId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _repo.GetAsync(familyId, nudgeId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy nudge.");
        if (existing.Status != FamilyTeamNudgeStatuses.Draft)
            throw new InvalidOperationException("Chỉ gửi được nudge ở trạng thái draft.");

        var sentCount = await _repo.CountSentTodayAsync(
            familyId, existing.FromMemberId, existing.FlowDate, cancellationToken);
        if (sentCount >= MaxSentPerFromPerDay)
            throw new InvalidOperationException(
                $"Mỗi con chỉ gửi tối đa {MaxSentPerFromPerDay} lời nhắc / ngày.");

        var updated = await _repo.MarkSentAsync(familyId, nudgeId, cancellationToken)
            ?? throw new InvalidOperationException("Không gửi được nudge.");
        if (updated.Status != FamilyTeamNudgeStatuses.Sent)
            throw new InvalidOperationException("Không gửi được nudge.");

        return Map(updated);
    }

    public async Task<FamilyTeamNudgeDto> AckAsync(
        Guid familyId,
        Guid nudgeId,
        FamilyTeamNudgeAckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        if (!FamilyTeamNudgeStatuses.Ack.Contains(status))
            throw new InvalidOperationException("status phải là seen | thanks | deferred.");

        var existing = await _repo.GetAsync(familyId, nudgeId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy nudge.");
        if (existing.Status is not (FamilyTeamNudgeStatuses.Sent or FamilyTeamNudgeStatuses.Seen))
            throw new InvalidOperationException("Nudge chưa gửi hoặc đã xử lý.");

        var updated = await _repo.AckAsync(familyId, nudgeId, status, cancellationToken)
            ?? throw new InvalidOperationException("Không cập nhật được nudge.");

        if (status == FamilyTeamNudgeStatuses.Thanks
            && FamilyMemoryKinds.All.Contains(FamilyMemoryKinds.Help))
        {
            try
            {
                await _memories.TryCaptureAsync(
                    _tenant.TenantId,
                    familyId,
                    updated.FlowDate,
                    FamilyMemoryKinds.Help,
                    $"{updated.ToName} cảm ơn lời nhắc từ {updated.FromName}",
                    noteVi: "Anh chị em giúp nhau hoàn thành ngày.",
                    icon: "🤝",
                    sourceRef: $"nudge-thanks:{updated.Id:D}",
                    memberId: updated.ToMemberId,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Best-effort journal signal.
            }
        }

        return Map(updated);
    }

    private static string ShortName(string displayName)
    {
        var t = (displayName ?? "").Trim();
        if (string.IsNullOrEmpty(t)) return "bạn";
        var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? t : parts[^1];
    }

    private static FamilyTeamNudgeDto Map(FamilyTeamNudgeRepository.NudgeRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.FlowDate,
            row.FromMemberId,
            row.FromName,
            row.ToMemberId,
            row.ToName,
            row.CommitmentId,
            row.TemplateCode,
            row.MessageVi,
            row.Status,
            row.SentAt,
            row.AckAt,
            row.CreatedAt);
}
