namespace PharmaCore.Application.CustomerApp;

public static class CustomerEngagementEventTypes
{
    public const string AiAsk = "ai_ask";
    public const string AppOpen = "app_open";
    public const string PushEnable = "push_enable";
}

public static class CustomerEngagementFunnelSteps
{
    public const string AppActive = "app_active";
    public const string ReminderOn = "reminder_on";
    public const string AiUsage = "ai_usage";
    public const string Chat = "chat";
    public const string OrderAgain = "order_again";
}

public sealed record CustomerEngagementFunnelStepDto(
    string Key,
    string Label,
    int Count,
    double RateFromCohort,
    double RateFromPrevious,
    double DeltaVsPriorPeriod);

public sealed record CustomerEngagementRetentionDto(
    int EligibleCount,
    int RetainedCount,
    double Rate,
    double DeltaVsPriorPeriod);

public sealed record CustomerEngagementAlertDto(
    string Key,
    string Severity,
    string Message);

public sealed record CustomerEngagementOverviewDto(
    int PeriodDays,
    int CohortSize,
    IReadOnlyList<CustomerEngagementFunnelStepDto> Funnel,
    CustomerEngagementRetentionDto Retention30d,
    IReadOnlyList<CustomerEngagementAlertDto> Alerts);

public sealed record CustomerEngagementDrillDownItemDto(
    Guid AccountId,
    Guid CustomerId,
    string FullName,
    string Phone,
    DateTimeOffset? LastLoginAt,
    DateTimeOffset? FirstLoginAt);

public sealed record CustomerEngagementDrillDownResultDto(
    string Step,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<CustomerEngagementDrillDownItemDto> Items);

public interface ICustomerEngagementEventService
{
    Task RecordEventAsync(
        Guid tenantId,
        Guid accountId,
        Guid customerId,
        string eventType,
        IReadOnlyDictionary<string, object?>? metadata = null,
        CancellationToken cancellationToken = default);

    Task TryRecordDailyAppOpenAsync(
        Guid tenantId,
        Guid accountId,
        Guid customerId,
        CancellationToken cancellationToken = default);
}

public interface ICustomerEngagementAnalyticsService
{
    Task<CustomerEngagementOverviewDto> GetOverviewAsync(
        int periodDays = 30,
        CancellationToken cancellationToken = default);

    Task<CustomerEngagementDrillDownResultDto> GetDrillDownAsync(
        string step,
        int periodDays = 30,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
