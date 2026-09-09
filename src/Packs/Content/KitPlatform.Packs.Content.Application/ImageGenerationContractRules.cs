using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_IMAGE_GENERATION_CONTRACT_V1 — gate only. No provider. No pixels.</summary>
public static class ImageGenerationContractRules
{
    public const string DocumentId = "PRODUCTION_IMAGE_GENERATION_CONTRACT_V1";
    public const string Project = "FAMIXA";
    public const string Version = "V1";

    public static readonly string[] Statuses =
        ["DRAFT", "VALIDATED", "DIRECTOR_APPROVED", "REJECTED", "SUPERSEDED"];

    public static readonly string[] ProviderTokens = ProductionPromptCompilerRules.ProviderTokens;

    public static bool AutoFix() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY";
    public static bool TouchesGolden(string? path) =>
        ProductionPromptCompilerRules.TouchesGolden(path);

    public sealed record Block(
        string Status,
        string Code,
        string Source,
        string Attribute,
        string? Requested,
        string? Authoritative,
        string Message);

    public sealed record GateInput(
        string CharacterId,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool GovernancePass,
        IReadOnlyList<CharacterIdentityGovernanceRules.Conflict> GovernanceConflicts,
        string ShotContractStatus,
        string PromptStatus,
        string MasterSha256,
        string LiveMasterSha256,
        string DnaSha256,
        string LiveDnaSha256,
        string PrpSha256,
        string LivePrpSha256,
        string ShotContractSha256,
        string LiveShotContractSha256,
        string PromptSha256,
        string LivePromptSha256,
        string PromptProvenanceMasterSha,
        string PromptProvenanceDnaSha,
        string PromptProvenancePrpSha,
        string PromptProvenanceContractSha,
        string? PromptText,
        JsonElement Payload);

    public sealed record GateOutput(
        string Status,
        IReadOnlyList<Block> Blocks,
        string? ContractSha256,
        bool Generation = false);

    public static bool IsApproved(string? status) =>
        string.Equals(status, "DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase);

    public static bool CanEdit(string? status) =>
        status is "DRAFT" or "VALIDATED" or "REJECTED" || string.IsNullOrWhiteSpace(status);

    public static string NextVersion(string? current)
    {
        var v = (current ?? "V1").Trim().ToUpperInvariant();
        var n = Regex.Match(v, @"^V(\d+)$");
        return n.Success && int.TryParse(n.Groups[1].Value, out var i) ? $"V{i + 1}" : "V2";
    }

    public static GateOutput Evaluate(GateInput input)
    {
        var blocks = new List<Block>();
        if (string.IsNullOrWhiteSpace(CharacterIdentityGovernanceRules.NormalizeCharacterId(input.CharacterId)))
            blocks.Add(NotReady("characterId", "", "required", "character_id is required. No identity fallback."));
        if (!input.MasterLocked)
            blocks.Add(NotReady("master", "MISSING", "LOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY: missing Master."));
        if (!input.DnaLocked)
            blocks.Add(NotReady("dna", "MISSING", "LOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY: missing DNA."));
        if (!input.PrpLocked)
            blocks.Add(NotReady("prp", "MISSING", "LOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY: missing PRP."));
        if (!ProductionPromptCompilerRules.IsApproved(input.ShotContractStatus)
            && !IsApproved(input.ShotContractStatus))
            blocks.Add(NotReady("shotContract", input.ShotContractStatus, "DIRECTOR_APPROVED", "Shot Contract must be DIRECTOR_APPROVED."));
        if (!string.Equals(input.PromptStatus, "COMPILED", StringComparison.OrdinalIgnoreCase))
            blocks.Add(NotReady("prompt", input.PromptStatus, "COMPILED", "Production Prompt must be COMPILED."));
        AddSha(blocks, "MASTER", input.MasterSha256, input.LiveMasterSha256);
        AddSha(blocks, "CHARACTER_DNA", input.DnaSha256, input.LiveDnaSha256);
        AddSha(blocks, "PRODUCTION_REFERENCE_PACK", input.PrpSha256, input.LivePrpSha256);
        AddSha(blocks, "SHOT_CONTRACT", input.ShotContractSha256, input.LiveShotContractSha256);
        AddSha(blocks, "PROMPT", input.PromptSha256, input.LivePromptSha256);
        if (!CharacterIdentityGovernanceRules.SameSha(input.PromptProvenanceMasterSha, input.LiveMasterSha256)
            || !CharacterIdentityGovernanceRules.SameSha(input.PromptProvenanceDnaSha, input.LiveDnaSha256)
            || !CharacterIdentityGovernanceRules.SameSha(input.PromptProvenancePrpSha, input.LivePrpSha256)
            || !CharacterIdentityGovernanceRules.SameSha(input.PromptProvenanceContractSha, input.LiveShotContractSha256))
            blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY", "PROMPT", "provenance", "mismatch", "match",
                "Prompt provenance mismatch. Không auto-compile. Không auto-fix."));
        if (!input.GovernancePass || input.GovernanceConflicts.Count > 0)
        {
            foreach (var c in input.GovernanceConflicts)
                blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_IDENTITY_CONFLICT", c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message));
            if (blocks.All(b => b.Code != "IMAGE_GENERATION_CONTRACT_IDENTITY_CONFLICT"))
                blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_IDENTITY_CONFLICT", "GOVERNANCE", "identity", null, null, "Identity Governance FAIL."));
        }
        // Attack phrases only. DNA/PRP forbidden echoes ("face swap", "different face identity") are constraints, not injection.
        if (ProductionPromptCompilerRules.ContainsInjectionPhrases(input.PromptText)
            || ProductionPromptCompilerRules.ContainsInjectionPhrases(
                input.Payload.ValueKind == JsonValueKind.Undefined ? "" : input.Payload.GetRawText()))
            blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_IDENTITY_CONFLICT", "PROMPT", "injection", "override", "DNA",
                "Prompt injection / identity override. Không tạo Contract."));
        if (ContainsProvider(input.Payload) || ProductionPromptCompilerRules.ContainsProviderSyntax(input.PromptText)
            || ProductionPromptCompilerRules.ContainsProviderSyntax(input.Payload.GetRawText()))
            blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_INVALID", "PROVIDER", "provider", "specific", "EXTERNAL",
                "Image Generation Contract must stay provider-independent."));
        if (blocks.Count > 0)
            return new GateOutput("BLOCKED", blocks, null, false);
        return new GateOutput("PASS", [], HashCanonical(input.Payload), false);
    }

    public static JsonElement BuildPayload(
        string characterId,
        Guid shotId,
        Guid? masterId, string masterSha,
        Guid? dnaId, string dnaSha,
        Guid? prpId, string prpSha,
        Guid? shotContractId, string shotContractSha,
        Guid? promptId, string promptSha,
        JsonElement shotContractPayload,
        IReadOnlyList<string> authorityForbidden,
        string? qualityPolicy = null,
        string? backgroundPolicy = null,
        string? outputFormat = null)
    {
        var production = Obj(shotContractPayload, "production");
        var constraints = Obj(shotContractPayload, "constraints");
        var derived = StringList(constraints, "forbidden").Concat(authorityForbidden)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var flags = new List<string> { "identityMustRemainLocked", "noIdentityAlteration" };
        if (derived.Any(s => s.Contains("age", StringComparison.OrdinalIgnoreCase) || s.Contains("identity", StringComparison.OrdinalIgnoreCase)))
            flags.Add("noAgeChange");
        if (derived.Any(s => s.Contains("hair", StringComparison.OrdinalIgnoreCase) || s.Contains("tóc", StringComparison.OrdinalIgnoreCase)))
            flags.Add("noHairChange");
        if (derived.Any(s => s.Contains("wardrobe", StringComparison.OrdinalIgnoreCase)))
            flags.Add("noWardrobeContinuityBreak");
        if (derived.Any(s => s.Contains("person", StringComparison.OrdinalIgnoreCase) || s.Contains("additional", StringComparison.OrdinalIgnoreCase)))
            flags.Add("noAdditionalPerson");

        var req = new Dictionary<string, object?>
        {
            ["aspectRatio"] = ReadString(production, "aspectRatio"),
            ["resolution"] = ReadString(production, "resolution"),
            ["fps"] = "NOT_APPLICABLE",
        };
        if (!string.IsNullOrWhiteSpace(outputFormat)) req["outputFormat"] = outputFormat.Trim();
        if (!string.IsNullOrWhiteSpace(qualityPolicy)) req["qualityPolicy"] = qualityPolicy.Trim();
        if (!string.IsNullOrWhiteSpace(backgroundPolicy)) req["backgroundPolicy"] = backgroundPolicy.Trim();

        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["shotId"] = shotId.ToString(),
            ["characterId"] = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId),
            ["providerPolicy"] = new Dictionary<string, object?>
            {
                ["providerCapability"] = "IMAGE_GENERATION",
                ["providerSelection"] = "EXTERNAL",
            },
            ["imageRequirements"] = req,
            ["inputReferences"] = new object[]
            {
                new { id = masterId?.ToString() ?? "", sha256 = masterSha, role = "MASTER_REFERENCE" },
                new { id = prpId?.ToString() ?? "", sha256 = prpSha, role = "PRP_REFERENCE" },
                new { id = promptId?.ToString() ?? "", sha256 = promptSha, role = "COMPILED_PROMPT" },
            },
            ["generationConstraints"] = new Dictionary<string, object?>
            {
                ["flags"] = flags,
                ["forbidden"] = derived,
            },
            ["provenance"] = new Dictionary<string, object?>
            {
                ["masterId"] = masterId?.ToString(),
                ["masterSha256"] = masterSha,
                ["dnaId"] = dnaId?.ToString(),
                ["dnaSha256"] = dnaSha,
                ["prpId"] = prpId?.ToString(),
                ["prpSha256"] = prpSha,
                ["shotContractId"] = shotContractId?.ToString(),
                ["shotContractSha256"] = shotContractSha,
                ["promptId"] = promptId?.ToString(),
                ["promptSha256"] = promptSha,
            },
        });
    }

    public static string HashCanonical(JsonElement payload) =>
        KitVideoIntegrityRules.Sha256Hex(Encoding.UTF8.GetBytes(ProductionShotContractRules.CanonicalJson(HashSurface(payload))));

    public static JsonElement HashSurface(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object)
            return JsonSerializer.SerializeToElement(new { });
        var keep = new Dictionary<string, JsonElement>();
        foreach (var name in new[] { "shotId", "characterId", "providerPolicy", "imageRequirements", "inputReferences", "generationConstraints", "provenance" })
        {
            if (payload.TryGetProperty(name, out var n))
                keep[name] = n;
        }
        return JsonSerializer.SerializeToElement(keep.ToDictionary(p => p.Key, p => (object)p.Value));
    }

    public static bool ContainsProvider(JsonElement el) =>
        ProductionPromptCompilerRules.ContainsProviderSyntax(el.ValueKind == JsonValueKind.Undefined ? "" : el.GetRawText())
        || Walk(el).Any(n => n.Contains("gemini", StringComparison.OrdinalIgnoreCase)
            || n.Contains("runway", StringComparison.OrdinalIgnoreCase)
            || n.Equals("provider", StringComparison.OrdinalIgnoreCase) && false);

    public static JsonElement WithPolicy(JsonElement payload, string? quality, string? background, string? format)
    {
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload.GetRawText()) ?? new();
        var req = dict.TryGetValue("imageRequirements", out var r) && r.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(r.GetRawText()) ?? new()
            : new Dictionary<string, JsonElement>();
        if (!string.IsNullOrWhiteSpace(quality)) req["qualityPolicy"] = JsonSerializer.SerializeToElement(quality.Trim());
        if (!string.IsNullOrWhiteSpace(background)) req["backgroundPolicy"] = JsonSerializer.SerializeToElement(background.Trim());
        if (!string.IsNullOrWhiteSpace(format)) req["outputFormat"] = JsonSerializer.SerializeToElement(format.Trim());
        dict["imageRequirements"] = JsonSerializer.SerializeToElement(req.ToDictionary(p => p.Key, p => (object)p.Value));
        return JsonSerializer.SerializeToElement(dict);
    }

    public sealed record WriteOverlay(string? QualityPolicy = null, string? BackgroundPolicy = null, string? OutputFormat = null);

    public static WriteOverlay ReadOverlay(JsonElement payload)
    {
        var req = payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("imageRequirements", out var r) && r.ValueKind == JsonValueKind.Object
            ? r : JsonSerializer.SerializeToElement(new { });
        return new WriteOverlay(ReadString(req, "qualityPolicy"), ReadString(req, "backgroundPolicy"), ReadString(req, "outputFormat"));
    }

    public static WriteOverlay ResolveOverlay(string? quality, string? background, string? format, JsonElement? existing)
    {
        var prior = existing is { ValueKind: JsonValueKind.Object } el ? ReadOverlay(el) : new WriteOverlay();
        return new WriteOverlay(
            FirstNonEmpty(quality, prior.QualityPolicy),
            FirstNonEmpty(background, prior.BackgroundPolicy),
            FirstNonEmpty(format, prior.OutputFormat));
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a.Trim() : !string.IsNullOrWhiteSpace(b) ? b.Trim() : null;

    private static void AddSha(List<Block> blocks, string source, string stored, string live)
    {
        if (!CharacterIdentityGovernanceRules.ShaExists(stored) || !CharacterIdentityGovernanceRules.ShaExists(live))
            blocks.Add(NotReady(source.ToLowerInvariant(), stored, live, $"IMAGE_GENERATION_CONTRACT_NOT_READY: missing {source}."));
        else if (!CharacterIdentityGovernanceRules.SameSha(stored, live))
            blocks.Add(new Block("BLOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY", source, "sha256", stored, live,
                $"{source} SHA mismatch. Không auto-fix."));
    }

    private static Block NotReady(string attribute, string? requested, string? authoritative, string message) =>
        new("BLOCKED", "IMAGE_GENERATION_CONTRACT_NOT_READY", "AUTHORITY", attribute, requested, authoritative, message);

    private static JsonElement Obj(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Object
            ? n : JsonSerializer.SerializeToElement(new { });

    private static string ReadString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return "";
        return n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim() : "";
    }

    private static IReadOnlyList<string> StringList(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n) || n.ValueKind != JsonValueKind.Array)
            return [];
        return n.EnumerateArray()
            .Select(i => i.ValueKind == JsonValueKind.String ? (i.GetString() ?? "").Trim() : "")
            .Where(s => s.Length > 0)
            .ToList();
    }

    private static IEnumerable<string> Walk(JsonElement el)
    {
        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var p in el.EnumerateObject())
            {
                yield return p.Name;
                foreach (var n in Walk(p.Value)) yield return n;
            }
        }
        else if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var i in el.EnumerateArray())
                foreach (var n in Walk(i)) yield return n;
        }
    }
}
