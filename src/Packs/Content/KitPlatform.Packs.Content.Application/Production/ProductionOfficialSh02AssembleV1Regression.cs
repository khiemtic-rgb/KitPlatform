using System.Text;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public static class ProductionOfficialSh02AssembleV1Regression
{
    public const string SuiteId = ProductionEp01Sh02AssembleCanon.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }
        try { RunAsync(Ok).GetAwaiter().GetResult(); }
        catch (Exception ex) { fail.Add("UNCAUGHT:" + ex.GetType().Name + ":" + ex.Message); }
        return fail;
    }

    static async Task RunAsync(Action<bool, string> Ok)
    {
        var sh02 = ProductionEp01Sh02AssembleCanon.Scope();
        var sh03 = new ProductionScope(sh02.BuildId, "EP01", "SC01", "EP01-SC01-SH03");
        var ep99 = new ProductionScope(ProductionScopeAnchorRules.OfficialEp99, "EP99", "SC01", "EP99-SC01-SH01");

        var wrongId = Harness(sh02);
        var otherMix = Guid.NewGuid();
        await SeedMixAsync(wrongId, sh02, otherMix, 8);
        await wrongId.Svc.AcceptOsAsync(AcceptCommand(sh02, otherMix));
        Ok(await Rejects(() => wrongId.Svc.AssembleOsAsync(AssembleCommand(sh02, otherMix)),
            ProductionFailureReasons.BindingChanged), "A wrong MIX ID");
        Ok(wrongId.MixTransport.ExecutionCount == 0, "A no remux");

        var missing = Harness(sh02);
        await SeedMixAsync(missing, sh02, ProductionEp01Sh02AssembleCanon.MixArtifactId, 8);
        Ok(await Rejects(() => missing.Svc.AssembleOsAsync(
            AssembleCommand(sh02, ProductionEp01Sh02AssembleCanon.MixArtifactId)),
            ProductionFailureReasons.MissingDependency), "B missing generic ACCEPTED");
        Ok(missing.MixTransport.ExecutionCount == 0, "B no remux");

        var role = Harness(sh03);
        var videoId = Guid.NewGuid();
        await SeedArtifactAsync(role, sh03, videoId, ProductionArtifactRoles.VideoTake, 8);
        await role.Svc.AcceptOsAsync(AcceptCommand(sh03, videoId));
        Ok(await Rejects(() => role.Svc.AssembleOsAsync(AssembleCommand(sh03, videoId)),
            ProductionFailureReasons.FailedIdentity), "C / I VIDEO role");

        var lip = Harness(sh03);
        var lipId = Guid.NewGuid();
        await SeedArtifactAsync(lip, sh03, lipId, ProductionArtifactRoles.Lipsync, 2.5m);
        await lip.Svc.AcceptOsAsync(AcceptCommand(sh03, lipId));
        Ok(await Rejects(() => lip.Svc.AssembleOsAsync(AssembleCommand(sh03, lipId)),
            ProductionFailureReasons.FailedIdentity), "J LIPSYNC role");

        var crossShot = Harness(sh02);
        var sh01Mix = Guid.NewGuid();
        var sh01 = ProductionEp01Sh01IngressCanon.Scope();
        await SeedMixAsync(crossShot, sh01, sh01Mix, 5.042m);
        await crossShot.Svc.AcceptOsAsync(AcceptCommand(sh01, sh01Mix));
        Ok(await Rejects(() => crossShot.Svc.AssembleOsAsync(AssembleCommand(sh02, sh01Mix)),
            ProductionFailureReasons.CrossShotForbidden), "D cross-shot MIX");

        var crossBuild = Harness(ep99);
        crossBuild.Scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp99);
        var ep01Mix = Guid.NewGuid();
        await SeedMixAsync(crossBuild, sh03, ep01Mix, 8);
        await crossBuild.Svc.AcceptOsAsync(AcceptCommand(sh03, ep01Mix));
        Ok(await Rejects(() => crossBuild.Svc.AssembleOsAsync(
            new ProductionActionApiCommand(ep99, Input() with { ParentArtifactIds = [ep01Mix], DurationSec = 8 },
                "regression", null, AssemblyShots: [(sh03.ShotCode, ep01Mix, 8)])),
            ProductionFailureReasons.CrossScopeForbidden), "E cross-build MIX");

        var sha = Harness(sh02);
        await SeedMixAsync(sha, sh02, ProductionEp01Sh02AssembleCanon.MixArtifactId, 8);
        await sha.Svc.AcceptOsAsync(AcceptCommand(sh02, ProductionEp01Sh02AssembleCanon.MixArtifactId));
        Ok(await Rejects(() => sha.Svc.AssembleOsAsync(
            AssembleCommand(sh02, ProductionEp01Sh02AssembleCanon.MixArtifactId)),
            ProductionFailureReasons.HashChanged), "F wrong SHA");
        Ok(sha.MixTransport.ExecutionCount == 0, "F no remux");

        var unverified = Harness(sh03);
        var badId = Guid.NewGuid();
        await SeedMixAsync(unverified, sh03, badId, 8, verified: false);
        await unverified.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.Accepted,
            badId, Guid.Empty, DateTimeOffset.UtcNow, null));
        Ok(await Rejects(() => unverified.Svc.AssembleOsAsync(AssembleCommand(sh03, badId)),
            ProductionFailureReasons.FailedVerification), "G MIX verification failure");

        var h = Harness(sh03);
        var liveMix = Guid.NewGuid();
        await SeedMixAsync(h, sh03, liveMix, 8);
        await h.Svc.AcceptOsAsync(AcceptCommand(sh03, liveMix));
        var first = await h.Svc.AssembleOsAsync(AssembleCommand(sh03, liveMix));
        Ok(first.Output is { Role: var roleOut } && roleOut == ProductionArtifactRoles.Assembly
            && first.Output.DurationSec == 8
            && first.Output.ParentArtifactIds?.Contains(liveMix) == true, "SH03 assemble 8s parent MIX");
        Ok(h.MixTransport.ExecutionCount == 1 && h.MixTransport.ComposeExecutionCount == 0, "compose 0 remux 1");
        var replay = await h.Svc.AssembleOsAsync(AssembleCommand(sh03, liveMix));
        Ok(replay.Output?.ArtifactId == first.Output!.ArtifactId
            && h.MixTransport.ExecutionCount == 1, "H replay same ASSEMBLY");

        Ok(ProcessFfmpegMixTransportCanon.RemuxCommand.Contains("-c copy", StringComparison.Ordinal)
            && !ProcessFfmpegMixTransportCanon.RemuxCommand.Contains("filter_complex", StringComparison.Ordinal),
            "remux is -c copy");
        Ok(h.FalTransport.StartCount == 0 && h.VoiceTransport.SynthesizeCount == 0 && h.RunwayTransport.CreateCalls == 0,
            "no Fal / ElevenLabs / Runway");

        var boom = Harness(sh03);
        var boomMix = Guid.NewGuid();
        await SeedMixAsync(boom, sh03, boomMix, 8);
        await boom.Svc.AcceptOsAsync(AcceptCommand(sh03, boomMix));
        boom.MixTransport.FailRemuxProcessRemaining = 1;
        string? assembleError = null;
        try { await boom.Svc.AssembleOsAsync(AssembleCommand(sh03, boomMix)); }
        catch (InvalidOperationException ex) { assembleError = ex.Message; }
        var asmTask = (await boom.Store.ListTasksByScopeAsync(sh03.BuildId, sh03.ShotCode))
            .LastOrDefault(t => t.Action == ProductionActions.Assemble);
        var asmAttempts = asmTask is null ? [] : await boom.Store.ListAttemptsAsync(asmTask.TaskId);
        var asmArts = asmTask is null ? [] : await boom.Store.ListArtifactsByTaskAsync(asmTask.TaskId);
        var asmCurrent = await boom.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly));
        var asmVisible = await boom.Store.GetActiveBindingAsync(
            sh03.BuildId, sh03.ShotCode, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly));
        var asmSnap = asmTask is null ? null : await boom.Store.GetSnapshotByTaskAsync(asmTask.TaskId);
        Ok(assembleError is not null
            && assembleError.StartsWith(ProductionFailureReasons.FailedIdentity + ":ffmpeg:", StringComparison.Ordinal)
            && boom.MixTransport.ExecutionCount == 1
            && boom.MixTransport.ComposeExecutionCount == 0
            && asmTask is not null
            && asmTask.Status == ProductionTaskStatuses.Failed
            && asmTask.FailureCode == ProductionFailureReasons.ExecutionProcessFailed
            && (await boom.Store.GetRequestAsync(asmTask.RequestId))?.Status == ProductionRequestStatuses.Failed
            && asmAttempts.Count == 1
            && asmAttempts[0].Status == ProductionAttemptStatuses.ProviderFailed
            && string.IsNullOrWhiteSpace(asmAttempts[0].ProviderJobId)
            && asmArts.Count == 0
            && asmCurrent is null
            && asmVisible is null
            && asmSnap?.CredentialId is null
            && boom.FalTransport.StartCount == 0
            && boom.VoiceTransport.SynthesizeCount == 0
            && boom.RunwayTransport.CreateCalls == 0, "shot assembly process failure closes without an output binding");
        var films = await boom.Store.ListAssembliesByBuildAsync(sh03.BuildId);
        var members = films.Count == 0 ? [] : await boom.Store.ListAssemblyShotsAsync(films[0].AssemblyId);
        Ok(films.Count == 1
            && members.Count == 1
            && members[0].ArtifactId == boomMix
            && asmArts.Count == 0, "shot failure keeps the pre-FFmpeg input film and writes no ASSEMBLY output");

        var sh01Suite = ProductionEp01Sh01RealFilmRunV1AssemblyRegression.Run();
        Ok(sh01Suite.Count == 0, "SH01 assemble suite still PASS");
    }

    static ProductionActionApiCommand AcceptCommand(ProductionScope scope, Guid artifact) =>
        new(scope, Input() with { AcceptArtifactId = artifact, ParentArtifactIds = [artifact] },
            "regression", "asm-accept-" + scope.ShotCode + "-" + artifact.ToString("N")[..8]);

    static ProductionActionApiCommand AssembleCommand(ProductionScope scope, Guid artifact) =>
        new(scope, Input() with { ParentArtifactIds = [artifact], DurationSec = 8 },
            "regression", null,
            AssemblyShots: [(scope.ShotCode, artifact, 8)]);

    static ShotExecutionInput Input() =>
        new(
            ProductionEp01Sh02IngressCanon.Story,
            ProductionEp01Sh02IngressCanon.ActingBeat,
            ProductionEp01Sh02IngressCanon.VisualContract,
            "", "", "8.0s", "", "",
            DurationSec: 8);

    static async Task<bool> Rejects(Func<Task> act, string reason)
    {
        try { await act(); return false; }
        catch (InvalidOperationException ex) { return ex.Message == reason; }
    }

    static async Task SeedMixAsync(
        HarnessBundle h, ProductionScope shot, Guid mixId, decimal duration,
        string? sha = null, bool verified = true)
    {
        await SeedArtifactAsync(h, shot, mixId, ProductionArtifactRoles.Mix, duration, sha, verified);
        await h.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), shot.BuildId, shot.ShotCode,
            ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix),
            mixId, Guid.Empty, DateTimeOffset.UtcNow, null));
    }

    static async Task SeedArtifactAsync(
        HarnessBundle h, ProductionScope shot, Guid artifactId, string role, decimal duration,
        string? sha = null, bool verified = true)
    {
        var pixels = Encoding.ASCII.GetBytes("ASM-CK26-" + shot.ShotCode + artifactId.ToString("N")[..8] + new string('Q', 24));
        var kf = await h.Svc.RegisterKeyframeAsync(new RegisterKeyframeCommand(
            shot, pixels, ProductionEp01Sh02IngressCanon.Mime, ProductionEp01Sh02IngressCanon.Story, "asm-ck26",
            ProductionEp01Sh02IngressCanon.ActingBeat, ProductionEp01Sh02IngressCanon.VisualContract, "",
            0, ProductionEp01Sh02IngressCanon.PictureRevisionId, ProductionEp01Sh02IngressCanon.ProvenanceJson()));
        var bytes = ProductionTestMp4.Encode("asm-" + artifactId.ToString("N"), duration);
        var fileSha = sha ?? KitVideoIntegrityRules.Sha256Hex(bytes);
        var key = "os/asm-" + artifactId.ToString("N")[..12];
        await h.Store.InsertArtifactAsync(new ProductionArtifact(
            artifactId, kf.TaskId, kf.AttemptId, role, ProductionTestMp4.Mime,
            bytes.Length, duration, fileSha, key, "asm-job",
            DateTimeOffset.UtcNow,
            verified ? ProductionArtifactStatuses.Verified : ProductionArtifactStatuses.FailedVerification,
            "asm-hash"));
        h.Files.Write("os", "asm-" + artifactId.ToString("N")[..12], bytes);
    }

    static HarnessBundle Harness(ProductionScope shot)
    {
        var store = new InMemoryProductionWorkStore();
        var files = new InMemoryProductionArtifactFileStore();
        var mixTransport = new FakeFfmpegMixTransport();
        var fixture = new FixtureMotionAdapter();
        var scopes = new InMemoryProductionScopeFoundationStore();
        scopes.SeedFilm(ProductionScopeAnchorRules.OfficialEp01);
        var permits = new InMemoryProductionExecutionPermitStore();
        var runway = new FakeRunwayTransport();
        var voice = new FakeElevenLabsVoiceTransport();
        var fal = new FakeFalLipsyncTransport();
        var os = new ProductionExecutionService(store, fixture, scopes);
        var svc = new ProductionCreateVideoService(
            os, store, files, fixture, scopes, officialPermits: permits,
            officialMix: new FfmpegMixExecutionAdapter(mixTransport),
            officialAssembly: new FfmpegAssemblyExecutionAdapter(mixTransport));
        return new HarnessBundle(svc, os, store, files, mixTransport, scopes, runway, voice, fal);
    }

    sealed class HarnessBundle(
        ProductionCreateVideoService Svc,
        ProductionExecutionService Os,
        InMemoryProductionWorkStore Store,
        InMemoryProductionArtifactFileStore Files,
        FakeFfmpegMixTransport MixTransport,
        InMemoryProductionScopeFoundationStore Scopes,
        FakeRunwayTransport RunwayTransport,
        FakeElevenLabsVoiceTransport VoiceTransport,
        FakeFalLipsyncTransport FalTransport)
    {
        public ProductionCreateVideoService Svc { get; } = Svc;
        public ProductionExecutionService Os { get; } = Os;
        public InMemoryProductionWorkStore Store { get; } = Store;
        public InMemoryProductionArtifactFileStore Files { get; } = Files;
        public FakeFfmpegMixTransport MixTransport { get; } = MixTransport;
        public InMemoryProductionScopeFoundationStore Scopes { get; } = Scopes;
        public FakeRunwayTransport RunwayTransport { get; } = RunwayTransport;
        public FakeElevenLabsVoiceTransport VoiceTransport { get; } = VoiceTransport;
        public FakeFalLipsyncTransport FalTransport { get; } = FalTransport;
    }
}

public static class ProcessFfmpegMixTransportCanon
{
    public const string RemuxCommand =
        "ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4";
}
