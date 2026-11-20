using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content;

/// <summary>
/// Bình luận đầu sau khi bài đã lên sóng. Không có chữ thì bỏ qua.
/// Facebook hẹn giờ, YouTube riêng tư hoặc có giờ công khai trong tương lai thì vào hàng chờ.
/// </summary>
public static class ContentFirstCommentRules
{
    public const int MaxAttempts = 6;
    public static readonly TimeSpan LiveGrace = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan RetryGap = TimeSpan.FromMinutes(10);

    public const string Pending = "pending";
    public const string PostedStatus = "posted";
    public const string FailedStatus = "failed";

    public const string Posted = "đã bình luận";
    public const string Waiting = "Đang chờ lên sóng";
    public const string PinByHand = "cần ghim tay";
    public const string NeedReconnect = "NEED_RECONNECT: Kết nối lại YouTube để Agent bình luận được";
    public const string NgvDisclaimer =
        "Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn.";

    public const string Youtube = "youtube_channel";
    public const string Facebook = "facebook_page";

    private static readonly Regex CommentMark = new(
        @"\n-{3,}[ \t]*\n[ \t]*Bình luận đầu:[ \t]*\n",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public enum Action { Skip, PostNow, Queue }

    public static bool IsDue(string status, int attempts, DateTimeOffset liveAt, DateTimeOffset? nextAttemptAt, DateTimeOffset now)
    {
        if (!string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)) return false;
        if (attempts >= MaxAttempts) return false;
        if (now < liveAt.Add(LiveGrace)) return false;
        return nextAttemptAt is not { } next || now >= next;
    }

    /// <summary>Một lần chưa đăng được. Hết 6 lần thì failed, không còn lịch thử.</summary>
    public static (string Status, int Attempts, DateTimeOffset? NextAttemptAt, string LastError) AfterMiss(
        int attempts, DateTimeOffset now, string error)
    {
        var next = attempts + 1;
        var reason = string.IsNullOrWhiteSpace(error) ? "Chưa bình luận được." : error.Trim();
        if (next >= MaxAttempts)
            return (FailedStatus, next, null, reason);
        return (Pending, next, now.Add(RetryGap), reason);
    }

    public static string FailedText(string reason) => "lỗi: " + reason.Trim();

    public static Action DecideFacebook(bool scheduled, bool hasText) =>
        !hasText ? Action.Skip : scheduled ? Action.Queue : Action.PostNow;

    public static Action DecideYoutube(string? privacy, DateTimeOffset? publishAt, DateTimeOffset now, bool hasText)
    {
        if (!hasText) return Action.Skip;
        if (string.Equals(privacy, "private", StringComparison.OrdinalIgnoreCase)) return Action.Queue;
        if (publishAt is { } at && at > now.Add(LiveGrace)) return Action.Queue;
        return Action.PostNow;
    }

    public static bool YoutubeIsPublic(string? privacy) =>
        privacy is not null
        && (privacy.Equals("public", StringComparison.OrdinalIgnoreCase)
            || privacy.Equals("unlisted", StringComparison.OrdinalIgnoreCase));

    /// <summary>Scope rỗng nghĩa là Google không trả danh sách — gọi API rồi xét lỗi.</summary>
    public static bool ScopeAllowsComment(string? grantedScope)
    {
        if (string.IsNullOrWhiteSpace(grantedScope)) return true;
        foreach (var part in grantedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.EndsWith("/auth/youtube", StringComparison.OrdinalIgnoreCase)) return true;
            if (part.Contains("youtube.force-ssl", StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static bool IsMissingCommentScope(int statusCode, string? reason)
    {
        if (statusCode is not (401 or 403)) return false;
        return reason is "insufficientPermissions" or "ACCESS_TOKEN_SCOPE_INSUFFICIENT" or "insufficient_scope";
    }

    /// <summary>Phần sau «Bình luận đầu:». Không có thì CTA, và với brand trading thêm câu miễn trừ.</summary>
    public static string? Compose(string? variantBody, string? ctaUrl, string? brandCode)
    {
        var mark = CommentMark.Match((variantBody ?? "").Replace("\r\n", "\n"));
        if (mark.Success)
        {
            var comment = variantBody!.Replace("\r\n", "\n")[(mark.Index + mark.Length)..].Trim();
            return string.IsNullOrWhiteSpace(comment) ? null : comment;
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(ctaUrl)) lines.Add(ctaUrl.Trim());
        if (string.Equals(brandCode, "trading", StringComparison.OrdinalIgnoreCase))
            lines.Add(NgvDisclaimer);
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }

    public static string WriteResult(string? resultJson, string firstComment, string? pinNote)
    {
        JsonObject node;
        try
        {
            node = JsonNode.Parse(string.IsNullOrWhiteSpace(resultJson) ? "{}" : resultJson) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            node = new JsonObject();
        }
        node["firstComment"] = firstComment;
        if (pinNote is not null)
            node["firstCommentPin"] = pinNote;
        return node.ToJsonString();
    }
}
