using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Đăng ký M1 "Dựng tập từ bảng câu". Gọi một dòng từ <c>AddContentPack</c>.
/// Không thêm vào <c>IEnumerable&lt;IMotionExecutionAdapter&gt;</c> hay bất kỳ danh sách dùng chung nào.
/// </summary>
public static class FamixaLinesheetDependencyInjection
{
    public static IServiceCollection AddFamixaLinesheet(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
            services.Configure<FamixaLinesheetOptions>(configuration.GetSection(FamixaLinesheetOptions.SectionName));
        else
            services.AddOptions<FamixaLinesheetOptions>();

        services.AddSingleton<IFamixaLinesheetFileStore, FamixaLinesheetFileStore>();
        services.AddSingleton<ILinesheetMediaTools, ProcessLinesheetMediaTools>();
        services.AddSingleton<ILinesheetLayerComposer, SkiaLinesheetLayerComposer>();
        services.AddScoped<IFamixaLinesheetStore, FamixaLinesheetRepository>();
        services.AddHttpClient(ElevenLabsLinesheetTts.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(2));
        services.AddScoped<ILinesheetTts, ElevenLabsLinesheetTts>();
        services.AddHttpClient(RunwayLinesheetApi.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(2));
        services.AddScoped(sp => new RunwayLinesheetApi(
            sp.GetRequiredService<ContentRunwayClient>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetService<IProductionCredentialResolver>()));
        services.AddScoped<ILinesheetVideoGen>(sp => new RunwayLinesheetVideoGen(sp.GetRequiredService<RunwayLinesheetApi>()));
        services.AddScoped<ILinesheetImageGen>(sp => new RunwayLinesheetImageGen(sp.GetRequiredService<RunwayLinesheetApi>()));
        services.AddHttpClient(FalLinesheetLipsync.HttpClientName, c => c.Timeout = TimeSpan.FromMinutes(2));
        services.AddScoped<ILinesheetLipsync>(sp => new FalLinesheetLipsync(
            sp.GetRequiredService<ContentFalClient>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetService<IProductionCredentialResolver>()));
        services.AddScoped<IFamixaLinesheetService>(sp => new FamixaLinesheetService(
            sp.GetRequiredService<IFamixaLinesheetStore>(),
            sp.GetRequiredService<IFamixaLinesheetFileStore>(),
            sp.GetRequiredService<ILinesheetMediaTools>(),
            sp.GetRequiredService<IProductionCallerTenant>(),
            sp.GetRequiredService<ILinesheetTts>(),
            sp.GetRequiredService<ILinesheetLayerComposer>(),
            sp.GetRequiredService<ILinesheetVideoGen>(),
            sp.GetRequiredService<ILinesheetLipsync>(),
            sp.GetRequiredService<ILinesheetImageGen>()));
        services.AddScoped<IFamixaLinesheetRenderer>(sp => new FamixaLinesheetRenderer(
            sp.GetRequiredService<IFamixaLinesheetStore>(),
            sp.GetRequiredService<IFamixaLinesheetFileStore>(),
            sp.GetRequiredService<ILinesheetMediaTools>(),
            sp.GetRequiredService<ILinesheetLayerComposer>(),
            TimeProvider.System));
        services.AddHostedService<FamixaLinesheetRenderWorker>();
        return services;
    }
}
