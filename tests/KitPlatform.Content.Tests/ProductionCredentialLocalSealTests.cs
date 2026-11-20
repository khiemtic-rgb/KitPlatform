using KitPlatform.Packs.Content.Infrastructure;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionCredentialLocalSealTests
{
    [Fact]
    public void Seal_requires_windows_dpapi()
    {
        var vault = Path.Combine(Path.GetTempPath(), "kit-seal-" + Guid.NewGuid().ToString("N"));
        var material = new byte[ProductionCredentialSecretEnvelope.KeySize];
        material[0] = 1;
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                var ex = Assert.Throws<PlatformNotSupportedException>(() =>
                    ProductionCredentialLocalSeal.Match("v1", material, out _, vault));
                Assert.Contains("Windows DPAPI", ex.Message, StringComparison.Ordinal);
                return;
            }

            var result = ProductionCredentialLocalSeal.Match("v1", material, out var openedId, vault);
            Assert.Equal(ProductionCredentialBackupEvidence.RestoreOk, result);
            Assert.Equal("v1", openedId);
        }
        finally
        {
            if (Directory.Exists(vault))
                Directory.Delete(vault, true);
        }
    }
}
