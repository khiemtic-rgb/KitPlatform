using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// ICharacterAuthorityGenerationProvider → ContentGeminiClient. One Master image. No retry. No fallback.
/// </summary>
internal sealed class GeminiCharacterAuthorityGenerationProvider : ICharacterAuthorityGenerationProvider
{
    private readonly ContentGeminiClient _gemini;

    public GeminiCharacterAuthorityGenerationProvider(ContentGeminiClient gemini) => _gemini = gemini;

    public string ProviderId => "GEMINI";

    public async Task<CharacterAuthorityGenerationResult> GenerateMasterAsync(
        CharacterAuthorityGenerationRequest request, CancellationToken cancellationToken)
    {
        if (CharacterFirstMasterVisualIngressV1Rules.RequiresBoundContract(request.GenerationType)
            && !CharacterFirstMasterVisualIngressV1Rules.ProviderMayCall(request))
            return new CharacterAuthorityGenerationResult(
                ProviderCalled: false,
                Succeeded: false,
                Bytes: null,
                Mime: null,
                ProviderRequestId: null,
                ErrorCode: CharacterFirstMasterVisualIngressV1Rules.GateSnapshot);

        var prompt = CharacterFirstMasterVisualIngressV1Rules.RequiresBoundContract(request.GenerationType)
            ? request.CanonicalText
            : CharacterDesignLanguageV2Rules.EnsureInPrompt(request.CanonicalText);
        var (accepted, bytes, mime, model, error) = await _gemini.GenerateProductionStillOnceAsync(
            prompt, [], request.AspectRatio, cancellationToken);
        var ok = accepted && bytes is { Length: > 32 };
        return new CharacterAuthorityGenerationResult(
            true,
            ok,
            ok ? bytes : null,
            mime,
            model,
            ok ? null : (error ?? "GENERATION_FAILED"));
    }
}
