using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoVideoTakeAcceptanceV1Tests
{
    [Fact]
    public void Official_veo_video_take_acceptance_is_offline_safe()
    {
        var fail = ProductionOfficialVeoVideoTakeAcceptanceV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
