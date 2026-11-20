using KitPlatform.Packs.Content.VisualIntelligence;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class SharedPerformanceFilmTests
{
    [Fact]
    public void Novixa_film_poses_a_shared_rig_and_a_second_brand_reuses_it()
    {
        IPerformanceEngine engine = new KitRigPerformanceEngine();
        var proof = Path.Combine(FindRepo(), "proof", "performance-film");
        Directory.CreateDirectory(proof);

        var sleeveCue = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Explain.Id, 0, 3) };
        var sleeveDir = Path.Combine(proof, "frames");
        Directory.CreateDirectory(sleeveDir);
        File.WriteAllBytes(Path.Combine(sleeveDir, "sleeve-mid.png"), engine.Render(PerformanceCatalog.Request(sleeveCue, PerformanceCatalog.Explain, 0.5f)));
        File.WriteAllBytes(Path.Combine(sleeveDir, "sleeve-point.png"), engine.Render(PerformanceCatalog.Request(sleeveCue, PerformanceCatalog.Explain, 1.2f)));

        var down = engine.Render(PerformanceCatalog.Request(PerformanceCatalog.NovixaCounter, PerformanceCatalog.Explain, 0));
        var raised = engine.Render(PerformanceCatalog.Request(PerformanceCatalog.NovixaCounter, PerformanceCatalog.Explain, 1));
        File.WriteAllBytes(Path.Combine(proof, "novixa-explain-0.png"), down);
        File.WriteAllBytes(Path.Combine(proof, "novixa-explain-1.png"), raised);
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(down)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raised)));

        var slower = PerformanceCatalog.NovixaCounter with
        {
            Cue = PerformanceCatalog.NovixaCounter.Cue with { Duration = 2 },
        };
        var halfway = engine.Render(PerformanceCatalog.Request(slower, PerformanceCatalog.Explain, 0.5f));
        var originalHalf = engine.Render(PerformanceCatalog.Request(PerformanceCatalog.NovixaCounter, PerformanceCatalog.Explain, 0.5f));
        File.WriteAllBytes(Path.Combine(proof, "novixa-explain-retimed.png"), halfway);
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(halfway)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(originalHalf)));

        var wave = engine.Render(PerformanceCatalog.Request(PerformanceCatalog.FamixaDoorway, PerformanceCatalog.Wave, 1));
        File.WriteAllBytes(Path.Combine(proof, "famixa-wave-1.png"), wave);
        var novixaWave = engine.Render(PerformanceCatalog.Request(
            PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Wave.Id, 0, 1) },
            PerformanceCatalog.Wave,
            1));
        File.WriteAllBytes(Path.Combine(proof, "novixa-wave-1.png"), novixaWave);
        var film = PerformanceFilm.Encode(engine, PerformanceCatalog.NovixaCounter, PerformanceCatalog.Explain, 2);
        File.WriteAllBytes(Path.Combine(proof, "novixa-explain.mp4"), film);
        Assert.True(film.Length > 20_000, "mp4 is too small to be a watchable film");
        var faster = PerformanceFilm.Encode(engine, PerformanceCatalog.NovixaCounter, PerformanceCatalog.Explain, 1);
        File.WriteAllBytes(Path.Combine(proof, "novixa-explain-duration-1.mp4"), faster);
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(film)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(faster)));
        var shortFilm = PerformanceFilm.EncodeSequence(engine, PerformanceCatalog.NovixaCounter, PerformanceCatalog.NovixaShort, 15);
        File.WriteAllBytes(Path.Combine(proof, "novixa-short.mp4"), shortFilm);
        Assert.True(shortFilm.Length > 40_000, "the short film is too small");
        var shortFaster = PerformanceFilm.EncodeSequence(engine, PerformanceCatalog.NovixaCounter, PerformanceCatalog.NovixaShort, 4);
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(shortFilm)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(shortFaster)));
        var famixaShort = PerformanceFilm.EncodeSequence(engine, PerformanceCatalog.FamixaDoorway, PerformanceCatalog.NovixaShort, 15);
        File.WriteAllBytes(Path.Combine(proof, "famixa-same-beats.mp4"), famixaShort);
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(shortFilm)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(famixaShort)));
        var famixaStillLan = PerformanceCatalog.Request(PerformanceCatalog.FamixaDoorway, PerformanceCatalog.Explain, 0);
        Assert.Equal(CharacterCatalog.Lan.Id, famixaStillLan.Character!.Id);
        Assert.Equal("plate-home.jpg", famixaStillLan.Timeline.PlateFile);
        Assert.Equal(PerformanceCatalog.Human.Id, PerformanceCatalog.Wave.RigId);
        Assert.Equal(PerformanceCatalog.Human.Id, PerformanceCatalog.Explain.RigId);
        Assert.Equal("kit-rig-v1", engine.EngineId);
        Assert.NotEqual(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(wave)), Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(raised)));
    }

    [Fact]
    public void Explain_pose_lifts_the_sleeve_then_holds_the_point()
    {
        IPerformanceEngine engine = new KitRigPerformanceEngine();
        var proof = Path.Combine(FindRepo(), "proof", "performance-film", "frames");
        Directory.CreateDirectory(proof);
        var cue = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Explain.Id, 0, 3) };
        var rest = engine.Render(PerformanceCatalog.Request(cue, PerformanceCatalog.Explain, 0));
        var mid = engine.Render(PerformanceCatalog.Request(cue, PerformanceCatalog.Explain, 0.5f));
        var point = engine.Render(PerformanceCatalog.Request(cue, PerformanceCatalog.Explain, 1.2f));
        File.WriteAllBytes(Path.Combine(proof, "sleeve-rest.png"), rest);
        File.WriteAllBytes(Path.Combine(proof, "sleeve-mid.png"), mid);
        File.WriteAllBytes(Path.Combine(proof, "sleeve-point.png"), point);
        var restHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(rest));
        var midHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(mid));
        var pointHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(point));
        Assert.NotEqual(restHash, midHash);
        Assert.NotEqual(midHash, pointHash);
        var lan = PerformanceCatalog.Request(cue, PerformanceCatalog.Explain, 0);
        Assert.Equal(CharacterCatalog.Lan.Id, lan.Character!.Id);
        var other = lan with { Character = new CharacterCut("fixture-other", PerformanceCatalog.Human.Id, "no-such-character.png") };
        var refused = Assert.Throws<InvalidOperationException>(() => engine.Render(other));
        Assert.Contains("no-such-character.png", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Point_wrist_moves_without_a_spike()
    {
        LanVector.TraceWrist = true;
        IPerformanceEngine engine = new KitRigPerformanceEngine();
        var cue = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Explain.Id, 0, 3) };
        var previous = 0f;
        var seen = false;
        for (var step = 0; step <= 12; step++)
        {
            engine.Render(PerformanceCatalog.Request(cue, PerformanceCatalog.Explain, step * 0.25f));
            var x = LanVector.RightWrist.X;
            if (seen)
                Assert.True(Math.Abs(x - previous) < 90f, $"wrist jumped from {previous:0} to {x:0}");
            previous = x;
            seen = true;
        }

        LanVector.TraceWrist = false;
    }

    [Fact]
    public void Wave_and_reach_hold_frames()
    {
        IPerformanceEngine engine = new KitRigPerformanceEngine();
        var proof = Path.Combine(FindRepo(), "proof", "performance-film", "frames");
        Directory.CreateDirectory(proof);
        var wave = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Wave.Id, 0, 2) };
        var reach = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Grasp.Id, 0, 2) };
        var point = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Explain.Id, 0, 3) };
        File.WriteAllBytes(Path.Combine(proof, "hold-wave.png"), engine.Render(PerformanceCatalog.Request(wave, PerformanceCatalog.Wave, 1.6f)));
        File.WriteAllBytes(Path.Combine(proof, "hold-reach.png"), engine.Render(PerformanceCatalog.Request(reach, PerformanceCatalog.Grasp, 1.6f)));
        File.WriteAllBytes(Path.Combine(proof, "hold-point.png"), engine.Render(PerformanceCatalog.Request(point, PerformanceCatalog.Explain, 2.2f)));
    }

    [Fact]
    public void Vector_shoulder_frames_at_film_time()
    {
        IPerformanceEngine engine = new KitRigPerformanceEngine();
        var proof = Path.Combine(FindRepo(), "proof", "performance-film", "frames");
        Directory.CreateDirectory(proof);
        var cue = PerformanceCatalog.NovixaCounter with { Cue = new TimelineCue(PerformanceCatalog.Ready.Id, 0, 15) };
        var marks = new (string Name, int Frame)[] { ("raise", 49), ("wave", 197), ("reach", 310) };
        var log = new System.Text.StringBuilder();
        foreach (var (name, frame) in marks)
        {
            for (var delta = -2; delta <= 2; delta++)
            {
                var time = (frame + delta) / 24f;
                var png = engine.Render(PerformanceCatalog.Request(cue, PerformanceCatalog.Ready, time));
                if (delta == 0)
                    File.WriteAllBytes(Path.Combine(proof, "vec-" + name + ".png"), png);
                log.AppendLine(
                    name + " t=" + time.ToString("0.000")
                    + " L=" + LanVector.LeftWrist.X.ToString("0.0") + "," + LanVector.LeftWrist.Y.ToString("0.0")
                    + " R=" + LanVector.RightWrist.X.ToString("0.0") + "," + LanVector.RightWrist.Y.ToString("0.0")
                    + " LS=" + LanVector.LeftShoulder.X.ToString("0.0") + "," + LanVector.LeftShoulder.Y.ToString("0.0")
                    + " RS=" + LanVector.RightShoulder.X.ToString("0.0") + "," + LanVector.RightShoulder.Y.ToString("0.0"));
            }
        }

        File.WriteAllText(Path.Combine(proof, "vec-wrists.txt"), log.ToString());
    }

    private static string FindRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "KitPlatform.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repo root was not found.");
    }
}
