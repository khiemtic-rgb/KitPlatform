namespace KitPlatform.Packs.FamilyOs;

public static class FamilyMirrorAgentStatuses
{
    public const string Online = "online";
    public const string Offline = "offline";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Online, Offline,
    };
}

public static class FamilyMirrorUsageKinds
{
    public const string App = "app";
    public const string Web = "web";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        App, Web,
    };
}

public static class FamilyMirrorParentNoteTones
{
    public const string Praise = "praise";
    public const string Soft = "soft";
    public const string Renegotiate = "renegotiate";

    public static readonly HashSet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Praise, Soft, Renegotiate,
    };
}

public sealed record FamilyMirrorAppSliceDto(
    string AppKey,
    string AppLabel,
    string Kind,
    int Seconds);

public sealed record FamilyMirrorParentNoteDto(
    Guid Id,
    Guid MemberId,
    DateOnly FlowDate,
    Guid FromMembershipId,
    string FromMemberName,
    string Tone,
    string BodyVi,
    DateTimeOffset CreatedAt);

public sealed record FamilyMirrorDayDto(
    DateOnly FlowDate,
    Guid MemberId,
    string MemberName,
    bool AgentOnline,
    DateTimeOffset? LastHeartbeatAt,
    string? LastForegroundApp,
    IReadOnlyList<FamilyMirrorAppSliceDto> TopApps,
    int TotalSeconds,
    string? InsightVi,
    IReadOnlyList<string> SuggestedActions,
    IReadOnlyList<FamilyMirrorParentNoteDto> ParentNotes);

public sealed record FamilyMirrorHeartbeatRequest(
    string DeviceId,
    Guid MemberId,
    string? DeviceLabel,
    string? AgentVersion,
    string? LastForegroundApp);

public sealed record FamilyMirrorUsageItemRequest(
    string AppKey,
    string? AppLabel,
    string? Kind,
    int Seconds);

public sealed record FamilyMirrorUsageIngestRequest(
    Guid MemberId,
    DateOnly? FlowDate,
    IReadOnlyList<FamilyMirrorUsageItemRequest> Items);

public sealed record FamilyMirrorParentNoteRequest(
    Guid MemberId,
    DateOnly? FlowDate,
    Guid FromMembershipId,
    string Tone,
    string BodyVi);

public interface IFamilyDigitalMirrorService
{
    Task HeartbeatAsync(
        Guid familyId,
        FamilyMirrorHeartbeatRequest request,
        CancellationToken cancellationToken = default);

    Task IngestUsageAsync(
        Guid familyId,
        FamilyMirrorUsageIngestRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilyMirrorDayDto> GetDayAsync(
        Guid familyId,
        Guid? memberId = null,
        DateOnly? flowDate = null,
        CancellationToken cancellationToken = default);

    Task<FamilyMirrorParentNoteDto> PostParentNoteAsync(
        Guid familyId,
        FamilyMirrorParentNoteRequest request,
        CancellationToken cancellationToken = default);
}
