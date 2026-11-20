using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionVeoMotionAdapterV1Tests
{
    [Fact]
    public void Veo_motion_adapter_v1_uses_frozen_input_and_fake_transport_only()
    {
        var fail = ProductionVeoMotionAdapterV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
