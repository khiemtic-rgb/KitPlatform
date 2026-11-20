using System.Diagnostics;
using System.Reflection;
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
/// Live proof for EXECUTION_PROCESS_FAILED: one failing FFmpeg entry, loser convergence, one successful retry.
/// </summary>
public static class ProductionExecutionProcessFailureContractV1
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    public static readonly Guid BuildId = Guid.Parse("f8f8f8f8-f8f8-4f8f-8f8f-f8f8f8f8f801");
    public static readonly Guid Sh01ArtifactId = Guid.Parse("f8f80001-f8f8-4f8f-8f8f-f8f800000001");
    public static readonly Guid Sh02ArtifactId = Guid.Parse("f8f80002-f8f8-4f8f-8f8f-f8f800000002");
    const string Sh01 = "EP01-SC01-SH01";
    const string Sh02 = "EP01-SC01-SH02";
    const string Sh01SourceKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1/os/6d4058f21f324ff7bf4aaa4cf91407cd";
    const string Sh02SourceKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1/os/309602be95d04c7ca46b752402b28da7";
    const string ExpectedHistorical = "2ddc3ed8071c652a8cbc63ff3ca0d05a";

    public static async Task<string> RunAsync(string connectionString, string videoRoot, CancellationToken ct = default)
    {
        var report = new StringBuilder();
        var failures = new List<string>();
        var db = new NpgsqlConnectionFactory(connectionString);
        var before = await SnapshotAsync(db, ct);
        var filesBefore = FileIndex(videoRoot);
        report.AppendLine("BEFORE " + before.Counts);
        report.AppendLine("HIST_BEFORE " + before.Historical);
        report.AppendLine("NULL_BEFORE " + before.NullCounts);
        if (await CountAsync(db, "SELECT count(*) FROM pack_content.series_build WHERE id = @Build", ct) != 0)
        {
            report.AppendLine("VERDICT FAIL");
            report.AppendLine("FAILURES build already existed");
            return report.ToString();
        }

        await ExecAsync(db, """
            INSERT INTO pack_content.series_build (
                id, series_code, episode_code, title, status, shot_count, voice_lines, kf_count, video_count,
                graph_json, created_at, updated_at, tenant_id)
            VALUES (
                @Build, 'RACE_EP01_PROCESS_FAIL', 'EP01', 'process failure contract f8', 'draft', 2, 0, 0, 0,
                '{}'::jsonb, now(), now(), @Tenant)
            """, ct);

        var seedOpen = Open(connectionString, videoRoot, new FailOnceConcatTransport());
        var sh01Bytes = seedOpen.Files.Read(Sh01SourceKey) ?? throw new InvalidOperationException("SH01 source missing");
        var sh02Bytes = seedOpen.Files.Read(Sh02SourceKey) ?? throw new InvalidOperationException("SH02 source missing");
        var sh01Dur = ProductionTestMp4.TryReadDuration(sh01Bytes, out var d1) ? d1 : 0m;
        var sh02Dur = ProductionTestMp4.TryReadDuration(sh02Bytes, out var d2) ? d2 : 0m;
        await SeedAsync(seedOpen, Sh01, Sh01ArtifactId, "f8f8sh01in", sh01Bytes, sh01Dur, ct);
        await SeedAsync(seedOpen, Sh02, Sh02ArtifactId, "f8f8sh02in", sh02Bytes, sh02Dur, ct);
        var contract = ProductionEpisodeAssemblyContract.Build(BuildId, "EP01",
        [
            Input(0, Sh01, Sh01ArtifactId, sh01Bytes, sh01Dur, TenantA.ToString("N") + "/os/f8f8sh01in"),
            Input(1, Sh02, Sh02ArtifactId, sh02Bytes, sh02Dur, TenantA.ToString("N") + "/os/f8f8sh02in"),
        ]);
        var idem = string.Join(':',
            ProductionActions.AssembleEpisode, contract.ContractVersion, BuildId.ToString("N"), "EP01", contract.ContractHash);
        report.AppendLine("IDENTITY build=" + BuildId + " hash=" + contract.ContractHash + " dur=" + sh01Dur + "+" + sh02Dur);

        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var winnerTransport = new FailOnceConcatTransport();
        var loserTransport = new FailOnceConcatTransport();
        var winnerStore = Gate(new ProductionWorkRepository(db, new FixedCaller(TenantA)),
            async innerCt => await bEntered.Task.WaitAsync(innerCt));
        var loserStore = Gate(new ProductionWorkRepository(db, new FixedCaller(TenantA)),
            async innerCt =>
            {
                bEntered.TrySetResult();
                await releaseB.Task.WaitAsync(innerCt);
            });
        var winner = CallAsync("A", connectionString, videoRoot, winnerStore, winnerTransport, sh01Dur, sh02Dur,
            () => Interlocked.Increment(ref arrived), () => Volatile.Read(ref arrived), ct);
        var loser = CallAsync("B", connectionString, videoRoot, loserStore, loserTransport, sh01Dur, sh02Dur,
            () => Interlocked.Increment(ref arrived), () => Volatile.Read(ref arrived), ct);
        CallerResult? winnerResult = null;
        CallerResult? loserResult = null;
        try
        {
            await bEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            winnerResult = await winner.WaitAsync(TimeSpan.FromSeconds(60), ct);
            releaseB.TrySetResult();
            var watch = Stopwatch.StartNew();
            loserResult = await loser.WaitAsync(TimeSpan.FromSeconds(30), ct);
            watch.Stop();
            report.AppendLine("WINNER " + Format(winnerResult));
            report.AppendLine("LOSER " + Format(loserResult) + " elapsedMs=" + watch.ElapsedMilliseconds);
            report.AppendLine("FFMPEG_RACE A=" + winnerTransport.ConcatExecutionCount + " B=" + loserTransport.ConcatExecutionCount
                + " physicalFail=" + winnerTransport.FailStarts);
            if (watch.ElapsedMilliseconds > 20_000)
                failures.Add("loser waited instead of observing the terminal code");
        }
        catch (Exception ex)
        {
            failures.Add("race " + ex.GetType().Name + ":" + One(ex.Message));
        }
        finally
        {
            releaseB.TrySetResult();
        }

        var failed = await DetailAsync(db, idem, ct);
        report.AppendLine("AFTER_FAIL " + failed.Text);
        if (winnerResult?.Error is null || !winnerResult.Error.Contains(ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:", StringComparison.Ordinal))
            failures.Add("winner did not surface the legacy process exception");
        if (loserResult?.Error is null || !loserResult.Error.Contains(ProductionFailureReasons.ExecutionProcessFailed, StringComparison.Ordinal))
            failures.Add("loser did not observe EXECUTION_PROCESS_FAILED");
        if (winnerTransport.ConcatExecutionCount != 1 || loserTransport.ConcatExecutionCount != 0 || winnerTransport.FailStarts != 1)
            failures.Add("race ffmpeg counts");
        if (failed.Requests != 1 || failed.Tasks != 1 || failed.Snapshots != 1 || failed.Attempts != 1 || failed.Artifacts != 0)
            failures.Add("failure identity counts " + failed.Text);
        if (failed.Current != 0 || failed.Visible != 0 || failed.Films != 0)
            failures.Add("failure created output binding or film");
        if (failed.RequestStatus != "FAILED" || failed.TaskStatus != "FAILED" || failed.AttemptStatus != "PROVIDER_FAILED")
            failures.Add("failure status " + failed.RequestStatus + "/" + failed.TaskStatus + "/" + failed.AttemptStatus);
        if (failed.FailureCode != ProductionFailureReasons.ExecutionProcessFailed)
            failures.Add("failure code " + failed.FailureCode);
        if (failed.FailureReason is null || !failed.FailureReason.Contains(":ffmpeg-video:", StringComparison.Ordinal))
            failures.Add("failure reason lost the process detail");
        if (failed.Credentials != 0 || failed.Ai != 0)
            failures.Add("credential or ai provider");
        if (failed.Graph != "{}")
            failures.Add("graph_json changed");

        var retryTransport = new FailOnceConcatTransport { FailStartsBudget = 0 };
        var retryOpen = Open(connectionString, videoRoot, retryTransport);
        string retryError = "not-run";
        try
        {
            var slice = await retryOpen.Svc.AssembleEpisodeOsAsync(Command("retry-same-snapshot", sh01Dur, sh02Dur), ct);
            retryError = slice.Output is null ? "no-output" : "artifact=" + slice.Output.ArtifactId;
        }
        catch (Exception ex)
        {
            retryError = ex.GetType().Name + ":" + One(ex.Message);
            failures.Add("retry threw " + retryError);
        }
        var done = await DetailAsync(db, idem, ct);
        report.AppendLine("RETRY " + retryError + " ffmpeg=" + retryTransport.ConcatExecutionCount);
        report.AppendLine("AFTER_RETRY " + done.Text);
        if (retryTransport.ConcatExecutionCount != 1)
            failures.Add("retry ffmpeg count " + retryTransport.ConcatExecutionCount);
        if (done.Requests != 1 || done.Tasks != 1 || done.Snapshots != 1 || done.Attempts != 2)
            failures.Add("retry identity counts " + done.Text);
        if (done.SnapshotId != failed.SnapshotId || done.ProviderId != "server" || done.ModelId != "ffmpeg")
            failures.Add("retry changed snapshot or target");
        if (done.FirstAttemptStatus != "PROVIDER_FAILED" || done.SecondAttemptStatus != "PROVIDER_SUCCEEDED")
            failures.Add("attempt lifecycle " + done.FirstAttemptStatus + "/" + done.SecondAttemptStatus);
        if (done.TaskStatus != "SUCCEEDED" || done.RequestStatus != "COMPLETED" || done.Artifacts != 1)
            failures.Add("retry did not complete");
        if (done.Current != 1 || done.Visible != 1)
            failures.Add("retry bindings " + done.Current + "/" + done.Visible);
        if (done.Credentials != 0 || done.Ai != 0)
            failures.Add("retry used a credential or ai provider");

        var tenantB = await TenantBAsync(connectionString, videoRoot, idem, ct);
        report.AppendLine("TENANT_B " + tenantB);
        if (!tenantB.Contains("assemble=InvalidOperationException:MISSING_DEPENDENCY", StringComparison.Ordinal)
            && !tenantB.Contains("ARTIFACT_MISSING", StringComparison.Ordinal))
            failures.Add("tenant B did not fail closed");
        if (!tenantB.Contains("otherTenantRequests=0", StringComparison.Ordinal))
            failures.Add("tenant B created a request");

        var after = await SnapshotAsync(db, ct);
        report.AppendLine("AFTER " + after.Counts);
        report.AppendLine("HIST_AFTER " + after.Historical);
        report.AppendLine("NULL_AFTER " + after.NullCounts);
        if (before.Historical != after.Historical || after.Historical != ExpectedHistorical)
            failures.Add("null-tenant historical fingerprint changed");
        if (before.NullCounts != after.NullCounts)
            failures.Add("null-tenant counts changed");
        var filesAfter = FileIndex(videoRoot);
        var changed = filesBefore.Count(pair => !filesAfter.TryGetValue(pair.Key, out var later) || later != pair.Value);
        var added = filesAfter.Keys.Except(filesBefore.Keys).ToArray();
        report.AppendLine("FILES_CHANGED " + changed);
        report.AppendLine("FILES_ADDED " + added.Length);
        if (changed != 0)
            failures.Add("historical file mutated");
        report.AppendLine(failures.Count == 0 ? "VERDICT PASS" : "VERDICT FAIL");
        if (failures.Count > 0)
            report.AppendLine("FAILURES " + string.Join(" | ", failures));
        return report.ToString();
    }

    static async Task<CallerResult> CallAsync(
        string label, string connectionString, string videoRoot, IProductionWorkStore store, FailOnceConcatTransport transport,
        decimal sh01Dur, decimal sh02Dur, Func<int> arrive, Func<int> arrived, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, transport, store);
        arrive();
        var until = DateTime.UtcNow.AddSeconds(20);
        while (arrived() < 2)
        {
            if (DateTime.UtcNow > until)
                return new CallerResult(label, "peer did not arrive");
            await Task.Delay(20, ct);
        }
        try
        {
            await opened.Svc.AssembleEpisodeOsAsync(Command(label, sh01Dur, sh02Dur), ct);
            return new CallerResult(label, null);
        }
        catch (Exception ex)
        {
            return new CallerResult(label, ex.GetType().Name + ": " + One(ex.Message));
        }
    }

    static ProductionActionApiCommand Command(string caller, decimal sh01Dur, decimal sh02Dur) =>
        new(
            new ProductionScope(BuildId, "EP01", "EP01", ProductionEpisodeAssemblyContract.EpisodeShotCode),
            new ShotExecutionInput("process failure", "beat", "visual", "", "", (sh01Dur + sh02Dur).ToString(System.Globalization.CultureInfo.InvariantCulture), "", "", DurationSec: sh01Dur + sh02Dur),
            "process-failure-contract",
            caller,
            AssemblyShots:
            [
                (Sh01, Sh01ArtifactId, sh01Dur),
                (Sh02, Sh02ArtifactId, sh02Dur),
            ]);

    static async Task SeedAsync(Opened opened, string shot, Guid artifactId, string fileId, byte[] bytes, decimal duration, CancellationToken ct)
    {
        var scope = new ProductionScope(BuildId, "EP01", "SC01", shot);
        var request = await opened.Os.CreateRequestAsync(scope, ProductionActions.Assemble, "process-seed", "process-seed-" + BuildId.ToString("N") + "-" + shot, ct: ct);
        var task = await opened.Os.CreateTaskAsync(request.RequestId, ct);
        await opened.Os.FreezeInputAsync(task.TaskId, new ShotExecutionInput(
            "process seed", "beat", "visual", "", "", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), "", "",
            DurationSec: duration), "server", "ffmpeg", ct);
        var attempt = await opened.Os.StartAttemptAsync(task.TaskId, "process-seed", ct);
        var key = opened.Files.Write("os", fileId, bytes);
        await opened.Store.InsertArtifactAsync(new ProductionArtifact(
            artifactId, task.TaskId, attempt.AttemptId, ProductionArtifactRoles.Assembly, "video/mp4",
            bytes.Length, duration, KitVideoIntegrityRules.Sha256Hex(bytes), key, "process-seed", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "process-seed"), ct);
        var now = DateTimeOffset.UtcNow;
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), BuildId, shot, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), BuildId, shot, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
    }

    static async Task<string> TenantBAsync(string connectionString, string videoRoot, string idem, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, new FailOnceConcatTransport { FailStartsBudget = 0 }, tenant: TenantB);
        string assemble;
        try
        {
            await opened.Svc.AssembleEpisodeOsAsync(Command("tenant-b", 4, 6), ct);
            assemble = "returned";
        }
        catch (Exception ex)
        {
            assemble = ex.GetType().Name + ":" + One(ex.Message);
        }
        var extra = await CountAsync(new NpgsqlConnectionFactory(connectionString),
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem AND tenant_id <> @Tenant", ct, idem);
        return "assemble=" + assemble + " otherTenantRequests=" + extra + " ffmpeg=" + 0;
    }

    static async Task<Detail> DetailAsync(IDbConnectionFactory db, string idem, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var requests = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem",
            new { Build = BuildId, Idem = idem });
        var row = await conn.QuerySingleOrDefaultAsync<DetailRow>("""
            SELECT r.status AS RequestStatus, t.status AS TaskStatus, t.failure_code AS FailureCode,
                   t.failure_reason AS FailureReason, t.provider_id AS ProviderId, t.model_id AS ModelId,
                   s.snapshot_id::text AS SnapshotId
            FROM pack_content.production_requests r
            LEFT JOIN pack_content.production_tasks t ON t.request_id = r.request_id AND t.action = 'ASSEMBLE_EPISODE'
            LEFT JOIN pack_content.production_task_input_snapshots s ON s.task_id = t.task_id
            WHERE r.build_id = @Build AND r.idempotency_key = @Idem
            """, new { Build = BuildId, Idem = idem });
        var attempts = (await conn.QueryAsync<(int Number, string Status)>("""
            SELECT a.attempt_number AS Number, a.status AS Status
            FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            ORDER BY a.attempt_number
            """, new { Build = BuildId })).ToArray();
        var tasks = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_tasks WHERE build_id = @Build AND action = 'ASSEMBLE_EPISODE'", new { Build = BuildId });
        var snaps = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId });
        var artifacts = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE' AND a.verification_status = 'VERIFIED'
            """, new { Build = BuildId });
        var current = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'CURRENT:ASSEMBLY' AND unbound_at IS NULL
            """, new { Build = BuildId });
        var visible = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'VISIBLE:ASSEMBLY' AND unbound_at IS NULL
            """, new { Build = BuildId });
        var films = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_film_assemblies WHERE build_id = @Build", new { Build = BuildId });
        var creds = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
              AND (s.credential_id IS NOT NULL OR s.credential_version_id IS NOT NULL)
            """, new { Build = BuildId });
        var ai = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build
              AND lower(coalesce(a.provider_id,'')) IN ('runway','fal','elevenlabs','google','veo','openai','gemini')
            """, new { Build = BuildId });
        var graph = await conn.ExecuteScalarAsync<string>(
            "SELECT graph_json::text FROM pack_content.series_build WHERE id = @Build", new { Build = BuildId });
        var text = "requests=" + requests + " tasks=" + tasks + " snapshots=" + snaps + " attempts=" + attempts.Length
            + " artifacts=" + artifacts + " current=" + current + " visible=" + visible + " films=" + films
            + " request=" + row?.RequestStatus + " task=" + row?.TaskStatus + " code=" + row?.FailureCode
            + " attempts=" + string.Join(",", attempts.Select(a => a.Number + ":" + a.Status));
        return new Detail(text, requests, tasks, snaps, attempts.Length, artifacts, current, visible, films,
            row?.RequestStatus ?? "", row?.TaskStatus ?? "", attempts.FirstOrDefault().Status ?? "",
            row?.FailureCode ?? "", row?.FailureReason, row?.ProviderId, row?.ModelId, row?.SnapshotId,
            attempts.ElementAtOrDefault(0).Status ?? "", attempts.ElementAtOrDefault(1).Status ?? "",
            creds, ai, graph ?? "");
    }

    static async Task<DbSnapshot> SnapshotAsync(IDbConnectionFactory db, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var counts = await conn.QuerySingleAsync<string>("""
            SELECT concat_ws('|',
              (SELECT count(*) FROM pack_content.production_requests),
              (SELECT count(*) FROM pack_content.production_tasks),
              (SELECT count(*) FROM pack_content.production_task_input_snapshots),
              (SELECT count(*) FROM pack_content.production_attempts),
              (SELECT count(*) FROM pack_content.production_artifacts),
              (SELECT count(*) FROM pack_content.production_bindings),
              (SELECT count(*) FROM pack_content.production_task_events),
              (SELECT count(*) FROM pack_content.production_film_assemblies),
              (SELECT count(*) FROM pack_content.production_film_assembly_shots))
            """);
        var historical = await conn.ExecuteScalarAsync<string>("""
            SELECT md5(concat_ws('|',
              (SELECT md5(coalesce(string_agg(id::text || status || episode_code || coalesce(graph_json::text,''), ',' ORDER BY id::text), ''))
                 FROM pack_content.series_build WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(artifact_id::text || byte_sha256 || storage_key || duration_sec::text || verification_status, ',' ORDER BY artifact_id::text), ''))
                 FROM pack_content.production_artifacts WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(binding_id::text || role || artifact_id::text || coalesce(unbound_at::text,'null'), ',' ORDER BY binding_id::text), ''))
                 FROM pack_content.production_bindings WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(assembly_id::text || title, ',' ORDER BY assembly_id::text), ''))
                 FROM pack_content.production_film_assemblies WHERE tenant_id IS NULL),
              (SELECT md5(coalesce(string_agg(assembly_shot_id::text || assembly_id::text || shot_code || artifact_id::text, ',' ORDER BY assembly_shot_id::text), ''))
                 FROM pack_content.production_film_assembly_shots WHERE tenant_id IS NULL)))
            """);
        var nullCounts = await conn.ExecuteScalarAsync<string>("""
            SELECT concat_ws('|',
              (SELECT count(*) FROM pack_content.series_build WHERE tenant_id IS NULL),
              (SELECT count(*) FROM pack_content.production_artifacts WHERE tenant_id IS NULL),
              (SELECT count(*) FROM pack_content.production_bindings WHERE tenant_id IS NULL),
              (SELECT count(*) FROM pack_content.production_film_assemblies WHERE tenant_id IS NULL),
              (SELECT count(*) FROM pack_content.production_film_assembly_shots WHERE tenant_id IS NULL))
            """);
        return new DbSnapshot(counts, historical ?? "", nullCounts ?? "");
    }

    static async Task<long> CountAsync(IDbConnectionFactory db, string sql, CancellationToken ct, string? idem = null)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql,
            new { Build = BuildId, Tenant = TenantA, Idem = idem }, cancellationToken: ct));
    }

    static async Task ExecAsync(IDbConnectionFactory db, string sql, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Build = BuildId, Tenant = TenantA }, cancellationToken: ct));
    }

    static IProductionWorkStore Gate(IProductionWorkStore inner, Func<CancellationToken, Task> before)
    {
        var proxy = DispatchProxy.Create<IProductionWorkStore, ProcessInsertGate>();
        var gate = (ProcessInsertGate)(object)proxy;
        gate.Inner = inner;
        gate.Before = before;
        return proxy;
    }

    static Opened Open(string connectionString, string videoRoot, FailOnceConcatTransport transport, IProductionWorkStore? store = null, Guid? tenant = null)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var caller = new FixedCaller(tenant ?? TenantA);
        store ??= new ProductionWorkRepository(db, caller);
        var files = new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot }), new HostEnv(videoRoot)),
            caller);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(),
            officialEpisode: new FfmpegEpisodeAssemblyExecutionAdapter(transport));
        return new Opened(svc, store, files, os);
    }

    static ProductionEpisodeAssemblyInput Input(int order, string shot, Guid id, byte[] bytes, decimal duration, string key) =>
        new(order, shot, ProductionArtifactRoles.Assembly, id, KitVideoIntegrityRules.Sha256Hex(bytes), key, duration, 0, 0);

    static Dictionary<string, long> FileIndex(string root)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return map;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            map[Path.GetRelativePath(root, path)] = new FileInfo(path).Length;
        return map;
    }

    static string Format(CallerResult row) => row.Label + " error=" + (row.Error ?? "returned");
    static string One(string raw) => raw.Replace('\r', ' ').Replace('\n', ' ').Trim();

    sealed record Opened(ProductionCreateVideoService Svc, IProductionWorkStore Store, IProductionArtifactFileStore Files, ProductionExecutionService Os);
    sealed record CallerResult(string Label, string? Error);
    sealed record DbSnapshot(string Counts, string Historical, string NullCounts);
    sealed record Detail(
        string Text, long Requests, long Tasks, long Snapshots, long Attempts, long Artifacts,
        long Current, long Visible, long Films, string RequestStatus, string TaskStatus, string AttemptStatus,
        string FailureCode, string? FailureReason, string? ProviderId, string? ModelId, string? SnapshotId,
        string FirstAttemptStatus, string SecondAttemptStatus, long Credentials, long Ai, string Graph);
    sealed class DetailRow
    {
        public string RequestStatus { get; set; } = "";
        public string TaskStatus { get; set; } = "";
        public string? FailureCode { get; set; }
        public string? FailureReason { get; set; }
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string? SnapshotId { get; set; }
    }

    sealed class FailOnceConcatTransport : IFfmpegMixTransport
    {
        readonly ProcessFfmpegMixTransport _inner = new(new ProductionFfmpegMixAudit());
        public int FailStartsBudget { get; set; } = 1;
        public int FailStarts { get; private set; }
        public bool RequiresNetwork => false;
        public int ExecutionCount => _inner.ExecutionCount;
        public int ComposeExecutionCount => _inner.ComposeExecutionCount;
        public int ConcatExecutionCount => FailStarts + _inner.ConcatExecutionCount;
        public string? LastCommand => _inner.LastCommand;
        public string? LastComposeCommand => _inner.LastComposeCommand;
        public string? LastConcatCommand => _inner.LastConcatCommand;
        public Task<FfmpegMixTransportResult> RemuxOnceAsync(FfmpegMixTransportRequest request, CancellationToken ct = default) =>
            _inner.RemuxOnceAsync(request, ct);
        public Task<FfmpegMixTransportResult> ComposeSegmentsOnceAsync(FfmpegMixComposeRequest request, CancellationToken ct = default) =>
            _inner.ComposeSegmentsOnceAsync(request, ct);
        public async Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(FfmpegEpisodeConcatRequest request, CancellationToken ct = default)
        {
            if (FailStarts < FailStartsBudget)
            {
                FailStarts++;
                var missing = Path.Combine(Path.GetTempPath(), "famixa-missing-" + Guid.NewGuid().ToString("N") + ".mp4");
                var psi = new ProcessStartInfo("ffmpeg", "-hide_banner -loglevel error -i \"" + missing + "\" -f null -")
                {
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi) ?? throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-missing");
                using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, limit.Token);
                string stderr;
                try
                {
                    _ = await process.StandardOutput.ReadToEndAsync(linked.Token);
                    stderr = await process.StandardError.ReadToEndAsync(linked.Token);
                    await process.WaitForExitAsync(linked.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { /* already gone */ }
                    throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:process timed out");
                }
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity + ":ffmpeg-video:" + stderr.Trim());
            }
            return await _inner.ConcatCopyOnceAsync(request, ct);
        }
    }

    public class ProcessInsertGate : DispatchProxy
    {
        internal IProductionWorkStore Inner { get; set; } = null!;
        internal Func<CancellationToken, Task> Before { get; set; } = _ => Task.CompletedTask;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("PROXY");
            if (targetMethod.Name == nameof(IProductionWorkStore.InsertRequestAsync))
                return InsertAsync(targetMethod, args);
            return targetMethod.Invoke(Inner, args);
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
            await ((Task)method.Invoke(Inner, args)!).ConfigureAwait(false);
        }
    }

    sealed class FixedCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class HostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "process-failure-contract";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
