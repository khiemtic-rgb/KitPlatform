using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoHttpCompositionV1Tests
{
    [Fact]
    public void Official_veo_http_composition_is_complete_and_offline()
    {
        var fail = ProductionOfficialVeoHttpCompositionV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
