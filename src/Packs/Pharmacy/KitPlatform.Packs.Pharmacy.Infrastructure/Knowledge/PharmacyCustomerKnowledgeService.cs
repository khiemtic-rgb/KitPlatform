using Dapper;
using Npgsql;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed class PharmacyCustomerKnowledgeService : IPharmacyCustomerKnowledgeService
{
    private readonly IDbConnectionFactory _db;

    public PharmacyCustomerKnowledgeService(IDbConnectionFactory db) => _db = db;

    public async Task<CustomerKnowledgeListResult> ListAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        if (customerId == Guid.Empty)
            throw new InvalidOperationException("Thiếu khách hàng.");
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var rows = await conn.QueryAsync<KnowledgeEntryRow>(
                new CommandDefinition(
                    """
                    SELECT e.id AS Id,
                           e.kind AS Kind,
                           e.body AS Body,
                           e.source_kind AS SourceKind,
                           e.needs_verification AS NeedsVerification,
                           e.recorded_by AS RecordedBy,
                           u.username AS RecordedByName,
                           e.recorded_at AS RecordedAt
                    FROM pharmacy_customer_knowledge_entry e
                    LEFT JOIN users u
                      ON u.tenant_id = e.tenant_id
                     AND u.id::text = e.recorded_by
                    WHERE e.tenant_id = @TenantId
                      AND e.customer_id = @CustomerId
                      AND e.revoked_at IS NULL
                    ORDER BY e.recorded_at DESC
                    """,
                    new { TenantId = tenantId, CustomerId = customerId },
                    cancellationToken: cancellationToken));
            var entries = rows.Select(row => new CustomerKnowledgeEntryDto(
                row.Id,
                row.Kind,
                row.Body,
                row.SourceKind,
                row.NeedsVerification,
                row.RecordedBy,
                row.RecordedByName,
                new DateTimeOffset(DateTime.SpecifyKind(row.RecordedAt, DateTimeKind.Utc)))).ToList();
            return new CustomerKnowledgeListResult(entries, true);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return new CustomerKnowledgeListResult([], false);
        }
    }

    public async Task<CustomerKnowledgeEntryDto> CreateAsync(
        Guid tenantId,
        Guid userId,
        CustomerKnowledgeWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        if (request.CustomerId == Guid.Empty)
            throw new InvalidOperationException("Thiếu khách hàng.");
        var kind = PharmacyCustomerKnowledgeRules.NormalizeKind(request.Kind);
        var source = PharmacyCustomerKnowledgeRules.NormalizeSource(request.SourceKind);
        var body = PharmacyCustomerKnowledgeRules.RequireBody(request.Body);
        var needsVerification = request.NeedsVerification || source == "UNVERIFIED";
        var actor = userId.ToString("D");
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var customerExists = await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    """
                    SELECT EXISTS (
                        SELECT 1 FROM customers
                        WHERE id = @CustomerId AND tenant_id = @TenantId AND deleted_at IS NULL)
                    """,
                    new { TenantId = tenantId, request.CustomerId },
                    cancellationToken: cancellationToken));
            if (!customerExists)
                throw new InvalidOperationException("Không tìm thấy khách hàng.");

            var id = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pharmacy_customer_knowledge_entry (
                        tenant_id, customer_id, kind, body, source_kind, needs_verification, recorded_by)
                    VALUES (
                        @TenantId, @CustomerId, @Kind, @Body, @SourceKind, @NeedsVerification, @Actor)
                    RETURNING id
                    """,
                    new
                    {
                        TenantId = tenantId,
                        request.CustomerId,
                        Kind = kind,
                        Body = body,
                        SourceKind = source,
                        NeedsVerification = needsVerification,
                        Actor = actor,
                    },
                    cancellationToken: cancellationToken));
            var listed = await ListAsync(tenantId, request.CustomerId, cancellationToken);
            return listed.Entries.First(entry => entry.Id == id);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw new InvalidOperationException("Kho ghi nhận khách hàng chưa được bật trên database.");
        }
    }

    public async Task RevokeAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        Guid entryId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        var actor = userId.ToString("D");
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var row = await conn.QuerySingleOrDefaultAsync<KnowledgeEntryHead>(
                new CommandDefinition(
                    """
                    SELECT recorded_by AS RecordedBy, revoked_at AS RevokedAt
                    FROM pharmacy_customer_knowledge_entry
                    WHERE id = @Id AND tenant_id = @TenantId
                    """,
                    new { Id = entryId, TenantId = tenantId },
                    cancellationToken: cancellationToken));
            if (row is null)
                throw new InvalidOperationException("Không tìm thấy ghi nhận.");
            if (row.RevokedAt is not null)
                return;
            if (!isTenantAdmin && !string.Equals(row.RecordedBy, actor, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Không có quyền thu hồi ghi nhận này.");
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE pharmacy_customer_knowledge_entry
                    SET revoked_at = NOW(), revoked_by = @Actor
                    WHERE id = @Id AND tenant_id = @TenantId AND revoked_at IS NULL
                    """,
                    new { Id = entryId, TenantId = tenantId, Actor = actor },
                    cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw new InvalidOperationException("Kho ghi nhận khách hàng chưa được bật trên database.");
        }
    }

    private static void RequireTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Thiếu tenant.");
    }

    private sealed class KnowledgeEntryHead
    {
        public string RecordedBy { get; init; } = "";
        public DateTime? RevokedAt { get; init; }
    }

    private sealed class KnowledgeEntryRow
    {
        public Guid Id { get; init; }
        public string Kind { get; init; } = "";
        public string Body { get; init; } = "";
        public string SourceKind { get; init; } = "";
        public bool NeedsVerification { get; init; }
        public string RecordedBy { get; init; } = "";
        public string? RecordedByName { get; init; }
        public DateTime RecordedAt { get; init; }
    }
}
