using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

/// <summary>
/// FAMIXA-FILM-PRODUCTION-29. Decision only. No provider call and no permit consume.
/// </summary>
public sealed class P29PaidNetworkGate
{
    static readonly Guid ProductionBuild = Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb");
    const string Shot = "EP01-SC01-SH01";

    [Fact]
    public async Task Famixa_production_sh01_passes_only_with_issued_permit()
    {
        var sandbox = new ProductionPaidNetworkOneShotPermit();
        var scope = new ProductionScope(ProductionBuild, "EP01", "SC01", Shot);
        var official = ProductionScopeAnchorRules.IsOfficialFilm(ProductionBuild);
        var tenant = ProductionPaidNetworkGate.IsTenantEp01PaidVideo(ProductionBuild, "EP01");
        var sandboxMatch = ProductionPaidNetworkOneShotPermit.Matches(scope, Shot, "runway", "gen4_turbo");
        var before = ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(ProductionBuild, "EP01", true, false);
        var noAuthorize = ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(ProductionBuild, "EP01", false, true);
        var sandboxStillClosed = ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(
            ProductionTestSandboxIds.ScopeId, "TEST", true, false);
        var officialStillOpen = ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(
            ProductionScopeAnchorRules.OfficialEp01, "EP01", true, false);
        var officialStillClosed = ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(
            ProductionScopeAnchorRules.OfficialEp01, "EP01", false, true);

        var store = new InMemoryProductionExecutionPermitStore();
        var issued = await store.IssueAsync(new IssueProductionExecutionPermitCommand(
            ProductionBuild, Shot, "EP01", "SC01", ProductionActions.CreateVideo,
            "runway", "gen4_turbo", "p29", "FAMIXA-FILM-PRODUCTION-29"));
        var matches = ProductionExecutionPermitCanon.Matches(
            issued, ProductionBuild, Shot, ProductionActions.CreateVideo, "runway", "gen4_turbo");
        var after = ProductionPaidNetworkGate.AllowsUnpreparedPaidStart(ProductionBuild, "EP01", true, matches);
        var consumedBeforeStart = ProductionExecutionPermitCanon.MatchesConsumed(
            issued, ProductionBuild, Shot, ProductionActions.CreateVideo, "runway", "gen4_turbo");
        var transportBefore = ProductionPaidNetworkGate.TransportUsesExecutionPermit(ProductionBuild, "EP01")
            && consumedBeforeStart;

        var consumed = await store.TryConsumeAsync(
            ProductionBuild, Shot, ProductionActions.CreateVideo, "runway", "gen4_turbo");
        var latest = await store.GetLatestAsync(
            ProductionBuild, Shot, ProductionActions.CreateVideo, "runway", "gen4_turbo");
        var transportAfter = latest is not null
            && ProductionPaidNetworkGate.TransportUsesExecutionPermit(ProductionBuild, "EP01")
            && ProductionExecutionPermitCanon.MatchesConsumed(
                latest, ProductionBuild, Shot, ProductionActions.CreateVideo, "runway", "gen4_turbo");
        var stillIssued = await store.GetIssuedAsync(
            ProductionBuild, Shot, ProductionActions.CreateVideo, "runway", "gen4_turbo");

        Assert.False(official);
        Assert.True(tenant);
        Assert.False(sandboxMatch);
        Assert.False(sandbox.IsEligible(scope, Shot, "runway", "gen4_turbo"));
        Assert.False(before);
        Assert.False(noAuthorize);
        Assert.False(sandboxStillClosed);
        Assert.True(officialStillOpen);
        Assert.False(officialStillClosed);
        Assert.True(matches);
        Assert.True(after);
        Assert.False(consumedBeforeStart);
        Assert.False(transportBefore);
        Assert.True(consumed);
        Assert.True(transportAfter);
        Assert.Null(stillIssued);
        Assert.Equal(0, sandbox.StartCount);
    }
}
