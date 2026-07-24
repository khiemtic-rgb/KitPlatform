namespace KitPlatform.Packs.FamilyOs;

public sealed record FamilyValueStateDto(
    IReadOnlyDictionary<string, int> HealthScores,
    IReadOnlyDictionary<string, int> NudgeCounts,
    FamilyOnboardingDto? Onboarding);

public sealed record FamilyOnboardingDto(
    string PayloadJson,
    DateTimeOffset CompletedAt);

public sealed record FamilyHealthScoreUpsertRequest(
    DateOnly ScoreDate,
    int Score,
    string? BreakdownJson);

public sealed record FamilyNudgeIncrementRequest(
    DateOnly NudgeDate,
    int Increment = 1);

public sealed record FamilyNudgeSetRequest(
    DateOnly NudgeDate,
    int Count);

public sealed record FamilyOnboardingUpsertRequest(
    string PayloadJson,
    DateTimeOffset? CompletedAt);

public interface IFamilyValueService
{
    Task<FamilyValueStateDto> GetStateAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default);

    Task UpsertHealthScoreAsync(
        Guid familyId,
        FamilyHealthScoreUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<int> IncrementNudgeAsync(
        Guid familyId,
        FamilyNudgeIncrementRequest request,
        CancellationToken cancellationToken = default);

    Task SetNudgeCountAsync(
        Guid familyId,
        FamilyNudgeSetRequest request,
        CancellationToken cancellationToken = default);

    Task UpsertOnboardingAsync(
        Guid familyId,
        FamilyOnboardingUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task ClearOnboardingAsync(
        Guid familyId,
        CancellationToken cancellationToken = default);
}
