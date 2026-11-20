using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ShortProductionQualityPipelineV1Tests
{
    [Fact]
    public void Quality_pipeline_keeps_technical_validity_distinct_from_visual_acceptance()
    {
        var fail = ShortProductionQualityPipelineV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
