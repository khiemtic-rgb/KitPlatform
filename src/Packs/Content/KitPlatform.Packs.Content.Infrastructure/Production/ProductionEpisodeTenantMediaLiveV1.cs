using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using KitPlatform.Infrastructure.Data;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// PRODUCTION-EPISODE-TENANT-MEDIA-01. One tenant-owned EP01 build, MIX compose, and shot assembly.
/// Does not call ASSEMBLE_EPISODE.
/// </summary>
public static class ProductionEpisodeTenantMediaLiveV1
{
    public static readonly Guid BuildId = Guid.Parse("c1c1c1c1-c1c1-c1c1-c1c1-c1c1c1c1c101");
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    public const string EpisodeCode = "EP01";

    public static async Task<string> ExecuteAsync(string connectionString, string videoRoot, CancellationToken ct = default)
    {
        if (ProductionScopeAnchorRules.IsOfficialFilm(BuildId))
            throw new InvalidOperationException("BUILD_IS_HISTORICAL_OFFICIAL_FILM");
        var db = new NpgsqlConnectionFactory(connectionString);
        var callerA = new FixedCaller(TenantA);
        var callerB = new FixedCaller(TenantB);
        var store = new ProductionWorkRepository(db, callerA);
        var storeB = new ProductionWorkRepository(db, callerB);
        var content = new ContentRepository(db, callerA);
        var contentB = new ContentRepository(db, callerB);
        var permits = new ProductionExecutionPermitRepository(db, callerA);
        var options = Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot });
        var files = new ProductionArtifactFileStore(new KitVideoVideoStore(options, new HostEnv(videoRoot)), callerA);
        var audit = new ProductionFfmpegMixAudit();
        var transport = new ProcessFfmpegMixTransport(audit);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(
            os, store, files, new FixtureMotionAdapter(),
            officialPermits: permits,
            officialMix: new FfmpegMixExecutionAdapter(transport),
            officialAssembly: new FfmpegAssemblyExecutionAdapter(transport));
        var engine = ProductionWorkExecutionEngine.CreateOfficial(store, null);

        var report = new StringBuilder();
        var filesBefore = CountFiles(videoRoot);
        var before = await CountsAsync(db, ct);
        report.AppendLine("BEFORE " + before);
        report.AppendLine("FILES_BEFORE " + filesBefore);

        await EnsureTenantAsync(db, ct);
        var build = await content.UpsertSeriesBuildAsync(
            BuildId, "TENANT_EP01", EpisodeCode, "EP01", "draft", 2, 0, 0, 0, "", ct);
        report.AppendLine("BUILD " + build.Id + " episode=" + build.EpisodeCode);

        var shots = new (string Code, int Seconds, int VideoHz, int LipHz)[]
        {
            ("EP01-SC01-SH01", 4, 440, 880),
            ("EP01-SC01-SH02", 6, 523, 659),
        };
        var assemblies = new List<ProductionArtifact>();
        foreach (var shot in shots)
        {
            var scope = new ProductionScope(BuildId, EpisodeCode, "SC01", shot.Code);
            var video = await EnsureParentAsync(engine, files, store, scope, ProductionActions.CreateVideo, ProductionArtifactRoles.VideoTake, shot.Seconds, shot.VideoHz, ct);
            var lipsync = await EnsureParentAsync(engine, files, store, scope, ProductionActions.CreateLipsync, ProductionArtifactRoles.Lipsync, shot.Seconds, shot.LipHz, ct);
            if (lipsync.DurationSec > video.DurationSec)
                throw new InvalidOperationException("LIPSYNC_LONGER_THAN_VIDEO " + shot.Code);
            var mix = await EnsureMixAsync(svc, permits, store, scope, lipsync.ArtifactId, ct);
            await EnsureAcceptedAsync(svc, store, scope, mix, ct);
            var assembly = await EnsureAssemblyAsync(svc, store, scope, mix, ct);
            assemblies.Add(assembly);
            report.AppendLine(await DescribeAsync(db, files, "MIX", mix, ct));
            report.AppendLine(await DescribeAsync(db, files, "ASSEMBLY", assembly, ct));
            report.AppendLine("PARENTS " + shot.Code
                + " video=" + video.ArtifactId + " lip=" + lipsync.ArtifactId
                + " mix=" + mix.ArtifactId + " assembly=" + assembly.ArtifactId);
        }

        var (concatOk, probe) = ProbeConcat(files, assemblies[0], assemblies[1]);
        report.AppendLine(probe);
        report.AppendLine(await IsolationAsync(content, contentB, store, storeB, svc, assemblies, ct));
        report.AppendLine(await SafetyAsync(db, ct));
        var after = await CountsAsync(db, ct);
        report.AppendLine("AFTER " + after);
        report.AppendLine("FILES_AFTER " + CountFiles(videoRoot));
        report.AppendLine("COMPOSE " + audit.ComposeExecutionCount + " REMUX " + audit.ExecutionCount + " CONCAT_OFFICIAL " + audit.ConcatExecutionCount);
        report.Insert(0, (concatOk ? "VERDICT PASS_MEDIA" : "VERDICT BLOCKED_CONCAT_DURATION") + Environment.NewLine);
        return report.ToString();
    }

    static async Task EnsureTenantAsync(IDbConnectionFactory db, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO public.tenants (id, tenant_code, tenant_name, business_vertical)
            SELECT @Id, 'CONTENT_EP01_SCOPE', 'Content EP01 scope fixture', 'hybrid'
            WHERE NOT EXISTS (SELECT 1 FROM public.tenants WHERE id = @Id)
            """, new { Id = TenantA }, cancellationToken: ct));
    }

    static async Task<ProductionArtifact> EnsureParentAsync(
        ProductionWorkExecutionEngine engine,
        IProductionArtifactFileStore files,
        IProductionWorkStore store,
        ProductionScope scope,
        string action,
        string role,
        int seconds,
        int hz,
        CancellationToken ct)
    {
        var current = await store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(role), ct);
        if (current is not null)
            return await store.GetArtifactAsync(current.ArtifactId, ct)
                ?? throw new InvalidOperationException("PARENT_MISSING");
        var bytes = RenderSource(seconds, hz);
        if (!ProductionTestMp4.TryReadDuration(bytes, out var duration) || duration <= 0)
            throw new InvalidOperationException("SOURCE_DURATION");
        var request = await engine.CreateRequestAsync(scope, action, "tenant-media", "source-" + scope.ShotCode + "-" + role, ct: ct);
        var task = await engine.CreateTaskAsync(request.RequestId, ct);
        var snap = await engine.FreezeInputAsync(task.TaskId, new ShotExecutionInput(
            "EP01 tenant-owned source", "beat", "visual", "", "",
            duration.ToString("0.###", CultureInfo.InvariantCulture) + "s", "", "",
            DurationSec: duration), "server", "ffmpeg", ct);
        var attempt = await engine.StartAttemptAsync(task.TaskId, snap.SnapshotHash, ct);
        var storage = files.Write(ProductionStoragePolicy.FilmVideoFolder, attempt.AttemptId.ToString("N"), bytes);
        await engine.SubmitProviderJobAsync(attempt.AttemptId, "ffmpeg-source-" + attempt.AttemptId.ToString("N")[..12], ct);
        await engine.RecordProviderSucceededAsync(attempt.AttemptId, KitVideoIntegrityRules.Sha256Hex(bytes), ct);
        var artifact = await engine.VerifyArtifactAsync(attempt.AttemptId, bytes, ProductionTestMp4.Mime, duration, storage, ct);
        await engine.BindAsync(artifact.ArtifactId, ProductionBindingRoles.CurrentFor(role), ct);
        return artifact;
    }

    static async Task<ProductionArtifact> EnsureMixAsync(
        ProductionCreateVideoService svc,
        IProductionExecutionPermitStore permits,
        IProductionWorkStore store,
        ProductionScope scope,
        Guid lipsyncId,
        CancellationToken ct)
    {
        var current = await store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Mix), ct);
        if (current is not null)
            return await store.GetArtifactAsync(current.ArtifactId, ct)
                ?? throw new InvalidOperationException("MIX_MISSING");
        var issued = await permits.GetIssuedAsync(
            scope.BuildId, scope.ShotCode, ProductionActions.MixVideo,
            ProductionMixCatalog.ServerProviderId, ProductionMixCatalog.FfmpegModelId, ct);
        if (issued is null)
        {
            await permits.IssueAsync(new IssueProductionExecutionPermitCommand(
                scope.BuildId, scope.ShotCode, scope.EpisodeCode, scope.SceneCode,
                ProductionActions.MixVideo,
                ProductionMixCatalog.ServerProviderId,
                ProductionMixCatalog.FfmpegModelId,
                "tenant-media",
                "tenant-owned EP01 MIX compose"), ct);
        }
        var slice = await svc.MixVideoAsync(new ProductionActionApiCommand(
            scope,
            new ShotExecutionInput(
                "EP01 tenant-owned source", "beat", "visual", "", "", "mix", "", "",
                LipsyncArtifactId: lipsyncId),
            "tenant-media",
            "mix-" + scope.ShotCode), ct);
        return slice.Artifact;
    }

    static async Task EnsureAcceptedAsync(
        ProductionCreateVideoService svc,
        IProductionWorkStore store,
        ProductionScope scope,
        ProductionArtifact mix,
        CancellationToken ct)
    {
        var accepted = await store.GetActiveBindingAsync(scope.BuildId, scope.ShotCode, ProductionBindingRoles.Accepted, ct);
        if (accepted is not null && accepted.ArtifactId == mix.ArtifactId)
            return;
        await svc.AcceptOsAsync(new ProductionActionApiCommand(
            scope,
            new ShotExecutionInput(
                "EP01 tenant-owned source", "beat", "visual", "", "", "", "", "",
                AcceptArtifactId: mix.ArtifactId),
            "tenant-media",
            "accept-mix-" + mix.ArtifactId.ToString("N")), ct);
    }

    static async Task<ProductionArtifact> EnsureAssemblyAsync(
        ProductionCreateVideoService svc,
        IProductionWorkStore store,
        ProductionScope scope,
        ProductionArtifact mix,
        CancellationToken ct)
    {
        var current = await store.GetActiveBindingAsync(
            scope.BuildId, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
        if (current is not null)
            return await store.GetArtifactAsync(current.ArtifactId, ct)
                ?? throw new InvalidOperationException("ASSEMBLY_MISSING");
        var slice = await svc.AssembleOsAsync(new ProductionActionApiCommand(
            scope,
            new ShotExecutionInput(
                "EP01 tenant-owned source", "beat", "visual", "", "", "assemble", "", "",
                DurationSec: mix.DurationSec,
                ParentArtifactIds: [mix.ArtifactId]),
            "tenant-media",
            "assemble-" + scope.ShotCode,
            AssemblyShots: [(scope.ShotCode, mix.ArtifactId, mix.DurationSec)]), ct);
        return slice.Output ?? throw new InvalidOperationException("ASSEMBLY_OUTPUT_MISSING");
    }

    static (bool Ok, string Text) ProbeConcat(IProductionArtifactFileStore files, ProductionArtifact sh01, ProductionArtifact sh02)
    {
        var a = files.Read(sh01.StorageKey) ?? throw new InvalidOperationException("SH01_BYTES_MISSING");
        var b = files.Read(sh02.StorageKey) ?? throw new InvalidOperationException("SH02_BYTES_MISSING");
        if (KitVideoIntegrityRules.Sha256Hex(a) != sh01.ByteSha256 || KitVideoIntegrityRules.Sha256Hex(b) != sh02.ByteSha256)
            throw new InvalidOperationException("PHYSICAL_SHA_MISMATCH");
        var ffmpeg = ResolveTool("ffmpeg") ?? throw new InvalidOperationException("FFMPEG_MISSING");
        var ffprobe = ResolveTool("ffprobe") ?? throw new InvalidOperationException("FFPROBE_MISSING");
        var work = Path.Combine(Path.GetTempPath(), "famixa-ep-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var p1 = Path.Combine(work, "sh01.mp4");
            var p2 = Path.Combine(work, "sh02.mp4");
            var list = Path.Combine(work, "list.txt");
            var output = Path.Combine(work, "output.mp4");
            File.WriteAllBytes(p1, a);
            File.WriteAllBytes(p2, b);
            File.WriteAllLines(list, ["file '" + p1.Replace("'", "'\\''") + "'", "file '" + p2.Replace("'", "'\\''") + "'"]);
            var code = Run(ffmpeg, "-y -hide_banner -loglevel error -f concat -safe 0 -i \"" + list + "\" -c copy -movflags +faststart \"" + output + "\"", work);
            if (code != 0 || !File.Exists(output))
                throw new InvalidOperationException("CONCAT_COPY_FAILED");
            var bytes = File.ReadAllBytes(output);
            if (!ProductionTestMp4.TryReadDuration(bytes, out var got))
                throw new InvalidOperationException("CONCAT_DURATION_UNREADABLE");
            var sum = sh01.DurationSec + sh02.DurationSec;
            var line = "CONCAT mvhd=" + got.ToString(CultureInfo.InvariantCulture)
                + " sum=" + sum.ToString(CultureInfo.InvariantCulture)
                + " decGot=" + ProductionMixContract.Dec(got)
                + " decSum=" + ProductionMixContract.Dec(sum)
                + " ffprobe=" + RunText(ffprobe, "-v error -show_entries format=duration -of default=nw=1:nk=1 \"" + output + "\"")
                + "\nSH01 " + ProbeFile(ffprobe, p1)
                + "\nSH02 " + ProbeFile(ffprobe, p2);
            return (ProductionMixContract.Dec(got) == ProductionMixContract.Dec(sum), line);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    static async Task<string> IsolationAsync(
        ContentRepository contentA,
        ContentRepository contentB,
        IProductionWorkStore storeA,
        IProductionWorkStore storeB,
        ProductionCreateVideoService svcA,
        IReadOnlyList<ProductionArtifact> assemblies,
        CancellationToken ct)
    {
        var own = await contentA.GetSeriesBuildAsync(BuildId, ct);
        var other = await contentB.GetSeriesBuildAsync(BuildId, ct);
        var histA = await contentA.GetSeriesBuildAsync(ProductionScopeAnchorRules.OfficialEp01, ct);
        var histB = await contentB.GetSeriesBuildAsync(ProductionScopeAnchorRules.OfficialEp01, ct);
        var artB = await storeB.GetArtifactAsync(assemblies[0].ArtifactId, ct);
        var histArt = await storeA.GetArtifactAsync(ProductionEp01Sh01AssemblyCanon.MixArtifactId, ct);
        var forbidden = false;
        try
        {
            await storeB.InsertRequestAsync(new ProductionRequest(
                Guid.NewGuid(),
                new ProductionScope(BuildId, EpisodeCode, "SC01", "EP01-SC01-SH01"),
                ProductionActions.Assemble, ProductionArtifactRoles.Assembly,
                DateTimeOffset.UtcNow, "tenant-b", ProductionRequestStatuses.Submitted, null,
                "tenant-b-denied", []), ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == ProductionTenantBoundary.Forbidden)
        {
            forbidden = true;
        }
        _ = svcA;
        return "ISOLATION ownBuild=" + (own?.Id.ToString() ?? "null")
            + " tenantBBuild=" + (other is null ? "null" : "LEAK")
            + " histA=" + (histA is null ? "null" : "LEAK")
            + " histB=" + (histB is null ? "null" : "LEAK")
            + " tenantBArtifact=" + (artB is null ? "null" : "LEAK")
            + " histArtifact=" + (histArt is null ? "null" : "LEAK")
            + " tenantBAssemble=" + (forbidden ? "FORBIDDEN" : "OPEN");
    }

    static async Task<string> SafetyAsync(IDbConnectionFactory db, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var episode = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_tasks
            WHERE action = 'ASSEMBLE_EPISODE' AND build_id = @Build
            """, new { Build = BuildId }, cancellationToken: ct));
        var ai = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build
              AND lower(a.provider_id) IN ('runway','fal','elevenlabs','google','veo','openai','gemini')
            """, new { Build = BuildId }, cancellationToken: ct));
        var creds = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build
              AND (s.credential_id IS NOT NULL OR s.credential_version_id IS NOT NULL)
            """, new { Build = BuildId }, cancellationToken: ct));
        var histParents = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_artifacts
            WHERE tenant_id = @Tenant
              AND (
                parent_artifact_ids::text ILIKE '%2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2%'
                OR parent_artifact_ids::text ILIKE '%58297402-e252-45e8-8d33-c7414749a297%'
                OR parent_artifact_ids::text ILIKE '%2f88e0a0313a47c68c1d2dbb189bbbf2%'
                OR parent_artifact_ids::text ILIKE '%58297402e25245e88d33c7414749a297%')
            """, new { Tenant = TenantA }, cancellationToken: ct));
        var histBuild = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.series_build
            WHERE id = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f' AND tenant_id IS NULL
            """, cancellationToken: ct));
        return "SAFETY episodeTasks=" + episode + " aiJobs=" + ai + " credentials=" + creds
            + " historicalParents=" + histParents + " historicalBuildNull=" + histBuild;
    }

    static async Task<string> DescribeAsync(IDbConnectionFactory db, IProductionArtifactFileStore files, string label, ProductionArtifact art, CancellationToken ct)
    {
        var bytes = files.Read(art.StorageKey);
        var sha = bytes is null ? "MISSING" : KitVideoIntegrityRules.Sha256Hex(bytes);
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync(new CommandDefinition("""
            SELECT a.tenant_id::text AS tenant, t.build_id::text AS build, t.shot_code AS shot, a.role, a.verification_status AS status
            FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE a.artifact_id = @Id
            """, new { Id = art.ArtifactId }, cancellationToken: ct));
        var current = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE artifact_id = @Id AND unbound_at IS NULL AND role = @Role AND tenant_id = @Tenant
            """, new { Id = art.ArtifactId, Role = ProductionBindingRoles.CurrentFor(art.Role), Tenant = TenantA }, cancellationToken: ct));
        var visible = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE artifact_id = @Id AND unbound_at IS NULL AND role = @Role AND tenant_id = @Tenant
            """, new { Id = art.ArtifactId, Role = ProductionBindingRoles.VisibleFor(art.Role), Tenant = TenantA }, cancellationToken: ct));
        var accepted = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE artifact_id = @Id AND unbound_at IS NULL AND role = @Role AND tenant_id = @Tenant
            """, new { Id = art.ArtifactId, Role = art.Role == ProductionArtifactRoles.Mix ? ProductionBindingRoles.Accepted : ProductionBindingRoles.AcceptedFor(art.Role), Tenant = TenantA }, cancellationToken: ct));
        return label + " id=" + art.ArtifactId
            + " tenant=" + row.tenant + " build=" + row.build + " shot=" + row.shot
            + " role=" + row.role + " status=" + row.status
            + " key=" + art.StorageKey
            + " sha=" + art.ByteSha256
            + " physical=" + sha
            + " duration=" + art.DurationSec.ToString(CultureInfo.InvariantCulture)
            + " current=" + current + " visible=" + visible + " accepted=" + accepted;
    }

    static async Task<string> CountsAsync(IDbConnectionFactory db, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var row = await conn.QuerySingleAsync(new CommandDefinition("""
            SELECT
              (SELECT count(*) FROM pack_content.series_build) AS builds,
              (SELECT count(*) FROM pack_content.series_build WHERE tenant_id IS NULL) AS null_builds,
              (SELECT count(*) FROM pack_content.series_build WHERE tenant_id IS NOT NULL) AS owned_builds,
              (SELECT count(*) FROM pack_content.production_requests) AS requests,
              (SELECT count(*) FROM pack_content.production_tasks) AS tasks,
              (SELECT count(*) FROM pack_content.production_attempts) AS attempts,
              (SELECT count(*) FROM pack_content.production_task_input_snapshots) AS snapshots,
              (SELECT count(*) FROM pack_content.production_artifacts) AS artifacts,
              (SELECT count(*) FROM pack_content.production_artifacts WHERE tenant_id IS NULL) AS artifacts_null,
              (SELECT count(*) FROM pack_content.production_bindings) AS bindings,
              (SELECT count(*) FROM pack_content.production_bindings WHERE tenant_id IS NULL) AS bindings_null,
              (SELECT count(*) FROM pack_content.production_film_assemblies) AS films,
              (SELECT count(*) FROM pack_content.production_film_assemblies WHERE tenant_id IS NULL) AS films_null,
              (SELECT count(*) FROM pack_content.production_film_assembly_shots) AS film_shots,
              (SELECT count(*) FROM pack_content.production_film_assembly_shots WHERE tenant_id IS NULL) AS film_shots_null
            """, cancellationToken: ct));
        return "builds=" + row.builds + " nullBuilds=" + row.null_builds + " ownedBuilds=" + row.owned_builds
            + " requests=" + row.requests + " tasks=" + row.tasks + " attempts=" + row.attempts
            + " snapshots=" + row.snapshots + " artifacts=" + row.artifacts + " artifactsNull=" + row.artifacts_null
            + " bindings=" + row.bindings + " bindingsNull=" + row.bindings_null
            + " films=" + row.films + " filmsNull=" + row.films_null
            + " filmShots=" + row.film_shots + " filmShotsNull=" + row.film_shots_null;
    }

    static byte[] RenderSource(int seconds, int hz)
    {
        var ffmpeg = ResolveTool("ffmpeg") ?? throw new InvalidOperationException("FFMPEG_MISSING");
        var work = Path.Combine(Path.GetTempPath(), "famixa-src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var output = Path.Combine(work, "source.mp4");
        try
        {
            var sec = seconds.ToString(CultureInfo.InvariantCulture);
            var args = "-y -hide_banner -loglevel error -f lavfi -i \"testsrc2=size=320x180:rate=24:duration=" + sec
                + "\" -f lavfi -i \"sine=frequency=" + hz.ToString(CultureInfo.InvariantCulture)
                + ":sample_rate=48000:duration=" + sec + "\" -shortest -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -r 24 -c:a aac -b:a 192k -ar 48000 -ac 2 -movflags +faststart \""
                + output + "\"";
            if (Run(ffmpeg, args, work) != 0 || !File.Exists(output))
                throw new InvalidOperationException("SOURCE_RENDER_FAILED");
            return File.ReadAllBytes(output);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    static string ProbeFile(string ffprobe, string path) =>
        RunText(ffprobe, "-v error -select_streams v:0 -show_entries stream=codec_name,time_base -of default=nw=1 \"" + path + "\"")
            .Replace('\n', ' ')
        + " | "
        + RunText(ffprobe, "-v error -select_streams a:0 -show_entries stream=codec_name,sample_rate,channels,time_base -of default=nw=1 \"" + path + "\"")
            .Replace('\n', ' ')
        + " | "
        + RunText(ffprobe, "-v error -show_entries format=format_name,duration -of default=nw=1 \"" + path + "\"")
            .Replace('\n', ' ');

    static int CountFiles(string root) =>
        Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Count() : 0;

    static string? ResolveTool(string name)
    {
        foreach (var candidate in new[] { name, name + ".exe" })
        {
            try
            {
                if (Run(candidate, "-version", Path.GetTempPath()) == 0) return candidate;
            }
            catch { /* next */ }
        }
        return null;
    }

    static int Run(string file, string args, string work)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            WorkingDirectory = work,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        });
        if (p is null) return -1;
        p.WaitForExit();
        return p.ExitCode;
    }

    static string RunText(string file, string args)
    {
        using var p = Process.Start(new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
        });
        if (p is null) return "";
        var text = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return text.Trim();
    }

    sealed class FixedCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class HostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tenant-media";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
