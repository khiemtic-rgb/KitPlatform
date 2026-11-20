using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01RealFilmRunV1LipsyncTests
{
    [Fact]
    public void Real_film_run_v1_lipsync_recover_does_not_start_again()
    {
        var fail = ProductionEp01Sh01RealFilmRunV1LipsyncRegression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
