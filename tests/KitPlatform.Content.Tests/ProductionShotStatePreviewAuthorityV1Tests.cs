using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionShotStatePreviewAuthorityV1Tests
{
    [Fact]
    public void Shot_state_and_preview_authority_T01_through_T44()
    {
        var fail = ProductionShotStatePreviewAuthorityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
