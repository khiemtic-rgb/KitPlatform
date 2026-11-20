using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ProductionEp01Sh01RealExecutionPermitV1Tests
{
    [Fact]
    public void Run()
    {
        var fail = ProductionEp01Sh01RealExecutionPermitV1Regression.Run();
        Assert.Empty(fail);
    }
}
