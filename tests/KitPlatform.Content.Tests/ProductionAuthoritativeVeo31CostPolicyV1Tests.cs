using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionAuthoritativeVeo31CostPolicyV1Tests
{
    [Fact]
    public void Authoritative_veo31_cost_policy_is_known_and_offline()
    {
        var fail = ProductionAuthoritativeVeo31CostPolicyV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
