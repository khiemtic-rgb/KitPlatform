using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionRealVertexExecutionReadinessV1Tests
{
    [Fact]
    public void Real_vertex_execution_readiness_is_closed_and_offline()
    {
        var fail = ProductionRealVertexExecutionReadinessV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
