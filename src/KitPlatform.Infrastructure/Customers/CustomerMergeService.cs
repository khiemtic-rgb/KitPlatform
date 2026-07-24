using System.Data;
using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Customers;
using KitPlatform.Infrastructure.Data;
using Npgsql;

namespace KitPlatform.Infrastructure.Customers;

internal sealed class CustomerMergeService : ICustomerMergeService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public CustomerMergeService(IDbConnectionFactory db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    private Guid TenantId => _tenant.TenantId;

    public async Task<MergeCustomersResult> MergeAsync(
        MergeCustomersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.KeeperCustomerId == request.SourceCustomerId)
            throw new InvalidOperationException("Khách giữ và khách nguồn phải khác nhau.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var keeper = await LoadCustomerAsync(conn, tx, request.KeeperCustomerId, cancellationToken);
        var source = await LoadCustomerAsync(conn, tx, request.SourceCustomerId, cancellationToken);
        if (keeper is null || source is null)
            throw new InvalidOperationException("Không tìm thấy khách hàng (hoặc đã bị xóa).");

        var mergeId = Guid.NewGuid();
        var args = new MergeArgs(TenantId, request.KeeperCustomerId, request.SourceCustomerId);

        var ordersMoved = await ReassignAsync(
            conn, tx,
            """
            UPDATE sales_orders
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """,
            args);

        var paymentsMoved = await ReassignAsync(
            conn, tx,
            """
            UPDATE customer_payments
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE tenant_id = @TenantId AND customer_id = @SourceId AND deleted_at IS NULL
            """,
            args);

        await ReassignAsync(
            conn, tx,
            """
            UPDATE loyalty_transactions
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """,
            args);

        var loyaltyMerged = await MergeLoyaltyAsync(conn, tx, args);
        var vouchersMoved = await MergeVouchersAsync(conn, tx, args);
        var consentsMoved = await MergeConsentsAsync(conn, tx, args);
        await MergeChatThreadsAsync(conn, tx, args);
        await MergeAccountsAsync(conn, tx, args);

        await ReassignAsync(
            conn, tx,
            """
            UPDATE customer_addresses
            SET customer_id = @KeeperId
            WHERE customer_id = @SourceId
            """,
            args);

        await ReassignIfExistsAsync(conn, tx, "public.customer_draft_orders",
            """
            UPDATE customer_draft_orders
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "public.customer_reservations",
            """
            UPDATE customer_reservations
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "public.customer_notifications",
            """
            UPDATE customer_notifications
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "public.medication_reminders",
            """
            UPDATE medication_reminders
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "public.medication_adherence_events",
            """
            UPDATE medication_adherence_events
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "public.repurchase_suggestions",
            """
            UPDATE repurchase_suggestions
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "public.customer_engagement_events",
            """
            UPDATE customer_engagement_events
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await MergeAdherenceDispatchesAsync(conn, tx, args);

        await ReassignIfExistsAsync(conn, tx, "public.family_members",
            """
            UPDATE family_members
            SET linked_customer_id = @KeeperId
            WHERE linked_customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_pharmacy.pharmacy_sales_order",
            """
            UPDATE pack_pharmacy.pharmacy_sales_order
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_pharmacy.pharmacy_dispensing_note",
            """
            UPDATE pack_pharmacy.pharmacy_dispensing_note
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_pharmacy.electronic_prescriptions",
            """
            UPDATE pack_pharmacy.electronic_prescriptions
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_clinic.clinic_appointment",
            """
            UPDATE pack_clinic.clinic_appointment
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_clinic.clinic_visit",
            """
            UPDATE pack_clinic.clinic_visit
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_clinic.clinic_prescription",
            """
            UPDATE pack_clinic.clinic_prescription
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_crm.crm_lead",
            """
            UPDATE pack_crm.crm_lead
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_crm.crm_opportunity",
            """
            UPDATE pack_crm.crm_opportunity
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_crm.crm_activity",
            """
            UPDATE pack_crm.crm_activity
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_connect.referrals",
            """
            UPDATE pack_connect.referrals
            SET pharmacy_customer_id = @KeeperId
            WHERE pharmacy_customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_connect.referrals",
            """
            UPDATE pack_connect.referrals
            SET clinic_customer_id = @KeeperId
            WHERE clinic_customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_connect.bookings",
            """
            UPDATE pack_connect.bookings
            SET pharmacy_customer_id = @KeeperId
            WHERE pharmacy_customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "kit_notify.notify_recipient",
            """
            UPDATE kit_notify.notify_recipient
            SET customer_id = @KeeperId
            WHERE customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "kit_ai.ai_conversation",
            """
            UPDATE kit_ai.ai_conversation
            SET customer_id = @KeeperId
            WHERE customer_id = @SourceId
            """, args);

        await ReassignIfExistsAsync(conn, tx, "pack_learning.customer_sale_feedback",
            """
            DELETE FROM pack_learning.customer_sale_feedback s
            WHERE s.customer_id = @SourceId
              AND EXISTS (
                  SELECT 1 FROM pack_learning.customer_sale_feedback k
                  WHERE k.sales_order_id = s.sales_order_id
                    AND k.customer_id = @KeeperId
              );
            UPDATE pack_learning.customer_sale_feedback
            SET customer_id = @KeeperId
            WHERE customer_id = @SourceId
            """, args);

        var softDeleted = await conn.ExecuteAsync(
            """
            UPDATE customers
            SET deleted_at = NOW(),
                status = 0,
                phone = @FreedPhone,
                customer_code = @FreedCode,
                updated_at = NOW()
            WHERE id = @SourceId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            """,
            new
            {
                TenantId,
                SourceId = request.SourceCustomerId,
                FreedPhone = FreedPhone(source.Id),
                FreedCode = FreedCode(source.Id),
            },
            tx) > 0;

        if (!softDeleted)
            throw new InvalidOperationException("Không soft-delete được khách nguồn.");

        var meta = JsonSerializer.Serialize(new
        {
            ordersMoved,
            paymentsMoved,
            loyaltyProgramsMerged = loyaltyMerged,
            vouchersMoved,
            consentsMoved,
            sourceCode = source.CustomerCode,
            sourcePhone = source.Phone,
            keeperCode = keeper.CustomerCode,
        });

        await conn.ExecuteAsync(
            """
            INSERT INTO customer_merge_events (
                id, tenant_id, keeper_customer_id, source_customer_id,
                merged_at, merged_by_user_id, reason, meta
            ) VALUES (
                @MergeId, @TenantId, @KeeperId, @SourceId,
                NOW(), @UserId, @Reason, CAST(@Meta AS jsonb)
            )
            """,
            new
            {
                MergeId = mergeId,
                TenantId,
                KeeperId = request.KeeperCustomerId,
                SourceId = request.SourceCustomerId,
                UserId = _tenant.IsAuthenticated ? _tenant.UserId : (Guid?)null,
                Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
                Meta = meta,
            },
            tx);

        await tx.CommitAsync(cancellationToken);

        return new MergeCustomersResult(
            mergeId,
            request.KeeperCustomerId,
            request.SourceCustomerId,
            softDeleted,
            ordersMoved,
            paymentsMoved,
            loyaltyMerged,
            vouchersMoved,
            consentsMoved);
    }

    private static string FreedPhone(Guid sourceId) =>
        ("M" + sourceId.ToString("N"))[..20];

    private static string FreedCode(Guid sourceId) =>
        "MERGED-" + sourceId.ToString("N");

    private async Task<CustomerLockRow?> LoadCustomerAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id AS Id, customer_code AS CustomerCode, phone AS Phone
            FROM customers
            WHERE id = @CustomerId
              AND tenant_id = @TenantId
              AND deleted_at IS NULL
            FOR UPDATE
            """;
        return await conn.QuerySingleOrDefaultAsync<CustomerLockRow>(
            new CommandDefinition(
                sql,
                new { CustomerId = customerId, TenantId },
                tx,
                cancellationToken: cancellationToken));
    }

    private static async Task<int> ReassignAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        string sql,
        object args) =>
        await conn.ExecuteAsync(sql, args, tx);

    private static async Task ReassignIfExistsAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        string relation,
        string sql,
        object args)
    {
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT to_regclass(@Rel) IS NOT NULL",
            new { Rel = relation },
            tx);
        if (!exists) return;
        try
        {
            await conn.ExecuteAsync(sql, args, tx);
        }
        catch (PostgresException ex) when (ex.SqlState is "42703" or "42P01")
        {
            // Missing column/table variant — skip
        }
    }

    private static async Task<int> MergeLoyaltyAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        object args)
    {
        var merged = await conn.ExecuteAsync(
            """
            UPDATE customer_loyalty k
            SET points_balance = k.points_balance + s.points_balance,
                lifetime_points = k.lifetime_points + s.lifetime_points,
                updated_at = NOW()
            FROM customer_loyalty s
            WHERE s.customer_id = @SourceId
              AND k.customer_id = @KeeperId
              AND k.program_id = s.program_id
            """,
            args,
            tx);

        await conn.ExecuteAsync(
            """
            DELETE FROM customer_loyalty s
            WHERE s.customer_id = @SourceId
              AND EXISTS (
                  SELECT 1 FROM customer_loyalty k
                  WHERE k.customer_id = @KeeperId AND k.program_id = s.program_id
              )
            """,
            args,
            tx);

        var moved = await conn.ExecuteAsync(
            """
            UPDATE customer_loyalty
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE customer_id = @SourceId
            """,
            args,
            tx);

        return merged + moved;
    }

    private static async Task<int> MergeVouchersAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        object args)
    {
        await conn.ExecuteAsync(
            """
            DELETE FROM customer_vouchers s
            WHERE s.customer_id = @SourceId
              AND EXISTS (
                  SELECT 1 FROM customer_vouchers k
                  WHERE k.customer_id = @KeeperId AND k.voucher_id = s.voucher_id
              )
            """,
            args,
            tx);

        return await conn.ExecuteAsync(
            """
            UPDATE customer_vouchers
            SET customer_id = @KeeperId
            WHERE customer_id = @SourceId
            """,
            args,
            tx);
    }

    private static async Task<int> MergeConsentsAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        object args)
    {
        await conn.ExecuteAsync(
            """
            UPDATE customer_consents k
            SET granted = TRUE,
                granted_at = COALESCE(k.granted_at, s.granted_at, NOW()),
                revoked_at = NULL,
                updated_at = NOW()
            FROM customer_consents s
            WHERE s.tenant_id = @TenantId
              AND s.customer_id = @SourceId
              AND k.tenant_id = @TenantId
              AND k.customer_id = @KeeperId
              AND k.channel = s.channel
              AND k.purpose = s.purpose
              AND s.granted = TRUE
              AND k.granted = FALSE
            """,
            args,
            tx);

        await conn.ExecuteAsync(
            """
            DELETE FROM customer_consents s
            WHERE s.tenant_id = @TenantId
              AND s.customer_id = @SourceId
              AND EXISTS (
                  SELECT 1 FROM customer_consents k
                  WHERE k.tenant_id = @TenantId
                    AND k.customer_id = @KeeperId
                    AND k.channel = s.channel
                    AND k.purpose = s.purpose
              )
            """,
            args,
            tx);

        return await conn.ExecuteAsync(
            """
            UPDATE customer_consents
            SET customer_id = @KeeperId, updated_at = NOW()
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """,
            args,
            tx);
    }

    private static async Task MergeChatThreadsAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        MergeArgs args)
    {
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('public.customer_chat_threads') IS NOT NULL",
            transaction: tx);
        if (!exists) return;

        var keeperThreadId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM customer_chat_threads
            WHERE tenant_id = @TenantId AND customer_id = @KeeperId
            """,
            args,
            tx);
        var sourceThreadId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM customer_chat_threads
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """,
            args,
            tx);

        if (sourceThreadId is null) return;

        if (keeperThreadId is null)
        {
            await conn.ExecuteAsync(
                """
                UPDATE customer_chat_threads
                SET customer_id = @KeeperId, updated_at = NOW()
                WHERE id = @SourceThreadId AND tenant_id = @TenantId
                """,
                new { args.TenantId, args.KeeperId, SourceThreadId = sourceThreadId.Value },
                tx);
            return;
        }

        await conn.ExecuteAsync(
            """
            UPDATE customer_chat_messages
            SET thread_id = @KeeperThreadId
            WHERE thread_id = @SourceThreadId
            """,
            new { KeeperThreadId = keeperThreadId.Value, SourceThreadId = sourceThreadId.Value },
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE customer_chat_threads k
            SET
                last_message_at = GREATEST(k.last_message_at, s.last_message_at),
                customer_unread_count = k.customer_unread_count + s.customer_unread_count,
                staff_unread_count = k.staff_unread_count + s.staff_unread_count,
                updated_at = NOW()
            FROM customer_chat_threads s
            WHERE k.id = @KeeperThreadId AND s.id = @SourceThreadId
            """,
            new { KeeperThreadId = keeperThreadId.Value, SourceThreadId = sourceThreadId.Value },
            tx);

        await conn.ExecuteAsync(
            "DELETE FROM customer_chat_threads WHERE id = @SourceThreadId",
            new { SourceThreadId = sourceThreadId.Value },
            tx);
    }

    private static async Task MergeAccountsAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        MergeArgs args)
    {
        var keeperAccountId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM customer_accounts
            WHERE tenant_id = @TenantId AND customer_id = @KeeperId
            ORDER BY status DESC, created_at
            LIMIT 1
            """,
            args,
            tx);
        var sourceAccountId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM customer_accounts
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            ORDER BY status DESC, created_at
            LIMIT 1
            """,
            args,
            tx);

        if (sourceAccountId is null) return;

        if (keeperAccountId is null)
        {
            await conn.ExecuteAsync(
                """
                UPDATE customer_accounts
                SET customer_id = @KeeperId, updated_at = NOW()
                WHERE id = @SourceAccountId AND tenant_id = @TenantId
                """,
                new { args.TenantId, args.KeeperId, SourceAccountId = sourceAccountId.Value },
                tx);
            return;
        }

        var freed = ("A" + sourceAccountId.Value.ToString("N"))[..20];
        await conn.ExecuteAsync(
            """
            UPDATE customer_accounts
            SET status = 0,
                phone = @FreedPhone,
                updated_at = NOW()
            WHERE id = @SourceAccountId
            """,
            new { SourceAccountId = sourceAccountId.Value, FreedPhone = freed },
            tx);
    }

    private static async Task MergeAdherenceDispatchesAsync(
        NpgsqlConnection conn,
        IDbTransaction tx,
        object args)
    {
        var exists = await conn.ExecuteScalarAsync<bool>(
            "SELECT to_regclass('public.customer_adherence_alert_dispatches') IS NOT NULL",
            transaction: tx);
        if (!exists) return;

        await conn.ExecuteAsync(
            """
            DELETE FROM customer_adherence_alert_dispatches s
            WHERE s.tenant_id = @TenantId
              AND s.customer_id = @SourceId
              AND EXISTS (
                  SELECT 1 FROM customer_adherence_alert_dispatches k
                  WHERE k.tenant_id = @TenantId
                    AND k.customer_id = @KeeperId
                    AND k.alert_date = s.alert_date
              )
            """,
            args,
            tx);

        await conn.ExecuteAsync(
            """
            UPDATE customer_adherence_alert_dispatches
            SET customer_id = @KeeperId
            WHERE tenant_id = @TenantId AND customer_id = @SourceId
            """,
            args,
            tx);
    }

    private sealed class CustomerLockRow
    {
        public Guid Id { get; init; }
        public string CustomerCode { get; init; } = "";
        public string Phone { get; init; } = "";
    }

    private sealed record MergeArgs(Guid TenantId, Guid KeeperId, Guid SourceId);
}
