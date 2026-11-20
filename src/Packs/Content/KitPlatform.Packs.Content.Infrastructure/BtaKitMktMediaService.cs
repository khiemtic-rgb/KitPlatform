using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Tạo ảnh và giọng BTA bằng khóa Kit MKT. Chỉ ghi file vào repo khi API chạy Development (máy chủ kênh).
/// Khóa lấy qua IProductionCredentialResolver: cùng khóa sản xuất đã niêm phong mà Famixa dùng, theo tenant đang đăng nhập.
/// </summary>
internal sealed class BtaKitMktMediaService : IBtaKitMktMediaService
{
    const string KenhRelative = "docs/marketing/bta/kich-ban/kenh.json";
    const string CreditNote = "ước tính khoảng 1 credit mỗi ký tự, chưa phải số trừ thực tế";

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    static readonly object Gate = new();
    readonly ContentRunwayClient _runway;
    readonly ContentElevenLabsClient _eleven;
    readonly IHostEnvironment _env;
    readonly IProductionCredentialResolver _credentials;

    public BtaKitMktMediaService(
        ContentRunwayClient runway,
        ContentElevenLabsClient eleven,
        IHostEnvironment env,
        IProductionCredentialResolver credentials)
    {
        _runway = runway;
        _eleven = eleven;
        _env = env;
        _credentials = credentials;
    }

    public async Task<BtaTextToImageResponse> CreateTextToImageAsync(
        BtaTextToImageCommand request,
        string? caller,
        CancellationToken cancellationToken)
    {
        RequireDev();
        BtaImageFiles.EnsureOutputName(request.File);
        var built = BtaRunwayTextToImage.Build(request.PromptText, request.Ratio, request.Model, request.Seed);
        var lenh = LenhOf(request.Lenh);
        var planned = PlannedItem(request.File, lenh);
        if (!string.Equals(built.PromptText, planned.PromptText, StringComparison.Ordinal)
            || !string.Equals(built.Ratio, planned.Ratio, StringComparison.Ordinal)
            || built.Seed != planned.Seed)
            throw new InvalidOperationException("BTA_IMAGE_PROMPT: câu lệnh không khớp lenh-tao-anh.json.");

        var dest = Path.Combine(ImageFolder(lenh), request.File);
        if (File.Exists(dest))
            throw new InvalidOperationException("BTA_IMAGE_SKIP: ảnh đã có, không tạo lại.");
        var ledger = await ReadIfExists(LedgerPath(lenh), cancellationToken);
        if (BtaCostLedger.HasFile(ledger, request.File))
            throw new InvalidOperationException("BTA_IMAGE_SKIP: ảnh này đã gửi, không chạy lại.");
        var spent = BtaCostLedger.SumCredit(ledger);
        var cap = BtaImageCommandPlan.Cap(File.ReadAllText(UnderBta(lenh)));
        if (spent + built.CreditUocTinh > cap)
            throw new InvalidOperationException(
                $"BTA_IMAGE_CAP: đã dùng {spent} credit, thêm {built.CreditUocTinh} vượt {cap}.");

        var key = await RunwayKeyAsync(cancellationToken);
        var taskId = await _runway.CreateTextToImageAsync(
            built.PromptText, built.Ratio, built.Model, built.Seed, cancellationToken, key);
        Append(LedgerPath(lenh), new
        {
            luc = Now(),
            nguoiGoi = string.IsNullOrWhiteSpace(caller) ? "admin" : caller.Trim(),
            model = built.Model,
            ratio = built.Ratio,
            file = request.File,
            taskId,
            creditUocTinh = built.CreditUocTinh,
        });
        return new BtaTextToImageResponse(taskId, built.CreditUocTinh);
    }

    public async Task<BtaMediaTaskResponse> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        RequireDev();
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("BTA_IMAGE_TASK: thiếu task id.");
        var key = await RunwayKeyAsync(cancellationToken);
        var read = await _runway.GetTaskAsync(taskId.Trim(), cancellationToken, key);
        return new BtaMediaTaskResponse(read.Status, read.VideoUrl, read.FailureCode, read.Error);
    }

    public async Task<BtaImagePlanResponse> PlanImagesAsync(BtaImagePlanCommand request, CancellationToken cancellationToken)
    {
        RequireDev();
        var lenh = LenhOf(request.Lenh);
        var json = await File.ReadAllTextAsync(UnderBta(lenh), cancellationToken);
        var plan = BtaImageCommandPlan.Read(json, request.Lo);
        var folder = ImageFolder(lenh);
        var ledger = await ReadIfExists(LedgerPath(lenh), cancellationToken);
        var items = plan.Anh.Select(item => item with
        {
            DaCo = File.Exists(Path.Combine(folder, item.File)),
            DaGui = BtaCostLedger.HasFile(ledger, item.File),
        }).ToList();
        var willSend = items.Where(item => !item.DaCo && !item.DaGui).Sum(item => item.CreditUocTinh);
        var spent = BtaCostLedger.SumCredit(ledger);
        return new BtaImagePlanResponse(plan.Lo, willSend, spent, BtaImageCommandPlan.Cap(json) - spent, items);
    }

    public async Task<BtaImageSaveResponse> SaveImageAsync(BtaImageSaveCommand request, CancellationToken cancellationToken)
    {
        RequireDev();
        BtaImageFiles.EnsureOutputName(request.File);
        var lenh = LenhOf(request.Lenh);
        PlannedItem(request.File, lenh);
        var dest = Path.Combine(ImageFolder(lenh), request.File);
        if (File.Exists(dest))
            return new BtaImageSaveResponse(request.File, "BO_QUA", "ảnh đã có", false);
        var costPath = Path.Combine(ImageFolder(lenh), "chi-phi.jsonl");
        var existing = await ReadIfExists(costPath, cancellationToken);
        if (BtaCostLedger.HasFile(existing, request.File))
            return new BtaImageSaveResponse(request.File, "BO_QUA", "đã ghi chi phí, không ghi đè", false);

        var result = (request.KetQua ?? "").Trim();
        string? error = string.IsNullOrWhiteSpace(request.Loi) ? null : request.Loi.Trim();
        var wrote = false;
        if (string.Equals(result, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            var url = (request.OutputUrl ?? "").Trim();
            if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                result = "LOI";
                error = "BTA_IMAGE_URL: link ảnh phải là https.";
            }
            else
            {
                var bytes = await _runway.DownloadOutputAsync(url, cancellationToken);
                if (!LooksLikeImage(bytes))
                {
                    result = "LOI";
                    error = "BTA_IMAGE_BYTES: file tải về không phải PNG/JPEG lớn hơn 100 KB.";
                }
                else
                {
                    Directory.CreateDirectory(ImageFolder(lenh));
                    var temp = dest + ".part";
                    await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
                    File.Move(temp, dest, overwrite: false);
                    wrote = true;
                    result = "SUCCEEDED";
                }
            }
        }

        Append(costPath, new
        {
            luc = Now(),
            file = request.File,
            model = string.IsNullOrWhiteSpace(request.Model) ? BtaRunwayTextToImage.ModelId : request.Model.Trim(),
            ratio = string.IsNullOrWhiteSpace(request.Ratio) ? "1920:1080" : request.Ratio.Trim(),
            creditUocTinh = request.CreditUocTinh,
            taskId = request.TaskId,
            ketQua = result,
            loi = error,
        });
        return new BtaImageSaveResponse(request.File, result, error, wrote);
    }

    public async Task<BtaVoicePlanResponse> PlanVoiceAsync(BtaVoicePlanCommand request, CancellationToken cancellationToken)
    {
        RequireDev();
        if (request.Phan is < 1 or > 3)
            throw new InvalidOperationException("BTA_VOICE_PART: chỉ nhận phần 1, 2 hoặc 3.");
        var path = UnderBta(string.IsNullOrWhiteSpace(request.Lenh) ? BtaVoiceScript.DefaultLenh : request.Lenh);
        var script = await File.ReadAllTextAsync(path, cancellationToken);
        var part = BtaVoiceScript.Split(script).Single(row => row.Phan == request.Phan);
        var chars = BtaVoiceScript.EstimateCredit(part.Text);
        var head = part.Text.Length <= 120 ? part.Text : part.Text[..120];
        return new BtaVoicePlanResponse(request.Phan, chars, chars, CreditNote, head);
    }

    public BtaVoiceProbePreview ProbePreview()
    {
        RequireDev();
        var text = BtaVoiceScript.ProbeSentence;
        if (text.Length >= BtaVoiceScript.ProbeMaxChars)
            throw new InvalidOperationException("BTA_VOICE_PROBE: câu thử phải dưới 200 ký tự.");
        var chars = BtaVoiceScript.EstimateCredit(text);
        return new BtaVoiceProbePreview(text, chars, chars, CreditNote);
    }

    public Task<BtaVoiceTakeResponse> SynthesizePartAsync(BtaVoiceTakeCommand request, CancellationToken cancellationToken)
    {
        RequireDev();
        if (request.Phan is < 1 or > 3)
            throw new InvalidOperationException("BTA_VOICE_PART: chỉ nhận phần 1, 2 hoặc 3.");
        if (!string.Equals(request.XacNhan?.Trim(), "THU-PHAN-" + request.Phan, StringComparison.Ordinal))
            throw new InvalidOperationException("BTA_VOICE_CONFIRM: chưa xác nhận thu phần này.");
        return SynthesizeAsync(
            string.IsNullOrWhiteSpace(request.Lenh) ? BtaVoiceScript.DefaultLenh : request.Lenh,
            request.Phan.ToString(System.Globalization.CultureInfo.InvariantCulture),
            request.Phan,
            cancellationToken);
    }

    public Task<BtaVoiceTakeResponse> SynthesizeProbeAsync(BtaVoiceProbeCommand request, CancellationToken cancellationToken)
    {
        RequireDev();
        if (!request.XacNhan)
            throw new InvalidOperationException("BTA_VOICE_CONFIRM: chưa xác nhận câu thử.");
        return SynthesizeAsync(BtaVoiceScript.DefaultLenh, "cau-thu", phan: null, cancellationToken);
    }

    async Task<BtaVoiceTakeResponse> SynthesizeAsync(
        string scriptRelative,
        string label,
        int? phan,
        CancellationToken cancellationToken)
    {
        var scriptPath = UnderBta(scriptRelative);
        var episode = EpisodeCode(scriptPath);
        var folder = UnderBta(Path.Combine("docs", "marketing", "bta", "am-thanh", episode + "-giong-api"));
        var mp3Path = Path.Combine(folder, label + ".mp3");
        var timingPath = Path.Combine(folder, label + ".timing.json");
        if (File.Exists(mp3Path) || File.Exists(timingPath))
            throw new InvalidOperationException("BTA_VOICE_SKIP: file đã có, không thu lại.");

        string text;
        if (phan is int n)
        {
            var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            text = BtaVoiceScript.Split(script).Single(row => row.Phan == n).Text;
        }
        else
        {
            text = BtaVoiceScript.ProbeSentence;
            if (text.Length >= BtaVoiceScript.ProbeMaxChars)
                throw new InvalidOperationException("BTA_VOICE_PROBE: câu thử phải dưới 200 ký tự.");
        }

        var kenh = UnderBta(KenhRelative);
        var voiceId = ReadVoiceId(kenh);
        var key = await ElevenLabsKeyAsync(cancellationToken);
        var modelId = await ModelIdAsync(key, cancellationToken);
        var speech = BtaElevenLabsSpeech.Build(text, modelId, voiceId);
        var chars = BtaVoiceScript.EstimateCredit(speech.Text);
        try
        {
            var take = await _eleven.SynthesizeWithTimestampsAsync(speech, cancellationToken, key);
            WriteModel(kenh, modelId);
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(mp3Path, take.Mp3, cancellationToken);
            await File.WriteAllTextAsync(timingPath, take.TimingJson, cancellationToken);
            Append(Path.Combine(folder, "chi-phi.jsonl"), new
            {
                luc = Now(),
                phan = label,
                soKyTu = chars,
                model = modelId,
                voice = voiceId,
                requestId = take.RequestId,
                creditUocTinh = chars,
                ketQua = "SUCCEEDED",
            });
            return new BtaVoiceTakeResponse(
                Relative(mp3Path), Relative(timingPath), modelId, voiceId, take.RequestId, chars, chars, null);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("ElevenLabs TTS", StringComparison.Ordinal))
        {
            Append(Path.Combine(folder, "chi-phi.jsonl"), new
            {
                luc = Now(),
                phan = label,
                soKyTu = chars,
                model = modelId,
                voice = voiceId,
                creditUocTinh = chars,
                ketQua = "LOI",
                loi = ex.Message,
            });
            throw;
        }
    }

    /// <summary>Khóa Runway sản xuất của tenant (khóa Famixa). Không ghi khóa ra log hay file.</summary>
    async Task<string> RunwayKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var material = await _credentials.ResolveActiveAsync(
                FamixaProviderIds.Runway, BtaKitMktKeys.RunwayCredentialModel, cancellationToken);
            return material.Secret;
        }
        catch (InvalidOperationException ex) when (BtaKitMktKeys.IsMissingCredential(ex.Message))
        {
            var resolved = await _runway.ResolveAsync(cancellationToken);
            if (BtaKitMktKeys.LooksLikeRunwayKey(resolved.RunwayApiKey))
                return resolved.RunwayApiKey!.Trim();
            throw new InvalidOperationException(
                "BTA_RUNWAY_KEY: tenant đang đăng nhập chưa có khóa Runway sản xuất (khóa Famixa đang dùng), "
                + "và ô Model AI → Video không phải khóa key_. BTA chạy ở tenant FAMIXA_PRODUCTION: đăng nhập tenant đó.");
        }
    }

    /// <summary>Khóa ElevenLabs sản xuất của tenant nếu có; không có thì để client dùng cấu hình Model AI.</summary>
    async Task<string?> ElevenLabsKeyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var material = await _credentials.ResolveActiveAsync(
                FamixaProviderIds.ElevenLabs, BtaKitMktKeys.ElevenLabsCredentialModel, cancellationToken);
            return material.Secret;
        }
        catch (InvalidOperationException ex) when (BtaKitMktKeys.IsMissingCredential(ex.Message))
        {
            return null;
        }
    }

    /// <summary>Hỏi mã model theo tên. Khóa thiếu quyền models_read thì dùng mã Eleven v4 đã biết.</summary>
    async Task<string> ModelIdAsync(string? key, CancellationToken cancellationToken)
    {
        try
        {
            return await _eleven.FindModelIdByNameAsync(BtaElevenLabsSpeech.ModelDisplayName, cancellationToken, key);
        }
        catch (InvalidOperationException ex) when (BtaElevenLabsSpeech.ModelsListDenied(ex.Message))
        {
            return BtaElevenLabsSpeech.ModelIdFallback;
        }
    }

    BtaImagePlanItem PlannedItem(string file, string lenh)
    {
        var json = File.ReadAllText(UnderBta(lenh));
        return BtaImageCommandPlan.FindItem(json, file)
               ?? throw new InvalidOperationException("BTA_IMAGE_FILE: file không có trong lenh-tao-anh.json.");
    }

    void RequireDev()
    {
        if (!_env.IsDevelopment())
            throw new InvalidOperationException(
                "BTA_DEV: nút này chỉ chạy khi API ở môi trường Development, vì file ghi vào repo trên máy chủ kênh.");
    }

    /// <summary>File lệnh ảnh của tập. Chỉ nhận docs/marketing/bta/hinh-ai/&lt;số tập&gt;/lenh-tao-anh.json; bỏ trống = tập 01.</summary>
    static string LenhOf(string? lenh)
    {
        var value = string.IsNullOrWhiteSpace(lenh) ? BtaImageCommandPlan.DefaultLenh : lenh.Trim().Replace('\\', '/');
        if (!Regex.IsMatch(value, @"^docs/marketing/bta/hinh-ai/\d{2,3}/lenh-tao-anh\.json$"))
            throw new InvalidOperationException("BTA_IMAGE_LENH: chỉ nhận docs/marketing/bta/hinh-ai/<số tập>/lenh-tao-anh.json.");
        return value;
    }

    /// <summary>Ảnh, nhật ký và sổ chi phí nằm cùng thư mục với file lệnh của tập (hinh-ai/01, hinh-ai/02…).</summary>
    string ImageFolder(string lenh) => Path.GetDirectoryName(UnderBta(lenh))!;

    string LedgerPath(string lenh) => Path.Combine(ImageFolder(lenh), "nhat-ky.jsonl");

    string UnderBta(string relative)
    {
        var rel = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar);
        if (rel.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException("BTA_PATH: đường dẫn không hợp lệ.");
        var full = Path.GetFullPath(Path.Combine(RepoRoot(), rel));
        var allowed = Path.GetFullPath(Path.Combine(RepoRoot(), "docs", "marketing", "bta"));
        var prefix = allowed + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("BTA_PATH: chỉ trong docs/marketing/bta.");
        return full;
    }

    string Relative(string full)
    {
        var root = RepoRoot().TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            ? full[root.Length..].Replace(Path.DirectorySeparatorChar, '/')
            : full;
    }

    string RepoRoot()
    {
        foreach (var start in new[] { _env.ContentRootPath, AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(start)) continue;
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, BtaImageCommandPlan.DefaultLenh.Replace('/', Path.DirectorySeparatorChar))))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException("BTA_REPO: không thấy repo từ thư mục API.");
    }

    static string ReadVoiceId(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var id = doc.RootElement.GetProperty("voice").GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("BTA_VOICE_KENH: thiếu voice.id.");
        return id.Trim();
    }

    static void WriteModel(string path, string modelId)
    {
        var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                   ?? throw new InvalidOperationException("BTA_VOICE_KENH: kenh.json không đọc được.");
        var voice = node["voice"] as JsonObject
                    ?? throw new InvalidOperationException("BTA_VOICE_KENH: thiếu khối voice.");
        voice["model"] = modelId;
        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        File.WriteAllText(path, node.ToJsonString(opts) + Environment.NewLine);
    }

    static string EpisodeCode(string scriptPath)
    {
        var match = Regex.Match(Path.GetFileName(scriptPath), @"^(\d+)");
        return match.Success ? match.Groups[1].Value : "01";
    }

    static bool LooksLikeImage(byte[] bytes)
    {
        if (bytes.Length <= 100 * 1024) return false;
        var png = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
        var jpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        return png || jpeg;
    }

    static string Now() => DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");

    static async Task<string> ReadIfExists(string path, CancellationToken cancellationToken) =>
        File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken) : "";

    static void Append(string path, object row)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var line = JsonSerializer.Serialize(row, JsonOpts) + "\n";
        lock (Gate)
            File.AppendAllText(path, line, Encoding.UTF8);
    }
}
