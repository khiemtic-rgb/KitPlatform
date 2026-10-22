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
        var page = await connector.ReadHistoryAsync(new ExchangeCredential
        {
            Environment = "MAINNET",
            ApiKey = Key,
            ApiSecret = Secret,
        }, new ExchangeHistoryQuery
        {
            ProductSubtype = "spot",
            Phase = "fills",
            Symbol = "BTCUSDT",
            WindowStartMs = 10,
            WindowEndMs = 20,
            Limit = 1000,
        }, CancellationToken.None);
        Assert.Equal("api.binance.com", binance.Host);
        Assert.Contains("signature=", binance.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, binance.Uri, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
        Assert.Single(page.Items);
        Assert.Equal("SPOT", page.Items[0].MarketType);

        var rejected = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            connector.ReadHistoryAsync(new ExchangeCredential { Environment = "https://evil.example", ApiKey = Key, ApiSecret = Secret },
                new ExchangeHistoryQuery { ProductSubtype = "spot", Symbol = "BTCUSDT", WindowStartMs = 1, WindowEndMs = 2, Limit = 10 }, CancellationToken.None));
        Assert.Equal("unsupported", rejected.Code);

        var limited = new RecordingHandler { Mode = "rate" };
        var rate = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new BinanceExchangeConnector(limited, log).ReadHistoryAsync(new ExchangeCredential { Environment = "TESTNET", ApiKey = Key, ApiSecret = Secret },
                new ExchangeHistoryQuery { ProductSubtype = "usd_m", Symbol = "BTCUSDT", WindowStartMs = 1, WindowEndMs = 2, Limit = 10 }, CancellationToken.None));
        Assert.Equal("rate_limit", rate.Code);
        Assert.Equal("testnet.binancefuture.com", limited.Host);

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

    sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Host { get; private set; }
        public string Uri { get; private set; } = "";
        public string? Passphrase { get; private set; }
        public string? Simulated { get; private set; }
        public string Mode { get; init; } = "binance";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Host = request.RequestUri?.Host;
            Uri = request.RequestUri?.ToString() ?? "";
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
                return Task.FromResult(Json("""{"code":-1022,"msg":"bad"}"""));
            return Task.FromResult(Json("""
                [{"symbol":"BTCUSDT","id":11,"orderId":22,"price":"10","qty":"1","quoteQty":"10","commission":"0.01","commissionAsset":"USDT","time":1700000000000,"isBuyer":true,"isMaker":false}]
                """));
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
