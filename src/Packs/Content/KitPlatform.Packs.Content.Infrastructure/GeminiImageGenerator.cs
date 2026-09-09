using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class GeminiImageGenerator : IImageGenerator
{
    private readonly ContentGeminiClient _gemini;
    private readonly ContentOptions _options;
    private readonly IConfiguration _configuration;

    public GeminiImageGenerator(
        ContentGeminiClient gemini,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _gemini = gemini;
        _options = options.Value;
        _configuration = configuration;
    }

    public string ProviderId => "GEMINI";

    public async Task<KitVideoImageGenerationResult> GenerateAsync(
        KitVideoImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var seconds = _options.KitVideoGeminiTimeoutSeconds > 0 ? _options.KitVideoGeminiTimeoutSeconds : 90;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(seconds));
        try
        {
            var refs = request.References
                .Select(r => (r.Mime, Convert.ToBase64String(r.Bytes), r.Label))
                .ToList();
            var (bytes, model) = await _gemini.GenerateImageWithRefsAsync(
                request.Prompt,
                refs,
                request.AspectRatio,
                timeout.Token);
            if (bytes is null || bytes.Length == 0)
            {
                return new KitVideoImageGenerationResult(
                    false, "PROVIDER_FAILED", null, null, ProviderId, model, null, null, null, true);
            }
            var mime = KitVideoArtifactRules.DetectMime(bytes) ?? "image/png";
            return new KitVideoImageGenerationResult(
                true, "", bytes, mime, ProviderId, model, Guid.NewGuid().ToString("N")[..16], null, null, true);
        }
        catch (OperationCanceledException)
        {
            return new KitVideoImageGenerationResult(
                false, "REQUEST_FAILED", null, null, ProviderId, ModelName(), null, null, null, true);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
        {
            return new KitVideoImageGenerationResult(
                false, "REQUEST_FAILED", null, null, ProviderId, ModelName(), null, null, null, true);
        }
        catch (Exception)
        {
            return new KitVideoImageGenerationResult(
                false, "PROVIDER_FAILED", null, null, ProviderId, ModelName(), null, null, null, true);
        }
    }

    private string ModelName() =>
        First(
            _options.KitVideoGeminiModel,
            _configuration["KIT_VIDEO_GEMINI_MODEL"],
            _options.ImageModel,
            "gemini-configured");

    private static string First(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";
}
