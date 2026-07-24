namespace KitPlatform.Packs.FamilyOs;

public static class FamilyMoodCodes
{
    public const string Mad = "mad";
    public const string Sad = "sad";
    public const string Ok = "ok";
    public const string Happy = "happy";
    public const string Love = "love";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Mad, Sad, Ok, Happy, Love,
    };
}

public sealed record FamilyMemberMoodDto(
    Guid Id,
    Guid FamilyId,
    Guid MemberId,
    string MemberName,
    DateOnly FlowDate,
    string MoodCode,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record FamilyMemberMoodUpsertRequest(
    DateOnly? FlowDate,
    string MoodCode,
    string? Note);

public interface IFamilyMoodService
{
    Task<IReadOnlyList<FamilyMemberMoodDto>> ListFamilyMoodsAsync(
        Guid familyId,
        DateOnly flowDate,
        CancellationToken cancellationToken = default);

    Task<FamilyMemberMoodDto?> GetMemberMoodAsync(
        Guid familyId,
        Guid memberId,
        DateOnly flowDate,
        CancellationToken cancellationToken = default);

    Task<FamilyMemberMoodDto> UpsertMemberMoodAsync(
        Guid familyId,
        Guid memberId,
        FamilyMemberMoodUpsertRequest request,
        CancellationToken cancellationToken = default);
}
