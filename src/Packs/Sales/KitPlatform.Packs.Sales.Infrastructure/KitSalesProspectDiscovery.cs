using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesProspectDiscovery : IKitSalesProspectDiscovery
{
    private const string Bbox = "21.30,105.40,22.05,106.20";

    private const string Query = """
        [out:json][timeout:20];
        node["amenity"="pharmacy"](21.30,105.40,22.05,106.20);
        out 15;
        """;

    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;

    public KitSalesProspectDiscovery(IDbConnectionFactory db, IHttpClientFactory http)
    {
        _db = db;
        _http = http;
    }

    public async Task<KitSalesDiscoveryRunDto> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var source = NovixaSourceRegistry.OsmOverpass;
        if (!NovixaSourceRegistry.AllowsCollection(source.Code))
            return new KitSalesDiscoveryRunDto(source.Code, "blocked", 0, 0, 0, false);

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var gateRow = await conn.QuerySingleAsync<GateRow>(
            new CommandDefinition(
                """
                SELECT
                    MAX(created_at) FILTER (WHERE status = 'completed') AS LastSuccess,
                    MAX(created_at) FILTER (WHERE status = 'failed') AS LastFailure
                FROM pack_sales.agent_run
                WHERE tenant_id = @TenantId AND agent_code = 'discovery'
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        var gate = DiscoverySchedule.Gate(
            AsUtc(gateRow.LastSuccess),
            AsUtc(gateRow.LastFailure),
            DateTimeOffset.UtcNow,
            source.MinInterval);
        if (gate is "rate_limited" or "backoff")
            return new KitSalesDiscoveryRunDto(source.Code, gate, 0, 0, 0, false);

        var fetched = await FetchAsync(source.MaxResults, cancellationToken);
        if (fetched.Body is null || !OsmOverpassParser.TryParse(fetched.Body, source.MaxResults, out var prospects, out _))
        {
            await RecordFailureAsync(conn, tenantId, fetched.Reason, cancellationToken);
            return new KitSalesDiscoveryRunDto(source.Code, "source_failed", 0, 0, 0, false);
        }

        var ids = (await conn.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT username
                FROM pack_sales.business_identity
                WHERE tenant_id = @TenantId AND platform = 'osm' AND username IS NOT NULL
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken))).ToHashSet(StringComparer.Ordinal);
        var names = (await conn.QueryAsync<string>(
            new CommandDefinition(
                "SELECT name FROM pack_sales.business WHERE tenant_id = @TenantId",
                new { TenantId = tenantId },
                cancellationToken: cancellationToken)))
            .Select(ProspectNames.FoldBusiness)
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.Ordinal);

        var created = new List<PublicProspect>();
        var decisions = new List<DiscoveryDecision>();
        foreach (var prospect in prospects)
        {
            var decision = ProspectDiscoveryPlanner.Decide(prospect, new DiscoveryMemory(ids, names));
            decisions.Add(decision);
            if (decision.Action != "create")
                continue;
            created.Add(prospect);
            ids.Add(prospect.ExternalId);
            names.Add(ProspectNames.FoldBusiness(prospect.Name));
        }

        var skipped = decisions.Count(item => item.Action == "skip_duplicate");
        var rejected = decisions.Count(item => item.Action == "reject");
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var runId = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.agent_run (
                    tenant_id, agent_code, status, input_json, output_json, policy_json)
                VALUES (
                    @TenantId, 'discovery', 'completed',
                    @Input::jsonb, @Output::jsonb, @Policy::jsonb)
                RETURNING id
                """,
                new
                {
                    TenantId = tenantId,
                    Input = JsonSerializer.Serialize(new
                    {
                        source = "osm_overpass",
                        bbox = Bbox,
                        maxResults = source.MaxResults,
                        endpoint = fetched.Endpoint,
                        model = "none",
                    }),
                    Output = JsonSerializer.Serialize(new
                    {
                        status = "completed",
                        created = created.Count,
                        skipped,
                        rejected,
                        source = source.Code,
                        endpoint = fetched.Endpoint,
                    }),
                    Policy = """{"autonomy":1,"outbound":"none","model":"none","roles":["discovery"]}""",
                },
                tx,
                cancellationToken: cancellationToken));

        foreach (var prospect in created)
        {
            var decision = ProspectDiscoveryPlanner.Decide(prospect, new DiscoveryMemory(new HashSet<string>(), new HashSet<string>()));
            var businessId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.business (
                        tenant_id, name, business_type, address, province, phone, website, source, confidence_score)
                    VALUES (
                        @TenantId, @Name, 'pharmacy', @Address, @Province, @Phone, @Website, 'osm_overpass', 70)
                    RETURNING id
                    """,
                    new
                    {
                        TenantId = tenantId,
                        Name = prospect.Name!.Trim(),
                        Address = Trim(prospect.Address),
                        Province = Trim(prospect.Province),
                        Phone = Short(prospect.Phone, 32),
                        Website = Trim(prospect.Website),
                    },
                    tx,
                    cancellationToken: cancellationToken));
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.business_identity (
                        tenant_id, business_id, identity_type, platform, url, username, source, confidence_score, is_public)
                    VALUES (
                        @TenantId, @BusinessId, 'map', 'osm', @Url, @Username, 'osm_overpass', 80, TRUE)
                    """,
                    new
                    {
                        TenantId = tenantId,
                        BusinessId = businessId,
                        Url = prospect.SourceUrl,
                        Username = prospect.ExternalId,
                    },
                    tx,
                    cancellationToken: cancellationToken));
            var leadId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.lead (
                        tenant_id, business_id, product_code, source, lead_status)
                    VALUES (
                        @TenantId, @BusinessId, 'novixa', 'osm_overpass', 'discovered')
                    RETURNING id
                    """,
                    new { TenantId = tenantId, BusinessId = businessId },
                    tx,
                    cancellationToken: cancellationToken));
            foreach (var statement in decision.Statements)
            {
                await conn.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO pack_sales.signal (
                            tenant_id, business_id, lead_id, signal_type, description, evidence, source, confidence, kind)
                        VALUES (
                            @TenantId, @BusinessId, @LeadId, 'prospect', @Description, @Evidence, 'osm_overpass', @Confidence, @Kind)
                        """,
                        new
                        {
                            TenantId = tenantId,
                            BusinessId = businessId,
                            LeadId = leadId,
                            Description = statement.Text,
                            Evidence = statement.Kind == "evidence" ? statement.Text : "© OpenStreetMap contributors",
                            Confidence = statement.Kind == "unknown" ? 0 : 80,
                            Kind = statement.Kind,
                        },
                        tx,
                        cancellationToken: cancellationToken));
            }

            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, tenantId, leadId, "prospect_discovered", null, "discovered",
                JsonSerializer.Serialize(new
                {
                    source = source.Code,
                    externalId = prospect.ExternalId,
                    latitude = prospect.Latitude,
                    longitude = prospect.Longitude,
                    priority = decision.Priority,
                }),
                null, runId, "prospect:" + prospect.ExternalId, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return new KitSalesDiscoveryRunDto(source.Code, "completed", created.Count, skipped, rejected, false);
    }

    public async Task<KitSalesDiscoverySummaryDto> SummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var row = await conn.QuerySingleAsync<SummaryRow>(
            new CommandDefinition(
                """
                SELECT
                    (SELECT COUNT(*)::int FROM pack_sales.lead
                     WHERE tenant_id = @TenantId AND source = 'osm_overpass') AS Prospects,
                    (SELECT COUNT(*)::int FROM pack_sales.signal
                     WHERE tenant_id = @TenantId AND source = 'osm_overpass' AND kind = 'fact') AS FactSignals,
                    (SELECT COUNT(*)::int FROM pack_sales.signal
                     WHERE tenant_id = @TenantId AND source = 'osm_overpass' AND kind = 'unknown') AS UnknownNeeds,
                    (SELECT created_at FROM pack_sales.agent_run
                     WHERE tenant_id = @TenantId AND agent_code = 'discovery'
                     ORDER BY created_at DESC LIMIT 1) AS LastRunAt,
                    (SELECT output_json->>'status' FROM pack_sales.agent_run
                     WHERE tenant_id = @TenantId AND agent_code = 'discovery'
                     ORDER BY created_at DESC LIMIT 1) AS LastStatus
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        return new KitSalesDiscoverySummaryDto(
            row.Prospects,
            row.FactSignals,
            row.UnknownNeeds,
            row.LastRunAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(row.LastRunAt.Value, DateTimeKind.Utc)),
            row.LastStatus,
            0);
    }

    private async Task<(string? Body, string? Endpoint, string Reason)> FetchAsync(int maxResults, CancellationToken cancellationToken)
    {
        var client = _http.CreateClient("kit-sales-osm");
        var reasons = new List<string>();
        foreach (var endpoint in NovixaSourceRegistry.OverpassInterpreters)
        {
            var host = new Uri(endpoint).Host;
            try
            {
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limit.CancelAfter(TimeSpan.FromSeconds(12));
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = Query });
                using var response = await client.PostAsync(endpoint, content, limit.Token);
                var body = await response.Content.ReadAsStringAsync(limit.Token);
                if (!response.IsSuccessStatusCode)
                {
                    reasons.Add(host + ":http_" + (int)response.StatusCode);
                    continue;
                }

                if (!OsmOverpassParser.TryParse(body, maxResults, out var parsed, out var error))
                {
                    reasons.Add(host + ":" + (error ?? "parse"));
                    continue;
                }

                if (parsed.Count == 0)
                {
                    reasons.Add(host + ":empty");
                    continue;
                }

                return (body, endpoint, "");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
            {
                reasons.Add(host + ":" + Classify(ex));
            }
        }

        return (null, null, reasons.Count == 0 ? "no_endpoint" : string.Join(",", reasons));
    }

    private static async Task RecordFailureAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        string reason,
        CancellationToken cancellationToken)
    {
        await conn.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.agent_run (
                    tenant_id, agent_code, status, input_json, output_json, policy_json)
                VALUES (
                    @TenantId, 'discovery', 'failed',
                    @Input::jsonb, @Output::jsonb, @Policy::jsonb)
                """,
                new
                {
                    TenantId = tenantId,
                    Input = JsonSerializer.Serialize(new { source = "osm_overpass", bbox = Bbox, maxResults = 15, model = "none" }),
                    Output = JsonSerializer.Serialize(new { status = "source_failed", reason }),
                    Policy = """{"autonomy":1,"outbound":"none","model":"none","roles":["discovery"]}""",
                },
                cancellationToken: cancellationToken));
    }

    private static string Classify(Exception exception)
    {
        if (exception is HttpRequestException http)
        {
            return http.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "dns",
                HttpRequestError.SecureConnectionError => "tls",
                HttpRequestError.ConnectionError => "timeout",
                _ => "http",
            };
        }

        return "timeout";
    }

    private static DateTimeOffset? AsUtc(DateTime? value) =>
        value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Short(string? value, int max)
    {
        var trimmed = Trim(value);
        if (trimmed is null || trimmed.Length <= max)
            return trimmed;
        return null;
    }

    private sealed class GateRow
    {
        public DateTime? LastSuccess { get; init; }
        public DateTime? LastFailure { get; init; }
    }

    private sealed class SummaryRow
    {
        public int Prospects { get; init; }
        public int FactSignals { get; init; }
        public int UnknownNeeds { get; init; }
        public DateTime? LastRunAt { get; init; }
        public string? LastStatus { get; init; }
    }
}
