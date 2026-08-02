namespace KitPlatform.Packs.FamilyOs;

/// <summary>
/// S1 catalog — 5 parent-facing behavior patterns + tactics.
/// Heuristic only (no LLM). Used by Motivation Engine (S2), Child Voice tips (S3),
/// and parent strategy mirror (S4). Changing tactic never raises nudge budget.
/// </summary>
public static class FamilyBehaviorPatternCodes
{
    public const string EveningFatigue = "evening_fatigue";
    public const string SubjectAvoidance = "subject_avoidance";
    public const string NudgeDependent = "nudge_dependent";
    public const string SocialBoost = "social_boost";
    public const string StreakFragile = "streak_fragile";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        EveningFatigue, SubjectAvoidance, NudgeDependent, SocialBoost, StreakFragile,
    };
}

public sealed record BehaviorTacticDef(
    string Code,
    string LabelVi,
    string ChildCueVi,
    string ParentAdviceVi);

public sealed record BehaviorPatternDef(
    string Code,
    string TitleVi,
    string WhyVi,
    string DefaultDriver,
    IReadOnlyList<BehaviorTacticDef> Tactics);

public static class FamilyBehaviorPatterns
{
    public static readonly IReadOnlyList<BehaviorPatternDef> Catalog =
    [
        new BehaviorPatternDef(
            FamilyBehaviorPatternCodes.EveningFatigue,
            "Chậm buổi tối",
            "Có thể con đang mệt — không nhất thiết là lười.",
            FamilyMotivationDrivers.Rest,
            [
                new BehaviorTacticDef(
                    "shorten",
                    "Rút ngắn mục tiêu",
                    "Chỉ cần một bước nhỏ trước khi nghỉ — vậy cũng đủ tốt.",
                    "Buổi tối: rút việc xuống 1 bước vừa sức, khen khi con mở việc."),
                new BehaviorTacticDef(
                    "earlier_window",
                    "Dời khung sớm hơn",
                    "Làm sớm hơn một chút — tối sẽ nhẹ hơn.",
                    "Thử dời cửa sổ việc tối sớm 20–30 phút; tránh nhắc dồn sau 20h."),
                new BehaviorTacticDef(
                    "rest_first",
                    "Nghỉ rồi làm",
                    "Nghỉ ngắn rồi quay lại cũng ổn.",
                    "Cho 10 phút nghỉ rồi làm — đừng tăng số lần nhắc."),
            ]),
        new BehaviorPatternDef(
            FamilyBehaviorPatternCodes.SubjectAvoidance,
            "Né môn khó",
            "Có thể con đang sợ / chưa thấy mình làm được — không chỉ là trì môn đó.",
            FamilyMotivationDrivers.Mastery,
            [
                new BehaviorTacticDef(
                    "tiny_start",
                    "Bắt đầu siêu nhỏ",
                    "Làm chậm một ý khó cũng được — hiểu mới quan trọng.",
                    "Cho con chọn 1 câu dễ mở đầu; khen nỗ lực, không chỉ đáp án."),
                new BehaviorTacticDef(
                    "together",
                    "Cùng làm mở đầu",
                    "Bố/mẹ ngồi cạnh 5 phút đầu — rồi con tự tiếp.",
                    "Ngồi cạnh 5 phút đầu rồi lùi dần — đừng thay con làm hết."),
                new BehaviorTacticDef(
                    "choice",
                    "Cho quyền chọn thứ tự",
                    "Con chọn làm phần nào trước cũng được.",
                    "Để con chọn thứ tự bài — giảm ép, tăng tự chủ."),
            ]),
        new BehaviorPatternDef(
            FamilyBehaviorPatternCodes.NudgeDependent,
            "Chỉ làm khi có nhắc",
            "Chưa hình thành tự chủ — động lực đang ở ngoài (sợ bị nhắc).",
            FamilyMotivationDrivers.Autonomy,
            [
                new BehaviorTacticDef(
                    "choice_first",
                    "Cho quyền lựa chọn",
                    "Con tự chọn lúc bắt đầu — mình tin con làm được.",
                    "Hôm nay đừng nhắc trước — hỏi con muốn làm lúc nào trong khung."),
                new BehaviorTacticDef(
                    "praise_self_start",
                    "Khen khi tự mở",
                    "Con tự mở việc là chiến thắng lớn hôm nay.",
                    "Chỉ khen khi con tự bắt đầu; tránh nhắc thêm nếu đã trong ngân sách."),
                new BehaviorTacticDef(
                    "observe",
                    "Quan sát một nhịp",
                    "Nhà mình đang ở đây — bắt đầu khi sẵn sàng nhé.",
                    "Thử observe một cửa sổ: không parent push; ghi nhận nếu con tự làm."),
            ]),
        new BehaviorPatternDef(
            FamilyBehaviorPatternCodes.SocialBoost,
            "Làm tốt hơn khi có người cùng",
            "Động lực xã hội cao — cô độc làm giảm nhịp.",
            FamilyMotivationDrivers.Relatedness,
            [
                new BehaviorTacticDef(
                    "pair",
                    "Làm cùng người thân",
                    "Làm cùng bố/mẹ/anh chị một đoạn ngắn nhé.",
                    "Giao việc cặp đôi 10 phút — rồi để con kết thúc một mình."),
                new BehaviorTacticDef(
                    "team_mission",
                    "Nhiệm vụ nhóm",
                    "Việc nhóm nhà mình đang chờ con — mình cùng thắng.",
                    "Ưu tiên nhiệm vụ nhóm / team unlock thay vì chỉ việc cá nhân."),
                new BehaviorTacticDef(
                    "cheer",
                    "Cổ vũ ngắn",
                    "Nhà mình đang cổ vũ con — một bước là được.",
                    "Gửi lời cổ vũ ngắn (không phải nhắc việc) trước khung giờ."),
            ]),
        new BehaviorPatternDef(
            FamilyBehaviorPatternCodes.StreakFragile,
            "Giữ nhịp yếu",
            "Không phải lười — khả năng duy trì đang thấp; tiến bộ không thẳng.",
            FamilyMotivationDrivers.Progress,
            [
                new BehaviorTacticDef(
                    "restart_small",
                    "Khởi động lại nhỏ",
                    "Mỗi lần xong là một bước tiến rõ — hôm nay chỉ cần mở lại.",
                    "Sau ngày gãy chuỗi: mục tiêu nhỏ hơn hôm qua, không trách."),
                new BehaviorTacticDef(
                    "visible_progress",
                    "Làm tiến bộ nhìn thấy",
                    "Chuỗi đang đẹp trở lại — thêm một bước nữa thôi.",
                    "Cho con thấy tiến bộ nhỏ (sao/ô xanh) ngay sau khi xong."),
                new BehaviorTacticDef(
                    "skip_shame",
                    "Bỏ cảm giác thất bại",
                    "Bỏ một ngày không sao — quay lại là dũng cảm.",
                    "Nói rõ: gãy chuỗi ≠ thất bại; tuần tới chỉ cần 3 ngày tốt."),
            ]),
    ];

    public static BehaviorPatternDef? Get(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return Catalog.FirstOrDefault(p =>
            string.Equals(p.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static BehaviorTacticDef? GetTactic(string? patternCode, string? tacticCode)
    {
        var p = Get(patternCode);
        if (p is null || string.IsNullOrWhiteSpace(tacticCode)) return null;
        return p.Tactics.FirstOrDefault(t =>
            string.Equals(t.Code, tacticCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public sealed record InferSignals(
        TimeOnly? WindowEnd,
        string ReminderState,
        bool IsLearningMission,
        string? Title,
        string? HabitStage,
        int HabitStreakDays,
        int ParentNudgesUsedToday,
        string? SkipReason);

    /// <summary>Pick at most one primary pattern for a live commitment.</summary>
    public static string? InferCode(InferSignals s)
    {
        var reminder = (s.ReminderState ?? "").Trim().ToLowerInvariant();
        var stage = (s.HabitStage ?? FamilyHabitStages.New).Trim().ToLowerInvariant();
        var title = (s.Title ?? "").Trim().ToLowerInvariant();
        var overdueOrDue = reminder is FamilyReminderStates.Overdue or FamilyReminderStates.DueNow;

        if (s.WindowEnd is { } end && end >= new TimeOnly(19, 0) && overdueOrDue)
            return FamilyBehaviorPatternCodes.EveningFatigue;

        if (s.IsLearningMission
            && (overdueOrDue
                || string.Equals(s.SkipReason, FamilySkipReasons.NeedHelp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.SkipReason, FamilySkipReasons.NotReady, StringComparison.OrdinalIgnoreCase)))
            return FamilyBehaviorPatternCodes.SubjectAvoidance;

        if (IsSocialTitle(title))
            return FamilyBehaviorPatternCodes.SocialBoost;

        if (s.ParentNudgesUsedToday >= 2
            && stage is FamilyHabitStages.New or FamilyHabitStages.Guided or FamilyHabitStages.Assisted
            && overdueOrDue)
            return FamilyBehaviorPatternCodes.NudgeDependent;

        if (s.HabitStreakDays is >= 1 and <= 3 && reminder == FamilyReminderStates.Overdue)
            return FamilyBehaviorPatternCodes.StreakFragile;

        if (s.ParentNudgesUsedToday >= 2 && overdueOrDue)
            return FamilyBehaviorPatternCodes.NudgeDependent;

        return null;
    }

    public static string PickTacticCode(
        string patternCode,
        DateOnly asOf,
        Guid? memberId,
        string? preferAvoidTactic = null)
    {
        var def = Get(patternCode);
        if (def is null || def.Tactics.Count == 0) return "choice_first";

        var pool = def.Tactics
            .Where(t => preferAvoidTactic is null
                || !string.Equals(t.Code, preferAvoidTactic, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (pool.Count == 0) pool = def.Tactics.ToList();

        var seed = asOf.DayNumber + (memberId?.GetHashCode() ?? 0);
        var idx = Math.Abs(seed) % pool.Count;
        return pool[idx].Code;
    }

    /// <summary>Monday (ISO) week start for playbook rows.</summary>
    public static DateOnly WeekStart(DateOnly asOf)
    {
        var diff = ((int)asOf.DayOfWeek + 6) % 7; // Monday=0
        return asOf.AddDays(-diff);
    }

    public static string ParentStrategyTipVi(
        string? patternCode,
        string? tacticCode,
        int parentNudgesThisWeek,
        int selfStartsThisWeek)
    {
        if (parentNudgesThisWeek >= 12 && selfStartsThisWeek <= 2)
        {
            return "Tuần này việc nhắc khá nhiều so với lúc con tự mở. Tuần tới hãy thử khen ngay khi con chủ động — không tăng nhắc.";
        }

        if (parentNudgesThisWeek >= 8 && selfStartsThisWeek <= 3)
        {
            return "Nhắc đang nhiều hơn tự khởi động. Thử một ngày observe trong khung giờ quen — chỉ ghi nhận nếu con tự làm.";
        }

        var tactic = GetTactic(patternCode, tacticCode);
        if (tactic is not null)
            return $"Tuần này thử chiến thuật «{tactic.LabelVi}»: {tactic.ParentAdviceVi}";

        var pattern = Get(patternCode);
        if (pattern is not null)
            return $"{pattern.TitleVi}: {pattern.WhyVi} Chọn một chiến thuật nhẹ — không tăng số lần nhắc.";

        return "Giữ nhịp: một gợi ý nhỏ mỗi tuần — tiến bộ không cần thẳng.";
    }

    public static IReadOnlyList<string> TipsFromChildVoice(
        string? hardestCode,
        string? wantParentCode,
        string? wishVi)
    {
        var tips = new List<string>();
        var want = (wantParentCode ?? "").Trim().ToLowerInvariant();
        var hard = (hardestCode ?? "").Trim().ToLowerInvariant();

        switch (want)
        {
            case "less_remind":
                tips.Add("Con muốn ít bị nhắc hơn — tuần tới thử để con tự mở trước, chỉ hỗ trợ nếu quá khung.");
                break;
            case "praise":
                tips.Add("Con muốn được ghi nhận — khen cụ thể ngay khi con tự bắt đầu (không đợi cả ngày).");
                break;
            case "together":
                tips.Add("Con muốn làm cùng người lớn — thử ngồi cạnh 5–10 phút đầu rồi lùi dần.");
                break;
            case "choose_time":
                tips.Add("Con muốn được chọn giờ — hỏi con mốc trong khung và tôn trọng lựa chọn đó.");
                break;
            case "friends":
                tips.Add("Con thích làm có bạn/người cùng — ưu tiên việc nhóm hoặc cổ vũ ngắn trước giờ.");
                break;
        }

        switch (hard)
        {
            case "evening":
                if (tips.Count < 2)
                    tips.Add("Con thấy tối khó — rút ngắn việc tối hoặc dời sớm hơn, tránh nhắc dồn.");
                break;
            case "subject":
                if (tips.Count < 2)
                    tips.Add("Con thấy môn/việc học khó — mở bằng bước siêu nhỏ, khen nỗ lực.");
                break;
            case "alone":
                if (tips.Count < 2)
                    tips.Add("Con thấy làm một mình khó — thêm nhịp cùng làm ngắn rồi giao lại.");
                break;
            case "long":
                if (tips.Count < 2)
                    tips.Add("Con thấy việc dài — tách thành 2 bước có điểm dừng rõ.");
                break;
        }

        if (tips.Count == 0 && !string.IsNullOrWhiteSpace(wishVi))
        {
            tips.Add("Con đã nói một mong muốn tuần này — bố mẹ chọn 1 ý nhỏ để thử, không cần đổi hết.");
        }

        if (tips.Count == 0)
            tips.Add("Tuần tới hỏi lại con một câu: «Điều gì khiến con muốn làm?» — lắng nghe trước khi nhắc.");

        return tips.Take(2).ToList();
    }

    public static string ChildVoicePromptHardestVi() =>
        "Tuần này việc nào khó nhất với con?";

    public static string ChildVoicePromptWantVi() =>
        "Con muốn bố mẹ làm gì khác một chút?";

    public static string ChildVoicePromptWishVi() =>
        "Con muốn giữ / bỏ / thêm gì tuần tới? (nói ngắn cũng được)";

    private static bool IsSocialTitle(string title) =>
        title.Contains("nhóm", StringComparison.Ordinal)
        || title.Contains("cùng", StringComparison.Ordinal)
        || title.Contains("team", StringComparison.Ordinal)
        || title.Contains("movie", StringComparison.Ordinal)
        || title.Contains("anh", StringComparison.Ordinal)
        || title.Contains("chị", StringComparison.Ordinal)
        || title.Contains("em ", StringComparison.Ordinal)
        || title.Contains("cả nhà", StringComparison.Ordinal);
}
