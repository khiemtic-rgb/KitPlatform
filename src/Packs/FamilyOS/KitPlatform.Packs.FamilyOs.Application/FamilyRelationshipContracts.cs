namespace KitPlatform.Packs.FamilyOs;

public static class FamilyParentVoiceTemplates
{
    public const string Praise = "praise";
    public const string Encourage = "encourage";
    public const string Custom = "custom";
    /// <summary>P1.9 — cảm ơn bố/mẹ / caregiver cùng nhà.</summary>
    public const string ThanksPartner = "thanks_partner";
    /// <summary>P1.9 — mời phụ việc nhẹ.</summary>
    public const string HelpOffer = "help_offer";
    /// <summary>P1.9 — lời ấm adult↔adult.</summary>
    public const string WarmAdult = "warm_adult";
    /// <summary>P1.10 — chúc sinh nhật / milestone.</summary>
    public const string Birthday = "birthday";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Praise, Encourage, Custom, ThanksPartner, HelpOffer, WarmAdult, Birthday,
    };

    public static readonly IReadOnlySet<string> AdultFacing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ThanksPartner, HelpOffer, WarmAdult, Custom,
    };

    public static readonly IReadOnlySet<string> ChildFacing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Praise, Encourage, Custom, Birthday,
    };
}

public static class FamilyParentVoiceStatuses
{
    public const string Sent = "sent";
    public const string Read = "read";
    public const string Thanks = "thanks";

    public static readonly IReadOnlySet<string> Ack = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Read, Thanks,
    };
}

public static class FamilyRelationshipTriggerCodes
{
    public const string PraiseStreak = "praise_streak";
    public const string EncourageDip = "encourage_dip";
    public const string CheerSibling = "cheer_sibling";
    public const string ThankParent = "thank_parent";
    public const string TeamEarlyFinish = "team_early_finish";
    public const string FirstDayComplete = "first_day_complete";
    public const string WarmCheckin = "warm_checkin";
    /// <summary>P1.9 — cảm ơn / phụ việc với bố/mẹ hoặc caregiver.</summary>
    public const string ThankPartner = "thank_partner";
    /// <summary>P1.10 — sinh nhật con trong cửa sổ ±1 ngày.</summary>
    public const string BirthdayWish = "birthday_wish";
}

public sealed record FamilyRelationshipTriggerDto(
    string Code,
    string TitleVi,
    string BodyVi,
    string CtaLabelVi,
    Guid? ToMemberId,
    string? ToMemberName,
    string? DraftBodyVi,
    string? TemplateCode,
    string WhyNow,
    bool IsGolden = false);

public sealed record FamilyParentVoiceDto(
    Guid Id,
    Guid FamilyId,
    Guid FromMemberId,
    string FromMemberName,
    Guid ToMemberId,
    string ToMemberName,
    DateOnly FlowDate,
    string TemplateCode,
    string BodyVi,
    string Status,
    DateTimeOffset SentAt,
    DateTimeOffset? AckAt);

public sealed record FamilyParentVoiceSendRequest(
    Guid FromMemberId,
    Guid ToMemberId,
    string TemplateCode,
    string BodyVi,
    DateOnly? FlowDate);

public sealed record FamilyParentVoiceAckRequest(string Status);

public sealed record FamilyEveningCircleAnswerDto(
    Guid MemoryId,
    Guid MemberId,
    string MemberName,
    string AnswerVi,
    DateTimeOffset HappenedAt);

public sealed record FamilyEveningCircleDto(
    DateOnly FlowDate,
    string PromptVi,
    bool AlreadyAnswered,
    IReadOnlyList<FamilyEveningCircleAnswerDto> Answers);

public sealed record FamilyEveningCircleAnswerRequest(
    Guid MemberId,
    string AnswerVi,
    DateOnly? FlowDate);

public sealed record FamilyWeeklyStoryLineDto(
    string Icon,
    string TextVi,
    Guid? MemoryId);

public sealed record FamilyWeeklyStoryDto(
    DateOnly From,
    DateOnly To,
    string HeadlineVi,
    int ParentVoiceCount,
    int HelpCount,
    int GratitudeCount,
    int RitualCount,
    int StreakMilestoneCount,
    int EveningCircleCount,
    IReadOnlyList<FamilyWeeklyStoryLineDto> Lines);

public static class FamilyRelationshipTriggerUiStates
{
    public const string Opened = "opened";
    public const string Dismissed = "dismissed";
    public const string Sent = "sent";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Opened, Dismissed, Sent,
    };
}

/// <summary>P1.2 — persisted trigger card state for “lời chưa gửi”.</summary>
public sealed record FamilyRelationshipTriggerStateDto(
    Guid Id,
    Guid FamilyId,
    Guid ViewerMemberId,
    DateOnly FlowDate,
    string TriggerCode,
    Guid? ToMemberId,
    string State,
    string? DraftBodyVi,
    string? TemplateCode,
    string? TitleVi,
    string? BodyVi,
    DateTimeOffset UpdatedAt);

public sealed record FamilyRelationshipTriggerStateUpsertRequest(
    Guid ViewerMemberId,
    string TriggerCode,
    string State,
    Guid? ToMemberId = null,
    DateOnly? FlowDate = null,
    string? DraftBodyVi = null,
    string? TemplateCode = null,
    string? TitleVi = null,
    string? BodyVi = null);

public interface IFamilyRelationshipService
{
    Task<IReadOnlyList<FamilyRelationshipTriggerDto>> ListTriggersAsync(
        Guid familyId,
        Guid forMemberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyRelationshipTriggerStateDto>> ListTriggerStatesAsync(
        Guid familyId,
        Guid viewerMemberId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<FamilyRelationshipTriggerStateDto> UpsertTriggerStateAsync(
        Guid familyId,
        FamilyRelationshipTriggerStateUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyParentVoiceDto>> ListParentVoiceAsync(
        Guid familyId,
        Guid? forMemberId = null,
        Guid? fromMemberId = null,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<FamilyParentVoiceDto> SendParentVoiceAsync(
        Guid familyId,
        FamilyParentVoiceSendRequest request,
        CancellationToken cancellationToken = default);

    Task AckParentVoiceAsync(
        Guid familyId,
        Guid messageId,
        FamilyParentVoiceAckRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyEveningCircleDto> GetEveningCircleAsync(
        Guid familyId,
        Guid? forMemberId = null,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<FamilyEveningCircleDto> AnswerEveningCircleAsync(
        Guid familyId,
        FamilyEveningCircleAnswerRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyWeeklyStoryDto> GetWeeklyStoryAsync(
        Guid familyId,
        DateOnly? asOf = null,
        CancellationToken cancellationToken = default);
}
