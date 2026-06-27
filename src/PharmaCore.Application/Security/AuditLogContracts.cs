namespace PharmaCore.Application.Security;

public sealed record AuditLogListItemDto(
    Guid Id,
    Guid? UserId,
    string? Username,
    string EntityType,
    Guid? EntityId,
    string Action,
    string? PayloadJson,
    DateTimeOffset CreatedAt);

public sealed record PagedAuditLogsResult(
    IReadOnlyList<AuditLogListItemDto> Items,
    int Total,
    int Page,
    int PageSize);
