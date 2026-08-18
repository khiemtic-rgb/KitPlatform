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
        services.AddHttpClient("content-facebook", c => c.Timeout = TimeSpan.FromSeconds(30));

        services.AddScoped<ContentRepository>();
        services.AddScoped<ContentWorkRepository>();
        services.AddScoped<IContentOrgSettingsService, ContentOrgSettingsService>();
        services.AddScoped<IContentBrandService, ContentBrandService>();
        services.AddScoped<IContentTopicService, ContentTopicService>();
        services.AddScoped<IContentPackageService, ContentPackageService>();
        services.AddScoped<IContentGenerateService, ContentGenerateService>();
        services.AddScoped<IContentPublishService, ContentPublishService>();
        services.AddScoped<IContentVideoService, ContentVideoService>();
        services.AddScoped<IContentOpsService, ContentOpsService>();
        services.AddScoped<IContentWorkQueueService, ContentWorkQueueService>();
        services.AddScoped<ContentFacebookClient>();
        services.AddScoped<IContentFacebookConnectionService, ContentFacebookConnectionService>();
        services.AddHostedService<ContentWorkWorker>();
        return services;
    }
}
