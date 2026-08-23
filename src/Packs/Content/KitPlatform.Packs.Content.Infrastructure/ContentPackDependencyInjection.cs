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
            // Pollinations free image gen can take 30–90s.
            c.Timeout = TimeSpan.FromMinutes(2);
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
        services.AddScoped<IContentSeriesTurboService, ContentSeriesTurboService>();
        services.AddScoped<IContentSeriesTakeProxyService, ContentSeriesTakeProxyService>();
        services.AddScoped<IContentSeriesAssembleService, ContentSeriesAssembleService>();
        services.AddScoped<IContentSeriesStillService, ContentSeriesStillService>();
        services.AddScoped<IContentSeriesScriptDraftService, ContentSeriesScriptDraftService>();
        services.AddScoped<IContentSeriesPilotService, ContentSeriesPilotService>();
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
