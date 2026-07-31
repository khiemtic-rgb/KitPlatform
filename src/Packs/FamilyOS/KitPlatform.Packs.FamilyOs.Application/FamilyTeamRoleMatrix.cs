namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// Sibling nudge eligibility by VN school stage / age band.
/// Preschool mainly receives; secondary can invite when their missions are done.
/// </summary>
public static class FamilyTeamRoleMatrix
{
    public const string Preschool = "preschool";
    public const string Primary = "primary";
    public const string LowerSecondary = "lower_secondary";
    public const string UpperSecondary = "upper_secondary";

    public static string StageFromAgeBand(string? ageBand) =>
        (ageBand ?? "").Trim() switch
        {
            "4-6" => Preschool,
            "7-9" => Primary,
            "10-12" => LowerSecondary,
            "13+" => UpperSecondary,
            _ => Primary,
        };

    public static string StageFromDateOfBirth(DateOnly? dob, DateOnly asOf)
    {
        if (dob is null) return Primary;
        var years = asOf.Year - dob.Value.Year;
        if (asOf < dob.Value.AddYears(years)) years--;
        if (years <= 6) return Preschool;
        if (years <= 9) return Primary;
        if (years <= 12) return LowerSecondary;
        return UpperSecondary;
    }

    public static int Rank(string stageCode) =>
        stageCode switch
        {
            Preschool => 0,
            Primary => 1,
            LowerSecondary => 2,
            UpperSecondary => 3,
            _ => 1,
        };

    /// <summary>
    /// Can this child be offered as "from" (inviter)?
    /// Preschool: never. Primary: only if missions complete. Secondary+: if complete or older rank.
    /// </summary>
    public static bool CanInvite(string stageCode, bool missionsComplete) =>
        stageCode switch
        {
            Preschool => false,
            Primary => missionsComplete,
            LowerSecondary or UpperSecondary => missionsComplete,
            _ => missionsComplete,
        };

    public static bool PreferAsInviter(string fromStage, string toStage) =>
        Rank(fromStage) >= Rank(toStage);
}
