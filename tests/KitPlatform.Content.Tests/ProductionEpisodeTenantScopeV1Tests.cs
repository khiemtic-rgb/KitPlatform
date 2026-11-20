using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEpisodeTenantScopeV1Tests
{
    [Fact]
    public void Tenant_owned_ep01_scope_does_not_use_the_historical_build()
    {
        var fail = ProductionEpisodeTenantScopeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
