using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.VisualIntelligence;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class FileVisualIntelligenceGateway : IVisualIntelligenceGateway
{
    private static readonly object Gate = new();
    private readonly string _root;
    private readonly ISharedVisualIntelligence _engine;

    public FileVisualIntelligenceGateway(
        IOptions<ContentOptions> options,
        IHostEnvironment environment,
        ISharedVisualIntelligence engine)
    {
        var configured = string.IsNullOrWhiteSpace(options.Value.VisualIntelligenceRoot)
            ? "App_Data/visual-intelligence"
            : options.Value.VisualIntelligenceRoot.Trim();
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(environment.ContentRootPath, configured);
        _engine = engine;
    }

    public VisualResolution Resolve(ShotVisualDemand demand, VisualResolveOptions? options = null)
    {
        lock (Gate)
        {
            var session = _engine.LoadOrSample(_root);
            var result = _engine.Resolve(session, demand, options ?? VisualResolveOptions.Studio);
            _engine.Save(_root, session);
            return result;
        }
    }

    public byte[]? ReadImage(string assetId)
    {
        if (!VisualHashes.SafeId(assetId)) return null;
        var path = Path.Combine(_root, assetId + ".png");
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public VisualShotLink ReadShot(string filmId, string shotId)
    {
        lock (Gate)
        {
            var session = _engine.TryLoad(_root);
            if (session is null) return VisualShotLink.Missing(filmId, shotId);
            return LinkOf(session, filmId, shotId);
        }
    }

    public VisualShotLink ApproveShot(string filmId, string shotId)
    {
        lock (Gate)
        {
            var session = _engine.TryLoad(_root) ?? throw new InvalidOperationException("Shot chưa có ảnh để duyệt.");
            session.Approve(filmId, shotId);
            _engine.Save(_root, session);
            return LinkOf(session, filmId, shotId);
        }
    }

    private VisualShotLink LinkOf(VisualSession session, string filmId, string shotId)
    {
        var binding = session.FindBinding(filmId, shotId);
        if (binding is null) return VisualShotLink.Missing(filmId, shotId);
        var asset = session.FindAsset(binding.AssetId);
        var hasImage = session.Pixels.ContainsKey(binding.AssetId)
            || File.Exists(Path.Combine(_root, binding.AssetId + ".png"));
        return new VisualShotLink(
            true,
            filmId,
            shotId,
            binding.AssetId,
            binding.Decision,
            binding.Approved,
            binding.BrandCodeAtBind,
            asset?.Sha256 ?? binding.AssetSha256,
            hasImage);
    }
}
