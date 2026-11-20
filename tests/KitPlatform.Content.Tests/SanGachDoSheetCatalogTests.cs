using KitPlatform.Packs.Content.Application;
using Xunit;

namespace KitPlatform.Content.Tests;

public class SanGachDoSheetCatalogTests
{
    [Fact]
    public void Read_groups_turnaround_face_and_sit_and_keeps_a_prop()
    {
        var root = Path.Combine(Path.GetTempPath(), "sgd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "Bo_goc.png"), [1]);
            File.WriteAllBytes(Path.Combine(root, "Bo_mat.png"), [1]);
            File.WriteAllBytes(Path.Combine(root, "Bo_ngoi.png"), [1]);
            File.WriteAllBytes(Path.Combine(root, "DAOCU_mu_la_co.png"), [1]);

            var rows = SanGachDoSheetCatalog.Read(root);

            Assert.Equal(2, rows.Count);
            var bo = Assert.Single(rows, r => r.Id == "Bo");
            Assert.Equal("Bố", bo.Name);
            Assert.Equal(["goc", "mat", "ngoi"], bo.Sheets);
            var prop = Assert.Single(rows, r => r.Kind == "prop");
            Assert.Equal("Mũ lá cọ", prop.Name);
            Assert.Equal("anh", Assert.Single(prop.Sheets));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SheetPath_rejects_a_path_outside_the_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "sgd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Null(SanGachDoSheetCatalog.SheetPath(root, "..", "goc"));
            Assert.Null(SanGachDoSheetCatalog.SheetPath(root, "Bo", "goc"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
