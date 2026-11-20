using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content.Production;

/// <summary>Ảnh của một shot (nền, lớp giữa, nhân vật theo nét mặt) và dấu của chúng. Dùng chung cho dựng và tạo chuyển động.</summary>
public static class LinesheetShotAssets
{
    public sealed record Resolved(
        LinesheetFileRecord? Background,
        LinesheetFileRecord? Mid,
        IReadOnlyDictionary<string, LinesheetFileRecord> Characters,
        IReadOnlyList<string> Shas,
        LinesheetFileRecord? Keyframe = null);

    /// <summary>Tên ảnh cảnh cho shot: theo mã shot, không có thì theo tên cảnh.</summary>
    public static string? KeyframeName(LinesheetShot shot, LinesheetSettings settings) =>
        settings.Keyframes.TryGetValue(shot.ShotId, out var k) && !string.IsNullOrWhiteSpace(k) ? k.Trim()
        : settings.Keyframes.TryGetValue(shot.Scene, out var s) && !string.IsNullOrWhiteSpace(s) ? s.Trim()
        : null;

    public static Resolved Resolve(LinesheetShot shot, IReadOnlyList<LinesheetFileRecord> files, LinesheetSettings settings)
    {
        var byKind = files.ToLookup(f => f.Kind);
        if (KeyframeName(shot, settings) is { } kfName)
        {
            var kfKey = LinesheetFileKinds.KeyframePrefix + FamixaLinesheetService.SafeName(kfName);
            var kf = byKind[LinesheetFileKinds.Character].FirstOrDefault(f => f.FileKey == kfKey)
                     ?? throw new LinesheetValidationException([new(0, shot.ShotId, LinesheetErrorCodes.MissingLayer,
                         $"Thiếu ảnh cảnh keyframes/{kfName}.png cho shot {shot.ShotId}.")]);
            return new Resolved(null, null, new Dictionary<string, LinesheetFileRecord>(), ["keyframe", kf.Sha256], kf);
        }

        LinesheetFileRecord Need(string kind, string key) =>
            byKind[kind].FirstOrDefault(f => f.FileKey == key)
            ?? throw new LinesheetValidationException([new(0, shot.ShotId, LinesheetErrorCodes.MissingLayer,
                $"Thiếu ảnh {kind} '{key}' cho shot {shot.ShotId}.")]);

        var bg = Need(LinesheetFileKinds.LayerBackground, shot.Setting);
        var mid = Need(LinesheetFileKinds.LayerMid, shot.Setting);
        // nét mặt (M1.1): dùng characters/<ID>_<nét>.png nếu đã tải, không thì ảnh thường <ID>.png
        LinesheetFileRecord CharacterFile(string id)
        {
            var expr = LinesheetShotPlanner.ExpressionFor(shot, id);
            return (expr.Length > 0 ? byKind[LinesheetFileKinds.Character].FirstOrDefault(f => f.FileKey == id + "_" + expr) : null)
                   ?? Need(LinesheetFileKinds.Character, id);
        }

        var chars = shot.Characters.Select(LinesheetShotPlanner.CharacterId).Distinct()
            .ToDictionary(id => id, CharacterFile);
        // cấu hình nhân vật (chiều cao, tên) và "chat_self" đổi thì hình đổi: đưa vào mã cache
        var look = LinesheetRenderPlan.Sha256Hex(JsonSerializer.Serialize(new
        {
            chars = chars.Keys.OrderBy(k => k, StringComparer.Ordinal).Select(k => settings.Characters[k]),
            self = settings.ChatSelf,
            light = settings.Settings.TryGetValue(shot.Setting, out var sd) ? new { sd.Light, sd.LightStrength } : null,
        }));
        var shas = new[] { bg.Sha256, mid.Sha256, look }
            .Concat(chars.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value.Sha256))
            .ToList();
        return new Resolved(bg, mid, chars, shas);
    }
}

/// <summary>Một đoạn chuyển động đã tạo cho một shot (lưu trong motion/manifest.json).</summary>
public sealed class LinesheetMotionEntry
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("storage_key")] public string StorageKey { get; set; } = "";
    [JsonPropertyName("seconds")] public int Seconds { get; set; }
    [JsonPropertyName("model")] public string Model { get; set; } = "";
    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
}

/// <summary>
/// Chuyển động bằng video AI (thêm ngày 2026-11-04): ảnh tĩnh của shot (đúng như bản dựng thường, không có chữ)
/// được gửi sang mô hình ảnh-thành-video, rồi đoạn video thay cho ảnh tĩnh khi dựng 16:9. Tiếng vẫn do M1 ghép.
/// </summary>
public static class LinesheetMotionPlan
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Runway gen4_turbo nhận 5 hoặc 10 giây.</summary>
    public static int ClipSeconds(int shotMs, int preferred = 10) => shotMs <= 5200 || preferred <= 5 ? 5 : 10;

    /// <summary>
    /// Câu lệnh mặc định (tiếng Anh, mô hình hiểu tốt hơn). Người nói được chỉ theo vị trí trái, giữa, phải
    /// vì mô hình không biết tên nhân vật.
    /// </summary>
    public static string DefaultPrompt(LinesheetShotSpan span)
    {
        var shot = span.Shot;
        var ids = shot.Characters.Select(LinesheetShotPlanner.CharacterId).Distinct().ToList();
        string Where(string id)
        {
            var i = ids.IndexOf(id);
            return ids.Count switch
            {
                1 => "the character",
                2 => i == 0 ? "the character on the left" : "the character on the right",
                _ => i == 0 ? "the character on the left" : i == ids.Count - 1 ? "the character on the right" : "the character in the middle",
            };
        }

        var speakers = span.Rows.Where(r => r.IsLine && ids.Contains(r.Speaker)).Select(r => r.Speaker).Distinct().ToList();
        var action = speakers.Count switch
        {
            0 => ids.Count == 0 ? "The scene is quiet; only light and small background details move." : "The characters stay quiet, thinking.",
            1 => $"{Cap(Where(speakers[0]))} is speaking, with small head nods and hand gestures; others listen and react.",
            _ => "The characters take turns speaking, with small head nods: " + string.Join(", then ", speakers.Select(Where)) + ".",
        };
        // không gọi tên kiểu vẽ (bài học 2026-11-05: câu "2D flat webtoon" còn sót sau khi chuyển sang 3D, kéo hình về phẳng)
        return "Animate this exact image. Keep the art style, characters, faces, outfits, lighting, colors and background exactly the same. "
               + "Locked static camera, no zoom, no cuts, no new people or objects, no text. "
               + LockRules
               + action + " Subtle natural motion: breathing, blinking, small head turns. Smooth gentle animation.";
    }

    /// <summary>
    /// Quy tắc giữ chỗ, thêm vào mọi câu lệnh (bài học 2026-11-04: nhân vật đi xuyên bàn, xuyên tường khi câu lệnh để mở).
    /// Miệng ngậm: mô hình video không nghe được tiếng nên môi động lung tung; môi do bước ghép môi làm theo đúng lời.
    /// </summary>
    public const string LockRules =
        "Everyone stays exactly where they are: seated people stay seated, nobody stands up, walks or leaves the frame, "
        + "nothing passes through tables or walls. Mouths stay gently closed; only eyes, heads and hands move a little. ";

    static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Dấu của đoạn chuyển động: đổi ảnh, nét mặt, bố cục, câu lệnh hoặc độ dài thì phải tạo lại.</summary>
    public static string MotionKey(LinesheetShot shot, IEnumerable<string> assetShas, string prompt, int seconds, string model) =>
        LinesheetRenderPlan.Sha256Hex("motion1|" + model + "|" + seconds.ToString(Inv) + "|" + shot.Setting + "|"
                                      + string.Join(";", shot.Characters) + "|" + shot.Expressions + "|" + shot.Focus + "|"
                                      + string.Join(";", assetShas) + "|" + prompt)[..16];

    /// <summary>Ảnh tĩnh đầu vào (JPEG) từ kết quả ghép lớp, không chữ, không chuyển máy.</summary>
    public static IReadOnlyList<string> StillArgs(LinesheetRenderPlan.ShotImages images, int width, int height, string outputJpg)
    {
        var args = new List<string> { "-v", "error", "-y" };
        if (!images.IsParallax)
        {
            args.AddRange(["-i", images.FlatPng ?? throw new InvalidOperationException(LinesheetErrorCodes.MissingLayer),
                "-vf", $"scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}"]);
        }
        else
        {
            args.AddRange(["-i", images.BackgroundPng!, "-i", images.FrontPng!, "-filter_complex",
                $"[0:v]scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height}[b];[1:v]scale=-1:{height}[f];[b][f]overlay=x=(W-w)/2:y=0"]);
        }

        args.AddRange(["-frames:v", "1", "-q:v", "3", outputJpg]);
        return args;
    }

    /// <summary>
    /// Đoạn chuyển động thay cho ảnh tĩnh: cắt đúng số khung của shot. Shot dài hơn đoạn video thì chạy chậm lại
    /// (tối đa 1,6 lần); dài hơn nữa thì chạy xuôi rồi ngược cho đủ.
    /// </summary>
    public static IReadOnlyList<string> MotionArgs(
        LinesheetShotSpan span, string motionMp4, int clipSeconds,
        IReadOnlyList<LinesheetRenderPlan.PlacedCard> cards, int width, int height, int fps, string outputPath,
        IReadOnlyList<LinesheetPlacedLip>? lips = null)
    {
        var g = new StringBuilder(BaseChain(span.Frames, clipSeconds, width, height, fps));
        var args = new List<string> { "-v", "error", "-y", "-i", motionMp4 };
        var cur = "v0";
        var idx = 1;
        foreach (var (lip, i) in (lips ?? []).Select((l, i) => (l, i)))
        {
            args.AddRange(["-i", lip.Mp4]);
            g.Append(LinesheetLipsyncPlan.OverlayGraph(lip, idx, cur, "lo" + i, fps));
            cur = "lo" + i;
            idx++;
        }

        return LinesheetRenderPlan.FinishShotArgs(args, g, cur, idx, span, cards, fps, outputPath);
    }

    /// <summary>
    /// Chuỗi lọc chung: đoạn video AI thành đúng số khung của shot, đặt tên [v0]. Ghép môi dùng lại chuỗi này nên khung
    /// của đoạn ghép môi trùng đúng khung của shot.
    /// </summary>
    public static string BaseChain(int frames, int clipSeconds, int width, int height, int fps)
    {
        var clipFrames = Math.Max(1, clipSeconds * fps - 2);
        var g = new StringBuilder($"[0:v]scale={width}:{height}:force_original_aspect_ratio=increase,crop={width}:{height},setsar=1");
        if (frames > clipFrames && frames <= clipFrames * 1.6)
            g.Append(",setpts=PTS*").Append((frames / (double)clipFrames).ToString("0.0000", Inv));
        g.Append(",fps=").Append(fps.ToString(Inv));
        if (frames > clipFrames * 1.6)
            g.Append(",split[ma][mb];[mb]reverse[mr];[ma][mr]concat=n=2:v=1:a=0,loop=loop=-1:size=")
             .Append((clipFrames * 2).ToString(Inv)).Append(":start=0");
        g.Append(",tpad=stop_mode=clone:stop_duration=2[v0]");
        return g.ToString();
    }
}
