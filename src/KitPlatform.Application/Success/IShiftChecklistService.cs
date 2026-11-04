namespace KitPlatform.Application.Success;

public interface IShiftChecklistService
{
    Task<ShiftChecklistTodayDto> GetTodayAsync(
        Guid? branchId,
        Guid? salesShiftId = null,
        CancellationToken cancellationToken = default);

    Task<ShiftChecklistRunDto> StartOrGetAsync(StartShiftChecklistRequest request, CancellationToken cancellationToken = default);

    Task<ShiftChecklistRunDto> SetItemCheckedAsync(
        Guid runId,
        Guid itemId,
        bool checkedValue,
        CancellationToken cancellationToken = default);

    Task<ShiftChecklistRunDto> CompleteAsync(Guid runId, CancellationToken cancellationToken = default);
}
