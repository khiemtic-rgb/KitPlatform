using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilySchoolScheduleService : IFamilySchoolScheduleService
{
    private readonly FamilyBlueprintRepository _blueprint;
    private readonly FamilyGraphRepository _families;

    public FamilySchoolScheduleService(
        FamilyBlueprintRepository blueprint,
        FamilyGraphRepository families)
    {
        _blueprint = blueprint;
        _families = families;
    }

    public async Task<FamilySchoolScheduleMemberDto> GetMemberAsync(
        Guid familyId,
        Guid memberId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");

        if (!IsChildRole(member.RoleCode))
            throw new InvalidOperationException("Chỉ con (child) mới có lịch mùa học.");

        var layers = (await _blueprint.GetAsync(familyId, cancellationToken))?.LayersJson;
        var schedule = FamilySchoolSchedule.ReadMemberSchedule(layers, memberId);
        var derived = FamilySchoolSchedule.Derive(schedule, asOf ?? DateTimeOffset.UtcNow, family.Timezone);

        return new FamilySchoolScheduleMemberDto(
            memberId,
            member.DisplayName,
            FamilySchoolSchedule.ToPayloadDto(schedule),
            derived);
    }

    public async Task<FamilySchoolQuietMapDto> GetQuietMapAsync(
        Guid familyId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var children = members.Where(m => IsChildRole(m.RoleCode)).ToList();
        var layers = (await _blueprint.GetAsync(familyId, cancellationToken))?.LayersJson;
        var moment = asOf ?? DateTimeOffset.UtcNow;
        var tz = string.IsNullOrWhiteSpace(family.Timezone)
            ? FamilySchoolSchedule.DefaultTimeZone
            : family.Timezone;

        var entries = new List<FamilySchoolQuietEntryDto>(children.Count);
        foreach (var child in children)
        {
            var schedule = FamilySchoolSchedule.ReadMemberSchedule(layers, child.Id);
            var derived = FamilySchoolSchedule.Derive(schedule, moment, tz);
            entries.Add(new FamilySchoolQuietEntryDto(
                child.Id,
                child.DisplayName,
                derived.QuietNow,
                derived.Phase,
                derived.QuietEnd));
        }

        var local = TimeZoneInfo.ConvertTime(moment, FamilyTimeZones.Resolve(tz));
        return new FamilySchoolQuietMapDto(
            local,
            FamilyTimeZones.ToPostgresId(tz),
            entries);
    }

    private static bool IsChildRole(string? roleCode) =>
        string.Equals(roleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase)
        || string.Equals(roleCode, "kid", StringComparison.OrdinalIgnoreCase);
}
