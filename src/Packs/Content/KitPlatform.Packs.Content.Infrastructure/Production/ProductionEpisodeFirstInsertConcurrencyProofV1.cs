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
/// PRODUCTION-EPISODE-FIRST-INSERT-CONCURRENCY-PROOF-01.
/// Races two official AssembleEpisodeOsAsync callers on a new tenant-owned identity.
/// Does not change freeze, idempotency, or episode contract behavior.
/// </summary>
public static class ProductionEpisodeFirstInsertConcurrencyProofV1
{
    public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    public static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
    public static readonly Guid BuildId = Guid.Parse("e1e1e1e1-e1e1-4e1e-8e1e-e1e1e1e1e101");
    public static readonly Guid Sh01ArtifactId = Guid.Parse("e1e10001-e1e1-4e1e-8e1e-e1e100000001");
    public static readonly Guid Sh02ArtifactId = Guid.Parse("e1e10002-e1e1-4e1e-8e1e-e1e100000002");
    const string Sh01 = "EP01-SC01-SH01";
    const string Sh02 = "EP01-SC01-SH02";
    const string Sh01SourceKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1/os/6d4058f21f324ff7bf4aaa4cf91407cd";
    const string Sh02SourceKey = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1/os/309602be95d04c7ca46b752402b28da7";

    public static async Task<string> RunAsync(string connectionString, string videoRoot, CancellationToken ct = default)
    {
        var report = new StringBuilder();
        var db = new NpgsqlConnectionFactory(connectionString);
        var before = await SnapshotAsync(db, ct);
        report.AppendLine("BEFORE " + before.Counts);
        report.AppendLine("HIST_BEFORE " + before.Historical);
        var filesBefore = FileIndex(videoRoot);

        var buildExists = await ScalarAsync(db, "SELECT count(*) FROM pack_content.series_build WHERE id = @Build", ct) != 0;
        if (!buildExists)
        {
            await ExecAsync(db, """
                INSERT INTO pack_content.series_build (
                    id, series_code, episode_code, title, status, shot_count, voice_lines, kf_count, video_count,
                    graph_json, created_at, updated_at, tenant_id)
                VALUES (
                    @Build, 'RACE_EP01_FIRST_INSERT', 'EP01', 'first-insert race', 'draft', 2, 0, 0, 0,
                    '{}'::jsonb, now(), now(), @Tenant)
                """, ct);
        }

        var seed = Open(connectionString, videoRoot, TenantA, new CountingTransport());
        var sh01Bytes = seed.Files.Read(Sh01SourceKey) ?? throw new InvalidOperationException("SH01 source missing");
        var sh02Bytes = seed.Files.Read(Sh02SourceKey) ?? throw new InvalidOperationException("SH02 source missing");
        var sh01Dur = Measured(sh01Bytes);
        var sh02Dur = Measured(sh02Bytes);
        if (!buildExists)
        {
            await SeedInputAsync(seed, Sh01, Sh01ArtifactId, "e1e1sh01raceinput", sh01Bytes, sh01Dur, ct);
            await SeedInputAsync(seed, Sh02, Sh02ArtifactId, "e1e1sh02raceinput", sh02Bytes, sh02Dur, ct);
        }

        var contract = ProductionEpisodeAssemblyContract.Build(
            BuildId, "EP01",
            [
                Input(0, Sh01, Sh01ArtifactId, sh01Bytes, sh01Dur, TenantPrefix() + "/os/e1e1sh01raceinput"),
                Input(1, Sh02, Sh02ArtifactId, sh02Bytes, sh02Dur, TenantPrefix() + "/os/e1e1sh02raceinput"),
            ]);
        var idem = string.Join(':',
            ProductionActions.AssembleEpisode, contract.ContractVersion, BuildId.ToString("N"), "EP01", contract.ContractHash);
        var absent = await AbsenceAsync(db, idem, ct);
        report.AppendLine("IDENTITY build=" + BuildId + " episode=EP01 tenant=" + TenantA);
        report.AppendLine("CONTRACT " + contract.ContractVersion + " hash=" + contract.ContractHash + " duration=" + contract.OutputDurationSec);
        report.AppendLine("IDEMPOTENCY " + idem);
        report.AppendLine("ABSENT " + absent);
        if (absent != "requests=0 tasks=0 snapshots=0 attempts=0 artifacts=0 current=0 visible=0 films=0")
            return report + "VERDICT FAIL" + Environment.NewLine + "identity was not absent";

        CountingTransport.Reset();
        var arrived = 0;
        var pair = await Task.WhenAll(
            CallAsync("A", "caller-a-not-identity", connectionString, videoRoot, () => Volatile.Read(ref arrived), () => Interlocked.Increment(ref arrived), sh01Dur, sh02Dur, ct),
            CallAsync("B", "caller-b-not-identity", connectionString, videoRoot, () => Volatile.Read(ref arrived), () => Interlocked.Increment(ref arrived), sh01Dur, sh02Dur, ct));
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
                + " error=" + (row.Error ?? ""));

        var final = await FinalAsync(db, idem, ct);
        report.AppendLine("FINAL " + final.Text);
        report.AppendLine("FFMPEG_CONCAT_ENTRIES " + CountingTransport.Starts);

        var tenantB = await TenantBAsync(connectionString, videoRoot, final.ArtifactId, ct);
        report.AppendLine("TENANT_B " + tenantB);

        var after = await SnapshotAsync(db, ct);
        report.AppendLine("AFTER " + after.Counts);
        report.AppendLine("HIST_AFTER " + after.Historical);
        var filesAfter = FileIndex(videoRoot);
        var changed = filesBefore.Count(pairFile =>
            !filesAfter.TryGetValue(pairFile.Key, out var later) || later != pairFile.Value);
        var added = filesAfter.Keys.Except(filesBefore.Keys).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        report.AppendLine("FILES_CHANGED " + changed);
        report.AppendLine("FILES_ADDED " + added.Length);
        foreach (var path in added)
            report.AppendLine("NEW_FILE " + path);

        report.AppendLine("FAILURE_RACE NOT_PROVEN");
        return report.ToString();
    }

    static async Task<CallerResult> CallAsync(
        string label, string callerKey, string connectionString, string videoRoot,
        Func<int> arrived, Func<int> arrive, decimal sh01Dur, decimal sh02Dur, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, TenantA, new CountingTransport());
        arrive();
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "ep-first-insert-proof.log"),
            DateTimeOffset.UtcNow.ToString("o") + " " + label + " arrived" + Environment.NewLine);
        var until = DateTime.UtcNow.AddSeconds(15);
        while (arrived() < 2)
        {
            if (DateTime.UtcNow > until)
                return new CallerResult(label, null, null, null, null, null, null, null, null, null, null, "peer did not arrive");
            await Task.Delay(20, ct);
        }
        try
        {
            var slice = await opened.Svc.AssembleEpisodeOsAsync(Command(callerKey, sh01Dur, sh02Dur), ct);
            var snap = await opened.Store.GetSnapshotByTaskAsync(slice.Task.TaskId, ct);
            string? contractHash = null;
            if (snap is not null && ProductionEpisodeAssemblyContract.TryParse(snap.ShotContract, out var frozen))
                contractHash = frozen.ContractHash;
            return new CallerResult(label, slice.Output?.ArtifactId, slice.Request.RequestId, slice.Task.TaskId,
                snap?.SnapshotId, slice.Attempt.AttemptId, snap?.SnapshotHash, contractHash,
                snap?.ProviderId, snap?.ModelId, snap?.CredentialId, null);
        }
        catch (Exception ex)
        {
            var detail = ex.GetType().Name + ": " + One(ex.Message);
            if (ex is PostgresException pg)
                detail += " sqlstate=" + pg.SqlState + " constraint=" + pg.ConstraintName;
            return new CallerResult(label, null, null, null, null, null, null, null, null, null, null, detail);
        }
    }

    static async Task SeedInputAsync(
        Opened opened, string shot, Guid artifactId, string fileId, byte[] bytes, decimal duration, CancellationToken ct)
    {
        var scope = new ProductionScope(BuildId, "EP01", "SC01", shot);
        var request = await opened.Os.CreateRequestAsync(scope, ProductionActions.Assemble, "race-seed", "race-seed-" + shot, ct: ct);
        var task = await opened.Os.CreateTaskAsync(request.RequestId, ct);
        await opened.Os.FreezeInputAsync(task.TaskId, new ShotExecutionInput(
            "race seed", "beat", "visual", "", "", duration.ToString(System.Globalization.CultureInfo.InvariantCulture), "", "",
            DurationSec: duration), "server", "ffmpeg", ct);
        var attempt = await opened.Os.StartAttemptAsync(task.TaskId, "race-seed", ct);
        var key = opened.Files.Write("os", fileId, bytes);
        var sha = KitVideoIntegrityRules.Sha256Hex(bytes);
        await opened.Store.InsertArtifactAsync(new ProductionArtifact(
            artifactId, task.TaskId, attempt.AttemptId, ProductionArtifactRoles.Assembly, "video/mp4",
            bytes.Length, duration, sha, key, "race-seed-local", DateTimeOffset.UtcNow,
            ProductionArtifactStatuses.Verified, "race-seed"), ct);
        var now = DateTimeOffset.UtcNow;
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), BuildId, shot, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
        await opened.Store.InsertBindingAsync(new ProductionBinding(
            Guid.NewGuid(), BuildId, shot, ProductionBindingRoles.VisibleFor(ProductionArtifactRoles.Assembly),
            artifactId, task.TaskId, now, null), ct);
    }

    static ProductionActionApiCommand Command(string callerKey, decimal sh01Dur, decimal sh02Dur) =>
        new(
            new ProductionScope(BuildId, "EP01", "EP01", ProductionEpisodeAssemblyContract.EpisodeShotCode),
            new ShotExecutionInput("first-insert race", "beat", "visual", "", "", "10", "", "", DurationSec: 10m),
            "episode-first-insert",
            callerKey,
            AssemblyShots:
            [
                (Sh01, Sh01ArtifactId, sh01Dur),
                (Sh02, Sh02ArtifactId, sh02Dur),
            ]);

    static async Task<string> AbsenceAsync(IDbConnectionFactory db, string idem, CancellationToken ct)
    {
        var requests = await ScalarAsync(db, "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem", ct, idem);
        var tasks = await ScalarAsync(db, "SELECT count(*) FROM pack_content.production_tasks WHERE build_id = @Build AND action = 'ASSEMBLE_EPISODE'", ct);
        var snaps = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, ct);
        var attempts = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, ct);
        var artifacts = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, ct);
        var current = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'CURRENT:ASSEMBLY' AND unbound_at IS NULL
            """, ct);
        var visible = await ScalarAsync(db, """
            SELECT count(*) FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01' AND role = 'VISIBLE:ASSEMBLY' AND unbound_at IS NULL
            """, ct);
        var films = await ScalarAsync(db, "SELECT count(*) FROM pack_content.production_film_assemblies WHERE build_id = @Build", ct);
        return "requests=" + requests + " tasks=" + tasks + " snapshots=" + snaps + " attempts=" + attempts
            + " artifacts=" + artifacts + " current=" + current + " visible=" + visible + " films=" + films;
    }

    static async Task<FinalState> FinalAsync(IDbConnectionFactory db, string idem, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        var text = new StringBuilder();
        text.Append("requests=" + await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_requests WHERE build_id = @Build AND idempotency_key = @Idem",
            new { Build = BuildId, Idem = idem }));
        text.Append(" tasks=" + await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_tasks WHERE build_id = @Build AND action = 'ASSEMBLE_EPISODE'",
            new { Build = BuildId }));
        text.Append(" snapshots=" + await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId }));
        text.Append(" attempts=" + await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId }));
        var arts = (await conn.QueryAsync<(Guid Id, string Sha, string Key, decimal Duration, string Status)>(
            """
            SELECT a.artifact_id, a.byte_sha256, a.storage_key, a.duration_sec, a.verification_status
            FROM pack_content.production_artifacts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
            """, new { Build = BuildId })).ToArray();
        text.Append(" artifacts=" + arts.Length);
        Guid? artifactId = arts.Length == 1 ? arts[0].Id : null;
        if (arts.Length == 1)
        {
            text.Append(" artifact=" + arts[0].Id);
            text.Append(" sha=" + arts[0].Sha);
            text.Append(" key=" + arts[0].Key);
            text.Append(" duration=" + arts[0].Duration);
            text.Append(" status=" + arts[0].Status);
        }
        var bindings = (await conn.QueryAsync<(string Role, Guid ArtifactId, bool Active)>(
            """
            SELECT role, artifact_id, unbound_at IS NULL
            FROM pack_content.production_bindings
            WHERE build_id = @Build AND shot_code = 'EP01'
            """, new { Build = BuildId })).ToArray();
        foreach (var role in new[] { "CURRENT:ASSEMBLY", "VISIBLE:ASSEMBLY", "VISIBLE" })
        {
            var active = bindings.Count(b => b.Role == role && b.Active);
            text.Append(" " + role + "=" + active);
        }
        text.Append(" films=" + await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_film_assemblies WHERE build_id = @Build", new { Build = BuildId }));
        text.Append(" film_shots=" + await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM pack_content.production_film_assembly_shots WHERE build_id = @Build", new { Build = BuildId }));
        text.Append(" creds=" + await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_task_input_snapshots s
            JOIN pack_content.production_tasks t ON t.task_id = s.task_id
            WHERE t.build_id = @Build AND t.action = 'ASSEMBLE_EPISODE'
              AND (s.credential_id IS NOT NULL OR s.credential_version_id IS NOT NULL)
            """, new { Build = BuildId }));
        text.Append(" ai=" + await conn.ExecuteScalarAsync<long>("""
            SELECT count(*) FROM pack_content.production_attempts a
            JOIN pack_content.production_tasks t ON t.task_id = a.task_id
            WHERE t.build_id = @Build
              AND lower(coalesce(a.provider_id,'')) IN ('runway','fal','elevenlabs','google','veo','openai','gemini')
            """, new { Build = BuildId }));
        return new FinalState(text.ToString(), artifactId);
    }

    static async Task<string> TenantBAsync(string connectionString, string videoRoot, Guid? artifactId, CancellationToken ct)
    {
        var opened = Open(connectionString, videoRoot, TenantB, new CountingTransport());
        var build = await opened.Content.GetSeriesBuildAsync(BuildId, ct);
        ProductionArtifact? art = null;
        if (artifactId is Guid id)
            art = await opened.Store.GetArtifactAsync(id, ct);
        string assemble;
        try
        {
            await opened.Svc.AssembleEpisodeOsAsync(Command("tenant-b", 4m, 6m), ct);
            assemble = "returned";
        }
        catch (Exception ex)
        {
            assemble = ex.GetType().Name + ":" + One(ex.Message);
        }
        return "build=" + (build is null ? "null" : "present")
            + " artifact=" + (art is null ? "null" : "present")
            + " assemble=" + assemble
            + " concat=" + 0;
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
        return new DbSnapshot(counts, historical ?? "");
    }

    static async Task<long> ScalarAsync(IDbConnectionFactory db, string sql, CancellationToken ct, string? idem = null)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        return await conn.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { Build = BuildId, Tenant = TenantA, Idem = idem }, cancellationToken: ct));
    }

    static async Task ExecAsync(IDbConnectionFactory db, string sql, CancellationToken ct)
    {
        await using var conn = await db.CreateOpenConnectionAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(sql, new { Build = BuildId, Tenant = TenantA }, cancellationToken: ct));
    }

    static Opened Open(string connectionString, string videoRoot, Guid tenant, CountingTransport counter)
    {
        var db = new NpgsqlConnectionFactory(connectionString);
        var caller = new FixedCaller(tenant);
        var store = new ProductionWorkRepository(db, caller);
        var files = new ProductionArtifactFileStore(
            new KitVideoVideoStore(Options.Create(new ContentOptions { KitVideoVideoRoot = videoRoot }), new HostEnv(videoRoot)),
            caller);
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var episode = new FfmpegEpisodeAssemblyExecutionAdapter(counter);
        var svc = new ProductionCreateVideoService(
            os, store, files, new FixtureMotionAdapter(), officialEpisode: episode);
        return new Opened(svc, store, files, os, new ContentRepository(db, caller));
    }

    static ProductionEpisodeAssemblyInput Input(int order, string shot, Guid id, byte[] bytes, decimal duration, string key) =>
        new(order, shot, ProductionArtifactRoles.Assembly, id, KitVideoIntegrityRules.Sha256Hex(bytes), key, duration, 0, 0);

    static decimal Measured(byte[] bytes) =>
        ProductionTestMp4.TryReadDuration(bytes, out var duration) ? duration : 0m;

    static string TenantPrefix() => TenantA.ToString("N");

    static Dictionary<string, long> FileIndex(string root)
    {
        var map = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(root)) return map;
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            map[Path.GetRelativePath(root, path)] = new FileInfo(path).Length;
        return map;
    }

    static string One(string raw) => raw.Replace('\r', ' ').Replace('\n', ' ').Trim();

    sealed record Opened(
        ProductionCreateVideoService Svc,
        ProductionWorkRepository Store,
        IProductionArtifactFileStore Files,
        ProductionExecutionService Os,
        ContentRepository Content);

    sealed record CallerResult(
        string Label, Guid? ArtifactId, Guid? RequestId, Guid? TaskId, Guid? SnapshotId, Guid? AttemptId,
        string? SnapshotHash, string? ContractHash, string? ProviderId, string? ModelId, Guid? CredentialId, string? Error);

    sealed record DbSnapshot(string Counts, string Historical);
    sealed record FinalState(string Text, Guid? ArtifactId);

    sealed class CountingTransport : IFfmpegMixTransport
    {
        static int _starts;
        readonly ProcessFfmpegMixTransport _inner = new(new ProductionFfmpegMixAudit());

        public static int Starts => _starts;
        public static void Reset() => _starts = 0;
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

        public Task<FfmpegMixTransportResult> ConcatCopyOnceAsync(FfmpegEpisodeConcatRequest request, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _starts);
            return _inner.ConcatCopyOnceAsync(request, ct);
        }
    }

    sealed class FixedCaller(Guid id) : IProductionCallerTenant
    {
        public Guid RequireTenant() => ProductionTenantBoundary.Require(id);
    }

    sealed class HostEnv(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "episode-first-insert";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
