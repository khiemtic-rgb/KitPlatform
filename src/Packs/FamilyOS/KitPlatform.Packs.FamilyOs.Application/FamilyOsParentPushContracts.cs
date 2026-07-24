namespace KitPlatform.Packs.FamilyOs;

public static class FamilyOsReminderSettings
{
    public const string SectionName = "FamilyOsReminder";
}

public sealed class FamilyOsReminderOptions
{
    public bool Enabled { get; init; } = true;
    public int PollIntervalSeconds { get; init; } = 60;
    /// <summary>Local family hour (0–23) to send evening digest once per day.</summary>
    public int EveningDigestHour { get; init; } = 20;
}

public sealed record FamilyParentPushStatusDto(
    bool Supported,
    bool Subscribed,
    string? PublicKey);

public sealed record FamilyParentPushSubscribeRequest(
    Guid MembershipId,
    string Endpoint,
    string P256dh,
    string Auth,
    string? UserAgent);

public sealed record SoftLockGuideDto(
    string TitleVi,
    string BodyVi,
    string IosUrl,
    string AndroidUrl,
    string ShareTextVi);

public static class FamilySoftLockGuides
{
    public const string IosScreenTimeUrl =
        "https://support.apple.com/vi-vn/HT208982";

    public const string AndroidFamilyLinkUrl =
        "https://families.google.com/familylink/";

    public static SoftLockGuideDto? ForConsequence(string? consequenceCode)
    {
        var code = (consequenceCode ?? "").Trim().ToLowerInvariant();
        var label = code switch
        {
            "screen_no_game_today" => "không chơi game hôm nay",
            "screen_reduce_15" => "giảm 15 phút Screen Time",
            "screen_reduce_30" => "giảm 30 phút Screen Time",
            "screen_reduce_30_weekend" => "giảm 30 phút Screen Time cuối tuần",
            "entertain_no_youtube" => "không xem YouTube hôm nay",
            _ => null,
        };
        if (label is null)
            return null;

        return new SoftLockGuideDto(
            TitleVi: "Khóa màn hình trên máy con",
            BodyVi:
                $"Đã áp dụng thỏa thuận nhà ({label}). FamilyOS không khóa máy giúp — " +
                "bật Screen Time (iPhone) hoặc Family Link (Android) để giới hạn app.",
            IosUrl: IosScreenTimeUrl,
            AndroidUrl: AndroidFamilyLinkUrl,
            ShareTextVi:
                $"Nhà mình áp dụng: {label}.\n" +
                $"iPhone: {IosScreenTimeUrl}\n" +
                $"Android: {AndroidFamilyLinkUrl}");
    }
}

public interface IFamilyOsParentPushService
{
    Task<FamilyParentPushStatusDto> GetStatusAsync(
        Guid familyId,
        Guid? membershipId,
        CancellationToken cancellationToken = default);

    Task SubscribeAsync(
        Guid familyId,
        FamilyParentPushSubscribeRequest request,
        CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(
        Guid familyId,
        Guid membershipId,
        string endpoint,
        CancellationToken cancellationToken = default);

    /// <summary>Worker tick: due_now / overdue push + evening digest.</summary>
    Task<int> DispatchDueParentRemindersAsync(CancellationToken cancellationToken = default);
}
