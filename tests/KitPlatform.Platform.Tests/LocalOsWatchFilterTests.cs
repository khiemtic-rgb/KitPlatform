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
