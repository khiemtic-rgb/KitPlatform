using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialSh02CreateVoiceV1Tests
{
    [Fact]
    public void Official_sh02_create_voice_is_offline_safe()
    {
        var fail = ProductionOfficialSh02CreateVoiceV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
