using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoLiveOfficialContractBindV1Tests
{
    [Fact]
    public void Official_veo_live_official_contract_bind_is_find_only_and_offline_safe()
    {
        var fail = ProductionOfficialVeoLiveOfficialContractBindV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
