using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesPainService : IKitSalesPainService
{
    private static readonly Guid TenantId = SalesPackDefinition.DedicatedTenantId;
    private readonly IDbConnectionFactory _db;

    public KitSalesPainService(IDbConnectionFactory db) => _db = db;

    public IReadOnlyList<KitSalesPainDto> ListPains() =>
        NovixaPainCatalog.All.Select(NovixaPainCatalog.ToDto).ToList();

    public async Task EnsureCatalogAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        foreach (var pain in NovixaPainCatalog.All)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_sales.pain (
                    code, product_code, category, name, description, payload, explore_weight, version
                )
                VALUES (
                    @Code, 'novixa', @Category, @Name, @Description, CAST(@Payload AS jsonb), @Weight, @Version
                )
                ON CONFLICT (code) DO UPDATE SET
                    category = EXCLUDED.category,
                    name = EXCLUDED.name,
                    description = EXCLUDED.description,
                    payload = EXCLUDED.payload,
                    explore_weight = EXCLUDED.explore_weight,
                    version = EXCLUDED.version,
                    updated_at = NOW()
                """,
                new
                {
                    pain.Code,
                    pain.Category,
                    pain.Name,
                    pain.Description,
                    Payload = NovixaPainCatalog.CatalogJson(pain),
                    Weight = NovixaPainCatalog.ExploreShare.TryGetValue(pain.Category, out var share)
                        ? share
                        : 0.10m,
                    Version = NovixaPainCatalog.Version,
                });
        }
    }

    public async Task<KitSalesPainMarketDto> GetMarketAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await conn.QueryAsync<LeadPainRow>(
            """
            SELECT pain_code AS PainCode, category AS Category, confidence AS Confidence,
                   response_code AS ResponseCode, approached_count AS ApproachedCount,
                   response_score AS ResponseScore, engagement_score AS EngagementScore,
                   confirmed AS Confirmed, last_approached_at AS LastApproachedAt
            FROM pack_sales.lead_pain
            WHERE tenant_id = @TenantId
            """,
            new { TenantId })).ToList();

        var buckets = NovixaPainCatalog.ExploreShare.Keys.Select(category =>
        {
            var set = rows.Where(r => r.Category == category).ToList();
            var approached = set.Sum(r => r.ApproachedCount);
            var replied = set.Count(r => r.ResponseCode is not null && r.ResponseCode != "no_response");
            var interested = set.Count(r => NovixaPainDiscovery.IsPositive(r.ResponseCode));
            var rate = approached <= 0 ? 0 : Math.Round((decimal)interested / approached, 3);
            return new KitSalesPainMarketBucketDto(
                category,
                NovixaPainCatalog.CategoryLabel(category),
                approached,
                replied,
                interested,
                rate,
                NovixaPainCatalog.ExploreShare[category]);
        }).ToList();

        var touched = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(DISTINCT lead_id)
            FROM pack_sales.lead_pain
            WHERE tenant_id = @TenantId AND approached_count > 0
            """,
            new { TenantId });

        return new KitSalesPainMarketDto(NovixaPainDiscovery.Mode(touched), touched, buckets);
    }

    public async Task<KitSalesLeadPainProfileDto> GetLeadProfileAsync(
        Guid leadId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM pack_sales.lead WHERE id = @LeadId AND tenant_id = @TenantId)",
            new { LeadId = leadId, TenantId });
        if (!exists)
            throw new InvalidOperationException("Lead không tồn tại.");

        var rows = (await conn.QueryAsync<LeadPainRow>(
            """
            SELECT pain_code AS PainCode, category AS Category, confidence AS Confidence,
                   response_code AS ResponseCode, approached_count AS ApproachedCount,
                   response_score AS ResponseScore, engagement_score AS EngagementScore,
                   confirmed AS Confirmed, last_approached_at AS LastApproachedAt,
                   evidence AS Evidence
            FROM pack_sales.lead_pain
            WHERE tenant_id = @TenantId AND lead_id = @LeadId
            """,
            new { TenantId, LeadId = leadId })).ToList();

        var market = await GetMarketAsync(cancellationToken);
        var pick = NovixaPainDiscovery.Pick(rows, market.ByCategory, market.TouchedLeads);
        var scores = NovixaPainCatalog.ExploreShare.Keys.Select(category =>
        {
            var best = rows
                .Where(r => r.Category == category)
                .OrderByDescending(r => r.Confidence)
                .FirstOrDefault();
            var sample = NovixaPainCatalog.All.First(p => p.Category == category);
            return new KitSalesLeadPainScoreDto(
                best?.PainCode ?? sample.Code,
                category,
                NovixaPainCatalog.CategoryLabel(category),
                best?.Confidence ?? 0,
                best?.ResponseCode,
                rows.Where(r => r.Category == category).Sum(r => r.ApproachedCount),
                best?.Confirmed ?? false);
        }).OrderByDescending(s => s.Confidence).ToList();

        return new KitSalesLeadPainProfileDto(
            leadId,
            pick.Pain.Code,
            pick.Mode,
            pick.Why,
            scores,
            market.ByCategory);
    }

    public async Task RecordApproachAsync(
        Guid leadId,
        string painCode,
        CancellationToken cancellationToken = default)
    {
        var pain = NovixaPainCatalog.Get(painCode);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.lead_pain (
                tenant_id, lead_id, pain_code, category, source, approached_count, last_approached_at
            )
            VALUES (
                @TenantId, @LeadId, @PainCode, @Category, 'outreach', 1, NOW()
            )
            ON CONFLICT (tenant_id, lead_id, pain_code) DO UPDATE SET
                approached_count = pack_sales.lead_pain.approached_count + 1,
                last_approached_at = NOW(),
                source = 'outreach',
                updated_at = NOW()
            """,
            new { TenantId, LeadId = leadId, PainCode = pain.Code, pain.Category });
    }

    public async Task RecordResponseAsync(
        Guid leadId,
        string painCode,
        RecordKitSalesPainResponseRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = (request.ResponseCode ?? "").Trim().ToLowerInvariant();
        if (!NovixaPainDiscovery.ResponseCodes.Contains(code))
            throw new ArgumentException("Mã phản hồi không hợp lệ.");

        var pain = NovixaPainCatalog.Get(painCode);
        var points = NovixaPainDiscovery.ResponsePoints(code);
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var current = await conn.QuerySingleOrDefaultAsync<LeadPainRow>(
            """
            SELECT pain_code AS PainCode, category AS Category, confidence AS Confidence,
                   response_code AS ResponseCode, approached_count AS ApproachedCount,
                   response_score AS ResponseScore, engagement_score AS EngagementScore,
                   confirmed AS Confirmed
            FROM pack_sales.lead_pain
            WHERE tenant_id = @TenantId AND lead_id = @LeadId AND pain_code = @PainCode
            """,
            new { TenantId, LeadId = leadId, PainCode = pain.Code });

        var confidence = NovixaPainDiscovery.BlendConfidence(current?.Confidence ?? 0, points);
        var engagement = Math.Clamp((current?.EngagementScore ?? 0) + Math.Max(points, 5), 0, 100);
        var confirmed = NovixaPainDiscovery.IsConfirmed(code);
        var evidence = string.IsNullOrWhiteSpace(request.Note) ? current?.Evidence : request.Note.Trim();

        await conn.ExecuteAsync(
            """
            INSERT INTO pack_sales.lead_pain (
                tenant_id, lead_id, pain_code, category, confidence, evidence, source,
                response_code, approached_count, response_score, engagement_score,
                confirmed, last_response_at
            )
            VALUES (
                @TenantId, @LeadId, @PainCode, @Category, @Confidence, @Evidence, 'response',
                @ResponseCode, 1, @Points, @Engagement, @Confirmed, NOW()
            )
            ON CONFLICT (tenant_id, lead_id, pain_code) DO UPDATE SET
                confidence = @Confidence,
                evidence = COALESCE(@Evidence, pack_sales.lead_pain.evidence),
                source = 'response',
                response_code = @ResponseCode,
                response_score = @Points,
                engagement_score = @Engagement,
                confirmed = @Confirmed,
                last_response_at = NOW(),
                updated_at = NOW()
            """,
            new
            {
                TenantId,
                LeadId = leadId,
                PainCode = pain.Code,
                pain.Category,
                Confidence = confidence,
                Evidence = evidence,
                ResponseCode = code,
                Points = points,
                Engagement = engagement,
                Confirmed = confirmed,
            });
    }
}
