namespace KitPlatform.Packs.Content.Production;

// ---------- bản ghi lưu trữ (Dapper ánh xạ theo tên thuộc tính) ----------

public sealed class LinesheetProductionRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UniverseCode { get; set; } = "";
    public string EpisodeCode { get; set; } = "";
    public string Title { get; set; } = "";
    public string SettingsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LinesheetVersionRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductionId { get; set; }
    public int VersionNo { get; set; }
    public string SourceSha256 { get; set; } = "";
    public string RowsJson { get; set; } = "[]";
    public int RowCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class LinesheetFileKinds
{
    public const string LineAudio = "line_audio";
    public const string CueMusic = "cue_music";
    public const string CueSfx = "cue_sfx";
    public const string LayerBackground = "layer_bg";
    public const string LayerMid = "layer_mid";
    public const string Character = "character";

    /// <summary>
    /// Ảnh vẽ trọn cảnh (nhân vật đúng tư thế trong bối cảnh) tải lên ở <c>keyframes/&lt;tên&gt;.png</c>. Lưu chung loại
    /// "character" với khóa có tiền tố KF_ để không phải sửa ràng buộc loại file của migration 430.
    /// </summary>
    public const string KeyframePrefix = "KF_";
    /// <summary>Ảnh mẫu nhân vật (bộ mẫu đã duyệt) tải ở "refs/&lt;tên&gt;.png", dùng làm tham chiếu khi tạo ảnh cảnh.</summary>
    public const string RefPrefix = "REF_";
}

public sealed class LinesheetFileRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductionId { get; set; }
    public string Kind { get; set; } = "";
    public string FileKey { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long SizeBytes { get; set; }
    public int? DurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LinesheetTimelineRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductionId { get; set; }
    public Guid VersionId { get; set; }
    public string AudioSetSha256 { get; set; } = "";
    public string EntriesJson { get; set; } = "[]";
    public string EstimatedJson { get; set; } = "[]";
    public int TotalMs { get; set; }
    public string? AudioStorageKey { get; set; }
    public int? AudioDurationMs { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LinesheetShotSetRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductionId { get; set; }
    public Guid VersionId { get; set; }
    public string ShotsJson { get; set; } = "[]";
    public int ShotCount { get; set; }
    public bool Edited { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class LinesheetRenderRunRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProductionId { get; set; }
    public Guid VersionId { get; set; }
    public Guid TimelineId { get; set; }
    public Guid ShotSetId { get; set; }
    public string Kind { get; set; } = "episode";
    public string Format { get; set; } = LinesheetFormats.Landscape;
    public int Width { get; set; }
    public int Height { get; set; }
    public int Fps { get; set; } = LinesheetRenderPlan.DefaultFps;
    public int RangeStartMs { get; set; }
    public int RangeEndMs { get; set; }
    public string TextLang { get; set; } = LinesheetLanguages.Vi;
    public string BurnSubs { get; set; } = "none";
    public string Status { get; set; } = LinesheetRunStatuses.Queued;
    public int NextStep { get; set; }
    public int StepCount { get; set; }
    public int Attempts { get; set; }
    public int? FramesExpected { get; set; }
    public int? FramesActual { get; set; }
    public string ShotLogJson { get; set; } = "[]";
    public string? OutputStorageKey { get; set; }
    public string? SrtViStorageKey { get; set; }
    public string? SrtEnStorageKey { get; set; }
    public string? Error { get; set; }
    public string CreatedBy { get; set; } = "";
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? HeartbeatAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record LinesheetShotLogEntry(
    string ShotId, string Setting, string Preset, int StartMs, int EndMs, int Frames,
    string CacheKey, bool Cached, long RenderMs, decimal CostUsd);

// ---------- cổng ra hạ tầng ----------

/// <summary>Kho dữ liệu M1. Mọi hàm nhận tenant tường minh: không có đường đọc/ghi không lọc tenant.</summary>
public interface IFamixaLinesheetStore
{
    Task<LinesheetProductionRecord> CreateProductionAsync(Guid tenant, string universeCode, string episodeCode, string title, CancellationToken ct);
    Task<LinesheetProductionRecord?> GetProductionAsync(Guid tenant, Guid productionId, CancellationToken ct);
    Task<IReadOnlyList<LinesheetProductionRecord>> ListProductionsAsync(Guid tenant, CancellationToken ct);
    Task UpdateSettingsAsync(Guid tenant, Guid productionId, string settingsJson, CancellationToken ct);

    Task<LinesheetVersionRecord> InsertVersionAsync(Guid tenant, Guid productionId, string sourceSha256, string rowsJson, int rowCount, CancellationToken ct);
    Task<LinesheetVersionRecord?> GetLatestVersionAsync(Guid tenant, Guid productionId, CancellationToken ct);
    Task<LinesheetVersionRecord?> GetVersionAsync(Guid tenant, Guid versionId, CancellationToken ct);

    Task UpsertFileAsync(Guid tenant, LinesheetFileRecord file, CancellationToken ct);
    Task<IReadOnlyList<LinesheetFileRecord>> ListFilesAsync(Guid tenant, Guid productionId, CancellationToken ct);

    Task<LinesheetTimelineRecord> InsertTimelineAsync(Guid tenant, LinesheetTimelineRecord timeline, CancellationToken ct);
    Task<LinesheetTimelineRecord?> GetLatestTimelineAsync(Guid tenant, Guid productionId, CancellationToken ct);
    Task<LinesheetTimelineRecord?> GetTimelineAsync(Guid tenant, Guid timelineId, CancellationToken ct);
    Task SetTimelineAudioAsync(Guid tenant, Guid timelineId, string storageKey, int durationMs, CancellationToken ct);

    Task<LinesheetShotSetRecord> InsertShotSetAsync(Guid tenant, LinesheetShotSetRecord shotSet, CancellationToken ct);
    Task<LinesheetShotSetRecord?> GetLatestShotSetAsync(Guid tenant, Guid productionId, CancellationToken ct);
    Task<LinesheetShotSetRecord?> GetShotSetAsync(Guid tenant, Guid shotSetId, CancellationToken ct);

    Task<LinesheetRenderRunRecord> InsertRunAsync(Guid tenant, LinesheetRenderRunRecord run, CancellationToken ct);
    Task<LinesheetRenderRunRecord?> GetRunAsync(Guid tenant, Guid runId, CancellationToken ct);
    Task<IReadOnlyList<LinesheetRenderRunRecord>> ListRunsAsync(Guid tenant, Guid productionId, int limit, CancellationToken ct);
    Task<bool> CancelRunAsync(Guid tenant, Guid runId, CancellationToken ct);

    /// <summary>Worker: nhận một lần dựng đang chờ (hoặc RUNNING mà heartbeat quá hạn) của bất kỳ tenant nào.</summary>
    Task<LinesheetRenderRunRecord?> ClaimNextRunAsync(TimeSpan staleAfter, CancellationToken ct);

    /// <summary>Worker: lưu tiến độ. WHERE theo cả id và tenant_id của bản ghi.</summary>
    Task SaveRunProgressAsync(LinesheetRenderRunRecord run, CancellationToken ct);
}

/// <summary>Kho file M1 trên đĩa. Khóa lưu trữ luôn có tiền tố tenant; đọc khóa của tenant khác bị từ chối.</summary>
public interface IFamixaLinesheetFileStore
{
    string Key(Guid tenant, Guid productionId, string relativePath);
    string PathFor(Guid tenant, string storageKey);
    string WorkDirectory(Guid tenant, Guid productionId, string name);
    string FontsDirectory { get; }
    string FontFamily { get; }
    string FontRegularPath { get; }
    string FontBoldPath { get; }
}

/// <summary>FFmpeg và ffprobe. Đối số là danh sách, không qua shell; có giới hạn thời gian.</summary>
public interface ILinesheetMediaTools
{
    Task RunFfmpegAsync(IReadOnlyList<string> args, string workingDirectory, TimeSpan timeout, CancellationToken ct);
    Task<string> RunFfprobeAsync(IReadOnlyList<string> args, string workingDirectory, TimeSpan timeout, CancellationToken ct);
}

public sealed record LinesheetComposeRequest(
    LinesheetShot Shot,
    LinesheetSettings Settings,
    string BackgroundPath,
    string MidPath,
    IReadOnlyDictionary<string, string> CharacterPaths,
    int Width,
    int Height,
    bool Vertical,
    string OutputDirectory);

/// <summary>Ghép lớp ảnh và vẽ thẻ chữ (SkiaSharp ở hạ tầng).</summary>
public interface ILinesheetLayerComposer
{
    LinesheetRenderPlan.ShotImages ComposeShot(LinesheetComposeRequest request);
    /// <param name="phoneInsertPng">Ảnh cận tay cầm điện thoại (màn hình trắng) để dán tin nhắn lên; không có thì vẽ điện thoại kín khung.</param>
    LinesheetRenderPlan.PlacedCard RenderCard(LinesheetTextLayer layer, int width, int height, bool vertical, IReadOnlyCollection<string> chatSelf, string outputPng, string? phoneInsertPng = null, string? titleLogoPng = null);
    /// <summary>Vẽ hai khung của đoạn kết ("HẾT TẬP" và khung kết) ra hai ảnh PNG kín khung.</summary>
    void RenderOutroCards(LinesheetOutro outro, int width, int height, bool vertical, string? logoPng, string titlePng, string cardPng);
}

/// <summary>Tạo giọng một câu (ElevenLabs ở hạ tầng). Trả về byte audio (mp3).</summary>
public interface ILinesheetTts
{
    /// <param name="take">Lần tạo của câu (1, 2, 3…). Mỗi lần tạo dùng một seed khác để ra một bản đọc khác.</param>
    Task<byte[]> SynthesizeAsync(string voiceId, string text, LinesheetVoiceDef voice, int take, CancellationToken ct);
}

/// <summary>Mô hình ảnh-thành-video (Runway ở hạ tầng). Khóa lấy từ cài đặt Video của Content.</summary>
public interface ILinesheetVideoGen
{
    /// <summary>Mô hình mặc định (gen4_turbo).</summary>
    string Model { get; }
    IReadOnlyList<string> Models { get; }
    int CreditsPerSecond(string model);
    Task<string> StartAsync(byte[] jpeg, string prompt, int seconds, string model, CancellationToken ct);
    Task<LinesheetVideoGenStatus> PollAsync(string taskId, CancellationToken ct);
    Task<byte[]> DownloadAsync(string url, CancellationToken ct);

    /// <summary>Số credit còn lại; null nếu không biết (khi đó không chặn). Thêm 2026-11-09.</summary>
    Task<int?> CreditBalanceAsync(CancellationToken ct) => Task.FromResult<int?>(null);
}

public sealed record LinesheetVideoGenStatus(string Status, string? VideoUrl, string? Error);

/// <summary>Một ảnh tham chiếu gửi kèm khi tạo ảnh: tag dùng trong câu lệnh là "@tag".</summary>
public sealed record LinesheetImageRef(string Tag, byte[] Jpeg);

/// <summary>
/// Mô hình tạo ảnh có tham chiếu (qua Runway ở hạ tầng): gen4_image (tối đa 3 ảnh tham chiếu) hoặc gpt_image_2
/// (tối đa 16, chất lượng low/medium/high). Tag 3–16 ký tự, bắt đầu bằng chữ.
/// </summary>
public interface ILinesheetImageGen
{
    IReadOnlyList<string> Models { get; }
    int MaxRefs(string model);
    /// <summary>Số credit cho một ảnh (đối chiếu với trang Usage của nhà cung cấp).</summary>
    int CreditsPerImage(string model, string ratio, string quality);

    /// <summary>Số credit còn lại; null nếu không biết (khi đó không chặn). Thêm 2026-11-09.</summary>
    Task<int?> CreditBalanceAsync(CancellationToken ct) => Task.FromResult<int?>(null);
    Task<byte[]> GenerateAsync(string prompt, IReadOnlyList<LinesheetImageRef> refs, string ratio, string model, string quality, int? seed, CancellationToken ct);
}

/// <summary>Mô hình ghép môi (fal.ai sync lipsync-2, lipsync-2-pro, sync-3 ở hạ tầng): nhận đoạn hình một khuôn mặt và tiếng, trả đoạn hình đã khớp môi.</summary>
public interface ILinesheetLipsync
{
    /// <summary>Các mô hình dùng được, ví dụ lipsync-2, lipsync-2-pro, sync-3.</summary>
    IReadOnlyList<string> Models { get; }
    decimal UsdPerSecond(string model);
    Task<byte[]> SyncAsync(byte[] videoMp4, byte[] audioMp3, string model, CancellationToken ct);
}

// ---------- API ----------

public sealed record LinesheetCreateProductionRequest(string? UniverseCode, string? EpisodeCode, string? Title);

public sealed record LinesheetQueueRenderRequest(
    string? Format,
    int? Width,
    int? Height,
    int? Fps,
    int? RangeStartMs,
    int? RangeEndMs,
    string? TextLang,
    string? BurnSubs);

public sealed record LinesheetUploadResultDto(
    int Accepted,
    IReadOnlyList<string> Skipped,
    IReadOnlyDictionary<string, int> ByKind);

public sealed record LinesheetProductionDto(
    Guid Id,
    string UniverseCode,
    string EpisodeCode,
    string Title,
    DateTimeOffset UpdatedAt);

public sealed record LinesheetStatusDto(
    LinesheetProductionDto Production,
    int? ScriptVersion,
    int RowCount,
    int LineCount,
    bool SettingsReady,
    IReadOnlyList<string> SettingsErrors,
    IReadOnlyDictionary<string, int> FileCounts,
    IReadOnlyList<string> MissingAudio,
    IReadOnlyList<string> MissingCues,
    IReadOnlyList<string> MissingLayers,
    int? TimelineTotalMs,
    int TimelineEstimatedCount,
    int? ShotCount,
    bool ShotsEdited);

public sealed record LinesheetRunDto(
    Guid Id,
    string Kind,
    string Format,
    int Width,
    int Height,
    int Fps,
    int RangeStartMs,
    int RangeEndMs,
    string TextLang,
    string BurnSubs,
    string Status,
    int NextStep,
    int StepCount,
    int? FramesExpected,
    int? FramesActual,
    bool HasOutput,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<LinesheetShotLogEntry> ShotLog);

public sealed record LinesheetTtsLineDto(string RowId, string Speaker, string Tags, string Text, int Chars, bool Pending, int Take = 1);

public sealed record LinesheetTtsPreviewDto(
    string Model,
    int LineCount,
    int TotalChars,
    int PendingCount,
    int PendingChars,
    IReadOnlyList<string> MissingVoices,
    IReadOnlyList<LinesheetTtsLineDto> Lines);

/// <summary>
/// <paramref name="ConfirmChars"/> phải bằng đúng số ký tự sẽ gửi (lấy từ bản xem trước), để không gọi nhầm
/// một lượt tốn tiền khi kịch bản vừa đổi.
/// </summary>
/// <param name="NewTake">Cùng với RowIds: tạo một bản đọc mới (seed khác) cho các câu đó thay vì đọc lại bản cũ.</param>
public sealed record LinesheetTtsRequest(IReadOnlyList<string>? RowIds, bool OnlyPending, int ConfirmChars, bool NewTake = false);

public sealed record LinesheetTtsResultDto(int Generated, int CharsSent, IReadOnlyList<string> Failed);

public sealed record LinesheetMotionShotDto(
    string ShotId, string Scene, string Setting, int DurationMs, int ClipSeconds, int Credits, string Prompt, bool Has);

/// <param name="Warnings">Cảnh báo dùng chung clip (motion_share). Thêm 2026-11-09, sổ lỗi A3.</param>
/// <param name="CreditBalance">Số credit Runway còn lại lúc xem trước; null nếu không đọc được.</param>
public sealed record LinesheetMotionPreviewDto(
    string Model, int ShotCount, int TotalCredits, int PendingCount, int PendingCredits, IReadOnlyList<LinesheetMotionShotDto> Shots,
    IReadOnlyList<string>? Warnings = null, int? CreditBalance = null);

/// <param name="ShotIds">Shot cần tạo; trống là mọi shot chưa có chuyển động.</param>
/// <param name="ConfirmCredits">Phải đúng số credit sẽ dùng (như xác nhận ký tự khi tạo giọng).</param>
/// <param name="Prompts">Câu lệnh riêng cho từng shot, ghi đè câu lệnh mặc định.</param>
public sealed record LinesheetMotionRequest(IReadOnlyList<string>? ShotIds, int ConfirmCredits, Dictionary<string, string>? Prompts = null, bool Force = false);

public sealed record LinesheetMotionResultDto(int Generated, int CreditsUsed, IReadOnlyList<string> Failed);

public sealed record LinesheetLipsyncLineDto(string RowId, string ShotId, string Speaker, double Seconds, decimal Usd, bool Has);

public sealed record LinesheetLipsyncPreviewDto(
    string Model, int LineCount, decimal TotalUsd, int PendingCount, decimal PendingUsd, IReadOnlyList<LinesheetLipsyncLineDto> Lines,
    IReadOnlyList<string> Notes);

/// <summary>ConfirmUsd phải đúng số tiền đã xem trước (làm tròn 2 chữ số).</summary>
public sealed record LinesheetLipsyncRequest(IReadOnlyList<string>? RowIds, decimal ConfirmUsd, bool Force = false);

public sealed record LinesheetLipsyncResultDto(int Generated, decimal UsdUsed, IReadOnlyList<string> Failed);

public sealed record LinesheetKeyframeGenDto(string Name, string Model, string Ratio, int Credits, IReadOnlyList<string> Refs, string Prompt, bool Has);

public sealed record LinesheetKeyframeGenPreviewDto(int Count, int TotalCredits, int PendingCount, int PendingCredits, IReadOnlyList<LinesheetKeyframeGenDto> Keyframes);

/// <summary>Tạo ảnh cảnh từ settings.keyframe_specs. ConfirmCredits phải đúng số credit đã xem trước.</summary>
public sealed record LinesheetKeyframeGenRequest(IReadOnlyList<string>? Names, int ConfirmCredits, bool Force = false);

public sealed record LinesheetKeyframeGenResultDto(int Generated, int CreditsUsed, IReadOnlyList<string> Failed);

public sealed record LinesheetDownload(string Path, string ContentType, string FileName);

public interface IFamixaLinesheetService
{
    Task<IReadOnlyList<LinesheetProductionDto>> ListProductionsAsync(CancellationToken ct);
    Task<LinesheetProductionDto> CreateProductionAsync(LinesheetCreateProductionRequest request, CancellationToken ct);
    Task<LinesheetStatusDto> GetStatusAsync(Guid productionId, CancellationToken ct);
    Task<int> ImportScriptAsync(Guid productionId, byte[] csvBytes, CancellationToken ct);
    Task PutSettingsAsync(Guid productionId, string settingsJson, CancellationToken ct);
    Task<string> GetSettingsAsync(Guid productionId, CancellationToken ct);
    Task<LinesheetUploadResultDto> UploadFilesAsync(Guid productionId, Stream zip, CancellationToken ct);
    Task<LinesheetStatusDto> BuildTimelineAsync(Guid productionId, bool allowMissing, CancellationToken ct);
    Task<string> GetTimelineCsvAsync(Guid productionId, CancellationToken ct);
    Task<string> GetSubtitlesAsync(Guid productionId, string lang, CancellationToken ct);
    Task<int> DraftShotsAsync(Guid productionId, bool force, CancellationToken ct);
    Task<string> GetShotsCsvAsync(Guid productionId, CancellationToken ct);
    Task<int> PutShotsCsvAsync(Guid productionId, byte[] csvBytes, CancellationToken ct);
    Task<LinesheetRunDto> QueueRenderAsync(Guid productionId, LinesheetQueueRenderRequest request, CancellationToken ct);
    Task<IReadOnlyList<LinesheetRunDto>> ListRunsAsync(Guid productionId, CancellationToken ct);
    Task<LinesheetRunDto> GetRunAsync(Guid runId, CancellationToken ct);
    Task<bool> CancelRunAsync(Guid runId, CancellationToken ct);
    Task<LinesheetDownload> DownloadRunFileAsync(Guid runId, string what, CancellationToken ct);
    Task<LinesheetTtsPreviewDto> PreviewTtsAsync(Guid productionId, CancellationToken ct);
    /// <summary>Giọng đã lưu của một câu (WAV), để nghe thử trên trang.</summary>
    Task<LinesheetDownload> GetLineAudioAsync(Guid productionId, string rowId, CancellationToken ct);
    Task<LinesheetMotionPreviewDto> PreviewMotionAsync(Guid productionId, CancellationToken ct);
    Task<LinesheetMotionResultDto> GenerateMotionAsync(Guid productionId, LinesheetMotionRequest request, CancellationToken ct);
    /// <summary>Đoạn chuyển động đã tạo của một shot (MP4), để xem thử.</summary>
    Task<LinesheetDownload> GetMotionClipAsync(Guid productionId, string shotId, CancellationToken ct);
    Task<LinesheetDownload> GetKeyframeImageAsync(Guid productionId, string name, CancellationToken ct);
    Task<LinesheetLipsyncPreviewDto> PreviewLipsyncAsync(Guid productionId, CancellationToken ct);
    Task<LinesheetLipsyncResultDto> GenerateLipsyncAsync(Guid productionId, LinesheetLipsyncRequest request, CancellationToken ct);
    Task<LinesheetKeyframeGenPreviewDto> PreviewKeyframeGenAsync(Guid productionId, CancellationToken ct);
    Task<LinesheetKeyframeGenResultDto> GenerateKeyframesAsync(Guid productionId, LinesheetKeyframeGenRequest request, CancellationToken ct);
    Task<LinesheetTtsResultDto> GenerateTtsAsync(Guid productionId, LinesheetTtsRequest request, CancellationToken ct);
}

/// <summary>Worker gọi: làm đúng một bước của một lần dựng rồi trả quyền cho vòng lặp.</summary>
public interface IFamixaLinesheetRenderer
{
    /// <returns>Mô tả ngắn bước đã làm, hoặc null nếu không có việc.</returns>
    Task<string?> RunNextStepAsync(CancellationToken ct);
}
