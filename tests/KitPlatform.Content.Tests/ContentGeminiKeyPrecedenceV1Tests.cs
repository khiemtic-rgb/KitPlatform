using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ContentGeminiKeyPrecedenceV1Tests
{
    [Fact]
    public void Secret_reference_wins_over_a_stored_gemini_key()
    {
        var fail = ContentGeminiKeyPrecedenceV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
