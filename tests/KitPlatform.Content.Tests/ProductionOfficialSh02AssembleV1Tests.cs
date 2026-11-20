using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialSh02AssembleV1Tests
{
    [Fact]
    public void Official_sh02_assemble_is_offline_safe()
    {
        var fail = ProductionOfficialSh02AssembleV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
