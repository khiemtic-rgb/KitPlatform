using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_PROMPT_COMPILER_V1 — compile or reject. No auto-fix. No provider. No generation.</summary>
public static class ProductionPromptCompilerRules
{
    public const string DocumentId = "PRODUCTION_PROMPT_COMPILER_V1";
    public const string Project = "FAMIXA";
    public const string Version = "V1";

    public static readonly string[] Statuses = ["NOT_READY", "BLOCKED", "COMPILED", "SUPERSEDED"];
    public static readonly string[] ForbiddenStatuses = ["GENERATED", "IMAGE_READY", "VIDEO_READY"];
    public static readonly string[] PromptSections =
    [
        "IDENTITY", "SCENE", "STORY BEAT", "CHARACTER STATE", "WARDROBE", "PROPS",
        "COMPOSITION", "LIGHTING", "MOTION INTENT", "CONTINUITY", "PRODUCTION REQUIREMENTS",
        "MANDATORY CONSTRAINTS", "FORBIDDEN CONSTRAINTS",
    ];

    public static readonly string[] ProviderTokens =
    [
        "gemini", "runway", "gen4_turbo", "gemini-2.5-flash-image", "gemini_model", "runway_model",
    ];

    public static readonly string[] InjectionPhrases =
    [
        "ignore the character dna",
        "let the ai choose a different face",
        "make minh 14",
        "change his hairstyle",
        "change her hairstyle",
        "choose a different face",
    ];

    public static readonly string[] InventedTokens =
        ["storm", "rainy", "happy ending", "hug and apologize", "plastic skin", "bad anatomy", "extra fingers"];

    public static bool AutoFix() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY";
    public static bool TouchesGolden(string? path) =>
        !string.IsNullOrWhiteSpace(path) && path.Contains("SH01-01", StringComparison.OrdinalIgnoreCase)
        && path.Contains("GOLDEN", StringComparison.OrdinalIgnoreCase);

    public sealed record Block(
        string Status,
        string Code,
        string Source,
        string Attribute,
        string? Requested,
        string? Authoritative,
        string Message);

    public sealed record CompileInput(
        string ContractStatus,
        JsonElement Payload,
        string ContractSha256,
        string LiveContractSha256,
        string MasterSha256,
        string LiveMasterSha256,
        string DnaSha256,
        string LiveDnaSha256,
        string PrpSha256,
        string LivePrpSha256,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool GovernancePass,
        IReadOnlyList<CharacterIdentityGovernanceRules.Conflict> GovernanceConflicts,
        string CharacterId,
        Guid? ContractId,
        Guid ShotId,
        string ContractVersion,
        Guid? MasterId,
        Guid? DnaId,
        Guid? PrpId,
        IReadOnlyList<string> AuthorityForbidden);

    public sealed record CompileOutput(
        string Status,
        string? Prompt,
        IReadOnlyList<string> NegativeConstraints,
        string? PromptSha256,
        IReadOnlyList<Block> Blocks,
        bool Generation = false);

    public static bool IsApproved(string? status) =>
        string.Equals(status, "DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase);

    public static bool CanOverwrite(string? status) =>
        !string.Equals(status, "COMPILED", StringComparison.OrdinalIgnoreCase);

    public static string NextVersion(string? current)
    {
        var v = (current ?? "V1").Trim().ToUpperInvariant();
        var n = Regex.Match(v, @"^V(\d+)$");
        return n.Success && int.TryParse(n.Groups[1].Value, out var i) ? $"V{i + 1}" : "V2";
    }

    public static bool FingerprintsReady(CompileInput input) =>
        CharacterIdentityGovernanceRules.ShaExists(input.MasterSha256)
        && CharacterIdentityGovernanceRules.ShaExists(input.DnaSha256)
        && CharacterIdentityGovernanceRules.ShaExists(input.PrpSha256)
        && CharacterIdentityGovernanceRules.ShaExists(input.ContractSha256);

    public static CompileOutput Evaluate(CompileInput input)
    {
        var blocks = new List<Block>();
        if (!FingerprintsReady(input)
            || !CharacterIdentityGovernanceRules.ShaExists(input.LiveMasterSha256)
            || !CharacterIdentityGovernanceRules.ShaExists(input.LiveDnaSha256)
            || !CharacterIdentityGovernanceRules.ShaExists(input.LivePrpSha256)
            || !CharacterIdentityGovernanceRules.ShaExists(input.LiveContractSha256))
        {
            blocks.Add(NotReady("fingerprint", "missing", "master+dna+prp+contract sha256", "PROMPT_COMPILER_NOT_READY: thiếu fingerprint."));
            return Blocked("NOT_READY", blocks);
        }
        if (!IsApproved(input.ContractStatus))
        {
            blocks.Add(new Block("BLOCKED", "PROMPT_COMPILER_NOT_READY", "CONTRACT", "status",
                input.ContractStatus, "DIRECTOR_APPROVED", "Contract phải DIRECTOR_APPROVED trước khi compile."));
            return Blocked("BLOCKED", blocks);
        }
        if (!input.MasterLocked)
            blocks.Add(NotReady("master", input.MasterLocked ? "LOCKED" : "MISSING", "LOCKED", "PROMPT_COMPILER_NOT_READY: missing Master."));
        if (!input.DnaLocked)
            blocks.Add(NotReady("dna", input.DnaLocked ? "LOCKED" : "MISSING", "LOCKED", "PROMPT_COMPILER_NOT_READY: missing DNA."));
        if (!input.PrpLocked)
            blocks.Add(NotReady("prp", input.PrpLocked ? "LOCKED" : "MISSING", "LOCKED", "PROMPT_COMPILER_NOT_READY: missing PRP."));
        if (!CharacterIdentityGovernanceRules.SameSha(input.MasterSha256, input.LiveMasterSha256))
            blocks.Add(Sha("MASTER", input.MasterSha256, input.LiveMasterSha256));
        if (!CharacterIdentityGovernanceRules.SameSha(input.DnaSha256, input.LiveDnaSha256))
            blocks.Add(Sha("CHARACTER_DNA", input.DnaSha256, input.LiveDnaSha256));
        if (!CharacterIdentityGovernanceRules.SameSha(input.PrpSha256, input.LivePrpSha256))
            blocks.Add(Sha("PRODUCTION_REFERENCE_PACK", input.PrpSha256, input.LivePrpSha256));
        if (!CharacterIdentityGovernanceRules.SameSha(input.ContractSha256, input.LiveContractSha256))
            blocks.Add(Sha("CONTRACT", input.ContractSha256, input.LiveContractSha256));
        if (blocks.Count > 0)
            return Blocked("BLOCKED", blocks);

        if (!input.GovernancePass || input.GovernanceConflicts.Count > 0)
        {
            foreach (var c in input.GovernanceConflicts)
                blocks.Add(new Block("BLOCKED", "PROMPT_COMPILER_IDENTITY_CONFLICT", c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message));
            if (blocks.Count == 0)
                blocks.Add(new Block("BLOCKED", "PROMPT_COMPILER_IDENTITY_CONFLICT", "GOVERNANCE", "identity", null, null, "Identity Governance FAIL. Không compile."));
            return Blocked("BLOCKED", blocks);
        }

        var issues = ProductionShotContractRules.Validate(input.Payload);
        if (issues.Count > 0)
        {
            foreach (var i in issues)
                blocks.Add(new Block("BLOCKED", "PROMPT_COMPILER_BLOCKED", "CONTRACT", i.Attribute, null, null, i.Message));
            return Blocked("BLOCKED", blocks);
        }

        if (ContainsInjection(input.Payload))
        {
            blocks.Add(new Block("BLOCKED", "PROMPT_COMPILER_BLOCKED", "CONTRACT", "injection", "prompt override", "DNA",
                "Prompt injection / identity override. Không compile."));
            return Blocked("BLOCKED", blocks);
        }

        var prompt = CompilePrompt(input.Payload, input.CharacterId);
        if (ContainsProviderSyntax(prompt))
        {
            blocks.Add(new Block("BLOCKED", "PROMPT_COMPILER_BLOCKED", "COMPILER", "prompt", "provider", "model-agnostic",
                "Compiled prompt must stay model-agnostic."));
            return Blocked("BLOCKED", blocks);
        }

        var negatives = CompileNegatives(input.Payload, input.AuthorityForbidden);
        var sha = HashPrompt(prompt);
        return new CompileOutput("COMPILED", prompt, negatives, sha, [], false);
    }

    public static string CompilePrompt(JsonElement payload, string characterId)
    {
        var sections = new List<string>();
        var id = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId);
        if (id.Length == 0 && payload.ValueKind == JsonValueKind.Object)
            id = CharacterIdentityGovernanceRules.NormalizeCharacterId(ReadString(payload, "characterId"));
        sections.Add(Join("IDENTITY",
            $"Use the locked Master Reference for {id}.",
            "Preserve the locked Character DNA identity.",
            "Use the approved Production Reference Pack.",
            "Do not alter facial identity, age, hairstyle, proportions, or other locked identity attributes."));

        var scene = Obj(payload, "scene");
        var sceneLines = Lines(
            Fact("location", ReadString(scene, "location")),
            Fact("time", ReadString(scene, "time")),
            Fact("environment", ReadString(scene, "environment")));
        if (sceneLines.Length > 0) sections.Add(Join("SCENE", sceneLines));

        var story = Obj(payload, "story");
        var storyLines = Lines(
            Fact("beat", ReadString(story, "beat")),
            Fact("action", ReadString(story, "action")),
            Fact("objective", ReadString(story, "objective")));
        if (storyLines.Length > 0) sections.Add(Join("STORY BEAT", storyLines));

        var character = Obj(payload, "character");
        var stateLines = Lines(
            Fact("expression", ReadString(character, "expression")),
            Fact("pose", ReadString(character, "pose")),
            Fact("gaze", ReadString(character, "gaze")),
            Fact("movement", ReadString(character, "movement")));
        if (stateLines.Length > 0) sections.Add(Join("CHARACTER STATE", stateLines));

        var wardrobe = Obj(payload, "wardrobe");
        var wardrobeLines = Lines(
            Fact("description", ReadString(wardrobe, "description")),
            Fact("continuity", ReadString(wardrobe, "continuity")));
        if (wardrobeLines.Length > 0) sections.Add(Join("WARDROBE", wardrobeLines));

        var propLines = PropLines(payload);
        if (propLines.Length > 0) sections.Add(Join("PROPS", propLines));

        var composition = Obj(payload, "composition");
        var compLines = Lines(
            Fact("framing", ReadString(composition, "framing")),
            Fact("cameraAngle", ReadString(composition, "cameraAngle")),
            Fact("cameraDistance", ReadString(composition, "cameraDistance")),
            Fact("cameraPosition", ReadString(composition, "cameraPosition")),
            Fact("subjectPosition", ReadString(composition, "subjectPosition")),
            Fact("spatialRelationship", ReadString(composition, "spatialRelationship")));
        if (compLines.Length > 0) sections.Add(Join("COMPOSITION", compLines));

        var lighting = ReadString(Obj(payload, "lighting"), "description");
        if (lighting.Length > 0) sections.Add(Join("LIGHTING", lighting));

        var motion = Obj(payload, "motion");
        var motionLines = Lines(
            Fact("intent", ReadString(motion, "intent")),
            Fact("cameraMotion", ReadString(motion, "cameraMotion")),
            Fact("subjectMotion", ReadString(motion, "subjectMotion")));
        if (motionLines.Length > 0) sections.Add(Join("MOTION INTENT", motionLines));

        var continuity = Obj(payload, "continuity");
        var contLines = new List<string>();
        var prev = ReadString(continuity, "previousShotId");
        if (prev.Length > 0) contLines.Add($"previousShotId: {prev}");
        foreach (var rule in StringList(continuity, "rules"))
            contLines.Add(rule);
        if (contLines.Count > 0)
            sections.Add(Join("CONTINUITY", ["Preserve all locked identity and continuity constraints.", .. contLines]));

        var production = Obj(payload, "production");
        var prodLines = Lines(
            Fact("aspectRatio", ReadString(production, "aspectRatio")),
            Fact("resolution", ReadString(production, "resolution")),
            Fact("frameRate", ReadString(production, "frameRate")));
        var duration = DurationText(payload);
        if (duration.Length > 0) prodLines = [.. prodLines, duration];
        if (prodLines.Length > 0) sections.Add(Join("PRODUCTION REQUIREMENTS", prodLines));

        var mandatory = StringList(Obj(payload, "constraints"), "mandatory");
        if (mandatory.Count > 0) sections.Add(Join("MANDATORY CONSTRAINTS", mandatory.ToArray()));

        var forbidden = StringList(Obj(payload, "constraints"), "forbidden");
        if (forbidden.Count > 0) sections.Add(Join("FORBIDDEN CONSTRAINTS", forbidden.ToArray()));

        return CanonicalPrompt(string.Join("\n\n", sections));
    }

    public static IReadOnlyList<string> CompileNegatives(JsonElement payload, IReadOnlyList<string> authorityForbidden)
    {
        var list = new List<string>();
        foreach (var item in StringList(Obj(payload, "constraints"), "forbidden"))
            if (!list.Contains(item, StringComparer.OrdinalIgnoreCase))
                list.Add(item);
        foreach (var item in authorityForbidden)
        {
            var t = (item ?? "").Trim();
            if (t.Length == 0) continue;
            if (t.Contains("bad anatomy", StringComparison.OrdinalIgnoreCase)
                || t.Contains("extra fingers", StringComparison.OrdinalIgnoreCase)
                || t.Contains("plastic skin", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!list.Contains(t, StringComparer.OrdinalIgnoreCase))
                list.Add(t);
        }
        return list;
    }

    public static string CanonicalPrompt(string prompt)
    {
        var lines = prompt.Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n')
            .Select(l => l.TrimEnd());
        return string.Join('\n', lines).Trim();
    }

    public static string HashPrompt(string prompt) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(CanonicalPrompt(prompt)));

    public static bool ContainsProviderSyntax(string? text)
    {
        var t = text ?? "";
        return ProviderTokens.Any(p => t.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ContainsInjectionPhrases(string? text) =>
        InjectionPhrases.Any(p => (text ?? "").Contains(p, StringComparison.OrdinalIgnoreCase));

    public static bool ContainsInjection(JsonElement payload)
    {
        var blob = payload.ValueKind == JsonValueKind.Object ? payload.GetRawText() : "";
        if (ContainsInjectionPhrases(blob))
            return true;
        return CharacterIdentityGovernanceRules.CollectRequests(default, blob)
            .Any(r => r.Attribute is "AUTHORITY" or "FACE" && r.Value is "model" or "different" or "swap");
    }

    public static bool Invents(string prompt, string token) =>
        prompt.Contains(token, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ExtractDnaForbidden(JsonElement dna)
    {
        var list = new List<string>();
        if (dna.ValueKind != JsonValueKind.Object) return list;
        foreach (var name in new[] { "forbiddenVariation" })
            list.AddRange(StringList(dna, name));
        foreach (var group in CharacterIdentityGovernanceRules.IdentityGroups)
        {
            if (!dna.TryGetProperty(group, out var g) || g.ValueKind != JsonValueKind.Object) continue;
            list.AddRange(StringList(g, "forbidden"));
        }
        return list.Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static CompileOutput Blocked(string status, List<Block> blocks) =>
        new(status, null, [], null, blocks, false);

    private static Block NotReady(string attribute, string? requested, string? authoritative, string message) =>
        new("NOT_READY", "PROMPT_COMPILER_NOT_READY", "AUTHORITY", attribute, requested, authoritative, message);

    private static Block Sha(string source, string? requested, string? authoritative) =>
        new("BLOCKED", "PROMPT_COMPILER_NOT_READY", source, "sha256", requested, authoritative,
            $"{source} SHA mismatch. Không compile. Không auto-fix.");

    private static string Join(string header, params string[] body) =>
        header + "\n" + string.Join('\n', body.Where(s => s.Length > 0));

    private static string[] Lines(params string[] items) => items.Where(s => s.Length > 0).ToArray();

    private static string Fact(string name, string value) =>
        value.Length == 0 ? "" : $"{name}: {value}";

    private static string[] PropLines(JsonElement payload)
    {
        if (!payload.TryGetProperty("props", out var props) || props.ValueKind != JsonValueKind.Array)
            return [];
        var lines = new List<string>();
        foreach (var p in props.EnumerateArray())
        {
            if (p.ValueKind != JsonValueKind.Object) continue;
            var id = ReadString(p, "id");
            var name = ReadString(p, "name");
            var state = ReadString(p, "state");
            if (id.Length == 0 && name.Length == 0 && state.Length == 0) continue;
            lines.Add(string.Join(", ", new[]
            {
                id.Length > 0 ? $"id={id}" : "",
                name.Length > 0 ? $"name={name}" : "",
                state.Length > 0 ? $"state={state}" : "",
            }.Where(s => s.Length > 0)));
        }
        return lines.ToArray();
    }

    private static string DurationText(JsonElement payload)
    {
        if (!payload.TryGetProperty("timing", out var t) || t.ValueKind != JsonValueKind.Object)
            return "";
        if (!t.TryGetProperty("durationSeconds", out var n)) return "";
        if (n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out var d) && d > 0)
            return $"durationSeconds: {d}";
        if (n.ValueKind == JsonValueKind.String && double.TryParse(n.GetString(), out d) && d > 0)
            return $"durationSeconds: {d}";
        return "";
    }

    private static JsonElement Obj(JsonElement payload, string name)
    {
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Object)
            return n;
        return JsonSerializer.SerializeToElement(new { });
    }

    private static IReadOnlyList<string> StringList(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return [];
        if (n.ValueKind == JsonValueKind.Array)
            return n.EnumerateArray()
                .Select(i => i.ValueKind == JsonValueKind.String ? (i.GetString() ?? "").Trim() : i.GetRawText().Trim('"'))
                .Where(s => s.Length > 0)
                .ToList();
        var one = n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim() : "";
        return one.Length == 0 ? [] : [one];
    }

    private static string ReadString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return "";
        return n.ValueKind == JsonValueKind.String
            ? (n.GetString() ?? "").Trim()
            : n.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? "" : n.GetRawText().Trim('"');
    }
}
