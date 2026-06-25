namespace PharmaCore.Application.Integration;

public interface IIntegrationOutboxQuery
{
    Task<IReadOnlyList<IntegrationOutboxItemDto>> ListRecentAsync(int limit = 50, CancellationToken cancellationToken = default);
}
