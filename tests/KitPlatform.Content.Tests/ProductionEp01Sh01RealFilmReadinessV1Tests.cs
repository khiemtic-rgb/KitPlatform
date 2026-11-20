using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01RealFilmReadinessV1Tests
{
    [Fact]
    public void Real_film_readiness_T01_through_T30()
    {
        var fail = ProductionEp01Sh01RealFilmReadinessV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
