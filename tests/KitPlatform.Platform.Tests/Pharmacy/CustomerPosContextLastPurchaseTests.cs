using KitPlatform.Packs.Pharmacy.Infrastructure.Sales;
using KitPlatform.Packs.Pharmacy.Sales;
using Xunit;

namespace KitPlatform.Platform.Tests.Pharmacy;

public class CustomerPosContextLastPurchaseTests
{
    [Fact]
    public void LatestPurchaseDate_Empty_IsNull()
    {
        Assert.Null(CustomerPosContextService.LatestPurchaseDate([]));
    }

    [Fact]
    public void LatestPurchaseDate_UsesNewestOrder_RegardlessOfListOrder()
    {
        var older = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
        var newer = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);
        var orders = new[]
        {
            Order(older, "SO-1"),
            Order(newer, "SO-2"),
        };

        Assert.Equal(newer, CustomerPosContextService.LatestPurchaseDate(orders));
    }

    [Fact]
    public void LatestPurchaseDate_SingleOrder_UsesThatDate()
    {
        var only = new DateTimeOffset(2026, 9, 2, 11, 0, 0, TimeSpan.Zero);

        Assert.Equal(only, CustomerPosContextService.LatestPurchaseDate([Order(only, "SO-1")]));
    }

    [Fact]
    public void LatestPurchaseDate_TiedTimestamps_ReturnsThatTimestamp()
    {
        var tied = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);
        var orders = new[]
        {
            Order(tied, "SO-A"),
            Order(tied, "SO-B"),
        };

        Assert.Equal(tied, CustomerPosContextService.LatestPurchaseDate(orders));
    }

    private static CustomerPosOrderDto Order(DateTimeOffset date, string number) =>
        new(date, number, 10m, []);
}
