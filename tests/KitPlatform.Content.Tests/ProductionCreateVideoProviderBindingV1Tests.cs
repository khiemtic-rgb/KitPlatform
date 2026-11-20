using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCreateVideoProviderBindingV1Tests
{
    [Fact]
    public void Official_active_selection_binds_and_unknown_provider_fails_closed()
    {
        var fail = ProductionCreateVideoProviderBindingV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
