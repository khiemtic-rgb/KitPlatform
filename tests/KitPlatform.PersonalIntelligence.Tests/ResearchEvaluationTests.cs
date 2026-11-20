using System.Text.Json;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Research;
using KitPlatform.Packs.PersonalIntelligence.Research;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchEvaluationTests
{
    [Fact]
    public async Task Published_candidate_compares_three_experiments_on_one_snapshot()
    {
        var root = FindPackage();
        Assert.NotNull(root);
        var reader = new FileResearchPublicationReader(Options.Create(new ResearchPublicationOptions { Root = root }));
        var comparison = ResearchEvaluationProjector.Project(await reader.ReadAsync());

        Assert.Equal("FRESH", comparison.Availability);
        Assert.Equal("pub-e65b6912e02a168d", comparison.PublicationId);
        Assert.True(comparison.SameSnapshot);
        Assert.Equal("126a733bc6b709fcc1f511e4fb01aff61bd47694bb3328918f8f32dad6ccb2c3", comparison.SnapshotSha256);
        Assert.Equal("NOT_SELECTABLE", comparison.Verdict);
        Assert.Equal("LOW", comparison.Confidence);
        Assert.Equal(
            new[] { "EXP-TREND-SMA-10-30-V1", "EXP-BREAKOUT-DONCHIAN-20-V1", "EXP-REVERSION-RANGE-20-V1" },
            comparison.Evaluations.Select(row => row.ExperimentId).ToArray());
        Assert.DoesNotContain(comparison.Evaluations, row => row.ExperimentId.Contains("sma-cross-v1", StringComparison.OrdinalIgnoreCase));
        Assert.All(comparison.Evaluations, row =>
        {
            Assert.Equal(comparison.SnapshotSha256, row.SnapshotSha256);
            Assert.Equal("NOT_SELECTABLE", row.Verdict);
            Assert.Contains("NOT_A_PERFORMANCE_CLAIM", row.Warnings);
            Assert.Equal("NOT_IN_SOURCE", row.Metrics.SharpeRatio);
            Assert.Equal("NOT_IN_SOURCE", row.Metrics.Cagr);
            Assert.Equal("NOT_IN_SOURCE", row.Metrics.MaximumDrawdown);
            Assert.Equal("NOT_IN_SOURCE", row.Metrics.Expectancy);
            Assert.Equal("NOT_IN_SOURCE", row.Metrics.EquityCurve);
            Assert.NotNull(row.Development);
            Assert.NotNull(row.OutOfSample);
            Assert.False(row.Development!.ProfitFactorIsPerformanceEvidence);
            Assert.False(row.LiveOrders);
            Assert.Equal(0, row.ExchangeOrdersSent);
            Assert.False(string.IsNullOrWhiteSpace(row.EvaluationId));
        });
        Assert.Equal(3, comparison.Evaluations.Select(row => row.EvaluationId).Distinct().Count());
    }

    [Fact]
    public void Different_snapshots_are_not_one_comparison()
    {
        using var development = JsonDocument.Parse("""{"trade_count":1,"performance_claim":"NOT_A_PERFORMANCE_CLAIM","profit_factor_is_performance_evidence":false}""");
        using var other = JsonDocument.Parse("""{"trade_count":1,"performance_claim":"NOT_A_PERFORMANCE_CLAIM","profit_factor_is_performance_evidence":false}""");
        var read = new ResearchPublicationRead(
            ResearchAvailability.Fresh,
            ResearchPublicationSchema.V1,
            "pub-split",
            DateTimeOffset.Parse("2026-10-30T00:00:00Z"),
            ResearchPublicationSchema.Publisher,
            Hashes(),
            null,
            [
                Experiment("EXP-ONE", "aaa", development.RootElement.Clone()),
                Experiment("EXP-TWO", "bbb", other.RootElement.Clone()),
            ],
            null,
            null);
        var comparison = ResearchEvaluationProjector.Project(read);
        Assert.False(comparison.SameSnapshot);
        Assert.Null(comparison.SnapshotSha256);
        Assert.Equal("INSUFFICIENT_DATA", comparison.Verdict);
        Assert.Equal(2, comparison.Evaluations.Count);
        Assert.Contains("SNAPSHOT_MISMATCH", comparison.Warnings);
    }

    static string? FindPackage()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "deploy", "second-brain-agent", "research-publication-v1");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
                return candidate;
            dir = dir.Parent;
        }

        return null;
    }

    static ResearchExperimentRead Experiment(string id, string snapshot, JsonElement development) => new(
        id,
        id,
        "1",
        "TREND",
        "SOLUSDT",
        "1h",
        "fixture",
        snapshot,
        "EXPERIMENT_NOT_APPROVED",
        "INSUFFICIENT_EVIDENCE",
        "NOT_A_PERFORMANCE_CLAIM",
        null,
        false,
        false,
        development,
        development.Clone(),
        false,
        0);

    static IReadOnlyList<ResearchObjectRead> Hashes() =>
    [
        new("lab-result", "LAB_RESULT", ResearchPublicationSchema.Publisher, ResearchAvailability.PresentUntimed, new string('a', 64), null),
        new("paper-status", "PAPER_OPS_STATUS", ResearchPublicationSchema.Publisher, ResearchAvailability.Fresh, new string('b', 64), null),
        new("paper-report", "PAPER_RESEARCH_REPORT", ResearchPublicationSchema.Publisher, ResearchAvailability.Fresh, new string('c', 64), null),
        new("paper-health", "PAPER_OPS_HEALTH", ResearchPublicationSchema.Publisher, ResearchAvailability.Fresh, new string('d', 64), null),
    ];
}
