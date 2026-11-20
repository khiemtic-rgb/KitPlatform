using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialSh02CreateMixV1Tests
{
    [Fact]
    public void Official_sh02_create_mix_compose_segments_is_offline_safe()
    {
        var fail = ProductionOfficialSh02CreateMixV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
