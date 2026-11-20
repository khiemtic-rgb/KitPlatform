using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionTestKeyframeManifest(
    string FixtureId,
    string Filename,
    string Sha256,
    string Mime,
    int Width,
    int Height,
    int ByteLength,
    string ScopeKind,
    string Purpose,
    bool TestOnly);

public sealed record ProductionTestKeyframeFixture(
    ProductionTestKeyframeManifest Manifest,
    string Path,
    byte[] Bytes);

public static class ProductionTestKeyframeCatalog
{
    public const string FixtureId = "TEST_KEYFRAME_V1";
    public const string RelativeDir = "testdata/famixa-production-os";
    public const string ManifestFile = "manifest.json";
    public const string ImageFile = "TEST_ONLY_keyframe_v1.png";

    public static ProductionTestKeyframeFixture Load(string fixtureId = FixtureId)
    {
        var dir = ResolveDirectory();
        var manifestPath = Path.Combine(dir, ManifestFile);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);
        var manifest = JsonSerializer.Deserialize<ProductionTestKeyframeManifest>(
            File.ReadAllText(manifestPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);
        if (!string.Equals(manifest.FixtureId, fixtureId, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);
        var imagePath = Path.Combine(dir, manifest.Filename);
        if (!File.Exists(imagePath))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);
        var bytes = File.ReadAllBytes(imagePath);
        Verify(manifest, bytes);
        return new ProductionTestKeyframeFixture(manifest, imagePath, bytes);
    }

    public static void Verify(ProductionTestKeyframeManifest manifest, byte[] bytes)
    {
        if (!string.Equals(manifest.ScopeKind, ProductionScopeAnchorRules.Test, StringComparison.Ordinal)
            || !manifest.TestOnly
            || !string.Equals(manifest.Purpose, "CREATE_VIDEO_E2E", StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);
        if (!string.Equals(manifest.Mime, "image/png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureMimeInvalid);
        if (!ProductionTestKeyframePng.HasPngSignature(bytes))
            throw new InvalidOperationException(ProductionFailureReasons.FixtureMimeInvalid);
        if (bytes.Length != manifest.ByteLength)
            throw new InvalidOperationException(ProductionFailureReasons.FixtureHashMismatch);
        var sha = KitPlatform.Packs.Content.KitVideoIntegrityRules.Sha256Hex(bytes);
        if (!string.Equals(sha, manifest.Sha256, StringComparison.OrdinalIgnoreCase) || sha.Length != 64)
            throw new InvalidOperationException(ProductionFailureReasons.FixtureHashMismatch);
        if (!ProductionTestKeyframePng.TryReadPngSize(bytes, out var w, out var h)
            || w != manifest.Width || h != manifest.Height
            || w < 16 || h < 16)
            throw new InvalidOperationException(ProductionFailureReasons.FixtureDimensionsInvalid);
    }

    public static string ResolveDirectory()
    {
        var env = Environment.GetEnvironmentVariable("FAMIXA_TESTDATA");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var hit = Path.Combine(dir.FullName, "testdata", "famixa-production-os");
            if (File.Exists(Path.Combine(hit, ManifestFile)))
                return hit;
            if (Directory.Exists(Path.Combine(dir.FullName, "migrations"))
                && File.Exists(Path.Combine(dir.FullName, "deploy", "ubuntu", "migration-files.content.txt")))
            {
                var rooted = Path.Combine(dir.FullName, "testdata", "famixa-production-os");
                if (File.Exists(Path.Combine(rooted, ManifestFile)))
                    return rooted;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException(ProductionFailureReasons.FixtureNotFound);
    }
}

public static class ProductionTestSandboxIds
{
    public static readonly Guid TestRunId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000371");
    public static readonly Guid ScopeId = Guid.Parse("bbbbbbbb-0000-4000-8000-000000000371");
    public const string ShotCode = "TEST-SC01-SH01";
}
