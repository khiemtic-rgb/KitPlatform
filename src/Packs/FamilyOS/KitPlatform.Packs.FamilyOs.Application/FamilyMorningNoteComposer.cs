namespace KitPlatform.Packs.FamilyOs;

/// <summary>Rule/template morning pep-talk (P0) — no LLM required.</summary>
public static class FamilyMorningNoteComposer
{
    public sealed record Input(
        string ChildShortName,
        int? AgeYears,
        IReadOnlyList<string> TodayTitles,
        IReadOnlyList<string> StudyTitles,
        double RecentStudyDoneRate,
        int ParentNudgesLast7Days,
        int StreakDays);

    public static string AgeBand(int? ageYears) => ageYears switch
    {
        null => "unknown",
        < 6 => "preschool",
        <= 10 => "elementary",
        <= 14 => "tween",
        _ => "teen",
    };

    public static (string BodyVi, string Tone) Compose(Input input)
    {
        var name = string.IsNullOrWhiteSpace(input.ChildShortName) ? "con" : input.ChildShortName.Trim();
        var band = AgeBand(input.AgeYears);
        var jobs = input.TodayTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Take(4).ToList();
        var study = input.StudyTitles.Where(t => !string.IsNullOrWhiteSpace(t)).Take(3).ToList();
        var jobLine = jobs.Count == 0
            ? "H\u00f4m nay nh\u00e0 m\u00ecnh ch\u01b0a c\u00f3 vi\u1ec7c g\u1eafn c\u1ee5 th\u1ec3 \u2014 v\u1eabn c\u00f3 th\u1ec3 ch\u1ecdn m\u1ed9t vi\u1ec7c nh\u1ecf \u0111\u1ec3 b\u1eaft \u0111\u1ea7u."
            : "H\u00f4m nay c\u00f3: " + string.Join(", ", jobs) + ".";

        var studyHint = study.Count == 0
            ? ""
            : " Ph\u1ea7n h\u1ecdc (" + string.Join(", ", study) + ") nh\u1edb c\u00f3 b\u1eb1ng ch\u1ee9ng nh\u00e9 \u2014 \u1ea3nh b\u00e0i h\u00f4m nay, kh\u00f4ng ch\u1ec9 tick.";

        var past = input.RecentStudyDoneRate >= 0.8
            ? $" M\u1ea5y ng\u00e0y qua {name} gi\u1eef cam k\u1ebft h\u1ecdc kh\u00e1 \u0111\u1ec1u \u2014 ti\u1ebfp t\u1ee5c nh\u00e9."
            : input.RecentStudyDoneRate >= 0.4
                ? $" M\u1ea5y ng\u00e0y qua c\u00f3 l\u00fac m\u01b0\u1ee3t, c\u00f3 l\u00fac tr\u1ec5 \u2014 h\u00f4m nay l\u00e0m t\u1eebng vi\u1ec7c m\u1ed9t l\u00e0 \u0111\u01b0\u1ee3c."
                : $" H\u00e3y ch\u1ecdn m\u1ed9t vi\u1ec7c \u0111\u1ea7u ti\u00ean v\u00e0 l\u00e0m xong \u2014 nh\u1ecf nh\u01b0ng th\u1eadt.";

        var nudge = input.ParentNudgesLast7Days >= 5
            ? " B\u1ed1 m\u1eb9 \u0111\u00e3 nh\u1eafc kh\u00e1 nhi\u1ec1u tu\u1ea7n n\u00e0y \u2014 n\u1ebfu {name} t\u1ef1 b\u1eaft \u0111\u1ea7u s\u1ebd d\u1ec5 th\u01a1 h\u01a1n cho c\u1ea3 nh\u00e0.".Replace("{name}", name)
            : input.ParentNudgesLast7Days >= 1
                ? " B\u1ed1 m\u1eb9 \u0111ang \u0111\u1ed3ng h\u00e0nh; {name} c\u0169ng c\u00f3 th\u1ec3 t\u1ef1 nh\u1eafc m\u00ecnh tr\u01b0\u1edbc.".Replace("{name}", name)
                : " H\u00f4m nay th\u1eed t\u1ef1 m\u1edf app v\u00e0 ch\u1ecdn vi\u1ec7c \u0111\u1ea7u ti\u00ean nh\u00e9.";

        var ageLead = band switch
        {
            "preschool" => $"Ch\u00e0o {name}! H\u00f4m nay m\u00ecnh l\u00e0m vi\u1ec7c nh\u1ecf nh\u01b0ng \u0111\u00fang gi\u1edd nh\u00e9.",
            "elementary" => $"Ch\u00e0o bu\u1ed5i s\u00e1ng {name}! C\u00f9ng xem nh\u1eefng vi\u1ec7c h\u00f4m nay.",
            "tween" => $"{name} \u01a1i, l\u1ecbch h\u00f4m nay \u0111ang ch\u1edd. L\u00e0m xong t\u1eebng m\u1ee5c s\u1ebd nh\u1eb9 h\u01a1n.",
            "teen" => $"{name}, \u0111\u00e2y l\u00e0 l\u1ecbch cam k\u1ebft h\u00f4m nay \u2014 ch\u1ecdn th\u1ee9 \u01b0u ti\u00ean nh\u1ea5t tr\u01b0\u1edbc.",
            _ => $"Ch\u00e0o {name}! C\u00f9ng xem vi\u1ec7c h\u00f4m nay.",
        };

        var streak = input.StreakDays >= 2
            ? $" Chu\u1ed7i {input.StreakDays} ng\u00e0y \u0111ang \u0111\u1eb9p \u2014 gi\u1eef nhịp nh\u00e9."
            : "";

        var body = ageLead + " " + jobLine + studyHint + past + nudge + streak;
        var tone = input.RecentStudyDoneRate >= 0.8 ? "celebrate"
            : input.ParentNudgesLast7Days >= 5 ? "gentle_autonomy"
            : "encourage";
        return (body.Trim(), tone);
    }
}
