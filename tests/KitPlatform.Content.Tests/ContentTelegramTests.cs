using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentTelegramTests
{
    static DateTimeOffset Vn(int y, int mo, int d, int h, int mi) =>
        new DateTimeOffset(y, mo, d, h, mi, 0, TimeSpan.FromHours(7)).ToUniversalTime();

    static readonly DateTimeOffset At = Vn(2026, 11, 12, 15, 0);

    [Fact]
    public void ContentTelegram_escapes_title_and_brand()
    {
        var html = ContentTelegramTemplates.Published(
            "A & B <brand>", "Kênh <1>", "youtube_channel", "Tập 1 <script>&",
            ContentTelegramPublishResult.Parse("""{"url":"https://youtu.be/x?a=1&b=2"}""", "youtube_channel"),
            At, null);
        Assert.Contains("Thương hiệu: A &amp; B &lt;brand&gt;", html);
        Assert.Contains("Bài: Tập 1 &lt;script&gt;&amp;", html);
        Assert.Contains("Kênh: Kênh &lt;1&gt; (YouTube)", html);
        Assert.Contains("https://youtu.be/x?a=1&amp;b=2", html);
        Assert.DoesNotContain("<script>", html);
        Assert.StartsWith("<b>Đã đăng</b>", html);
        Assert.Contains("Lúc: 12/11/2026 15:00", html);
    }

    [Fact]
    public void ContentTelegram_error_hides_tokens_and_is_cut()
    {
        var err = "Graph lỗi access_token=EAAB123secretvalue&x refresh_token=1//abc bot123456:AAHsecret Bearer ya29.zzz <b>";
        var html = ContentTelegramTemplates.Failure("B", "C", "facebook_page", "T", err, "h", At);
        Assert.DoesNotContain("EAAB123secretvalue", html);
        Assert.DoesNotContain("1//abc", html);
        Assert.DoesNotContain("AAHsecret", html);
        Assert.DoesNotContain("ya29.zzz", html);
        Assert.Contains("[đã ẩn]", html);
        Assert.Contains("&lt;b&gt;", html);

        var longErr = new string('x', 1000);
        var cut = ContentTelegramText.Error(longErr);
        Assert.True(cut.Length <= 301);
    }

    [Theory]
    [InlineData("Kết nối YouTube hết hiệu lực — Kết nối lại.", ContentTelegramHints.Youtube)]
    [InlineData("Phiên YouTube hết hạn — bấm Kết nối lại.", ContentTelegramHints.Youtube)]
    [InlineData("Facebook: mất quyền đăng. Bấm Kết nối lại trên hàng này, rồi Đăng lại + ảnh.", ContentTelegramHints.Facebook)]
    [InlineData("Facebook: thiếu quyền pages_manage_posts. Kết nối lại.", ContentTelegramHints.Facebook)]
    [InlineData("Kết nối TikTok hết hiệu lực — Kết nối lại.", ContentTelegramHints.Tiktok)]
    [InlineData("Phiên TikTok hết hạn — bấm Kết nối lại.", ContentTelegramHints.Tiktok)]
    [InlineData("TikTok tạm thời chưa trả lời, chưa mất kết nối.", ContentTelegramHints.TiktokBusy)]
    [InlineData("Máy tắt hoặc API khởi động lại khi đang đăng. Kiểm tra kênh xem bài đã lên chưa; chưa lên thì bấm Đăng lại.", ContentTelegramHints.Stuck)]
    [InlineData("Tenant id claim missing.", ContentTelegramHints.Tenant)]
    [InlineData("PRODUCTION_TENANT_REQUIRED", ContentTelegramHints.Tenant)]
    public void ContentTelegram_hint_class_matches_table(string error, string expected)
    {
        var (cls, hint) = ContentTelegramHints.For(error, Guid.NewGuid(), "x");
        Assert.Equal(expected, cls);
        Assert.False(string.IsNullOrWhiteSpace(hint));
    }

    [Fact]
    public void ContentTelegram_hint_specific_texts()
    {
        Assert.Contains("Kết nối lại YouTube", ContentTelegramHints.For("Kết nối YouTube hết hiệu lực — Kết nối lại.").Hint);
        Assert.Contains("chọn đúng Page", ContentTelegramHints.For("Facebook: thiếu quyền pages_manage_posts").Hint);
        Assert.Contains("không bấm Kết nối lại", ContentTelegramHints.For("PRODUCTION_TENANT_REQUIRED").Hint);
        Assert.Contains("Đăng nhập lại", ContentTelegramHints.For("Tenant id claim missing.").Hint);
    }

    [Fact]
    public void ContentTelegram_other_class_is_stable_per_brand_and_text()
    {
        var brand = Guid.NewGuid();
        var a = ContentTelegramHints.For("WordPress 500 Internal", brand, "wordpress_rest").Class;
        var b = ContentTelegramHints.For("WordPress 500 Internal", brand, "wordpress_rest").Class;
        var c = ContentTelegramHints.For("WordPress 500 Internal", Guid.NewGuid(), "wordpress_rest").Class;
        Assert.StartsWith("other:", a);
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a.Length <= 80);
    }

    [Fact]
    public void ContentTelegram_same_class_in_window_is_suppressed()
    {
        Assert.Equal(ContentTelegramDedupeDecision.Suppress, ContentTelegramDedupe.Decide(classSentRecently: true));
        Assert.Equal(ContentTelegramDedupeDecision.Send, ContentTelegramDedupe.Decide(classSentRecently: false));
    }

    [Fact]
    public void ContentTelegram_quiet_22_to_7()
    {
        var afternoon = Vn(2026, 11, 12, 15, 0);
        Assert.False(ContentTelegramQuiet.IsQuiet(afternoon, 22, 7));
        Assert.Equal(afternoon, ContentTelegramQuiet.NextSendUtc(afternoon, 22, 7));

        var late = Vn(2026, 11, 12, 23, 30);
        Assert.True(ContentTelegramQuiet.IsQuiet(late, 22, 7));
        Assert.Equal(Vn(2026, 11, 13, 7, 0), ContentTelegramQuiet.NextSendUtc(late, 22, 7));

        var early = Vn(2026, 11, 13, 6, 59);
        Assert.Equal(Vn(2026, 11, 13, 7, 0), ContentTelegramQuiet.NextSendUtc(early, 22, 7));

        Assert.False(ContentTelegramQuiet.IsQuiet(Vn(2026, 11, 13, 7, 0), 22, 7));
        Assert.True(ContentTelegramQuiet.IsQuiet(Vn(2026, 11, 12, 22, 0), 22, 7));
        Assert.False(ContentTelegramQuiet.IsQuiet(late, 7, 7));
        Assert.True(ContentTelegramQuiet.IsQuiet(Vn(2026, 11, 12, 13, 0), 12, 14));
    }

    [Fact]
    public void ContentTelegram_tiktok_inbox_has_no_link()
    {
        var r = ContentTelegramPublishResult.Parse(
            """{"tiktok":"inbox_draft","note":"Đã vào hộp nháp","url":"https://made.up/x"}""", "tiktok_channel");
        Assert.True(r.TiktokInbox);
        Assert.Null(r.Url);
        var html = ContentTelegramTemplates.Published("B", "TT", "tiktok_channel", "T", r, At, null);
        Assert.StartsWith("<b>Đã vào hộp nháp TikTok</b>", html);
        Assert.DoesNotContain("http", html);

        var direct = ContentTelegramPublishResult.Parse("""{"tiktok":"direct_post","publishId":"p1"}""", "tiktok_channel");
        Assert.Contains("không có link công khai", ContentTelegramTemplates.Published("B", "TT", "tiktok_channel", "T", direct, At, null));
    }

    [Fact]
    public void ContentTelegram_scheduled_says_hen_gio()
    {
        var r = ContentTelegramPublishResult.Parse("""{"scheduled":true,"url":"https://youtu.be/abc"}""", "youtube_channel");
        var html = ContentTelegramTemplates.Published("B", "C", "youtube_channel", "T", r, At, Vn(2026, 11, 14, 19, 30));
        Assert.StartsWith("<b>Đã hẹn giờ</b>", html);
        Assert.Contains("Sẽ hiện lúc: 14/11/2026 19:30", html);
        Assert.DoesNotContain("Đã đăng", html);
    }

    [Fact]
    public void ContentTelegram_facebook_link_fallback()
    {
        Assert.Equal("https://www.facebook.com/123_456",
            ContentTelegramPublishResult.Parse("""{"postId":"123_456"}""", "facebook_page").Url);
        Assert.Equal("https://www.facebook.com/9_8",
            ContentTelegramPublishResult.Parse("""{"feedId":"9_8"}""", "facebook_page").Url);
        Assert.Equal("https://site/x", ContentTelegramPublishResult.Parse("""{"link":"https://site/x"}""", "wordpress_rest").Url);
        Assert.Null(ContentTelegramPublishResult.Parse("""{"path":"src/x.md"}""", "astro_git").Url);
        Assert.Null(ContentTelegramPublishResult.Parse("not json", "youtube_channel").Url);
    }

    [Fact]
    public void ContentTelegram_admin_link_from_config_or_text()
    {
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var withBase = ContentTelegramTemplates.Draft("B", "T", id, "https://famixa.example.io/");
        Assert.Contains("Mở admin: https://famixa.example.io/content/topics?topic=11111111-2222-3333-4444-555555555555", withBase);
        Assert.Contains("Mở admin → Bài viết.", ContentTelegramTemplates.Draft("B", "T", id, null));

        var p = ContentTelegramTemplates.Proposal("B", "T", "Fanpage", At, "https://famixa.example.io");
        Assert.Contains("https://famixa.example.io/content/calendar?tab=duyet", p);
        Assert.Contains("tab Agent đăng bài", ContentTelegramTemplates.Proposal("B", "T", "F", At, "  "));
        Assert.Contains("tab Agent đăng bài", ContentTelegramTemplates.Proposal("B", "T", "F", At, "javascript:alert(1)"));
    }

    [Fact]
    public void ContentTelegram_low_stock_rule()
    {
        var now = At;
        Assert.True(ContentTelegramLowStock.ShouldAlert(2, now.AddHours(5), now, 3, 2));
        Assert.True(ContentTelegramLowStock.ShouldAlert(5, null, now, 3, 2));
        Assert.True(ContentTelegramLowStock.ShouldAlert(5, now.AddDays(3), now, 3, 2));
        Assert.False(ContentTelegramLowStock.ShouldAlert(3, now.AddHours(20), now, 3, 2));
        Assert.False(ContentTelegramLowStock.IsScanTime(Vn(2026, 11, 12, 7, 59)));
        Assert.True(ContentTelegramLowStock.IsScanTime(Vn(2026, 11, 12, 8, 0)));
        Assert.Equal(
            ContentTelegramDedupe.LowStock(Guid.Empty, Guid.Empty, Vn(2026, 11, 12, 8, 0)),
            ContentTelegramDedupe.LowStock(Guid.Empty, Guid.Empty, Vn(2026, 11, 12, 23, 59)));
    }

    [Fact]
    public void ContentTelegram_retry_policy()
    {
        var now = At;
        Assert.Equal(ContentTelegramSendOutcome.Sent, ContentTelegramRetry.Decide(ContentTelegramSendResult.Success(), 1, now).Outcome);

        var limited = ContentTelegramRetry.Decide(new ContentTelegramSendResult(false, 429, 17, "Too Many"), 2, now);
        Assert.Equal(ContentTelegramSendOutcome.Retry, limited.Outcome);
        Assert.Equal(now.AddSeconds(17), limited.NextAt);
        Assert.True(limited.RefundAttempt);

        Assert.Equal(now.AddSeconds(30), ContentTelegramRetry.Decide(ContentTelegramSendResult.Network("x"), 1, now).NextAt);
        Assert.Equal(now.AddMinutes(2), ContentTelegramRetry.Decide(new(false, 502, null, null), 2, now).NextAt);
        Assert.Equal(now.AddMinutes(10), ContentTelegramRetry.Decide(new(false, 500, null, null), 3, now).NextAt);
        Assert.Equal(ContentTelegramSendOutcome.Failed, ContentTelegramRetry.Decide(new(false, 500, null, null), 4, now).Outcome);

        foreach (var code in new[] { 400, 401, 403, 404 })
            Assert.Equal(ContentTelegramSendOutcome.Failed, ContentTelegramRetry.Decide(new(false, code, null, "x"), 1, now).Outcome);

        Assert.Equal("Sai chat_id hoặc bot chưa ở trong nhóm.", ContentTelegramRetry.TestMessage(new(false, 400, null, "chat not found")));
    }

    [Fact]
    public void ContentTelegram_digest_groups_items_and_stays_under_limit()
    {
        var items = Enumerable.Range(1, 3).Select(i => $"<b>Đã đăng</b>\nBài: {i}").ToList();
        var d = ContentTelegramTemplates.Digest(items);
        Assert.StartsWith("<b>Tóm tắt giờ im lặng</b> (3 tin)", d);
        Assert.Contains("Bài: 1", d);
        Assert.Contains("Bài: 3", d);

        var many = Enumerable.Range(1, 200).Select(i => "<b>Đã đăng</b>\n" + new string('x', 100) + i).ToList();
        var big = ContentTelegramTemplates.Digest(many);
        Assert.True(big.Length <= ContentTelegramDefaults.MaxHtml);
        Assert.Contains("tin nữa", big);
        Assert.Equal(CountOf(big, "<b>"), CountOf(big, "</b>"));
    }

    [Fact]
    public void ContentTelegram_validation()
    {
        Assert.True(ContentTelegramDefaults.IsValidChatId("-1001234567890"));
        Assert.True(ContentTelegramDefaults.IsValidChatId("123456789"));
        Assert.False(ContentTelegramDefaults.IsValidChatId("@mychannel"));
        Assert.False(ContentTelegramDefaults.IsValidChatId("12"));
        Assert.True(ContentTelegramDefaults.IsValidToken("123456789:AAHdqTcvCH1vGWJxfSeofSAs0K5PALDsaw"));
        Assert.False(ContentTelegramDefaults.IsValidToken("123/../x"));
        Assert.True(ContentTelegramDefaults.IsValidSecretRef("TELEGRAM_BOT_TOKEN"));
        Assert.False(ContentTelegramDefaults.IsValidSecretRef("ConnectionStrings:Default"));
        Assert.False(ContentTelegramDefaults.IsValidSecretRef("YOUTUBE_CLIENT_SECRET"));
    }

    [Fact]
    public void ContentTelegram_worker_flag_overrides_content_flag()
    {
        Assert.True(ContentTelegramOptions.IsWorkerEnabled(null, true));
        Assert.False(ContentTelegramOptions.IsWorkerEnabled(null, false));
        Assert.True(ContentTelegramOptions.IsWorkerEnabled(true, false));
        Assert.False(ContentTelegramOptions.IsWorkerEnabled(false, true));
    }

    static int CountOf(string s, string part)
    {
        var n = 0;
        for (var i = s.IndexOf(part, StringComparison.Ordinal); i >= 0; i = s.IndexOf(part, i + part.Length, StringComparison.Ordinal)) n++;
        return n;
    }
}
