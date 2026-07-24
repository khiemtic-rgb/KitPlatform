using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyValueService : IFamilyValueService
{
    private readonly FamilyValueRepository _repo;
    private readonly FamilyGraphRepository _families;

    public FamilyValueService(FamilyValueRepository repo, FamilyGraphRepository families)
    {
        _repo = repo;
        _families = families;
    }

    public async Task<FamilyValueStateDto> GetStateAsync(
        Guid familyId,
        DateOnly? from = null,
        DateOnly? to = null,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var rangeTo = to ?? today;
        var rangeFrom = from ?? rangeTo.AddDays(-44);
        if (rangeTo < rangeFrom)
            (rangeFrom, rangeTo) = (rangeTo, rangeFrom);
        if (rangeTo.DayNumber - rangeFrom.DayNumber > 90)
            throw new InvalidOperationException("Khoảng value state tối đa 90 ngày.");

        var scores = await _repo.ListHealthScoresAsync(familyId, rangeFrom, rangeTo, cancellationToken);
        var nudges = await _repo.ListNudgeDaysAsync(familyId, rangeFrom, rangeTo, cancellationToken);
        var onboarding = await _repo.GetOnboardingAsync(familyId, cancellationToken);

        return new FamilyValueStateDto(
            scores.ToDictionary(r => r.ScoreDate.ToString("yyyy-MM-dd"), r => r.Score),
            nudges.ToDictionary(r => r.NudgeDate.ToString("yyyy-MM-dd"), r => r.NudgeCount),
            onboarding is null
                ? null
                : new FamilyOnboardingDto(onboarding.PayloadJson, onboarding.CompletedAt));
    }

    public async Task UpsertHealthScoreAsync(
        Guid familyId,
        FamilyHealthScoreUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (request.Score is < 0 or > 100)
            throw new InvalidOperationException("Score phải trong khoảng 0–100.");

        await _repo.UpsertHealthScoreAsync(
            familyId,
            request.ScoreDate,
            request.Score,
            request.BreakdownJson,
            cancellationToken);
    }

    public async Task<int> IncrementNudgeAsync(
        Guid familyId,
        FamilyNudgeIncrementRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var inc = Math.Max(1, request.Increment);
        return await _repo.IncrementNudgeAsync(familyId, request.NudgeDate, inc, cancellationToken);
    }

    public async Task SetNudgeCountAsync(
        Guid familyId,
        FamilyNudgeSetRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (request.Count < 0)
            throw new InvalidOperationException("Nudge count không được âm.");

        await _repo.SetNudgeCountAsync(familyId, request.NudgeDate, request.Count, cancellationToken);
    }

    public async Task UpsertOnboardingAsync(
        Guid familyId,
        FamilyOnboardingUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        if (string.IsNullOrWhiteSpace(request.PayloadJson))
            throw new InvalidOperationException("Onboarding payload bắt buộc.");

        var completedAt = request.CompletedAt ?? DateTimeOffset.UtcNow;
        await _repo.UpsertOnboardingAsync(familyId, request.PayloadJson, completedAt, cancellationToken);
    }

    public async Task ClearOnboardingAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        _ = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        await _repo.SoftDeleteOnboardingAsync(familyId, cancellationToken);
    }
}
