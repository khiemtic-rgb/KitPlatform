using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Frozen character-count lock for CREATE_VIDEO. gen4_turbo image-to-video has no negativePrompt field;
/// the lock has to travel inside promptText and survive the 1000-character Runway cut.
/// </summary>
public static class ProductionVisualContinuity
{
    public const string Marker = "[FAMIXA CONTINUITY]";

    public static readonly string[] AcceptanceChecklist =
    [
        "Exactly the named characters, each once",
        "No duplicate of a named character",
        "No unexpected person",
        "No character appearing or disappearing unless the shot text says so",
        "No body or face merging",
        "Identity stable across the shot",
        "Wardrobe stable",
        "Spatial relationship stable",
        "Camera and framing stay on the first frame",
        "Props stable",
        "Scene and background stable",
        "Motion natural enough for production",
        "No obvious generated-image artifacts",
    ];

    public static ShotExecutionInput Attach(ShotExecutionInput input)
    {
        var motion = (input.MotionRequirements ?? "").Trim();
        if (motion.Contains(Marker, StringComparison.Ordinal))
            return input;
        var clause = Format(input);
        var shot = motion.Length == 0 ? clause : motion + "\n" + clause;
        return input with { MotionRequirements = shot };
    }

    public static string Format(ShotExecutionInput input)
    {
        var names = Names(input.VisualContract, input.CharacterReferences);
        var countLine = names.Count == 0
            ? "Only people already visible in the first frame."
            : $"Exactly {names.Count} people in frame: {string.Join(", ", names)}. Each person appears once.";
        return Marker + " " + countLine
            + " No duplicate character. No second copy when someone enters. No extra person."
            + " No merging. No morphing.";
    }

    public static string Slice(string? shotContract)
    {
        var raw = shotContract ?? "";
        var at = raw.IndexOf(Marker, StringComparison.Ordinal);
        if (at < 0) return "";
        return Collapse(raw[at..]);
    }

    public static IReadOnlyList<string> Names(string? visualContract, string? characterReferences)
    {
        var found = new List<string>();
        AppendJsonNames(visualContract, found);
        if (found.Count == 0)
            AppendList(characterReferences, found);
        return found;
    }

    static void AppendJsonNames(string? visual, List<string> found)
    {
        var raw = (visual ?? "").Trim();
        if (!raw.StartsWith('{')) return;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("primary", out var primary))
                AddName(primary, found);
            if (root.TryGetProperty("secondary", out var secondary) && secondary.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in secondary.EnumerateArray())
                    AddName(item, found);
            }
        }
        catch (JsonException)
        {
            /* prose visual contract */
        }
    }

    static void AddName(JsonElement node, List<string> found)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("name", out var nameEl))
            return;
        var name = nameEl.GetString()?.Trim() ?? "";
        if (name.Length == 0) return;
        if (found.Exists(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
            return;
        found.Add(name);
    }

    static void AppendList(string? raw, List<string> found)
    {
        foreach (var part in (raw ?? "").Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Length < 2) continue;
            if (found.Exists(existing => string.Equals(existing, part, StringComparison.OrdinalIgnoreCase)))
                continue;
            found.Add(part);
        }
    }

    static string Collapse(string value)
    {
        var parts = value.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}
