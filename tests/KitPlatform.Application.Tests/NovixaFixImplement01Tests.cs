using System.Text.Json;
using Dapper;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Reports;
using KitPlatform.Infrastructure.Dashboard;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Reports;
using KitPlatform.Infrastructure.Success;
using KitPlatform.Packs.Pharmacy.Infrastructure;
using KitPlatform.Packs.Pharmacy.Sales;
using Xunit;

namespace KitPlatform.Application.Tests;

public sealed class NovixaFixImplement01CalendarTests
{
    private static readonly DateTime SmokeUtc = new(2026, 10, 13, 3, 15, 0, DateTimeKind.Utc);

    [Fact]
    public void Today_range_is_vietnam_calendar_day_as_utc()
    {
        var (start, end) = VietnamBusinessCalendar.TodayRangeUtc(SmokeUtc);

        Assert.Equal(DateTimeKind.Utc, start.Kind);
        Assert.Equal(DateTimeKind.Utc, end.Kind);
        Assert.Equal(new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc), end);

        var eveningPrevious = new DateTime(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc);
        var beforeMidnight = new DateTime(2026, 10, 12, 16, 59, 0, DateTimeKind.Utc);
        var midnight = new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Utc);
        var morning = new DateTime(2026, 10, 13, 1, 0, 0, DateTimeKind.Utc);

        Assert.False(In(eveningPrevious, start, end));
        Assert.False(In(beforeMidnight, start, end));
        Assert.True(In(midnight, start, end));
        Assert.True(In(morning, start, end));
    }

    [Fact]
    public void Rolling_and_month_ranges_keep_utc_kind()
    {
        var (weekStart, weekEnd) = VietnamBusinessCalendar.RollingDaysRangeUtc(SmokeUtc, 7);
        var (monthStart, monthEnd) = VietnamBusinessCalendar.MonthToDateRangeUtc(SmokeUtc);

        Assert.Equal(DateTimeKind.Utc, weekStart.Kind);
        Assert.Equal(DateTimeKind.Utc, weekEnd.Kind);
        Assert.Equal(DateTimeKind.Utc, monthStart.Kind);
        Assert.Equal(DateTimeKind.Utc, monthEnd.Kind);
        Assert.Equal(new DateTime(2026, 10, 6, 17, 0, 0, DateTimeKind.Utc), weekStart);
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), monthStart);
        Assert.Equal(new DateTime(2026, 10, 31, 17, 0, 0, DateTimeKind.Utc), monthEnd);
    }

    [Fact]
    public void Report_default_range_is_utc_and_explicit_range_is_unchanged()
    {
        var (from, to) = ReportsDateHelper.ResolveRangeUtc(null, null, SmokeUtc);
        Assert.Equal(DateTimeKind.Utc, from.Kind);
        Assert.Equal(DateTimeKind.Utc, to.Kind);
        Assert.Equal(new DateTime(2026, 9, 30, 17, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Utc), to);

        var givenFrom = new DateTime(2026, 10, 12, 17, 0, 0, DateTimeKind.Unspecified);
        var givenTo = new DateTime(2026, 10, 13, 17, 0, 0, DateTimeKind.Unspecified);
        var (keptFrom, keptTo) = ReportsDateHelper.ResolveRangeUtc(givenFrom, givenTo, SmokeUtc);
        Assert.Equal(DateTimeKind.Unspecified, keptFrom.Kind);
        Assert.Equal(DateTimeKind.Unspecified, keptTo.Kind);
        Assert.Equal(givenFrom, keptFrom);
        Assert.Equal(givenTo, keptTo);
    }

    private static bool In(DateTime instant, DateTime start, DateTime end) =>
        instant >= start && instant < end;
}

[CollectionDefinition("NovixaFixImplement01", DisableParallelization = true)]
public sealed class NovixaFixImplement01Collection;

[Collection("NovixaFixImplement01")]
public sealed class NovixaFixImplement01DatabaseTests
{
    [Fact]
    public async Task Vietnam_session_shifts_unspecified_bounds_and_keeps_utc_bounds()
    {
        await using var conn = await OpenAsync();
        var zone = await conn.ExecuteScalarAsync<string>("SHOW TimeZone");
        Assert.Equal("Asia/Ho_Chi_Minh", zone);

        var (start, end) = VietnamBusinessCalendar.TodayRangeUtc(new DateTime(2026, 10, 13, 3, 15, 0, DateTimeKind.Utc));
        var evening = new DateTime(2026, 10, 12, 13, 0, 0, DateTimeKind.Utc);
        var lateToday = new DateTime(2026, 10, 13, 11, 0, 0, DateTimeKind.Utc);
        var unspecifiedStart = DateTime.SpecifyKind(start, DateTimeKind.Unspecified);
        var unspecifiedEnd = DateTime.SpecifyKind(end, DateTimeKind.Unspecified);

        var eveningInUnspecified = await InsideAsync(conn, evening, unspecifiedStart, unspecifiedEnd);
        var lateInUnspecified = await InsideAsync(conn, lateToday, unspecifiedStart, unspecifiedEnd);
        var eveningInUtc = await InsideAsync(conn, evening, start, end);
        var lateInUtc = await InsideAsync(conn, lateToday, start, end);

        Assert.True(eveningInUnspecified);
        Assert.False(lateInUnspecified);
        Assert.False(eveningInUtc);
        Assert.True(lateInUtc);
    }

    [Fact]
    public async Task Dashboard_order_list_and_revenue_share_the_vietnam_day_including_returns()
    {
        var tenantId = await CreateTenantAsync();
        try
        {
            var day = VietnamBusinessCalendar.Today(DateTime.UtcNow);
            var today = VietnamBusinessCalendar.TodayRangeUtc(DateTime.UtcNow);
            var month = VietnamBusinessCalendar.MonthToDateRangeUtc(DateTime.UtcNow);
            var week = VietnamBusinessCalendar.RollingDaysRangeUtc(DateTime.UtcNow, 7);

            var branchId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var unitId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            await using (var conn = await OpenAsync())
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO branches (id, tenant_id, branch_code, branch_name)
                    VALUES (@Id, @TenantId, 'BR', 'Chi nhánh')
                    """,
                    new { Id = branchId, TenantId = tenantId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type)
                    VALUES (@Id, @TenantId, @BranchId, 'WH', 'Kho', 1)
                    """,
                    new { Id = warehouseId, TenantId = tenantId, BranchId = branchId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO products (id, tenant_id, product_code, product_name)
                    VALUES (@Id, @TenantId, 'P1', 'Hang')
                    """,
                    new { Id = productId, TenantId = tenantId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO product_units (id, tenant_id, product_id, unit_name, is_base_unit, is_sale_unit)
                    VALUES (@Id, @TenantId, @ProductId, 'Hop', TRUE, TRUE)
                    """,
                    new { Id = unitId, TenantId = tenantId, ProductId = productId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO inventory_batches (
                        id, tenant_id, warehouse_id, product_id, batch_number, expiry_date,
                        unit_cost, quantity_received, quantity_available)
                    VALUES (
                        @Id, @TenantId, @WarehouseId, @ProductId, 'LOT', @Expiry,
                        1, 10, 10)
                    """,
                    new
                    {
                        Id = batchId,
                        TenantId = tenantId,
                        WarehouseId = warehouseId,
                        ProductId = productId,
                        Expiry = day.AddDays(400),
                    });
            }

            var eveningId = Guid.NewGuid();
            var beforeMidnightId = Guid.NewGuid();
            var midnightId = Guid.NewGuid();
            var morningId = Guid.NewGuid();
            var draftId = Guid.NewGuid();
            var otherDayId = Guid.NewGuid();
            var monthOutsideId = Guid.NewGuid();
            var monthInsideId = Guid.NewGuid();
            var otherDayAt = today.StartUtc.AddDays(-2);
            var returnAt = today.StartUtc.AddHours(3);
            const decimal morningTotal = 100_000m;
            const decimal checkoutPaid = 60_000m;
            const decimal refund = 20_000m;
            const decimal otherHeader = 30_000m;

            var orders = new List<SaleRow>
            {
                new(eveningId, today.StartUtc.AddHours(-4), 55_000m, 55_000m, SalesOrderStatuses.Completed),
                new(beforeMidnightId, today.StartUtc.AddMinutes(-1), 1_001m, 1_001m, SalesOrderStatuses.Completed),
                new(midnightId, today.StartUtc, 1_002m, 1_002m, SalesOrderStatuses.Completed),
                new(morningId, today.StartUtc.AddMinutes(1), morningTotal, morningTotal, SalesOrderStatuses.Completed),
                new(draftId, today.StartUtc.AddHours(2), 7_000m, 7_000m, SalesOrderStatuses.Draft),
                new(otherDayId, otherDayAt, otherHeader, otherHeader + refund, SalesOrderStatuses.Completed),
                new(monthOutsideId, month.StartUtc.AddMinutes(-1), 111m, 111m, SalesOrderStatuses.Completed),
                new(monthInsideId, month.StartUtc, 222m, 222m, SalesOrderStatuses.Completed),
            };

            await using (var conn = await OpenAsync())
            {
                foreach (var order in orders)
                {
                    await conn.ExecuteAsync(
                        """
                        INSERT INTO sales_orders (
                            id, tenant_id, order_number, branch_id, warehouse_id, order_date,
                            total_amount, amount_paid, outstanding, status)
                        VALUES (
                            @Id, @TenantId, @Number, @BranchId, @WarehouseId, @OrderDate,
                            @Total, 0, @Total, @Status)
                        """,
                        new
                        {
                            order.Id,
                            TenantId = tenantId,
                            Number = order.Id.ToString("N")[..12],
                            BranchId = branchId,
                            WarehouseId = warehouseId,
                            OrderDate = order.At,
                            Total = order.HeaderTotal,
                            order.Status,
                        });
                }

                var itemId = Guid.NewGuid();
                await conn.ExecuteAsync(
                    """
                    INSERT INTO sales_order_items (
                        id, sales_order_id, product_id, product_unit_id, batch_id,
                        quantity, unit_price, line_total)
                    VALUES (@Id, @OrderId, @ProductId, @UnitId, @BatchId, 1, @Refund, @Refund)
                    """,
                    new { Id = itemId, OrderId = otherDayId, ProductId = productId, UnitId = unitId, BatchId = batchId, Refund = refund });
                var returnId = Guid.NewGuid();
                await conn.ExecuteAsync(
                    """
                    INSERT INTO sales_returns (id, tenant_id, return_number, sales_order_id, return_date, status)
                    VALUES (@Id, @TenantId, 'RT1', @OrderId, @ReturnDate, @Status)
                    """,
                    new
                    {
                        Id = returnId,
                        TenantId = tenantId,
                        OrderId = otherDayId,
                        ReturnDate = returnAt,
                        Status = SalesReturnStatuses.Completed,
                    });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO sales_return_items (
                        sales_return_id, sales_order_item_id, batch_id, quantity, refund_amount)
                    VALUES (@ReturnId, @ItemId, @BatchId, 1, @Refund)
                    """,
                    new { ReturnId = returnId, ItemId = itemId, BatchId = batchId, Refund = refund });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO sales_payments (sales_order_id, payment_method, amount, paid_at)
                    VALUES (@OrderId, @Method, @Amount, @PaidAt)
                    """,
                    new
                    {
                        OrderId = morningId,
                        Method = SalesPaymentMethods.Cash,
                        Amount = checkoutPaid,
                        PaidAt = today.StartUtc.AddMinutes(1),
                    });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO sales_return_payments (sales_return_id, payment_method, amount, paid_at)
                    VALUES (@ReturnId, @Method, @Amount, @PaidAt)
                    """,
                    new
                    {
                        ReturnId = returnId,
                        Method = SalesPaymentMethods.Cash,
                        Amount = refund,
                        PaidAt = returnAt,
                    });
            }

            Assert.Equal(day, VietnamBusinessCalendar.Today(DateTime.UtcNow));
            Assert.Equal(today.StartUtc, VietnamBusinessCalendar.TodayRangeUtc(DateTime.UtcNow).StartUtc);

            var db = new NpgsqlConnectionFactory(ConnectionString());
            var tenant = new FixedTenant(tenantId);
            var dashboard = new DashboardRepository(db, tenant);
            var reports = new ReportsRepository(db, tenant);
            var sales = new SalesRepository(db, tenant, new InventoryRepository(db, tenant), null!, null!, null!, null!, null!, null!, null!, null!);
            var cockpit = new OwnerCockpitRepository(db, tenant);

            var overview = await dashboard.GetOverviewAsync(30, 10, null, CancellationToken.None);
            var revenue = await reports.GetSalesRevenueByPeriodAsync(
                today.StartUtc, today.EndUtc, ReportGroupBy.Day, null, null, CancellationToken.None);
            var list = await sales.GetSalesOrdersAsync(
                new SalesOrderListFilter(From: today.StartUtc, To: today.EndUtc, Page: 1, PageSize: 50),
                null,
                CancellationToken.None);
            var extras = await cockpit.GetExtrasAsync(30, 30, 7, 7, null, CancellationToken.None);

            var soldToday = orders.Where(o =>
                (o.Status is SalesOrderStatuses.Completed or SalesOrderStatuses.Refunded)
                && In(o.At, today.StartUtc, today.EndUtc)).ToList();
            var draftsToday = orders.Count(o => o.Status == SalesOrderStatuses.Draft && In(o.At, today.StartUtc, today.EndUtc));
            var expectedSales = soldToday.Sum(o => o.OriginalTotal);
            var expectedNet = expectedSales - refund;
            var expectedCollected = checkoutPaid - refund;

            Assert.Equal(soldToday.Count, overview.Sales.TodayOrderCount);
            Assert.Equal(expectedNet, overview.Sales.TodayNetTotal);
            Assert.Equal(expectedCollected, overview.Sales.TodayCollected);
            Assert.NotEqual(expectedSales, overview.Sales.TodayCollected);
            Assert.DoesNotContain(soldToday, o => o.Id == eveningId || o.Id == beforeMidnightId);
            Assert.Contains(soldToday, o => o.Id == midnightId);
            Assert.Contains(soldToday, o => o.Id == morningId);

            var row = Assert.Single(revenue);
            Assert.Equal(day.ToString("dd/MM/yyyy"), row["periodLabel"]);
            Assert.Equal(soldToday.Count, Convert.ToInt32(row["orderCount"]));
            Assert.Equal(expectedSales, Money(row["salesAmount"]));
            Assert.Equal(refund, Money(row["refundAmount"]));
            Assert.Equal(expectedNet, Money(row["netAmount"]));
            Assert.Equal(overview.Sales.TodayNetTotal, Money(row["netAmount"]));
            Assert.Equal(refund, Money(row["salesAmount"]) - Money(row["netAmount"]));

            Assert.Equal(soldToday.Count + draftsToday, list.Total);
            Assert.Contains(list.Items, item => item.Id == draftId);
            Assert.Contains(list.Items, item => item.Id == midnightId);
            Assert.DoesNotContain(list.Items, item => item.Id == eveningId || item.Id == beforeMidnightId);

            var monthSold = orders.Where(o =>
                (o.Status is SalesOrderStatuses.Completed or SalesOrderStatuses.Refunded)
                && In(o.At, month.StartUtc, month.EndUtc)).ToList();
            var weekSold = orders.Where(o =>
                (o.Status is SalesOrderStatuses.Completed or SalesOrderStatuses.Refunded)
                && In(o.At, week.StartUtc, week.EndUtc)).ToList();
            var monthNet = monthSold.Sum(o => o.OriginalTotal) - (In(returnAt, month.StartUtc, month.EndUtc) ? refund : 0m);
            Assert.Equal(monthSold.Count, extras.Sales.MonthOrderCount);
            Assert.Equal(weekSold.Count, extras.Sales.WeekOrderCount);
            Assert.Equal(monthNet, extras.Sales.MonthNetTotal);
            Assert.DoesNotContain(monthSold, o => o.Id == monthOutsideId);
            Assert.Contains(monthSold, o => o.Id == monthInsideId);
        }
        finally
        {
            await DeleteTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Near_expiry_keeps_today_through_cutoff_and_drops_expired_lots()
    {
        var tenantId = await CreateTenantAsync();
        try
        {
            var today = VietnamBusinessCalendar.Today(DateTime.UtcNow);
            var smokeMarch = new DateOnly(2026, 3, 31);
            var smokeJuly = new DateOnly(2026, 7, 31);
            Assert.True(smokeMarch < today);
            Assert.True(smokeJuly < today);

            var branchId = Guid.NewGuid();
            var warehouseId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var lots = new (string Number, DateOnly? Expiry, decimal Qty)[]
            {
                ("YDAY", today.AddDays(-1), 5m),
                ("TODAY", today, 4m),
                ("WITHIN", today.AddDays(10), 3m),
                ("CUTOFF", today.AddDays(30), 2m),
                ("AFTER", today.AddDays(31), 6m),
                ("ZERO", today.AddDays(5), 0m),
                ("MAR", smokeMarch, 1m),
                ("JUL", smokeJuly, 1m),
            };

            await using (var conn = await OpenAsync())
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO branches (id, tenant_id, branch_code, branch_name)
                    VALUES (@Id, @TenantId, 'BR', 'Chi nhánh')
                    """,
                    new { Id = branchId, TenantId = tenantId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type)
                    VALUES (@Id, @TenantId, @BranchId, 'WH', 'Kho', 1)
                    """,
                    new { Id = warehouseId, TenantId = tenantId, BranchId = branchId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO products (id, tenant_id, product_code, product_name)
                    VALUES (@Id, @TenantId, 'P1', 'Hang')
                    """,
                    new { Id = productId, TenantId = tenantId });
                foreach (var lot in lots)
                {
                    await conn.ExecuteAsync(
                        """
                        INSERT INTO inventory_batches (
                            id, tenant_id, warehouse_id, product_id, batch_number, expiry_date,
                            unit_cost, quantity_received, quantity_available)
                        VALUES (
                            @Id, @TenantId, @WarehouseId, @ProductId, @Number, @Expiry,
                            1, @Qty, @Qty)
                        """,
                        new
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            WarehouseId = warehouseId,
                            ProductId = productId,
                            lot.Number,
                            lot.Expiry,
                            lot.Qty,
                        });
                }
            }

            var db = new NpgsqlConnectionFactory(ConnectionString());
            var tenant = new FixedTenant(tenantId);
            var dashboard = new DashboardRepository(db, tenant);
            var reports = new ReportsRepository(db, tenant);
            var inventory = new InventoryRepository(db, tenant);

            var overview = await dashboard.GetOverviewAsync(30, 10, null, CancellationToken.None);
            var rows = await reports.GetInventoryNearExpiryAsync(
                null, null, today, today.AddDays(30), CancellationToken.None);
            var numbers = rows.Select(row => (string)row["batchNumber"]!).OrderBy(n => n).ToArray();

            Assert.Equal(new[] { "CUTOFF", "TODAY", "WITHIN" }, numbers);
            Assert.Equal(3, overview.Inventory.NearExpiryBatchCount);
            Assert.DoesNotContain("YDAY", numbers);
            Assert.DoesNotContain("AFTER", numbers);
            Assert.DoesNotContain("ZERO", numbers);
            Assert.DoesNotContain("MAR", numbers);
            Assert.DoesNotContain("JUL", numbers);

            var expired = await inventory.GetStockBatchesAsync(null, null, null, null, "expired", 1, 20, CancellationToken.None);
            var expiredNumbers = expired.Items.Select(item => item.BatchNumber).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "JUL", "MAR", "YDAY" }, expiredNumbers);
            Assert.Equal(3, expired.Total);
            Assert.DoesNotContain(expired.Items, item => item.BatchNumber == "TODAY");
        }
        finally
        {
            await DeleteTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Low_stock_total_pages_under_exact_and_over_500()
    {
        var tenantId = await CreateTenantAsync();
        try
        {
            var branchId = Guid.NewGuid();
            var warehouseA = Guid.NewGuid();
            var warehouseB = Guid.NewGuid();
            var bareProductId = Guid.NewGuid();
            await using (var conn = await OpenAsync())
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO branches (id, tenant_id, branch_code, branch_name)
                    VALUES (@Id, @TenantId, 'BR', 'Chi nhánh')
                    """,
                    new { Id = branchId, TenantId = tenantId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type)
                    VALUES (@Id, @TenantId, @BranchId, @Code, @Name, 1)
                    """,
                    new[]
                    {
                        new { Id = warehouseA, TenantId = tenantId, BranchId = branchId, Code = "A", Name = "A-Kho" },
                        new { Id = warehouseB, TenantId = tenantId, BranchId = branchId, Code = "B", Name = "B-Kho" },
                    });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO products (id, tenant_id, product_code, product_name)
                    VALUES (@Id, @TenantId, 'BARE', 'Khong lo')
                    """,
                    new { Id = bareProductId, TenantId = tenantId });
                await InsertProductsAsync(conn, tenantId, 199, "U");
            }

            var db = new NpgsqlConnectionFactory(ConnectionString());
            var tenant = new FixedTenant(tenantId);
            var inventory = new InventoryRepository(db, tenant);
            var service = new InventoryService(inventory, tenant, null!, new UnrestrictedBranchAccess());
            var dashboard = new DashboardRepository(db, tenant);

            await AssertPagesAsync(inventory, 400, 20);
            await using (var conn = await OpenAsync())
                await InsertProductsAsync(conn, tenantId, 50, "E");
            await AssertPagesAsync(inventory, 500, 100);
            await using (var conn = await OpenAsync())
                await InsertProductsAsync(conn, tenantId, 1, "O");
            var over = await AssertPagesAsync(inventory, 502, 100);
            Assert.Contains(over, key => key.StartsWith(bareProductId.ToString("D"), StringComparison.OrdinalIgnoreCase));

            var barePage = await inventory.GetLowStockProductsAsync(null, null, 10, 1, 100, CancellationToken.None);
            Assert.NotEqual(barePage.Total, barePage.Items.Count);

            await using (var conn = await OpenAsync())
            {
                await InsertStockedProductAsync(conn, tenantId, warehouseA, warehouseB, "HIGH", 100m);
                await InsertStockedProductAsync(conn, tenantId, warehouseA, warehouseB, "LOW3", 3m);
            }

            var all = await inventory.GetLowStockProductsAsync(null, null, 10, 1, 100, CancellationToken.None);
            var oneWarehouse = await inventory.GetLowStockProductsAsync(warehouseA, null, 10, 1, 100, CancellationToken.None);
            var zeroThreshold = await inventory.GetLowStockProductsAsync(null, null, 0, 1, 100, CancellationToken.None);
            var overview = await dashboard.GetOverviewAsync(30, 10, null, CancellationToken.None);
            var overviewZero = await dashboard.GetOverviewAsync(30, 0, null, CancellationToken.None);
            var clamped = await service.GetLowStockProductsAsync(null, 10, 1, 500, CancellationToken.None);

            Assert.Equal(504, all.Total);
            Assert.Equal(252, oneWarehouse.Total);
            Assert.True(oneWarehouse.Total < all.Total);
            Assert.Equal(all.Total, overview.Inventory.LowStockProductCount);
            Assert.Equal(502, zeroThreshold.Total);
            Assert.Equal(zeroThreshold.Total, overviewZero.Inventory.LowStockProductCount);
            Assert.NotEqual(overview.Inventory.LowStockProductCount, overviewZero.Inventory.LowStockProductCount);
            Assert.Equal(100, clamped.PageSize);
            Assert.Equal(100, clamped.Items.Count);
            Assert.Equal(504, clamped.Total);
            Assert.NotEqual(clamped.Total, clamped.Items.Count);

            var walked = await AssertPagesAsync(inventory, 504, 100);
            Assert.Contains(walked, key => key.Contains(bareProductId.ToString("D"), StringComparison.OrdinalIgnoreCase));
            var bare = await FindAsync(inventory, bareProductId);
            Assert.Equal(0m, bare.TotalQuantity);
            Assert.Equal(0, bare.BatchCount);
        }
        finally
        {
            await DeleteTenantAsync(tenantId);
        }
    }

    [Fact]
    public async Task Low_stock_tied_sort_keys_cover_every_page_without_duplicate_or_loss()
    {
        var tenantId = await CreateTenantAsync();
        try
        {
            var branchId = Guid.NewGuid();
            var warehouseIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var tiedProductIds = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
            var laterProductId = Guid.NewGuid();
            const string tiedWarehouseName = "Kho trung";
            const string tiedProductName = "San pham trung";
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var productId in tiedProductIds.Append(laterProductId))
            {
                foreach (var warehouseId in warehouseIds)
                    expected.Add($"{productId:D}|{warehouseId:D}");
            }

            await using (var conn = await OpenAsync())
            {
                await conn.ExecuteAsync(
                    """
                    INSERT INTO branches (id, tenant_id, branch_code, branch_name)
                    VALUES (@Id, @TenantId, 'BR', 'Chi nhánh')
                    """,
                    new { Id = branchId, TenantId = tenantId });
                await conn.ExecuteAsync(
                    """
                    INSERT INTO warehouses (id, tenant_id, branch_id, warehouse_code, warehouse_name, warehouse_type)
                    VALUES (@Id, @TenantId, @BranchId, @Code, @Name, 1)
                    """,
                    warehouseIds.Select((id, index) => new
                    {
                        Id = id,
                        TenantId = tenantId,
                        BranchId = branchId,
                        Code = "K" + (index + 1),
                        Name = tiedWarehouseName,
                    }));
                await conn.ExecuteAsync(
                    """
                    INSERT INTO products (id, tenant_id, product_code, product_name)
                    VALUES (@Id, @TenantId, @Code, @Name)
                    """,
                    tiedProductIds.Select((id, index) => new
                    {
                        Id = id,
                        TenantId = tenantId,
                        Code = "T" + (index + 1).ToString("00"),
                        Name = tiedProductName,
                    }));
                await conn.ExecuteAsync(
                    """
                    INSERT INTO products (id, tenant_id, product_code, product_name)
                    VALUES (@Id, @TenantId, 'LATER', 'Sau')
                    """,
                    new { Id = laterProductId, TenantId = tenantId });
                foreach (var warehouseId in warehouseIds)
                {
                    await conn.ExecuteAsync(
                        """
                        INSERT INTO inventory_batches (
                            id, tenant_id, warehouse_id, product_id, batch_number,
                            unit_cost, quantity_received, quantity_available)
                        VALUES (@Id, @TenantId, @WarehouseId, @ProductId, 'LOT', 1, 5, 5)
                        """,
                        new
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            WarehouseId = warehouseId,
                            ProductId = laterProductId,
                        });
                }
            }

            var inventory = new InventoryRepository(
                new NpgsqlConnectionFactory(ConnectionString()),
                new FixedTenant(tenantId));
            const int pageSize = 5;
            var first = await WalkLowStockAsync(inventory, pageSize);
            var second = await WalkLowStockAsync(inventory, pageSize);

            Assert.Equal(expected.Count, first.Count);
            Assert.Equal(expected, first.Select(row => row.Key).ToHashSet(StringComparer.OrdinalIgnoreCase));
            Assert.Equal(first.Select(row => row.Key), second.Select(row => row.Key));
            Assert.Equal(24, first.Count(row => row.TotalQuantity == 0m));
            Assert.Equal(3, first.Count(row => row.TotalQuantity == 5m));
            Assert.All(first.Take(24), row =>
            {
                Assert.Equal(0m, row.TotalQuantity);
                Assert.Equal(tiedWarehouseName, row.WarehouseName);
                Assert.Equal(tiedProductName, row.ProductName);
            });
            Assert.All(first.Skip(24), row => Assert.Equal(5m, row.TotalQuantity));
        }
        finally
        {
            await DeleteTenantAsync(tenantId);
        }
    }

    private static async Task<List<LowStockPageRow>> WalkLowStockAsync(InventoryRepository inventory, int pageSize)
    {
        var rows = new List<LowStockPageRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var page = 1;
        while (true)
        {
            var result = await inventory.GetLowStockProductsAsync(null, null, 10, page, pageSize, CancellationToken.None);
            Assert.Equal(27, result.Total);
            Assert.NotEqual(result.Total, result.Items.Count);
            if (result.Items.Count == 0)
                break;
            Assert.True(result.Items.Count <= pageSize);
            foreach (var item in result.Items)
            {
                var key = $"{item.ProductId:D}|{item.WarehouseId:D}";
                Assert.True(seen.Add(key));
                rows.Add(new LowStockPageRow(key, item.TotalQuantity, item.WarehouseName, item.ProductName));
            }

            if (rows.Count >= result.Total)
                break;
            page++;
        }

        Assert.Equal(27, rows.Count);
        Assert.Equal((27 + pageSize - 1) / pageSize, page);
        return rows;
    }

    private static async Task<HashSet<string>> AssertPagesAsync(InventoryRepository inventory, int expectedTotal, int pageSize)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var page = 1;
        var collected = 0;
        while (true)
        {
            var result = await inventory.GetLowStockProductsAsync(null, null, 10, page, pageSize, CancellationToken.None);
            Assert.Equal(expectedTotal, result.Total);
            Assert.NotEqual(result.Total, result.Items.Count);
            Assert.True(result.Items.Count <= pageSize);
            if (result.Items.Count == 0)
                break;
            foreach (var item in result.Items)
            {
                var key = $"{item.ProductId:D}|{item.WarehouseId:D}";
                Assert.True(seen.Add(key));
            }
            collected += result.Items.Count;
            if (collected >= result.Total)
                break;
            page++;
        }

        Assert.Equal(expectedTotal, seen.Count);
        Assert.Equal((expectedTotal + pageSize - 1) / pageSize, page);
        return seen;
    }

    private static async Task<LowStockRow> FindAsync(InventoryRepository inventory, Guid productId)
    {
        var page = 1;
        while (true)
        {
            var result = await inventory.GetLowStockProductsAsync(null, null, 10, page, 100, CancellationToken.None);
            var match = result.Items.FirstOrDefault(item => item.ProductId == productId);
            if (match is not null)
                return new LowStockRow(match.TotalQuantity, match.BatchCount);
            if (page * 100 >= result.Total)
                throw new InvalidOperationException("Bare product was missing from low stock.");
            page++;
        }
    }

    private static async Task InsertProductsAsync(Npgsql.NpgsqlConnection conn, Guid tenantId, int count, string prefix)
    {
        await conn.ExecuteAsync(
            """
            INSERT INTO products (id, tenant_id, product_code, product_name)
            SELECT gen_random_uuid(), @TenantId, @Prefix || lpad(g::text, 4, '0'), @Prefix || g::text
            FROM generate_series(1, @Count) g
            """,
            new { TenantId = tenantId, Prefix = prefix, Count = count });
    }

    private static async Task InsertStockedProductAsync(
        Npgsql.NpgsqlConnection conn,
        Guid tenantId,
        Guid warehouseA,
        Guid warehouseB,
        string code,
        decimal quantity)
    {
        var productId = Guid.NewGuid();
        await conn.ExecuteAsync(
            """
            INSERT INTO products (id, tenant_id, product_code, product_name)
            VALUES (@Id, @TenantId, @Code, @Code)
            """,
            new { Id = productId, TenantId = tenantId, Code = code });
        foreach (var warehouseId in new[] { warehouseA, warehouseB })
        {
            await conn.ExecuteAsync(
                """
                INSERT INTO inventory_batches (
                    id, tenant_id, warehouse_id, product_id, batch_number,
                    unit_cost, quantity_received, quantity_available)
                VALUES (@Id, @TenantId, @WarehouseId, @ProductId, 'LOT', 1, @Qty, @Qty)
                """,
                new
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    WarehouseId = warehouseId,
                    ProductId = productId,
                    Qty = quantity,
                });
        }
    }

    private static async Task<bool> InsideAsync(
        Npgsql.NpgsqlConnection conn,
        DateTime instant,
        DateTime start,
        DateTime end)
    {
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT @Instant >= @Start AND @Instant < @End
            """,
            new { Instant = instant, Start = start, End = end });
    }

    private static async Task<Guid> CreateTenantAsync()
    {
        var id = Guid.NewGuid();
        var code = "FIXIMPL01_" + id.ToString("N")[..12];
        await using var conn = await OpenAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO tenants (id, tenant_code, tenant_name)
            VALUES (@Id, @Code, 'NOVIXA-FIX-IMPLEMENT-01')
            """,
            new { Id = id, Code = code });
        return id;
    }

    private static async Task DeleteTenantAsync(Guid tenantId)
    {
        await using var conn = await OpenAsync();
        var code = await conn.ExecuteScalarAsync<string?>(
            "SELECT tenant_code FROM tenants WHERE id = @Id",
            new { Id = tenantId });
        if (code is null)
            return;
        if (!code.StartsWith("FIXIMPL01_", StringComparison.Ordinal)
            || code is "NT_XUANHOA" or "DEMO_PHARMACY")
            throw new InvalidOperationException($"Refusing to delete tenant {code}.");

        var deletes = new[]
        {
            "DELETE FROM sales_return_payments WHERE sales_return_id IN (SELECT id FROM sales_returns WHERE tenant_id = @Id)",
            "DELETE FROM sales_return_items WHERE sales_return_id IN (SELECT id FROM sales_returns WHERE tenant_id = @Id)",
            "DELETE FROM sales_returns WHERE tenant_id = @Id",
            "DELETE FROM sales_payments WHERE sales_order_id IN (SELECT id FROM sales_orders WHERE tenant_id = @Id)",
            "DELETE FROM sales_order_items WHERE sales_order_id IN (SELECT id FROM sales_orders WHERE tenant_id = @Id)",
            "DELETE FROM sales_orders WHERE tenant_id = @Id",
            "DELETE FROM inventory_batches WHERE tenant_id = @Id",
            "DELETE FROM product_units WHERE tenant_id = @Id",
            "DELETE FROM products WHERE tenant_id = @Id",
            "DELETE FROM warehouses WHERE tenant_id = @Id",
            "DELETE FROM branches WHERE tenant_id = @Id",
            "DELETE FROM tenants WHERE id = @Id AND tenant_code LIKE 'FIXIMPL01_%'",
        };
        await using var tx = await conn.BeginTransactionAsync();
        var removed = 0;
        for (var i = 0; i < deletes.Length; i++)
        {
            var affected = await conn.ExecuteAsync(deletes[i], new { Id = tenantId }, tx);
            if (i == deletes.Length - 1)
                removed = affected;
        }
        await tx.CommitAsync();
        if (removed != 1)
            throw new InvalidOperationException($"Test tenant {code} was not removed.");
    }

    private static async Task<Npgsql.NpgsqlConnection> OpenAsync()
    {
        DapperTypeHandlers.EnsureRegistered();
        var conn = new Npgsql.NpgsqlConnection(ConnectionString());
        await conn.OpenAsync();
        return conn;
    }

    private static string ConnectionString()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, "src", "KitPlatform.Api", "appsettings.Development.json");
            if (File.Exists(path))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var cs = doc.RootElement.GetProperty("ConnectionStrings").GetProperty("Default").GetString()
                    ?? throw new InvalidOperationException("Missing development connection string.");
                return cs.Contains("Timezone=", StringComparison.OrdinalIgnoreCase)
                    ? cs
                    : cs.TrimEnd(';') + ";Timezone=Asia/Ho_Chi_Minh";
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("appsettings.Development.json was not found.");
    }

    private static bool In(DateTime instant, DateTime start, DateTime end) =>
        instant >= start && instant < end;

    private static decimal Money(object? value) => Convert.ToDecimal(value);

    private sealed record SaleRow(Guid Id, DateTime At, decimal HeaderTotal, decimal OriginalTotal, short Status);

    private sealed record LowStockRow(decimal TotalQuantity, int BatchCount);

    private sealed record LowStockPageRow(string Key, decimal TotalQuantity, string WarehouseName, string ProductName);

    private sealed class FixedTenant(Guid tenantId) : ITenantContext
    {
        public Guid TenantId { get; } = tenantId;
        public Guid UserId => Guid.Empty;
        public bool IsAuthenticated => true;
        public Guid? WorkspaceId => null;
    }

    private sealed class UnrestrictedBranchAccess : IBranchAccessService
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
            Task.FromResult<(Guid?, Guid[]?)>((requestedWarehouseId, null));
    }
}
