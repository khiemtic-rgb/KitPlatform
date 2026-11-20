using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class OfficialPreviewIntegrityV1Tests
{
    [Fact]
    public void Official_preview_requires_os_artifact_and_matching_sha()
    {
        var fail = OfficialPreviewIntegrityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
