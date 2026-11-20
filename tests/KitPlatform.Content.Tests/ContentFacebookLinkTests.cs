using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentFacebookLinkTests
{
    [Fact]
    public void Apply_writes_camel_case_connected_page()
    {
        var json = ContentFacebookLink.Apply(null, new ContentFacebookLinkState
        {
            Status = "CONNECTED",
            PageId = "1503049169552227",
            PageName = "Người Giữ Vốn",
            ConnectedAt = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero),
        });

        using var doc = JsonDocument.Parse(json);
        var fb = doc.RootElement.GetProperty("facebook");
        Assert.Equal("CONNECTED", fb.GetProperty("status").GetString());
        Assert.Equal("1503049169552227", fb.GetProperty("pageId").GetString());
        Assert.Equal("Người Giữ Vốn", fb.GetProperty("pageName").GetString());
        Assert.False(string.IsNullOrWhiteSpace(fb.GetProperty("connectedAt").GetString()));
        Assert.False(fb.TryGetProperty("Status", out _));
        Assert.True(ContentScheduleRules.IsFacebookAutoReady(json, null));
    }
}
