using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionLipsyncSpeechSourceV1Tests
{
    [Fact]
    public void Lipsync_uses_the_approved_speech_interval_and_the_mix_places_it()
    {
        var fail = ProductionLipsyncSpeechSourceV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
