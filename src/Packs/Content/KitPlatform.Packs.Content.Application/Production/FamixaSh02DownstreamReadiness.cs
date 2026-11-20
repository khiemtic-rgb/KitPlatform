using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Acceptance facts for one Famixa shot downstream of CREATE_VIDEO.
/// This is a predicate over an existing artifact. It does not execute a provider,
/// publish, or create a second pipeline.
/// </summary>
public sealed record FamixaDownstreamCandidate(
    string ProviderId,
    string ModelId,
    string Role,
    string MimeType,
    byte[]? Bytes,
    string? ByteSha256,
    decimal DurationSec,
    decimal AudioDurationSec,
    decimal LeadingSilenceSec,
    decimal TrailingSilenceSec,
    decimal ApprovedLeadSec,
    decimal ApprovedTailSec,
    bool FixtureProvenance,
    string? ProviderJobId,
    Guid ArtifactId,
    int BoundShotCount,
    int ExpectedShotCount,
    bool SpeechContentVerified,
    bool MinhVoiceIdentityVerified,
    bool UsageRightsVerified,
    bool SpeechTimingVerified);

public sealed record FamixaDownstreamVerdict(
    bool PlayableRealVideo,
    bool VoiceVerified,
    bool MixAcceptable,
    bool EpisodeAssembled,
    IReadOnlyList<string> Defects);

/// <summary>
/// SH02 real-video and downstream gates. A fixture file, the current SH02 mix,
/// unverified Minh speech, short audio, or extra silence cannot be reported as
/// a finished episode.
/// </summary>
public static class FamixaSh02DownstreamReadiness
{
    public const string FixtureNotReal = "FIXTURE_NOT_REAL_PROVIDER";
    public const string NotPlayable = "NOT_PLAYABLE";
    public const string ResolutionInvalid = "RESOLUTION_INVALID";
    public const string HashMismatch = "ARTIFACT_HASH_MISMATCH";
    public const string AudioShorterThanVideo = "AUDIO_SHORTER_THAN_VIDEO";
    public const string ExcessSilence = "EXCESS_SILENCE";
    public const string SpeechUnverified = "SPEECH_UNVERIFIED";
    public const string MixIsNotEpisode = "MIX_IS_NOT_EPISODE";
    public const string EpisodeIncomplete = "EPISODE_ASSEMBLY_INCOMPLETE";
    public const decimal SilenceToleranceSec = 0.35m;
    public const decimal AudioToleranceSec = 0.05m;
    public const int MinWidth = 640;
    public const int MinHeight = 360;

    public static FamixaDownstreamVerdict Evaluate(FamixaDownstreamCandidate candidate)
    {
        var defects = new List<string>();
        var knownMix = IsKnownSh02Mix(candidate);
        if (knownMix)
            defects.Add(MixIsNotEpisode);

        var fixture = candidate.FixtureProvenance
            || IsFixtureProvider(candidate.ProviderId)
            || IsFixtureProvider(candidate.ModelId)
            || (candidate.Bytes is { Length: > 0 } && ProductionTestMp4.ContainsFixtureMarker(candidate.Bytes))
            || IsFixtureJob(candidate.ProviderJobId);
        if (fixture)
            defects.Add(FixtureNotReal);

        var playable = !fixture && !knownMix && IsPlayableVeo(candidate, defects);
        var voice = candidate.SpeechContentVerified
            && candidate.MinhVoiceIdentityVerified
            && candidate.UsageRightsVerified
            && candidate.SpeechTimingVerified;
        if (!voice)
            defects.Add(SpeechUnverified);

        var audioShort = candidate.AudioDurationSec > 0
            && candidate.DurationSec > 0
            && candidate.AudioDurationSec + AudioToleranceSec < candidate.DurationSec;
        if (audioShort)
            defects.Add(AudioShorterThanVideo);

        var excessSilence = candidate.LeadingSilenceSec > candidate.ApprovedLeadSec + SilenceToleranceSec
            || candidate.TrailingSilenceSec > candidate.ApprovedTailSec + SilenceToleranceSec;
        if (excessSilence)
            defects.Add(ExcessSilence);

        var episode = !knownMix
            && !fixture
            && string.Equals(candidate.Role, ProductionArtifactRoles.Assembly, StringComparison.Ordinal)
            && candidate.ExpectedShotCount >= 2
            && candidate.BoundShotCount == candidate.ExpectedShotCount
            && candidate.Bytes is { Length: >= 32 }
            && ProductionTestMp4.IsDecodable(candidate.Bytes, out _)
            && !ProductionTestMp4.ContainsFixtureMarker(candidate.Bytes)
            && ProductionTestMp4.TryReadDuration(candidate.Bytes, out var episodeDuration)
            && episodeDuration > 0;
        if (string.Equals(candidate.Role, ProductionArtifactRoles.Assembly, StringComparison.Ordinal) && !episode)
            defects.Add(EpisodeIncomplete);
        if (string.Equals(candidate.Role, ProductionArtifactRoles.Mix, StringComparison.Ordinal))
            defects.Add(MixIsNotEpisode);

        return new FamixaDownstreamVerdict(
            playable,
            voice,
            playable && voice && !audioShort && !excessSilence && !knownMix,
            episode,
            defects);
    }

    static bool IsPlayableVeo(FamixaDownstreamCandidate candidate, List<string> defects)
    {
        if (!string.Equals(candidate.ProviderId, FamixaProviderIds.Google, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(candidate.ModelId, ProductionMotionCatalog.Veo31ModelId, StringComparison.OrdinalIgnoreCase))
        {
            defects.Add(NotPlayable);
            return false;
        }
        if (!string.Equals(candidate.MimeType, ProductionTestMp4.Mime, StringComparison.OrdinalIgnoreCase)
            || candidate.Bytes is not { Length: >= 32 }
            || candidate.DurationSec <= 0
            || string.IsNullOrWhiteSpace(candidate.ProviderJobId))
        {
            defects.Add(NotPlayable);
            return false;
        }
        var sha = KitVideoIntegrityRules.Sha256Hex(candidate.Bytes);
        if (!string.Equals(sha, (candidate.ByteSha256 ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
        {
            defects.Add(HashMismatch);
            return false;
        }
        if (!ProductionTestMp4.IsDecodable(candidate.Bytes, out _)
            || !ProductionTestMp4.TryReadDuration(candidate.Bytes, out var duration)
            || duration <= 0)
        {
            defects.Add(NotPlayable);
            return false;
        }
        if (!ProductionTestMp4.TryReadDisplaySize(candidate.Bytes, out var width, out var height)
            || width < MinWidth
            || height < MinHeight)
        {
            defects.Add(ResolutionInvalid);
            return false;
        }
        return true;
    }

    static bool IsKnownSh02Mix(FamixaDownstreamCandidate candidate) =>
        candidate.ArtifactId == ProductionEp01Sh02AssembleCanon.MixArtifactId
        || string.Equals(
            (candidate.ByteSha256 ?? "").Trim(),
            ProductionEp01Sh02AssembleCanon.MixSha256,
            StringComparison.OrdinalIgnoreCase);

    static bool IsFixtureProvider(string? value)
    {
        var raw = (value ?? "").Trim();
        return raw.Equals(ProductionMotionCatalog.FixtureProviderId, StringComparison.OrdinalIgnoreCase)
            || raw.Equals(ProductionMotionCatalog.FixtureModelId, StringComparison.OrdinalIgnoreCase);
    }

    static bool IsFixtureJob(string? jobId)
    {
        var raw = (jobId ?? "").Trim();
        return raw.StartsWith("fixture", StringComparison.OrdinalIgnoreCase)
            || raw.StartsWith("local-", StringComparison.OrdinalIgnoreCase);
    }
}
