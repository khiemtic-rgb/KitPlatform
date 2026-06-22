using System.Text.Json;
using Dapper;
using PharmaCore.Application.Abstractions;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.Security;

internal sealed class AuditLogRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public AuditLogRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task WriteAsync(
        string entityType,
        Guid? entityId,
        string action,
        object? payload,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO audit_logs (tenant_id, user_id, entity_type, entity_id, action, payload)
            VALUES (@TenantId, @UserId, @EntityType, @EntityId, @Action, @Payload::jsonb)
            """;
        var payloadJson = payload is null
            ? null
            : JsonSerializer.Serialize(payload);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            TenantId = _tenant.TenantId,
            UserId = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
            EntityType = entityType,
            EntityId = entityId,
            Action = action,
            Payload = payloadJson,
        });
    }
}

internal sealed class AuditLogService : IAuditLogService
{
    private readonly AuditLogRepository _repository;

    public AuditLogService(AuditLogRepository repository) => _repository = repository;

    public Task WriteAsync(
        string entityType,
        Guid? entityId,
        string action,
        object? payload = null,
        CancellationToken cancellationToken = default) =>
        _repository.WriteAsync(entityType, entityId, action, payload, cancellationToken);
}
