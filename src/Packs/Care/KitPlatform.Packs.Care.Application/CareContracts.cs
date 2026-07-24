namespace KitPlatform.Packs.Care;

public sealed record CareEventDto(
    Guid Id,
    Guid? CustomerId,
    Guid? FamilyMemberId,
    string EventType,
    short Tier,
    DateTimeOffset OccurredAt,
    string SourceSystem,
    string? SourceRefType,
    Guid? SourceRefId,
    string PayloadJson,
    Guid? CorrelationId,
    DateTimeOffset CreatedAt);

public sealed record CreateCareEventRequest(
    string EventType,
    short Tier = 2,
    Guid? CustomerId = null,
    Guid? FamilyMemberId = null,
    DateTimeOffset? OccurredAt = null,
    string SourceSystem = "manual",
    string? SourceRefType = null,
    Guid? SourceRefId = null,
    string? PayloadJson = null,
    Guid? CorrelationId = null);

public sealed record CareCohortDefinitionDto(
    Guid Id,
    string CohortCode,
    string DisplayName,
    string? Description,
    short TierTarget,
    string Status,
    string CriteriaJson);

public sealed record CareCohortMembershipDto(
    Guid Id,
    Guid CohortId,
    string CohortCode,
    Guid CustomerId,
    string Status,
    string Source,
    DateTimeOffset AssignedAt,
    string? Notes);

public sealed record AssignCareCohortRequest(
    Guid CohortId,
    Guid CustomerId,
    string? Notes = null,
    string Source = "manual");

public sealed record CareKpiDefinitionDto(
    Guid Id,
    string KpiCode,
    string DisplayName,
    string? Description,
    short TierTarget,
    short? ProblemIndex,
    string Unit,
    string Status,
    string ComputeHintsJson,
    string RunnableWhen);

public interface ICareEventService
{
    Task<IReadOnlyList<CareEventDto>> ListEventsAsync(
        Guid? customerId,
        string? eventType,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CareEventDto> CreateEventAsync(
        CreateCareEventRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICareCohortService
{
    Task<IReadOnlyList<CareCohortDefinitionDto>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CareCohortMembershipDto>> ListMembershipsAsync(
        Guid? cohortId,
        CancellationToken cancellationToken = default);

    Task<CareCohortMembershipDto> AssignAsync(
        AssignCareCohortRequest request,
        CancellationToken cancellationToken = default);
}

public interface ICareKpiService
{
    Task<IReadOnlyList<CareKpiDefinitionDto>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default);
}
