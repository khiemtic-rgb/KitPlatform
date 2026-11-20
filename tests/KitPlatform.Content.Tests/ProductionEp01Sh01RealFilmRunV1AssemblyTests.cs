using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class ProductionEp01Sh01RealFilmRunV1AssemblyTests
{
    [Fact]
    public void Real_film_run_v1_assemble_is_one_ffmpeg_remux()
    {
        var fail = ProductionEp01Sh01RealFilmRunV1AssemblyRegression.Run();
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }
}
