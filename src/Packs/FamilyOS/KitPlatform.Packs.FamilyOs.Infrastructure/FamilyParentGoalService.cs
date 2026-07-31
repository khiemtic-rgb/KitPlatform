using KitPlatform.Application.Abstractions;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyParentGoalService : IFamilyParentGoalService
{
    private const int MaxTitleLength = 200;
    private const int MaxGoalsPerMember = 6;
    private const int LookbackDays = 60;

    private readonly FamilyParentGoalRepository _repo;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyMemoryService _memories;
    private readonly ITenantContext _tenant;

    public FamilyParentGoalService(
        FamilyParentGoalRepository repo,
        FamilyGraphRepository families,
        IFamilyMemoryService memories,
        ITenantContext tenant)
    {
        _repo = repo;
        _families = families;
        _memories = memories;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<ParentGoalDto>> ListForMemberAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        await RequireParentAsync(familyId, memberId, cancellationToken);

        var goals = await _repo.ListForMemberAsync(familyId, memberId, cancellationToken);
        return await ComposeAsync(family.Timezone, goals, cancellationToken);
    }

    public async Task<ParentGoalDto> CreateAsync(
        Guid familyId,
        CreateParentGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        await RequireParentAsync(familyId, request.MemberId, cancellationToken);

        var title = NormalizeTitle(request.Title);
        var existing = await _repo.ListForMemberAsync(familyId, request.MemberId, cancellationToken);
        if (existing.Count(g => g.IsActive) >= MaxGoalsPerMember)
            throw new InvalidOperationException(
                $"Mỗi người tối đa {MaxGoalsPerMember} mục tiêu đang hoạt động — hãy ẩn bớt trước.");

        var target = ClampTarget(request.TargetDaysPerWeek);
        var emoji = NormalizeEmoji(request.Emoji);

        var id = await _repo.InsertAsync(
            familyId,
            request.MemberId,
            title,
            emoji,
            target,
            request.ShareWithFamily ?? false,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không tạo được mục tiêu.");
        return (await ComposeAsync(family.Timezone, [row], cancellationToken))[0];
    }

    public async Task<ParentGoalDto> UpdateAsync(
        Guid familyId,
        Guid goalId,
        UpdateParentGoalRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        var existing = await _repo.GetAsync(familyId, goalId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy mục tiêu.");

        var title = request.Title is null ? null : NormalizeTitle(request.Title);
        var target = request.TargetDaysPerWeek is null ? (int?)null : ClampTarget(request.TargetDaysPerWeek);
        var emoji = request.Emoji is null ? null : NormalizeEmoji(request.Emoji);

        await _repo.UpdateAsync(
            familyId,
            goalId,
            title,
            emoji,
            target,
            request.ShareWithFamily,
            request.IsActive,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, goalId, cancellationToken) ?? existing;
        return (await ComposeAsync(family.Timezone, [row], cancellationToken))[0];
    }

    public async Task DeleteAsync(
        Guid familyId,
        Guid goalId,
        CancellationToken cancellationToken = default)
    {
        await RequireFamilyAsync(familyId, cancellationToken);
        if (await _repo.GetAsync(familyId, goalId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy mục tiêu.");
        await _repo.SoftDeleteAsync(familyId, goalId, cancellationToken);
    }

    public async Task<ParentGoalDto> CheckinAsync(
        Guid familyId,
        Guid goalId,
        ParentGoalCheckinRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        var goal = await _repo.GetAsync(familyId, goalId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy mục tiêu.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var date = request.Date ?? today;
        if (date > today)
            throw new InvalidOperationException("Không thể check-in cho ngày tương lai.");

        var status = (request.Status ?? "").Trim().ToLowerInvariant();
        switch (status)
        {
            case ParentGoalCheckinStatuses.Clear:
                await _repo.ClearCheckinAsync(goalId, date, cancellationToken);
                break;
            case ParentGoalCheckinStatuses.Done:
            case ParentGoalCheckinStatuses.Skip:
                await _repo.UpsertCheckinAsync(
                    goalId, goal.MemberId, familyId, date, status,
                    NormalizeNote(request.Note), cancellationToken);
                break;
            default:
                throw new InvalidOperationException("Trạng thái check-in phải là done | skip | clear.");
        }

        if (status == ParentGoalCheckinStatuses.Done)
        {
            try
            {
                await _memories.TryCaptureAsync(
                    _tenant.TenantId,
                    familyId,
                    date,
                    FamilyMemoryKinds.ParentHabit,
                    $"Bố/mẹ: {goal.Title}",
                    noteVi: "Thói quen bố mẹ tuần này — tách khỏi % đội con.",
                    icon: "🌿",
                    sourceRef: $"parent-habit:{goalId:D}:{date:yyyy-MM-dd}",
                    memberId: goal.MemberId,
                    cancellationToken: cancellationToken);
            }
            catch
            {
                // Best-effort journal.
            }
        }

        var row = await _repo.GetAsync(familyId, goalId, cancellationToken) ?? goal;
        return (await ComposeAsync(family.Timezone, [row], cancellationToken))[0];
    }

    public async Task<IReadOnlyList<SharedParentProgressDto>> ListSharedAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        var family = await RequireFamilyAsync(familyId, cancellationToken);
        var goals = await _repo.ListSharedAsync(familyId, cancellationToken);
        if (goals.Count == 0) return [];

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var checkins = await _repo.ListCheckinsAsync(
            goals.Select(g => g.Id).ToArray(),
            today.AddDays(-6),
            cancellationToken);
        var byGoal = GroupDoneDates(checkins);

        return goals
            .Select(g =>
            {
                var done = byGoal.TryGetValue(g.Id, out var set) ? set : [];
                return new SharedParentProgressDto(
                    g.MemberId,
                    g.MemberName,
                    g.Id,
                    g.Title,
                    g.Emoji,
                    g.TargetDaysPerWeek,
                    done.Contains(today),
                    CountWithin(done, today, 7));
            })
            .ToList();
    }

    // -- helpers -------------------------------------------------------------

    private async Task<IReadOnlyList<ParentGoalDto>> ComposeAsync(
        string timezone,
        IReadOnlyList<FamilyParentGoalRepository.GoalRow> goals,
        CancellationToken cancellationToken)
    {
        if (goals.Count == 0) return [];

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(timezone).DateTime);
        var checkins = await _repo.ListCheckinsAsync(
            goals.Select(g => g.Id).ToArray(),
            today.AddDays(-LookbackDays),
            cancellationToken);

        var doneByGoal = GroupDoneDates(checkins);
        var todayStatusByGoal = checkins
            .Where(c => c.CheckinDate == today)
            .GroupBy(c => c.GoalId)
            .ToDictionary(g => g.Key, g => g.First().Status);

        return goals
            .Select(g =>
            {
                var done = doneByGoal.TryGetValue(g.Id, out var set) ? set : [];
                todayStatusByGoal.TryGetValue(g.Id, out var todayStatus);
                return new ParentGoalDto(
                    g.Id,
                    g.FamilyId,
                    g.MemberId,
                    g.MemberName,
                    g.Title,
                    g.Emoji,
                    g.TargetDaysPerWeek,
                    g.ShareWithFamily,
                    g.IsActive,
                    g.SortOrder,
                    todayStatus,
                    CountWithin(done, today, 7),
                    ComputeStreak(done, today));
            })
            .ToList();
    }

    private static Dictionary<Guid, HashSet<DateOnly>> GroupDoneDates(
        IReadOnlyList<FamilyParentGoalRepository.CheckinRow> checkins) =>
        checkins
            .Where(c => c.Status == ParentGoalCheckinStatuses.Done)
            .GroupBy(c => c.GoalId)
            .ToDictionary(g => g.Key, g => g.Select(c => c.CheckinDate).ToHashSet());

    private static int CountWithin(HashSet<DateOnly> done, DateOnly today, int windowDays)
    {
        var from = today.AddDays(-(windowDays - 1));
        return done.Count(d => d >= from && d <= today);
    }

    private static int ComputeStreak(HashSet<DateOnly> done, DateOnly today)
    {
        // Allow today to be still pending: start from today if done, else yesterday.
        var cursor = done.Contains(today) ? today : today.AddDays(-1);
        var streak = 0;
        while (done.Contains(cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return streak;
    }

    private async Task<FamilyGraphRepository.FamilyRow> RequireFamilyAsync(
        Guid familyId,
        CancellationToken cancellationToken) =>
        await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

    private async Task RequireParentAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");

        var role = (member.RoleCode ?? "").ToLowerInvariant();
        if (role is not (FamilyMembershipRoles.Guardian or FamilyMembershipRoles.Caregiver))
            throw new InvalidOperationException("Chỉ bố/mẹ hoặc người chăm sóc mới đặt mục tiêu này.");
    }

    private static string NormalizeTitle(string? title)
    {
        var trimmed = (title ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("Tên mục tiêu là bắt buộc.");
        return trimmed.Length <= MaxTitleLength ? trimmed : trimmed[..MaxTitleLength];
    }

    private static string? NormalizeEmoji(string? emoji)
    {
        var trimmed = emoji?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length <= 16 ? trimmed : trimmed[..16];
    }

    private static string? NormalizeNote(string? note)
    {
        var trimmed = note?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length <= 500 ? trimmed : trimmed[..500];
    }

    private static int ClampTarget(int? value) => Math.Clamp(value ?? 5, 1, 7);
}
