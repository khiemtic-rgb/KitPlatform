using KitPlatform.Application.CustomerApp;

namespace KitPlatform.Infrastructure.CustomerApp;

internal sealed class CustomerCareCardService : ICustomerCareCardService
{
    private readonly CustomerCareCardRepository _repo;

    public CustomerCareCardService(CustomerCareCardRepository repo) => _repo = repo;

    public async Task<CustomerCareCardDto?> GetTodayAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var today = VietnamToday();
        var birthTask = _repo.GetDateOfBirthAsync(tenantId, customerId, cancellationToken);
        var productsTask = _repo.ListRecentProductsAsync(
            tenantId, customerId, CustomerCareCardRules.LookbackDays, cancellationToken);
        await Task.WhenAll(birthTask, productsTask);

        return CustomerCareCardRules.Pick(new CustomerCareCardFacts(
            today,
            await birthTask,
            await productsTask));
    }

    internal static DateOnly VietnamToday()
    {
        var tz = ResolveVietnamZone();
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
    }

    private static TimeZoneInfo ResolveVietnamZone()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
