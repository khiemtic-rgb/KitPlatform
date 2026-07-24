namespace KitPlatform.Packs.FamilyOs;

public static class FamilyMembershipRoles
{
    public const string Guardian = "guardian";
    public const string Caregiver = "caregiver";
    public const string Child = "child";
    public const string Viewer = "viewer";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Guardian, Caregiver, Child, Viewer,
    };
}

public static class FamilyRoutineKinds
{
    public const string SchoolDay = "school_day";
    public const string Weekend = "weekend";
    public const string Holiday = "holiday";
    public const string Exam = "exam";
    public const string Travel = "travel";
    public const string Custom = "custom";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        SchoolDay, Weekend, Holiday, Exam, Travel, Custom,
    };
}

public static class FamilyCommitmentStatuses
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Done = "done";
    public const string Skipped = "skipped";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending, InProgress, Done, Skipped,
    };
}

public sealed record FamilyDto(
    Guid Id,
    string DisplayName,
    string Timezone,
    string Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<FamilyMembershipDto> Members);

public sealed record FamilyMembershipDto(
    Guid Id,
    Guid FamilyId,
    string DisplayName,
    string RoleCode,
    DateOnly? DateOfBirth,
    Guid? AccountId,
    int SortOrder,
    string Status);

public sealed record CreateFamilyRequest(
    string DisplayName,
    string? Timezone,
    IReadOnlyList<CreateMembershipRequest>? Members);

public sealed record CreateMembershipRequest(
    string DisplayName,
    string RoleCode,
    DateOnly? DateOfBirth,
    Guid? AccountId,
    int? SortOrder);

public sealed record AddMembershipRequest(
    string DisplayName,
    string RoleCode,
    DateOnly? DateOfBirth,
    Guid? AccountId,
    int? SortOrder);

public sealed record UpdateMembershipRequest(
    string? DisplayName,
    string? RoleCode,
    DateOnly? DateOfBirth,
    bool ClearDateOfBirth = false,
    int? SortOrder = null,
    string? Status = null);

public sealed record UpdateFamilyRequest(
    string? DisplayName,
    string? Timezone);

public interface IFamilyGraphService
{
    Task<IReadOnlyList<FamilyDto>> ListFamiliesAsync(CancellationToken cancellationToken = default);
    Task<FamilyDto?> GetFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);
    Task<FamilyDto> CreateFamilyAsync(CreateFamilyRequest request, CancellationToken cancellationToken = default);
    Task<FamilyDto> UpdateFamilyAsync(
        Guid familyId,
        UpdateFamilyRequest request,
        CancellationToken cancellationToken = default);
    Task<FamilyMembershipDto> AddMemberAsync(
        Guid familyId,
        AddMembershipRequest request,
        CancellationToken cancellationToken = default);
    Task<FamilyMembershipDto> UpdateMemberAsync(
        Guid familyId,
        Guid memberId,
        UpdateMembershipRequest request,
        CancellationToken cancellationToken = default);
}
