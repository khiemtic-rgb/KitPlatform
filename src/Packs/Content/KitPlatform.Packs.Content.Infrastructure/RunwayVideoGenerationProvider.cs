using System.Text.RegularExpressions;
using System.Threading;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Execution-layer adapter. Video Contract never sees this type.</summary>
internal sealed class RunwayVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly ContentRunwayClient _runway;
    private readonly IContentSeriesTakeProxyService _takes;
    private static int _calls;
    private static int _creates;

    public RunwayVideoGenerationProvider(ContentRunwayClient runway, IContentSeriesTakeProxyService takes)
    {
        _runway = runway;
        _takes = takes;
    }

    public string ProviderId => "RUNWAY";
    public string ConfigVersion => VideoGenerationExecutionRules.ProviderConfigVersion;
    public static int CallCount => _calls;
    public static int CreateCount => _creates;

    public async Task<VideoGenerationProviderResult> GenerateAsync(
        VideoGenerationExecutionRequest request, CancellationToken cancellationToken)
    {
        if (!VideoVisualIngressV1Rules.ProviderMayCall(request))
            return Fail("FAILED", null);
        var prompt = request.CompiledPrompt!;
        Interlocked.Increment(ref _calls);
        if (ContainsProviderLeak(prompt))
            return Fail("FAILED", null);
        var duration = (int)Math.Round(request.DurationSeconds, MidpointRounding.AwayFromZero);
        if (!VideoGenerationExecutionRules.AllowedDuration(duration))
            return Fail("FAILED", null);
        var ratio = MapRatio(request.AspectRatio, request.Resolution);
        var mime = string.IsNullOrWhiteSpace(request.SourceMime) ? "image/png" : request.SourceMime;
        var dataUri = $"data:{mime};base64,{Convert.ToBase64String(request.SourceImage)}";
        string taskId;
        try
        {
            Interlocked.Increment(ref _creates);
            taskId = await _runway.CreateImageToVideoAsync(dataUri, prompt, duration, ratio, cancellationToken);
        }
        catch
        {
            return Fail("FAILED", null);
        }
        if (string.IsNullOrWhiteSpace(taskId))
            return new VideoGenerationProviderResult(true, false, "ACCEPTED", null, null, ProviderId, null, true, null);
        return await WaitAsync(taskId, cancellationToken);
    }

    public Task<VideoGenerationProviderResult> ResumeAsync(string providerRequestId, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        return WaitAsync(providerRequestId, cancellationToken);
    }

    private async Task<VideoGenerationProviderResult> WaitAsync(string taskId, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 90; i++)
        {
            (string Status, string? VideoUrl, string? Error, string? FailureCode) task;
            try
            {
                task = await _runway.GetTaskAsync(taskId, cancellationToken);
            }
            catch
            {
                return new VideoGenerationProviderResult(true, false, "PROCESSING", null, null, ProviderId, taskId, true, null);
            }
            if (task.Status is "PENDING" or "RUNNING" or "THROTTLED" or "PROCESSING")
            {
                await Task.Delay(2000, cancellationToken);
                continue;
            }
            if (task.Status != "SUCCEEDED" || string.IsNullOrWhiteSpace(task.VideoUrl))
                return new VideoGenerationProviderResult(true, false, "FAILED", null, null, ProviderId, taskId, true, null);
            try
            {
                var fetched = await _takes.FetchAsync(task.VideoUrl, cancellationToken);
                if (fetched.Bytes is null || fetched.Bytes.Length == 0)
                    return new VideoGenerationProviderResult(true, false, "SUCCEEDED", null, null, ProviderId, taskId, true, null);
                return new VideoGenerationProviderResult(
                    true, true, "SUCCEEDED", fetched.Bytes,
                    string.IsNullOrWhiteSpace(fetched.ContentType) ? "video/mp4" : fetched.ContentType,
                    ProviderId, taskId, true, null);
            }
            catch
            {
                return new VideoGenerationProviderResult(true, false, "FAILED", null, null, ProviderId, taskId, true, null);
            }
        }
        return new VideoGenerationProviderResult(true, false, "PROCESSING", null, null, ProviderId, taskId, true, null);
    }

    private static VideoGenerationProviderResult Fail(string status, string? id) =>
        new(false, false, status, null, null, "RUNWAY", id, true, null);

    private static string MapRatio(string aspect, string resolution)
    {
        var blob = (aspect + " " + resolution).Replace('x', ':');
        return blob.Contains("1280", StringComparison.OrdinalIgnoreCase) || blob.Contains("16:9", StringComparison.OrdinalIgnoreCase)
            ? "1280:720"
            : "1280:720";
    }

    private static bool ContainsProviderLeak(string? text) =>
        Regex.IsMatch(text ?? "", @"runway|gemini|gen4|model_prompt|FINAL_PROMPT", RegexOptions.IgnoreCase);
}
