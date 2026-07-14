namespace KitPlatform.Application.Success;

public interface ILossPreventionService
{
    Task<LossCashVarianceTodayDto> GetCashVarianceTodayAsync(
        decimal? threshold = null,
        CancellationToken cancellationToken = default);

    Task<OwnerCockpitRiskStripDto> GetRiskStripAsync(
        decimal? cashVarianceThreshold = null,
        CancellationToken cancellationToken = default);

    Task<LossEmployeeReportsDto> GetEmployeeReportsAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task<LossAuditFeedDto> GetAuditFeedAsync(
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        Guid? branchId = null,
        Guid? userId = null,
        string? eventType = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
