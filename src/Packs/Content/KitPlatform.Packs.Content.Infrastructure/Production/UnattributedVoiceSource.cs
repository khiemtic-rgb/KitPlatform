using Dapper;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// SELECT-only bridge for a historical VOICE whose tenant_id is NULL.
/// Storage location comes from the artifact row. The caller cannot supply a path.
/// </summary>
internal sealed class UnattributedVoiceSource : IUnattributedVoiceSource
{
    readonly IDbConnectionFactory _db;
    readonly KitVideoVideoStore _videos;

    public UnattributedVoiceSource(IDbConnectionFactory db, KitVideoVideoStore videos)
    {
        _db = db;
        _videos = videos;
    }

    public async Task<UnattributedVoice?> ReadAsync(Guid artifactId, CancellationToken ct = default)
    {
        if (artifactId == Guid.Empty) return null;
        await using var conn = await _db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<Row>(new CommandDefinition("""
            SELECT a.artifact_id AS ArtifactId,
                   a.role AS Role,
                   a.mime_type AS MimeType,
                   a.byte_sha256 AS ByteSha256,
                   a.duration_sec AS DurationSec,
                   a.storage_key AS StorageKey,
                   a.verification_status AS VerificationStatus,
                   t.provider_id AS ProviderId,
                   t.model_id AS ModelId,
                   s.story AS Story,
                   s.performance_payload_json::text AS PayloadJson
            FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t
              ON t.task_id = a.task_id
             AND t.tenant_id IS NULL
            LEFT JOIN LATERAL (
                SELECT story, performance_payload_json
                FROM pack_content.production_task_input_snapshots
                WHERE task_id = a.task_id
                  AND tenant_id IS NULL
                ORDER BY frozen_at DESC
                LIMIT 1
            ) s ON true
            WHERE a.artifact_id = @Id
              AND a.tenant_id IS NULL
              AND a.role = @Role
              AND a.verification_status = @Verified
            """, new
        {
            Id = artifactId,
            Role = ProductionArtifactRoles.Voice,
            Verified = ProductionArtifactStatuses.Verified,
        }, cancellationToken: ct));
        if (row is null) return null;
        if (!TrySplitControlledKey(row.StorageKey, out var folder, out var fileId))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        var bytes = _videos.TryReadNamed(folder, fileId);
        if (bytes is null || bytes.Length < 32) return null;
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.Equals(sha, row.ByteSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        return new UnattributedVoice(
            row.ArtifactId,
            row.Role,
            row.MimeType,
            sha,
            row.DurationSec,
            row.ProviderId ?? "",
            row.ModelId ?? "",
            row.Story ?? "",
            LineIdOf(row.PayloadJson),
            bytes);
    }

    static string LineIdOf(string? payload)
    {
        var json = payload ?? "";
        const string key = "\"lineId\":\"";
        var i = json.IndexOf(key, StringComparison.Ordinal);
        if (i < 0) return "";
        var start = i + key.Length;
        var end = json.IndexOf('"', start);
        if (end <= start) return "";
        return json[start..end].Trim();
    }

    static bool TrySplitControlledKey(string? storageKey, out string folder, out string fileId)
    {
        folder = "";
        fileId = "";
        var raw = (storageKey ?? "").Trim();
        if (raw.Length == 0
            || raw.Contains('\\')
            || raw.Contains("..", StringComparison.Ordinal)
            || raw.Contains("://", StringComparison.Ordinal)
            || raw.StartsWith('/')
            || Path.IsPathRooted(raw))
            return false;
        var i = raw.LastIndexOf('/');
        if (i <= 0 || i >= raw.Length - 1) return false;
        folder = raw[..i];
        fileId = raw[(i + 1)..];
        if (fileId.Length < 8) return false;
        if (!Allowed(folder, slash: true) || !Allowed(fileId, slash: false)) return false;
        return true;
    }

    static bool Allowed(string value, bool slash)
    {
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch == '-') continue;
            if (slash && ch == '/') continue;
            return false;
        }
        return value.Length > 0;
    }

    sealed class Row
    {
        public Guid ArtifactId { get; set; }
        public string Role { get; set; } = "";
        public string MimeType { get; set; } = "";
        public string ByteSha256 { get; set; } = "";
        public decimal DurationSec { get; set; }
        public string StorageKey { get; set; } = "";
        public string VerificationStatus { get; set; } = "";
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string? Story { get; set; }
        public string? PayloadJson { get; set; }
    }
}
