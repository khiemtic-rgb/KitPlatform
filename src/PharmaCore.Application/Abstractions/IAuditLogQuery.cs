namespace PharmaCore.Application.Abstractions;

using PharmaCore.Application.Security;

public interface IAuditLogQuery
{
    Task<PagedAuditLogsResult> ListAsync(
        string? entityType,
        string? action,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
