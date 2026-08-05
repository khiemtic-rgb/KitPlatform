using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyDigitalMirrorService : IFamilyDigitalMirrorService
{
    private const int AgentOnlineWindowMinutes = 5;
    private const int TopAppsLimit = 5;
    private const int MaxParentNotesPerDay = 5;
    private const int MaxBodyLength = 2000;
    private const int MaxDeviceIdLength = 80;
    private const int ModerateUsageMinSeconds = 30 * 60;
    private const int ModerateUsageMaxSeconds = 4 * 60 * 60;

    private readonly FamilyDigitalMirrorRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyDigitalMirrorService(
        FamilyDigitalMirrorRepository repo,
        FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task HeartbeatAsync(
        Guid familyId,
        FamilyMirrorHeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var deviceId = request.DeviceId?.Trim()
            ?? throw new InvalidOperationException("Thiếu mã thiết bị.");
        if (deviceId.Length == 0 || deviceId.Length > MaxDeviceIdLength)
            throw new InvalidOperationException("Mã thiết bị không hợp lệ.");

        var child = await RequireChildMemberAsync(familyId, request.MemberId, cancellationToken);

        await _repo.UpsertHeartbeatAsync(
            familyId,
            child.Id,
            deviceId,
            TrimOptional(request.DeviceLabel, 120),
            TrimOptional(request.AgentVersion, 40),
            TrimOptional(request.LastForegroundApp, 160),
            cancellationToken);
    }

    public async Task IngestUsageAsync(
        Guid familyId,
        FamilyMirrorUsageIngestRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var child = await RequireChildMemberAsync(familyId, request.MemberId, cancellationToken);
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;

        if (request.Items is null || request.Items.Count == 0)
            return;

        var items = new List<FamilyDigitalMirrorRepository.UsageIngestItem>();
        foreach (var raw in request.Items)
        {
            var appKey = raw.AppKey?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(appKey))
                continue;

            var kind = string.IsNullOrWhiteSpace(raw.Kind)
                ? FamilyMirrorUsageKinds.App
                : raw.Kind.Trim().ToLowerInvariant();
            if (!FamilyMirrorUsageKinds.All.Contains(kind))
                throw new InvalidOperationException("Loại usage không hợp lệ.");

            var seconds = Math.Max(0, raw.Seconds);
            if (seconds == 0) continue;

            items.Add(new FamilyDigitalMirrorRepository.UsageIngestItem(
                appKey[..Math.Min(appKey.Length, 120)],
                TrimOptional(raw.AppLabel, 160),
                kind,
                seconds));
        }

        if (items.Count == 0) return;

        await _repo.IngestUsageAsync(familyId, child.Id, flowDate, items, cancellationToken);
    }

    public async Task<FamilyMirrorDayDto> GetDayAsync(
        Guid familyId,
        Guid? memberId = null,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = flowDate ?? today;

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var children = members
            .Where(m => m.RoleCode.Equals(FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var child = memberId is Guid mid
            ? children.FirstOrDefault(m => m.Id == mid)
                ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.")
            : children.FirstOrDefault()
                ?? throw new InvalidOperationException("Gia đình chưa có thành viên con.");

        var device = await _repo.GetLatestDeviceAsync(familyId, child.Id, cancellationToken);
        var agentOnline = device?.LastHeartbeatAt is DateTimeOffset hb
                          && hb >= DateTimeOffset.UtcNow.AddMinutes(-AgentOnlineWindowMinutes);

        var usageRows = await _repo.ListUsageDayAsync(
            familyId, child.Id, date, TopAppsLimit, cancellationToken);
        var totalSeconds = await _repo.SumUsageSecondsAsync(
            familyId, child.Id, date, cancellationToken);

        var topApps = usageRows
            .Select(r => new FamilyMirrorAppSliceDto(r.AppKey, r.AppLabel, r.Kind, r.Seconds))
            .ToList();

        var insight = BuildInsightVi(child.DisplayName, topApps, totalSeconds);
        var actions = BuildSuggestedActions(totalSeconds, topApps, agentOnline);
        var notes = await _repo.ListParentNotesAsync(familyId, child.Id, date, cancellationToken);

        return new FamilyMirrorDayDto(
            date,
            child.Id,
            child.DisplayName,
            agentOnline,
            device?.LastHeartbeatAt,
            device?.LastForegroundApp,
            topApps,
            totalSeconds,
            insight,
            actions,
            notes.Select(MapNote).ToList());
    }

    public async Task<FamilyMirrorParentNoteDto> PostParentNoteAsync(
        Guid familyId,
        FamilyMirrorParentNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var child = await RequireChildMemberAsync(familyId, request.MemberId, cancellationToken);
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var parent = members.FirstOrDefault(m => m.Id == request.FromMembershipId)
            ?? throw new InvalidOperationException("Người gửi không thuộc gia đình này.");

        if (parent.RoleCode is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
            throw new InvalidOperationException("Chỉ bố/mẹ mới gửi ghi chú Mirror.");

        var tone = request.Tone?.Trim().ToLowerInvariant()
            ?? throw new InvalidOperationException("Thiếu tone ghi chú.");
        if (!FamilyMirrorParentNoteTones.All.Contains(tone))
            throw new InvalidOperationException("Tone ghi chú không hợp lệ.");

        var body = request.BodyVi?.Trim()
            ?? throw new InvalidOperationException("Thiếu nội dung ghi chú.");
        if (body.Length == 0)
            throw new InvalidOperationException("Thiếu nội dung ghi chú.");
        if (body.Length > MaxBodyLength)
            body = body[..MaxBodyLength];

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;

        var count = await _repo.CountParentNotesAsync(
            familyId, child.Id, flowDate, parent.Id, cancellationToken);
        if (count >= MaxParentNotesPerDay)
            throw new InvalidOperationException($"Tối đa {MaxParentNotesPerDay} ghi chú Mirror mỗi ngày.");

        var row = await _repo.InsertParentNoteAsync(
            familyId, child.Id, flowDate, parent.Id, tone, body, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được ghi chú Mirror.");

        return MapNote(row);
    }

    private async Task<FamilyGraphRepository.MembershipRow> RequireChildMemberAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var member = await _families.GetMemberAsync(familyId, memberId, cancellationToken)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");

        if (!member.RoleCode.Equals(FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Mirror chỉ theo dõi thành viên con.");

        return member;
    }

    private static string? BuildInsightVi(
        string memberName,
        IReadOnlyList<FamilyMirrorAppSliceDto> topApps,
        int totalSeconds)
    {
        if (totalSeconds <= 0 || topApps.Count == 0)
            return null;

        var top = topApps[0];
        var label = string.IsNullOrWhiteSpace(top.AppLabel) ? top.AppKey : top.AppLabel;
        var minutes = Math.Max(1, (int)Math.Round(top.Seconds / 60.0));
        return $"Hôm nay {memberName} dành nhiều thời gian với {label} ({minutes} phút).";
    }

    private static IReadOnlyList<string> BuildSuggestedActions(
        int totalSeconds,
        IReadOnlyList<FamilyMirrorAppSliceDto> topApps,
        bool agentOnline)
    {
        var actions = new List<string>(3);

        if (!agentOnline)
            actions.Add("Kiểm tra Agent trên máy con");

        if (totalSeconds >= ModerateUsageMinSeconds && totalSeconds <= ModerateUsageMaxSeconds)
            actions.Add("Gửi lời khen nhẹ nhàng");
        else if (totalSeconds > ModerateUsageMaxSeconds)
            actions.Add("Trò chuyện về thói quen màn hình");
        else if (totalSeconds > 0)
            actions.Add("Hỏi con về app yêu thích hôm nay");

        if (topApps.Count > 0)
            actions.Add("Đặt lịch nghỉ mắt cùng nhau");

        if (actions.Count < 3)
            actions.Add("Xem lại Mirror cùng con tối nay");

        return actions.Take(3).ToList();
    }

    private static string? TrimOptional(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen];
    }

    private static FamilyMirrorParentNoteDto MapNote(FamilyDigitalMirrorRepository.ParentNoteRow row) =>
        new(
            row.Id,
            row.MemberId,
            row.FlowDate,
            row.FromMembershipId,
            row.FromMemberName,
            row.Tone,
            row.BodyVi,
            row.CreatedAt);
}
