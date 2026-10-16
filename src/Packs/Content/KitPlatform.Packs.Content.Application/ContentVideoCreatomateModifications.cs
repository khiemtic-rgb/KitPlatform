using System.Text.Json;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Maps a Video Factory storyboard onto the Creatomate template configured for this org.
/// Element names come from template config, not from a second video pipeline.
/// </summary>
public static class ContentVideoCreatomateModifications
{
    public static Dictionary<string, string> Build(
        string? templateConfigJson,
        string title,
        string script,
        IReadOnlyList<ContentVideoCreatomateScene> scenes,
        string? voicePublicUrl)
    {
        var mods = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Title"] = title,
            ["Title.text"] = title,
            ["Script"] = script,
            ["Script.text"] = script,
        };

        Dictionary<string, string>? textKeys = null;
        Dictionary<string, string>? imageKeys = null;
        Dictionary<string, string>? videoKeys = null;
        string? voiceKey = null;
        try
        {
            using var cfg = JsonDocument.Parse(
                string.IsNullOrWhiteSpace(templateConfigJson) ? "{}" : templateConfigJson);
            if (cfg.RootElement.TryGetProperty("sceneTextKeys", out var tk) && tk.ValueKind == JsonValueKind.Object)
                textKeys = ReadMap(tk);
            if (cfg.RootElement.TryGetProperty("sceneImageKeys", out var ik) && ik.ValueKind == JsonValueKind.Object)
                imageKeys = ReadMap(ik);
            if (cfg.RootElement.TryGetProperty("sceneVideoKeys", out var vk) && vk.ValueKind == JsonValueKind.Object)
                videoKeys = ReadMap(vk);
            if (cfg.RootElement.TryGetProperty("voiceKey", out var voiceEl))
                voiceKey = voiceEl.GetString();

            if (cfg.RootElement.TryGetProperty("modifications", out var map)
                && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in map.EnumerateObject())
                {
                    var template = prop.Value.GetString() ?? "";
                    mods[prop.Name] = template
                        .Replace("{{script}}", script, StringComparison.OrdinalIgnoreCase)
                        .Replace("{{title}}", title, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        catch (JsonException)
        {
            /* bad template config — keep the default text keys */
        }

        foreach (var scene in scenes)
        {
            var beat = scene.Beat;
            mods[beat] = scene.Text;
            mods[$"{beat}.text"] = scene.Text;
            if (textKeys is not null && textKeys.TryGetValue(beat, out var textMod))
                mods[textMod] = scene.Text;

            var media = FirstNonEmpty(scene.MediaUrl);
            if (media is null)
                continue;

            mods[$"{beat}.image"] = media;
            if (imageKeys is not null && imageKeys.TryGetValue(beat, out var imageMod))
                mods[imageMod] = media;

            if (videoKeys is not null
                && videoKeys.TryGetValue(beat, out var videoMod)
                && ContentVideoRenderStatus.IsPlayableVideo(media))
            {
                mods[SourceKey(videoMod)] = media;
            }
        }

        if (!string.IsNullOrWhiteSpace(voicePublicUrl))
        {
            mods["Voice.source"] = voicePublicUrl;
            mods["Voice.audio"] = voicePublicUrl;
            if (!string.IsNullOrWhiteSpace(voiceKey))
            {
                mods[voiceKey] = voicePublicUrl;
                mods[SourceKey(voiceKey)] = voicePublicUrl;
            }
        }
        else if (!string.IsNullOrWhiteSpace(voiceKey))
        {
            var volumeKey = VoiceElement(voiceKey) + ".volume";
            if (!mods.ContainsKey(volumeKey))
                mods[volumeKey] = "0%";
        }

        return mods;
    }

    private static Dictionary<string, string> ReadMap(JsonElement obj)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in obj.EnumerateObject())
            map[prop.Name] = prop.Value.GetString() ?? prop.Name;
        return map;
    }

    private static string SourceKey(string key) =>
        key.EndsWith(".source", StringComparison.OrdinalIgnoreCase) ? key : key + ".source";

    private static string VoiceElement(string voiceKey)
    {
        if (voiceKey.EndsWith(".source", StringComparison.OrdinalIgnoreCase))
            return voiceKey[..^".source".Length];
        if (voiceKey.EndsWith(".audio", StringComparison.OrdinalIgnoreCase))
            return voiceKey[..^".audio".Length];
        return voiceKey;
    }

    private static string? FirstNonEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record ContentVideoCreatomateScene(string Beat, string Text, string? MediaUrl);
