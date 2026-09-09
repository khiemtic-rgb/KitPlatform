using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// ICharacterReferenceGenerationProvider → ContentGeminiClient. One logical set, sequential views, no retry.
/// </summary>
internal sealed class GeminiCharacterReferenceGenerationProvider : ICharacterReferenceGenerationProvider
{
    private readonly ContentGeminiClient _gemini;

    public GeminiCharacterReferenceGenerationProvider(ContentGeminiClient gemini) => _gemini = gemini;

    public string ProviderId => "GEMINI";

    public async Task<CharacterReferenceSetResult> GenerateSetAsync(
        CharacterReferenceSetRequest request, CancellationToken cancellationToken)
    {
        var views = new List<CharacterReferenceGeneratedView>();
        string? lastRequestId = null;
        foreach (var view in request.Views)
        {
            var refs = view.IdentityRefs
                .Select(r => (r.Mime, Convert.ToBase64String(r.Bytes), r.Label))
                .ToList();
            var (accepted, bytes, mime, model, _) = await _gemini.GenerateProductionStillOnceAsync(
                CharacterAgeGenerationIntegrationV1Rules.ProviderPrompt(request, view),
                refs, view.AspectRatio, cancellationToken);
            lastRequestId = model;
            var ok = accepted && bytes is { Length: > 32 };
            views.Add(new CharacterReferenceGeneratedView(
                view.ReferenceType, ok, ok ? bytes : null, mime, model));
            if (!ok)
                break;
        }

        var complete = views.Count == request.Views.Count && views.All(v => v.Succeeded);
        return new CharacterReferenceSetResult(
            true,
            complete,
            views,
            complete ? null : (views.Exists(v => v.Succeeded) ? "REFERENCE_SET_INCOMPLETE" : "REFERENCE_GENERATION_FAILED"),
            lastRequestId);
    }
}
