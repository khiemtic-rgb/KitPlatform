using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Bước dựng của worker M1. Mỗi lần gọi làm đúng MỘT bước của MỘT lần dựng rồi trả quyền:
/// bước 0 = ghép tiếng cả tập; bước 1..N = dựng shot thứ N (có cache); bước N+1 = nối, gắn tiếng và phụ đề, kiểm số khung.
/// Tenant lấy từ chính bản ghi lần dựng (worker không có HTTP), và mọi đọc/ghi sau đó đều đi kèm tenant đó.
/// </summary>
public sealed class FamixaLinesheetRenderer : IFamixaLinesheetRenderer
{
    public static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(10);
    public const int MaxAttempts = 3;
    static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(8);
    static readonly TimeSpan ProbeTimeout = TimeSpan.FromMinutes(2);

    readonly IFamixaLinesheetStore _store;
    readonly IFamixaLinesheetFileStore _files;
    readonly ILinesheetMediaTools _media;
    readonly ILinesheetLayerComposer _composer;
    readonly TimeProvider _clock;

    public FamixaLinesheetRenderer(
        IFamixaLinesheetStore store,
        IFamixaLinesheetFileStore files,
        ILinesheetMediaTools media,
        ILinesheetLayerComposer composer,
        TimeProvider? clock = null)
    {
        _store = store;
        _files = files;
        _media = media;
        _composer = composer;
        _clock = clock ?? TimeProvider.System;
    }

    sealed record Context(
        LinesheetRenderRunRecord Run,
        LinesheetProductionRecord Production,
        IReadOnlyList<LinesheetRow> Rows,
        LinesheetTimelineRecord TimelineRecord,
        LinesheetTimeline Timeline,
        LinesheetSettings Settings,
        IReadOnlyList<LinesheetShotSpan> Spans,
        IReadOnlyList<LinesheetFileRecord> Files);

    public async Task<string?> RunNextStepAsync(CancellationToken ct)
    {
        var run = await _store.ClaimNextRunAsync(StaleAfter, ct);
        if (run is null) return null;
        var step = run.NextStep;
        try
        {
            var c = await LoadAsync(run, ct);
            string done;
            if (step == 0)
                done = await AudioStepAsync(c, ct);
            else if (step <= c.Spans.Count)
                done = await ShotStepAsync(c, c.Spans[step - 1], ct);
            else
                done = await FinalStepAsync(c, ct);

            run.NextStep = step + 1;
            run.Attempts = 0;
            run.Error = null;
            run.HeartbeatAt = null; // trả "khóa" để vòng sau nhận bước kế tiếp
            if (run.NextStep >= run.StepCount)
            {
                run.Status = LinesheetRunStatuses.Succeeded;
                run.CompletedAt = _clock.GetUtcNow();
            }

            await _store.SaveRunProgressAsync(run, ct);
            return $"{run.Id:N} bước {step}/{run.StepCount - 1}: {done}";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // máy dừng: để heartbeat quá hạn, lần sau làm lại bước này
        }
        catch (Exception ex)
        {
            run.Attempts++;
            run.Error = Short(ex);
            run.HeartbeatAt = null;
            var permanent = ex is LinesheetValidationException;
            if (permanent || run.Attempts >= MaxAttempts)
            {
                run.Status = LinesheetRunStatuses.Failed;
                run.CompletedAt = _clock.GetUtcNow();
            }
            else
            {
                run.AvailableAt = _clock.GetUtcNow().AddSeconds(30 * run.Attempts);
            }

            await _store.SaveRunProgressAsync(run, CancellationToken.None);
            return $"{run.Id:N} bước {step}: lỗi lần {run.Attempts}: {run.Error}";
        }
    }

    static string Short(Exception ex)
    {
        var msg = ex is LinesheetValidationException v
            ? string.Join("; ", v.Errors.Take(5).Select(e => $"{e.RowId} {e.Message}"))
            : ex.Message;
        return msg.Length > 900 ? msg[..900] : msg;
    }

    async Task<Context> LoadAsync(LinesheetRenderRunRecord run, CancellationToken ct)
    {
        var t = run.TenantId;
        var p = await _store.GetProductionAsync(t, run.ProductionId, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NotFound);
        var v = await _store.GetVersionAsync(t, run.VersionId, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NoScript);
        var tl = await _store.GetTimelineAsync(t, run.TimelineId, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NoTimeline);
        var ss = await _store.GetShotSetAsync(t, run.ShotSetId, ct) ?? throw new InvalidOperationException(LinesheetErrorCodes.NoShots);
        var rows = FamixaLinesheetService.Rows(v);
        var timeline = FamixaLinesheetService.Timeline(tl);
        var spans = LinesheetRenderPlan.Spans(rows, timeline, FamixaLinesheetService.Shots(ss), run.Fps, run.RangeStartMs, run.RangeEndMs);
        if (spans.Count + 2 != run.StepCount)
            throw new InvalidOperationException("LINESHEET_RUN_CHANGED: số shot khác lúc xếp hàng.");
        return new Context(run, p, rows, tl, timeline, FamixaLinesheetService.ParseSettings(p.SettingsJson), spans,
            await _store.ListFilesAsync(t, run.ProductionId, ct));
    }

    // ---------- bước 0: tiếng ----------

    async Task<string> AudioStepAsync(Context c, CancellationToken ct)
    {
        var tenant = c.Run.TenantId;
        var mixTag = LinesheetRenderPlan.MixTag(c.Settings) + "_" + LinesheetRenderPlan.CueSetTag(c.Files);
        var key = _files.Key(tenant, c.Production.Id, $"timelines/{c.TimelineRecord.Id:N}/episode_{mixTag}.wav");
        if (c.TimelineRecord.AudioStorageKey == key && File.Exists(_files.PathFor(tenant, key)))
            return "tiếng đã có sẵn";

        var work = _files.WorkDirectory(tenant, c.Production.Id, "audio-" + c.TimelineRecord.Id.ToString("N"));
        var byKind = c.Files.ToLookup(f => f.Kind);
        var voices = byKind[LinesheetFileKinds.LineAudio].ToDictionary(f => f.FileKey);
        var music = byKind[LinesheetFileKinds.CueMusic].ToDictionary(f => f.FileKey);
        var sfx = byKind[LinesheetFileKinds.CueSfx].ToDictionary(f => f.FileKey);
        var times = c.Timeline.Entries.ToDictionary(e => e.RowId);
        var segments = new List<string>();
        var silenceCache = new Dictionary<int, string>();

        async Task<string> Silence(int ms)
        {
            if (silenceCache.TryGetValue(ms, out var s)) return s;
            s = Path.Combine(work, $"sil_{ms}.wav");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.SilenceArgs(ms, s), work, StepTimeout, ct);
            return silenceCache[ms] = s;
        }

        var n = 0;
        var order = c.Timeline.Entries;
        var nextStart = new Dictionary<string, int>();
        for (var i = 0; i < order.Count; i++)
            nextStart[order[i].RowId] = i + 1 < order.Count ? order[i + 1].StartMs : c.Timeline.TotalMs;
        foreach (var r in c.Rows)
        {
            ct.ThrowIfCancellationRequested();
            var e = times[r.Id];
            var dur = e.EndMs - e.StartMs;
            if (r.IsLine && voices.TryGetValue(r.Id, out var voice) && !c.Timeline.EstimatedRowIds.Contains(r.Id))
            {
                if (voice.DurationMs != dur)
                    throw new LinesheetValidationException([new(0, r.Id, LinesheetErrorCodes.NoTimeline,
                        "Giọng đã đổi sau khi tính dòng thời gian; tính lại rồi dựng lại.")]);
                segments.Add(await LeveledVoiceAsync(c, r, _files.PathFor(tenant, voice.StorageKey), work, n++, ct));
            }
            else if ((r.IsMusic && music.TryGetValue(r.Cue, out var cue)) || (r.IsSfx && sfx.TryGetValue(r.Cue, out cue)))
            {
                var fitted = Path.Combine(work, $"cue_{n++:0000}.wav");
                await _media.RunFfmpegAsync(LinesheetRenderPlan.FitCueArgs(_files.PathFor(tenant, cue.StorageKey), dur, fitted), work, StepTimeout, ct);
                segments.Add(fitted);
            }
            else if (dur > 0)
            {
                // khoảng lặng, câu thiếu giọng (đã ước thời lượng), nhạc hoặc hiệu ứng chưa có file
                segments.Add(await Silence(dur));
            }

            // khoảng sau hàng lấy từ dòng thời gian (gồm cả phần giữ thêm để đọc chữ), không lấy pause_after_ms
            var gap = nextStart[r.Id] - e.EndMs;
            if (gap > 0)
                segments.Add(await Silence(gap));
        }

        var list = Path.Combine(work, "list.txt");
        await File.WriteAllTextAsync(list, LinesheetRenderPlan.ConcatList(segments), new UTF8Encoding(false), ct);
        var path = _files.PathFor(tenant, key);
        // lớp nhạc nền dưới lời (settings.music_beds, 2026-11-06): ghép nối trước, trộn nhạc nền, rồi mới cân cả tập
        var beds = new List<(string, double, double, double, double, double, double)>();
        foreach (var b in c.Settings.MusicBeds)
        {
            if (!music.TryGetValue(b.Cue, out var bf) || !times.TryGetValue(b.FromRow, out var bs) || !times.TryGetValue(b.ToRow, out var be)) continue;
            var startMs = bs.StartMs;
            var endMs = nextStart.TryGetValue(b.ToRow, out var ns) ? ns : be.EndMs;
            if (endMs - startMs < 500) continue;
            beds.Add((_files.PathFor(tenant, bf.StorageKey), startMs / 1000.0, (endMs - startMs) / 1000.0, b.GainDb, Math.Max(0, b.OffsetSec),
                Math.Max(0, b.FadeInMs) / 1000.0, Math.Max(0, b.FadeOutMs) / 1000.0));
        }

        if (beds.Count > 0)
        {
            var dry = Path.Combine(work, "episode_dry.wav");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.ConcatArgs(list, dry), work, StepTimeout, ct);
            var wet = Path.Combine(work, "episode_beds.wav");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.BedMixArgs(dry, beds, wet), work, StepTimeout, ct);
            await File.WriteAllTextAsync(list, LinesheetRenderPlan.ConcatList([wet]), new UTF8Encoding(false), ct);
        }

        if (c.Settings.MasterLufs < 0 || c.Settings.RoomToneDb < 0)
        {
            // cân cả tập về master_lufs, thêm tiếng nền phòng (2026-11-05)
            var raw = Path.Combine(work, "episode_raw.wav");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.ConcatArgs(list, raw), work, StepTimeout, ct);
            double gain = 0;
            if (c.Settings.MasterLufs < 0)
            {
                double? measured = null;
                try
                {
                    measured = LinesheetRenderPlan.ParseIntegratedLufs(await _media.RunFfprobeAsync(
                        LinesheetRenderPlan.LoudnessProbeArgs(Path.GetFileName(raw)), work, StepTimeout, ct));
                }
                catch (InvalidOperationException)
                {
                    // không đo được thì không cân
                }

                if (measured is { } m) gain = Math.Clamp(c.Settings.MasterLufs - m, -12, 15);
            }

            var rawMs = await ProbeMsAsync(raw, ct);
            await _media.RunFfmpegAsync(LinesheetRenderPlan.MasterArgs(raw, gain, c.Settings.RoomToneDb, rawMs / 1000.0, path), work, StepTimeout, ct);
        }
        else
        {
            await _media.RunFfmpegAsync(LinesheetRenderPlan.ConcatArgs(list, path), work, StepTimeout, ct);
        }

        var ms = await ProbeMsAsync(path, ct);
        await _store.SetTimelineAudioAsync(tenant, c.TimelineRecord.Id, key, ms, ct);
        TryDeleteDirectory(work);
        return $"ghép tiếng {segments.Count} đoạn, {ms / 1000.0:0.0} giây";
    }

    /// <summary>
    /// Cân một câu thoại về mức chung (settings.loudness_lufs) cộng gain_db của giọng. Giữ đúng số mẫu nên dòng thời gian
    /// không đổi. Sau EP01 (2026-11-04): mỗi giọng ElevenLabs ra một độ to khác nhau, Mẹ nghe to hơn hẳn các nhân vật khác.
    /// </summary>
    async Task<string> LeveledVoiceAsync(Context c, LinesheetRow row, string voicePath, string work, int n, CancellationToken ct)
    {
        var voiceGain = c.Settings.Voices.TryGetValue(row.Speaker, out var v) ? v.GainDb : 0;
        double? measured = null;
        if (c.Settings.LoudnessLufs < 0)
        {
            try
            {
                var stdout = await _media.RunFfprobeAsync(LinesheetRenderPlan.LoudnessProbeArgs(Path.GetFileName(voicePath)),
                    Path.GetDirectoryName(voicePath)!, StepTimeout, ct);
                measured = LinesheetRenderPlan.ParseIntegratedLufs(stdout);
            }
            catch (InvalidOperationException)
            {
                // không đo được thì chỉ cộng gain_db của giọng
            }
        }

        var gain = LinesheetRenderPlan.VoiceGainDb(measured, c.Settings.LoudnessLufs, voiceGain);
        if (Math.Abs(gain) < 0.3) return voicePath;
        var leveled = Path.Combine(work, $"voice_{n:0000}.wav");
        await _media.RunFfmpegAsync(LinesheetRenderPlan.GainArgs(voicePath, gain, leveled), work, StepTimeout, ct);
        return leveled;
    }

    // ---------- bước shot ----------

    string CacheDir(Context c)
    {
        var r = c.Run;
        return _files.WorkDirectory(r.TenantId, c.Production.Id, $"cache/{r.Format}_{r.Width}x{r.Height}_{r.Fps}_{r.TextLang}");
    }

    async Task<string> ShotStepAsync(Context c, LinesheetShotSpan span, CancellationToken ct)
    {
        var run = c.Run;
        var tenant = run.TenantId;
        var vertical = run.Format == LinesheetFormats.Vertical;
        var shot = span.Shot;
        var assets = LinesheetShotAssets.Resolve(shot, c.Files, c.Settings);
        var (bg, mid, chars) = (assets.Background, assets.Mid, assets.Characters);
        IEnumerable<string> shas = assets.Shas;

        // chuyển động (video AI): chỉ dùng cho 16:9, khi đoạn đã tạo còn khớp ảnh, nét mặt và câu lệnh hiện tại
        string? motionPath = null;
        LinesheetMotionEntry? motionEntry = null;
        var motionSeconds = 0;
        if (!vertical)
        {
            var manifest = FamixaLinesheetService.ReadMotionManifest(
                _files.PathFor(tenant, _files.Key(tenant, c.Production.Id, "motion/manifest.json")));
            if (FamixaLinesheetService.TryMotion(manifest, c.Settings, shot.ShotId, out var m)
                && m.Key == LinesheetMotionPlan.MotionKey(shot, assets.Shas, m.Prompt, m.Seconds, m.Model)
                && File.Exists(_files.PathFor(tenant, m.StorageKey)))
            {
                motionPath = _files.PathFor(tenant, m.StorageKey);
                motionEntry = m;
                motionSeconds = m.Seconds;
                shas = shas.Append("motion:" + m.Sha256);
            }
        }

        // ghép môi: đoạn mặt đã khớp lời của từng câu, chỉ khi còn khớp đoạn chuyển động, giọng, khuôn mặt và nhịp khung
        var lips = new List<LinesheetPlacedLip>();
        if (motionPath is not null && motionEntry is not null && assets.Keyframe is { } kfl
            && LinesheetShotAssets.KeyframeName(shot, c.Settings) is { } kfName && c.Settings.Faces.ContainsKey(kfName))
        {
            var lipManifest = FamixaLinesheetService.ReadLipManifest(
                _files.PathFor(tenant, _files.Key(tenant, c.Production.Id, "lipsync/manifest.json")));
            var voices = c.Files.Where(f => f.Kind == LinesheetFileKinds.LineAudio).ToDictionary(f => f.FileKey);
            var size = LinesheetLipsyncPlan.ImageSize(_files.PathFor(tenant, kfl.StorageKey));
            foreach (var item in LinesheetLipsyncPlan.Items(span, c.Timeline, c.Settings, kfName, size, voices, motionEntry.Sha256, run.Fps))
            {
                if (!lipManifest.TryGetValue(item.Row.Id, out var le) || le.Fps != run.Fps
                    || le.Key != LinesheetLipsyncPlan.LipKey(item, le.Model)) continue;
                var lipPath = _files.PathFor(tenant, le.StorageKey);
                if (!File.Exists(lipPath)) continue;
                lips.Add(new LinesheetPlacedLip(lipPath, item.F0, item.F1, LinesheetLipsyncPlan.Scale(item.Rect, run.Width, run.Height)));
                shas = shas.Append("lip:" + le.Sha256);
            }
        }

        // cảnh cận tay cầm điện thoại cho từng dòng tin nhắn (ảnh đổi thì dựng lại)
        // logo kênh trên thẻ tên phim (settings.title_logo = tên ảnh trong refs/, thêm 2026-11-05)
        string? titleLogo = null;
        if (c.Settings.TitleLogo is { Length: > 0 } logoName && span.Rows.Any(r => r.Cue == "title_sting"))
        {
            var logoKey = LinesheetFileKinds.RefPrefix + FamixaLinesheetService.SafeName(logoName);
            var lf = c.Files.FirstOrDefault(f => f.Kind == LinesheetFileKinds.Character && f.FileKey == logoKey);
            if (lf is not null)
            {
                titleLogo = _files.PathFor(tenant, lf.StorageKey);
                shas = shas.Append("logo:" + lf.Sha256);
            }
        }

        var inserts = new Dictionary<string, string>();
        foreach (var r in span.Rows)
        {
            if (!c.Settings.PhoneInserts.TryGetValue(r.Id, out var insName)) continue;
            var insKey = LinesheetFileKinds.KeyframePrefix + FamixaLinesheetService.SafeName(insName);
            var ins = c.Files.FirstOrDefault(f => f.Kind == LinesheetFileKinds.Character && f.FileKey == insKey);
            if (ins is null) continue;
            inserts[r.Id] = _files.PathFor(tenant, ins.StorageKey);
            shas = shas.Append("phone:" + r.Id + ":" + ins.Sha256);
        }

        var key = LinesheetRenderPlan.ShotCacheKey(span, c.Timeline, run.Width, run.Height, run.Fps, run.TextLang, shas);

        var cacheDir = CacheDir(c);
        var clip = Path.Combine(cacheDir, $"{SafeFile(shot.ShotId)}_{key}.mp4");
        var watch = Stopwatch.StartNew();
        var cached = File.Exists(clip);
        if (!cached)
        {
            var tmp = Path.Combine(cacheDir, "_layers", SafeFile(shot.ShotId) + "_" + key);
            Directory.CreateDirectory(tmp);
            try
            {
                LinesheetRenderPlan.ShotImages images;
                if (assets.Keyframe is { } kf)
                {
                    // ảnh vẽ trọn cảnh: cắt đúng khung (16:9 hoặc 9:16) rồi dùng như ảnh phẳng
                    var still = Path.Combine(tmp, "keyframe.png");
                    await _media.RunFfmpegAsync(LinesheetMotionPlan.StillArgs(
                        new LinesheetRenderPlan.ShotImages(_files.PathFor(tenant, kf.StorageKey), null, null), run.Width, run.Height, still), tmp, StepTimeout, ct);
                    images = new LinesheetRenderPlan.ShotImages(still, null, null);
                }
                else
                {
                    images = _composer.ComposeShot(new LinesheetComposeRequest(
                        shot, c.Settings,
                        _files.PathFor(tenant, bg!.StorageKey),
                        _files.PathFor(tenant, mid!.StorageKey),
                        chars.ToDictionary(kv => kv.Key, kv => _files.PathFor(tenant, kv.Value.StorageKey)),
                        run.Width, run.Height, vertical, tmp));
                }
                var cards = LinesheetRenderPlan.TextLayers(span, c.Timeline, run.TextLang)
                    .Select((layer, i) => _composer.RenderCard(layer, run.Width, run.Height, vertical, c.Settings.ChatSelf, Path.Combine(tmp, $"card_{i:00}.png"),
                        inserts.GetValueOrDefault(layer.RowId),
                        span.Rows.Any(r => r.Id == layer.RowId && r.Cue == "title_sting") ? titleLogo : null))
                    .ToList();
                var partial = clip + ".partial.mp4";
                var shotArgs = motionPath is not null
                    ? LinesheetMotionPlan.MotionArgs(span, motionPath, motionSeconds, cards, run.Width, run.Height, run.Fps, partial, lips)
                    : LinesheetRenderPlan.ShotArgs(span, images, cards, run.Width, run.Height, run.Fps, partial);
                await _media.RunFfmpegAsync(shotArgs, tmp, StepTimeout, ct);
                File.Move(partial, clip, overwrite: true);
            }
            finally
            {
                TryDeleteDirectory(tmp);
            }
        }

        var log = ShotLog(run);
        log.RemoveAll(l => l.ShotId == shot.ShotId);
        log.Add(new LinesheetShotLogEntry(shot.ShotId, shot.Setting, shot.CameraPreset, span.StartMs, span.EndMs, span.Frames,
            key, cached, cached ? 0 : watch.ElapsedMilliseconds, 0m));
        run.ShotLogJson = JsonSerializer.Serialize(log, FamixaLinesheetService.Json);
        return $"{shot.ShotId} {shot.CameraPreset} {span.Frames} khung {(cached ? "(cache)" : $"{watch.Elapsed.TotalSeconds:0.0}s")}";
    }

    static List<LinesheetShotLogEntry> ShotLog(LinesheetRenderRunRecord run) =>
        JsonSerializer.Deserialize<List<LinesheetShotLogEntry>>(run.ShotLogJson, FamixaLinesheetService.Json) ?? [];

    static string SafeFile(string s) => FamixaLinesheetService.SafeName(s) is { Length: > 0 } x ? x : "shot";

    // ---------- bước cuối ----------

    async Task<string> FinalStepAsync(Context c, CancellationToken ct)
    {
        var run = c.Run;
        var tenant = run.TenantId;
        var vertical = run.Format == LinesheetFormats.Vertical;
        var audioKey = c.TimelineRecord.AudioStorageKey ?? throw new InvalidOperationException("LINESHEET_NO_AUDIO: bước ghép tiếng chưa chạy.");
        var audio = _files.PathFor(tenant, audioKey);

        var cacheDir = CacheDir(c);
        var log = ShotLog(run).ToDictionary(l => l.ShotId);
        var clips = new List<string>();
        foreach (var span in c.Spans)
        {
            if (!log.TryGetValue(span.Shot.ShotId, out var entry))
                throw new InvalidOperationException("LINESHEET_SHOT_NOT_RENDERED: " + span.Shot.ShotId);
            var clip = Path.Combine(cacheDir, $"{SafeFile(span.Shot.ShotId)}_{entry.CacheKey}.mp4");
            if (!File.Exists(clip))
                throw new InvalidOperationException("LINESHEET_CLIP_MISSING: " + span.Shot.ShotId);
            clips.Add(clip);
        }

        var outRel = $"renders/{run.Id:N}";
        var outKey = _files.Key(tenant, c.Production.Id, outRel + "/output.mp4");
        var output = _files.PathFor(tenant, outKey);
        var outDir = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(outDir);

        // đoạn kết (settings.outro, 2026-11-06): chỉ khi dựng cả tập
        var expected = c.Spans.Sum(s => s.Frames);
        var audioEndMs = run.RangeEndMs;
        var outro = c.Settings.Outro;
        if (outro.Enabled && run.RangeStartMs == 0 && run.RangeEndMs >= c.Timeline.TotalMs)
        {
            var logoName = outro.Logo is { Length: > 0 } ln ? ln : c.Settings.TitleLogo;
            var logoFile = logoName.Length == 0 ? null : c.Files.FirstOrDefault(f =>
                f.Kind == LinesheetFileKinds.Character && f.FileKey == LinesheetFileKinds.RefPrefix + FamixaLinesheetService.SafeName(logoName));
            var titlePng = Path.Combine(outDir, "outro_title.png");
            var cardPng = Path.Combine(outDir, "outro_card.png");
            _composer.RenderOutroCards(outro, run.Width, run.Height, vertical,
                logoFile is null ? null : _files.PathFor(tenant, logoFile.StorageKey), titlePng, cardPng);
            var outroClip = Path.Combine(outDir, "outro.mp4");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.OutroVideoArgs(titlePng, cardPng, outro.TitleSeconds, outro.CardSeconds,
                run.Width, run.Height, run.Fps, outroClip), outDir, StepTimeout, ct);
            clips.Add(outroClip);

            string? CuePath(string name) => name.Length == 0 ? null : c.Files.FirstOrDefault(f =>
                f.Kind == LinesheetFileKinds.CueMusic && f.FileKey == name) is { } cf ? _files.PathFor(tenant, cf.StorageKey) : null;
            var outroWav = Path.Combine(outDir, "outro.wav");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.OutroAudioArgs(CuePath(outro.StingCue), CuePath(outro.MusicCue), outro.MusicStartSec,
                outro.TitleSeconds, outro.CardSeconds, outroWav), outDir, StepTimeout, ct);
            var audioList = Path.Combine(outDir, "audio_concat.txt");
            await File.WriteAllTextAsync(audioList, LinesheetRenderPlan.ConcatList([audio, outroWav]), new UTF8Encoding(false), ct);
            var withOutro = Path.Combine(outDir, "audio_with_outro.wav");
            await _media.RunFfmpegAsync(LinesheetRenderPlan.ConcatArgs(audioList, withOutro), outDir, StepTimeout, ct);
            audio = withOutro;
            var outroFrames = (int)Math.Round((outro.TitleSeconds + outro.CardSeconds) * run.Fps, MidpointRounding.AwayFromZero);
            expected += outroFrames;
            audioEndMs = c.Timeline.TotalMs + (int)Math.Round(outroFrames * 1000.0 / run.Fps);
        }

        var list = Path.Combine(outDir, "concat.txt");
        await File.WriteAllTextAsync(list, LinesheetRenderPlan.ConcatList(clips), new UTF8Encoding(false), ct);
        var video = Path.Combine(outDir, "video_only.mp4");
        await _media.RunFfmpegAsync(LinesheetRenderPlan.ConcatArgs(list, video), outDir, StepTimeout, ct);

        string? srtVi = null, srtEn = null;
        var tracks = new List<(string Path, string Language)>();
        foreach (var lang in new[] { LinesheetLanguages.Vi, LinesheetLanguages.En })
        {
            var text = LinesheetTimelineBuilder.SrtRange(c.Rows, c.Timeline, lang, run.RangeStartMs, run.RangeEndMs);
            if (text.Length == 0) continue; // FFmpeg không nhận SRT rỗng
            var key = _files.Key(tenant, c.Production.Id, $"{outRel}/subs.{lang}.srt");
            await File.WriteAllTextAsync(_files.PathFor(tenant, key), text, new UTF8Encoding(false), ct);
            if (lang == LinesheetLanguages.Vi) srtVi = key; else srtEn = key;
            tracks.Add((_files.PathFor(tenant, key), lang == LinesheetLanguages.Vi ? "vie" : "eng"));
        }

        var partial = Path.Combine(outDir, "output.partial.mp4");
        if (run.BurnSubs is LinesheetLanguages.Vi or LinesheetLanguages.En)
        {
            var burnKey = run.BurnSubs == LinesheetLanguages.Vi ? srtVi : srtEn;
            var subName = burnKey is null ? null : Path.GetFileName(_files.PathFor(tenant, burnKey));
            var fontsRel = Path.GetRelativePath(outDir, _files.FontsDirectory).Replace('\\', '/');
            await _media.RunFfmpegAsync(
                LinesheetRenderPlan.BurnArgs(video, audio, run.RangeStartMs, audioEndMs, expected, run.Fps,
                    subName, fontsRel, _files.FontFamily, vertical, partial),
                outDir, StepTimeout, ct);
        }
        else
        {
            await _media.RunFfmpegAsync(
                LinesheetRenderPlan.MuxArgs(video, audio, run.RangeStartMs, audioEndMs, expected, run.Fps, tracks, partial),
                outDir, StepTimeout, ct);
        }

        var counted = await _media.RunFfprobeAsync(LinesheetRenderPlan.CountFramesProbeArgs(partial), outDir, StepTimeout, ct);
        if (!int.TryParse(counted.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var actual))
            throw new InvalidOperationException(LinesheetErrorCodes.FrameMismatch + ": không đếm được khung.");
        run.FramesExpected = expected;
        run.FramesActual = actual;
        if (Math.Abs(actual - expected) > 1)
            throw new LinesheetValidationException([new(0, "", LinesheetErrorCodes.FrameMismatch,
                $"Số khung {actual} khác dự kiến {expected}.")]);

        File.Move(partial, output, overwrite: true);
        TryDelete(video);
        TryDelete(list);
        run.OutputStorageKey = outKey;
        run.SrtViStorageKey = srtVi;
        run.SrtEnStorageKey = srtEn;
        return $"xong {actual} khung ({actual / (double)run.Fps:0.0} giây)";
    }

    async Task<int> ProbeMsAsync(string path, CancellationToken ct)
    {
        var text = await _media.RunFfprobeAsync(LinesheetRenderPlan.DurationProbeArgs(path), Path.GetDirectoryName(path)!, ProbeTimeout, ct);
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var sec)
            ? (int)Math.Round(sec * 1000, MidpointRounding.ToEven)
            : throw new InvalidOperationException("LINESHEET_PROBE_FAILED: " + Path.GetFileName(path));
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* tạm */ }
    }

    static void TryDeleteDirectory(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* tạm */ }
    }
}
