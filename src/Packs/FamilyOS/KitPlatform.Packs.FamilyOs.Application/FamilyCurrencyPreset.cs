namespace KitPlatform.Packs.FamilyOs;

/// <summary>Built-in Family Currency preset <c>balanced_v1</c> (SoT JSON under docs/novixa).</summary>
public static class FamilyCurrencyPreset
{
    public const string BalancedV1Id = "balanced_v1";

    public static FamilyCurrencyConfig BalancedV1 { get; } = new(
        PresetId: BalancedV1Id,
        BudgetByAgeBand: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [FamilyCurrencyAgeBands.Age6To10] = 20,
            [FamilyCurrencyAgeBands.Age11To15] = 30,
            [FamilyCurrencyAgeBands.Age16To18] = 40,
        },
        CategoryWeights:
        [
            new(FamilyCurrencyCategories.Growth, "Phát triển bản thân", 48, FamilyCurrencyStarKinds.Growth),
            new(FamilyCurrencyCategories.Responsibility, "Trách nhiệm", 28, FamilyCurrencyStarKinds.Responsibility),
            new(FamilyCurrencyCategories.Kindness, "Hành vi tích cực", 16, FamilyCurrencyStarKinds.Kindness),
            new(FamilyCurrencyCategories.Cue, "Động viên nhỏ", 6, FamilyCurrencyStarKinds.Responsibility),
            new(FamilyCurrencyCategories.Play, "Giải trí", 2, FamilyCurrencyStarKinds.Growth, MaxStarsPerEvent: 1),
        ],
        DutyRules:
        [
            new(["đánh răng", "brush teeth"], true, ["new", "guided"], 1, FamilyCurrencyStarKinds.Responsibility),
            new(["chào hỏi", "chao hoi", "greet"], true, ["new"], 1, FamilyCurrencyStarKinds.Kindness),
            new(["đi học", "di hoc"], false, [], 0, FamilyCurrencyStarKinds.Responsibility),
            new(["đi ngủ đúng giờ", "ngủ đúng giờ", "bedtime"], true,
                ["new", "guided", "assisted"], 2, FamilyCurrencyStarKinds.Responsibility),
        ],
        HabitStageMultipliers: new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [FamilyHabitStages.New] = 1.0,
            [FamilyHabitStages.Guided] = 0.85,
            [FamilyHabitStages.Assisted] = 0.7,
            [FamilyHabitStages.HabitForming] = 0.5,
            [FamilyHabitStages.Autonomous] = 0.0,
            [FamilyHabitStages.Maintained] = 0.0,
        },
        StretchBonusStars: 2,
        InitiativeBonusStars: 4,
        StretchOverflowMaxPctOfBudget: 20,
        GraduateCopyVi: "Hành vi này đã tốt nghiệp. Không còn sao — con đã duy trì {streakDays} ngày liên tục.");

    public static FamilyCurrencyConfig Resolve(string? presetId) =>
        string.Equals(presetId, BalancedV1Id, StringComparison.OrdinalIgnoreCase)
            ? BalancedV1
            : BalancedV1;

    public static int ResolveBudget(
        FamilyCurrencyConfig config,
        string ageBand,
        int? dailyBudgetOverride)
    {
        if (dailyBudgetOverride is int o && o is >= 10 and <= 80)
            return o;
        if (config.BudgetByAgeBand.TryGetValue(ageBand, out var b))
            return b;
        return config.BudgetByAgeBand[FamilyCurrencyAgeBands.Age11To15];
    }
}
