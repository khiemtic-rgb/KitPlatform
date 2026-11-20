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
        var veo = _availability.Inspect(FamixaProviderIds.Google);
        return new FamixaProviderRouterContext(
            new FamixaProviderConfiguredSnapshot(
                runway.Configuration == FamixaProviderAvailabilityState.Configured,
                wan.Configuration == FamixaProviderAvailabilityState.Configured,
                voice.Configuration == FamixaProviderAvailabilityState.Configured,
                picture.Configuration == FamixaProviderAvailabilityState.Configured,
                veo.Configuration == FamixaProviderAvailabilityState.Configured));
    }
}

/// <summary>Key snapshot only. Never AVAILABLE. No vendor HTTP. Same org JSON as Cấu hình AI.</summary>
internal sealed class FamixaProviderAvailability : IFamixaProviderAvailability
{
    private readonly ContentRepository _repo;
    private readonly IOptions<ContentOptions> _options;
    private readonly IConfiguration _configuration;
    private FamixaProviderConfiguredSnapshot? _snapshot;

    public FamixaProviderAvailability(
        ContentRepository repo,
        IOptions<ContentOptions> options,
        IConfiguration configuration)
    {
        _repo = repo;
        _options = options;
        _configuration = configuration;
    }

    public FamixaProviderAvailabilityResult Inspect(string providerId, string? modelId = null) =>
        FamixaProviderAvailabilityRules.Classify(providerId, Snapshot(), modelId);

    private FamixaProviderConfiguredSnapshot Snapshot()
    {
        if (_snapshot is not null) return _snapshot;
        var row = _repo.GetOrgSettingsAsync(CancellationToken.None).GetAwaiter().GetResult();
        var video = ContentVideoConfigParser.Resolve(
            ContentVideoConfigParser.Parse(row.VideoConfigJson), _options.Value, _configuration);
        var ai = ContentAiConfigParser.Resolve(
            ContentAiConfigParser.Parse(row.AiConfigJson), _options.Value, _configuration);
        _snapshot = new FamixaProviderConfiguredSnapshot(
            video.RunwayConfigured,
            video.FalConfigured,
            video.ElevenLabsConfigured,
            ai.ApiKeyConfigured,
            video.VeoJoinConfigured);
        return _snapshot;
    }
}
