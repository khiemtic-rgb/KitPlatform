namespace KitPlatform.Packs.FamilyOs;
public sealed record RoutineDto(
    Guid Id,
    Guid FamilyId,
    string Code,
    string DisplayName,
    string Kind,
    IReadOnlyList<int> Weekdays,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<CommitmentTemplateDto> Templates);
public sealed record CommitmentTemplateDto(
    Guid Id,
    Guid RoutineId,
    Guid? MemberId,
    string Title,
    string? Description,
    TimeOnly? WindowStart,
    TimeOnly? WindowEnd,
    int SortOrder,
    bool IsActive,
    string Priority,
    int? ExpectedDurationMinutes,
    string? ContextAnchor,
    IReadOnlyList<Guid> DependsOnTemplateIds,
    bool AllowEarlyComplete,
    int EarlyLeadMinutes,
    int OnTimeGraceMinutes,
    int StarReward,
    string CommitmentKind = FamilyCommitmentKinds.Chore);
public sealed record CreateRoutineRequest(
    string Code,
    string DisplayName,
    string? Kind,
    IReadOnlyList<int>? Weekdays,
    int? SortOrder,
    IReadOnlyList<CreateCommitmentTemplateRequest>? Templates);
public sealed record CreateCommitmentTemplateRequest(
    string Title,
    string? Description,
    Guid? MemberId,
    TimeOnly? WindowStart,
    TimeOnly? WindowEnd,
    int? SortOrder,
    string? Priority = null,
    int? ExpectedDurationMinutes = null,
    string? ContextAnchor = null,
    IReadOnlyList<Guid>? DependsOnTemplateIds = null,
    bool? AllowEarlyComplete = null,
    int? EarlyLeadMinutes = null,
    int? OnTimeGraceMinutes = null,
    int? StarReward = null,
    string? CommitmentKind = null);
public sealed record AddCommitmentTemplateRequest(
    string Title,
    string? Description,
    Guid? MemberId,
    TimeOnly? WindowStart,
    TimeOnly? WindowEnd,
    int? SortOrder,
    string? Priority = null,
    int? ExpectedDurationMinutes = null,
    string? ContextAnchor = null,
    IReadOnlyList<Guid>? DependsOnTemplateIds = null,
    bool? AllowEarlyComplete = null,
    int? EarlyLeadMinutes = null,
    int? OnTimeGraceMinutes = null,
    int? StarReward = null,
    string? CommitmentKind = null);
public sealed record UpdateRoutineRequest(
    string? DisplayName,
    string? Kind,
    IReadOnlyList<int>? Weekdays,
    bool? IsActive,
    int? SortOrder);
/// <summary>Full replace of editable template fields (admin form always sends complete values).</summary>
public sealed record UpdateCommitmentTemplateRequest(
    string Title,
    string? Description,
    Guid? MemberId,
    TimeOnly? WindowStart,
    TimeOnly? WindowEnd,
    int SortOrder,
    bool IsActive,
    string? Priority = null,
    int? ExpectedDurationMinutes = null,
    string? ContextAnchor = null,
    IReadOnlyList<Guid>? DependsOnTemplateIds = null,
    bool? AllowEarlyComplete = null,
    int? EarlyLeadMinutes = null,
    int? OnTimeGraceMinutes = null,
    int? StarReward = null,
    string? CommitmentKind = null);
public interface IFamilyRoutineService
{
    Task<IReadOnlyList<RoutineDto>> ListRoutinesAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
    Task<RoutineDto?> GetRoutineAsync(
        Guid familyId,
        Guid routineId,
        CancellationToken cancellationToken = default);
    Task<RoutineDto> CreateRoutineAsync(
        Guid familyId,
        CreateRoutineRequest request,
        CancellationToken cancellationToken = default);
    Task<RoutineDto> UpdateRoutineAsync(
        Guid familyId,
        Guid routineId,
        UpdateRoutineRequest request,
        CancellationToken cancellationToken = default);
    Task<CommitmentTemplateDto> AddTemplateAsync(
        Guid familyId,
        Guid routineId,
        AddCommitmentTemplateRequest request,
        CancellationToken cancellationToken = default);
    Task<CommitmentTemplateDto> UpdateTemplateAsync(
        Guid familyId,
        Guid routineId,
        Guid templateId,
        UpdateCommitmentTemplateRequest request,
        CancellationToken cancellationToken = default);
    Task RemoveTemplateAsync(
        Guid familyId,
        Guid routineId,
        Guid templateId,
        CancellationToken cancellationToken = default);
}
