namespace KitPlatform.Packs.FamilyOs;

public static class FamilyCommitmentPriorities
{
    public const string Critical = "critical";
    public const string Normal = "normal";
    public const string Optional = "optional";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Critical, Normal, Optional,
    };
}

public static class FamilyContextAnchors
{
    public const string AfterWake = "after_wake";
    public const string BeforeBreakfast = "before_breakfast";
    public const string AfterBreakfast = "after_breakfast";
    public const string BeforeSchool = "before_school";
    public const string AfterSchool = "after_school";
    public const string BeforeDinner = "before_dinner";
    public const string AfterDinner = "after_dinner";
    public const string BeforeSleep = "before_sleep";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        AfterWake, BeforeBreakfast, AfterBreakfast, BeforeSchool,
        AfterSchool, BeforeDinner, AfterDinner, BeforeSleep,
    };

    public static string? LabelVi(string? code) => code switch
    {
        AfterWake => "Sau khi dậy",
        BeforeBreakfast => "Trước ăn sáng",
        AfterBreakfast => "Sau ăn sáng",
        BeforeSchool => "Trước giờ đi học",
        AfterSchool => "Sau giờ học",
        BeforeDinner => "Trước ăn tối",
        AfterDinner => "Sau ăn tối",
        BeforeSleep => "Trước khi ngủ",
        _ => null,
    };
}
