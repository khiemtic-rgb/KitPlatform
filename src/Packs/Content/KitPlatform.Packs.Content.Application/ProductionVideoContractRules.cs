using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>PRODUCTION_VIDEO_CONTRACT_V1 — facts + motion + timing. No provider. No video.</summary>
public static class ProductionVideoContractRules
{
    public const string DocumentId = "PRODUCTION_VIDEO_CONTRACT_V1";
    public const string Project = "FAMIXA";

    public static readonly string[] Statuses =
        ["DRAFT", "VALIDATED", "DIRECTOR_APPROVED", "REJECTED", "SUPERSEDED"];

    public static readonly string[] AllowedCameraTypes =
        ["static", "slow_push_in", "slow_pull_out", "slight_pan", "hold"];
    public static readonly string[] AllowedSubjectTypes =
        ["none", "slight_turn", "eye_glance", "natural_breathing", "hold", "slight_lower"];

    public static readonly string[] IdentityChangePhrases =
    [
        "change face", "different face", "face swap", "identity merge", "identity blending",
        "change eye structure", "different eye", "change hairstyle", "change hair length",
        "grow hair", "shorten hair", "change age", "make him older", "make him younger",
        "make her older", "make her younger", "change body proportion", "change wardrobe",
        "change wardrobe identity",
    ];

    public static bool AutoFix() => false;
    public static bool AutoApprove() => false;
    public static bool AutoLock() => false;
    public static bool CallsProvider() => false;
    public static bool AllowsGemini() => false;
    public static bool AllowsRunway() => false;
    public static bool CreatesPixels(string? action) =>
        action is "GENERATE" or "REGENERATE" or "GEMINI" or "RUNWAY" or "I2V";
    public static bool TouchesGolden(string? path) =>
        ProductionPromptCompilerRules.TouchesGolden(path);

    public static bool IsApproved(string? status) =>
        string.Equals(status, "DIRECTOR_APPROVED", StringComparison.OrdinalIgnoreCase);
    public static bool CanEdit(string? status) =>
        status is "DRAFT" or "VALIDATED" or "REJECTED" || string.IsNullOrWhiteSpace(status);
    public static bool RejectReasonRequired(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length >= 3;

    public static string NextVersion(string? current)
    {
        var v = (current ?? "V1").Trim().ToUpperInvariant();
        var n = Regex.Match(v, @"^V(\d+)$");
        return n.Success && int.TryParse(n.Groups[1].Value, out var i) ? $"V{i + 1}" : "V2";
    }

    public static bool IsStillApproved(string? executionStatus, string? directorReview)
    {
        var exec = (executionStatus ?? "").Trim().ToUpperInvariant();
        var review = (directorReview ?? "").Trim().ToUpperInvariant();
        return (exec is "IMAGE_APPROVED" or "APPROVED") && review == "APPROVED";
    }

    public static bool IsStillRejected(string? executionStatus, string? directorReview) =>
        executionStatus is "IMAGE_REJECTED" or "REJECTED" || directorReview is "REJECTED";

    public static bool IsStillSuperseded(string? directorReview) =>
        string.Equals(directorReview, "SUPERSEDED", StringComparison.OrdinalIgnoreCase);

    public sealed record Block(
        string Status, string Code, string Source, string Attribute,
        string? Requested, string? Authoritative, string Message);

    public sealed record GateInput(
        string CharacterId,
        bool MasterLocked,
        bool DnaLocked,
        bool PrpLocked,
        bool GovernancePass,
        IReadOnlyList<CharacterIdentityGovernanceRules.Conflict> GovernanceConflicts,
        string ShotContractStatus,
        string ExecutionStatus,
        string DirectorReviewStatus,
        bool ArtifactExists,
        bool ArtifactReadable,
        string MasterSha256,
        string LiveMasterSha256,
        string DnaSha256,
        string LiveDnaSha256,
        string PrpSha256,
        string LivePrpSha256,
        string ShotContractSha256,
        string LiveShotContractSha256,
        string StoredStillSha,
        string LiveStillSha,
        Guid? RequestedStillId,
        Guid? AnchorStillId,
        JsonElement Payload);

    public sealed record GateOutput(
        string Status,
        IReadOnlyList<Block> Blocks,
        string? ContractSha256,
        bool Generation = false);

    public static GateOutput Evaluate(GateInput input)
    {
        var blocks = new List<Block>();
        if (string.IsNullOrWhiteSpace(CharacterIdentityGovernanceRules.NormalizeCharacterId(input.CharacterId)))
            blocks.Add(NotReady("characterId", "", "required", "character_id is required. No identity fallback."));
        if (!input.MasterLocked)
            blocks.Add(NotReady("master", "MISSING", "LOCKED", "VIDEO_CONTRACT_NOT_READY: Master must be LOCKED."));
        if (!input.DnaLocked)
            blocks.Add(NotReady("dna", "MISSING", "LOCKED", "VIDEO_CONTRACT_NOT_READY: DNA must be LOCKED."));
        if (!input.PrpLocked)
            blocks.Add(NotReady("prp", "MISSING", "LOCKED", "VIDEO_CONTRACT_NOT_READY: PRP must be LOCKED."));
        if (!ProductionShotContractRules.IsApproved(input.ShotContractStatus)
            && !ImageGenerationContractRules.IsApproved(input.ShotContractStatus))
            blocks.Add(NotReady("shotContract", input.ShotContractStatus, "DIRECTOR_APPROVED", "Shot Contract must be DIRECTOR_APPROVED."));

        if (IsStillRejected(input.ExecutionStatus, input.DirectorReviewStatus))
            blocks.Add(NotReady("still", input.ExecutionStatus, "IMAGE_APPROVED", "Approved production still required. Still was rejected."));
        else if (IsStillSuperseded(input.DirectorReviewStatus))
            blocks.Add(NotReady("still", input.DirectorReviewStatus, "APPROVED", "Approved production still required. Still review is SUPERSEDED."));
        else if (!IsStillApproved(input.ExecutionStatus, input.DirectorReviewStatus))
            blocks.Add(NotReady("still", input.ExecutionStatus + "/" + input.DirectorReviewStatus, "IMAGE_APPROVED",
                "VIDEO_CONTRACT_NOT_READY: Approved production still required."));
        if (!input.ArtifactExists || !input.ArtifactReadable)
            blocks.Add(NotReady("artifact", "missing", "readable", "Approved still artifact missing or unreadable."));

        AddSha(blocks, "MASTER", input.MasterSha256, input.LiveMasterSha256);
        AddSha(blocks, "CHARACTER_DNA", input.DnaSha256, input.LiveDnaSha256);
        AddSha(blocks, "PRODUCTION_REFERENCE_PACK", input.PrpSha256, input.LivePrpSha256);
        AddSha(blocks, "SHOT_CONTRACT", input.ShotContractSha256, input.LiveShotContractSha256);
        AddSha(blocks, "APPROVED_STILL", input.StoredStillSha, input.LiveStillSha);

        if (input.RequestedStillId is { } requested && input.AnchorStillId is { } anchor && requested != anchor)
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_NOT_READY", "STILL", "approvedStillExecutionId",
                requested.ToString(), anchor.ToString(), "Approved still cannot be replaced in the same version."));

        if (!input.GovernancePass || input.GovernanceConflicts.Count > 0)
        {
            foreach (var c in input.GovernanceConflicts)
                blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_IDENTITY_CONFLICT", c.Source, c.Attribute, c.RequestedValue, c.AuthoritativeValue, c.Message));
            if (blocks.All(b => b.Code != "VIDEO_CONTRACT_IDENTITY_CONFLICT"))
                blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_IDENTITY_CONFLICT", "GOVERNANCE", "identity", null, null, "Identity Governance FAIL."));
        }

        var blob = input.Payload.ValueKind == JsonValueKind.Undefined ? "" : input.Payload.GetRawText();
        if (HasIdentityChangeRequest(blob))
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_IDENTITY_CONFLICT", "VIDEO_CONTRACT", "identity", "change", "INVARIANT",
                "Video Contract cannot change face/eyes/hair/age/body/wardrobe identity. Không auto-fix."));
        if (HasCameraActionMix(input.Payload))
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_CAMERA_ACTION_CONFLICT", "CAMERA", "cameraMovement", "mixed", "separated",
                "Camera and subject action must be separate. Không auto-fix."));
        if (HasInvalidTiming(input.Payload, out var timingMsg))
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_NOT_READY", "TIMING", "duration", timingMsg, ">0 and beats<=duration", timingMsg));
        if (ProductionPromptCompilerRules.ContainsProviderSyntax(blob)
            || ContainsPromptLeak(blob)
            || ProductionPromptCompilerRules.ContainsInjectionPhrases(blob)
            || HasMotionProviderWord(input.Payload))
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_NOT_READY", "PROVIDER", "injection", "provider", "facts",
                "Video Contract cannot contain provider/model/prompt syntax."));
        if (HasUnstructuredMotionOnly(input.Payload))
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_NOT_READY", "MOTION", "structure", "free-text", "structured",
                "Motion must be structured type/direction/intensity. Text is metadata only."));

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
        Guid stillExecutionId, string stillSha,
        JsonElement shotContract,
        WriteOverlay? overlay = null)
    {
        var scene = Obj(shotContract, "scene");
        var character = Obj(shotContract, "character");
        var wardrobe = Obj(shotContract, "wardrobe");
        var composition = Obj(shotContract, "composition");
        var lighting = Obj(shotContract, "lighting");
        var continuity = Obj(shotContract, "continuity");
        var constraints = Obj(shotContract, "constraints");
        var production = Obj(shotContract, "production");
        var ident = Obj(shotContract, "identity");
        var o = overlay ?? new WriteOverlay();
        var duration = o.DurationSeconds ?? ReadDuration(shotContract);
        if (o.DurationSeconds is null && duration <= 0) duration = 5;
        var cam = o.CameraMovement ?? Motion("slow_push_in", "forward", "low");
        var head = o.HeadMovement ?? Motion("slight_turn", "right", "low");
        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["authority"] = new Dictionary<string, object?>
            {
                ["masterId"] = masterId?.ToString(),
                ["masterSha256"] = masterSha,
                ["dnaId"] = dnaId?.ToString(),
                ["dnaSha256"] = dnaSha,
                ["prpId"] = prpId?.ToString(),
                ["prpSha256"] = prpSha,
                ["shotContractId"] = shotContractId?.ToString(),
                ["shotContractSha256"] = shotContractSha,
                ["approvedStillExecutionId"] = stillExecutionId.ToString(),
                ["approvedStillArtifactSha256"] = stillSha,
            },
            ["character"] = new Dictionary<string, object?>
            {
                ["characterId"] = CharacterIdentityGovernanceRules.NormalizeCharacterId(characterId),
                ["age"] = ReadNested(ident, "age") is { Length: > 0 } a ? a : "inherited",
                ["identityState"] = "inherited",
                ["wardrobeState"] = First(ReadString(wardrobe, "description"), "inherited"),
                ["hairState"] = "inherited",
                ["expressionState"] = First(ReadString(character, "expression"), "inherited"),
                ["bodyState"] = First(ReadString(character, "pose"), "inherited"),
            },
            ["scene"] = new Dictionary<string, object?>
            {
                ["sceneId"] = shotId.ToString("N")[..8],
                ["location"] = ReadString(scene, "location"),
                ["environmentState"] = ReadString(scene, "environment"),
                ["timeOfDay"] = First(ReadString(scene, "time"), ReadString(scene, "timeOfDay")),
                ["backgroundState"] = ReadString(lighting, "description"),
            },
            ["camera"] = new Dictionary<string, object?>
            {
                ["framing"] = ReadString(composition, "framing"),
                ["cameraPosition"] = ReadString(composition, "cameraPosition"),
                ["cameraAngle"] = ReadString(composition, "cameraAngle"),
                ["lensIntent"] = "inherited",
                ["cameraMovement"] = cam,
                ["cameraSpeed"] = ReadString(cam, "intensity"),
                ["cameraPath"] = ReadString(cam, "direction"),
            },
            ["subjectMotion"] = new Dictionary<string, object?>
            {
                ["body"] = Motion("natural_breathing", "none", "low"),
                ["head"] = head,
                ["eye"] = Motion("eye_glance", "paper", "low"),
                ["hand"] = Motion("slight_lower", "down", "low"),
                ["face"] = Motion("hold", "none", "low"),
                ["posture"] = Motion("hold", "none", "low"),
            },
            ["expression"] = new Dictionary<string, object?>
            {
                ["startingExpression"] = First(o.StartingExpression, ReadString(character, "expression")),
                ["endingExpression"] = First(o.EndingExpression, ReadString(character, "expression")),
                ["expressionTransition"] = "hold",
                ["intensity"] = "low",
            },
            ["hairClothing"] = new Dictionary<string, object?>
            {
                ["hairMotion"] = First(o.HairMotion, "moves slightly with air"),
                ["clothMotion"] = First(o.ClothMotion, "moves naturally"),
            },
            ["environment"] = new Dictionary<string, object?>
            {
                ["lightMovement"] = "none",
                ["curtainMovement"] = "none",
                ["backgroundMovement"] = "none",
                ["particleMovement"] = "none",
                ["objectMovement"] = "none",
            },
            ["props"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["propId"] = "paper",
                    ["initialState"] = "held_by_character",
                    ["motion"] = "slightly lowered",
                    ["finalState"] = "held_by_character",
                },
            },
            ["timing"] = new Dictionary<string, object?>
            {
                ["duration"] = duration,
                ["fps"] = First(ReadString(production, "frameRate"), "24"),
                ["startState"] = "approved_still",
                ["beats"] = new object[] { new { at = 0, label = "start" }, new { at = duration, label = "end" } },
                ["endState"] = "same_identity",
            },
            ["continuity"] = new Dictionary<string, object?>
            {
                ["previousShotId"] = ReadString(continuity, "previousShotId"),
                ["nextShotId"] = "",
                ["continuityRules"] = StringList(continuity, "rules"),
                ["startContinuity"] = "approved_still",
                ["endContinuity"] = "same_wardrobe_location_prop",
            },
            ["composition"] = new Dictionary<string, object?>
            {
                ["subjectPosition"] = ReadString(composition, "subjectPosition"),
                ["screenDirection"] = "inherited",
                ["headroom"] = "inherited",
                ["lookDirection"] = First(ReadString(character, "gaze"), "inherited"),
                ["foreground"] = "inherited",
                ["background"] = "inherited",
            },
            ["constraints"] = new Dictionary<string, object?>
            {
                ["invariants"] = new[] { "FACE identity", "EYES structure", "HAIR identity", "AGE", "WARDROBE", "BODY proportion" },
                ["forbidden"] = StringList(constraints, "forbidden").Concat(["provider syntax", "identity change"]).Distinct().ToArray(),
                ["required"] = new[] { "approved still visual anchor", "structured camera", "structured subject motion" },
            },
            ["output"] = new Dictionary<string, object?>
            {
                ["aspectRatio"] = First(ReadString(production, "aspectRatio"), "16:9"),
                ["resolution"] = First(ReadString(production, "resolution"), "1280x720"),
                ["fps"] = First(ReadString(production, "frameRate"), "24"),
                ["duration"] = duration,
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
        foreach (var name in new[]
                 {
                     "authority", "character", "scene", "camera", "subjectMotion", "expression",
                     "hairClothing", "environment", "props", "timing", "continuity", "composition",
                     "constraints", "output",
                 })
        {
            if (payload.TryGetProperty(name, out var n))
                keep[name] = n;
        }
        return JsonSerializer.SerializeToElement(keep.ToDictionary(p => p.Key, p => (object)p.Value));
    }

    public static bool HasIdentityChangeRequest(string? text)
    {
        var t = text ?? "";
        return IdentityChangePhrases.Any(p => t.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasCameraActionMix(JsonElement payload)
    {
        var camera = Obj(payload, "camera");
        var subject = Obj(payload, "subjectMotion");
        var camText = camera.GetRawText() + " " + ReadString(camera, "note");
        var subText = subject.GetRawText();
        if (ProductionShotContractRules.ContainsCameraInstruction(subText))
            return true;
        if (Regex.IsMatch(camText, @"\b(walks|walk|runs|run|lifts|sits|stands)\b", RegexOptions.IgnoreCase)
            && Regex.IsMatch(camText, @"\b(camera|push|dolly|pan|tilt)\b", RegexOptions.IgnoreCase))
            return true;
        return false;
    }

    public static bool HasInvalidTiming(JsonElement payload, out string message)
    {
        message = "";
        var timing = Obj(payload, "timing");
        var duration = ReadNumber(timing, "duration");
        if (duration <= 0)
        {
            message = duration < 0 ? "Duration cannot be negative." : "Duration must be > 0.";
            return true;
        }
        if (timing.TryGetProperty("beats", out var beats) && beats.ValueKind == JsonValueKind.Array)
        {
            foreach (var b in beats.EnumerateArray())
            {
                var at = ReadNumber(b, "at");
                if (at > duration)
                {
                    message = "Beat exceeds duration.";
                    return true;
                }
            }
        }
        return false;
    }

    public static bool HasUnstructuredMotionOnly(JsonElement payload)
    {
        var camera = Obj(payload, "camera");
        if (camera.TryGetProperty("cameraMovement", out var cm) && cm.ValueKind == JsonValueKind.String)
            return true;
        var subject = Obj(payload, "subjectMotion");
        if (subject.TryGetProperty("head", out var h) && h.ValueKind == JsonValueKind.String)
            return true;
        return false;
    }

    private static bool HasMotionProviderWord(JsonElement payload)
    {
        var hair = Obj(payload, "hairClothing");
        var expr = Obj(payload, "expression");
        var text = string.Join(' ',
            ReadString(hair, "hairMotion"), ReadString(hair, "clothMotion"),
            ReadString(expr, "startingExpression"), ReadString(expr, "endingExpression"));
        return Regex.IsMatch(text, @"\bprovider\b", RegexOptions.IgnoreCase);
    }

    public static bool ContainsPromptLeak(string? text)
    {
        var t = text ?? "";
        return t.Contains("runway_prompt", StringComparison.OrdinalIgnoreCase)
            || t.Contains("gemini_prompt", StringComparison.OrdinalIgnoreCase)
            || t.Contains("model_prompt", StringComparison.OrdinalIgnoreCase)
            || t.Contains("FINAL_PROMPT", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Runway hãy", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Gemini hãy", StringComparison.OrdinalIgnoreCase)
            || t.Contains("gen4", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(t, @"\bmodel\b", RegexOptions.IgnoreCase);
    }

    public sealed record WriteOverlay(
        double? DurationSeconds = null,
        JsonElement? CameraMovement = null,
        JsonElement? HeadMovement = null,
        string? StartingExpression = null,
        string? EndingExpression = null,
        string? HairMotion = null,
        string? ClothMotion = null);

    public static WriteOverlay ReadOverlay(JsonElement payload)
    {
        var cam = Obj(payload, "camera");
        var subject = Obj(payload, "subjectMotion");
        var expr = Obj(payload, "expression");
        var hair = Obj(payload, "hairClothing");
        var timing = Obj(payload, "timing");
        JsonElement? camMove = cam.TryGetProperty("cameraMovement", out var cm) && cm.ValueKind == JsonValueKind.Object ? cm : null;
        JsonElement? head = subject.TryGetProperty("head", out var h) && h.ValueKind == JsonValueKind.Object ? h : null;
        return new WriteOverlay(ReadNumber(timing, "duration"), camMove, head,
            ReadString(expr, "startingExpression"), ReadString(expr, "endingExpression"),
            ReadString(hair, "hairMotion"), ReadString(hair, "clothMotion"));
    }

    public static WriteOverlay ResolveOverlay(WriteOverlay? requested, JsonElement? existing)
    {
        var prior = existing is { ValueKind: JsonValueKind.Object } el ? ReadOverlay(el) : new WriteOverlay();
        var r = requested ?? new WriteOverlay();
        return new WriteOverlay(
            r.DurationSeconds is > 0 ? r.DurationSeconds : prior.DurationSeconds,
            r.CameraMovement ?? prior.CameraMovement,
            r.HeadMovement ?? prior.HeadMovement,
            First(r.StartingExpression, prior.StartingExpression),
            First(r.EndingExpression, prior.EndingExpression),
            First(r.HairMotion, prior.HairMotion),
            First(r.ClothMotion, prior.ClothMotion));
    }

    public static JsonElement Motion(string type, string direction, string intensity) =>
        JsonSerializer.SerializeToElement(new { type, direction, intensity, intent = type });

    private static void AddSha(List<Block> blocks, string source, string stored, string live)
    {
        if (!CharacterIdentityGovernanceRules.ShaExists(stored) || !CharacterIdentityGovernanceRules.ShaExists(live))
            blocks.Add(NotReady(source.ToLowerInvariant(), stored, live, $"VIDEO_CONTRACT_NOT_READY: missing {source}."));
        else if (!CharacterIdentityGovernanceRules.SameSha(stored, live))
            blocks.Add(new Block("BLOCKED", "VIDEO_CONTRACT_NOT_READY", source, "sha256", stored, live,
                $"{source} SHA mismatch. Không auto-fix."));
    }

    private static Block NotReady(string attribute, string? requested, string? authoritative, string message) =>
        new("BLOCKED", "VIDEO_CONTRACT_NOT_READY", "AUTHORITY", attribute, requested, authoritative, message);

    private static JsonElement Obj(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var n) && n.ValueKind == JsonValueKind.Object
            ? n : JsonSerializer.SerializeToElement(new { });

    private static string ReadString(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return "";
        return n.ValueKind == JsonValueKind.String ? (n.GetString() ?? "").Trim() : n.ValueKind is JsonValueKind.Number ? n.ToString() : "";
    }

    private static string ReadNested(JsonElement obj, string name) => ReadString(obj, name);

    private static double ReadNumber(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var n))
            return 0;
        if (n.ValueKind == JsonValueKind.Number && n.TryGetDouble(out var d))
            return d;
        return double.TryParse(n.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0;
    }

    private static double ReadDuration(JsonElement shot)
    {
        var timing = Obj(shot, "timing");
        var n = ReadNumber(timing, "durationSeconds");
        return n > 0 ? n : ReadNumber(timing, "duration");
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

    private static string First(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a.Trim() : (b ?? "").Trim();
}
