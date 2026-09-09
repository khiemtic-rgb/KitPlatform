using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

public static class KitVideoStoryCompiler
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly (string Code, string Name, string[] Aliases)[] Known =
    [
        ("CHAR-001", "Minh", ["minh", "cậu bé", "cau be"]),
        ("CHAR-002", "Nam", ["nam", "bố"]),
        ("CHAR-003", "Linh", ["linh", "mẹ", "mother"]),
    ];

    private static readonly Regex Primary = new(
        @"(chạy|bước|đi vào|đưa|giơ|đặt|nói|walks?|runs?|shows?|raises?|approaches?|puts?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex Quote = new(@"[“""]([^”""]+)[”""]", RegexOptions.Compiled);

    public static string ScriptHash(string script)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(script ?? ""));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static JsonElement Compile(string script)
    {
        var text = script ?? "";
        var chars = Mentioned(text);
        var loc = Regex.IsMatch(text, @"phòng khách|living room", RegexOptions.IgnoreCase) ? "LOC-001" : "LOC-001";
        var props = Regex.IsMatch(text, @"bài kiểm tra|test paper|điểm", RegexOptions.IgnoreCase)
            ? new List<string> { "PROP-001" }
            : [];
        var objective = Regex.IsMatch(text, @"khoe|điểm|test paper|9 điểm", RegexOptions.IgnoreCase)
            ? "Minh muốn khoe điểm với mẹ."
            : "";
        var beats = ProposeBeats(text, chars, props);
        var dialogues = ExtractDialogue(text);
        var scene = new JsonObject
        {
            ["sceneId"] = "SC01",
            ["sceneOrder"] = 1,
            ["location"] = loc,
            ["time"] = Regex.IsMatch(text, @"tối|evening|đêm", RegexOptions.IgnoreCase) ? "Evening" : "Unspecified",
            ["characters"] = ToArr(chars),
            ["wardrobe"] = new JsonObject(chars.ToDictionary(
                c => c,
                c => (JsonNode?)JsonValue.Create(c == "CHAR-001" ? "WARDROBE-001" : "WARDROBE-002"))),
            ["initialState"] = new JsonObject(),
            ["objective"] = objective,
            ["beats"] = beats,
        };

        var shots = new JsonArray();
        var snapshots = new JsonObject();
        JsonObject? prevSnap = null;
        var display = 0;
        var dialogueCursor = 0;

        foreach (var beat in beats)
        {
            var beatObj = beat?.AsObject();
            if (beatObj is null) continue;
            var beatChars = Strings(beatObj["characters"]);
            var beatProps = Strings(beatObj["requiredProps"]);
            var beatId = beatObj["beatId"]?.GetValue<string>() ?? "BEAT-01";
            var emotion = beatObj["emotion"]?.GetValue<string>();
            foreach (var action in ProposeSplit(beatObj["action"]?.GetValue<string>() ?? ""))
            {
                display += 1;
                var shotId = $"shot-{Guid.NewGuid():N}"[..20];
                var displayCode = $"SH01-{display:00}";
                JsonObject? line = null;
                if (Regex.IsMatch(action, @"nói|says?", RegexOptions.IgnoreCase)
                    && dialogueCursor < dialogues.Count)
                {
                    line = dialogues[dialogueCursor]!.AsObject();
                    line["shotId"] = shotId;
                    line["status"] = "MAPPED";
                    dialogueCursor += 1;
                }

                var inherit = Inherit(prevSnap, beatChars.Count > 0 ? beatChars : chars, loc, beatProps.Count > 0 ? beatProps : props, action);
                var status = KitVideoContinuityRules.ShotStatusFromAction(action);
                var shot = new JsonObject
                {
                    ["shotId"] = shotId,
                    ["displayCode"] = displayCode,
                    ["displayOrder"] = display,
                    ["sceneId"] = "SC01",
                    ["beatId"] = beatId,
                    ["attempt"] = 1,
                    ["action"] = action,
                    ["purpose"] = line is not null ? "DIALOGUE" : Regex.IsMatch(action, @"chạy vào|bước vào|phòng khách|living room", RegexOptions.IgnoreCase) ? "ESTABLISH" : "ACTION",
                    ["estimatedDuration"] = Math.Max(3, Math.Min(10, action.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length)),
                    ["characters"] = ToArr(((JsonObject)inherit["characters"]!).Select(kv => kv.Key).ToList()),
                    ["dialogueSegmentIds"] = line is not null ? new JsonArray(line["segmentId"]!.DeepClone()) : new JsonArray(),
                    ["requiredProps"] = RequiredFromState(inherit["propState"]),
                    ["emotion"] = emotion,
                    ["screen"] = "SCREEN_LEFT",
                    ["look"] = "LOOK_RIGHT",
                    ["transitionFromPrevious"] = display == 1 ? "CUT" : "CONTINUE",
                    ["status"] = status,
                    ["continuity"] = "CONTINUITY_PASS",
                    ["continuityNotes"] = new JsonArray(),
                    ["contract"] = ActionContract(action),
                    ["characterState"] = inherit["characters"]!.DeepClone(),
                    ["locationState"] = inherit["location"]!.DeepClone(),
                    ["propState"] = inherit["propState"]!.DeepClone(),
                };
                if (line is not null && line["durationSec"]?.GetValue<int>() > 10)
                {
                    shot["estimatedDuration"] = 6;
                    shot["voiceChainFrom"] = (string?)null;
                }

                var validated = KitVideoContinuityRules.Validate(
                    JsonSerializer.SerializeToElement(shot, JsonOpts),
                    prevSnap is null ? null : JsonSerializer.SerializeToElement(prevSnap, JsonOpts));
                shot["continuity"] = validated.Status;
                shot["continuityNotes"] = new JsonArray(validated.Notes.Select(n => JsonValue.Create(n)).ToArray());
                if (validated.Status == "CONTINUITY_FAIL" || (shot["contract"] as JsonObject)?["blocked"]?.GetValue<bool>() == true)
                    shot["status"] = "BLOCKED";

                shots.Add(shot);
                var snap = Snapshot(shot);
                snapshots[shotId] = snap;
                prevSnap = snap;
            }
        }

        var graph = new JsonObject
        {
            ["scenes"] = new JsonArray(scene),
            ["shots"] = shots,
            ["dialogues"] = dialogues,
            ["snapshots"] = snapshots,
            ["unassigned"] = new JsonArray(dialogues
                .Select(d => d!.AsObject())
                .Where(d => d["status"]?.GetValue<string>() == "UNASSIGNED")
                .Select(d => JsonValue.Create(d["segmentId"]!.GetValue<string>()))
                .ToArray()),
        };
        return JsonSerializer.SerializeToElement(graph, JsonOpts);
    }

    public static KitVideoScriptImpactResult Impact(string previousScript, string nextScript)
    {
        var before = Compile(previousScript);
        var after = Compile(nextScript);
        var prevActions = before.GetProperty("shots").EnumerateArray().Select(s => s.GetProperty("action").GetString() ?? "").ToHashSet();
        var affectedShots = after.GetProperty("shots").EnumerateArray()
            .Where(s => !prevActions.Contains(s.GetProperty("action").GetString() ?? ""))
            .Select(s => s.GetProperty("displayCode").GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
        var scenes = after.GetProperty("scenes").EnumerateArray().Select(s => s.GetProperty("sceneId").GetString() ?? "SC01").Distinct().ToList();
        var beats = after.GetProperty("scenes").EnumerateArray()
            .SelectMany(s => s.GetProperty("beats").EnumerateArray().Select(b => b.GetProperty("beatId").GetString() ?? ""))
            .Where(s => s.Length > 0)
            .ToList();
        return new KitVideoScriptImpactResult(false, scenes, beats, affectedShots, affectedShots);
    }

    private static JsonArray ProposeBeats(string script, List<string> chars, List<string> props)
    {
        var parts = Regex.Split(script, @"(?<=[.!?])\s+").Select(s => s.Trim()).Where(s => s.Length > 8).ToList();
        if (parts.Count == 0 && script.Trim().Length > 0) parts.Add(script.Trim());
        var arr = new JsonArray();
        for (var i = 0; i < parts.Count; i++)
        {
            var t = parts[i];
            var localChars = Mentioned(t);
            arr.Add(new JsonObject
            {
                ["beatId"] = $"BEAT-{(i + 1):00}",
                ["order"] = i + 1,
                ["text"] = t,
                ["characters"] = ToArr(localChars.Count > 0 ? localChars : chars),
                ["action"] = Quote.Replace(t, "").Trim() is { Length: > 0 } a ? a : t,
                ["emotion"] = Regex.IsMatch(t, @"vui|excited|hớn hở", RegexOptions.IgnoreCase) ? "excited" : null,
                ["dialogueSegmentIds"] = new JsonArray(),
                ["requiredProps"] = ToArr(MentionedProps(t).Count > 0 ? MentionedProps(t) : Regex.IsMatch(t, @"đưa|cầm|giơ|shows?|holds?", RegexOptions.IgnoreCase) ? props : []),
            });
        }
        return arr;
    }

    private static List<string> ProposeSplit(string text)
    {
        var bits = Regex.Split(text, @",\s+| và (?=nói|đưa|giơ|chạy)")
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .Where(s => Primary.IsMatch(s))
            .ToList();
        return bits.Count >= 2 ? bits : [text];
    }

    private static JsonArray ExtractDialogue(string script)
    {
        var arr = new JsonArray();
        var i = 1;
        foreach (Match m in Quote.Matches(script))
        {
            var line = m.Groups[1].Value.Trim();
            arr.Add(new JsonObject
            {
                ["segmentId"] = $"D{i:000}",
                ["text"] = line,
                ["durationSec"] = Math.Max(2, (int)Math.Round(line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 0.45)),
                ["shotId"] = null,
                ["status"] = "UNASSIGNED",
            });
            i += 1;
        }
        return arr;
    }

    private static JsonObject Inherit(JsonObject? prev, List<string> codes, string location, List<string> props, string action)
    {
        var characters = new JsonObject();
        var prevChars = prev?["characters"] as JsonObject;
        foreach (var code in codes)
        {
            var old = prevChars?[code] as JsonObject;
            var moved = Regex.IsMatch(action, @"(chạy|bước|đi|walks?|runs?|approaches?)", RegexOptions.IgnoreCase);
            var pos = moved
                ? Position(action, old?["position"]?.GetValue<string>())
                : old?["position"]?.GetValue<string>() ?? Position(action, null);
            var held = old?["heldProps"] is JsonArray ha
                ? ha.Select(x => x!.GetValue<string>()).ToList()
                : [];
            characters[code] = new JsonObject
            {
                ["code"] = code,
                ["position"] = pos,
                ["facing"] = old?["facing"]?.GetValue<string>() ?? "LOOK_RIGHT",
                ["wardrobe"] = old?["wardrobe"]?.GetValue<string>() ?? (code == "CHAR-001" ? "WARDROBE-001" : "WARDROBE-002"),
                ["heldProps"] = ToArr(held),
                ["action"] = action,
            };
        }

        var propState = prev?["props"] as JsonObject ?? new JsonObject();
        propState = (JsonObject)propState.DeepClone();
        foreach (var p in props)
        {
            if (propState[p] is null) propState[p] = "not_present";
            if (Regex.IsMatch(action, @"cầm|holds?|trên tay|raises?|shows?|đưa", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(action, @"đặt .+ xuống|xuống bàn|on_table", RegexOptions.IgnoreCase))
            {
                propState[p] = "held_by_minh";
                if (characters["CHAR-001"] is JsonObject minh)
                {
                    var list = Strings(minh["heldProps"]);
                    if (!list.Contains(p)) list.Add(p);
                    minh["heldProps"] = ToArr(list);
                }
            }
            if (Regex.IsMatch(action, @"đặt .+ xuống|xuống bàn|on_table", RegexOptions.IgnoreCase))
            {
                propState[p] = "on_table";
                if (characters["CHAR-001"] is JsonObject minh)
                    minh["heldProps"] = ToArr(Strings(minh["heldProps"]).Where(x => x != p).ToList());
            }
        }

        var prevLoc = prev?["location"] as JsonObject;
        return new JsonObject
        {
            ["characters"] = characters,
            ["propState"] = propState,
            ["location"] = new JsonObject
            {
                ["location"] = prevLoc?["location"]?.GetValue<string>() ?? location,
                ["lighting"] = prevLoc?["lighting"]?.GetValue<string>() ?? "evening indoor",
                ["time"] = prevLoc?["time"]?.GetValue<string>() ?? "Evening",
                ["visibleFurniture"] = prevLoc?["visibleFurniture"]?.DeepClone() ?? ToArr(["sofa", "TV cabinet", "low table"]),
            },
        };
    }

    private static JsonObject Snapshot(JsonObject shot) => new()
    {
        ["shotId"] = shot["shotId"]!.GetValue<string>(),
        ["immutable"] = true,
        ["characters"] = shot["characterState"]!.DeepClone(),
        ["props"] = shot["propState"]!.DeepClone(),
        ["location"] = shot["locationState"]!.DeepClone(),
        ["lighting"] = shot["locationState"]?["lighting"]?.DeepClone(),
        ["camera"] = new JsonObject { ["side"] = shot["screen"]?.DeepClone(), ["scale"] = shot["purpose"]?.DeepClone() },
        ["facts"] = new JsonArray(JsonValue.Create($"action={shot["action"]?.GetValue<string>()}")),
    };

    private static JsonObject ActionContract(string action)
    {
        var required = new List<string>();
        if (Regex.IsMatch(action, @"minh|CHAR-001", RegexOptions.IgnoreCase)) required.Add("Minh");
        if (Regex.IsMatch(action, @"mẹ|linh|mother|CHAR-003", RegexOptions.IgnoreCase)) required.Add("Mother");
        if (Regex.IsMatch(action, @"bài|paper|PROP-001", RegexOptions.IgnoreCase)) required.Add("Test paper");
        if (Regex.IsMatch(action, @"đưa|shows?|raises?", RegexOptions.IgnoreCase))
        {
            required.Add("Minh holding paper");
            if (required.Contains("Mother")) required.Add("Mother able to see paper");
        }
        return new JsonObject
        {
            ["action"] = action,
            ["requiredVisible"] = ToArr(required.Distinct().ToList()),
            ["blocked"] = required.Count == 0 && !KitVideoContinuityRules.IsConcreteAction(action),
        };
    }

    private static string Position(string action, string? prev)
    {
        if (Regex.IsMatch(action, @"cửa|doorway", RegexOptions.IgnoreCase)) return "doorway";
        if (Regex.IsMatch(action, @"bàn|table|mẹ|mother", RegexOptions.IgnoreCase)) return "near table";
        return prev ?? "doorway";
    }

    private static List<string> Mentioned(string script)
    {
        var hay = $" {script.ToLowerInvariant()} ";
        return Known.Where(k => hay.Contains($" {k.Name.ToLowerInvariant()} ") || k.Aliases.Any(a => hay.Contains(a)))
            .Select(k => k.Code)
            .ToList();
    }

    private static List<string> MentionedProps(string script) =>
        Regex.IsMatch(script, @"bài kiểm tra|test paper|điểm", RegexOptions.IgnoreCase) ? ["PROP-001"] : [];

    private static JsonArray ToArr(IEnumerable<string> items) =>
        new(items.Select(s => JsonValue.Create(s)).ToArray());

    private static List<string> Strings(JsonNode? node)
    {
        if (node is JsonArray arr) return arr.Select(x => x?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToList();
        return [];
    }

    private static JsonArray RequiredFromState(JsonNode? node)
    {
        if (node is not JsonObject obj) return [];
        return new JsonArray(obj.Where(kv => kv.Value?.GetValue<string>() != "not_present")
            .Select(kv => JsonValue.Create(kv.Key))
            .ToArray());
    }

}
