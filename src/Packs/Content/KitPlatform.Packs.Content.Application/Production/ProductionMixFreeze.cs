using System.Globalization;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// MIX freeze. V0: lipsyncId|lipsyncSha (SH01 remux).
/// V1: MIX_CONTRACT_V1 COMPOSE_SEGMENTS token. No schema change.
/// </summary>
public static class ProductionMixFreeze
{
    public const string V1Prefix = "V1";

    public static string Token(Guid lipsyncArtifactId, string lipsyncSha256) =>
        string.Join('|', lipsyncArtifactId.ToString("N"), (lipsyncSha256 ?? "").Trim().ToLowerInvariant());

    public static bool TryParse(string? raw, out Guid lipsyncId, out string sha)
    {
        lipsyncId = Guid.Empty;
        sha = "";
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 2) return false;
        if (string.Equals(parts[0].Trim(), V1Prefix, StringComparison.Ordinal))
            return false;
        sha = parts[1].Trim().ToLowerInvariant();
        return Guid.TryParseExact(parts[0].Trim(), "N", out lipsyncId) && sha.Length == 64;
    }

    public static string TokenV1(ProductionMixContractV1 contract)
    {
        var hashed = string.IsNullOrWhiteSpace(contract.ContractHash)
            ? contract with { ContractHash = ProductionMixContract.Hash(contract) }
            : contract;
        var rows = new List<string>
        {
            V1Prefix,
            hashed.ContractVersion,
            hashed.CompositionMode,
            hashed.OutputClock,
            ProductionMixContract.Dec(hashed.OutputDurationSec),
            ProductionMixContract.Dec(hashed.DurationToleranceSec),
            hashed.TimingHash,
            hashed.ContractHash,
            "P",
            hashed.Parents.Count.ToString(CultureInfo.InvariantCulture),
        };
        foreach (var p in hashed.Parents)
        {
            rows.Add(p.Role);
            rows.Add(p.ArtifactId.ToString("N"));
            rows.Add(ProductionMixContract.NormSha(p.Sha256));
            rows.Add(p.Binding);
            rows.Add(p.Consume);
        }
        rows.Add("PIC");
        rows.Add(hashed.PictureSegments.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var s in hashed.PictureSegments)
            AddSeg(rows, s);
        rows.Add("AUD");
        rows.Add(hashed.AudioSegments.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var s in hashed.AudioSegments)
            AddSeg(rows, s);
        return string.Join('|', rows);
    }

    public static bool TryParseV1(string? raw, out ProductionMixContractV1 contract)
    {
        contract = new ProductionMixContractV1(
            "", "", "", 0, 0, [], [], [], "", "");
        var parts = (raw ?? "").Split('|');
        if (parts.Length < 12 || !string.Equals(parts[0].Trim(), V1Prefix, StringComparison.Ordinal))
            return false;
        var version = parts[1].Trim();
        var mode = parts[2].Trim();
        var clock = parts[3].Trim();
        if (!ProductionMixContract.TryDec(parts[4], out var output) || output <= 0) return false;
        if (!ProductionMixContract.TryDec(parts[5], out var tol) || tol < 0) return false;
        var timing = parts[6].Trim();
        var hash = parts[7].Trim().ToLowerInvariant();
        if (hash.Length != 64) return false;
        if (!string.Equals(parts[8].Trim(), "P", StringComparison.Ordinal)) return false;
        if (!int.TryParse(parts[9], NumberStyles.None, CultureInfo.InvariantCulture, out var parentCount)
            || parentCount < 1)
            return false;
        var i = 10;
        var parents = new List<ProductionMixParent>();
        for (var n = 0; n < parentCount; n++)
        {
            if (i + 5 > parts.Length) return false;
            if (!Guid.TryParseExact(parts[i + 1].Trim(), "N", out var id)) return false;
            var sha = parts[i + 2].Trim().ToLowerInvariant();
            if (sha.Length != 64) return false;
            parents.Add(new ProductionMixParent(
                parts[i].Trim(), id, sha, parts[i + 3].Trim(), parts[i + 4].Trim()));
            i += 5;
        }
        if (i >= parts.Length || !string.Equals(parts[i].Trim(), "PIC", StringComparison.Ordinal))
            return false;
        i++;
        if (i >= parts.Length
            || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var picCount)
            || picCount < 1)
            return false;
        i++;
        var picture = new List<ProductionMixSegment>();
        for (var n = 0; n < picCount; n++)
        {
            if (!TryReadSeg(parts, ref i, out var seg)) return false;
            picture.Add(seg);
        }
        if (i >= parts.Length || !string.Equals(parts[i].Trim(), "AUD", StringComparison.Ordinal))
            return false;
        i++;
        if (i >= parts.Length
            || !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var audCount)
            || audCount < 1)
            return false;
        i++;
        var audio = new List<ProductionMixSegment>();
        for (var n = 0; n < audCount; n++)
        {
            if (!TryReadSeg(parts, ref i, out var seg)) return false;
            audio.Add(seg);
        }
        if (i != parts.Length) return false;
        var parsed = new ProductionMixContractV1(
            version, mode, clock, output, tol, parents, picture, audio, timing, hash);
        if (ProductionMixContract.Hash(parsed) != hash)
            return false;
        contract = parsed;
        return ProductionMixContract.Validate(parsed) is null;
    }

    public static bool IsComposeV1(string? raw) => TryParseV1(raw, out _);

    static void AddSeg(List<string> rows, ProductionMixSegment s)
    {
        rows.Add(ProductionMixContract.Dec(s.DestStartSec));
        rows.Add(ProductionMixContract.Dec(s.DestEndSec));
        rows.Add(s.SourceRole);
        rows.Add(s.SourceArtifactId == Guid.Empty ? "-" : s.SourceArtifactId.ToString("N"));
        rows.Add(string.IsNullOrWhiteSpace(s.SourceSha256) ? "-" : ProductionMixContract.NormSha(s.SourceSha256));
        rows.Add(ProductionMixContract.Dec(s.SourceStartSec));
        rows.Add(ProductionMixContract.Dec(s.SourceEndSec));
    }

    static bool TryReadSeg(string[] parts, ref int i, out ProductionMixSegment seg)
    {
        seg = new ProductionMixSegment(0, 0, "", Guid.Empty, "", 0, 0);
        if (i + 7 > parts.Length) return false;
        if (!ProductionMixContract.TryDec(parts[i], out var d0)) return false;
        if (!ProductionMixContract.TryDec(parts[i + 1], out var d1)) return false;
        var role = parts[i + 2].Trim();
        var idRaw = parts[i + 3].Trim();
        var shaRaw = parts[i + 4].Trim();
        if (!ProductionMixContract.TryDec(parts[i + 5], out var s0)) return false;
        if (!ProductionMixContract.TryDec(parts[i + 6], out var s1)) return false;
        Guid id;
        if (idRaw == "-") id = Guid.Empty;
        else if (!Guid.TryParseExact(idRaw, "N", out id)) return false;
        var sha = shaRaw == "-" ? "" : shaRaw.ToLowerInvariant();
        i += 7;
        seg = new ProductionMixSegment(d0, d1, role, id, sha, s0, s1);
        return true;
    }
}
