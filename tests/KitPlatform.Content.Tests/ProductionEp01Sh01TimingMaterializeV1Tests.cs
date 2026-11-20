using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01TimingMaterializeV1Tests
{
    [Fact]
    public void Materialize_sh01_timing_T01_through_T24()
    {
        var fail = ProductionEp01Sh01TimingMaterializeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
