using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyRewardService : IFamilyRewardService
{
    private readonly FamilyRewardRepository _rewards;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyCommercialService _commercial;

    public FamilyRewardService(
        FamilyRewardRepository rewards,
        FamilyGraphRepository families,
        IFamilyCommercialService commercial)
    {
        _rewards = rewards;
        _families = families;
        _commercial = commercial;
    }

    public async Task<IReadOnlyList<RewardCatalogItemDto>> GetCatalogAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        await EnsureDefaultCatalogAsync(familyId, cancellationToken);

        var rows = await _rewards.ListCatalogAsync(familyId, cancellationToken);
        return rows.Select(MapCatalog).ToList();
    }

    public async Task<IReadOnlyList<RewardRedemptionDto>> ListRedemptionsAsync(
        Guid familyId,
        Guid? memberId = null,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (memberId is not null)
        {
            var members = await _families.ListMembersAsync(familyId, cancellationToken);
            if (members.All(m => m.Id != memberId.Value))
                throw new InvalidOperationException("Thành viên không thuộc gia đình này.");
        }

        var rows = await _rewards.ListRedemptionsAsync(familyId, memberId, cancellationToken);
        return rows.Select(MapRedemption).ToList();
    }

    public async Task<RewardRedeemResultDto> RedeemAsync(
        Guid familyId,
        Guid memberId,
        RewardRedeemRequest request,
        CancellationToken cancellationToken = default)
    {
        await _commercial.EnsureEntitledAsync(familyId, cancellationToken);

        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var member = members.FirstOrDefault(m => m.Id == memberId)
            ?? throw new InvalidOperationException("Thành viên không thuộc gia đình này.");

        if (!string.Equals(member.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ con mới đổi quà từ Kho báu.");

        var item = await _rewards.GetCatalogItemAsync(familyId, request.CatalogId, cancellationToken)
            ?? throw new InvalidOperationException("Phần thưởng không tồn tại hoặc đã tắt.");

        if (item.IsSpecial || item.Cost is null)
            throw new InvalidOperationException("Phần thưởng này cần bố mẹ chọn — hãy để dành nhé!");

        var (_, _, balance) = await _rewards.RedeemAsync(
            familyId,
            memberId,
            request.CatalogId,
            item.Cost.Value,
            cancellationToken);

        var redemptions = await _rewards.ListRedemptionsAsync(familyId, memberId, cancellationToken);
        var latest = redemptions.FirstOrDefault()
            ?? throw new InvalidOperationException("Không lưu được lượt đổi quà.");

        return new RewardRedeemResultDto(balance, MapRedemption(latest));
    }

    public async Task<RewardRedemptionDto> FulfillAsync(
        Guid familyId,
        Guid redemptionId,
        RewardFulfillRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var members = await _families.ListMembersAsync(familyId, cancellationToken);
        var parent = members.FirstOrDefault(m => m.Id == request.FulfilledBy)
            ?? throw new InvalidOperationException("Người xác nhận không thuộc gia đình này.");

        if (string.Equals(parent.RoleCode, FamilyMembershipRoles.Child, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Chỉ bố mẹ mới xác nhận đổi quà.");

        var row = await _rewards.FulfillAsync(
            familyId,
            redemptionId,
            request.FulfilledBy,
            cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy lượt đổi quà đang chờ.");

        return MapRedemption(row);
    }

    public async Task<RewardCatalogItemDto> CreateCatalogItemAsync(
        Guid familyId,
        UpsertRewardCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCatalogUpsert(request);

        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        await EnsureDefaultCatalogAsync(familyId, cancellationToken);

        var id = await _rewards.InsertCatalogItemAsync(
            familyId,
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Icon) ? "🎁" : request.Icon.Trim(),
            request.Cost,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            string.IsNullOrWhiteSpace(request.Tone) ? null : request.Tone.Trim(),
            request.SortOrder,
            cancellationToken);

        var row = await _rewards.GetCatalogItemAsync(familyId, id, cancellationToken)
            ?? throw new InvalidOperationException("Không lưu được phần thưởng.");

        return MapCatalog(row);
    }

    public async Task<RewardCatalogItemDto> UpdateCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        UpsertRewardCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCatalogUpsert(request);

        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var existing = await _rewards.GetCatalogItemAsync(familyId, catalogId, cancellationToken)
            ?? throw new InvalidOperationException("Phần thưởng không tồn tại hoặc đã tắt.");

        if (existing.IsSpecial)
            throw new InvalidOperationException("Thưởng đội chỉ chỉnh qua seed hoặc Thỏa thuận nhà.");

        var ok = await _rewards.UpdateCatalogItemAsync(
            familyId,
            catalogId,
            request.Title.Trim(),
            string.IsNullOrWhiteSpace(request.Icon) ? "🎁" : request.Icon.Trim(),
            request.Cost,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            string.IsNullOrWhiteSpace(request.Tone) ? null : request.Tone.Trim(),
            request.SortOrder,
            cancellationToken);

        if (!ok)
            throw new InvalidOperationException("Không cập nhật được phần thưởng.");

        var row = await _rewards.GetCatalogItemAsync(familyId, catalogId, cancellationToken)
            ?? throw new InvalidOperationException("Phần thưởng không tồn tại hoặc đã tắt.");

        return MapCatalog(row);
    }

    public async Task DeactivateCatalogItemAsync(
        Guid familyId,
        Guid catalogId,
        CancellationToken cancellationToken = default)
    {
        if (await _families.GetFamilyAsync(familyId, cancellationToken) is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");

        var ok = await _rewards.DeactivateCatalogItemAsync(familyId, catalogId, cancellationToken);
        if (!ok)
            throw new InvalidOperationException("Phần thưởng không tồn tại hoặc không thể ẩn.");
    }

    private async Task EnsureDefaultCatalogAsync(Guid familyId, CancellationToken cancellationToken)
    {
        if (await _rewards.CountCatalogAsync(familyId, cancellationToken) == 0)
            await _rewards.InsertDefaultCatalogAsync(familyId, cancellationToken);
    }

    private static void ValidateCatalogUpsert(UpsertRewardCatalogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Tên phần thưởng không được để trống.");

        if (request.Cost <= 0)
            throw new InvalidOperationException("Giá sao phải lớn hơn 0.");

        if (request.SortOrder < 0)
            throw new InvalidOperationException("Thứ tự không hợp lệ.");
    }

    private static RewardCatalogItemDto MapCatalog(FamilyRewardRepository.CatalogRow row) =>
        new(
            row.Id,
            row.Title,
            row.Icon,
            row.Cost,
            row.Tone,
            row.IsSpecial,
            row.SortOrder,
            row.Description,
            row.Active);

    private static RewardRedemptionDto MapRedemption(FamilyRewardRepository.RedemptionRow row) =>
        new(
            row.Id,
            row.CatalogId,
            row.Title,
            row.Icon,
            row.StarCost,
            row.Status,
            row.CreatedAt,
            row.FulfilledAt);
}
