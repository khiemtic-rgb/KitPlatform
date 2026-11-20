using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;
using KitPlatform.Packs.Content.VisualIntelligence;

namespace KitPlatform.Packs.Content.Infrastructure;

public static class ContentPackDependencyInjection
{
    public static IServiceCollection AddContentPack(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<ContentOptions>(configuration.GetSection(ContentOptions.SectionName));
            services.Configure<TopicScoutOptions>(configuration.GetSection(TopicScoutOptions.SectionName));
        }
        else
        {
            services.AddOptions<ContentOptions>();
            services.AddOptions<TopicScoutOptions>();
        }

        if (configuration is not null)
            services.Configure<ContentTelegramOptions>(configuration.GetSection(ContentTelegramOptions.SectionName));
        else
            services.AddOptions<ContentTelegramOptions>();

        services.AddHttpClient<ContentGeminiClient>(c =>
        {
            // Studio turntable: 4 sequential flash-image calls, each often >120s.
            c.Timeout = TimeSpan.FromMinutes(4);
        });
        services.AddScoped<INgvHinhPlanService, NgvHinhPlanService>();
        services.AddHttpClient("content-publish");
        services.AddHttpClient("content-remote", c => c.Timeout = TimeSpan.FromMinutes(20));
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
        services.AddHttpClient<ContentVeoClient>(c =>
        {
            c.Timeout = TimeSpan.FromMinutes(4);
        });
        services.AddHttpClient("content-take-proxy", c =>
        {
            c.Timeout = TimeSpan.FromMinutes(2);
            c.DefaultRequestHeaders.UserAgent.ParseAdd("KitPlatform-Content-TakeProxy/1.0");
        });
        services.AddHttpClient("content-facebook", c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient("content-youtube", c => c.Timeout = TimeSpan.FromMinutes(30));
        services.AddHttpClient("content-topic-scout", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient("content-tiktok", c => c.Timeout = TimeSpan.FromMinutes(30));
        // Bot token nằm trong đường dẫn URL: tắt logger mặc định (log "POST https://api.telegram.org/bot<token>/...").
        services.AddHttpClient(ContentTelegramSender.ClientName, c =>
            {
                c.BaseAddress = new Uri("https://api.telegram.org/");
                c.Timeout = TimeSpan.FromSeconds(20);
            })
            .RemoveAllLoggers();

        services.AddScoped<ContentRepository>();
        services.AddScoped<ContentWorkRepository>();
        services.AddScoped<ContentArticleSeriesRepository>();
        services.AddScoped<IContentArticleSeriesService, ContentArticleSeriesService>();
        services.AddScoped<IContentOrgSettingsService, ContentOrgSettingsService>();
        services.AddScoped<IContentBrandService, ContentBrandService>();
        services.AddScoped<IContentTopicService, ContentTopicService>();
        services.AddScoped<IContentPackageService, ContentPackageService>();
        services.AddScoped<IContentGenerateService, ContentGenerateService>();
        services.AddScoped<IContentLocalOsPublisher, UnconfiguredContentLocalOsPublisher>();
        services.AddScoped<IContentPublishService, ContentPublishService>();
        services.AddScoped<IContentPublishPlanStore, ContentPublishPlanStore>();
        services.AddScoped<IContentScheduleAdvisor, ContentScheduleAdvisor>();
        services.AddScoped<IContentScheduleProposalService, ContentScheduleProposalService>();
        services.AddScoped<IContentAutopilotService, ContentAutopilotService>();
        services.AddScoped<ITopicScoutService, TopicScoutService>();
        services.AddScoped<IContentMediaCleanupService, ContentMediaCleanupService>();
        services.AddScoped<ContentTenantGeminiKeySource>();
        services.AddScoped<IContentVideoService, ContentVideoService>();
        services.AddScoped<GeminiPictureProvider>();
        services.AddScoped<RunwayMotionProvider>();
        services.AddScoped<WanMotionProvider>();
        services.AddScoped<VeoMotionProvider>();
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
        services.AddScoped<IContentFilmTypeService, ContentFilmTypeService>();
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
        services.AddSingleton<ISharedVisualIntelligence, SharedVisualIntelligence>();
        services.AddScoped<IVisualIntelligenceGateway, FileVisualIntelligenceGateway>();
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
        services.AddScoped<IGeminiProductionStillClient, GeminiProductionStillClient>();
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

        services.AddScoped<IProductionCallerTenant, HttpProductionCallerTenant>();

        services.AddScoped<IProductionWorkStore, ProductionWorkRepository>();
        services.AddScoped<IProductionArtifactFileStore, ProductionArtifactFileStore>();
        services.AddScoped<IProductionScopeFoundationStore, ProductionScopeFoundationRepository>();
        services.AddScoped<IProductionExecutionPermitStore, ProductionExecutionPermitRepository>();
        services.AddScoped<IProductionAiSelectionStore, ProductionAiSelectionRepository>();
        services.AddScoped<IPerformanceSnapshotStore, PerformanceSnapshotRepository>();

        services.AddSingleton<IProductionCredentialKeyRing, ConfigurationProductionCredentialKeyRing>();
        services.AddScoped<IProductionCredentialStore, ProductionCredentialRepository>();
        services.AddScoped<IProductionCredentialSecretStore, ProductionCredentialSecretRepository>();
        services.AddScoped<IProductionCredentialRotation, ProductionCredentialRotationRepository>();
        services.AddScoped<IProductionCredentialRegistration, ProductionCredentialRegistrationService>();
        services.AddScoped<IProductionCredentialResolver, ProductionCredentialResolver>();

        // Fixture must be the last IMotionExecutionAdapter: the single-instance injection is the non-official default.
        // google/veo-3.1 uses HttpVeoMotionTransport. The token client is real HTTPS.
        // Registration does not call Vertex. AuthorizeFrozenAsync runs only for a frozen tenant credential.
        // RealVertexExecutionOpen stays false; the CK live probe is a separate gate.
        services.AddScoped<IRunwayTransport, HttpRunwayTransport>();
        services.AddScoped<IMotionExecutionAdapter, RunwayMotionExecutionAdapter>();
        services.AddHttpClient(ProductionVeoHttpClientPolicy.Name, c =>
        {
            c.Timeout = ProductionVeoHttpClientPolicy.RequestTimeout;
        });
        services.AddSingleton<IVeoJwtBearerTokenClient>(sp =>
            new HttpVeoJwtBearerTokenClient(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient(ProductionVeoHttpClientPolicy.Name)));
        services.AddSingleton<IVeoHttpAuthenticator>(sp =>
            new ProductionVeoHttpAuthenticator(null, sp.GetRequiredService<IVeoJwtBearerTokenClient>()));
        services.AddSingleton(new VeoHttpSettings("tenant-veo-closed"));
        services.AddHttpClient<IVeoMotionTransport, HttpVeoMotionTransport>();
        services.AddScoped<IMotionExecutionAdapter, VeoMotionExecutionAdapter>();
        services.AddScoped<IMotionExecutionAdapter, FixtureMotionAdapter>();
        services.AddScoped<IProductionMotionAdapterResolver, ProductionMotionAdapterResolver>();
        services.AddScoped<IProductionPaidNetworkPermit, ProductionPaidNetworkOneShotPermit>();
        // Concrete paid adapters only. IVoiceExecutionAdapter and ILipSyncExecutionAdapter stay unregistered
        // so an unpaid call keeps the fixture default.
        services.AddScoped<IElevenLabsVoiceTransport, HttpElevenLabsVoiceTransport>();
        services.AddScoped<ElevenLabsVoiceExecutionAdapter>();
        services.AddScoped<IFalLipsyncTransport, HttpFalLipsyncTransport>();
        services.AddScoped<FalLipSyncExecutionAdapter>();
        services.AddHttpClient<HttpGeminiTenantImageTransport>();
        services.AddScoped<IGeminiTenantImageTransport>(sp => sp.GetRequiredService<HttpGeminiTenantImageTransport>());
        services.AddScoped<IProductionLockedMasterSource, ProductionLockedMasterSource>();
        services.AddScoped<TenantGeminiImageExecutionAdapter>();

        services.AddSingleton<ProductionRunwayHttpAudit>();
        services.AddSingleton<ProductionElevenLabsHttpAudit>();
        services.AddSingleton<ProductionFalHttpAudit>();
        services.AddSingleton<ProductionFfmpegMixAudit>();
        services.AddScoped<IFfmpegMixTransport, ProcessFfmpegMixTransport>();
        services.AddScoped<FfmpegMixExecutionAdapter>();
        services.AddScoped<FfmpegAssemblyExecutionAdapter>();
        services.AddScoped<FfmpegEpisodeAssemblyExecutionAdapter>();

        services.AddScoped<IProductionTenantAiConfigStore, ProductionTenantAiConfigRepository>();
        services.AddScoped<IProductionTenantAiCatalog, ProductionTenantAiCatalog>();
        services.AddScoped<IProductionTenantAiSettings, ProductionTenantAiSettingsService>();
        services.AddScoped<IProductionTenantAiResolution, ProductionTenantAiResolution>();

        services.AddScoped<IPerformanceContractService, PerformanceContractService>();
        services.AddScoped<IProductionExecutionService, ProductionExecutionService>();
        services.AddScoped<IProductionReadinessAuthority, ProductionReadinessAuthority>();
        services.AddScoped<IProductionTestFoundationService, ProductionTestFoundationService>();
        services.AddScoped<IProductionTestKeyframeService, ProductionTestKeyframeService>();
        services.AddScoped<IProductionCreateVideoService, ProductionCreateVideoService>();
        services.AddScoped<IContentOpsService, ContentOpsService>();
        services.AddScoped<IContentWorkQueueService, ContentWorkQueueService>();
        services.AddScoped<ContentFacebookClient>();
        services.AddScoped<IContentFacebookConnectionService, ContentFacebookConnectionService>();
        services.AddScoped<ContentYoutubeClient>();
        services.AddScoped<IContentYoutubeConnectionService, ContentYoutubeConnectionService>();
        services.AddScoped<ContentTiktokClient>();
        services.AddScoped<IContentTiktokConnectionService, ContentTiktokConnectionService>();
        services.AddScoped<IContentVariantImportService, ContentVariantImportService>();
        services.AddScoped<IContentPublishExtrasService, ContentPublishExtrasService>();
        services.AddScoped<ContentArticleCoverService>();
        services.AddScoped<ContentFirstCommentStore>();
        services.AddScoped<ContentFirstCommentService>();
        services.AddScoped<ContentPlatformPostStore>();
        services.AddScoped<IContentPlatformSyncService, ContentPlatformSyncService>();
        services.AddScoped<ContentTelegramStore>();
        services.AddScoped<ContentTelegramSender>();
        services.AddScoped<ContentTelegramNotifier>();
        services.AddScoped<IContentTelegramSettingsService, ContentTelegramSettingsService>();
        services.AddScoped<IBtaKitMktMediaService, BtaKitMktMediaService>();
        services.AddFamixaLinesheet(configuration);
        services.AddHostedService<NovixaE2eIsolationGuard>();
        services.AddHostedService<ContentWorkWorker>();
        services.AddHostedService<ContentPublishDueWorker>();
        services.AddHostedService<ContentAutopilotWorker>();
        services.AddHostedService<ContentMediaCleanupWorker>();
        services.AddHostedService<ContentFirstCommentWorker>();
        services.AddHostedService<ContentPlatformSyncWorker>();
        services.AddHostedService<ContentTelegramWorker>();
        services.AddHostedService<TopicScoutWorker>();
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
