using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionRealVertexExecutionPreflightV1Tests
{
    [Fact]
    public void Real_vertex_preflight_is_deterministic_and_closed()
    {
        var fail = ProductionRealVertexExecutionPreflightV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
