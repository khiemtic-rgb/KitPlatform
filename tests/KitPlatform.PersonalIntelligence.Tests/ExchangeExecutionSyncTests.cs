using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using KitPlatform.Api.Authorization;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class BybitExecutionHistoryTests
{
    const string Secret = "fixture-api-secret-0001";
    const string Key = "fixture-api-key-0001";

    [Fact]
    public void Query_signs_the_encoded_cursor_and_keeps_the_seven_day_call_limit()
    {
        var query = BybitExecutionMapper.Query("LINEAR", 10, 20, 100, "542:1,542:1");
        Assert.Equal("category=linear&startTime=10&endTime=20&limit=100&cursor=542%3A1%2C542%3A1", query);
        var sign = BybitSigner.Sign(Secret, "1658384431891", Key, BybitSigner.RecvWindow, query);
        Assert.Equal(64, sign.Length);
        Assert.DoesNotContain(Secret, query, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, sign, StringComparison.Ordinal);

        var start = DateTimeOffset.FromUnixTimeMilliseconds(0);
        BybitExecutionMapper.ValidateCallWindow(start.ToUnixTimeMilliseconds(), start.AddDays(7).ToUnixTimeMilliseconds());
        var tooWide = Assert.Throws<ExchangeConnectionException>(() =>
            BybitExecutionMapper.ValidateCallWindow(start.ToUnixTimeMilliseconds(), start.AddDays(8).ToUnixTimeMilliseconds()));
        Assert.Equal("window", tooWide.Code);

        var windows = BybitExecutionMapper.Windows(start, start.AddDays(10));
        Assert.Equal(2, windows.Count);
        Assert.True(windows[0].Start > windows[1].Start);
        var overRange = Assert.Throws<ExchangeConnectionException>(() => BybitExecutionMapper.Windows(start, start.AddDays(31).AddSeconds(1)));
        Assert.Equal("window", overRange.Code);
    }

    [Fact]
    public void Mapper_keeps_multiple_fills_for_one_order_and_does_not_import_funding()
    {
        using var doc = JsonDocument.Parse("""
            {"retCode":0,"result":{"category":"linear","nextPageCursor":"CURSOR2","list":[
              {"symbol":"BTCUSDT","orderId":"order-1","side":"Buy","execFee":"0.05","execId":"exec-1","execPrice":"25000.5","execQty":"0.01","execType":"Trade","execValue":"250.005","execTime":"1729000000000","feeCurrency":"USDT"},
              {"symbol":"BTCUSDT","orderId":"order-1","side":"Sell","execFee":"-0.01","execId":"exec-2","execPrice":"25010","execQty":"0.01","execType":"Trade","execValue":"250.1","execTime":"1729000001000","feeCurrency":"USDT"},
              {"symbol":"BTCUSDT","orderId":"order-2","side":"Buy","execFee":"0.01","execId":"fund-1","execPrice":"25000","execQty":"1","execType":"Funding","execTime":"1729000002000","feeCurrency":"USDT"},
              {"symbol":"ETHUSDT","orderId":"order-3","side":"Buy","execFee":"0.01","execId":"bad-1","execPrice":"0","execQty":"1","execType":"Trade","execTime":"1729000003000"},
              {"symbol":"BTCUSDT","orderId":"order-4","side":"Buy","execId":"exec-fee","execPrice":"10","execQty":"1","execType":"Trade","execTime":"1729000004000","execFee":"0.02"}
            ]}}
            """);
        var page = BybitExecutionMapper.ReadPage(doc.RootElement, "TESTNET", "linear");
        Assert.Equal("CURSOR2", page.NextPageCursor);
        var mapped = page.Items.Where(item => item.Mappable).ToList();
        Assert.Equal(3, mapped.Count);
        Assert.Equal("order-1", mapped[0].OrderId);
        Assert.Equal("order-1", mapped[1].OrderId);
        Assert.NotEqual(mapped[0].ExternalTradeId, mapped[1].ExternalTradeId);
        Assert.Equal("BYBIT:TESTNET:linear:exec-1", mapped[0].ExternalTradeId);
        Assert.Equal("BYBIT:TESTNET:linear:exec-2", mapped[1].ExternalTradeId);
        Assert.Equal(-0.01m, mapped[1].FeeAmount);
        Assert.Contains("feeCurrency", mapped[2].MissingFields);
        Assert.Equal("EXCHANGE", TradeSource.Exchange);

        var funding = page.Items.Single(item => item.ExecId == "fund-1");
        Assert.False(funding.Mappable);
        Assert.Contains("Funding", funding.Reason, StringComparison.Ordinal);
        var zero = page.Items.Single(item => item.ExecId == "bad-1");
        Assert.False(zero.Mappable);
        Assert.Contains("không dương", zero.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_category_and_category_mismatch_are_explicit()
    {
        var error = Assert.Throws<ExchangeConnectionException>(() => BybitExecutionMapper.NormalizeCategory("margin"));
        Assert.Equal("unsupported_category", error.Code);
        using var doc = JsonDocument.Parse("""{"retCode":0,"result":{"category":"spot","list":[]}}""");
        var mismatch = Assert.Throws<ExchangeConnectionException>(() => BybitExecutionMapper.ReadPage(doc.RootElement, "TESTNET", "linear"));
        Assert.Equal("category_mismatch", mismatch.Code);
    }

    [Fact]
    public async Task Execution_request_is_signed_paginated_and_rate_limited_without_logging_the_secret()
    {
        var log = new ListLog();
        var handler = new ExecutionScriptHandler();
        var connector = new BybitExchangeConnector(handler, log);
        var first = await connector.ListExecutionsAsync("TESTNET", Key, Secret, "linear", 10, 20, 100, null, CancellationToken.None);
        var second = await connector.ListExecutionsAsync("TESTNET", Key, Secret, "linear", 10, 20, 100, first.NextPageCursor, CancellationToken.None);
        Assert.Equal("CURSOR2", first.NextPageCursor);
        Assert.True(string.IsNullOrEmpty(second.NextPageCursor));
        Assert.Equal(2, handler.Calls);
        Assert.Contains("cursor=CURSOR2", handler.Uris[1], StringComparison.Ordinal);
        Assert.Equal("api-testnet.bybit.com", handler.Host);
        Assert.DoesNotContain(Secret, string.Join('\n', handler.Uris), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, string.Join('\n', log.Lines), StringComparison.Ordinal);

        var idle = new ExecutionScriptHandler();
        var rejected = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new BybitExchangeConnector(idle, log).ListExecutionsAsync("TESTNET", Key, Secret, "linear", 0, 8L * 24 * 60 * 60 * 1000, 100, null, CancellationToken.None));
        Assert.Equal("window", rejected.Code);
        Assert.Equal(0, idle.Calls);

        var limited = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new BybitExchangeConnector(new ExecutionScriptHandler { Mode = "ret-10006" }, log)
                .ListExecutionsAsync("TESTNET", Key, Secret, "spot", 10, 20, 50, null, CancellationToken.None));
        Assert.Equal(429, limited.StatusCode);
        Assert.Equal("rate_limit", limited.Code);
        Assert.DoesNotContain(Secret, limited.Message, StringComparison.Ordinal);

        var http = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new BybitExchangeConnector(new ExecutionScriptHandler { Mode = "http-429" }, log)
                .ListExecutionsAsync("TESTNET", Key, Secret, "option", 10, 20, 50, null, CancellationToken.None));
        Assert.Equal(429, http.StatusCode);
        var invalid = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            new BybitExchangeConnector(new ExecutionScriptHandler { Mode = "invalid-json" }, log)
                .ListExecutionsAsync("TESTNET", Key, Secret, "inverse", 10, 20, 50, null, CancellationToken.None));
        Assert.Equal("exchange", invalid.Code);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
    }

    sealed class ExecutionScriptHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? Host { get; private set; }
        public List<string> Uris { get; } = [];
        public string Mode { get; init; } = "ok";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            Host = request.RequestUri?.Host;
            Uris.Add(request.RequestUri?.ToString() ?? "");
            var sign = request.Headers.GetValues("X-BAPI-SIGN").First();
            var timestamp = request.Headers.GetValues("X-BAPI-TIMESTAMP").First();
            var query = (request.RequestUri?.Query ?? "").TrimStart('?');
            var expected = BybitSigner.Sign(Secret, timestamp, Key, BybitSigner.RecvWindow, query);
            if (!string.Equals(sign, expected, StringComparison.Ordinal))
                return Task.FromResult(Json(BybitEnvelope(10004)));
            if (Mode == "http-429")
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)429));
            if (Mode == "invalid-json")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{", Encoding.UTF8, "application/json") });
            if (Mode == "ret-10006")
                return Task.FromResult(Json(BybitEnvelope(10006)));
            var cursor = query.Contains("cursor=", StringComparison.Ordinal);
            var body = cursor
                ? """{"retCode":0,"retMsg":"OK","result":{"category":"linear","nextPageCursor":"","list":[]}}"""
                : """{"retCode":0,"retMsg":"OK","result":{"category":"linear","nextPageCursor":"CURSOR2","list":[{"symbol":"BTCUSDT","orderId":"order-1","side":"Buy","execFee":"0.01","feeCurrency":"USDT","execId":"exec-a","execPrice":"10","execQty":"1","execType":"Trade","execTime":"1729000000000"}]}}""";
            return Task.FromResult(Json(body));
        }

        static HttpResponseMessage Json(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

        static string BybitEnvelope(int code) => "{\"retCode\":" + code + ",\"retMsg\":\"error\",\"result\":{}}";
    }
}

[Collection("PiExchange")]
public sealed class ExchangeExecutionSyncApiTests : IClassFixture<PiValidationDatabase>
{
    const string Secret = "fixture-api-secret-0001";
    const string Key = "fixture-api-key-0001";
    static readonly string KeyMaterial = Convert.ToBase64String(Enumerable.Repeat((byte)0x11, 32).ToArray());
    readonly PiValidationDatabase _db;

    public ExchangeExecutionSyncApiTests(PiValidationDatabase db) => _db = db;

    [Fact]
    public async Task Sync_is_idempotent_isolated_and_does_not_create_decisions()
    {
        await CleanupAsync();
        try
        {
        var handler = new StubBybitHandler();
        var log = new ListLog();
        await using var host = await ExchangeHost.Start(_db, handler, log, new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission, PersonalIntelligencePolicies.ExchangeReadPermission);
        var saved = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
        {
            ExchangeCode = "BYBIT", Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret,
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var savedJson = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
        var connectionId = savedJson.RootElement.GetProperty("connection").GetProperty("connectionId").GetGuid();
        var callsAfterSave = handler.Calls;
        var window = new ExchangeExecutionSyncRequest
        {
            Category = "LINEAR",
            Start = DateTimeOffset.Parse("2024-10-01T00:00:00Z"),
            End = DateTimeOffset.Parse("2024-10-02T00:00:00Z"),
        };

        var unsupported = await client.PostAsJsonAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions/preview", new ExchangeExecutionSyncRequest
        {
            Category = "margin", Start = window.Start, End = window.End,
        });
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
        Assert.Contains("unsupported_category", await unsupported.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(callsAfterSave, handler.Calls);

        var preview = await client.PostAsJsonAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions/preview", window);
        var previewText = await preview.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.DoesNotContain(Secret, previewText, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, previewText, StringComparison.Ordinal);
        using var previewJson = JsonDocument.Parse(previewText);
        Assert.False(previewJson.RootElement.GetProperty("committed").GetBoolean());
        Assert.Equal(3, previewJson.RootElement.GetProperty("insertedCount").GetInt32());
        Assert.Equal(2, previewJson.RootElement.GetProperty("unmappedCount").GetInt32());
        Assert.Equal("EXCHANGE", previewJson.RootElement.GetProperty("source").GetString());
        Assert.Equal("linear", previewJson.RootElement.GetProperty("category").GetString());
        await using (var conn = await _db.OpenAsync())
        {
            var writes = await conn.QuerySingleAsync<(long Trades, long Syncs)>(
                """
                SELECT
                    (SELECT count(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantA AND external_trade_id LIKE 'BYBIT:MAINNET:%'),
                    (SELECT count(*) FROM pack_personal_intelligence.exchange_execution_sync WHERE tenant_id = @TenantA)
                """,
                new { _db.TenantA });
            Assert.Equal(0, writes.Trades);
            Assert.Equal(0, writes.Syncs);
        }

        var committed = await client.PostAsJsonAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions", window);
        var committedText = await committed.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, committed.StatusCode);
        Assert.DoesNotContain(Secret, committedText, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, committedText, StringComparison.Ordinal);
        using var committedJson = JsonDocument.Parse(committedText);
        var accountId = committedJson.RootElement.GetProperty("tradingAccountId").GetGuid();
        Assert.True(committedJson.RootElement.GetProperty("committed").GetBoolean());
        Assert.Equal(3, committedJson.RootElement.GetProperty("insertedCount").GetInt32());
        Assert.Equal(0, committedJson.RootElement.GetProperty("duplicateCount").GetInt32());
        Assert.Contains("feeCurrency", committedJson.RootElement.GetProperty("missingFields").EnumerateArray().Select(item => item.GetString()));
        Assert.Contains(committedJson.RootElement.GetProperty("issues").EnumerateArray(), item =>
            item.GetProperty("execType").GetString() == "Funding");
        Assert.Equal("api.bybit.com", handler.LastHost);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);

        await using (var conn = await _db.OpenAsync())
        {
            var rows = (await conn.QueryAsync<(string External, string Source, string SourceType, decimal Price, decimal? Fee, string? FeeCurrency, string? OrderId, string Endpoint)>(
                """
                SELECT external_trade_id, source, source_type, price, fee_amount, fee_currency,
                       metadata->>'exchangeOrderId', metadata->>'sourceEndpoint'
                FROM pack_personal_intelligence.trade
                WHERE tenant_id = @TenantA AND trading_account_id = @AccountId
                ORDER BY external_trade_id
                """,
                new { _db.TenantA, AccountId = accountId })).ToList();
            Assert.Equal(3, rows.Count);
            Assert.All(rows, row =>
            {
                Assert.Equal("EXCHANGE", row.Source);
                Assert.Equal("EXCHANGE", row.SourceType);
                Assert.NotEqual("FIXTURE", row.Source);
                Assert.NotEqual("IMPORT", row.Source);
                Assert.Equal("GET /v5/execution/list", row.Endpoint);
            });
            Assert.Equal(2, rows.Count(row => row.OrderId == "order-1"));
            var third = rows.Single(row => row.External.EndsWith("exec-3", StringComparison.Ordinal));
            Assert.Equal(0.03m, third.Fee);
            Assert.Null(third.FeeCurrency);
            var fees = await conn.ExecuteScalarAsync<long>(
                """
                SELECT count(*) FROM pack_personal_intelligence.fee f
                JOIN pack_personal_intelligence.trade t ON t.trade_id = f.trade_id AND t.tenant_id = f.tenant_id
                WHERE t.tenant_id = @TenantA AND t.trading_account_id = @AccountId
                """,
                new { _db.TenantA, AccountId = accountId });
            Assert.Equal(0, fees);
            await conn.ExecuteAsync(
                """
                UPDATE pack_personal_intelligence.trade
                SET price = 1.23
                WHERE tenant_id = @TenantA AND external_trade_id = 'BYBIT:MAINNET:linear:exec-1'
                """,
                new { _db.TenantA });
        }

        var again = await client.PostAsJsonAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions", window);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        using var againJson = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
        Assert.Equal(0, againJson.RootElement.GetProperty("insertedCount").GetInt32());
        Assert.Equal(3, againJson.RootElement.GetProperty("duplicateCount").GetInt32());
        await using (var conn = await _db.OpenAsync())
        {
            var kept = await conn.QuerySingleAsync<(decimal Price, long Rows, long Decisions, long Policies, long Operations, long Orders, long Executions)>(
                """
                SELECT
                    (SELECT price FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantA AND external_trade_id = 'BYBIT:MAINNET:linear:exec-1'),
                    (SELECT count(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantA AND external_trade_id LIKE 'BYBIT:MAINNET:linear:exec-%'),
                    (SELECT count(*) FROM pack_personal_intelligence.pi_decision WHERE tenant_id = @TenantA AND thesis ILIKE '%BYBIT:MAINNET:linear:exec-%'),
                    (SELECT count(*) FROM pack_personal_intelligence.pi_policy WHERE tenant_id = @TenantA AND rule_text ILIKE '%BYBIT:MAINNET:linear:exec-%'),
                    (SELECT count(*) FROM pack_personal_intelligence.pi_operation WHERE tenant_id = @TenantA AND CAST(result AS text) ILIKE '%BYBIT:MAINNET:linear:exec-%'),
                    (SELECT count(*) FROM pack_personal_intelligence.trading_order WHERE tenant_id = @TenantA AND external_order_id LIKE 'BYBIT:MAINNET%'),
                    (SELECT count(*) FROM pack_personal_intelligence.trading_execution WHERE tenant_id = @TenantA AND external_execution_id LIKE 'BYBIT:MAINNET%')
                """,
                new { _db.TenantA });
            Assert.Equal(1.23m, kept.Price);
            Assert.Equal(3, kept.Rows);
            Assert.Equal(0, kept.Decisions);
            Assert.Equal(0, kept.Policies);
            Assert.Equal(0, kept.Operations);
            Assert.Equal(0, kept.Orders);
            Assert.Equal(0, kept.Executions);
            var otherTrades = await conn.ExecuteScalarAsync<long>(
                "SELECT count(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantB AND external_trade_id LIKE 'BYBIT:MAINNET:%'",
                new { _db.TenantB });
            Assert.Equal(0, otherTrades);
        }

        var other = host.Client(_db.TenantB, _db.UserB, PersonalIntelligencePolicies.ExchangeConnectPermission);
        var hidden = await other.PostAsJsonAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions", window);
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
        var isolated = new ExchangeExecutionSyncService(
            _db.Factory,
            new ExchangeTestTenant(_db.TenantB, _db.UserB),
            new BybitExchangeConnector(handler, log),
            new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        var missing = await Assert.ThrowsAsync<ExchangeConnectionException>(() => isolated.LatestAsync(connectionId, CancellationToken.None));
        Assert.Equal(404, missing.StatusCode);
        var denied = await Assert.ThrowsAsync<ExchangeConnectionException>(() => isolated.CommitAsync(connectionId, window, CancellationToken.None));
        Assert.Equal(404, denied.StatusCode);
        var latest = await client.GetAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions/latest");
        Assert.Equal(HttpStatusCode.OK, latest.StatusCode);
        Assert.DoesNotContain(Secret, await latest.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await CleanupAsync();
        }
    }

    [Fact]
    public async Task Sync_stops_at_the_page_cap_and_reports_the_history_as_incomplete()
    {
        await CleanupAsync();
        try
        {
        var handler = new CappedExecutionHandler();
        await using var host = await ExchangeHost.Start(_db, handler, new ListLog(), new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
        var saved = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
        {
            ExchangeCode = "BYBIT", Environment = "MAINNET", ApiKey = Key, ApiSecret = Secret,
        });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        using var savedJson = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
        var connectionId = savedJson.RootElement.GetProperty("connection").GetProperty("connectionId").GetGuid();
        var response = await client.PostAsJsonAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/executions", new ExchangeExecutionSyncRequest
        {
            Category = "linear",
            Start = DateTimeOffset.Parse("2024-10-01T00:00:00Z"),
            End = DateTimeOffset.Parse("2024-10-20T00:00:00Z"),
        });
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(text);
        Assert.True(json.RootElement.GetProperty("truncated").GetBoolean());
        Assert.Equal(BybitExecutionMapper.MaxPages, json.RootElement.GetProperty("pages").GetInt32());
        Assert.Equal(BybitExecutionMapper.MaxPages, json.RootElement.GetProperty("insertedCount").GetInt32());
        Assert.Equal(BybitExecutionMapper.MaxPages, handler.ExecutionCalls);
        Assert.True(json.RootElement.TryGetProperty("resumeBefore", out var resume) && resume.ValueKind == JsonValueKind.String);
        await using var conn = await _db.OpenAsync();
        var eleventh = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_personal_intelligence.trade WHERE tenant_id = @TenantA AND external_trade_id = 'BYBIT:MAINNET:linear:page-11'",
            new { _db.TenantA });
        Assert.Equal(0, eleventh);
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
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'MAINNET';
            DELETE FROM pack_personal_intelligence.exchange_execution_sync s
            USING pack_personal_intelligence.exchange_connection c
            WHERE s.connection_id = c.connection_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'MAINNET';
            DELETE FROM pack_personal_intelligence.fee f
            USING pack_personal_intelligence.trade t
            WHERE f.trade_id = t.trade_id AND f.tenant_id = t.tenant_id
              AND t.tenant_id = @TenantA AND t.external_trade_id LIKE 'BYBIT:MAINNET:%';
            DELETE FROM pack_personal_intelligence.trade
            WHERE tenant_id = @TenantA AND external_trade_id LIKE 'BYBIT:MAINNET:%';
            DELETE FROM pack_personal_intelligence.trading_account
            WHERE tenant_id = @TenantA AND external_account_id LIKE 'BYBIT:MAINNET:%';
            DELETE FROM pack_personal_intelligence.exchange_asset_balance b
            USING pack_personal_intelligence.exchange_account_snapshot s
            JOIN pack_personal_intelligence.exchange_connection c ON c.connection_id = s.connection_id
            WHERE b.snapshot_id = s.snapshot_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'MAINNET';
            DELETE FROM pack_personal_intelligence.exchange_account_snapshot s
            USING pack_personal_intelligence.exchange_connection c
            WHERE s.connection_id = c.connection_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'MAINNET';
            DELETE FROM pack_personal_intelligence.exchange_connection
            WHERE person_id = @PersonA AND exchange_code = 'BYBIT' AND environment = 'MAINNET';
            """,
            new { _db.PersonA, _db.TenantA });
    }

    sealed class CappedExecutionHandler : HttpMessageHandler
    {
        public int ExecutionCalls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/execution/list", StringComparison.Ordinal))
            {
                ExecutionCalls++;
                var n = ExecutionCalls;
                var time = 1800000000000L - n * 1000;
                var cursor = n > 12 ? "" : "c" + n;
                var body = "{\"retCode\":0,\"retMsg\":\"OK\",\"result\":{\"category\":\"linear\",\"nextPageCursor\":\"" + cursor
                    + "\",\"list\":[{\"symbol\":\"BTCUSDT\",\"orderId\":\"ord\",\"side\":\"Buy\",\"execFee\":\"0.01\",\"feeCurrency\":\"USDT\",\"execId\":\"page-"
                    + n + "\",\"execPrice\":\"10\",\"execQty\":\"1\",\"execType\":\"Trade\",\"execValue\":\"10\",\"execTime\":\"" + time + "\"}]}}";
                return Task.FromResult(Ok(body));
            }

            if (path.EndsWith("/query-api", StringComparison.Ordinal))
            {
                return Task.FromResult(Ok("""
                    {"retCode":0,"retMsg":"","result":{"readOnly":1,"permissions":{"ContractTrade":[],"Spot":[],"Wallet":[],"Options":[]},"uta":1,"userID":9001,"deadlineDay":-2,"expiredAt":"1970-01-01T00:00:00Z"}}
                    """));
            }

            if (path.EndsWith("/account/info", StringComparison.Ordinal))
                return Task.FromResult(Ok("""{"retCode":0,"retMsg":"OK","result":{"marginMode":"REGULAR_MARGIN","unifiedMarginStatus":4}}"""));
            if (path.EndsWith("/wallet-balance", StringComparison.Ordinal))
                return Task.FromResult(Ok("""{"retCode":0,"retMsg":"OK","result":{"list":[{"accountType":"UNIFIED","totalEquity":"1","totalWalletBalance":"1","coin":[{"coin":"USDT","walletBalance":"1","equity":"1","usdValue":"1"}]}]}}"""));
            return Task.FromResult(Ok("{\"retCode\":10001,\"retMsg\":\"error\",\"result\":{}}"));
        }

        static HttpResponseMessage Ok(string body) =>
            new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
