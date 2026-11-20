using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public class ContentYoutubeAutoPlaylistTests
{
    const string Video = "vid-1";

    static ContentYoutubePlaylistVideo Film(string? brand = "trading") => new(
        "Đã cháy một lần, vì sao vẫn cháy lần hai",
        "Phần tiếp của vì sao người giao dịch thua lỗ.",
        ["giữ vốn"],
        "Toán của thua lỗ",
        2,
        brand,
        ["NguoiGiuVon"]);

    [Fact]
    public async Task Named_box_uses_that_name()
    {
        var ops = new Ops { Found = "PL1" };
        var judge = new Judge { ThrowIfCalled = true };
        var result = await ContentYoutubeAutoPlaylist.ResolveAsync(
            Film(), Video, "Toán của thua lỗ: vì sao giữ vốn trước tiên", null, null, ops, judge, default);
        Assert.Equal("added", result.Action);
        Assert.Equal("PL1", result.Id);
        Assert.Null(result.Warning);
        Assert.Contains("add:PL1", ops.Calls);
        Assert.Empty(judge.Calls);
    }

    [Fact]
    public async Task Empty_box_adds_when_score_is_high_enough()
    {
        var ops = new Ops
        {
            Lists =
            [
                new("PL-loss", "Toán của thua lỗ: vì sao giữ vốn trước tiên", "Giữ vốn"),
                new("PL-who", "Bắt đầu từ đây: Người Giữ Vốn là ai?", "Giới thiệu"),
            ],
        };
        var judge = new Judge { Pick = new ContentYoutubePlaylistPick("PL-loss", 0.91, "Cùng chủ đề thua lỗ") };
        var result = await ContentYoutubeAutoPlaylist.ResolveAsync(Film(), Video, null, null, null, ops, judge, default);
        Assert.Equal("added", result.Action);
        Assert.Equal("PL-loss", result.Id);
        Assert.Equal("Toán của thua lỗ: vì sao giữ vốn trước tiên", result.Title);
        Assert.Contains("add:PL-loss", ops.Calls);
        Assert.DoesNotContain(ops.Calls, c => c.StartsWith("create", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Empty_box_creates_when_nothing_matches()
    {
        var ops = new Ops { Lists = [new("PL-who", "Bắt đầu từ đây", "Giới thiệu")] };
        var judge = new Judge
        {
            Pick = new ContentYoutubePlaylistPick(null, 0.2, "Không cùng chủ đề"),
            Draft = ("Vốn sống và vốn giao dịch", "Tiền nào được mang vào thị trường"),
        };
        var result = await ContentYoutubeAutoPlaylist.ResolveAsync(Film(), Video, "  ", null, null, ops, judge, default);
        Assert.Equal("created", result.Action);
        Assert.Equal("NEW", result.Id);
        Assert.Contains(ContentYoutubePlaylistRules.NgvDisclaimer, ops.CreatedDescription);
        Assert.Contains("#NguoiGiuVon", ops.CreatedDescription);
        Assert.True(ops.CreatedTitle!.Length <= 60);
    }

    [Fact]
    public async Task Series_saved_playlist_is_reused_without_scoring()
    {
        var ops = new Ops();
        var judge = new Judge { ThrowIfCalled = true };
        var result = await ContentYoutubeAutoPlaylist.ResolveAsync(
            Film(), Video, null, "PL-saved", "Toán của thua lỗ: vì sao giữ vốn trước tiên", ops, judge, default);
        Assert.Equal("added", result.Action);
        Assert.Equal("PL-saved", result.Id);
        Assert.Contains("Tập cùng series", result.Reason);
        Assert.Empty(judge.Calls);
    }

    [Fact]
    public async Task Playlist_error_is_a_warning_and_does_not_throw()
    {
        var ops = new Ops { ThrowOnAdd = true, Found = "PL1" };
        var result = await ContentYoutubeAutoPlaylist.ResolveAsync(
            Film(), Video, "Danh sách A", null, null, ops, new Judge(), default);
        Assert.Null(result.Action);
        Assert.Contains("quota", result.Warning);
    }

    sealed class Ops : IYoutubePlaylistOps
    {
        public List<string> Calls { get; } = [];
        public List<ContentYoutubePlaylistItem> Lists { get; init; } = [];
        public string? Found { get; init; }
        public bool ThrowOnAdd { get; init; }
        public string? CreatedTitle { get; private set; }
        public string? CreatedDescription { get; private set; }

        public Task<IReadOnlyList<ContentYoutubePlaylistItem>> ListAsync(CancellationToken ct)
        {
            Calls.Add("list");
            return Task.FromResult<IReadOnlyList<ContentYoutubePlaylistItem>>(Lists);
        }

        public Task<string?> FindByTitleAsync(string title, CancellationToken ct)
        {
            Calls.Add("find:" + title);
            return Task.FromResult(Found);
        }

        public Task<string> CreateAsync(string title, string? description, CancellationToken ct)
        {
            Calls.Add("create:" + title);
            CreatedTitle = title;
            CreatedDescription = description;
            return Task.FromResult("NEW");
        }

        public Task AddAsync(string playlistId, string videoId, CancellationToken ct)
        {
            if (ThrowOnAdd) throw new InvalidOperationException("YouTube quota");
            Calls.Add("add:" + playlistId);
            return Task.CompletedTask;
        }
    }

    sealed class Judge : IYoutubePlaylistJudge
    {
        public List<string> Calls { get; } = [];
        public bool ThrowIfCalled { get; init; }
        public ContentYoutubePlaylistPick Pick { get; init; } = new(null, 0, null);
        public (string? Title, string? Description) Draft { get; init; }

        public Task<ContentYoutubePlaylistPick> PickAsync(
            ContentYoutubePlaylistVideo video, IReadOnlyList<ContentYoutubePlaylistItem> playlists, CancellationToken ct)
        {
            Calls.Add("pick");
            if (ThrowIfCalled) throw new InvalidOperationException("judge");
            return Task.FromResult(Pick);
        }

        public Task<(string? Title, string? Description)> DraftAsync(ContentYoutubePlaylistVideo video, CancellationToken ct)
        {
            Calls.Add("draft");
            if (ThrowIfCalled) throw new InvalidOperationException("judge");
            return Task.FromResult(Draft);
        }
    }
}
