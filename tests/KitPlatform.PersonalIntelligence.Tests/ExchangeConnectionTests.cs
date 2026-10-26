using System.Net;
using System.Reflection;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Dapper;
using KitPlatform.Api.Authorization;
using KitPlatform.Api.Controllers.PersonalIntelligence;
using KitPlatform.Application.Abstractions;
using KitPlatform.Application.Configuration;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Infrastructure.Security;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure;
using KitPlatform.Packs.PersonalIntelligence.Infrastructure.Trading;
using KitPlatform.Packs.PersonalIntelligence.Trading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace KitPlatform.PersonalIntelligence.Tests;

public sealed class BybitConnectorTests
{
    const string Secret = "fixture-api-secret-0001";
    const string Key = "fixture-api-key-0001";

    [Fact]
    public void Signature_uses_documented_payload_and_lowercase_hmac()
    {
        const string timestamp = "1658384431891";
        const string query = "accountType=UNIFIED";
        var payload = timestamp + Key + BybitSigner.RecvWindow + query;
        Assert.Equal(payload, BybitSigner.Payload(timestamp, Key, BybitSigner.RecvWindow, query));
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        Assert.Equal(expected, BybitSigner.Sign(Secret, timestamp, Key, BybitSigner.RecvWindow, query));
        Assert.Equal(64, expected.Length);
    }

    [Fact]
    public void Response_dtos_have_no_secret_fields()
    {
        foreach (var type in new[]
        {
            typeof(ExchangeProbeDto), typeof(ExchangeConnectionDto), typeof(ExchangeAccountViewDto), typeof(ExchangeAssetDto),
        })
        {
            foreach (var property in type.GetProperties())
            {
                Assert.DoesNotContain("secret", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("ciphertext", property.Name, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("signature", property.Name, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Protector_roundtrip_does_not_store_plaintext_and_missing_key_fails()
    {
        var material = Convert.ToBase64String(Enumerable.Repeat((byte)0x11, 32).ToArray());
        var key = ExchangeCredentialProtector.Parse("pi-test", material);
        var envelope = ExchangeCredentialProtector.Encrypt(Secret, key);
        Assert.Equal(Secret, ExchangeCredentialProtector.Decrypt(envelope, key));
        Assert.Equal(-1, envelope.Ciphertext.AsSpan().IndexOf(Encoding.UTF8.GetBytes(Secret)));
        Assert.DoesNotContain(Secret, envelope.ToString(), StringComparison.Ordinal);
        var missing = Assert.Throws<ExchangeConnectionException>(() => ExchangeCredentialProtector.Parse("pi-test", null));
        Assert.Equal("key_missing", missing.Code);
        Assert.Equal(503, missing.StatusCode);
        var other = ExchangeCredentialProtector.Parse("pi-test", Convert.ToBase64String(Enumerable.Repeat((byte)0x22, 32).ToArray()));
        var failed = Assert.Throws<ExchangeConnectionException>(() => ExchangeCredentialProtector.Decrypt(envelope, other));
        Assert.Equal("ciphertext_invalid", failed.Code);
        Assert.Equal(503, failed.StatusCode);
        Assert.DoesNotContain(Secret, failed.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"readOnly":0,"permissions":{"Wallet":[]}}""", "not_read_only")]
    [InlineData("""{"readOnly":1,"permissions":{"Wallet":["Withdraw"]}}""", "withdrawal")]
    [InlineData("""{"readOnly":1,"permissions":{"Wallet":["AccountTransfer"]}}""", "transfer")]
    [InlineData("""{"readOnly":1,"permissions":{"Spot":["SpotTrade"]}}""", "not_read_only")]
    [InlineData("""{"readOnly":1,"permissions":{"ContractTrade":["Order"]}}""", "not_read_only")]
    [InlineData("""{"readOnly":1}""", "unverified")]
    [InlineData("""{"readOnly":1,"permissions":{}}""", "unverified")]
    [InlineData("""{"readOnly":"1","permissions":{"Wallet":[]}}""", "unverified")]
    [InlineData("""{"readOnly":1,"permissions":{"Wallet":"Withdraw"}}""", "unverified")]
    [InlineData("""{"permissions":{"Wallet":[]}}""", "unverified")]
    public void Permission_gate_rejects_write_withdraw_and_missing_metadata(string json, string code)
    {
        using var doc = JsonDocument.Parse(json);
        var error = Assert.Throws<ExchangeConnectionException>(() => BybitPermissionGate.EnsureReadable(doc.RootElement));
        Assert.Equal(code, error.Code);
    }

    [Fact]
    public async Task Read_maps_exchange_fields_and_does_not_log_credentials()
    {
        var log = new ListLog();
        var handler = new StubBybitHandler();
        var connector = new BybitExchangeConnector(handler, log);
        var probe = await connector.ReadAsync("TESTNET", Key, Secret, CancellationToken.None);

        Assert.True(probe.ReadOnlyVerified);
        Assert.Equal("123.45", probe.TotalEquity);
        Assert.Equal("120.00", probe.TotalWalletBalance);
        Assert.Null(probe.TotalAvailableBalance);
        Assert.Equal("UNIFIED", probe.AccountType);
        Assert.Equal("REGULAR_MARGIN", probe.MarginMode);
        Assert.Equal(4, probe.UnifiedMarginStatus);
        Assert.Single(probe.Assets);
        Assert.Equal("USDT", probe.Assets[0].Coin);
        Assert.Null(probe.Assets[0].AvailableToWithdraw);
        Assert.Equal("api-testnet.bybit.com", handler.LastHost);
        Assert.Contains("accountType=UNIFIED", handler.RequestText);
        Assert.DoesNotContain(Secret, handler.RequestText, StringComparison.Ordinal);
        Assert.DoesNotContain("LEAKED-FULL-KEY", JsonSerializer.Serialize(probe), StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
        Assert.DoesNotContain(Key, string.Join('\n', log.Lines), StringComparison.Ordinal);
        Assert.All(handler.Signatures, sign => Assert.DoesNotContain(sign, string.Join('\n', log.Lines), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Read_keeps_missing_balances_null_for_non_unified_and_wallet_errors()
    {
        var uta0 = await new BybitExchangeConnector(new StubBybitHandler(), new ListLog())
            .ReadAsync("TESTNET", "fixture-uta0-key", Secret, CancellationToken.None);
        Assert.Null(uta0.TotalEquity);
        Assert.Empty(uta0.Assets);
        Assert.Contains("uta=0", uta0.BalanceError, StringComparison.Ordinal);

        var wallet = await new BybitExchangeConnector(new StubBybitHandler(), new ListLog())
            .ReadAsync("TESTNET", "fixture-wallet-fail", Secret, CancellationToken.None);
        Assert.Null(wallet.TotalEquity);
        Assert.False(string.IsNullOrWhiteSpace(wallet.BalanceError));
    }

    [Theory]
    [InlineData("fixture-bad-key", 400, "invalid_key")]
    [InlineData("fixture-sign-key", 400, "signature")]
    [InlineData("fixture-denied-key", 403, "permission")]
    [InlineData("fixture-rate-key", 429, "rate_limit")]
    [InlineData("fixture-network-key", 502, "network")]
    [InlineData("fixture-write-key", 400, "not_read_only")]
    [InlineData("fixture-withdraw-key", 400, "withdrawal")]
    public async Task Read_reports_exchange_failures(string key, int status, string code)
    {
        var connector = new BybitExchangeConnector(new StubBybitHandler(), new ListLog());
        var error = await Assert.ThrowsAsync<ExchangeConnectionException>(() => connector.ReadAsync("TESTNET", key, Secret, CancellationToken.None));
        Assert.Equal(status, error.StatusCode);
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain(Secret, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_cannot_choose_the_exchange_host()
    {
        var handler = new StubBybitHandler();
        var connector = new BybitExchangeConnector(handler, new ListLog());
        var error = await Assert.ThrowsAsync<ExchangeConnectionException>(() =>
            connector.ReadAsync("https://evil.example", Key, Secret, CancellationToken.None));
        Assert.Equal("unsupported", error.Code);
        Assert.Equal(0, handler.Calls);
    }
}

[Collection("PiExchange")]
public sealed class ExchangeConnectionApiTests : IClassFixture<PiValidationDatabase>
{
    const string Secret = "fixture-api-secret-0001";
    const string Key = "fixture-api-key-0001";
    static readonly string KeyMaterial = Convert.ToBase64String(Enumerable.Repeat((byte)0x11, 32).ToArray());
    readonly PiValidationDatabase _db;

    public ExchangeConnectionApiTests(PiValidationDatabase db) => _db = db;

    [Fact]
    public async Task Lifecycle_encrypts_isolates_snapshots_and_does_not_create_trading_records()
    {
        await CleanupAsync();
        var handler = new StubBybitHandler();
        var log = new ListLog();
        await using var host = await ExchangeHost.Start(_db, handler, log, new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
        var body = new ExchangeCredentialRequest { ExchangeCode = "BYBIT", Environment = "TESTNET", ApiKey = Key, ApiSecret = Secret };

        var test = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges/test", body);
        var testText = await test.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        Assert.DoesNotContain(Secret, testText, StringComparison.Ordinal);
        Assert.DoesNotContain("LEAKED-FULL-KEY", testText, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, testText, StringComparison.Ordinal);

        var saved = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", body);
        var savedText = await saved.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.DoesNotContain(Secret, savedText, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, savedText, StringComparison.Ordinal);
        using var savedJson = JsonDocument.Parse(savedText);
        var connectionId = savedJson.RootElement.GetProperty("connection").GetProperty("connectionId").GetGuid();
        Assert.Equal("123.45", savedJson.RootElement.GetProperty("totalEquity").GetString());
        Assert.Equal("FRESH", savedJson.RootElement.GetProperty("freshness").GetString());
        Assert.Equal(JsonValueKind.Null, savedJson.RootElement.GetProperty("totalAvailableBalance").ValueKind);

        await using (var conn = await _db.OpenAsync())
        {
            var stored = await conn.QuerySingleAsync<(string Mask, int SecretAt, int KeyAt, int Snapshots)>(
                """
                SELECT c.api_key_mask,
                       position(convert_to(@Secret, 'UTF8') in c.ciphertext),
                       position(convert_to(@Key, 'UTF8') in c.ciphertext),
                       (SELECT count(*) FROM pack_personal_intelligence.exchange_account_snapshot s
                        WHERE s.connection_id = c.connection_id AND s.tenant_id = c.tenant_id)
                FROM pack_personal_intelligence.exchange_connection c
                WHERE c.connection_id = @ConnectionId AND c.tenant_id = @TenantA
                """,
                new { Secret, Key, ConnectionId = connectionId, _db.TenantA });
            Assert.Equal("••••0001", stored.Mask);
            Assert.Equal(0, stored.SecretAt);
            Assert.Equal(0, stored.KeyAt);
            Assert.Equal(1, stored.Snapshots);
        }

        var other = host.Client(_db.TenantB, _db.UserB, PersonalIntelligencePolicies.ExchangeConnectPermission);
        var hidden = await other.GetAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/account");
        Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);

        var registry = new ExchangeAdapterRegistry([new BybitExchangeConnector(handler, log)]);
        var isolated = new ExchangeConnectionService(
            _db.Factory,
            new ExchangeTestTenant(_db.TenantB, _db.UserB),
            registry,
            new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial),
            new ExchangeBackfillService(_db.Factory, new ExchangeTestTenant(_db.TenantB, _db.UserB), registry, new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial), Microsoft.Extensions.Options.Options.Create(new ExchangeBackfillOptions { WorkerEnabled = false })));
        Assert.Empty(await isolated.ListAsync(CancellationToken.None));
        var missing = await Assert.ThrowsAsync<ExchangeConnectionException>(() => isolated.GetAccountAsync(connectionId, CancellationToken.None));
        Assert.Equal(404, missing.StatusCode);

        var refreshed = await client.PostAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/refresh", null);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        await using (var conn = await _db.OpenAsync())
        {
            var sideEffects = await conn.QuerySingleAsync<TradingCounts>(
                """
                SELECT
                    (SELECT count(*) FROM pack_personal_intelligence.pi_decision
                     WHERE tenant_id = @TenantA AND (thesis ILIKE @Needle OR COALESCE(expected_outcome, '') ILIKE @Needle OR COALESCE(invalidation, '') ILIKE @Needle)) AS Decisions,
                    (SELECT count(*) FROM pack_personal_intelligence.pi_policy
                     WHERE tenant_id = @TenantA AND rule_text ILIKE @Needle) AS Policies,
                    (SELECT count(*) FROM pack_personal_intelligence.pi_operation
                     WHERE tenant_id = @TenantA AND (operation_key ILIKE @Needle OR CAST(result AS text) ILIKE @Needle)) AS Operations,
                    (SELECT count(*) FROM pack_personal_intelligence.trading_order
                     WHERE tenant_id = @TenantA AND (external_order_id = @ConnectionText OR source ILIKE @Needle)) AS Orders,
                    (SELECT count(*) FROM pack_personal_intelligence.trading_execution
                     WHERE tenant_id = @TenantA AND external_execution_id = @ConnectionText) AS Executions
                """,
                new { _db.TenantA, Needle = "%" + connectionId + "%", ConnectionText = connectionId.ToString() });
            Assert.Equal(0, sideEffects.Decisions);
            Assert.Equal(0, sideEffects.Policies);
            Assert.Equal(0, sideEffects.Operations);
            Assert.Equal(0, sideEffects.Orders);
            Assert.Equal(0, sideEffects.Executions);
            var snapshots = await conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM pack_personal_intelligence.exchange_account_snapshot WHERE connection_id = @ConnectionId AND tenant_id = @TenantA",
                new { ConnectionId = connectionId, _db.TenantA });
            Assert.Equal(2, snapshots);
        }

        var removed = await client.DeleteAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        await using (var conn = await _db.OpenAsync())
        {
            var row = await conn.QuerySingleAsync<(string Status, bool SecretGone, int Snapshots)>(
                """
                SELECT status, ciphertext IS NULL AND nonce IS NULL AND tag IS NULL,
                       (SELECT count(*) FROM pack_personal_intelligence.exchange_account_snapshot s
                        WHERE s.connection_id = c.connection_id AND s.tenant_id = c.tenant_id)
                FROM pack_personal_intelligence.exchange_connection c
                WHERE c.connection_id = @ConnectionId AND c.tenant_id = @TenantA
                """,
                new { ConnectionId = connectionId, _db.TenantA });
            Assert.Equal("DISCONNECTED", row.Status);
            Assert.True(row.SecretGone);
            Assert.Equal(2, row.Snapshots);
        }

        var account = await client.GetFromJsonAsync<ExchangeAccountViewDto>($"/api/personal-intelligence/trading/exchanges/{connectionId}/account");
        Assert.NotNull(account);
        Assert.Equal("DISCONNECTED", account.Freshness);
        Assert.Equal("123.45", account.TotalEquity);
        var callsBeforeRevoke = handler.Calls;
        var again = await client.PostAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/refresh", null);
        var againBody = await again.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(callsBeforeRevoke, handler.Calls);
        Assert.DoesNotContain(Secret, againBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, againBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
        await using (var conn = await _db.OpenAsync())
        {
            var audits = (await conn.QueryAsync<(string Operation, string Result, string? Code)>(
                """
                SELECT operation, result, error_code
                FROM pack_personal_intelligence.exchange_connection_audit
                WHERE tenant_id = @TenantA AND connection_id = @ConnectionId
                ORDER BY occurred_at, audit_id
                """,
                new { _db.TenantA, ConnectionId = connectionId })).ToList();
            Assert.Contains(audits, row => row.Operation == "save" && row.Result == "ok" && row.Code is null);
            Assert.Contains(audits, row => row.Operation == "refresh" && row.Result == "ok" && row.Code is null);
            Assert.Contains(audits, row => row.Operation == "disconnect" && row.Result == "ok" && row.Code is null);
            Assert.Contains(audits, row => row.Operation == "read" && row.Result == "ok" && row.Code is null);
            Assert.Contains(audits, row => row.Operation == "refresh" && row.Result == "rejected" && row.Code == "disconnected");
            var leaked = await conn.ExecuteScalarAsync<int>(
                """
                SELECT count(*)
                FROM pack_personal_intelligence.exchange_connection_audit
                WHERE actor_user_id = @UserA
                  AND (COALESCE(error_code, '') LIKE @Secret OR operation LIKE @Secret OR result LIKE @Secret)
                """,
                new { _db.UserA, Secret = "%" + Secret + "%" });
            Assert.Equal(0, leaked);
        }

        await CleanupAsync();
    }

    [Fact]
    public async Task Failed_checks_do_not_create_a_connection()
    {
        await CleanupAsync();
        var handler = new StubBybitHandler();
        await using var host = await ExchangeHost.Start(_db, handler, new ListLog(), new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
        foreach (var key in new[] { "fixture-bad-key", "fixture-write-key", "fixture-withdraw-key", "fixture-rate-key" })
        {
            var response = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
            {
                ExchangeCode = "BYBIT", Environment = "TESTNET", ApiKey = key, ApiSecret = Secret,
            });
            Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        }

        var runOnly = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission);
        var forbidden = await runOnly.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges/test", new ExchangeCredentialRequest
        {
            ExchangeCode = "BYBIT", Environment = "TESTNET", ApiKey = Key, ApiSecret = Secret,
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var readOnly = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeReadPermission);
        var cannotSave = await readOnly.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
        {
            ExchangeCode = "BYBIT", Environment = "TESTNET", ApiKey = Key, ApiSecret = Secret,
        });
        Assert.Equal(HttpStatusCode.Forbidden, cannotSave.StatusCode);
        var canRead = await readOnly.GetAsync("/api/personal-intelligence/trading/exchanges");
        Assert.Equal(HttpStatusCode.OK, canRead.StatusCode);

        await using var conn = await _db.OpenAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM pack_personal_intelligence.exchange_connection WHERE person_id = @PersonA AND exchange_code = 'BYBIT'",
            new { _db.PersonA });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Missing_encryption_key_refuses_save_without_calling_the_exchange()
    {
        var handler = new StubBybitHandler();
        await using var host = await ExchangeHost.Start(_db, handler, new ListLog(), new MissingExchangeKeyRing());
        var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
        var response = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
        {
            ExchangeCode = "BYBIT", Environment = "TESTNET", ApiKey = Key, ApiSecret = Secret,
        });
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("key_missing", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.DoesNotContain(Key, text, StringComparison.Ordinal);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Anonymous_and_trading_permission_cannot_read_or_guess_a_connection()
    {
        await using var host = await ExchangeHost.Start(_db, new StubBybitHandler(), new ListLog(), new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        var anonymous = host.Anonymous();
        var denied = await anonymous.GetAsync("/api/personal-intelligence/trading/exchanges");
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);

        var runOnly = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission);
        var forbidden = await runOnly.GetAsync("/api/personal-intelligence/trading/exchanges");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        var guessed = await runOnly.GetAsync($"/api/personal-intelligence/trading/exchanges/{Guid.NewGuid()}/account");
        Assert.Equal(HttpStatusCode.Forbidden, guessed.StatusCode);
        var cannotRevoke = await runOnly.DeleteAsync($"/api/personal-intelligence/trading/exchanges/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, cannotRevoke.StatusCode);
    }

    [Fact]
    public async Task Binance_revoke_stops_a_new_request_and_other_callers_cannot_read()
    {
        var handler = new BinanceReadOnlyHandler();
        var log = new ListLog();
        await using var host = await ExchangeHost.Start(_db, handler, log, new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        try
        {
            var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
            var saved = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
            {
                ExchangeCode = "BINANCE",
                Environment = "TESTNET",
                ApiKey = Key,
                ApiSecret = Secret,
            });
            var savedText = await saved.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            Assert.DoesNotContain(Secret, savedText, StringComparison.Ordinal);
            Assert.DoesNotContain(Key, savedText, StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", savedText, StringComparison.Ordinal);
            using var savedJson = JsonDocument.Parse(savedText);
            var connectionId = savedJson.RootElement.GetProperty("connection").GetProperty("connectionId").GetGuid();
            var callsAfterSave = handler.Calls;
            Assert.True(callsAfterSave >= 2);

            var runOnly = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.Permission);
            var forbidden = await runOnly.GetAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/account");
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Equal(callsAfterSave, handler.Calls);

            var other = host.Client(_db.TenantB, _db.UserB, PersonalIntelligencePolicies.ExchangeConnectPermission);
            var hidden = await other.GetAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/account");
            Assert.Equal(HttpStatusCode.Forbidden, hidden.StatusCode);
            Assert.Equal(callsAfterSave, handler.Calls);

            var removed = await client.DeleteAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}");
            Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
            var again = await client.PostAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/refresh", null);
            var againText = await again.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(callsAfterSave, handler.Calls);
            Assert.DoesNotContain(Secret, againText, StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", againText, StringComparison.Ordinal);
            Assert.DoesNotContain(Secret, string.Join('\n', log.Lines), StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", string.Join('\n', log.Lines), StringComparison.Ordinal);
        }
        finally
        {
            await using var conn = await _db.OpenAsync();
            await conn.ExecuteAsync(
                """
                DELETE FROM pack_personal_intelligence.exchange_connection_audit
                WHERE actor_user_id IN (@UserA, @UserB);
                DELETE FROM pack_personal_intelligence.exchange_backfill_job j
                USING pack_personal_intelligence.exchange_connection c
                WHERE j.connection_id = c.connection_id
                  AND c.person_id = @PersonA AND c.exchange_code = 'BINANCE';
                DELETE FROM pack_personal_intelligence.exchange_asset_balance b
                USING pack_personal_intelligence.exchange_account_snapshot s
                JOIN pack_personal_intelligence.exchange_connection c ON c.connection_id = s.connection_id
                WHERE b.snapshot_id = s.snapshot_id
                  AND c.person_id = @PersonA AND c.exchange_code = 'BINANCE';
                DELETE FROM pack_personal_intelligence.exchange_account_snapshot s
                USING pack_personal_intelligence.exchange_connection c
                WHERE s.connection_id = c.connection_id
                  AND c.person_id = @PersonA AND c.exchange_code = 'BINANCE';
                DELETE FROM pack_personal_intelligence.exchange_connection
                WHERE person_id = @PersonA AND exchange_code = 'BINANCE';
                """,
                new { _db.PersonA, _db.UserA, _db.UserB });
        }
    }

    [Fact]
    public async Task In_flight_binance_call_can_finish_after_revoke_and_the_next_call_cannot()
    {
        var handler = new GatedBinanceHandler();
        await using var host = await ExchangeHost.Start(_db, handler, new ListLog(), new StaticExchangeCredentialKeyRing("pi-test", KeyMaterial));
        try
        {
            var client = host.Client(_db.TenantA, _db.UserA, PersonalIntelligencePolicies.ExchangeConnectPermission);
            var saved = await client.PostAsJsonAsync("/api/personal-intelligence/trading/exchanges", new ExchangeCredentialRequest
            {
                ExchangeCode = "BINANCE",
                Environment = "TESTNET",
                ApiKey = Key,
                ApiSecret = Secret,
            });
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            using var savedJson = JsonDocument.Parse(await saved.Content.ReadAsStringAsync());
            var connectionId = savedJson.RootElement.GetProperty("connection").GetProperty("connectionId").GetGuid();
            var callsAfterSave = handler.Calls;

            handler.Arm();
            var refresh = client.PostAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/refresh", null);
            await handler.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var removed = await client.DeleteAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}");
            Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
            handler.Release.TrySetResult();
            var finished = await refresh;
            var finishedText = await finished.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.NotFound, finished.StatusCode);
            Assert.Contains("not_found", finishedText, StringComparison.Ordinal);
            Assert.Equal(callsAfterSave + 2, handler.Calls);
            Assert.DoesNotContain(Secret, finishedText, StringComparison.Ordinal);
            Assert.DoesNotContain("signature=", finishedText, StringComparison.Ordinal);

            var callsAfterInFlight = handler.Calls;
            var again = await client.PostAsync($"/api/personal-intelligence/trading/exchanges/{connectionId}/refresh", null);
            Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
            Assert.Equal(callsAfterInFlight, handler.Calls);
        }
        finally
        {
            await using var conn = await _db.OpenAsync();
            await conn.ExecuteAsync(
                """
                DELETE FROM pack_personal_intelligence.exchange_connection_audit
                WHERE actor_user_id IN (@UserA, @UserB);
                DELETE FROM pack_personal_intelligence.exchange_backfill_job j
                USING pack_personal_intelligence.exchange_connection c
                WHERE j.connection_id = c.connection_id
                  AND c.person_id = @PersonA AND c.exchange_code = 'BINANCE';
                DELETE FROM pack_personal_intelligence.exchange_asset_balance b
                USING pack_personal_intelligence.exchange_account_snapshot s
                JOIN pack_personal_intelligence.exchange_connection c ON c.connection_id = s.connection_id
                WHERE b.snapshot_id = s.snapshot_id
                  AND c.person_id = @PersonA AND c.exchange_code = 'BINANCE';
                DELETE FROM pack_personal_intelligence.exchange_account_snapshot s
                USING pack_personal_intelligence.exchange_connection c
                WHERE s.connection_id = c.connection_id
                  AND c.person_id = @PersonA AND c.exchange_code = 'BINANCE';
                DELETE FROM pack_personal_intelligence.exchange_connection
                WHERE person_id = @PersonA AND exchange_code = 'BINANCE';
                """,
                new { _db.PersonA, _db.UserA, _db.UserB });
        }
    }

    [Fact]
    public void Binance_adapter_exposes_no_order_or_transfer_method()
    {
        var names = typeof(BinanceExchangeConnector)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToList();
        Assert.DoesNotContain(names, name =>
            name.Contains("Order", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Withdraw", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Transfer", StringComparison.OrdinalIgnoreCase));
    }

    async Task CleanupAsync()
    {
        await using var conn = await _db.OpenAsync();
        await conn.ExecuteAsync(
            """
            DELETE FROM pack_personal_intelligence.exchange_connection_audit
            WHERE actor_user_id IN (@UserA, @UserB);
            DELETE FROM pack_personal_intelligence.exchange_backfill_job j
            USING pack_personal_intelligence.exchange_connection c
            WHERE j.connection_id = c.connection_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'TESTNET';
            DELETE FROM pack_personal_intelligence.exchange_execution_sync s
            USING pack_personal_intelligence.exchange_connection c
            WHERE s.connection_id = c.connection_id
              AND c.person_id = @PersonA AND c.exchange_code = 'BYBIT' AND c.environment = 'TESTNET';
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
            new { _db.PersonA, _db.UserA, _db.UserB });
    }

    sealed class TradingCounts
    {
        public long Decisions { get; set; }
        public long Policies { get; set; }
        public long Operations { get; set; }
        public long Orders { get; set; }
        public long Executions { get; set; }
    }
}

internal sealed class MissingExchangeKeyRing : IExchangeCredentialKeyRing
{
    public ExchangeCredentialKey RequireCurrent() => ExchangeCredentialProtector.Parse(null, null);
}

internal sealed class ExchangeTestTenant(Guid tenantId, Guid userId) : ITenantContext
{
    public Guid TenantId { get; } = tenantId;
    public Guid UserId { get; } = userId;
    public bool IsAuthenticated => true;
    public Guid? WorkspaceId => null;
}

internal sealed class ListLog : IExchangeConnectorLog
{
    public List<string> Lines { get; } = [];
    public void Info(string message) => Lines.Add(message);
}

internal sealed class GatedBinanceHandler : HttpMessageHandler
{
    int _calls;
    int _block;
    public int Calls => Volatile.Read(ref _calls);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Arm() => Volatile.Write(ref _block, 1);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        if (Interlocked.Exchange(ref _block, 0) == 1)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }

        var path = request.RequestUri?.AbsolutePath;
        var body = path == "/api/v3/account"
            ? """{"accountType":"SPOT","balances":[{"asset":"BTC","free":"0.10000000","locked":"0.00000000"}]}"""
            : """{"enableReading":true,"enableWithdrawals":false,"enableInternalTransfer":false,"permitsUniversalTransfer":false,"enableSpotAndMarginTrading":false,"enableFutures":false,"enableMargin":false,"enableVanillaOptions":false,"ipRestrict":true}""";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed class BinanceReadOnlyHandler : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        var path = request.RequestUri?.AbsolutePath;
        var body = path == "/api/v3/account"
            ? """{"accountType":"SPOT","balances":[{"asset":"BTC","free":"0.10000000","locked":"0.00000000"}]}"""
            : """{"enableReading":true,"enableWithdrawals":false,"enableInternalTransfer":false,"permitsUniversalTransfer":false,"enableSpotAndMarginTrading":false,"enableFutures":false,"enableMargin":false,"enableVanillaOptions":false,"ipRestrict":true}""";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        });
    }
}

internal sealed class StubBybitHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    public string? LastHost { get; private set; }
    public string RequestText { get; private set; } = "";
    public List<string> Signatures { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastHost = request.RequestUri?.Host;
        var key = request.Headers.GetValues("X-BAPI-API-KEY").First();
        var sign = request.Headers.GetValues("X-BAPI-SIGN").First();
        Signatures.Add(sign);
        RequestText += request.RequestUri + "\n";
        if (key == "fixture-network-key")
            throw new HttpRequestException("network");
        var path = request.RequestUri!.AbsolutePath;
        var json = Response(key, path, request.RequestUri.Query);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
    }

    static string Response(string key, string path, string query)
    {
        if (key == "fixture-bad-key") return Envelope(10003);
        if (key == "fixture-sign-key") return Envelope(10004);
        if (key == "fixture-denied-key") return Envelope(10005);
        if (key == "fixture-rate-key") return Envelope(10006);
        if (path.EndsWith("/query-api", StringComparison.Ordinal))
        {
            var readOnly = key == "fixture-write-key" ? 0 : 1;
            var wallet = key == "fixture-withdraw-key" ? """["Withdraw"]""" : "[]";
            var uta = key == "fixture-uta0-key" ? 0 : 1;
            var permissions = key == "fixture-noperm-key"
                ? ""
                : "\"permissions\": {\"ContractTrade\": [], \"Spot\": [], \"Wallet\": " + wallet + ", \"Options\": []}, ";
            var readOnlyJson = key == "fixture-noperm-key" ? "" : "\"readOnly\": " + readOnly + ", ";
            return "{\"retCode\":0,\"retMsg\":\"\",\"result\":{\"apiKey\":\"LEAKED-FULL-KEY\","
                + readOnlyJson + permissions
                + "\"uta\":" + uta + ",\"userID\":9001,\"deadlineDay\":-2,\"expiredAt\":\"1970-01-01T00:00:00Z\"}}";
        }

        if (path.EndsWith("/account/info", StringComparison.Ordinal))
            return """{"retCode":0,"retMsg":"OK","result":{"marginMode":"REGULAR_MARGIN","unifiedMarginStatus":4}}""";
        if (path.EndsWith("/wallet-balance", StringComparison.Ordinal))
        {
            if (key == "fixture-wallet-fail") return Envelope(10001);
            return """{"retCode":0,"retMsg":"OK","result":{"list":[{"accountType":"UNIFIED","totalEquity":"123.45","totalWalletBalance":"120.00","coin":[{"coin":"USDT","walletBalance":"120.00","equity":"123.45","usdValue":"123.45"}]}]}}""";
        }

        if (path.EndsWith("/execution/list", StringComparison.Ordinal))
        {
            if (query.Contains("cursor=", StringComparison.Ordinal))
            {
                return """
                    {"retCode":0,"retMsg":"OK","result":{"category":"linear","nextPageCursor":"","list":[
                      {"symbol":"BTCUSDT","orderId":"order-9","side":"Buy","execFee":"0.03","execId":"exec-3","execPrice":"25020","execQty":"0.02","execType":"Trade","execValue":"500.4","execTime":"1729000004000","isMaker":false,"feeRate":"0.0001"}
                    ]}}
                    """;
            }

            return """
                {"retCode":0,"retMsg":"OK","result":{"category":"linear","nextPageCursor":"CURSOR2","list":[
                  {"symbol":"BTCUSDT","orderId":"order-1","side":"Buy","execFee":"0.05","execId":"exec-1","execPrice":"25000.5","execQty":"0.01","execType":"Trade","execValue":"250.005","execTime":"1729000000000","feeCurrency":"USDT","isMaker":true},
                  {"symbol":"BTCUSDT","orderId":"order-1","side":"Sell","execFee":"0.02","execId":"exec-2","execPrice":"25010","execQty":"0.01","execType":"Trade","execValue":"250.1","execTime":"1729000001000","feeCurrency":"USDT"},
                  {"symbol":"BTCUSDT","orderId":"order-2","side":"Buy","execFee":"0.01","execId":"fund-1","execPrice":"25000","execQty":"0.01","execType":"Funding","execTime":"1729000002000","feeCurrency":"USDT"},
                  {"symbol":"ETHUSDT","orderId":"order-3","side":"Buy","execFee":"0.01","execId":"bad-1","execPrice":"0","execQty":"1","execType":"Trade","execTime":"1729000003000","feeCurrency":"USDT"}
                ]}}
                """;
        }

        return Envelope(10001);
    }

    static string Envelope(int code) => "{\"retCode\":" + code + ",\"retMsg\":\"error\",\"result\":{}}";
}

internal sealed class ExchangeHost : IAsyncDisposable
{
    readonly WebApplication _app;
    ExchangeHost(WebApplication app) => _app = app;

    public static async Task<ExchangeHost> Start(
        PiValidationDatabase db, HttpMessageHandler handler, IExchangeConnectorLog log, IExchangeCredentialKeyRing keys)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IDbConnectionFactory>(db.Factory);
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddScoped<ITenantPlatformSettings, ExchangeModuleSettings>();
        builder.Services.AddSingleton<HttpMessageHandler>(handler);
        builder.Services.AddSingleton<IExchangeConnectorLog>(log);
        builder.Services.AddSingleton<IExchangeCredentialKeyRing>(keys);
        builder.Services.AddPersonalIntelligencePack();
        builder.Services.Configure<ExchangeBackfillOptions>(options => options.WorkerEnabled = false);
        builder.Services.AddAuthentication(ExchangeHeaderAuth.AuthScheme)
            .AddScheme<AuthenticationSchemeOptions, ExchangeHeaderAuth>(ExchangeHeaderAuth.AuthScheme, _ => { });
        builder.Services.AddAuthorization(options => options.AddPersonalIntelligenceAuthorization());
        builder.Services.AddControllers().AddApplicationPart(typeof(TradingLoopController).Assembly);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        await app.StartAsync();
        return new ExchangeHost(app);
    }

    public HttpClient Anonymous() => _app.GetTestClient();

    public HttpClient Client(Guid tenant, Guid user, params string[] permissions)
    {
        var client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user.ToString());
        client.DefaultRequestHeaders.Add("X-Test-Tenant", tenant.ToString());
        foreach (var permission in permissions)
            client.DefaultRequestHeaders.Add("X-Test-Permission", permission);
        return client;
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    sealed class ExchangeModuleSettings(IDbConnectionFactory db, ITenantContext tenant) : ITenantPlatformSettings
    {
        public async Task<bool> IsModuleEnabledAsync(string moduleCode, CancellationToken cancellationToken = default)
        {
            await using var conn = await db.CreateOpenConnectionAsync(cancellationToken);
            var json = await conn.ExecuteScalarAsync<string>(
                "SELECT settings::text FROM public.tenants WHERE id = @TenantId AND deleted_at IS NULL",
                new { tenant.TenantId });
            if (string.IsNullOrWhiteSpace(json))
                return false;
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("platform", out var platform)
                || !platform.TryGetProperty("enabled_modules", out var list)
                || list.ValueKind != JsonValueKind.Array)
                return false;
            return list.EnumerateArray().Any(item => string.Equals(item.GetString(), moduleCode, StringComparison.OrdinalIgnoreCase));
        }

        public Task<TenantPlatformSettingsDto> GetAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlatformModuleRegistryItemDto>> ListModulesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<TenantPlatformSettingsUpdateResultDto> UpdateAsync(UpdateTenantPlatformSettingsRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsModuleEnabledForTenantCodeAsync(string tenantCode, string moduleCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFeatureEnabledAsync(string featureCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> IsFeatureEnabledForTenantCodeAsync(string tenantCode, string featureCode, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string?> GetLabelAsync(string translationKey, string? locale = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ResolvedTenantLocaleDto> ResolveLocaleAsync(Guid? branchId = null, Guid? customerAccountId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    sealed class ExchangeHeaderAuth(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string AuthScheme = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var user) || !Guid.TryParse(user, out _))
                return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim>
            {
                new("sub", user.ToString()),
                new("tenant_id", Request.Headers["X-Test-Tenant"].ToString()),
            };
            foreach (var permission in Request.Headers["X-Test-Permission"])
            {
                if (!string.IsNullOrWhiteSpace(permission))
                    claims.Add(new Claim("permission", permission));
            }

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, AuthScheme)), AuthScheme)));
        }
    }
}
