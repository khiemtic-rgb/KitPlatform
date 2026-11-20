using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ShortProductionAcceptanceV1Tests
{
    [Fact]
    public void Short_acceptance_keeps_job_success_distinct_from_production_accept()
    {
        var fail = ShortProductionAcceptanceV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
