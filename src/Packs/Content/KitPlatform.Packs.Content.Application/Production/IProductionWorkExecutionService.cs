namespace KitPlatform.Packs.Content.Production;

public interface IProductionWorkExecutionService
{
    IReadOnlyList<string> RunRegression();
}
