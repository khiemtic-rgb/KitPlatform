using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.PersonalIntelligence.Research;

namespace KitPlatform.Packs.PersonalIntelligence.Infrastructure.Research;

public sealed class FileResearchPublicationReader : IResearchPublicationReader
{
    static readonly (string Id, string Kind, string Relative, string HashField)[] Objects =
    [
        ("lab-result", "LAB_RESULT", "lab/LAB_RESULT.json", "labResultSha256"),
        ("paper-status", "PAPER_OPS_STATUS", "paper/PAPER_OPS_STATUS.json", "paperStatusSha256"),
        ("paper-report", "PAPER_RESEARCH_REPORT", "paper/PAPER_RESEARCH_REPORT.json", "paperReportSha256"),
        ("paper-health", "PAPER_OPS_HEALTH", "paper/PAPER_OPS_HEALTH.json", "paperHealthSha256"),
    ];

    readonly ResearchPublicationOptions _options;

    public FileResearchPublicationReader(IOptions<ResearchPublicationOptions> options) =>
        _options = options.Value;

    public Task<ResearchPublicationRead> ReadAsync(CancellationToken cancellationToken = default) =>
        ReadAsync(publicationId: null, cancellationToken);

    public Task<ResearchPublicationRead> ReadAsync(string? publicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Read(publicationId));

    public Task<ResearchPublicationIndex> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(List());

    ResearchPublicationRead Read(string? publicationId)
    {
        var requested = string.IsNullOrWhiteSpace(publicationId) ? null : publicationId.Trim();
        if (requested is not null && !ResearchPublicationIds.IsPublicationId(requested))
            return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication id is invalid.");

        var root = (_options.Root ?? "").Trim();
        if (root.Length == 0 || !Directory.Exists(root))
            return Fail(ResearchAvailability.Missing, "MISSING", "Publication is missing.");

        var latestPath = Path.Combine(root, "latest.json");
        if (!File.Exists(latestPath))
        {
            if (requested is null)
                return ReadDirectory(root);
            var direct = ReadDirectory(root);
            if (string.Equals(direct.PublicationId, requested, StringComparison.Ordinal))
                return direct;
            return ReadHistory(requested) ?? Fail(ResearchAvailability.Missing, "MISSING", "Publication is missing.");
        }

        if (!TryPointer(latestPath, out var latestId))
            return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication pointer is invalid.");

        var selected = requested ?? latestId;
        var fromCatalog = ReadCatalog(root, selected);
        if (fromCatalog is not null)
            return fromCatalog;
        if (requested is not null)
            return ReadHistory(requested) ?? Fail(ResearchAvailability.Missing, "MISSING", "Publication is missing.", publicationId: requested);
        return Fail(ResearchAvailability.Missing, "MISSING", "Publication is missing.", publicationId: latestId);
    }

    ResearchPublicationRead ReadDirectory(string root)
    {
        var manifestPath = FixedPath(root, "manifest.json");
        if (manifestPath is null || !File.Exists(manifestPath))
            return Fail(ResearchAvailability.Missing, "MISSING", "Publication manifest is missing.");

        JsonDocument manifestDoc;
        try
        {
            manifestDoc = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication manifest could not be parsed.");
        }

        using (manifestDoc)
        {
            if (manifestDoc.RootElement.ValueKind != JsonValueKind.Object)
                return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication manifest is invalid.");

            var manifest = manifestDoc.RootElement;
            if (!manifest.TryGetProperty("schemaVersion", out var schemaNode) || schemaNode.ValueKind != JsonValueKind.String)
                return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication manifest is invalid.");
            var schema = schemaNode.GetString();
            if (!string.Equals(schema, ResearchPublicationSchema.V1, StringComparison.Ordinal))
                return Fail(ResearchAvailability.UnsupportedVersion, "UNSUPPORTED_VERSION", "Publication schema is not supported.", schema);

            if (!TryRequiredString(manifest, "publicationId", out var publicationId)
                || !TryRequiredString(manifest, "publisher", out var publisher)
                || !TryPublishedAt(manifest, out var publishedAt))
                return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication manifest is invalid.", schema);

            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var obj in Objects)
            {
                if (!manifest.TryGetProperty(obj.HashField, out var hashNode)
                    || hashNode.ValueKind != JsonValueKind.String
                    || !IsSha256(hashNode.GetString()))
                    return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication manifest is invalid.", schema, publicationId, publishedAt, publisher);
                hashes[obj.HashField] = hashNode.GetString()!;
            }

            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var obj in Objects)
            {
                var path = FixedPath(root, obj.Relative);
                if (path is null || !File.Exists(path))
                    return Fail(ResearchAvailability.Missing, "MISSING", "A required publication object is missing.", schema, publicationId, publishedAt, publisher);
                try
                {
                    files[obj.Id] = File.ReadAllBytes(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "A publication object could not be read.", schema, publicationId, publishedAt, publisher);
                }
            }

            foreach (var obj in Objects)
            {
                var actual = Convert.ToHexString(SHA256.HashData(files[obj.Id])).ToLowerInvariant();
                if (!string.Equals(actual, hashes[obj.HashField], StringComparison.OrdinalIgnoreCase))
                    return Fail(ResearchAvailability.Inconsistent, "INCONSISTENT", "A publication hash does not match.", schema, publicationId, publishedAt, publisher);
            }

            JsonDocument lab;
            JsonDocument status;
            JsonDocument report;
            JsonDocument health;
            try
            {
                lab = JsonDocument.Parse(files["lab-result"]);
                status = JsonDocument.Parse(files["paper-status"]);
                report = JsonDocument.Parse(files["paper-report"]);
                health = JsonDocument.Parse(files["paper-health"]);
            }
            catch (JsonException)
            {
                return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "A publication object could not be parsed.", schema, publicationId, publishedAt, publisher);
            }

            using (lab)
            using (status)
            using (report)
            using (health)
            {
                if (!health.RootElement.TryGetProperty("health", out var healthNode) || healthNode.ValueKind != JsonValueKind.String)
                    return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication health is invalid.", schema, publicationId, publishedAt, publisher);
                var healthValue = healthNode.GetString();
                var availability = healthValue switch
                {
                    "OK" => ResearchAvailability.Fresh,
                    "STALE" => ResearchAvailability.Stale,
                    _ => null,
                };
                if (availability is null)
                    return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication health is invalid.", schema, publicationId, publishedAt, publisher);

                var experiments = MapExperiments(lab.RootElement);
                if (experiments is null)
                    return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "Publication lab result is invalid.", schema, publicationId, publishedAt, publisher);
                var paperStatus = MapStatus(status.RootElement, health.RootElement, availability);
                var paperReport = MapReport(report.RootElement, availability);
                if (paperStatus is null || paperReport is null)
                    return Fail(ResearchAvailability.Unreadable, "UNREADABLE", "A publication object is invalid.", schema, publicationId, publishedAt, publisher);

                return new ResearchPublicationRead(
                    availability,
                    schema,
                    publicationId,
                    publishedAt,
                    publisher,
                    Objects.Select(obj => new ResearchObjectRead(
                        obj.Id,
                        obj.Kind,
                        ResearchPublicationSchema.Publisher,
                        obj.Id == "lab-result" ? ResearchAvailability.PresentUntimed : availability,
                        hashes[obj.HashField].ToLowerInvariant(),
                        null)).ToArray(),
                    null,
                    experiments,
                    paperStatus,
                    paperReport);
            }
        }
    }

    static IReadOnlyList<ResearchExperimentRead>? MapExperiments(JsonElement lab)
    {
        if (!lab.TryGetProperty("experiments", out var list) || list.ValueKind != JsonValueKind.Array)
            return null;
        var rows = new List<ResearchExperimentRead>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                return null;
            var id = Text(item, "experiment_id");
            if (!ResearchExperimentIds.IsCatalogId(id))
                return null;
            rows.Add(new ResearchExperimentRead(
                id!,
                Text(item, "strategy_id"),
                Text(item, "strategy_version"),
                Text(item, "family"),
                Text(item, "symbol"),
                Text(item, "timeframe"),
                Text(item, "data_source"),
                Text(item, "snapshot_sha256"),
                Text(item, "approval_status"),
                Text(item, "evidence_status"),
                Text(item, "performance_claim"),
                Text(item, "risk_limits_approval"),
                Bool(item, "selected_by_pnl"),
                Bool(item, "parameters_chosen_on_out_of_sample"),
                Clone(item, "development"),
                Clone(item, "out_of_sample"),
                Bool(item, "live_orders"),
                Int(item, "exchange_orders_sent")));
        }

        return rows;
    }

    static ResearchPaperStatusRead? MapStatus(JsonElement status, JsonElement health, string availability)
    {
        if (status.ValueKind != JsonValueKind.Object || health.ValueKind != JsonValueKind.Object)
            return null;
        return new ResearchPaperStatusRead(
            availability,
            Text(status, "runner_status"),
            Text(status, "cycle_outcome"),
            SafeToken(Text(status, "last_run_at")),
            Long(status, "last_processed_open_time"),
            SafeToken(Text(status, "last_error")),
            Text(health, "health"),
            Long(health, "last_bar_age_ms"),
            Bool(status, "live_orders"),
            Int(status, "exchange_orders_sent"),
            SafeToken(Text(status, "effective_live_mode")),
            SafeToken(Text(status, "risk_limits_approval")),
            Int(status, "extension_count"),
            SafeToken(Text(status, "extension_sha256")));
    }

    static ResearchPaperReportRead? MapReport(JsonElement report, string availability)
    {
        if (report.ValueKind != JsonValueKind.Object)
            return null;
        if (!report.TryGetProperty("strategies", out var list) || list.ValueKind != JsonValueKind.Array)
            return null;
        var strategies = new List<ResearchPaperStrategyRead>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                return null;
            strategies.Add(new ResearchPaperStrategyRead(
                SafeToken(Text(item, "experiment_id")),
                Int(item, "extension_bars_represented"),
                Int(item, "no_signal_cycles"),
                Int(item, "simulated_entries"),
                Int(item, "closed_trades"),
                Int(item, "open_trades"),
                Int(item, "risk_blocks"),
                Bool(item, "no_trade_is_not_a_win_or_loss"),
                SafeToken(Text(item, "net_pnl")),
                SafeToken(Text(item, "profit_factor")),
                SafeToken(Text(item, "max_drawdown_fraction")),
                SafeToken(Text(item, "expectancy")),
                SafeToken(Text(item, "fees")),
                SafeToken(Text(item, "funding")),
                SafeToken(Text(item, "slippage")),
                SafeToken(Text(item, "stand_aside_net_pnl")),
                SafeToken(Text(item, "in_sample_out_of_sample")),
                SafeToken(Text(item, "sample_warning")),
                SafeToken(Text(item, "fill_model")),
                SafeToken(Text(item, "signal_time")),
                SafeToken(Text(item, "assumed_execution_time")),
                Bool(item, "exchange_fill")));
        }

        return new ResearchPaperReportRead(
            availability,
            SafeToken(Text(report, "report_kind")),
            SafeToken(Text(report, "symbol")),
            SafeToken(Text(report, "timeframe")),
            Int(report, "extension_bar_count"),
            SafeToken(Text(report, "extension_start_utc")),
            SafeToken(Text(report, "extension_end_utc")),
            strategies,
            "SEE_LAB_RESULT_ENDPOINT",
            SafeToken(Text(report, "performance_claim")),
            Bool(report, "live_orders"),
            Int(report, "exchange_orders_sent"),
            SafeToken(Text(report, "effective_live_mode")));
    }

    static ResearchPublicationRead Fail(
        string availability,
        string code,
        string message,
        string? schema = null,
        string? publicationId = null,
        DateTimeOffset? publishedAt = null,
        string? publisher = null) =>
        new(
            availability,
            schema,
            publicationId,
            publishedAt,
            publisher,
            Objects.Select(obj => new ResearchObjectRead(obj.Id, obj.Kind, ResearchPublicationSchema.Publisher, availability, null, null)).ToArray(),
            new ResearchReadError(code, message),
            [],
            null,
            null);

    ResearchPublicationRead? ReadCatalog(string root, string publicationId)
    {
        if (!TryChildDirectory(root, publicationId, out var directory))
            return null;
        var read = ReadDirectory(directory);
        if (read.PublicationId is null)
            return read;
        if (!string.Equals(read.PublicationId, publicationId, StringComparison.Ordinal))
            return Fail(
                ResearchAvailability.Inconsistent,
                "INCONSISTENT",
                "Publication id does not match its directory.",
                read.SchemaVersion,
                publicationId,
                read.PublishedAt,
                read.Publisher);
        return read;
    }

    ResearchPublicationRead? ReadHistory(string publicationId)
    {
        foreach (var directory in HistoryDirectories())
        {
            if (IsReparse(directory))
                continue;
            var read = ReadDirectory(directory);
            if (string.Equals(read.PublicationId, publicationId, StringComparison.Ordinal))
                return read;
        }

        return null;
    }

    ResearchPublicationIndex List()
    {
        var root = (_options.Root ?? "").Trim();
        string? latest = null;
        if (root.Length > 0 && Directory.Exists(root) && File.Exists(Path.Combine(root, "latest.json")) && TryPointer(Path.Combine(root, "latest.json"), out var latestId))
            latest = latestId;

        var entries = new List<ResearchPublicationIndexEntry>();
        if (root.Length > 0 && Directory.Exists(root))
        {
            foreach (var directory in Directory.EnumerateDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(directory);
                if (!ResearchPublicationIds.IsPublicationId(name) || IsReparse(directory))
                    continue;
                if (!string.Equals(ManifestId(directory), name, StringComparison.Ordinal))
                    continue;
                entries.Add(new ResearchPublicationIndexEntry(
                    name,
                    string.Equals(name, latest, StringComparison.Ordinal) ? "latest" : "retained"));
            }
        }

        foreach (var directory in HistoryDirectories())
        {
            if (IsReparse(directory))
                continue;
            var id = ManifestId(directory);
            if (!ResearchPublicationIds.IsPublicationId(id) || entries.Any(entry => entry.PublicationId == id))
                continue;
            entries.Add(new ResearchPublicationIndexEntry(id!, "archive"));
        }

        return new ResearchPublicationIndex(latest, entries);
    }

    IEnumerable<string> HistoryDirectories()
    {
        var catalog = (_options.Root ?? "").Trim();
        var catalogFull = catalog.Length > 0 && Directory.Exists(catalog) ? Path.GetFullPath(catalog) : null;
        foreach (var raw in (_options.HistoryRoots ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Directory.Exists(raw))
                continue;
            var full = Path.GetFullPath(raw);
            if (catalogFull is not null && string.Equals(full, catalogFull, StringComparison.OrdinalIgnoreCase))
                continue;
            yield return full;
        }
    }

    static bool TryPointer(string path, out string publicationId)
    {
        publicationId = "";
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;
            if (!root.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String || kind.GetString() != "POINTER_NOT_A_PUBLICATION")
                return false;
            if (root.TryGetProperty("live_orders", out var live) && live.ValueKind != JsonValueKind.False)
                return false;
            if (!root.TryGetProperty("publicationId", out var idNode) || idNode.ValueKind != JsonValueKind.String)
                return false;
            var id = idNode.GetString();
            if (!ResearchPublicationIds.IsPublicationId(id))
                return false;
            publicationId = id!;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    static bool TryChildDirectory(string root, string publicationId, out string directory)
    {
        directory = "";
        if (!ResearchPublicationIds.IsPublicationId(publicationId))
            return false;
        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(Path.Combine(rootFull, publicationId));
        var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(full) || IsReparse(full))
            return false;
        directory = full;
        return true;
    }

    static string? ManifestId(string directory)
    {
        var path = Path.Combine(directory, "manifest.json");
        if (!File.Exists(path))
            return null;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            if (!document.RootElement.TryGetProperty("publicationId", out var node) || node.ValueKind != JsonValueKind.String)
                return null;
            return node.GetString();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    static bool IsReparse(string path) =>
        new DirectoryInfo(path).Attributes.HasFlag(FileAttributes.ReparsePoint);

    static string? FixedPath(string root, string relative)
    {
        var parts = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is not ("manifest.json" or "lab" or "paper" or "LAB_RESULT.json" or "PAPER_OPS_STATUS.json" or "PAPER_RESEARCH_REPORT.json" or "PAPER_OPS_HEALTH.json")))
            return null;
        var combined = root;
        foreach (var part in parts)
            combined = Path.Combine(combined, part);
        var rootFull = Path.GetFullPath(root);
        var full = Path.GetFullPath(combined);
        var prefix = rootFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase))
            return null;
        return full;
    }

    static bool TryRequiredString(JsonElement obj, string name, out string value)
    {
        value = "";
        if (!obj.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.String)
            return false;
        value = node.GetString() ?? "";
        return value.Length > 0 && SafeToken(value) == value;
    }

    static bool TryPublishedAt(JsonElement obj, out DateTimeOffset publishedAt)
    {
        publishedAt = default;
        if (!obj.TryGetProperty("publishedAt", out var node) || node.ValueKind != JsonValueKind.String)
            return false;
        var text = node.GetString();
        return text is not null
            && SafeToken(text) == text
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out publishedAt);
    }

    static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));

    static string? Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node))
            return null;
        return node.ValueKind switch
        {
            JsonValueKind.String => node.GetString(),
            JsonValueKind.Null => null,
            _ => null,
        };
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

    static int? Int(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out var value))
            return null;
        return value;
    }

    static long? Long(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node) || node.ValueKind != JsonValueKind.Number || !node.TryGetInt64(out var value))
            return null;
        return value;
    }

    static JsonElement? Clone(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var node) || node.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        return node.Clone();
    }

    static string? SafeToken(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        if (value.Contains('\\') || value.Contains('/') || value.Contains("..", StringComparison.Ordinal) || value.Contains("api_key", StringComparison.OrdinalIgnoreCase))
            return null;
        return value;
    }
}
