using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyRewardRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyRewardRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<int> CountCatalogAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.reward_catalog
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND active = TRUE
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task InsertDefaultCatalogAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.reward_catalog (
                tenant_id, family_id, title, icon, cost, description, sort_order, tone, is_special, active
            )
            VALUES
                (@TenantId, @FamilyId, 'Kem yêu thích', '🍦', 100, '1 ly kem tùy chọn', 1, 'pink', FALSE, TRUE),
                (@TenantId, @FamilyId, 'Đồ chơi nhỏ', '🧸', 500, 'Gấu bông hoặc đồ chơi nhỏ', 2, 'lemon', FALSE, TRUE),
                (@TenantId, @FamilyId, 'Sách mới', '📖', 800, '1 cuốn sách mới', 3, 'sky', FALSE, TRUE),
                (@TenantId, @FamilyId, '30 phút chơi game', '🎮', 300, '30 phút chơi game', 4, 'mint', FALSE, TRUE),
                (@TenantId, @FamilyId, 'Bố mẹ chọn', '🎁', NULL, 'Phần thưởng do bố mẹ lựa chọn cho cả gia đình', 5, 'lilac', TRUE, TRUE)
            """,
            new { TenantId, FamilyId = familyId });
    }

    public async Task<IReadOnlyList<CatalogRow>> ListCatalogAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<CatalogRow>(
            """
            SELECT
                id AS Id,
                title AS Title,
                icon AS Icon,
                cost AS Cost,
                tone AS Tone,
                is_special AS IsSpecial,
                sort_order AS SortOrder,
                description AS Description,
                active AS Active
            FROM pack_family.reward_catalog
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND active = TRUE
            ORDER BY sort_order, created_at
            """,
            new { TenantId, FamilyId = familyId });
        return rows.AsList();
    }

    public async Task<CatalogRow?> GetCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<CatalogRow>(
            """
            SELECT
                id AS Id,
                title AS Title,
                icon AS Icon,
                cost AS Cost,
                tone AS Tone,
                is_special AS IsSpecial,
                sort_order AS SortOrder,
                description AS Description,
                active AS Active
            FROM pack_family.reward_catalog
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @CatalogId
              AND active = TRUE
            """,
            new { TenantId, FamilyId = familyId, CatalogId = catalogId });
    }

    public async Task<IReadOnlyList<RedemptionRow>> ListRedemptionsAsync(
        Guid familyId,
        Guid? memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<RedemptionRow>(
            """
            SELECT
                r.id AS Id,
                r.catalog_id AS CatalogId,
                c.title AS Title,
                c.icon AS Icon,
                r.star_cost AS StarCost,
                r.status AS Status,
                r.created_at AS CreatedAt,
                r.fulfilled_at AS FulfilledAt
            FROM pack_family.reward_redemption r
            INNER JOIN pack_family.reward_catalog c
                ON c.tenant_id = r.tenant_id
               AND c.id = r.catalog_id
            WHERE r.tenant_id = @TenantId
              AND r.family_id = @FamilyId
              AND (@MemberId IS NULL OR r.member_id = @MemberId)
              AND r.status <> 'cancelled'
            ORDER BY r.created_at DESC
            LIMIT 50
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
        return rows.AsList();
    }

    public async Task<RedemptionRow?> GetRedemptionAsync(
        Guid familyId,
        Guid redemptionId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<RedemptionRow>(
            """
            SELECT
                r.id AS Id,
                r.catalog_id AS CatalogId,
                c.title AS Title,
                c.icon AS Icon,
                r.star_cost AS StarCost,
                r.status AS Status,
                r.created_at AS CreatedAt,
                r.fulfilled_at AS FulfilledAt
            FROM pack_family.reward_redemption r
            INNER JOIN pack_family.reward_catalog c
                ON c.tenant_id = r.tenant_id
               AND c.id = r.catalog_id
            WHERE r.tenant_id = @TenantId
              AND r.family_id = @FamilyId
              AND r.id = @RedemptionId
            """,
            new { TenantId, FamilyId = familyId, RedemptionId = redemptionId });
    }

    public async Task<(Guid RedemptionId, Guid LedgerId, int Balance)> RedeemAsync(
        Guid familyId,
        Guid memberId,
        Guid catalogId,
        int starCost,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var balance = await conn.ExecuteScalarAsync<int>(
            """
            SELECT COALESCE(SUM(delta), 0)::int
            FROM pack_family.star_ledger
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND member_id = @MemberId
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId },
            tx);

        if (balance < starCost)
            throw new InvalidOperationException("Không đủ sao để đổi quà này.");

        var redemptionId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.reward_redemption (
                tenant_id, family_id, member_id, catalog_id, star_cost, status
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @CatalogId, @StarCost, 'pending'
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                CatalogId = catalogId,
                StarCost = starCost,
            },
            tx);

        var ledgerId = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.star_ledger (
                tenant_id, family_id, member_id, commitment_id,
                delta, tier, star_reward, late_minutes, reason
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, NULL,
                @Delta, 'redeem', @StarReward, NULL, @Reason
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                Delta = -starCost,
                StarReward = starCost,
                Reason = $"redeem:{catalogId}",
            },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE pack_family.reward_redemption
            SET ledger_id = @LedgerId
            WHERE tenant_id = @TenantId AND id = @RedemptionId
            """,
            new { TenantId, RedemptionId = redemptionId, LedgerId = ledgerId },
            tx);

        var newBalance = balance - starCost;
        await tx.CommitAsync(cancellationToken);
        return (redemptionId, ledgerId, newBalance);
    }

    public async Task<RedemptionRow?> FulfillAsync(
        Guid familyId,
        Guid redemptionId,
        Guid fulfilledBy,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_family.reward_redemption
            SET status = 'fulfilled',
                fulfilled_by = @FulfilledBy,
                fulfilled_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @RedemptionId
              AND status = 'pending'
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                RedemptionId = redemptionId,
                FulfilledBy = fulfilledBy,
            });

        if (updated == 0)
            return null;

        return await GetRedemptionAsync(familyId, redemptionId, cancellationToken);
    }

    public async Task<Guid> InsertCatalogItemAsync(
        Guid familyId,
        string title,
        string icon,
        int cost,
        string? description,
        string? tone,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO pack_family.reward_catalog (
                tenant_id, family_id, title, icon, cost, description,
                sort_order, tone, is_special, active
            )
            VALUES (
                @TenantId, @FamilyId, @Title, @Icon, @Cost, @Description,
                @SortOrder, @Tone, FALSE, TRUE
            )
            RETURNING id
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                Title = title,
                Icon = icon,
                Cost = cost,
                Description = description,
                SortOrder = sortOrder,
                Tone = tone,
            });
    }

    public async Task<bool> UpdateCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        string title,
        string icon,
        int cost,
        string? description,
        string? tone,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_family.reward_catalog
            SET title = @Title,
                icon = @Icon,
                cost = @Cost,
                description = @Description,
                tone = @Tone,
                sort_order = @SortOrder
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @CatalogId
              AND active = TRUE
              AND is_special = FALSE
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                CatalogId = catalogId,
                Title = title,
                Icon = icon,
                Cost = cost,
                Description = description,
                Tone = tone,
                SortOrder = sortOrder,
            });
        return updated > 0;
    }

    public async Task<bool> DeactivateCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_family.reward_catalog
            SET active = FALSE
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @CatalogId
              AND active = TRUE
              AND is_special = FALSE
            """,
            new { TenantId, FamilyId = familyId, CatalogId = catalogId });
        return updated > 0;
    }

    internal sealed class CatalogRow
    {
        public Guid Id { get; init; }
        public string Title { get; init; } = "";
        public string Icon { get; init; } = "🎁";
        public int? Cost { get; init; }
        public string? Tone { get; init; }
        public bool IsSpecial { get; init; }
        public int SortOrder { get; init; }
        public string? Description { get; init; }
        public bool Active { get; init; } = true;
    }

    internal sealed class RedemptionRow
    {
        public Guid Id { get; init; }
        public Guid CatalogId { get; init; }
        public string Title { get; init; } = "";
        public string Icon { get; init; } = "🎁";
        public int StarCost { get; init; }
        public string Status { get; init; } = "";
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? FulfilledAt { get; init; }
    }
}
