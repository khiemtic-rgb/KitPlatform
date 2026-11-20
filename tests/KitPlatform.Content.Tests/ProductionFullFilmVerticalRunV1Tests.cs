using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionFullFilmVerticalRunV1Tests
{
    [Fact]
    public void Full_film_vertical_run_T01_through_T53()
    {
        var fail = ProductionFullFilmVerticalRunV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
