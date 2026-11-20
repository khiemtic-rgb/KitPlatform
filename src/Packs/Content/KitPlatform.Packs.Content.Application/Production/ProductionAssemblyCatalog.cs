namespace KitPlatform.Packs.Content.Production;

/// <summary>Official ASSEMBLE uses the same unpaid server FFmpeg engine as MIX. Not an AI provider.</summary>
public static class ProductionAssemblyCatalog
{
    public const string FixtureProviderId = "fixture";
    public const string FixtureModelId = "fixture-assembly-v1";
    public const string ServerProviderId = ProductionMixCatalog.ServerProviderId;
    public const string FfmpegModelId = ProductionMixCatalog.FfmpegModelId;
}
