namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Cấu hình M1, section <c>Content:FamixaLinesheet</c>. Tách khỏi <c>ContentOptions</c> để không đụng lớp chung.</summary>
public sealed class FamixaLinesheetOptions
{
    public const string SectionName = "Content:FamixaLinesheet";

    /// <summary>Bật worker dựng. Để trống thì theo <c>Content:WorkerEnabled</c>.</summary>
    public bool? WorkerEnabled { get; set; }

    public int WorkerPollSeconds { get; set; } = 5;

    /// <summary>Thư mục gốc kho file M1; đường dẫn tương đối tính từ ContentRootPath.</summary>
    public string Root { get; set; } = "App_Data/famixa-linesheet";

    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";

    /// <summary>Giới hạn file zip tải lên (MB).</summary>
    public int MaxUploadMb { get; set; } = 1024;
}
