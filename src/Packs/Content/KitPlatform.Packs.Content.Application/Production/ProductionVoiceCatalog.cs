using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionVoiceCatalog
{
    public const string FixtureProviderId = "fixture";
    public const string FixtureModelId = "fixture-voice-v1";
    public const string ElevenLabsProviderId = FamixaProviderIds.ElevenLabs;
    public const string ElevenV3 = "eleven_v3";

    public static readonly ProductionMotionModel Fixture = new(
        FixtureProviderId, FixtureModelId, FamixaProviderCapability.Voice,
        "Fixture Voice", ProductionActions.CreateVoice, Enabled: true, IsPaid: false);

    public static readonly ProductionMotionModel ElevenLabsV3 = new(
        ElevenLabsProviderId, ElevenV3, FamixaProviderCapability.Voice,
        "ElevenLabs V3", ProductionActions.CreateVoice, Enabled: true, IsPaid: true);

    public static IReadOnlyList<ProductionMotionModel> All { get; } = [Fixture, ElevenLabsV3];

    public static bool TryGet(string? providerId, string? modelId, out ProductionMotionModel model)
    {
        var provider = (providerId ?? "").Trim();
        var mid = (modelId ?? "").Trim();
        model = All.FirstOrDefault(m =>
            string.Equals(m.ProviderId, provider, StringComparison.OrdinalIgnoreCase)
            && string.Equals(m.ModelId, mid, StringComparison.OrdinalIgnoreCase))
            ?? default!;
        return model is not null;
    }
}
