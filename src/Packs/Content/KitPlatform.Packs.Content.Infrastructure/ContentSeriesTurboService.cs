using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentSeriesTurboService : IContentSeriesTurboService
{
    private const string PlaceholderPng =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private readonly ContentRunwayClient _runway;
    private readonly ContentFalClient _fal;

    public ContentSeriesTurboService(ContentRunwayClient runway, ContentFalClient fal)
    {
        _runway = runway;
        _fal = fal;
    }

    public async Task<ContentSeriesTurboTaskDto> StartAsync(
        ContentSeriesTurboStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var wan = IsWan(request.Engine);
        var seconds = request.Seconds >= 8 ? 10 : 5;
        var ratio = MapRatio(request.Ratio);
        var image = NormalizeImage(request.ImageDataUrl);
        var usedPlaceholder = string.IsNullOrWhiteSpace(request.ImageDataUrl)
                              || !LooksLikeImage(request.ImageDataUrl);
        if (usedPlaceholder)
            image = PlaceholderPng;

        var prompt = BuildPrompt(request.Prompt);
        if (wan)
        {
            var wanId = await _fal.CreateImageToVideoAsync(
                image,
                prompt,
                request.NegativePrompt,
                seconds,
                request.Ratio,
                cancellationToken);
            return new ContentSeriesTurboTaskDto(
                wanId,
                "PENDING",
                null,
                null,
                usedPlaceholder,
                ContentFalClient.WanModel,
                seconds >= 8 ? 6 : 5);
        }

        var taskId = await _runway.CreateImageToVideoAsync(image, prompt, seconds, ratio, cancellationToken);
        return new ContentSeriesTurboTaskDto(
            taskId,
            "PENDING",
            null,
            null,
            usedPlaceholder,
            ContentRunwayClient.TurboModel,
            seconds);
    }

    public async Task<ContentSeriesTurboTaskDto> GetAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("Thiếu task id.");

        var id = taskId.Trim();
        if (ContentFalClient.IsWanTask(id))
        {
            var wan = await _fal.GetTaskAsync(id, cancellationToken);
            return new ContentSeriesTurboTaskDto(
                id,
                wan.Status,
                wan.VideoUrl,
                wan.Error,
                false,
                ContentFalClient.WanModel,
                0);
        }

        var (status, video, error) = await _runway.GetTaskAsync(id, cancellationToken);
        return new ContentSeriesTurboTaskDto(
            id,
            status,
            video,
            error,
            false,
            ContentRunwayClient.TurboModel,
            0);
    }

    private static bool IsWan(string? engine) =>
        string.Equals((engine ?? "").Trim(), "wan", StringComparison.OrdinalIgnoreCase);

    private static string MapRatio(string? raw)
    {
        var r = (raw ?? "").Trim().ToLowerInvariant();
        if (r is "9:16" or "9x16" or "720:1280") return "720:1280";
        return "1280:720";
    }

    private static bool LooksLikeImage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw.Trim();
        return t.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
               || t.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeImage(string? raw)
    {
        var t = (raw ?? "").Trim();
        return t;
    }

    private static string BuildPrompt(string? prompt)
    {
        var raw = (prompt ?? "").Trim();
        const string safe =
            "Cinematic live-action dinner scene. The photo is the first frame only. " +
            "Start motion right away: people blink and breathe, hands serve rice, chopsticks, steam from bowls, " +
            "small head turns, soft eye contact, gentle camera drift. Keep the same seats and faces. No captions.";
        if (string.IsNullOrWhiteSpace(raw)) return safe;
        return raw.Length <= 980 ? raw : raw[..980];
    }
}
