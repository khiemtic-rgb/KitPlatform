using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed record ContentYoutubePlaylistItem(string Id, string Title, string Description);

internal sealed record ContentYoutubePlaylistVideo(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    string? SeriesName,
    int? EpisodeNo,
    string? BrandCode,
    IReadOnlyList<string> Hashtags);

internal sealed record ContentYoutubePlaylistPick(string? PlaylistId, double Score, string? Reason);

internal sealed record ContentYoutubePlaylistOutcome(
    string? Id,
    string? Title,
    string? Action,
    string? Reason,
    string? Warning);

internal interface IYoutubePlaylistOps
{
    Task<IReadOnlyList<ContentYoutubePlaylistItem>> ListAsync(CancellationToken ct);
    Task<string?> FindByTitleAsync(string title, CancellationToken ct);
    Task<string> CreateAsync(string title, string? description, CancellationToken ct);
    Task AddAsync(string playlistId, string videoId, CancellationToken ct);
}

internal interface IYoutubePlaylistJudge
{
    Task<ContentYoutubePlaylistPick> PickAsync(
        ContentYoutubePlaylistVideo video, IReadOnlyList<ContentYoutubePlaylistItem> playlists, CancellationToken ct);
    Task<(string? Title, string? Description)> DraftAsync(ContentYoutubePlaylistVideo video, CancellationToken ct);
}

/// <summary>Không ném lỗi ra ngoài: quota hoặc thiếu quyền thành cảnh báo, video đã upload vẫn thành công.</summary>
internal static class ContentYoutubeAutoPlaylist
{
    public static async Task<ContentYoutubePlaylistOutcome> ResolveAsync(
        ContentYoutubePlaylistVideo video,
        string videoId,
        string? explicitName,
        string? savedPlaylistId,
        string? savedTitle,
        IYoutubePlaylistOps ops,
        IYoutubePlaylistJudge judge,
        CancellationToken ct)
    {
        try
        {
            var plan = ContentYoutubePlaylistRules.ChoosePlan(explicitName, savedPlaylistId);
            if (plan == ContentYoutubePlaylistRules.Plan.Named)
            {
                var name = explicitName!.Trim();
                var existing = await ops.FindByTitleAsync(name, ct);
                var id = existing ?? await ops.CreateAsync(name, null, ct);
                await ops.AddAsync(id, videoId, ct);
                return new ContentYoutubePlaylistOutcome(
                    id, name, existing is null ? "created" : "added", "Theo tên đã chọn", null);
            }

            if (plan == ContentYoutubePlaylistRules.Plan.Reuse)
            {
                await ops.AddAsync(savedPlaylistId!, videoId, ct);
                return new ContentYoutubePlaylistOutcome(
                    savedPlaylistId, savedTitle, "added", "Tập cùng series, dùng lại danh sách đã chọn.", null);
            }

            var lists = await ops.ListAsync(ct);
            ContentYoutubePlaylistPick? chosen = null;
            if (lists.Count > 0)
            {
                try
                {
                    var pick = await judge.PickAsync(video, lists, ct);
                    if (ContentYoutubePlaylistRules.Accept(pick.PlaylistId, pick.Score, lists.Select(p => p.Id).ToList()))
                        chosen = pick;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    chosen = null;
                }
            }

            if (chosen?.PlaylistId is { } matchId)
            {
                var title = lists.First(p => p.Id == matchId).Title;
                await ops.AddAsync(matchId, videoId, ct);
                return new ContentYoutubePlaylistOutcome(matchId, title, "added", chosen.Reason, null);
            }

            (string? Title, string? Description) draft;
            try { draft = await judge.DraftAsync(video, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { draft = (null, null); }
            var normalized = ContentYoutubePlaylistRules.NormalizeNew(
                draft.Title, draft.Description, video.Title, video.BrandCode, video.Hashtags);
            var created = await ops.CreateAsync(normalized.Title, normalized.Description, ct);
            await ops.AddAsync(created, videoId, ct);
            return new ContentYoutubePlaylistOutcome(
                created, normalized.Title, "created", "Không có danh sách đạt 0,7 nên tạo mới.", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ContentYoutubePlaylistOutcome(null, null, null, null, ex.Message);
        }
    }
}

internal sealed class ContentYoutubePlaylistJudge : IYoutubePlaylistJudge
{
    private readonly ContentGeminiClient _gemini;

    public ContentYoutubePlaylistJudge(ContentGeminiClient gemini) => _gemini = gemini;

    public async Task<ContentYoutubePlaylistPick> PickAsync(
        ContentYoutubePlaylistVideo video, IReadOnlyList<ContentYoutubePlaylistItem> playlists, CancellationToken ct)
    {
        var raw = await _gemini.GenerateJsonAsync(PickSystem, User(video, playlists), ct, maxOutputTokens: 400, disableThinking: true);
        return ParsePick(raw);
    }

    public async Task<(string? Title, string? Description)> DraftAsync(ContentYoutubePlaylistVideo video, CancellationToken ct)
    {
        var raw = await _gemini.GenerateJsonAsync(DraftSystem, User(video, []), ct, maxOutputTokens: 400, disableThinking: true);
        return ParseDraft(raw);
    }

    internal static ContentYoutubePlaylistPick ParsePick(string raw)
    {
        using var doc = JsonDocument.Parse(Unwrap(raw));
        var root = doc.RootElement;
        var id = root.TryGetProperty("playlistId", out var pid) && pid.ValueKind == JsonValueKind.String ? pid.GetString() : null;
        var score = root.TryGetProperty("score", out var sc) && sc.TryGetDouble(out var n) ? n : 0;
        var reason = root.TryGetProperty("reason", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
        return new ContentYoutubePlaylistPick(string.IsNullOrWhiteSpace(id) ? null : id.Trim(), score, reason);
    }

    internal static (string? Title, string? Description) ParseDraft(string raw)
    {
        using var doc = JsonDocument.Parse(Unwrap(raw));
        var root = doc.RootElement;
        string? S(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        return (S("title"), S("description"));
    }

    static string Unwrap(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var start = text.IndexOf('\n');
            var end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start >= 0 && end > start) text = text[(start + 1)..end];
        }
        return text.Trim();
    }

    static string User(ContentYoutubePlaylistVideo video, IReadOnlyList<ContentYoutubePlaylistItem> playlists)
    {
        var lists = playlists.Count == 0
            ? "(không có danh sách)"
            : string.Join("\n", playlists.Take(30).Select(p =>
                $"- id={p.Id} | {Trim(p.Title, 80)} | {Trim(p.Description, 160)}"));
        var series = string.IsNullOrWhiteSpace(video.SeriesName)
            ? "không thuộc series"
            : $"{video.SeriesName}" + (video.EpisodeNo is { } n ? $", tập {n}" : "");
        return $"""
            Tiêu đề: {Trim(video.Title, 120)}
            Series: {series}
            Mô tả: {Trim(video.Description, 500)}
            Tags: {string.Join(", ", video.Tags.Take(12))}
            Danh sách phát của kênh:
            {lists}
            """;
    }

    static string Trim(string? text, int max)
    {
        var s = (text ?? "").Trim();
        return s.Length <= max ? s : s[..max];
    }

    const string PickSystem = """
        Chọn một danh sách phát YouTube có sẵn đúng chủ đề video. Chỉ trả JSON:
        {"playlistId": "id hoặc null", "score": 0 đến 1, "reason": "một câu"}
        playlistId phải là một id trong danh sách, hoặc null. Không bịa id.
        score >= 0.7 chỉ khi cùng chủ đề rõ. Tập sau của cùng series phải vào đúng danh sách của tập trước.
        Không hợp thì playlistId null và score dưới 0.7.
        """;

    const string DraftSystem = """
        Đặt tên danh sách phát YouTube mới theo đúng chủ đề video. Chỉ trả JSON:
        {"title": "tối đa 60 ký tự, nói chủ đề, không câu giật tít", "description": "một hoặc hai câu"}
        Không dùng chữ in hoa toàn bộ, không dấu chấm than.
        """;
}

internal sealed class YoutubeClientOps : IYoutubePlaylistOps
{
    private readonly ContentYoutubeClient _yt;
    private readonly string _access;

    public YoutubeClientOps(ContentYoutubeClient yt, string access)
    {
        _yt = yt;
        _access = access;
    }

    public async Task<IReadOnlyList<ContentYoutubePlaylistItem>> ListAsync(CancellationToken ct)
    {
        var rows = await _yt.ListMyPlaylistsAsync(_access, ct);
        return rows.Select(r => new ContentYoutubePlaylistItem(r.Id, r.Title, r.Description)).ToList();
    }

    public Task<string?> FindByTitleAsync(string title, CancellationToken ct) =>
        _yt.FindPlaylistByTitleAsync(_access, title, ct);

    public Task<string> CreateAsync(string title, string? description, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(description)
            ? _yt.CreatePlaylistAsync(_access, title, "public", ct)
            : _yt.CreateDescribedPlaylistAsync(_access, title, description, ct);

    public Task AddAsync(string playlistId, string videoId, CancellationToken ct) =>
        _yt.AddToPlaylistAsync(_access, playlistId, videoId, ct);
}
