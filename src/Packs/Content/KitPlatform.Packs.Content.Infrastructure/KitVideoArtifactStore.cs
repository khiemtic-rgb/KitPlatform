using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SkiaSharp;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class KitVideoArtifactStore
{
    private readonly string _root;
    private readonly string _masterRoot;
    private readonly string _identityRoot;
    private readonly string _stressRoot;
    public KitVideoArtifactStore(IOptions<ContentOptions> options, IHostEnvironment env)
    {
        var configured = options.Value.KitVideoKeyframeRoot;
        _root = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, configured));
        Directory.CreateDirectory(_root);
        var master = options.Value.KitVideoMasterRoot;
        _masterRoot = Path.IsPathRooted(master)
            ? master
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, master));
        Directory.CreateDirectory(_masterRoot);
        var identity = options.Value.KitVideoIdentityRoot;
        _identityRoot = Path.IsPathRooted(identity)
            ? identity
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, identity));
        Directory.CreateDirectory(_identityRoot);
        var stress = options.Value.KitVideoStressRoot;
        _stressRoot = Path.IsPathRooted(stress)
            ? stress
            : Path.GetFullPath(Path.Combine(env.ContentRootPath, stress));
        Directory.CreateDirectory(_stressRoot);
    }

    public (string Path, byte[] Jpeg, KitVideoArtifactValidation Check) Persist(
        Guid productionId,
        string shotCode,
        int attemptNo,
        byte[] source)
    {
        var jpeg = NormalizeJpeg(source);
        var check = KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        var dir = Path.Combine(_root, productionId.ToString("N"), shotCode);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"attempt-{attemptNo:00}.jpg");
        if (check.Ok)
            File.WriteAllBytes(path, jpeg);
        return (path, jpeg, check);
    }

    public (string Path, byte[] Jpeg, KitVideoArtifactValidation Check) PersistMaster(
        string characterId,
        string era,
        string candidateCode,
        byte[] source)
    {
        using var decoded = SKBitmap.Decode(source)
            ?? throw new InvalidOperationException("ARTIFACT_FAILED: cannot decode image.");
        var max = 1536;
        var scale = Math.Min(1f, Math.Min((float)max / decoded.Width, (float)max / decoded.Height));
        var w = Math.Max(512, (int)(decoded.Width * scale));
        var h = Math.Max(512, (int)(decoded.Height * scale));
        using var dest = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(dest))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, new SKRect(0, 0, w, h));
        }
        using var image = SKImage.FromBitmap(dest);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        var jpeg = data.ToArray();
        var check = KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        var dir = Path.Combine(_masterRoot, characterId, era);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{candidateCode}.jpg");
        if (check.Ok)
            File.WriteAllBytes(path, jpeg);
        return (path, jpeg, check);
    }

    public (string Path, byte[] Jpeg, KitVideoArtifactValidation Check) PersistIdentity(
        string characterId,
        string era,
        Guid testId,
        string variant,
        int attempt,
        byte[] source)
    {
        using var decoded = SKBitmap.Decode(source)
            ?? throw new InvalidOperationException("ARTIFACT_FAILED: cannot decode image.");
        var max = 1536;
        var scale = Math.Min(1f, Math.Min((float)max / decoded.Width, (float)max / decoded.Height));
        var w = Math.Max(512, (int)(decoded.Width * scale));
        var h = Math.Max(512, (int)(decoded.Height * scale));
        using var dest = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(dest))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, new SKRect(0, 0, w, h));
        }
        using var image = SKImage.FromBitmap(dest);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        var jpeg = data.ToArray();
        var check = KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        var dir = Path.Combine(_identityRoot, characterId, era, testId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{variant}-a{attempt:00}.jpg");
        if (File.Exists(path))
            throw new InvalidOperationException("IDENTITY_TEST: artifact immutable — không overwrite.");
        if (check.Ok)
            File.WriteAllBytes(path, jpeg);
        return (path, jpeg, check);
    }

    public (string Path, byte[] Jpeg, KitVideoArtifactValidation Check) PersistStress(
        string characterId,
        string era,
        Guid testId,
        string testCase,
        int attempt,
        byte[] source)
    {
        using var decoded = SKBitmap.Decode(source)
            ?? throw new InvalidOperationException("ARTIFACT_FAILED: cannot decode image.");
        var max = 1536;
        var scale = Math.Min(1f, Math.Min((float)max / decoded.Width, (float)max / decoded.Height));
        var w = Math.Max(512, (int)(decoded.Width * scale));
        var h = Math.Max(512, (int)(decoded.Height * scale));
        using var dest = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(dest))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, new SKRect(0, 0, w, h));
        }
        using var image = SKImage.FromBitmap(dest);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        var jpeg = data.ToArray();
        var check = KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        var dir = Path.Combine(_stressRoot, characterId, era, testId.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{testCase}-a{attempt:00}.jpg");
        if (File.Exists(path))
            throw new InvalidOperationException("STRESS: artifact immutable — không overwrite.");
        if (check.Ok)
            File.WriteAllBytes(path, jpeg);
        return (path, jpeg, check);
    }

    public (string Path, byte[] Bytes) PersistProductionExecution(Guid shotId, Guid executionId, byte[] source)
    {
        if (source is null || source.Length == 0)
            throw new InvalidOperationException("IMAGE_GENERATION_EXECUTION: empty artifact.");
        var dir = Path.Combine(_root, "production-execution", shotId.ToString("N"));
        Directory.CreateDirectory(dir);
        var mime = KitVideoArtifactRules.DetectMime(source) ?? "image/png";
        var ext = mime.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var path = Path.Combine(dir, $"{executionId:N}.{ext}");
        if (File.Exists(path))
            throw new InvalidOperationException("IMAGE_GENERATION_EXECUTION_LOCKED: artifact immutable.");
        File.WriteAllBytes(path, source);
        return (path, source);
    }

    public (string Path, byte[] Bytes) PersistReference(string characterId, string era, Guid packId, string type, byte[] source)
    {
        if (source is null || source.Length < 32)
            throw new InvalidOperationException("CRP_GATE_NOT_SATISFIED: Artifact missing");
        var dir = Path.Combine(_masterRoot, "character-reference-pack", characterId, era, packId.ToString("N"));
        Directory.CreateDirectory(dir);
        var mime = KitVideoArtifactRules.DetectMime(source) ?? "image/png";
        var ext = mime.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var stem = type.Trim().ToUpperInvariant();
        var path = Path.Combine(dir, $"{stem}.{ext}");
        foreach (var leftover in new[] { "jpg", "jpeg", "png", "webp" })
        {
            var other = Path.Combine(dir, $"{stem}.{leftover}");
            if (!other.Equals(path, StringComparison.OrdinalIgnoreCase) && File.Exists(other))
                File.Delete(other);
        }
        File.WriteAllBytes(path, source);
        return (path, source);
    }

    public (string Path, byte[] Bytes) PersistReferenceCandidate(
        string characterId, string era, Guid packId, string type, Guid executionId, byte[] source)
    {
        if (source is null || source.Length < 32)
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: Artifact missing");
        var kind = type.Trim().ToUpperInvariant();
        var dir = Path.Combine(_masterRoot, "character-reference-pack", characterId, era, packId.ToString("N"), "candidates");
        Directory.CreateDirectory(dir);
        var mime = KitVideoArtifactRules.DetectMime(source) ?? "image/png";
        var ext = mime.Contains("png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
        var path = Path.Combine(dir, $"{kind}-{executionId:N}.{ext}");
        if (File.Exists(path))
            throw new InvalidOperationException("REFERENCE_GENERATION_BLOCKED: candidate artifact immutable.");
        File.WriteAllBytes(path, source);
        return (path, source);
    }

    public string PersistReferenceCandidateMeta(string characterId, string era, Guid packId, string type, Guid executionId, string json)
    {
        var kind = type.Trim().ToUpperInvariant();
        var dir = Path.Combine(_masterRoot, "character-reference-pack", characterId, era, packId.ToString("N"), "candidates");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{kind}-{executionId:N}.json");
        File.WriteAllText(path, json);
        return path;
    }

    public IReadOnlyList<string> ListReferenceCandidateMeta(string characterId, string era, Guid packId)
    {
        var dir = Path.Combine(_masterRoot, "character-reference-pack", characterId, era, packId.ToString("N"), "candidates");
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();
    }

    public (string Path, byte[] Jpeg, KitVideoArtifactValidation Check) PersistCalibration(
        string packId,
        string runId,
        string subjectId,
        string view,
        byte[] source)
    {
        if (string.IsNullOrWhiteSpace(runId))
            throw new InvalidOperationException(IdentityConditionedCalibrationLiveV1Rules.GateRun);
        using var decoded = SKBitmap.Decode(source)
            ?? throw new InvalidOperationException("ARTIFACT_FAILED: cannot decode image.");
        var max = 1536;
        var scale = Math.Min(1f, Math.Min((float)max / decoded.Width, (float)max / decoded.Height));
        var w = Math.Max(512, (int)(decoded.Width * scale));
        var h = Math.Max(512, (int)(decoded.Height * scale));
        using var dest = new SKBitmap(w, h, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(dest))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, new SKRect(0, 0, w, h));
        }
        using var image = SKImage.FromBitmap(dest);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        var jpeg = data.ToArray();
        var check = KitVideoArtifactRules.Validate(jpeg, "image/jpeg");
        var dir = Path.Combine(_masterRoot, "CALIBRATION", packId, runId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, IdentityConditionedCalibrationLiveV1Rules.ArtifactFileName(runId, subjectId, view));
        if (File.Exists(path))
            throw new InvalidOperationException(IdentityConditionedCalibrationLiveV1Rules.GateOverwrite);
        if (check.Ok)
            File.WriteAllBytes(path, jpeg);
        return (path, jpeg, check);
    }

    public byte[]? Read(string? path) =>
        string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? null : File.ReadAllBytes(path);

    public static byte[] NormalizeJpeg(byte[] source)
    {
        using var decoded = SKBitmap.Decode(source)
            ?? throw new InvalidOperationException("ARTIFACT_FAILED: cannot decode image.");
        var dest = new SKBitmap(KitVideoArtifactRules.TargetWidth, KitVideoArtifactRules.TargetHeight, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(dest))
        {
            canvas.Clear(SKColors.Black);
            var scale = Math.Min(
                (float)KitVideoArtifactRules.TargetWidth / decoded.Width,
                (float)KitVideoArtifactRules.TargetHeight / decoded.Height);
            var w = decoded.Width * scale;
            var h = decoded.Height * scale;
            var x = (KitVideoArtifactRules.TargetWidth - w) / 2f;
            var y = (KitVideoArtifactRules.TargetHeight - h) / 2f;
            canvas.DrawBitmap(decoded, new SKRect(x, y, x + w, y + h));
        }
        using var image = SKImage.FromBitmap(dest);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 88);
        dest.Dispose();
        return data.ToArray();
    }

    public static byte[] SolidJpeg(SKColor color)
    {
        using var bmp = new SKBitmap(KitVideoArtifactRules.TargetWidth, KitVideoArtifactRules.TargetHeight);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(color);
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 80);
        return data.ToArray();
    }
}
