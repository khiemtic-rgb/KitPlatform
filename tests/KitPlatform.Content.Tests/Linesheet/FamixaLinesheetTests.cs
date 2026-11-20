using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests.Linesheet;

public sealed class FamixaLinesheetTests
{
    [Fact]
    public void Linesheet_core_regression_passes() =>
        Assert.Empty(FamixaLinesheetV1Regression.Run());

    [Fact]
    public void Linesheet_ep01_matches_reference_renderer() =>
        Assert.Empty(FamixaLinesheetEp01Golden.Check(Path.Combine(AppContext.BaseDirectory, "Linesheet", "Fixtures")));
}
