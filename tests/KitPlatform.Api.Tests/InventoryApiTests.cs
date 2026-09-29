using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

public sealed class InventoryApiTests : IClassFixture<KitPlatformWebApplicationFactory>
{
    private readonly KitPlatformWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InventoryApiTests(KitPlatformWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Inventory_stock_requires_authentication()
    {
        var response = await _client.GetAsync("/api/inventory/stock/batches");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_revalue_unit_cost_requires_authentication()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/api/inventory/stock/batches/{Guid.Empty}/unit-cost",
            new { unitCost = 400m });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inventory_opening_balance_and_transfer_flow()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var warehousesResponse = await authed.GetAsync("/api/inventory/warehouses");
        Assert.Equal(HttpStatusCode.OK, warehousesResponse.StatusCode);
        var warehouses = await warehousesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var mainWarehouseId = warehouses.EnumerateArray().First().GetProperty("id").GetGuid();

        var branchesResponse = await authed.GetAsync("/api/inventory/warehouses/branches");
        Assert.Equal(HttpStatusCode.OK, branchesResponse.StatusCode);
        var branchId = (await branchesResponse.Content.ReadFromJsonAsync<JsonElement>())!
            .EnumerateArray().First().GetProperty("id").GetGuid();

        var destCode = $"WH_T{Guid.NewGuid():N}"[..12];
        var createWhResponse = await authed.PostAsJsonAsync("/api/inventory/warehouses", new
        {
            branchId,
            warehouseCode = destCode,
            warehouseName = "Kho chi nhánh test",
            warehouseType = (short)2,
            isDefault = false,
        });
        Assert.Equal(HttpStatusCode.Created, createWhResponse.StatusCode);
        var destWarehouseId = (await createWhResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var batchNumber = $"OB{Guid.NewGuid():N}"[..14];
        var openingResponse = await authed.PostAsJsonAsync("/api/inventory/opening-balance", new
        {
            warehouseId = mainWarehouseId,
            notes = "Integration test opening balance",
            lines = new[]
            {
                new
                {
                    productId = Guid.Parse("66666666-6666-6666-6666-666666666604"),
                    batchNumber,
                    expiryDate = "2030-12-31",
                    unitCost = 5000m,
                    quantity = 100m,
                },
            },
        });
        var openingBody = await openingResponse.Content.ReadAsStringAsync();
        Assert.True(
            openingResponse.StatusCode == HttpStatusCode.OK,
            $"Opening balance failed: {(int)openingResponse.StatusCode} {openingBody}");

        var stockResponse = await authed.GetAsync(
            $"/api/inventory/stock/batches?warehouseId={mainWarehouseId}&search={batchNumber}");
        Assert.Equal(HttpStatusCode.OK, stockResponse.StatusCode);
        var stock = await stockResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchId = stock.GetProperty("items").EnumerateArray().First().GetProperty("id").GetGuid();

        var transferResponse = await authed.PostAsJsonAsync("/api/inventory/transfers", new
        {
            fromWarehouseId = mainWarehouseId,
            toWarehouseId = destWarehouseId,
            notes = "Test transfer",
            items = new[] { new { batchId, quantity = 10m } },
        });
        var transferBody = await transferResponse.Content.ReadAsStringAsync();
        Assert.True(
            transferResponse.StatusCode == HttpStatusCode.Created,
            $"Create transfer failed: {(int)transferResponse.StatusCode} {transferBody}");
        var transferId = (await transferResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var completeResponse = await authed.PostAsync($"/api/inventory/transfers/{transferId}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        var destStockResponse = await authed.GetAsync(
            $"/api/inventory/stock/batches?warehouseId={destWarehouseId}&search={batchNumber}");
        Assert.Equal(HttpStatusCode.OK, destStockResponse.StatusCode);
        var destStock = await destStockResponse.Content.ReadFromJsonAsync<JsonElement>();
        var destQty = destStock.GetProperty("items").EnumerateArray().First().GetProperty("quantityAvailable").GetDecimal();
        Assert.Equal(10m, destQty);

        var voidBlockedResponse = await authed.DeleteAsync($"/api/inventory/opening-balance/batches/{batchId}");
        Assert.Equal(HttpStatusCode.BadRequest, voidBlockedResponse.StatusCode);

        var voidBatchNumber = $"OBV{Guid.NewGuid():N}"[..14];
        var voidOpeningResponse = await authed.PostAsJsonAsync("/api/inventory/opening-balance", new
        {
            warehouseId = mainWarehouseId,
            lines = new[]
            {
                new
                {
                    productId = Guid.Parse("66666666-6666-6666-6666-666666666604"),
                    batchNumber = voidBatchNumber,
                    expiryDate = "2031-12-31",
                    unitCost = 1000m,
                    quantity = 25m,
                },
            },
        });
        Assert.Equal(HttpStatusCode.OK, voidOpeningResponse.StatusCode);

        var listResponse = await authed.GetAsync(
            $"/api/inventory/opening-balance/batches?warehouseId={mainWarehouseId}");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        var voidBatch = list.EnumerateArray()
            .First(b => b.GetProperty("batchNumber").GetString() == voidBatchNumber);
        Assert.True(voidBatch.GetProperty("canVoid").GetBoolean());
        var voidBatchId = voidBatch.GetProperty("batchId").GetGuid();

        var voidResponse = await authed.DeleteAsync($"/api/inventory/opening-balance/batches/{voidBatchId}");
        Assert.Equal(HttpStatusCode.NoContent, voidResponse.StatusCode);

        var listAfterVoid = await authed.GetAsync(
            $"/api/inventory/opening-balance/batches?warehouseId={mainWarehouseId}");
        var listAfter = await listAfterVoid.Content.ReadFromJsonAsync<JsonElement>();
        Assert.DoesNotContain(
            listAfter.EnumerateArray(),
            b => b.GetProperty("batchId").GetGuid() == voidBatchId);
    }

    [Fact]
    public async Task Inventory_stock_products_summary()
    {
        var token = await LoginAsync();
        using var authed = CreateAuthedClient(token);

        var response = await authed.GetAsync("/api/inventory/stock/products?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("total").GetInt32() >= 1);
        var first = body.GetProperty("items").EnumerateArray().First();
        Assert.True(first.GetProperty("totalQuantity").GetDecimal() > 0);
        Assert.True(first.GetProperty("warehouseCount").GetInt32() >= 1);
        Assert.True(first.GetProperty("batchCount").GetInt32() >= 1);

        var productId = first.GetProperty("productId").GetGuid();
        var batchesResponse = await authed.GetAsync(
            $"/api/inventory/stock/batches?productId={productId}&page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, batchesResponse.StatusCode);
        var batches = await batchesResponse.Content.ReadFromJsonAsync<JsonElement>();
        var batchTotal = batches.GetProperty("items").EnumerateArray()
            .Sum(item => item.GetProperty("quantityAvailable").GetDecimal());
        Assert.Equal(first.GetProperty("totalQuantity").GetDecimal(), batchTotal);
    }

    private async Task<string> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
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
