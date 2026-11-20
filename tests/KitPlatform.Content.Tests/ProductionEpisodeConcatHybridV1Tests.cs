using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEpisodeConcatHybridV1Tests
{
    [Fact]
    public async Task Hybrid_concat_preserves_presented_ten_second_clock()
    {
        var result = await ProductionEpisodeConcatHybridV1Regression.RunAsync();
        Assert.True(result.Failures.Count == 0, string.Join("; ", result.Failures) + " || " + result.Proof);
    }
}
