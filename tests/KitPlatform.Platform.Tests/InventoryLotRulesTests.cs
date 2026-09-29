using KitPlatform.Packs.Pharmacy.Inventory;
using Xunit;

namespace KitPlatform.Platform.Tests;

public class InventoryLotRulesTests
{
    [Theory]
    [InlineData("  ab  12 ", "AB 12")]
    [InlineData("lot-01", "LOT-01")]
    [InlineData("", "")]
    public void Normalizes_batch_number(string input, string expected)
    {
        Assert.Equal(expected, InventoryLotRules.NormalizeBatchNumber(input));
    }

    [Fact]
    public void SameLot_ignores_case_and_spaces()
    {
        Assert.True(InventoryLotRules.SameLot("ab  12", "AB 12"));
        Assert.False(InventoryLotRules.SameLot("AB12", "AB13"));
    }

    [Fact]
    public void Rejects_conflicting_dates_on_same_document()
    {
        var product = Guid.NewGuid();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryLotRules.EnsureDocumentLotsConsistent(
            [
                (product, "L1", new DateOnly(2024, 1, 1), new DateOnly(2026, 1, 31)),
                (product, "l1", new DateOnly(2024, 2, 1), new DateOnly(2026, 1, 31)),
            ]));
        Assert.Contains("cùng một NSX", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_fills_from_existing_lot()
    {
        var existing = new InventoryLotIdentity(
            true, false, "AB12", new DateOnly(2024, 3, 1), new DateOnly(2026, 3, 31));
        var resolved = InventoryLotRules.Resolve("ab12", null, null, existing);
        Assert.Equal(existing.ManufactureDate, resolved.ManufactureDate);
        Assert.Equal(existing.ExpiryDate, resolved.ExpiryDate);
    }

    [Fact]
    public void Resolve_rejects_different_expiry()
    {
        var existing = new InventoryLotIdentity(true, false, "AB12", null, new DateOnly(2026, 3, 31));
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryLotRules.Resolve("AB12", null, new DateOnly(2027, 3, 31), existing));
        Assert.Contains("HSD", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_rejects_dirty_lot_conflict()
    {
        var existing = new InventoryLotIdentity(true, true, "AB12", null, null);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InventoryLotRules.Resolve("AB12", null, new DateOnly(2026, 3, 31), existing));
        Assert.Contains("nhiều NSX/HSD", ex.Message, StringComparison.Ordinal);
    }
}
