using System.Globalization;
using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Sales;

namespace KitPlatform.Packs.Sales.Infrastructure;

internal sealed class KitSalesProspectDiscovery : IKitSalesProspectDiscovery
{
    private readonly IDbConnectionFactory _db;
    private readonly IHttpClientFactory _http;
    private readonly KitSalesTelegramNotice _telegram;

    public KitSalesProspectDiscovery(IDbConnectionFactory db, IHttpClientFactory http, KitSalesTelegramNotice telegram)
    {
        _db = db;
        _http = http;
        _telegram = telegram;
    }

    public async Task<KitSalesDiscoveryRunDto> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        var osm = await DiscoverOsmAsync(cancellationToken);
        var directory = await DiscoverDirectoryAsync(cancellationToken);
        await _telegram.NotifyUnsentAsync(cancellationToken);
        return directory.Created > 0 ? directory : osm;
    }

    private async Task<KitSalesDiscoveryRunDto> DiscoverOsmAsync(CancellationToken cancellationToken = default)
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

        var cursor = await conn.QuerySingleOrDefaultAsync<TileCursor>(
            new CommandDefinition(
                """
                SELECT (input_json->>'tile')::int AS Tile,
                       COALESCE((output_json->>'created')::int, 0) AS Created,
                       status AS Status
                FROM pack_sales.agent_run
                WHERE tenant_id = @TenantId AND agent_code = 'discovery'
                  AND input_json ? 'tile'
                ORDER BY created_at DESC
                LIMIT 1
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        var tiles = VietnamPharmacyTiles.All;
        var tileIndex = VietnamPharmacyTiles.NextIndex(
            cursor?.Tile,
            cursor?.Created ?? 0,
            tiles.Count,
            string.Equals(cursor?.Status, "failed", StringComparison.Ordinal));
        var tile = tiles[tileIndex];
        var bbox = string.Create(CultureInfo.InvariantCulture, $"{tile.South},{tile.West},{tile.North},{tile.East}");
        var fetched = await FetchAsync(TileQuery(tile), 60, cancellationToken);
        var prospects = new List<PublicProspect>();
        if (fetched.Body is not null)
        {
            if (!OsmOverpassParser.TryParse(fetched.Body, 60, out var parsed, out _))
            {
                await RecordFailureAsync(conn, tenantId, tileIndex, bbox, fetched.Reason, cancellationToken);
                return new KitSalesDiscoveryRunDto(source.Code, "source_failed", 0, 0, 0, false);
            }

            prospects.AddRange(parsed);
        }
        else if (!fetched.Empty)
        {
            await RecordFailureAsync(conn, tenantId, tileIndex, bbox, fetched.Reason, cancellationToken);
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
            if (created.Count >= source.MaxResults)
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
                        bbox,
                        tile = tileIndex,
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

    private async Task<KitSalesDiscoveryRunDto> DiscoverDirectoryAsync(CancellationToken cancellationToken)
    {
        var path = DirectoryFile();
        if (path is null)
            return new KitSalesDiscoveryRunDto("syt_4252", "source_missing", 0, 0, 0, false);

        var tenantId = SalesPackDefinition.DedicatedTenantId;
        await using var conn = await _db.CreateOpenConnectionAsync(cancellationToken);
        var gateRow = await conn.QuerySingleAsync<GateRow>(
            new CommandDefinition(
                """
                SELECT
                    MAX(created_at) FILTER (WHERE status = 'completed') AS LastSuccess,
                    MAX(created_at) FILTER (WHERE status = 'failed') AS LastFailure
                FROM pack_sales.agent_run
                WHERE tenant_id = @TenantId AND agent_code = 'directory'
                """,
                new { TenantId = tenantId },
                cancellationToken: cancellationToken));
        var gate = DiscoverySchedule.Gate(AsUtc(gateRow.LastSuccess), AsUtc(gateRow.LastFailure), DateTimeOffset.UtcNow, TimeSpan.FromHours(24));
        if (gate is "rate_limited" or "backoff")
            return new KitSalesDiscoveryRunDto("syt_4252", gate, 0, 0, 0, false);

        List<DirectoryRow> rows;
        try
        {
            rows = JsonSerializer.Deserialize<List<DirectoryRow>>(await File.ReadAllTextAsync(path, cancellationToken)) ?? [];
        }
        catch (JsonException)
        {
            await RecordDirectoryFailureAsync(conn, tenantId, "directory_json", cancellationToken);
            return new KitSalesDiscoveryRunDto("syt_4252", "source_failed", 0, 0, 0, false);
        }

        var outlets = rows
            .Where(row => PublicDirectory.IsSellableOutlet(row.FacilityType)
                && !string.IsNullOrWhiteSpace(row.CandidateId)
                && !string.IsNullOrWhiteSpace(row.Name))
            .Take(SalesAccountImport.MaxRows)
            .ToList();
        var ids = (await conn.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT username FROM pack_sales.business_identity
                WHERE tenant_id = @TenantId AND platform = 'syt' AND username IS NOT NULL
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

        var created = new List<DirectoryRow>();
        var skipped = 0;
        foreach (var row in outlets)
        {
            var folded = ProspectNames.FoldBusiness(row.Name);
            if (ids.Contains(row.CandidateId!) || names.Contains(folded))
            {
                skipped++;
                continue;
            }

            created.Add(row);
            ids.Add(row.CandidateId!);
            if (folded.Length > 0)
                names.Add(folded);
        }

        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        var runId = await conn.ExecuteScalarAsync<Guid>(
            new CommandDefinition(
                """
                INSERT INTO pack_sales.agent_run (
                    tenant_id, agent_code, status, input_json, output_json, policy_json)
                VALUES (
                    @TenantId, 'directory', 'completed',
                    @Input::jsonb, @Output::jsonb, @Policy::jsonb)
                RETURNING id
                """,
                new
                {
                    TenantId = tenantId,
                    Input = """{"source":"syt_4252","file":"classified.json","model":"none"}""",
                    Output = JsonSerializer.Serialize(new
                    {
                        status = "completed",
                        created = created.Count,
                        skipped,
                        rejected = rows.Count - outlets.Count,
                        source = "syt_4252",
                    }),
                    Policy = """{"autonomy":1,"outbound":"none","model":"none","roles":["directory"]}""",
                },
                tx,
                cancellationToken: cancellationToken));

        foreach (var row in created)
        {
            var businessId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.business (
                        tenant_id, name, business_type, address, province, source, confidence_score)
                    VALUES (
                        @TenantId, @Name, 'pharmacy', @Address, @Province, 'syt_4252', 80)
                    RETURNING id
                    """,
                    new
                    {
                        TenantId = tenantId,
                        Name = row.Name!.Trim(),
                        Address = Trim(row.Address),
                        Province = Province(row),
                    },
                    tx,
                    cancellationToken: cancellationToken));
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.business_identity (
                        tenant_id, business_id, identity_type, platform, url, username, source, confidence_score, is_public)
                    VALUES (
                        @TenantId, @BusinessId, 'directory', 'syt', @Url, @Username, 'syt_4252', 80, TRUE)
                    """,
                    new
                    {
                        TenantId = tenantId,
                        BusinessId = businessId,
                        Url = Trim(row.OriginUrl),
                        Username = row.CandidateId,
                    },
                    tx,
                    cancellationToken: cancellationToken));
            var leadId = await conn.ExecuteScalarAsync<Guid>(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.lead (
                        tenant_id, business_id, product_code, source, lead_status)
                    VALUES (
                        @TenantId, @BusinessId, 'novixa', 'syt_4252', 'discovered')
                    RETURNING id
                    """,
                    new { TenantId = tenantId, BusinessId = businessId },
                    tx,
                    cancellationToken: cancellationToken));
            await conn.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO pack_sales.signal (
                        tenant_id, business_id, lead_id, signal_type, description, evidence, source, confidence, kind)
                    VALUES (
                        @TenantId, @BusinessId, @LeadId, 'prospect', @Description, @Evidence, 'syt_4252', 80, 'fact')
                    """,
                    new
                    {
                        TenantId = tenantId,
                        BusinessId = businessId,
                        LeadId = leadId,
                        Description = "Tên trong thông báo công khai 4252/TB-SYT: " + row.Name!.Trim(),
                        Evidence = Trim(row.OriginUrl) ?? "4252/TB-SYT",
                    },
                    tx,
                    cancellationToken: cancellationToken));
            await KitSalesLeadEventWriter.WriteAsync(
                conn, tx, tenantId, leadId, "prospect_discovered", null, "discovered",
                JsonSerializer.Serialize(new { source = "syt_4252", externalId = row.CandidateId }),
                null, runId, "directory:" + row.CandidateId, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
        return new KitSalesDiscoveryRunDto("syt_4252", "completed", created.Count, skipped, rows.Count - outlets.Count, false);
    }

    private static string? DirectoryFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var path = Path.Combine(dir.FullName, ".tmp", "kit-sales-syt-4252", "classified.json");
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    private static string? Province(DirectoryRow row)
    {
        var area = ProspectNames.Fold(row.AdminArea + " " + row.Address);
        if (area.Contains("thai nguyen", StringComparison.Ordinal))
            return "Thái Nguyên";
        return Short(row.AdminArea, 64);
    }

    private static async Task RecordDirectoryFailureAsync(
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
                    @TenantId, 'directory', 'failed',
                    '{"source":"syt_4252","model":"none"}'::jsonb,
                    @Output::jsonb,
                    '{"autonomy":1,"outbound":"none","model":"none","roles":["directory"]}'::jsonb)
                """,
                new { TenantId = tenantId, Output = JsonSerializer.Serialize(new { status = "source_failed", reason }) },
                cancellationToken: cancellationToken));
    }

    private static string TileQuery(MapTile tile)
    {
        var culture = CultureInfo.InvariantCulture;
        var box = tile.South.ToString(culture) + ","
            + tile.West.ToString(culture) + ","
            + tile.North.ToString(culture) + ","
            + tile.East.ToString(culture);
        return "[out:json][timeout:20];\n"
            + "node[\"amenity\"=\"pharmacy\"][\"name\"](" + box + ");\n"
            + "out body 60;\n";
    }

    private async Task<OsmFetch> FetchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        var client = _http.CreateClient("kit-sales-osm");
        var reasons = new List<string>();
        string? emptyBody = null;
        string? emptyEndpoint = null;
        foreach (var endpoint in NovixaSourceRegistry.OverpassInterpreters)
        {
            var host = new Uri(endpoint).Host;
            try
            {
                using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                limit.CancelAfter(TimeSpan.FromSeconds(22));
                using var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["data"] = query });
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
                    emptyBody ??= body;
                    emptyEndpoint ??= endpoint;
                    reasons.Add(host + ":empty");
                    continue;
                }

                return new OsmFetch(body, endpoint, "", false);
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

        if (emptyBody is not null)
            return new OsmFetch(emptyBody, emptyEndpoint, "", false);

        var reason = reasons.Count == 0 ? "no_endpoint" : string.Join(",", reasons);
        return new OsmFetch(null, null, reason, false);
    }

    private static async Task RecordFailureAsync(
        System.Data.Common.DbConnection conn,
        Guid tenantId,
        int tile,
        string bbox,
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
                    Input = JsonSerializer.Serialize(new
                    {
                        source = "osm_overpass",
                        bbox,
                        tile,
                        maxResults = 15,
                        model = "none",
                    }),
                    Output = JsonSerializer.Serialize(new { status = "source_failed", reason, created = 0 }),
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

    private sealed record OsmFetch(string? Body, string? Endpoint, string Reason, bool Empty);

    private sealed class TileCursor
    {
        public int Tile { get; init; }
        public int Created { get; init; }
        public string Status { get; init; } = "";
    }

    private sealed class DirectoryRow
    {
        [System.Text.Json.Serialization.JsonPropertyName("candidate_id")]
        public string? CandidateId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("name")]
        public string? Name { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("facility_type")]
        public string? FacilityType { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("address")]
        public string? Address { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("current_admin_area")]
        public string? AdminArea { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("origin_url")]
        public string? OriginUrl { get; set; }
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
