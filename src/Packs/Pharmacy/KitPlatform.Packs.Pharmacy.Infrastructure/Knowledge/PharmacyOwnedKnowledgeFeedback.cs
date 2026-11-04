using Dapper;
using Npgsql;
using KitPlatform.Packs.Pharmacy.Knowledge;

namespace KitPlatform.Packs.Pharmacy.Infrastructure.Knowledge;

internal sealed partial class PharmacyOwnedKnowledgeService
{
    private const string FeedbackStoreMissing = "Phản hồi tri thức chưa được bật trên database.";

    public async Task RecordFeedbackAsync(
        Guid tenantId,
        Guid userId,
        bool isTenantAdmin,
        PharmacyKnowledgeFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        RequireUser(userId);
        if (request is null)
            throw new InvalidOperationException("Thiếu nội dung phản hồi.");
        var kind = PharmacyKnowledgeFeedbackKinds.NormalizeSubmitted(request.Kind);
        var query = PharmacyKnowledgeFeedbackKinds.NormalizeQuery(request.Query, kind);
        var knowledgeId = request.KnowledgeId is Guid id && id != Guid.Empty ? id : (Guid?)null;
        if (kind == PharmacyKnowledgeFeedbackKinds.NotHelpful && knowledgeId is null)
            throw new InvalidOperationException("Cần chọn tri thức chưa giúp được.");
        if (knowledgeId is Guid target
            && await GetInUseAsync(tenantId, userId, isTenantAdmin, target, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy tri thức đang dùng.");
        try
        {
            await InsertFeedbackAsync(tenantId, userId, knowledgeId, kind, query, cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw new InvalidOperationException(FeedbackStoreMissing);
        }
    }

    public async Task RecordOpenAsync(
        Guid tenantId,
        Guid userId,
        Guid knowledgeId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || userId == Guid.Empty || knowledgeId == Guid.Empty)
            return;
        try
        {
            await InsertFeedbackAsync(tenantId, userId, knowledgeId, PharmacyKnowledgeFeedbackKinds.Open, null, cancellationToken);
        }
        catch (NpgsqlException)
        {
            // Usage counting must never block reading an in-use item.
        }
    }

    public async Task<PharmacyKnowledgeFeedbackSummaryDto> GetFeedbackSummaryAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        RequireTenant(tenantId);
        try
        {
            await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
            var counts = await conn.QueryAsync<PharmacyKnowledgeFeedbackCountDto>(
                new CommandDefinition(
                    """
                    SELECT knowledge_id AS KnowledgeId,
                           COUNT(*) FILTER (WHERE kind = 'open')::int AS OpenCount,
                           COUNT(*) FILTER (WHERE kind = 'not_helpful')::int AS NotHelpfulCount,
                           COUNT(*) FILTER (WHERE kind = 'missing')::int AS MissingCount
                    FROM pharmacy_knowledge_feedback
                    WHERE tenant_id = @TenantId
                      AND knowledge_id IS NOT NULL
                    GROUP BY knowledge_id
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));
            var missing = await conn.QueryAsync<PharmacyKnowledgeMissingDto>(
                new CommandDefinition(
                    """
                    SELECT query_text AS Query,
                           created_at AS CreatedAt
                    FROM pharmacy_knowledge_feedback
                    WHERE tenant_id = @TenantId
                      AND kind = 'missing'
                      AND query_text IS NOT NULL
                    ORDER BY created_at DESC
                    LIMIT 20
                    """,
                    new { TenantId = tenantId },
                    cancellationToken: cancellationToken));
            return new PharmacyKnowledgeFeedbackSummaryDto(counts.ToList(), missing.ToList(), true);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return new PharmacyKnowledgeFeedbackSummaryDto([], [], false);
        }
    }

    private async Task InsertFeedbackAsync(
        Guid tenantId,
        Guid userId,
        Guid? knowledgeId,
        string kind,
        string? query,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pharmacy_knowledge_feedback (tenant_id, knowledge_id, kind, query_text, user_id)
                VALUES (@TenantId, @KnowledgeId, @Kind, @Query, @UserId)
                """,
                new { TenantId = tenantId, KnowledgeId = knowledgeId, Kind = kind, Query = query, UserId = userId },
                cancellationToken: cancellationToken));
    }
}
