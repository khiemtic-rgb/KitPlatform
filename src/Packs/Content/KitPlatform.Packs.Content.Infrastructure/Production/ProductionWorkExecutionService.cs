using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ProductionWorkExecutionService : IProductionWorkExecutionService
{
    public ProductionWorkExecutionService(IProductionWorkStore store) => _ = store;

    public IReadOnlyList<string> RunRegression() => ProductionWorkExecutionV1Regression.Run();
}
