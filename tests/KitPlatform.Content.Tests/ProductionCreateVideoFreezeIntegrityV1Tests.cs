using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCreateVideoFreezeIntegrityV1Tests
{
    [Fact]
    public void Freeze_integrity_v1_twelve_fields_fail_closed()
    {
        var fail = ProductionCreateVideoFreezeIntegrityV1Regression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
