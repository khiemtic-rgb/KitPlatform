namespace KitPlatform.Packs.FamilyOs;

public static class FamilyCalendarPeriodKinds
{
    public const string SchoolYear = "school_year";
    public const string Summer = "summer";
    public const string Exam = "exam";
    public const string Travel = "travel";
    public const string Holiday = "holiday";
    public const string Custom = "custom";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        SchoolYear, Summer, Exam, Travel, Holiday, Custom,
    };

    /// <summary>Default priority when caller omits it — travel wins over seasons.</summary>
    public static int DefaultPriority(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        Travel => 100,
        Holiday => 80,
        Exam => 60,
        Custom => 50,
        Summer => 40,
        SchoolYear => 20,
        _ => 0,
    };

    public static string LabelVi(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        SchoolYear => "Năm học",
        Summer => "Nghỉ hè",
        Exam => "Ôn thi",
        Travel => "Du lịch",
        Holiday => "Nghỉ lễ",
        Custom => "Tùy chỉnh",
        _ => kind,
    };
}

public sealed record CalendarPeriodSlotDto(
    Guid Id,
    Guid PeriodId,
    IReadOnlyList<int> Weekdays,
    Guid RoutineId,
    string? RoutineDisplayName,
    int SortOrder);

public sealed record CalendarPeriodDto(
    Guid Id,
    Guid FamilyId,
    string Code,
    string DisplayName,
    string Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    int Priority,
    bool IsActive,
    string? Notes,
    IReadOnlyList<CalendarPeriodSlotDto> Slots);

public sealed record CalendarPeriodSlotInput(
    IReadOnlyList<int> Weekdays,
    Guid RoutineId,
    int? SortOrder = null);

public sealed record CreateCalendarPeriodRequest(
    string Code,
    string DisplayName,
    string? Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    int? Priority,
    bool? IsActive,
    string? Notes,
    IReadOnlyList<CalendarPeriodSlotInput>? Slots);

public sealed record UpdateCalendarPeriodRequest(
    string? DisplayName,
    string? Kind,
    DateOnly? StartDate,
    DateOnly? EndDate,
    int? Priority,
    bool? IsActive,
    string? Notes,
    IReadOnlyList<CalendarPeriodSlotInput>? Slots);

public sealed record ResolvedCalendarRoutineDto(
    DateOnly FlowDate,
    int IsoWeekday,
    Guid RoutineId,
    string RoutineDisplayName,
    string Source,
    Guid? PeriodId,
    string? PeriodDisplayName,
    string? PeriodKind);

public interface IFamilyCalendarPeriodService
{
    Task<IReadOnlyList<CalendarPeriodDto>> ListAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<CalendarPeriodDto?> GetAsync(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken = default);

    Task<CalendarPeriodDto> CreateAsync(
        Guid familyId,
        CreateCalendarPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task<CalendarPeriodDto> UpdateAsync(
        Guid familyId,
        Guid periodId,
        UpdateCalendarPeriodRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken = default);

    Task<ResolvedCalendarRoutineDto> ResolveAsync(
        Guid familyId,
        DateOnly? flowDate,
        CancellationToken cancellationToken = default);
}
