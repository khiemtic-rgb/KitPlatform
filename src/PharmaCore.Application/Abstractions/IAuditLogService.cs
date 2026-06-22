namespace PharmaCore.Application.Abstractions;

public interface IAuditLogService
{
    Task WriteAsync(
        string entityType,
        Guid? entityId,
        string action,
        object? payload = null,
        CancellationToken cancellationToken = default);
}
