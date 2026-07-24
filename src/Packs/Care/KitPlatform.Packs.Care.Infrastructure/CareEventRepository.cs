using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Care;

namespace KitPlatform.Packs.Care.Infrastructure;

internal sealed class CareEventRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CareEventRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<IReadOnlyList<CareEventDto>> ListAsync(
        Guid? customerId,
        string? eventType,
        int limit,
        CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 200);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CareEventRow>(
            """
            SELECT id AS Id, customer_id AS CustomerId, family_member_id AS FamilyMemberId,
                   event_type AS EventType, tier AS Tier, occurred_at AS OccurredAt,
                   source_system AS SourceSystem, source_ref_type AS SourceRefType,
                   source_ref_id AS SourceRefId, payload::text AS PayloadJson,
                   correlation_id AS CorrelationId, created_at AS CreatedAt
            FROM pack_care.care_event
            WHERE tenant_id = @TenantId
              AND (@CustomerId IS NULL OR customer_id = @CustomerId)
              AND (@EventType IS NULL OR event_type = @EventType)
            ORDER BY occurred_at DESC
            LIMIT @Limit
            """,
            new { TenantId, CustomerId = customerId, EventType = eventType, Limit = limit });
        return rows.Select(Map).ToList();
    }

    public async Task<CareEventDto> InsertAsync(
        CreateCareEventRequest request,
        Guid? actorUserId,
        CancellationToken cancellationToken)
    {
        var payload = string.IsNullOrWhiteSpace(request.PayloadJson) ? "{}" : request.PayloadJson.Trim();
        if (!IsJsonObject(payload))
            throw new ArgumentException("payloadJson phải là JSON object.");

        var occurred = request.OccurredAt?.UtcDateTime ?? DateTime.UtcNow;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<CareEventRow>(
            """
            INSERT INTO pack_care.care_event (
                tenant_id, customer_id, family_member_id, event_type, tier, occurred_at,
                source_system, source_ref_type, source_ref_id, payload, correlation_id, created_by
            )
            VALUES (
                @TenantId, @CustomerId, @FamilyMemberId, @EventType, @Tier, @OccurredAt,
                @SourceSystem, @SourceRefType, @SourceRefId, CAST(@PayloadJson AS jsonb),
                @CorrelationId, @ActorUserId
            )
            RETURNING id AS Id, customer_id AS CustomerId, family_member_id AS FamilyMemberId,
                      event_type AS EventType, tier AS Tier, occurred_at AS OccurredAt,
                      source_system AS SourceSystem, source_ref_type AS SourceRefType,
                      source_ref_id AS SourceRefId, payload::text AS PayloadJson,
                      correlation_id AS CorrelationId, created_at AS CreatedAt
            """,
            new
            {
                TenantId,
                request.CustomerId,
                request.FamilyMemberId,
                request.EventType,
                Tier = request.Tier is >= 1 and <= 3 ? request.Tier : (short)2,
                OccurredAt = occurred,
                SourceSystem = string.IsNullOrWhiteSpace(request.SourceSystem) ? "manual" : request.SourceSystem.Trim(),
                request.SourceRefType,
                request.SourceRefId,
                PayloadJson = payload,
                request.CorrelationId,
                ActorUserId = actorUserId,
            });
        return Map(row);
    }

    private static bool IsJsonObject(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }

    private static CareEventDto Map(CareEventRow r) => new(
        r.Id,
        r.CustomerId,
        r.FamilyMemberId,
        r.EventType,
        r.Tier,
        new DateTimeOffset(DateTime.SpecifyKind(r.OccurredAt, DateTimeKind.Utc)),
        r.SourceSystem,
        r.SourceRefType,
        r.SourceRefId,
        r.PayloadJson,
        r.CorrelationId,
        new DateTimeOffset(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc)));

    private sealed class CareEventRow
    {
        public Guid Id { get; init; }
        public Guid? CustomerId { get; init; }
        public Guid? FamilyMemberId { get; init; }
        public string EventType { get; init; } = "";
        public short Tier { get; init; }
        public DateTime OccurredAt { get; init; }
        public string SourceSystem { get; init; } = "";
        public string? SourceRefType { get; init; }
        public Guid? SourceRefId { get; init; }
        public string PayloadJson { get; init; } = "{}";
        public Guid? CorrelationId { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
