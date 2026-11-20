using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoRealVertexStartV1Tests
{
    [Fact]
    public void Official_veo_real_vertex_start_is_one_shot_and_offline_safe()
    {
        var fail = ProductionOfficialVeoRealVertexStartV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
