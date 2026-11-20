using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialSh02CreateLipsyncV1Tests
{
    [Fact]
    public void Official_sh02_create_lipsync_is_offline_safe()
    {
        var fail = ProductionOfficialSh02CreateLipsyncV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
