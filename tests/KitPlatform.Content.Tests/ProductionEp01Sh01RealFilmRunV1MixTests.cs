using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01RealFilmRunV1MixTests
{
    [Fact]
    public void Real_film_run_v1_mix_keeps_sh01_canon_and_blocks_sh02_without_video()
    {
        var fail = ProductionEp01Sh01RealFilmRunV1MixRegression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Mix_contract_v1_compose_segments_is_deterministic()
    {
        var fail = ProductionMixContractV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
