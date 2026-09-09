using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>CHARACTER_IDENTITY_GOVERNANCE_V1_1 — DNA-driven. No character identity fallback.</summary>
public static class CharacterIdentityGovernanceRules
{
    public const string DocumentId = "CHARACTER_IDENTITY_GOVERNANCE_V1_1";
    public const string Project = "FAMIXA";

    public static readonly string[] Hierarchy =
        ["MASTER", "CHARACTER_DNA", "PRODUCTION_REFERENCE_PACK", "SHOT_SPECIFICATION", "USER_PROMPT", "GENERATION_MODEL"];

    public static readonly string[] GateCodes =
    [
        "MASTER_GATE", "DNA_GATE", "PRP_GATE", "IDENTITY_GATE", "STRESS_GATE",
        "CONTINUITY_GATE", "REGRESSION_GATE", "SHOT_GATE", "PROMPT_GATE",
    ];

    public static readonly string[] IdentityGroups =
        ["face", "eyes", "hair", "age", "expression", "body", "proportion", "style"];

    public static bool AutoFix() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool AutoOverride() => false;
    public static bool AutoRegenerate() => false;
    public static bool ModelHasIdentityAuthority() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY";
    public static bool TouchesGolden(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Contains("SH01-01", StringComparison.OrdinalIgnoreCase)
        && path.Contains("GOLDEN", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeCharacterId(string? raw)
    {
        var v = (raw ?? "").Trim().ToUpperInvariant();
        if (v.Length == 0) return "";
        var m = Regex.Match(v, @"^(CHAR-\d{3})");
        return m.Success ? m.Groups[1].Value : v;
    }

    public static bool SameSha(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static bool ShaExists(string? sha) =>
        !string.IsNullOrWhiteSpace(sha) && sha.Trim().Length >= 32;

    public static bool IsLocked(string? status) =>
        string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "MASTER_REFERENCE_LOCKED", StringComparison.OrdinalIgnoreCase);

    public sealed record Conflict(
        string Status,
        string Code,
        string Source,
        string Attribute,
        string? RequestedValue,
        string? AuthoritativeValue,
        string Message);

    public sealed record GateItem(string Code, string Label, bool Pass, string? Reason);

    public sealed record AttributeRequest(string Attribute, string Value, string Surface);

    public static Conflict Block(string code, string source, string attribute, string? requested, string? authoritative, string message) =>
        new("BLOCKED", code, source, attribute, requested, authoritative, message);

    public static IReadOnlyList<Conflict> Evaluate(
        JsonElement dnaSpec,
        JsonElement? prpSpec,
        JsonElement? shotSpec,
        JsonElement? previousShot,
        string? userPrompt,
        AuthorityState authority)
    {
        var conflicts = new List<Conflict>();
        if (string.IsNullOrWhiteSpace(authority.CharacterId))
            conflicts.Add(Block("MASTER_CONFLICT", "MASTER", "character_id", "", "required", "character_id is required. No identity fallback."));
        if (!authority.MasterLocked)
            conflicts.Add(Block("MASTER_CONFLICT", "MASTER", "status", authority.MasterStatus ?? "MISSING", "LOCKED", "Chỉ Master LOCKED mới là Identity Authority."));
        if (!authority.DnaLocked)
            conflicts.Add(Block("DNA_CONFLICT", "CHARACTER_DNA", "status", authority.DnaStatus ?? "MISSING", "LOCKED", "DNA chưa LOCKED."));
        if (!authority.PrpLocked)
            conflicts.Add(Block("PRP_CONFLICT", "PRODUCTION_REFERENCE_PACK", "status", authority.PrpStatus ?? "MISSING", "LOCKED", "PRP chưa LOCKED."));
        if (authority.MasterLocked && !authority.MasterShaMatch)
            conflicts.Add(Block("SHA_MISMATCH", "MASTER", "sha256", "mismatch", "match", "Master SHA mismatch. Không tự cập nhật SHA."));
        if (authority.DnaLocked && !authority.DnaShaMatch)
            conflicts.Add(Block("SHA_MISMATCH", "CHARACTER_DNA", "sha256", "mismatch", "match", "DNA SHA mismatch. Không tự overwrite."));
        if (authority.PrpLocked && !authority.PrpShaMatch)
            conflicts.Add(Block("SHA_MISMATCH", "PRODUCTION_REFERENCE_PACK", "sha256", "mismatch", "match", "PRP SHA mismatch. Không tự tạo version mới."));

        if (prpSpec is { ValueKind: JsonValueKind.Object } prp && dnaSpec.ValueKind == JsonValueKind.Object)
            conflicts.AddRange(EvaluatePrpAgainstDna(prp, dnaSpec));
        if (shotSpec is { ValueKind: JsonValueKind.Object } shot && dnaSpec.ValueKind == JsonValueKind.Object)
            conflicts.AddRange(EvaluateRequestsAgainstDna(CollectRequests(shot, null), dnaSpec, "SHOT_SPECIFICATION"));
        if (!string.IsNullOrWhiteSpace(userPrompt) && dnaSpec.ValueKind == JsonValueKind.Object)
            conflicts.AddRange(EvaluateRequestsAgainstDna(CollectRequests(default, userPrompt), dnaSpec, "USER_PROMPT"));
        if (shotSpec is { ValueKind: JsonValueKind.Object } shot2)
        {
            if (!MultiPersonSeparable(shot2))
                conflicts.Add(Block("IDENTITY_CONFLICT", "SHOT_SPECIFICATION", "multi_person", "blend", "separable", "Identity must remain separable."));
            if (OcclusionCreatesNewFace(shot2))
                conflicts.Add(Block("IDENTITY_CONFLICT", "SHOT_SPECIFICATION", "occlusion", "new face", "consistent", "Không được tạo khuôn mặt mới để bù phần bị che."));
        }
        if (ContainsModelAuthority(userPrompt, shotSpec))
            conflicts.Add(Block("IDENTITY_CONFLICT", "GENERATION_MODEL", "authority", "model", "MASTER+DNA+PRP", "Generation model has no Identity Authority."));
        conflicts.AddRange(EvaluateContinuity(previousShot, shotSpec, dnaSpec, authority));
        return conflicts;
    }

    public sealed record AuthorityState(
        string CharacterId,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool MasterShaMatch,
        bool DnaShaMatch,
        bool PrpShaMatch,
        string? MasterStatus = null,
        string? DnaStatus = null,
        string? PrpStatus = null,
        Guid? MasterId = null,
        Guid? DnaId = null,
        Guid? PrpId = null);

    public static IReadOnlyList<Conflict> EvaluatePrpAgainstDna(JsonElement prp, JsonElement dna)
    {
        var list = new List<Conflict>();
        if (prp.ValueKind != JsonValueKind.Object || dna.ValueKind != JsonValueKind.Object)
            return list;
        foreach (var (left, right, attr) in new[] { ("faceReference", "face", "face"), ("eyesReference", "eyes", "eyes"), ("hairReference", "hair", "hair") })
        {
            if (IdentityNodeConflicts(ReadNode(prp, left), ReadGroup(dna, right), attr))
                list.Add(Block("PRP_CONFLICT", "CHARACTER_DNA", attr, Compact(ReadNode(prp, left)), Compact(ReadGroup(dna, right)), "PRP cannot override DNA. MASTER > DNA > PRP."));
        }
        if (prp.TryGetProperty("ageProportion", out var age) && age.ValueKind == JsonValueKind.Object)
        {
            var req = ExtractAgeToken(FullText(ReadNode(age, "age")));
            var auth = ExtractAuthoritativeAge(dna);
            if (auth is null)
                list.Add(Block("PRP_CONFLICT", "CHARACTER_DNA", "age", req, null, "Authoritative DNA age unreadable. BLOCKED. No fallback."));
            else if (req is not null && req != auth)
                list.Add(Block("PRP_CONFLICT", "CHARACTER_DNA", "age", req, auth, "PRP cannot override DNA age."));
        }
        return list;
    }

    public static IReadOnlyList<AttributeRequest> CollectRequests(JsonElement shot, string? prompt)
    {
        var list = new List<AttributeRequest>();
        var text = (prompt ?? "") + " " + (shot.ValueKind == JsonValueKind.Object ? RequestSurface(shot) : "");
        foreach (var (phrase, attr, value) in Synonyms())
        {
            if (text.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                list.Add(new AttributeRequest(attr, value, "text"));
        }
        if (shot.ValueKind == JsonValueKind.Object)
        {
            foreach (var group in IdentityGroups)
            {
                var raw = ReadIdentity(shot, group);
                if (string.IsNullOrWhiteSpace(raw) || LooksLikeInheritedAuthority(raw)) continue;
                if (group == "age" || group == "body" || group == "proportion")
                {
                    var age = ExtractAgeToken(raw);
                    if (age is not null) list.Add(new AttributeRequest("AGE", age, "shot"));
                    else if (!LooksAllowedVariation(raw, []))
                        list.Add(new AttributeRequest(group.ToUpperInvariant(), Canonical(raw), "shot"));
                }
                else if (group == "hair")
                    list.Add(new AttributeRequest("HAIR", Canonical(raw), "shot"));
                else
                    list.Add(new AttributeRequest(group.ToUpperInvariant(), Canonical(raw), "shot"));
            }
            var glasses = RequestSurface(shot);
            if (glasses.Contains("glasses", StringComparison.OrdinalIgnoreCase) || glasses.Contains("kính", StringComparison.OrdinalIgnoreCase))
                list.Add(new AttributeRequest("ACCESSORY", "glasses", "shot"));
        }
        var promptAge = ExtractAgeToken(prompt ?? "");
        if (promptAge is not null)
            list.Add(new AttributeRequest("AGE", promptAge, "prompt"));
        return list;
    }

    public static IReadOnlyList<Conflict> EvaluateRequestsAgainstDna(
        IReadOnlyList<AttributeRequest> requests, JsonElement dna, string requestSource)
    {
        var list = new List<Conflict>();
        if (dna.ValueKind != JsonValueKind.Object)
        {
            if (requests.Count > 0)
                list.Add(Block("DNA_CONFLICT", "CHARACTER_DNA", "spec", "requested", null, "DNA spec unreadable. BLOCKED. No fallback."));
            return list;
        }
        var allowedVar = ReadStringList(dna, "allowedVariation");
        var forbiddenVar = ReadStringList(dna, "forbiddenVariation");
        foreach (var req in requests)
        {
            if (LooksAllowedVariation(req.Value, allowedVar) && req.Attribute is "CAMERA" or "LIGHTING" or "POSE" or "FRAMING" or "ENVIRONMENT" or "MOTION" or "COMPOSITION")
                continue;
            var group = GroupFor(req.Attribute);
            var groupEl = ReadGroup(dna, group);
            if (groupEl.ValueKind != JsonValueKind.Object && req.Attribute is "AGE" or "HAIR" or "FACE" or "EYES" or "STYLE" or "PROPORTION")
            {
                list.Add(Block("DNA_CONFLICT", "CHARACTER_DNA", req.Attribute, req.Value, null, $"DNA {group} unreadable. BLOCKED. No fallback."));
                continue;
            }
            var allowed = ReadStringList(groupEl, "allowed").Concat(allowedVar).ToList();
            var forbidden = ReadStringList(groupEl, "forbidden").Concat(forbiddenVar).ToList();
            var invariant = ReadStringList(groupEl, "invariant");
            if (forbidden.Any(f => MatchesForbidden(req, f)))
            {
                var auth = req.Attribute == "AGE"
                    ? ExtractAuthoritativeAge(dna)
                    : req.Attribute is "HAIR" or "HAIR_LENGTH"
                        ? HairAuthority(groupEl)
                        : Compact(groupEl);
                list.Add(Block(
                    requestSource == "USER_PROMPT" ? "DNA_FORBIDDEN_ATTRIBUTE" : "DNA_SHOT_CONFLICT",
                    "CHARACTER_DNA", req.Attribute == "AGE" ? "age" : req.Attribute, req.Value, auth,
                    requestSource == "USER_PROMPT"
                        ? "User Prompt không có quyền override Identity Authority."
                        : "Shot conflicts with locked Character DNA."));
                continue;
            }
            if (req.Attribute == "AGE")
            {
                var auth = ExtractAuthoritativeAge(dna);
                if (auth is null)
                    list.Add(Block("DNA_CONFLICT", "CHARACTER_DNA", "AGE", req.Value, null, "Authoritative DNA age unreadable. BLOCKED. No fallback."));
                else if (auth != req.Value)
                    list.Add(Block("DNA_SHOT_CONFLICT", "CHARACTER_DNA", "age", req.Value, auth, "Shot conflicts with locked Character DNA."));
                continue;
            }
            if (req.Attribute is "HAIR" or "HAIR_LENGTH")
            {
                if (IsHairIdentityChange(req.Value, allowed, invariant, forbidden))
                    list.Add(Block("DNA_FORBIDDEN_ATTRIBUTE", "CHARACTER_DNA", "HAIR_LENGTH", req.Value, HairAuthority(groupEl), "Requested hair change is forbidden by Character DNA."));
                continue;
            }
            if (req.Attribute == "ACCESSORY" && forbidden.Any(f => f.Contains(req.Value, StringComparison.OrdinalIgnoreCase)))
                list.Add(Block("DNA_FORBIDDEN_ATTRIBUTE", "CHARACTER_DNA", req.Attribute, req.Value, string.Join(',', forbidden), "Requested accessory is forbidden by Character DNA."));
            if (req.Attribute is "FACE" or "EYES" or "STYLE" or "PROPORTION" or "EXPRESSION")
            {
                if (LooksAllowedVariation(req.Value, allowed)) continue;
                if (invariant.Count > 0 && !ContainsInsensitive(string.Join(' ', invariant), req.Value) && !LooksAllowedVariation(req.Value, allowed))
                    list.Add(Block("IDENTITY_CONFLICT", "CHARACTER_DNA", req.Attribute, req.Value, string.Join(',', invariant), "Invariant mutation. Shot/Prompt cannot redefine Character Identity."));
            }
        }
        return list;
    }

    public static IReadOnlyList<Conflict> EvaluateContinuity(
        JsonElement? previous, JsonElement? current, JsonElement dna, AuthorityState authority)
    {
        var list = new List<Conflict>();
        if (current is not { ValueKind: JsonValueKind.Object } cur)
            return list;
        if (previous is not { ValueKind: JsonValueKind.Object } prev)
            return list;
        Guid? prevMaster = ReadGuid(prev, "master_id") ?? ReadGuid(ReadNode(prev, "continuity"), "master_id") ?? ReadGuid(ReadNode(prev, "source"), "master_id");
        Guid? curMaster = ReadGuid(cur, "master_id") ?? ReadGuid(ReadNode(cur, "continuity"), "master_id");
        if (authority.MasterId is { } mid && curMaster is { } cmid && cmid != mid)
            list.Add(Block("IDENTITY_CONFLICT", "SHOT_SPECIFICATION", "master_id", cmid.ToString(), mid.ToString(), "Shot master_id must match locked Master."));
        if (prevMaster is { } pmid && curMaster is { } cmid2 && pmid != cmid2)
            list.Add(Block("IDENTITY_CONFLICT", "SHOT_SPECIFICATION", "continuity.master_id", cmid2.ToString(), pmid.ToString(), "Continuity: master_id changed across shots."));
        foreach (var attr in new[] { "age", "face", "hair", "eyes", "style" })
        {
            var a = ReadIdentity(cur, attr);
            var b = ReadIdentity(prev, attr);
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) continue;
            if (LooksAllowedVariation(a, ReadStringList(dna, "allowedVariation"))) continue;
            if (attr == "age")
            {
                var na = ExtractAgeToken(a);
                var nb = ExtractAgeToken(b);
                if (na is not null && nb is not null && na != nb)
                    list.Add(Block("IDENTITY_CONFLICT", "SHOT_SPECIFICATION", "age", na, nb, "Continuity: age invariant changed across shots."));
                continue;
            }
            if (!string.Equals(Canonical(a), Canonical(b), StringComparison.Ordinal) && !b.Contains(a, StringComparison.OrdinalIgnoreCase))
                list.Add(Block("IDENTITY_CONFLICT", "SHOT_SPECIFICATION", attr, CompactLimit(a), CompactLimit(b), "Continuity: identity attribute changed across shots."));
        }
        return list;
    }

    public static string StressState(string? stressResult, bool stressRulesDefined)
    {
        if (!string.IsNullOrWhiteSpace(stressResult) && ResultPass(stressResult))
            return "STRESS_PASS";
        if (!string.IsNullOrWhiteSpace(stressResult) && stressResult.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            return "STRESS_FAIL";
        if (!string.IsNullOrWhiteSpace(stressResult))
            return "STRESS_TESTED";
        if (stressRulesDefined)
            return "STRESS_DEFINED";
        return "IDENTITY_STRESS_NOT_VERIFIED";
    }

    public static bool StressGatePass(string state) => state == "STRESS_PASS";

    public static bool ResultPass(string? result) =>
        !string.IsNullOrWhiteSpace(result)
        && result.Contains("PASS", StringComparison.OrdinalIgnoreCase)
        && !result.Contains("FAIL", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<GateItem> EvaluateGates(
        AuthorityState authority,
        bool identityTestedPass,
        string stressState,
        bool continuityPass,
        bool regressionPass,
        IReadOnlyList<Conflict> conflicts)
    {
        var shotOk = conflicts.All(c => c.Source != "SHOT_SPECIFICATION" && c.Code != "DNA_SHOT_CONFLICT");
        var promptOk = conflicts.All(c => c.Source != "USER_PROMPT");
        var identityOk = identityTestedPass && conflicts.All(c => c.Code is not "IDENTITY_CONFLICT" and not "DNA_SHOT_CONFLICT" and not "DNA_FORBIDDEN_ATTRIBUTE" and not "DNA_CONFLICT");
        var dnaOk = authority.DnaLocked && authority.DnaShaMatch && conflicts.All(c => c.Code != "DNA_CONFLICT");
        var prpOk = authority.PrpLocked && authority.PrpShaMatch && conflicts.All(c => c.Code != "PRP_CONFLICT");
        return
        [
            Item("MASTER_GATE", "MASTER GATE", authority.MasterLocked && authority.MasterShaMatch, "Master LOCKED + SHA MATCH"),
            Item("DNA_GATE", "DNA GATE", dnaOk, authority.DnaLocked ? (conflicts.FirstOrDefault(c => c.Code == "DNA_CONFLICT")?.Message ?? "DNA SHA mismatch") : "DNA chưa LOCKED."),
            Item("PRP_GATE", "PRP GATE", prpOk, authority.PrpLocked ? (conflicts.FirstOrDefault(c => c.Code == "PRP_CONFLICT")?.Message ?? "PRP SHA mismatch") : "PRP chưa LOCKED."),
            Item("IDENTITY_GATE", "IDENTITY GATE", identityOk, identityTestedPass ? (conflicts.FirstOrDefault(c => c.Code.Contains("IDENTITY") || c.Code.Contains("DNA_"))?.Message ?? "Identity conflict") : "IDENTITY_NOT_VERIFIED"),
            Item("STRESS_GATE", "STRESS GATE", StressGatePass(stressState), stressState == "IDENTITY_STRESS_NOT_VERIFIED" ? "IDENTITY_STRESS_NOT_VERIFIED" : stressState),
            Item("CONTINUITY_GATE", "CONTINUITY GATE", continuityPass, "Continuity FAIL"),
            Item("REGRESSION_GATE", "REGRESSION GATE", regressionPass, "Regression FAIL"),
            Item("SHOT_GATE", "SHOT GATE", shotOk, "Shot conflict"),
            Item("PROMPT_GATE", "PROMPT GATE", promptOk, "Prompt conflict"),
        ];
    }

    public static bool AllPass(IReadOnlyList<GateItem> gates) => gates.All(x => x.Pass);

    public static bool ProductionReady(IReadOnlyList<GateItem> gates, int p0) =>
        AllPass(gates) && p0 == 0;

    public static bool HierarchyHolds() =>
        Hierarchy[0] == "MASTER"
        && Hierarchy[5] == "GENERATION_MODEL"
        && !ModelHasIdentityAuthority();

    public static string DirectorApproval() => "PENDING";

    public sealed record GenericDnaFixture(JsonElement Spec, string Age, string HairForbidden);

    public static JsonElement BuildGenericDna(string ageAppearance, IReadOnlyList<string> hairForbidden, IReadOnlyList<string>? extraForbidden = null)
    {
        var spec = new Dictionary<string, object?>
        {
            ["face"] = new Dictionary<string, object?> { ["invariant"] = new[] { "face identity" }, ["allowed"] = new[] { "3/4 view" }, ["forbidden"] = new[] { "different face identity" } },
            ["eyes"] = new Dictionary<string, object?> { ["invariant"] = new[] { "eye identity" }, ["allowed"] = new[] { "gaze" }, ["forbidden"] = new[] { "different eye structure" } },
            ["hair"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { "hairstyle identity", "hair silhouette" },
                ["allowed"] = new[] { "slight styling", "wind" },
                ["forbidden"] = hairForbidden.ToArray(),
                ["hair_length"] = new Dictionary<string, object?> { ["value"] = "canonical silhouette locked" },
            },
            ["age"] = new Dictionary<string, object?>
            {
                ["invariant"] = new[] { $"age appearance approximately {ageAppearance} years old" },
                ["allowed"] = new[] { "camera angle", "pose" },
                ["forbidden"] = new[] { "different age", "adult appearance" },
            },
            ["expression"] = new Dictionary<string, object?> { ["invariant"] = new[] { "same identity" }, ["allowed"] = new[] { "sad", "smile" }, ["forbidden"] = new[] { "treat expression as a new identity" } },
            ["body"] = new Dictionary<string, object?> { ["invariant"] = new[] { "body proportion" }, ["allowed"] = new[] { "pose" }, ["forbidden"] = new[] { "wrong body proportion" } },
            ["style"] = new Dictionary<string, object?> { ["invariant"] = new[] { "illustration" }, ["allowed"] = new[] { "lighting" }, ["forbidden"] = (extraForbidden ?? Array.Empty<string>()).Concat(["photorealistic transformation"]).ToArray() },
            ["allowedVariation"] = new[] { "camera angle", "lighting", "pose", "3/4 view", "environment" },
            ["forbiddenVariation"] = new[] { "identity blending", "face swap" },
            ["continuityRules"] = new Dictionary<string, object?> { ["sameIdentityAcrossShots"] = true },
            ["stressRules"] = new Dictionary<string, object?> { ["required"] = true },
            ["regressionRules"] = new Dictionary<string, object?> { ["noGenerate"] = true },
        };
        return JsonSerializer.SerializeToElement(spec);
    }

    private static GateItem Item(string code, string label, bool pass, string reason) =>
        new(code, label, pass, pass ? null : reason);

    private static (string phrase, string attr, string value)[] Synonyms() =>
    [
        ("tóc dài", "HAIR_LENGTH", "long"),
        ("toc dai", "HAIR_LENGTH", "long"),
        ("long hair", "HAIR_LENGTH", "long"),
        ("tóc ngang vai", "HAIR_LENGTH", "shoulder_length"),
        ("toc ngang vai", "HAIR_LENGTH", "shoulder_length"),
        ("mái tóc chạm vai", "HAIR_LENGTH", "shoulder_length"),
        ("mai toc cham vai", "HAIR_LENGTH", "shoulder_length"),
        ("shoulder-length", "HAIR_LENGTH", "shoulder_length"),
        ("shoulder length", "HAIR_LENGTH", "shoulder_length"),
        ("tóc ngắn", "HAIR_LENGTH", "short"),
        ("short hair", "HAIR_LENGTH", "short"),
        ("khuôn mặt khác", "FACE", "different"),
        ("different face", "FACE", "different"),
        ("face swap", "FACE", "swap"),
        ("identity blending", "FACE", "blend"),
        ("identity averaging", "FACE", "average"),
        ("trưởng thành", "AGE", "adult"),
        ("adult appearance", "AGE", "adult"),
        ("gemini decide", "AUTHORITY", "model"),
        ("model decide", "AUTHORITY", "model"),
        ("model chọn mặt", "AUTHORITY", "model"),
        ("ai tự vẽ mặt", "AUTHORITY", "model"),
        ("3/4 view", "CAMERA", "three_quarter"),
        ("góc thấp", "CAMERA", "low_angle"),
        ("low angle", "CAMERA", "low_angle"),
        ("high angle", "CAMERA", "high_angle"),
    ];

    private static bool IsHairIdentityChange(string requested, IReadOnlyList<string> allowed, IReadOnlyList<string> invariant, IReadOnlyList<string> forbidden)
    {
        if (allowed.Any(a => a.Contains(requested, StringComparison.OrdinalIgnoreCase) || requested.Contains(a, StringComparison.OrdinalIgnoreCase)))
            return false;
        var change = requested is "long" or "medium" or "shoulder_length";
        if (!change) return forbidden.Any(f => MatchesForbidden(new AttributeRequest("HAIR_LENGTH", requested, "text"), f));
        return forbidden.Any(f => f.Contains("hair", StringComparison.OrdinalIgnoreCase) || f.Contains("tóc", StringComparison.OrdinalIgnoreCase))
            || invariant.Any(i => i.Contains("hairstyle", StringComparison.OrdinalIgnoreCase) || i.Contains("silhouette", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesForbidden(AttributeRequest req, string forbidden)
    {
        var f = forbidden.ToLowerInvariant();
        var v = req.Value.ToLowerInvariant();
        if (f.Contains(v) || v.Contains(f)) return true;
        if (req.Attribute is "HAIR" or "HAIR_LENGTH" && (f.Contains("hair identity") || f.Contains("different hair") || f.Contains("hairstyle")))
            return v is "long" or "medium" or "shoulder_length" or "different";
        if (req.Attribute == "AGE" && (f.Contains("adult appearance") || f.Equals("adult") || f.Contains("trưởng thành")))
            return v is "adult";
        if (req.Attribute == "AGE" && f.Contains("different age"))
            return false;
        if (req.Attribute == "FACE" && (f.Contains("face") || f.Contains("blend") || f.Contains("swap")))
            return true;
        if (req.Attribute == "ACCESSORY" && f.Contains(v)) return true;
        return false;
    }

    private static string GroupFor(string attribute) => attribute.ToUpperInvariant() switch
    {
        "AGE" => "age",
        "HAIR" or "HAIR_LENGTH" => "hair",
        "FACE" => "face",
        "EYES" => "eyes",
        "STYLE" => "style",
        "PROPORTION" or "BODY" => "body",
        "EXPRESSION" => "expression",
        "ACCESSORY" => "style",
        _ => attribute.ToLowerInvariant(),
    };

    private static string HairAuthority(JsonElement hair)
    {
        if (hair.TryGetProperty("hair_length", out var n))
            return Compact(n);
        var inv = ReadStringList(hair, "invariant");
        return inv.Count > 0 ? string.Join(',', inv) : Compact(hair);
    }

    private static bool LooksLikeInheritedAuthority(string raw)
    {
        var t = raw.TrimStart();
        return t.StartsWith('{')
            && (t.Contains("\"invariant\"", StringComparison.Ordinal)
                || t.Contains("\"allowed\"", StringComparison.Ordinal)
                || t.Contains("\"forbidden\"", StringComparison.Ordinal)
                || t.Contains("\"age_appearance\"", StringComparison.Ordinal));
    }

    private static bool LooksAllowedVariation(string value, IReadOnlyList<string>? allowed)
    {
        var v = value.ToLowerInvariant();
        allowed ??= [];
        if (allowed.Any(a => v.Contains(a, StringComparison.OrdinalIgnoreCase) || a.Contains(v, StringComparison.OrdinalIgnoreCase)))
            return true;
        return v.Contains("3/4") || v.Contains("low angle") || v.Contains("high angle") || v.Contains("góc thấp")
            || v.Contains("lighting") || v.Contains("pose") || v.Contains("camera") || v.Contains("smile")
            || v.Contains("sad") || v.Contains("motion") || v.Contains("partial occlusion") || v.Contains("environment");
    }

    private static bool ContainsModelAuthority(string? prompt, JsonElement? shot)
    {
        var t = (prompt ?? "") + " " + (shot is { ValueKind: JsonValueKind.Object } s ? s.GetRawText() : "");
        return t.Contains("gemini decide", StringComparison.OrdinalIgnoreCase)
            || t.Contains("model decide", StringComparison.OrdinalIgnoreCase)
            || t.Contains("model chọn mặt", StringComparison.OrdinalIgnoreCase)
            || t.Contains("ai tự vẽ mặt", StringComparison.OrdinalIgnoreCase);
    }

    public static string? ExtractAuthoritativeAge(JsonElement dna)
    {
        if (dna.ValueKind != JsonValueKind.Object) return null;
        var group = ReadGroup(dna, "age");
        if (group.ValueKind == JsonValueKind.Object)
        {
            foreach (var inv in ReadStringList(group, "invariant"))
            {
                var token = ExtractAgeToken(inv);
                if (token is not null) return token;
            }
            if (group.TryGetProperty("age_appearance", out var appearance))
            {
                var raw = appearance.ValueKind == JsonValueKind.Object && appearance.TryGetProperty("value", out var value)
                    ? FullText(value)
                    : FullText(appearance);
                var token = ExtractAgeToken(raw);
                if (token is not null) return token;
            }
        }
        if (dna.TryGetProperty("identityCore", out var core) && core.ValueKind == JsonValueKind.Object
            && core.TryGetProperty("ageAppearance", out var coreAge))
        {
            var token = ExtractAgeToken(FullText(coreAge));
            if (token is not null) return token;
        }
        return ExtractAgeToken(FullText(group));
    }

    public static string? ExtractAgeToken(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var years = Regex.Match(text, @"approximately\s+(\d{1,2})\s+years|\b(\d{1,2})\s*(?:tuổi|years old)\b", RegexOptions.IgnoreCase);
        if (years.Success)
            return years.Groups[1].Success ? years.Groups[1].Value : years.Groups[2].Value;
        if (text.Trim().Equals("adult", StringComparison.OrdinalIgnoreCase)
            || text.Trim().Equals("trưởng thành", StringComparison.OrdinalIgnoreCase))
            return "adult";
        if (Regex.IsMatch(text.Trim(), @"^\d{1,2}$"))
            return text.Trim();
        return null;
    }

    private static string FullText(JsonElement n)
    {
        if (n.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return "";
        if (n.ValueKind == JsonValueKind.String) return n.GetString() ?? "";
        if (n.ValueKind == JsonValueKind.Number) return n.ToString();
        return n.GetRawText();
    }

    private static string GroupText(JsonElement dna, string group) => FullText(ReadGroup(dna, group));

    private static JsonElement ReadGroup(JsonElement spec, string group)
    {
        if (spec.ValueKind != JsonValueKind.Object) return default;
        if (group == "proportion") group = "body";
        return spec.TryGetProperty(group, out var n) ? n : default;
    }

    private static JsonElement ReadNode(JsonElement spec, string key) =>
        spec.ValueKind == JsonValueKind.Object && spec.TryGetProperty(key, out var n) ? n : default;

    private static string ReadIdentity(JsonElement spec, string attr)
    {
        if (spec.ValueKind != JsonValueKind.Object) return "";
        if (spec.TryGetProperty("identity", out var ident) && ident.ValueKind == JsonValueKind.Object && ident.TryGetProperty(attr, out var n))
            return Compact(n);
        if (spec.TryGetProperty(attr, out var direct)) return Compact(direct);
        if (attr == "age" && spec.TryGetProperty("ageProportion", out var ap) && ap.ValueKind == JsonValueKind.Object && ap.TryGetProperty("age", out var age))
            return Compact(age);
        return "";
    }

    private static IReadOnlyList<string> ReadStringList(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return [];
        return arr.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? "" : x.GetRawText()).Where(s => s.Length > 0).ToList();
    }

    private static bool IdentityNodeConflicts(JsonElement requested, JsonElement authoritative, string attr)
    {
        var req = Compact(requested);
        var auth = Compact(authoritative);
        if (string.IsNullOrWhiteSpace(req) || string.IsNullOrWhiteSpace(auth)) return false;
        if (string.Equals(req, auth, StringComparison.OrdinalIgnoreCase)) return false;
        if (LooksAllowedVariation(req, [])) return false;
        if (attr == "age")
        {
            var a = ExtractAgeToken(req);
            var b = ExtractAgeToken(auth);
            if (a is not null && b is not null) return a != b;
        }
        if (auth.Contains(req, StringComparison.OrdinalIgnoreCase)) return false;
        return !string.Equals(Canonical(req), Canonical(auth), StringComparison.Ordinal);
    }

    private static bool HasIdentityOverride(JsonElement ageNode, string key) =>
        ageNode.TryGetProperty(key, out var n) && n.ValueKind is JsonValueKind.Object or JsonValueKind.String or JsonValueKind.Number;

    private static bool MultiPersonSeparable(JsonElement shot)
    {
        if (!shot.TryGetProperty("interaction", out var n) || n.ValueKind != JsonValueKind.Object)
            return true;
        var people = n.TryGetProperty("peopleCount", out var p) && p.TryGetInt32(out var c) ? c : 1;
        if (people <= 1) return true;
        var distinct = n.TryGetProperty("identitiesSeparable", out var s) && s.ValueKind == JsonValueKind.True;
        var noBlend = n.TryGetProperty("noBlend", out var b) && b.ValueKind == JsonValueKind.True;
        var noSwap = n.TryGetProperty("noSwap", out var w) && w.ValueKind == JsonValueKind.True;
        return distinct && noBlend && noSwap;
    }

    private static bool OcclusionCreatesNewFace(JsonElement shot) =>
        shot.TryGetProperty("occlusion", out var n) && n.ValueKind == JsonValueKind.Object
        && n.TryGetProperty("inventsFace", out var f) && f.ValueKind == JsonValueKind.True;

    private static string RequestSurface(JsonElement node, string? key = null)
    {
        if (key is "forbidden" or "forbiddenVariation" or "forbiddenConditions" or "forbiddenProduction" or "forbiddenProductionConditions" or "negative")
            return "";
        if (node.ValueKind == JsonValueKind.String) return node.GetString() ?? "";
        if (node.ValueKind == JsonValueKind.Object)
            return string.Join(' ', node.EnumerateObject().Select(p => RequestSurface(p.Value, p.Name)));
        if (node.ValueKind == JsonValueKind.Array)
            return string.Join(' ', node.EnumerateArray().Select(x => RequestSurface(x, key)));
        return "";
    }

    private static Guid? ReadGuid(JsonElement spec, string key)
    {
        if (spec.ValueKind != JsonValueKind.Object || !spec.TryGetProperty(key, out var n)) return null;
        if (n.ValueKind == JsonValueKind.String && Guid.TryParse(n.GetString(), out var g)) return g;
        return null;
    }

    private static bool ContainsInsensitive(string hay, string needle) =>
        hay.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private static string Compact(JsonElement n)
    {
        if (n.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return "";
        if (n.ValueKind == JsonValueKind.String) return n.GetString() ?? "";
        if (n.ValueKind == JsonValueKind.Number) return n.ToString();
        var raw = n.GetRawText();
        return raw.Length > 160 ? raw[..160] : raw;
    }

    private static string CompactLimit(string s) => s.Length > 80 ? s[..80] : s;

    private static string Canonical(string s) => Regex.Replace(s.Trim().ToLowerInvariant(), @"\s+", " ");
}
