using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCreateVoiceCutoverV1Tests
{
    [Fact]
    public void Create_voice_cutover_T01_through_T42()
    {
        var fail = ProductionCreateVoiceCutoverV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
