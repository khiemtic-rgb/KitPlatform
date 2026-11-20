using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionOfficialCreateVideoPermitAuthorizationV1Tests
{
    [Fact]
    public void Official_create_video_permit_is_issued_once_and_offline()
    {
        var fail = ProductionOfficialCreateVideoPermitAuthorizationV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
