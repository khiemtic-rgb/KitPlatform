using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialCreateVideoFreezeV1Tests
{
    [Fact]
    public void Official_create_video_freeze_is_idempotent_and_offline()
    {
        var fail = ProductionOfficialCreateVideoFreezeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
