using Dapper;
using PharmaCore.Application.CustomerApp;
using PharmaCore.Application.Sales;
using PharmaCore.Application.Abstractions;
using PharmaCore.Infrastructure.Data;

namespace PharmaCore.Infrastructure.CustomerApp;

internal sealed class CustomerEngagementAnalyticsRepository
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CustomerEngagementAnalyticsRepository(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<FunnelCounts> GetFunnelCountsAsync(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        CancellationToken cancellationToken)
    {
        const string sql = """
            WITH cohort AS (
                SELECT ca.id, ca.customer_id
                FROM customer_accounts ca
                WHERE ca.tenant_id = @TenantId AND ca.status = 1
            ),
            app_active AS (
                SELECT c.id, c.customer_id
                FROM cohort c
                INNER JOIN customer_accounts ca ON ca.id = c.id
                WHERE ca.last_login_at >= @PeriodStart
                  AND ca.last_login_at < @PeriodEnd
            ),
            reminder_on AS (
                SELECT DISTINCT a.id, a.customer_id
                FROM app_active a
                WHERE EXISTS (
                    SELECT 1 FROM medication_reminders mr
                    WHERE mr.tenant_id = @TenantId
                      AND mr.customer_id = a.customer_id
                      AND mr.is_active = TRUE
                )
                OR EXISTS (
                    SELECT 1 FROM repurchase_suggestions rs
                    WHERE rs.customer_account_id = a.id
                      AND rs.drink_reminders_created_at IS NOT NULL
                )
            ),
            ai_usage AS (
                SELECT DISTINCT e.account_id AS id, e.customer_id
                FROM customer_engagement_events e
                INNER JOIN app_active a ON a.id = e.account_id
                WHERE e.tenant_id = @TenantId
                  AND e.event_type = 'ai_ask'
                  AND e.event_at >= @PeriodStart
                  AND e.event_at < @PeriodEnd
            ),
            chat_usage AS (
                SELECT DISTINCT a.id, a.customer_id
                FROM app_active a
                INNER JOIN customer_chat_threads t
                    ON t.tenant_id = @TenantId AND t.customer_id = a.customer_id
                INNER JOIN customer_chat_messages m
                    ON m.thread_id = t.id
                   AND m.sender_type = 1
                   AND m.created_at >= @PeriodStart
                   AND m.created_at < @PeriodEnd
            ),
            order_again AS (
                SELECT DISTINCT a.id, a.customer_id
                FROM app_active a
                WHERE (
                    SELECT COUNT(*)::int
                    FROM sales_orders so
                    WHERE so.tenant_id = @TenantId
                      AND so.customer_id = a.customer_id
                      AND so.status = @OrderCompleted
                ) >= 2
                OR EXISTS (
                    SELECT 1 FROM customer_reservations r
                    WHERE r.tenant_id = @TenantId
                      AND r.customer_id = a.customer_id
                      AND r.created_at >= @PeriodStart
                      AND r.created_at < @PeriodEnd
                )
                OR EXISTS (
                    SELECT 1 FROM repurchase_suggestions rs
                    WHERE rs.customer_account_id = a.id
                      AND rs.drink_reminders_created_at >= @PeriodStart
                      AND rs.drink_reminders_created_at < @PeriodEnd
                )
            )
            SELECT
                (SELECT COUNT(*)::int FROM cohort) AS CohortSize,
                (SELECT COUNT(*)::int FROM app_active) AS AppActive,
                (SELECT COUNT(*)::int FROM reminder_on) AS ReminderOn,
                (SELECT COUNT(*)::int FROM ai_usage) AS AiUsage,
                (SELECT COUNT(*)::int FROM chat_usage) AS Chat,
                (SELECT COUNT(*)::int FROM order_again) AS OrderAgain
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<FunnelCounts>(sql, new
        {
            TenantId,
            PeriodStart = periodStartUtc,
            PeriodEnd = periodEndUtc,
            OrderCompleted = SalesOrderStatuses.Completed,
        });
    }

    public async Task<RetentionCounts> GetRetentionCountsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                COUNT(*) FILTER (
                    WHERE COALESCE(first_login_at, created_at) <= NOW() - INTERVAL '30 days'
                )::int AS Eligible,
                COUNT(*) FILTER (
                    WHERE COALESCE(first_login_at, created_at) <= NOW() - INTERVAL '30 days'
                      AND last_login_at >= NOW() - INTERVAL '30 days'
                )::int AS Retained
            FROM customer_accounts
            WHERE tenant_id = @TenantId AND status = 1
            """;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleAsync<RetentionCounts>(sql, new { TenantId });
    }

    public async Task<(IReadOnlyList<DrillDownRow> Items, int Total)> ListDrillDownAsync(
        string step,
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var whereClause = step switch
        {
            CustomerEngagementFunnelSteps.AppActive => """
                ca.status = 1 AND ca.last_login_at >= @PeriodStart AND ca.last_login_at < @PeriodEnd
                """,
            CustomerEngagementFunnelSteps.ReminderOn => """
                ca.status = 1 AND ca.last_login_at >= @PeriodStart AND ca.last_login_at < @PeriodEnd
                AND (
                    EXISTS (
                        SELECT 1 FROM medication_reminders mr
                        WHERE mr.tenant_id = @TenantId AND mr.customer_id = ca.customer_id AND mr.is_active = TRUE
                    )
                    OR EXISTS (
                        SELECT 1 FROM repurchase_suggestions rs
                        WHERE rs.customer_account_id = ca.id AND rs.drink_reminders_created_at IS NOT NULL
                    )
                )
                """,
            CustomerEngagementFunnelSteps.AiUsage => """
                ca.status = 1 AND ca.last_login_at >= @PeriodStart AND ca.last_login_at < @PeriodEnd
                AND EXISTS (
                    SELECT 1 FROM customer_engagement_events e
                    WHERE e.account_id = ca.id AND e.tenant_id = @TenantId
                      AND e.event_type = 'ai_ask'
                      AND e.event_at >= @PeriodStart AND e.event_at < @PeriodEnd
                )
                """,
            CustomerEngagementFunnelSteps.Chat => """
                ca.status = 1 AND ca.last_login_at >= @PeriodStart AND ca.last_login_at < @PeriodEnd
                AND EXISTS (
                    SELECT 1 FROM customer_chat_threads t
                    INNER JOIN customer_chat_messages m ON m.thread_id = t.id
                    WHERE t.tenant_id = @TenantId AND t.customer_id = ca.customer_id
                      AND m.sender_type = 1
                      AND m.created_at >= @PeriodStart AND m.created_at < @PeriodEnd
                )
                """,
            CustomerEngagementFunnelSteps.OrderAgain => """
                ca.status = 1 AND ca.last_login_at >= @PeriodStart AND ca.last_login_at < @PeriodEnd
                AND (
                    (SELECT COUNT(*) FROM sales_orders so
                     WHERE so.tenant_id = @TenantId AND so.customer_id = ca.customer_id
                       AND so.status = @OrderCompleted) >= 2
                    OR EXISTS (
                        SELECT 1 FROM customer_reservations r
                        WHERE r.tenant_id = @TenantId AND r.customer_id = ca.customer_id
                          AND r.created_at >= @PeriodStart AND r.created_at < @PeriodEnd
                    )
                    OR EXISTS (
                        SELECT 1 FROM repurchase_suggestions rs
                        WHERE rs.customer_account_id = ca.id
                          AND rs.drink_reminders_created_at >= @PeriodStart
                          AND rs.drink_reminders_created_at < @PeriodEnd
                    )
                )
                """,
            _ => throw new InvalidOperationException("Bước funnel không hợp lệ."),
        };

        var countSql = $"""
            SELECT COUNT(*)::int
            FROM customer_accounts ca
            INNER JOIN customers c ON c.id = ca.customer_id
            WHERE ca.tenant_id = @TenantId AND {whereClause}
            """;

        var listSql = $"""
            SELECT
                ca.id AS AccountId,
                ca.customer_id AS CustomerId,
                c.full_name AS FullName,
                ca.phone AS Phone,
                ca.last_login_at AS LastLoginAt,
                ca.first_login_at AS FirstLoginAt
            FROM customer_accounts ca
            INNER JOIN customers c ON c.id = ca.customer_id
            WHERE ca.tenant_id = @TenantId AND {whereClause}
            ORDER BY ca.last_login_at DESC NULLS LAST, c.full_name
            LIMIT @PageSize OFFSET @Offset
            """;

        var param = new
        {
            TenantId,
            PeriodStart = periodStartUtc,
            PeriodEnd = periodEndUtc,
            OrderCompleted = SalesOrderStatuses.Completed,
            PageSize = pageSize,
            Offset = (page - 1) * pageSize,
        };

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var total = await conn.ExecuteScalarAsync<int>(countSql, param);
        var items = (await conn.QueryAsync<DrillDownRow>(listSql, param)).ToList();
        return (items, total);
    }

    internal sealed class FunnelCounts
    {
        public int CohortSize { get; init; }
        public int AppActive { get; init; }
        public int ReminderOn { get; init; }
        public int AiUsage { get; init; }
        public int Chat { get; init; }
        public int OrderAgain { get; init; }
    }

    internal sealed class RetentionCounts
    {
        public int Eligible { get; init; }
        public int Retained { get; init; }
    }

    internal sealed class DrillDownRow
    {
        public Guid AccountId { get; init; }
        public Guid CustomerId { get; init; }
        public string FullName { get; init; } = "";
        public string Phone { get; init; } = "";
        public DateTime? LastLoginAt { get; init; }
        public DateTime? FirstLoginAt { get; init; }
    }
}

internal sealed class CustomerEngagementAnalyticsService : ICustomerEngagementAnalyticsService
{
    private const double AlertDeltaThreshold = -0.05;

    private readonly CustomerEngagementAnalyticsRepository _repo;

    public CustomerEngagementAnalyticsService(CustomerEngagementAnalyticsRepository repo) => _repo = repo;

    public async Task<CustomerEngagementOverviewDto> GetOverviewAsync(
        int periodDays = 30,
        CancellationToken cancellationToken = default)
    {
        periodDays = Math.Clamp(periodDays, 7, 365);
        var now = DateTime.UtcNow;
        var periodStart = now.AddDays(-periodDays);
        var priorStart = now.AddDays(-periodDays * 2);

        var current = await _repo.GetFunnelCountsAsync(periodStart, now, cancellationToken);
        var prior = await _repo.GetFunnelCountsAsync(priorStart, periodStart, cancellationToken);
        var retention = await _repo.GetRetentionCountsAsync(cancellationToken);

        var funnel = BuildFunnel(current, prior);
        var retentionDto = BuildRetention(retention);
        var alerts = BuildAlerts(funnel);

        return new CustomerEngagementOverviewDto(periodDays, current.CohortSize, funnel, retentionDto, alerts);
    }

    public async Task<CustomerEngagementDrillDownResultDto> GetDrillDownAsync(
        string step,
        int periodDays = 30,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        periodDays = Math.Clamp(periodDays, 7, 365);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 200);

        var periodStart = DateTime.UtcNow.AddDays(-periodDays);
        var periodEnd = DateTime.UtcNow;
        var (rows, total) = await _repo.ListDrillDownAsync(step, periodStart, periodEnd, page, pageSize, cancellationToken);

        var items = rows.Select(row => new CustomerEngagementDrillDownItemDto(
            row.AccountId,
            row.CustomerId,
            row.FullName,
            row.Phone,
            ToOffset(row.LastLoginAt),
            ToOffset(row.FirstLoginAt))).ToList();

        return new CustomerEngagementDrillDownResultDto(step, total, page, pageSize, items);
    }

    private static IReadOnlyList<CustomerEngagementFunnelStepDto> BuildFunnel(
        CustomerEngagementAnalyticsRepository.FunnelCounts current,
        CustomerEngagementAnalyticsRepository.FunnelCounts prior)
    {
        var steps = new (string Key, string Label, int Count, int PriorCount)[]
        {
            (CustomerEngagementFunnelSteps.AppActive, "App Active", current.AppActive, prior.AppActive),
            (CustomerEngagementFunnelSteps.ReminderOn, "Reminder On", current.ReminderOn, prior.ReminderOn),
            (CustomerEngagementFunnelSteps.AiUsage, "AI Usage", current.AiUsage, prior.AiUsage),
            (CustomerEngagementFunnelSteps.Chat, "Chat", current.Chat, prior.Chat),
            (CustomerEngagementFunnelSteps.OrderAgain, "Order Again", current.OrderAgain, prior.OrderAgain),
        };

        var cohort = Math.Max(current.CohortSize, 1);
        var priorCohort = Math.Max(prior.CohortSize, 1);
        var result = new List<CustomerEngagementFunnelStepDto>();
        var previousCount = current.CohortSize;

        foreach (var (key, label, count, priorCount) in steps)
        {
            var rateFromCohort = count / (double)cohort;
            var rateFromPrevious = previousCount > 0 ? count / (double)previousCount : 0;
            var priorRate = priorCount / (double)priorCohort;
            var currentRate = count / (double)cohort;
            var delta = currentRate - priorRate;

            result.Add(new CustomerEngagementFunnelStepDto(
                key,
                label,
                count,
                Math.Round(rateFromCohort, 4),
                Math.Round(rateFromPrevious, 4),
                Math.Round(delta, 4)));

            previousCount = Math.Max(count, 1);
        }

        return result;
    }

    private static CustomerEngagementRetentionDto BuildRetention(
        CustomerEngagementAnalyticsRepository.RetentionCounts retention)
    {
        var eligible = retention.Eligible;
        var retained = retention.Retained;
        var rate = eligible > 0 ? retained / (double)eligible : 0;
        return new CustomerEngagementRetentionDto(eligible, retained, Math.Round(rate, 4), 0);
    }

    private static IReadOnlyList<CustomerEngagementAlertDto> BuildAlerts(
        IReadOnlyList<CustomerEngagementFunnelStepDto> funnel)
    {
        var alerts = new List<CustomerEngagementAlertDto>();
        foreach (var step in funnel)
        {
            if (step.DeltaVsPriorPeriod <= AlertDeltaThreshold)
            {
                alerts.Add(new CustomerEngagementAlertDto(
                    step.Key,
                    "warning",
                    $"{step.Label} giảm {Math.Abs(step.DeltaVsPriorPeriod * 100):0.#}% so với kỳ trước (tính trên cohort)."));
            }
        }

        return alerts;
    }

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
}
