using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Core.Engines;
using KitPlatform.Application.Configuration;
using KitPlatform.Application.Integration;
using KitPlatform.Application.Platform.Events;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Sales;
using KitPlatform.Packs.Pharmacy.Workflow;
using Moq;
using Npgsql;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

[CollectionDefinition("kitplatform_test-sales-idempotency")]
public sealed class SalesDuplicateProtectionCollection
{
}

/// <summary>
/// Isolated duplicate-sale checks. Refuses kitplatform, kitplatform_test, and any
/// database that already contains a real tenant code.
/// </summary>
[Collection("kitplatform_test-sales-idempotency")]
public sealed class SalesDuplicateProtectionTests
{
    private const string ConnectionEnvironmentVariable = "POS_IDEMPOTENCY_TEST_CONNECTION";

    private static readonly HashSet<string> DeniedDatabases = new(StringComparer.OrdinalIgnoreCase)
    {
        "kitplatform",
        "kitplatform_test",
        "postgres",
        "novixa_prod",
        "pharmacore",
    };

    [Fact]
    public async Task Sequential_same_key_creates_one_completed_sale()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = "seq-" + Guid.NewGuid().ToString("N");

        var first = await world.CompleteAsync(key, 1m);
        var second = await world.CompleteAsync(key, 1m);

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.OrderId, second.OrderId);
        Assert.Equal(1, await world.CountOrdersAsync(key));
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, await world.CountPaymentsAsync(first.OrderId));
        Assert.Equal(1, world.OutboxCalls);
    }

    [Fact]
    public async Task Concurrent_same_return_key_writes_stock_once()
    {
        await RequireIsolatedTestDatabaseAsync();
        await RequireClientReturnColumnAsync();
        await using var world = await SaleWorld.CreateAsync();
        var sale = await world.CompleteAsync("sale-" + Guid.NewGuid().ToString("N"), 2m);
        var stockAfterSale = await world.StockAsync();
        var key = "ret-" + Guid.NewGuid().ToString("N");
        var errors = new ConcurrentBag<Exception>();

        var left = world.ReturnIsolatedAsync(sale.OrderId, key, 1m, errors);
        var right = world.ReturnIsolatedAsync(sale.OrderId, key, 1m, errors);
        await Task.WhenAll(left, right);

        var results = new[] { await left, await right }.Where(item => item is not null).Select(item => item!).ToList();
        Assert.Empty(errors);
        Assert.Equal(2, results.Count);
        Assert.Single(results.Select(item => item.Id).Distinct());
        Assert.Equal(1, results.Count(item => item.Replayed));
        Assert.Equal(1, await world.CountReturnsAsync(key));
        Assert.Equal(stockAfterSale + 1m, await world.StockAsync());
        Assert.Equal(1, await world.CountReturnMovementsAsync(results[0].Id));
        Assert.Equal(1, await world.CountReturnPaymentsAsync(results[0].Id));
        Assert.Equal(1, world.AuditCompletes);
    }

    [Fact]
    public async Task Return_replay_after_commit_does_not_repeat_side_effects()
    {
        await RequireIsolatedTestDatabaseAsync();
        await RequireClientReturnColumnAsync();
        await using var world = await SaleWorld.CreateAsync();
        var sale = await world.CompleteAsync("sale-" + Guid.NewGuid().ToString("N"), 1m);
        var key = "replay-ret-" + Guid.NewGuid().ToString("N");

        var first = await world.ReturnAsync(sale.OrderId, key, 1m);
        var stock = await world.StockAsync();
        var movements = await world.CountReturnMovementsAsync(first.Id);
        var payments = await world.CountReturnPaymentsAsync(first.Id);
        var audits = world.AuditCompletes;
        var outbox = world.OutboxCalls;

        var second = await world.ReturnAsync(sale.OrderId, key, 1m);

        Assert.False(first.Replayed);
        Assert.True(second.Replayed);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(stock, await world.StockAsync());
        Assert.Equal(movements, await world.CountReturnMovementsAsync(first.Id));
        Assert.Equal(payments, await world.CountReturnPaymentsAsync(first.Id));
        Assert.Equal(audits, world.AuditCompletes);
        Assert.Equal(outbox, world.OutboxCalls);
    }

    [Fact]
    public async Task Different_return_keys_can_both_return_similar_lines()
    {
        await RequireIsolatedTestDatabaseAsync();
        await RequireClientReturnColumnAsync();
        await using var world = await SaleWorld.CreateAsync();
        var sale = await world.CompleteAsync("sale-" + Guid.NewGuid().ToString("N"), 2m);
        var firstKey = "ret-a-" + Guid.NewGuid().ToString("N");
        var secondKey = "ret-b-" + Guid.NewGuid().ToString("N");

        var first = await world.ReturnAsync(sale.OrderId, firstKey, 1m);
        var second = await world.ReturnAsync(sale.OrderId, secondKey, 1m);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(1, await world.CountReturnsAsync(firstKey));
        Assert.Equal(1, await world.CountReturnsAsync(secondKey));
        Assert.Equal(20m, await world.StockAsync());
    }

    [Fact]
    public async Task Same_return_key_on_a_different_order_is_rejected()
    {
        await RequireIsolatedTestDatabaseAsync();
        await RequireClientReturnColumnAsync();
        await using var world = await SaleWorld.CreateAsync();
        var firstSale = await world.CompleteAsync("sale-a-" + Guid.NewGuid().ToString("N"), 1m);
        var secondSale = await world.CompleteAsync("sale-b-" + Guid.NewGuid().ToString("N"), 1m);
        var key = "shared-ret-" + Guid.NewGuid().ToString("N");

        await world.ReturnAsync(firstSale.OrderId, key, 1m);
        var stock = await world.StockAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.ReturnAsync(secondSale.OrderId, key, 1m));

        Assert.Contains("đơn khác", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, await world.CountReturnsAsync(key));
        Assert.Equal(stock, await world.StockAsync());
    }

    [Fact]
    public async Task Retry_after_commit_does_not_repeat_side_effects()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = "retry-" + Guid.NewGuid().ToString("N");

        var first = await world.CompleteAsync(key, 1m);
        var stockAfterCommit = await world.StockAsync();
        var paymentsAfterCommit = await world.CountPaymentsAsync(first.OrderId);
        var outboxAfterCommit = world.OutboxCalls;

        var retry = await world.CompleteAsync(key, 1m);

        Assert.True(retry.Replayed);
        Assert.Equal(first.OrderId, retry.OrderId);
        Assert.Equal(stockAfterCommit, await world.StockAsync());
        Assert.Equal(paymentsAfterCommit, await world.CountPaymentsAsync(first.OrderId));
        Assert.Equal(outboxAfterCommit, world.OutboxCalls);
        Assert.Equal(19m, stockAfterCommit);
    }

    [Fact]
    public async Task Same_key_with_different_quantity_keeps_the_first_sale()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = "mismatch-" + Guid.NewGuid().ToString("N");

        var first = await world.CompleteAsync(key, 1m);
        var second = await world.CompleteAsync(key, 2m);

        Assert.Equal(first.OrderId, second.OrderId);
        Assert.True(second.Replayed);
        Assert.Equal(1m, await world.LineQuantityAsync(first.OrderId));
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, await world.CountOrdersAsync(key));
    }

    [Fact]
    public async Task Two_requests_without_a_key_create_two_sales()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();

        var first = await world.CompleteAsync(null, 1m);
        var second = await world.CompleteAsync("   ", 1m);

        Assert.NotEqual(first.OrderId, second.OrderId);
        Assert.False(first.Replayed);
        Assert.False(second.Replayed);
        Assert.Equal(18m, await world.StockAsync());
        Assert.Equal(2, world.OutboxCalls);
    }

    [Fact]
    public async Task Same_key_on_two_tenants_creates_two_sales()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var firstTenant = await SaleWorld.CreateAsync();
        await using var secondTenant = await SaleWorld.CreateAsync();
        var key = "tenant-" + Guid.NewGuid().ToString("N");

        var first = await firstTenant.CompleteAsync(key, 1m);
        var second = await secondTenant.CompleteAsync(key, 1m);

        Assert.NotEqual(first.OrderId, second.OrderId);
        Assert.False(second.Replayed);
        Assert.Equal(1, await firstTenant.CountOrdersAsync(key));
        Assert.Equal(1, await secondTenant.CountOrdersAsync(key));
        Assert.Equal(19m, await firstTenant.StockAsync());
        Assert.Equal(19m, await secondTenant.StockAsync());
    }

    [Fact]
    public async Task Concurrent_same_key_on_two_connections_keeps_one_sale()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = "race-" + Guid.NewGuid().ToString("N");
        var errors = new ConcurrentBag<Exception>();

        var left = world.CompleteIsolatedAsync(key, 1m, errors);
        var right = world.CompleteIsolatedAsync(key, 1m, errors);
        var completed = (await Task.WhenAll(left, right)).Where(x => x is not null).Select(x => x!.Value).ToList();

        Assert.Equal(1, await world.CountOrdersAsync(key));
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, world.OutboxCalls);
        Assert.NotEmpty(completed);
        var winner = completed[0].OrderId;
        Assert.All(completed, item => Assert.Equal(winner, item.OrderId));
        Assert.Equal(1, await world.CountPaymentsAsync(winner));

        var retry = await world.CompleteAsync(key, 1m);
        Assert.Equal(winner, retry.OrderId);
        Assert.True(retry.Replayed);
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, world.OutboxCalls);
    }

    [Fact]
    public async Task Concurrent_same_key_with_voucher_and_loyalty_keeps_one_sale()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        await world.AttachBenefitsAsync();
        var key = "race-benefits-" + Guid.NewGuid().ToString("N");
        var errors = new ConcurrentBag<Exception>();

        var left = world.CompleteBenefitsIsolatedAsync(key, errors);
        var right = world.CompleteBenefitsIsolatedAsync(key, errors);
        var completed = (await Task.WhenAll(left, right)).Where(x => x is not null).Select(x => x!.Value).ToList();

        Assert.Equal(1, await world.CountOrdersAsync(key));
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, world.OutboxCalls);
        Assert.Equal(1, await world.VoucherUsedCountAsync());
        Assert.NotEmpty(completed);
        var winner = completed[0].OrderId;
        Assert.All(completed, item => Assert.Equal(winner, item.OrderId));

        var points = await world.PointsBalanceAsync();
        var loyaltyRows = await world.CountLoyaltyTransactionsAsync(winner);
        var retry = await world.CompleteBenefitsAsync(key);
        Assert.Equal(winner, retry.OrderId);
        Assert.True(retry.Replayed);
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, world.OutboxCalls);
        Assert.Equal(1, await world.VoucherUsedCountAsync());
        Assert.Equal(points, await world.PointsBalanceAsync());
        Assert.Equal(loyaltyRows, await world.CountLoyaltyTransactionsAsync(winner));
    }

    [Fact]
    public async Task Key_longer_than_64_characters_is_rejected_without_a_sale()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = new string('k', 65);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => world.CompleteAsync(key, 1m));

        Assert.Contains("64", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, await world.CountOrdersByKeyOrEmptyAsync(key));
        Assert.Equal(20m, await world.StockAsync());
        Assert.Equal(0, world.OutboxCalls);
    }

    [Fact]
    public async Task Replay_does_not_repeat_stock_payment_voucher_loyalty_or_outbox()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        await world.AttachBenefitsAsync();
        var key = "benefits-" + Guid.NewGuid().ToString("N");

        var first = await world.CompleteBenefitsAsync(key);
        var stock = await world.StockAsync();
        var payments = await world.CountPaymentsAsync(first.OrderId);
        var movements = await world.CountStockMovementsAsync(first.OrderId);
        var pharmacyOrders = await world.CountPharmacyOrdersAsync(first.OrderId);
        var voucherUses = await world.VoucherUsedCountAsync();
        var points = await world.PointsBalanceAsync();
        var loyaltyRows = await world.CountLoyaltyTransactionsAsync(first.OrderId);
        var outbox = world.OutboxCalls;

        var retry = await world.CompleteBenefitsAsync(key);

        Assert.False(first.Replayed);
        Assert.True(retry.Replayed);
        Assert.Equal(first.OrderId, retry.OrderId);
        Assert.Equal(stock, await world.StockAsync());
        Assert.Equal(19m, stock);
        Assert.Equal(payments, await world.CountPaymentsAsync(first.OrderId));
        Assert.Equal(1, payments);
        Assert.Equal(movements, await world.CountStockMovementsAsync(first.OrderId));
        Assert.Equal(1, movements);
        Assert.Equal(pharmacyOrders, await world.CountPharmacyOrdersAsync(first.OrderId));
        Assert.Equal(1, pharmacyOrders);
        Assert.Equal(voucherUses, await world.VoucherUsedCountAsync());
        Assert.Equal(1, voucherUses);
        Assert.Equal(points, await world.PointsBalanceAsync());
        Assert.Equal(loyaltyRows, await world.CountLoyaltyTransactionsAsync(first.OrderId));
        Assert.True(loyaltyRows >= 1);
        Assert.Equal(outbox, world.OutboxCalls);
        Assert.Equal(1, outbox);
    }

    [Fact]
    public async Task Concurrent_draft_completion_decrements_stock_once()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var draftId = await world.CreateDraftAsync();
        Assert.Equal(20m, await world.StockAsync());
        var errors = new ConcurrentBag<Exception>();

        var left = world.CompleteDraftIsolatedAsync(draftId, errors);
        var right = world.CompleteDraftIsolatedAsync(draftId, errors);
        var outcomes = (await Task.WhenAll(left, right)).Where(x => x is not null).Select(x => x!.Value).ToList();

        Assert.Contains(DraftCompletionOutcome.CompletedNow, outcomes);
        Assert.Contains(DraftCompletionOutcome.AlreadyCompleted, outcomes);
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, await world.CountPaymentsForOrderAsync(draftId));
        Assert.Equal(1, world.OutboxCalls);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Completing_an_already_completed_draft_does_not_repeat_side_effects()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var draftId = await world.CreateDraftAsync();

        var first = await world.CompleteDraftAsync(draftId);
        var stock = await world.StockAsync();
        var payments = await world.CountPaymentsForOrderAsync(draftId);
        var outbox = world.OutboxCalls;

        var second = await world.CompleteDraftAsync(draftId);

        Assert.Equal(DraftCompletionOutcome.CompletedNow, first);
        Assert.Equal(DraftCompletionOutcome.AlreadyCompleted, second);
        Assert.Equal(stock, await world.StockAsync());
        Assert.Equal(payments, await world.CountPaymentsForOrderAsync(draftId));
        Assert.Equal(outbox, world.OutboxCalls);
        Assert.Equal(19m, stock);
    }

    private static string? ActiveConnectionString { get; set; }

    private static async Task RequireIsolatedTestDatabaseAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Fail(
                $"Stopped before any connection. Set {ConnectionEnvironmentVariable} to an empty isolated database. "
                + "kitplatform and kitplatform_test are refused.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (DeniedDatabases.Contains(builder.Database ?? "")
            || (builder.Database ?? "").Contains("prod", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"Refusing database '{builder.Database}'. No connection was opened for writes.");
        }

        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
        if (!string.Equals(database, builder.Database, StringComparison.Ordinal)
            || DeniedDatabases.Contains(database))
        {
            Assert.Fail($"Refusing to run against database '{database}'. No writes were attempted.");
        }

        await NovixaValidationSession.EnsureIsolatedRoleAsync(conn);

        var realTenants = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
            FROM tenants
            WHERE tenant_code IN ('DEMO_PHARMACY', 'NT_XUANHOA', 'KIT_MKT')
            """);
        if (realTenants != 0)
            Assert.Fail("Refusing database that already contains a real tenant code. No test rows were inserted.");

        ActiveConnectionString = builder.ConnectionString;

        var missing = new List<string>();
        var column = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'sales_orders'
              AND column_name = 'client_sale_id'
            """);
        if (column != 1)
            missing.Add("public.sales_orders.client_sale_id");

        var index = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
            FROM pg_indexes
            WHERE indexname = 'uq_sales_orders_tenant_client_sale_id'
            """);
        if (index != 1)
            missing.Add("uq_sales_orders_tenant_client_sale_id");

        foreach (var relation in new[]
        {
            "public.sales_shifts",
            "public.repurchase_suggestions",
            "public.customer_accounts",
            "public.sales_payments",
            "public.stock_movements",
            "public.product_prices",
            "public.medication_reminders",
            "pack_pharmacy.pharmacy_sales_order",
        })
        {
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT to_regclass(@Relation) IS NOT NULL",
                new { Relation = relation });
            if (!exists)
                missing.Add(relation);
        }

        if (missing.Count > 0)
        {
            Assert.Fail(
                "Stopped before any write. Isolated database is missing: "
                + string.Join(", ", missing)
                + ". No migration was applied.");
        }
    }

    private static async Task RequireClientReturnColumnAsync()
    {
        await using var conn = new NpgsqlConnection(ActiveConnectionString);
        await conn.OpenAsync();
        var column = await conn.ExecuteScalarAsync<int>("""
            SELECT COUNT(*)::int
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'sales_returns'
              AND column_name = 'client_return_id'
            """);
        if (column != 1)
            Assert.Fail("Apply migrations/411_sales_return_client_return_id.sql on the isolated database before this test.");
    }

    [Fact]
    public async Task Service_replay_returns_http_equivalent_order_without_a_second_audit()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = "svc-" + Guid.NewGuid().ToString("N");

        var first = await world.Service.CreateSaleAsync(world.SaleRequest(key, 1m));
        var second = await world.Service.CreateSaleAsync(world.SaleRequest(key, 1m));

        Assert.False(first.IdempotentReplay);
        Assert.True(second.IdempotentReplay);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, world.AuditCompletes);
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, world.OutboxCalls);
        Assert.Equal(1, await world.CountPaymentsAsync(first.Id));
    }

    [Fact]
    public async Task Service_same_key_with_different_quantity_keeps_the_first_sale()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var key = "svc-diff-" + Guid.NewGuid().ToString("N");

        var first = await world.Service.CreateSaleAsync(world.SaleRequest(key, 1m));
        var second = await world.Service.CreateSaleAsync(world.SaleRequest(key, 2m));

        Assert.Equal(first.Id, second.Id);
        Assert.True(second.IdempotentReplay);
        Assert.Equal(1m, await world.LineQuantityAsync(first.Id));
        Assert.Equal(1, world.AuditCompletes);
        Assert.Equal(19m, await world.StockAsync());
    }

    [Fact]
    public async Task Service_without_a_key_creates_two_audited_sales()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();

        var first = await world.Service.CreateSaleAsync(world.SaleRequest(null, 1m));
        var second = await world.Service.CreateSaleAsync(world.SaleRequest(null, 1m));

        Assert.NotEqual(first.Id, second.Id);
        Assert.False(first.IdempotentReplay);
        Assert.False(second.IdempotentReplay);
        Assert.Equal(2, world.AuditCompletes);
        Assert.Equal(18m, await world.StockAsync());
    }

    [Fact]
    public async Task Service_invalid_key_is_rejected_without_an_audit()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => world.Service.CreateSaleAsync(world.SaleRequest(new string('k', 65), 1m)));

        Assert.Contains("64", ex.Message, StringComparison.Ordinal);
        Assert.Equal(0, world.AuditCompletes);
        Assert.Equal(20m, await world.StockAsync());
    }

    [Fact]
    public async Task Service_second_draft_completion_does_not_audit_again()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        var draft = await world.Service.CreateSaleAsync(world.SaleRequest(null, 1m) with { SaveAsDraft = true });

        var first = await world.Service.CompleteDraftSaleAsync(draft.Id, null);
        var second = await world.Service.CompleteDraftSaleAsync(draft.Id, null);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(1, world.AuditCompletes);
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(1, world.OutboxCalls);
    }

    [Fact]
    public async Task Service_same_key_on_two_tenants_returns_each_tenants_order()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var firstTenant = await SaleWorld.CreateAsync();
        await using var secondTenant = await SaleWorld.CreateAsync();
        var key = "svc-tenant-" + Guid.NewGuid().ToString("N");

        var first = await firstTenant.Service.CreateSaleAsync(firstTenant.SaleRequest(key, 1m));
        var second = await secondTenant.Service.CreateSaleAsync(secondTenant.SaleRequest(key, 1m));
        var replay = await firstTenant.Service.CreateSaleAsync(firstTenant.SaleRequest(key, 1m));

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.Id, replay.Id);
        Assert.True(replay.IdempotentReplay);
        Assert.False(second.IdempotentReplay);
        Assert.Equal(1, firstTenant.AuditCompletes);
        Assert.Equal(1, secondTenant.AuditCompletes);
    }

    [Fact]
    public async Task Service_lookup_by_client_sale_id_is_tenant_scoped_and_has_no_side_effects()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var owner = await SaleWorld.CreateAsync();
        await using var other = await SaleWorld.CreateAsync();
        var key = "svc-lookup-" + Guid.NewGuid().ToString("N");

        Assert.Null(await owner.Service.FindSaleByClientSaleIdAsync(key));
        Assert.Null(await owner.Service.FindSaleByClientSaleIdAsync("missing-" + Guid.NewGuid().ToString("N")));
        Assert.False(await owner.UncommittedKeyVisibleAsync(key));
        Assert.Null(await owner.Service.FindSaleByClientSaleIdAsync(key));

        var created = await owner.Service.CreateSaleAsync(owner.SaleRequest(key, 1m));
        var found = await owner.Service.FindSaleByClientSaleIdAsync(key);
        var hidden = await other.Service.FindSaleByClientSaleIdAsync(key);
        var audits = owner.AuditCompletes;
        var stock = await owner.StockAsync();
        var payments = await owner.CountPaymentsAsync(created.Id);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found!.Id);
        Assert.Null(hidden);
        Assert.Equal(1, audits);
        Assert.Equal(19m, stock);
        Assert.Equal(1, payments);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            owner.Service.FindSaleByClientSaleIdAsync(new string('x', 65)));
    }

    [Fact]
    public async Task Service_replay_does_not_repeat_voucher_loyalty_or_audit()
    {
        await RequireIsolatedTestDatabaseAsync();
        await using var world = await SaleWorld.CreateAsync();
        await world.AttachBenefitsAsync();
        var key = "svc-benefits-" + Guid.NewGuid().ToString("N");

        var first = await world.CompleteBenefitsThroughServiceAsync(key);
        var points = await world.PointsBalanceAsync();
        var voucherUses = await world.VoucherUsedCountAsync();
        var loyaltyRows = await world.CountLoyaltyTransactionsAsync(first.Id);
        var second = await world.CompleteBenefitsThroughServiceAsync(key);

        Assert.False(first.IdempotentReplay);
        Assert.True(second.IdempotentReplay);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, world.AuditCompletes);
        Assert.Equal(1, world.OutboxCalls);
        Assert.Equal(19m, await world.StockAsync());
        Assert.Equal(voucherUses, await world.VoucherUsedCountAsync());
        Assert.Equal(1, voucherUses);
        Assert.Equal(points, await world.PointsBalanceAsync());
        Assert.Equal(loyaltyRows, await world.CountLoyaltyTransactionsAsync(first.Id));
        Assert.Equal(1, await world.CountPaymentsAsync(first.Id));
        Assert.Equal(1, await world.CountStockMovementsAsync(first.Id));
        Assert.Equal(1, await world.CountPharmacyOrdersAsync(first.Id));
    }

    private sealed class SaleWorld : IAsyncDisposable
    {
        private readonly NpgsqlConnectionFactory _db;
        private readonly MutableTenant _tenant;
        private readonly CountingOutbox _outbox;
        private readonly CountingAudit _audit;
        private readonly SalesRepository _repository;
        private readonly Guid _branchId = Guid.NewGuid();
        private readonly Guid _warehouseId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _productId = Guid.NewGuid();
        private readonly Guid _unitId = Guid.NewGuid();
        private readonly Guid _batchId = Guid.NewGuid();
        private readonly Guid _priceId = Guid.NewGuid();
        private readonly Guid _shiftId = Guid.NewGuid();
        private readonly Guid _benefitCustomerId = Guid.NewGuid();
        private readonly Guid _benefitProgramId = Guid.NewGuid();
        private readonly Guid _benefitVoucherId = Guid.NewGuid();
        private readonly Guid _benefitWalletId = Guid.NewGuid();
        private readonly string _code;

        private SaleWorld(
            NpgsqlConnectionFactory db,
            MutableTenant tenant,
            CountingOutbox outbox,
            CountingAudit audit,
            SalesRepository repository,
            ISalesService service,
            string code)
        {
            _db = db;
            _tenant = tenant;
            _outbox = outbox;
            _audit = audit;
            _repository = repository;
            Service = service;
            _code = code;
        }

        public int OutboxCalls => _outbox.Calls;

        public int AuditCompletes => _audit.Completes;

        public ISalesService Service { get; }

        public static async Task<SaleWorld> CreateAsync()
        {
            var db = new NpgsqlConnectionFactory(ActiveConnectionString
                ?? throw new InvalidOperationException("Isolated connection was not verified."));
            var tenant = new MutableTenant(Guid.NewGuid(), Guid.NewGuid());
            var outbox = new CountingOutbox();
            var platform = new CountingPlatformEvents();
            var settings = new Mock<ITenantSettingsService>();
            settings.Setup(s => s.GetBatchModeAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(TenantBatchMode.Off);
            settings.Setup(s => s.GetRxSettingsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TenantRxSettingsDto("off", false));
            var workflow = new Mock<IWorkflowEngine>();
            workflow.Setup(w => w.EnforcePosDiscountApprovalAsync(
                    It.IsAny<SaleOrderPricingResult>(),
                    It.IsAny<SalesDiscountPolicy>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var inventory = new InventoryRepository(db, tenant);
            var loyalty = CreateInternal("KitPlatform.Infrastructure.Loyalty.LoyaltyPosService", db);
            var voucherRepository = CreateInternal("KitPlatform.Infrastructure.Loyalty.VoucherRepository", db);
            var vouchers = CreateInternal(
                "KitPlatform.Infrastructure.Loyalty.VoucherPosService",
                voucherRepository,
                db);
            var repositoryType = typeof(InventoryRepository).Assembly.GetType(
                "KitPlatform.Packs.Pharmacy.Infrastructure.SalesRepository",
                throwOnError: true)!;
            var repository = (SalesRepository)Activator.CreateInstance(
                repositoryType,
                db,
                tenant,
                inventory,
                new BatchResolver(db, tenant),
                settings.Object,
                outbox,
                platform,
                workflow.Object,
                loyalty,
                vouchers,
                new PrescriptionRepository(db, tenant, inventory))!;

            var audit = new CountingAudit();
            var serviceType = repositoryType.Assembly.GetType(
                "KitPlatform.Packs.Pharmacy.Infrastructure.SalesService",
                throwOnError: true)!;
            var service = (ISalesService)Activator.CreateInstance(
                serviceType,
                repository,
                loyalty,
                vouchers,
                tenant,
                new AdminUser(),
                audit,
                new AllowAllBranches())!;
            var world = new SaleWorld(db, tenant, outbox, audit, repository, service, "P" + Guid.NewGuid().ToString("N")[..12]);
            world._tenant.UserId = world._userId;
            await world.SeedAsync();
            return world;
        }

        public async Task<bool> UncommittedKeyVisibleAsync(string key)
        {
            await using var hold = await _db.CreateOpenConnectionAsync();
            await using var tx = await hold.BeginTransactionAsync();
            await hold.ExecuteAsync(
                """
                INSERT INTO sales_orders (
                    tenant_id, order_number, branch_id, warehouse_id,
                    status, price_type, subtotal, discount_amount, total_amount,
                    amount_paid, outstanding, client_sale_id)
                VALUES (
                    @TenantId, @OrderNumber, @BranchId, @WarehouseId,
                    2, 1, 1000, 0, 1000, 1000, 0, @Key)
                """,
                new
                {
                    TenantId = _tenant.TenantId,
                    OrderNumber = "HOLD-" + Guid.NewGuid().ToString("N")[..12],
                    BranchId = _branchId,
                    WarehouseId = _warehouseId,
                    Key = key,
                },
                tx);
            var seen = await Service.FindSaleByClientSaleIdAsync(key);
            await tx.RollbackAsync();
            return seen is not null;
        }

        public async Task AttachBenefitsAsync()
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
            if (DeniedDatabases.Contains(database))
                throw new InvalidOperationException($"Refusing benefit seed against database '{database}'.");

            await conn.ExecuteAsync("""
                UPDATE tenants
                SET settings = COALESCE(settings, '{}'::jsonb) || '{"loyalty_enabled": true}'::jsonb
                WHERE id = @TenantId;

                INSERT INTO customers (id, tenant_id, customer_code, full_name, phone, status)
                VALUES (@CustomerId, @TenantId, @Code, @Code, @Code, 1);

                INSERT INTO loyalty_programs (
                    id, tenant_id, program_code, program_name, points_per_amount, amount_per_point, max_redeem_percent, status)
                VALUES (@ProgramId, @TenantId, @Code, @Code, 10000, 200, 100, 1);

                INSERT INTO customer_loyalty (customer_id, program_id, points_balance, lifetime_points)
                VALUES (@CustomerId, @ProgramId, 5, 5);

                INSERT INTO vouchers (
                    id, tenant_id, voucher_code, voucher_name, discount_type, discount_value,
                    min_order_amount, max_uses, used_count, valid_from, valid_to, status)
                VALUES (
                    @VoucherId, @TenantId, @Code, @Code, 2, 200,
                    0, 1, 0, NOW() - INTERVAL '1 day', NOW() + INTERVAL '30 days', 1);

                INSERT INTO customer_vouchers (id, customer_id, voucher_id)
                VALUES (@WalletId, @CustomerId, @VoucherId);
                """, new
            {
                TenantId = _tenant.TenantId,
                CustomerId = _benefitCustomerId,
                ProgramId = _benefitProgramId,
                VoucherId = _benefitVoucherId,
                WalletId = _benefitWalletId,
                Code = _code,
            });
        }

        public Task<SalesOrderDetailDto> CompleteBenefitsThroughServiceAsync(string clientSaleId) =>
            Service.CreateSaleAsync(BenefitsRequest(clientSaleId));

        public Task<CompletedSaleCreateResult> CompleteBenefitsAsync(string clientSaleId) =>
            _repository.CreateCompletedSaleAsync(
                new CreateSaleRequest(
                    _warehouseId,
                    _benefitCustomerId,
                    SalesPriceTypes.Retail,
                    [new CreateSaleLineRequest(_productId, _unitId, 1m)],
                    OrderDiscountValue: 0,
                    LoyaltyDiscountAmount: 200m,
                    CustomerVoucherId: _benefitWalletId,
                    ClientSaleId: clientSaleId),
                _userId,
                new SalesDiscountPolicy(true, true, 100m),
                false,
                CancellationToken.None);

        public Task<CompletedSaleCreateResult> CompleteAsync(string? clientSaleId, decimal quantity) =>
            _repository.CreateCompletedSaleAsync(
                Request(clientSaleId, quantity),
                _userId,
                new SalesDiscountPolicy(true, true, 100m),
                false,
                CancellationToken.None);

        public async Task<CompletedSaleCreateResult?> CompleteBenefitsIsolatedAsync(
            string clientSaleId,
            ConcurrentBag<Exception> errors)
        {
            try
            {
                return await CompleteBenefitsAsync(clientSaleId);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                return null;
            }
        }

        public async Task<CompletedSaleCreateResult?> CompleteIsolatedAsync(
            string clientSaleId,
            decimal quantity,
            ConcurrentBag<Exception> errors)
        {
            try
            {
                return await CompleteAsync(clientSaleId, quantity);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                return null;
            }
        }

        public Task<Guid> CreateDraftAsync() =>
            _repository.CreateDraftSaleAsync(
                Request(null, 1m),
                _userId,
                new SalesDiscountPolicy(true, true, 100m),
                false,
                CancellationToken.None);

        public Task<DraftCompletionOutcome> CompleteDraftAsync(Guid draftId) =>
            _repository.CompleteDraftSaleAsync(
                draftId,
                null,
                new SalesDiscountPolicy(true, true, 100m),
                false,
                CancellationToken.None);

        public async Task<DraftCompletionOutcome?> CompleteDraftIsolatedAsync(
            Guid draftId,
            ConcurrentBag<Exception> errors)
        {
            try
            {
                return await CompleteDraftAsync(draftId);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                return null;
            }
        }

        public async Task<SalesReturnDetailDto> ReturnAsync(Guid orderId, string clientReturnId, decimal quantity)
        {
            var order = await Service.GetOrderAsync(orderId)
                ?? throw new InvalidOperationException("Đơn bán không tồn tại.");
            var line = order.Items.First();
            return await Service.CreateSaleReturnAsync(
                orderId,
                new CreateSaleReturnRequest(
                    "synthetic",
                    [new CreateSaleReturnLineRequest(line.Id, quantity)],
                    null,
                    clientReturnId));
        }

        public async Task<SalesReturnDetailDto?> ReturnIsolatedAsync(
            Guid orderId,
            string clientReturnId,
            decimal quantity,
            ConcurrentBag<Exception> errors)
        {
            try
            {
                return await ReturnAsync(orderId, clientReturnId, quantity);
            }
            catch (Exception ex)
            {
                errors.Add(ex);
                return null;
            }
        }

        public async Task<int> CountReturnsAsync(string key)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int
                FROM sales_returns
                WHERE tenant_id = @TenantId AND client_return_id = @Key
                """, new { TenantId = _tenant.TenantId, Key = key });
        }

        public async Task<int> CountReturnMovementsAsync(Guid returnId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int
                FROM stock_movements
                WHERE tenant_id = @TenantId AND reference_id = @ReturnId
                """, new { TenantId = _tenant.TenantId, ReturnId = returnId });
        }

        public async Task<int> CountReturnPaymentsAsync(Guid returnId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int
                FROM sales_return_payments
                WHERE sales_return_id = @ReturnId
                """, new { ReturnId = returnId });
        }

        public async Task<int> CountOrdersAsync(string key)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int
                FROM sales_orders
                WHERE tenant_id = @TenantId AND client_sale_id = @Key
                """, new { TenantId = _tenant.TenantId, Key = key });
        }

        public Task<int> CountOrdersByKeyOrEmptyAsync(string key) => CountOrdersAsync(key);

        public async Task<decimal> StockAsync()
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<decimal>("""
                SELECT quantity_available
                FROM inventory_batches
                WHERE id = @BatchId AND tenant_id = @TenantId
                """, new { BatchId = _batchId, TenantId = _tenant.TenantId });
        }

        public async Task<int> CountPaymentsAsync(Guid orderId) => await CountPaymentsForOrderAsync(orderId);

        public async Task<int> CountPaymentsForOrderAsync(Guid orderId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int
                FROM sales_payments
                WHERE sales_order_id = @OrderId
                """, new { OrderId = orderId });
        }

        public async Task<int> CountStockMovementsAsync(Guid orderId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int FROM stock_movements
                WHERE tenant_id = @TenantId AND reference_id = @OrderId
                """, new { TenantId = _tenant.TenantId, OrderId = orderId });
        }

        public async Task<int> CountPharmacyOrdersAsync(Guid orderId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int FROM pack_pharmacy.pharmacy_sales_order
                WHERE tenant_id = @TenantId AND sales_order_id = @OrderId
                """, new { TenantId = _tenant.TenantId, OrderId = orderId });
        }

        public async Task<int> VoucherUsedCountAsync()
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT used_count FROM vouchers WHERE id = @VoucherId AND tenant_id = @TenantId
                """, new { VoucherId = _benefitVoucherId, TenantId = _tenant.TenantId });
        }

        public async Task<decimal> PointsBalanceAsync()
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<decimal>("""
                SELECT points_balance FROM customer_loyalty
                WHERE customer_id = @CustomerId AND program_id = @ProgramId
                """, new { CustomerId = _benefitCustomerId, ProgramId = _benefitProgramId });
        }

        public async Task<int> CountLoyaltyTransactionsAsync(Guid orderId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<int>("""
                SELECT COUNT(*)::int FROM loyalty_transactions
                WHERE tenant_id = @TenantId AND sales_order_id = @OrderId
                """, new { TenantId = _tenant.TenantId, OrderId = orderId });
        }

        public async Task<decimal> LineQuantityAsync(Guid orderId)
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            return await conn.ExecuteScalarAsync<decimal>("""
                SELECT quantity
                FROM sales_order_items
                WHERE sales_order_id = @OrderId
                """, new { OrderId = orderId });
        }

        public async ValueTask DisposeAsync()
        {
            await using var conn = new NpgsqlConnection(ActiveConnectionString);
            await conn.OpenAsync();
            var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
            if (DeniedDatabases.Contains(database))
                throw new InvalidOperationException($"Refusing cleanup against database '{database}'.");

            await conn.ExecuteAsync("""
                DELETE FROM loyalty_transactions WHERE tenant_id = @TenantId;
                DELETE FROM customer_vouchers
                WHERE customer_id IN (SELECT id FROM customers WHERE tenant_id = @TenantId);
                DELETE FROM customer_loyalty
                WHERE customer_id IN (SELECT id FROM customers WHERE tenant_id = @TenantId);
                DELETE FROM medication_reminders WHERE tenant_id = @TenantId;
                DELETE FROM repurchase_suggestions WHERE tenant_id = @TenantId;
                DELETE FROM pack_pharmacy.pharmacy_sales_order WHERE tenant_id = @TenantId;
                DELETE FROM sales_order_batch_allocations
                WHERE sales_order_item_id IN (
                    SELECT id FROM sales_order_items
                    WHERE sales_order_id IN (SELECT id FROM sales_orders WHERE tenant_id = @TenantId)
                );
                DELETE FROM sales_return_payments
                WHERE sales_return_id IN (SELECT id FROM sales_returns WHERE tenant_id = @TenantId);
                DELETE FROM sales_return_items
                WHERE sales_return_id IN (SELECT id FROM sales_returns WHERE tenant_id = @TenantId);
                DELETE FROM sales_returns WHERE tenant_id = @TenantId;
                DELETE FROM sales_payments
                WHERE sales_order_id IN (SELECT id FROM sales_orders WHERE tenant_id = @TenantId);
                DELETE FROM stock_movements WHERE tenant_id = @TenantId;
                DELETE FROM sales_order_items
                WHERE sales_order_id IN (SELECT id FROM sales_orders WHERE tenant_id = @TenantId);
                DELETE FROM sales_orders WHERE tenant_id = @TenantId;
                DELETE FROM sales_shifts WHERE tenant_id = @TenantId;
                DELETE FROM inventory_batches WHERE tenant_id = @TenantId;
                DELETE FROM product_prices WHERE tenant_id = @TenantId;
                DELETE FROM product_units WHERE tenant_id = @TenantId;
                DELETE FROM products WHERE tenant_id = @TenantId;
                DELETE FROM vouchers WHERE tenant_id = @TenantId;
                DELETE FROM loyalty_programs WHERE tenant_id = @TenantId;
                DELETE FROM customers WHERE tenant_id = @TenantId;
                DELETE FROM users WHERE tenant_id = @TenantId;
                DELETE FROM warehouses WHERE tenant_id = @TenantId;
                DELETE FROM branches WHERE tenant_id = @TenantId;
                DELETE FROM tenants WHERE id = @TenantId;
                """, new { TenantId = _tenant.TenantId });
        }

        private CreateSaleRequest BenefitsRequest(string clientSaleId) =>
            new(
                _warehouseId,
                _benefitCustomerId,
                SalesPriceTypes.Retail,
                [new CreateSaleLineRequest(_productId, _unitId, 1m)],
                OrderDiscountValue: 0,
                LoyaltyDiscountAmount: 200m,
                CustomerVoucherId: _benefitWalletId,
                ClientSaleId: clientSaleId);

        public CreateSaleRequest SaleRequest(string? clientSaleId, decimal quantity) =>
            Request(clientSaleId, quantity);

        private CreateSaleRequest Request(string? clientSaleId, decimal quantity) =>
            new(
                _warehouseId,
                null,
                SalesPriceTypes.Retail,
                [new CreateSaleLineRequest(_productId, _unitId, quantity)],
                OrderDiscountValue: 0,
                ClientSaleId: clientSaleId);

        private async Task SeedAsync()
        {
            await using var conn = await _db.CreateOpenConnectionAsync();
            var database = await conn.ExecuteScalarAsync<string>("SELECT current_database()");
            if (DeniedDatabases.Contains(database))
                throw new InvalidOperationException($"Refusing seed against database '{database}'.");

            await conn.ExecuteAsync("""
                INSERT INTO tenants (id, tenant_code, tenant_name, status)
                VALUES (@TenantId, @Code, @Code, 1);

                INSERT INTO branches (id, tenant_id, branch_code, branch_name, status)
                VALUES (@BranchId, @TenantId, @Code, @Code, 1);

                INSERT INTO warehouses (
                    id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type, status)
                VALUES (@WarehouseId, @TenantId, @BranchId, @Code, @Code, 1, 1);

                INSERT INTO users (id, tenant_id, username, email, password_hash, status)
                VALUES (@UserId, @TenantId, @Code, @Email, 'not-a-login', 1);

                INSERT INTO products (id, tenant_id, product_code, product_name, status)
                VALUES (@ProductId, @TenantId, @Code, @Code, 1);

                INSERT INTO product_units (
                    id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit, status)
                VALUES (@UnitId, @TenantId, @ProductId, 'Viên', 1, TRUE, TRUE, 1);

                INSERT INTO product_prices (
                    id, tenant_id, product_id, product_unit_id, price_type, price, status)
                VALUES (@PriceId, @TenantId, @ProductId, @UnitId, 1, 1000, 1);

                INSERT INTO inventory_batches (
                    id, tenant_id, warehouse_id, product_id, batch_number,
                    unit_cost, quantity_received, quantity_available, status)
                VALUES (@BatchId, @TenantId, @WarehouseId, @ProductId, 'LOT-1', 100, 20, 20, 1);

                INSERT INTO sales_shifts (
                    id, tenant_id, warehouse_id, opened_by, shift_number, opening_cash, status)
                VALUES (@ShiftId, @TenantId, @WarehouseId, @UserId, @Code, 0, 1);
                """, new
            {
                TenantId = _tenant.TenantId,
                Code = _code,
                BranchId = _branchId,
                WarehouseId = _warehouseId,
                UserId = _userId,
                Email = _code + "@example.test",
                ProductId = _productId,
                UnitId = _unitId,
                PriceId = _priceId,
                BatchId = _batchId,
                ShiftId = _shiftId,
            });
        }

        private static object CreateInternal(string fullName, params object[] args)
        {
            var type = typeof(NpgsqlConnectionFactory).Assembly.GetType(fullName, throwOnError: true)!;
            return Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;
        }
    }

    private sealed class MutableTenant : ITenantContext
    {
        public MutableTenant(Guid tenantId, Guid userId)
        {
            TenantId = tenantId;
            UserId = userId;
        }

        public Guid TenantId { get; }
        public Guid UserId { get; set; }
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    private sealed class CountingAudit : IAuditEngine
    {
        private int _completes;
        public int Completes => _completes;

        public Task WriteAsync(
            string entityType,
            Guid? entityId,
            string action,
            object? payload = null,
            CancellationToken cancellationToken = default)
        {
            if (action == "complete")
                Interlocked.Increment(ref _completes);
            return Task.CompletedTask;
        }
    }

    private sealed class AdminUser : ICurrentUserAccessor
    {
        public IReadOnlyList<string> Permissions { get; } = ["sales.pos"];
        public bool IsInRole(string role) => role is "ADMIN" or "MANAGER";
    }

    private sealed class AllowAllBranches : IBranchAccessService
    {
        public Task<BranchAccessScope> GetScopeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(BranchAccessScope.UnrestrictedScope());

        public Task EnsureWarehouseAccessAsync(Guid warehouseId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task EnsureBranchAccessAsync(Guid branchId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<(Guid? WarehouseId, Guid[]? AllowedWarehouseIds)> ResolveWarehouseQueryAsync(
            Guid? requestedWarehouseId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(Guid?, Guid[]?)>((null, null));
    }

    private sealed class CountingOutbox : IIntegrationOutboxWriter
    {
        private int _calls;
        public int Calls => _calls;

        public Task WriteAsync(
            IDbConnection conn,
            IDbTransaction tx,
            string eventType,
            string aggregateType,
            Guid aggregateId,
            object data,
            Guid? actorUserId,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingPlatformEvents : IPlatformEventWriter
    {
        public Task WriteAsync(
            IDbConnection conn,
            IDbTransaction tx,
            string eventType,
            string aggregateType,
            Guid aggregateId,
            object data,
            Guid? actorUserId = null,
            Guid? correlationId = null,
            string source = PlatformEventSources.PharmacyPack,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WriteForTenantAsync(
            IDbConnection conn,
            IDbTransaction tx,
            Guid tenantId,
            string eventType,
            string aggregateType,
            Guid aggregateId,
            object data,
            Guid? actorUserId = null,
            Guid? correlationId = null,
            string source = PlatformEventSources.PharmacyPack,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PublishForTenantAsync(
            Guid tenantId,
            string eventType,
            string aggregateType,
            Guid aggregateId,
            object data,
            Guid? actorUserId = null,
            Guid? correlationId = null,
            string source = PlatformEventSources.PharmacyPack,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
