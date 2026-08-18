using KitPlatform.Packs.LocalOs;
using Xunit;

namespace KitPlatform.Platform.Tests;

public class LocalOsSourceLinkTests
{
    [Theory]
    [InlineData("https://www.facebook.com/groups/5719882594776915/", "FacebookGroupFeed")]
    [InlineData("https://facebook.com/groups/159548018082242", "FacebookGroupFeed")]
    [InlineData("https://www.facebook.com/groups/5719882594776915/posts/123456789/", "FacebookPost")]
    [InlineData("https://www.facebook.com/groups/abc/permalink/999/", "FacebookPost")]
    [InlineData("https://www.facebook.com/permalink.php?story_fbid=1&id=2", "FacebookPost")]
    [InlineData("https://ictu.edu.vn/thong-bao/workshop", "PublicWeb")]
    public void Classify_known_shapes(string url, string expected)
    {
        Assert.True(LocalOsSourceLink.TryParse(url, out _, out var kind));
        Assert.Equal(expected, kind.ToString());
    }

    [Fact]
    public void Rejects_non_http()
    {
        Assert.False(LocalOsSourceLink.TryParse("javascript:alert(1)", out _, out _));
    }

    [Fact]
    public void Extracts_group_key_from_post()
    {
        Assert.True(LocalOsSourceLink.TryParse(
            "https://www.facebook.com/groups/5719882594776915/posts/123/",
            out var uri,
            out _));
        Assert.True(LocalOsSourceLink.TryExtractFacebookGroupKey(uri!, out var key));
        Assert.Equal("5719882594776915", key);
    }

    [Fact]
    public void Matches_registered_group_not_unrelated_host()
    {
        var group = new LocalSourceDto(
            Guid.Parse("b1111111-1111-1111-1111-111111111101"),
            "facebook_group",
            "G1",
            "https://www.facebook.com/groups/5719882594776915/",
            "active",
            "facebook",
            "job",
            "student",
            "thai_nguyen",
            null);
        var ictu = new LocalSourceDto(
            Guid.Parse("b1111111-1111-1111-1111-111111111201"),
            "official_web",
            "ICTU",
            "https://ictu.edu.vn/",
            "active",
            "web",
            "event",
            "student",
            "thai_nguyen",
            null);

        Assert.True(LocalOsSourceLink.TryParse(
            "https://www.facebook.com/groups/5719882594776915/posts/99/",
            out var post,
            out _));
        var hit = LocalOsSourceMatch.Find([group, ictu], post!, null);
        Assert.Equal(group.Id, hit?.Id);

        Assert.True(LocalOsSourceLink.TryParse("https://ictu.edu.vn/thong-bao/x", out var web, out _));
        Assert.Equal(ictu.Id, LocalOsSourceMatch.Find([group, ictu], web!, null)?.Id);
    }

    [Fact]
    public void Matches_university_facebook_page_slug()
    {
        var page = new LocalSourceDto(
            Guid.Parse("b1111111-1111-1111-1111-111111111403"),
            "facebook_page",
            "Fanpage ICTU",
            "https://www.facebook.com/ictu.vn",
            "active",
            "facebook",
            "event",
            "student",
            "thai_nguyen",
            null);

        Assert.True(LocalOsSourceLink.TryParse(
            "https://www.facebook.com/ictu.vn/posts/123/",
            out var post,
            out _));
        Assert.Equal(page.Id, LocalOsSourceMatch.Find([page], post!, null)?.Id);
        Assert.True(LocalOsSourceLink.TryExtractFacebookPageKey(post!, out var key));
        Assert.Equal("ictu.vn", key);
    }

    [Fact]
    public void Matches_google_sheet_by_id_not_host()
    {
        var tnus = new LocalSourceDto(
            Guid.Parse("b1111111-1111-1111-1111-111111111204"),
            "official_web",
            "TNUS sheet",
            "https://docs.google.com/spreadsheets/d/1zAv9IswrXGwDB4iKu7icj38SjpwPIwjH/edit",
            "active",
            "web",
            "room",
            "student",
            "thai_nguyen",
            null);

        Assert.True(LocalOsSourceLink.TryParse(
            "https://docs.google.com/spreadsheets/d/1zAv9IswrXGwDB4iKu7icj38SjpwPIwjH/edit?gid=1",
            out var same,
            out _));
        Assert.Equal(tnus.Id, LocalOsSourceMatch.Find([tnus], same!, null)?.Id);

        Assert.True(LocalOsSourceLink.TryParse(
            "https://docs.google.com/spreadsheets/d/otherSheetId999/edit",
            out var other,
            out _));
        Assert.Null(LocalOsSourceMatch.Find([tnus], other!, null));
    }
}
