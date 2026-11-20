using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01RealFilmRunV2Tests
{
    [Fact]
    public void Real_film_run_v2_official_execute_is_one_shot()
    {
        var fail = ProductionEp01Sh01RealFilmRunV2Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
