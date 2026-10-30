using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.PersonalIntelligence.Research;

/// <summary>
/// Read-only comparison of experiments already present in one publication.
/// It does not recompute returns, drawdown, or an equity curve, and it does not write a ledger.
/// </summary>
public static class ResearchEvaluationProjector
{
    public const string Mode = "publication_projection";
    public const string NotSelectable = "NOT_SELECTABLE";
    public const string InsufficientData = "INSUFFICIENT_DATA";
    public const string NotInSource = "NOT_IN_SOURCE";
    public const string Low = "LOW";
    public const string None = "NONE";

    static readonly string[] LimitationCodes =
    [
        "NOT_A_PERFORMANCE_CLAIM",
        "NOT_LIVE_TRADING",
        "NO_EQUITY_CURVE",
        "NO_RECOMPUTE",
        "NOT_PAPER_LEDGER",
    ];

    public static ResearchEvaluationComparison Project(ResearchPublicationRead read)
    {
        if (!Readable(read))
        {
            return new ResearchEvaluationComparison(
                read.Availability,
                Mode,
                read.PublicationId,
                null,
                false,
                InsufficientData,
                None,
                WarningList(read),
                LimitationCodes,
                [],
                read.ReadError);
        }

        var rows = read.Experiments.Select(item => Row(read, item)).ToArray();
        var snapshot = SharedSnapshot(rows);
        var warnings = new List<string>();
        if (read.ReadError is not null)
            warnings.Add(read.ReadError.Code);
        if (snapshot is null)
            warnings.Add("SNAPSHOT_MISMATCH");
        if (rows.Length == 0)
            warnings.Add(InsufficientData);
        foreach (var row in rows)
        {
            foreach (var warning in row.Warnings)
            {
                if (!warnings.Contains(warning, StringComparer.Ordinal))
                    warnings.Add(warning);
            }
        }

        var verdict = snapshot is null || rows.Length == 0
            ? InsufficientData
            : rows.Any(row => row.Verdict == NotSelectable) ? NotSelectable : InsufficientData;
        return new ResearchEvaluationComparison(
            read.Availability,
            Mode,
            read.PublicationId,
            snapshot,
            snapshot is not null,
            verdict,
            rows.Length == 0 ? None : Low,
            warnings,
            LimitationCodes,
            rows,
            read.ReadError);
    }

    public static ResearchEvaluationSingle ProjectOne(ResearchPublicationRead read, string experimentId)
    {
        if (!Readable(read))
        {
            return new ResearchEvaluationSingle(read.Availability, null, false, read.ReadError);
        }

        var row = read.Experiments.FirstOrDefault(item => item.ExperimentId == experimentId);
        if (row is null)
            return new ResearchEvaluationSingle(read.Availability, null, true, null);
        return new ResearchEvaluationSingle(read.Availability, Row(read, row), false, null);
    }

    static ResearchEvaluationRow Row(ResearchPublicationRead read, ResearchExperimentRead experiment)
    {
        var development = Sample("development", experiment.Development);
        var outOfSample = Sample("out_of_sample", experiment.OutOfSample);
        var warnings = new List<string>();
        AddClaim(warnings, experiment.PerformanceClaim);
        AddClaim(warnings, experiment.ApprovalStatus);
        AddClaim(warnings, experiment.EvidenceStatus);
        AddClaim(warnings, development?.PerformanceClaim);
        AddClaim(warnings, outOfSample?.PerformanceClaim);
        if (development is null || outOfSample is null)
            warnings.Add(InsufficientData);
        if (string.IsNullOrWhiteSpace(experiment.SnapshotSha256))
            warnings.Add("SNAPSHOT_MISSING");
        if (experiment.SelectedByPnl == true)
            warnings.Add("SELECTED_BY_PNL");
        if (experiment.ParametersChosenOnOutOfSample == true)
            warnings.Add("PARAMETERS_CHOSEN_ON_OUT_OF_SAMPLE");

        var claim = experiment.PerformanceClaim;
        var sourceRefusesSelection = string.IsNullOrWhiteSpace(claim)
            || claim.Contains("NOT_A_PERFORMANCE_CLAIM", StringComparison.Ordinal)
            || claim.Contains("NOT_A_VERIFIED_PERFORMANCE_CLAIM", StringComparison.Ordinal)
            || experiment.ApprovalStatus == "EXPERIMENT_NOT_APPROVED"
            || experiment.EvidenceStatus == "INSUFFICIENT_EVIDENCE";
        var verdict = sourceRefusesSelection ? NotSelectable : InsufficientData;

        return new ResearchEvaluationRow(
            EvaluationId(read, experiment.ExperimentId),
            Mode,
            experiment.ExperimentId,
            experiment.StrategyId,
            experiment.StrategyVersion,
            experiment.Symbol,
            experiment.Timeframe,
            experiment.SnapshotSha256,
            verdict,
            Low,
            claim,
            experiment.ApprovalStatus,
            experiment.EvidenceStatus,
            experiment.SelectedByPnl,
            experiment.ParametersChosenOnOutOfSample,
            experiment.LiveOrders,
            experiment.ExchangeOrdersSent,
            development,
            outOfSample,
            ResearchEvaluationMetrics.NotComputed,
            warnings,
            LimitationCodes);
    }

    static string? EvaluationId(ResearchPublicationRead read, string experimentId)
    {
        if (string.IsNullOrWhiteSpace(read.PublicationId))
            return null;
        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var obj in read.Objects)
        {
            if (string.IsNullOrWhiteSpace(obj.ContentSha256))
                return null;
            hashes[obj.Id] = obj.ContentSha256;
        }

        foreach (var id in new[] { "lab-result", "paper-status", "paper-report", "paper-health" })
        {
            if (!hashes.ContainsKey(id))
                return null;
        }

        var text = string.Join(
            '\n',
            read.PublicationId,
            hashes["lab-result"],
            hashes["paper-status"],
            hashes["paper-report"],
            hashes["paper-health"],
            experimentId,
            Mode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }

    static string? SharedSnapshot(IReadOnlyList<ResearchEvaluationRow> rows)
    {
        if (rows.Count == 0)
            return null;
        var first = rows[0].SnapshotSha256;
        if (string.IsNullOrWhiteSpace(first))
            return null;
        return rows.All(row => string.Equals(row.SnapshotSha256, first, StringComparison.Ordinal)) ? first : null;
    }

    static ResearchEvaluationSample? Sample(string name, JsonElement? source)
    {
        if (source is not { ValueKind: JsonValueKind.Object } element)
            return null;
        return new ResearchEvaluationSample(
            name,
            Int(element, "trade_count"),
            Int(element, "win_count"),
            Int(element, "loss_count"),
            Text(element, "win_rate"),
            Text(element, "net_pnl"),
            Text(element, "profit_factor"),
            Bool(element, "win_rate_is_performance_evidence"),
            Bool(element, "profit_factor_is_performance_evidence"),
            Text(element, "performance_claim"));
    }

    static void AddClaim(List<string> warnings, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        if (!warnings.Contains(value, StringComparer.Ordinal))
            warnings.Add(value);
    }

    static List<string> WarningList(ResearchPublicationRead read)
    {
        var warnings = new List<string> { InsufficientData };
        if (read.ReadError is not null)
            warnings.Add(read.ReadError.Code);
        return warnings;
    }

    static bool Readable(ResearchPublicationRead read) =>
        read.Availability is ResearchAvailability.Fresh or ResearchAvailability.Stale;

    static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node))
            return null;
        return node.ValueKind switch
        {
            JsonValueKind.String => node.GetString(),
            JsonValueKind.Number => node.GetRawText(),
            _ => null,
        };
    }

    static int? Int(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out var value))
            return null;
        return value;
    }

    static bool? Bool(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node))
            return null;
        return node.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}

public sealed record ResearchEvaluationMetrics(
    string SharpeRatio,
    string Cagr,
    string MaximumDrawdown,
    string Expectancy,
    string EquityCurve)
{
    public static readonly ResearchEvaluationMetrics NotComputed = new(
        ResearchEvaluationProjector.NotInSource,
        ResearchEvaluationProjector.NotInSource,
        ResearchEvaluationProjector.NotInSource,
        ResearchEvaluationProjector.NotInSource,
        ResearchEvaluationProjector.NotInSource);
}

public sealed record ResearchEvaluationSample(
    string Name,
    int? TradeCount,
    int? WinCount,
    int? LossCount,
    string? WinRate,
    string? NetPnl,
    string? ProfitFactor,
    bool? WinRateIsPerformanceEvidence,
    bool? ProfitFactorIsPerformanceEvidence,
    string? PerformanceClaim);

public sealed record ResearchEvaluationRow(
    string? EvaluationId,
    string Mode,
    string ExperimentId,
    string? StrategyId,
    string? StrategyVersion,
    string? Symbol,
    string? Timeframe,
    string? SnapshotSha256,
    string Verdict,
    string Confidence,
    string? PerformanceClaim,
    string? ApprovalStatus,
    string? EvidenceStatus,
    bool? SelectedByPnl,
    bool? ParametersChosenOnOutOfSample,
    bool? LiveOrders,
    int? ExchangeOrdersSent,
    ResearchEvaluationSample? Development,
    ResearchEvaluationSample? OutOfSample,
    ResearchEvaluationMetrics Metrics,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Limitations);

public sealed record ResearchEvaluationComparison(
    string Availability,
    string Mode,
    string? PublicationId,
    string? SnapshotSha256,
    bool SameSnapshot,
    string Verdict,
    string Confidence,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<ResearchEvaluationRow> Evaluations,
    ResearchReadError? ReadError);

public sealed record ResearchEvaluationSingle(
    string Availability,
    ResearchEvaluationRow? Evaluation,
    bool NotInPublication,
    ResearchReadError? ReadError);
