using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionEpisodeAssemblyV1Regression
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_EPISODE_ASSEMBLY_CONTRACT_V1";

    static readonly Guid Sh01Asm = Guid.Parse("28f8de3e-f6d3-4972-934e-703a2be45939");
    static readonly Guid Sh02Asm = Guid.Parse("92621f9e-57eb-4185-8cea-a962ec399136");
    static readonly Guid Sh01Mix = Guid.Parse("2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2");
    static readonly Guid Sh02Mix = Guid.Parse("58297402-e252-45e8-8d33-c7414749a297");
    static readonly Guid HistSh01 = Guid.Parse("d56a3c63-fbe7-41d6-8355-0af246c092b7");
    static readonly Guid HistSh02 = Guid.Parse("1c210b73-333e-46ce-9d02-8beab8ef2f5d");
    const decimal Sh01Dur = 5.042m;
    const decimal Sh02Dur = 8m;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex)
        {
            var inner = ex.InnerException is null ? "" : " INNER:" + ex.InnerException.GetType().Name + ":" + ex.InnerException.Message;
            fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message + inner);
        }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        ContractShape(Ok);
        EvaluatePure(Ok);
        await ServiceGates(Ok);
        await HappyReplay(Ok);
        await Concurrent(Ok);
        await ProcessFailureConvergence(Ok);
        await LongProcessDiagnosticPersists(Ok);
        await RemuxProcessFailurePersists(Ok);
        RegressionLocks(Ok);
        Safety(Ok);
    }

    static void ContractShape(Action<bool, string> Ok)
    {
        var a = Row(0, "EP01-SC01-SH01", Sh01Asm, "a".PadLeft(64, 'a'), "os/a", Sh01Dur);
        var b = Row(1, "EP01-SC01-SH02", Sh02Asm, "b".PadLeft(64, 'b'), "os/b", Sh02Dur);
        var c1 = ProductionEpisodeAssemblyContract.Build(ProductionScopeAnchorRules.OfficialEp01, "EP01", [a, b]);
        var c2 = ProductionEpisodeAssemblyContract.Build(ProductionScopeAnchorRules.OfficialEp01, "EP01", [a, b]);
        Ok(c1.ContractVersion == ProductionEpisodeAssemblyContract.Version
            && c1.CompositionMode == ProductionEpisodeAssemblyContract.ConcatCopy
            && c1.OutputClock == ProductionEpisodeAssemblyContract.OutputClockAssembly
            && c1.DurationToleranceSec == 0
            && c1.OutputDurationSec == 13.042m
            && c1.OrderedInputs[0].StartSec == 0
            && c1.OrderedInputs[0].EndSec == Sh01Dur
            && c1.OrderedInputs[1].StartSec == Sh01Dur
            && c1.OrderedInputs[1].EndSec == 13.042m
            && c1.ContractHash == c2.ContractHash
            && c1.TimelineHash == c2.TimelineHash, "contract shape + deterministic hash");
        Ok(ProductionEpisodeAssemblyContract.TryParse(ProductionEpisodeAssemblyContract.Token(c1), out var parsed)
            && parsed.ContractHash == c1.ContractHash, "token roundtrip");
        Ok(!ProductionAssemblyFreeze.TryParse(ProductionEpisodeAssemblyContract.Token(c1), out _, out _, out _),
            "shot ProductionAssemblyFreeze does not parse episode token");
        var reordered = ProductionEpisodeAssemblyContract.Hash(
            c1 with
            {
                OrderedInputs = ProductionEpisodeAssemblyContract.Abut([b with { Order = 0 }, a with { Order = 1 }]),
                ContractHash = "",
            });
        Ok(reordered != c1.ContractHash, "Y hash changes when order changes");
        var durChanged = ProductionEpisodeAssemblyContract.Hash(
            c1 with
            {
                OrderedInputs = ProductionEpisodeAssemblyContract.Abut([a with { DurationSec = 6 }, b]),
                OutputDurationSec = 14,
                ContractHash = "",
            });
        Ok(durChanged != c1.ContractHash, "Z hash changes when duration changes");
        Ok(ProductionActions.IsKnown(ProductionActions.AssembleEpisode)
            && ProductionActionContractRegistry.AssembleEpisode.Target == "Episode"
            && ProductionActionContractRegistry.AssembleEpisode.OutputArtifactRole == ProductionArtifactRoles.Assembly
            && ProductionActionContractRegistry.AssembleEpisode.UiCta == "Lắp tập phim"
            && ProductionActionContractRegistry.AssembleEpisode.SuccessCondition.Contains("No FINAL claim", StringComparison.Ordinal),
            "ASSEMBLE_EPISODE action contract");
    }

    static void EvaluatePure(Action<bool, string> Ok)
    {
        var a = Row(0, "EP01-SC01-SH01", Sh01Asm, "a".PadLeft(64, 'a'), "os/a", Sh01Dur);
        var b = Row(1, "EP01-SC01-SH02", Sh02Asm, "b".PadLeft(64, 'b'), "os/b", Sh02Dur);
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01", [a], true, null)
            == ProductionFailureReasons.MissingDependency, "F N<2");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01",
                [a, a with { Order = 0, ShotCode = "EP01-SC01-SH02" }], true, null)
            == ProductionFailureReasons.InvalidRange, "D duplicate order");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01",
                [a, b with { Order = 2 }], true, null)
            == ProductionFailureReasons.InvalidRange, "E non-contiguous order");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01",
                [a, a with { Order = 1 }], true, null)
            == ProductionFailureReasons.FailedIdentity, "C duplicate shot");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01",
                [a, b with { DurationSec = 0 }], true, null)
            == ProductionFailureReasons.InvalidRange, "O duration<=0");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp99, "EP01", [a, b], true, null)
            == ProductionFailureReasons.CrossScopeForbidden, "G wrong build");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP99", [a, b], true, null)
            == ProductionFailureReasons.CrossScopeForbidden, "G wrong episode");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01", [b with { Order = 0 }, a with { Order = 1 }], true, null)
            == ProductionFailureReasons.FailedIdentity, "Y official reorder rejected");
        Ok(ProductionEpisodeAssemblyContract.ValidateInputs(
                ProductionScopeAnchorRules.OfficialEp01, "EP01", [a, b], false, null)
            == ProductionFailureReasons.UnsupportedCompositionMode, "T incompatible streams");
        Ok(ProductionEpisodeAssemblyContract.RejectLegacy("series_build.graph_json")
            == ProductionFailureReasons.HiddenFallbackForbidden, "U legacy graph");
        Ok(ProductionEpisodeAssemblyContract.RejectLegacy("/content/series/assemble")
            == ProductionFailureReasons.HiddenFallbackForbidden, "U legacy assemble");

        var built = ProductionEpisodeAssemblyContract.Build(ProductionScopeAnchorRules.OfficialEp01, "EP01", [a, b]);
        var gap = built with
        {
            OrderedInputs =
            [
                built.OrderedInputs[0],
                built.OrderedInputs[1] with { StartSec = 6, EndSec = 14 },
            ],
        };
        Ok(ProductionEpisodeAssemblyContract.Validate(gap) == ProductionFailureReasons.PictureGap, "P timeline gap");
        var overlap = built with
        {
            OrderedInputs =
            [
                built.OrderedInputs[0],
                built.OrderedInputs[1] with { StartSec = 4, EndSec = 12 },
            ],
        };
        Ok(ProductionEpisodeAssemblyContract.Validate(overlap) == ProductionFailureReasons.PictureOverlap, "Q timeline overlap");
        Ok(ProductionEpisodeAssemblyContract.Validate(built with { ContractVersion = "EPISODE_ASSEMBLY_CONTRACT_V0" })
            == ProductionFailureReasons.ContractVersionMismatch, "R version mismatch");
        Ok(ProductionEpisodeAssemblyContract.Validate(built with { CompositionMode = "CONCAT_REENCODE" })
            == ProductionFailureReasons.UnsupportedCompositionMode, "S unsupported mode");
        Ok(ProductionEpisodeAssemblyContract.MatchesFrozen(built, [a with { Sha256 = "c".PadLeft(64, 'c') }, b])
            == ProductionFailureReasons.HashChanged, "W/AA frozen SHA drift");
        Ok(ProductionEpisodeAssemblyContract.MatchesFrozen(built, [a with { DurationSec = 6, StartSec = 0, EndSec = 6 }, b])
            == ProductionFailureReasons.DurationMismatch, "Z frozen duration drift");
        Ok(ProductionEpisodeAssemblyContract.MatchesFrozen(built, [b, a])
            == ProductionFailureReasons.InputSnapshotMismatch, "W id/order after freeze");
    }

    static async Task ServiceGates(Action<bool, string> Ok)
    {
        var ep = ProductionEpisodeAssemblyContract.OfficialEpisodeScope();
        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        var sh02 = ProductionEp01Sh02AssembleCanon.Scope();

        var missing1 = Harness();
        await SeedAssemblyAsync(missing1, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => missing1.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.MissingDependency), "A missing SH01 CURRENT:ASSEMBLY");
        Ok(missing1.MixTransport.ConcatExecutionCount == 0, "A no concat");

        var missing2 = Harness();
        await SeedAssemblyAsync(missing2, sh01, Sh01Asm, Sh01Dur);
        Ok(await Rejects(() => missing2.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.MissingDependency), "B missing SH02 CURRENT:ASSEMBLY");

        var one = Harness();
        await SeedAssemblyAsync(one, sh01, Sh01Asm, Sh01Dur);
        Ok(await Rejects(() => one.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur)])),
            ProductionFailureReasons.MissingDependency), "F service N<2");

        var wrongBuild = Harness();
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP01", "EP01", "EP01");
        wrongBuild.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        Ok(await Rejects(() => wrongBuild.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep99, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.CrossScopeForbidden), "G service wrong build");

        var cross = Harness();
        await SeedAssemblyAsync(cross, sh02, Sh02Asm, Sh02Dur);
        await BindCurrentAssemblyAsync(cross, sh01, Sh02Asm);
        Ok(await Rejects(() => cross.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh02Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.CrossShotForbidden), "H wrong shot owner");

        var mix = Harness();
        await SeedArtifactAsync(mix, sh01, Sh01Mix, ProductionArtifactRoles.Mix, Sh01Dur);
        await BindCurrentAssemblyAsync(mix, sh01, Sh01Mix);
        await SeedAssemblyAsync(mix, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => mix.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Mix, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.ArtifactRoleMismatch), "I MIX instead of ASSEMBLY");

        var video = Harness();
        await SeedArtifactAsync(video, sh01, Sh01Asm, ProductionArtifactRoles.VideoTake, Sh01Dur);
        await BindCurrentAssemblyAsync(video, sh01, Sh01Asm);
        await SeedAssemblyAsync(video, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => video.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.ArtifactRoleMismatch), "J VIDEO instead of ASSEMBLY");

        var lip = Harness();
        await SeedArtifactAsync(lip, sh01, Sh01Asm, ProductionArtifactRoles.Lipsync, Sh01Dur);
        await BindCurrentAssemblyAsync(lip, sh01, Sh01Asm);
        await SeedAssemblyAsync(lip, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => lip.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.ArtifactRoleMismatch), "K LIPSYNC instead of ASSEMBLY");

        var unverified = Harness();
        await SeedAssemblyAsync(unverified, sh01, Sh01Asm, Sh01Dur, verified: false);
        await SeedAssemblyAsync(unverified, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => unverified.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.FailedVerification), "L unverified");

        var sha = Harness();
        await SeedAssemblyAsync(sha, sh01, Sh01Asm, Sh01Dur, sha256: new string('d', 64));
        await SeedAssemblyAsync(sha, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => sha.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.HashChanged), "M/AA SHA changed");

        var storage = Harness();
        await SeedAssemblyAsync(storage, sh01, Sh01Asm, Sh01Dur, storageKey: "os/other-key");
        await SeedAssemblyAsync(storage, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => storage.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.FailedIdentity), "N storage changed");

        var legacy = Harness();
        await SeedAssemblyAsync(legacy, sh01, Sh01Asm, Sh01Dur);
        await SeedAssemblyAsync(legacy, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => legacy.Svc.AssembleEpisodeOsAsync(
            EpisodeCommand(ep, [(sh01.ShotCode, Sh01Asm, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)], timing: "series_build.graph_json")),
            ProductionFailureReasons.HiddenFallbackForbidden), "U service legacy graph");

        var membership = Harness();
        await SeedAssemblyAsync(membership, sh01, Sh01Asm, Sh01Dur);
        await SeedAssemblyAsync(membership, sh02, Sh02Asm, Sh02Dur);
        await SeedArtifactAsync(membership, sh01, Sh01Mix, ProductionArtifactRoles.Mix, Sh01Dur);
        Ok(await Rejects(() => membership.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep, [(sh01.ShotCode, Sh01Mix, Sh01Dur), (sh02.ShotCode, Sh02Asm, Sh02Dur)])),
            ProductionFailureReasons.FailedIdentity), "V FilmAssemblyShot MIX id rejected");

        var incompat = Harness();
        await SeedReadyAsync(incompat);
        incompat.MixTransport.ForceIncompatibleConcat = true;
        Ok(await Rejects(() => incompat.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep)),
            ProductionFailureReasons.UnsupportedCompositionMode), "T service incompatible");
        Ok(incompat.MixTransport.ExecutionCount == 0 && incompat.MixTransport.ComposeExecutionCount == 0,
            "T remux/compose stay 0");

        var mismatch = Harness();
        await SeedReadyAsync(mismatch);
        mismatch.MixTransport.ForceConcatDuration = 12;
        Ok(await Rejects(() => mismatch.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep)),
            ProductionFailureReasons.DurationMismatch), "AB output duration mismatch");

        var reversed = Harness();
        await SeedReadyAsync(reversed);
        Ok(await Rejects(() => reversed.Svc.AssembleEpisodeOsAsync(EpisodeCommand(ep,
                [(sh02.ShotCode, Sh02Asm, Sh02Dur), (sh01.ShotCode, Sh01Asm, Sh01Dur)])),
            ProductionFailureReasons.FailedIdentity), "T2 wrong order");

        var stale = Harness();
        await SeedReadyAsync(stale);
        var staleCurrent = await stale.Store.GetActiveBindingAsync(
            sh01.BuildId, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        var staleVisible = await stale.Store.GetActiveBindingAsync(
            sh01.BuildId, sh01.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly));
        if (staleCurrent is not null) await stale.Store.UnbindAsync(staleCurrent.BindingId, DateTimeOffset.UtcNow);
        if (staleVisible is not null) await stale.Store.UnbindAsync(staleVisible.BindingId, DateTimeOffset.UtcNow);
        var replacement = Guid.NewGuid();
        await SeedAssemblyAsync(stale, sh01, replacement, Sh01Dur);
        Ok(await Rejects(() => stale.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep)),
            ProductionFailureReasons.FailedIdentity), "T5 stale CURRENT:ASSEMBLY");

        var visibleDrift = Harness();
        await SeedReadyAsync(visibleDrift);
        var vis = await visibleDrift.Store.GetActiveBindingAsync(
            sh01.BuildId, sh01.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly));
        if (vis is not null) await visibleDrift.Store.UnbindAsync(vis.BindingId, DateTimeOffset.UtcNow);
        var otherVisible = Guid.NewGuid();
        await SeedArtifactAsync(visibleDrift, sh01, otherVisible, ProductionArtifactRoles.Assembly, Sh01Dur);
        await visibleDrift.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), sh01.BuildId, sh01.ShotCode,
            ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
            otherVisible, Guid.Empty, DateTimeOffset.UtcNow, null));
        Ok(await Rejects(() => visibleDrift.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep)),
            ProductionFailureReasons.FailedIdentity), "T6 VISIBLE != CURRENT");

        var url = Harness();
        await SeedAssemblyAsync(url, sh01, Sh01Asm, Sh01Dur, storageKey: "https://cdn.example/sh01.mp4");
        await SeedAssemblyAsync(url, sh02, Sh02Asm, Sh02Dur);
        Ok(await Rejects(() => url.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep)),
            ProductionFailureReasons.FailedIdentity), "T9 URL StorageKey");

        var callerKey = Harness();
        await SeedReadyAsync(callerKey);
        var overridden = ReadyCommand(ep) with { IdempotencyKey = "caller-override-not-contract" };
        var keyed = await callerKey.Svc.AssembleEpisodeOsAsync(overridden);
        Ok(keyed.Request.IdempotencyKey.StartsWith(ProductionActions.AssembleEpisode + ":", StringComparison.Ordinal)
            && !keyed.Request.IdempotencyKey.Contains("caller-override", StringComparison.Ordinal),
            "caller IdempotencyKey cannot replace contract identity");
    }

    static async Task Concurrent(Action<bool, string> Ok)
    {
        var ep = ProductionEpisodeAssemblyContract.OfficialEpisodeScope();
        var h = Harness();
        await SeedReadyAsync(h);
        var first = h.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep));
        var second = h.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep));
        var results = new List<FilmAssemblySliceResult>();
        var inflight = 0;
        foreach (var job in new[] { first, second })
        {
            try { results.Add(await job); }
            catch (InvalidOperationException ex) when (ex.Message == ProductionFailureReasons.InFlightReplay)
            { inflight++; }
        }
        var outputId = results.FirstOrDefault(r => r.Output is not null)?.Output?.ArtifactId;
        var currents = 0;
        var visibles = 0;
        var films = 0;
        if (outputId is Guid id)
        {
            var current = await h.Store.GetActiveBindingAsync(
                ep.BuildId, "EP01", ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
            var visible = await h.Store.GetActiveBindingAsync(
                ep.BuildId, "EP01", ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly));
            if (current?.ArtifactId == id) currents++;
            if (visible?.ArtifactId == id) visibles++;
            films = (await h.Store.ListAssembliesByBuildAsync(ep.BuildId))
                .Count(a => a.Title == "EP01");
        }
        Ok(h.MixTransport.ConcatExecutionCount == 1
            && results.Count + inflight == 2
            && outputId is not null
            && results.All(r => r.Output is null || r.Output.ArtifactId == outputId)
            && currents == 1
            && visibles == 1
            && films == 1, "concurrent identical episode assembly");
    }

    static async Task HappyReplay(Action<bool, string> Ok)
    {
        var ep = ProductionEpisodeAssemblyContract.OfficialEpisodeScope();
        var h = Harness();
        await SeedReadyAsync(h);
        await SeedHistoricalAsync(h);
        var first = await h.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep));
        Ok(first.Output is { Role: var role } && role == ProductionArtifactRoles.Assembly
            && ProductionMixContract.Dec(first.Output.DurationSec) == ProductionMixContract.Dec(13.042m)
            && first.Output.ParentArtifactIds is { Count: 2 }
            && first.Output.ParentArtifactIds[0] == Sh01Asm
            && first.Output.ParentArtifactIds[1] == Sh02Asm, "happy output ASSEMBLY parents");
        Ok(first.Assembly.Title == "EP01"
            && first.Assembly.AssemblyId != HistSh01
            && first.Assembly.AssemblyId != HistSh02, "new episode FilmAssembly");
        var members = await h.Store.ListAssemblyShotsAsync(first.Assembly.AssemblyId);
        Ok(members.Count == 2
            && members[0].ArtifactId == Sh01Asm && members[0].Order == 1 && members[0].Transition == "CUT"
            && members[1].ArtifactId == Sh02Asm && members[1].Order == 2 && members[1].AudioState == "BOUND",
            "episode membership is ASSEMBLY");
        var sh01Bind = await h.Store.GetActiveBindingAsync(ep.BuildId, "EP01-SC01-SH01",
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        var epBind = await h.Store.GetActiveBindingAsync(ep.BuildId, "EP01",
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        var epVis = await h.Store.GetActiveBindingAsync(ep.BuildId, "EP01",
            ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly));
        var epAccepted = await h.Store.GetActiveBindingAsync(ep.BuildId, "EP01",
            ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Assembly));
        var epFinal = await h.Store.GetActiveBindingAsync(ep.BuildId, "EP01", ProductionBindingRoles.Final);
        Ok(sh01Bind?.ArtifactId == Sh01Asm, "SH01 CURRENT:ASSEMBLY unchanged");
        Ok(epBind?.ArtifactId == first.Output!.ArtifactId && epVis?.ArtifactId == first.Output.ArtifactId
            && epAccepted is null && epFinal is null, "episode CURRENT/VISIBLE only");
        Ok(h.MixTransport.ConcatExecutionCount == 1
            && h.MixTransport.ExecutionCount == 0
            && h.MixTransport.ComposeExecutionCount == 0
            && h.MixTransport.LastConcatCommand == FakeFfmpegMixTransport.ConcatCommand, "concat-copy once");

        var hist1 = await h.Store.ListAssemblyShotsAsync(HistSh01);
        var hist2 = await h.Store.ListAssemblyShotsAsync(HistSh02);
        Ok(hist1.Count == 1 && hist1[0].ArtifactId == Sh01Mix, "historical SH01 FilmAssembly MIX untouched");
        Ok(hist2.Count == 1 && hist2[0].ArtifactId == Sh02Mix, "historical SH02 FilmAssembly MIX untouched");

        var snap = await h.Store.GetSnapshotByTaskAsync(first.Task.TaskId);
        var frozen = new ProductionEpisodeAssemblyContractV1("", "", "", Guid.Empty, "", [], 0, 0, "", "");
        var parsed = snap is not null && ProductionEpisodeAssemblyContract.TryParse(snap.ShotContract, out frozen);
        Ok(parsed
            && snap!.DurationSec == 13.042m
            && snap.ParentArtifactIds!.SequenceEqual(new[] { Sh01Asm, Sh02Asm })
            && frozen.ContractHash.Length == 64, "freeze uses ProductionTaskInputSnapshot");

        var replay = await h.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep));
        Ok(replay.Output?.ArtifactId == first.Output!.ArtifactId
            && replay.Assembly.AssemblyId == first.Assembly.AssemblyId
            && h.MixTransport.ConcatExecutionCount == 1, "X same contract replay");
        Ok(first.Request.IdempotencyKey.StartsWith(
                ProductionActions.AssembleEpisode + ":" + ProductionEpisodeAssemblyContract.Version + ":",
                StringComparison.Ordinal)
            && first.Request.IdempotencyKey.EndsWith(frozen.ContractHash, StringComparison.Ordinal),
            "idempotency is contract identity");
        var preview = await h.Svc.GetPreviewAsync(ep);
        var previewBytes = await h.Svc.ReadPreviewMediaAsync(ep);
        Ok(!preview.IsEmpty && preview.ArtifactId == first.Output.ArtifactId && previewBytes is { Length: >= 32 },
            "preview resolves episode ASSEMBLY");

        var drifted = Harness();
        await SeedReadyAsync(drifted);
        var once = await drifted.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep));
        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        var current = await drifted.Store.GetActiveBindingAsync(
            sh01.BuildId, sh01.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        var visible = await drifted.Store.GetActiveBindingAsync(
            sh01.BuildId, sh01.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly));
        if (current is not null) await drifted.Store.UnbindAsync(current.BindingId, DateTimeOffset.UtcNow);
        if (visible is not null) await drifted.Store.UnbindAsync(visible.BindingId, DateTimeOffset.UtcNow);
        await SeedAssemblyAsync(drifted, sh01, Guid.NewGuid(), Sh01Dur);
        Ok(await Rejects(() => drifted.Svc.AssembleEpisodeOsAsync(ReadyCommand(ep)),
            ProductionFailureReasons.FailedIdentity)
            && once.Output is not null
            && drifted.MixTransport.ConcatExecutionCount == 1, "W changed CURRENT after freeze");
    }

    static void RegressionLocks(Action<bool, string> Ok)
    {
        var src = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Packs", "Content", "KitPlatform.Packs.Content.Application",
            "Production", "ProductionCreateVideoService.Actions.cs"));
        if (!src.Contains("resolved.Count != 1", StringComparison.Ordinal))
            src = File.ReadAllText(FindActions());
        Ok(src.Contains("if (officialFilm && resolved.Count != 1)", StringComparison.Ordinal)
            && src.Contains("art.Role != ProductionArtifactRoles.Mix", StringComparison.Ordinal)
            && src.Contains("AssembleEpisodeOsAsync", StringComparison.Ordinal),
            "shot ASSEMBLE remains count==1 MIX");
        Ok(ProductionOfficialVeoVideoTakeAcceptanceV1Regression.Run().Count == 0, "CK20 PASS");
        Ok(ProductionEp01Sh01RealFilmVoiceV1Regression.Run().Count == 0, "CK22 PASS");
        Ok(ProductionEp01Sh01RealFilmRunV1LipsyncRegression.Run().Count == 0, "CK23B PASS");
        Ok(ProductionOfficialSh02CreateMixV1Regression.Run().Count == 0, "CK24D PASS");
        Ok(ProductionOfficialSh02AssembleV1Regression.Run().Count == 0, "CK26 PASS");
        Ok(ProductionEp01Sh01RealFilmRunV1AssemblyRegression.Run().Count == 0, "SH01 assemble PASS");
    }

    static void Safety(Action<bool, string> Ok)
    {
        var process = File.ReadAllText(FindProcessTransport());
        Ok(process.Contains("ConcatCopyOnceAsync", StringComparison.Ordinal)
            && process.Contains("-f concat", StringComparison.Ordinal)
            && process.Contains("-c copy", StringComparison.Ordinal)
            && !process.Contains("HiddenFallbackForbidden", StringComparison.Ordinal),
            "official concat-copy is the episode transport");
        var controller = File.ReadAllText(FindController());
        Ok(controller.Contains("AssembleEpisodeOsAsync", StringComparison.Ordinal)
            && !controller.Contains("HiddenFallbackForbidden", StringComparison.Ordinal),
            "Director POST ASSEMBLE_EPISODE is official");
        Ok(!ProductionLegacyExportAuthorityV1.MayCreateOfficialArtifact()
            && !ProductionLegacyExportAuthorityV1.MayCreateOfficialBinding()
            && !ProductionLegacyExportAuthorityV1.MayCreateFilmAssembly()
            && !ProductionLegacyExportAuthorityV1.AssembleOneShotMayExecuteOfficialProduction(),
            "T19 T20 legacy assemble stays non-official");
        Ok(ProductionBindingRoles.Final == "FINAL"
            && ProductionArtifactRoles.Assembly == "ASSEMBLY", "FINAL unused");
    }

    static ProductionEpisodeAssemblyInput Row(
        int order, string shot, Guid id, string sha, string storage, decimal dur) =>
        new(order, shot, ProductionArtifactRoles.Assembly, id, sha, storage, dur, 0, 0);

    static ProductionActionApiCommand EpisodeCommand(
        ProductionScope scope,
        IReadOnlyList<(string ShotCode, Guid ArtifactId, decimal DurationSec)> shots,
        string timing = "13.042") =>
        new(scope, Input() with { DurationSec = 13.042m, Timing = timing }, "regression", null, AssemblyShots: shots);

    static ProductionActionApiCommand ReadyCommand(ProductionScope scope) =>
        EpisodeCommand(scope, [("EP01-SC01-SH01", Sh01Asm, Sh01Dur), ("EP01-SC01-SH02", Sh02Asm, Sh02Dur)]);

    static ShotExecutionInput Input() =>
        new(
            ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "13.042", "", "",
            DurationSec: 13.042m);

    static async Task SeedReadyAsync(HarnessBundle h)
    {
        await SeedAssemblyAsync(h, ProductionEp01Sh01IngressCanon.Scope(), Sh01Asm, Sh01Dur);
        await SeedAssemblyAsync(h, ProductionEp01Sh02AssembleCanon.Scope(), Sh02Asm, Sh02Dur);
    }

    static async Task SeedHistoricalAsync(HarnessBundle h)
    {
        await SeedArtifactAsync(h, ProductionEp01Sh01IngressCanon.Scope(), Sh01Mix, ProductionArtifactRoles.Mix, Sh01Dur);
        await SeedArtifactAsync(h, ProductionEp01Sh02AssembleCanon.Scope(), Sh02Mix, ProductionArtifactRoles.Mix, Sh02Dur);
        await h.Store.InsertAssemblyAsync(new FilmAssembly(HistSh01, h.Episode.BuildId, "EP01", DateTimeOffset.UtcNow));
        await h.Store.InsertAssemblyAsync(new FilmAssembly(HistSh02, h.Episode.BuildId, "EP01", DateTimeOffset.UtcNow));
        await h.Store.InsertAssemblyShotAsync(new FilmAssemblyShot(
            Guid.NewGuid(), HistSh01, h.Episode.BuildId, "EP01-SC01-SH01", Sh01Mix, 0, Sh01Dur, "CUT", "BOUND"));
        await h.Store.InsertAssemblyShotAsync(new FilmAssemblyShot(
            Guid.NewGuid(), HistSh02, h.Episode.BuildId, "EP01-SC01-SH02", Sh02Mix, 0, Sh02Dur, "CUT", "BOUND"));
    }

    static async Task SeedAssemblyAsync(
        HarnessBundle h, ProductionScope shot, Guid artifactId, decimal duration, bool verified = true,
        string? sha256 = null, string? storageKey = null)
    {
        await SeedArtifactAsync(h, shot, artifactId, ProductionArtifactRoles.Assembly, duration, verified: verified, sha256: sha256, storageKey: storageKey);
        await BindCurrentAssemblyAsync(h, shot, artifactId);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
            artifactId, Guid.Empty, DateTimeOffset.UtcNow, null));
    }

    static Task BindCurrentAssemblyAsync(HarnessBundle h, ProductionScope shot, Guid artifactId) =>
        h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            artifactId, Guid.Empty, DateTimeOffset.UtcNow, null));

    static async Task SeedArtifactAsync(
        HarnessBundle h, ProductionScope shot, Guid artifactId, string role, decimal duration,
        bool verified = true, string? sha256 = null, string? storageKey = null)
    {
        var pixels = Encoding.ASCII.GetBytes("EP-CK29-" + shot.ShotCode + artifactId.ToString("N")[..8] + new string('Q', 24));
        var kf = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh02IngressCanon.Mime, ProductionEp01Sh02IngressCanon.Story, "asm-ck29",
            ProductionEp01Sh02IngressCanon.ActingBeat, ProductionEp01Sh02IngressCanon.VisualContract, "",
            0, ProductionEp01Sh02IngressCanon.PictureRevisionId, ProductionEp01Sh02IngressCanon.ProvenanceJson()));
        var bytes = ProductionTestMp4.Encode("ep-" + artifactId.ToString("N"), duration);
        var fileSha = sha256 ?? KitVideoIntegrityRules.Sha256Hex(bytes);
        var key = storageKey ?? ("os/ep-" + artifactId.ToString("N")[..12]);
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            artifactId, kf.TaskId, kf.AttemptId, role, ProductionTestMp4.Mime,
            bytes.Length, duration, fileSha, key, "ep-job",
            DateTimeOffset.UtcNow,
            verified ? ProductionArtifactStatuses.Verified : ProductionArtifactStatuses.FailedVerification,
            "ep-hash"));
        if (storageKey is null)
            h.Files.Write("os", "ep-" + artifactId.ToString("N")[..12], bytes);
    }

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static HarnessBundle Harness()
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var mixTransport = new FakeFfmpegMixTransport();
        var scopes = new InMemoryProductionScopeFoundationStore();
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var svc = BuildSvc(store, files, mixTransport, scopes);
        return new HarnessBundle(svc, store, files, mixTransport, scopes,
            ProductionEpisodeAssemblyContract.OfficialEpisodeScope());
    }

    static ProductionCreateVideoService BuildSvc(
        IProductionWorkStore store,
        InMemoryProductionArtifactFileStore files,
        FakeFfmpegMixTransport mixTransport,
        InMemoryProductionScopeFoundationStore scopes)
    {
        var fixture = new FixtureMotionAdapter();
        var permits = new InMemoryProductionExecutionPermitStore();
        var os = new ProductionExecutionService(store, fixture, scopes);
        return new ProductionCreateVideoService(
            os, store, files, fixture, scopes, officialPermits: permits,
            officialMix: new FfmpegMixExecutionAdapter(mixTransport),
            officialAssembly: new FfmpegAssemblyExecutionAdapter(mixTransport),
            officialEpisode: new FfmpegEpisodeAssemblyExecutionAdapter(mixTransport));
    }

    static async Task ProcessFailureConvergence(Action<bool, string> Ok)
    {
        var h = Harness();
        await SeedReadyAsync(h);
        h.MixTransport.FailConcatProcessRemaining = 1;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var winner = BuildSvc(Gate(h.Store, async _ => await entered.Task), h.Files, h.MixTransport, h.Scopes);
        var loser = BuildSvc(Gate(h.Store, async _ =>
        {
            entered.TrySetResult();
            await release.Task;
        }), h.Files, h.MixTransport, h.Scopes);
        var ep = h.Episode;
        var a = Invoke(winner, ep);
        var b = Invoke(loser, ep);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var winnerError = await a.WaitAsync(TimeSpan.FromSeconds(15));
        var tasks = await h.Store.ListTasksByScopeAsync(ep.BuildId, ep.ShotCode);
        var episodeTask = tasks.LastOrDefault(t => t.Action == ProductionActions.AssembleEpisode);
        var attempts = episodeTask is null ? [] : await h.Store.ListAttemptsAsync(episodeTask.TaskId);
        var arts = episodeTask is null ? [] : await h.Store.ListArtifactsByTaskAsync(episodeTask.TaskId);
        Ok(winnerError is not null
            && winnerError.Contains(ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:", StringComparison.Ordinal)
            && episodeTask is not null
            && episodeTask.Status == ProductionTaskStatuses.Failed
            && episodeTask.FailureCode == ProductionFailureReasons.ExecutionProcessFailed
            && episodeTask.FailureReason is not null
            && episodeTask.FailureReason.Contains(":ffmpeg-video:", StringComparison.Ordinal)
            && attempts.Count == 1
            && attempts[0].Status == ProductionAttemptStatuses.ProviderFailed
            && attempts[0].ErrorCode == episodeTask.FailureReason
            && arts.Count == 0
            && h.MixTransport.ConcatExecutionCount == 1, "winner persists EXECUTION_PROCESS_FAILED without an artifact");
        release.TrySetResult();
        var loserError = await b.WaitAsync(TimeSpan.FromSeconds(15));
        var afterLoser = await h.Store.ListAttemptsAsync(episodeTask!.TaskId);
        Ok(loserError == ProductionFailureReasons.ExecutionProcessFailed
            && afterLoser.Count == 1
            && h.MixTransport.ConcatExecutionCount == 1, "loser observes the canonical code and does not start FFmpeg");

        var failedError = attempts[0].ErrorCode;
        var snapBefore = await h.Store.GetSnapshotByTaskAsync(episodeTask.TaskId);
        h.MixTransport.FailConcatProcessRemaining = 0;
        var retry = BuildSvc(h.Store, h.Files, h.MixTransport, h.Scopes);
        var done = await retry.AssembleEpisodeOsAsync(ReadyCommand(ep));
        var retried = await h.Store.ListAttemptsAsync(episodeTask.TaskId);
        var snaps = await h.Store.GetSnapshotByTaskAsync(episodeTask.TaskId);
        Ok(done.Output is not null
            && done.Output.VerificationStatus == ProductionArtifactStatuses.Verified
            && done.Task.Status == ProductionTaskStatuses.Succeeded
            && done.Task.FailureCode is null
            && done.Task.FailureReason is null
            && done.Request.RequestId == episodeTask.RequestId
            && done.Task.TaskId == episodeTask.TaskId
            && snaps is not null
            && snapBefore is not null
            && snaps.SnapshotId == snapBefore.SnapshotId
            && done.Task.ProviderId == ProductionAssemblyCatalog.ServerProviderId
            && done.Task.ModelId == ProductionAssemblyCatalog.FfmpegModelId
            && retried.Count == 2
            && retried[0].Status == ProductionAttemptStatuses.ProviderFailed
            && retried[0].ErrorCode == failedError
            && retried[1].Status == ProductionAttemptStatuses.ProviderSucceeded
            && retried[0].AttemptId != retried[1].AttemptId
            && h.MixTransport.ConcatExecutionCount == 2, "retry reuses the frozen target, clears the task code, and succeeds");
    }

    static async Task LongProcessDiagnosticPersists(Action<bool, string> Ok)
    {
        var h = Harness();
        await SeedReadyAsync(h);
        var suffix = new string('x', 90);
        h.MixTransport.FailProcessSuffix = suffix;
        h.MixTransport.FailConcatProcessRemaining = 1;
        var error = await Invoke(h.Svc, h.Episode);
        var task = (await h.Store.ListTasksByScopeAsync(h.Episode.BuildId, h.Episode.ShotCode))
            .LastOrDefault(t => t.Action == ProductionActions.AssembleEpisode);
        var attempts = task is null ? [] : await h.Store.ListAttemptsAsync(task.TaskId);
        var arts = task is null ? [] : await h.Store.ListArtifactsByTaskAsync(task.TaskId);
        var current = await h.Store.GetActiveBindingAsync(
            h.Episode.BuildId, h.Episode.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        var expected = ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:" + suffix;
        Ok(error == expected
            && expected.Length > 64
            && task is not null
            && task.Status == ProductionTaskStatuses.Failed
            && task.FailureCode == ProductionFailureReasons.ExecutionProcessFailed
            && task.FailureReason == expected
            && (await h.Store.GetRequestAsync(task.RequestId))?.Status == ProductionRequestStatuses.Failed
            && attempts.Count == 1
            && attempts[0].Status == ProductionAttemptStatuses.ProviderFailed
            && attempts[0].ErrorCode is { Length: <= 64 } bounded
            && bounded == expected[..64]
            && string.IsNullOrWhiteSpace(attempts[0].ProviderJobId)
            && arts.Count == 0
            && current is null
            && h.MixTransport.ConcatExecutionCount == 1, "long FFmpeg text persists without overflowing ErrorCode");

        var store = new InMemoryProductionWorkStore();
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var scope = new ProductionScope(Guid.NewGuid(), "EP02", "SC01", "EP02-SC01-SH09");
        var req = await os.CreateRequestAsync(scope, ProductionActions.MixVideo, "gap", "gap-long-provider");
        var created = await os.CreateTaskAsync(req.RequestId);
        await os.FreezeInputAsync(created.TaskId, new ShotExecutionInput(
            "story", "beat", "visual", "", "", "1", "", "", DurationSec: 1), "server", "ffmpeg");
        var started = await os.StartAttemptAsync(created.TaskId, "payload");
        var raw = new string('p', 90);
        await os.FailAttemptAsync(started.AttemptId, raw);
        var savedAttempt = await store.GetAttemptAsync(started.AttemptId);
        var savedTask = await store.GetTaskAsync(created.TaskId);
        Ok(savedAttempt is not null
            && savedAttempt.Status == ProductionAttemptStatuses.ProviderFailed
            && savedAttempt.ErrorCode == raw[..64]
            && savedTask is not null
            && savedTask.Status == ProductionTaskStatuses.Failed
            && savedTask.FailureCode == ProductionFailureReasons.ProviderUnavailable
            && savedTask.FailureReason == raw, "provider failure writer bounds ErrorCode and keeps the full reason");
    }

    static async Task RemuxProcessFailurePersists(Action<bool, string> Ok)
    {
        var store = new InMemoryProductionWorkStore();
        var transport = new FakeFfmpegMixTransport { FailRemuxProcessRemaining = 1 };
        var adapter = new FfmpegMixExecutionAdapter(transport);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(os, store, new InMemoryProductionArtifactFileStore(), new FixtureMotionAdapter(), mix: adapter);
        var shot = new ProductionScope(Guid.Parse("c3c3c3c3-c3c3-4c3c-8c3c-c3c3c3c3c301"), "EP02", "SC01", "EP02-SC01-SH01");
        var lipId = Guid.NewGuid();
        var lipBytes = ProductionTestMp4.Encode("remux-gap-lip", 5);
        var token = ProductionMixFreeze.Token(lipId, KitVideoIntegrityRules.Sha256Hex(lipBytes));
        var req = await os.CreateRequestAsync(shot, ProductionActions.MixVideo, "gap", "remux-gap-process");
        var created = await os.CreateTaskAsync(req.RequestId);
        var snap = await os.FreezeInputAsync(created.TaskId, new ShotExecutionInput(
            "remux gap", "beat", "visual", "", "", "5", "", token,
            DurationSec: 5, ParentArtifactIds: [lipId], LipsyncArtifactId: lipId),
            adapter.ProviderId, adapter.ModelId);
        var attempt = await os.StartAttemptAsync(created.TaskId, snap.SnapshotHash);
        var execute = typeof(ProductionCreateVideoService).GetMethod(
            "ExecuteLocalProcessAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        string? error = null;
        if (execute is not null)
        {
            var pending = (Task)execute.Invoke(svc, new object[]
            {
                attempt.AttemptId,
                (Func<CancellationToken, Task<ProductionMediaResult>>)(ct =>
                    adapter.ExecuteFrozenAsync(snap, new MixStartContext(attempt.AttemptId, lipBytes), ct)),
                CancellationToken.None,
            })!;
            try { await pending; }
            catch (InvalidOperationException ex) { error = ex.Message; }
        }
        var task = await store.GetTaskAsync(created.TaskId);
        var attempts = await store.ListAttemptsAsync(created.TaskId);
        var arts = await store.ListArtifactsByTaskAsync(created.TaskId);
        var mixBinding = await store.GetActiveBindingAsync(
            shot.BuildId, shot.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix));
        Ok(ProductionMixFreeze.TryParse(snap.ShotContract, out var parsedLip, out _)
            && parsedLip == lipId
            && error is not null
            && error.StartsWith(ProductionFailureReasons.FailedIdentity + ":ffmpeg:", StringComparison.Ordinal)
            && transport.ExecutionCount == 1
            && transport.ComposeExecutionCount == 0
            && transport.ConcatExecutionCount == 0
            && task is not null
            && task.Status == ProductionTaskStatuses.Failed
            && task.FailureCode == ProductionFailureReasons.ExecutionProcessFailed
            && (await store.GetRequestAsync(task.RequestId))?.Status == ProductionRequestStatuses.Failed
            && attempts.Count == 1
            && attempts[0].Status == ProductionAttemptStatuses.ProviderFailed
            && string.IsNullOrWhiteSpace(attempts[0].ProviderJobId)
            && arts.Count == 0
            && mixBinding is null
            && snap.CredentialId is null
            && snap.ProviderId == ProductionMixCatalog.ServerProviderId
            && snap.ModelId == ProductionMixCatalog.FfmpegModelId, "MIX remux process failure closes without an artifact");
    }

    static async Task<string?> Invoke(ProductionCreateVideoService svc, ProductionScope ep)
    {
        try
        {
            await svc.AssembleEpisodeOsAsync(ReadyCommand(ep));
            return null;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message;
        }
    }

    static IProductionWorkStore Gate(IProductionWorkStore inner, Func<CancellationToken, Task> before)
    {
        var proxy = DispatchProxy.Create<IProductionWorkStore, EpisodeInsertGate>();
        var gate = (EpisodeInsertGate)(object)proxy;
        gate.Inner = inner;
        gate.Before = before;
        return proxy;
    }

    static string FindActions() => FirstExisting(
        Path.Combine("src", "Packs", "Content", "KitPlatform.Packs.Content.Application",
            "Production", "ProductionCreateVideoService.Actions.cs"));

    static string FindProcessTransport() => FirstExisting(
        Path.Combine("src", "Packs", "Content", "KitPlatform.Packs.Content.Infrastructure",
            "Production", "ProcessFfmpegMixTransport.cs"));

    static string FindController() => FirstExisting(
        Path.Combine("src", "KitPlatform.Api", "Controllers", "Content", "ContentProductionController.cs"));

    static string FirstExisting(string relative)
    {
        var roots = new[]
        {
            AppContext.BaseDirectory,
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."),
            Directory.GetCurrentDirectory(),
            Path.Combine(Directory.GetCurrentDirectory(), ".."),
        };
        foreach (var root in roots)
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            if (File.Exists(full)) return full;
        }
        return relative;
    }

    public class EpisodeInsertGate : DispatchProxy
    {
        internal IProductionWorkStore Inner { get; set; } = null!;
        internal Func<CancellationToken, Task> Before { get; set; } = _ => Task.CompletedTask;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("PROXY");
            if (targetMethod.Name == nameof(IProductionWorkStore.InsertRequestAsync))
                return InsertAsync(targetMethod, args);
            try
            {
                return targetMethod.Invoke(Inner, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                return null;
            }
        }

        async Task InsertAsync(MethodInfo method, object?[]? args)
        {
            var token = CancellationToken.None;
            if (args is not null)
            {
                foreach (var arg in args)
                {
                    if (arg is CancellationToken found)
                        token = found;
                }
            }
            await Before(token).ConfigureAwait(false);
            try
            {
                await ((Task)method.Invoke(Inner, args)!).ConfigureAwait(false);
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeFfmpegMixTransport MixTransport,
        InMemoryProductionScopeFoundationStore Scopes,
        ProductionScope Episode)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeFfmpegMixTransport MixTransport { get; } = MixTransport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public ProductionScope Episode { get; } = Episode;
    }
}
