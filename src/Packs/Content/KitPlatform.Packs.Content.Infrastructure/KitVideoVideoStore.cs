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

    /// <summary>
    /// Reads <c>{videoRoot}/{folder}/{fileId}.mp4</c>.
    /// Folder and file id are single safe segments. The resolved path must stay inside the video root.
    /// Empty files and files over 80_000_000 bytes are rejected.
    /// </summary>
    public byte[]? TryReadNamed(string folder, string fileId)
    {
        if (folder is not ("veo" or "fal")) return null;
        if (!IsStoredFileId(fileId)) return null;
        var candidate = Path.Combine(_root, folder, fileId + ".mp4");
        if (!TryContain(candidate, out var full)) return null;
        return ReadBounded(full, 80_000_000);
    }

    private bool TryContain(string? candidate, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        try
        {
            var full = Path.GetFullPath(candidate);
            var root = Path.GetFullPath(_root);
            var prefix = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
            if (ReparseEscapes(full, prefix, root))
                return false;
            fullPath = full;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    private static bool ReparseEscapes(string fullPath, string rootPrefix, string root)
    {
        var current = fullPath;
        while (!string.IsNullOrEmpty(current)
               && current.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                var target = File.ResolveLinkTarget(current, returnFinalTarget: true);
                if (target is null) return true;
                var resolved = Path.GetFullPath(target.FullName);
                if (!resolved.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(resolved, root, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrEmpty(parent)
                || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase)
                || string.Equals(parent, root, StringComparison.OrdinalIgnoreCase))
                break;
            current = parent;
        }

        return false;
    }

    private static bool IsStoredFileId(string fileId)
    {
        if (fileId.Length is < 8 or > 64) return false;
        foreach (var ch in fileId)
        {
            if (!(char.IsAsciiLetterOrDigit(ch) || ch == '-')) return false;
        }

        return true;
    }

    private static byte[]? ReadBounded(string fullPath, int maxBytes)
    {
        if (!File.Exists(fullPath)) return null;
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maxBytes) return null;
        var length = (int)stream.Length;
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var n = stream.Read(buffer, read, length - read);
            if (n == 0) return null;
            read += n;
        }

        if (stream.ReadByte() >= 0) return null;
        return buffer;
    }
}
