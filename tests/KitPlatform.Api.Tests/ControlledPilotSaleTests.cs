using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace KitPlatform.Api.Tests;

/// <summary>
/// Local DEMO_PHARMACY proof for the counter consultation sale.
/// Refuses to run against NT_XUANHOA. Does not create a user.
/// </summary>
public sealed class ControlledPilotSaleTests : IClassFixture<ControlledPilotHostFactory>
{
    private static readonly Guid DemoCustomerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    private static readonly Guid ParaProductId = Guid.Parse("66666666-6666-6666-6666-666666666601");
    private static readonly Guid ParaUnitId = Guid.Parse("77777777-7777-7777-7777-777777777701");

    private readonly ControlledPilotHostFactory _factory;
    private readonly HttpClient _client;

    public ControlledPilotSaleTests(ControlledPilotHostFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Unauthenticated_session_save_is_rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/sales/pos/consultation/sessions", new
        {
            consultationLevel = 1,
            confirmedFacts = Facts(34, ["cough"], false, []),
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_only_products_are_not_sellable_and_in_date_fefo_sale_links()
    {
        var (token, tenantCode) = await LoginDemoAsync();
        Assert.Equal("DEMO_PHARMACY", tenantCode);
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetWarehouseIdAsync(authed, "WH_MAIN");
        await EnsureShiftOpenAsync(authed, warehouseId);
        var today = DateOnly.FromDateTime(DateTime.Today);

        await AssertSafetyGatesAsync(authed, warehouseId);

        var suggestions = await SuggestAsync(authed, warehouseId, Facts(34, ["cough", "sore_throat"], false, []));
        Assert.False(suggestions.GetProperty("blocked").GetBoolean());
        var codes = suggestions.GetProperty("suggestions").EnumerateArray()
            .Select(row => row.GetProperty("productCode").GetString())
            .ToArray();
        Assert.NotEmpty(codes);
        Assert.DoesNotContain("DECK001", codes);
        Assert.DoesNotContain("DECK006", codes);

        await AssertExpiredProductRefusedAsync(authed, warehouseId, "DECK001", today);
        await AssertExpiredProductRefusedAsync(authed, warehouseId, "DECK006", today);

        var ordersBefore = await OrderTotalAsync(authed);
        var lotsBefore = await LotsAsync(authed, warehouseId, ParaProductId);
        var expected = EarliestSellable(lotsBefore, today);
        Assert.NotNull(expected.Expiry);

        var preview = await authed.PostAsJsonAsync("/api/sales/pos/preview-allocation", new
        {
            warehouseId,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewBody = await preview.Content.ReadFromJsonAsync<JsonElement>();
        var allocated = previewBody.GetProperty("lines")[0].GetProperty("allocations")[0];
        Assert.Equal(expected.BatchNumber, allocated.GetProperty("batchNumber").GetString());
        Assert.Equal(expected.Expiry, DateOnly.Parse(allocated.GetProperty("expiryDate").GetString()!));

        var sessionResponse = await authed.PostAsJsonAsync("/api/sales/pos/consultation/sessions", new
        {
            customerId = DemoCustomerId,
            consultationLevel = 1,
            naturalLanguage = "ho và đau họng 2 ngày, không sốt",
            confirmedFacts = Facts(34, ["cough", "sore_throat"], false, []),
        });
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        var session = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = session.GetProperty("id").GetGuid();
        Assert.Equal("confirmed", session.GetProperty("status").GetString());

        var saleResponse = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            customerId = DemoCustomerId,
            priceType = 1,
            items = new[] { new { productId = ParaProductId, productUnitId = ParaUnitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.Created, saleResponse.StatusCode);
        var order = await saleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, order.GetProperty("status").GetInt16());
        Assert.Equal(0m, order.GetProperty("outstanding").GetDecimal());
        Assert.Equal(order.GetProperty("totalAmount").GetDecimal(), order.GetProperty("amountPaid").GetDecimal());
        var line = order.GetProperty("items").EnumerateArray().Single();
        Assert.Equal(expected.BatchNumber, line.GetProperty("batchNumber").GetString());
        Assert.Equal(expected.Expiry, DateOnly.Parse(line.GetProperty("expiryDate").GetString()!));
        var orderId = order.GetProperty("id").GetGuid();

        var linkResponse = await authed.PostAsJsonAsync(
            $"/api/sales/pos/consultation/sessions/{sessionId}/link-order",
            new { salesOrderId = orderId });
        Assert.Equal(HttpStatusCode.OK, linkResponse.StatusCode);
        var linked = await linkResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("linked", linked.GetProperty("status").GetString());
        Assert.Equal(orderId, linked.GetProperty("salesOrderId").GetGuid());

        Assert.Equal(ordersBefore + 1, await OrderTotalAsync(authed));

        var lotsAfter = await LotsAsync(authed, warehouseId, ParaProductId);
        foreach (var before in lotsBefore)
        {
            var after = lotsAfter.Single(lot => lot.BatchNumber == before.BatchNumber);
            var expectedQty = before.BatchNumber == expected.BatchNumber
                ? before.Quantity - 1m
                : before.Quantity;
            Assert.Equal(expectedQty, after.Quantity);
        }
    }

    [Fact]
    public async Task Label_optional_scan_of_expired_lot_does_not_fall_through_to_fefo()
    {
        var (token, tenantCode) = await LoginDemoAsync();
        Assert.Equal("DEMO_PHARMACY", tenantCode);
        using var authed = CreateAuthedClient(token);
        var warehouseId = await GetWarehouseIdAsync(authed, "WH_MAIN");
        await EnsureShiftOpenAsync(authed, warehouseId);

        var modeResponse = await authed.GetAsync("/api/sales/settings/batch-mode");
        Assert.Equal(HttpStatusCode.OK, modeResponse.StatusCode);
        var originalMode = (await modeResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("batchMode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(originalMode));

        var switched = await authed.PutAsJsonAsync("/api/sales/settings/batch-mode", new { batchMode = "label_optional" });
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        try
        {
            var productId = Guid.Parse("66666666-6666-6666-6666-666666666604");
            var unitId = Guid.Parse("77777777-7777-7777-7777-777777777705");
            var lotsBefore = await LotsAsync(authed, warehouseId, productId);
            var expired = lotsBefore.Single(lot => lot.BatchNumber == "VITC-NEAR");
            var inDate = lotsBefore.Single(lot => lot.BatchNumber == "4444");
            Assert.True(expired.Expiry < DateOnly.FromDateTime(DateTime.Today));
            Assert.True(inDate.Expiry >= DateOnly.FromDateTime(DateTime.Today));
            var ordersBefore = await OrderTotalAsync(authed);

            var sale = await authed.PostAsJsonAsync("/api/sales/orders", new
            {
                warehouseId,
                customerId = DemoCustomerId,
                priceType = 1,
                items = new[]
                {
                    new
                    {
                        productId,
                        productUnitId = unitId,
                        quantity = 1m,
                        batchNumber = "VITC-NEAR",
                    },
                },
            });
            Assert.Equal(HttpStatusCode.BadRequest, sale.StatusCode);
            var body = await sale.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains("hết hạn", body.GetProperty("message").GetString(), StringComparison.Ordinal);
            Assert.Equal(ordersBefore, await OrderTotalAsync(authed));

            var lotsAfter = await LotsAsync(authed, warehouseId, productId);
            Assert.Equal(expired.Quantity, lotsAfter.Single(lot => lot.BatchNumber == "VITC-NEAR").Quantity);
            Assert.Equal(inDate.Quantity, lotsAfter.Single(lot => lot.BatchNumber == "4444").Quantity);
        }
        finally
        {
            var restore = await authed.PutAsJsonAsync(
                "/api/sales/settings/batch-mode",
                new { batchMode = originalMode });
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
            var current = await authed.GetFromJsonAsync<JsonElement>("/api/sales/settings/batch-mode");
            Assert.Equal(originalMode, current.GetProperty("batchMode").GetString());
        }
    }

    private async Task AssertSafetyGatesAsync(HttpClient authed, Guid warehouseId)
    {
        var seizure = await SuggestAsync(authed, warehouseId, Facts(34, ["cough"], false, ["seizure"]));
        Assert.True(seizure.GetProperty("blocked").GetBoolean());
        Assert.Equal("stop_sale", seizure.GetProperty("safetyLevel").GetString());
        Assert.Equal(0, seizure.GetProperty("suggestions").GetArrayLength());

        var infant = await SuggestAsync(authed, warehouseId, Facts(1, ["fever"], true, []));
        Assert.True(infant.GetProperty("blocked").GetBoolean());
        Assert.Equal("refer_medical", infant.GetProperty("safetyLevel").GetString());
        Assert.Equal(0, infant.GetProperty("suggestions").GetArrayLength());

        var pregnant = await SuggestAsync(
            authed,
            warehouseId,
            new
            {
                ageYears = 34,
                symptoms = new[] { "cough" },
                hasFever = false,
                isPregnant = true,
                redFlags = Array.Empty<string>(),
            });
        Assert.False(pregnant.GetProperty("blocked").GetBoolean());
        Assert.Equal("refer_pharmacist", pregnant.GetProperty("safetyLevel").GetString());
        Assert.True(pregnant.GetProperty("suggestions").GetArrayLength() > 0);
    }

    private async Task AssertExpiredProductRefusedAsync(
        HttpClient authed,
        Guid warehouseId,
        string productCode,
        DateOnly today)
    {
        var lookupResponse = await authed.GetAsync(
            $"/api/sales/pos/lookup?query={productCode}&warehouseId={warehouseId}");
        Assert.Equal(HttpStatusCode.OK, lookupResponse.StatusCode);
        var lookup = await lookupResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(productCode, lookup.GetProperty("productCode").GetString());
        Assert.Equal(0m, lookup.GetProperty("stockAvailable").GetDecimal());
        if (lookup.TryGetProperty("batchHints", out var hints) && hints.ValueKind == JsonValueKind.Array)
        {
            Assert.DoesNotContain(
                hints.EnumerateArray(),
                hint => hint.TryGetProperty("expiryDate", out var expiry)
                    && expiry.ValueKind == JsonValueKind.String
                    && DateOnly.Parse(expiry.GetString()!) < today);
        }

        var productId = lookup.GetProperty("productId").GetGuid();
        var unitId = lookup.GetProperty("productUnitId").GetGuid();
        var lotsBefore = await LotsAsync(authed, warehouseId, productId);
        Assert.Contains(lotsBefore, lot => lot.Quantity > 0 && lot.Expiry is DateOnly expiry && expiry < today);
        Assert.DoesNotContain(lotsBefore, lot => lot.Quantity > 0 && (lot.Expiry is null || lot.Expiry >= today));
        var ordersBefore = await OrderTotalAsync(authed);

        var preview = await authed.PostAsJsonAsync("/api/sales/pos/preview-allocation", new
        {
            warehouseId,
            items = new[] { new { productId, productUnitId = unitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);

        var sale = await authed.PostAsJsonAsync("/api/sales/orders", new
        {
            warehouseId,
            customerId = DemoCustomerId,
            priceType = 1,
            items = new[] { new { productId, productUnitId = unitId, quantity = 1m } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, sale.StatusCode);
        var body = await sale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("hết hạn", body.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(ordersBefore, await OrderTotalAsync(authed));

        var lotsAfter = await LotsAsync(authed, warehouseId, productId);
        foreach (var before in lotsBefore)
        {
            var after = lotsAfter.Single(lot => lot.BatchNumber == before.BatchNumber);
            Assert.Equal(before.Quantity, after.Quantity);
        }
    }

    private async Task<JsonElement> SuggestAsync(HttpClient authed, Guid warehouseId, object facts)
    {
        var response = await authed.PostAsJsonAsync("/api/sales/pos/consultation/suggest", new
        {
            warehouseId,
            limit = 12,
            confirmedFacts = facts,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static object Facts(int ageYears, string[] symptoms, bool hasFever, string[] redFlags) => new
    {
        ageYears,
        symptoms,
        hasFever,
        redFlags,
    };

    private async Task<(string Token, string TenantCode)> LoginDemoAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            username = "admin",
            password = "Admin@123",
            tenantCode = "DEMO_PHARMACY",
        });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var user = payload.GetProperty("user");
        var tenantCode = user.GetProperty("tenantCode").GetString()
            ?? throw new InvalidOperationException("Missing tenant.");
        if (tenantCode == "NT_XUANHOA")
            throw new InvalidOperationException("Refusing to run the pilot proof on NT_XUANHOA.");
        var token = payload.GetProperty("accessToken").GetString()
            ?? throw new InvalidOperationException("Missing access token.");
        return (token, tenantCode);
    }

    private HttpClient CreateAuthedClient(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task EnsureShiftOpenAsync(HttpClient authed, Guid warehouseId)
    {
        var current = await authed.GetAsync($"/api/sales/shifts/current?warehouseId={warehouseId}");
        if (current.StatusCode == HttpStatusCode.OK)
            return;

        var open = await authed.PostAsJsonAsync("/api/sales/shifts/open", new
        {
            warehouseId,
            openingCash = 0m,
        });
        Assert.True(
            open.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created,
            $"Could not reuse or open the DEMO shift: {open.StatusCode}");
    }

    private static async Task<Guid> GetWarehouseIdAsync(HttpClient authed, string code)
    {
        var warehouses = await authed.GetFromJsonAsync<JsonElement>("/api/inventory/warehouses");
        return warehouses.EnumerateArray()
            .First(row => row.GetProperty("warehouseCode").GetString() == code)
            .GetProperty("id")
            .GetGuid();
    }

    private static async Task<int> OrderTotalAsync(HttpClient authed)
    {
        var list = await authed.GetFromJsonAsync<JsonElement>("/api/sales/orders?page=1&pageSize=1");
        return list.GetProperty("total").GetInt32();
    }

    private static async Task<List<LotRow>> LotsAsync(HttpClient authed, Guid warehouseId, Guid productId)
    {
        var stock = await authed.GetFromJsonAsync<JsonElement>(
            $"/api/inventory/stock/batches?warehouseId={warehouseId}&productId={productId}&page=1&pageSize=50");
        return stock.GetProperty("items").EnumerateArray().Select(row =>
        {
            DateOnly? expiry = row.TryGetProperty("expiryDate", out var value) && value.ValueKind == JsonValueKind.String
                ? DateOnly.Parse(value.GetString()!)
                : null;
            return new LotRow(
                row.GetProperty("batchNumber").GetString() ?? "",
                expiry,
                row.GetProperty("quantityAvailable").GetDecimal());
        }).ToList();
    }

    private static LotRow EarliestSellable(IReadOnlyList<LotRow> lots, DateOnly today)
    {
        var sellable = lots.Where(lot => lot.Quantity > 0 && (lot.Expiry is null || lot.Expiry >= today)).ToList();
        var dated = sellable.Where(lot => lot.Expiry is DateOnly).OrderBy(lot => lot.Expiry).FirstOrDefault();
        return dated ?? throw new InvalidOperationException("No in-date lot to prove FEFO.");
    }

    private sealed record LotRow(string BatchNumber, DateOnly? Expiry, decimal Quantity);
}
