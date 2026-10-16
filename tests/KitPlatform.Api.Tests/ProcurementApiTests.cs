using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class ProcurementApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private readonly KitPlatformWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private static readonly Guid SupplierId = Guid.Parse("88888888-8888-8888-8888-888888888801");
    private static readonly Guid ProductId = Guid.Parse("66666666-6666-6666-6666-666666666604");
    private static readonly Guid ProductUnitId = Guid.Parse("77777777-7777-7777-7777-777777777705");

    public ProcurementApiTests(KitPlatformWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Procurement_purchase_order_requires_authentication()
    {
        var response = await _client.GetAsync("/api/procurement/purchase-orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Procurement_po_approve_grn_complete_increases_stock()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehousesResponse = await authed.GetAsync("/api/inventory/warehouses");
        Assert.Equal(HttpStatusCode.OK, warehousesResponse.StatusCode);
        var warehouseId = (await warehousesResponse.Content.ReadFromJsonAsync<JsonElement>())!
            .EnumerateArray().First().GetProperty("id").GetGuid();

        var vatTreatmentId = await ActiveVatTreatmentIdAsync(authed);
        var poResponse = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId,
            notes = "Integration test PO",
            items = new[]
            {
                new
                {
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    orderedQty = 40m,
                    unitPrice = 7500m,
                },
            },
        });
        var poBody = await poResponse.Content.ReadAsStringAsync();
        Assert.True(
            poResponse.StatusCode == HttpStatusCode.Created,
            $"Create PO failed: {(int)poResponse.StatusCode} {poBody}");
        var po = await poResponse.Content.ReadFromJsonAsync<JsonElement>();
        var poId = po.GetProperty("id").GetGuid();
        var poItemId = po.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var approveResponse = await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var batchNumber = $"GRN{Guid.NewGuid():N}"[..14];
        var grnResponse = await authed.PostAsJsonAsync("/api/procurement/goods-receipts", new
        {
            purchaseOrderId = poId,
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId,
            items = new[]
            {
                new
                {
                    purchaseOrderItemId = poItemId,
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    batchNumber,
                    expiryDate = "2032-06-30",
                    quantity = 40m,
                    unitCost = 7500m,
                },
            },
        });
        var grnBody = await grnResponse.Content.ReadAsStringAsync();
        Assert.True(
            grnResponse.StatusCode == HttpStatusCode.Created,
            $"Create GRN failed: {(int)grnResponse.StatusCode} {grnBody}");
        var grnId = (await grnResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var stockBeforeResponse = await authed.GetAsync(
            $"/api/inventory/stock/batches?warehouseId={warehouseId}&search={batchNumber}");
        Assert.Equal(HttpStatusCode.OK, stockBeforeResponse.StatusCode);
        var stockBefore = await stockBeforeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var beforeQty = stockBefore.TryGetProperty("items", out var beforeItems) && beforeItems.GetArrayLength() > 0
            ? beforeItems.EnumerateArray().First().GetProperty("quantityAvailable").GetDecimal()
            : 0m;

        var completeResponse = await authed.PostAsync($"/api/procurement/goods-receipts/{grnId}/complete", null);
        var completeBody = await completeResponse.Content.ReadAsStringAsync();
        Assert.True(
            completeResponse.StatusCode == HttpStatusCode.OK,
            $"Complete GRN failed: {(int)completeResponse.StatusCode} {completeBody}");

        var stockAfterResponse = await authed.GetAsync(
            $"/api/inventory/stock/batches?warehouseId={warehouseId}&search={batchNumber}");
        Assert.Equal(HttpStatusCode.OK, stockAfterResponse.StatusCode);
        var stockAfter = await stockAfterResponse.Content.ReadFromJsonAsync<JsonElement>();
        var afterQty = stockAfter.GetProperty("items").EnumerateArray().First().GetProperty("quantityAvailable").GetDecimal();
        Assert.Equal(beforeQty + 40m, afterQty);

        var poAfterResponse = await authed.GetAsync($"/api/procurement/purchase-orders/{poId}");
        var poAfter = await poAfterResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(4, poAfter.GetProperty("status").GetInt16());
    }

    [Fact]
    public async Task Procurement_partial_receipt_then_close_po()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poId = await CreateAndApprovePoAsync(authed, warehouseId, 50m);
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        var poItemId = po.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var grn1 = await CreateGrnAsync(authed, poId, warehouseId, poItemId, 20m);
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/goods-receipts/{grn1}/complete", null)).StatusCode);

        var poPartial = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        Assert.Equal(3, poPartial.GetProperty("status").GetInt16());

        var grn2 = await CreateGrnAsync(authed, poId, warehouseId, poItemId, 30m);
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/goods-receipts/{grn2}/complete", null)).StatusCode);

        var poReceived = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        Assert.Equal(4, poReceived.GetProperty("status").GetInt16());

        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/close", null)).StatusCode);
        var poClosed = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        Assert.Equal(5, poClosed.GetProperty("status").GetInt16());
    }

    [Fact]
    public async Task Procurement_cancel_draft_grn()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poId = await CreateAndApprovePoAsync(authed, warehouseId, 10m);
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        var poItemId = po.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();
        var grnId = await CreateGrnAsync(authed, poId, warehouseId, poItemId, 10m);

        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/goods-receipts/{grnId}/cancel", null)).StatusCode);
        var grn = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/goods-receipts/{grnId}");
        Assert.Equal(3, grn.GetProperty("status").GetInt16());
    }

    [Fact]
    public async Task Procurement_supplier_payment_draft_post_cancel()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var createResponse = await authed.PostAsJsonAsync("/api/procurement/supplier-payments", new
        {
            supplierId = SupplierId,
            amount = 1_500_000m,
            paymentMethod = 2,
            notes = "Test payment draft",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var paymentId = created.GetProperty("id").GetGuid();
        Assert.Equal(1, created.GetProperty("status").GetInt16());

        var updateResponse = await authed.PutAsJsonAsync($"/api/procurement/supplier-payments/{paymentId}", new
        {
            supplierId = SupplierId,
            amount = 1_600_000m,
            paymentMethod = 1,
            notes = "Updated draft",
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1_600_000m, updated.GetProperty("amount").GetDecimal());

        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/supplier-payments/{paymentId}/post", null)).StatusCode);
        var posted = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/supplier-payments/{paymentId}");
        Assert.Equal(2, posted.GetProperty("status").GetInt16());
        Assert.False(posted.TryGetProperty("postedAt", out var postedAt) && postedAt.ValueKind == JsonValueKind.Null);

        var cancelPosted = await authed.PostAsync($"/api/procurement/supplier-payments/{paymentId}/cancel", null);
        Assert.Equal(HttpStatusCode.BadRequest, cancelPosted.StatusCode);

        var draft2Response = await authed.PostAsJsonAsync("/api/procurement/supplier-payments", new
        {
            supplierId = SupplierId,
            amount = 500_000m,
            paymentMethod = 2,
        });
        var draft2Id = (await draft2Response.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/supplier-payments/{draft2Id}/cancel", null)).StatusCode);
        var cancelled = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/supplier-payments/{draft2Id}");
        Assert.Equal(3, cancelled.GetProperty("status").GetInt16());
    }

    [Fact]
    public async Task Procurement_supplier_payment_validates_po_grn_links()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var vatTreatmentId = await ActiveVatTreatmentIdAsync(authed);
        var poResponse = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId,
            items = new[] { new { productId = ProductId, productUnitId = ProductUnitId, orderedQty = 1m, unitPrice = 1000m } },
        });
        var poId = (await poResponse.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetGuid();

        var invalidPoLink = await authed.PostAsJsonAsync("/api/procurement/supplier-payments", new
        {
            supplierId = SupplierId,
            purchaseOrderId = poId,
            amount = 100_000m,
            paymentMethod = 2,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPoLink.StatusCode);

        await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/approve", null);
        var validDraft = await authed.PostAsJsonAsync("/api/procurement/supplier-payments", new
        {
            supplierId = SupplierId,
            purchaseOrderId = poId,
            amount = 100_000m,
            paymentMethod = 2,
            paymentDate = "2026-06-15",
        });
        Assert.Equal(HttpStatusCode.Created, validDraft.StatusCode);
        var payment = await validDraft.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-06-15", payment.GetProperty("paymentDate").GetString()![..10]);

        var bogusGrn = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var invalidGrn = await authed.PutAsJsonAsync(
            $"/api/procurement/supplier-payments/{payment.GetProperty("id").GetGuid()}",
            new
            {
                supplierId = SupplierId,
                purchaseOrderId = poId,
                goodsReceiptId = bogusGrn,
                amount = 100_000m,
                paymentMethod = 2,
                paymentDate = "2026-06-15",
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidGrn.StatusCode);
    }

    [Fact]
    public async Task Procurement_archive_and_purge_cancelled_po()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var vatTreatmentId = await ActiveVatTreatmentIdAsync(authed);
        var poResponse = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId,
            items = new[] { new { productId = ProductId, productUnitId = ProductUnitId, orderedQty = 1m, unitPrice = 1000m } },
        });
        var poId = (await poResponse.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetGuid();
        await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/approve", null);
        await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/cancel", null);

        Assert.Equal(HttpStatusCode.NoContent, (await authed.DeleteAsync($"/api/procurement/purchase-orders/{poId}")).StatusCode);
        var hidden = await authed.GetAsync($"/api/procurement/purchase-orders/{poId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var archivedListResponse = await authed.GetAsync("/api/procurement/purchase-orders?includeArchived=true");
        Assert.Equal(HttpStatusCode.OK, archivedListResponse.StatusCode);
        var archivedList = await archivedListResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            archivedList!.GetProperty("items").EnumerateArray(),
            row => row.GetProperty("id").GetGuid() == poId && row.TryGetProperty("deletedAt", out _));

        Assert.Equal(HttpStatusCode.NoContent, (await authed.DeleteAsync($"/api/procurement/purchase-orders/{poId}/purge")).StatusCode);
        var purgedList = await authed.GetFromJsonAsync<JsonElement>(
            "/api/procurement/purchase-orders?includeArchived=true");
        Assert.DoesNotContain(purgedList!.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetGuid() == poId);
    }

    [Fact]
    public async Task Procurement_update_po_increases_ordered_qty()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poId = await CreateAndApprovePoAsync(authed, warehouseId, 10m);
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        var itemId = po!.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var updateResponse = await authed.PutAsJsonAsync($"/api/procurement/purchase-orders/{poId}", new
        {
            vatTreatmentId = po.GetProperty("vatTreatmentId").GetGuid(),
            notes = "Tăng SL giao thừa",
            items = new[]
            {
                new
                {
                    id = itemId,
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    orderedQty = 15m,
                    unitPrice = 5000m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(15m, updated!.GetProperty("items").EnumerateArray().First().GetProperty("orderedQty").GetDecimal());
    }

    [Fact]
    public async Task Procurement_update_po_rejects_price_change_on_existing_line()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poId = await CreateAndApprovePoAsync(authed, warehouseId, 10m);
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        var item = po!.GetProperty("items").EnumerateArray().First();

        var updateResponse = await authed.PutAsJsonAsync($"/api/procurement/purchase-orders/{poId}", new
        {
            vatTreatmentId = po.GetProperty("vatTreatmentId").GetGuid(),
            items = new[]
            {
                new
                {
                    id = item.GetProperty("id").GetGuid(),
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    orderedQty = 12m,
                    unitPrice = 6000m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Procurement_update_po_rejects_decrease_qty_on_unreceived_line()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poId = await CreateAndApprovePoAsync(authed, warehouseId, 10m);
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        var item = po!.GetProperty("items").EnumerateArray().First();

        var updateResponse = await authed.PutAsJsonAsync($"/api/procurement/purchase-orders/{poId}", new
        {
            vatTreatmentId = po.GetProperty("vatTreatmentId").GetGuid(),
            items = new[]
            {
                new
                {
                    id = item.GetProperty("id").GetGuid(),
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    orderedQty = 8m,
                    unitPrice = 5000m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task Procurement_update_po_rejects_edit_on_received_line()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poId = await CreateAndApprovePoAsync(authed, warehouseId, 10m);
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        var item = po!.GetProperty("items").EnumerateArray().First();
        var itemId = item.GetProperty("id").GetGuid();

        var grnId = await CreateGrnAsync(authed, poId, warehouseId, itemId, 5m);
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/goods-receipts/{grnId}/complete", null)).StatusCode);

        var updateResponse = await authed.PutAsJsonAsync($"/api/procurement/purchase-orders/{poId}", new
        {
            vatTreatmentId = po.GetProperty("vatTreatmentId").GetGuid(),
            items = new[]
            {
                new
                {
                    id = itemId,
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    orderedQty = 12m,
                    unitPrice = 5000m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    private static async Task<Guid> GetMainWarehouseIdAsync(HttpClient authed)
    {
        var warehouses = await authed.GetFromJsonAsync<JsonElement>("/api/inventory/warehouses");
        return warehouses!.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<Guid> ActiveVatTreatmentIdAsync(HttpClient authed)
    {
        var list = await authed.GetFromJsonAsync<JsonElement>("/api/procurement/vat-treatments?activeOnly=true");
        Assert.Equal(JsonValueKind.Array, list!.ValueKind);
        Assert.True(list.GetArrayLength() > 0, "DEMO_PHARMACY has no active VAT treatment.");
        return list.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreateAndApprovePoAsync(HttpClient authed, Guid warehouseId, decimal qty)
    {
        var vatTreatmentId = await ActiveVatTreatmentIdAsync(authed);
        var poResponse = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId,
            items = new[] { new { productId = ProductId, productUnitId = ProductUnitId, orderedQty = qty, unitPrice = 5000m } },
        });
        Assert.Equal(HttpStatusCode.Created, poResponse.StatusCode);
        var poId = (await poResponse.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/approve", null)).StatusCode);
        return poId;
    }

    private static async Task<Guid> CreateGrnAsync(HttpClient authed, Guid poId, Guid warehouseId, Guid poItemId, decimal qty)
    {
        var vatTreatmentId = await ActiveVatTreatmentIdAsync(authed);
        var batchNumber = $"GRN{Guid.NewGuid():N}"[..14];
        var grnResponse = await authed.PostAsJsonAsync("/api/procurement/goods-receipts", new
        {
            purchaseOrderId = poId,
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId,
            items = new[]
            {
                new
                {
                    purchaseOrderItemId = poItemId,
                    productId = ProductId,
                    productUnitId = ProductUnitId,
                    batchNumber,
                    expiryDate = "2032-12-31",
                    quantity = qty,
                    unitCost = 5000m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, grnResponse.StatusCode);
        return (await grnResponse.Content.ReadFromJsonAsync<JsonElement>())!.GetProperty("id").GetGuid();
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
            tenantCode = "DEMO_PHARMACY",
        });

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"Login failed ({response.StatusCode}). Ensure PostgreSQL is running with demo seed.");
        }

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
