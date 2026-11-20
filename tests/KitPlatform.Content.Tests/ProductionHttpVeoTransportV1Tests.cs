using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionHttpVeoTransportV1Tests
{
    [Fact]
    public void Http_veo_transport_is_offline_and_vertex_shaped()
    {
        var fail = ProductionHttpVeoTransportV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
