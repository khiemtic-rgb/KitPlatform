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
/// PRODUCTION-EPISODE-FIRST-INSERT-FAILURE-CONVERGENCE-01.
/// Observes the official Episode Assembly path when local FFmpeg fails during first insert.
/// Does not add a failure transition.
/// </summary>
public static class ProductionEpisodeFirstInsertFailureConvergenceV1
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid BuildId = Guid.Parse("f4f4f4f4-f4f4-4f4f-8f4f-f4f4f4f4f401");
    public static readonly Guid Sh01ArtifactId = Guid.Parse("f4f40001-f4f4-4f4f-8f4f-f4f400000001");
    public static readonly Guid Sh02ArtifactId = Guid.Parse("f4f40002-f4f4-4f4f-8f4f-f4f400000002");
    const string Sh01 = "EP01-SC01-SH01";
    const string Sh02 = "EP01-SC01-SH02";
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
        report.AppendLine("GRAPH_BEFORE " + before.Graph);

        if (await CountAsync(db, "SELECT count(*) FROM pack_content.series_build WHERE id = @Build", null, ct) != 0)
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
                @Build, 'RACE_EP01_FAILURE', 'EP01', 'first-insert failure', 'draft', 2, 0, 0, 0,
                '{}'::jsonb, now(), now(), @Tenant)
            """, ct);

        var tx = await ProveTransactionAsync(connectionString, videoRoot, ct);
        report.AppendLine("TX_IDEMPOTENCY " + tx.Idem);
        report.AppendLine("TX_OTHER_23505 " + tx.Other);
        report.AppendLine("TX_ROWS_LEFT " + tx.Left);
        if (tx.Idem != "TX_SELECT_OK")
            failures.Add("transaction reload after 23505 failed");
        if (!tx.Other.StartsWith("PRESERVED 23505 ", StringComparison.Ordinal)
            || tx.Other.Contains("uq_prod_request_tenant_idempotency", StringComparison.Ordinal))
            failures.Add("non-idempotency 23505 was translated");
        if (tx.Left != 0)
            failures.Add("transaction proof committed a row");

        var sh01Bytes = new byte[64];
        var sh02Bytes = new byte[64];
        Array.Fill(sh01Bytes, (byte)0x11);
        Array.Fill(sh02Bytes, (byte)0x22);
        var seed = Open(connectionString, videoRoot, new RealTransport());
        await SeedAsync(seed, Sh01, Sh01ArtifactId, "f4f4sh01failin", sh01Bytes, 4m, ct);
        await SeedAsync(seed, Sh02, Sh02ArtifactId, "f4f4sh02failin", sh02Bytes, 6m, ct);
        var contract = ProductionEpisodeAssemblyContract.Build(BuildId, "EP01",
        [
            Input(0, Sh01, Sh01ArtifactId, sh01Bytes, 4m, TenantA.ToString("N") + "/os/f4f4sh01failin"),
            Input(1, Sh02, Sh02ArtifactId, sh02Bytes, 6m, TenantA.ToString("N") + "/os/f4f4sh02failin"),
        ]);
        var idem = string.Join(':',
            ProductionActions.AssembleEpisode, contract.ContractVersion, BuildId.ToString("N"), "EP01", contract.ContractHash);
        var absent = await StateAsync(db, idem, ct);
        report.AppendLine("IDENTITY build=" + BuildId + " hash=" + contract.ContractHash);
        report.AppendLine("IDEMPOTENCY " + idem);
        report.AppendLine("ABSENT " + absent.Text);
        if (absent.Requests != 0 || absent.Tasks != 0 || absent.Snapshots != 0 || absent.Attempts != 0
            || absent.Artifacts != 0 || absent.Current != 0 || absent.Visible != 0)
            failures.Add("identity was not absent");

        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var winnerTransport = new RealTransport();
        var loserTransport = new RealTransport();
        var winnerStore = Gate(new ProductionWorkRepository(db, new FixedCaller(TenantA)),
            async innerCt => await bEntered.Task.WaitAsync(innerCt));
        var loserStore = Gate(new ProductionWorkRepository(db, new FixedCaller(TenantA)),
            async innerCt =>
            {
                bEntered.TrySetResult();
                await releaseB.Task.WaitAsync(innerCt);
            });
        var winner = CallAsync("A", "caller-a-fail-not-identity", connectionString, videoRoot, winnerStore, winnerTransport,
            () => Interlocked.Increment(ref arrived), () => Volatile.Read(ref arrived), ct);
        var loser = CallAsync("B", "caller-b-fail-not-identity", connectionString, videoRoot, loserStore, loserTransport,
            () => Interlocked.Increment(ref arrived), () => Volatile.Read(ref arrived), ct);
        CallerResult? winnerResult = null;
        CallerResult? loserResult = null;
        try
        {
            await bEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            winnerResult = await winner.WaitAsync(TimeSpan.FromSeconds(60), ct);
            var afterWinner = await StateAsync(db, idem, ct);
            report.AppendLine("WINNER " + Format(winnerResult));
            report.AppendLine("AFTER_WINNER " + afterWinner.Text
                + " ffmpegA=" + winnerTransport.ConcatStarts + " ffmpegB=" + loserTransport.ConcatStarts
                + " loserDone=" + loser.IsCompleted);
            if (loser.IsCompleted)
                failures.Add("loser finished before its insert was released");
            if (afterWinner.Requests != 1 || afterWinner.Tasks != 1 || afterWinner.Snapshots != 1 || afterWinner.Attempts != 1
                || afterWinner.Artifacts != 0 || afterWinner.Current != 0 || afterWinner.Visible != 0)
                failures.Add("winner failure row counts " + afterWinner.Text);
            if (winnerTransport.ConcatStarts != 1 || loserTransport.ConcatStarts != 0)
                failures.Add("ffmpeg entry count");
            releaseB.TrySetResult();
            await Task.Delay(500, ct);
            var waiting = await StateAsync(db, idem, ct);
            report.AppendLine("LOSER_WAITING loserDone=" + loser.IsCompleted + " " + waiting.Text
                + " ffmpegB=" + loserTransport.ConcatStarts);
            if (loser.IsCompleted)
                failures.Add("loser returned before the in-flight bound");
            if (waiting.Requests != 1 || waiting.Artifacts != 0 || loserTransport.ConcatStarts != 0)
                failures.Add("loser created work while winner was failed");
            var watch = Stopwatch.StartNew();
            loserResult = await loser.WaitAsync(TimeSpan.FromSeconds(120), ct);
            watch.Stop();
            report.AppendLine("LOSER " + Format(loserResult) + " elapsedMs=" + watch.ElapsedMilliseconds);
            if (watch.ElapsedMilliseconds < 80_000)
                failures.Add("loser did not wait for the official bound");
        }
        catch (Exception ex)
        {
            failures.Add("harness " + ex.GetType().Name + ":" + One(ex.Message));
        }
        finally
        {
            releaseB.TrySetResult();
        }

        var final = await StateAsync(db, idem, ct);
        report.AppendLine("FINAL " + final.Text);
        report.AppendLine("FFMPEG_A " + winnerTransport.ConcatStarts + " FFMPEG_B " + loserTransport.ConcatStarts);
        if (winnerResult?.Error is null
            || !winnerResult.Error.Contains(ProductionFailureReasons.FailedIdentity + ":ffmpeg-", StringComparison.Ordinal))
            failures.Add("winner did not fail inside local ffmpeg");
        if (loserResult?.Error is null || !loserResult.Error.Contains(ProductionFailureReasons.InFlightReplay, StringComparison.Ordinal))
            failures.Add("loser did not end on IN_FLIGHT_REPLAY");
        if (final.Requests != 1 || final.Tasks != 1 || final.Snapshots != 1 || final.Attempts != 1 || final.Artifacts != 0)
            failures.Add("final identity counts");
        if (final.Current != 0 || final.Visible != 0)
            failures.Add("failure created a binding");
        if (final.RequestStatus != "IN_PROGRESS" || final.TaskStatus != "RUNNING" || final.AttemptStatus != "STARTED")
            failures.Add("terminal state " + final.RequestStatus + "/" + final.TaskStatus + "/" + final.AttemptStatus);
        if (final.Ai != 0 || final.Credentials != 0)
            failures.Add("provider or credential");

        var after = await SnapshotAsync(db, ct);
        report.AppendLine("AFTER " + after.Counts);
        report.AppendLine("HIST_AFTER " + after.Historical);
        report.AppendLine("NULL_AFTER " + after.NullCounts);
        report.AppendLine("GRAPH_AFTER " + after.Graph);
        if (before.Historical != after.Historical || after.Historical != ExpectedHistorical)
            failures.Add("null-tenant historical fingerprint changed");
        if (before.NullCounts != after.NullCounts)
            failures.Add("null-tenant counts changed");
        var filesAfter = FileIndex(videoRoot);
        var changed = filesBefore.Count(pair => !filesAfter.TryGetValue(pair.Key, out var later) || later != pair.Value);
        var added = filesAfter.Keys.Except(filesBefore.Keys).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.AppendLine("FILES_CHANGED " + changed);
        report.AppendLine("FILES_ADDED " + added.Length);
        foreach (var path in added)
            report.AppendLine("NEW_FILE " + path);
        if (changed != 0)
            failures.Add("historical file mutated");
        report.AppendLine("FAILURE-CONVERGENCE-CONTRACT = NOT DEFINED");
        report.AppendLine("EPISODE-ASSEMBLY-RETRY-AFTER-FIRST-INSERT-FAILURE = NOT PROVEN");
        report.AppendLine(failures.Count == 0 ? "OBSERVATION COMPLETE" : "OBSERVATION INCOMPLETE");
        if (failures.Count > 0)
            report.AppendLine("FAILURES " + string.Join(" | ", failures));
        return report.ToString();
    }

    static async Task<CallerResult> CallAsync(
        string label, string callerKey, string connectionString, string videoRoot,
        IProductionWorkStore store, RealTransport transport, Func<int> arrive, Func<int> arrived, CancellationToken ct)
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
            await opened.Svc.AssembleEpisodeOsAsync(Command(callerKey), ct);
            return new CallerResult(label, null);
        }
        catch (Exception ex)
        {
            var detail = ex.GetType().Name + ": " + One(ex.Message);
            if (ex is PostgresException pg)
                detail += " sqlstate=" + pg.SqlState + " constraint=" + pg.ConstraintName;
            return new CallerResult(label, detail);
        }
    }

    static async Task SeedAsync(
        Opened opened, string shot, Guid artifactId, string fileId, byte[] bytes, decimal duration, CancellationToken ct)
    {
        var scope = new ProductionScope(BuildId, "EP01", "SC01", shot);
        var request = await opened.Os.CreateRequestAsync(scope, ProductionActions.Assemble, "fail-seed", "fail-seed-" + shot, ct: ct);
        var task = await opened.Os.CreateTaskAsync(request.RequestId, ct);
        await opened.Os.FreezeInputAsync(task.TaskId, new ShotExecutionInput(
            "failure seed", "beat", "visual", "", "", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), "", "",
            DurationSec: duration), "server", "ffmpeg", ct);
        var attempt = await opened.Os.StartAttemptAsync(task.TaskId, "fail-seed", ct);
        var key = opened.Files.Write("os", fileId, bytes);
        await opened.Store.InsertArtifactAsync(new ProductionArtifact(
            artifactId, task.TaskId, attempt.AttemptId, ProductionArtifactRoles.Assembly, "video/mp4",
            bytes.Length, duration, KitVideoIntegrityRules.Sha256Hex(bytes), key, "fail-seed-local", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "fail-seed"), ct);
        var now = DateTimeOffset.UtcNow;
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), BuildId, shot, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), BuildId, shot, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
    }

    static ProductionActionApiCommand Command(string callerKey) =>
        new(
            new ProductionScope(BuildId, "EP01", "EP01", ProductionEpisodeAssemblyContract.EpisodeShotCode),
            new ShotExecutionInput("failure convergence", "beat", "visual", "", "", "10", "", "", DurationSec: 10m),
            "episode-failure-convergence",
            callerKey,
            AssemblyShots:
            [
                (Sh01, Sh01ArtifactId, 4m),
                (Sh02, Sh02ArtifactId, 6m),
            ]);

    static async Task<(string Idem, string Other, long Left)> ProveTransactionAsync(
        string connectionString, string videoRoot, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, new RealTransport());
        var scope = new ProductionScope(BuildId, "EP01", "SC01", Sh01);
        var idem = await ConflictAsync(opened, scope, "fail-tx-proof-" + BuildId.ToString("N"), sameRequestId: false, ct);
        var other = await ConflictAsync(opened, scope, "fail-tx-proof-pk-" + BuildId.ToString("N"), sameRequestId: true, ct);
        var left = await CountAsync(new NpgsqlConnectionFactory(connectionString),
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key LIKE 'fail-tx-proof-%'",
            null, ct);
        return (idem, other, left);
    }

    static async Task<string> ConflictAsync(Opened opened, ProductionScope scope, string key, bool sameRequestId, CancellationToken ct)
    {
        var result = "NOT_RUN";
        try
        {
            await opened.Store.InTransactionAsync<int>(async inner =>
            {
                var first = await opened.Os.CreateRequestAsync(scope, ProductionActions.Assemble, "tx-proof", key, ct: inner);
                try
                {
                    var duplicate = sameRequestId
                        ? first with { IdempotencyKey = key + "-other" }
                        : first with { RequestId = Guid.NewGuid() };
                    await opened.Store.InsertRequestAsync(duplicate, inner);
                    result = "NO_CONFLICT";
                }
                catch (InvalidOperationException ex) when (ex.Message == "IDEMPOTENCY_CONFLICT")
                {
                    var loaded = await opened.Store.GetRequestByIdempotencyAsync(key, inner);
                    result = loaded?.RequestId == first.RequestId ? "TX_SELECT_OK" : "TX_SELECT_MISS";
                }
                catch (PostgresException pg)
                {
                    result = "PRESERVED " + pg.SqlState + " " + pg.ConstraintName;
                }
                throw new InvalidOperationException("ROLLBACK_PROOF");
            }, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message == "ROLLBACK_PROOF")
        {
        }
        catch (Exception ex)
        {
            result = "OUTER " + ex.GetType().Name + ":" + One(ex.Message) + " inner=" + result;
        }
        return result;
    }

    static async Task<RaceState> StateAsync(IDbConnectionFactory db, string idem, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var requests = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem",
            new { Build = BuildId, Idem = idem });
        var row = await conn.QuerySingleOrDefaultAsync<StatusRow>("""
            SELECT r.status AS RequestStatus, t.status AS TaskStatus, a.status AS AttemptStatus
            FROM pack_content.production_requests r
            LEFT JOIN pack_content.production_tasks t ON t.request_id = r.request_id AND t.action = 'ASSEMBLE_EPISODE'
            LEFT JOIN pack_content.production_attempts a ON a.task_id = t.task_id
            WHERE r.build_id = @Build AND r.idempotency_key = @Idem
            """, new { Build = BuildId, Idem = idem });
        var tasks = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_tasks WHERE build_id = @Build AND action = 'ASSEMBLE_EPISODE'",
            new { Build = BuildId });
        var snaps = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId });
        var attempts = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId });
        var artifacts = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId });
        var current = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'CURRENT:ASSEMBLY' AND unbound_at IS NULL
            """, new { Build = BuildId });
        var visible = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'VISIBLE:ASSEMBLY' AND unbound_at IS NULL
            """, new { Build = BuildId });
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
        var text = "requests=" + requests + " tasks=" + tasks + " snapshots=" + snaps + " attempts=" + attempts
            + " artifacts=" + artifacts + " current=" + current + " visible=" + visible
            + " requestStatus=" + (row?.RequestStatus ?? "")
            + " taskStatus=" + (row?.TaskStatus ?? "")
            + " attemptStatus=" + (row?.AttemptStatus ?? "")
            + " creds=" + creds + " ai=" + ai;
        return new RaceState(text, requests, tasks, snaps, attempts, artifacts, current, visible,
            row?.RequestStatus ?? "", row?.TaskStatus ?? "", row?.AttemptStatus ?? "", creds, ai);
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
        var graph = await conn.ExecuteScalarAsync<string>("""
            SELECT md5(coalesce(string_agg(id::text || coalesce(tenant_id::text,'null') || status || coalesce(graph_json::text,''), ',' ORDER BY id::text), ''))
            FROM pack_content.series_build
            """);
        return new DbSnapshot(counts, historical ?? "", nullCounts ?? "", graph ?? "");
    }

    static async Task<long> CountAsync(IDbConnectionFactory db, string sql, string? idem, CancellationToken ct)
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
        var proxy = DispatchProxy.Create<IProductionWorkStore, FailureInsertGate>();
        var gate = (FailureInsertGate)(object)proxy;
        gate.Inner = inner;
        gate.Before = before;
        return proxy;
    }

    static Opened Open(string connectionString, string videoRoot, RealTransport transport, IProductionWorkStore? store = null)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var caller = new FixedCaller(TenantA);
        store ??= new ProductionWorkRepository(db, caller);
        var files = new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot }), new HostEnv(videoRoot)),
            caller);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var episode = new FfmpegEpisodeAssemblyExecutionAdapter(transport);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), officialEpisode: episode);
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
    sealed record DbSnapshot(string Counts, string Historical, string NullCounts, string Graph);
    sealed record RaceState(
        string Text, long Requests, long Tasks, long Snapshots, long Attempts, long Artifacts,
        long Current, long Visible, string RequestStatus, string TaskStatus, string AttemptStatus,
        long Credentials, long Ai);
    sealed class StatusRow
    {
        public string RequestStatus { get; set; } = "";
        public string TaskStatus { get; set; } = "";
        public string AttemptStatus { get; set; } = "";
    }

    sealed class RealTransport : IFfmpegMixTransport
    {
        readonly ProcessFfmpegMixTransport _inner = new(new ProductionFfmpegMixAudit());
        public int ConcatStarts => _inner.ConcatExecutionCount;
        public bool RequiresNetwork => false;
        public int ExecutionCount => _inner.ExecutionCount;
        public int ComposeExecutionCount => _inner.ComposeExecutionCount;
        public int ConcatExecutionCount => _inner.ConcatExecutionCount;
        public string? LastCommand => _inner.LastCommand;
        public string? LastComposeCommand => _inner.LastComposeCommand;
        public string? LastConcatCommand => _inner.LastConcatCommand;
        public Task<FfmpegMixTransportResult> RemuxOnceAsync(FfmpegMixTransportRequest request, CancellationToken ct = default) =>
            _inner.RemuxOnceAsync(request, ct);
        public Task<FfmpegMixTransportResult> ComposeSegmentsOnceAsync(FfmpegMixComposeRequest request, CancellationToken ct = default) =>
            _inner.ComposeSegmentsOnceAsync(request, ct);
        public Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(FfmpegEpisodeConcatRequest request, CancellationToken ct = default) =>
            _inner.ConcatCopyOnceAsync(request, ct);
    }

    public class FailureInsertGate : DispatchProxy
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
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "episode-failure-convergence";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
