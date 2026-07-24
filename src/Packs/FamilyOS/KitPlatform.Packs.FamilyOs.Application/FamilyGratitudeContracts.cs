namespace KitPlatform.Packs.FamilyOs;

public sealed record FamilyChildGratitudeDto(
    Guid Id,
    Guid FamilyId,
    Guid FromMemberId,
    string FromMemberName,
    Guid? ToMemberId,
    string? ToMemberName,
    DateOnly FlowDate,
    string MessageVi,
    string? PraiseContext,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool AlreadySent);

public sealed record FamilyChildGratitudeSendRequest(
    Guid FromMemberId,
    DateOnly? FlowDate,
    string? PraiseContext,
    string? MessageVi);

public interface IFamilyGratitudeService
{
    Task<IReadOnlyList<FamilyChildGratitudeDto>> ListAsync(
        Guid familyId,
        DateOnly? flowDate = null,
        Guid? fromMemberId = null,
        CancellationToken cancellationToken = default);

    Task<FamilyChildGratitudeDto> SendAsync(
        Guid familyId,
        FamilyChildGratitudeSendRequest request,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        Guid familyId,
        Guid gratitudeId,
        CancellationToken cancellationToken = default);
}
