using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Loyalty;
using KitPlatform.Infrastructure.Data;

namespace KitPlatform.Infrastructure.Loyalty;

internal sealed class LoyaltyAdminService : ILoyaltyAdminService
{
    private readonly LoyaltyAdminRepository _repo;
    private readonly IDbConnectionFactory _db;
    private readonly ITenantContext _tenant;

    public LoyaltyAdminService(
        LoyaltyAdminRepository repo,
        IDbConnectionFactory db,
        ITenantContext tenant)
    {
        _repo = repo;
        _db = db;
        _tenant = tenant;
    }

    public async Task<LoyaltyAdminSettingsDto> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenant.TenantId;
        var enabled = await _repo.GetLoyaltyEnabledAsync(tenantId, cancellationToken);
        var program = await _repo.GetDefaultProgramAsync(tenantId, cancellationToken);

        if (program is null)
        {
            return new LoyaltyAdminSettingsDto(enabled, null);
        }

        var tiers = await _repo.GetTiersAsync(program.Id, cancellationToken);
        return new LoyaltyAdminSettingsDto(enabled, MapProgram(program, tiers));
    }

    public async Task<LoyaltyAdminSettingsDto> SaveSettingsAsync(
        UpdateLoyaltyAdminSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var tenantId = _tenant.TenantId;
        var programRequest = request.Program;

        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await _repo.SetLoyaltyEnabledAsync(tenantId, request.LoyaltyEnabled, conn, tx);

        var existing = await _repo.GetDefaultProgramAsync(tenantId, cancellationToken, conn, tx);
        var programStatus = ResolveProgramStatus(request.LoyaltyEnabled, programRequest.Status);

        Guid programId;
        if (existing is null)
        {
            programId = await _repo.InsertProgramAsync(
                tenantId,
                programRequest.ProgramCode.Trim(),
                programRequest.ProgramName.Trim(),
                programRequest.PointsPerAmount,
                programRequest.AmountPerPoint,
                programRequest.MaxRedeemPercent,
                programStatus,
                conn,
                tx);
        }
        else
        {
            programId = existing.Id;
            await _repo.UpdateProgramAsync(
                programId,
                tenantId,
                programRequest.ProgramName.Trim(),
                programRequest.PointsPerAmount,
                programRequest.AmountPerPoint,
                programRequest.MaxRedeemPercent,
                programStatus,
                conn,
                tx);
        }

        var existingTierIds = await _repo.GetTierIdsAsync(programId, conn, tx);
        var keptTierIds = new HashSet<Guid>();

        var orderedTiers = programRequest.Tiers
            .Select((tier, index) => (tier, index))
            .OrderBy(x => x.tier.SortOrder)
            .ThenBy(x => x.index)
            .Select(x => x.tier)
            .ToList();

        for (var i = 0; i < orderedTiers.Count; i++)
        {
            var tier = orderedTiers[i];
            await _repo.UpsertTierAsync(
                programId,
                tier.Id,
                tier.TierCode.Trim(),
                tier.TierName.Trim(),
                tier.MinPoints,
                tier.DiscountPercent,
                tier.SortOrder > 0 ? tier.SortOrder : i + 1,
                conn,
                tx);

            if (tier.Id is Guid tierId)
                keptTierIds.Add(tierId);
        }

        foreach (var tierId in existingTierIds)
        {
            if (keptTierIds.Contains(tierId))
                continue;

            if (await _repo.IsTierInUseAsync(tierId, conn, tx))
            {
                throw new InvalidOperationException(
                    "Không xóa được hạng đang được gán cho khách hàng. Hãy chuyển khách sang hạng khác trước.");
            }

            await _repo.DeleteTierAsync(tierId, programId, conn, tx);
        }

        await tx.CommitAsync(cancellationToken);
        return await GetSettingsAsync(cancellationToken);
    }

    private static short ResolveProgramStatus(bool loyaltyEnabled, short requestedStatus) =>
        loyaltyEnabled ? (short)1 : requestedStatus is 1 or 2 ? requestedStatus : (short)0;

    private static LoyaltyProgramAdminDto MapProgram(
        LoyaltyProgramRow program,
        IReadOnlyList<LoyaltyTierRow> tiers) =>
        new(
            program.Id,
            program.ProgramCode,
            program.ProgramName,
            program.PointsPerAmount,
            program.AmountPerPoint,
            program.MaxRedeemPercent,
            program.Status,
            tiers.Select(t => new LoyaltyTierAdminDto(
                t.Id,
                t.TierCode,
                t.TierName,
                t.MinPoints,
                t.DiscountPercent,
                t.SortOrder)).ToList());

    private static void ValidateRequest(UpdateLoyaltyAdminSettingsRequest request)
    {
        var program = request.Program;
        if (string.IsNullOrWhiteSpace(program.ProgramCode))
            throw new InvalidOperationException("Mã chương trình không được để trống.");
        if (string.IsNullOrWhiteSpace(program.ProgramName))
            throw new InvalidOperationException("Tên chương trình không được để trống.");
        if (program.PointsPerAmount <= 0)
            throw new InvalidOperationException("Số tiền quy đổi điểm phải lớn hơn 0.");
        if (program.AmountPerPoint <= 0)
            throw new InvalidOperationException("Giá trị mỗi điểm phải lớn hơn 0.");
        if (program.MaxRedeemPercent is < 0 or > 100)
            throw new InvalidOperationException("Tỷ lệ trừ tối đa trên đơn phải từ 0 đến 100%.");

        if (program.Tiers.Count == 0)
            throw new InvalidOperationException("Cần ít nhất một hạng thành viên.");

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var prevMin = -1;
        foreach (var tier in program.Tiers.OrderBy(t => t.SortOrder).ThenBy(t => t.MinPoints))
        {
            if (string.IsNullOrWhiteSpace(tier.TierCode) || string.IsNullOrWhiteSpace(tier.TierName))
                throw new InvalidOperationException("Mã và tên hạng không được để trống.");
            if (!codes.Add(tier.TierCode.Trim()))
                throw new InvalidOperationException($"Trùng mã hạng: {tier.TierCode}");
            if (tier.MinPoints < 0)
                throw new InvalidOperationException("Ngưỡng điểm hạng không được âm.");
            if (tier.MinPoints < prevMin)
                throw new InvalidOperationException("Ngưỡng điểm hạng phải tăng dần.");
            if (tier.DiscountPercent is < 0 or > 100)
                throw new InvalidOperationException("Phần trăm giảm giá hạng phải từ 0 đến 100.");
            prevMin = tier.MinPoints;
        }
    }
}
