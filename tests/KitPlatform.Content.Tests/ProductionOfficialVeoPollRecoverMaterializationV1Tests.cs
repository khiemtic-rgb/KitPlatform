using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoPollRecoverMaterializationV1Tests
{
    [Fact]
    public void Official_veo_poll_recover_materialization_is_offline_safe()
    {
        var fail = ProductionOfficialVeoPollRecoverMaterializationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
