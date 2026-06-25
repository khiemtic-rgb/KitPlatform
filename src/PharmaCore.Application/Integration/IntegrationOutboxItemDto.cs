namespace PharmaCore.Application.Integration;

public sealed record IntegrationOutboxItemDto(
    Guid Id,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    DateTime OccurredAt,
    DateTime? PublishedAt,
    int PublishAttempts,
    string? LastError);
