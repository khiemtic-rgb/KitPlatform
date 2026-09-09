using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content;

public static class CharacterReferencePackRules
{
    public const string DocumentId = "CHARACTER_REFERENCE_PACK_V1";
    public const string SuiteId = "CHARACTER_REFERENCE_PACK_V1_REGRESSION";
    public const string Version = "V1";

    public static readonly string[] Statuses =
        ["DRAFT", "REVIEW", "VALIDATED", "APPROVED", "DIRECTOR_APPROVED", "LOCKED", "REJECTED", "BLOCKED", "SUPERSEDED"];
    public const string IdentityConflictCode = "REFERENCE_PACK_IDENTITY_CONFLICT";
    public const string AssetMissingCode = "REFERENCE_ASSET_MISSING";
    public static readonly string[] RequiredTypes = ["FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY"];
    public static readonly string[] OptionalTypes =
        ["NEUTRAL", "HAPPY", "SAD", "WORRIED", "SURPRISED", "ANGRY", "THINKING"];
    public static readonly string[] AssetTypes =
    [
        "MASTER_IMAGE", "FACE_REFERENCE", "EYES_REFERENCE", "HAIR_REFERENCE", "FULL_BODY_REFERENCE",
        "WARDROBE_REFERENCE", "EXPRESSION_REFERENCE", "POSE_REFERENCE", "CONTINUITY_REFERENCE",
        "FRONT", "THREE_QUARTER", "SIDE", "FULL_BODY",
    ];
    public static readonly string[] IdentityKeys = ["face", "eyes", "hair", "age", "expression", "proportion", "style"];
    public static readonly string[] ProductionOnlyKeys = ["camera", "shot", "lighting", "angle", "prompt", "gemini", "runway", "veo"];
    public static readonly string[] ValidationGates =
        ["MASTER", "DNA", "OWNERSHIP", "IDENTITY", "ASSET", "CONTINUITY", "ALLOWED", "FORBIDDEN", "STRUCTURE", "PROVENANCE", "SHA", "VERSION"];

    public static bool RequireCharacterId(string? characterId) =>
        !string.IsNullOrWhiteSpace(characterId);

    public static string NormalizeCharacterId(string? raw)
    {
        var v = (raw ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0) throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: characterId bắt buộc. Không mặc định nhân vật.");
        return v;
    }

    public static bool SameTenant(string? requested, string? owned) =>
        !string.IsNullOrWhiteSpace(requested)
        && string.Equals(requested.Trim(), owned?.Trim(), StringComparison.OrdinalIgnoreCase);

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static bool ShaExists(string? sha) =>
        !string.IsNullOrWhiteSpace(sha) && sha.Trim().Length >= 32;

    public static bool MasterLocked(string? status) =>
        string.Equals(status, KitVideoMasterLockRules.LockedStatus, StringComparison.OrdinalIgnoreCase);

    public static bool DnaLocked(string? status) =>
        string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase);

    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoFix() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY" or "VEO";
    public static bool IsDirectorApproved(string? status) =>
        status is "APPROVED" or "DIRECTOR_APPROVED";
    public static bool IsValidated(string? status) =>
        status is "VALIDATED" or "REVIEW";
    public static bool ContainsProvider(string? raw)
    {
        var t = (raw ?? "").ToLowerInvariant();
        return t.Contains("gemini_prompt") || t.Contains("runway_prompt") || t.Contains("veo_prompt")
            || t.Contains("model_prompt") || t.Contains("final_prompt") || t.Contains("gen4_turbo");
    }
    public static bool TouchesGolden(string? path) =>
        (path ?? "").Contains("GOLDEN", StringComparison.OrdinalIgnoreCase)
        || (path ?? "").Contains("SH01-01", StringComparison.OrdinalIgnoreCase);

    public static void EnsureDirector(string? actor)
    {
        var a = (actor ?? "").Trim();
        if (a.Length == 0 || a.Equals("anonymous", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: chỉ Director được duyệt Character Reference Pack.");
    }

    public static void EnsureImmutable(string action, string? status)
    {
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"CRP_LOCKED: V1 không overwrite khi {action}. Tạo REFERENCE PACK V2.");
    }

    public sealed record CheckItem(string Code, string Label, bool Pass, string? Reason, string Tone = "wait");
    public sealed record RefEntry(string Type, string ArtifactPath, string ArtifactSha256, string? LiveSha, JsonElement Metadata, bool Required);
    public sealed record IdentityFinding(string Attribute, string Verdict, string LockedValue, string ReferenceValue, string Message);

    public static IReadOnlyList<CheckItem> EvaluateAuthorityGate(
        bool masterExists, bool masterLocked, bool masterShaMatch,
        bool dnaExists, bool dnaLocked, bool dnaShaMatch,
        bool ownershipOk,
        bool prpExists = true, bool prpLocked = true, bool prpShaMatch = true)
    {
        return
        [
            Check("master_exists", "Master tồn tại", masterExists, "Master missing"),
            Check("master_locked", "Master đã khóa", masterLocked, "Master unlocked"),
            Check("master_sha", "Master SHA khớp", masterShaMatch, "Master SHA mismatch"),
            Check("dna_exists", "DNA tồn tại", dnaExists, "DNA missing"),
            Check("dna_locked", "DNA đã khóa", dnaLocked, "DNA unlocked"),
            Check("dna_sha", "DNA SHA khớp", dnaShaMatch, "DNA SHA mismatch"),
            Check("prp_exists", "PRP tồn tại", prpExists, "PRP missing"),
            Check("prp_locked", "PRP đã khóa", prpLocked, "PRP unlocked"),
            Check("prp_sha", "PRP SHA khớp", prpShaMatch, "PRP SHA mismatch"),
            Check("ownership", "Đúng nhân vật", ownershipOk, "Character ownership conflict"),
        ];
    }

    public static bool CanUse(
        bool masterLocked, bool dnaLocked, bool prpLocked,
        bool shaMatch, string? crpStatus, bool coverageReady, bool identityPass) =>
        masterLocked && dnaLocked && prpLocked && shaMatch && coverageReady && identityPass
        && (IsValidated(crpStatus) || IsDirectorApproved(crpStatus)
            || string.Equals(crpStatus, "LOCKED", StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> MissingTypes(IReadOnlyList<CheckItem> coverage) =>
        coverage.Where(x => !x.Pass).Select(x => x.Label).ToList();

    public static string StaffViewLabel(string? type) => (type ?? "").Trim().ToUpperInvariant() switch
    {
        "FRONT" => "Trước mặt",
        "THREE_QUARTER" => "3/4",
        "SIDE" => "Nghiêng",
        "FULL_BODY" => "Toàn thân",
        _ => (type ?? "").Trim(),
    };

    public static string StaffMissingReason(string? type) =>
        $"Thiếu ảnh {StaffViewLabel(type).ToLowerInvariant()}.";

    public static IReadOnlyList<CheckItem> EvaluateDuplicateOrientation(IEnumerable<string> types)
    {
        return types
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .GroupBy(t => NormalizeRefType(t), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0 && g.Count() > 1)
            .Select(g => Check($"dup_{g.Key.ToLowerInvariant()}", g.Key, false, "Duplicate orientation"))
            .ToList();
    }

    public static IReadOnlyList<CheckItem> EvaluateItemProvenance(
        string liveMasterSha, string liveDnaSha, string livePrpSha, IReadOnlyList<RefEntry> entries)
    {
        var items = new List<CheckItem>();
        foreach (var e in entries.Where(x => !string.IsNullOrWhiteSpace(x.ArtifactPath)))
        {
            var master = ReadMeta(e.Metadata, "masterSha256");
            var dna = ReadMeta(e.Metadata, "dnaSha256");
            var prp = ReadMeta(e.Metadata, "prpSha256");
            var character = ReadMeta(e.Metadata, "characterId");
            if (master.Length > 0 && !SameSha(master, liveMasterSha))
                items.Add(Check($"prov_master_{e.Type}", e.Type, false, "Master SHA mismatch"));
            if (dna.Length > 0 && !SameSha(dna, liveDnaSha))
                items.Add(Check($"prov_dna_{e.Type}", e.Type, false, "DNA SHA mismatch"));
            if (prp.Length > 0 && !SameSha(prp, livePrpSha))
                items.Add(Check($"prov_prp_{e.Type}", e.Type, false, "PRP SHA mismatch"));
            if (character.Length > 0 && !RequireCharacterId(character))
                items.Add(Check($"prov_char_{e.Type}", e.Type, false, "Character ownership conflict"));
        }
        if (items.Count == 0)
            items.Add(Check("provenance", "Provenance", ShaExists(liveMasterSha) && ShaExists(liveDnaSha) && ShaExists(livePrpSha), "Provenance mismatch"));
        return items;
    }

    public static bool AllowedRegisterPath(string? path, string characterId, IEnumerable<string>? roots = null)
    {
        var full = string.IsNullOrWhiteSpace(path) ? "" : Path.GetFullPath(path);
        if (full.Length == 0 || !File.Exists(full)) return false;
        if (TouchesGolden(full)) return false;
        if (!full.Contains(NormalizeCharacterId(characterId), StringComparison.OrdinalIgnoreCase)) return false;
        if (roots is null) return true;
        return roots.Any(root =>
            !string.IsNullOrWhiteSpace(root)
            && full.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
    }

    public static bool PathMatchesView(string? path, string type)
    {
        var n = (path ?? "").Replace('\\', '/').ToUpperInvariant();
        var t = NormalizeRefType(type);
        if (n.Length == 0 || t.Length == 0 || TouchesGolden(n)) return false;
        return t switch
        {
            "FULL_BODY" => n.Contains("FULL_BODY") || n.Contains("FULL-BODY") || n.Contains("FULLBODY")
                || n.Contains("FULL_BODY_REFERENCE"),
            "FRONT" => n.Contains("FRONT") && !n.Contains("FULL"),
            "SIDE" => n.Contains("SIDE") || n.Contains("PROFILE"),
            "THREE_QUARTER" => n.Contains("THREE_QUARTER") || n.Contains("THREE-QUARTER") || n.Contains("34L") || n.Contains("34R"),
            _ => n.Contains(t, StringComparison.Ordinal),
        };
    }

    public static bool GatePass(IReadOnlyList<CheckItem> items) => items.All(x => x.Pass);

    public static void EnsureCanCreate(IReadOnlyList<CheckItem> gate, bool lockedPack = false)
    {
        if (lockedPack)
            throw new InvalidOperationException("CRP_LOCKED: V1 đã khóa. Tạo REFERENCE PACK V2.");
        ThrowFirst(gate);
    }

    public static bool CanCreateOfficialPack(bool authorityPass, bool officialPackMissing, bool officialMasterRowExists) =>
        authorityPass && officialPackMissing && officialMasterRowExists;

    public static string StaffOfficialCreateBlocked =>
        "Bộ ảnh chuẩn đi theo Character Authority Pipeline. Không tạo hàng official khi chưa có Master official.";

    public static Guid WorkspaceItemId(string characterId, string type, string sha256)
    {
        var raw = $"{NormalizeCharacterId(characterId)}|{NormalizeRefType(type)}|{(sha256 ?? "").Trim().ToLowerInvariant()}";
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static IReadOnlyList<CheckItem> EvaluateCoverage(IReadOnlyList<RefEntry> entries)
    {
        return RequiredTypes.Select(type =>
        {
            var hit = entries.FirstOrDefault(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
            var present = hit is not null && !string.IsNullOrWhiteSpace(hit.ArtifactPath);
            return Check($"req_{type.ToLowerInvariant()}", type, present, StaffMissingReason(type));
        }).ToList();
    }

    public static IReadOnlyList<CheckItem> EvaluateArtifacts(IReadOnlyList<RefEntry> entries)
    {
        var items = new List<CheckItem>();
        foreach (var e in entries.Where(x => x.Required || !string.IsNullOrWhiteSpace(x.ArtifactPath)))
        {
            if (string.IsNullOrWhiteSpace(e.ArtifactPath))
            {
                items.Add(Check($"art_{e.Type}", e.Type, false, "Artifact missing"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(e.LiveSha))
            {
                items.Add(Check($"art_{e.Type}", e.Type, false, "Artifact unreadable"));
                continue;
            }
            items.Add(Check($"art_{e.Type}", e.Type, SameSha(e.ArtifactSha256, e.LiveSha), "Artifact SHA mismatch"));
        }
        return items;
    }

    public static IReadOnlyList<IdentityFinding> EvaluateIdentity(JsonElement dna, IReadOnlyList<RefEntry> entries)
    {
        var findings = new List<IdentityFinding>();
        foreach (var key in IdentityKeys)
        {
            var locked = ReadDnaValue(dna, key);
            foreach (var entry in entries.Where(e => !string.IsNullOrWhiteSpace(e.ArtifactPath)))
            {
                var seen = ReadMeta(entry.Metadata, key);
                findings.Add(CompareIdentity(key, locked, seen, entry.Type, dna));
            }
        }
        return findings;
    }

    public static IdentityFinding CompareIdentity(string attribute, string locked, string seen, string view, JsonElement dna)
    {
        if (string.IsNullOrWhiteSpace(locked) && string.IsNullOrWhiteSpace(seen))
            return new IdentityFinding(attribute, "UNKNOWN", "", "", "DNA không định nghĩa. Không tự bịa.");
        if (string.IsNullOrWhiteSpace(locked) && !string.IsNullOrWhiteSpace(seen))
        {
            if (IsForbidden(dna, seen, attribute))
                return new IdentityFinding(attribute, "FAIL", "", seen, $"Ảnh {view} có đặc điểm bị cấm.");
            return new IdentityFinding(attribute, "NEEDS_REVIEW", "", seen, "UNSPECIFIED / NEEDS REVIEW");
        }
        if (!string.IsNullOrWhiteSpace(locked) && string.IsNullOrWhiteSpace(seen))
            return new IdentityFinding(attribute, "UNKNOWN", locked, "", "Ảnh chưa ghi đặc điểm này.");
        if (Normalize(locked) == Normalize(seen))
            return new IdentityFinding(attribute, "PASS", locked, seen, "Khớp DNA.");
        if (IsAllowed(dna, seen, attribute))
            return new IdentityFinding(attribute, "PASS", locked, seen, "Allowed variation PASS");
        return new IdentityFinding(attribute, "FAIL", locked, seen, $"Ảnh {view} khác đặc điểm nhân vật đã khóa.");
    }

    public static bool IdentityBlocks(IReadOnlyList<IdentityFinding> findings) =>
        findings.Any(f => f.Verdict == "FAIL");

    public static bool ReadyForDirector(
        IReadOnlyList<CheckItem> authority,
        IReadOnlyList<CheckItem> coverage,
        IReadOnlyList<CheckItem> artifacts,
        IReadOnlyList<IdentityFinding> identity) =>
        GatePass(authority) && GatePass(coverage) && GatePass(artifacts) && !IdentityBlocks(identity);

    public static void EnsureCanApprove(
        string? status,
        IReadOnlyList<CheckItem> authority,
        IReadOnlyList<CheckItem> coverage,
        IReadOnlyList<CheckItem> artifacts,
        IReadOnlyList<IdentityFinding> identity)
    {
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)) return;
        if (!ReadyForDirector(authority, coverage, artifacts, identity))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: bộ tham chiếu chưa đủ điều kiện duyệt.");
    }

    public static void EnsureCanLock(string? status)
    {
        if (string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)) return;
        if (!IsDirectorApproved(status))
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Director chưa APPROVE.");
    }

    public static void EnsureRejectReason(string? note)
    {
        if ((note ?? "").Trim().Length < 3)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Reject phải có lý do.");
    }

    public static string NormalizeRefType(string? raw)
    {
        var t = (raw ?? "").Trim().ToUpperInvariant();
        return t switch
        {
            "FACE_REFERENCE" or "MASTER_IMAGE" => "FRONT",
            "FULL_BODY_REFERENCE" => "FULL_BODY",
            "PROFILE" or "SIDE_REFERENCE" => "SIDE",
            "THREE_QUARTER_LEFT" or "THREE_QUARTER_RIGHT" => "THREE_QUARTER",
            "EXPRESSION_REFERENCE" => "NEUTRAL",
            "POSE_REFERENCE" => "FRONT",
            _ => t,
        };
    }

    public static string CanonicalPayload(
        string characterId, string version, string masterId, string masterSha, string dnaId, string dnaSha,
        IEnumerable<RefEntry> entries, JsonElement? spec = null)
    {
        var refs = entries
            .OrderBy(e => e.Type, StringComparer.Ordinal)
            .Select(e => new
            {
                type = e.Type.ToUpperInvariant(),
                artifact_sha256 = (e.ArtifactSha256 ?? "").ToLowerInvariant(),
                metadata = SortElement(e.Metadata),
            })
            .ToList();
        var surface = new
        {
            character_id = characterId.Trim().ToUpperInvariant(),
            version = version.Trim().ToUpperInvariant(),
            master_id = masterId,
            master_sha256 = masterSha.ToLowerInvariant(),
            dna_id = dnaId,
            dna_sha256 = dnaSha.ToLowerInvariant(),
            spec = spec is { ValueKind: JsonValueKind.Object } s ? SortElement(s) : null,
            references = refs,
        };
        return JsonSerializer.Serialize(surface, CanonicalOptions);
    }

    public static string PackSha(
        string characterId, string version, string masterId, string masterSha, string dnaId, string dnaSha,
        IEnumerable<RefEntry> entries, JsonElement? spec = null) =>
        KitVideoIntegrityRules.Sha256Hex(System.Text.Encoding.UTF8.GetBytes(
            CanonicalPayload(characterId, version, masterId, masterSha, dnaId, dnaSha, entries, spec)));

    public static JsonElement DeriveSpec(
        string characterId, string characterName, string eraId,
        string masterId, string masterSha, string masterStatus,
        string dnaId, string dnaSha, string dnaStatus, JsonElement dna,
        string prpId = "", string prpSha = "", string prpStatus = "")
    {
        var face = ReadDnaValue(dna, "face");
        var eyes = ReadDnaValue(dna, "eyes");
        var hair = ReadDnaValue(dna, "hair");
        var age = ReadDnaValue(dna, "age");
        var proportion = ReadDnaValue(dna, "proportion");
        var payload = new
        {
            identity = new { character_id = characterId, character_name = characterName, era_id = eraId },
            master = new { master_id = masterId, master_sha256 = masterSha, master_status = masterStatus },
            dna = new { dna_id = dnaId, dna_sha256 = dnaSha, dna_status = dnaStatus },
            prp = new { prp_id = prpId, prp_sha256 = prpSha, prp_status = prpStatus },
            face = new { value = face },
            eyes = new { value = eyes },
            hair = new { value = hair },
            age_body = new { age, proportion },
            expression = new { allowed = ReadList(dna, "allowedVariation"), forbidden = ReadList(dna, "forbiddenVariation") },
            pose = new { allowed = Array.Empty<string>(), forbidden = Array.Empty<string>() },
            wardrobe = new { },
            accessory = new { },
            character_state = new { },
            continuity = new { anchors = new[] { "face", "hair", "proportion" } },
            allowed_variation = ReadList(dna, "allowedVariation"),
            forbidden = ReadList(dna, "forbiddenVariation"),
            production_notes = "",
        };
        return JsonSerializer.SerializeToElement(payload);
    }

    public static IReadOnlyList<IdentityFinding> EvaluateSpecConflicts(JsonElement dna, JsonElement spec)
    {
        var findings = new List<IdentityFinding>();
        if (spec.ValueKind != JsonValueKind.Object) return findings;
        foreach (var key in IdentityKeys)
        {
            var locked = ReadDnaValue(dna, key);
            var seen = ReadSpecValue(spec, key);
            if (string.IsNullOrWhiteSpace(seen)) continue;
            findings.Add(CompareIdentity(key, locked, seen, "SPEC", dna));
        }
        if (spec.TryGetProperty("identity", out var identity) && HasProductionKey(identity))
            findings.Add(new IdentityFinding("structure", "FAIL", "identity", "camera/prompt", "Không đưa camera/action/prompt vào Identity."));
        return findings;
    }

    public static IReadOnlyList<CheckItem> EvaluateValidationGates(
        IReadOnlyList<CheckItem> authority,
        IReadOnlyList<CheckItem> coverage,
        IReadOnlyList<CheckItem> artifacts,
        IReadOnlyList<IdentityFinding> identity,
        bool provenanceOk,
        bool versionOk)
    {
        bool Auth(string code) => authority.Any(x => x.Code == code && x.Pass);
        var identityFail = identity.Any(x => x.Verdict == "FAIL" && x.Attribute is "face" or "eyes" or "hair" or "age" or "proportion" or "style");
        var forbiddenFail = identity.Any(x => x.Verdict == "FAIL" && (x.Attribute == "structure" || x.Message.Contains("cấm", StringComparison.Ordinal)));
        var allowedOk = identity.Where(x => x.Verdict == "PASS").Any() || !identityFail;
        return
        [
            Gate("MASTER", Auth("master_exists") && Auth("master_locked") && Auth("master_sha")),
            Gate("DNA", Auth("dna_exists") && Auth("dna_locked") && Auth("dna_sha")),
            Gate("OWNERSHIP", Auth("ownership")),
            Gate("IDENTITY", !identityFail),
            Gate("ASSET", GatePass(artifacts) || artifacts.Count == 0),
            Gate("CONTINUITY", !identityFail),
            Gate("ALLOWED", allowedOk),
            Gate("FORBIDDEN", !forbiddenFail),
            Gate("STRUCTURE", identity.All(x => x.Attribute != "structure" || x.Verdict != "FAIL")),
            Gate("PROVENANCE", provenanceOk),
            Gate("SHA", Auth("master_sha") && Auth("dna_sha") && Auth("prp_sha")),
            Gate("VERSION", versionOk),
        ];
    }

    public static void EnsureValidatedOrThrow(
        IReadOnlyList<CheckItem> authority,
        IReadOnlyList<CheckItem> coverage,
        IReadOnlyList<CheckItem> artifacts,
        IReadOnlyList<IdentityFinding> identity)
    {
        var fails = identity.Where(x => x.Verdict == "FAIL").ToList();
        if (fails.Count > 0)
        {
            var detail = string.Join(" | ", fails.Select(f => $"{f.Attribute}:{f.ReferenceValue}->{f.LockedValue}"));
            throw new InvalidOperationException($"{IdentityConflictCode}: {detail}");
        }
        if (!ReadyForDirector(authority, coverage, artifacts, identity))
            throw new InvalidOperationException("REFERENCE_PACK_NOT_READY: bộ tham chiếu chưa đủ điều kiện.");
    }

    public static int VersionNumber(string? current)
    {
        var raw = (current ?? "V1").Trim().ToUpperInvariant();
        return raw.StartsWith('V') && int.TryParse(raw[1..], out var parsed) ? parsed : 0;
    }

    public static string NextVersion(string? current) => $"V{VersionNumber(current) + 1}";

    public static string DisplayAge(JsonElement dna) => ReadDnaValue(dna, "age");

    public static string DisplaySummary(JsonElement dna)
    {
        var face = ReadDnaValue(dna, "face");
        var hair = ReadDnaValue(dna, "hair");
        var style = ReadDnaValue(dna, "style");
        var parts = new[] { face, hair, style }.Where(x => x.Length > 0);
        return string.Join(" · ", parts);
    }

    public static string PackCode(string characterId, string eraId, string version) =>
        $"{NormalizeCharacterId(characterId)}-{eraId.Trim().ToUpperInvariant()}-REF-{version.Trim().ToUpperInvariant()}";

    public static string StatusVi(string? status) => (status ?? "DRAFT").ToUpperInvariant() switch
    {
        "LOCKED" => "Đã khóa",
        "APPROVED" or "DIRECTOR_APPROVED" => "Đã duyệt",
        "READY_FOR_DIRECTOR" => "Đang chờ duyệt",
        "VALIDATED" or "REVIEW" => "Đã kiểm tra",
        "REJECTED" => "Không đạt",
        "BLOCKED" => "Chưa sẵn sàng",
        "SUPERSEDED" => "Đã thay thế",
        _ => "Đang xây dựng",
    };

    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static CheckItem Check(string code, string label, bool pass, string reason) =>
        new(code, label, pass, pass ? null : reason, pass ? "done" : "block");

    private static CheckItem Gate(string code, bool pass) =>
        Check(code, code, pass, pass ? "PASS" : "FAIL");

    private static string[] ReadList(JsonElement dna, string key)
    {
        if (dna.ValueKind != JsonValueKind.Object || !dna.TryGetProperty(key, out var list) || list.ValueKind != JsonValueKind.Array)
            return [];
        return list.EnumerateArray()
            .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
            .Select(x => x.GetString()!.Trim())
            .ToArray();
    }

    private static string ReadSpecValue(JsonElement spec, string key)
    {
        if (spec.ValueKind != JsonValueKind.Object) return "";
        if (key == "age" || key == "proportion")
        {
            if (spec.TryGetProperty("age_body", out var body))
                return FirstString(body, key);
        }
        if (spec.TryGetProperty(key, out var node))
            return FirstString(node, "value", key);
        return "";
    }

    private static bool HasProductionKey(JsonElement spec)
    {
        var raw = spec.GetRawText().ToLowerInvariant();
        return ProductionOnlyKeys.Any(k => raw.Contains('"' + k + '"', StringComparison.Ordinal));
    }

    private static void ThrowFirst(IReadOnlyList<CheckItem> gate)
    {
        var fail = gate.FirstOrDefault(x => !x.Pass);
        if (fail is null) return;
        throw new InvalidOperationException($"CRP_GATE_NOT_SATISFIED: {fail.Reason}");
    }

    private static string ReadDnaValue(JsonElement dna, string key)
    {
        if (dna.ValueKind != JsonValueKind.Object) return "";
        if (key == "proportion")
            return FirstString(dna, "proportion", "body", "ageProportion");
        return FirstString(dna, key, key + "Reference");
    }

    private static string ReadMeta(JsonElement meta, string key)
    {
        if (meta.ValueKind != JsonValueKind.Object) return "";
        return FirstString(meta, key);
    }

    private static string FirstString(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var node)) continue;
            if (node.ValueKind == JsonValueKind.String) return node.GetString()?.Trim() ?? "";
            if (node.ValueKind == JsonValueKind.Number) return node.ToString();
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.String)
                    return val.GetString()?.Trim() ?? "";
                foreach (var nested in new[] { "age_appearance", key + "_appearance", key + "_shape" })
                {
                    if (!node.TryGetProperty(nested, out var child) || child.ValueKind != JsonValueKind.Object) continue;
                    if (child.TryGetProperty("value", out var cv) && cv.ValueKind == JsonValueKind.String)
                    {
                        var text = cv.GetString()?.Trim() ?? "";
                        if (text.Length > 0) return text;
                    }
                }
                if (node.TryGetProperty("invariant", out var inv))
                {
                    if (inv.ValueKind == JsonValueKind.String) return inv.GetString()?.Trim() ?? "";
                    if (inv.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in inv.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                                return item.GetString()!.Trim();
                        }
                    }
                }
            }
        }
        return "";
    }

    private static bool IsForbidden(JsonElement dna, string seen, string attribute)
    {
        if (dna.ValueKind != JsonValueKind.Object) return false;
        if (!dna.TryGetProperty("forbiddenVariation", out var list) || list.ValueKind != JsonValueKind.Array)
            return false;
        var n = Normalize(seen);
        return list.EnumerateArray().Any(x =>
            x.ValueKind == JsonValueKind.String && Normalize(x.GetString()) is { Length: > 0 } f
            && (n.Contains(f) || f.Contains(n) || f.Contains(attribute)));
    }

    private static bool IsAllowed(JsonElement dna, string seen, string attribute)
    {
        if (dna.ValueKind != JsonValueKind.Object) return false;
        if (!dna.TryGetProperty("allowedVariation", out var list) || list.ValueKind != JsonValueKind.Array)
            return false;
        var n = Normalize(seen);
        return list.EnumerateArray().Any(x =>
            x.ValueKind == JsonValueKind.String && Normalize(x.GetString()) is { Length: > 0 } a
            && (a == n || a == attribute));
    }

    private static string Normalize(string? raw) =>
        (raw ?? "").Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');

    private static object? SortElement(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object) return null;
        return el.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(p => p.Name, p => p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString());
    }
}
