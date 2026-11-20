using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Dịch vụ phía API của M1. Tenant luôn lấy từ <see cref="IProductionCallerTenant"/> (claim đã xác thực),
/// không lấy từ body, id hay đường dẫn.
/// </summary>
public sealed class FamixaLinesheetService : IFamixaLinesheetService
{
    public const long MaxZipEntryBytes = 80L * 1024 * 1024;
    public const long MaxZipTotalBytes = 1024L * 1024 * 1024;
    public const int MaxZipEntries = 2000;
    static readonly TimeSpan MediaTimeout = TimeSpan.FromMinutes(2);
    static readonly string[] AudioExts = [".wav", ".mp3", ".m4a", ".ogg", ".flac"];
    static readonly string[] ImageExts = [".png"];

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly IFamixaLinesheetStore _store;
    readonly IFamixaLinesheetFileStore _files;
    readonly ILinesheetMediaTools _media;
    readonly IProductionCallerTenant _caller;
    readonly ILinesheetTts? _tts;

    public FamixaLinesheetService(
        IFamixaLinesheetStore store,
        IFamixaLinesheetFileStore files,
        ILinesheetMediaTools media,
        IProductionCallerTenant caller,
        ILinesheetTts? tts = null,
        ILinesheetLayerComposer? composer = null,
        ILinesheetVideoGen? video = null,
        ILinesheetLipsync? lipsync = null,
        ILinesheetImageGen? image = null)
    {
        _store = store;
        _files = files;
        _media = media;
        _caller = caller;
        _tts = tts;
        _composer = composer;
        _video = video;
        _lipsync = lipsync;
        _image = image;
    }

    readonly ILinesheetLayerComposer? _composer;
    readonly ILinesheetVideoGen? _video;
    readonly ILinesheetLipsync? _lipsync;
    readonly ILinesheetImageGen? _image;

    Guid Tenant() => _caller.RequireTenant();

    async Task<LinesheetProductionRecord> RequireProduction(Guid tenant, Guid productionId, CancellationToken ct) =>
        await _store.GetProductionAsync(tenant, productionId, ct)
        ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);

    // ---------- sản xuất ----------

    public async Task<IReadOnlyList<LinesheetProductionDto>> ListProductionsAsync(CancellationToken ct) =>
        (await _store.ListProductionsAsync(Tenant(), ct)).Select(ToDto).ToList();

    public async Task<LinesheetProductionDto> CreateProductionAsync(LinesheetCreateProductionRequest request, CancellationToken ct)
    {
        var universe = Clean(request.UniverseCode, 64);
        var episode = Clean(request.EpisodeCode, 32);
        if (universe.Length == 0 || episode.Length == 0)
            throw new InvalidOperationException("LINESHEET_CODE_REQUIRED: cần universeCode và episodeCode.");
        var title = (request.Title ?? "").Trim();
        if (title.Length > 200) title = title[..200];
        return ToDto(await _store.CreateProductionAsync(Tenant(), universe, episode, title, ct));
    }

    static string Clean(string? code, int max)
    {
        var s = string.Concat((code ?? "").Trim().Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));
        return s.Length > max ? s[..max] : s;
    }

    static LinesheetProductionDto ToDto(LinesheetProductionRecord p) =>
        new(p.Id, p.UniverseCode, p.EpisodeCode, p.Title, p.UpdatedAt);

    // ---------- kịch bản và cấu hình ----------

    public async Task<int> ImportScriptAsync(Guid productionId, byte[] csvBytes, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        if (csvBytes is null || csvBytes.Length == 0)
            throw new InvalidOperationException(LinesheetErrorCodes.Empty);
        var rows = LinesheetCsv.ParseScript(Encoding.UTF8.GetString(csvBytes));
        var v = await _store.InsertVersionAsync(
            tenant, productionId, LinesheetRenderPlan.Sha256Hex(csvBytes),
            JsonSerializer.Serialize(rows, Json), rows.Count, ct);
        return v.VersionNo;
    }

    public async Task PutSettingsAsync(Guid productionId, string settingsJson, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var settings = ParseSettings(settingsJson);
        var latest = await _store.GetLatestVersionAsync(tenant, productionId, ct);
        if (latest is not null)
        {
            var errors = LinesheetShotPlanner.ValidateSettings(Rows(latest), settings);
            if (errors.Count > 0)
                throw new LinesheetValidationException(errors);
        }

        await _store.UpdateSettingsAsync(tenant, productionId, JsonSerializer.Serialize(settings, Json), ct);
    }

    public async Task<string> GetSettingsAsync(Guid productionId, CancellationToken ct) =>
        (await RequireProduction(Tenant(), productionId, ct)).SettingsJson;

    public static LinesheetSettings ParseSettings(string? json)
    {
        try
        {
            var s = JsonSerializer.Deserialize<LinesheetSettings>(json ?? "", new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
            if (s is null || s.Settings.Count == 0 || s.SceneToSetting.Count == 0)
                throw new InvalidOperationException(LinesheetErrorCodes.NoSettings);
            return s;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(LinesheetErrorCodes.NoSettings + ": " + ex.Message);
        }
    }

    internal static IReadOnlyList<LinesheetRow> Rows(LinesheetVersionRecord v) =>
        JsonSerializer.Deserialize<List<LinesheetRow>>(v.RowsJson, Json) ?? [];

    internal static IReadOnlyList<LinesheetShot> Shots(LinesheetShotSetRecord s) =>
        JsonSerializer.Deserialize<List<LinesheetShot>>(s.ShotsJson, Json) ?? [];

    internal static LinesheetTimeline Timeline(LinesheetTimelineRecord t) =>
        new(JsonSerializer.Deserialize<List<LinesheetTimelineEntry>>(t.EntriesJson, Json) ?? [],
            t.TotalMs,
            JsonSerializer.Deserialize<List<string>>(t.EstimatedJson, Json) ?? []);

    // ---------- tải file ----------

    /// <summary>
    /// Một file zip, đường dẫn trong zip quyết định loại:
    /// <c>audio/&lt;row_id&gt;.wav|mp3|m4a|ogg|flac</c>, <c>music/&lt;cue&gt;.*</c>, <c>sfx/&lt;cue&gt;.*</c>,
    /// <c>settings/&lt;bối cảnh&gt;/bg.png</c>, <c>settings/&lt;bối cảnh&gt;/mid.png</c>, <c>characters/&lt;ID&gt;.png</c>.
    /// Giọng được chuẩn hóa (48 kHz mono, cắt khoảng lặng thừa) và đo thời lượng ngay khi nhận.
    /// </summary>
    public async Task<LinesheetUploadResultDto> UploadFilesAsync(Guid productionId, Stream zip, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var work = _files.WorkDirectory(tenant, productionId, "upload-" + Guid.NewGuid().ToString("N"));
        var skipped = new List<string>();
        var byKind = new Dictionary<string, int>();
        var accepted = 0;
        long total = 0;
        try
        {
            using var archive = new ZipArchive(zip, ZipArchiveMode.Read, leaveOpen: true);
            if (archive.Entries.Count > MaxZipEntries)
                throw new InvalidOperationException(LinesheetErrorCodes.BadUpload + ": quá nhiều file trong zip.");
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.FullName.EndsWith('/')) continue;
                var target = Classify(entry.FullName);
                if (target is null)
                {
                    skipped.Add(entry.FullName);
                    continue;
                }

                if (entry.Length > MaxZipEntryBytes || (total += entry.Length) > MaxZipTotalBytes)
                    throw new InvalidOperationException(LinesheetErrorCodes.BadUpload + ": file quá lớn: " + entry.FullName);

                var (kind, key, ext) = target.Value;
                var raw = Path.Combine(work, Guid.NewGuid().ToString("N") + ext);
                await using (var src = entry.Open())
                await using (var dst = File.Create(raw))
                    await src.CopyToAsync(dst, ct);

                var record = kind == LinesheetFileKinds.LineAudio
                    ? await StoreVoiceAsync(tenant, productionId, key, raw, ct)
                    : await StoreRawAsync(tenant, productionId, kind, key, ext, raw, ct);
                await _store.UpsertFileAsync(tenant, record, ct);
                accepted++;
                byKind[kind] = byKind.GetValueOrDefault(kind) + 1;
            }
        }
        catch (InvalidDataException)
        {
            throw new InvalidOperationException(LinesheetErrorCodes.BadUpload + ": không đọc được file zip.");
        }
        finally
        {
            TryDeleteDirectory(work);
        }

        return new LinesheetUploadResultDto(accepted, skipped, byKind);
    }

    /// <summary>Phân loại theo đường dẫn trong zip. Tên được lọc chỉ còn chữ, số, '-', '_' để không thoát khỏi thư mục.</summary>
    public static (string Kind, string Key, string Ext)? Classify(string fullName)
    {
        var parts = fullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts.Any(p => p is "." or "..")) return null;
        // Cho phép thư mục bọc ngoài ("ep01_assets/audio/E01_002.wav"): loại xác định bằng thư mục cha gần nhất,
        // riêng layer bối cảnh là "settings/<bối cảnh>/bg.png".
        var top = parts.Length >= 3 && parts[^3].Equals("settings", StringComparison.OrdinalIgnoreCase)
            ? "settings"
            : parts[^2].ToLowerInvariant();

        var file = parts[^1];
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var stem = SafeName(Path.GetFileNameWithoutExtension(file));
        if (stem.Length == 0) return null;

        switch (top)
        {
            case "audio" when AudioExts.Contains(ext):
                return (LinesheetFileKinds.LineAudio, stem, ext);
            case "music" when AudioExts.Contains(ext):
                return (LinesheetFileKinds.CueMusic, stem, ext);
            case "sfx" when AudioExts.Contains(ext):
                return (LinesheetFileKinds.CueSfx, stem, ext);
            case "characters" when ImageExts.Contains(ext):
                return (LinesheetFileKinds.Character, stem, ext);
            case "keyframes" when ImageExts.Contains(ext):
                return (LinesheetFileKinds.Character, LinesheetFileKinds.KeyframePrefix + stem, ext);
            case "refs" when ImageExts.Contains(ext):
                return (LinesheetFileKinds.Character, LinesheetFileKinds.RefPrefix + stem, ext);
            case "settings" when ImageExts.Contains(ext) && parts.Length >= 3:
            {
                var setting = SafeName(parts[^2]);
                if (setting.Length == 0) return null;
                return stem.ToLowerInvariant() switch
                {
                    "bg" => (LinesheetFileKinds.LayerBackground, setting, ext),
                    "mid" => (LinesheetFileKinds.LayerMid, setting, ext),
                    _ => null,
                };
            }
            default:
                return null;
        }
    }

    public static string SafeName(string s) =>
        string.Concat((s ?? "").Where(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'));

    async Task<LinesheetFileRecord> StoreVoiceAsync(Guid tenant, Guid productionId, string rowId, string raw, CancellationToken ct)
    {
        var key = _files.Key(tenant, productionId, $"audio/{rowId}.wav");
        var path = _files.PathFor(tenant, key);
        var tmp = path + ".partial.wav";
        await _media.RunFfmpegAsync(LinesheetRenderPlan.NormalizeVoiceArgs(raw, tmp), Path.GetDirectoryName(tmp)!, MediaTimeout, ct);
        File.Move(tmp, path, overwrite: true);
        var ms = await ProbeMsAsync(path, ct);
        var bytes = await File.ReadAllBytesAsync(path, ct);
        return new LinesheetFileRecord
        {
            ProductionId = productionId,
            Kind = LinesheetFileKinds.LineAudio,
            FileKey = rowId,
            StorageKey = key,
            Sha256 = LinesheetRenderPlan.Sha256Hex(bytes),
            SizeBytes = bytes.LongLength,
            DurationMs = ms,
        };
    }

    async Task<LinesheetFileRecord> StoreRawAsync(Guid tenant, Guid productionId, string kind, string key, string ext, string raw, CancellationToken ct)
    {
        var rel = kind switch
        {
            LinesheetFileKinds.CueMusic => $"cues/music/{key}{ext}",
            LinesheetFileKinds.CueSfx => $"cues/sfx/{key}{ext}",
            LinesheetFileKinds.Character => $"layers/characters/{key}{ext}",
            LinesheetFileKinds.LayerBackground => $"layers/settings/{key}/bg{ext}",
            LinesheetFileKinds.LayerMid => $"layers/settings/{key}/mid{ext}",
            _ => throw new InvalidOperationException(LinesheetErrorCodes.BadUpload),
        };
        var storageKey = _files.Key(tenant, productionId, rel);
        var path = _files.PathFor(tenant, storageKey);
        File.Copy(raw, path, overwrite: true);
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (ext == ".png" && !(bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47))
            throw new InvalidOperationException(LinesheetErrorCodes.BadUpload + ": không phải PNG: " + rel);
        return new LinesheetFileRecord
        {
            ProductionId = productionId,
            Kind = kind,
            FileKey = key,
            StorageKey = storageKey,
            Sha256 = LinesheetRenderPlan.Sha256Hex(bytes),
            SizeBytes = bytes.LongLength,
            DurationMs = kind is LinesheetFileKinds.CueMusic or LinesheetFileKinds.CueSfx ? await ProbeMsAsync(path, ct) : null,
        };
    }

    async Task<int> ProbeMsAsync(string path, CancellationToken ct)
    {
        var text = await _media.RunFfprobeAsync(LinesheetRenderPlan.DurationProbeArgs(path), Path.GetDirectoryName(path)!, MediaTimeout, ct);
        if (!double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var sec))
            throw new InvalidOperationException(LinesheetErrorCodes.BadUpload + ": không đo được thời lượng " + Path.GetFileName(path));
        return (int)Math.Round(sec * 1000, MidpointRounding.ToEven);
    }

    static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch
        {
            /* thư mục tạm, để lần sau dọn */
        }
    }

    // ---------- trạng thái ----------

    public async Task<LinesheetStatusDto> GetStatusAsync(Guid productionId, CancellationToken ct)
    {
        var tenant = Tenant();
        var p = await RequireProduction(tenant, productionId, ct);
        var version = await _store.GetLatestVersionAsync(tenant, productionId, ct);
        var rows = version is null ? [] : Rows(version);
        var files = await _store.ListFilesAsync(tenant, productionId, ct);
        var counts = files.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Count());

        var settingsErrors = new List<string>();
        LinesheetSettings? settings = null;
        try
        {
            settings = ParseSettings(p.SettingsJson);
            if (rows.Count > 0)
                settingsErrors.AddRange(LinesheetShotPlanner.ValidateSettings(rows, settings).Select(e => e.Message));
        }
        catch (InvalidOperationException ex)
        {
            settingsErrors.Add(ex.Message);
        }

        var have = files.GroupBy(f => f.Kind).ToDictionary(g => g.Key, g => g.Select(f => f.FileKey).ToHashSet(StringComparer.Ordinal));
        HashSet<string> Have(string kind) => have.TryGetValue(kind, out var s) ? s : [];
        var missingAudio = rows.Where(r => r.IsLine && !Have(LinesheetFileKinds.LineAudio).Contains(r.Id)).Select(r => r.Id).ToList();
        var missingCues = rows.Where(r => (r.IsMusic && !Have(LinesheetFileKinds.CueMusic).Contains(r.Cue))
                                          || (r.IsSfx && !Have(LinesheetFileKinds.CueSfx).Contains(r.Cue)))
                              .Select(r => r.Cue).Where(c => c.Length > 0).Distinct().ToList();
        var missingLayers = new List<string>();
        if (settings is not null)
        {
            foreach (var key in settings.Settings.Keys)
            {
                if (!Have(LinesheetFileKinds.LayerBackground).Contains(key)) missingLayers.Add($"settings/{key}/bg.png");
                if (!Have(LinesheetFileKinds.LayerMid).Contains(key)) missingLayers.Add($"settings/{key}/mid.png");
            }

            foreach (var cid in settings.Characters.Keys.Where(c => !Have(LinesheetFileKinds.Character).Contains(c)))
                missingLayers.Add($"characters/{cid}.png");
        }

        var timeline = await _store.GetLatestTimelineAsync(tenant, productionId, ct);
        var timelineCurrent = timeline is not null && version is not null && timeline.VersionId == version.Id
                              && timeline.AudioSetSha256 == AudioSetSha(rows, files);
        var shots = await _store.GetLatestShotSetAsync(tenant, productionId, ct);
        var shotsCurrent = shots is not null && version is not null && shots.VersionId == version.Id;

        return new LinesheetStatusDto(
            ToDto(p),
            version?.VersionNo,
            rows.Count,
            rows.Count(r => r.IsLine),
            settings is not null && settingsErrors.Count == 0,
            settingsErrors,
            counts,
            missingAudio,
            missingCues,
            missingLayers,
            timelineCurrent ? timeline!.TotalMs : null,
            timelineCurrent ? (JsonSerializer.Deserialize<List<string>>(timeline!.EstimatedJson, Json)?.Count ?? 0) : 0,
            shotsCurrent ? shots!.ShotCount : null,
            shotsCurrent && shots!.Edited);
    }

    // ---------- dòng thời gian ----------

    public async Task<LinesheetStatusDto> BuildTimelineAsync(Guid productionId, bool allowMissing, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var version = await _store.GetLatestVersionAsync(tenant, productionId, ct)
                      ?? throw new InvalidOperationException(LinesheetErrorCodes.NoScript);
        var rows = Rows(version);
        var audio = (await _store.ListFilesAsync(tenant, productionId, ct))
            .Where(f => f.Kind == LinesheetFileKinds.LineAudio && f.DurationMs is > 0)
            .ToDictionary(f => f.FileKey, f => f);
        // bảng shot của đúng phiên bản kịch bản: giữ thêm cuối shot cho đủ thời gian đọc chữ (2026-11-05)
        var shotSet = await _store.GetLatestShotSetAsync(tenant, productionId, ct);
        var shots = shotSet is not null && shotSet.VersionId == version.Id ? Shots(shotSet) : null;
        var timeline = LinesheetTimelineBuilder.Build(rows, audio.ToDictionary(kv => kv.Key, kv => kv.Value.DurationMs!.Value), allowMissing,
            LinesheetTimelineBuilder.DefaultCharsPerSecond, shots);
        var audioSet = AudioSetSha(rows, audio.Values);
        await _store.InsertTimelineAsync(tenant, new LinesheetTimelineRecord
        {
            ProductionId = productionId,
            VersionId = version.Id,
            AudioSetSha256 = audioSet,
            EntriesJson = JsonSerializer.Serialize(timeline.Entries, Json),
            EstimatedJson = JsonSerializer.Serialize(timeline.EstimatedRowIds, Json),
            TotalMs = timeline.TotalMs,
        }, ct);
        return await GetStatusAsync(productionId, ct);
    }

    /// <summary>Dấu vân tay bộ giọng: tải giọng mới sau khi tính dòng thời gian thì dòng thời gian cũ hết hiệu lực.</summary>
    public static string AudioSetSha(IReadOnlyList<LinesheetRow> rows, IEnumerable<LinesheetFileRecord> files)
    {
        var audio = files.Where(f => f.Kind == LinesheetFileKinds.LineAudio && f.DurationMs is > 0)
            .ToDictionary(f => f.FileKey, f => f.Sha256, StringComparer.Ordinal);
        return LinesheetRenderPlan.Sha256Hex(string.Join("\n",
            rows.Where(r => r.IsLine).Select(r => r.Id + ":" + (audio.TryGetValue(r.Id, out var sha) ? sha : "-"))));
    }

    async Task<(LinesheetVersionRecord Version, IReadOnlyList<LinesheetRow> Rows, LinesheetTimelineRecord Record, LinesheetTimeline Timeline)>
        RequireCurrentTimeline(Guid tenant, Guid productionId, CancellationToken ct)
    {
        var version = await _store.GetLatestVersionAsync(tenant, productionId, ct)
                      ?? throw new InvalidOperationException(LinesheetErrorCodes.NoScript);
        var t = await _store.GetLatestTimelineAsync(tenant, productionId, ct);
        var rows = Rows(version);
        if (t is null || t.VersionId != version.Id
            || t.AudioSetSha256 != AudioSetSha(rows, await _store.ListFilesAsync(tenant, productionId, ct)))
            throw new InvalidOperationException(LinesheetErrorCodes.NoTimeline + ": bấm Tính dòng thời gian sau khi nhập kịch bản hoặc giọng.");
        return (version, rows, t, Timeline(t));
    }

    public async Task<string> GetTimelineCsvAsync(Guid productionId, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var (_, rows, _, timeline) = await RequireCurrentTimeline(tenant, productionId, ct);
        return LinesheetCsv.WriteTimeline(rows, timeline);
    }

    public async Task<string> GetSubtitlesAsync(Guid productionId, string lang, CancellationToken ct)
    {
        if (!LinesheetLanguages.IsKnown(lang)) throw new InvalidOperationException("LINESHEET_BAD_LANG");
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var (_, rows, _, timeline) = await RequireCurrentTimeline(tenant, productionId, ct);
        return LinesheetTimelineBuilder.Srt(rows, timeline, lang);
    }

    // ---------- bảng shot ----------

    public async Task<int> DraftShotsAsync(Guid productionId, bool force, CancellationToken ct)
    {
        var tenant = Tenant();
        var p = await RequireProduction(tenant, productionId, ct);
        var (version, rows, _, timeline) = await RequireCurrentTimeline(tenant, productionId, ct);
        var existing = await _store.GetLatestShotSetAsync(tenant, productionId, ct);
        if (!force && existing is { Edited: true } && existing.VersionId == version.Id)
            throw new InvalidOperationException("LINESHEET_SHOTS_EDITED: bảng shot đã được sửa tay; gửi force=true để ghi đè.");
        var shots = LinesheetShotPlanner.Draft(rows, timeline, ParseSettings(p.SettingsJson));
        await _store.InsertShotSetAsync(tenant, new LinesheetShotSetRecord
        {
            ProductionId = productionId,
            VersionId = version.Id,
            ShotsJson = JsonSerializer.Serialize(shots, Json),
            ShotCount = shots.Count,
            Edited = false,
        }, ct);
        return shots.Count;
    }

    public async Task<string> GetShotsCsvAsync(Guid productionId, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var s = await _store.GetLatestShotSetAsync(tenant, productionId, ct)
                ?? throw new InvalidOperationException(LinesheetErrorCodes.NoShots);
        return LinesheetCsv.WriteShots(Shots(s));
    }

    public async Task<int> PutShotsCsvAsync(Guid productionId, byte[] csvBytes, CancellationToken ct)
    {
        var tenant = Tenant();
        var p = await RequireProduction(tenant, productionId, ct);
        var version = await _store.GetLatestVersionAsync(tenant, productionId, ct)
                      ?? throw new InvalidOperationException(LinesheetErrorCodes.NoScript);
        var rows = Rows(version);
        var shots = LinesheetCsv.ParseShots(Encoding.UTF8.GetString(csvBytes ?? []));
        var errors = LinesheetShotPlanner.ValidateShots(rows, shots, ParseSettings(p.SettingsJson));
        if (errors.Count > 0)
            throw new LinesheetValidationException(errors);
        await _store.InsertShotSetAsync(tenant, new LinesheetShotSetRecord
        {
            ProductionId = productionId,
            VersionId = version.Id,
            ShotsJson = JsonSerializer.Serialize(shots, Json),
            ShotCount = shots.Count,
            Edited = true,
        }, ct);
        return shots.Count;
    }

    // ---------- dựng ----------

    public async Task<LinesheetRunDto> QueueRenderAsync(Guid productionId, LinesheetQueueRenderRequest request, CancellationToken ct)
    {
        var tenant = Tenant();
        var p = await RequireProduction(tenant, productionId, ct);
        var (version, rows, timelineRecord, timeline) = await RequireCurrentTimeline(tenant, productionId, ct);
        var shotSet = await _store.GetLatestShotSetAsync(tenant, productionId, ct);
        if (shotSet is null || shotSet.VersionId != version.Id)
            throw new InvalidOperationException(LinesheetErrorCodes.NoShots + ": tạo bảng shot cho kịch bản hiện tại trước.");
        var settings = ParseSettings(p.SettingsJson);
        var shots = Shots(shotSet);
        var shotErrors = LinesheetShotPlanner.ValidateShots(rows, shots, settings);
        if (shotErrors.Count > 0)
            throw new LinesheetValidationException(shotErrors);

        var format = LinesheetFormats.IsKnown(request.Format) ? request.Format! : LinesheetFormats.Landscape;
        var (dw, dh) = LinesheetFormats.DefaultSize(format);
        var width = Even(Math.Clamp(request.Width ?? dw, 240, 3840));
        var height = Even(Math.Clamp(request.Height ?? dh, 240, 3840));
        var fps = Math.Clamp(request.Fps ?? LinesheetRenderPlan.DefaultFps, 12, 60);
        var from = Math.Max(0, request.RangeStartMs ?? 0);
        var to = Math.Min(timeline.TotalMs, request.RangeEndMs ?? timeline.TotalMs);
        if (to - from < 1000)
            throw new InvalidOperationException(LinesheetErrorCodes.BadRange + ": khoảng dựng phải dài ít nhất 1 giây.");
        var lang = LinesheetLanguages.IsKnown(request.TextLang) ? request.TextLang! : LinesheetLanguages.Vi;
        var burn = request.BurnSubs is LinesheetLanguages.Vi or LinesheetLanguages.En ? request.BurnSubs : "none";

        var spans = LinesheetRenderPlan.Spans(rows, timeline, shots, fps, from, to);
        var run = await _store.InsertRunAsync(tenant, new LinesheetRenderRunRecord
        {
            ProductionId = productionId,
            VersionId = version.Id,
            TimelineId = timelineRecord.Id,
            ShotSetId = shotSet.Id,
            Kind = from == 0 && to == timeline.TotalMs ? "episode" : "clip",
            Format = format,
            Width = width,
            Height = height,
            Fps = fps,
            RangeStartMs = from,
            RangeEndMs = to,
            TextLang = lang,
            BurnSubs = burn,
            Status = LinesheetRunStatuses.Queued,
            NextStep = 0,
            StepCount = spans.Count + 2,
            FramesExpected = spans.Sum(s => s.Frames),
        }, ct);
        return ToRunDto(run);
    }

    static int Even(int v) => v % 2 == 0 ? v : v - 1;

    public async Task<IReadOnlyList<LinesheetRunDto>> ListRunsAsync(Guid productionId, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        return (await _store.ListRunsAsync(tenant, productionId, 30, ct)).Select(ToRunDto).ToList();
    }

    public async Task<LinesheetRunDto> GetRunAsync(Guid runId, CancellationToken ct) =>
        ToRunDto(await _store.GetRunAsync(Tenant(), runId, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound));

    public Task<bool> CancelRunAsync(Guid runId, CancellationToken ct) => _store.CancelRunAsync(Tenant(), runId, ct);

    public async Task<LinesheetDownload> DownloadRunFileAsync(Guid runId, string what, CancellationToken ct)
    {
        var tenant = Tenant();
        var run = await _store.GetRunAsync(tenant, runId, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var p = await RequireProduction(tenant, run.ProductionId, ct);
        var baseName = $"{p.UniverseCode}_{p.EpisodeCode}_{run.Format}_{run.Id.ToString("N")[..8]}";
        var (key, type, name) = what switch
        {
            "video" => (run.OutputStorageKey, "video/mp4", baseName + ".mp4"),
            "srt-vi" => (run.SrtViStorageKey, "application/x-subrip", baseName + ".vi.srt"),
            "srt-en" => (run.SrtEnStorageKey, "application/x-subrip", baseName + ".en.srt"),
            _ => (null, "", ""),
        };
        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var path = _files.PathFor(tenant, key);
        if (!File.Exists(path)) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        return new LinesheetDownload(path, type, name);
    }

    public async Task<LinesheetDownload> GetLineAudioAsync(Guid productionId, string rowId, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var safe = SafeName(rowId);
        var file = (await _store.ListFilesAsync(tenant, productionId, ct))
            .FirstOrDefault(f => f.Kind == LinesheetFileKinds.LineAudio && f.FileKey == safe)
            ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var path = _files.PathFor(tenant, file.StorageKey);
        if (!File.Exists(path)) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        return new LinesheetDownload(path, "audio/wav", safe + ".wav");
    }

    // ---------- chuyển động bằng video AI ----------

    const int MotionParallel = 2;
    static readonly TimeSpan MotionPollEvery = TimeSpan.FromSeconds(5);
    static readonly TimeSpan MotionMaxWait = TimeSpan.FromMinutes(12);
    const int MotionWidth = 1280, MotionHeight = 720;

    string MotionManifestPath(Guid tenant, Guid productionId) =>
        _files.PathFor(tenant, _files.Key(tenant, productionId, "motion/manifest.json"));

    /// <summary>Clip chuyển động của shot: clip riêng, không có thì clip của shot nguồn trong settings.motion_share.</summary>
    internal static bool TryMotion(Dictionary<string, LinesheetMotionEntry> manifest, LinesheetSettings settings, string shotId,
        [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out LinesheetMotionEntry entry)
    {
        // khai dùng chung thì lấy clip nguồn trước (bài học 2026-11-05: shot còn clip cũ từ bản 2D thì clip cũ lệch khóa, che mất clip nguồn)
        if (settings.MotionShare.TryGetValue(shotId, out var src) && manifest.TryGetValue(src, out entry)) return true;
        if (manifest.TryGetValue(shotId, out entry)) return true;
        entry = null;
        return false;
    }

    internal static Dictionary<string, LinesheetMotionEntry> ReadMotionManifest(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, LinesheetMotionEntry>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    sealed record MotionItem(LinesheetShotSpan Span, string Prompt, int Seconds, int Credits, string Key, bool Has, string Model);

    async Task<(List<MotionItem> Items, LinesheetSettings Settings, IReadOnlyList<LinesheetFileRecord> Files, List<string> Warnings, List<string> Blocking)> MotionPlanAsync(
        Guid tenant, Guid productionId, IReadOnlyDictionary<string, string>? prompts, CancellationToken ct)
    {
        if (_video is null) throw new InvalidOperationException("LINESHEET_MOTION_UNAVAILABLE: chưa bật video AI trên máy chủ.");
        var p = await RequireProduction(tenant, productionId, ct);
        var (version, rows, _, timeline) = await RequireCurrentTimeline(tenant, productionId, ct);
        var shotSet = await _store.GetLatestShotSetAsync(tenant, productionId, ct);
        if (shotSet is null || shotSet.VersionId != version.Id)
            throw new InvalidOperationException(LinesheetErrorCodes.NoShots + ": tạo bảng shot cho kịch bản hiện tại trước.");
        var settings = ParseSettings(p.SettingsJson);
        var model = settings.MotionModel.Trim();
        if (!_video.Models.Contains(model))
            throw new InvalidOperationException($"LINESHEET_MOTION_MODEL: motion_model '{model}' không có, chọn một trong: {string.Join(", ", _video.Models)}.");
        var files = await _store.ListFilesAsync(tenant, productionId, ct);
        var manifest = ReadMotionManifest(MotionManifestPath(tenant, productionId));
        var spans = LinesheetRenderPlan.Spans(rows, timeline, Shots(shotSet), LinesheetRenderPlan.DefaultFps, 0, timeline.TotalMs);
        var items = new List<MotionItem>();
        var shared = new List<LinesheetShotSpan>();
        foreach (var span in spans)
        {
            var id = span.Shot.ShotId;
            if (settings.MotionShare.ContainsKey(id)) { shared.Add(span); continue; } // dùng clip của shot khác
            var prompt = prompts is not null && prompts.TryGetValue(id, out var custom) && !string.IsNullOrWhiteSpace(custom)
                ? (custom.Contains("stays exactly where", StringComparison.Ordinal) ? custom.Trim() : custom.Trim() + " " + LinesheetMotionPlan.LockRules.Trim())
                : manifest.TryGetValue(id, out var old) && old.Prompt.Length > 0 ? old.Prompt : LinesheetMotionPlan.DefaultPrompt(span);
            var seconds = LinesheetMotionPlan.ClipSeconds(span.EndMs - span.StartMs, settings.MotionClipSeconds);
            var assets = LinesheetShotAssets.Resolve(span.Shot, files, settings);
            var key = LinesheetMotionPlan.MotionKey(span.Shot, assets.Shas, prompt, seconds, model);
            // "đã có" khớp đúng cách bản dựng chọn clip: clip còn khớp ảnh với câu lệnh, độ dài, mô hình lúc tạo
            // (sửa 2026-11-06: so với mô hình/độ dài hiện tại thì clip gen4.5 hay shot vừa kéo dài bị coi là thiếu → tạo lại phí credit)
            var has = manifest.TryGetValue(id, out var e) && File.Exists(_files.PathFor(tenant, e.StorageKey))
                && (e.Key == key || e.Key == LinesheetMotionPlan.MotionKey(span.Shot, assets.Shas, e.Prompt, e.Seconds, e.Model));
            items.Add(new MotionItem(span, prompt, seconds, seconds * _video.CreditsPerSecond(model), key, has, model));
        }

        var (warnings, blocking) = MotionShareCheck(shared, items, files, settings);
        return (items, settings, files, warnings, blocking);
    }

    /// <summary>
    /// Kiểm tra motion_share (thêm 2026-11-09, sổ lỗi A3). Clip dùng chung chỉ được dựng và ghép môi khi khóa của shot dùng chung
    /// trùng khóa clip nguồn (cùng bối cảnh, nhân vật, nét mặt, trọng tâm, ảnh, câu lệnh, độ dài, mô hình). Lệch khóa thì shot
    /// dựng ảnh tĩnh và KHÔNG ghép môi được. EP03 có 9 shot thoại như vậy, phải tạo clip và ghép môi lại.
    /// Shot có thoại bị lệch khóa thì chặn tạo chuyển động; shot không thoại chỉ cảnh báo.
    /// </summary>
    static (List<string> Warnings, List<string> Blocking) MotionShareCheck(
        List<LinesheetShotSpan> shared, List<MotionItem> items, IReadOnlyList<LinesheetFileRecord> files, LinesheetSettings settings)
    {
        var warnings = new List<string>();
        var blocking = new List<string>();
        var byId = items.ToDictionary(i => i.Span.Shot.ShotId, StringComparer.Ordinal);
        foreach (var span in shared)
        {
            var id = span.Shot.ShotId;
            var src = settings.MotionShare[id];
            var talks = span.Rows.Any(r => r.IsLine);
            string? problem = null;
            if (!byId.TryGetValue(src, out var source))
                problem = $"{id}: motion_share trỏ tới {src} nhưng {src} không có clip riêng trong bảng shot hiện tại.";
            else
            {
                var assets = LinesheetShotAssets.Resolve(span.Shot, files, settings);
                if (LinesheetMotionPlan.MotionKey(span.Shot, assets.Shas, source.Prompt, source.Seconds, source.Model) != source.Key)
                    problem = $"{id}: dùng chung clip {src} nhưng khác ảnh/nhân vật/nét mặt/trọng tâm nên sẽ dựng ảnh tĩnh"
                              + (talks ? " và KHÔNG ghép môi được" : "") + ".";
            }

            if (problem is null) continue;
            if (talks)
            {
                warnings.Add("CHẶN " + problem + " Bỏ dòng này khỏi motion_share hoặc trỏ tới shot thoại cùng ảnh.");
                blocking.Add(id);
            }
            else warnings.Add(problem);
        }

        return (warnings, blocking);
    }

    /// <summary>
    /// Chặn lô khi số dư không đủ (thêm 2026-11-09, sổ lỗi C3): cần ít nhất chi phí lô + 20%.
    /// Không đọc được số dư thì không chặn.
    /// </summary>
    static void RequireBalance(int? balance, int credits, string code)
    {
        if (balance is { } b && b < credits + (credits + 4) / 5)
            throw new InvalidOperationException($"{code}: số dư Runway còn {b} credit, lô này cần {credits} (+20% dự phòng = {credits + (credits + 4) / 5}). Nạp thêm hoặc chia lô nhỏ hơn.");
    }

    public async Task<LinesheetMotionPreviewDto> PreviewMotionAsync(Guid productionId, CancellationToken ct)
    {
        var (items, _, _, warnings, _) = await MotionPlanAsync(Tenant(), productionId, null, ct);
        return new LinesheetMotionPreviewDto(
            items.Count > 0 ? items[0].Model : _video!.Model, items.Count, items.Sum(i => i.Credits), items.Count(i => !i.Has), items.Where(i => !i.Has).Sum(i => i.Credits),
            items.Select(i => new LinesheetMotionShotDto(i.Span.Shot.ShotId, i.Span.Shot.Scene, i.Span.Shot.Setting,
                i.Span.EndMs - i.Span.StartMs, i.Seconds, i.Credits, i.Prompt, i.Has)).ToList(),
            warnings, await _video!.CreditBalanceAsync(ct));
    }

    /// <summary>
    /// Tạo chuyển động cho các shot được chọn (hoặc mọi shot chưa có). Mỗi shot: ghép ảnh tĩnh 1280×720 không chữ,
    /// gửi mô hình, chờ xong, tải MP4 về. Không chạy nếu ConfirmCredits khác số credit thật.
    /// </summary>
    public async Task<LinesheetMotionResultDto> GenerateMotionAsync(Guid productionId, LinesheetMotionRequest request, CancellationToken ct)
    {
        if (_composer is null) throw new InvalidOperationException("LINESHEET_MOTION_UNAVAILABLE: thiếu bộ ghép ảnh.");
        var tenant = Tenant();
        var (items, settings, _, _, blocking) = await MotionPlanAsync(tenant, productionId, request.Prompts, ct);
        if (blocking.Count > 0)
            throw new InvalidOperationException("LINESHEET_MOTION_SHARE: shot có thoại dùng chung clip lệch khóa, sẽ không ghép môi được: "
                                                + string.Join(", ", blocking) + ". Sửa motion_share rồi xem trước lại.");
        var only = request.ShotIds is { Count: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : null;
        var todo = items.Where(i => only is not null ? only.Contains(i.Span.Shot.ShotId) && (request.Force || !i.Has) : !i.Has).ToList();
        if (todo.Count == 0) return new LinesheetMotionResultDto(0, 0, []);
        var credits = todo.Sum(i => i.Credits);
        if (request.ConfirmCredits != credits)
            throw new InvalidOperationException($"LINESHEET_MOTION_CONFIRM: sẽ dùng {credits} credit, xác nhận đang là {request.ConfirmCredits}. Xem trước lại rồi xác nhận.");
        RequireBalance(await _video!.CreditBalanceAsync(ct), credits, "LINESHEET_MOTION_BALANCE");

        var manifestPath = MotionManifestPath(tenant, productionId);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var manifest = ReadMotionManifest(manifestPath);
        var gate = new SemaphoreSlim(MotionParallel);
        var sync = new object();
        var failed = new List<string>();
        var done = 0;
        var used = 0;
        var work = _files.WorkDirectory(tenant, productionId, "motion-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.WhenAll(todo.Select(async item =>
            {
                await gate.WaitAsync(ct);
                var shot = item.Span.Shot;
                try
                {
                    var assets = LinesheetShotAssets.Resolve(shot, (await _store.ListFilesAsync(tenant, productionId, ct)), settings);
                    var dir = Path.Combine(work, FamixaLinesheetService.SafeName(shot.ShotId));
                    Directory.CreateDirectory(dir);
                    var images = assets.Keyframe is { } kf
                        ? new LinesheetRenderPlan.ShotImages(_files.PathFor(tenant, kf.StorageKey), null, null)
                        : _composer.ComposeShot(new LinesheetComposeRequest(
                            shot, settings,
                            _files.PathFor(tenant, assets.Background!.StorageKey),
                            _files.PathFor(tenant, assets.Mid!.StorageKey),
                            assets.Characters.ToDictionary(kv => kv.Key, kv => _files.PathFor(tenant, kv.Value.StorageKey)),
                            MotionWidth, MotionHeight, false, dir));
                    var still = Path.Combine(dir, "still.jpg");
                    await _media.RunFfmpegAsync(LinesheetMotionPlan.StillArgs(images, MotionWidth, MotionHeight, still), dir, MediaTimeout, ct);
                    var task = await _video!.StartAsync(await File.ReadAllBytesAsync(still, ct), item.Prompt, item.Seconds, item.Model, ct);
                    lock (sync) used += item.Credits;
                    var waited = TimeSpan.Zero;
                    LinesheetVideoGenStatus st;
                    while (true)
                    {
                        await Task.Delay(MotionPollEvery, ct);
                        waited += MotionPollEvery;
                        st = await _video.PollAsync(task, ct);
                        if (st.Status is "SUCCEEDED" or "FAILED" or "CANCELLED") break;
                        if (waited > MotionMaxWait) throw new InvalidOperationException("LINESHEET_MOTION_TIMEOUT: quá 12 phút chưa xong.");
                    }

                    if (st.Status != "SUCCEEDED" || string.IsNullOrWhiteSpace(st.VideoUrl))
                        throw new InvalidOperationException($"LINESHEET_MOTION_FAILED ({st.Status}): {st.Error}");
                    var bytes = await _video.DownloadAsync(st.VideoUrl, ct);
                    var storageKey = _files.Key(tenant, productionId, $"motion/{FamixaLinesheetService.SafeName(shot.ShotId)}_{item.Key}.mp4");
                    var path = _files.PathFor(tenant, storageKey);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, bytes, ct);
                    lock (sync)
                    {
                        manifest[shot.ShotId] = new LinesheetMotionEntry
                        {
                            Key = item.Key, StorageKey = storageKey, Seconds = item.Seconds, Model = item.Model,
                            Prompt = item.Prompt, Sha256 = LinesheetRenderPlan.Sha256Hex(bytes),
                        };
                        done++;
                        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (sync) failed.Add($"{shot.ShotId}: {(ex.Message.Length > 240 ? ex.Message[..240] : ex.Message)}");
                }
                finally
                {
                    gate.Release();
                }
            }));
        }
        finally
        {
            TryDeleteDirectory(work);
        }

        return new LinesheetMotionResultDto(done, used, failed);
    }

    public async Task<LinesheetDownload> GetMotionClipAsync(Guid productionId, string shotId, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var manifest = ReadMotionManifest(MotionManifestPath(tenant, productionId));
        if (!manifest.TryGetValue(shotId, out var e)) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var path = _files.PathFor(tenant, e.StorageKey);
        if (!File.Exists(path)) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        return new LinesheetDownload(path, "video/mp4", FamixaLinesheetService.SafeName(shotId) + ".mp4");
    }

    /// <summary>Ảnh cảnh keyframes/&lt;tên&gt;.png đã tải lên hoặc đã tạo (thêm 2026-11-06 cho giao diện xem kết quả).</summary>
    public async Task<LinesheetDownload> GetKeyframeImageAsync(Guid productionId, string name, CancellationToken ct)
    {
        var tenant = Tenant();
        await RequireProduction(tenant, productionId, ct);
        var key = "KF_" + SafeName(name);
        var file = (await _store.ListFilesAsync(tenant, productionId, ct))
            .FirstOrDefault(f => f.Kind == LinesheetFileKinds.Character && f.FileKey == key)
            ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var path = _files.PathFor(tenant, file.StorageKey);
        if (!File.Exists(path)) throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var type = ext is ".jpg" or ".jpeg" ? "image/jpeg" : ext == ".webp" ? "image/webp" : "image/png";
        return new LinesheetDownload(path, type, SafeName(name) + (ext == "" ? ".png" : ext));
    }

    // ---------- ghép môi ----------

    const int LipParallel = 2;

    string LipManifestPath(Guid tenant, Guid productionId) =>
        _files.PathFor(tenant, _files.Key(tenant, productionId, "lipsync/manifest.json"));

    internal static Dictionary<string, LinesheetLipsyncEntry> ReadLipManifest(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, LinesheetLipsyncEntry>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    sealed record LipWork(LinesheetShotSpan Span, LinesheetMotionEntry Motion, string MotionPath, LinesheetLipItem Item, string Key, bool Has, double Seconds, decimal Usd);

    async Task<(List<LipWork> Items, List<string> Notes, string Model)> LipPlanAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        if (_lipsync is null) throw new InvalidOperationException("LINESHEET_LIPSYNC_UNAVAILABLE: chưa bật ghép môi trên máy chủ.");
        var p = await RequireProduction(tenant, productionId, ct);
        var (version, rows, _, timeline) = await RequireCurrentTimeline(tenant, productionId, ct);
        var shotSet = await _store.GetLatestShotSetAsync(tenant, productionId, ct);
        if (shotSet is null || shotSet.VersionId != version.Id)
            throw new InvalidOperationException(LinesheetErrorCodes.NoShots + ": tạo bảng shot cho kịch bản hiện tại trước.");
        var settings = ParseSettings(p.SettingsJson);
        var files = await _store.ListFilesAsync(tenant, productionId, ct);
        var voices = files.Where(f => f.Kind == LinesheetFileKinds.LineAudio).ToDictionary(f => f.FileKey);
        var motions = ReadMotionManifest(MotionManifestPath(tenant, productionId));
        var lips = ReadLipManifest(LipManifestPath(tenant, productionId));
        var model = settings.LipsyncModel.Trim();
        if (!_lipsync.Models.Contains(model))
            throw new InvalidOperationException($"LINESHEET_LIPSYNC_MODEL: lipsync_model '{model}' không có, chọn một trong: {string.Join(", ", _lipsync.Models)}.");
        var fps = LinesheetRenderPlan.DefaultFps;
        var spans = LinesheetRenderPlan.Spans(rows, timeline, Shots(shotSet), fps, 0, timeline.TotalMs);
        var items = new List<LipWork>();
        var notes = new List<string>();
        foreach (var span in spans)
        {
            var shot = span.Shot;
            if (LinesheetShotAssets.KeyframeName(shot, settings) is not { } kfName || !settings.Faces.ContainsKey(kfName)) continue;
            var assets = LinesheetShotAssets.Resolve(shot, files, settings);
            if (!TryMotion(motions, settings, shot.ShotId, out var m)
                || m.Key != LinesheetMotionPlan.MotionKey(shot, assets.Shas, m.Prompt, m.Seconds, m.Model)
                || !File.Exists(_files.PathFor(tenant, m.StorageKey)))
            {
                notes.Add($"{shot.ShotId}: chưa có chuyển động, chưa ghép môi được.");
                continue;
            }

            var size = LinesheetLipsyncPlan.ImageSize(_files.PathFor(tenant, assets.Keyframe!.StorageKey));
            foreach (var item in LinesheetLipsyncPlan.Items(span, timeline, settings, kfName, size, voices, m.Sha256, fps))
            {
                var key = LinesheetLipsyncPlan.LipKey(item, model);
                var has = lips.TryGetValue(item.Row.Id, out var e) && e.Key == key && File.Exists(_files.PathFor(tenant, e.StorageKey));
                var seconds = item.Frames / (double)fps;
                items.Add(new LipWork(span, m, _files.PathFor(tenant, m.StorageKey), item, key, has, seconds,
                    Math.Round((decimal)seconds * _lipsync.UsdPerSecond(model), 3)));
            }
        }

        return (items, notes, model);
    }

    public async Task<LinesheetLipsyncPreviewDto> PreviewLipsyncAsync(Guid productionId, CancellationToken ct)
    {
        var (items, notes, model) = await LipPlanAsync(Tenant(), productionId, ct);
        var pending = items.Where(i => !i.Has).ToList();
        return new LinesheetLipsyncPreviewDto(model, items.Count, Math.Round(items.Sum(i => i.Usd), 2), pending.Count,
            Math.Round(pending.Sum(i => i.Usd), 2),
            items.Select(i => new LinesheetLipsyncLineDto(i.Item.Row.Id, i.Span.Shot.ShotId, i.Item.Row.Speaker, Math.Round(i.Seconds, 2), i.Usd, i.Has)).ToList(),
            notes);
    }

    /// <summary>
    /// Ghép môi cho các câu được chọn (hoặc mọi câu chưa có). Không chạy nếu ConfirmUsd khác số tiền thật (làm tròn 2 chữ số).
    /// </summary>
    public async Task<LinesheetLipsyncResultDto> GenerateLipsyncAsync(Guid productionId, LinesheetLipsyncRequest request, CancellationToken ct)
    {
        var tenant = Tenant();
        var (items, _, model) = await LipPlanAsync(tenant, productionId, ct);
        var only = request.RowIds is { Count: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : null;
        var todo = items.Where(i => only is not null ? only.Contains(i.Item.Row.Id) && (request.Force || !i.Has) : !i.Has).ToList();
        if (todo.Count == 0) return new LinesheetLipsyncResultDto(0, 0, []);
        var usd = Math.Round(todo.Sum(i => i.Usd), 2);
        if (Math.Round(request.ConfirmUsd, 2) != usd)
            throw new InvalidOperationException($"LINESHEET_LIPSYNC_CONFIRM: sẽ tốn {usd} USD, xác nhận đang là {request.ConfirmUsd}. Xem trước lại rồi xác nhận.");

        var manifestPath = LipManifestPath(tenant, productionId);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var manifest = ReadLipManifest(manifestPath);
        var fps = LinesheetRenderPlan.DefaultFps;
        var gate = new SemaphoreSlim(LipParallel);
        var sync = new object();
        var failed = new List<string>();
        var done = 0;
        decimal used = 0;
        var work = _files.WorkDirectory(tenant, productionId, "lipsync-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Task.WhenAll(todo.Select(async w =>
            {
                await gate.WaitAsync(ct);
                var row = w.Item.Row;
                try
                {
                    var dir = Path.Combine(work, SafeName(row.Id));
                    Directory.CreateDirectory(dir);
                    var seg = Path.Combine(dir, "face.mp4");
                    var voice = Path.Combine(dir, "voice.mp3");
                    await _media.RunFfmpegAsync(LinesheetLipsyncPlan.SegmentArgs(w.Span, w.MotionPath, w.Motion.Seconds, w.Item, fps, seg), dir, MediaTimeout, ct);
                    await _media.RunFfmpegAsync(LinesheetLipsyncPlan.VoiceArgs(_files.PathFor(tenant, w.Item.Voice.StorageKey), w.Item, fps, voice), dir, MediaTimeout, ct);
                    var bytes = await _lipsync!.SyncAsync(await File.ReadAllBytesAsync(seg, ct), await File.ReadAllBytesAsync(voice, ct), model, ct);
                    lock (sync) used += w.Usd;
                    var storageKey = _files.Key(tenant, productionId, $"lipsync/{SafeName(row.Id)}_{w.Key}.mp4");
                    var path = _files.PathFor(tenant, storageKey);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllBytesAsync(path, bytes, ct);
                    lock (sync)
                    {
                        manifest[row.Id] = new LinesheetLipsyncEntry
                        {
                            Key = w.Key, StorageKey = storageKey, ShotId = w.Span.Shot.ShotId, Model = model, Fps = fps,
                            Seconds = Math.Round(w.Seconds, 3), Sha256 = LinesheetRenderPlan.Sha256Hex(bytes),
                        };
                        done++;
                        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (sync) failed.Add($"{row.Id}: {(ex.Message.Length > 240 ? ex.Message[..240] : ex.Message)}");
                }
                finally
                {
                    gate.Release();
                }
            }));
        }
        finally
        {
            TryDeleteDirectory(work);
        }

        return new LinesheetLipsyncResultDto(done, Math.Round(used, 2), failed);
    }

    // ---------- tạo ảnh cảnh bằng API ----------

    sealed record KeyframeWork(string Name, LinesheetKeyframeSpec Spec, string Prompt, IReadOnlyList<(string Tag, LinesheetFileRecord File)> Refs, int Credits, bool Has, string Key);

    string KeyframeGenManifestPath(Guid tenant, Guid productionId) =>
        _files.PathFor(tenant, _files.Key(tenant, productionId, "keyframes/generated.json"));

    /// <summary>
    /// Câu lệnh đầy đủ: câu lệnh của ảnh cảnh, rồi đoạn mô tả cố định của từng nhân vật (gọi bằng @MÃ), rồi phong cách chung.
    /// </summary>
    public static string KeyframePrompt(LinesheetKeyframeSpec spec, LinesheetSettings settings, int extraRefsPerCharacter = 0)
    {
        var sb = new StringBuilder(spec.Prompt.Trim());
        foreach (var c in spec.Characters)
            if (settings.CharacterNotes.TryGetValue(c, out var note) && !string.IsNullOrWhiteSpace(note))
                sb.Append(' ').Append('@').Append(c).Append(": ").Append(note.Trim().TrimEnd('.')).Append('.');
        if (extraRefsPerCharacter > 0)
            foreach (var c in spec.Characters)
                sb.Append(' ').Append(string.Join(" and ", Enumerable.Range(2, extraRefsPerCharacter).Select(i => "@" + c + i)))
                  .Append(" show the same person as @").Append(c).Append(" from other angles.");
        if (!string.IsNullOrWhiteSpace(settings.ImageStyle)) sb.Append(' ').Append(settings.ImageStyle.Trim());
        // tag của API phải dài 3–16 ký tự, bắt đầu bằng chữ: "@ME" thành "@ME_ref" (bài học 2026-11-05: Runway từ chối tag 2 ký tự)
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"@([A-Za-z][A-Za-z0-9_]*)",
            m => "@" + ImageTag(m.Groups[1].Value, spec.Characters));
    }

    /// <summary>Tag API cho mã nhân vật (thêm số thứ tự ảnh nếu có): ME → ME_ref, ME2 → ME_ref2, QUAN giữ nguyên.</summary>
    public static string ImageTag(string word, IReadOnlyList<string> characters)
    {
        foreach (var c in characters.OrderByDescending(c => c.Length))
        {
            if (!word.StartsWith(c, StringComparison.Ordinal)) continue;
            var rest = word[c.Length..];
            if (rest.Length > 0 && !rest.All(char.IsDigit)) continue;
            var baseTag = c.Length >= 3 ? c : c + "_ref";
            return (baseTag + rest).Length <= 16 ? baseTag + rest : (baseTag + rest)[..16];
        }

        return word;
    }

    async Task<List<KeyframeWork>> KeyframePlanAsync(Guid tenant, Guid productionId, CancellationToken ct)
    {
        if (_image is null) throw new InvalidOperationException("LINESHEET_IMAGE_UNAVAILABLE: chưa bật tạo ảnh trên máy chủ.");
        var p = await RequireProduction(tenant, productionId, ct);
        var settings = ParseSettings(p.SettingsJson);
        var files = await _store.ListFilesAsync(tenant, productionId, ct);
        var chars = files.Where(f => f.Kind == LinesheetFileKinds.Character).ToDictionary(f => f.FileKey);
        var done = ReadKeyframeGenManifest(KeyframeGenManifestPath(tenant, productionId));
        var list = new List<KeyframeWork>();
        foreach (var (name, spec) in settings.KeyframeSpecs.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (!_image.Models.Contains(spec.Model))
                throw new InvalidOperationException($"LINESHEET_IMAGE_MODEL: '{spec.Model}' không có cho ảnh {name}.");
            var maxRefs = _image.MaxRefs(spec.Model);
            if (spec.Characters.Count > maxRefs)
                throw new InvalidOperationException($"LINESHEET_IMAGE_REFS: ảnh {name} có quá {maxRefs} nhân vật tham chiếu cho {spec.Model}.");
            // mỗi nhân vật được bao nhiêu ảnh mẫu: gen4_image chỉ 1, gpt_image_2 tối đa 3 (ngồi, mặt, nhiều góc)
            var perChar = Math.Clamp(maxRefs / Math.Max(1, spec.Characters.Count), 1, 3);
            var refs = new List<(string, LinesheetFileRecord)>();
            var extra = int.MaxValue;
            foreach (var c in spec.Characters)
            {
                var names = settings.CharacterRefs.TryGetValue(c, out var r) ? r : [];
                var found = names.Take(perChar).Select(n => chars.GetValueOrDefault(LinesheetFileKinds.RefPrefix + SafeName(n))).ToList();
                if (found.Count == 0 || found.Any(f => f is null))
                    throw new InvalidOperationException($"LINESHEET_IMAGE_REFS: thiếu ảnh mẫu của {c} (character_refs và refs/<tên>.png).");
                for (var i = 0; i < found.Count; i++)
                    refs.Add((ImageTag(i == 0 ? c : c + (i + 1), spec.Characters), found[i]!));
                extra = Math.Min(extra, found.Count - 1);
            }

            var prompt = KeyframePrompt(spec, settings, extra == int.MaxValue ? 0 : extra);
            var maxPrompt = spec.Model == "gpt_image_2" ? 32000 : 1000;
            if (prompt.Length > maxPrompt)
                throw new InvalidOperationException($"LINESHEET_IMAGE_PROMPT: câu lệnh ảnh {name} dài {prompt.Length} ký tự, {spec.Model} nhận tối đa {maxPrompt}. Rút gọn prompt, character_notes hoặc image_style.");
            var sig = LinesheetRenderPlan.Sha256Hex("kfgen1|" + spec.Model + "|" + spec.Ratio + "|" + spec.Quality + "|" + spec.Seed + "|" + prompt + "|"
                                                    + string.Join(";", refs.Select(x => x.Item1 + ":" + x.Item2.Sha256)))[..16];
            var has = done.TryGetValue(name, out var d) && d == sig
                      && chars.ContainsKey(LinesheetFileKinds.KeyframePrefix + SafeName(name));
            list.Add(new KeyframeWork(name, spec, prompt, refs, _image.CreditsPerImage(spec.Model, spec.Ratio, spec.Quality), has, sig));
        }

        return list;
    }

    static Dictionary<string, string> ReadKeyframeGenManifest(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    public async Task<LinesheetKeyframeGenPreviewDto> PreviewKeyframeGenAsync(Guid productionId, CancellationToken ct)
    {
        var items = await KeyframePlanAsync(Tenant(), productionId, ct);
        return new LinesheetKeyframeGenPreviewDto(items.Count, items.Sum(i => i.Credits), items.Count(i => !i.Has), items.Where(i => !i.Has).Sum(i => i.Credits),
            items.Select(i => new LinesheetKeyframeGenDto(i.Name, i.Spec.Model, i.Spec.Ratio, i.Credits, i.Refs.Select(r => r.Tag).ToList(), i.Prompt, i.Has)).ToList());
    }

    /// <summary>
    /// Tạo ảnh cảnh bằng API, gửi kèm ảnh mẫu nhân vật, rồi lưu như một ảnh tải lên ở keyframes/&lt;tên&gt;.png
    /// (ghi đè ảnh cũ cùng tên). Không chạy nếu ConfirmCredits khác số credit thật.
    /// </summary>
    public async Task<LinesheetKeyframeGenResultDto> GenerateKeyframesAsync(Guid productionId, LinesheetKeyframeGenRequest request, CancellationToken ct)
    {
        var tenant = Tenant();
        var items = await KeyframePlanAsync(tenant, productionId, ct);
        var only = request.Names is { Count: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : null;
        var todo = items.Where(i => only is not null ? only.Contains(i.Name) && (request.Force || !i.Has) : !i.Has).ToList();
        if (todo.Count == 0) return new LinesheetKeyframeGenResultDto(0, 0, []);
        var credits = todo.Sum(i => i.Credits);
        if (request.ConfirmCredits != credits)
            throw new InvalidOperationException($"LINESHEET_IMAGE_CONFIRM: sẽ dùng {credits} credit, xác nhận đang là {request.ConfirmCredits}. Xem trước lại rồi xác nhận.");
        RequireBalance(await _image!.CreditBalanceAsync(ct), credits, "LINESHEET_IMAGE_BALANCE");
        var manifestPath = KeyframeGenManifestPath(tenant, productionId);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        var manifest = ReadKeyframeGenManifest(manifestPath);
        var failed = new List<string>();
        var done = 0;
        var used = 0;
        var work = _files.WorkDirectory(tenant, productionId, "kfgen-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var w in todo)
            {
                try
                {
                    var refs = new List<LinesheetImageRef>();
                    foreach (var (tag, file) in w.Refs)
                    {
                        // ảnh mẫu thu về JPEG cạnh dài 1280 cho nhẹ (API nhận ảnh dạng data URI)
                        var jpg = Path.Combine(work, $"ref_{SafeName(tag)}.jpg");
                        await _media.RunFfmpegAsync(["-v", "error", "-y", "-i", _files.PathFor(tenant, file.StorageKey),
                            "-vf", "scale='min(1280,iw)':-2", "-q:v", "3", jpg], work, MediaTimeout, ct);
                        refs.Add(new LinesheetImageRef(tag, await File.ReadAllBytesAsync(jpg, ct)));
                    }

                    var bytes = await _image!.GenerateAsync(w.Prompt, refs, w.Spec.Ratio, w.Spec.Model, w.Spec.Quality, w.Spec.Seed, ct);
                    used += w.Credits;
                    var zipPath = Path.Combine(work, SafeName(w.Name) + ".zip");
                    using (var zip = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        var entry = zip.CreateEntry("keyframes/" + SafeName(w.Name) + ".png");
                        await using var es = entry.Open();
                        await es.WriteAsync(bytes, ct);
                    }

                    await using (var zs = File.OpenRead(zipPath))
                        await UploadFilesAsync(productionId, zs, ct);
                    manifest[w.Name] = w.Key;
                    await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest), ct);
                    done++;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed.Add($"{w.Name}: {(ex.Message.Length > 240 ? ex.Message[..240] : ex.Message)}");
                }
            }
        }
        finally
        {
            TryDeleteDirectory(work);
        }

        return new LinesheetKeyframeGenResultDto(done, used, failed);
    }

    // ---------- giọng ElevenLabs ----------

    const int TtsParallel = 3;

    string TtsManifestPath(Guid tenant, Guid productionId) =>
        _files.PathFor(tenant, _files.Key(tenant, productionId, "tts/manifest.json"));

    Dictionary<string, string> ReadTtsManifest(Guid tenant, Guid productionId)
    {
        var path = TtsManifestPath(tenant, productionId);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    string TtsTakesPath(Guid tenant, Guid productionId) =>
        _files.PathFor(tenant, _files.Key(tenant, productionId, "tts/takes.json"));

    /// <summary>Lần tạo hiện tại của từng câu (không có trong file nghĩa là lần 1).</summary>
    Dictionary<string, int> ReadTtsTakes(Guid tenant, Guid productionId)
    {
        var path = TtsTakesPath(tenant, productionId);
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, int>>(File.ReadAllText(path)) ?? new()
                : new();
        }
        catch (JsonException)
        {
            return new();
        }
    }

    async Task<(IReadOnlyList<LinesheetTtsItem> Items, IReadOnlyList<string> Missing, HashSet<string> Pending)> TtsPlanAsync(
        Guid tenant, Guid productionId, IReadOnlyCollection<string>? onlyRowIds, CancellationToken ct,
        IReadOnlyDictionary<string, int>? takes = null)
    {
        var p = await RequireProduction(tenant, productionId, ct);
        var version = await _store.GetLatestVersionAsync(tenant, productionId, ct)
                      ?? throw new InvalidOperationException(LinesheetErrorCodes.NoScript);
        var settings = ParseSettings(p.SettingsJson);
        var items = LinesheetTtsPlan.Plan(Rows(version), settings, out var missing, onlyRowIds, takes ?? ReadTtsTakes(tenant, productionId));
        var manifest = ReadTtsManifest(tenant, productionId);
        var audio = (await _store.ListFilesAsync(tenant, productionId, ct))
            .Where(f => f.Kind == LinesheetFileKinds.LineAudio).Select(f => f.FileKey).ToHashSet(StringComparer.Ordinal);
        var pending = items.Where(i => !audio.Contains(i.RowId) || !manifest.TryGetValue(i.RowId, out var fp) || fp != i.Fingerprint)
            .Select(i => i.RowId).ToHashSet(StringComparer.Ordinal);
        return (items, missing, pending);
    }

    public async Task<LinesheetTtsPreviewDto> PreviewTtsAsync(Guid productionId, CancellationToken ct)
    {
        var (items, missing, pending) = await TtsPlanAsync(Tenant(), productionId, null, ct);
        return new LinesheetTtsPreviewDto(
            LinesheetTtsPlan.Model,
            items.Count,
            items.Sum(i => i.Chars),
            pending.Count,
            items.Where(i => pending.Contains(i.RowId)).Sum(i => i.Chars),
            missing,
            items.Select(i => new LinesheetTtsLineDto(i.RowId, i.Speaker, i.Tags, i.Text, i.Chars, pending.Contains(i.RowId), i.Take)).ToList());
    }

    /// <summary>
    /// Gọi ElevenLabs cho các câu được chọn (hoặc các câu chưa có giọng đúng), chuẩn hóa và lưu như giọng tải lên.
    /// Không chạy nếu ConfirmChars khác số ký tự thật sẽ gửi.
    /// </summary>
    public async Task<LinesheetTtsResultDto> GenerateTtsAsync(Guid productionId, LinesheetTtsRequest request, CancellationToken ct)
    {
        if (_tts is null) throw new InvalidOperationException("LINESHEET_TTS_UNAVAILABLE: chưa bật ElevenLabs trên máy chủ.");
        var tenant = Tenant();
        var only = request.RowIds is { Count: > 0 } ids ? ids.ToHashSet(StringComparer.Ordinal) : null;
        if (request.NewTake && only is null)
            throw new InvalidOperationException("LINESHEET_TTS_TAKE: chọn câu cần tạo bản đọc mới.");
        var takes = ReadTtsTakes(tenant, productionId);
        var nextTakes = new Dictionary<string, int>(takes, StringComparer.Ordinal);
        if (request.NewTake)
            foreach (var id in only!)
                nextTakes[id] = (takes.TryGetValue(id, out var t) && t > 1 ? t : 1) + 1;
        var (items, missing, pending) = await TtsPlanAsync(tenant, productionId, only, ct, nextTakes);
        var todo = items.Where(i => only is not null || !request.OnlyPending || pending.Contains(i.RowId)).ToList();
        if (todo.Count == 0) return new LinesheetTtsResultDto(0, 0, []);
        var chars = todo.Sum(i => i.Chars);
        if (request.ConfirmChars != chars)
            throw new InvalidOperationException($"LINESHEET_TTS_CONFIRM: sẽ gửi {chars} ký tự, xác nhận đang là {request.ConfirmChars}. Xem trước lại rồi xác nhận.");

        var work = _files.WorkDirectory(tenant, productionId, "tts-" + Guid.NewGuid().ToString("N"));
        var manifest = ReadTtsManifest(tenant, productionId);
        var failed = new List<string>();
        var done = 0;
        var gate = new SemaphoreSlim(TtsParallel);
        var manifestLock = new object();
        var p = await RequireProduction(tenant, productionId, ct);
        var settings = ParseSettings(p.SettingsJson);
        try
        {
            await Task.WhenAll(todo.Select(async item =>
            {
                await gate.WaitAsync(ct);
                try
                {
                    var voice = LinesheetTtsPlan.VoiceFor(item.Speaker, settings)!;
                    var bytes = await _tts.SynthesizeAsync(item.VoiceId, item.Text, voice, item.Take, ct);
                    var raw = Path.Combine(work, item.RowId + ".mp3");
                    await File.WriteAllBytesAsync(raw, bytes, ct);
                    var record = await StoreVoiceAsync(tenant, productionId, item.RowId, raw, ct);
                    await _store.UpsertFileAsync(tenant, record, ct);
                    lock (manifestLock)
                    {
                        manifest[item.RowId] = item.Fingerprint;
                        if (item.Take > 1) takes[item.RowId] = item.Take;
                        else takes.Remove(item.RowId);
                        done++;
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lock (manifestLock) failed.Add($"{item.RowId}: {(ex.Message.Length > 200 ? ex.Message[..200] : ex.Message)}");
                }
                finally
                {
                    gate.Release();
                }
            }));
        }
        finally
        {
            await File.WriteAllTextAsync(TtsManifestPath(tenant, productionId), JsonSerializer.Serialize(manifest), CancellationToken.None);
            await File.WriteAllTextAsync(TtsTakesPath(tenant, productionId), JsonSerializer.Serialize(takes), CancellationToken.None);
            TryDeleteDirectory(work);
        }

        return new LinesheetTtsResultDto(done, todo.Where(i => !failed.Any(f => f.StartsWith(i.RowId + ":", StringComparison.Ordinal))).Sum(i => i.Chars), failed);
    }

    internal static LinesheetRunDto ToRunDto(LinesheetRenderRunRecord r) =>
        new(r.Id, r.Kind, r.Format, r.Width, r.Height, r.Fps, r.RangeStartMs, r.RangeEndMs, r.TextLang, r.BurnSubs,
            r.Status, r.NextStep, r.StepCount, r.FramesExpected, r.FramesActual,
            !string.IsNullOrEmpty(r.OutputStorageKey), r.Error, r.CreatedAt, r.StartedAt, r.CompletedAt,
            JsonSerializer.Deserialize<List<LinesheetShotLogEntry>>(r.ShotLogJson, Json) ?? []);
}
