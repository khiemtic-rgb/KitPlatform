using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionAiCatalogApiV1Tests
{
    [Fact]
    public void Production_ai_catalog_api_v1_regression_passes()
    {
        var fail = ProductionAiCatalogApiV1Regression.Run();
        Assert.Empty(fail);
    }
}
