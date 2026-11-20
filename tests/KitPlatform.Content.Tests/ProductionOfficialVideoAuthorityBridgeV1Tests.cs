using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVideoAuthorityBridgeV1Tests
{
    [Fact]
    public void Official_video_authority_bridge_is_offline_safe()
    {
        var fail = ProductionOfficialVideoAuthorityBridgeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
