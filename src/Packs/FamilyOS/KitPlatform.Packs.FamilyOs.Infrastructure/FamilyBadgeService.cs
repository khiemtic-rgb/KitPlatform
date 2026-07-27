using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyBadgeRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyBadgeRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task EnsureSeedAsync(Guid familyId, CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        foreach (var seed in Seeds)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.badge_definition (
                    tenant_id, family_id, code, label_vi, unlock_json, rule_json, active
                )
                VALUES (
                    @TenantId, @FamilyId, @Code, @LabelVi, @UnlockJson::jsonb, @RuleJson::jsonb, TRUE
                )
                ON CONFLICT (tenant_id, family_id, code) DO NOTHING
                """,
                new
                {
                    TenantId,
                    FamilyId = familyId,
                    seed.Code,
                    seed.LabelVi,
                    UnlockJson = JsonSerializer.Serialize(seed.Unlock),
                    RuleJson = JsonSerializer.Serialize(seed.Rule),
                });
        }
    }

    public async Task<IReadOnlyList<FamilyBadgeDto>> ListForMemberAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<BadgeJoinRow>(
            """
            SELECT b.id AS Id,
                   d.code AS Code,
                   d.label_vi AS LabelVi,
                   b.awarded_at AS AwardedAt
            FROM pack_family.member_badge b
            INNER JOIN pack_family.badge_definition d ON d.id = b.badge_id
            WHERE b.tenant_id = @TenantId
              AND b.family_id = @FamilyId
              AND b.member_id = @MemberId
            ORDER BY b.awarded_at DESC
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });

        return rows
            .Select(r => new FamilyBadgeDto(r.Id, r.Code, r.LabelVi, r.AwardedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<BadgeDefRow>> ListActiveDefsAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<BadgeDefRow>(
            """
            SELECT id AS Id, code AS Code, label_vi AS LabelVi, rule_json::text AS RuleJson
            FROM pack_family.badge_definition
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND active = TRUE
            """,
            new { TenantId, FamilyId = familyId });
        return rows.ToList();
    }

    public async Task TryAwardAsync(
        Guid familyId,
        Guid memberId,
        Guid badgeId,
        object payload,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.member_badge (
                tenant_id, family_id, member_id, badge_id, payload_json
            )
            VALUES (
                @TenantId, @FamilyId, @MemberId, @BadgeId, @Payload::jsonb
            )
            ON CONFLICT (tenant_id, member_id, badge_id) DO NOTHING
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                MemberId = memberId,
                BadgeId = badgeId,
                Payload = JsonSerializer.Serialize(payload),
            });
    }

    public async Task<int> CountKindnessDoneAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*)::int
            FROM pack_family.commitment c
            INNER JOIN pack_family.day_flow d ON d.id = c.day_flow_id
            WHERE c.tenant_id = @TenantId
              AND d.family_id = @FamilyId
              AND c.member_id = @MemberId
              AND c.status = 'done'
              AND c.deleted_at IS NULL
              AND (
                    c.currency_category = 'kindness'
                 OR LOWER(c.title) LIKE '%giúp%'
                 OR LOWER(c.title) LIKE '%chia sẻ%'
              )
            """,
            new { TenantId, FamilyId = familyId, MemberId = memberId });
    }

    private static readonly SeedBadge[] Seeds =
    [
        new("read_30d", "Đọc sách 30 ngày", ["avatar_frame"],
            new { streakDays = 30, titleContains = new[] { "đọc", "sách" } }),
        new("room_tidy_14d", "Giữ phòng gọn 14 ngày", ["story_chapter"],
            new { streakDays = 14, titleContains = new[] { "phòng", "dọn" } }),
        new("peace_7d", "Không cãi bố mẹ 7 ngày", ["title"],
            new { streakDays = 7, eventType = "family_peace_day" }),
        new("help_10x", "Giúp người khác 10 lần", ["badge_wall"],
            new { count = 10, category = "kindness" }),
    ];

    private sealed record SeedBadge(string Code, string LabelVi, string[] Unlock, object Rule);

    internal sealed class BadgeJoinRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = "";
        public string LabelVi { get; init; } = "";
        public DateTimeOffset AwardedAt { get; init; }
    }

    internal sealed class BadgeDefRow
    {
        public Guid Id { get; init; }
        public string Code { get; init; } = "";
        public string LabelVi { get; init; } = "";
        public string RuleJson { get; init; } = "{}";
    }
}

internal sealed class FamilyBadgeService : IFamilyBadgeService
{
    private readonly FamilyBadgeRepository _badges;
    private readonly FamilyGraphRepository _families;

    public FamilyBadgeService(FamilyBadgeRepository badges, FamilyGraphRepository families)
    {
        _badges = badges;
        _families = families;
    }

    public async Task EnsureSeedBadgesAsync(Guid familyId, CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        if (family is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");
        await _badges.EnsureSeedAsync(familyId, cancellationToken);
    }

    public async Task<IReadOnlyList<FamilyBadgeDto>> ListMemberBadgesAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedBadgesAsync(familyId, cancellationToken);
        return await _badges.ListForMemberAsync(familyId, memberId, cancellationToken);
    }

    public async Task EvaluateAfterCommitmentDoneAsync(
        Guid familyId,
        Guid memberId,
        Guid commitmentId,
        string title,
        string? currencyCategory,
        int habitStreakDays,
        CancellationToken cancellationToken = default)
    {
        await EnsureSeedBadgesAsync(familyId, cancellationToken);
        var defs = await _badges.ListActiveDefsAsync(familyId, cancellationToken);
        var titleLower = (title ?? "").ToLowerInvariant();
        var category = currencyCategory ?? FamilyCurrencyAllocator.InferCategory(title, null);

        foreach (var def in defs)
        {
            var award = def.Code switch
            {
                "read_30d" when ContainsAny(titleLower, "đọc", "sách") && habitStreakDays >= 30 => true,
                "room_tidy_14d" when ContainsAny(titleLower, "phòng", "dọn") && habitStreakDays >= 14 => true,
                "help_10x" when category == FamilyCurrencyCategories.Kindness
                    || ContainsAny(titleLower, "giúp", "chia sẻ") =>
                    await _badges.CountKindnessDoneAsync(familyId, memberId, cancellationToken) >= 10,
                _ => false,
            };

            if (!award)
                continue;

            await _badges.TryAwardAsync(
                familyId,
                memberId,
                def.Id,
                new { commitmentId, habitStreakDays, title },
                cancellationToken);
        }
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));
}
