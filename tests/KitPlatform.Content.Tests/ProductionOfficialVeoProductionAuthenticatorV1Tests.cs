using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoProductionAuthenticatorV1Tests
{
    [Fact]
    public void Official_veo_production_authenticator_is_composed_and_offline()
    {
        var fail = ProductionOfficialVeoProductionAuthenticatorV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
