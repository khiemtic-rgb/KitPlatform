using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class FamixaProviderSelectionService : IFamixaProviderSelectionService
{
    private readonly IFamixaProviderAvailability _availability;

    public FamixaProviderSelectionService(IFamixaProviderAvailability availability)
    {
        _availability = availability;
    }

    public FamixaProviderSelectionResult Route(FamixaProviderSelectionRequirements requirements) =>
        FamixaProviderSelectionFoundation.Route(requirements, Context());

    public FamixaProviderSelectionDecision Select(FamixaProviderSelectionRequirements requirements) =>
        FamixaProviderSelectionFoundation.Select(requirements, Context());

    private FamixaProviderRouterContext Context()
    {
        var runway = _availability.Inspect(FamixaProviderIds.Runway);
        var wan = _availability.Inspect(FamixaProviderIds.Wan);
        var voice = _availability.Inspect(FamixaProviderIds.ElevenLabs);
        var picture = _availability.Inspect(FamixaProviderIds.Gemini);
        return new FamixaProviderRouterContext(
            new FamixaProviderConfiguredSnapshot(
                runway.Configuration == FamixaProviderAvailabilityState.Configured,
                wan.Configuration == FamixaProviderAvailabilityState.Configured,
                voice.Configuration == FamixaProviderAvailabilityState.Configured,
                picture.Configuration == FamixaProviderAvailabilityState.Configured));
    }
}

/// <summary>Key snapshot only. Never AVAILABLE. No vendor HTTP.</summary>
internal sealed class FamixaProviderAvailability : IFamixaProviderAvailability
{
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;

    public FamixaProviderAvailability(IOptions<ContentOptions> options, IConfiguration configuration)
    {
        _options = options;
        _configuration = configuration;
    }

    public FamixaProviderAvailabilityResult Inspect(string providerId, string? modelId = null)
    {
        var video = ContentVideoConfigParser.Resolve(new ContentVideoConfigState(), _options.Value, _configuration);
        var ai = ContentAiConfigParser.Resolve(new ContentAiConfigState(), _options.Value, _configuration);
        return FamixaProviderAvailabilityRules.Classify(
            providerId,
            new FamixaProviderConfiguredSnapshot(
                video.RunwayConfigured,
                video.FalConfigured,
                video.ElevenLabsConfigured,
                ai.ApiKeyConfigured),
            modelId);
    }
}
