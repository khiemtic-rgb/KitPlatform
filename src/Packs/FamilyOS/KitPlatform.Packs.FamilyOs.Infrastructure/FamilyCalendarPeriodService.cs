using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyCalendarPeriodService : IFamilyCalendarPeriodService
{
    private readonly FamilyCalendarPeriodRepository _repo;
    private readonly FamilyDayFlowRepository _dayFlows;
    private readonly FamilyGraphRepository _families;
    private readonly IFamilyDayFlowService _dayFlowService;

    public FamilyCalendarPeriodService(
        FamilyCalendarPeriodRepository repo,
        FamilyDayFlowRepository dayFlows,
        FamilyGraphRepository families,
        IFamilyDayFlowService dayFlowService)
    {
        _repo = repo;
        _dayFlows = dayFlows;
        _families = families;
        _dayFlowService = dayFlowService;
    }

    public async Task<IReadOnlyList<CalendarPeriodDto>> ListAsync(
        Guid familyId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var periods = await _repo.ListPeriodsAsync(familyId, cancellationToken);
        var slots = await _repo.ListSlotsForPeriodsAsync(
            periods.Select(p => p.Id).ToList(), cancellationToken);
        var byPeriod = slots.GroupBy(s => s.PeriodId).ToDictionary(g => g.Key, g => g.ToList());
        return periods.Select(p => Map(p, byPeriod.GetValueOrDefault(p.Id) ?? [])).ToList();
    }

    public async Task<CalendarPeriodDto?> GetAsync(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var period = await _repo.GetPeriodAsync(familyId, periodId, cancellationToken);
        if (period is null) return null;
        var slots = await _repo.ListSlotsAsync(periodId, cancellationToken);
        return Map(period, slots);
    }

    public async Task<CalendarPeriodDto> CreateAsync(
        Guid familyId,
        CreateCalendarPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);

        var code = (request.Code ?? "").Trim().ToLowerInvariant();
        var name = (request.DisplayName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("code và displayName là bắt buộc.");

        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? FamilyCalendarPeriodKinds.Custom
            : request.Kind.Trim().ToLowerInvariant();
        if (!FamilyCalendarPeriodKinds.All.Contains(kind))
            throw new InvalidOperationException(
                "kind phải là school_year | summer | exam | travel | holiday | custom.");

        if (request.EndDate < request.StartDate)
            throw new InvalidOperationException("endDate phải >= startDate.");

        var priority = request.Priority ?? FamilyCalendarPeriodKinds.DefaultPriority(kind);
        var isActive = request.IsActive ?? true;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        var slots = await NormalizeSlotsAsync(familyId, request.Slots, cancellationToken);

        Guid periodId;
        try
        {
            periodId = await _repo.InsertPeriodAsync(
                familyId, code, name, kind,
                request.StartDate, request.EndDate, priority, isActive, notes,
                cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("uq_calendar_period_family_code", StringComparison.OrdinalIgnoreCase)
                                   || ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Mã kỳ '{code}' đã tồn tại.");
        }

        if (slots.Count > 0)
            await _repo.ReplaceSlotsAsync(periodId, slots, cancellationToken);

        await TryRebuildTodayAsync(familyId, request.StartDate, request.EndDate, cancellationToken);
        return (await GetAsync(familyId, periodId, cancellationToken))!;
    }

    public async Task<CalendarPeriodDto> UpdateAsync(
        Guid familyId,
        Guid periodId,
        UpdateCalendarPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var existing = await _repo.GetPeriodAsync(familyId, periodId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kỳ lịch.");

        var name = string.IsNullOrWhiteSpace(request.DisplayName)
            ? existing.DisplayName
            : request.DisplayName.Trim();
        var kind = string.IsNullOrWhiteSpace(request.Kind)
            ? existing.Kind
            : request.Kind.Trim().ToLowerInvariant();
        if (!FamilyCalendarPeriodKinds.All.Contains(kind))
            throw new InvalidOperationException(
                "kind phải là school_year | summer | exam | travel | holiday | custom.");

        var start = request.StartDate ?? existing.StartDate;
        var end = request.EndDate ?? existing.EndDate;
        if (end < start)
            throw new InvalidOperationException("endDate phải >= startDate.");

        var priority = request.Priority ?? existing.Priority;
        var isActive = request.IsActive ?? existing.IsActive;
        var notes = request.Notes is null
            ? existing.Notes
            : (string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        await _repo.UpdatePeriodAsync(
            familyId, periodId, name, kind, start, end, priority, isActive, notes,
            cancellationToken);

        if (request.Slots is not null)
        {
            var slots = await NormalizeSlotsAsync(familyId, request.Slots, cancellationToken);
            await _repo.ReplaceSlotsAsync(periodId, slots, cancellationToken);
        }

        await TryRebuildTodayAsync(familyId, start, end, cancellationToken);
        return (await GetAsync(familyId, periodId, cancellationToken))!;
    }

    public async Task DeleteAsync(
        Guid familyId,
        Guid periodId,
        CancellationToken cancellationToken = default)
    {
        await EnsureFamilyAsync(familyId, cancellationToken);
        var existingPeriod = await _repo.GetPeriodAsync(familyId, periodId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy kỳ lịch.");
        await _repo.SoftDeletePeriodAsync(familyId, periodId, cancellationToken);
        await TryRebuildTodayAsync(familyId, existingPeriod.StartDate, existingPeriod.EndDate, cancellationToken);
    }

    public async Task<ResolvedCalendarRoutineDto> ResolveAsync(
        Guid familyId,
        DateOnly? flowDate,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var date = flowDate
            ?? DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var isoDow = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;

        var periodPick = await _repo.PickPeriodRoutineAsync(
            familyId, date, (short)isoDow, cancellationToken);
        if (periodPick is not null)
        {
            return new ResolvedCalendarRoutineDto(
                date,
                isoDow,
                periodPick.RoutineId,
                periodPick.RoutineDisplayName,
                "period",
                periodPick.PeriodId,
                periodPick.PeriodDisplayName,
                periodPick.PeriodKind);
        }

        var routine = await _dayFlows.PickRoutineForDateAsync(
            familyId, date, preferredRoutineId: null, cancellationToken, skipPeriodLookup: true)
            ?? throw new InvalidOperationException("Chưa có routine active để áp dụng.");

        return new ResolvedCalendarRoutineDto(
            date,
            isoDow,
            routine.Id,
            routine.DisplayName,
            "weekday",
            null,
            null,
            null);
    }


    private async Task TryRebuildTodayAsync(
        Guid familyId,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken);
        if (family is null) return;
        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        if (today < start || today > end) return;
        if (await _dayFlows.GetByDateAsync(familyId, today, cancellationToken) is null) return;

        try
        {
            await _dayFlowService.EnsureDayFlowAsync(
                familyId,
                new EnsureDayFlowRequest(today, null, ForceRebuild: true),
                cancellationToken);
        }
        catch
        {
            // Period save must succeed even when no routine can be resolved yet.
        }
    }

    private async Task EnsureFamilyAsync(Guid familyId, CancellationToken cancellationToken)
    {
        if (!await _repo.FamilyExistsAsync(familyId, cancellationToken))
            throw new InvalidOperationException("Không tìm thấy gia đình.");
    }

    private async Task<IReadOnlyList<(IReadOnlyList<int> Weekdays, Guid RoutineId, int SortOrder)>> NormalizeSlotsAsync(
        Guid familyId,
        IReadOnlyList<CalendarPeriodSlotInput>? slots,
        CancellationToken cancellationToken)
    {
        if (slots is null || slots.Count == 0) return [];

        var result = new List<(IReadOnlyList<int>, Guid, int)>();
        var order = 0;
        foreach (var slot in slots)
        {
            var weekdays = (slot.Weekdays ?? [])
                .Where(d => d is >= 1 and <= 7)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
            if (weekdays.Count == 0)
                throw new InvalidOperationException("Mỗi slot cần ít nhất một weekday (1=T2 … 7=CN).");

            if (!await _repo.RoutineExistsAsync(familyId, slot.RoutineId, cancellationToken))
                throw new InvalidOperationException("Routine trong slot không tồn tại hoặc đã tắt.");

            result.Add((weekdays, slot.RoutineId, slot.SortOrder ?? order));
            order++;
        }

        return result;
    }

    private static CalendarPeriodDto Map(
        FamilyCalendarPeriodRepository.PeriodRow period,
        IReadOnlyList<FamilyCalendarPeriodRepository.SlotRow> slots)
    {
        return new CalendarPeriodDto(
            period.Id,
            period.FamilyId,
            period.Code,
            period.DisplayName,
            period.Kind,
            period.StartDate,
            period.EndDate,
            period.Priority,
            period.IsActive,
            period.Notes,
            slots.Select(s => new CalendarPeriodSlotDto(
                s.Id,
                s.PeriodId,
                s.Weekdays?.ToList() ?? [],
                s.RoutineId,
                s.RoutineDisplayName,
                s.SortOrder)).ToList());
    }
}
