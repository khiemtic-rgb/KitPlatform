using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialVeoExecutionAuthorizationV1Tests
{
    [Fact]
    public void Official_veo_execution_authorization_is_complete_and_offline()
    {
        var fail = ProductionOfficialVeoExecutionAuthorizationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
