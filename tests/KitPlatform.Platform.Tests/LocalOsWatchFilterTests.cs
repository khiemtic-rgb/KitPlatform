using KitPlatform.Packs.LocalOs;
using Xunit;

namespace KitPlatform.Platform.Tests;

public class LocalOsWatchFilterTests
{
    [Fact]
    public void Allows_su_kien_title_on_event_source()
    {
        Assert.Equal(
            LocalOsWatchDecision.Allow,
            LocalOsWatchFilter.Decide("Sự kiện chào tân sinh viên 2026", "/su-kien/x", "event"));
    }

    [Fact]
    public void Allows_festival_on_event_source()
    {
        Assert.Equal(
            LocalOsWatchDecision.Allow,
            LocalOsWatchFilter.Decide("Festival Trà Quốc tế Thái Nguyên 2026", "/ke-hoach-312", "event"));
    }

    [Fact]
    public void Denies_politics()
    {
        Assert.Equal(
            LocalOsWatchDecision.DenyPolitics,
            LocalOsWatchFilter.Decide("Kỳ họp HĐND tỉnh cho ý kiến chỉ đạo", "/tin", "event"));
    }

    [Fact]
    public void Denies_past_year_without_2026()
    {
        Assert.Equal(
            LocalOsWatchDecision.DenyPast,
            LocalOsWatchFilter.Decide("Lễ hội đền Đuổm 2023 đã kết thúc", "/le-hoi-2023", "event"));
    }

    [Fact]
    public void Allows_school_year_2025_2026()
    {
        Assert.Equal(
            LocalOsWatchDecision.Allow,
            LocalOsWatchFilter.Decide("Tuyển dụng thực tập sinh năm học 2025-2026", "/tuyen-dung/x", "job"));
    }

    [Fact]
    public void Allows_job_on_job_source()
    {
        Assert.Equal(
            LocalOsWatchDecision.Allow,
            LocalOsWatchFilter.Decide("Tuyển dụng thực tập sinh ICTU", "/tuyen-dung/x", "job"));
    }

    [Fact]
    public void Denies_admin_news_on_event_source()
    {
        Assert.Equal(
            LocalOsWatchDecision.DenyNoise,
            LocalOsWatchFilter.Decide("UBND làm việc với đoàn công tác", "/tin-tuc/1", "event"));
    }

    [Fact]
    public void Allows_thai_nguyen_football_friendly()
    {
        Assert.Equal(
            LocalOsWatchDecision.Allow,
            LocalOsWatchFilter.Decide(
                "FC Thái Nguyên gặp CA Hà Nội trên sân nhà",
                "/the-thao/202608/giao-huu",
                "event"));
    }

    [Fact]
    public void Denies_event_index_page()
    {
        Assert.Equal(
            LocalOsWatchDecision.DenyNoise,
            LocalOsWatchFilter.Decide("TIN TỨC SỰ KIỆN - Sở Văn hóa, Thể thao và Du lịch", "/tin-tuc-su-kien", "event"));
    }

    [Fact]
    public void Denies_event_whose_date_already_passed()
    {
        Assert.Equal(
            LocalOsWatchDecision.DenyPast,
            LocalOsWatchFilter.Decide(
                "Lễ hội văn hóa truyền thống Chợ tình Xuân Dương năm 2026 diễn ra 10-11/5/2026",
                "/su-kien/cho-tinh",
                "event"));
    }

    [Fact]
    public void Denies_already_happened_phrase_even_with_2026()
    {
        Assert.Equal(
            LocalOsWatchDecision.DenyPast,
            LocalOsWatchFilter.Decide("Festival Trà 2026 đã kết thúc", "/festival", "event"));
    }

    [Fact]
    public void Allows_city_news_without_su_kien_keyword()
    {
        Assert.Equal(
            LocalOsWatchDecision.Allow,
            LocalOsWatchFilter.Decide(
                "Các trường thuộc Đại học Thái Nguyên tổ chức đón tân sinh viên",
                "/giao-duc/202608/don-tan-sinh-vien",
                "event"));
    }
}

public class LocalOsEventDateTests
{
    private static readonly DateOnly Today = new(2026, 8, 22);

    [Fact]
    public void Reads_range_10_11_may_2026_as_past()
    {
        Assert.True(LocalOsEventDate.IsPastInText(
            "Lễ hội Chợ tình Xuân Dương năm 2026 diễn ra trong hai ngày 10-11/5/2026",
            Today));
    }

    [Fact]
    public void Keeps_25_aug_2026_upcoming()
    {
        Assert.False(LocalOsEventDate.IsPastInText(
            "Giao hữu FC Thái Nguyên – Hà Nội FC 25/8/2026",
            Today));
    }

    [Fact]
    public void No_date_is_not_past()
    {
        Assert.False(LocalOsEventDate.IsPastInText(
            "Các trường thuộc Đại học Thái Nguyên tổ chức đón tân sinh viên",
            Today));
    }
}

public class LocalOsTextExtractSummaryTests
{
    [Fact]
    public void GuessSummary_strips_repeated_title_and_keeps_sentences()
    {
        var title = "Lễ hội văn hóa truyền thống \"Chợ tình Xuân Dương\" năm 2026 | Lễ Hội Việt Nam";
        var blob = title + "\n\nLễ hội diễn ra trong hai ngày 10-11/5 tại xã Xuân Dương, Thái Nguyên. Khách có thể xem hát then và chợ phiên. Ban tổ chức không bán vé trên Thái Nguyên Life.";
        var lead = LocalOsTextExtract.GuessSummary(title, blob, 900);
        Assert.DoesNotContain("Lễ Hội Việt Nam Lễ hội", lead, StringComparison.Ordinal);
        Assert.Contains("10-11/5", lead, StringComparison.Ordinal);
        Assert.Contains("hát then", lead, StringComparison.Ordinal);
    }

    [Fact]
    public void StripHtml_reads_article_paragraphs_not_only_meta()
    {
        const string html = """
            <html><head>
            <title>Chợ tình Xuân Dương 2026</title>
            <meta property="og:description" content="Lễ hội diễn ra trong hai ngày 10-11/5 tại xã Xuân Dương...">
            </head><body>
            <nav><p>Trang chủ</p></nav>
            <article>
            <p>Lễ hội văn hóa truyền thống Chợ tình Xuân Dương năm 2026 diễn ra trong hai ngày 10-11/5 tại xã Xuân Dương, Thái Nguyên.</p>
            <p>Chương trình có hát then, chợ phiên và các trò chơi dân gian do người dân địa phương tổ chức.</p>
            </article>
            </body></html>
            """;
        var text = LocalOsTextExtract.StripHtml(html);
        Assert.Contains("hát then", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chợ phiên", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StripHtml_keeps_lead_that_starts_with_title_not_truncated_meta()
    {
        const string html = """
            <html><head>
            <title>Lễ hội truyền thống chùa Long Sào | Lễ Hội Việt Nam</title>
            <meta property="og:title" content="Lễ hội truyền thống chùa Long Sào">
            <meta property="og:description" content="Lễ hội truyền thống chùa Long Sào diễn ra tại xóm Cơ Phi với phần lễ dâng hương, cầu an và tưởng nhớ tiền nhân. Phần hội có văn nghệ, trò chơi dân gian, thu...">
            </head><body>
            <nav><p>Đăng nhập để theo dõi lễ hội và nhận cập nhật phù hợp.</p></nav>
            <h1>Lễ hội truyền thống chùa Long Sào</h1>
            <p class="mb-0">Lễ hội truyền thống chùa Long Sào diễn ra tại xóm Cơ Phi với phần lễ dâng hương, cầu an và tưởng nhớ tiền nhân. Phần hội có văn nghệ, trò chơi dân gian, thu hút người dân địa phương và du khách thập phương.</p>
            <p class="v2-detail-locality-cta-lead">Đi sâu vào Xã Thành Công hoặc mở trang Thái Nguyên để xem thêm khu vực và lịch lễ hội.</p>
            <h3>Điểm nhấn</h3>
            <ul>
            <li>Có nghi thức dâng hương, cầu quốc thái dân an</li>
            <li>Tưởng nhớ công đức các bậc tiền nhân</li>
            </ul>
            </body></html>
            """;
        var text = LocalOsTextExtract.StripHtml(html);
        Assert.Contains("thu hút người dân địa phương", text, StringComparison.Ordinal);
        Assert.Contains("cầu quốc thái dân an", text, StringComparison.Ordinal);
        Assert.DoesNotContain("thu...", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Đi sâu vào", text, StringComparison.Ordinal);
        var lead = LocalOsTextExtract.GuessSummary("Lễ hội truyền thống chùa Long Sào | Lễ Hội Việt Nam", text);
        Assert.False(LocalOsTextExtract.IsThinLead(lead));
        Assert.Contains("thu hút người dân địa phương", lead, StringComparison.Ordinal);
    }
}

public class LocalOsIndexLinksTests
{
    [Fact]
    public void Extracts_same_host_article_skips_facebook_and_index()
    {
        var page = new Uri("https://thainguyen.gov.vn/tin-tuc-su-kien");
        const string html = """
            <a href="/tin-tuc-su-kien">mục lục</a>
            <a href="https://thainguyen.gov.vn/thong-tin-ke-hoach/festival-tra-2026">Festival</a>
            <a href="https://www.facebook.com/groups/1/">fb</a>
            <a href="https://other.vn/x">ngoài</a>
            <a href="mailto:a@b.c">mail</a>
            """;
        var links = LocalOsIndexLinks.Extract(html, page, 10);
        Assert.Single(links);
        Assert.Contains("festival-tra-2026", links[0].AbsoluteUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prefers_job_article_over_nav()
    {
        var page = new Uri("https://fit.ictu.edu.vn/");
        const string html = """
            <a href="/gioi-thieu">Giới thiệu</a>
            <a href="/lien-he">Liên hệ</a>
            <a href="/tuyen-dung/thuc-tap-sinh-2026">Tuyển dụng thực tập sinh 2026</a>
            """;
        var hits = LocalOsIndexLinks.ExtractHits(html, page, 5);
        Assert.Contains(hits, h => h.Uri.AbsoluteUri.Contains("thuc-tap-sinh-2026", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(hits, h => h.Uri.AbsolutePath.Contains("gioi-thieu", StringComparison.OrdinalIgnoreCase));
    }
}
