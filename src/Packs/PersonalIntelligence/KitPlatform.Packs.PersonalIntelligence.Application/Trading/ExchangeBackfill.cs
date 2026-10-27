namespace KitPlatform.Packs.PersonalIntelligence.Trading;

public static class ExchangeBackfillStatus
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Interrupted = "INTERRUPTED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
    public const string Paused = "PAUSED";
}

public sealed class ExchangeBackfillRequest
{
    public string? Mode { get; set; }
    public string? ProductSubtype { get; set; }
}

public sealed class ExchangeBackfillJobDto
{
    public Guid JobId { get; set; }
    public Guid ConnectionId { get; set; }
    public string ExchangeCode { get; set; } = "";
    public string Environment { get; set; } = "";
    public string MarketType { get; set; } = "";
    public string ProductSubtype { get; set; } = "";
    public string Status { get; set; } = "";
    public string Mode { get; set; } = "";
    public DateTimeOffset? WindowStart { get; set; }
    public DateTimeOffset? WindowEnd { get; set; }
    public DateTimeOffset? OldestExecAt { get; set; }
    public DateTimeOffset? NewestExecAt { get; set; }
    public DateTimeOffset? CutoffAt { get; set; }
    public string? CutoffReason { get; set; }
    public int Pages { get; set; }
    public int Batches { get; set; }
    public int FetchedCount { get; set; }
    public int InsertedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class ExchangeBackfillOptions
{
    public const string SectionName = "PersonalIntelligence:ExchangeBackfill";

    public bool WorkerEnabled { get; set; } = true;
    public int DelayMilliseconds { get; set; } = 200;
    public int StepsPerPump { get; set; } = 20;
}

public interface IExchangeBackfill
{
    Task EnqueueInitialAsync(Guid connectionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExchangeBackfillJobDto>> ListAsync(Guid? connectionId, string? marketType, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExchangeBackfillJobDto>> RequestAsync(Guid connectionId, ExchangeBackfillRequest request, CancellationToken cancellationToken);
    Task<ExchangeBackfillJobDto> PauseAsync(Guid jobId, CancellationToken cancellationToken);
    Task<ExchangeBackfillJobDto> ResumeAsync(Guid jobId, CancellationToken cancellationToken);
    Task<ExchangeBackfillJobDto> RetryAsync(Guid jobId, CancellationToken cancellationToken);
    Task PauseOpenAsync(Guid connectionId, string reason, CancellationToken cancellationToken);
    Task<bool> PumpAsync(CancellationToken cancellationToken);
}
