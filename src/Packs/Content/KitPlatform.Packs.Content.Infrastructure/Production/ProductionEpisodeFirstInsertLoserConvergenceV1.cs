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
/// PRODUCTION-EPISODE-FIRST-INSERT-LOSER-CONVERGENCE-01.
/// Proves a losing official AssembleEpisodeOsAsync caller converges on a fresh identity.
/// </summary>
public static class ProductionEpisodeFirstInsertLoserConvergenceV1
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    public static readonly Guid InFlightBuildId = Guid.Parse("f2f2f2f2-f2f2-4f2f-8f2f-f2f2f2f2f201");
    public static readonly Guid CompletedBuildId = Guid.Parse("f3f3f3f3-f3f3-4f3f-8f3f-f3f3f3f3f301");
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
        report.AppendLine("GRAPH_BEFORE " + before.Graph);

        var seed = Open(connectionString, videoRoot, TenantA, new CountTransport());
        var sh01Bytes = seed.Files.Read(Sh01SourceKey) ?? throw new InvalidOperationException("SH01 source missing");
        var sh02Bytes = seed.Files.Read(Sh02SourceKey) ?? throw new InvalidOperationException("SH02 source missing");
        var sh01Dur = Measured(sh01Bytes);
        var sh02Dur = Measured(sh02Bytes);
        report.AppendLine("INPUT_DURATION sh01=" + sh01Dur + " sh02=" + sh02Dur);
        if (sh01Dur <= 0 || sh02Dur <= 0)
            failures.Add("input duration");

        await ProveTransactionAsync(connectionString, videoRoot, report, failures, ct);

        var inflight = await RaceAsync(
            connectionString, videoRoot, db, sh01Bytes, sh02Bytes, sh01Dur, sh02Dur,
            new CaseSpec(
                "IN_FLIGHT", InFlightBuildId,
                Guid.Parse("f2f20001-f2f2-4f2f-8f2f-f2f200000001"),
                Guid.Parse("f2f20002-f2f2-4f2f-8f2f-f2f200000002"),
                "f2f2sh01loserin", "f2f2sh02loserin", "RACE_EP01_LOSER_INFLIGHT",
                PauseWinner: true),
            report, failures, ct);

        var completed = await RaceAsync(
            connectionString, videoRoot, db, sh01Bytes, sh02Bytes, sh01Dur, sh02Dur,
            new CaseSpec(
                "COMPLETED", CompletedBuildId,
                Guid.Parse("f3f30001-f3f3-4f3f-8f3f-f3f300000001"),
                Guid.Parse("f3f30002-f3f3-4f3f-8f3f-f3f300000002"),
                "f3f3sh01loserok", "f3f3sh02loserok", "RACE_EP01_LOSER_COMPLETE",
                PauseWinner: false),
            report, failures, ct);

        if (inflight is not null)
            report.AppendLine("TENANT_B " + await TenantBAsync(connectionString, videoRoot, inflight, ct));
        report.AppendLine("FIRST-INSERT-FAILURE-CONVERGENCE = NOT PROVEN");

        var after = await SnapshotAsync(db, ct);
        report.AppendLine("AFTER " + after.Counts);
        report.AppendLine("HIST_AFTER " + after.Historical);
        report.AppendLine("NULL_AFTER " + after.NullCounts);
        report.AppendLine("GRAPH_AFTER " + after.Graph);
        if (before.Historical != after.Historical || after.Historical != ExpectedHistorical)
            failures.Add("null-tenant historical fingerprint changed");
        if (before.NullCounts != after.NullCounts)
            failures.Add("null-tenant counts changed");
        report.AppendLine("ALL_BUILDS_GRAPH_HELPER includes tenant-owned series_build rows; it is not the null-tenant historical population.");

        var filesAfter = FileIndex(videoRoot);
        var changed = filesBefore.Count(pair => !filesAfter.TryGetValue(pair.Key, out var later) || later != pair.Value);
        var added = filesAfter.Keys.Except(filesBefore.Keys).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.AppendLine("FILES_CHANGED " + changed);
        report.AppendLine("FILES_ADDED " + added.Length);
        foreach (var path in added)
            report.AppendLine("NEW_FILE " + path);
        if (changed != 0)
            failures.Add("historical file mutated");
        if (added.Any(path => !path.Replace('\\', '/').StartsWith(TenantA.ToString("N") + "/", StringComparison.OrdinalIgnoreCase)))
            failures.Add("unprefixed file write");

        report.AppendLine(failures.Count == 0 ? "VERDICT PASS" : "VERDICT FAIL");
        if (failures.Count > 0)
            report.AppendLine("FAILURES " + string.Join(" | ", failures));
        return report.ToString();
    }

    static async Task ProveTransactionAsync(
        string connectionString, string videoRoot, StringBuilder report, List<string> failures, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, TenantA, new CountTransport());
        var scope = new ProductionScope(InFlightBuildId, "EP01", "SC01", Sh01);
        if (await CountAsync(new NpgsqlConnectionFactory(connectionString),
                "SELECT count(*) FROM pack_content.series_build WHERE id = @Build", InFlightBuildId, null, ct) == 0)
        {
            await ExecBuildAsync(new NpgsqlConnectionFactory(connectionString), InFlightBuildId, "RACE_EP01_LOSER_INFLIGHT", ct);
        }

        var key = "loser-tx-proof-" + InFlightBuildId.ToString("N");
        var tx = await ConflictInsideTransactionAsync(opened, scope, key, sameRequestId: false, ct);
        report.AppendLine("TX_IDEMPOTENCY " + tx);
        if (tx != "TX_SELECT_OK")
            failures.Add("transaction reload after 23505 failed");

        var pk = await ConflictInsideTransactionAsync(opened, scope, key + "-pk", sameRequestId: true, ct);
        report.AppendLine("TX_OTHER_23505 " + pk);
        if (!pk.StartsWith("PRESERVED 23505 ", StringComparison.Ordinal)
            || pk.Contains("uq_prod_request_tenant_idempotency", StringComparison.Ordinal))
            failures.Add("non-idempotency 23505 was translated");

        var left = await CountAsync(new NpgsqlConnectionFactory(connectionString),
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key LIKE 'loser-tx-proof-%'",
            InFlightBuildId, null, ct);
        report.AppendLine("TX_ROWS_LEFT " + left);
        if (left != 0)
            failures.Add("transaction proof committed a row");
    }

    static async Task<string> ConflictInsideTransactionAsync(
        Opened opened, ProductionScope scope, string key, bool sameRequestId, CancellationToken ct)
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

    static async Task<CaseOutcome?> RaceAsync(
        string connectionString, string videoRoot, IDbConnectionFactory db,
        byte[] sh01Bytes, byte[] sh02Bytes, decimal sh01Dur, decimal sh02Dur,
        CaseSpec spec, StringBuilder report, List<string> failures, CancellationToken ct)
    {
        report.AppendLine("CASE " + spec.Name);
        if (await CountAsync(db, "SELECT count(*) FROM pack_content.series_build WHERE id = @Build", spec.BuildId, null, ct) != 0
            && spec.BuildId != InFlightBuildId)
        {
            failures.Add(spec.Name + " build already existed");
            return null;
        }
        if (spec.BuildId != InFlightBuildId)
            await ExecBuildAsync(db, spec.BuildId, spec.SeriesCode, ct);

        var seed = Open(connectionString, videoRoot, TenantA, new CountTransport());
        var sh01Key = TenantA.ToString("N") + "/os/" + spec.Sh01File;
        var sh02Key = TenantA.ToString("N") + "/os/" + spec.Sh02File;
        await SeedInputAsync(seed, spec, Sh01, spec.Sh01Artifact, spec.Sh01File, sh01Bytes, sh01Dur, ct);
        await SeedInputAsync(seed, spec, Sh02, spec.Sh02Artifact, spec.Sh02File, sh02Bytes, sh02Dur, ct);
        var contract = ProductionEpisodeAssemblyContract.Build(spec.BuildId, "EP01",
        [
            Input(0, Sh01, spec.Sh01Artifact, sh01Bytes, sh01Dur, sh01Key),
            Input(1, Sh02, spec.Sh02Artifact, sh02Bytes, sh02Dur, sh02Key),
        ]);
        var idem = string.Join(':',
            ProductionActions.AssembleEpisode, contract.ContractVersion, spec.BuildId.ToString("N"), "EP01", contract.ContractHash);
        var absent = await EpisodeCountsAsync(db, spec.BuildId, idem, ct);
        report.AppendLine("IDENTITY build=" + spec.BuildId + " hash=" + contract.ContractHash + " idem=" + idem);
        report.AppendLine("ABSENT " + absent.Text);
        if (absent.Requests != 0 || absent.Tasks != 0 || absent.Snapshots != 0 || absent.Attempts != 0
            || absent.Artifacts != 0 || absent.Current != 0 || absent.Visible != 0 || absent.Films != 0)
        {
            failures.Add(spec.Name + " identity was not absent");
            return null;
        }

        var bEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseB = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var aPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        var winnerTransport = spec.PauseWinner
            ? new CountTransport(async () =>
            {
                aPaused.TrySetResult();
                await releaseA.Task.WaitAsync(ct);
            })
            : new CountTransport();
        var loserTransport = new CountTransport();
        var winnerStore = Gate(new ProductionWorkRepository(new NpgsqlConnectionFactory(connectionString), new FixedCaller(TenantA)),
            async innerCt => await bEntered.Task.WaitAsync(innerCt));
        var loserStore = Gate(new ProductionWorkRepository(new NpgsqlConnectionFactory(connectionString), new FixedCaller(TenantA)),
            async innerCt =>
            {
                bEntered.TrySetResult();
                await releaseB.Task.WaitAsync(innerCt);
            });

        var winner = CallAsync("A", "caller-a-loser-not-identity", connectionString, videoRoot, winnerStore, winnerTransport,
            spec, sh01Dur, sh02Dur, () => Interlocked.Increment(ref arrived), () => Volatile.Read(ref arrived), ct);
        var loser = CallAsync("B", "caller-b-loser-not-identity", connectionString, videoRoot, loserStore, loserTransport,
            spec, sh01Dur, sh02Dur, () => Interlocked.Increment(ref arrived), () => Volatile.Read(ref arrived), ct);

        try
        {
            try
            {
                await bEntered.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            }
            catch (TimeoutException)
            {
                failures.Add(spec.Name + " loser did not reach insert");
                return null;
            }
            report.AppendLine("LOSER_AT_INSERT winnerPaused=" + spec.PauseWinner);

            if (spec.PauseWinner)
            {
                try
                {
                    await aPaused.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
                }
                catch (TimeoutException)
                {
                    failures.Add(spec.Name + " winner did not reach concat");
                    return null;
                }
                var owned = await EpisodeCountsAsync(db, spec.BuildId, idem, ct);
                report.AppendLine("IN_FLIGHT_OWNED " + owned.Text + " ffmpegA=" + winnerTransport.Starts + " ffmpegB=" + loserTransport.Starts);
                if (owned.Requests != 1 || owned.Tasks != 1 || owned.Snapshots != 1 || owned.Attempts != 1 || owned.Artifacts != 0)
                    failures.Add(spec.Name + " in-flight row counts " + owned.Text);
                if (winnerTransport.Starts != 0 || loserTransport.Starts != 0)
                    failures.Add(spec.Name + " ffmpeg ran before release");
                releaseB.TrySetResult();
                await Task.Delay(400, ct);
                var waiting = await EpisodeCountsAsync(db, spec.BuildId, idem, ct);
                report.AppendLine("IN_FLIGHT_LOSER_WAITING loserDone=" + loser.IsCompleted
                    + " " + waiting.Text + " ffmpegA=" + winnerTransport.Starts + " ffmpegB=" + loserTransport.Starts);
                if (loser.IsCompleted)
                    failures.Add(spec.Name + " loser finished while winner was paused");
                if (waiting.Requests != 1 || waiting.Tasks != 1 || waiting.Snapshots != 1 || waiting.Attempts != 1 || waiting.Artifacts != 0)
                    failures.Add(spec.Name + " loser created rows while winner was in flight");
                if (winnerTransport.Starts != 0 || loserTransport.Starts != 0)
                    failures.Add(spec.Name + " ffmpeg ran while winner was paused");
                releaseA.TrySetResult();
            }
            else
            {
                var winnerResult = await winner.WaitAsync(TimeSpan.FromSeconds(90), ct);
                var owned = await EpisodeCountsAsync(db, spec.BuildId, idem, ct);
                report.AppendLine("COMPLETED_BEFORE_LOSER_INSERT loserDone=" + loser.IsCompleted
                    + " winnerArtifact=" + (winnerResult.ArtifactId?.ToString() ?? "null")
                    + " " + owned.Text + " ffmpegA=" + winnerTransport.Starts + " ffmpegB=" + loserTransport.Starts);
                if (loser.IsCompleted)
                    failures.Add(spec.Name + " loser finished before its insert was released");
                if (owned.Artifacts != 1 || winnerTransport.Starts != 1 || loserTransport.Starts != 0)
                    failures.Add(spec.Name + " completed-winner physical state");
                releaseB.TrySetResult();
            }

            var pair = await Task.WhenAll(winner, loser).WaitAsync(TimeSpan.FromSeconds(90), ct);
            foreach (var row in pair)
                report.AppendLine("CALLER " + row.Label
                    + " artifact=" + (row.ArtifactId?.ToString() ?? "null")
                    + " request=" + (row.RequestId?.ToString() ?? "null")
                    + " task=" + (row.TaskId?.ToString() ?? "null")
                    + " snapshot=" + (row.SnapshotId?.ToString() ?? "null")
                    + " attempt=" + (row.AttemptId?.ToString() ?? "null")
                    + " hash=" + (row.SnapshotHash ?? "")
                    + " contract=" + (row.ContractHash ?? "")
                    + " provider=" + (row.ProviderId ?? "")
                    + " model=" + (row.ModelId ?? "")
                    + " credential=" + (row.CredentialId?.ToString() ?? "null")
                    + " idem=" + (row.IdempotencyKey ?? "")
                    + " error=" + (row.Error ?? ""));
            report.AppendLine("FFMPEG_A " + winnerTransport.Starts + " FFMPEG_B " + loserTransport.Starts);
            var final = await EpisodeCountsAsync(db, spec.BuildId, idem, ct);
            report.AppendLine("FINAL " + final.Text);
            var a = pair.Single(r => r.Label == "A");
            var b = pair.Single(r => r.Label == "B");
            if (a.Error is not null || b.Error is not null)
                failures.Add(spec.Name + " caller error");
            if (a.ArtifactId is null || a.ArtifactId != b.ArtifactId)
                failures.Add(spec.Name + " artifact mismatch");
            if (a.RequestId != b.RequestId || a.TaskId != b.TaskId || a.SnapshotId != b.SnapshotId || a.AttemptId != b.AttemptId)
                failures.Add(spec.Name + " identity mismatch");
            if (a.SnapshotHash != b.SnapshotHash || a.ContractHash != b.ContractHash || a.ContractHash != contract.ContractHash)
                failures.Add(spec.Name + " contract mismatch");
            if (winnerTransport.Starts != 1 || loserTransport.Starts != 0)
                failures.Add(spec.Name + " ffmpeg count");
            if (final.Requests != 1 || final.Tasks != 1 || final.Snapshots != 1 || final.Attempts != 1 || final.Artifacts != 1)
                failures.Add(spec.Name + " final counts");
            if (final.Current != 1 || final.Visible != 1 || final.Films != 1 || final.FilmShots != 2)
                failures.Add(spec.Name + " binding or film counts");
            if (final.Duration != 10m || !string.Equals(final.Status, "VERIFIED", StringComparison.Ordinal))
                failures.Add(spec.Name + " media " + final.Duration + " " + final.Status);
            if (!string.Equals(final.Sha, final.FileSha, StringComparison.OrdinalIgnoreCase))
                failures.Add(spec.Name + " sha mismatch");
            if (a.CredentialId is not null || b.CredentialId is not null || final.CredentialPins != 0 || final.AiJobs != 0)
                failures.Add(spec.Name + " provider or credential");
            if ((a.IdempotencyKey ?? "").Contains("caller-", StringComparison.Ordinal)
                || a.IdempotencyKey != idem)
                failures.Add(spec.Name + " idempotency key");
            return new CaseOutcome(spec.BuildId, a.ArtifactId, idem);
        }
        finally
        {
            releaseA.TrySetResult();
            releaseB.TrySetResult();
        }
    }

    static async Task<CallerResult> CallAsync(
        string label, string callerKey, string connectionString, string videoRoot,
        IProductionWorkStore store, CountTransport transport, CaseSpec spec,
        decimal sh01Dur, decimal sh02Dur, Func<int> arrive, Func<int> arrived, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, TenantA, transport, store);
        arrive();
        var until = DateTime.UtcNow.AddSeconds(20);
        while (arrived() < 2)
        {
            if (DateTime.UtcNow > until)
                return new CallerResult(label, null, null, null, null, null, null, null, null, null, null, null, "peer did not arrive");
            await Task.Delay(20, ct);
        }
        try
        {
            var slice = await opened.Svc.AssembleEpisodeOsAsync(Command(spec, callerKey, sh01Dur, sh02Dur), ct);
            var snap = await opened.Store.GetSnapshotByTaskAsync(slice.Task.TaskId, ct);
            string? contractHash = null;
            if (snap is not null && ProductionEpisodeAssemblyContract.TryParse(snap.ShotContract, out var frozen))
                contractHash = frozen.ContractHash;
            return new CallerResult(label, slice.Output?.ArtifactId, slice.Request.RequestId, slice.Task.TaskId,
                snap?.SnapshotId, slice.Attempt.AttemptId, snap?.SnapshotHash, contractHash,
                snap?.ProviderId, snap?.ModelId, snap?.CredentialId, slice.Request.IdempotencyKey, null);
        }
        catch (Exception ex)
        {
            var detail = ex.GetType().Name + ": " + One(ex.Message);
            if (ex is PostgresException pg)
                detail += " sqlstate=" + pg.SqlState + " constraint=" + pg.ConstraintName;
            return new CallerResult(label, null, null, null, null, null, null, null, null, null, null, null, detail);
        }
    }

    static async Task SeedInputAsync(
        Opened opened, CaseSpec spec, string shot, Guid artifactId, string fileId, byte[] bytes, decimal duration, CancellationToken ct)
    {
        var scope = new ProductionScope(spec.BuildId, "EP01", "SC01", shot);
        var request = await opened.Os.CreateRequestAsync(scope, ProductionActions.Assemble, "loser-seed", "loser-seed-" + spec.Name + "-" + shot, ct: ct);
        var task = await opened.Os.CreateTaskAsync(request.RequestId, ct);
        await opened.Os.FreezeInputAsync(task.TaskId, new ShotExecutionInput(
            "loser seed", "beat", "visual", "", "", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), "", "",
            DurationSec: duration), "server", "ffmpeg", ct);
        var attempt = await opened.Os.StartAttemptAsync(task.TaskId, "loser-seed", ct);
        var key = opened.Files.Write("os", fileId, bytes);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        await opened.Store.InsertArtifactAsync(new ProductionArtifact(
            artifactId, task.TaskId, attempt.AttemptId, ProductionArtifactRoles.Assembly, "video/mp4",
            bytes.Length, duration, sha, key, "loser-seed-local", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "loser-seed"), ct);
        var now = DateTimeOffset.UtcNow;
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), spec.BuildId, shot, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), spec.BuildId, shot, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
    }

    static ProductionActionApiCommand Command(CaseSpec spec, string callerKey, decimal sh01Dur, decimal sh02Dur) =>
        new(
            new ProductionScope(spec.BuildId, "EP01", "EP01", ProductionEpisodeAssemblyContract.EpisodeShotCode),
            new ShotExecutionInput("loser convergence", "beat", "visual", "", "", "10", "", "", DurationSec: 10m),
            "episode-loser-convergence",
            callerKey,
            AssemblyShots:
            [
                (Sh01, spec.Sh01Artifact, sh01Dur),
                (Sh02, spec.Sh02Artifact, sh02Dur),
            ]);

    static async Task<EpisodeState> EpisodeCountsAsync(IDbConnectionFactory db, Guid buildId, string idem, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var requests = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem",
            new { Build = buildId, Idem = idem });
        var tasks = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_tasks WHERE build_id = @Build AND action = 'ASSEMBLE_EPISODE'",
            new { Build = buildId });
        var snaps = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = buildId });
        var attempts = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = buildId });
        var art = await conn.QuerySingleOrDefaultAsync<ArtRow>("""
            SELECT a.artifact_id AS Id, a.byte_sha256 AS Sha, a.storage_key AS Key,
                   a.duration_sec AS Duration, a.verification_status AS Status
            FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = buildId });
        var current = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'CURRENT:ASSEMBLY' AND unbound_at IS NULL
            """, new { Build = buildId });
        var visible = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'VISIBLE:ASSEMBLY' AND unbound_at IS NULL
            """, new { Build = buildId });
        var films = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_film_assemblies WHERE build_id = @Build", new { Build = buildId });
        var filmShots = await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_film_assembly_shots WHERE build_id = @Build", new { Build = buildId });
        var pins = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
              AND (s.credential_id IS NOT NULL OR s.credential_version_id IS NOT NULL)
            """, new { Build = buildId });
        var ai = await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build
              AND lower(coalesce(a.provider_id,'')) IN ('runway','fal','elevenlabs','google','veo','openai','gemini')
            """, new { Build = buildId });
        string fileSha = "";
        if (art is not null)
        {
            var opened = Open(conn, TenantA);
            var bytes = opened.Read(art.Key);
            if (bytes is not null)
                fileSha = KitVideoIntegrityRules.Sha256Hex(bytes);
        }
        var text = "requests=" + requests + " tasks=" + tasks + " snapshots=" + snaps + " attempts=" + attempts
            + " artifacts=" + (art is null ? 0 : 1)
            + " current=" + current + " visible=" + visible
            + " films=" + films + " film_shots=" + filmShots
            + (art is null ? "" : " artifact=" + art.Id + " sha=" + art.Sha + " key=" + art.Key
                + " duration=" + art.Duration + " status=" + art.Status + " fileSha=" + fileSha)
            + " creds=" + pins + " ai=" + ai;
        return new EpisodeState(text, requests, tasks, snaps, attempts, art is null ? 0 : 1, current, visible, films, filmShots,
            art?.Duration ?? 0, art?.Status ?? "", art?.Sha ?? "", fileSha, pins, ai);
    }

    static FileReader Open(NpgsqlConnection conn, Guid tenant) =>
        new(new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = GuessRoot(conn) }), new HostEnv("")),
            new FixedCaller(tenant)));

    static string GuessRoot(NpgsqlConnection conn) =>
        Path.Combine(FindRepoRoot(), "src", "KitPlatform.Api", "App_Data", "kit-video-i2v");

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "KitPlatform.Api")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return @"E:\KitPlatform";
    }

    static async Task<string> TenantBAsync(string connectionString, string videoRoot, CaseOutcome outcome, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, TenantB, new CountTransport());
        var build = await opened.Content.GetSeriesBuildAsync(outcome.BuildId, ct);
        ProductionArtifact? art = null;
        if (outcome.ArtifactId is Guid id)
            art = await opened.Store.GetArtifactAsync(id, ct);
        string assemble;
        try
        {
            await opened.Svc.AssembleEpisodeOsAsync(new ProductionActionApiCommand(
                new ProductionScope(outcome.BuildId, "EP01", "EP01", ProductionEpisodeAssemblyContract.EpisodeShotCode),
                new ShotExecutionInput("loser convergence", "beat", "visual", "", "", "10", "", "", DurationSec: 10m),
                "tenant-b",
                "tenant-b-not-identity",
                AssemblyShots:
                [
                    (Sh01, Guid.Parse("f2f20001-f2f2-4f2f-8f2f-f2f200000001"), 4m),
                    (Sh02, Guid.Parse("f2f20002-f2f2-4f2f-8f2f-f2f200000002"), 6m),
                ]), ct);
            assemble = "returned";
        }
        catch (Exception ex)
        {
            assemble = ex.GetType().Name + ":" + One(ex.Message);
        }
        var db = new NpgsqlConnectionFactory(connectionString);
        var extra = await CountAsync(db,
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem AND tenant_id <> @Tenant",
            outcome.BuildId, outcome.Idem, ct);
        return "build=" + (build is null ? "null" : "present")
            + " artifact=" + (art is null ? "null" : "present")
            + " assemble=" + assemble
            + " otherTenantRequests=" + extra;
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

    static async Task<long> CountAsync(IDbConnectionFactory db, string sql, Guid buildId, string? idem, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql,
            new { Build = buildId, Tenant = TenantA, Idem = idem }, cancellationToken: ct));
    }

    static async Task ExecBuildAsync(IDbConnectionFactory db, Guid buildId, string series, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT INTO pack_content.series_build (
                id, series_code, episode_code, title, status, shot_count, voice_lines, kf_count, video_count,
                graph_json, created_at, updated_at, tenant_id)
            VALUES (
                @Build, @Series, 'EP01', @Series, 'draft', 2, 0, 0, 0,
                '{}'::jsonb, now(), now(), @Tenant)
            """, new { Build = buildId, Series = series, Tenant = TenantA }, cancellationToken: ct));
    }

    static IProductionWorkStore Gate(IProductionWorkStore inner, Func<CancellationToken, Task> before)
    {
        var proxy = DispatchProxy.Create<IProductionWorkStore, InsertGate>();
        var gate = (InsertGate)(object)proxy;
        gate.Inner = inner;
        gate.Before = before;
        return proxy;
    }

    static Opened Open(string connectionString, string videoRoot, Guid tenant, CountTransport transport, IProductionWorkStore? store = null)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var caller = new FixedCaller(tenant);
        store ??= new ProductionWorkRepository(db, caller);
        var files = new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot }), new HostEnv(videoRoot)),
            caller);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var episode = new FfmpegEpisodeAssemblyExecutionAdapter(transport);
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), officialEpisode: episode);
        return new Opened(svc, store, files, os, new ContentRepository(db, caller));
    }

    static ProductionEpisodeAssemblyInput Input(int order, string shot, Guid id, byte[] bytes, decimal duration, string key) =>
        new(order, shot, ProductionArtifactRoles.Assembly, id, KitVideoIntegrityRules.Sha256Hex(bytes), key, duration, 0, 0);

    static decimal Measured(byte[] bytes) =>
        ProductionTestMp4.TryReadDuration(bytes, out var duration) ? duration : 0m;

    static Dictionary<string, long> FileIndex(string root)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return map;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            map[Path.GetRelativePath(root, path)] = new FileInfo(path).Length;
        return map;
    }

    static string One(string raw) => raw.Replace('\r', ' ').Replace('\n', ' ').Trim();

    sealed record CaseSpec(
        string Name, Guid BuildId, Guid Sh01Artifact, Guid Sh02Artifact, string Sh01File, string Sh02File, string SeriesCode, bool PauseWinner);

    sealed record CaseOutcome(Guid BuildId, Guid? ArtifactId, string Idem);
    sealed record Opened(
        ProductionCreateVideoService Svc, IProductionWorkStore Store, IProductionArtifactFileStore Files,
        ProductionExecutionService Os, ContentRepository Content);
    sealed record CallerResult(
        string Label, Guid? ArtifactId, Guid? RequestId, Guid? TaskId, Guid? SnapshotId, Guid? AttemptId,
        string? SnapshotHash, string? ContractHash, string? ProviderId, string? ModelId, Guid? CredentialId,
        string? IdempotencyKey, string? Error);
    sealed record DbSnapshot(string Counts, string Historical, string NullCounts, string Graph);
    sealed record EpisodeState(
        string Text, long Requests, long Tasks, long Snapshots, long Attempts, long Artifacts,
        long Current, long Visible, long Films, long FilmShots, decimal Duration, string Status,
        string Sha, string FileSha, long CredentialPins, long AiJobs);
    sealed class ArtRow
    {
        public Guid Id { get; set; }
        public string Sha { get; set; } = "";
        public string Key { get; set; } = "";
        public decimal Duration { get; set; }
        public string Status { get; set; } = "";
    }
    sealed class FileReader(IProductionArtifactFileStore files)
    {
        public byte[]? Read(string key) => files.Read(key);
    }

    sealed class CountTransport : IFfmpegMixTransport
    {
        readonly ProcessFfmpegMixTransport _inner = new(new ProductionFfmpegMixAudit());
        readonly Func<Task>? _before;
        int _starts;

        public CountTransport(Func<Task>? before = null) => _before = before;
        public int Starts => _starts;
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

        public async Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(FfmpegEpisodeConcatRequest request, CancellationToken ct = default)
        {
            if (_before is not null)
                await _before();
            Interlocked.Increment(ref _starts);
            return await _inner.ConcatCopyOnceAsync(request, ct);
        }
    }

    public class InsertGate : DispatchProxy
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
                    if (arg is CancellationToken ct)
                        token = ct;
                }
            }
            await Before(token).ConfigureAwait(false);
            var pending = (Task)method.Invoke(Inner, args)!;
            await pending.ConfigureAwait(false);
        }
    }

    sealed class FixedCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class HostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "episode-loser-convergence";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
