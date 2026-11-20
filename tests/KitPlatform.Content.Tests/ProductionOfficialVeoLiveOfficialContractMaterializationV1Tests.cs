using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoLiveOfficialContractMaterializationV1Tests
{
    [Fact]
    public void Official_veo_live_official_contract_materialization_is_canonical_and_offline_safe()
    {
        var fail = ProductionOfficialVeoLiveOfficialContractMaterializationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
