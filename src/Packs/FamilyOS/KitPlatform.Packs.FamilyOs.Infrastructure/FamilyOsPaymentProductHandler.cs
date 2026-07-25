using Dapper;
using KitPlatform.Application.Payment;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.FamilyOs;
using Microsoft.Extensions.Logging;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

/// <summary>
/// Famixa fulfillment — keeps pack_family.family_subscription in sync when
/// Kit Payment marks an order paid. Platform subscription remains source of truth.
/// </summary>
internal sealed class FamilyOsPaymentProductHandler : IPaymentProductHandler
{
    private readonly IDbConnectionFactory _db;
    private readonly ILogger<FamilyOsPaymentProductHandler> _logger;

    public FamilyOsPaymentProductHandler(
        IDbConnectionFactory db,
        ILogger<FamilyOsPaymentProductHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string ProductCode => PaymentProductCodes.FamilyOs;

    public async Task OnOrderPaidAsync(
        PaymentOrderPaidContext context,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(context.SubjectType, PaymentSubjectTypes.Family, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Family OS handler ignored non-family subject {SubjectType}/{SubjectId}",
                context.SubjectType, context.SubjectId);
            return;
        }

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(
            "SELECT set_config('app.tenant_id', @Value, true)",
            new { Value = context.TenantId.ToString() });

        var updated = await conn.ExecuteAsync(
            """
            UPDATE pack_family.family_subscription
            SET status = @Status,
                plan_code = @PlanCode,
                current_period_end = @PeriodEnd,
                updated_at = NOW()
            WHERE family_id = @FamilyId AND tenant_id = @TenantId
            """,
            new
            {
                FamilyId = context.SubjectId,
                TenantId = context.TenantId,
                Status = FamilySubscriptionStatuses.Active,
                PlanCode = context.PlanCode,
                PeriodEnd = context.PeriodEnd.UtcDateTime,
            });

        if (updated == 0)
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO pack_family.family_subscription (
                    tenant_id, family_id, plan_code, status, current_period_end
                )
                VALUES (@TenantId, @FamilyId, @PlanCode, @Status, @PeriodEnd)
                """,
                new
                {
                    TenantId = context.TenantId,
                    FamilyId = context.SubjectId,
                    PlanCode = context.PlanCode,
                    Status = FamilySubscriptionStatuses.Active,
                    PeriodEnd = context.PeriodEnd.UtcDateTime,
                });
        }

        // Bridge legacy checkout table if a matching order_code exists (pre-migration dual-write).
        await conn.ExecuteAsync(
            """
            UPDATE pack_family.billing_checkout
            SET status = 'paid',
                paid_at = NOW(),
                updated_at = NOW(),
                description = COALESCE(description, @PublicCode)
            WHERE order_code = @OrderCode AND tenant_id = @TenantId AND status = 'pending'
            """,
            new
            {
                OrderCode = context.OrderCode,
                TenantId = context.TenantId,
                PublicCode = context.PublicCode,
            });

        _logger.LogInformation(
            "Famixa subscription synced family={FamilyId} until {PeriodEnd:o} public={PublicCode}",
            context.SubjectId, context.PeriodEnd, context.PublicCode);
    }
}
