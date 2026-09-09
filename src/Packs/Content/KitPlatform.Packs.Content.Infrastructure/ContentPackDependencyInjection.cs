using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

public static class ContentPackDependencyInjection
{
    public static IServiceCollection AddContentPack(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
            services.Configure<ContentOptions>(configuration.GetSection(ContentOptions.SectionName));
        else
            services.AddOptions<ContentOptions>();

        services.AddHttpClient<ContentGeminiClient>(c =>
        {
            // Studio turntable: 4 sequential flash-image calls, each often >120s.
            c.Timeout = TimeSpan.FromMinutes(4);
        });
        services.AddHttpClient("content-publish");
        services.AddHttpClient<ContentCreatomateClient>(c =>
        {
            c.BaseAddress = new Uri("https://api.creatomate.com/");
            c.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddHttpClient<ContentElevenLabsClient>(c =>
        {
            c.BaseAddress = new Uri("https://api.elevenlabs.io/");
            c.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddHttpClient<ContentRunwayClient>(c =>
        {
            c.BaseAddress = new Uri("https://api.dev.runwayml.com/");
            c.Timeout = TimeSpan.FromMinutes(2);
        });
        services.AddHttpClient<ContentFalClient>(c =>
        {
            c.BaseAddress = new Uri("https://queue.fal.run/");
            c.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddHttpClient("content-take-proxy", c =>
        {
            c.Timeout = TimeSpan.FromMinutes(2);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("KitPlatform-Content-TakeProxy/1.0");
        });
        services.AddHttpClient("content-facebook", c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<ContentRepository>();
        services.AddScoped<ContentWorkRepository>();
        services.AddScoped<IContentOrgSettingsService, ContentOrgSettingsService>();
        services.AddScoped<IContentBrandService, ContentBrandService>();
        services.AddScoped<IContentTopicService, ContentTopicService>();
        services.AddScoped<IContentPackageService, ContentPackageService>();
        services.AddScoped<IContentGenerateService, ContentGenerateService>();
        services.AddScoped<IContentLocalOsPublisher, UnconfiguredContentLocalOsPublisher>();
        services.AddScoped<IContentPublishService, ContentPublishService>();
        services.AddScoped<IContentVideoService, ContentVideoService>();
        services.AddScoped<GeminiPictureProvider>();
        services.AddScoped<RunwayMotionProvider>();
        services.AddScoped<WanMotionProvider>();
        services.AddScoped<ElevenLabsVoiceProvider>();
        services.AddScoped<FalLipSyncProvider>();
        services.AddScoped<IFamixaPictureProvider>(sp => sp.GetRequiredService<GeminiPictureProvider>());
        services.AddScoped<IFamixaVoiceProvider>(sp => sp.GetRequiredService<ElevenLabsVoiceProvider>());
        services.AddScoped<IFamixaLipSyncProvider>(sp => sp.GetRequiredService<FalLipSyncProvider>());
        services.AddScoped<FamixaProviderRegistry>();
        services.AddScoped<IFamixaProviderRegistry>(sp => sp.GetRequiredService<FamixaProviderRegistry>());
        services.AddScoped<IFamixaProviderSelectionService, FamixaProviderSelectionService>();
        services.AddScoped<IFamixaProviderAvailability, FamixaProviderAvailability>();
        services.AddScoped<IContentSeriesTurboService, ContentSeriesTurboService>();
        services.AddScoped<IContentSeriesTakeProxyService, ContentSeriesTakeProxyService>();
        services.AddScoped<IContentSeriesAssembleService, ContentSeriesAssembleService>();
        services.AddScoped<IContentSeriesStillService, ContentSeriesStillService>();
        services.AddScoped<IContentSeriesScriptDraftService, ContentSeriesScriptDraftService>();
        services.AddScoped<IContentSeriesPilotService, ContentSeriesPilotService>();
        services.AddScoped<IFamixaCharacterService, FamixaCharacterService>();
        services.AddScoped<IKitVideoEngineService, KitVideoEngineService>();
        services.AddScoped<KitVideoJobRepository>();
        services.AddScoped<IMediaValidator, KitVideoMediaValidator>();
        services.AddScoped<IKitVideoJobService, KitVideoJobService>();
        services.AddScoped<KitVideoAssetRepository>();
        services.AddScoped<KitVideoAssetService>();
        services.AddScoped<IKitVideoAssetService>(sp => sp.GetRequiredService<KitVideoAssetService>());
        services.AddScoped<IAssetResolver>(sp => sp.GetRequiredService<KitVideoAssetService>());
        services.AddScoped<IImagePromptCompiler>(sp => sp.GetRequiredService<KitVideoAssetService>());
        services.AddSingleton<IContinuityValidator, KitVideoContinuityValidator>();
        services.AddScoped<KitVideoContinuityRepository>();
        services.AddScoped<IKitVideoContinuityService, KitVideoContinuityService>();
        services.AddSingleton<IVisualPromptCompiler, KitVideoVisualPromptCompiler>();
        services.AddSingleton<IVisionQa, KitVideoVisionQaEngine>();
        services.AddScoped<IImageGenerator, GeminiImageGenerator>();
        services.AddScoped<IImageVisionAnalyzer, GeminiVisionAnalyzer>();
        services.AddScoped<KitVideoVisionRepository>();
        services.AddScoped<IKitVideoVisionService, KitVideoVisionService>();
        services.AddScoped<KitVideoArtifactStore>();
        services.AddScoped<IKitVideoPixelService, KitVideoPixelService>();
        services.AddScoped<KitVideoMotionRepository>();
        services.AddScoped<KitVideoVideoStore>();
        services.AddSingleton<IRunwayRequestCompiler, KitVideoRunwayCompiler>();
        services.AddSingleton<IVideoOutputQA, KitVideoOutputQaEngine>();
        services.AddScoped<IKitVideoMotionService, KitVideoMotionService>();
        services.AddScoped<KitVideoVisualSystemRepository>();
        services.AddScoped<IKitVideoVisualSystemService, KitVideoVisualSystemService>();
        services.AddScoped<IProjectVisualStyleAuthority, ProjectVisualStyleAuthority>();
        services.AddScoped<IVisualUniverseAuthority, VisualUniverseAuthority>();
        services.AddScoped<IVisualUniverseSnapshotResolver, VisualUniverseSnapshotResolver>();
        services.AddScoped<IUnifiedVisualCompiler, UnifiedVisualCompiler>();
        services.AddScoped<IVisualCalibrationPackService, VisualCalibrationPackService>();
        services.AddScoped<IIdentityConditionedCalibrationDirectorReviewService, IdentityConditionedCalibrationDirectorReviewService>();
        services.AddScoped<IVisualCalibrationGenerationProvider, VisualCalibrationGenerationProvider>();
        services.AddScoped<KitVideoMasterReferenceRepository>();
        services.AddScoped<IKitVideoMasterReferenceService, KitVideoMasterReferenceService>();
        services.AddScoped<KitVideoIdentityTestRepository>();
        services.AddScoped<IKitVideoIdentityTestService, KitVideoIdentityTestService>();
        services.AddScoped<KitVideoIdentityStressRepository>();
        services.AddScoped<IKitVideoIdentityStressService, KitVideoIdentityStressService>();
        services.AddScoped<KitVideoMasterReviewRepository>();
        services.AddScoped<KitVideoMasterLockRepository>();
        services.AddScoped<IKitVideoMasterReviewService, KitVideoMasterReviewService>();
        services.AddScoped<KitVideoCharacterDnaRepository>();
        services.AddScoped<IKitVideoCharacterDnaService, KitVideoCharacterDnaService>();
        services.AddScoped<KitVideoProductionReferencePackRepository>();
        services.AddScoped<IKitVideoProductionReferencePackService, KitVideoProductionReferencePackService>();
        services.AddScoped<CharacterReferencePackRepository>();
        services.AddScoped<ICharacterReferencePackService, CharacterReferencePackService>();
        services.AddScoped<ICharacterReferenceGenerationService, CharacterReferenceGenerationService>();
        services.AddScoped<ICharacterReferenceGenerationProvider, GeminiCharacterReferenceGenerationProvider>();
        services.AddScoped<ICharacterReferenceAutoGenerationV2Service, CharacterReferenceAutoGenerationV2Service>();
        services.AddScoped<CharacterAuthorityStore>();
        services.AddScoped<ICharacterAuthorityGenerationProvider, GeminiCharacterAuthorityGenerationProvider>();
        services.AddScoped<ICharacterAuthorityInitializationV1Service, CharacterAuthorityInitializationV1Service>();
        services.AddScoped<ICharacterAuthorityPipelineV1Service, CharacterAuthorityPipelineV1Service>();
        services.AddScoped<ICharacterReferenceRegenerationV1Service, CharacterReferenceRegenerationV1Service>();
        services.AddSingleton<ICharacterReferenceSelector, CharacterReferenceSelector>();
        services.AddScoped<ICharacterGenerationProvider, GeminiCharacterGenerationProvider>();
        services.AddScoped<ICharacterStudioIdentityJudge, GeminiCharacterStudioIdentityJudge>();
        services.AddScoped<ICharacterAgeConsistencyEvaluator, DeferredCharacterAgeConsistencyEvaluator>();
        services.AddScoped<ICharacterStudioOrchestrator, CharacterStudioOrchestrator>();
        services.AddScoped<ICharacterStudioGenerationService, CharacterStudioGenerationService>();
        services.AddScoped<ICharacterProductionLibraryService, CharacterProductionLibraryService>();
        services.AddScoped<IProductionProgressService, ProductionProgressService>();
        services.AddScoped<KitVideoProductionShotRepository>();
        services.AddScoped<IKitVideoProductionShotService, KitVideoProductionShotService>();
        services.AddScoped<CharacterIdentityGovernanceRepository>();
        services.AddScoped<ICharacterIdentityGovernanceService, CharacterIdentityGovernanceService>();
        services.AddScoped<ProductionShotContractRepository>();
        services.AddScoped<IProductionShotContractService, ProductionShotContractService>();
        services.AddScoped<ProductionPromptCompilerRepository>();
        services.AddScoped<IProductionPromptCompiler, ProductionPromptCompilerService>();
        services.AddScoped<ImageGenerationContractRepository>();
        services.AddScoped<IImageGenerationContractService, ImageGenerationContractService>();
        services.AddScoped<ImageGenerationExecutionRepository>();
        services.AddScoped<IGeminiImageGenerationProvider, GeminiImageGenerationProvider>();
        services.AddScoped<IImageGenerationExecutionService, ImageGenerationExecutionService>();
        services.AddScoped<IFirstRealProductionService, FirstRealProductionService>();
        services.AddScoped<ImageGenerationDirectorReviewRepository>();
        services.AddScoped<IImageGenerationDirectorReviewService, ImageGenerationDirectorReviewService>();
        services.AddScoped<ProductionVideoContractRepository>();
        services.AddScoped<IProductionVideoContractService, ProductionVideoContractService>();
        services.AddScoped<VideoGenerationExecutionRepository>();
        services.AddScoped<IVideoGenerationProvider, RunwayVideoGenerationProvider>();
        services.AddScoped<IVideoGenerationExecutionService, VideoGenerationExecutionService>();
        services.AddScoped<ProductionOsService>();
        services.AddScoped<IProductionOsService>(sp => sp.GetRequiredService<ProductionOsService>());
        services.AddScoped<IProductionProviderSelector>(sp => sp.GetRequiredService<ProductionOsService>());
        services.AddScoped<IContentOpsService, ContentOpsService>();
        services.AddScoped<IContentWorkQueueService, ContentWorkQueueService>();
        services.AddScoped<ContentFacebookClient>();
        services.AddScoped<IContentFacebookConnectionService, ContentFacebookConnectionService>();
        services.AddHostedService<ContentWorkWorker>();
        return services;
    }
}

internal sealed class UnconfiguredContentLocalOsPublisher : IContentLocalOsPublisher
{
    public Task<ContentLocalOsPublishResult> PublishArticleAsync(
        ContentLocalOsPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = request;
        _ = cancellationToken;
        throw new InvalidOperationException("Chưa gắn publisher Thái Nguyên Life trên API.");
    }
}
