using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class SeriesGraphMediaWriteCutoverV1Tests
{
    [Fact]
    public void Graph_media_write_cutover_preserves_historical_media()
    {
        var fail = SeriesGraphMediaWriteCutoverV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
