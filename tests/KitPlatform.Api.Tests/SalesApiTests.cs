using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class SalesApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private readonly KitPlatformWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly Guid DemoCustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    private static readonly Guid ParaProductId = Guid.Parse("66666666-6666-6666-6666-666666666601");
    private static readonly Guid ParaUnitId = Guid.Parse("77777777-7777-7777-7777-777777777701");

    public SalesApiTests(KitPlatformWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Sales_create_order_decreases_stock_via_fefo()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehouseId = await GetMainWarehouseIdAsync(authed);
        await OpenShiftForWarehouseAsync(authed, warehouseId);
        var stockBefore = await GetProductStockAsync(authed, warehouseId, ParaProductId);

        var response = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            customerId = DemoCustomerId,
            priceType = 1,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, order.GetProperty("status").GetInt16());
        Assert.True(order.GetProperty("totalAmount").GetDecimal() > 0);

        var stockAfter = await GetProductStockAsync(authed, warehouseId, ParaProductId);
        Assert.True(stockAfter < stockBefore);
    }

    [Fact]
    public async Task Sales_barcode_lookup_returns_price()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);

        var response = await authed.GetAsync(
            $"/api/sales/pos/lookup?barcode=8934567890012&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ParaProductId, item.GetProperty("productId").GetGuid());
        Assert.Equal(500m, item.GetProperty("unitPrice").GetDecimal());
    }

    [Fact]
    public async Task Sales_barcode_lookup_includes_fefo_batch_hints()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);

        var response = await authed.GetAsync(
            $"/api/sales/pos/lookup?barcode=8934567890012&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Tồn theo hệ thống", item.GetProperty("stockSourceLabel").GetString());
        var hints = item.GetProperty("batchHints");
        Assert.Equal(JsonValueKind.Array, hints.ValueKind);
        Assert.True(hints.GetArrayLength() > 0);
        Assert.True(hints[0].GetProperty("isSuggested").GetBoolean());
        Assert.Equal("LOT2026A", hints[0].GetProperty("batchNumber").GetString());
    }

    [Fact]
    public async Task Sales_pos_preview_allocation_matches_fefo()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);

        var response = await authed.PostAsJsonAsync("/api/sales/pos/preview-allocation", new
        {
            warehouseId,
            items = new[]
            {
                new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m },
            },
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        var lines = preview.GetProperty("lines");
        Assert.Equal(1, lines.GetArrayLength());
        var allocations = lines[0].GetProperty("allocations");
        Assert.Equal(1, allocations.GetArrayLength());
        Assert.Equal("LOT2026A", allocations[0].GetProperty("batchNumber").GetString());
    }

    [Fact]
    public async Task Sales_pos_barcode_lookup_then_complete_sale()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        await OpenShiftForWarehouseAsync(authed, warehouseId);
        const string barcode = "8934567890012";

        var lookupResponse = await authed.GetAsync(
            $"/api/sales/pos/lookup?barcode={barcode}&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        var lookup = await lookupResponse.Content.ReadFromJsonAsync<JsonElement>();
        var productId = lookup.GetProperty("productId").GetGuid();
        var productUnitId = lookup.GetProperty("productUnitId").GetGuid();
        var unitPrice = lookup.GetProperty("unitPrice").GetDecimal();
        Assert.True(lookup.GetProperty("stockAvailable").GetDecimal() > 0);

        var stockBefore = await GetProductStockAsync(authed, warehouseId, productId);

        var createResponse = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            priceType = 1,
            items = new[] { new { productId, productUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var order = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetGuid();
        Assert.Equal(2, order.GetProperty("status").GetInt16());
        Assert.Equal(unitPrice, order.GetProperty("totalAmount").GetDecimal());

        var listResponse = await authed.GetAsync("/api/sales/orders");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            list.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == orderId);

        var stockAfter = await GetProductStockAsync(authed, warehouseId, productId);
        Assert.Equal(stockBefore - 1m, stockAfter);
    }

    [Fact]
    public async Task Sales_draft_complete_and_return_restores_stock()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        await OpenShiftForWarehouseAsync(authed, warehouseId);
        var stockBefore = await GetProductStockAsync(authed, warehouseId, ParaProductId);

        var draftResponse = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            priceType = 1,
            saveAsDraft = true,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
        var draftId = draft.GetProperty("id").GetGuid();
        Assert.Equal(1, draft.GetProperty("status").GetInt16());

        var stockAfterDraft = await GetProductStockAsync(authed, warehouseId, ParaProductId);
        Assert.Equal(stockBefore, stockAfterDraft);

        var completeResponse = await authed.PostAsJsonAsync($"/api/sales/orders/{draftId}/complete", new { });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, completed.GetProperty("status").GetInt16());
        var itemId = completed.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var stockAfterSale = await GetProductStockAsync(authed, warehouseId, ParaProductId);
        Assert.Equal(stockBefore - 1m, stockAfterSale);

        var returnResponse = await authed.PostAsJsonAsync($"/api/sales/orders/{draftId}/returns", new
        {
            reason = "Test return",
            items = new[] { new { salesOrderItemId = itemId, quantity = 1m } },
            payments = new[] { new { paymentMethod = 1, amount = 500m } },
        });
        Assert.Equal(HttpStatusCode.Created, returnResponse.StatusCode);
        var ret = await returnResponse.Content.ReadFromJsonAsync<JsonElement>();
        var returnId = ret.GetProperty("id").GetGuid();

        var orderReturns = await authed.GetFromJsonAsync<JsonElement>($"/api/sales/orders/{draftId}/returns");
        Assert.Single(orderReturns!.EnumerateArray());
        Assert.Equal(returnId, orderReturns.EnumerateArray().First().GetProperty("id").GetGuid());

        var allReturns = await authed.GetFromJsonAsync<JsonElement>("/api/sales/returns?search=SR");
        Assert.Contains(allReturns!.EnumerateArray(), row => row.GetProperty("id").GetGuid() == returnId);

        var stockAfterReturn = await GetProductStockAsync(authed, warehouseId, ParaProductId);
        Assert.Equal(stockBefore, stockAfterReturn);

        var orderAfterReturn = await authed.GetFromJsonAsync<JsonElement>($"/api/sales/orders/{draftId}");
        Assert.Equal(4, orderAfterReturn!.GetProperty("status").GetInt16());
        Assert.Equal(500m, orderAfterReturn.GetProperty("totalRefunded").GetDecimal());
        var refundPayments = orderAfterReturn.GetProperty("refundPayments").EnumerateArray().ToList();
        Assert.Single(refundPayments);
        Assert.Equal(1, refundPayments[0].GetProperty("paymentMethod").GetInt16());
        Assert.Equal(500m, refundPayments[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Sales_line_and_order_discount_reduce_total()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        await OpenShiftForWarehouseAsync(authed, warehouseId);

        var response = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            priceType = 1,
            orderDiscountType = 1,
            orderDiscountValue = 10m,
            items = new[]
            {
                new
                {
                    productId = ParaProductId,
                    productUnitId = ParaUnitId,
                    quantity = 2m,
                    discountType = 1,
                    discountValue = 5m,
                },
            },
            payments = new[] { new { paymentMethod = 1, amount = 855m } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, order.GetProperty("status").GetInt16());
        Assert.Equal(1000m, order.GetProperty("subtotal").GetDecimal());
        Assert.Equal(855m, order.GetProperty("totalAmount").GetDecimal());
    }

    [Fact]
    public async Task Sales_return_with_discount_refunds_net_amount()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        await OpenShiftForWarehouseAsync(authed, warehouseId);

        var saleResponse = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            priceType = 1,
            orderDiscountType = 1,
            orderDiscountValue = 10m,
            items = new[]
            {
                new
                {
                    productId = ParaProductId,
                    productUnitId = ParaUnitId,
                    quantity = 2m,
                    discountType = 1,
                    discountValue = 5m,
                },
            },
            payments = new[] { new { paymentMethod = 1, amount = 855m } },
        });
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);
        var order = await saleResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = order.GetProperty("id").GetGuid();
        var itemId = order.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var returnResponse = await authed.PostAsJsonAsync($"/api/sales/orders/{orderId}/returns", new
        {
            items = new[] { new { salesOrderItemId = itemId, quantity = 1m } },
            payments = new[] { new { paymentMethod = 1, amount = 428m } },
        });
        Assert.Equal(HttpStatusCode.Created, returnResponse.StatusCode);
        var ret = await returnResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(428m, ret.GetProperty("totalRefund").GetDecimal());
        Assert.Equal(428m, ret.GetProperty("payments").EnumerateArray().First().GetProperty("amount").GetDecimal());

        var shiftResponse = await authed.GetAsync(
            $"/api/sales/shift-summary?from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow.AddDays(1):O}");
        Assert.Equal(HttpStatusCode.OK, shiftResponse.StatusCode);
        var shift = await shiftResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(shift.GetProperty("totalRefunds").GetDecimal() >= 428m);

        var orderAfterReturn = await authed.GetFromJsonAsync<JsonElement>($"/api/sales/orders/{orderId}");
        Assert.Equal(428m, orderAfterReturn!.GetProperty("totalRefunded").GetDecimal());
        var refundPayments = orderAfterReturn.GetProperty("refundPayments").EnumerateArray().ToList();
        Assert.Single(refundPayments);
        Assert.Equal(1, refundPayments[0].GetProperty("paymentMethod").GetInt16());
        Assert.Equal(428m, refundPayments[0].GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task Sales_draft_update_then_complete()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        await OpenShiftForWarehouseAsync(authed, warehouseId);

        var draftResponse = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            priceType = 1,
            saveAsDraft = true,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
        var draftId = draft.GetProperty("id").GetGuid();
        var orderNumber = draft.GetProperty("orderNumber").GetString();

        var updateResponse = await authed.PutAsJsonAsync($"/api/sales/orders/{draftId}", new
        {
            priceType = 1,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 2m } },
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderNumber, updated.GetProperty("orderNumber").GetString());
        Assert.Equal(1000m, updated.GetProperty("totalAmount").GetDecimal());

        var completeResponse = await authed.PostAsJsonAsync($"/api/sales/orders/{draftId}/complete", new
        {
            payments = new[] { new { paymentMethod = 1, amount = 1000m } },
        });
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, completed.GetProperty("status").GetInt16());
    }

    [Fact]
    public async Task Sales_shift_open_close_and_blocks_sale_without_shift()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetWarehouseWithoutOpenShiftAsync(authed);

        var saleWithoutShift = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            priceType = 1,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, saleWithoutShift.StatusCode);

        var openResponse = await authed.PostAsJsonAsync("/api/sales/shifts/open", new
        {
            warehouseId,
            openingCash = 100_000m,
        });
        Assert.True(
            openResponse.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"Unexpected open status: {openResponse.StatusCode}");
        var opened = await openResponse.Content.ReadFromJsonAsync<JsonElement>();
        var shiftId = opened.GetProperty("id").GetGuid();
        Assert.Equal(100_000m, opened.GetProperty("openingCash").GetDecimal());
        Assert.Equal(1, opened.GetProperty("status").GetInt16());

        var openAgain = await authed.PostAsJsonAsync("/api/sales/shifts/open", new
        {
            warehouseId,
            openingCash = 100_000m,
        });
        Assert.Equal(HttpStatusCode.OK, openAgain.StatusCode);
        var reopened = await openAgain.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(shiftId, reopened.GetProperty("id").GetGuid());

        var current = await authed.GetFromJsonAsync<JsonElement>($"/api/sales/shifts/current?warehouseId={warehouseId}");
        Assert.Equal(shiftId, current!.GetProperty("id").GetGuid());

        var closeResponse = await authed.PostAsJsonAsync($"/api/sales/shifts/{shiftId}/close", new
        {
            closingCash = 100_000m,
            closeNotes = "Test close",
        });
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, closed!.GetProperty("status").GetInt16());
        Assert.Equal(100_000m, closed.GetProperty("closingCash").GetDecimal());
        Assert.Equal(0m, closed.GetProperty("cashVariance").GetDecimal());

        var noShift = await authed.GetAsync($"/api/sales/shifts/current?warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.NotFound, noShift.StatusCode);
    }

    private static async Task<Guid> GetWarehouseWithoutOpenShiftAsync(HttpClient authed)
    {
        var warehouses = await authed.GetFromJsonAsync<JsonElement>("/api/inventory/warehouses");
        foreach (var wh in warehouses!.EnumerateArray())
        {
            var id = wh.GetProperty("id").GetGuid();
            var current = await authed.GetAsync($"/api/sales/shifts/current?warehouseId={id}");
            if (current.StatusCode == HttpStatusCode.NotFound)
                return id;
        }

        throw new InvalidOperationException("No warehouse without an open shift is available for testing.");
    }

    private static async Task<Guid> OpenShiftForWarehouseAsync(HttpClient authed, Guid warehouseId, decimal openingCash = 0m)
    {
        var currentResponse = await authed.GetAsync($"/api/sales/shifts/current?warehouseId={warehouseId}");
        if (currentResponse.StatusCode == HttpStatusCode.OK)
        {
            var current = await currentResponse.Content.ReadFromJsonAsync<JsonElement>();
            return current!.GetProperty("id").GetGuid();
        }

        var openResponse = await authed.PostAsJsonAsync("/api/sales/shifts/open", new
        {
            warehouseId,
            openingCash,
        });
        openResponse.EnsureSuccessStatusCode();
        var opened = await openResponse.Content.ReadFromJsonAsync<JsonElement>();
        return opened!.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> GetMainWarehouseIdAsync(HttpClient authed)
    {
        var warehouses = await authed.GetFromJsonAsync<JsonElement>("/api/inventory/warehouses");
        return warehouses!.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<decimal> GetProductStockAsync(HttpClient authed, Guid warehouseId, Guid productId)
    {
        var stock = await authed.GetFromJsonAsync<JsonElement>(
            $"/api/inventory/stock/batches?warehouseId={warehouseId}&productId={productId}&page=1&pageSize=50");
        return stock!.GetProperty("items").EnumerateArray()
            .Sum(row => row.GetProperty("quantityAvailable").GetDecimal());
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
            tenantCode = "DEMO_PHARMACY",
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        return payload.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Missing access token.");
    }

    private HttpClient CreateAuthedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
