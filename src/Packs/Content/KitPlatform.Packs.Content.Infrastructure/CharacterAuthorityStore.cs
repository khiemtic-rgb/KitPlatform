using System.Text.Json;
using System.Text.Json.Nodes;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class CharacterAuthorityStore
{
    private readonly KitVideoMasterReferenceRepository _assets;

    public CharacterAuthorityStore(KitVideoMasterReferenceRepository assets) => _assets = assets;

    public sealed record Snapshot(
        Guid AssetId,
        Guid VersionId,
        string CharacterId,
        string EraId,
        string CharacterName,
        string Role,
        string IdentityVersion,
        string MasterStatus,
        string? MasterSha,
        string? MasterPath,
        Guid? MasterCandidateId,
        string? MasterFingerprint,
        string? Provider,
        string? ProviderRequestId,
        string DnaStatus,
        string? DnaSha,
        string? DnaSpec,
        string? DnaMasterSha,
        string PrpStatus,
        string? PrpSha,
        string? PrpSpec,
        string? PrpMasterSha,
        string? PrpDnaSha);

    public async Task<KitVideoMasterReferenceRepository.AssetRow?> GetAssetAsync(
        string characterId, CancellationToken ct) =>
        await _assets.GetAssetAsync("FAMIXA", characterId, ct);

    public async Task<Snapshot?> ReadAsync(string characterId, string eraId, CancellationToken ct)
    {
        var asset = await GetAssetAsync(characterId, ct);
        if (asset is null) return null;
        var root = Parse(asset.ExtraJson);
        var node = root["authorityInitialization"] as JsonObject;
        if (node is null)
        {
            return new Snapshot(
                asset.AssetId, asset.VersionId, characterId, eraId, "", "", "",
                CharacterAuthorityInitializationV1Rules.Missing, null, null, null, null, null, null,
                CharacterAuthorityInitializationV1Rules.Missing, null, null, null,
                CharacterAuthorityInitializationV1Rules.Missing, null, null, null, null);
        }

        var master = node["master"] as JsonObject;
        var dna = node["dna"] as JsonObject;
        var prp = node["prp"] as JsonObject;
        return new Snapshot(
            asset.AssetId,
            asset.VersionId,
            Text(node, "characterId") ?? characterId,
            Text(node, "era") ?? eraId,
            Text(node, "characterName") ?? "",
            Text(node, "role") ?? "",
            Text(node, "identityVersion") ?? "",
            Text(master, "status") ?? CharacterAuthorityInitializationV1Rules.Missing,
            Text(master, "sha256"),
            Text(master, "artifactPath"),
            GuidOrNull(master, "candidateId"),
            Text(master, "fingerprint"),
            Text(master, "provider"),
            Text(master, "providerRequestId"),
            Text(dna, "status") ?? CharacterAuthorityInitializationV1Rules.Missing,
            Text(dna, "sha256"),
            Text(dna, "specJson"),
            Text(dna, "masterSha256"),
            Text(prp, "status") ?? CharacterAuthorityInitializationV1Rules.Missing,
            Text(prp, "sha256"),
            Text(prp, "specJson"),
            Text(prp, "masterSha256"),
            Text(prp, "dnaSha256"));
    }

    public async Task WriteAsync(Snapshot snap, CancellationToken ct)
    {
        var doc = new JsonObject
        {
            ["authorityInitialization"] = new JsonObject
            {
                ["document"] = CharacterAuthorityInitializationV1Rules.DocumentId,
                ["characterId"] = snap.CharacterId,
                ["era"] = snap.EraId,
                ["characterName"] = snap.CharacterName,
                ["role"] = snap.Role,
                ["identityVersion"] = snap.IdentityVersion,
                ["master"] = new JsonObject
                {
                    ["status"] = snap.MasterStatus,
                    ["sha256"] = snap.MasterSha,
                    ["artifactPath"] = snap.MasterPath,
                    ["candidateId"] = snap.MasterCandidateId?.ToString(),
                    ["fingerprint"] = snap.MasterFingerprint,
                    ["provider"] = snap.Provider,
                    ["providerRequestId"] = snap.ProviderRequestId,
                    ["version"] = "V1",
                },
                ["dna"] = new JsonObject
                {
                    ["status"] = snap.DnaStatus,
                    ["sha256"] = snap.DnaSha,
                    ["specJson"] = snap.DnaSpec,
                    ["masterSha256"] = snap.DnaMasterSha,
                },
                ["prp"] = new JsonObject
                {
                    ["status"] = snap.PrpStatus,
                    ["sha256"] = snap.PrpSha,
                    ["specJson"] = snap.PrpSpec,
                    ["masterSha256"] = snap.PrpMasterSha,
                    ["dnaSha256"] = snap.PrpDnaSha,
                },
            },
        };
        await _assets.MergeAssetExtraAsync(snap.AssetId, doc.ToJsonString(), ct);
    }

    public sealed record CrpItem(string Type, string Path, string Sha256);

    public sealed record CrpHistoryEntry(
        string Version,
        string Status,
        string? Sha256,
        IReadOnlyList<CrpItem> Items,
        string? RejectReasonCode,
        string? RejectReasonText,
        string? RejectedBy,
        string? RejectedAt,
        string? Provider,
        string? SetId,
        string? Fingerprint);

    public sealed record CrpSnapshot(
        string CharacterId,
        string EraId,
        string Status,
        int Coverage,
        bool CanUse,
        string? Sha256,
        IReadOnlyList<CrpItem> Items,
        string Version = "V1",
        string? SetId = null,
        string? Provider = null,
        string? Fingerprint = null,
        string? MasterSha = null,
        string? DnaSha = null,
        string? PrpSha = null,
        string? RejectReasonCode = null,
        string? RejectReasonText = null,
        string? RejectedBy = null,
        string? RejectedAt = null,
        IReadOnlyList<CrpHistoryEntry>? History = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? SlotScores = null);

    public async Task<CrpSnapshot?> ReadCrpAsync(string characterId, string eraId, CancellationToken ct)
    {
        var asset = await GetAssetAsync(characterId, ct);
        if (asset is null) return null;
        var root = Parse(asset.ExtraJson);
        var node = root["characterAuthorityCrp"] as JsonObject;
        if (node is null) return null;
        var items = ReadItems(node["items"] as JsonArray);
        var history = new List<CrpHistoryEntry>();
        if (node["history"] is JsonArray hist)
        {
            foreach (var row in hist.OfType<JsonObject>())
            {
                history.Add(new CrpHistoryEntry(
                    Text(row, "version") ?? "V1",
                    Text(row, "status") ?? "REJECTED",
                    Text(row, "sha256"),
                    ReadItems(row["items"] as JsonArray),
                    Text(row, "rejectReasonCode"),
                    Text(row, "rejectReasonText"),
                    Text(row, "rejectedBy"),
                    Text(row, "rejectedAt"),
                    Text(row, "provider"),
                    Text(row, "setId"),
                    Text(row, "fingerprint")));
            }
        }
        return new CrpSnapshot(
            Text(node, "characterId") ?? characterId,
            Text(node, "era") ?? eraId,
            Text(node, "status") ?? "DRAFT",
            items.Count,
            node["canUse"] is JsonValue flag && flag.TryGetValue<bool>(out var canUse) && canUse,
            Text(node, "sha256"),
            items,
            Text(node, "version") ?? "V1",
            Text(node, "setId"),
            Text(node, "provider"),
            Text(node, "fingerprint"),
            Text(node, "masterSha256"),
            Text(node, "dnaSha256"),
            Text(node, "prpSha256"),
            Text(node, "rejectReasonCode"),
            Text(node, "rejectReasonText"),
            Text(node, "rejectedBy"),
            Text(node, "rejectedAt"),
            history,
            ReadSlotScores(node["slotScores"] as JsonObject));
    }

    public async Task WriteCrpAsync(Guid assetId, CrpSnapshot snap, CancellationToken ct)
    {
        var history = new JsonArray();
        foreach (var row in snap.History ?? [])
        {
            history.Add(new JsonObject
            {
                ["version"] = row.Version,
                ["status"] = row.Status,
                ["sha256"] = row.Sha256,
                ["items"] = ToItemArray(row.Items),
                ["rejectReasonCode"] = row.RejectReasonCode,
                ["rejectReasonText"] = row.RejectReasonText,
                ["rejectedBy"] = row.RejectedBy,
                ["rejectedAt"] = row.RejectedAt,
                ["provider"] = row.Provider,
                ["setId"] = row.SetId,
                ["fingerprint"] = row.Fingerprint,
            });
        }
        var doc = new JsonObject
        {
            ["characterAuthorityCrp"] = new JsonObject
            {
                ["characterId"] = snap.CharacterId,
                ["era"] = snap.EraId,
                ["status"] = snap.Status,
                ["coverage"] = snap.Coverage,
                ["canUse"] = snap.CanUse,
                ["sha256"] = snap.Sha256,
                ["items"] = ToItemArray(snap.Items),
                ["version"] = snap.Version,
                ["setId"] = snap.SetId,
                ["provider"] = snap.Provider,
                ["fingerprint"] = snap.Fingerprint,
                ["masterSha256"] = snap.MasterSha,
                ["dnaSha256"] = snap.DnaSha,
                ["prpSha256"] = snap.PrpSha,
                ["rejectReasonCode"] = snap.RejectReasonCode,
                ["rejectReasonText"] = snap.RejectReasonText,
                ["rejectedBy"] = snap.RejectedBy,
                ["rejectedAt"] = snap.RejectedAt,
                ["history"] = history,
                ["slotScores"] = ToScoreObject(snap.SlotScores),
            },
        };
        await _assets.MergeAssetExtraAsync(assetId, doc.ToJsonString(), ct);
    }

    private static List<CrpItem> ReadItems(JsonArray? arr)
    {
        var items = new List<CrpItem>();
        if (arr is null) return items;
        foreach (var row in arr.OfType<JsonObject>())
        {
            var type = Text(row, "type");
            var path = Text(row, "path");
            var sha = Text(row, "sha256");
            if (type is null || path is null || sha is null) continue;
            items.Add(new CrpItem(type, path, sha));
        }
        return items;
    }

    private static Dictionary<string, IReadOnlyDictionary<string, int>>? ReadSlotScores(JsonObject? node)
    {
        if (node is null) return null;
        var map = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (view, value) in node)
        {
            if (value is not JsonObject row) continue;
            var scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, n) in row)
            {
                if (n is JsonValue jv && jv.TryGetValue<int>(out var v))
                    scores[key] = v;
            }
            if (scores.Count > 0) map[view] = scores;
        }
        return map.Count == 0 ? null : map;
    }

    private static JsonObject ToScoreObject(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>? scores)
    {
        var obj = new JsonObject();
        if (scores is null) return obj;
        foreach (var (view, row) in scores)
        {
            var inner = new JsonObject();
            foreach (var (key, value) in row)
                inner[key] = value;
            obj[view] = inner;
        }
        return obj;
    }

    private static JsonArray ToItemArray(IEnumerable<CrpItem> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            arr.Add(new JsonObject
            {
                ["type"] = item.Type,
                ["path"] = item.Path,
                ["sha256"] = item.Sha256,
            });
        }
        return arr;
    }

    public sealed record RevisionSnapshot(
        string CharacterId,
        string Status,
        string? RevisionId,
        string? CurrentMasterId,
        string? CurrentMasterSha,
        string? CurrentMasterPath,
        string? CurrentMasterVersion,
        string? CandidateMasterId,
        string? CandidateMasterSha,
        string? CandidateMasterPath,
        string? CandidateMasterVersion,
        string? RevisionReason,
        string? RevisionNotes,
        string? AppearanceProfileSha,
        string? AgeProfileSha,
        string? ProjectVisualStyleSha,
        string? IdentitySha,
        string? Fingerprint,
        string? GenerationContractSha,
        string? Provider,
        string? ProviderRequestId,
        string? RequestedBy,
        string? CreatedAt,
        string? DnaStatus,
        string? PrpStatus,
        string? CrpStatus,
        string? ReviewedBy = null,
        string? ReviewedAt = null,
        string? ReviewDecision = null,
        string? RejectionReason = null,
        string? ReviewNotes = null,
        string? AuditJson = null);

    public async Task<RevisionSnapshot?> ReadRevisionAsync(string characterId, CancellationToken ct)
    {
        var asset = await GetAssetAsync(characterId, ct);
        if (asset is null) return null;
        var node = Parse(asset.ExtraJson)[CharacterMasterRevisionV1Rules.ExtraKey] as JsonObject;
        if (node is null) return null;
        return new RevisionSnapshot(
            Text(node, "characterId") ?? characterId,
            Text(node, "status") ?? CharacterMasterRevisionV1Rules.StatusRequested,
            Text(node, "revisionId"),
            Text(node, "currentMasterId"),
            Text(node, "currentMasterSha"),
            Text(node, "currentMasterPath"),
            Text(node, "currentMasterVersion"),
            Text(node, "candidateMasterId"),
            Text(node, "candidateMasterSha"),
            Text(node, "candidateMasterPath"),
            Text(node, "candidateMasterVersion"),
            Text(node, "revisionReason"),
            Text(node, "revisionNotes"),
            Text(node, "appearanceProfileSha"),
            Text(node, "ageProfileSha"),
            Text(node, "projectVisualStyleSha"),
            Text(node, "identitySha"),
            Text(node, "fingerprint"),
            Text(node, "generationContractSha"),
            Text(node, "provider"),
            Text(node, "providerRequestId"),
            Text(node, "requestedBy"),
            Text(node, "createdAt"),
            Text(node, "dnaStatus"),
            Text(node, "prpStatus"),
            Text(node, "crpStatus"),
            Text(node, "reviewedBy"),
            Text(node, "reviewedAt"),
            Text(node, "reviewDecision"),
            Text(node, "rejectionReason"),
            Text(node, "reviewNotes"),
            node["audit"]?.ToJsonString());
    }

    public async Task WriteRevisionAsync(Guid assetId, RevisionSnapshot snap, CancellationToken ct)
    {
        var doc = new JsonObject
        {
            [CharacterMasterRevisionV1Rules.ExtraKey] = new JsonObject
            {
                ["document"] = CharacterMasterRevisionV1Rules.DocumentId,
                ["characterId"] = snap.CharacterId,
                ["status"] = snap.Status,
                ["revisionId"] = snap.RevisionId,
                ["currentMasterId"] = snap.CurrentMasterId,
                ["currentMasterSha"] = snap.CurrentMasterSha,
                ["currentMasterPath"] = snap.CurrentMasterPath,
                ["currentMasterVersion"] = snap.CurrentMasterVersion,
                ["candidateMasterId"] = snap.CandidateMasterId,
                ["candidateMasterSha"] = snap.CandidateMasterSha,
                ["candidateMasterPath"] = snap.CandidateMasterPath,
                ["candidateMasterVersion"] = snap.CandidateMasterVersion,
                ["revisionReason"] = snap.RevisionReason,
                ["revisionNotes"] = snap.RevisionNotes,
                ["appearanceProfileSha"] = snap.AppearanceProfileSha,
                ["ageProfileSha"] = snap.AgeProfileSha,
                ["projectVisualStyleSha"] = snap.ProjectVisualStyleSha,
                ["identitySha"] = snap.IdentitySha,
                ["fingerprint"] = snap.Fingerprint,
                ["generationContractSha"] = snap.GenerationContractSha,
                ["provider"] = snap.Provider,
                ["providerRequestId"] = snap.ProviderRequestId,
                ["requestedBy"] = snap.RequestedBy,
                ["createdAt"] = snap.CreatedAt,
                ["dnaStatus"] = snap.DnaStatus,
                ["prpStatus"] = snap.PrpStatus,
                ["crpStatus"] = snap.CrpStatus,
                ["reviewedBy"] = snap.ReviewedBy,
                ["reviewedAt"] = snap.ReviewedAt,
                ["reviewDecision"] = snap.ReviewDecision,
                ["rejectionReason"] = snap.RejectionReason,
                ["reviewNotes"] = snap.ReviewNotes,
                ["audit"] = ParseAudit(snap.AuditJson),
            },
        };
        await _assets.MergeAssetExtraAsync(assetId, doc.ToJsonString(), ct);
    }

    public async Task<bool> WriteRevisionIfStatusAsync(
        string characterId, Guid assetId, string expectedStatus, RevisionSnapshot snap, CancellationToken ct)
    {
        var current = await ReadRevisionAsync(characterId, ct);
        if (current is null || !string.Equals(current.Status, expectedStatus, StringComparison.Ordinal))
            return false;
        await WriteRevisionAsync(assetId, snap, ct);
        return true;
    }

    public KitVideoMasterLockRepository.MasterRow? ToOfficialMaster(Snapshot? snap)
    {
        if (snap is null || !CharacterAuthorityInitializationV1Rules.IsLocked(snap.MasterStatus)
            || string.IsNullOrWhiteSpace(snap.MasterSha))
            return null;
        return new KitVideoMasterLockRepository.MasterRow
        {
            Id = snap.MasterCandidateId ?? snap.AssetId,
            MasterCode = $"{snap.CharacterId}-AUTH-MASTER-V1",
            CharacterId = snap.CharacterId,
            CharacterName = snap.CharacterName,
            EraId = snap.EraId,
            ArtifactPath = snap.MasterPath ?? "",
            Sha256 = snap.MasterSha,
            Version = "V1",
            Status = KitVideoMasterLockRules.LockedStatus,
            SourceCandidateId = snap.MasterCandidateId ?? Guid.Empty,
        };
    }

    public KitVideoCharacterDnaRepository.DnaRow? ToOfficialDna(Snapshot? snap)
    {
        if (snap is null || !CharacterAuthorityInitializationV1Rules.IsLocked(snap.DnaStatus)
            || string.IsNullOrWhiteSpace(snap.DnaSpec))
            return null;
        return new KitVideoCharacterDnaRepository.DnaRow
        {
            Id = snap.AssetId,
            CharacterId = snap.CharacterId,
            CharacterName = snap.CharacterName,
            EraId = snap.EraId,
            MasterSha256 = snap.DnaMasterSha ?? snap.MasterSha ?? "",
            SpecJson = snap.DnaSpec,
            Status = "LOCKED",
            DnaVersion = "V1",
        };
    }

    public KitVideoProductionReferencePackRepository.PackRow? ToOfficialPrp(Snapshot? snap)
    {
        if (snap is null || !CharacterAuthorityInitializationV1Rules.IsLocked(snap.PrpStatus)
            || string.IsNullOrWhiteSpace(snap.PrpSha))
            return null;
        return new KitVideoProductionReferencePackRepository.PackRow
        {
            Id = snap.AssetId,
            CharacterId = snap.CharacterId,
            CharacterName = snap.CharacterName,
            EraId = snap.EraId,
            MasterSha256 = snap.PrpMasterSha ?? snap.MasterSha ?? "",
            DnaSha256 = snap.PrpDnaSha ?? snap.DnaSha ?? "",
            SpecJson = snap.PrpSpec ?? "{}",
            Status = "LOCKED",
            PackVersion = "V1",
            PrpSha256 = snap.PrpSha,
        };
    }

    private static JsonObject Parse(string? raw)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonNode ParseAudit(string? raw)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw) ?? new JsonArray();
        }
        catch (JsonException)
        {
            return new JsonArray();
        }
    }

    private static string? Text(JsonObject? node, string key)
    {
        if (node is null || !node.TryGetPropertyValue(key, out var value) || value is null)
            return null;
        var t = value.GetValue<string?>();
        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static Guid? GuidOrNull(JsonObject? node, string key)
    {
        var t = Text(node, key);
        return Guid.TryParse(t, out var id) ? id : null;
    }
}
