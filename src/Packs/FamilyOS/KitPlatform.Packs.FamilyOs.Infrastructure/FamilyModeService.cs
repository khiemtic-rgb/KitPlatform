using KitPlatform.Packs.FamilyOs;

namespace KitPlatform.Packs.FamilyOs.Infrastructure;

internal sealed class FamilyModeService : IFamilyModeService
{
    private readonly IFamilyCalendarPeriodService _periods;
    private readonly IFamilyRoutineService _routines;
    private readonly FamilyGraphRepository _families;

    public FamilyModeService(
        IFamilyCalendarPeriodService periods,
        IFamilyRoutineService routines,
        FamilyGraphRepository families)
    {
        _periods = periods;
        _routines = routines;
        _families = families;
    }

    public async Task<FamilyModeActivateResult> ActivateAsync(
        Guid familyId,
        FamilyModeActivateRequest request,
        CancellationToken cancellationToken = default)
    {
        var family = await _families.GetFamilyAsync(familyId, cancellationToken)
            ?? throw new InvalidOperationException("Không tìm thấy gia đình.");

        var mode = (request.Mode ?? "").Trim().ToLowerInvariant();
        if (!FamilyModeKinds.All.Contains(mode))
            throw new InvalidOperationException(
                "Mode phải là normal | summer | exam | travel | weekend | holiday.");

        var today = DateOnly.FromDateTime(FamilyTimeZones.NowIn(family.Timezone).DateTime);
        var start = request.StartDate ?? today;
        var end = request.EndDate ?? mode switch
        {
            FamilyModeKinds.Travel => start.AddDays(3),
            FamilyModeKinds.Weekend => start.AddDays(1),
            FamilyModeKinds.Exam => start.AddDays(14),
            FamilyModeKinds.Summer => start.AddDays(60),
            FamilyModeKinds.Holiday => start.AddDays(5),
            _ => start.AddDays(30),
        };
        if (end < start) end = start;

        if (mode == FamilyModeKinds.Normal)
        {
            // Soft-deactivate overlapping high-priority seasonal periods by ending them yesterday.
            var existing = await _periods.ListAsync(familyId, cancellationToken);
            foreach (var p in existing.Where(p => p.IsActive
                         && p.StartDate <= today
                         && p.EndDate >= today
                         && p.Kind is not ("school_year")))
            {
                var newEnd = today.AddDays(-1);
                if (newEnd >= p.StartDate)
                {
                    await _periods.UpdateAsync(
                        familyId,
                        p.Id,
                        new UpdateCalendarPeriodRequest(
                            DisplayName: null,
                            Kind: null,
                            StartDate: null,
                            EndDate: newEnd,
                            Priority: null,
                            IsActive: true,
                            Notes: "Kết thúc sớm — về chế độ Bình thường",
                            Slots: null),
                        cancellationToken);
                }
            }

            var routinesNormal = await _routines.ListRoutinesAsync(familyId, cancellationToken);
            var primaryNormal = PickRoutine(routinesNormal, FamilyModeKinds.Normal, weekend: false)
                ?? routinesNormal.FirstOrDefault();
            return new FamilyModeActivateResult(
                FamilyModeKinds.Normal,
                FamilyModeKinds.LabelVi(FamilyModeKinds.Normal),
                null,
                "Đã về chế độ Bình thường. Routine theo lịch năm học / mặc định.",
                primaryNormal?.Id,
                primaryNormal?.DisplayName,
                primaryNormal?.Templates.Count(t => t.IsActive) ?? 0);
        }

        var routines = await _routines.ListRoutinesAsync(familyId, cancellationToken);
        var weekdayRoutine = PickRoutine(routines, mode, weekend: false);
        var weekendRoutine = PickRoutine(routines, mode, weekend: true)
            ?? weekdayRoutine
            ?? routines.FirstOrDefault();

        if (weekdayRoutine is null && weekendRoutine is null)
            throw new InvalidOperationException(
                "Chưa có Routine nào để gán chế độ. Hãy hoàn tất Setup Wizard trước.");

        var periodKind = mode switch
        {
            FamilyModeKinds.Weekend => FamilyCalendarPeriodKinds.Custom,
            FamilyModeKinds.Summer => FamilyCalendarPeriodKinds.Summer,
            FamilyModeKinds.Exam => FamilyCalendarPeriodKinds.Exam,
            FamilyModeKinds.Travel => FamilyCalendarPeriodKinds.Travel,
            FamilyModeKinds.Holiday => FamilyCalendarPeriodKinds.Holiday,
            _ => FamilyCalendarPeriodKinds.Custom,
        };

        var slots = new List<CalendarPeriodSlotInput>();
        if (mode == FamilyModeKinds.Weekend)
        {
            if (weekendRoutine is not null)
                slots.Add(new CalendarPeriodSlotInput([6, 7], weekendRoutine.Id, 0));
        }
        else
        {
            if (weekdayRoutine is not null)
                slots.Add(new CalendarPeriodSlotInput([1, 2, 3, 4, 5], weekdayRoutine.Id, 0));
            if (weekendRoutine is not null)
                slots.Add(new CalendarPeriodSlotInput([6, 7], weekendRoutine.Id, 1));
        }

        var code = $"mode_{mode}_{start:yyyyMMdd}";
        var name = $"{FamilyModeKinds.LabelVi(mode)} ({start:dd/MM}–{end:dd/MM})";

        CalendarPeriodDto period;
        try
        {
            period = await _periods.CreateAsync(
                familyId,
                new CreateCalendarPeriodRequest(
                    code,
                    name,
                    periodKind,
                    start,
                    end,
                    FamilyCalendarPeriodKinds.DefaultPriority(periodKind),
                    true,
                    $"Family Mode · kích hoạt 1 chạm",
                    slots),
                cancellationToken);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("đã tồn tại", StringComparison.OrdinalIgnoreCase))
        {
            var list = await _periods.ListAsync(familyId, cancellationToken);
            var existing = list.FirstOrDefault(p =>
                string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase));
            if (existing is null) throw;
            period = await _periods.UpdateAsync(
                familyId,
                existing.Id,
                new UpdateCalendarPeriodRequest(
                    name, periodKind, start, end,
                    FamilyCalendarPeriodKinds.DefaultPriority(periodKind),
                    true,
                    "Family Mode · cập nhật",
                    slots),
                cancellationToken);
        }

        var primary = weekdayRoutine ?? weekendRoutine;
        return new FamilyModeActivateResult(
            mode,
            FamilyModeKinds.LabelVi(mode),
            period,
            $"Đã bật chế độ {FamilyModeKinds.LabelVi(mode)} đến {end:dd/MM}. AI đã đổi mapping Routine — không cần vào Settings.",
            primary?.Id,
            primary?.DisplayName,
            primary?.Templates.Count(t => t.IsActive) ?? 0);
    }

    private static RoutineDto? PickRoutine(
        IReadOnlyList<RoutineDto> routines,
        string mode,
        bool weekend)
    {
        string[] preferKinds = (mode, weekend) switch
        {
            (FamilyModeKinds.Exam, false) => [FamilyRoutineKinds.Exam, FamilyRoutineKinds.SchoolDay],
            (FamilyModeKinds.Travel, _) => [FamilyRoutineKinds.Travel, FamilyRoutineKinds.Holiday, FamilyRoutineKinds.Weekend],
            (FamilyModeKinds.Summer, false) => [FamilyRoutineKinds.Holiday, FamilyRoutineKinds.Weekend, FamilyRoutineKinds.SchoolDay],
            (FamilyModeKinds.Summer, true) => [FamilyRoutineKinds.Weekend, FamilyRoutineKinds.Holiday],
            (FamilyModeKinds.Holiday, _) => [FamilyRoutineKinds.Holiday, FamilyRoutineKinds.Weekend],
            (FamilyModeKinds.Weekend, _) => [FamilyRoutineKinds.Weekend, FamilyRoutineKinds.Holiday],
            (_, true) => [FamilyRoutineKinds.Weekend, FamilyRoutineKinds.Holiday],
            _ => [FamilyRoutineKinds.SchoolDay, FamilyRoutineKinds.Custom],
        };

        foreach (var kind in preferKinds)
        {
            var hit = routines.FirstOrDefault(r =>
                string.Equals(r.Kind, kind, StringComparison.OrdinalIgnoreCase));
            if (hit is not null) return hit;
        }

        return routines.FirstOrDefault();
    }
}
