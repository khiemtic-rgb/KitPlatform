using KitPlatform.Packs.Content.VisualIntelligence;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class SharedVisualIntelligenceV1Tests
{
    [Fact]
    public void Shared_visual_intelligence_meets_the_acceptance_suite()
    {
        var proof = Path.Combine(FindRepo(), "proof", "shared-visual-intelligence");
        var fail = SharedVisualIntelligenceV1Regression.Run(proof);
        Assert.True(fail.Count == 0, string.Join("; ", fail));
    }

    [Fact]
    public void Studio_search_refuses_an_unrelated_still_and_reload_does_not_render()
    {
        var engine = new SharedVisualIntelligence();
        var session = engine.CreateSampleSession();
        var before = session.LocalRenders;
        var demand = new ShotVisualDemand(
            "shot-a", "film-a", "famixa", "rain-shoes", "tie", "doorway", "Mưa ngoài hiên",
            "stand", "calm", "medium");
        var search = engine.Resolve(session, demand, VisualResolveOptions.Search);
        Assert.Equal(VisualDecisions.NoFit, search.Decision);
        Assert.Null(search.AssetId);
        Assert.DoesNotContain(search.Candidates, row => row.Eligible);
        Assert.Equal(before, session.LocalRenders);
        Assert.Equal(0, session.PaidProviderCalls);
        Assert.Null(session.FindBinding("film-a", "shot-a"));

        var otherBrand = engine.Resolve(session, demand with { BrandCode = "novixa" }, VisualResolveOptions.Search);
        Assert.Equal(VisualDecisions.NoFit, otherBrand.Decision);
        Assert.Null(otherBrand.AssetId);
        Assert.DoesNotContain(otherBrand.Candidates, row => row.Eligible && row.AssetId == "nx-counter-consult");

        var made = engine.Resolve(session, demand, VisualResolveOptions.Studio);
        Assert.Equal(VisualDecisions.GenerateLocal, made.Decision);
        Assert.NotNull(made.AssetId);
        Assert.NotEqual("fx-breakfast", made.AssetId);
        Assert.Equal(1, session.LocalRenders);

        var again = engine.Resolve(session, demand, VisualResolveOptions.Search);
        Assert.Equal(VisualDecisions.Bound, again.Decision);
        Assert.Equal(made.AssetId, again.AssetId);
        Assert.Equal(1, session.LocalRenders);

        var empty = Path.Combine(Path.GetTempPath(), "kit-visual-read-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Null(engine.TryLoad(empty));
            Assert.False(File.Exists(Path.Combine(empty, "library.json")));
        }
        finally
        {
            if (Directory.Exists(empty)) Directory.Delete(empty, true);
        }

        session.Approve("film-a", "shot-a");
        var approved = session.FindAsset(made.AssetId!)!;
        var sha = approved.Sha256;
        var pixels = session.Pixels[approved.Id].ToArray();
        var variant = engine.Resolve(session, demand, VisualResolveOptions.Studio.AsRegenerate());
        Assert.Equal(VisualDecisions.VariantStored, variant.Decision);
        Assert.Equal(approved.Id, variant.AssetId);
        Assert.NotEqual(approved.Id, variant.VariantAssetId);
        var kept = session.FindBinding("film-a", "shot-a")!;
        Assert.True(kept.Approved);
        Assert.Equal(approved.Id, kept.AssetId);
        Assert.Equal(sha, session.FindAsset(approved.Id)!.Sha256);
        Assert.True(session.Pixels[approved.Id].AsSpan().SequenceEqual(pixels));
        Assert.Equal(0, session.PaidProviderCalls);
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Không thấy gốc repo để ghi ảnh kiểm thử.");
    }
}
