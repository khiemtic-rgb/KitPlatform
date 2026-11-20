using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCreateLipsyncInputCutoverV1Tests
{
    [Fact]
    public void Create_lipsync_input_cutover_T01_through_T51()
    {
        var fail = ProductionCreateLipsyncInputCutoverV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
