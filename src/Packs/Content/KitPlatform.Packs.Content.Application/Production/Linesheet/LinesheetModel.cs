using System.Text.Json.Serialization;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// M1 "Dựng tập từ bảng câu". Một hàng của kịch bản bảng câu (CSV 13 cột).
/// Hàng là nguồn duy nhất cho giọng, phụ đề, lớp chữ và độ dài shot.
/// </summary>
public sealed record LinesheetRow(
    int Ord,
    string Id,
    string Scene,
    string Type,
    string Speaker,
    string Vi,
    string En,
    string Direction,
    int? DurationMs,
    int PauseAfterMs,
    string Cue,
    string ScreenVi,
    string ScreenEn,
    bool TestFirst,
    string TtsTags = "")
{
    public bool IsLine => Type == LinesheetRowTypes.Line;
    public bool IsPause => Type == LinesheetRowTypes.Pause;
    public bool IsMusic => Type == LinesheetRowTypes.Music;
    public bool IsSfx => Type == LinesheetRowTypes.Sfx;

    public string Screen(string lang) => lang == LinesheetLanguages.En ? ScreenEn : ScreenVi;
    public string Text(string lang) => lang == LinesheetLanguages.En ? En : Vi;
}

public static class LinesheetRowTypes
{
    public const string Line = "line";
    public const string Pause = "pause";
    public const string Music = "music";
    public const string Sfx = "sfx";

    public static readonly IReadOnlyList<string> All = [Line, Pause, Music, Sfx];
}

public static class LinesheetLanguages
{
    public const string Vi = "vi";
    public const string En = "en";

    public static bool IsKnown(string? lang) => lang is Vi or En;
}

public static class LinesheetCameraPresets
{
    public const string Static = "static";
    public const string SlowZoomIn = "slow_zoom_in";
    public const string SlowZoomOut = "slow_zoom_out";
    public const string PanLeft = "pan_left";
    public const string PanRight = "pan_right";
    public const string Parallax = "parallax";
    public const string ShakeLight = "shake_light";

    public static readonly IReadOnlyList<string> All =
        [Static, SlowZoomIn, SlowZoomOut, PanLeft, PanRight, Parallax, ShakeLight];

    /// <summary>Vòng xoay mặc định của bản nháp, theo số thứ tự shot (1-based) chia lấy dư.</summary>
    public static readonly IReadOnlyList<string> DraftRotation =
        [SlowZoomIn, PanRight, Static, SlowZoomOut, PanLeft];

    public static bool IsKnown(string? preset) => preset is not null && All.Contains(preset);
}

public static class LinesheetTransitions
{
    public const string Cut = "cut";
    public const string Fade = "fade";

    public static bool IsKnown(string? t) => t is Cut or Fade;
}

public static class LinesheetFormats
{
    public const string Landscape = "16x9";
    public const string Vertical = "9x16";

    public static bool IsKnown(string? f) => f is Landscape or Vertical;

    public static (int Width, int Height) DefaultSize(string format) =>
        format == Vertical ? (1080, 1920) : (1920, 1080);

    public static (int Width, int Height) PreviewSize(string format) =>
        format == Vertical ? (540, 960) : (1280, 720);
}

public static class LinesheetRunStatuses
{
    public const string Queued = "QUEUED";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>Một shot của bảng shot. Shot kéo dài từ hàng đầu của nó đến hàng đầu của shot kế tiếp.</summary>
public sealed record LinesheetShot(
    string ShotId,
    string Scene,
    string Setting,
    string StartRow,
    string EndRow,
    IReadOnlyList<string> Characters,
    string Focus,
    string CameraPreset,
    string TransitionIn,
    string AiClipSlot,
    string Notes,
    string Expressions = "");

/// <summary>Mốc thời gian của một hàng, tính từ âm thanh thật (đã cắt khoảng lặng thừa).</summary>
public sealed record LinesheetTimelineEntry(string RowId, int StartMs, int EndMs);

public sealed record LinesheetTimeline(
    IReadOnlyList<LinesheetTimelineEntry> Entries,
    int TotalMs,
    IReadOnlyList<string> EstimatedRowIds)
{
    public LinesheetTimelineEntry this[string rowId] =>
        Entries.FirstOrDefault(e => e.RowId == rowId)
        ?? throw new KeyNotFoundException(rowId);
}

/// <summary>Cấu hình bối cảnh và dàn nhân vật (cùng dạng với ep01_settings.json).</summary>
public sealed class LinesheetSettings
{
    [JsonPropertyName("scene_to_setting")]
    public Dictionary<string, string> SceneToSetting { get; set; } = new();

    [JsonPropertyName("row_setting_rules")]
    public List<LinesheetSettingRule> RowSettingRules { get; set; } = new();

    [JsonPropertyName("settings")]
    public Dictionary<string, LinesheetSettingDef> Settings { get; set; } = new();

    [JsonPropertyName("characters")]
    public Dictionary<string, LinesheetCharacterDef> Characters { get; set; } = new();

    /// <summary>
    /// Quy tắc chọn nét mặt cho bảng shot nháp (M1.1). Ảnh nét mặt đặt tên <c>characters/&lt;ID&gt;_&lt;nét&gt;.png</c>;
    /// thiếu ảnh thì dùng ảnh thường <c>&lt;ID&gt;.png</c>.
    /// </summary>
    [JsonPropertyName("expression_rules")]
    public List<LinesheetExpressionRule> ExpressionRules { get; set; } = new();

    /// <summary>Giọng ElevenLabs theo người nói (ID trong cột speaker, kể cả NARRATOR).</summary>
    [JsonPropertyName("voices")]
    public Dictionary<string, LinesheetVoiceDef> Voices { get; set; } = new();

    /// <summary>Chuyển ghi chú diễn xuất (cột direction) thành thẻ cảm xúc của Eleven v3. Trống thì dùng bộ mặc định.</summary>
    [JsonPropertyName("tts_tag_rules")]
    public List<LinesheetTtsTagRule> TtsTagRules { get; set; } = new();

    /// <summary>Tên người gửi được vẽ là "mình" trong thẻ tin nhắn (bong bóng xanh, bên phải), ví dụ ["Quân"].</summary>
    [JsonPropertyName("chat_self")]
    public List<string> ChatSelf { get; set; } = new();

    /// <summary>
    /// Mức âm lượng chung (LUFS) mà mọi câu thoại được cân về khi ghép tiếng, để không giọng nào to hơn hẳn.
    /// Mặc định -20. Đặt 0 để tắt.
    /// </summary>
    [JsonPropertyName("loudness_lufs")]
    public double LoudnessLufs { get; set; } = -20;

    /// <summary>
    /// Độ to cả tập sau khi ghép (LUFS), chặn đỉnh ở −1 dB. Mặc định −14 theo chuẩn YouTube/Facebook; 0 để tắt.
    /// Thêm 2026-11-05: EP01 bản 9 chỉ đạt −21 LUFS, nghe nhỏ hơn hẳn video khác.
    /// </summary>
    [JsonPropertyName("master_lufs")]
    public double MasterLufs { get; set; } = -14;

    /// <summary>
    /// Tiếng nền phòng rất nhỏ (dBFS sau khi cân cả tập) chạy suốt tập, để khoảng lặng không im tuyệt đối như mất tiếng.
    /// Mặc định −50; 0 để tắt. Thêm 2026-11-05 sau hai khoảng lặng 5 và 4 giây ở EP01.
    /// </summary>
    [JsonPropertyName("room_tone_db")]
    public double RoomToneDb { get; set; } = -50;

    /// <summary>
    /// Lớp nhạc nền chạy DƯỚI lời thoại (thêm 2026-11-06 sau EP02: các đỉnh cảm xúc chỉ có tiếng động nên kém cảm xúc hơn EP01).
    /// Mỗi lớp lấy một file nhạc (music/), chạy từ đầu hàng <c>from_row</c> đến hết hàng <c>to_row</c>, nhỏ hơn lời
    /// <c>gain_db</c> dB, mờ vào và mờ ra. Trộn vào tiếng cả tập trước bước cân cuối, không đổi dòng thời gian.
    /// </summary>
    [JsonPropertyName("music_beds")]
    public List<LinesheetMusicBed> MusicBeds { get; set; } = new();

    /// <summary>
    /// Logo kênh đặt trên thẻ tên phim (hàng cue = title_sting): tên ảnh trong refs/ (không đuôi), ví dụ
    /// "famixa_logo_nen_toi". Để trống thì thẻ tên phim chỉ có chữ. Thêm 2026-11-05.
    /// </summary>
    [JsonPropertyName("title_logo")]
    public string TitleLogo { get; set; } = "";

    /// <summary>Đoạn kết cuối tập (chỉ khi dựng cả tập): "HẾT TẬP", rồi khung kết chừa chỗ cho màn hình kết thúc YouTube.</summary>
    [JsonPropertyName("outro")]
    public LinesheetOutro Outro { get; set; } = new();

    /// <summary>
    /// Ảnh vẽ trọn cảnh dùng thay cho ghép nền + nhân vật: khóa là mã shot (E01_SH009) hoặc tên cảnh (cột scene),
    /// giá trị là tên file trong keyframes/ (không đuôi). Mã shot được ưu tiên hơn tên cảnh.
    /// </summary>
    [JsonPropertyName("keyframes")]
    public Dictionary<string, string> Keyframes { get; set; } = new();

    /// <summary>Độ dài đoạn chuyển động (5 hoặc 10 giây). 5 giây ít bị trôi hình hơn; shot dài hơn thì chạy chậm hoặc xuôi ngược.</summary>
    [JsonPropertyName("motion_clip_seconds")]
    public int MotionClipSeconds { get; set; } = 5;

    /// <summary>
    /// Khuôn mặt trên ảnh cảnh để ghép môi (thêm 2026-11-04): tên ảnh cảnh → mã nhân vật → [x, y, rộng, cao]
    /// tính theo điểm ảnh của chính ảnh cảnh. Câu của nhân vật có khuôn mặt ở đây mới được ghép môi.
    /// </summary>
    [JsonPropertyName("faces")]
    public Dictionary<string, Dictionary<string, int[]>> Faces { get; set; } = new();

    /// <summary>Mô hình ghép môi: lipsync-2 (rẻ, khoảng 0,05 USD/giây), lipsync-2-pro (khoảng 0,083), sync-3 (khoảng 0,133, bắt mặt nghiêng tốt hơn).</summary>
    [JsonPropertyName("lipsync_model")]
    public string LipsyncModel { get; set; } = "lipsync-2";

    /// <summary>Mô hình chuyển động: gen4_turbo (5 credit/giây) hoặc gen4.5 (12 credit/giây, ổn định hơn).</summary>
    [JsonPropertyName("motion_model")]
    public string MotionModel { get; set; } = "gen4_turbo";

    /// <summary>
    /// Bộ mẫu nhân vật đã duyệt (thêm 2026-11-05): mã nhân vật → tên ảnh trong refs/ (không có đuôi). Ảnh đầu tiên
    /// được gửi làm tham chiếu khi tạo ảnh cảnh có nhân vật đó.
    /// </summary>
    [JsonPropertyName("character_refs")]
    public Dictionary<string, List<string>> CharacterRefs { get; set; } = new();

    /// <summary>Đoạn mô tả cố định của từng nhân vật, tự chèn vào mọi câu lệnh tạo ảnh có nhân vật đó.</summary>
    [JsonPropertyName("character_notes")]
    public Dictionary<string, string> CharacterNotes { get; set; } = new();

    /// <summary>Ảnh cảnh tạo bằng API: tên ảnh cảnh → câu lệnh, nhân vật tham chiếu, khung, mô hình.</summary>
    [JsonPropertyName("keyframe_specs")]
    public Dictionary<string, LinesheetKeyframeSpec> KeyframeSpecs { get; set; } = new();

    /// <summary>
    /// Cảnh cận tay cầm điện thoại (thêm 2026-11-05): mã dòng có tin nhắn → tên ảnh cảnh (keyframes/, màn hình trắng phát sáng).
    /// Phần mềm tự tìm màn hình trên ảnh và dán tin nhắn lên đúng góc nghiêng. Dòng không có ở đây thì vẽ điện thoại kín khung.
    /// </summary>
    [JsonPropertyName("phone_inserts")]
    public Dictionary<string, string> PhoneInserts { get; set; } = new();

    /// <summary>
    /// Dùng chung clip chuyển động (thêm 2026-11-05): mã shot → mã shot nguồn đã có clip. Các câu cùng người nói, cùng ảnh cảnh
    /// dùng lại một clip 5 giây thay vì tạo clip mới cho từng câu (mỗi clip 25 credit). Shot có ở đây không được tạo clip riêng.
    /// Clip nguồn phải cùng ảnh cảnh, cùng câu lệnh và cùng cột nét mặt, nếu không khóa không khớp thì shot dựng ảnh tĩnh.
    /// </summary>
    [JsonPropertyName("motion_share")]
    public Dictionary<string, string> MotionShare { get; set; } = new();

    /// <summary>Câu thêm vào cuối mọi câu lệnh tạo ảnh cảnh (phong cách chung của phim).</summary>
    [JsonPropertyName("image_style")]
    public string ImageStyle { get; set; } = "";
}

public sealed class LinesheetSettingRule
{
    [JsonPropertyName("scene")] public string Scene { get; set; } = "";
    [JsonPropertyName("ids")] public List<string>? Ids { get; set; }
    [JsonPropertyName("speaker")] public string? Speaker { get; set; }
    [JsonPropertyName("cue_contains")] public List<string>? CueContains { get; set; }
    [JsonPropertyName("setting")] public string Setting { get; set; } = "";
}

public sealed class LinesheetVoiceDef
{
    [JsonPropertyName("voice_id")] public string VoiceId { get; set; } = "";
    [JsonPropertyName("label")] public string Label { get; set; } = "";

    /// <summary>Eleven v3 nhận 0 (sáng tạo, nhiều cảm xúc), 0.5 (tự nhiên) hoặc 1 (ổn định).</summary>
    [JsonPropertyName("stability")] public double Stability { get; set; } = 0.5;
    [JsonPropertyName("similarity_boost")] public double SimilarityBoost { get; set; } = 0.75;
    [JsonPropertyName("style")] public double Style { get; set; }
    [JsonPropertyName("speed")] public double Speed { get; set; } = 1.0;

    /// <summary>Chỉnh âm lượng riêng giọng này (dB), cộng thêm sau khi đã cân về mức chung. Âm là nhỏ đi.</summary>
    [JsonPropertyName("gain_db")] public double GainDb { get; set; }
}

public sealed class LinesheetTtsTagRule
{
    [JsonPropertyName("direction_contains")] public List<string> DirectionContains { get; set; } = new();
    [JsonPropertyName("tag")] public string Tag { get; set; } = "";
}

public sealed class LinesheetExpressionRule
{
    [JsonPropertyName("character")] public string Character { get; set; } = "";
    [JsonPropertyName("expression")] public string Expression { get; set; } = "";

    /// <summary>Khớp nếu cột direction của câu thoại chứa một trong các cụm (không phân biệt hoa thường).</summary>
    [JsonPropertyName("direction_contains")] public List<string>? DirectionContains { get; set; }

    /// <summary>Khớp nếu lời thoại (vi) chứa một trong các cụm.</summary>
    [JsonPropertyName("text_contains")] public List<string>? TextContains { get; set; }

    /// <summary>Khớp nếu trong shot có nhạc hoặc hiệu ứng mà cue bắt đầu bằng một trong các cụm (ví dụ "song_").</summary>
    [JsonPropertyName("cue_prefix")] public List<string>? CuePrefix { get; set; }

    /// <summary>Chỉ áp dụng ở các bối cảnh này (ví dụ dáng "laixe" chỉ dùng trong "car"). Trống = mọi bối cảnh.</summary>
    [JsonPropertyName("settings")] public List<string>? Settings { get; set; }

    /// <summary>Quy tắc chỉ có <c>settings</c> (không điều kiện khác) thì luôn khớp trong bối cảnh đó: dùng cho dáng theo bối cảnh.</summary>
    public bool SettingOnly => DirectionContains is not { Count: > 0 } && TextContains is not { Count: > 0 } && CuePrefix is not { Count: > 0 };
}

public sealed class LinesheetSettingDef
{
    [JsonPropertyName("label_vi")] public string LabelVi { get; set; } = "";
    [JsonPropertyName("palette")] public List<string> Palette { get; set; } = new();
    [JsonPropertyName("cast")] public List<string> Cast { get; set; } = new();

    /// <summary>Màu ánh sáng phủ lên nhân vật trong bối cảnh này (ví dụ "#6F86C9" cho bếp đêm). Trống = không phủ.</summary>
    [JsonPropertyName("light")] public string? Light { get; set; }

    /// <summary>Độ phủ 0–1 (0,45 = nhân vật ám 45% màu ánh sáng).</summary>
    [JsonPropertyName("light_strength")] public double LightStrength { get; set; }
}

public sealed class LinesheetCharacterDef
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("color")] public string Color { get; set; } = "#888888";
    [JsonPropertyName("height")] public double Height { get; set; } = 0.62;

    /// <summary>Hướng nhân vật nhìn trong ảnh gốc: "right" hoặc "left". M1 lật ảnh để người đứng hai bên quay vào giữa.</summary>
    [JsonPropertyName("faces")] public string Faces { get; set; } = "right";
}

public sealed record LinesheetValidationError(int Line, string RowId, string Code, string Message);

public sealed class LinesheetValidationException : InvalidOperationException
{
    public LinesheetValidationException(IReadOnlyList<LinesheetValidationError> errors)
        : base(LinesheetErrorCodes.Invalid + ": " + string.Join("; ", errors.Take(5).Select(e => $"{e.RowId}:{e.Code}")))
        => Errors = errors;

    public IReadOnlyList<LinesheetValidationError> Errors { get; }
}

public static class LinesheetErrorCodes
{
    public const string Invalid = "LINESHEET_INVALID";
    public const string MissingColumn = "LINESHEET_MISSING_COLUMN";
    public const string DuplicateId = "LINESHEET_DUPLICATE_ID";
    public const string EmptyId = "LINESHEET_EMPTY_ID";
    public const string UnknownType = "LINESHEET_UNKNOWN_TYPE";
    public const string EmptyLine = "LINESHEET_EMPTY_LINE";
    public const string MissingDuration = "LINESHEET_MISSING_DURATION";
    public const string BadNumber = "LINESHEET_BAD_NUMBER";
    public const string ColumnCount = "LINESHEET_COLUMN_COUNT";
    public const string Empty = "LINESHEET_EMPTY";
    public const string UnknownScene = "LINESHEET_UNKNOWN_SCENE";
    public const string UnknownSetting = "LINESHEET_UNKNOWN_SETTING";
    public const string UnknownCharacter = "LINESHEET_UNKNOWN_CHARACTER";
    public const string ShotGap = "LINESHEET_SHOT_GAP";
    public const string ShotOrder = "LINESHEET_SHOT_ORDER";
    public const string ShotUnknownRow = "LINESHEET_SHOT_UNKNOWN_ROW";
    public const string ShotBadPreset = "LINESHEET_SHOT_BAD_PRESET";
    public const string MissingAudio = "LINESHEET_MISSING_AUDIO";
    public const string NotFound = "LINESHEET_NOT_FOUND";
    public const string NoTimeline = "LINESHEET_NO_TIMELINE";
    public const string NoShots = "LINESHEET_NO_SHOTS";
    public const string NoScript = "LINESHEET_NO_SCRIPT";
    public const string NoSettings = "LINESHEET_NO_SETTINGS";
    public const string MissingLayer = "LINESHEET_MISSING_LAYER";
    public const string FrameMismatch = "LINESHEET_FRAME_MISMATCH";
    public const string BadRange = "LINESHEET_BAD_RANGE";
    public const string BadUpload = "LINESHEET_BAD_UPLOAD";
}

/// <summary>Cách tạo một ảnh cảnh bằng API. Câu lệnh gọi nhân vật bằng @MÃ (ví dụ @ME), tối đa 3 nhân vật.</summary>
public sealed class LinesheetKeyframeSpec
{
    [JsonPropertyName("prompt")] public string Prompt { get; set; } = "";
    [JsonPropertyName("characters")] public List<string> Characters { get; set; } = new();
    [JsonPropertyName("ratio")] public string Ratio { get; set; } = "1920:1080";
    [JsonPropertyName("model")] public string Model { get; set; } = "gen4_image";
    [JsonPropertyName("seed")] public int? Seed { get; set; }

    /// <summary>Chỉ cho gpt_image_2: low, medium hoặc high.</summary>
    [JsonPropertyName("quality")] public string Quality { get; set; } = "high";
}

/// <summary>
/// Đoạn kết (thêm 2026-11-06 sau EP01 bản 11): <see cref="TitleSeconds"/> giây chữ "HẾT TẬP" kèm nhạc kết, rồi
/// <see cref="CardSeconds"/> giây khung kết. Khung kết có logo kênh, câu mời theo dõi, tên tập sau, và hai ô chừa trống
/// (vòng tròn nút Đăng ký, khung "Xem tiếp") để gắn màn hình kết thúc trong YouTube Studio, vốn cần 5–20 giây cuối video.
/// Chỉ thêm khi dựng cả tập, không thêm khi dựng thử một đoạn.
/// </summary>
public sealed class LinesheetOutro
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("end_title")] public string EndTitle { get; set; } = "HẾT TẬP 1";
    [JsonPropertyName("series_line")] public string SeriesLine { get; set; } = "";
    [JsonPropertyName("follow_line")] public string FollowLine { get; set; } = "Theo dõi để không lỡ tập mới";
    [JsonPropertyName("next_label")] public string NextLabel { get; set; } = "TẬP 2";
    [JsonPropertyName("next_title")] public string NextTitle { get; set; } = "";
    [JsonPropertyName("next_note")] public string NextNote { get; set; } = "Sắp ra mắt";
    /// <summary>Ảnh logo trong refs/ (không đuôi); trống thì dùng title_logo.</summary>
    [JsonPropertyName("logo")] public string Logo { get; set; } = "";
    /// <summary>Nhạc dưới chữ "HẾT TẬP" (tên cue nhạc).</summary>
    [JsonPropertyName("sting_cue")] public string StingCue { get; set; } = "end_sting";
    /// <summary>Nhạc nền khung kết (tên cue nhạc) và giây bắt đầu lấy trong bài.</summary>
    [JsonPropertyName("music_cue")] public string MusicCue { get; set; } = "";
    [JsonPropertyName("music_start_sec")] public double MusicStartSec { get; set; }
    [JsonPropertyName("title_seconds")] public double TitleSeconds { get; set; } = 3;
    [JsonPropertyName("card_seconds")] public double CardSeconds { get; set; } = 17;
}

/// <summary>Một lớp nhạc nền dưới lời thoại (settings.music_beds).</summary>
public sealed class LinesheetMusicBed
{
    [JsonPropertyName("cue")] public string Cue { get; set; } = "";
    [JsonPropertyName("from_row")] public string FromRow { get; set; } = "";
    [JsonPropertyName("to_row")] public string ToRow { get; set; } = "";
    /// <summary>Âm lượng so với file gốc (dB), mặc định −16 để nằm dưới lời.</summary>
    [JsonPropertyName("gain_db")] public double GainDb { get; set; } = -16;
    /// <summary>Bắt đầu từ giây thứ mấy trong file nhạc.</summary>
    [JsonPropertyName("offset_sec")] public double OffsetSec { get; set; }
    [JsonPropertyName("fade_in_ms")] public int FadeInMs { get; set; } = 1500;
    [JsonPropertyName("fade_out_ms")] public int FadeOutMs { get; set; } = 2500;
}
