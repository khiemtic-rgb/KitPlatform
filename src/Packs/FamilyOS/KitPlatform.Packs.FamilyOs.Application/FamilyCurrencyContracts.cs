namespace KitPlatform.Packs.FamilyOs;

public static class FamilyCurrencyStarKinds
{
    public const string Growth = "growth";
    public const string Responsibility = "responsibility";
    public const string Kindness = "kindness";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Growth, Responsibility, Kindness,
    };

    public static string LabelVi(string? kind) =>
        (kind ?? "").Trim().ToLowerInvariant() switch
        {
            Growth => "Sao Phát triển",
            Responsibility => "Sao Trách nhiệm",
            Kindness => "Sao Tử tế",
            _ => "Sao",
        };

    public static string Normalize(string? kind) =>
        All.Contains(kind ?? "")
            ? (kind ?? Growth).Trim().ToLowerInvariant()
            : Growth;
}

public static class FamilyCurrencyCategories
{
    public const string Growth = "growth";
    public const string Responsibility = "responsibility";
    public const string Kindness = "kindness";
    public const string Cue = "cue";
    public const string Play = "play";
    public const string Duty = "duty";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Growth, Responsibility, Kindness, Cue, Play, Duty,
    };
}

public static class FamilyCurrencyAgeBands
{
    public const string Age6To10 = "6_10";
    public const string Age11To15 = "11_15";
    public const string Age16To18 = "16_18";
    public const string Custom = "custom";

    public static string FromAgeYears(int ageYears) =>
        ageYears switch
        {
            <= 10 => Age6To10,
            <= 15 => Age11To15,
            _ => Age16To18,
        };

    public static string FromDateOfBirth(DateOnly? dob, DateOnly today)
    {
        if (dob is null)
            return Age11To15;
        var age = today.Year - dob.Value.Year;
        if (dob.Value > today.AddYears(-age))
            age--;
        return FromAgeYears(Math.Clamp(age, 0, 25));
    }
}

public sealed record FamilyCurrencyCategoryWeight(
    string Code,
    string LabelVi,
    int BudgetPct,
    string DefaultStarKind,
    int? MaxStarsPerEvent = null);

public sealed record FamilyCurrencyDutyRule(
    IReadOnlyList<string> Match,
    bool FormationStarsEnabled,
    IReadOnlyList<string> FormationOnlyStages,
    int FormationMaxStars,
    string FormationStarKind);

public sealed record FamilyCurrencyConfig(
    string PresetId,
    IReadOnlyDictionary<string, int> BudgetByAgeBand,
    IReadOnlyList<FamilyCurrencyCategoryWeight> CategoryWeights,
    IReadOnlyList<FamilyCurrencyDutyRule> DutyRules,
    IReadOnlyDictionary<string, double> HabitStageMultipliers,
    int StretchBonusStars,
    int InitiativeBonusStars,
    int StretchOverflowMaxPctOfBudget,
    string GraduateCopyVi);

public sealed record FamilyCurrencySettingsDto(
    bool Enabled,
    string PresetId,
    string? AgeBand,
    int? DailyBudgetOverride,
    int ResolvedDailyBudget,
    string ResolvedAgeBand,
    FamilyCurrencyConfig Config,
    bool IsConfigured);

public sealed record UpdateFamilyCurrencySettingsRequest(
    bool Enabled,
    string? PresetId,
    string? AgeBand,
    int? DailyBudgetOverride);

public sealed record MemberStarBalancesDto(
    Guid MemberId,
    int Total,
    int Growth,
    int Responsibility,
    int Kindness);

public sealed record FamilyBadgeDto(
    Guid Id,
    string Code,
    string LabelVi,
    DateTimeOffset AwardedAt);

public interface IFamilyCurrencySettingsService
{
    Task<FamilyCurrencySettingsDto> GetSettingsAsync(
        Guid familyId,
        Guid? memberId = null,
        CancellationToken cancellationToken = default);

    Task<FamilyCurrencySettingsDto> UpsertSettingsAsync(
        Guid familyId,
        UpdateFamilyCurrencySettingsRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyCurrencySettingsDto> ApplyPresetAsync(
        Guid familyId,
        string presetId = FamilyCurrencyPreset.BalancedV1Id,
        CancellationToken cancellationToken = default);
}

public interface IFamilyBadgeService
{
    Task<IReadOnlyList<FamilyBadgeDto>> ListMemberBadgesAsync(
        Guid familyId,
        Guid memberId,
        CancellationToken cancellationToken = default);

    Task EnsureSeedBadgesAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);

    Task EvaluateAfterCommitmentDoneAsync(
        Guid familyId,
        Guid memberId,
        Guid commitmentId,
        string title,
        string? currencyCategory,
        int habitStreakDays,
        CancellationToken cancellationToken = default);
}
