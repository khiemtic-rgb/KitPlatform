using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Payment;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Payment;

internal sealed class PaymentService : IPaymentService
{
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;
    private readonly IReadOnlyDictionary<string, IPaymentProductHandler> _handlers;
    private readonly PaymentPayOsOptions _payOs;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IDbConnectionFactory db,
        ITenantContext tenant,
        IEnumerable<IPaymentProvider> providers,
        IEnumerable<IPaymentProductHandler> handlers,
        IOptions<PaymentPayOsOptions> payOs,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _tenant = tenant;
        _providers = providers.ToDictionary(p => p.ProviderCode, StringComparer.OrdinalIgnoreCase);
        _handlers = handlers.ToDictionary(h => h.ProductCode, StringComparer.OrdinalIgnoreCase);
        _payOs = payOs.Value;
        _logger = logger;
    }

    public async Task<PaymentPlanDto?> GetPlanAsync(
        string productCode,
        string planCode,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<PlanRow>(
            """
            SELECT
                product_code AS ProductCode,
                plan_code AS PlanCode,
                display_name AS DisplayName,
                amount_vnd AS AmountVnd,
                currency AS Currency,
                interval_days AS IntervalDays
            FROM payment.plan
            WHERE product_code = @ProductCode
              AND plan_code = @PlanCode
              AND is_active = TRUE
            """,
            new { ProductCode = productCode, PlanCode = planCode });
        return row is null
            ? null
            : new PaymentPlanDto(
                row.ProductCode, row.PlanCode, row.DisplayName,
                row.AmountVnd, row.Currency, row.IntervalDays);
    }

    public async Task<PaymentSubscriptionDto?> GetSubscriptionAsync(
        string productCode,
        string subjectType,
        Guid subjectId,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<SubscriptionRow>(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                product_code AS ProductCode,
                subject_type AS SubjectType,
                subject_id AS SubjectId,
                plan_code AS PlanCode,
                status AS Status,
                trial_ends_at AS TrialEndsAt,
                current_period_end AS CurrentPeriodEnd,
                auto_renew AS AutoRenew
            FROM payment.subscription
            WHERE product_code = @ProductCode
              AND subject_type = @SubjectType
              AND subject_id = @SubjectId
              AND (@TenantId = '00000000-0000-0000-0000-000000000000'::uuid
                   OR tenant_id = @TenantId)
            """,
            new
            {
                ProductCode = productCode,
                SubjectType = subjectType,
                SubjectId = subjectId,
                TenantId = _tenant.IsAuthenticated ? _tenant.TenantId : Guid.Empty,
            });

        return row is null ? null : MapSubscription(row);
    }

    public async Task<PaymentOrderDto> CreateOrderAsync(
        CreatePaymentOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenant.IsAuthenticated || _tenant.TenantId == Guid.Empty)
            throw new InvalidOperationException("Cần đăng nhập để tạo thanh toán.");

        var productCode = RequireCode(request.ProductCode, "productCode");
        var subjectType = RequireCode(request.SubjectType, "subjectType");
        if (request.SubjectId == Guid.Empty)
            throw new InvalidOperationException("subjectId là bắt buộc.");

        var planCode = string.IsNullOrWhiteSpace(request.PlanCode)
            ? "starter_month"
            : request.PlanCode.Trim().ToLowerInvariant();

        var plan = await GetPlanAsync(productCode, planCode, cancellationToken)
            ?? throw new InvalidOperationException($"Không tìm thấy gói {productCode}/{planCode}.");

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);

        // Ensure subscription row exists (trial may live in product table; platform row is source of truth going forward)
        var subscriptionId = await conn.ExecuteScalarAsync<Guid?>(
            """
            SELECT id FROM payment.subscription
            WHERE tenant_id = @TenantId
              AND product_code = @ProductCode
              AND subject_type = @SubjectType
              AND subject_id = @SubjectId
            """,
            new
            {
                TenantId = _tenant.TenantId,
                ProductCode = productCode,
                SubjectType = subjectType,
                SubjectId = request.SubjectId,
            });

        if (subscriptionId is null)
        {
            subscriptionId = await conn.ExecuteScalarAsync<Guid>(
                """
                INSERT INTO payment.subscription (
                    tenant_id, product_code, subject_type, subject_id,
                    plan_code, status, trial_ends_at, current_period_end
                )
                VALUES (
                    @TenantId, @ProductCode, @SubjectType, @SubjectId,
                    @PlanCode, 'trial', NOW() + INTERVAL '30 days', NOW() + INTERVAL '30 days'
                )
                ON CONFLICT (tenant_id, product_code, subject_type, subject_id)
                DO UPDATE SET updated_at = NOW()
                RETURNING id
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    ProductCode = productCode,
                    SubjectType = subjectType,
                    SubjectId = request.SubjectId,
                    PlanCode = planCode,
                });
        }

        var orderCode = await AllocateOrderCodeAsync(conn, cancellationToken);
        var publicCode = await AllocatePublicCodeAsync(conn, productCode, cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.AddHours(24);

        string? checkoutUrl = null;
        string? qrCode = null;
        string? providerPaymentId = null;
        string? providerCode = null;

        var preferred = string.IsNullOrWhiteSpace(request.PreferredProvider)
            ? PaymentProviderCodes.PayOs
            : request.PreferredProvider.Trim().ToLowerInvariant();

        if (_providers.TryGetValue(preferred, out var provider) && provider.IsReady)
        {
            var returnUrl = FirstNonEmpty(request.ReturnUrl, _payOs.ReturnUrl)
                ?? throw new InvalidOperationException("Thiếu returnUrl (Payment:PayOS:ReturnUrl).");
            var cancelUrl = FirstNonEmpty(request.CancelUrl, _payOs.CancelUrl)
                ?? throw new InvalidOperationException("Thiếu cancelUrl (Payment:PayOS:CancelUrl).");

            var created = await provider.CreateCheckoutAsync(
                orderCode,
                publicCode,
                plan.AmountVnd,
                publicCode,
                returnUrl,
                cancelUrl,
                cancellationToken);

            checkoutUrl = created.CheckoutUrl;
            qrCode = created.QrCode;
            providerPaymentId = created.ProviderPaymentId;
            providerCode = provider.ProviderCode;
            if (created.ExpiresAt is DateTimeOffset exp) expiresAt = exp;
        }
        else
        {
            // Fallback: VietQR-ready public_code for bank transfer / future SePay — no manual content typing.
            providerCode = PaymentProviderCodes.Manual;
        }

        var id = await conn.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO payment.payment_order (
                tenant_id, subscription_id, product_code, subject_type, subject_id,
                order_code, public_code, plan_code, amount_vnd, currency, status,
                provider_code, provider_payment_id, checkout_url, qr_code, description,
                return_url, cancel_url, expires_at
            )
            VALUES (
                @TenantId, @SubscriptionId, @ProductCode, @SubjectType, @SubjectId,
                @OrderCode, @PublicCode, @PlanCode, @AmountVnd, @Currency, @Status,
                @ProviderCode, @ProviderPaymentId, @CheckoutUrl, @QrCode, @Description,
                @ReturnUrl, @CancelUrl, @ExpiresAt
            )
            RETURNING id
            """,
            new
            {
                TenantId = _tenant.TenantId,
                SubscriptionId = subscriptionId,
                ProductCode = productCode,
                SubjectType = subjectType,
                SubjectId = request.SubjectId,
                OrderCode = orderCode,
                PublicCode = publicCode,
                PlanCode = planCode,
                AmountVnd = plan.AmountVnd,
                Currency = plan.Currency,
                Status = PaymentOrderStatuses.Pending,
                ProviderCode = providerCode,
                ProviderPaymentId = providerPaymentId,
                CheckoutUrl = checkoutUrl,
                QrCode = qrCode,
                Description = publicCode,
                ReturnUrl = request.ReturnUrl,
                CancelUrl = request.CancelUrl,
                ExpiresAt = expiresAt.UtcDateTime,
            });

        return new PaymentOrderDto(
            id,
            _tenant.TenantId,
            productCode,
            subjectType,
            request.SubjectId,
            orderCode,
            publicCode,
            planCode,
            plan.AmountVnd,
            plan.Currency,
            PaymentOrderStatuses.Pending,
            providerCode,
            checkoutUrl,
            qrCode,
            publicCode,
            null,
            DateTimeOffset.UtcNow,
            expiresAt);
    }

    public async Task<PaymentOrderDto> GetOrderAsync(
        string productCode,
        Guid subjectId,
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<OrderRow>(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                product_code AS ProductCode,
                subject_type AS SubjectType,
                subject_id AS SubjectId,
                order_code AS OrderCode,
                public_code AS PublicCode,
                plan_code AS PlanCode,
                amount_vnd AS AmountVnd,
                currency AS Currency,
                status AS Status,
                provider_code AS ProviderCode,
                checkout_url AS CheckoutUrl,
                qr_code AS QrCode,
                description AS Description,
                paid_at AS PaidAt,
                created_at AS CreatedAt,
                expires_at AS ExpiresAt
            FROM payment.payment_order
            WHERE order_code = @OrderCode
              AND product_code = @ProductCode
              AND subject_id = @SubjectId
              AND tenant_id = @TenantId
            """,
            new
            {
                OrderCode = orderCode,
                ProductCode = productCode,
                SubjectId = subjectId,
                TenantId = _tenant.TenantId,
            });

        if (row is null)
            throw new InvalidOperationException("Không tìm thấy đơn thanh toán.");

        return MapOrder(row);
    }

    public async Task HandleProviderWebhookAsync(
        string providerCode,
        string rawBody,
        CancellationToken cancellationToken = default)
    {
        if (!_providers.TryGetValue(providerCode, out var provider))
            throw new InvalidOperationException($"Provider không hỗ trợ: {providerCode}");

        var parsed = provider.ParseAndVerifyWebhook(rawBody);
        if (!parsed.IsSuccess)
        {
            _logger.LogInformation(
                "Payment webhook ignored (not success) provider={Provider} order={OrderCode}",
                providerCode, parsed.OrderCode);
            return;
        }

        await MarkPaidAndFulfillAsync(parsed.OrderCode, providerCode, parsed.ProviderTxnId, rawBody, cancellationToken);
    }

    public async Task<PaymentOrderDto> ActivateOrderAsync(
        long orderCode,
        CancellationToken cancellationToken = default)
    {
        await MarkPaidAndFulfillAsync(orderCode, PaymentProviderCodes.Manual, null, null, cancellationToken);

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleOrDefaultAsync<OrderRow>(
            """
            SELECT
                id AS Id,
                tenant_id AS TenantId,
                product_code AS ProductCode,
                subject_type AS SubjectType,
                subject_id AS SubjectId,
                order_code AS OrderCode,
                public_code AS PublicCode,
                plan_code AS PlanCode,
                amount_vnd AS AmountVnd,
                currency AS Currency,
                status AS Status,
                provider_code AS ProviderCode,
                checkout_url AS CheckoutUrl,
                qr_code AS QrCode,
                description AS Description,
                paid_at AS PaidAt,
                created_at AS CreatedAt,
                expires_at AS ExpiresAt
            FROM payment.payment_order
            WHERE order_code = @OrderCode
            """,
            new { OrderCode = orderCode });

        if (row is null)
            throw new InvalidOperationException("Không tìm thấy đơn thanh toán.");

        return MapOrder(row);
    }

    private async Task MarkPaidAndFulfillAsync(
        long orderCode,
        string providerCode,
        string? providerTxnId,
        string? rawWebhook,
        CancellationToken cancellationToken)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        var order = await conn.QuerySingleOrDefaultAsync<OrderActivateRow>(
            """
            SELECT
                o.id AS Id,
                o.tenant_id AS TenantId,
                o.subscription_id AS SubscriptionId,
                o.product_code AS ProductCode,
                o.subject_type AS SubjectType,
                o.subject_id AS SubjectId,
                o.order_code AS OrderCode,
                o.public_code AS PublicCode,
                o.plan_code AS PlanCode,
                o.amount_vnd AS AmountVnd,
                o.status AS Status,
                COALESCE(p.interval_days, 30) AS IntervalDays,
                s.current_period_end AS CurrentPeriodEnd
            FROM payment.payment_order o
            LEFT JOIN payment.plan p
                ON p.product_code = o.product_code AND p.plan_code = o.plan_code
            LEFT JOIN payment.subscription s ON s.id = o.subscription_id
            WHERE o.order_code = @OrderCode
            FOR UPDATE OF o
            """,
            new { OrderCode = orderCode },
            tx);

        if (order is null)
            throw new InvalidOperationException("Không tìm thấy đơn thanh toán.");

        await conn.ExecuteAsync(
            "SELECT set_config('app.tenant_id', @Value, true)",
            new { Value = order.TenantId.ToString() },
            tx);

        if (order.Status == PaymentOrderStatuses.Paid)
        {
            await tx.CommitAsync(cancellationToken);
            return;
        }

        if (order.Status is PaymentOrderStatuses.Canceled or PaymentOrderStatuses.Expired)
            throw new InvalidOperationException("Đơn thanh toán đã hủy hoặc hết hạn.");

        // Stack remaining time — paying early must not shorten the period.
        var baseEnd = order.CurrentPeriodEnd is DateTime existing && existing > DateTime.UtcNow
            ? existing
            : DateTime.UtcNow;
        var periodEnd = new DateTimeOffset(DateTime.SpecifyKind(baseEnd, DateTimeKind.Utc))
            .AddDays(order.IntervalDays);

        await conn.ExecuteAsync(
            """
            UPDATE payment.payment_order
            SET status = @Status,
                paid_at = NOW(),
                updated_at = NOW(),
                provider_code = COALESCE(provider_code, @ProviderCode),
                raw_webhook = CASE
                    WHEN @RawWebhook IS NULL THEN raw_webhook
                    ELSE @RawWebhook::jsonb
                END
            WHERE id = @Id
            """,
            new
            {
                Id = order.Id,
                Status = PaymentOrderStatuses.Paid,
                ProviderCode = providerCode,
                RawWebhook = rawWebhook,
            },
            tx);

        await conn.ExecuteAsync(
            """
            INSERT INTO payment.payment_transaction (
                tenant_id, order_id, provider_code, provider_txn_id, amount_vnd, status, raw_payload
            )
            VALUES (
                @TenantId, @OrderId, @ProviderCode, @ProviderTxnId, @AmountVnd, 'succeeded',
                CASE WHEN @RawWebhook IS NULL THEN NULL ELSE @RawWebhook::jsonb END
            )
            """,
            new
            {
                TenantId = order.TenantId,
                OrderId = order.Id,
                ProviderCode = providerCode,
                ProviderTxnId = providerTxnId,
                AmountVnd = order.AmountVnd,
                RawWebhook = rawWebhook,
            },
            tx);

        Guid subscriptionId;
        if (order.SubscriptionId is Guid existingSub)
        {
            await conn.ExecuteAsync(
                """
                UPDATE payment.subscription
                SET status = @Status,
                    plan_code = @PlanCode,
                    current_period_end = @PeriodEnd,
                    updated_at = NOW()
                WHERE id = @Id
                """,
                new
                {
                    Id = existingSub,
                    Status = PaymentSubscriptionStatuses.Active,
                    PlanCode = order.PlanCode,
                    PeriodEnd = periodEnd.UtcDateTime,
                },
                tx);
            subscriptionId = existingSub;
        }
        else
        {
            subscriptionId = await conn.ExecuteScalarAsync<Guid>(
                """
                INSERT INTO payment.subscription (
                    tenant_id, product_code, subject_type, subject_id,
                    plan_code, status, current_period_end
                )
                VALUES (
                    @TenantId, @ProductCode, @SubjectType, @SubjectId,
                    @PlanCode, @Status, @PeriodEnd
                )
                ON CONFLICT (tenant_id, product_code, subject_type, subject_id)
                DO UPDATE SET
                    status = EXCLUDED.status,
                    plan_code = EXCLUDED.plan_code,
                    current_period_end = EXCLUDED.current_period_end,
                    updated_at = NOW()
                RETURNING id
                """,
                new
                {
                    TenantId = order.TenantId,
                    ProductCode = order.ProductCode,
                    SubjectType = order.SubjectType,
                    SubjectId = order.SubjectId,
                    PlanCode = order.PlanCode,
                    Status = PaymentSubscriptionStatuses.Active,
                    PeriodEnd = periodEnd.UtcDateTime,
                },
                tx);

            await conn.ExecuteAsync(
                "UPDATE payment.payment_order SET subscription_id = @SubId WHERE id = @Id",
                new { SubId = subscriptionId, Id = order.Id },
                tx);
        }

        await tx.CommitAsync(cancellationToken);

        if (_handlers.TryGetValue(order.ProductCode, out var handler))
        {
            await handler.OnOrderPaidAsync(
                new PaymentOrderPaidContext(
                    order.TenantId,
                    order.Id,
                    order.OrderCode,
                    order.PublicCode,
                    order.ProductCode,
                    order.SubjectType,
                    order.SubjectId,
                    order.PlanCode,
                    order.AmountVnd,
                    order.IntervalDays,
                    periodEnd,
                    subscriptionId),
                cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "No IPaymentProductHandler for product {ProductCode} — platform subscription updated only",
                order.ProductCode);
        }

        _logger.LogInformation(
            "Payment activated order {OrderCode} public={PublicCode} product={Product} until {PeriodEnd:o}",
            orderCode, order.PublicCode, order.ProductCode, periodEnd);
    }

    private static async Task<long> AllocateOrderCodeAsync(
        Npgsql.NpgsqlConnection conn,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < 8; i++)
        {
            var candidate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + RandomNumberGenerator.GetInt32(0, 1000);
            var exists = await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT EXISTS(SELECT 1 FROM payment.payment_order WHERE order_code = @Code)",
                    new { Code = candidate },
                    cancellationToken: cancellationToken));
            if (!exists) return candidate;
        }

        return RandomNumberGenerator.GetInt32(100_000_000, int.MaxValue);
    }

    private static async Task<string> AllocatePublicCodeAsync(
        Npgsql.NpgsqlConnection conn,
        string productCode,
        CancellationToken cancellationToken)
    {
        var prefix = productCode.Equals(PaymentProductCodes.FamilyOs, StringComparison.OrdinalIgnoreCase)
            ? "FMX"
            : "KIT";
        var day = DateTime.UtcNow.AddHours(7).ToString("yyMMdd"); // VN calendar day

        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var suffix = new char[3];
        for (var i = 0; i < 12; i++)
        {
            for (var s = 0; s < suffix.Length; s++)
                suffix[s] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

            var code = $"{prefix}{day}{new string(suffix)}";
            var exists = await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    "SELECT EXISTS(SELECT 1 FROM payment.payment_order WHERE public_code = @Code)",
                    new { Code = code },
                    cancellationToken: cancellationToken));
            if (!exists) return code;
        }

        return $"{prefix}{day}{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    }

    private static PaymentSubscriptionDto MapSubscription(SubscriptionRow row)
    {
        var status = NormalizeStatus(row);
        var entitled = status is PaymentSubscriptionStatuses.Trial
            or PaymentSubscriptionStatuses.Active;
        // past_due is grace — entitled until we add dunning; still date-check period end
        if (status == PaymentSubscriptionStatuses.PastDue
            && row.CurrentPeriodEnd is DateTime graceEnd
            && graceEnd.AddDays(3) >= DateTime.UtcNow)
        {
            entitled = true;
        }

        return new PaymentSubscriptionDto(
            row.Id,
            row.TenantId,
            row.ProductCode,
            row.SubjectType,
            row.SubjectId,
            row.PlanCode,
            status,
            ToOffset(row.TrialEndsAt),
            ToOffset(row.CurrentPeriodEnd),
            row.AutoRenew,
            entitled);
    }

    private static string NormalizeStatus(SubscriptionRow row)
    {
        var status = (row.Status ?? "").Trim().ToLowerInvariant();
        if (status == PaymentSubscriptionStatuses.Trial
            && row.TrialEndsAt is DateTime ends
            && ends < DateTime.UtcNow)
            return PaymentSubscriptionStatuses.Expired;
        if (status == PaymentSubscriptionStatuses.Active
            && row.CurrentPeriodEnd is DateTime periodEnd
            && periodEnd < DateTime.UtcNow)
            return PaymentSubscriptionStatuses.Expired;
        if (status == PaymentSubscriptionStatuses.PastDue
            && row.CurrentPeriodEnd is DateTime pastDueEnd
            && pastDueEnd.AddDays(3) < DateTime.UtcNow)
            return PaymentSubscriptionStatuses.Expired;
        return status;
    }

    private static PaymentOrderDto MapOrder(OrderRow row) =>
        new(
            row.Id,
            row.TenantId,
            row.ProductCode,
            row.SubjectType,
            row.SubjectId,
            row.OrderCode,
            row.PublicCode,
            row.PlanCode,
            row.AmountVnd,
            row.Currency,
            row.Status,
            row.ProviderCode,
            row.CheckoutUrl,
            row.QrCode,
            row.Description,
            ToOffset(row.PaidAt),
            ToOffset(row.CreatedAt) ?? DateTimeOffset.UtcNow,
            ToOffset(row.ExpiresAt));

    private static DateTimeOffset? ToOffset(DateTime? value) =>
        value is DateTime dt
            ? new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc))
            : null;

    private static string RequireCode(string? value, string label)
    {
        var trimmed = (value ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException($"{label} là bắt buộc.");
        return trimmed;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }

    private sealed class PlanRow
    {
        public string ProductCode { get; init; } = "";
        public string PlanCode { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public int AmountVnd { get; init; }
        public string Currency { get; init; } = "VND";
        public int IntervalDays { get; init; }
    }

    private sealed class SubscriptionRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public string ProductCode { get; init; } = "";
        public string SubjectType { get; init; } = "";
        public Guid SubjectId { get; init; }
        public string PlanCode { get; init; } = "";
        public string Status { get; init; } = "";
        public DateTime? TrialEndsAt { get; init; }
        public DateTime? CurrentPeriodEnd { get; init; }
        public bool AutoRenew { get; init; }
    }

    private sealed class OrderRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public string ProductCode { get; init; } = "";
        public string SubjectType { get; init; } = "";
        public Guid SubjectId { get; init; }
        public long OrderCode { get; init; }
        public string PublicCode { get; init; } = "";
        public string PlanCode { get; init; } = "";
        public int AmountVnd { get; init; }
        public string Currency { get; init; } = "VND";
        public string Status { get; init; } = "";
        public string? ProviderCode { get; init; }
        public string? CheckoutUrl { get; init; }
        public string? QrCode { get; init; }
        public string? Description { get; init; }
        public DateTime? PaidAt { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? ExpiresAt { get; init; }
    }

    private sealed class OrderActivateRow
    {
        public Guid Id { get; init; }
        public Guid TenantId { get; init; }
        public Guid? SubscriptionId { get; init; }
        public string ProductCode { get; init; } = "";
        public string SubjectType { get; init; } = "";
        public Guid SubjectId { get; init; }
        public long OrderCode { get; init; }
        public string PublicCode { get; init; } = "";
        public string PlanCode { get; init; } = "";
        public int AmountVnd { get; init; }
        public string Status { get; init; } = "";
        public int IntervalDays { get; init; }
        public DateTime? CurrentPeriodEnd { get; init; }
    }
}
