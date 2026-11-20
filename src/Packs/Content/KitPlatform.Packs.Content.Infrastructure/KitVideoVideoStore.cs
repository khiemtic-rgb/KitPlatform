using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

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

    public string PersistNamed(string folder, string fileId, byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0)
            throw new InvalidOperationException("VEO_TAKE: empty artifact.");
        var segments = SafeSegments(folder);
        var safeId = ProductionStorageFileNames.Stem(fileId);
        if (segments.Length == 0 || safeId.Length < 8)
            throw new InvalidOperationException("VEO_TAKE: invalid file id.");
        var dir = Path.Combine(new[] { _root }.Concat(segments).ToArray());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{safeId}.mp4");
        ProductionArtifactPhysicalWrite.CommitFile(path, bytes);
        return path;
    }

    public byte[]? TryReadNamed(string folder, string fileId)
    {
        var segments = SafeSegments(folder);
        var names = ProductionStorageFileNames.Candidates(fileId);
        if (segments.Length == 0 || names.Count == 0) return null;
        foreach (var name in names)
        {
            if (name.Contains("..", StringComparison.Ordinal) || name.Contains('/') || name.Contains('\\'))
                continue;
            var path = Path.Combine(new[] { _root }.Concat(segments).Append(name).ToArray());
            if (File.Exists(path)) return File.ReadAllBytes(path);
        }
        return null;
    }

    static string[] SafeSegments(string? folder) =>
        (folder ?? "veo").Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => string.Concat(s.Where(ch => char.IsLetterOrDigit(ch) || ch == '-')))
            .Where(s => s.Length > 0)
            .ToArray();

}
