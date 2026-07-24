using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyTeamUnlockRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public FamilyTeamUnlockRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<UnlockRow?> GetAsync(
        Guid familyId,
        Guid unlockId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<UnlockRow>(
            """
            SELECT
                id AS Id, family_id AS FamilyId, day_flow_id AS DayFlowId,
                flow_date AS FlowDate, reward_code AS RewardCode, label_vi AS LabelVi,
                agreement_id AS AgreementId, team_done AS TeamDone, team_total AS TeamTotal,
                team_percent AS TeamPercent, status AS Status,
                confirmed_by AS ConfirmedBy, confirmed_at AS ConfirmedAt,
                decision_note AS DecisionNote, created_at AS CreatedAt
            FROM pack_family.team_unlock_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @UnlockId
              AND deleted_at IS NULL
            """,
            new { TenantId, FamilyId = familyId, UnlockId = unlockId });
    }

    public async Task<IReadOnlyList<UnlockRow>> ListAsync(
        Guid familyId,
        DateOnly? flowDate,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var rows = await conn.QueryAsync<UnlockRow>(
            """
            SELECT
                id AS Id, family_id AS FamilyId, day_flow_id AS DayFlowId,
                flow_date AS FlowDate, reward_code AS RewardCode, label_vi AS LabelVi,
                agreement_id AS AgreementId, team_done AS TeamDone, team_total AS TeamTotal,
                team_percent AS TeamPercent, status AS Status,
                confirmed_by AS ConfirmedBy, confirmed_at AS ConfirmedAt,
                decision_note AS DecisionNote, created_at AS CreatedAt
            FROM pack_family.team_unlock_event
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND (@FlowDate::date IS NULL OR flow_date = @FlowDate::date)
            ORDER BY flow_date DESC, created_at DESC
            """,
            new { TenantId, FamilyId = familyId, FlowDate = flowDate });
        return rows.AsList();
    }

    public async Task UpsertPendingAsync(
        Guid familyId,
        Guid dayFlowId,
        DateOnly flowDate,
        string rewardCode,
        string labelVi,
        Guid? agreementId,
        int teamDone,
        int teamTotal,
        int teamPercent,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            """
            INSERT INTO pack_family.team_unlock_event (
                tenant_id, family_id, day_flow_id, flow_date,
                reward_code, label_vi, agreement_id,
                team_done, team_total, team_percent, status
            )
            VALUES (
                @TenantId, @FamilyId, @DayFlowId, @FlowDate,
                @RewardCode, @LabelVi, @AgreementId,
                @TeamDone, @TeamTotal, @TeamPercent, 'pending_confirm'
            )
            ON CONFLICT (tenant_id, family_id, flow_date, reward_code)
            DO UPDATE SET
                day_flow_id = EXCLUDED.day_flow_id,
                label_vi = EXCLUDED.label_vi,
                agreement_id = COALESCE(EXCLUDED.agreement_id, pack_family.team_unlock_event.agreement_id),
                team_done = EXCLUDED.team_done,
                team_total = EXCLUDED.team_total,
                team_percent = EXCLUDED.team_percent,
                -- Do not reopen confirmed/deferred unlocks
                status = CASE
                    WHEN pack_family.team_unlock_event.status IN ('confirmed', 'deferred')
                        THEN pack_family.team_unlock_event.status
                    ELSE 'pending_confirm'
                END,
                updated_at = NOW(),
                deleted_at = NULL
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                DayFlowId = dayFlowId,
                FlowDate = flowDate,
                RewardCode = rewardCode,
                LabelVi = labelVi,
                AgreementId = agreementId,
                TeamDone = teamDone,
                TeamTotal = teamTotal,
                TeamPercent = teamPercent,
            });
    }

    public async Task<UnlockRow?> DecideAsync(
        Guid familyId,
        Guid unlockId,
        string status,
        Guid confirmedBy,
        string? decisionNote,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<UnlockRow>(
            """
            UPDATE pack_family.team_unlock_event
            SET status = @Status,
                confirmed_by = @ConfirmedBy,
                confirmed_at = NOW(),
                decision_note = @DecisionNote,
                updated_at = NOW()
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND id = @UnlockId
              AND deleted_at IS NULL
              AND status = 'pending_confirm'
            RETURNING
                id AS Id, family_id AS FamilyId, day_flow_id AS DayFlowId,
                flow_date AS FlowDate, reward_code AS RewardCode, label_vi AS LabelVi,
                agreement_id AS AgreementId, team_done AS TeamDone, team_total AS TeamTotal,
                team_percent AS TeamPercent, status AS Status,
                confirmed_by AS ConfirmedBy, confirmed_at AS ConfirmedAt,
                decision_note AS DecisionNote, created_at AS CreatedAt
            """,
            new
            {
                TenantId,
                FamilyId = familyId,
                UnlockId = unlockId,
                Status = status,
                ConfirmedBy = confirmedBy,
                DecisionNote = decisionNote,
            });
    }

    public async Task<RewardPickRow?> PickFamilyRewardAsync(
        Guid familyId,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        // Prefer accepted reward agreements with family/experience codes
        var fromAgreement = await conn.QuerySingleOrDefaultAsync<RewardPickRow>(
            """
            SELECT
                a.id AS AgreementId,
                COALESCE(a.terms->>'rewardCode', a.terms->'result'->>'code') AS RewardCode,
                COALESCE(
                    a.terms->'result'->>'labelVi',
                    o.label_vi,
                    a.title
                ) AS LabelVi
            FROM pack_family.agreement a
            LEFT JOIN pack_family.accountability_option o
                ON o.family_id = a.family_id
               AND o.code = COALESCE(a.terms->>'rewardCode', a.terms->'result'->>'code')
               AND o.kind = 'reward'
               AND o.deleted_at IS NULL
            WHERE a.tenant_id = @TenantId
              AND a.family_id = @FamilyId
              AND a.deleted_at IS NULL
              AND a.status = 'accepted'
              AND a.target_type IN ('reward', 'reward_rule')
              AND COALESCE(a.terms->>'rewardCode', a.terms->'result'->>'code') IS NOT NULL
              AND COALESCE(o.option_group, a.terms->'result'->>'group', 'family')
                  IN ('family', 'experience')
            ORDER BY
                CASE COALESCE(a.terms->>'rewardCode', a.terms->'result'->>'code')
                    WHEN 'reward_choose_movie_sat' THEN 0
                    WHEN 'reward_movie_outing' THEN 1
                    WHEN 'reward_family_activity' THEN 2
                    ELSE 9
                END,
                a.decided_at DESC NULLS LAST
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId });

        if (fromAgreement is not null && !string.IsNullOrWhiteSpace(fromAgreement.RewardCode))
            return fromAgreement;

        var fromCatalog = await conn.QuerySingleOrDefaultAsync<RewardPickRow>(
            """
            SELECT
                NULL::uuid AS AgreementId,
                code AS RewardCode,
                label_vi AS LabelVi
            FROM pack_family.accountability_option
            WHERE tenant_id = @TenantId
              AND family_id = @FamilyId
              AND deleted_at IS NULL
              AND kind = 'reward'
              AND status = 'active'
              AND option_group IN ('family', 'experience')
            ORDER BY
                CASE code
                    WHEN 'reward_choose_movie_sat' THEN 0
                    WHEN 'reward_movie_outing' THEN 1
                    WHEN 'reward_family_activity' THEN 2
                    ELSE 9
                END,
                sort_order
            LIMIT 1
            """,
            new { TenantId, FamilyId = familyId });

        return fromCatalog;
    }

    internal sealed class UnlockRow
    {
        public Guid Id { get; init; }
        public Guid FamilyId { get; init; }
        public Guid DayFlowId { get; init; }
        public DateOnly FlowDate { get; init; }
        public string RewardCode { get; init; } = "";
        public string LabelVi { get; init; } = "";
        public Guid? AgreementId { get; init; }
        public int TeamDone { get; init; }
        public int TeamTotal { get; init; }
        public int TeamPercent { get; init; }
        public string Status { get; init; } = "";
        public Guid? ConfirmedBy { get; init; }
        public DateTimeOffset? ConfirmedAt { get; init; }
        public string? DecisionNote { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }

    internal sealed class RewardPickRow
    {
        public Guid? AgreementId { get; init; }
        public string RewardCode { get; init; } = "";
        public string LabelVi { get; init; } = "";
    }
}
