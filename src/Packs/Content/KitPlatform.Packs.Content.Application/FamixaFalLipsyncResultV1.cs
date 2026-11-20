using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>Fal queue COMPLETED bodies nest video under payload/data. 0 provider calls.</summary>
public static class FamixaFalLipsyncResultV1
{
    public const string SuiteId = "FAMIXA_FAL_LIPSYNC_RESULT_V1";

    public static string? ReadVideoUrl(JsonElement root) => FindVideoUrl(root, 0);

    public static string? ReadVideoUrl(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var hit = ReadVideoUrl(doc.RootElement);
            if (hit != null) return hit;
        }
        catch (JsonException)
        {
            /* HTML / text — scan raw */
        }

        return ScanRawVideoUrl(json);
    }

    /// <summary>Usage / Assets lists can contain many jobs — only take the matching request_id.</summary>
    public static string? ReadPlatformVideoUrl(string? json, string requestId)
    {
        var want = (requestId ?? "").Trim();
        if (want.Length == 0 || string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return ReadVideoUrl(root);

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                return ReadMatchingOutput(items, want);

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                return ReadMatchingOutput(assets, want);

            return ReadOutputOnly(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Usage found this request but json_output is not a video — stop polling.</summary>
    public static string? ReadPlatformNoVideoReason(string? json, string requestId)
    {
        var want = (requestId ?? "").Trim();
        if (want.Length == 0 || string.IsNullOrWhiteSpace(json)) return null;
        if (ReadPlatformVideoUrl(json, want) != null) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
                return null;
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = item.TryGetProperty("request_id", out var rid) && rid.ValueKind == JsonValueKind.String
                    ? rid.GetString() ?? ""
                    : "";
                if (!id.Equals(want, StringComparison.OrdinalIgnoreCase)) continue;
                if (item.TryGetProperty("json_output", out var output) && LooksLikeValidationOutput(output))
                    return $"Fal từ chối {ReadValidationHint(output)} — job không có mp4. Hỏi lại sẽ không ra file.";
                return "Fal Usage có job này — không có video.url. Job không tạo được file môi. Gắn Fal · 0$ nếu fal.ai có link. Hỏi lại sẽ không ra file.";
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string DescribePlatformPeek(string? json, string requestId)
    {
        var want = (requestId ?? "").Trim();
        var keys = DescribeKeys(json);
        if (string.IsNullOrWhiteSpace(json)) return $"keys={keys}";
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return $"keys={keys}";

            if (root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                return $"keys={keys} items={items.GetArrayLength()} {DescribeMatch(items, want)} out={DescribeOutputShape(items, want)}";
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                return $"keys={keys} assets={assets.GetArrayLength()} {DescribeMatch(assets, want)}";
            return $"keys={keys}";
        }
        catch (JsonException)
        {
            return $"keys={keys}";
        }
    }

    public static string? ScanRawVideoUrl(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        foreach (Match m in Regex.Matches(raw, @"https://[^\s""'\\<>]+", RegexOptions.IgnoreCase))
        {
            var url = m.Value.TrimEnd('.', ',', ')', ']', ';');
            if (LooksLikeFalOutputVideo(url)) return url;
        }

        return null;
    }

    public static string DescribeKeys(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return "";
            return string.Join(",", doc.RootElement.EnumerateObject().Select(p => p.Name).Take(12));
        }
        catch (JsonException)
        {
            return "";
        }
    }

    private static string? ReadMatchingOutput(JsonElement array, string requestId)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var id = item.TryGetProperty("request_id", out var rid) && rid.ValueKind == JsonValueKind.String
                ? rid.GetString() ?? ""
                : "";
            if (!id.Equals(requestId, StringComparison.OrdinalIgnoreCase)) continue;
            return ReadOutputOnly(item);
        }

        return null;
    }

    /// <summary>Never walk json_input — that is the mute take uploaded to Fal.</summary>
    private static string? ReadOutputOnly(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) return ReadVideoUrl(item);
        if (item.TryGetProperty("json_output", out var output))
        {
            var hit = output.ValueKind == JsonValueKind.String
                ? ReadVideoUrl(output.GetString())
                : ReadVideoUrl(output);
            if (hit != null) return hit;
            var raw = output.ValueKind is JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.String
                ? ScanRawVideoUrl(output.ValueKind == JsonValueKind.String ? output.GetString() : output.GetRawText())
                : null;
            if (raw != null) return raw;
        }

        if (item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
        {
            var s = url.GetString();
            if (LooksLikeFalOutputVideo(s)) return s;
        }

        return null;
    }

    private static string DescribeMatch(JsonElement array, string requestId)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var id = item.TryGetProperty("request_id", out var rid) && rid.ValueKind == JsonValueKind.String
                ? rid.GetString() ?? ""
                : "";
            if (!id.Equals(requestId, StringComparison.OrdinalIgnoreCase)) continue;
            var itemKeys = string.Join(",", item.EnumerateObject().Select(p => p.Name).Take(12));
            var outputKind = item.TryGetProperty("json_output", out var output)
                ? output.ValueKind.ToString()
                : "missing";
            return $"match=yes itemKeys={itemKeys} json_output={outputKind}";
        }

        return "match=no";
    }

    private static string DescribeOutputShape(JsonElement array, string requestId)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var id = item.TryGetProperty("request_id", out var rid) && rid.ValueKind == JsonValueKind.String
                ? rid.GetString() ?? ""
                : "";
            if (!id.Equals(requestId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!item.TryGetProperty("json_output", out var output)) return "json_output=missing";
            return RedactShape(output, 0);
        }

        return "json_output=nomatch";
    }

    private static string RedactShape(JsonElement el, int depth)
    {
        if (depth > 4) return "…";
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString() ?? "";
            if (s.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(s, UriKind.Absolute, out var uri))
                return uri.Host + uri.AbsolutePath;
            return $"str:{Math.Min(s.Length, 80)}";
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            var parts = new List<string>();
            foreach (var p in el.EnumerateObject().Take(12))
                parts.Add($"{p.Name}={RedactShape(p.Value, depth + 1)}");
            return "{" + string.Join(",", parts) + "}";
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            var parts = el.EnumerateArray().Take(3).Select(x => RedactShape(x, depth + 1));
            return "[" + string.Join("|", parts) + "]";
        }
        return el.ValueKind.ToString();
    }

    private static string? FindVideoUrl(JsonElement el, int depth)
    {
        if (depth > 8) return null;
        if (el.ValueKind == JsonValueKind.String)
        {
            var s = el.GetString() ?? "";
            if (LooksLikeFalOutputVideo(s)) return s;
            if (s.Length > 8 && (s[0] is '{' or '['))
            {
                try
                {
                    using var nested = JsonDocument.Parse(s);
                    return FindVideoUrl(nested.RootElement, depth + 1);
                }
                catch (JsonException)
                {
                    /* not nested json */
                }
            }

            return null;
        }

        if (el.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in new[] { "video", "video_url", "url", "file_url", "output", "payload", "data", "response", "result", "json_output" })
            {
                if (!el.TryGetProperty(name, out var child)) continue;
                var hit = FindVideoUrl(child, depth + 1);
                if (hit != null) return hit;
            }

            foreach (var p in el.EnumerateObject())
            {
                if (p.Name.Equals("json_input", StringComparison.OrdinalIgnoreCase)
                    || p.Name.Equals("input", StringComparison.OrdinalIgnoreCase))
                    continue;
                var hit = FindVideoUrl(p.Value, depth + 1);
                if (hit != null) return hit;
            }
        }

        if (el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                var hit = FindVideoUrl(item, depth + 1);
                if (hit != null) return hit;
            }
        }

        return null;
    }

    private static bool LooksLikeValidationOutput(JsonElement output)
    {
        if (output.ValueKind == JsonValueKind.Object
            && output.TryGetProperty("detail", out var detail)
            && detail.ValueKind == JsonValueKind.Array)
            return true;
        if (output.ValueKind == JsonValueKind.String)
        {
            var s = output.GetString() ?? "";
            return s.Contains("\"detail\"", StringComparison.OrdinalIgnoreCase)
                   && s.Contains("\"loc\"", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    internal static string ReadValidationHint(JsonElement output)
    {
        if (LooksLikeIntType(output))
            return "tọa độ mặt (cần số nguyên pixel)";
        if (output.ValueKind == JsonValueKind.Object
            && output.TryGetProperty("detail", out var detail)
            && detail.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in detail.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                var msg = row.TryGetProperty("msg", out var m) && m.ValueKind == JsonValueKind.String
                    ? (m.GetString() ?? "").Trim()
                    : "";
                if (msg.Length == 0 || msg.StartsWith("http", StringComparison.OrdinalIgnoreCase)) continue;
                var loc = "";
                if (row.TryGetProperty("loc", out var l) && l.ValueKind == JsonValueKind.Array)
                    loc = string.Join(".", l.EnumerateArray().Select(x =>
                        x.ValueKind == JsonValueKind.String ? x.GetString() ?? ""
                        : x.ValueKind == JsonValueKind.Number ? x.GetRawText()
                        : ""));
                loc = loc.Replace("body.", "", StringComparison.OrdinalIgnoreCase).Trim('.');
                var hint = string.IsNullOrWhiteSpace(loc) ? msg : $"{loc}: {msg}";
                return hint.Length <= 180 ? hint : hint[..180];
            }
        }

        return "input (validation)";
    }

    private static bool LooksLikeIntType(JsonElement output)
    {
        if (output.ValueKind == JsonValueKind.Object
            && output.TryGetProperty("detail", out var detail)
            && detail.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in detail.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object) continue;
                if (row.TryGetProperty("type", out var type)
                    && type.ValueKind == JsonValueKind.String
                    && string.Equals(type.GetString(), "int_type", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return output.ValueKind == JsonValueKind.String
            && (output.GetString() ?? "").Contains("int_type", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeFalOutputVideo(string? raw)
    {
        var s = (raw ?? "").Trim();
        if (s.Length < 12 || !s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("queue.fal.run", StringComparison.OrdinalIgnoreCase)) return false;
        if (s.Contains("/veo-video", StringComparison.OrdinalIgnoreCase)) return false;
        return s.Contains(".mp4", StringComparison.OrdinalIgnoreCase)
               || s.Contains("fal.media", StringComparison.OrdinalIgnoreCase);
    }
}
