namespace KitPlatform.Packs.FamilyOs;

public sealed record RewardCatalogItemDto(
    Guid Id,
    string Title,
    string Icon,
    int? Cost,
    string? Tone,
    bool IsSpecial,
    int SortOrder,
    string? Description,
    bool Active);

public sealed record UpsertRewardCatalogRequest(
    string Title,
    string Icon,
    int Cost,
    string? Description,
    string? Tone,
    int SortOrder);

public sealed record RewardRedemptionDto(
    Guid Id,
    Guid CatalogId,
    string Title,
    string Icon,
    int StarCost,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FulfilledAt);

public sealed record RewardRedeemRequest(Guid CatalogId);

public sealed record RewardRedeemResultDto(
    int Balance,
    RewardRedemptionDto Redemption);

public sealed record RewardFulfillRequest(Guid FulfilledBy);

public interface IFamilyRewardService
{
    Task<IReadOnlyList<RewardCatalogItemDto>> GetCatalogAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RewardRedemptionDto>> ListRedemptionsAsync(
        Guid familyId,
        Guid? memberId = null,
        CancellationToken cancellationToken = default);

    Task<RewardRedeemResultDto> RedeemAsync(
        Guid familyId,
        Guid memberId,
        RewardRedeemRequest request,
        CancellationToken cancellationToken = default);

    Task<RewardRedemptionDto> FulfillAsync(
        Guid familyId,
        Guid redemptionId,
        RewardFulfillRequest request,
        CancellationToken cancellationToken = default);

    Task<RewardCatalogItemDto> CreateCatalogItemAsync(
        Guid familyId,
        UpsertRewardCatalogRequest request,
        CancellationToken cancellationToken = default);

    Task<RewardCatalogItemDto> UpdateCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        UpsertRewardCatalogRequest request,
        CancellationToken cancellationToken = default);

    Task DeactivateCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        CancellationToken cancellationToken = default);
}
