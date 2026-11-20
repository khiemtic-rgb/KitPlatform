using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01ProductionIngressV1Tests
{
    [Fact]
    public void Production_ingress_v1_still_passes()
    {
        var fail = ProductionEp01Sh01ProductionIngressV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
