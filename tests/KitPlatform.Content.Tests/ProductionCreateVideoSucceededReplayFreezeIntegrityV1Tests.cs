using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCreateVideoSucceededReplayFreezeIntegrityV1Tests
{
    [Fact]
    public void Succeeded_replay_must_assert_frozen_match_before_returning_old_artifact()
    {
        var fail = ProductionCreateVideoSucceededReplayFreezeIntegrityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
