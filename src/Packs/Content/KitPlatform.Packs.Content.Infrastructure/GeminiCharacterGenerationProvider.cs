using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// ICharacterGenerationProvider → existing Gemini adapters. Application never sees Gemini SDK/HTTP.
/// </summary>
internal sealed class GeminiCharacterGenerationProvider : ICharacterGenerationProvider
{
    private readonly ICharacterAuthorityGenerationProvider _master;
    private readonly ICharacterReferenceGenerationProvider _views;

    public GeminiCharacterGenerationProvider(
        ICharacterAuthorityGenerationProvider master,
        ICharacterReferenceGenerationProvider views)
    {
        _master = master;
        _views = views;
    }

    public string ProviderId =>
        string.IsNullOrWhiteSpace(_views.ProviderId) ? _master.ProviderId : _views.ProviderId;

    public Task<CharacterAuthorityGenerationResult> GenerateMasterAsync(
        CharacterAuthorityGenerationRequest request, CancellationToken cancellationToken) =>
        _master.GenerateMasterAsync(request, cancellationToken);

    public Task<CharacterReferenceSetResult> GenerateViewsAsync(
        CharacterReferenceSetRequest request, CancellationToken cancellationToken)
    {
        if (!CharacterAgeGenerationIntegrationV1Rules.ProviderRequestHasAge(request))
            return Task.FromResult(new CharacterReferenceSetResult(
                false, false, [], CharacterAgeConsistencyV1Rules.GateNotReady, null));
        if (!CharacterAgeGenerationIntegrationV1Rules.ProviderRequestHasAppearance(request))
            return Task.FromResult(new CharacterReferenceSetResult(
                false, false, [], CharacterAppearanceProfileV1Rules.GateNotReady, null));
        return _views.GenerateSetAsync(
            CharacterAgeGenerationIntegrationV1Rules.EnsureAgeOnProviderRequest(request),
            cancellationToken);
    }
}
