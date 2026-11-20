using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// BTA dùng chung khóa Runway / ElevenLabs của Kit MKT. Phần này chỉ dựng request và tính credit, không gọi mạng.
/// </summary>
public static class BtaRunwayTextToImage
{
    public const string ModelId = "gen4_image";
    public const int MaxPromptChars = 1000;

    public static BtaRunwayTextToImageRequest Build(string? promptText, string? ratio, string? model, int? seed)
    {
        var modelId = (model ?? "").Trim();
        if (!string.Equals(modelId, ModelId, StringComparison.Ordinal))
            throw new InvalidOperationException("BTA_IMAGE_MODEL: chỉ nhận gen4_image.");
        var r = (ratio ?? "").Trim();
        if (r is not ("1920:1080" or "1280:720"))
            throw new InvalidOperationException("BTA_IMAGE_RATIO: chỉ nhận 1920:1080 hoặc 1280:720.");
        var prompt = (promptText ?? "").Trim();
        if (prompt.Length == 0)
            throw new InvalidOperationException("BTA_IMAGE_PROMPT: câu lệnh trống.");
        if (prompt.Length > MaxPromptChars)
            prompt = prompt[..MaxPromptChars];
        var credit = r == "1920:1080" ? 8 : 5;
        return new BtaRunwayTextToImageRequest(prompt, r, ModelId, seed, credit);
    }
}

public sealed record BtaRunwayTextToImageRequest(
    string PromptText,
    string Ratio,
    string Model,
    int? Seed,
    int CreditUocTinh);

public static class BtaImageCommandPlan
{
    public const string DefaultLenh = "docs/marketing/bta/hinh-ai/01/lenh-tao-anh.json";
    /// <summary>Trần mặc định khi file lệnh không ghi "tran_credit".</summary>
    public const int CreditCap = 72;

    /// <summary>Trần credit của cả tập, đọc từ "tran_credit" trong file lệnh (chủ kênh duyệt thì sửa số này).</summary>
    public static int Cap(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("tran_credit", out var cap) && cap.TryGetInt32(out var n) && n > 0
            ? n
            : CreditCap;
    }

    /// <summary>Tìm một ảnh theo tên file trong mọi lô của file lệnh.</summary>
    public static BtaImagePlanItem? FindItem(string json, string file)
    {
        var lots = new List<string>();
        using (var doc = JsonDocument.Parse(json))
        {
            foreach (var row in doc.RootElement.GetProperty("anh").EnumerateArray())
            {
                var lo = row.GetProperty("lo").GetString() ?? "";
                if (lo.Length > 0 && !lots.Contains(lo, StringComparer.Ordinal))
                    lots.Add(lo);
            }
        }
        foreach (var lo in lots)
        {
            var hit = Read(json, lo).Anh
                .FirstOrDefault(item => string.Equals(item.File, file, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return hit;
        }
        return null;
    }

    public static BtaImageBatchPlan Read(string json, string lo)
    {
        var batch = (lo ?? "").Trim();
        if (batch.Length == 0)
            throw new InvalidOperationException("BTA_IMAGE_LO: thiếu lô.");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var styles = root.GetProperty("phong_cach");
        var items = new List<BtaImagePlanItem>();
        foreach (var row in root.GetProperty("anh").EnumerateArray())
        {
            var rowLo = row.GetProperty("lo").GetString() ?? "";
            var file = row.GetProperty("file").GetString() ?? "";
            if (!string.Equals(rowLo, batch, StringComparison.Ordinal)
                && !string.Equals(file, batch, StringComparison.OrdinalIgnoreCase))
                continue;
            var kieu = row.GetProperty("kieu").GetString() ?? "";
            if (!styles.TryGetProperty(kieu, out var styleEl))
                throw new InvalidOperationException("BTA_IMAGE_KIEU: thiếu phong cách " + kieu + ".");
            var raw = ((styleEl.GetString() ?? "") + " " + (row.GetProperty("lenh").GetString() ?? "")).Trim();
            var copies = row.TryGetProperty("so_ban", out var copiesEl) && copiesEl.TryGetInt32(out var n) && n > 0 ? n : 1;
            int? seed = row.TryGetProperty("seed", out var seedEl) && seedEl.TryGetInt32(out var seedValue) ? seedValue : null;
            for (var k = 0; k < copies; k++)
            {
                var name = k == 0
                    ? file
                    : file.Replace(".png", "." + (char)('b' + k - 1) + ".png", StringComparison.Ordinal);
                BtaImageFiles.EnsureOutputName(name);
                int? itemSeed = seed is int s ? s + k : null;
                var built = BtaRunwayTextToImage.Build(raw, "1920:1080", BtaRunwayTextToImage.ModelId, itemSeed);
                items.Add(new BtaImagePlanItem(name, built.PromptText, built.Ratio, built.Model, itemSeed, built.CreditUocTinh));
            }
        }
        if (items.Count == 0)
            throw new InvalidOperationException("BTA_IMAGE_LO: không có ảnh trong lô " + batch + ".");
        return new BtaImageBatchPlan(batch, items, items.Sum(x => x.CreditUocTinh));
    }
}

public sealed record BtaImageBatchPlan(string Lo, IReadOnlyList<BtaImagePlanItem> Anh, int TongCredit);

public sealed record BtaImagePlanItem(
    string File,
    string PromptText,
    string Ratio,
    string Model,
    int? Seed,
    int CreditUocTinh,
    bool DaCo = false,
    bool DaGui = false);

public static partial class BtaImageFiles
{
    public static void EnsureOutputName(string? file)
    {
        var name = (file ?? "").Trim();
        if (!OutputName().IsMatch(name))
            throw new InvalidOperationException("BTA_IMAGE_FILE: tên file không hợp lệ.");
        if (Protected().IsMatch(name))
            throw new InvalidOperationException("BTA_IMAGE_FILE: không ghi đè a01–a08.");
    }

    public static bool IsProtected(string? file) => Protected().IsMatch((file ?? "").Trim());

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{1,80}\\.png$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutputName();

    [GeneratedRegex("^a0[1-8]([.-]|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Protected();
}

public static class BtaCostLedger
{
    public static int SumCredit(string? jsonl)
    {
        var sum = 0;
        foreach (var line in (jsonl ?? "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("creditUocTinh", out var credit) && credit.TryGetInt32(out var n))
                sum += n;
        }
        return sum;
    }

    public static bool HasFile(string? jsonl, string file)
    {
        foreach (var line in (jsonl ?? "").Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("file", out var name)
                && string.Equals(name.GetString(), file, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}

public static class BtaVoiceScript
{
    public const string DefaultLenh = "docs/marketing/bta/kich-ban/01-vietnam-zodiac-cat.giong-v4.txt";
    public const string ProbeSentence =
        "[warm, curious] You might know the Chinese zodiac from a placemat at a Chinese restaurant.";
    public const int ProbeMaxChars = 200;

    static readonly Regex PartHead = new(
        @"^#\s*=+\s*PHẦN\s+(\d+)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<BtaVoicePart> Split(string script)
    {
        var parts = new Dictionary<int, List<string>>();
        var current = 0;
        foreach (var line in (script ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var head = PartHead.Match(line.Trim());
            if (head.Success)
            {
                current = int.Parse(head.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (!parts.ContainsKey(current))
                    parts[current] = [];
                continue;
            }
            if (current == 0) continue;
            parts[current].Add(line);
        }
        if (!parts.ContainsKey(1) || !parts.ContainsKey(2) || !parts.ContainsKey(3))
            throw new InvalidOperationException("BTA_VOICE_PART: lời đọc phải có phần 1, 2 và 3.");
        return new[] { 1, 2, 3 }
            .Select(n => new BtaVoicePart(n, Spoken(parts[n])))
            .ToList();
    }

    public static string Spoken(IEnumerable<string> lines)
    {
        var kept = lines.Where(line => !line.TrimStart().StartsWith('#')).ToList();
        return string.Join("\n", kept).Trim();
    }

    public static int EstimateCredit(string text) => (text ?? "").Length;
}

public sealed record BtaVoicePart(int Phan, string Text);

public static class BtaElevenLabsSpeech
{
    public const string VoiceId = "wJGJJlqiL7g3nHR8c3SQ";
    public const string VoiceName = "BTA_Narrator";
    public const double Stability = 0.5;
    public const double SimilarityBoost = 0.75;
    public const double Style = 0.0;
    public const bool UseSpeakerBoost = true;
    public const string ModelDisplayName = "Eleven v4";

    /// <summary>Mã API của Eleven v4 (ra mắt 28/9/2026). Dùng khi khóa không có quyền models_read.</summary>
    public const string ModelIdFallback = "eleven_v4";

    /// <summary>GET v1/models bị từ chối vì khóa thiếu quyền, không phải lỗi mạng hay lỗi máy chủ.</summary>
    public static bool ModelsListDenied(string? message)
    {
        var m = message ?? "";
        return m.StartsWith("ElevenLabs models 401", StringComparison.Ordinal)
               || m.StartsWith("ElevenLabs models 403", StringComparison.Ordinal);
    }

    public static BtaElevenLabsSpeechRequest Build(string? text, string? modelId, string? voiceId)
    {
        var model = (modelId ?? "").Trim();
        if (model.Length == 0)
            throw new InvalidOperationException("BTA_VOICE_MODEL: thiếu mã model.");
        var voice = string.IsNullOrWhiteSpace(voiceId) ? VoiceId : voiceId.Trim();
        var spoken = (text ?? "").Trim();
        if (spoken.Length == 0)
            throw new InvalidOperationException("BTA_VOICE_TEXT: lời đọc trống.");
        return new BtaElevenLabsSpeechRequest(
            spoken, model, voice, Stability, SimilarityBoost, Style, UseSpeakerBoost);
    }
}

public sealed record BtaElevenLabsSpeechRequest(
    string Text,
    string ModelId,
    string VoiceId,
    double Stability,
    double SimilarityBoost,
    double Style,
    bool UseSpeakerBoost)
{
    public string Path => "v1/text-to-speech/" + Uri.EscapeDataString(VoiceId) + "/with-timestamps";

    public object Body() => new
    {
        text = Text,
        model_id = ModelId,
        voice_settings = new
        {
            stability = Stability,
            similarity_boost = SimilarityBoost,
            style = Style,
            use_speaker_boost = UseSpeakerBoost,
        },
    };
}

/// <summary>
/// Nguồn khóa của nhánh BTA: dùng chung khóa sản xuất của Kit MKT (khóa đã niêm phong mà Famixa đang chạy).
/// Khóa là khóa tài khoản nhà cung cấp; nó được đăng ký theo model Famixa dùng, nên BTA hỏi đúng model đó.
/// </summary>
public static class BtaKitMktKeys
{
    public const string RunwayCredentialModel = "gen4_turbo";
    public const string ElevenLabsCredentialModel = "eleven_v3";

    /// <summary>Khóa API Runway luôn bắt đầu bằng key_. Giá trị khác thì Runway trả 401 ngay.</summary>
    public static bool LooksLikeRunwayKey(string? key) =>
        (key ?? "").Trim().StartsWith("key_", StringComparison.Ordinal);

    /// <summary>Tenant đang đăng nhập không có (hoặc có hai) khóa sản xuất cho nhà cung cấp này.</summary>
    public static bool IsMissingCredential(string? message) =>
        message is "PRODUCTION_CREDENTIAL_UNRESOLVED" or "PRODUCTION_CREDENTIAL_AMBIGUOUS";
}

public static class BtaVoiceTimingReader
{
    public static BtaVoiceTimingRead Read(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var alignment = ReadTrack(root, "alignment");
        var normalized = root.TryGetProperty("normalized_alignment", out var extra)
            ? ReadTrack(extra, "normalized_alignment")
            : null;
        return new BtaVoiceTimingRead(
            alignment.Count,
            alignment.FirstStart,
            alignment.LastEnd,
            normalized?.Count ?? 0);
    }

    static Track ReadTrack(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty("characters", out var chars))
        {
            if (parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var nested))
                return ReadTrack(nested, name);
            throw new InvalidOperationException("BTA_VOICE_TIMING: thiếu " + name + ".");
        }
        if (chars.ValueKind != JsonValueKind.Array || chars.GetArrayLength() == 0)
            throw new InvalidOperationException("BTA_VOICE_TIMING: không có mốc ký tự.");
        var starts = parent.GetProperty("character_start_times_seconds");
        var ends = parent.GetProperty("character_end_times_seconds");
        if (starts.GetArrayLength() != chars.GetArrayLength() || ends.GetArrayLength() != chars.GetArrayLength())
            throw new InvalidOperationException("BTA_VOICE_TIMING: số mốc không khớp số ký tự.");
        return new Track(chars.GetArrayLength(), starts[0].GetDouble(), ends[ends.GetArrayLength() - 1].GetDouble());
    }

    sealed record Track(int Count, double FirstStart, double LastEnd);
}

public sealed record BtaVoiceTimingRead(
    int CharacterCount,
    double FirstStartSeconds,
    double LastEndSeconds,
    int NormalizedCharacterCount);

/// <summary>`Lenh`: file lenh-tao-anh.json của tập (vd. docs/marketing/bta/hinh-ai/02/lenh-tao-anh.json). Bỏ trống = tập 01.</summary>
public sealed record BtaTextToImageCommand(string PromptText, string Ratio, string Model, int? Seed, string File, string? Lenh = null);

public sealed record BtaTextToImageResponse(string TaskId, int CreditUocTinh);

public sealed record BtaMediaTaskResponse(string Status, string? OutputUrl, string? FailureCode, string? Failure);

public sealed record BtaImagePlanCommand(string? Lenh, string Lo);

public sealed record BtaImagePlanResponse(
    string Lo,
    int TongCredit,
    int CreditDaDung,
    int CreditConLai,
    IReadOnlyList<BtaImagePlanItem> Anh);

public sealed record BtaImageSaveCommand(
    string File,
    string TaskId,
    string? OutputUrl,
    int CreditUocTinh,
    string Model,
    string Ratio,
    string KetQua,
    string? Loi,
    string? Lenh = null);

public sealed record BtaImageSaveResponse(string File, string KetQua, string? Loi, bool DaGhi);

public sealed record BtaVoicePlanCommand(string? Lenh, int Phan);

public sealed record BtaVoicePlanResponse(int Phan, int SoKyTu, int CreditUocTinh, string GhiChu, string DoanDau);

public sealed record BtaVoiceProbePreview(string Text, int SoKyTu, int CreditUocTinh, string GhiChu);

public sealed record BtaVoiceTakeCommand(string? Lenh, int Phan, string XacNhan);

public sealed record BtaVoiceProbeCommand(bool XacNhan);

public sealed record BtaVoiceTakeResponse(
    string FileMp3,
    string FileTiming,
    string ModelId,
    string VoiceId,
    string? RequestId,
    int SoKyTu,
    int CreditUocTinh,
    string? Loi);

public interface IBtaKitMktMediaService
{
    Task<BtaTextToImageResponse> CreateTextToImageAsync(BtaTextToImageCommand request, string? caller, CancellationToken cancellationToken);
    Task<BtaMediaTaskResponse> GetTaskAsync(string taskId, CancellationToken cancellationToken);
    Task<BtaImagePlanResponse> PlanImagesAsync(BtaImagePlanCommand request, CancellationToken cancellationToken);
    Task<BtaImageSaveResponse> SaveImageAsync(BtaImageSaveCommand request, CancellationToken cancellationToken);
    Task<BtaVoicePlanResponse> PlanVoiceAsync(BtaVoicePlanCommand request, CancellationToken cancellationToken);
    BtaVoiceProbePreview ProbePreview();
    Task<BtaVoiceTakeResponse> SynthesizePartAsync(BtaVoiceTakeCommand request, CancellationToken cancellationToken);
    Task<BtaVoiceTakeResponse> SynthesizeProbeAsync(BtaVoiceProbeCommand request, CancellationToken cancellationToken);
}
