using System.Text.RegularExpressions;

namespace KitPlatform.Packs.FamilyOs;

public static class FamilyAgreementStatuses
{
    public const string Proposed = "proposed";
    public const string Discussing = "discussing";
    public const string Accepted = "accepted";
    public const string Rejected = "rejected";
    public const string Withdrawn = "withdrawn";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Proposed, Discussing, Accepted, Rejected, Withdrawn,
    };

    public static readonly HashSet<string> Open = new(StringComparer.OrdinalIgnoreCase)
    {
        Proposed, Discussing,
    };
}

/// <summary>Agreement taxonomy categories (stored in target_type column).</summary>
public static class FamilyAgreementCategories
{
    public const string Foundation = "foundation";
    public const string Routine = "routine";
    public const string Commitment = "commitment";
    public const string Reward = "reward";
    public const string Accountability = "accountability";
    public const string Grace = "grace";
    public const string Exception = "exception";
    public const string Change = "change";
    public const string Value = "value";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Foundation, Routine, Commitment, Reward, Accountability,
        Grace, Exception, Change, Value,
    };

    /// <summary>Normalize API/legacy codes to taxonomy category.</summary>
    public static string Normalize(string? raw)
    {
        var v = (raw ?? "").Trim().ToLowerInvariant();
        return v switch
        {
            "" => Value,
            "accountability_rule" => Accountability,
            "reward_rule" => Reward,
            "routine_change" => Change,
            "commitment_change" => Change,
            "other" => Value,
            _ when All.Contains(v) => v,
            _ => throw new InvalidOperationException(
                "category phải là foundation|routine|commitment|reward|accountability|grace|exception|change|value."),
        };
    }

    public static string LabelVi(string? category) => NormalizeSafe(category) switch
    {
        Foundation => "Nền tảng",
        Routine => "Nhịp sống",
        Commitment => "Cam kết",
        Reward => "Thưởng / quyền lợi",
        Accountability => "Accountability",
        Grace => "Grace (gia hạn)",
        Exception => "Ngoại lệ",
        Change => "Điều chỉnh",
        Value => "Giá trị nhà",
        _ => category ?? "",
    };

    public static string? NormalizeSafe(string? raw)
    {
        try { return Normalize(raw); }
        catch { return null; }
    }
}

/// <summary>Legacy alias — prefer <see cref="FamilyAgreementCategories"/>.</summary>
public static class FamilyAgreementTargetTypes
{
    public const string RoutineChange = "change";
    public const string CommitmentChange = "change";
    public const string AccountabilityRule = "accountability";
    public const string RewardRule = "reward";
    public const string Other = "value";

    public static readonly HashSet<string> All = FamilyAgreementCategories.All;
}

public static class FamilyAccountabilityOptionKinds
{
    public const string Consequence = "consequence";
    public const string Reward = "reward";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Consequence, Reward,
    };
}

/// <summary>
/// Safe consequence / reward catalog — FamilyOS Default Family Constitution v1.0.
/// Never physical/emotional harm. UI presents these as thỏa thuận đã thống nhất, not punishment.
/// </summary>
public static class FamilyAccountabilityDefaults
{
    public sealed record Item(string Kind, string Code, string Group, string LabelVi, string DescriptionVi, int SortOrder);

    public static IReadOnlyList<Item> All { get; } =
    [
        // Screen
        new(FamilyAccountabilityOptionKinds.Consequence, "screen_reduce_15", "screen",
            "Giảm 15 phút Screen Time", "Giảm thời lượng màn hình đã thống nhất trong ngày.", 10),
        new(FamilyAccountabilityOptionKinds.Consequence, "screen_reduce_30", "screen",
            "Giảm 30 phút Screen Time", "Giảm thời lượng màn hình đã thống nhất trong ngày.", 20),
        new(FamilyAccountabilityOptionKinds.Consequence, "screen_reduce_30_weekend", "screen",
            "Giảm 30 phút Screen Time cuối tuần", "Áp dụng cuối tuần theo thỏa thuận (vd. ngủ muộn 3 ngày).", 25),
        new(FamilyAccountabilityOptionKinds.Consequence, "screen_no_game_today", "screen",
            "Không chơi game hôm nay", "Hoàn thành trách nhiệm trước khi giải trí game.", 30),
        new(FamilyAccountabilityOptionKinds.Consequence, "entertain_no_youtube", "screen",
            "Không xem YouTube hôm nay", "Chuyển sang hoạt động khác đã thống nhất.", 40),
        // Responsibility
        new(FamilyAccountabilityOptionKinds.Consequence, "duty_extra_chore", "responsibility",
            "Thêm một việc nhà", "Một việc nhà nhỏ đã thỏa thuận trước.", 50),
        new(FamilyAccountabilityOptionKinds.Consequence, "duty_fold_clothes", "responsibility",
            "Gấp quần áo", "Việc nhà hỗ trợ cả nhà.", 55),
        new(FamilyAccountabilityOptionKinds.Consequence, "duty_wipe_table", "responsibility",
            "Lau bàn ăn", "Giúp giữ không gian ăn uống sạch sẽ.", 60),
        new(FamilyAccountabilityOptionKinds.Consequence, "duty_water_plants", "responsibility",
            "Tưới cây", "Chăm sóc góc xanh trong nhà.", 65),
        // Learning
        new(FamilyAccountabilityOptionKinds.Consequence, "learn_read_15", "learning",
            "Đọc sách thêm 15 phút", "Bổ sung thói quen đọc đã thống nhất.", 70),
        new(FamilyAccountabilityOptionKinds.Consequence, "learn_journal_10", "learning",
            "Viết nhật ký 10 phút", "Ghi lại ngày học / cảm xúc ngắn.", 75),
        new(FamilyAccountabilityOptionKinds.Consequence, "learn_finish_homework", "learning",
            "Hoàn thành bài còn thiếu", "Bù phần cam kết học tập chưa xong.", 80),
        // Family
        new(FamilyAccountabilityOptionKinds.Consequence, "family_help_dinner", "family",
            "Giúp bố mẹ chuẩn bị bữa tối", "Cùng nhau chuẩn bị bữa ăn.", 90),
        new(FamilyAccountabilityOptionKinds.Consequence, "family_clean_living", "family",
            "Cùng dọn phòng khách", "Chia sẻ việc giữ nhà gọn.", 95),
        new(FamilyAccountabilityOptionKinds.Consequence, "family_wash_dishes", "family",
            "Cùng rửa bát", "Chia sẻ việc sau bữa ăn.", 100),
        // Rewards — Screen
        new(FamilyAccountabilityOptionKinds.Reward, "reward_screen_15_today", "screen",
            "+15 phút Screen Time", "Thêm thời lượng màn hình trong ngày khi đạt 100% cam kết.", 110),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_extra_game_20", "screen",
            "+20 phút Game cuối tuần", "Thưởng giải trí cuối tuần (streak 5 ngày).", 120),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_extra_game_30", "screen",
            "+30 phút Game cuối tuần", "Thưởng giải trí cuối tuần đã thống nhất.", 125),
        // Rewards — Family
        new(FamilyAccountabilityOptionKinds.Reward, "reward_choose_dinner", "family",
            "Chọn món ăn cuối tuần", "Con được chọn món ăn cùng nhà.", 130),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_choose_movie_sat", "family",
            "Chọn phim tối thứ Bảy", "Con chọn phim xem cùng nhà.", 135),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_choose_outing", "family",
            "Chọn địa điểm đi chơi", "Con chọn nơi cả nhà đi chơi.", 140),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_family_activity", "family",
            "Chọn hoạt động gia đình cuối tuần", "Công viên / xem phim / bơi… theo thỏa thuận.", 145),
        // Rewards — Experience
        new(FamilyAccountabilityOptionKinds.Reward, "reward_park_trip", "experience",
            "Đi công viên", "Hoạt động ngoài trời cùng nhà.", 150),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_swim", "experience",
            "Đi bơi", "Hoạt động vận động cùng nhà.", 155),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_movie_outing", "experience",
            "Đi xem phim", "Buổi xem phim ngoài đã hẹn.", 160),
        new(FamilyAccountabilityOptionKinds.Reward, "reward_picnic", "experience",
            "Đi picnic", "Buổi picnic gia đình.", 165),
        // Rewards — Recognition
        new(FamilyAccountabilityOptionKinds.Reward, "badge_streak_7", "recognition",
            "Huy hiệu \"7 ngày liên tiếp\"", "Ghi nhận tuần tự giác.", 170),
        new(FamilyAccountabilityOptionKinds.Reward, "badge_streak_30", "recognition",
            "Huy hiệu \"30 ngày tự giác\"", "Ghi nhận thói quen dài hạn.", 175),
        new(FamilyAccountabilityOptionKinds.Reward, "badge_promise_keeper", "recognition",
            "Huy hiệu \"Người giữ lời hứa\"", "Ghi nhận giữ cam kết đã đồng ý.", 180),
    ];

    /// <summary>Backward-compatible view used by older consequence-library callers.</summary>
    public static IReadOnlyList<FamilyConsequenceLibrary.Item> ConsequenceItems =>
        All.Where(i => i.Kind == FamilyAccountabilityOptionKinds.Consequence)
            .Select(i => new FamilyConsequenceLibrary.Item(i.Code, i.Group, i.LabelVi, i.DescriptionVi))
            .ToList();
}

/// <summary>Legacy alias — prefer <see cref="FamilyAccountabilityDefaults"/>.</summary>
public static class FamilyConsequenceLibrary
{
    public sealed record Item(string Code, string Group, string LabelVi, string DescriptionVi);

    public static IReadOnlyList<Item> All => FamilyAccountabilityDefaults.ConsequenceItems;

    public static readonly HashSet<string> ForbiddenPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "cấm ăn", "cam an", "đánh", "danh", "xúc phạm", "xuc pham", "phạt tiền", "phat tien",
        "nhịn đói", "nhin doi", "tát", "tat", "phạt", "phat",
    };

    private static readonly Regex CodePattern = new(
        @"^[a-z][a-z0-9_]{2,48}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool IsValidCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CodePattern.IsMatch(code.Trim());
}

public sealed record FamilyAccountabilityOptionDto(
    Guid Id,
    Guid FamilyId,
    string Kind,
    string Code,
    string OptionGroup,
    string LabelVi,
    string DescriptionVi,
    bool IsSystem,
    int SortOrder,
    string Status);

public sealed record CreateAccountabilityOptionRequest(
    string Kind,
    string Code,
    string OptionGroup,
    string LabelVi,
    string? DescriptionVi,
    int? SortOrder);

public sealed record UpdateAccountabilityOptionRequest(
    string? OptionGroup,
    string? LabelVi,
    string? DescriptionVi,
    int? SortOrder,
    string? Status);

public sealed record FamilyAgreementDto(
    Guid Id,
    Guid FamilyId,
    Guid ProposedBy,
    string? ProposedByName,
    string Title,
    string ProposalBody,
    string TargetType,
    Guid? TargetId,
    string Status,
    string TermsJson,
    DateTimeOffset? DecidedAt,
    Guid? DecidedBy,
    string? DecisionNote,
    DateTimeOffset CreatedAt,
    string? Purpose = null,
    DateOnly? EffectiveOn = null,
    int? ReviewAfterDays = null,
    Guid? AppliesToMemberId = null);

public sealed record CreateFamilyAgreementRequest(
    Guid ProposedBy,
    string Title,
    string ProposalBody,
    string? TargetType,
    Guid? TargetId,
    string? TermsJson,
    string? Purpose = null,
    DateOnly? EffectiveOn = null,
    int? ReviewAfterDays = null,
    Guid? AppliesToMemberId = null);

public sealed record DecideFamilyAgreementRequest(
    string Status,
    Guid DecidedBy,
    string? DecisionNote);

public interface IFamilyAgreementService
{
    Task<IReadOnlyList<FamilyAgreementDto>> ListAsync(
        Guid familyId,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<FamilyAgreementDto?> GetAsync(
        Guid familyId,
        Guid agreementId,
        CancellationToken cancellationToken = default);

    Task<FamilyAgreementDto> CreateAsync(
        Guid familyId,
        CreateFamilyAgreementRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyAgreementDto> DecideAsync(
        Guid familyId,
        Guid agreementId,
        DecideFamilyAgreementRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<FamilyConsequenceLibrary.Item> ListConsequenceLibrary();

    Task<IReadOnlyList<FamilyAccountabilityOptionDto>> ListOptionsAsync(
        Guid familyId,
        string? kind = null,
        CancellationToken cancellationToken = default);

    Task<FamilyAccountabilityOptionDto> CreateOptionAsync(
        Guid familyId,
        CreateAccountabilityOptionRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyAccountabilityOptionDto> UpdateOptionAsync(
        Guid familyId,
        Guid optionId,
        UpdateAccountabilityOptionRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteOptionAsync(
        Guid familyId,
        Guid optionId,
        CancellationToken cancellationToken = default);
}
