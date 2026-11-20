using KitPlatform.Packs.Content;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Armed only when NOVIXA_E2E_DATABASE is set on the test API process.
/// Refuses business databases and external media providers before any job write.
/// </summary>
internal static class NovixaE2eIsolation
{
    public const string DatabaseVariable = "NOVIXA_E2E_DATABASE";
    public const string PortVariable = "NOVIXA_E2E_PORT";
    public const string AssetVariable = "NOVIXA_E2E_ASSET_ROOT";

    private static readonly string[] ForbiddenDatabases =
    [
        "kitplatform",
        "kitplatform_test",
        "pharmacore",
        "postgres",
    ];

    private static readonly string[] ProviderEnvironmentVariables =
    [
        "ELEVENLABS_API_KEY",
        "CREATOMATE_API_KEY",
        "RUNWAY_API_KEY",
        "FAL_KEY",
        "GEMINI_API_KEY",
        "GOOGLE_API_KEY",
        "GOOGLE_GENERATIVE_AI_API_KEY",
        "Content__ElevenLabsApiKey",
        "Content__CreatomateApiKey",
        "Content__RunwayApiKey",
        "Content__GeminiApiKey",
        "Content__FalApiKey",
    ];

    public static bool IsArmed =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DatabaseVariable));

    public static async Task AssertAsync(
        ContentRepository repo,
        string? configuredAssetRoot,
        CancellationToken cancellationToken)
    {
        if (!IsArmed)
            return;

        var expectedDb = Required(DatabaseVariable);
        var expectedPort = RequiredPort();
        var expectedAsset = Required(AssetVariable);
        if (IsForbidden(expectedDb))
            throw new InvalidOperationException("NOVIXA E2E từ chối database nghiệp vụ đã có.");
        if (expectedPort == 5432)
            throw new InvalidOperationException("NOVIXA E2E từ chối cổng 5432 của cụm nghiệp vụ.");

        foreach (var name in ProviderEnvironmentVariables)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)))
                throw new InvalidOperationException("NOVIXA E2E từ chối biến nhà cung cấp " + name + ".");
        }

        var (database, port) = await repo.ReadServerIdentityAsync(cancellationToken);
        if (IsForbidden(database))
            throw new InvalidOperationException("NOVIXA E2E đang nối vào database nghiệp vụ.");
        if (!string.Equals(database, expectedDb, StringComparison.Ordinal))
            throw new InvalidOperationException("NOVIXA E2E sai database đang mở.");
        if (port != expectedPort)
            throw new InvalidOperationException("NOVIXA E2E sai cổng PostgreSQL.");

        var actualAsset = Path.GetFullPath(
            ContentVideoMediaPaths.ResolveRoot(configuredAssetRoot, Directory.GetCurrentDirectory()));
        var requiredAsset = Path.GetFullPath(expectedAsset);
        if (!string.Equals(actualAsset, requiredAsset, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("NOVIXA E2E sai thư mục media.");
        if (!Directory.Exists(requiredAsset))
            throw new InvalidOperationException("NOVIXA E2E chưa có thư mục media.");
    }

    public static void ThrowIfExternalCall(string operation)
    {
        if (!IsArmed)
            return;
        throw new InvalidOperationException("NOVIXA E2E chặn gọi nhà cung cấp ngoài: " + operation);
    }

    private static string Required(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("NOVIXA E2E thiếu " + name + ".");
        return value.Trim();
    }

    private static int RequiredPort()
    {
        var text = Required(PortVariable);
        if (!int.TryParse(text, out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("NOVIXA E2E port không hợp lệ.");
        return port;
    }

    private static bool IsForbidden(string database) =>
        ForbiddenDatabases.Contains(database, StringComparer.OrdinalIgnoreCase);
}

internal sealed class NovixaE2eIsolationGuard : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ContentOptions> _options;

    public NovixaE2eIsolationGuard(IServiceScopeFactory scopes, IOptions<ContentOptions> options)
    {
        _scopes = scopes;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!NovixaE2eIsolation.IsArmed)
            return;
        await using var scope = _scopes.CreateAsyncScope();
        var repo = scope.ServiceProvider.GetRequiredService<ContentRepository>();
        await NovixaE2eIsolation.AssertAsync(repo, _options.Value.VideoAssetRoot, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
