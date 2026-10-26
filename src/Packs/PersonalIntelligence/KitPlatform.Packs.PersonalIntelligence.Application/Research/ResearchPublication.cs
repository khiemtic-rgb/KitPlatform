using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.Research;

public static class ResearchAvailability
{
    public const string Fresh = "FRESH";
    public const string Stale = "STALE";
    public const string Missing = "MISSING";
    public const string Unreadable = "UNREADABLE";
    public const string Inconsistent = "INCONSISTENT";
    public const string UnsupportedVersion = "UNSUPPORTED_VERSION";
    public const string PresentUntimed = "PRESENT_UNTIMED";
}

public static class ResearchPublicationSchema
{
    public const string V1 = "research-publication/v1";
    public const string Publisher = "research-lab-v1";
}

public sealed class ResearchPublicationOptions
{
    public const string SectionName = "PersonalIntelligence:ResearchPublication";

    public string? Root { get; set; }
}

public sealed record ResearchReadError(string Code, string Message);

public sealed record ResearchObjectRead(
    string Id,
    string Kind,
    string Source,
    string Availability,
    string? ContentSha256,
    DateTimeOffset? PublishedAt);

public sealed record ResearchExperimentRead(
    string ExperimentId,
    string? StrategyId,
    string? StrategyVersion,
    string? Family,
    string? Symbol,
    string? Timeframe,
    string? DataSource,
    string? SnapshotSha256,
    string? ApprovalStatus,
    string? EvidenceStatus,
    string? PerformanceClaim,
    string? RiskLimitsApproval,
    bool? SelectedByPnl,
    bool? ParametersChosenOnOutOfSample,
    JsonElement? Development,
    JsonElement? OutOfSample,
    bool? LiveOrders,
    int? ExchangeOrdersSent);

public sealed record ResearchPaperStatusRead(
    string Availability,
    string? RunnerStatus,
    string? CycleOutcome,
    string? LastRunAt,
    long? LastProcessedOpenTime,
    string? LastError,
    string? Health,
    long? LastBarAgeMs,
    bool? LiveOrders,
    int? ExchangeOrdersSent,
    string? EffectiveLiveMode,
    string? RiskLimitsApproval,
    int? ExtensionCount,
    string? ExtensionSha256);

public sealed record ResearchPaperStrategyRead(
    string? ExperimentId,
    int? ExtensionBarsRepresented,
    int? NoSignalCycles,
    int? SimulatedEntries,
    int? ClosedTrades,
    int? OpenTrades,
    int? RiskBlocks,
    bool? NoTradeIsNotAWinOrLoss,
    string? NetPnl,
    string? ProfitFactor,
    string? MaxDrawdownFraction,
    string? Expectancy,
    string? Fees,
    string? Funding,
    string? Slippage,
    string? StandAsideNetPnl,
    string? InSampleOutOfSample,
    string? SampleWarning,
    string? FillModel,
    string? SignalTime,
    string? AssumedExecutionTime,
    bool? ExchangeFill);

public sealed record ResearchPaperReportRead(
    string Availability,
    string? ReportKind,
    string? Symbol,
    string? Timeframe,
    int? ExtensionBarCount,
    string? ExtensionStartUtc,
    string? ExtensionEndUtc,
    IReadOnlyList<ResearchPaperStrategyRead> Strategies,
    string LabResult,
    string? PerformanceClaim,
    bool? LiveOrders,
    int? ExchangeOrdersSent,
    string? EffectiveLiveMode);

public sealed record ResearchPublicationRead(
    string Availability,
    string? SchemaVersion,
    string? PublicationId,
    DateTimeOffset? PublishedAt,
    string? Publisher,
    IReadOnlyList<ResearchObjectRead> Objects,
    ResearchReadError? ReadError,
    IReadOnlyList<ResearchExperimentRead> Experiments,
    ResearchPaperStatusRead? PaperStatus,
    ResearchPaperReportRead? PaperReport);

public interface IResearchPublicationReader
{
    Task<ResearchPublicationRead> ReadAsync(CancellationToken cancellationToken = default);
}

public static class ResearchExperimentIds
{
    public static bool IsCatalogId(string? experimentId)
    {
        if (string.IsNullOrEmpty(experimentId) || experimentId.Length > 80)
            return false;
        if (!IsToken(experimentId[0]))
            return false;
        for (var i = 1; i < experimentId.Length; i++)
        {
            var c = experimentId[i];
            if (!IsToken(c) && c != '_' && c != '-')
                return false;
        }

        return true;
    }

    static bool IsToken(char c) => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
