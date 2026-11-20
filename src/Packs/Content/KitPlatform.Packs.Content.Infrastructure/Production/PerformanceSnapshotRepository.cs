using System.Text.Json;
using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class PerformanceSnapshotRepository : IPerformanceSnapshotStore
{
    readonly IDbConnectionFactory _db;
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PerformanceSnapshotRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertAsync(PerformanceSnapshot snapshot, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO pack_content.performance_snapshot (
                performance_snapshot_id, build_id, shot_code, episode_code, scene_code,
                performance_hash, payload_json, source_json, created_by, frozen_at)
            VALUES (
                @PerformanceSnapshotId, @BuildId, @ShotCode, @EpisodeCode, @SceneCode,
                @PerformanceHash, @PayloadJson, @SourceJson, @CreatedBy, @FrozenAt)
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            snapshot.PerformanceSnapshotId,
            snapshot.Draft.BuildId,
            snapshot.Draft.ShotCode,
            snapshot.Draft.EpisodeCode,
            snapshot.Draft.SceneCode,
            snapshot.PerformanceHash,
            snapshot.PayloadJson,
            SourceJson = JsonSerializer.Serialize(snapshot.Source, JsonOpts),
            snapshot.CreatedBy,
            snapshot.FrozenAt,
        }, cancellationToken: ct));
    }

    public async Task<PerformanceSnapshot?> GetAsync(Guid performanceSnapshotId, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT performance_snapshot_id AS PerformanceSnapshotId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, performance_hash AS PerformanceHash,
                   payload_json AS PayloadJson, source_json AS SourceJson, created_by AS CreatedBy, frozen_at AS FrozenAt
            FROM pack_content.performance_snapshot
            WHERE performance_snapshot_id = @performanceSnapshotId
            """;
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, new { performanceSnapshotId }, cancellationToken: ct));
        if (row is null) return null;
        var source = string.IsNullOrWhiteSpace(row.SourceJson)
            ? new PerformanceSourceRef(false, [], false, null)
            : JsonSerializer.Deserialize<PerformanceSourceRef>(row.SourceJson, JsonOpts)
              ?? new PerformanceSourceRef(false, [], false, null);
        return new PerformanceSnapshot(
            row.PerformanceSnapshotId,
            row.PerformanceHash,
            row.FrozenAt,
            row.CreatedBy,
            source,
            ReconstructDraft(row),
            row.PayloadJson);
    }

    public async Task<PerformanceSnapshot?> GetByScopeAsync(Guid buildId, string shotCode, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT performance_snapshot_id AS PerformanceSnapshotId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, performance_hash AS PerformanceHash,
                   payload_json AS PayloadJson, source_json AS SourceJson, created_by AS CreatedBy, frozen_at AS FrozenAt
            FROM pack_content.performance_snapshot
            WHERE build_id = @buildId AND shot_code = @shotCode
            ORDER BY frozen_at DESC
            LIMIT 1
            """;
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, new { buildId, shotCode }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<PerformanceSnapshot?> GetByHashAsync(Guid buildId, string shotCode, string performanceHash, CancellationToken ct = default)
    {
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        const string sql = """
            SELECT performance_snapshot_id AS PerformanceSnapshotId, build_id AS BuildId, shot_code AS ShotCode,
                   episode_code AS EpisodeCode, scene_code AS SceneCode, performance_hash AS PerformanceHash,
                   payload_json AS PayloadJson, source_json AS SourceJson, created_by AS CreatedBy, frozen_at AS FrozenAt
            FROM pack_content.performance_snapshot
            WHERE build_id = @buildId AND shot_code = @shotCode AND performance_hash = @performanceHash
            ORDER BY frozen_at ASC
            LIMIT 1
            """;
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(sql, new { buildId, shotCode, performanceHash }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    static PerformanceSnapshot Map(Row row)
    {
        var source = string.IsNullOrWhiteSpace(row.SourceJson)
            ? new PerformanceSourceRef(false, [], false, null)
            : JsonSerializer.Deserialize<PerformanceSourceRef>(row.SourceJson, JsonOpts)
              ?? new PerformanceSourceRef(false, [], false, null);
        return new PerformanceSnapshot(
            row.PerformanceSnapshotId,
            row.PerformanceHash,
            row.FrozenAt,
            row.CreatedBy,
            source,
            ReconstructDraft(row),
            row.PayloadJson);
    }

    static PerformanceDraft ReconstructDraft(Row row)
    {
        using var doc = JsonDocument.Parse(row.PayloadJson);
        var root = doc.RootElement;
        return new PerformanceDraft(
            Str(root, "documentId") ?? PerformanceContractIds.DocumentId,
            root.TryGetProperty("schemaVersion", out var ver) ? ver.GetInt32() : PerformanceContractIds.SchemaVersion,
            row.BuildId,
            row.ShotCode,
            row.EpisodeCode,
            row.SceneCode,
            ReadSlice(root),
            ReadCharacters(root),
            ReadLines(root),
            new PerformanceTimingRef(
                Str(root.GetProperty("timingRef"), "timingDocumentId") ?? PerformanceContractIds.TimingDocumentId,
                Opt(root.GetProperty("timingRef"), "timingHash")),
            new PerformanceVisualRef(
                ReadStringArray(root.GetProperty("visualRef"), "characterIdentityIds"),
                Opt(root.GetProperty("visualRef"), "visualAuthorityId"),
                Opt(root.GetProperty("visualRef"), "visualContractHash")),
            new PerformanceStoryRef(
                ReadStringArray(root.GetProperty("storyRef"), "dialogueSegmentIds"),
                Opt(root.GetProperty("storyRef"), "storyTextHash"),
                Opt(root.GetProperty("storyRef"), "beatId")));
    }

    static ShotPerformanceSlice ReadSlice(JsonElement root)
    {
        if (!root.TryGetProperty("shotPerformance", out var shot)) return new ShotPerformanceSlice();
        ShotPerformanceBefore? before = null;
        ShotPerformanceDuring? during = null;
        ShotPerformanceAfter? after = null;
        if (shot.TryGetProperty("before", out var b))
            before = new ShotPerformanceBefore(Opt(b, "action"), Opt(b, "body"), Opt(b, "prop"), Opt(b, "gaze"), Opt(b, "room"));
        if (shot.TryGetProperty("during", out var d))
            during = new ShotPerformanceDuring(Opt(d, "emotion"), d.TryGetProperty("intensity", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : null);
        if (shot.TryGetProperty("after", out var a))
            after = new ShotPerformanceAfter(Opt(a, "action"), Opt(a, "gaze"));
        return new ShotPerformanceSlice(before, during, after, Opt(shot, "gazeTarget"));
    }

    static IReadOnlyList<CharacterInShot> ReadCharacters(JsonElement root)
    {
        if (!root.TryGetProperty("characters", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(c => new CharacterInShot(
            Str(c, "characterId") ?? "",
            Str(c, "participation") ?? "",
            Str(c, "visualRole") ?? "",
            Str(c, "speechRole") ?? "",
            Opt(c, "emotionalState"),
            c.TryGetProperty("intensity", out var n) && n.ValueKind == JsonValueKind.Number ? n.GetInt32() : null,
            ReadStringArray(c, "lineIds"))).ToArray();
    }

    static IReadOnlyList<PerformanceLine> ReadLines(JsonElement root)
    {
        if (!root.TryGetProperty("lines", out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(l =>
        {
            var text = l.GetProperty("textIdentity");
            var del = l.GetProperty("delivery");
            return new PerformanceLine(
                Str(l, "lineId") ?? "",
                Str(l, "characterId") ?? "",
                new PerformanceTextIdentity(Str(text, "text") ?? "", Str(text, "textHash") ?? ""),
                new PerformanceDelivery(
                    Str(del, "emotion") ?? "",
                    del.TryGetProperty("intensity", out var n) ? n.GetInt32() : 2,
                    Str(del, "pace") ?? "natural",
                    Str(del, "volume") ?? "mid",
                    del.TryGetProperty("pauseSec", out var p) ? p.GetDouble() : 0.15,
                    Str(del, "label") ?? "",
                    Opt(del, "emphasis"),
                    del.TryGetProperty("locked", out var locked) && locked.GetBoolean()));
        }).ToArray();
    }

    static IReadOnlyList<string> ReadStringArray(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
    }

    static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    static string? Opt(JsonElement obj, string name)
    {
        var s = Str(obj, name);
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    sealed class Row
    {
        public Guid PerformanceSnapshotId { get; set; }
        public Guid BuildId { get; set; }
        public string ShotCode { get; set; } = "";
        public string EpisodeCode { get; set; } = "";
        public string SceneCode { get; set; } = "";
        public string PerformanceHash { get; set; } = "";
        public string PayloadJson { get; set; } = "";
        public string SourceJson { get; set; } = "{}";
        public string CreatedBy { get; set; } = "";
        public DateTimeOffset FrozenAt { get; set; }
    }
}
