using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEpisodeAssemblyV1Tests
{
    [Fact]
    public void Episode_assembly_contract_is_offline_safe()
    {
        var fail = ProductionEpisodeAssemblyV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
