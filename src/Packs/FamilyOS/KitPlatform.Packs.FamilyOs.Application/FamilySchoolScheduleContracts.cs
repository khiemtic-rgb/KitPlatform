namespace KitPlatform.Packs.FamilyOs;

/// <summary>SCH-02 — API surface for school schedule / quiet-map (read helpers).</summary>
public sealed record FamilySchoolSchedulePayloadDto(
    int SchemaVersion,
    bool SeasonOn,
    string Mode,
    IReadOnlyList<int> Weekdays,
    string SchoolStart,
    string SchoolEnd,
    bool HasExtraClass,
    string? ExtraEnd,
    string Source,
    string UpdatedAt,
    Guid? UpdatedByMemberId);

public sealed record FamilySchoolScheduleMemberDto(
    Guid MemberId,
    string? DisplayName,
    FamilySchoolSchedulePayloadDto? Schedule,
    FamilySchoolDerived Derived);

public sealed record FamilySchoolQuietEntryDto(
    Guid MemberId,
    string? DisplayName,
    bool QuietNow,
    string Phase,
    string QuietEnd);

public sealed record FamilySchoolQuietMapDto(
    DateTimeOffset AsOf,
    string TimeZone,
    IReadOnlyList<FamilySchoolQuietEntryDto> Members);

public sealed record FamilyMemberSchoolPhaseDto(
    Guid MemberId,
    string Phase,
    bool QuietNow,
    string QuietEnd);

public interface IFamilySchoolScheduleService
{
    Task<FamilySchoolScheduleMemberDto> GetMemberAsync(
        Guid familyId,
        Guid memberId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);

    Task<FamilySchoolQuietMapDto> GetQuietMapAsync(
        Guid familyId,
        DateTimeOffset? asOf = null,
        CancellationToken cancellationToken = default);
}
