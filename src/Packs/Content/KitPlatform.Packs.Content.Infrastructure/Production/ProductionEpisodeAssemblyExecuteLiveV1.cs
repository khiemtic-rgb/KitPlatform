using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
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
/// PRODUCTION-EPISODE-ASSEMBLY-EXECUTE-01.
/// Calls official AssembleEpisodeOsAsync once for the tenant-owned EP01 build.
/// Does not change contract, tolerance, historical rows, or credentials.
/// </summary>
public static class ProductionEpisodeAssemblyExecuteLiveV1
{
    public static readonly Guid BuildId = ProductionEpisodeTenantMediaLiveV1.BuildId;
    public static readonly Guid TenantA = ProductionEpisodeTenantMediaLiveV1.TenantA;
    public const string EpisodeCode = "EP01";
    public static readonly Guid Sh01AssemblyId = Guid.Parse("c69e4bcb-c73a-47a2-8dc7-41583c2d3b5f");
    public static readonly Guid Sh02AssemblyId = Guid.Parse("4d114dda-488a-4f24-9c4d-ef86d4e13cc7");
    const string CallerKey = "caller-ignored-execute-01";

    public static async Task<string> ExecuteAsync(string connectionString, string videoRoot, CancellationToken ct = default)
    {
        if (ProductionScopeAnchorRules.IsOfficialFilm(BuildId))
            throw new InvalidOperationException("BUILD_IS_HISTORICAL_OFFICIAL_FILM");

        var report = new StringBuilder();
        var failures = new List<string>();
        var db = new NpgsqlConnectionFactory(connectionString);
        var callerA = new FixedCaller(TenantA);
        var store = new ProductionWorkRepository(db, callerA);
        var files = new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot }), new HostEnv(videoRoot)),
            callerA);
        var audit = new ProductionFfmpegMixAudit();
        var transport = new ProcessFfmpegMixTransport(audit);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(
            os, store, files, new FixtureMotionAdapter(),
            officialAssembly: new FfmpegAssemblyExecutionAdapter(transport),
            officialEpisode: new FfmpegEpisodeAssemblyExecutionAdapter(transport));

        var before = await SnapshotAsync(db, videoRoot, ct);
        report.AppendLine("BEFORE " + before.Counts);
        report.AppendLine("BEFORE_HIST " + before.Historical);
        report.AppendLine("BEFORE_ORPHAN " + before.Orphan);
        report.AppendLine("BEFORE_GRAPH " + before.Graph);
        report.AppendLine("BEFORE_FILES " + before.Files);
        var tempsBefore = TempDirs();

        FilmAssemblySliceResult? slice = null;
        try
        {
            var existing = await EpisodeTaskCountAsync(db, ct);
            if (existing != 0)
                throw new InvalidOperationException("ASSEMBLE_EPISODE_ALREADY_PRESENT " + existing);

            var sh01 = await RequireCurrentAssemblyAsync(store, files, "EP01-SC01-SH01", Sh01AssemblyId, 4m, ct);
            var sh02 = await RequireCurrentAssemblyAsync(store, files, "EP01-SC01-SH02", Sh02AssemblyId, 6m, ct);
            report.AppendLine("INPUT SH01 " + DescribeInput(sh01));
            report.AppendLine("INPUT SH02 " + DescribeInput(sh02));

            var scope = new ProductionScope(BuildId, EpisodeCode, EpisodeCode, ProductionEpisodeAssemblyContract.EpisodeShotCode);
            slice = await svc.AssembleEpisodeOsAsync(new ProductionActionApiCommand(
                scope,
                new ShotExecutionInput(
                    "EP01 tenant-owned source", "beat", "visual", "", "", "10", "", "",
                    DurationSec: 10m),
                "episode-assembly-execute",
                CallerKey,
                AssemblyShots:
                [
                    (sh01.Shot, sh01.Artifact.ArtifactId, sh01.Artifact.DurationSec),
                    (sh02.Shot, sh02.Artifact.ArtifactId, sh02.Artifact.DurationSec),
                ]), ct);
        }
        catch (Exception ex)
        {
            failures.Add(ex.GetType().Name + ": " + ex.Message);
        }

        var after = await SnapshotAsync(db, videoRoot, ct);
        report.AppendLine("AFTER " + after.Counts);
        report.AppendLine("AFTER_HIST " + after.Historical);
        report.AppendLine("AFTER_ORPHAN " + after.Orphan);
        report.AppendLine("AFTER_GRAPH " + after.Graph);
        report.AppendLine("AFTER_FILES " + after.Files);
        report.AppendLine("CONCAT_RUNS " + audit.ConcatExecutionCount);
        var leaked = TempDirs().Except(tempsBefore, StringComparer.OrdinalIgnoreCase).ToArray();
        report.AppendLine("TEMP_LEAK " + leaked.Length);

        if (slice?.Output is null)
            failures.Add("official output missing");
        else
            await ProveOutputAsync(db, files, slice, failures, report, ct);

        if (!string.Equals(before.Historical, after.Historical, StringComparison.Ordinal))
            failures.Add("historical fingerprint changed");
        if (!string.Equals(before.Orphan, after.Orphan, StringComparison.Ordinal))
            failures.Add("orphan attempt changed");
        if (!string.Equals(before.Graph, after.Graph, StringComparison.Ordinal))
            failures.Add("series_build graph changed");
        if (before.NullCounts != after.NullCounts)
            failures.Add("null-tenant counts changed " + before.NullCounts + " -> " + after.NullCounts);
        if (leaked.Length > 0)
            failures.Add("temp leftover " + string.Join(",", leaked));

        var ai = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build
              AND lower(a.provider_id) IN ('runway','fal','elevenlabs','google','veo','openai','gemini')
            """, ct);
        var creds = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build
              AND (s.credential_id IS NOT NULL OR s.credential_version_id IS NOT NULL)
            """, ct);
        report.AppendLine("AI_PROVIDER_JOBS " + ai);
        report.AppendLine("CREDENTIAL_PINS " + creds);
        if (ai != 0) failures.Add("ai provider jobs " + ai);
        if (creds != 0) failures.Add("credential pins " + creds);
        if (audit.ConcatExecutionCount != 1) failures.Add("concat runs " + audit.ConcatExecutionCount);

        report.Insert(0, (failures.Count == 0 ? "VERDICT PASS" : "VERDICT FAIL") + Environment.NewLine);
        if (failures.Count > 0)
            report.Insert(("VERDICT FAIL" + Environment.NewLine).Length, "FAILURES " + string.Join(" | ", failures) + Environment.NewLine);
        return report.ToString();
    }

    static async Task ProveOutputAsync(
        IDbConnectionFactory db,
        IProductionArtifactFileStore files,
        FilmAssemblySliceResult slice,
        List<string> failures,
        StringBuilder report,
        CancellationToken ct)
    {
        var art = slice.Output ?? throw new InvalidOperationException("OUTPUT_MISSING");
        var bytes = files.Read(art.StorageKey);
        var physical = bytes is null ? "MISSING" : Sha(bytes);
        report.AppendLine("OUTPUT id=" + art.ArtifactId
            + " role=" + art.Role
            + " status=" + art.VerificationStatus
            + " key=" + art.StorageKey
            + " sha=" + art.ByteSha256
            + " physical=" + physical
            + " duration=" + art.DurationSec.ToString(CultureInfo.InvariantCulture)
            + " task=" + slice.Task.TaskId
            + " statusTask=" + slice.Task.Status
            + " attempt=" + slice.Attempt.AttemptId
            + " provider=" + slice.Attempt.ProviderId + "/" + slice.Attempt.ModelId
            + " job=" + (slice.Attempt.ProviderJobId ?? "")
            + " idem=" + slice.Request.IdempotencyKey
            + " film=" + slice.Assembly.AssemblyId
            + " filmTitle=" + slice.Assembly.Title);

        if (art.Role != ProductionArtifactRoles.Assembly)
            failures.Add("role " + art.Role);
        if (art.VerificationStatus != ProductionArtifactStatuses.Verified)
            failures.Add("verification " + art.VerificationStatus);
        if (!ProductionTenantBoundary.OwnsStorageKey(TenantA, art.StorageKey))
            failures.Add("storage key is not tenant-prefixed");
        if (!string.Equals(art.ByteSha256, physical, StringComparison.OrdinalIgnoreCase))
            failures.Add("physical sha mismatch");
        if (slice.Request.IdempotencyKey.Contains(CallerKey, StringComparison.Ordinal))
            failures.Add("caller idempotency key was stored");
        if (!slice.Request.IdempotencyKey.StartsWith(
                ProductionActions.AssembleEpisode + ":" + ProductionEpisodeAssemblyContract.Version + ":",
                StringComparison.Ordinal))
            failures.Add("idempotency identity " + slice.Request.IdempotencyKey);
        if (!string.Equals(slice.Attempt.ProviderId, ProductionAssemblyCatalog.ServerProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(slice.Attempt.ModelId, ProductionAssemblyCatalog.FfmpegModelId, StringComparison.OrdinalIgnoreCase))
            failures.Add("provider " + slice.Attempt.ProviderId + "/" + slice.Attempt.ModelId);
        if (art.ParentArtifactIds is not { Count: 2 }
            || art.ParentArtifactIds[0] != Sh01AssemblyId
            || art.ParentArtifactIds[1] != Sh02AssemblyId)
            failures.Add("parents " + string.Join(",", art.ParentArtifactIds ?? []));
        if (slice.Assembly.Title != EpisodeCode)
            failures.Add("film title " + slice.Assembly.Title);
        if (slice.AcceptedArtifactIds.Count != 2
            || slice.AcceptedArtifactIds[0] != Sh01AssemblyId
            || slice.AcceptedArtifactIds[1] != Sh02AssemblyId)
            failures.Add("membership order");

        if (bytes is null)
        {
            failures.Add("output bytes missing");
            return;
        }
        if (!ProductionTestMp4.TryReadDuration(bytes, out var duration))
            failures.Add("TryReadDuration failed");
        else
        {
            report.AppendLine("DURATION tryRead=" + duration.ToString(CultureInfo.InvariantCulture)
                + " dec=" + ProductionMixContract.Dec(duration)
                + " decSum=" + ProductionMixContract.Dec(10m)
                + " mvhd=" + ReadMvhd(bytes));
            if (ProductionMixContract.Dec(duration) != "10" || ProductionMixContract.Dec(art.DurationSec) != "10")
                failures.Add("official duration is not 10");
            if (ReadMvhd(bytes) != "10000/1000")
                failures.Add("mvhd " + ReadMvhd(bytes));
        }

        var probe = Probe(bytes);
        report.AppendLine("PROBE " + probe.Text);
        failures.AddRange(probe.Failures);

        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var binds = (await conn.QueryAsync(new CommandDefinition("""
            SELECT shot_code AS shot, role, artifact_id::text AS artifact, (unbound_at IS NULL) AS active, tenant_id::text AS tenant
            FROM pack_content.production_bindings
            WHERE artifact_id = @Id
            ORDER BY role
            """, new { Id = art.ArtifactId }, cancellationToken: ct))).ToList();
        foreach (var row in binds)
            report.AppendLine("BIND shot=" + row.shot + " role=" + row.role + " artifact=" + row.artifact
                + " active=" + row.active + " tenant=" + row.tenant);
        var current = binds.Count(b => (bool)b.active && (string)b.role == ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly) && (string)b.shot == "EP01");
        var visible = binds.Count(b => (bool)b.active && (string)b.role == ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly) && (string)b.shot == "EP01");
        if (current != 1) failures.Add("CURRENT:ASSEMBLY active " + current);
        if (visible != 1) failures.Add("VISIBLE:ASSEMBLY active " + visible);

        var snap = await conn.QuerySingleOrDefaultAsync(new CommandDefinition("""
            SELECT s.credential_id::text AS cred, s.duration_sec AS dur, s.provider_id AS provider, s.model_id AS model,
                   s.shot_contract AS contract
            FROM pack_content.production_task_input_snapshots s
            WHERE s.task_id = @Task
            """, new { Task = slice.Task.TaskId }, cancellationToken: ct));
        if (snap is null)
            failures.Add("snapshot missing");
        else
        {
            report.AppendLine("SNAPSHOT provider=" + snap.provider + "/" + snap.model
                + " dur=" + ((decimal)snap.dur).ToString(CultureInfo.InvariantCulture)
                + " cred=" + (snap.cred ?? "null"));
            if (snap.cred is not null) failures.Add("snapshot credential");
            if (ProductionMixContract.Dec((decimal)snap.dur) != "10") failures.Add("snapshot duration");
            if (!ProductionEpisodeAssemblyContract.TryParse((string)snap.contract, out var frozen)
                || ProductionMixContract.Dec(frozen.OutputDurationSec) != "10"
                || frozen.DurationToleranceSec != 0
                || frozen.CompositionMode != ProductionEpisodeAssemblyContract.ConcatCopy)
                failures.Add("frozen contract");
            else
                report.AppendLine("CONTRACT hash=" + frozen.ContractHash + " mode=" + frozen.CompositionMode
                    + " tolerance=" + frozen.DurationToleranceSec.ToString(CultureInfo.InvariantCulture));
        }
    }

    static async Task<ResolvedInput> RequireCurrentAssemblyAsync(
        IProductionWorkStore store,
        IProductionArtifactFileStore files,
        string shot,
        Guid expectedId,
        decimal expectedDuration,
        CancellationToken ct)
    {
        var current = await store.GetActiveBindingAsync(
            BuildId, shot, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct)
            ?? throw new InvalidOperationException("CURRENT_ASSEMBLY_MISSING " + shot);
        if (current.ArtifactId != expectedId)
            throw new InvalidOperationException("CURRENT_ASSEMBLY_UNEXPECTED " + shot + " " + current.ArtifactId);
        var visible = await store.GetActiveBindingAsync(
            BuildId, shot, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly), ct)
            ?? throw new InvalidOperationException("VISIBLE_ASSEMBLY_MISSING " + shot);
        if (visible.ArtifactId != current.ArtifactId)
            throw new InvalidOperationException("VISIBLE_DRIFT " + shot);
        var art = await store.GetArtifactAsync(current.ArtifactId, ct)
            ?? throw new InvalidOperationException("ARTIFACT_MISSING " + shot);
        if (art.Role != ProductionArtifactRoles.Assembly
            || art.VerificationStatus != ProductionArtifactStatuses.Verified)
            throw new InvalidOperationException("ARTIFACT_NOT_VERIFIED_ASSEMBLY " + shot);
        if (ProductionMixContract.Dec(art.DurationSec) != ProductionMixContract.Dec(expectedDuration))
            throw new InvalidOperationException("DURATION_UNEXPECTED " + shot);
        if (!ProductionTenantBoundary.OwnsStorageKey(TenantA, art.StorageKey))
            throw new InvalidOperationException("STORAGE_NOT_TENANT " + shot);
        if (art.StorageKey.Contains("://", StringComparison.Ordinal))
            throw new InvalidOperationException("URL_INPUT " + shot);
        var bytes = files.Read(art.StorageKey) ?? throw new InvalidOperationException("BYTES_MISSING " + shot);
        var sha = Sha(bytes);
        if (!string.Equals(sha, art.ByteSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("PHYSICAL_SHA_MISMATCH " + shot);
        var owner = await store.GetTaskAsync(art.TaskId, ct)
            ?? throw new InvalidOperationException("TASK_MISSING " + shot);
        if (owner.Scope.BuildId != BuildId || owner.Scope.ShotCode != shot)
            throw new InvalidOperationException("SHOT_OWNER_MISMATCH " + shot);
        return new ResolvedInput(shot, art, sha, bytes.Length);
    }

    static string DescribeInput(ResolvedInput row) =>
        row.Shot + " id=" + row.Artifact.ArtifactId + " sha=" + row.Sha + " key=" + row.Artifact.StorageKey
        + " duration=" + row.Artifact.DurationSec.ToString(CultureInfo.InvariantCulture) + " bytes=" + row.Bytes;

    static async Task<int> EpisodeTaskCountAsync(IDbConnectionFactory db, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.production_tasks
            WHERE action = 'ASSEMBLE_EPISODE' AND build_id = @Build
            """, new { Build = BuildId }, cancellationToken: ct));
    }

    static async Task<int> ScalarAsync(IDbConnectionFactory db, string sql, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Build = BuildId }, cancellationToken: ct));
    }

    static async Task<LiveSnapshot> SnapshotAsync(IDbConnectionFactory db, string videoRoot, CancellationToken ct)
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
              (SELECT count(*) FROM pack_content.production_bindings WHERE unbound_at IS NULL) AS bindings_active,
              (SELECT count(*) FROM pack_content.production_film_assemblies) AS films,
              (SELECT count(*) FROM pack_content.production_film_assemblies WHERE tenant_id IS NULL) AS films_null,
              (SELECT count(*) FROM pack_content.production_film_assembly_shots) AS film_shots,
              (SELECT count(*) FROM pack_content.production_film_assembly_shots WHERE tenant_id IS NULL) AS film_shots_null,
              (SELECT count(*) FROM pack_content.production_task_events) AS events,
              (SELECT count(*) FROM pack_content.production_execution_permit) AS permits
            """, cancellationToken: ct));
        var counts = "builds=" + row.builds + " nullBuilds=" + row.null_builds + " ownedBuilds=" + row.owned_builds
            + " requests=" + row.requests + " tasks=" + row.tasks + " attempts=" + row.attempts
            + " snapshots=" + row.snapshots + " artifacts=" + row.artifacts + " artifactsNull=" + row.artifacts_null
            + " bindings=" + row.bindings + " bindingsNull=" + row.bindings_null + " bindingsActive=" + row.bindings_active
            + " films=" + row.films + " filmsNull=" + row.films_null
            + " filmShots=" + row.film_shots + " filmShotsNull=" + row.film_shots_null
            + " events=" + row.events + " permits=" + row.permits;
        var nullCounts = row.null_builds + "|" + row.artifacts_null + "|" + row.bindings_null + "|" + row.films_null + "|" + row.film_shots_null;
        var historical = await conn.ExecuteScalarAsync<string>(new CommandDefinition("""
            SELECT md5(concat_ws('|',
              (SELECT md5(coalesce(string_agg(id::text || status || episode_code || coalesce(graph_json::text,''), ',' ORDER BY id::text), '')) FROM pack_content.series_build WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(artifact_id::text || byte_sha256 || storage_key || duration_sec::text || verification_status, ',' ORDER BY artifact_id::text), '')) FROM pack_content.production_artifacts WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(binding_id::text || role || artifact_id::text || coalesce(unbound_at::text,'null'), ',' ORDER BY binding_id::text), '')) FROM pack_content.production_bindings WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(assembly_id::text || title, ',' ORDER BY assembly_id::text), '')) FROM pack_content.production_film_assemblies WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(assembly_shot_id::text || assembly_id::text || shot_code || artifact_id::text, ',' ORDER BY assembly_shot_id::text), '')) FROM pack_content.production_film_assembly_shots WHERE tenant_id IS NULL)
            ))
            """, cancellationToken: ct)) ?? "";
        var orphan = await conn.ExecuteScalarAsync<string>(new CommandDefinition("""
            SELECT coalesce(string_agg(
              t.task_id::text || ':' || t.status || ':' || coalesce(t.failure_code,'') || ':' ||
              a.status || ':' || a.provider_id || ':' || coalesce(a.provider_job_id,'') || ':' || coalesce(a.error_code,'') || ':' ||
              (SELECT count(*) FROM pack_content.production_artifacts x WHERE x.task_id = t.task_id)::text || ':' ||
              (SELECT count(*) FROM pack_content.production_bindings b WHERE b.bound_by_task_id = t.task_id)::text,
              ',' ORDER BY t.task_id::text), '')
            FROM pack_content.production_tasks t
            JOIN pack_content.production_attempts a ON a.task_id = t.task_id
            WHERE t.build_id = @Build AND t.action = 'CREATE_VIDEO' AND t.shot_code = 'EP01-SC01-SH01'
            """, new { Build = BuildId }, cancellationToken: ct)) ?? "";
        var graph = await conn.ExecuteScalarAsync<string>(new CommandDefinition("""
            SELECT md5(coalesce(string_agg(id::text || coalesce(tenant_id::text,'null') || status || coalesce(graph_json::text,''), ',' ORDER BY id::text), ''))
            FROM pack_content.series_build
            """, cancellationToken: ct)) ?? "";
        var files = FileManifest(videoRoot);
        return new LiveSnapshot(counts, nullCounts.ToString(), historical, orphan, graph, files);
    }

    static string FileManifest(string root)
    {
        if (!Directory.Exists(root)) return "missing";
        var prefix = ProductionTenantBoundary.Prefix(TenantA) + "/";
        var outside = new List<string>();
        var inside = 0;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) inside++;
            else outside.Add(rel + ":" + new FileInfo(path).Length);
        }
        outside.Sort(StringComparer.Ordinal);
        return "tenantFiles=" + inside + " otherFiles=" + outside.Count + " otherFp=" + Sha(Encoding.UTF8.GetBytes(string.Join("\n", outside)));
    }

    static (IReadOnlyList<string> Failures, string Text) Probe(byte[] bytes)
    {
        var failures = new List<string>();
        var work = Path.Combine(Path.GetTempPath(), "famixa-ep-exec-proof-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var path = Path.Combine(work, "out.mp4");
        try
        {
            File.WriteAllBytes(path, bytes);
            var ffprobe = ResolveTool("ffprobe");
            if (ffprobe is null)
            {
                failures.Add("ffprobe missing");
                return (failures, "probe-missing");
            }
            var format = Run(ffprobe, "-v error -show_entries format=duration -of csv=p=0 \"" + path + "\"");
            var video = Run(ffprobe, "-v error -select_streams v:0 -show_entries stream=codec_name,time_base,r_frame_rate,start_pts,duration,duration_ts,nb_frames -of csv=p=0 \"" + path + "\"");
            var audio = Run(ffprobe, "-v error -select_streams a:0 -show_entries stream=codec_name,profile,sample_rate,channels,time_base,start_time,duration,duration_ts,nb_frames -of csv=p=0 \"" + path + "\"");
            var packets = Run(ffprobe, "-v error -select_streams a:0 -show_packets -show_entries packet=pts,duration -of csv=p=0 \"" + path + "\"");
            var text = "format=" + format.Trim() + " video=" + OneLine(video) + " audio=" + OneLine(audio);
            if (format.Trim() != "10.000000") failures.Add("ffprobe " + format.Trim());
            if (!video.Contains("1/12288", StringComparison.Ordinal) || !video.Contains("24/1", StringComparison.Ordinal)
                || !video.StartsWith("h264,", StringComparison.Ordinal) || !video.Contains(",0,", StringComparison.Ordinal)
                || !video.Contains("10.000000", StringComparison.Ordinal) || !video.Contains("122880", StringComparison.Ordinal)
                || !video.TrimEnd().EndsWith("240", StringComparison.Ordinal))
                failures.Add("video clock " + OneLine(video));
            if (!audio.Contains("aac", StringComparison.OrdinalIgnoreCase) || !audio.Contains("LC", StringComparison.Ordinal)
                || !audio.Contains("48000", StringComparison.Ordinal) || !audio.Contains(",2,", StringComparison.Ordinal)
                || !audio.Contains("480000", StringComparison.Ordinal))
                failures.Add("audio stream " + OneLine(audio));
            var editText = ReadEditLists(bytes);
            text += " edit=" + editText;
            if (editText.Split("1024/10000", StringSplitOptions.None).Length - 1 < 2)
                failures.Add("edit list " + editText);
            var parsed = new List<(long Pts, long Dur)>();
            foreach (var line in packets.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split(',');
                if (parts.Length >= 2 && long.TryParse(parts[0], out var pts) && long.TryParse(parts[1], out var dur))
                    parsed.Add((pts, dur));
            }
            var negative = parsed.Count(p => p.Pts < 0);
            var ones = parsed.Count(p => p.Dur == 1);
            var junction = parsed.FirstOrDefault(p => p.Pts <= 192000 && p.Pts + p.Dur > 192000);
            text += " audioPackets=" + parsed.Count + " negativePts=" + negative + " dur1=" + ones
                + " junction=" + junction.Pts + ":" + junction.Dur;
            if (negative != 1 || ones != 0 || junction.Dur != 1024)
                failures.Add("priming junction");
            return (failures, text);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { /* temp */ }
        }
    }

    static string ReadEditLists(byte[] bytes)
    {
        var found = new List<string>();
        for (var i = 0; i <= bytes.Length - 24; i++)
        {
            if (bytes[i] != (byte)'e' || bytes[i + 1] != (byte)'l' || bytes[i + 2] != (byte)'s' || bytes[i + 3] != (byte)'t')
                continue;
            if (bytes[i + 4] != 0) continue;
            var count = ReadU32(bytes, i + 8);
            var cursor = i + 12;
            for (var n = 0; n < count && cursor + 8 <= bytes.Length; n++)
            {
                found.Add(ReadU32(bytes, cursor + 4).ToString(CultureInfo.InvariantCulture) + "/" + ReadU32(bytes, cursor).ToString(CultureInfo.InvariantCulture));
                cursor += 12;
            }
        }
        return string.Join(",", found);
    }

    static string ReadMvhd(byte[] bytes)
    {
        for (var i = 0; i <= bytes.Length - 28; i++)
        {
            if (bytes[i] == (byte)'m' && bytes[i + 1] == (byte)'v' && bytes[i + 2] == (byte)'h' && bytes[i + 3] == (byte)'d' && bytes[i + 4] == 0)
                return ReadU32(bytes, i + 20).ToString(CultureInfo.InvariantCulture) + "/" + ReadU32(bytes, i + 16).ToString(CultureInfo.InvariantCulture);
        }
        return "none";
    }

    static uint ReadU32(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    static string[] TempDirs() =>
        Directory.Exists(Path.GetTempPath())
            ? Directory.GetDirectories(Path.GetTempPath(), "famixa-episode-concat-*")
            : [];

    static string? ResolveTool(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            var candidate = Path.Combine(dir, name + ".exe");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        var winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(winget))
            return Directory.EnumerateFiles(winget, name + ".exe", SearchOption.AllDirectories).FirstOrDefault();
        return null;
    }

    static string Run(string file, string args)
    {
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = file,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        p.Start();
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }

    public static Task<string> ReplayAsync(string connectionString, string videoRoot, CancellationToken ct = default) =>
        RunFollowAsync(connectionString, videoRoot, "replay", ct);

    public static Task<string> ConcurrencyAsync(string connectionString, string videoRoot, CancellationToken ct = default) =>
        RunFollowAsync(connectionString, videoRoot, "concurrency", ct);

    public static Task<string> IsolationAsync(string connectionString, string videoRoot, CancellationToken ct = default) =>
        RunFollowAsync(connectionString, videoRoot, "isolation", ct);

    public static async Task<string> HistoricalAsync(string connectionString, string videoRoot, CancellationToken ct = default)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var snap = await SnapshotAsync(db, videoRoot, ct);
        var failures = new List<string>();
        if (snap.Historical != "2ddc3ed8071c652a8cbc63ff3ca0d05a")
            failures.Add("historical fingerprint " + snap.Historical);
        if (snap.Graph != "b6556f1e9fa683f6f6ff88722ff0c624")
            failures.Add("graph fingerprint " + snap.Graph);
        if (snap.NullCounts != "4|36|113|5|5")
            failures.Add("null counts " + snap.NullCounts);
        if (!snap.Files.Contains("otherFiles=56", StringComparison.Ordinal)
            || !snap.Files.Contains("otherFp=5738b96c2d59d5e609cc9babb6a3b856387764fcf482407422ac5b8c2f536a8e", StringComparison.Ordinal))
            failures.Add("historical file manifest " + snap.Files);
        if (snap.Orphan != "bb3f233a-e077-49f1-905c-bc70b586f7f0:RUNNING::STARTED:server:::0:0,e3be9e14-09f7-4d2f-85a5-0cdeaae64725:SUCCEEDED::PROVIDER_SUCCEEDED:server:ffmpeg-source-82d45bd70f3f::1:1")
            failures.Add("orphan changed");
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var histBuild = await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            SELECT count(*) FROM pack_content.series_build
            WHERE id = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f' AND tenant_id IS NULL
            """, cancellationToken: ct));
        if (histBuild != 1) failures.Add("historical EP01 build " + histBuild);
        var report = new StringBuilder();
        report.AppendLine(failures.Count == 0 ? "VERDICT PASS" : "VERDICT FAIL");
        if (failures.Count > 0) report.AppendLine("FAILURES " + string.Join(" | ", failures));
        report.AppendLine("STATE " + snap.Counts);
        report.AppendLine("HIST " + snap.Historical);
        report.AppendLine("GRAPH " + snap.Graph);
        report.AppendLine("FILES " + snap.Files);
        report.AppendLine("NULL " + snap.NullCounts);
        report.AppendLine("ORPHAN " + snap.Orphan);
        report.AppendLine("HIST_BUILD_NULL " + histBuild);
        return report.ToString();
    }

    public static async Task<string> RegressionAsync()
    {
        var lines = new List<string>();
        var blocking = new List<string>();
        void Take(string name, IReadOnlyList<string> fail)
        {
            lines.Add(name + " failures=" + fail.Count);
            foreach (var item in fail)
            {
                if (Preexisting(item))
                    lines.Add("PREEXISTING " + name + " :: " + item);
                else
                {
                    lines.Add("NEW " + name + " :: " + item);
                    blocking.Add(name + " :: " + item);
                }
            }
        }

        Take("EpisodeAssembly", ProductionEpisodeAssemblyV1Regression.Run());
        lines.Add("CK23B_INNER " + string.Join(" | ", ProductionEp01Sh01RealFilmRunV1LipsyncRegression.Run()));
        var hybrid = await ProductionEpisodeConcatHybridV1Regression.RunAsync();
        Take("HybridConcat", hybrid.Failures);
        Take("WorkExecution", ProductionWorkExecutionV1Regression.Run());
        Take("RetryIdempotency", ProductionExecutionRetryIdempotencyV1Regression.Run());
        Take("OfficialStateBinding", ProductionOfficialStateBindingV1Regression.Run());
        Take("TenantBoundary", ProductionTenantBoundaryV1Regression.Run());
        Take("TenantScope", ProductionEpisodeTenantScopeV1Regression.Run());
        Take("ArtifactImmutability", ProductionArtifactImmutabilityV1Regression.Run());
        Take("CredentialFreeze", ProductionCredentialFreezeV1Regression.Run());
        Take("CredentialRotation", ProductionCredentialRotationV1Regression.Run());
        Take("CredentialPathClosure", ProductionCredentialOfficialPathClosureV1Regression.Run());

        var report = new StringBuilder();
        report.AppendLine(blocking.Count == 0 ? "VERDICT PASS" : "VERDICT FAIL");
        if (blocking.Count > 0) report.AppendLine("FAILURES " + string.Join(" | ", blocking));
        foreach (var line in lines) report.AppendLine(line);
        return report.ToString();
    }

    static bool Preexisting(string failure) =>
        failure.Contains("MISSING_PRODUCTION_AI_SELECTION", StringComparison.Ordinal)
        || failure.Contains("MISSING_KEYFRAME", StringComparison.Ordinal)
        || failure.Contains("PRODUCTION_CREDENTIAL_UNRESOLVED", StringComparison.Ordinal)
        || failure.Contains("CROSS_SCOPE_FORBIDDEN", StringComparison.Ordinal)
        || failure.Contains("CK23B PASS", StringComparison.Ordinal);

    static async Task<string> RunFollowAsync(string connectionString, string videoRoot, string mode, CancellationToken ct)
    {
        var report = new StringBuilder();
        var failures = new List<string>();
        var db = new NpgsqlConnectionFactory(connectionString);
        var before = await SnapshotAsync(db, videoRoot, ct);
        report.AppendLine("BEFORE " + before.Counts);
        var officialId = await ScalarGuidAsync(db, """
            SELECT b.artifact_id FROM pack_content.production_bindings b
            WHERE b.build_id = @Build AND b.shot_code = 'EP01' AND b.role = 'CURRENT:ASSEMBLY' AND b.unbound_at IS NULL
            """, ct);

        if (mode == "isolation")
            await IsolateAsync(connectionString, videoRoot, failures, report, ct);
        else if (mode == "concurrency")
            await RaceAsync(connectionString, videoRoot, officialId, failures, report, ct);
        else
            await OnceAsync(connectionString, videoRoot, officialId, failures, report, ct);

        var after = await SnapshotAsync(db, videoRoot, ct);
        report.AppendLine("AFTER " + after.Counts);
        if (before.Counts != after.Counts) failures.Add("counts changed");
        if (before.Historical != after.Historical) failures.Add("historical fingerprint changed");
        if (before.Graph != after.Graph) failures.Add("graph changed");
        if (before.Files != after.Files) failures.Add("files changed");
        if (before.Orphan != after.Orphan) failures.Add("orphan changed");
        var current = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'CURRENT:ASSEMBLY' AND unbound_at IS NULL
            """, ct);
        var visible = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'VISIBLE:ASSEMBLY' AND unbound_at IS NULL
            """, ct);
        report.AppendLine("ACTIVE_CURRENT " + current);
        report.AppendLine("ACTIVE_VISIBLE_ASSEMBLY " + visible);
        if (current != 1 || visible != 1) failures.Add("active binding current=" + current + " visible=" + visible);
        var ai = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build
              AND lower(a.provider_id) IN ('runway','fal','elevenlabs','google','veo','openai','gemini')
            """, ct);
        var creds = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND (s.credential_id IS NOT NULL OR s.credential_version_id IS NOT NULL)
            """, ct);
        report.AppendLine("AI_PROVIDER_JOBS " + ai);
        report.AppendLine("CREDENTIAL_PINS " + creds);
        if (ai != 0) failures.Add("ai jobs");
        if (creds != 0) failures.Add("credential pins");
        report.Insert(0, (failures.Count == 0 ? "VERDICT PASS" : "VERDICT FAIL") + Environment.NewLine);
        if (failures.Count > 0)
            report.Insert(("VERDICT FAIL" + Environment.NewLine).Length, "FAILURES " + string.Join(" | ", failures) + Environment.NewLine);
        return report.ToString();
    }

    static async Task OnceAsync(
        string connectionString, string videoRoot, Guid officialId, List<string> failures, StringBuilder report, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, TenantA);
        var sh01 = await RequireCurrentAssemblyAsync(opened.Store, opened.Files, "EP01-SC01-SH01", Sh01AssemblyId, 4m, ct);
        var sh02 = await RequireCurrentAssemblyAsync(opened.Store, opened.Files, "EP01-SC01-SH02", Sh02AssemblyId, 6m, ct);
        FilmAssemblySliceResult slice;
        try
        {
            slice = await opened.Svc.AssembleEpisodeOsAsync(Command(sh01, sh02, "caller-ignored-replay-01"), ct);
        }
        catch (Exception ex)
        {
            failures.Add(ex.GetType().Name + ": " + ex.Message);
            return;
        }
        report.AppendLine("REPLAY artifact=" + (slice.Output?.ArtifactId.ToString() ?? "null")
            + " idem=" + slice.Request.IdempotencyKey
            + " concat=" + opened.Audit.ConcatExecutionCount
            + " film=" + slice.Assembly.AssemblyId);
        if (slice.Output?.ArtifactId != officialId) failures.Add("replay artifact differs");
        if (opened.Audit.ConcatExecutionCount != 0) failures.Add("replay ran ffmpeg");
        if (slice.Request.IdempotencyKey.Contains("caller-ignored", StringComparison.Ordinal))
            failures.Add("caller key stored");
        if (!slice.Request.IdempotencyKey.Contains("b0381cd2b9f92484ce606072fedfeea43c3df689151971e3f2d8e1af431877ae", StringComparison.Ordinal))
            failures.Add("contract hash identity changed");
    }

    static async Task RaceAsync(
        string connectionString, string videoRoot, Guid officialId, List<string> failures, StringBuilder report, CancellationToken ct)
    {
        async Task<(string Label, Guid? Artifact, int Concat, string? Error)> Call(string label)
        {
            try
            {
                var opened = Open(connectionString, videoRoot, TenantA);
                var sh01 = await RequireCurrentAssemblyAsync(opened.Store, opened.Files, "EP01-SC01-SH01", Sh01AssemblyId, 4m, ct);
                var sh02 = await RequireCurrentAssemblyAsync(opened.Store, opened.Files, "EP01-SC01-SH02", Sh02AssemblyId, 6m, ct);
                var slice = await opened.Svc.AssembleEpisodeOsAsync(Command(sh01, sh02, "caller-ignored-race-01"), ct);
                return (label, slice.Output?.ArtifactId, opened.Audit.ConcatExecutionCount, null);
            }
            catch (Exception ex)
            {
                return (label, null, -1, ex.GetType().Name + ": " + ex.Message);
            }
        }

        var pair = await Task.WhenAll(Call("A"), Call("B"));
        foreach (var row in pair)
            report.AppendLine("RACE " + row.Label + " artifact=" + (row.Artifact?.ToString() ?? "null")
                + " concat=" + row.Concat + " error=" + (row.Error ?? ""));
        if (pair.Any(r => r.Error is not null))
            failures.Add("concurrent call failed");
        if (pair.Any(r => r.Artifact != officialId))
            failures.Add("concurrent artifact is not the official output");
        if (pair.Any(r => r.Concat != 0))
            failures.Add("concurrent call ran ffmpeg");
    }

    static async Task IsolateAsync(string connectionString, string videoRoot, List<string> failures, StringBuilder report, CancellationToken ct)
    {
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        var openedA = Open(connectionString, videoRoot, TenantA);
        var openedB = Open(connectionString, videoRoot, tenantB);
        var own = await openedA.Content.GetSeriesBuildAsync(BuildId, ct);
        var other = await openedB.Content.GetSeriesBuildAsync(BuildId, ct);
        var art = await openedB.Store.GetArtifactAsync(Guid.Parse("660b6005-ad91-4191-916f-273d61cf4012"), ct);
        var binding = await openedB.Store.GetActiveBindingAsync(
            BuildId, "EP01", ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
        string assemble = "OPEN";
        try
        {
            await openedB.Svc.AssembleEpisodeOsAsync(new ProductionActionApiCommand(
                new ProductionScope(BuildId, EpisodeCode, EpisodeCode, ProductionEpisodeAssemblyContract.EpisodeShotCode),
                new ShotExecutionInput("EP01 tenant-owned source", "beat", "visual", "", "", "10", "", "", DurationSec: 10m),
                "tenant-b",
                "caller-ignored-tenant-b",
                AssemblyShots:
                [
                    ("EP01-SC01-SH01", Sh01AssemblyId, 4m),
                    ("EP01-SC01-SH02", Sh02AssemblyId, 6m),
                ]), ct);
        }
        catch (InvalidOperationException ex)
        {
            assemble = ex.Message;
        }
        string bind = "OPEN";
        try
        {
            await openedB.Os.BindNamedAsync(Guid.Parse("660b6005-ad91-4191-916f-273d61cf4012"), ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly), ct);
        }
        catch (InvalidOperationException ex)
        {
            bind = ex.Message;
        }
        report.AppendLine("ISOLATION ownBuild=" + (own is null ? "null" : "present")
            + " tenantBBuild=" + (other is null ? "null" : "LEAK")
            + " tenantBArtifact=" + (art is null ? "null" : "LEAK")
            + " tenantBBinding=" + (binding is null ? "null" : "LEAK")
            + " tenantBAssemble=" + assemble
            + " tenantBBind=" + bind);
        if (own is null) failures.Add("tenant A cannot read own build");
        if (other is not null) failures.Add("tenant B read build");
        if (art is not null) failures.Add("tenant B read artifact");
        if (binding is not null) failures.Add("tenant B read binding");
        if (assemble is not (ProductionFailureReasons.MissingDependency or ProductionTenantBoundary.Forbidden))
            failures.Add("tenant B assemble " + assemble);
        if (bind is not ("ARTIFACT_MISSING" or ProductionTenantBoundary.Forbidden))
            failures.Add("tenant B bind " + bind);
        if (assemble.Contains("aaaaaaaa", StringComparison.OrdinalIgnoreCase)
            || assemble.Contains("046d15f3", StringComparison.OrdinalIgnoreCase)
            || bind.Contains("aaaaaaaa", StringComparison.OrdinalIgnoreCase))
            failures.Add("isolation response leaked tenant storage identity");
    }

    static ProductionActionApiCommand Command(ResolvedInput sh01, ResolvedInput sh02, string callerKey) =>
        new(
            new ProductionScope(BuildId, EpisodeCode, EpisodeCode, ProductionEpisodeAssemblyContract.EpisodeShotCode),
            new ShotExecutionInput("EP01 tenant-owned source", "beat", "visual", "", "", "10", "", "", DurationSec: 10m),
            "episode-assembly-follow",
            callerKey,
            AssemblyShots:
            [
                (sh01.Shot, sh01.Artifact.ArtifactId, sh01.Artifact.DurationSec),
                (sh02.Shot, sh02.Artifact.ArtifactId, sh02.Artifact.DurationSec),
            ]);

    static Opened Open(string connectionString, string videoRoot, Guid tenant)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var caller = new FixedCaller(tenant);
        var store = new ProductionWorkRepository(db, caller);
        var files = new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot }), new HostEnv(videoRoot)),
            caller);
        var audit = new ProductionFfmpegMixAudit();
        var transport = new ProcessFfmpegMixTransport(audit);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(
            os, store, files, new FixtureMotionAdapter(),
            officialAssembly: new FfmpegAssemblyExecutionAdapter(transport),
            officialEpisode: new FfmpegEpisodeAssemblyExecutionAdapter(transport));
        return new Opened(svc, store, files, audit, new ContentRepository(db, caller), os);
    }

    static async Task<Guid> ScalarGuidAsync(IDbConnectionFactory db, string sql, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { Build = BuildId }, cancellationToken: ct));
    }

    static string OneLine(string raw) => raw.Replace('\r', ' ').Replace('\n', ' ').Trim();

    sealed record Opened(
        ProductionCreateVideoService Svc,
        ProductionWorkRepository Store,
        IProductionArtifactFileStore Files,
        ProductionFfmpegMixAudit Audit,
        ContentRepository Content,
        ProductionExecutionService Os);

    sealed record ResolvedInput(string Shot, ProductionArtifact Artifact, string Sha, int Bytes);

    sealed record LiveSnapshot(string Counts, string NullCounts, string Historical, string Orphan, string Graph, string Files);

    sealed class FixedCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class HostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "episode-assembly-execute";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
