namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Per-family late star tier thresholds and award/penalty percentages of <c>star_reward</c>.
/// </summary>
public sealed record FamilyStarTierSettings(
    int LateT1Minutes = 30,
    int LateT2Minutes = 60,
    int LateT3Minutes = 90,
    int LateHalfPct = 50,
    int LateZeroPct = 0,
    int LatePenaltyHalfPct = -50,
    int LatePenaltyFullPct = -100)
{
    public static FamilyStarTierSettings Default { get; } = new();
}

public sealed record FamilyStarSettingsDto(
    int LateT1Minutes,
    int LateT2Minutes,
    int LateT3Minutes,
    int LateHalfPct,
    int LateZeroPct,
    int LatePenaltyHalfPct,
    int LatePenaltyFullPct,
    bool IsConfigured);

public sealed record UpdateFamilyStarSettingsRequest(
    int LateT1Minutes,
    int LateT2Minutes,
    int LateT3Minutes,
    int LateHalfPct,
    int LateZeroPct,
    int LatePenaltyHalfPct,
    int LatePenaltyFullPct);

public interface IFamilyStarSettingsService
{
    Task<FamilyStarSettingsDto> GetSettingsAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task<FamilyStarSettingsDto> UpsertSettingsAsync(
        Guid familyId,
        UpdateFamilyStarSettingsRequest request,
        CancellationToken cancellationToken = default);
}
