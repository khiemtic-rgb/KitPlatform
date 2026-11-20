using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionRealVertexExecutionV1Tests
{
    [Fact]
    public void Real_vertex_execution_v1_stops_before_network()
    {
        var fail = ProductionRealVertexExecutionV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
