using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionWorkExecutionV1Tests
{
    [Fact]
    public void Work_execution_control_plane_T01_through_T24()
    {
        var fail = ProductionWorkExecutionV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
