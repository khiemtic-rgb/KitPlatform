namespace KitPlatform.Application.Success;

public interface IOwnerCockpitService
{
    Task<OwnerCockpitDto> GetAsync(
        int expiryDays = 30,
        decimal lowStockThreshold = 10,
        int dormantDays = 30,
        int peakHoursWindowDays = 30,
        int urgentExpiryDays = 7,
        CancellationToken cancellationToken = default);
}
