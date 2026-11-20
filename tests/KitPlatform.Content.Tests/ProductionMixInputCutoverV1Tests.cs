using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionMixInputCutoverV1Tests
{
    [Fact]
    public void Mix_input_cutover_T01_through_T36()
    {
        var fail = ProductionMixInputCutoverV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
