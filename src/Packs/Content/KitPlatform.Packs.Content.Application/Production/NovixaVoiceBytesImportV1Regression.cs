using System.Buffers.Binary;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

public static class NovixaVoiceBytesImportV1Regression
{
    public const string SuiteId = "NOVIXA_VOICE_BYTES_IMPORT_V1";

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
        Ok(NovixaVoiceBytesGateThrows(ProductionTenantBoundary.Forbidden, ProductionScopeAnchorRules.OfficialEp01, "FAMIXA", "{\"id\":\"EP01-SC01-SH02\"}", "EP01-SC01-SH02"),
            "famixa pilot build is refused");
        Ok(NovixaVoiceBytesGateThrows(ProductionTenantBoundary.Forbidden, Guid.Parse("30ba4061-a443-4c0d-85aa-1d13ff4fdedb"), "TENANT_EP01", "{\"id\":\"EP01-SC01-SH01\"}", "EP01-SC01-SH01"),
            "tenant episode build is refused");
        Ok(NovixaVoiceBytesGateThrows(ProductionFailureReasons.FailedIdentity, Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"), "NOVIXA_S001", "{\"id\":\"EP01-SC01-SH02\"}", "EP01-SC01-SH01"),
            "shot missing from the novixa graph is refused");
        Ok(!NovixaVoiceBytesGateThrows(ProductionTenantBoundary.Forbidden, Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"), "NOVIXA_S001", "{\"id\":\"EP01-SC01-SH01\"}", "EP01-SC01-SH01"),
            "novixa build that lists the shot is allowed");

        var mpeg = MpegFrames(40);
        var wav = Wav(sampleRate: 8000, samples: 8000);
        var mpegMeasured = ProductionVoicePhysical.TryMeasureDuration(mpeg, "audio/mpeg", out var mpegDuration);
        Ok(mpegMeasured && mpegDuration == Math.Round(40m * 1152m / 44100m, 3, MidpointRounding.AwayFromZero),
            "mpeg duration is measured");
        Ok(ProductionVoicePhysical.TryMeasureDuration(wav, "audio/wav", out var wavDuration) && wavDuration == 1.000m, "wav duration is measured");
        Ok(!ProductionVoicePhysical.TryMeasureDuration(new byte[64], "audio/mpeg", out _), "non-audio is refused");
        Ok(!ProductionVoicePhysical.TryMeasureDuration(mpeg, "audio/wav", out _), "mime that disagrees with the bytes is refused");

        var tenant = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var caller = new SwitchTenant { Id = tenant };
        var store = new InMemoryProductionWorkStore(caller);
        var files = new InMemoryProductionArtifactFileStore(caller);
        var voice = new CountingVoice();
        var build = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        store.RegisterBuild(build, tenant);
        var shots = new Shots(build, "EP01-SC01-SH01");
        var os = new ProductionExecutionService(store, new FixtureMotionAdapter());
        var svc = new ProductionCreateVideoService(os, store, files, new FixtureMotionAdapter(), voice: voice, targetShots: shots);
        var scope = new ProductionScope(build, "EP01", "SC01", "EP01-SC01-SH01");
        var art = await svc.RegisterVoiceBytesAsync(
            new RegisterVoiceBytesCommand(scope, mpeg, "audio/mpeg", "Nhà thuốc vẫn bán ra hàng ngày không có nghĩa là đang quản lý tốt.", "operator"),
            default);
        var sha = KitVideoIntegrityRules.Sha256Hex(mpeg);
        Ok(art.Role == ProductionArtifactRoles.Voice, "role is voice");
        Ok(art.DurationSec == mpegDuration, "artifact duration matches the measured audio");
        Ok(string.Equals(art.ByteSha256, sha, StringComparison.OrdinalIgnoreCase), "artifact hash matches the received bytes");
        var stored = files.Read(art.StorageKey);
        Ok(stored is not null && KitVideoIntegrityRules.Sha256Hex(stored) == sha, "stored bytes match the hash");
        Ok(art.StorageKey.StartsWith(ProductionTenantBoundary.Prefix(tenant) + "/", StringComparison.OrdinalIgnoreCase), "storage stays on the caller tenant");
        var current = await store.GetActiveBindingAsync(build, scope.ShotCode, ProductionBindingRoles.CurrentFor(ProductionArtifactRoles.Voice));
        Ok(current is not null && current.ArtifactId == art.ArtifactId, "CURRENT:VOICE is bound");
        var accepted = await store.GetActiveBindingAsync(build, scope.ShotCode, ProductionBindingRoles.AcceptedFor(ProductionArtifactRoles.Voice));
        Ok(accepted is null, "ACCEPTED:VOICE is not created");
        Ok(voice.Calls == 0, "fixture and paid voice adapters are not called");
        var again = await svc.RegisterVoiceBytesAsync(
            new RegisterVoiceBytesCommand(scope, mpeg, "audio/mpeg", "same line", "operator"), default);
        Ok(again.ArtifactId == art.ArtifactId, "the same bytes return the same artifact");
        Ok(await Throws(ProductionFailureReasons.FailedIdentity, () => svc.RegisterVoiceBytesAsync(
            new RegisterVoiceBytesCommand(scope, new byte[64], "audio/mpeg", "broken", "operator"), default)),
            "undecodable audio is refused");
        Ok(await Throws(ProductionTenantBoundary.Forbidden, () => svc.RegisterVoiceBytesAsync(
            new RegisterVoiceBytesCommand(scope with { BuildId = ProductionScopeAnchorRules.OfficialEp01 }, wav, "audio/wav", "line", "operator"), default)),
            "the official film build is refused");
        Ok(voice.Calls == 0, "a refused import still does not call a voice adapter");
    }

    static bool NovixaVoiceBytesGateThrows(string message, Guid build, string series, string graph, string shot)
    {
        try
        {
            NovixaVoiceBytesGate.Require(build, series, graph, shot);
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == message;
        }
    }

    static async Task<bool> Throws(string message, Func<Task> work)
    {
        try
        {
            await work();
            return false;
        }
        catch (InvalidOperationException ex)
        {
            return ex.Message == message;
        }
    }

    static byte[] MpegFrames(int frames)
    {
        var frameSize = 144 * 128000 / 44100;
        var frame = new byte[frameSize];
        frame[0] = 0xFF;
        frame[1] = 0xFB;
        frame[2] = 0x90;
        var all = new byte[frameSize * frames];
        for (var i = 0; i < frames; i++)
            Buffer.BlockCopy(frame, 0, all, i * frameSize, frameSize);
        return all;
    }

    static byte[] Wav(int sampleRate, int samples)
    {
        var data = new byte[samples * 2];
        using var ms = new MemoryStream();
        void U32(uint value)
        {
            Span<byte> buf = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
            ms.Write(buf);
        }
        void U16(ushort value)
        {
            Span<byte> buf = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(buf, value);
            ms.Write(buf);
        }
        ms.Write(Encoding.ASCII.GetBytes("RIFF"));
        U32((uint)(36 + data.Length));
        ms.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        U32(16);
        U16(1);
        U16(1);
        U32((uint)sampleRate);
        U32((uint)(sampleRate * 2));
        U16(2);
        U16(16);
        ms.Write(Encoding.ASCII.GetBytes("data"));
        U32((uint)data.Length);
        ms.Write(data);
        return ms.ToArray();
    }

    sealed class SwitchTenant : IProductionCallerTenant
    {
        public Guid Id { get; set; }
        public Guid RequireTenant() => ProductionTenantBoundary.Require(Id);
    }

    sealed class Shots(Guid buildId, params string[] codes) : IProductionTargetShotCheck
    {
        public Task RequireAsync(Guid build, string shotCode, CancellationToken ct = default)
        {
            if (build != buildId)
                throw new InvalidOperationException(ProductionTenantBoundary.Forbidden);
            if (!codes.Contains(shotCode, StringComparer.Ordinal))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            return Task.CompletedTask;
        }
    }

    sealed class CountingVoice : IVoiceExecutionAdapter
    {
        public int Calls;
        public string ProviderId => "fixture";
        public string ModelId => "fixture-voice-v1";
        public bool IsPaid => false;
        public bool RequiresNetwork => false;
        public Task<ProductionMediaResult> ExecuteFrozenAsync(ProductionTaskInputSnapshot snapshot, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ProductionMediaResult("no", [], "audio/wav", 0));
        }
    }
}
