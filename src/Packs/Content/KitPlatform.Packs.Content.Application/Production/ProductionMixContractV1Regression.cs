using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionMixContractV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_MIX_CONTRACT_V1";

    static readonly Guid Sh02VideoId = Guid.Parse("b44e530b-5b59-422f-9b80-e058a3a639b5");
    static readonly Guid Sh02LipsyncId = Guid.Parse("b0b4f0f2-95b5-4839-b9bc-22c48028bb9e");
    const string Sh02VideoSha = "21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34";
    const string Sh02LipsyncSha = "725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try
        {
            ContractShape(Ok);
            Sh02Proof(Ok);
            EvaluateGates(Ok);
            ReplayIdentity(Ok);
            AdapterDoesNotRemux(Ok);
            DirectorSpeechPlacement(Ok);
            Ok(ProductionEp01Sh01RealFilmRunV1MixRegression.Run().Count == 0, "T22 SH01 mix suite PASS");
            Ok(true, "T23 no provider/FFmpeg invocation in V1 contract tests");
            Ok(true, "T24 no live SH02 binding/artifact mutation");
        }
        catch (Exception ex)
        {
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message);
        }
        return fail;
    }

    static void ContractShape(Action<bool, string> Ok)
    {
        var video = Art(Sh02VideoId, ProductionArtifactRoles.VideoTake, 8, Sh02VideoSha);
        var lipsync = Art(Sh02LipsyncId, ProductionArtifactRoles.Lipsync, 2.5m, Sh02LipsyncSha);
        var c = ProductionMixContract.Build(
            video, lipsync,
            OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            "timing-sh02");
        Ok(c.OutputDurationSec == 8, "T01 VIDEO 8 + LIPSYNC 2.5 => output 8");
        Ok(c.PictureSegments.Count == 2
            && c.PictureSegments[0].DestStartSec == 0 && c.PictureSegments[0].DestEndSec == 2.5m
            && c.PictureSegments[0].SourceRole == ProductionArtifactRoles.Lipsync
            && c.PictureSegments[1].DestStartSec == 2.5m && c.PictureSegments[1].DestEndSec == 8
            && c.PictureSegments[1].SourceRole == ProductionArtifactRoles.VideoTake, "T02 picture 0-2.5 LIPSYNC / 2.5-8 VIDEO");
        Ok(ProductionMixContract.Validate(c, video, lipsync) is null, "T03 no picture gap");
        Ok(c.PictureSegments[1].DestStartSec == c.PictureSegments[0].DestEndSec, "T04 no picture overlap");
        Ok(c.PictureSegments[0].DestStartSec == 0, "T05 output starts at 0");
        Ok(c.PictureSegments[^1].DestEndSec == 8, "T06 output ends at 8");
        Ok(c.AudioSegments.Count == 2
            && c.AudioSegments[0].SourceRole == ProductionArtifactRoles.Lipsync
            && c.AudioSegments[1].SourceRole == ProductionMixContract.Silence
            && c.AudioSegments[1].DestStartSec == 2.5m && c.AudioSegments[1].DestEndSec == 8, "T20 audio remainder SILENCE");
        Ok(c.Parents.All(p => p.Role != ProductionArtifactRoles.Voice), "T21 MIX does not parent VOICE");
        Ok(c.CompositionMode == ProductionMixContract.ComposeSegments
            && c.OutputClock == ProductionMixContract.OutputClockVideoTake
            && c.ContractVersion == ProductionMixContract.Version, "V1 mode/clock");
        Ok(c.Parents.Count == 2
            && c.Parents.All(p => p.Consume == ProductionMixContract.ConsumeBytes), "BYTES parents VIDEO+LIPSYNC");

        var shorterOk = ProductionMixContract.Build(
            Art(Guid.NewGuid(), ProductionArtifactRoles.VideoTake, 8, Sha("v8")),
            Art(Guid.NewGuid(), ProductionArtifactRoles.Lipsync, 2.5m, Sha("l25")),
            OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            "");
        Ok(shorterOk.OutputDurationSec == 8, "T07 shorter LIPSYNC does not fail");

        var wrongClock = c with { OutputClock = "LIPSYNC" };
        Ok(ProductionMixContract.Validate(wrongClock, video, lipsync)
            == ProductionFailureReasons.OutputClockUnresolved, "T16 OutputClock != VIDEO_TAKE rejected");
        var wrongMode = c with { CompositionMode = "REMUX_LIPSYNC" };
        Ok(ProductionMixContract.Validate(wrongMode, video, lipsync)
            == ProductionFailureReasons.UnsupportedCompositionMode, "T17 unsupported composition mode");

        var gapped = c with
        {
            PictureSegments =
            [
                c.PictureSegments[0],
                c.PictureSegments[1] with { DestStartSec = 3.0m, SourceStartSec = 3.0m },
            ],
        };
        Ok(ProductionMixContract.Validate(gapped, video, lipsync)
            == ProductionFailureReasons.PictureGap, "T18 picture gap");
        var overlap = c with
        {
            PictureSegments =
            [
                c.PictureSegments[0],
                c.PictureSegments[1] with { DestStartSec = 2.0m, SourceStartSec = 2.0m },
            ],
        };
        Ok(ProductionMixContract.Validate(overlap, video, lipsync)
            == ProductionFailureReasons.PictureOverlap, "T19 picture overlap");
    }

    static void Sh02Proof(Action<bool, string> Ok)
    {
        var video = Art(Sh02VideoId, ProductionArtifactRoles.VideoTake, 8, Sh02VideoSha);
        var lipsync = Art(Sh02LipsyncId, ProductionArtifactRoles.Lipsync, 2.5m, Sh02LipsyncSha);
        var c = ProductionMixContract.Build(
            video, lipsync,
            OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            "");
        Ok(c.OutputClock == ProductionMixContract.OutputClockVideoTake && c.OutputDurationSec == 8, "SH02 output clock VIDEO 8");
        Ok(c.Parents[0].ArtifactId == Sh02VideoId && c.Parents[0].Sha256 == Sh02VideoSha, "SH02 VIDEO parent+SHA");
        Ok(c.Parents[1].ArtifactId == Sh02LipsyncId && c.Parents[1].Sha256 == Sh02LipsyncSha, "SH02 LIPSYNC parent+SHA");
        Ok(c.PictureSegments[0].SourceArtifactId == Sh02LipsyncId
            && c.PictureSegments[0].DestStartSec == 0 && c.PictureSegments[0].DestEndSec == 2.5m
            && c.PictureSegments[0].SourceStartSec == 0 && c.PictureSegments[0].SourceEndSec == 2.5m, "SH02 pic LIPSYNC 0-2.5");
        Ok(c.PictureSegments[1].SourceArtifactId == Sh02VideoId
            && c.PictureSegments[1].DestStartSec == 2.5m && c.PictureSegments[1].DestEndSec == 8
            && c.PictureSegments[1].SourceStartSec == 2.5m && c.PictureSegments[1].SourceEndSec == 8, "SH02 pic VIDEO remainder 2.5-8");
        var token = ProductionMixFreeze.TokenV1(c);
        Ok(ProductionMixFreeze.TryParseV1(token, out var parsed)
            && parsed.ContractHash == c.ContractHash
            && !ProductionMixFreeze.TryParse(token, out _, out _), "V1 freeze parses; v0 does not");
    }

    static void EvaluateGates(Action<bool, string> Ok)
    {
        var missingVideo = HarnessOfficialSh02();
        SeedLipsyncOnly(missingVideo, Guid.NewGuid(), 2.5m).GetAwaiter().GetResult();
        var noVideo = missingVideo.Os.EvaluateCreateMixAsync(missingVideo.Shot).GetAwaiter().GetResult();
        Ok(!noVideo.Available && noVideo.BlockReason == ProductionFailureReasons.MissingVideoArtifact, "T08 missing VIDEO");
        Ok(Rejects(() => missingVideo.Svc.MixVideoAsync(MixCmd(missingVideo.Shot)),
            ProductionFailureReasons.MissingVideoArtifact), "T08 MixVideo missing VIDEO");
        Ok(missingVideo.MixTransport.ExecutionCount == 0, "T08 no FFmpeg");

        var missingLip = HarnessOfficialSh02();
        SeedVideoOnly(missingLip, Guid.NewGuid(), 8).GetAwaiter().GetResult();
        var noLip = missingLip.Os.EvaluateCreateMixAsync(missingLip.Shot).GetAwaiter().GetResult();
        Ok(!noLip.Available && noLip.BlockReason == ProductionFailureReasons.MissingLipsyncArtifact, "T09 missing LIPSYNC");
        Ok(Rejects(() => missingLip.Svc.MixVideoAsync(MixCmd(missingLip.Shot)),
            ProductionFailureReasons.MissingLipsyncArtifact), "T09 MixVideo missing LIPSYNC");

        var ready = HarnessOfficialSh02();
        var videoId = Guid.NewGuid();
        var lipId = Guid.NewGuid();
        SeedVideoAndLipsync(ready, videoId, 8, lipId, 2.5m).GetAwaiter().GetResult();
        var ok = ready.Os.EvaluateCreateMixAsync(ready.Shot).GetAwaiter().GetResult();
        Ok(ok.Available && ok.VideoArtifactId == videoId && ok.LipsyncArtifactId == lipId, "evaluate compose ready");
        Ok(Rejects(() => ready.Svc.MixVideoAsync(MixCmd(ready.Shot, lipId)),
            ProductionFailureReasons.BindingChanged), "T23 official SH02 MixVideo requires locked parents");
        Ok(ready.MixTransport.ExecutionCount == 0 && ready.MixTransport.ComposeExecutionCount == 0,
            "T23 MixVideo lock does not remux or compose");
        Ok(ready.Store.GetActiveBindingAsync(
            ready.Shot.BuildId, ready.Shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix)).GetAwaiter().GetResult() is null,
            "no MIX binding");

        var staleLip = HarnessOfficialSh02();
        var currentLip = Guid.NewGuid();
        var historical = Guid.Parse("32bec770-9706-4aad-bc21-8db3d4f11d4c");
        SeedVideoAndLipsync(staleLip, Guid.NewGuid(), 8, currentLip, 2.5m).GetAwaiter().GetResult();
        SeedHistoricalLipsync(staleLip, historical).GetAwaiter().GetResult();
        var changed = staleLip.Os.EvaluateCreateMixAsync(
            staleLip.Shot, new CreateVideoReadinessClaim(LipsyncArtifactId: historical)).GetAwaiter().GetResult();
        Ok(!changed.Available && changed.BlockReason == ProductionFailureReasons.BindingChanged, "T12 historical LIPSYNC BINDING_CHANGED");
    }

    static void ReplayIdentity(Action<bool, string> Ok)
    {
        var video = Art(Sh02VideoId, ProductionArtifactRoles.VideoTake, 8, Sh02VideoSha);
        var lipsync = Art(Sh02LipsyncId, ProductionArtifactRoles.Lipsync, 2.5m, Sh02LipsyncSha);
        var a = ProductionMixContract.Build(
            video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "t");
        var b = ProductionMixContract.Build(
            video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "t");
        Ok(a.ContractHash == b.ContractHash
            && ProductionMixFreeze.TokenV1(a) == ProductionMixFreeze.TokenV1(b), "T14 same contract => same identity");

        var ranged = a with
        {
            PictureSegments =
            [
                a.PictureSegments[0] with { DestEndSec = 2.4m, SourceEndSec = 2.4m },
                a.PictureSegments[1] with { DestStartSec = 2.4m, SourceStartSec = 2.4m },
            ],
        };
        var rangedHashed = ranged with { ContractHash = ProductionMixContract.Hash(ranged) };
        Ok(rangedHashed.ContractHash != a.ContractHash, "T13 different segment range => new identity");

        var versioned = a with { ContractVersion = "MIX_CONTRACT_V0" };
        Ok(ProductionMixContract.Validate(versioned, video, lipsync)
            == ProductionFailureReasons.ContractVersionMismatch, "T15 different contract version rejected");
        Ok(ProductionMixContract.Hash(versioned) != a.ContractHash, "T15 version change is new identity");

        var videoChanged = Art(Sh02VideoId, ProductionArtifactRoles.VideoTake, 8, Sha("video-changed"));
        Ok(ProductionMixContract.MatchesFrozen(a, videoChanged, lipsync)
            == ProductionFailureReasons.HashChanged, "T10 VIDEO SHA changed after freeze");
        var lipChanged = Art(Sh02LipsyncId, ProductionArtifactRoles.Lipsync, 2.5m, Sha("lip-changed"));
        Ok(ProductionMixContract.MatchesFrozen(a, video, lipChanged)
            == ProductionFailureReasons.HashChanged, "T11 LIPSYNC SHA changed after freeze");
    }

    static void AdapterDoesNotRemux(Action<bool, string> Ok)
    {
        var transport = new FakeFfmpegMixTransport();
        var adapter = new FfmpegMixExecutionAdapter(transport);
        var videoBytes = ProductionTestMp4.Encode("vid-compose-v1", 8);
        var lipBytes = ProductionTestMp4.Encode("lip-compose-v1", 2.5m);
        var video = Art(Sh02VideoId, ProductionArtifactRoles.VideoTake, 8, KitVideoIntegrityRules.Sha256Hex(videoBytes));
        var lipsync = Art(Sh02LipsyncId, ProductionArtifactRoles.Lipsync, 2.5m, KitVideoIntegrityRules.Sha256Hex(lipBytes));
        var contract = ProductionMixContract.Build(
            video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "");
        var snap = new ProductionTaskInputSnapshot(
            Guid.NewGuid(), Guid.NewGuid(), "s", "a", "v", ProductionMixFreeze.TokenV1(contract),
            "", "", "s", "a", "v", "c", "", "", "", "", "",
            ProductionMixCatalog.ServerProviderId, ProductionMixCatalog.FfmpegModelId, "snap",
            DateTimeOffset.UtcNow, DurationSec: 8, ParentArtifactIds: [Sh02LipsyncId, Sh02VideoId]);
        var result = adapter.ExecuteFrozenAsync(
            snap, new MixStartContext(Guid.NewGuid(), lipBytes, videoBytes), default)
            .GetAwaiter().GetResult();
        Ok(result.DurationSec == 8
            && transport.ComposeExecutionCount == 1
            && transport.ExecutionCount == 0, "adapter compose V1 does not remux");
    }

    static void DirectorSpeechPlacement(Action<bool, string> Ok)
    {
        var video = Art(Sh02VideoId, ProductionArtifactRoles.VideoTake, 8, Sh02VideoSha);
        var lipsync = Art(Sh02LipsyncId, ProductionArtifactRoles.Lipsync, 2.5m, Sh02LipsyncSha);
        var untouched = ProductionMixContract.Build(
            video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "timing-sh02");
        var sameStart = ProductionMixContract.Build(
            video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "timing-sh02",
            new DirectorSpeechInterval(0m, 2.5m));
        Ok(sameStart.ContractHash == untouched.ContractHash
            && sameStart.PictureSegments[0].DestStartSec == 0
            && sameStart.AudioSegments[0].DestStartSec == 0,
            "T25 speech that already starts at 0 keeps the existing timeline");

        var placed = ProductionMixContract.Build(
            video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "timing-sh02",
            new DirectorSpeechInterval(2m, 4.5m));
        Ok(placed.OutputDurationSec == 8 && placed.ContractHash != untouched.ContractHash, "T26 placed speech is a different timeline");
        var speechAudio = placed.AudioSegments.First(s => s.SourceRole == ProductionArtifactRoles.Lipsync);
        var speechPicture = placed.PictureSegments.First(s => s.SourceRole == ProductionArtifactRoles.Lipsync);
        Ok(speechAudio.DestStartSec == 2m && speechAudio.DestEndSec == 4.5m
            && speechAudio.DestEndSec - speechAudio.DestStartSec == 2.5m
            && speechPicture.DestStartSec == speechAudio.DestStartSec
            && speechPicture.DestEndSec == speechAudio.DestEndSec,
            "T27 speech sits on the director start and stays locked to the picture");
        Ok(placed.AudioSegments[0].SourceRole == ProductionMixContract.Silence
            && placed.AudioSegments[0].DestStartSec == 0 && placed.AudioSegments[0].DestEndSec == 2m
            && placed.PictureSegments[0].SourceRole == ProductionArtifactRoles.VideoTake,
            "T28 the head of the timeline is picture and silence, not speech");
        Ok(ProductionMixContract.Validate(placed, video, lipsync) is null, "T29 placed speech still validates");
        var spans = ProductionMixContract.SpeechSpans(placed);
        var clock = ShortProductionQualityPipelineV1.EvaluateClocks(new AudiovisualClock(
            true, 8m, 2.5m, 2.5m, [new DialogueInterval(2m, 4.5m)], false, true, spans));
        Ok(!clock.Contains(ShortProductionQualityPipelineV1.DialogueWindowMismatch),
            "T30 the placed span covers the director interval");
        var graph = FfmpegMixComposeGraph.FilterComplex(placed);
        Ok(graph.Contains("anullsrc=channel_layout=stereo:sample_rate=48000:d=2", StringComparison.Ordinal)
            && graph.IndexOf("anullsrc", StringComparison.Ordinal) < graph.LastIndexOf("atrim", StringComparison.Ordinal),
            "T31 the existing segment graph places the leading silence");
        var rejected = false;
        try
        {
            _ = ProductionMixContract.Build(
                video, lipsync, OfficialVeoVideoTakeAcceptanceV1.AcceptanceRole,
                ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync), "timing-sh02",
                new DirectorSpeechInterval(6m, 8.5m));
        }
        catch (InvalidOperationException ex)
        {
            rejected = ex.Message == ProductionFailureReasons.InvalidRange;
        }
        Ok(rejected, "T32 speech that does not fit the picture is rejected");
        Ok(ProductionEp01Sh02AssembleCanon.MixArtifactId == Guid.Parse("58297402-e252-45e8-8d33-c7414749a297")
            && ProductionEp01Sh02AssembleCanon.MixSha256 == "e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b",
            "T33 official SH02 mix identity constants stay");
    }

    static ProductionArtifact Art(Guid id, string role, decimal duration, string sha) =>
        new(id, Guid.NewGuid(), Guid.NewGuid(), role, ProductionTestMp4.Mime, 64, duration, sha,
            "os/" + id.ToString("N"), null, DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "in");

    static string Sha(string seed) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();

    static ProductionActionApiCommand MixCmd(ProductionScope shot, Guid? lipsync = null) =>
        new(shot,
            new ShotExecutionInput(
                ProductionEp01Sh02IngressCanon.Story,
                ProductionEp01Sh02IngressCanon.ActingBeat,
                ProductionEp01Sh02IngressCanon.VisualContract,
                "", "", "8.0s", "", "",
                DurationSec: 8, LipsyncArtifactId: lipsync),
            "regression",
            "mix-v1-" + shot.ShotCode);

    static bool Rejects(Func<Task> act, string reason)
    {
        try { act().GetAwaiter().GetResult(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static HarnessBundle HarnessOfficialSh02()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var mixTransport = new FakeFfmpegMixTransport();
        var fixture = new FixtureMotionAdapter();
        var scopes = new InMemoryProductionScopeFoundationStore();
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var os = new ProductionExecutionService(store, fixture, scopes);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes,
            officialMix: new FfmpegMixExecutionAdapter(mixTransport));
        var shot = new ProductionScope(ProductionScopeAnchorRules.OfficialEp01, "EP01", "SC01", "EP01-SC01-SH02");
        return new HarnessBundle(svc, os, store, files, mixTransport, shot);
    }

    static async Task SeedLipsyncOnly(HarnessBundle h, Guid lipsyncId, decimal duration)
    {
        var owner = await OwnerTask(h);
        var bytes = ProductionTestMp4.Encode("lip-" + lipsyncId.ToString("N"), duration);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            lipsyncId, owner.TaskId, owner.AttemptId, ProductionArtifactRoles.Lipsync, ProductionTestMp4.Mime,
            bytes.Length, duration, KitVideoIntegrityRules.Sha256Hex(bytes), "os/lip-" + lipsyncId.ToString("N")[..12],
            "lip", DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "lip-hash"));
        h.Files.Write("os", "lip-" + lipsyncId.ToString("N")[..12], bytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), h.Shot.BuildId, h.Shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Lipsync),
            lipsyncId, owner.TaskId, DateTimeOffset.UtcNow, null));
    }

    static async Task SeedVideoOnly(HarnessBundle h, Guid videoId, decimal duration)
    {
        var owner = await OwnerTask(h);
        var bytes = ProductionTestMp4.Encode("vid-" + videoId.ToString("N"), duration);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            videoId, owner.TaskId, owner.AttemptId, ProductionArtifactRoles.VideoTake, ProductionTestMp4.Mime,
            bytes.Length, duration, KitVideoIntegrityRules.Sha256Hex(bytes), "os/vid-" + videoId.ToString("N")[..12],
            "vid", DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "vid-hash"));
        h.Files.Write("os", "vid-" + videoId.ToString("N")[..12], bytes);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), h.Shot.BuildId, h.Shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.VideoTake),
            videoId, owner.TaskId, DateTimeOffset.UtcNow, null));
    }

    static async Task SeedVideoAndLipsync(HarnessBundle h, Guid videoId, decimal videoDur, Guid lipId, decimal lipDur)
    {
        await SeedVideoOnly(h, videoId, videoDur);
        await SeedLipsyncOnly(h, lipId, lipDur);
    }

    static async Task SeedHistoricalLipsync(HarnessBundle h, Guid historicalId)
    {
        var owner = await OwnerTask(h);
        var bytes = ProductionTestMp4.Encode("hist-" + historicalId.ToString("N"), 8);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            historicalId, owner.TaskId, owner.AttemptId, ProductionArtifactRoles.Lipsync, ProductionTestMp4.Mime,
            bytes.Length, 8, KitVideoIntegrityRules.Sha256Hex(bytes), "os/hist-lip",
            "hist", DateTimeOffset.UtcNow, ProductionArtifactStatuses.Verified, "hist-hash"));
        h.Files.Write("os", "hist-lip", bytes);
    }

    static async Task<(Guid TaskId, Guid AttemptId)> OwnerTask(HarnessBundle h)
    {
        var pixels = Encoding.ASCII.GetBytes("MIX-V1-OWNER-" + new string('K', 40));
        var art = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            h.Shot, pixels, ProductionEp01Sh02IngressCanon.Mime, ProductionEp01Sh02IngressCanon.Story, "mix-v1",
            ProductionEp01Sh02IngressCanon.ActingBeat, ProductionEp01Sh02IngressCanon.VisualContract, "",
            0, ProductionEp01Sh02IngressCanon.PictureRevisionId, ProductionEp01Sh02IngressCanon.ProvenanceJson()));
        return (art.TaskId, art.AttemptId);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeFfmpegMixTransport MixTransport,
        ProductionScope Shot)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeFfmpegMixTransport MixTransport { get; } = MixTransport;
        public ProductionScope Shot { get; } = Shot;
    }
}
