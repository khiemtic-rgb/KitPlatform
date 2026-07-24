namespace KitPlatform.Packs.Pharmacy.Catalog;

public sealed record DuplicateProductMemberDto(
    Guid Id,
    string ProductCode,
    string ProductName,
    string UnitName,
    decimal TotalQuantity,
    int WarehouseCount,
    bool SuggestedKeeper);

public sealed record DuplicateProductClusterDto(
    string NormalizedName,
    string DisplayName,
    IReadOnlyList<DuplicateProductMemberDto> Products,
    /// <summary>Null = exact normalized-name cluster; otherwise max pairwise similarity in the group.</summary>
    double? MaxSimilarity = null);

public sealed record DuplicateProductClustersResult(
    IReadOnlyList<DuplicateProductClusterDto> Clusters,
    int ClusterCount,
    int ProductCount,
    /// <summary>Threshold used when building fuzzy clusters (e.g. 0.8); null for exact-name clusters.</summary>
    double? SimilarityThreshold = null);

public sealed record MergeDuplicateProductStockRequest(
    Guid KeeperProductId,
    Guid SourceProductId,
    /// <summary>1 đơn vị của mã nguồn = bao nhiêu đơn vị của mã giữ (vd 1 hộp = 10 vỉ → 10).</summary>
    decimal ConversionFactor,
    bool SoftDeleteSource = true,
    string? Reason = null);

public sealed record MergeDuplicateStockLineDto(
    Guid WarehouseId,
    string WarehouseName,
    Guid SourceBatchId,
    string BatchNumber,
    Guid KeeperBatchId,
    decimal SourceQuantity,
    decimal KeeperQuantityAdded);

public sealed record MergeDuplicateProductStockResult(
    Guid KeeperProductId,
    Guid SourceProductId,
    decimal ConversionFactor,
    bool SourceSoftDeleted,
    decimal TotalSourceQuantity,
    decimal TotalKeeperQuantityAdded,
    IReadOnlyList<MergeDuplicateStockLineDto> Lines);

public sealed record ProductMergeHistoryItemDto(
    Guid MergeId,
    DateTimeOffset MergedAt,
    Guid? SourceProductId,
    string SourceProductCode,
    string SourceProductName,
    Guid? KeeperProductId,
    string KeeperProductCode,
    string KeeperProductName,
    decimal SourceQuantity,
    decimal KeeperQuantityAdded,
    string? Notes,
    bool SourceStillHidden);

public sealed record ProductMergeHistoryResult(
    IReadOnlyList<ProductMergeHistoryItemDto> Items,
    int Total);

public sealed record HiddenProductItemDto(
    Guid Id,
    string ProductCode,
    string ProductName,
    string UnitName,
    DateTimeOffset? DeletedAt,
    decimal RemainingStock,
    bool CanHardDelete,
    IReadOnlyList<string> BlockReasons,
    bool MergedAway);

public sealed record HiddenProductsResult(
    IReadOnlyList<HiddenProductItemDto> Items,
    int Total);

public sealed record HardDeleteProductResult(
    Guid ProductId,
    bool Deleted);
