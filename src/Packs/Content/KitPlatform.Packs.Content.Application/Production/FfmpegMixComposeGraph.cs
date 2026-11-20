using System.Globalization;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Builds the COMPOSE_SEGMENTS FFmpeg graph from a frozen MIX_CONTRACT_V1.
/// Does not decide duration, remainder, or sources.
/// </summary>
public static class FfmpegMixComposeGraph
{
    /// <summary>
    /// Frame rate whose frame count lands on the voice duration in whole milliseconds.
    /// A fixed 30 fps grid ends past a duration such as 4.152s, and the MP4 movie header then disagrees with the voice.
    /// </summary>
    public static (int Fps, int Frames) StillClock(decimal durationSec)
    {
        var millis = (int)decimal.Round(durationSec * 1000m, 0, MidpointRounding.AwayFromZero);
        if (millis < 1) millis = 1;
        var step = Gcd(millis, 1000);
        return (1000 / step, millis / step);
    }

    static int Gcd(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }
        return a == 0 ? 1 : a;
    }

    public static string CanonicalCommand(
        ProductionMixContractV1 contract,
        IReadOnlyList<string> inputPaths,
        string outputPath)
    {
        var err = ProductionMixContract.Validate(contract);
        if (err is not null)
            throw new InvalidOperationException(err);
        if (inputPaths.Count != contract.Parents.Count)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (string.Equals(contract.CompositionMode, ProductionMixContract.VoiceOverStill, StringComparison.Ordinal))
        {
            var duration = ProductionMixContract.Dec(contract.OutputDurationSec);
            var clock = StillClock(contract.OutputDurationSec);
            return "ffmpeg -y -hide_banner -loglevel error -loop 1 -framerate " + clock.Fps.ToString(CultureInfo.InvariantCulture)
                + " -i \"" + inputPaths[0] + "\" -i \"" + inputPaths[1]
                + "\" -frames:v " + clock.Frames.ToString(CultureInfo.InvariantCulture)
                + " -t " + duration
                + " -map 0:v:0 -map 1:a:0 -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p"
                + " -c:a aac -b:a 192k -ar 48000 -ac 2 -movflags +faststart \""
                + outputPath + "\"";
        }

        var fc = FilterComplex(contract);
        var inputs = new StringBuilder();
        foreach (var path in inputPaths)
            inputs.Append("-i \"").Append(path).Append("\" ");
        return "ffmpeg -y -hide_banner -loglevel error "
            + inputs
            + "-filter_complex \"" + fc + "\" -map \"[vout]\" -map \"[aout]\" "
            + "-t " + ProductionMixContract.Dec(contract.OutputDurationSec) + " "
            + "-c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p "
            + "-c:a aac -b:a 192k -ar 48000 -ac 2 -movflags +faststart "
            + "\"" + outputPath + "\"";
    }

    public static string FilterComplex(ProductionMixContractV1 contract)
    {
        var index = new Dictionary<Guid, int>();
        for (var i = 0; i < contract.Parents.Count; i++)
            index[contract.Parents[i].ArtifactId] = i;

        var parts = new List<string>();
        var picLabels = new List<string>();
        for (var i = 0; i < contract.PictureSegments.Count; i++)
        {
            var s = contract.PictureSegments[i];
            if (!index.TryGetValue(s.SourceArtifactId, out var idx))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            var label = contract.PictureSegments.Count == 1
                ? "vout"
                : "pv" + i.ToString(CultureInfo.InvariantCulture);
            parts.Add("[" + idx + ":v]trim=start=" + ProductionMixContract.Dec(s.SourceStartSec)
                + ":end=" + ProductionMixContract.Dec(s.SourceEndSec)
                + ",setpts=PTS-STARTPTS[" + label + "]");
            picLabels.Add("[" + label + "]");
        }
        if (picLabels.Count > 1)
            parts.Add(string.Concat(picLabels) + "concat=n="
                + picLabels.Count.ToString(CultureInfo.InvariantCulture) + ":v=1:a=0[vout]");

        var audLabels = new List<string>();
        for (var i = 0; i < contract.AudioSegments.Count; i++)
        {
            var s = contract.AudioSegments[i];
            var label = contract.AudioSegments.Count == 1
                ? "aout"
                : "pa" + i.ToString(CultureInfo.InvariantCulture);
            var dur = ProductionMixContract.Dec(s.DestEndSec - s.DestStartSec);
            if (string.Equals(s.SourceRole, ProductionMixContract.Silence, StringComparison.Ordinal))
            {
                parts.Add("anullsrc=channel_layout=stereo:sample_rate=48000:d=" + dur + "[" + label + "]");
            }
            else
            {
                if (!index.TryGetValue(s.SourceArtifactId, out var idx))
                    throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
                parts.Add("[" + idx + ":a]atrim=start=" + ProductionMixContract.Dec(s.SourceStartSec)
                    + ":end=" + ProductionMixContract.Dec(s.SourceEndSec)
                    + ",asetpts=PTS-STARTPTS,aformat=sample_fmts=fltp:channel_layouts=stereo:sample_rates=48000["
                    + label + "]");
            }
            audLabels.Add("[" + label + "]");
        }
        if (audLabels.Count > 1)
            parts.Add(string.Concat(audLabels) + "concat=n="
                + audLabels.Count.ToString(CultureInfo.InvariantCulture) + ":v=0:a=1[aout]");
        return string.Join(';', parts);
    }
}
