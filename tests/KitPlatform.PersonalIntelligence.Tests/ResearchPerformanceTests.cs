using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Xunit;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.PersonalIntelligence;
using KitPlatform.Packs.PersonalIntelligence.Trading;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class ResearchPerformanceTests
{
    [Fact]
    public void Closed_round_trip_metrics_use_net_pnl_and_do_not_treat_missing_as_zero()
    {
        var report = ResearchPerformance.FromClosedTrades(
        [
            Closed("a", 2m, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            Closed("b", -1m, new DateTimeOffset(2026, 1, 3, 0, 0, 0, TimeSpan.Zero)),
            Open(),
        ]);

        Assert.Equal(2, report.ClosedTrades);
        Assert.Equal(0.5m, report.WinRate);
        Assert.Equal(2m, report.ProfitFactor);
        Assert.Equal(0.5m, report.Expectancy);
        Assert.Equal(1m, report.NetPnl);
        Assert.Equal(1m, report.MaxDrawdown);
        Assert.True(report.WinRateKnown);
    }

    [Fact]
    public void No_closed_trade_keeps_metrics_unknown()
    {
        var report = ResearchPerformance.FromClosedTrades([Open()]);

        Assert.Equal(0, report.ClosedTrades);
        Assert.False(report.WinRateKnown);
        Assert.False(report.NetPnlKnown);
        Assert.False(report.MaxDrawdownKnown);
        Assert.Null(report.NetPnl);
        Assert.Null(report.WinRate);
    }

    [Fact]
    public void Wins_without_losses_do_not_invent_a_profit_factor()
    {
        var report = ResearchPerformance.FromClosedTrades(
        [
            Closed("a", 1m, new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
        ]);

        Assert.Equal(1m, report.WinRate);
        Assert.False(report.ProfitFactorKnown);
        Assert.Equal("undefined", report.ProfitFactorStatus);
        Assert.Equal(0m, report.MaxDrawdown);
        Assert.True(report.MaxDrawdownKnown);
    }

    [Fact]
    public void Exchange_routes_stay_hidden_unless_the_local_read_flag_is_set()
    {
        var hidden = Apply(false);
        var exposed = Apply(true);

        Assert.Contains(hidden, type => type == typeof(ResearchPublicationController));
        Assert.DoesNotContain(hidden, type => type == typeof(TradingExchangeController));
        Assert.DoesNotContain(hidden, type => type == typeof(TradingDeskController));
        Assert.Contains(exposed, type => type == typeof(TradingExchangeController));
        Assert.DoesNotContain(exposed, type => type == typeof(TradingDeskController));
        Assert.DoesNotContain(exposed, type => type == typeof(TradingLoopController));
    }

    static IReadOnlyList<Type> Apply(bool exposeExchangeRead)
    {
        var application = new ApplicationModel();
        foreach (var type in new[] { typeof(ResearchPublicationController), typeof(TradingExchangeController), typeof(TradingDeskController), typeof(TradingLoopController) })
            application.Controllers.Add(new ControllerModel(type.GetTypeInfo(), []));
        new ResearchOnlyControllerConvention(exposeExchangeRead).Apply(application);
        return application.Controllers.Select(controller => controller.ControllerType.AsType()).ToArray();
    }

    static LedgerTrade Closed(string id, decimal net, DateTimeOffset closedAt) => new(
        id, ResearchTradeLedger.Closed, ResearchTradeLedger.Long, "BTCUSDT", "1h", SmaCrossStrategy.Id,
        closedAt.AddHours(-1), closedAt, 100m, 101m, 0.001m, net, true, 0.01m, true, 0.01m, true, net, true,
        PaperSession.Realized, ResearchTradeLedger.CloseReasonSmaCrossBelow, [id]);

    static LedgerTrade Open() => new(
        "open", ResearchTradeLedger.Open, ResearchTradeLedger.Long, "BTCUSDT", "1h", SmaCrossStrategy.Id,
        DateTimeOffset.UnixEpoch, null, 100m, null, 0.001m, null, false, 0.01m, true, 0.01m, true, null, false,
        PaperSession.NoClosedPosition, null, ["open"]);
}
