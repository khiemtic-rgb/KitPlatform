using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.Extensions.Options;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class MultiExchangeConnectorTests
{
    const string Secret = "fixture-api-secret-0001";
    const string Key = "fixture-api-key-0001";
    const string Passphrase = "fixture-passphrase-0001";

    [Fact]
    public void Permission_gates_fail_closed_for_unsafe_missing_and_malformed_metadata()
    {
        using var spotOnly = JsonDocument.Parse("""
            {"enableReading":true,"enableWithdrawals":false,"enableInternalTransfer":false,"permitsUniversalTransfer":false,"enableSpotAndMarginTrading":false,"enableFutures":false,"enableMargin":false,"enableVanillaOptions":false,"ipRestrict":true}
            """);
        BinancePermissionGate.EnsureNotTransfer(spotOnly.RootElement);
        Assert.True(BinancePermissionGate.ReadOnly(spotOnly.RootElement));
        Assert.Null(BinancePermissionGate.SpotHistoryBlock(["reading", "spot-read", "futures-blocked"]));
        Assert.Contains("Không bật Enable Futures", BinancePermissionGate.FuturesHistoryBlock(["reading", "spot-read", "futures-blocked"]), StringComparison.Ordinal);

        using var futuresTrade = JsonDocument.Parse("""{"enableReading":true,"enableFutures":true}""");
        Assert.False(BinancePermissionGate.ReadOnly(futuresTrade.RootElement));
        Assert.Contains("không yêu cầu bật", BinancePermissionGate.FuturesHistoryBlock(["reading", "futures", "futures-unsafe", "futures-blocked"]), StringComparison.Ordinal);
        Assert.Contains("Futures không được đồng bộ", BinancePermissionGate.Describe(futuresTrade.RootElement, false), StringComparison.Ordinal);

        using var spotTrade = JsonDocument.Parse("""{"enableReading":true,"enableSpotAndMarginTrading":true}""");
        Assert.False(BinancePermissionGate.ReadOnly(spotTrade.RootElement));
        Assert.Contains("Spot", BinancePermissionGate.SpotHistoryBlock(["reading", "spotTrading", "spot-trade"]), StringComparison.Ordinal);

        using var withdraw = JsonDocument.Parse("""{"enableReading":true,"enableWithdrawals":true}""");
        Assert.Equal("withdrawal", Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureNotTransfer(withdraw.RootElement)).Code);
        using var transfer = JsonDocument.Parse("""{"enableReading":true,"permitsUniversalTransfer":true}""");
        Assert.Equal("transfer", Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureNotTransfer(transfer.RootElement)).Code);
        using var noRead = JsonDocument.Parse("""{"enableWithdrawals":false}""");
        Assert.Equal("permission", Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureNotTransfer(noRead.RootElement)).Code);
        Assert.Contains("chưa xác minh", BinancePermissionGate.FuturesHistoryBlock(["restrictions-unverified"]), StringComparison.Ordinal);
        Assert.Contains("chưa xác minh", BinancePermissionGate.SpotHistoryBlock(["restrictions-unverified"]), StringComparison.Ordinal);

        using var okxRead = JsonDocument.Parse("""{"perm":" read_only "}""");
        var read = OkxPermissionGate.Evaluate(okxRead.RootElement);
        Assert.True(read.ReadOnlyVerified);
        Assert.Contains("read_only", read.Names);
        Assert.Null(OkxPermissionGate.HistoryBlock(read.Names, "swap"));

        using var okxTrade = JsonDocument.Parse("""{"perm":"read_only,trade"}""");
        Assert.Equal("not_read_only", Assert.Throws<ExchangeConnectionException>(() => OkxPermissionGate.Evaluate(okxTrade.RootElement)).Code);
        using var okxWithdraw = JsonDocument.Parse("""{"perm":"withdraw"}""");
        Assert.Equal("withdrawal", Assert.Throws<ExchangeConnectionException>(() => OkxPermissionGate.Evaluate(okxWithdraw.RootElement)).Code);
        using var okxMissing = JsonDocument.Parse("""{"uid":"1"}""");
        var missing = OkxPermissionGate.Evaluate(okxMissing.RootElement);
        Assert.False(missing.ReadOnlyVerified);
        Assert.Contains("swap", OkxPermissionGate.HistoryBlock(missing.Names, "swap"), StringComparison.Ordinal);
        using var okxUnknown = JsonDocument.Parse("""{"perm":"read"}""");
        Assert.False(OkxPermissionGate.Evaluate(okxUnknown.RootElement).ReadOnlyVerified);
        using var okxEmpty = JsonDocument.Parse("""{"perm":" , "}""");
        Assert.False(OkxPermissionGate.Evaluate(okxEmpty.RootElement).ReadOnlyVerified);
        using var okxArray = JsonDocument.Parse("""{"perm":["read_only"]}""");
        Assert.False(OkxPermissionGate.Evaluate(okxArray.RootElement).ReadOnlyVerified);
        Assert.DoesNotContain(Secret, missing.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Binance_signature_matches_the_published_vector_and_okx_signs_timestamp_method_and_path()
    {
        var query = "symbol=LTCBTC&side=BUY&type=LIMIT&timeInForce=GTC&quantity=1&price=0.1&recvWindow=5000&timestamp=1499827319559";
        var sign = BinanceSigner.Sign("NhqPtmdSJYdKjVHjA7PZj4Mge3R5YNiP1e3UZjInClVN65XAbvqqM6A7H5fATj0j", query);
        Assert.Equal("c8db56825ae71d6d79447849e617115f4a920fa2acdcab2b053c4b2838bd6b71", sign);
        Assert.DoesNotContain(Secret, sign, StringComparison.Ordinal);

        var okx = OkxSigner.Sign(Secret, "2020-12-08T09:08:57.715Z", "GET", "/api/v5/account/balance");
        Assert.Equal("NdVrk9TBwnhatkckQ1LeAoh8CaUl6xgzuDsvXoic9VI=", okx);
        Assert.DoesNotContain(Secret, okx, StringComparison.Ordinal);
        Assert.DoesNotContain(Passphrase, okx, StringComparison.Ordinal);
    }

    [Fact]
    public void Mappers_keep_spot_and_futures_subtypes_and_do_not_invent_pnl()
    {
        using var spot = JsonDocument.Parse("""
            {"symbol":"BTCUSDT","id":11,"orderId":22,"price":"10","qty":"2","quoteQty":"20","commission":"0.1","commissionAsset":"USDT","time":1700000000000,"isBuyer":true,"isMaker":false}
            """);
        var spotRow = BinanceExecutionMapper.MapSpot(spot.RootElement, "MAINNET");
        Assert.True(spotRow.Mappable);
        Assert.Equal("SPOT", spotRow.MarketType);
        Assert.Equal("spot", spotRow.ProductSubtype);
        Assert.Equal("BUY", spotRow.Side);
        Assert.Equal("BINANCE:MAINNET:spot:BTCUSDT:11", spotRow.ExternalTradeId);
        Assert.Null(spotRow.RealizedPnl);

        using var futures = JsonDocument.Parse("""
            {"symbol":"BTCUSDT","id":12,"orderId":23,"price":"10","qty":"2","quoteQty":"20","commission":"0.2","commissionAsset":"USDT","time":1700000001000,"side":"SELL","positionSide":"SHORT","realizedPnl":"-1.5","maker":true}
            """);
        var usd = BinanceExecutionMapper.MapFutures(futures.RootElement, "MAINNET", "usd_m", "GET /fapi/v1/userTrades");
        var coin = BinanceExecutionMapper.MapFutures(futures.RootElement, "MAINNET", "coin_m", "GET /dapi/v1/userTrades");
        Assert.Equal("FUTURES", usd.MarketType);
        Assert.Equal("usd_m", usd.ProductSubtype);
        Assert.Equal("coin_m", coin.ProductSubtype);
        Assert.NotEqual(usd.ExternalTradeId, coin.ExternalTradeId);
        Assert.Equal("-1.5", usd.RealizedPnl);
        Assert.Equal("SHORT", usd.PositionSide);
        Assert.Equal("SELL", usd.Side);

        using var swap = JsonDocument.Parse("""
            {"instType":"SWAP","instId":"BTC-USDT-SWAP","tradeId":"t1","ordId":"o1","side":"buy","fillPx":"10","fillSz":"3","fee":"-0.01","feeCcy":"USDT","ts":"1700000002000","fillPnl":"0.4","posSide":"long"}
            """);
        using var expiry = JsonDocument.Parse("""
            {"instType":"FUTURES","instId":"BTC-USD-240329","tradeId":"t1","ordId":"o1","side":"sell","fillPx":"10","fillSz":"3","fee":"-0.02","feeCcy":"USD","ts":"1700000003000"}
            """);
        var swapRow = OkxExecutionMapper.MapItem(swap.RootElement, "MAINNET", "swap");
        var expiryRow = OkxExecutionMapper.MapItem(expiry.RootElement, "MAINNET", "expiry");
        Assert.Equal("FUTURES", swapRow.MarketType);
        Assert.Equal("swap", swapRow.ProductSubtype);
        Assert.Equal("expiry", expiryRow.ProductSubtype);
        Assert.Equal(-0.01m, swapRow.FeeAmount);
        Assert.Equal("0.4", swapRow.RealizedPnl);
        Assert.Equal("BUY", swapRow.Side);
        Assert.NotEqual(swapRow.ExternalTradeId, expiryRow.ExternalTradeId);

        using var margin = JsonDocument.Parse("""
            {"instType":"MARGIN","instId":"BTC-USDT","tradeId":"m1","ordId":"o1","side":"buy","fillPx":"10","fillSz":"1","fee":"-0.01","feeCcy":"USDT","ts":"1700000004000"}
            """);
        var marginRow = OkxExecutionMapper.MapItem(margin.RootElement, "MAINNET", "spot");
        Assert.False(marginRow.Mappable);
        Assert.Contains("MARGIN", marginRow.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Backfill_walks_more_than_ten_pages_and_more_than_thirty_one_days_then_reports_partial()
    {
        var stream = new ExchangeStreamDefinition
        {
            ExchangeCode = "BYBIT",
            MarketType = "FUTURES",
            ProductSubtype = "linear",
            WindowMs = 7 * ExchangeMarket.DayMs,
            RetentionMs = 40 * ExchangeMarket.DayMs,
            RetentionLimited = true,
            RetentionNote = "fixture retention",
            Pagination = "cursor",
            Discover = "none",
            PageSize = 100,
        };
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var checkpoint = ExchangeBackfillMachine.Start(stream, now, null, "FULL");
        Assert.True(checkpoint.Windows.Count >= 6);
        Assert.True(checkpoint.Windows[0].End - checkpoint.Windows[0].Start <= stream.WindowMs);

        for (var page = 0; page < 12; page++)
        {
            var step = ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage { NextCursor = "c" + page, PageFull = true });
            Assert.False(step.Terminal);
            checkpoint = step.Checkpoint;
        }

        Assert.Equal("c11", checkpoint.Cursor);
        Assert.Equal(0, checkpoint.WindowIndex);

        var endWindow = ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage());
        Assert.False(endWindow.Terminal);
        Assert.Equal(1, endWindow.Checkpoint.WindowIndex);
        Assert.Null(endWindow.Checkpoint.Cursor);

        checkpoint = endWindow.Checkpoint;
        while (!ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage()).Terminal)
            checkpoint = ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage()).Checkpoint;
        var done = ExchangeBackfillMachine.Advance(checkpoint, new ExchangeHistoryPage());
        Assert.True(done.Terminal);
        Assert.Equal("PARTIAL", done.TerminalStatus);
        Assert.Contains("fixture retention", done.CutoffReason, StringComparison.Ordinal);
        Assert.Equal(checkpoint.VenueFloorMs, done.CutoffMs);
    }

    [Fact]
    public void One_stream_checkpoint_does_not_move_another_stream()
    {
        var spot = ExchangeBackfillMachine.Start(ExchangeCatalog.Require("OKX", "spot"), DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, "FULL");
        var swap = ExchangeBackfillMachine.Start(ExchangeCatalog.Require("OKX", "swap"), DateTimeOffset.Parse("2026-01-01T00:00:00Z"), null, "FULL");
        var moved = ExchangeBackfillMachine.Advance(spot, new ExchangeHistoryPage { NextCursor = "trade-1", PageFull = true });
        Assert.Equal("trade-1", moved.Checkpoint.Cursor);
        Assert.Null(swap.Cursor);
        Assert.Equal(spot.WindowIndex, swap.WindowIndex);
    }

    [Fact]
    public async Task Connectors_sign_requests_and_do_not_log_secrets()
    {
        var log = new ListLog();
        var binance = new RecordingHandler();
        var connector = new BinanceExchangeConnector(binance, log);
        var probe = await connector.ProbeAsync(new ExchangeCredential
        {
            Environment = "MAINNET",
            ApiKey = Key,
            ApiSecret = Secret,
        }, CancellationToken.None);
        Assert.Equal("api.binance.com", binance.Host);
        Assert.Equal(new[] { "/sapi/v1/account/apiRestrictions", "/api/v3/account" }, binance.Paths);
        Assert.Contains("signature=", binance.Uri, StringComparison.Ordinal);
        Assert.Contains("recvWindow=5000", binance.Uri, StringComparison.Ordinal);
        Assert.Contains("timestamp=", binance.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, binance.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, binance.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
        Assert.DoesNotContain("signature=", string.Join('\n', log.Lines), StringComparison.Ordinal);
        Assert.True(probe.ReadOnlyVerified);
        Assert.True(probe.Accepted);
        Assert.Equal("BTC", probe.Assets[0].Coin);

        var beforeHistory = binance.Calls;
        var blocked = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            connector.ReadHistoryAsync(new ExchangeCredential { Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret },
                new ExchangeHistoryQuery { ProductSubtype = "spot", Phase = "fills", Symbol = "BTCUSDT", WindowStartMs = 1, WindowEndMs = 2, Limit = 10 }, CancellationToken.None));
        Assert.Equal("not_allowlisted", blocked.Code);
        Assert.Equal(beforeHistory, binance.Calls);

        var rejected = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            connector.ProbeAsync(new ExchangeCredential { Environment = "https://evil.example", ApiKey = Key, ApiSecret = Secret }, CancellationToken.None));
        Assert.Equal("unsupported", rejected.Code);
        Assert.Equal(beforeHistory, binance.Calls);

        var limited = new RecordingHandler { Mode = "rate" };
        var rate = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new BinanceExchangeConnector(limited, log).ProbeAsync(new ExchangeCredential { Environment = "TESTNET", ApiKey = Key, ApiSecret = Secret }, CancellationToken.None));
        Assert.Equal("rate_limit", rate.Code);
        Assert.Equal("testnet.binance.vision", limited.Host);
        Assert.Equal(1, limited.Calls);
        Assert.DoesNotContain(Secret, rate.Message, StringComparison.Ordinal);

        var okxLog = new ListLog();
        var okxHandler = new RecordingHandler { Mode = "okx" };
        var okx = new OkxExchangeConnector(okxHandler, okxLog);
        var history = await okx.ReadHistoryAsync(new ExchangeCredential
        {
            Environment = "TESTNET",
            ApiKey = Key,
            ApiSecret = Secret,
            Passphrase = Passphrase,
        }, new ExchangeHistoryQuery { ProductSubtype = "swap", WindowStartMs = 10, WindowEndMs = 20, Limit = 100 }, CancellationToken.None);
        Assert.Equal("www.okx.com", okxHandler.Host);
        Assert.Equal("1", okxHandler.Simulated);
        Assert.Equal(Passphrase, okxHandler.Passphrase);
        Assert.DoesNotContain(Passphrase, string.Join('\n', okxLog.Lines), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, string.Join('\n', okxLog.Lines), StringComparison.Ordinal);
        Assert.Equal("FUTURES", history.Items[0].MarketType);
        Assert.Equal("swap", history.Items[0].ProductSubtype);

        var badPass = new RecordingHandler { Mode = "okx-pass" };
        var passError = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new OkxExchangeConnector(badPass, okxLog).ProbeAsync(new ExchangeCredential { Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret, Passphrase = Passphrase }, CancellationToken.None));
        Assert.Equal("passphrase", passError.Code);
        Assert.DoesNotContain(Passphrase, passError.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Binance_allowlist_rejects_unsafe_permissions_and_transport_errors_before_extra_http()
    {
        var log = new ListLog();
        var credential = new ExchangeCredential { Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret };

        var readOnly = new ScriptHandler(ScriptHandler.ReadOnlyBody);
        var probe = await new BinanceExchangeConnector(readOnly, log).ProbeAsync(credential, CancellationToken.None);
        Assert.Equal(2, readOnly.Calls);
        Assert.Equal(new[] { "/sapi/v1/account/apiRestrictions", "/api/v3/account" }, readOnly.Paths);
        Assert.True(probe.Accepted);
        Assert.True(probe.ReadOnlyVerified);
        Assert.Equal("0.10000000", probe.Assets[0].WalletBalance);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(probe), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, JsonSerializer.Serialize(probe), StringComparison.Ordinal);

        await AssertRejected("trade", "not_read_only", 1);
        await AssertRejected("withdraw", "withdrawal", 1);
        await AssertRejected("missing", "unverified", 1);
        await AssertRejected("malformed", "exchange", 1);
        await AssertRejected("signature", "signature", 1);
        await AssertRejected("invalid-key", "invalid_key", 1);
        await AssertRejected("ip", "permission", 1);
        await AssertRejected("timestamp", "timestamp", 1);
        await AssertRejected("rate", "rate_limit", 1);
        await AssertRejected("busy", "rate_limit", 1);
        await AssertRejected("limit-code", "rate_limit", 1);
        await AssertRejected("timeout", "network", 1);
        await AssertRejected("network", "network", 1);

        var order = Assert.Throws<ExchangeConnectionException>(() => BinanceExchangeConnector.EnsureAllowed("POST", "/api/v3/order"));
        Assert.Equal("not_allowlisted", order.Code);
        var withdrawPath = Assert.Throws<ExchangeConnectionException>(() => BinanceExchangeConnector.EnsureAllowed("GET", "/sapi/v1/capital/withdraw/apply"));
        Assert.Equal("not_allowlisted", withdrawPath.Code);
        BinanceExchangeConnector.EnsureAllowed("GET", "/api/v3/account");

        using var partial = JsonDocument.Parse("""{"enableReading":true}""");
        Assert.False(BinancePermissionGate.ReadOnly(partial.RootElement));
        Assert.Equal("unverified", Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureReadOnly(partial.RootElement)).Code);

        async Task AssertRejected(string mode, string code, int calls)
        {
            var handler = new ScriptHandler(mode);
            var error = await Assert.ThrowsAsync<ExchangeConnectionException>(() => new BinanceExchangeConnector(handler, log).ProbeAsync(credential, CancellationToken.None));
            Assert.Equal(code, error.Code);
            Assert.Equal(calls, handler.Calls);
            Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Key, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("LEAKED-MSG", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", error.Message, StringComparison.Ordinal);
            if (mode == "ip")
                Assert.Contains("IP", error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", string.Join('\n', log.Lines), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("enableReading")]
    [InlineData("enableWithdrawals")]
    [InlineData("enableInternalTransfer")]
    [InlineData("permitsUniversalTransfer")]
    [InlineData("enableSpotAndMarginTrading")]
    [InlineData("enableFutures")]
    [InlineData("enableMargin")]
    [InlineData("enableVanillaOptions")]
    public void Missing_each_required_binance_permission_field_is_rejected(string field)
    {
        using var document = JsonDocument.Parse(WithoutPermission(field));
        var error = Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureReadOnly(document.RootElement));
        Assert.Equal("unverified", error.Code);
        Assert.False(BinancePermissionGate.ReadOnly(document.RootElement));
    }

    [Fact]
    public async Task Unknown_permission_fields_fail_closed_unless_explicitly_false()
    {
        var log = new ListLog();
        var credential = new ExchangeCredential { Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret };

        var enabled = new ScriptHandler("unknown-true");
        var rejected = await Assert.ThrowsAsync<ExchangeConnectionException>(() => new BinanceExchangeConnector(enabled, log).ProbeAsync(credential, CancellationToken.None));
        Assert.Equal("unverified", rejected.Code);
        Assert.Equal(1, enabled.Calls);
        Assert.Equal("/sapi/v1/account/apiRestrictions", enabled.Paths[0]);
        Assert.DoesNotContain(Secret, rejected.Message, StringComparison.Ordinal);

        var wrongType = new ScriptHandler("unknown-type");
        var typed = await Assert.ThrowsAsync<ExchangeConnectionException>(() => new BinanceExchangeConnector(wrongType, log).ProbeAsync(credential, CancellationToken.None));
        Assert.Equal("unverified", typed.Code);
        Assert.Equal(1, wrongType.Calls);

        var malformed = new ScriptHandler("malformed");
        var broken = await Assert.ThrowsAsync<ExchangeConnectionException>(() => new BinanceExchangeConnector(malformed, log).ProbeAsync(credential, CancellationToken.None));
        Assert.Equal("exchange", broken.Code);
        Assert.Equal(1, malformed.Calls);

        using var unsafeFutures = JsonDocument.Parse(ScriptHandler.ReadOnlyBody.Replace("\"enableFutures\":false", "\"enableFutures\":true", StringComparison.Ordinal));
        Assert.Equal("not_read_only", Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureReadOnly(unsafeFutures.RootElement)).Code);
        using var hiddenUnsafe = JsonDocument.Parse(ScriptHandler.ReadOnlyBody.Replace("\"enableFutures\":false", "\"enableFutures\":true", StringComparison.Ordinal).TrimEnd('}') + ",\"enablePortfolioMarginTrading\":false}");
        Assert.Equal("not_read_only", Assert.Throws<ExchangeConnectionException>(() => BinancePermissionGate.EnsureReadOnly(hiddenUnsafe.RootElement)).Code);

        var disabled = new ScriptHandler("unknown-false");
        var probe = await new BinanceExchangeConnector(disabled, log).ProbeAsync(credential, CancellationToken.None);
        Assert.True(probe.ReadOnlyVerified);
        Assert.Equal(2, disabled.Calls);
        Assert.Equal(new[] { "/sapi/v1/account/apiRestrictions", "/api/v3/account" }, disabled.Paths);
        Assert.DoesNotContain(Secret, JsonSerializer.Serialize(probe), StringComparison.Ordinal);
        Assert.DoesNotContain("enablePortfolioMarginTrading", string.Join('\n', log.Lines), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redirect_is_rejected_before_a_second_request()
    {
        var log = new ListLog();
        var credential = new ExchangeCredential { Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret };
        var sockets = new SocketsHttpHandler();
        _ = new BinanceExchangeConnector(sockets, log);
        Assert.False(sockets.AllowAutoRedirect);
        sockets.Dispose();

        await AssertRedirect("https://evil.example/steal", HttpStatusCode.Found);
        await AssertRedirect("https://api.binance.com/api/v3/order", HttpStatusCode.MovedPermanently);
        await AssertRedirect("http://api.binance.com/api/v3/account", HttpStatusCode.TemporaryRedirect);

        async Task AssertRedirect(string location, HttpStatusCode status)
        {
            var handler = new RedirectHandler(location, status);
            var error = await Assert.ThrowsAsync<ExchangeConnectionException>(() => new BinanceExchangeConnector(handler, log).ProbeAsync(credential, CancellationToken.None));
            Assert.Equal("redirect", error.Code);
            Assert.Equal(502, error.StatusCode);
            Assert.Equal(1, handler.Calls);
            Assert.Single(handler.Uris);
            Assert.Contains("/sapi/v1/account/apiRestrictions", handler.Uris[0], StringComparison.Ordinal);
            Assert.DoesNotContain(location, handler.Uris[0], StringComparison.Ordinal);
            Assert.DoesNotContain(location, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Key, error.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", error.Message, StringComparison.Ordinal);
            var lines = string.Join('\n', log.Lines);
            Assert.Contains("redirect", lines, StringComparison.Ordinal);
            Assert.DoesNotContain(location, lines, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, lines, StringComparison.Ordinal);
        }
    }

    static string WithoutPermission(string field)
    {
        var text = ScriptHandler.ReadOnlyBody.Replace($"\"{field}\":true,", "", StringComparison.Ordinal);
        return text.Replace($"\"{field}\":false,", "", StringComparison.Ordinal);
    }

    sealed class ScriptHandler : HttpMessageHandler
    {
        public const string ReadOnlyBody = """{"enableReading":true,"enableWithdrawals":false,"enableInternalTransfer":false,"permitsUniversalTransfer":false,"enableSpotAndMarginTrading":false,"enableFutures":false,"enableMargin":false,"enableVanillaOptions":false,"ipRestrict":true}""";
        readonly string _mode;
        public int Calls { get; private set; }
        public List<string> Paths { get; } = new();

        public ScriptHandler(string mode) => _mode = mode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Paths.Add(request.RequestUri?.AbsolutePath ?? "");
            if (_mode == "timeout")
                throw new TaskCanceledException("timeout");
            if (_mode == "network")
                throw new HttpRequestException("down");
            if (_mode == "rate")
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429));
            if (_mode == "busy")
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)418));
            var path = request.RequestUri?.AbsolutePath;
            if (_mode == "malformed" && path == "/sapi/v1/account/apiRestrictions")
                return Task.FromResult(Text("not-json"));
            if (path == "/sapi/v1/account/apiRestrictions")
                return Task.FromResult(Text(Restrictions(_mode)));
            if (path == "/api/v3/account")
                return Task.FromResult(Text("""{"accountType":"SPOT","balances":[{"asset":"BTC","free":"0.10000000","locked":"0.00000000"}]}"""));
            return Task.FromResult(Text("""{"code":-1121,"msg":"LEAKED-MSG"}"""));
        }

        static string Restrictions(string mode) => mode switch
        {
            "trade" => ReadOnlyBody.Replace("\"enableSpotAndMarginTrading\":false", "\"enableSpotAndMarginTrading\":true", StringComparison.Ordinal),
            "withdraw" => ReadOnlyBody.Replace("\"enableWithdrawals\":false", "\"enableWithdrawals\":true", StringComparison.Ordinal),
            "missing" => """{"enableReading":true}""",
            "signature" => """{"code":-1022,"msg":"LEAKED-MSG"}""",
            "invalid-key" => """{"code":-2014,"msg":"LEAKED-MSG"}""",
            "ip" => """{"code":-2015,"msg":"LEAKED-MSG"}""",
            "timestamp" => """{"code":-1021,"msg":"LEAKED-MSG"}""",
            "limit-code" => """{"code":-1003,"msg":"LEAKED-MSG"}""",
            "unknown-true" => ReadOnlyBody.TrimEnd('}') + ",\"enablePortfolioMarginTrading\":true}",
            "unknown-false" => ReadOnlyBody.TrimEnd('}') + ",\"enablePortfolioMarginTrading\":false}",
            "unknown-type" => ReadOnlyBody.TrimEnd('}') + ",\"enablePortfolioMarginTrading\":\"true\"}",
            _ => ReadOnlyBody,
        };

        static HttpResponseMessage Text(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    sealed class RedirectHandler : HttpMessageHandler
    {
        readonly string _location;
        readonly HttpStatusCode _status;
        public int Calls { get; private set; }
        public List<string> Uris { get; } = new();

        public RedirectHandler(string location, HttpStatusCode status)
        {
            _location = location;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Uris.Add(request.RequestUri?.AbsoluteUri ?? "");
            var response = new HttpResponseMessage(_status);
            response.Headers.Location = new Uri(_location);
            response.Content = new StringContent("{\"msg\":\"LEAKED-MSG\"}", Encoding.UTF8, "application/json");
            return Task.FromResult(response);
        }
    }

    sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Host { get; private set; }
        public string Uri { get; private set; } = "";
        public List<string> Paths { get; } = new();
        public int Calls { get; private set; }
        public string? Passphrase { get; private set; }
        public string? Simulated { get; private set; }
        public string Mode { get; init; } = "binance";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Host = request.RequestUri?.Host;
            Uri = request.RequestUri?.ToString() ?? "";
            Paths.Add(request.RequestUri?.AbsolutePath ?? "");
            if (request.Headers.TryGetValues("OK-ACCESS-PASSPHRASE", out var pass))
                Passphrase = pass.First();
            if (request.Headers.TryGetValues("x-simulated-trading", out var sim))
                Simulated = sim.First();
            if (Mode == "rate")
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429));
            if (Mode == "okx-pass")
                return Task.FromResult(Json("""{"code":"50105","msg":"bad","data":[]}"""));
            if (Mode == "okx")
            {
                var timestamp = request.Headers.GetValues("OK-ACCESS-TIMESTAMP").First();
                var path = request.RequestUri!.PathAndQuery;
                var expected = OkxSigner.Sign(Secret, timestamp, "GET", path);
                var actual = request.Headers.GetValues("OK-ACCESS-SIGN").First();
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    return Task.FromResult(Json("""{"code":"50113","msg":"sign","data":[]}"""));
                return Task.FromResult(Json("""
                    {"code":"0","data":[{"instType":"SWAP","instId":"BTC-USDT-SWAP","tradeId":"t9","ordId":"o9","side":"buy","fillPx":"10","fillSz":"1","fee":"-0.01","feeCcy":"USDT","ts":"1700000000000"}]}
                    """));
            }

            var query = (request.RequestUri?.Query ?? "").TrimStart('?');
            var without = query.Contains("&signature=", StringComparison.Ordinal)
                ? query[..query.IndexOf("&signature=", StringComparison.Ordinal)]
                : query;
            var signature = BinanceSigner.Sign(Secret, without);
            if (!query.Contains("signature=" + signature, StringComparison.Ordinal))
                return Task.FromResult(Json("""{"code":-1022,"msg":"LEAKED-MSG"}"""));
            var absolutePath = request.RequestUri?.AbsolutePath;
            if (absolutePath == "/sapi/v1/account/apiRestrictions")
                return Task.FromResult(Json(ScriptHandler.ReadOnlyBody));
            if (absolutePath == "/api/v3/account")
                return Task.FromResult(Json("""{"accountType":"SPOT","balances":[{"asset":"BTC","free":"0.10000000","locked":"0.00000000"}]}"""));
            return Task.FromResult(Json("""{"code":-1121,"msg":"LEAKED-MSG"}"""));
        }

        static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}

[Collection("PiExchange")]
public sealed class MultiExchangeBackfillTests : IClassFixture<PiValidationDatabase>
{
    const string Secret = "fixture-api-secret-0001";
    const string Key = "fixture-api-key-0001";
    static readonly string KeyMaterial = Convert.ToBase64String(Enumerable.Repeat((byte)0x11, 32).ToArray());
    readonly PiValidationDatabase _db;

    public MultiExchangeBackfillTests(PiValidationDatabase db) => _db = db;

    [Fact]
    public async Task Backfill_is_durable_idempotent_and_isolated_by_stream()
    {
        await CleanupAsync();
        try
        {
            var handler = new StubBybitHandler();
            var keys = new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial);
            await using var host = await ExchangeHost.Start(_db, handler, new ListLog(), keys);
            var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
            var saved = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
            {
                ExchangeCode = "BYBIT",
                Environment = "TESTNET",
                ApiKey = Key,
                ApiSecret = Secret,
            });
            var savedText = await saved.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.DoesNotContain(Secret, savedText, StringComparison.Ordinal);
            using var savedJson = JsonDocument.Parse(savedText);
            var connectionId = savedJson.RootElement.GetProperty("connection").GetProperty("connectionId").GetGuid();

            var registry = new ExchangeAdapterRegistry([new ScriptAdapter()]);
            var service = new ExchangeBackfillService(
                _db.Factory,
                new ExchangeTestTenant(_db.TenantA, _db.UserA),
                registry,
                keys,
                Options.Create(new ExchangeBackfillOptions { WorkerEnabled = false, DelayMilliseconds = 0, StepsPerPump = 20 }));
            for (var attempt = 0; attempt < 8; attempt++)
                await service.PumpAsync(CancellationToken.None);

            await using var conn = await _db.OpenAsync();
            var jobs = (await conn.QueryAsync<(string Subtype, string Status, int Inserted, int Duplicate, int Pages)>(
                """
                SELECT product_subtype, status, inserted_count, duplicate_count, pages
                FROM pack_personal_intelligence.exchange_backfill_job
                WHERE tenant_id = @TenantA AND connection_id = @ConnectionId
                """,
                new { _db.TenantA, ConnectionId = connectionId })).ToList();
            Assert.Contains(jobs, job => job.Subtype == "inverse" && job.Status == "FAILED");
            Assert.Contains(jobs, job => job.Subtype == "spot" && job.Status != "FAILED" && job.Inserted >= 1 && job.Pages > 10);
            Assert.Contains(jobs, job => job.Subtype == "linear" && job.Status != "FAILED");
            var trades = await conn.ExecuteScalarAsync<long>(
                """
                SELECT count(*) FROM pack_personal_intelligence.trade
                WHERE tenant_id = @TenantA AND external_trade_id = 'BYBIT:TESTNET:spot:same-1'
                """,
                new { _db.TenantA });
            Assert.Equal(1, trades);
            var spot = jobs.Single(job => job.Subtype == "spot");
            Assert.True(spot.Duplicate >= 1);
            var futuresInSpot = await conn.ExecuteScalarAsync<long>(
                """
                SELECT count(*) FROM pack_personal_intelligence.trade
                WHERE tenant_id = @TenantA AND market_type = 'SPOT' AND product_subtype <> 'spot'
                  AND external_trade_id LIKE 'BYBIT:TESTNET:spot:%'
                """,
                new { _db.TenantA });
            Assert.Equal(0, futuresInSpot);

            var pagesBeforeStop = spot.Pages;
            await conn.ExecuteAsync(
                """
                UPDATE pack_personal_intelligence.exchange_backfill_job
                SET status = 'RUNNING', lease_until = NOW() - INTERVAL '5 minutes'
                WHERE tenant_id = @TenantA AND connection_id = @ConnectionId AND product_subtype = 'spot'
                """,
                new { _db.TenantA, ConnectionId = connectionId });
            await service.PumpAsync(CancellationToken.None);
            var resumed = await conn.QuerySingleAsync<(string Status, int Pages)>(
                """
                SELECT status, pages
                FROM pack_personal_intelligence.exchange_backfill_job
                WHERE tenant_id = @TenantA AND connection_id = @ConnectionId AND product_subtype = 'spot'
                  AND status IN ('QUEUED', 'RUNNING', 'INTERRUPTED', 'PARTIAL', 'COMPLETED')
                ORDER BY updated_at DESC
                LIMIT 1
                """,
                new { _db.TenantA, ConnectionId = connectionId });
            Assert.True(resumed.Pages > pagesBeforeStop);
            Assert.NotEqual("RUNNING", resumed.Status);
            var stillOne = await conn.ExecuteScalarAsync<long>(
                """
                SELECT count(*) FROM pack_personal_intelligence.trade
                WHERE tenant_id = @TenantA AND external_trade_id = 'BYBIT:TESTNET:spot:same-1'
                """,
                new { _db.TenantA });
            Assert.Equal(1, stillOne);
            var otherTenant = await conn.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantB AND external_trade_id LIKE 'BYBIT:TESTNET:spot:%'",
                new { _db.TenantB });
            Assert.Equal(0, otherTenant);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    async Task CleanupAsync()
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            """
            DELETE FROM pack_personal_intelligence.exchange_backfill_job j
            USING pack_personal_intelligence.exchange_connection c
            WHERE j.connection_id = c.connection_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'TESTNET';
            DELETE FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantA AND external_trade_id LIKE 'BYBIT:TESTNET:%';
            DELETE FROM pack_personal_intelligence.trading_account
            WHERE tenant_id = @TenantA AND external_account_id LIKE 'BYBIT:TESTNET:%';
            DELETE FROM pack_personal_intelligence.exchange_asset_balance b
            USING pack_personal_intelligence.exchange_account_snapshot s
            JOIN pack_personal_intelligence.exchange_connection c ON c.connection_id = s.connection_id
            WHERE b.snapshot_id = s.snapshot_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'TESTNET';
            DELETE FROM pack_personal_intelligence.exchange_account_snapshot s
            USING pack_personal_intelligence.exchange_connection c
            WHERE s.connection_id = c.connection_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'TESTNET';
            DELETE FROM pack_personal_intelligence.exchange_connection
            WHERE person_id = @PersonA AND exchange_code = 'BYBIT' AND environment = 'TESTNET';
            """,
            new { _db.PersonA, _db.TenantA });
    }

    sealed class ScriptAdapter : IExchangeAdapter
    {
        public string ExchangeCode => "BYBIT";
        public bool RequiresPassphrase => false;
        readonly Dictionary<string, int> _calls = new();

        public Task<ExchangeProbeDto> ProbeAsync(ExchangeCredential credential, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ExchangeHistoryPage> ReadHistoryAsync(ExchangeCredential credential, ExchangeHistoryQuery query, CancellationToken cancellationToken)
        {
            if (query.ProductSubtype == "inverse")
                throw new ExchangeConnectionException(403, "permission", "inverse denied");
            if (query.ProductSubtype == "linear")
                throw new ExchangeConnectionException(429, "rate_limit", "linear limited");
            var n = _calls.GetValueOrDefault(query.ProductSubtype) + 1;
            _calls[query.ProductSubtype] = n;
            var cursor = n < 12 ? "c" + n : null;
            return Task.FromResult(new ExchangeHistoryPage
            {
                NextCursor = cursor,
                PageFull = cursor is not null,
                Items =
                [
                    Item("same-1", 1_700_000_000_000),
                    Item("page-" + n, 1_700_000_000_000 - n),
                ],
            });
        }

        static ExchangeExecutionItemDto Item(string id, long ms) => new()
        {
            Mappable = true,
            ExecId = id,
            OrderId = "order",
            Symbol = "BTCUSDT",
            Side = "BUY",
            Quantity = 1,
            Price = 10,
            TotalValue = 10,
            FeeAmount = 0.01m,
            FeeCurrency = "USDT",
            ExecutedAt = DateTimeOffset.FromUnixTimeMilliseconds(ms),
            ExecType = "Trade",
            ExternalTradeId = "BYBIT:TESTNET:spot:" + id,
            MarketType = "SPOT",
            ProductSubtype = "spot",
            QuantityUnit = "execQty",
            SourceEndpoint = "GET /v5/execution/list",
        };
    }
}
