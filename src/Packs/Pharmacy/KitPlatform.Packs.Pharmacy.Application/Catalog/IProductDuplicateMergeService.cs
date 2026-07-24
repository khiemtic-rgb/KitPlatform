namespace KitPlatform.Packs.Pharmacy.Catalog;

public interface IProductDuplicateMergeService
{
    Task<DuplicateProductClustersResult> GetDuplicateClustersAsync(CancellationToken cancellationToken = default);

    /// <summary>Clusters of products with pg_trgm similarity ≥ threshold and different normalized names (import near-duplicates).</summary>
    Task<DuplicateProductClustersResult> GetSimilarClustersAsync(
        double similarityThreshold = 0.8,
        CancellationToken cancellationToken = default);

    Task<MergeDuplicateProductStockResult> MergeStockAsync(        MergeDuplicateProductStockRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductMergeHistoryResult> GetMergeHistoryAsync(
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task<HiddenProductsResult> GetHiddenProductsAsync(
        int limit = 500,
        CancellationToken cancellationToken = default);

    Task<bool> RestoreHiddenProductAsync(Guid productId, CancellationToken cancellationToken = default);

    Task<HardDeleteProductResult> HardDeleteHiddenProductAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
