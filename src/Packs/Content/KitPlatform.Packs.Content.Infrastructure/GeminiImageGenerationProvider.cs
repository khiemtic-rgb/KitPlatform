using System.Threading;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class GeminiImageGenerationProvider : IGeminiImageGenerationProvider
{
    private readonly ContentGeminiClient _gemini;
    private static int _calls;

    public GeminiImageGenerationProvider(ContentGeminiClient gemini) => _gemini = gemini;

    public string ProviderId => ImageGenerationExecutionRules.Provider;
    public static int CallCount => _calls;

    public async Task<ImageGenerationProviderResult> GenerateAsync(
        ImageGenerationExecutionRequest request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _calls);
        var refs = request.References
            .Select(r => (r.Mime, Convert.ToBase64String(r.Bytes), r.Label))
            .ToList();
        var (accepted, bytes, mime, model, error) = await _gemini.GenerateProductionStillOnceAsync(
            request.Prompt, refs, request.AspectRatio, cancellationToken);
        if (!accepted)
        {
            return new ImageGenerationProviderResult(
                false, false, "FAILED", null, null, ProviderId, model, null, true, null);
        }
        if (bytes is null || bytes.Length == 0)
        {
            return new ImageGenerationProviderResult(
                true, false, "ACCEPTED", null, null, ProviderId, model, null, true, null);
        }
        return new ImageGenerationProviderResult(
            true, true, "ACCEPTED", bytes, mime, ProviderId, model, null, true, null);
    }
}
