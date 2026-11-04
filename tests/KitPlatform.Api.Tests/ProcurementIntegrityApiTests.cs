using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class ProcurementIntegrityApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid SupplierId = Guid.Parse("88888888-8888-8888-8888-888888888801");
    private static readonly Guid ProductId = Guid.Parse("66666666-6666-6666-6666-666666666604");
    private static readonly Guid BaseUnitId = Guid.Parse("77777777-7777-7777-7777-777777777705");

    private readonly KitPlatformWebApplicationFactory _factory;

    public ProcurementIntegrityApiTests(KitPlatformWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Box_unit_is_rejected_on_purchase_order_and_goods_receipt()
    {
        using var authed = await AuthedAsync();
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var vat = await GetVatTreatmentIdAsync(authed);
        var boxUnitId = await InsertBoxUnitAsync();
        try
        {
            var poResponse = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
            {
                supplierId = SupplierId,
                warehouseId,
                vatTreatmentId = vat,
                items = new[] { new { productId = ProductId, productUnitId = boxUnitId, orderedQty = 1m, unitPrice = 50000m } },
            });
            Assert.Equal(HttpStatusCode.BadRequest, poResponse.StatusCode);
            Assert.Contains("đơn vị gốc", await poResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var grnResponse = await authed.PostAsJsonAsync("/api/procurement/goods-receipts", new
            {
                supplierId = SupplierId,
                warehouseId,
                vatTreatmentId = vat,
                items = new[]
                {
                    new
                    {
                        productId = ProductId,
                        productUnitId = boxUnitId,
                        batchNumber = $"BOX{Guid.NewGuid():N}"[..14],
                        expiryDate = "2032-12-31",
                        quantity = 1m,
                        unitCost = 50000m,
                    },
                },
            });
            Assert.Equal(HttpStatusCode.BadRequest, grnResponse.StatusCode);
            Assert.Contains("đơn vị gốc", await grnResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await DeleteUnitAsync(boxUnitId);
        }
    }

    [Fact]
    public async Task Goods_receipt_cannot_use_a_line_from_another_purchase_order()
    {
        using var authed = await AuthedAsync();
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var poA = await CreateAndApprovePoAsync(authed, warehouseId, 5m);
        var poB = await CreateAndApprovePoAsync(authed, warehouseId, 5m);
        var poBItemId = await GetFirstItemIdAsync(authed, poB);

        var response = await authed.PostAsJsonAsync("/api/procurement/goods-receipts", new
        {
            purchaseOrderId = poA,
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId = await GetVatTreatmentIdAsync(authed),
            items = new[]
            {
                new
                {
                    purchaseOrderItemId = poBItemId,
                    productId = ProductId,
                    productUnitId = BaseUnitId,
                    batchNumber = $"XPO{Guid.NewGuid():N}"[..14],
                    expiryDate = "2032-12-31",
                    quantity = 1m,
                    unitCost = 5000m,
                },
            },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var poBAfter = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poB}");
        Assert.Equal(0m, poBAfter.GetProperty("items")[0].GetProperty("receivedQty").GetDecimal());
    }

    [Fact]
    public async Task New_purchase_order_gets_a_fresh_number_after_an_older_one_is_purged()
    {
        using var authed = await AuthedAsync();
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var older = await CreateDraftPoAsync(authed, warehouseId);
        var newer = await CreateDraftPoAsync(authed, warehouseId);

        await authed.PostAsync($"/api/procurement/purchase-orders/{older.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/purchase-orders/{older.Id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await authed.DeleteAsync($"/api/procurement/purchase-orders/{older.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await authed.DeleteAsync($"/api/procurement/purchase-orders/{older.Id}/purge")).StatusCode);

        var next = await CreateDraftPoAsync(authed, warehouseId);

        Assert.NotEqual(newer.Number, next.Number);
        Assert.True(string.CompareOrdinal(next.Number, newer.Number) > 0, $"{next.Number} should follow {newer.Number}");
    }

    [Fact]
    public async Task Supplier_payment_linked_to_a_receipt_cannot_exceed_what_is_owed()
    {
        using var authed = await AuthedAsync();
        var warehouseId = await GetMainWarehouseIdAsync(authed);
        var grnResponse = await authed.PostAsJsonAsync("/api/procurement/goods-receipts", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId = await GetVatTreatmentIdAsync(authed),
            items = new[]
            {
                new
                {
                    productId = ProductId,
                    productUnitId = BaseUnitId,
                    batchNumber = $"PAY{Guid.NewGuid():N}"[..14],
                    expiryDate = "2032-12-31",
                    quantity = 2m,
                    unitCost = 5000m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, grnResponse.StatusCode);
        var grnId = (await grnResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var completed = await authed.PostAsync($"/api/procurement/goods-receipts/{grnId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var total = (await completed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalAmount").GetDecimal();
        Assert.True(total > 0);

        var over = await CreateSupplierPaymentAsync(authed, grnId, total + 1000m);
        Assert.Equal(HttpStatusCode.BadRequest, (await authed.PostAsync($"/api/procurement/supplier-payments/{over}/post", null)).StatusCode);

        var exact = await CreateSupplierPaymentAsync(authed, grnId, total);
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/supplier-payments/{exact}/post", null)).StatusCode);
    }

    private static async Task<Guid> CreateSupplierPaymentAsync(HttpClient authed, Guid grnId, decimal amount)
    {
        var response = await authed.PostAsJsonAsync("/api/procurement/supplier-payments", new
        {
            supplierId = SupplierId,
            goodsReceiptId = grnId,
            amount,
            paymentMethod = 1,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<HttpClient> AuthedAsync()
    {
        using var anonymous = _factory.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/api/auth/login", new
        {
            tenantCode = "DEMO_PHARMACY",
            username = "admin",
            password = "Admin@123",
        });
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> GetMainWarehouseIdAsync(HttpClient authed)
    {
        var warehouses = await authed.GetFromJsonAsync<JsonElement>("/api/inventory/warehouses");
        return warehouses.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<Guid> GetVatTreatmentIdAsync(HttpClient authed)
    {
        var treatments = await authed.GetFromJsonAsync<JsonElement>("/api/procurement/vat-treatments?activeOnly=true");
        return treatments.EnumerateArray().First().GetProperty("id").GetGuid();
    }

    private static async Task<(Guid Id, string Number)> CreateDraftPoAsync(HttpClient authed, Guid warehouseId)
    {
        var response = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId = await GetVatTreatmentIdAsync(authed),
            items = new[] { new { productId = ProductId, productUnitId = BaseUnitId, orderedQty = 1m, unitPrice = 1000m } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("id").GetGuid(), body.GetProperty("poNumber").GetString()!);
    }

    private static async Task<Guid> CreateAndApprovePoAsync(HttpClient authed, Guid warehouseId, decimal qty)
    {
        var response = await authed.PostAsJsonAsync("/api/procurement/purchase-orders", new
        {
            supplierId = SupplierId,
            warehouseId,
            vatTreatmentId = await GetVatTreatmentIdAsync(authed),
            items = new[] { new { productId = ProductId, productUnitId = BaseUnitId, orderedQty = qty, unitPrice = 5000m } },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var poId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await authed.PostAsync($"/api/procurement/purchase-orders/{poId}/approve", null)).StatusCode);
        return poId;
    }

    private static async Task<Guid> GetFirstItemIdAsync(HttpClient authed, Guid poId)
    {
        var po = await authed.GetFromJsonAsync<JsonElement>($"/api/procurement/purchase-orders/{poId}");
        return po.GetProperty("items")[0].GetProperty("id").GetGuid();
    }

    private static async Task<Guid> InsertBoxUnitAsync()
    {
        var id = Guid.NewGuid();
        await using var conn = new NpgsqlConnection(Environment.GetEnvironmentVariable(KitPlatformWebApplicationFactory.ConnectionEnvironmentVariable));
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO product_units (id, tenant_id, product_id, unit_name, conversion_factor, is_base_unit, is_sale_unit, status)
            VALUES (@Id, @TenantId, @ProductId, @Name, 10, FALSE, FALSE, 1)
            """,
            conn);
        command.Parameters.AddWithValue("Id", id);
        command.Parameters.AddWithValue("TenantId", DemoTenantId);
        command.Parameters.AddWithValue("ProductId", ProductId);
        command.Parameters.AddWithValue("Name", "Hộp " + id.ToString("N")[..6]);
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task DeleteUnitAsync(Guid id)
    {
        await using var conn = new NpgsqlConnection(Environment.GetEnvironmentVariable(KitPlatformWebApplicationFactory.ConnectionEnvironmentVariable));
        await conn.OpenAsync();
        await using var command = new NpgsqlCommand("DELETE FROM product_units WHERE id = @Id", conn);
        command.Parameters.AddWithValue("Id", id);
        await command.ExecuteNonQueryAsync();
    }
}
