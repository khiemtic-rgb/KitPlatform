using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCostOfficialPermitAuthorizationV1Tests
{
    [Fact]
    public void Cost_and_official_permit_authorization_is_fail_closed_and_offline()
    {
        var fail = ProductionCostOfficialPermitAuthorizationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
