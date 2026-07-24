using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyStarSettingsService : IFamilyStarSettingsService
{
    private readonly FamilyStarSettingsRepository _settings;
    private readonly FamilyGraphRepository _families;

    public FamilyStarSettingsService(
        FamilyStarSettingsRepository settings,
        FamilyGraphRepository families)
    {
        _settings = settings;
        _families = families;
    }

    public async Task<FamilyStarSettingsDto> GetSettingsAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var row = await _settings.GetAsync(familyId, cancellationToken);
        if (row is null)
            return MapDefaults(isConfigured: false);

        return MapRow(row, isConfigured: true);
    }

    public async Task<FamilyStarSettingsDto> UpsertSettingsAsync(
        Guid familyId,
        UpdateFamilyStarSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var tier = ValidateAndMap(request);
        var row = await _settings.UpsertAsync(familyId, tier, cancellationToken);
        return MapRow(row, isConfigured: true);
    }

    internal static FamilyStarTierSettings ResolveTierSettings(FamilyStarSettingsRepository.FamilyStarSettingsRow? row) =>
        row is null
            ? FamilyStarTierSettings.Default
            : new FamilyStarTierSettings(
                row.LateT1Minutes,
                row.LateT2Minutes,
                row.LateT3Minutes,
                row.LateHalfPct,
                row.LateZeroPct,
                row.LatePenaltyHalfPct,
                row.LatePenaltyFullPct);

    private async Task EnsureFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        if (family is null)
            throw new InvalidOperationException("Không tìm thấy gia đình.");
    }

    private static FamilyStarTierSettings ValidateAndMap(UpdateFamilyStarSettingsRequest request)
    {
        if (request.LateT1Minutes <= 0
            || request.LateT2Minutes <= request.LateT1Minutes
            || request.LateT3Minutes <= request.LateT2Minutes)
        {
            throw new InvalidOperationException(
                "Ngưỡng muộn phải tăng dần: T1 > 0, T2 > T1, T3 > T2 (phút).");
        }

        if (request.LateHalfPct is < 0 or > 100)
            throw new InvalidOperationException("Hệ số nửa sao thưởng phải từ 0 đến 100%.");

        if (request.LateZeroPct != 0)
            throw new InvalidOperationException("Tầng không thưởng luôn là 0%.");

        if (request.LatePenaltyHalfPct is > -1 or < -100)
            throw new InvalidOperationException("Hệ số phạt nửa sao phải từ -100 đến -1%.");

        if (request.LatePenaltyFullPct != -100)
            throw new InvalidOperationException("Hệ số phạt full sao luôn là -100%.");

        return new FamilyStarTierSettings(
            request.LateT1Minutes,
            request.LateT2Minutes,
            request.LateT3Minutes,
            request.LateHalfPct,
            request.LateZeroPct,
            request.LatePenaltyHalfPct,
            request.LatePenaltyFullPct);
    }

    private static FamilyStarSettingsDto MapDefaults(bool isConfigured)
    {
        var d = FamilyStarTierSettings.Default;
        return new FamilyStarSettingsDto(
            d.LateT1Minutes,
            d.LateT2Minutes,
            d.LateT3Minutes,
            d.LateHalfPct,
            d.LateZeroPct,
            d.LatePenaltyHalfPct,
            d.LatePenaltyFullPct,
            isConfigured);
    }

    private static FamilyStarSettingsDto MapRow(
        FamilyStarSettingsRepository.FamilyStarSettingsRow row,
        bool isConfigured) =>
        new(
            row.LateT1Minutes,
            row.LateT2Minutes,
            row.LateT3Minutes,
            row.LateHalfPct,
            row.LateZeroPct,
            row.LatePenaltyHalfPct,
            row.LatePenaltyFullPct,
            isConfigured);
}
