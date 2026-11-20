using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoPaidNetworkAuthorizationV1Tests
{
    [Fact]
    public void Official_veo_paid_network_is_scoped_and_offline()
    {
        var fail = ProductionOfficialVeoPaidNetworkAuthorizationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
