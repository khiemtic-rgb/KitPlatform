using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// EPISODE_ASSEMBLY_CONTRACT_V1 — CONCAT_COPY of ordered CURRENT:ASSEMBLY shots.
/// Does not execute FFmpeg or any provider.
/// </summary>
public static class ProductionEpisodeAssemblyContract
{
    public const string Version = "EPISODE_ASSEMBLY_CONTRACT_V1";
    public const string ConcatCopy = "CONCAT_COPY";
    public const string OutputClockAssembly = "ASSEMBLY";
    public const decimal DurationToleranceSec = 0;
    public const string EpisodeShotCode = "EP01";

    public static readonly IReadOnlyList<string> OfficialEp01Order =
        ["EP01-SC01-SH01", "EP01-SC01-SH02"];

    public static bool IsEpisodeScope(ProductionScope scope) =>
        ProductionScopeAnchorRules.IsEp01EpisodeBuild(scope.BuildId, scope.EpisodeCode)
        && string.Equals((scope.ShotCode ?? "").Trim(), EpisodeShotCode, StringComparison.Ordinal);

    public static ProductionScope OfficialEpisodeScope() =>
        new(ProductionScopeAnchorRules.OfficialEp01, "EP01", "EP01", EpisodeShotCode);

    public static string? OrderGate(IReadOnlyList<string> codes, bool fullEpisodeQualityPermitted)
    {
        var two = codes.SequenceEqual(OfficialEp01Order);
        var six = codes.SequenceEqual(ShortProductionQualityPipelineV1.Ep01ShotOrder);
        if (six && !fullEpisodeQualityPermitted)
            return ShortProductionQualityPipelineV1.ShotNotAccepted;
        if (!two && !six)
            return ProductionFailureReasons.FailedIdentity;
        return null;
    }

    public static ProductionEpisodeAssemblyContractV1 Build(
        Guid buildId,
        string episodeCode,
        IReadOnlyList<ProductionEpisodeAssemblyInput> inputs,
        bool fullEpisodeQualityPermitted = false)
    {
        var err = ValidateInputs(buildId, episodeCode, inputs, streamCompatible: true, legacyHint: null, fullEpisodeQualityPermitted);
        if (err is not null)
            throw new InvalidOperationException(err);
        var rows = Abut(inputs);
        var output = rows.Sum(r => r.DurationSec);
        var contract = new ProductionEpisodeAssemblyContractV1(
            Version,
            ConcatCopy,
            OutputClockAssembly,
            buildId,
            (episodeCode ?? "").Trim(),
            rows,
            output,
            DurationToleranceSec,
            "",
            "");
        contract = contract with
        {
            TimelineHash = TimelineHash(contract),
            ContractHash = "",
        };
        return contract with { ContractHash = Hash(contract) };
    }

    public static string? Validate(ProductionEpisodeAssemblyContractV1 contract)
    {
        if (!string.Equals(contract.ContractVersion, Version, StringComparison.Ordinal))
            return ProductionFailureReasons.ContractVersionMismatch;
        if (!string.Equals(contract.CompositionMode, ConcatCopy, StringComparison.Ordinal))
            return ProductionFailureReasons.UnsupportedCompositionMode;
        if (!string.Equals(contract.OutputClock, OutputClockAssembly, StringComparison.Ordinal))
            return ProductionFailureReasons.OutputClockUnresolved;
        if (contract.DurationToleranceSec != DurationToleranceSec)
            return ProductionFailureReasons.DurationMismatch;
        if (contract.OrderedInputs is null || contract.OrderedInputs.Count < 2)
            return ProductionFailureReasons.MissingDependency;
        if (contract.OutputDurationSec <= 0)
            return ProductionFailureReasons.InvalidRange;
        if (ProductionMixContract.Dec(contract.OutputDurationSec)
            != ProductionMixContract.Dec(contract.OrderedInputs.Sum(r => r.DurationSec)))
            return ProductionFailureReasons.DurationMismatch;

        var seenOrder = new HashSet<int>();
        var seenShot = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < contract.OrderedInputs.Count; i++)
        {
            var row = contract.OrderedInputs[i];
            if (row.Order != i || !seenOrder.Add(row.Order))
                return ProductionFailureReasons.InvalidRange;
            if (string.IsNullOrWhiteSpace(row.ShotCode) || !seenShot.Add(row.ShotCode.Trim()))
                return ProductionFailureReasons.FailedIdentity;
            if (row.Role != ProductionArtifactRoles.Assembly)
                return ProductionFailureReasons.ArtifactRoleMismatch;
            if (row.ArtifactId == Guid.Empty)
                return ProductionFailureReasons.FailedIdentity;
            if (ProductionMixContract.NormSha(row.Sha256).Length != 64)
                return ProductionFailureReasons.HashChanged;
            if (string.IsNullOrWhiteSpace(row.StorageKey) || row.StorageKey.Contains("://", StringComparison.Ordinal))
                return ProductionFailureReasons.FailedIdentity;
            if (row.DurationSec <= 0)
                return ProductionFailureReasons.InvalidRange;
            if (i == 0 && row.StartSec != 0)
                return ProductionFailureReasons.InvalidRange;
            if (row.EndSec != row.StartSec + row.DurationSec)
                return ProductionFailureReasons.InvalidRange;
            if (i > 0)
            {
                var prev = contract.OrderedInputs[i - 1];
                if (row.StartSec < prev.EndSec)
                    return ProductionFailureReasons.PictureOverlap;
                if (row.StartSec > prev.EndSec)
                    return ProductionFailureReasons.PictureGap;
                if (row.StartSec != prev.EndSec)
                    return ProductionFailureReasons.AudioGap;
            }
        }

        if (!string.Equals(contract.TimelineHash, TimelineHash(contract), StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.InputSnapshotMismatch;
        if (!string.Equals(contract.ContractHash, Hash(contract with { ContractHash = "" }), StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.InputSnapshotMismatch;
        return null;
    }

    public static string? ValidateInputs(
        Guid buildId,
        string episodeCode,
        IReadOnlyList<ProductionEpisodeAssemblyInput>? inputs,
        bool streamCompatible,
        string? legacyHint,
        bool fullEpisodeQualityPermitted = false)
    {
        var legacy = RejectLegacy(legacyHint);
        if (legacy is not null) return legacy;
        if (!streamCompatible)
            return ProductionFailureReasons.UnsupportedCompositionMode;
        if (inputs is null || inputs.Count < 2)
            return ProductionFailureReasons.MissingDependency;
        if (!ProductionScopeAnchorRules.IsEp01EpisodeBuild(buildId, episodeCode))
            return ProductionFailureReasons.CrossScopeForbidden;

        var seenOrder = new HashSet<int>();
        var seenShot = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < inputs.Count; i++)
        {
            var row = inputs[i];
            if (!seenOrder.Add(row.Order))
                return ProductionFailureReasons.InvalidRange;
            if (row.Order != i)
                return ProductionFailureReasons.InvalidRange;
            if (string.IsNullOrWhiteSpace(row.ShotCode) || !seenShot.Add(row.ShotCode.Trim()))
                return ProductionFailureReasons.FailedIdentity;
            if (row.Role != ProductionArtifactRoles.Assembly)
                return ProductionFailureReasons.ArtifactRoleMismatch;
            if (row.DurationSec <= 0)
                return ProductionFailureReasons.InvalidRange;
            if (row.ArtifactId == Guid.Empty)
                return ProductionFailureReasons.FailedIdentity;
            if (ProductionMixContract.NormSha(row.Sha256).Length != 64)
                return ProductionFailureReasons.HashChanged;
            if (string.IsNullOrWhiteSpace(row.StorageKey) || row.StorageKey.Contains("://", StringComparison.Ordinal))
                return ProductionFailureReasons.FailedIdentity;
        }

        return OrderGate(inputs.Select(r => r.ShotCode.Trim()).ToArray(), fullEpisodeQualityPermitted);
    }

    public static string? Evaluate(
        ProductionScope episodeScope,
        IReadOnlyList<(string ShotCode, Guid ArtifactId)> caller,
        IReadOnlyList<ProductionEpisodeAssemblyResolved> resolved,
        bool streamCompatible = true,
        string? legacyHint = null,
        bool fullEpisodeQualityPermitted = false)
    {
        var legacy = RejectLegacy(legacyHint);
        if (legacy is not null) return legacy;
        if (!IsEpisodeScope(episodeScope))
            return ProductionFailureReasons.CrossScopeForbidden;
        if (caller is null || resolved is null || caller.Count < 2 || resolved.Count < 2)
            return ProductionFailureReasons.MissingDependency;
        if (caller.Count != resolved.Count)
            return ProductionFailureReasons.FailedIdentity;

        var rows = new List<ProductionEpisodeAssemblyInput>();
        for (var i = 0; i < caller.Count; i++)
        {
            var asked = caller[i];
            var live = resolved[i];
            if (!string.Equals(asked.ShotCode.Trim(), live.ShotCode.Trim(), StringComparison.Ordinal))
                return ProductionFailureReasons.CrossShotForbidden;
            if (live.CurrentAssemblyId is null)
                return ProductionFailureReasons.MissingDependency;
            if (asked.ArtifactId != live.CurrentAssemblyId)
                return ProductionFailureReasons.FailedIdentity;
            if (live.Artifact.Role != ProductionArtifactRoles.Assembly)
                return ProductionFailureReasons.ArtifactRoleMismatch;
            if (live.Artifact.VerificationStatus != ProductionArtifactStatuses.Verified)
                return ProductionFailureReasons.FailedVerification;
            if (live.BuildOwner != episodeScope.BuildId)
                return ProductionFailureReasons.CrossScopeForbidden;
            if (!string.Equals(live.ShotOwner, asked.ShotCode.Trim(), StringComparison.Ordinal))
                return ProductionFailureReasons.CrossShotForbidden;
            if (live.Artifact.DurationSec <= 0)
                return ProductionFailureReasons.InvalidRange;
            if (ProductionMixContract.NormSha(live.FileSha) != ProductionMixContract.NormSha(live.Artifact.ByteSha256))
                return ProductionFailureReasons.HashChanged;
            if (!string.Equals((live.FileStorageKey ?? "").Trim(), (live.Artifact.StorageKey ?? "").Trim(), StringComparison.Ordinal))
                return ProductionFailureReasons.FailedIdentity;
            if (live.VisibleAssemblyId is { } vis && vis != live.CurrentAssemblyId)
                return ProductionFailureReasons.FailedIdentity;
            rows.Add(new ProductionEpisodeAssemblyInput(
                i, asked.ShotCode.Trim(), ProductionArtifactRoles.Assembly,
                live.Artifact.ArtifactId, live.Artifact.ByteSha256, live.Artifact.StorageKey ?? "",
                live.Artifact.DurationSec, 0, 0));
        }

        var shaped = Abut(rows);
        return ValidateInputs(
            episodeScope.BuildId, episodeScope.EpisodeCode, shaped, streamCompatible, null, fullEpisodeQualityPermitted);
    }

    public static string? MatchesFrozen(
        ProductionEpisodeAssemblyContractV1 frozen,
        IReadOnlyList<ProductionEpisodeAssemblyInput> live)
    {
        var err = Validate(frozen);
        if (err is not null) return err;
        if (live.Count != frozen.OrderedInputs.Count)
            return ProductionFailureReasons.InputSnapshotMismatch;
        for (var i = 0; i < frozen.OrderedInputs.Count; i++)
        {
            var a = frozen.OrderedInputs[i];
            var b = live[i];
            if (a.ArtifactId != b.ArtifactId || a.Order != b.Order
                || !string.Equals(a.ShotCode, b.ShotCode, StringComparison.Ordinal))
                return ProductionFailureReasons.InputSnapshotMismatch;
            if (ProductionMixContract.NormSha(a.Sha256) != ProductionMixContract.NormSha(b.Sha256))
                return ProductionFailureReasons.HashChanged;
            if (!string.Equals(a.StorageKey, b.StorageKey, StringComparison.Ordinal))
                return ProductionFailureReasons.FailedIdentity;
            if (ProductionMixContract.Dec(a.DurationSec) != ProductionMixContract.Dec(b.DurationSec))
                return ProductionFailureReasons.DurationMismatch;
        }
        return null;
    }

    public static string? RejectLegacy(string? hint)
    {
        var raw = (hint ?? "").Trim();
        if (raw.Length == 0) return null;
        if (raw.Contains("graph_json", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("series_build", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("/content/series/assemble", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("ContentSeriesAssembleService", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("RemuxOnceAsync", StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.HiddenFallbackForbidden;
        return null;
    }

    public static string Token(ProductionEpisodeAssemblyContractV1 contract)
    {
        var err = Validate(contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        var rows = new List<string>
        {
            contract.ContractVersion,
            contract.CompositionMode,
            contract.OutputClock,
            contract.BuildId.ToString("N"),
            contract.EpisodeCode,
            contract.OrderedInputs.Count.ToString(CultureInfo.InvariantCulture),
            ProductionMixContract.Dec(contract.OutputDurationSec),
            ProductionMixContract.Dec(contract.DurationToleranceSec),
            contract.TimelineHash,
            contract.ContractHash,
        };
        foreach (var s in contract.OrderedInputs)
        {
            rows.Add(string.Join('|',
                s.Order.ToString(CultureInfo.InvariantCulture),
                s.ShotCode,
                s.Role,
                s.ArtifactId.ToString("N"),
                ProductionMixContract.NormSha(s.Sha256),
                s.StorageKey,
                ProductionMixContract.Dec(s.DurationSec),
                ProductionMixContract.Dec(s.StartSec),
                ProductionMixContract.Dec(s.EndSec)));
        }
        return string.Join('|', rows);
    }

    public static bool TryParse(string? raw, out ProductionEpisodeAssemblyContractV1 contract)
    {
        contract = new ProductionEpisodeAssemblyContractV1(
            "", "", "", Guid.Empty, "", [], 0, 0, "", "");
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 19)
            return false;
        if (!string.Equals(parts[0], Version, StringComparison.Ordinal))
            return false;
        if (!Guid.TryParseExact(parts[3], "N", out var buildId))
            return false;
        if (!int.TryParse(parts[5], NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 2)
            return false;
        if (!ProductionMixContract.TryDec(parts[6], out var output) || output <= 0)
            return false;
        if (!ProductionMixContract.TryDec(parts[7], out var tol))
            return false;
        var rows = new List<ProductionEpisodeAssemblyInput>();
        var cursor = 10;
        for (var i = 0; i < n; i++)
        {
            if (cursor + 8 >= parts.Length) return false;
            if (!int.TryParse(parts[cursor], NumberStyles.None, CultureInfo.InvariantCulture, out var order))
                return false;
            if (!Guid.TryParseExact(parts[cursor + 3], "N", out var id))
                return false;
            if (!ProductionMixContract.TryDec(parts[cursor + 6], out var dur)
                || !ProductionMixContract.TryDec(parts[cursor + 7], out var start)
                || !ProductionMixContract.TryDec(parts[cursor + 8], out var end))
                return false;
            rows.Add(new ProductionEpisodeAssemblyInput(
                order, parts[cursor + 1], parts[cursor + 2], id,
                parts[cursor + 4], parts[cursor + 5], dur, start, end));
            cursor += 9;
        }
        contract = new ProductionEpisodeAssemblyContractV1(
            parts[0], parts[1], parts[2], buildId, parts[4], rows, output, tol, parts[8], parts[9]);
        return Validate(contract) is null;
    }

    public static string Identity(ProductionScope scope, ProductionEpisodeAssemblyContractV1 contract) =>
        string.Join('|',
            ProductionActions.AssembleEpisode,
            contract.ContractVersion,
            scope.BuildId.ToString("N"),
            (scope.EpisodeCode ?? "").Trim(),
            contract.ContractHash);

    public static string Hash(ProductionEpisodeAssemblyContractV1 contract)
    {
        var canonical = string.Join('|',
            contract.ContractVersion,
            contract.CompositionMode,
            contract.OutputClock,
            contract.BuildId.ToString("N"),
            contract.EpisodeCode,
            ProductionMixContract.Dec(contract.OutputDurationSec),
            ProductionMixContract.Dec(contract.DurationToleranceSec),
            string.Join(';', contract.OrderedInputs.Select(RowCanon)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static string TimelineHash(ProductionEpisodeAssemblyContractV1 contract)
    {
        var canonical = string.Join(';', contract.OrderedInputs.Select(s =>
            s.Order + "," + s.ShotCode + "," + ProductionMixContract.Dec(s.StartSec) + ","
            + ProductionMixContract.Dec(s.EndSec) + "," + ProductionMixContract.Dec(s.DurationSec)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static IReadOnlyList<ProductionEpisodeAssemblyInput> Abut(
        IReadOnlyList<ProductionEpisodeAssemblyInput> inputs)
    {
        var rows = new List<ProductionEpisodeAssemblyInput>();
        decimal start = 0;
        foreach (var s in inputs.OrderBy(r => r.Order))
        {
            rows.Add(s with { StartSec = start, EndSec = start + s.DurationSec });
            start += s.DurationSec;
        }
        return rows;
    }

    static string RowCanon(ProductionEpisodeAssemblyInput s) =>
        string.Join(',',
            s.Order.ToString(CultureInfo.InvariantCulture),
            s.ShotCode,
            s.Role,
            s.ArtifactId.ToString("N"),
            ProductionMixContract.NormSha(s.Sha256),
            s.StorageKey,
            ProductionMixContract.Dec(s.DurationSec),
            ProductionMixContract.Dec(s.StartSec),
            ProductionMixContract.Dec(s.EndSec));
}

public sealed record ProductionEpisodeAssemblyInput(
    int Order,
    string ShotCode,
    string Role,
    Guid ArtifactId,
    string Sha256,
    string StorageKey,
    decimal DurationSec,
    decimal StartSec,
    decimal EndSec);

public sealed record ProductionEpisodeAssemblyResolved(
    string ShotCode,
    ProductionArtifact Artifact,
    Guid? CurrentAssemblyId,
    Guid? VisibleAssemblyId,
    string FileSha,
    string FileStorageKey,
    Guid BuildOwner,
    string ShotOwner);

public sealed record ProductionEpisodeAssemblyContractV1(
    string ContractVersion,
    string CompositionMode,
    string OutputClock,
    Guid BuildId,
    string EpisodeCode,
    IReadOnlyList<ProductionEpisodeAssemblyInput> OrderedInputs,
    decimal OutputDurationSec,
    decimal DurationToleranceSec,
    string TimelineHash,
    string ContractHash);
