namespace PharmaCore.Application.Configuration;

public interface ITenantSettingsService
{
    Task<TenantBatchMode> GetBatchModeAsync(CancellationToken cancellationToken = default);
}
