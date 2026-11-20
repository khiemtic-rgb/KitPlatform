using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ShotProductionOutcomeV1Tests
{
    [Fact]
    public void Isolated_tenant_shot_outcome_keeps_technical_and_visual_decisions_apart()
    {
        var fail = ShotProductionOutcomeV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
