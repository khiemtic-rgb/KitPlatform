using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>Delegates to <see cref="ContentGeminiClient.GenerateProductionStillOnceAsync"/>. One HTTP generation. No fallback.</summary>
internal sealed class GeminiProductionStillClient : IGeminiProductionStillClient
{
    readonly ContentGeminiClient _gemini;
    readonly ContentOptions _options;
    int _calls;

    public GeminiProductionStillClient(ContentGeminiClient gemini, IOptions<ContentOptions> options)
    {
        _gemini = gemini;
        _options = options.Value;
    }

    public int GenerationCount => Volatile.Read(ref _calls);

    public async Task<string> RequireModelAsync(CancellationToken ct = default)
    {
        var resolved = await _gemini.ResolveConfigAsync(ct);
        if (!resolved.ApiKeyConfigured || !resolved.ImagesEnabled)
            throw new InvalidOperationException("IMAGE_PROVIDER_NOT_CONFIGURED");
        _ = _options;
        return ProductionImageCatalog.GeminiFlashImage;
    }

    public async Task<GeminiProductionStillResult> GenerateOnceAsync(string prompt, string? aspectRatio, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _calls);
        var (accepted, bytes, mime, model, error) = await _gemini.GenerateProductionStillOnceAsync(
            prompt, [], aspectRatio, ct);
        return new GeminiProductionStillResult(accepted, bytes, mime, model, error);
    }
}
