using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>Cấu hình Telegram từ appsettings / env (<c>Content__Telegram__*</c>). Không chứa giá trị ghi cứng.</summary>
public sealed class ContentTelegramOptions
{
    public const string SectionName = "Content:Telegram";

    /// <summary>Bot token dự phòng (env <c>Content__Telegram__BotToken</c>). Secret: không log, không trả API.</summary>
    public string? BotToken { get; set; }

    /// <summary>Origin trang admin, ví dụ https://admin.example.com. Trống thì tin không có link.</summary>
    public string? AdminBaseUrl { get; set; }

    /// <summary>
    /// null (mặc định) = theo <c>Content:WorkerEnabled</c>. true/false = bật/tắt riêng worker Telegram
    /// (env <c>Content__Telegram__WorkerEnabled</c>), để thử trên máy dev mà không bật worker đăng bài.
    /// </summary>
    public bool? WorkerEnabled { get; set; }

    public static bool IsWorkerEnabled(bool? telegramWorkerEnabled, bool contentWorkerEnabled) =>
        telegramWorkerEnabled ?? contentWorkerEnabled;
}

public sealed record ContentTelegramSettingsDto(
    bool BotTokenConfigured,
    string BotTokenSecretRef,
    Guid BrandId,
    bool Enabled,
    string? ChatId,
    bool NotifySuccess,
    bool NotifyLowStock,
    bool NotifyFailure,
    bool NotifyProposal,
    int LowStockRemaining,
    int LowStockEmptyDays,
    int QuietFromHour,
    int QuietToHour);

public sealed class ContentTelegramSettingsRequest
{
    /// <summary>null = giữ token cũ; "" = xóa; có chữ = thay.</summary>
    public string? BotToken { get; set; }
    public string? BotTokenSecretRef { get; set; }
    public bool Enabled { get; set; }
    public string? ChatId { get; set; }
    public bool NotifySuccess { get; set; } = true;
    public bool NotifyLowStock { get; set; } = true;
    public bool NotifyFailure { get; set; } = true;
    public bool NotifyProposal { get; set; } = true;
    public int LowStockRemaining { get; set; } = 3;
    public int LowStockEmptyDays { get; set; } = 2;
    public int QuietFromHour { get; set; } = 22;
    public int QuietToHour { get; set; } = 7;
}

public sealed record ContentTelegramTestResultDto(bool Ok, string Message);

/// <summary>Cấu hình Telegram cho admin. Lỗi nhập liệu ném <see cref="InvalidOperationException"/> (controller trả 400).</summary>
public interface IContentTelegramSettingsService
{
    Task<ContentTelegramSettingsDto> GetAsync(Guid brandId, CancellationToken ct = default);
    Task<ContentTelegramSettingsDto> SaveAsync(Guid brandId, ContentTelegramSettingsRequest request, CancellationToken ct = default);
    Task<ContentTelegramTestResultDto> TestAsync(Guid brandId, CancellationToken ct = default);
}

/// <summary>Giá trị mặc định và giới hạn, khớp CHECK của migration 443.</summary>
public static class ContentTelegramDefaults
{
    public const string SecretRef = "TELEGRAM_BOT_TOKEN";
    public const int LowStockRemaining = 3;
    public const int LowStockEmptyDays = 2;
    public const int QuietFromHour = 22;
    public const int QuietToHour = 7;
    public const int LowStockScanFromHourVn = 8;
    public const int MaxHtml = 4000;
    public const int MaxAttempts = 4;
    public static readonly TimeSpan FailureClassWindow = TimeSpan.FromHours(6);

    public const string KindSuccess = "success";
    public const string KindFailure = "failure";
    public const string KindLowStock = "low_stock";
    public const string KindProposal = "proposal";

    public const string StatusPending = "Pending";
    public const string StatusSent = "Sent";
    public const string StatusFailed = "Failed";
    public const string StatusSuppressed = "Suppressed";

    private static readonly Regex ChatIdRx = new(@"^-?\d{5,20}$", RegexOptions.CultureInvariant);
    private static readonly Regex TokenRx = new(@"^\d{5,15}:[A-Za-z0-9_-]{20,100}$", RegexOptions.CultureInvariant);
    // Chỉ cho tên biến môi trường dành cho Telegram, để ô "tên biến" không đọc được secret khác của máy chủ.
    private static readonly Regex SecretRefRx = new(@"^TELEGRAM_[A-Z0-9_]{1,90}$", RegexOptions.CultureInvariant);

    public const string ChatIdInvalid =
        "chat_id không hợp lệ. Với nhóm, thêm bot vào nhóm rồi lấy id (thường bắt đầu bằng -100).";
    public const string TokenInvalid = "Token bot không đúng dạng (dạng 123456789:ABC...).";
    public const string SecretRefInvalid = "Tên biến môi trường phải bắt đầu bằng TELEGRAM_ và chỉ gồm chữ in hoa, số, dấu _.";

    public static bool IsValidChatId(string? chatId) => chatId is not null && ChatIdRx.IsMatch(chatId);
    public static bool IsValidToken(string? token) => token is not null && TokenRx.IsMatch(token);
    public static bool IsValidSecretRef(string? name) => name is not null && SecretRefRx.IsMatch(name);

    public static int ClampRemaining(int v) => Math.Clamp(v, 1, 50);
    public static int ClampEmptyDays(int v) => Math.Clamp(v, 1, 30);
    public static int ClampHour(int v) => Math.Clamp(v, 0, 23);
}

/// <summary>Escape và làm sạch chữ trước khi ghép vào HTML Telegram.</summary>
public static class ContentTelegramText
{
    private static readonly Regex SecretLike = new(
        @"(?i)(access_token|refresh_token|bot\d+:|bearer )[^\s<]{0,80}",
        RegexOptions.CultureInvariant);

    public static string Escape(string? s) =>
        string.IsNullOrEmpty(s)
            ? ""
            : s.Replace("&", "&amp;", StringComparison.Ordinal)
               .Replace("<", "&lt;", StringComparison.Ordinal)
               .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>Ẩn đoạn trông giống token. Không escape.</summary>
    public static string Redact(string? s) =>
        string.IsNullOrEmpty(s) ? "" : SecretLike.Replace(s, "[đã ẩn]");

    public static string Cut(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var t = s.Trim();
        return t.Length <= max ? t : t[..max].TrimEnd() + "…";
    }

    /// <summary>Lỗi đăng: ẩn token, cắt 300 ký tự, escape.</summary>
    public static string Error(string? lastError) => Escape(Cut(Redact(lastError), 300));

    /// <summary>Tên / tiêu đề: cắt 200 ký tự, escape.</summary>
    public static string Field(string? s, string fallback = "—")
    {
        var t = Cut(s, 200);
        return Escape(t.Length == 0 ? fallback : t);
    }

    /// <summary>Giữ tin dưới giới hạn Telegram. Cắt ở xuống dòng để không cắt ngang thẻ <c>&lt;b&gt;</c> ở dòng đầu.</summary>
    public static string CapHtml(string html, int max = ContentTelegramDefaults.MaxHtml)
    {
        if (html.Length <= max) return html;
        var cut = html.LastIndexOf('\n', max - 2);
        if (cut <= 0) cut = max - 1;
        return html[..cut] + "\n…";
    }

    public static string? NormalizeBaseUrl(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        var t = baseUrl.Trim().TrimEnd('/');
        return Uri.TryCreate(t, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)
            ? t
            : null;
    }
}

/// <summary>Giờ Việt Nam (UTC+7, không có giờ mùa hè) và giờ im lặng.</summary>
public static class ContentTelegramQuiet
{
    public static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    public static DateTimeOffset ToVn(DateTimeOffset t) => t.ToOffset(VnOffset);

    public static string FormatVn(DateTimeOffset t) =>
        ToVn(t).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string DateKeyVn(DateTimeOffset t) =>
        ToVn(t).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>from = to nghĩa là không có giờ im lặng. from=22,to=7: 22:00–06:59 là im.</summary>
    public static bool IsQuiet(DateTimeOffset utcNow, int fromHour, int toHour)
    {
        if (fromHour == toHour) return false;
        var h = ToVn(utcNow).Hour;
        return fromHour < toHour ? h >= fromHour && h < toHour : h >= fromHour || h < toHour;
    }

    /// <summary><paramref name="utcNow"/> nếu ngoài khung im; ngược lại mốc UTC lúc hết khung (giờ VN, phút 00).</summary>
    public static DateTimeOffset NextSendUtc(DateTimeOffset utcNow, int fromHour, int toHour)
    {
        if (!IsQuiet(utcNow, fromHour, toHour)) return utcNow;
        var vn = ToVn(utcNow);
        var end = new DateTimeOffset(vn.Year, vn.Month, vn.Day, toHour, 0, 0, VnOffset);
        if (end <= vn) end = end.AddDays(1);
        return end.ToUniversalTime();
    }
}

/// <summary>Lớp lỗi và gợi ý (plan mục 1.4, 2.4).</summary>
public static class ContentTelegramHints
{
    public const string Stuck = "stuck";
    public const string Youtube = "yt";
    public const string Facebook = "fb";
    public const string Tiktok = "tt";
    public const string Tenant = "tenant";
    public const string TiktokBusy = "tt_busy";

    private static bool Has(string s, string part) => s.Contains(part, StringComparison.OrdinalIgnoreCase);

    public static (string Class, string Hint) For(string? lastError, Guid brandId = default, string? connector = null)
    {
        var e = lastError ?? "";
        if (Has(e, "Máy tắt hoặc API khởi động lại"))
            return (Stuck, "Kiểm tra kênh xem bài đã lên chưa. Chưa có thì bấm Đăng lại.");
        if (Has(e, "YouTube") && (Has(e, "hết hiệu lực") || Has(e, "hết hạn") || Has(e, "Kết nối lại") || Has(e, "invalid_grant")))
            return (Youtube, "Mở Kênh, bấm Kết nối lại YouTube, rồi Đăng lại.");
        if (Has(e, "Facebook:") && Has(e, "thiếu quyền"))
            return (Facebook, "Kết nối lại Fanpage và chọn đúng Page, rồi Đăng lại.");
        if (Has(e, "Facebook:") && (Has(e, "mất quyền") || Has(e, "Kết nối lại")))
            return (Facebook, "Mở kênh Fanpage, bấm Kết nối lại, rồi Đăng lại.");
        if (Has(e, "TikTok tạm thời chưa trả lời"))
            return (TiktokBusy, "Chưa mất kết nối TikTok. Đợi một lúc rồi bấm Đăng lại.");
        if (Has(e, "TikTok") && (Has(e, "hết hiệu lực") || Has(e, "hết hạn") || Has(e, "Kết nối lại")))
            return (Tiktok, "Mở kênh TikTok, bấm Kết nối lại, rồi Đăng lại.");
        if (Has(e, "Tenant id claim missing"))
            return (Tenant, "Đăng nhập lại admin tenant KIT_MKT rồi Đăng lại.");
        if (Has(e, "PRODUCTION_TENANT_REQUIRED"))
            return (Tenant, "Việc production thiếu tenant, không phải mất kênh. Xem log API, không bấm Kết nối lại kênh.");

        var head = e.Length > 120 ? e[..120] : e;
        var sha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(head)))[..8].ToLowerInvariant();
        var conn = Cut(connector, 24);
        return ($"other:{brandId:N}:{conn}:{sha}", "Mở Lịch đăng, xem lỗi, rồi bấm Đăng lại.");
    }

    private static string Cut(string? s, int n) => string.IsNullOrEmpty(s) ? "" : s.Length <= n ? s : s[..n];
}

public enum ContentTelegramDedupeDecision
{
    Send,
    Suppress,
}

public static class ContentTelegramDedupe
{
    public static ContentTelegramDedupeDecision Decide(bool classSentRecently) =>
        classSentRecently ? ContentTelegramDedupeDecision.Suppress : ContentTelegramDedupeDecision.Send;

    public static string Success(Guid jobId) => $"ok:{jobId:N}";
    public static string Failure(Guid jobId) => $"fail:{jobId:N}";
    public static string Draft(Guid topicId) => $"draft:{topicId:N}";
    public static string Proposal(Guid proposalId) => $"proposal:{proposalId:N}";

    public static string LowStock(Guid brandId, Guid targetId, DateTimeOffset utcNow) =>
        $"low:{brandId:N}:{targetId:N}:{ContentTelegramQuiet.DateKeyVn(utcNow)}";
}

/// <summary>Quyết định "gần hết tin" (chủ kênh chốt: còn &lt; 3 bài đã xếp lịch HOẶC trống lịch trong 2 ngày tới).</summary>
public static class ContentTelegramLowStock
{
    public static bool ShouldAlert(int remaining, DateTimeOffset? nextAt, DateTimeOffset utcNow, int threshold, int emptyDays) =>
        remaining < threshold || nextAt is null || nextAt.Value > utcNow.AddDays(emptyDays);

    public static bool IsScanTime(DateTimeOffset utcNow) =>
        ContentTelegramQuiet.ToVn(utcNow).Hour >= ContentTelegramDefaults.LowStockScanFromHourVn;
}

public enum ContentTelegramSendOutcome
{
    Sent,
    Retry,
    Failed,
}

/// <summary>Kết quả một lần gọi Bot API (không chứa token, không chứa URL).</summary>
public sealed record ContentTelegramSendResult(bool Ok, int? StatusCode, int? RetryAfterSeconds, string? Description)
{
    public static ContentTelegramSendResult Success() => new(true, 200, null, null);
    public static ContentTelegramSendResult Network(string what) => new(false, null, null, what);
}

/// <summary>Chính sách gửi lại (plan mục 2.6). <paramref name="attempts"/> là số lần đã thử, gồm lần vừa rồi.</summary>
public static class ContentTelegramRetry
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(10)];

    public static (ContentTelegramSendOutcome Outcome, DateTimeOffset? NextAt, bool RefundAttempt) Decide(
        ContentTelegramSendResult r, int attempts, DateTimeOffset utcNow)
    {
        if (r.Ok) return (ContentTelegramSendOutcome.Sent, null, false);

        if (r.StatusCode == 429)
        {
            var wait = Math.Clamp(r.RetryAfterSeconds ?? 30, 1, 3600);
            return (ContentTelegramSendOutcome.Retry, utcNow.AddSeconds(wait), true);
        }

        var transient = r.StatusCode is null || r.StatusCode >= 500;
        if (!transient || attempts >= ContentTelegramDefaults.MaxAttempts)
            return (ContentTelegramSendOutcome.Failed, null, false);

        var idx = Math.Clamp(attempts - 1, 0, Backoff.Length - 1);
        return (ContentTelegramSendOutcome.Retry, utcNow + Backoff[idx], false);
    }

    /// <summary>Câu tiếng Việt cho nút Gửi tin thử.</summary>
    public static string TestMessage(ContentTelegramSendResult r)
    {
        if (r.Ok) return "Đã gửi.";
        if (r.StatusCode == 429) return "Telegram đang giới hạn tốc độ. Thử lại sau ít giây.";
        if (r.StatusCode is 400 or 403 or 404) return "Sai chat_id hoặc bot chưa ở trong nhóm.";
        if (r.StatusCode == 401) return "Token bot không dùng được.";
        return "Không gọi được Telegram. Thử lại sau.";
    }
}

/// <summary>Đọc <c>publish_job.result_json</c> để lấy link và trạng thái hẹn giờ.</summary>
public sealed record ContentTelegramPublishResult(string? Url, bool Scheduled, string? Tiktok)
{
    public bool TiktokInbox => string.Equals(Tiktok, "inbox_draft", StringComparison.OrdinalIgnoreCase);

    public static ContentTelegramPublishResult Parse(string? resultJson, string? connector)
    {
        string? url = null, tiktok = null, postId = null, feedId = null;
        var scheduled = false;
        if (!string.IsNullOrWhiteSpace(resultJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(resultJson);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    url = Str(root, "url") ?? Str(root, "link");
                    tiktok = Str(root, "tiktok");
                    postId = Str(root, "postId");
                    feedId = Str(root, "feedId");
                    scheduled = root.TryGetProperty("scheduled", out var s) && s.ValueKind == JsonValueKind.True;
                }
            }
            catch (JsonException)
            {
            }
        }

        if (url is null && string.Equals(connector, "facebook_page", StringComparison.OrdinalIgnoreCase))
        {
            var id = postId ?? feedId;
            if (!string.IsNullOrWhiteSpace(id) && id.All(c => char.IsAsciiDigit(c) || c == '_'))
                url = "https://www.facebook.com/" + id;
        }

        // TikTok: không có URL công khai, không bịa link.
        if (string.Equals(connector, "tiktok_channel", StringComparison.OrdinalIgnoreCase))
            url = null;

        if (url is not null && !(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                                 || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            url = null;

        return new ContentTelegramPublishResult(url, scheduled, tiktok);
    }

    private static string? Str(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim()
            : null;
}

/// <summary>Mẫu tin tiếng Việt, <c>parse_mode=HTML</c>. Dữ liệu người dùng luôn được escape.</summary>
public static class ContentTelegramTemplates
{
    public static string ConnectorLabel(string? connector) => connector switch
    {
        "youtube_channel" => "YouTube",
        "facebook_page" => "Fanpage",
        "tiktok_channel" => "TikTok",
        "wordpress_rest" => "Website",
        "astro_git" => "Website",
        "local_os" => "Thái Nguyên Life",
        _ => connector ?? "",
    };

    private static string Channel(string? channel, string? connector)
    {
        var label = ConnectorLabel(connector);
        var name = ContentTelegramText.Field(channel);
        return label.Length == 0 ? name : $"{name} ({ContentTelegramText.Escape(label)})";
    }

    public static string Published(
        string? brand, string? channel, string? connector, string? title,
        ContentTelegramPublishResult result, DateTimeOffset at, DateTimeOffset? publishAt)
    {
        if (result.TiktokInbox)
            return TiktokInbox(brand, channel, title, at);

        var link = result.Url is null ? null : ContentTelegramText.Escape(result.Url);
        var sb = new StringBuilder();
        if (result.Scheduled)
        {
            sb.Append("<b>Đã hẹn giờ</b>\n");
            sb.Append("Thương hiệu: ").Append(ContentTelegramText.Field(brand)).Append('\n');
            sb.Append("Kênh: ").Append(Channel(channel, connector)).Append('\n');
            sb.Append("Bài: ").Append(ContentTelegramText.Field(title)).Append('\n');
            sb.Append("Sẽ hiện lúc: ").Append(publishAt is { } p ? ContentTelegramQuiet.FormatVn(p) : "theo lịch đã đặt").Append('\n');
            sb.Append("Link: ").Append(link ?? "kênh sẽ hiện khi đến giờ");
        }
        else
        {
            sb.Append("<b>Đã đăng</b>\n");
            sb.Append("Thương hiệu: ").Append(ContentTelegramText.Field(brand)).Append('\n');
            sb.Append("Kênh: ").Append(Channel(channel, connector)).Append('\n');
            sb.Append("Bài: ").Append(ContentTelegramText.Field(title)).Append('\n');
            sb.Append("Link: ").Append(link ?? "không có link công khai").Append('\n');
            sb.Append("Lúc: ").Append(ContentTelegramQuiet.FormatVn(at));
        }
        return sb.ToString();
    }

    public static string TiktokInbox(string? brand, string? channel, string? title, DateTimeOffset at) =>
        "<b>Đã vào hộp nháp TikTok</b>\n"
        + "Thương hiệu: " + ContentTelegramText.Field(brand) + "\n"
        + "Kênh: " + ContentTelegramText.Field(channel) + "\n"
        + "Bài: " + ContentTelegramText.Field(title) + "\n"
        + "Mở TikTok, dán chú thích rồi đăng.\n"
        + "Lúc: " + ContentTelegramQuiet.FormatVn(at);

    public static string Failure(
        string? brand, string? channel, string? connector, string? title,
        string? lastError, string hint, DateTimeOffset at) =>
        "<b>Đăng lỗi</b>\n"
        + "Thương hiệu: " + ContentTelegramText.Field(brand) + "\n"
        + "Kênh: " + Channel(channel, connector) + "\n"
        + "Bài: " + ContentTelegramText.Field(title) + "\n"
        + "Lỗi: " + ContentTelegramText.Error(string.IsNullOrWhiteSpace(lastError) ? "không có mô tả" : lastError) + "\n"
        + "Gợi ý: " + ContentTelegramText.Escape(hint) + "\n"
        + "Lúc: " + ContentTelegramQuiet.FormatVn(at);

    public static string LowStock(
        string? brand, string? channel, string? connector, int remaining, DateTimeOffset? nextAt, int emptyDays) =>
        "<b>Sắp hết bài xếp lịch</b>\n"
        + "Thương hiệu: " + ContentTelegramText.Field(brand) + "\n"
        + "Kênh: " + Channel(channel, connector) + "\n"
        + $"Còn {remaining} bài đã xếp giờ.\n"
        + "Bài kế: " + (nextAt is { } n ? ContentTelegramQuiet.FormatVn(n) : $"không còn trong {emptyDays} ngày tới");

    public static string Draft(string? brand, string? title, Guid topicId, string? adminBase)
    {
        var b = ContentTelegramText.NormalizeBaseUrl(adminBase);
        return "<b>Agent gửi bản nháp cần duyệt</b>\n"
               + "Thương hiệu: " + ContentTelegramText.Field(brand) + "\n"
               + "Bài: " + ContentTelegramText.Field(title) + "\n"
               + (b is null
                   ? "Mở admin → Bài viết."
                   : "Mở admin: " + ContentTelegramText.Escape($"{b}/content/topics?topic={topicId}"));
    }

    public static string Proposal(
        string? brand, string? title, string? targetLabel, DateTimeOffset publishAt, string? adminBase)
    {
        var b = ContentTelegramText.NormalizeBaseUrl(adminBase);
        return "<b>Agent đề xuất lịch cần duyệt</b>\n"
               + "Thương hiệu: " + ContentTelegramText.Field(brand) + "\n"
               + "Bài: " + ContentTelegramText.Field(title) + "\n"
               + "Kênh: " + ContentTelegramText.Field(targetLabel) + "\n"
               + "Giờ đề xuất: " + ContentTelegramQuiet.FormatVn(publishAt) + "\n"
               + (b is null
                   ? "Mở admin → Lịch đăng → tab Agent đăng bài."
                   : "Mở admin: " + ContentTelegramText.Escape($"{b}/content/calendar?tab=duyet"));
    }

    /// <summary>
    /// Gom các tin (không phải lỗi) rơi vào giờ im lặng thành một tin lúc hết giờ im.
    /// Mỗi mục đã là HTML hợp lệ; chỉ ghép nguyên mục, không cắt ngang.
    /// </summary>
    public static string Digest(IReadOnlyList<string> items, int max = ContentTelegramDefaults.MaxHtml)
    {
        var head = $"<b>Tóm tắt giờ im lặng</b> ({items.Count} tin)";
        var sb = new StringBuilder(head);
        var used = 0;
        foreach (var item in items)
        {
            var piece = "\n\n" + item;
            var tailIfStop = $"\n\n… và {items.Count - used} tin nữa. Mở admin để xem.";
            if (sb.Length + piece.Length + tailIfStop.Length > max && used > 0)
            {
                sb.Append(tailIfStop);
                return sb.ToString();
            }
            sb.Append(piece);
            used++;
        }
        return ContentTelegramText.CapHtml(sb.ToString(), max);
    }

    public static string TestPlain(string? brandName) =>
        $"KitPlatform: tin thử cho {ContentTelegramText.Cut(string.IsNullOrWhiteSpace(brandName) ? "thương hiệu" : brandName, 200)}.";
}
