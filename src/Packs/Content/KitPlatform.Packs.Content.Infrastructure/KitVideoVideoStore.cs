using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoVideoStore
{
    private readonly string _root;
    public KitVideoVideoStore(IOptions<ContentOptions> options, IHostEnvironment env)
    {
        var configured = options.Value.KitVideoVideoRoot;
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
    }

    public string Persist(Guid productionId, string shotCode, int attemptNo, byte[] bytes)
    {
        var dir = Path.Combine(_root, productionId.ToString("N"), shotCode);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"take-{attemptNo:00}.mp4");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public string PersistProductionExecution(Guid shotId, Guid executionId, byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("VIDEO_GENERATION_EXECUTION: empty artifact.");
        var dir = Path.Combine(_root, "production-execution", shotId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{executionId:N}.mp4");
        if (File.Exists(path))
            throw new InvalidOperationException("VIDEO_GENERATION_EXECUTION_LOCKED: artifact immutable.");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    public byte[]? Read(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : File.ReadAllBytes(path);
}
