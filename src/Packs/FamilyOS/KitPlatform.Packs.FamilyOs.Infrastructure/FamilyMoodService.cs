using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyMoodService : IFamilyMoodService
{
    private const int MaxNoteLength = 2000;

    private readonly FamilyMoodRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyMoodService(FamilyMoodRepository repo, FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task<IReadOnlyList<FamilyMemberMoodDto>> ListFamilyMoodsAsync(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var rows = await _repo.ListByFamilyDayAsync(familyId, flowDate, cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<FamilyMemberMoodDto?> GetMemberMoodAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var row = await _repo.GetByMemberDayAsync(familyId, memberId, flowDate, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<FamilyMemberMoodDto> UpsertMemberMoodAsync(
        Guid familyId,
        Guid memberId,
        FamilyMemberMoodUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");

        if (!string.Equals(member.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ con mới ghi tâm trạng nhật ký.");

        var moodCode = request.MoodCode?.Trim().ToLowerInvariant()
            ?? throw new InvalidOperationException("Thiếu mã tâm trạng.");
        if (!FamilyMoodCodes.All.Contains(moodCode))
            throw new InvalidOperationException("Mã tâm trạng không hợp lệ.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var flowDate = request.FlowDate ?? today;
        var note = TrimOptionalNote(request.Note);

        var id = await _repo.UpsertAsync(
            familyId,
            memberId,
            flowDate,
            moodCode,
            note,
            cancellationToken);

        var row = await _repo.GetAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được tâm trạng.");

        return Map(row);
    }

    private static string? TrimOptionalNote(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var trimmed = value.Trim();
        return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
    }

    private static FamilyMemberMoodDto Map(FamilyMoodRepository.MoodRow row) =>
        new(
            row.Id,
            row.FamilyId,
            row.MemberId,
            row.MemberName,
            row.FlowDate,
            row.MoodCode,
            row.Note,
            row.CreatedAt,
            row.UpdatedAt);
}
