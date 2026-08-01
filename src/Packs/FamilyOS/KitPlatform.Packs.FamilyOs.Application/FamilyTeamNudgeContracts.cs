namespace KitPlatform.Packs.FamilyOs;

public static class FamilyTeamNudgeStatuses
{
    public const string Draft = "draft";
    public const string Sent = "sent";
    public const string Seen = "seen";
    public const string Thanks = "thanks";
    public const string Deferred = "deferred";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Draft, Sent, Seen, Thanks, Deferred,
    };

    public static readonly HashSet<string> Ack = new(StringComparer.OrdinalIgnoreCase)
    {
        Seen, Thanks, Deferred,
    };
}

public static class FamilyTeamNudgeTemplates
{
    public const string CheerUp = "cheer_up";
    public const string OneLeft = "one_left";
    public const string YouGotThis = "you_got_this";
    /// <summary>P1.6 — em gửi cảm ơn anh/chị sau ack thanks.</summary>
    public const string ThanksBack = "thanks_back";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        CheerUp, OneLeft, YouGotThis, ThanksBack,
    };

    /// <summary>Templates that skip CanInvite / age gates (reciprocal thank only).</summary>
    public static readonly HashSet<string> Reciprocal = new(StringComparer.OrdinalIgnoreCase)
    {
        ThanksBack,
    };

    public static string MessageVi(string templateCode, string fromShort, string toShort) =>
        templateCode.ToLowerInvariant() switch
        {
            OneLeft => $"{toShort} ơi, cả đội còn 1 việc nữa thôi — {fromShort} cổ vũ em!",
            YouGotThis => $"{toShort} cố lên nhé! {fromShort} tin em làm được.",
            ThanksBack => $"{toShort} ơi, cảm ơn lời nhắc hôm nay — {fromShort} nhớ ơn anh/chị!",
            _ => $"{toShort} ơi, {fromShort} nhắc nhẹ — mình cùng xong ngày hôm nay nhé!",
        };
}

public sealed record FamilyTeamNudgeDto(
    Guid Id,
    Guid FamilyId,
    DateOnly FlowDate,
    Guid FromMemberId,
    string FromName,
    Guid ToMemberId,
    string ToName,
    Guid? CommitmentId,
    string TemplateCode,
    string MessageVi,
    string Status,
    DateTimeOffset? SentAt,
    DateTimeOffset? AckAt,
    DateTimeOffset CreatedAt);

public sealed record FamilyTeamNudgeCreateRequest(
    Guid FromMemberId,
    Guid ToMemberId,
    string TemplateCode,
    DateOnly? FlowDate = null,
    Guid? CommitmentId = null);

public sealed record FamilyTeamNudgeAckRequest(string Status);

public interface IFamilyTeamNudgeService
{
    Task<IReadOnlyList<FamilyTeamNudgeDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        Guid? forMemberId = null,
        CancellationToken cancellationToken = default);

    Task<FamilyTeamNudgeDto> CreateAsync(
        Guid familyId,
        FamilyTeamNudgeCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyTeamNudgeDto> SendAsync(
        Guid familyId,
        Guid nudgeId,
        CancellationToken cancellationToken = default);

    Task<FamilyTeamNudgeDto> AckAsync(
        Guid familyId,
        Guid nudgeId,
        FamilyTeamNudgeAckRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Children eligible to send a nudge today (done / older bands).</summary>
    Task<IReadOnlyList<FamilyTeamNudgeCandidateDto>> ListFromCandidatesAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);
}

public sealed record FamilyTeamNudgeCandidateDto(
    Guid MemberId,
    string DisplayName,
    string StageCode,
    bool CanInvite,
    bool MissionsComplete);
