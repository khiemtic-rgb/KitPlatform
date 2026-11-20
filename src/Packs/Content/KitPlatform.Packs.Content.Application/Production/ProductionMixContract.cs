using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// MIX_CONTRACT_V1. COMPOSE_SEGMENTS keeps the VIDEO_TAKE clock.
/// VOICE_OVER_STILL holds an approved keyframe for the approved voice duration.
/// Does not execute FFmpeg or any provider.
/// </summary>
public static class ProductionMixContract
{
    public const string Version = "MIX_CONTRACT_V1";
    public const string ComposeSegments = "COMPOSE_SEGMENTS";
    public const string VoiceOverStill = "VOICE_OVER_STILL";
    public const string OutputClockVideoTake = "VIDEO_TAKE";
    public const string OutputClockVoice = "VOICE";
    public const string ConsumeBytes = "BYTES";
    public const string ConsumeIdentity = "IDENTITY";
    public const string Silence = "SILENCE";
    public const decimal DurationToleranceSec = 0;

    public static bool AppliesTo(ProductionScope scope) =>
        ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode)
        || (ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId)
            && !ProductionEp01Sh01MixCanon.MatchesScope(scope));

    public static ProductionMixContractV1 Build(
        ProductionArtifact video,
        ProductionArtifact lipsync,
        string videoBinding,
        string lipsyncBinding,
        string timingHash,
        DirectorSpeechInterval? speech = null)
    {
        if (video.Role != ProductionArtifactRoles.VideoTake)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (lipsync.Role != ProductionArtifactRoles.Lipsync)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (video.DurationSec <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.OutputClockUnresolved);
        if (lipsync.DurationSec <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingTimeline);
        if (lipsync.DurationSec > video.DurationSec)
            throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);

        var output = video.DurationSec;
        var lip = lipsync.DurationSec;
        var start = 0m;
        if (speech is { } interval)
        {
            if (!ProductionDirectorTiming.TryApprovedSpeech(interval.StartSec, interval.EndSec, out var approvedStart, out _))
                throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);
            start = approvedStart;
        }
        if (start + lip > output)
            throw new InvalidOperationException(ProductionFailureReasons.InvalidRange);

        var picture = new List<ProductionMixSegment>();
        var audio = new List<ProductionMixSegment>();
        if (start > 0)
        {
            picture.Add(Seg(0, start, ProductionArtifactRoles.VideoTake, video.ArtifactId, video.ByteSha256, 0, start));
            audio.Add(new ProductionMixSegment(0, start, Silence, Guid.Empty, "", 0, start));
        }
        var placedEnd = start + lip;
        picture.Add(Seg(start, placedEnd, ProductionArtifactRoles.Lipsync, lipsync.ArtifactId, lipsync.ByteSha256, 0, lip));
        audio.Add(Seg(start, placedEnd, ProductionArtifactRoles.Lipsync, lipsync.ArtifactId, lipsync.ByteSha256, 0, lip));
        if (placedEnd < output)
        {
            picture.Add(Seg(placedEnd, output, ProductionArtifactRoles.VideoTake, video.ArtifactId, video.ByteSha256, placedEnd, output));
            audio.Add(new ProductionMixSegment(placedEnd, output, Silence, Guid.Empty, "", 0, output - placedEnd));
        }

        var contract = new ProductionMixContractV1(
            Version,
            ComposeSegments,
            OutputClockVideoTake,
            output,
            DurationToleranceSec,
            [
                new ProductionMixParent(
                    ProductionArtifactRoles.VideoTake, video.ArtifactId, video.ByteSha256,
                    videoBinding, ConsumeBytes),
                new ProductionMixParent(
                    ProductionArtifactRoles.Lipsync, lipsync.ArtifactId, lipsync.ByteSha256,
                    lipsyncBinding, ConsumeBytes),
            ],
            picture,
            audio,
            (timingHash ?? "").Trim(),
            "");
        var error = Validate(contract, video, lipsync);
        if (error is not null)
            throw new InvalidOperationException(error);
        return contract with { ContractHash = Hash(contract) };
    }

    /// <summary>
    /// A shot with no spoken line. Picture is the take. Audio is silence for the whole take.
    /// Does not invent a lip-sync parent.
    /// </summary>
    public static ProductionMixContractV1 BuildSilent(
        ProductionArtifact video,
        string videoBinding,
        string timingHash)
    {
        if (video.Role != ProductionArtifactRoles.VideoTake)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (video.DurationSec <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.OutputClockUnresolved);
        var output = video.DurationSec;
        var contract = new ProductionMixContractV1(
            Version,
            ComposeSegments,
            OutputClockVideoTake,
            output,
            DurationToleranceSec,
            [
                new ProductionMixParent(
                    ProductionArtifactRoles.VideoTake, video.ArtifactId, video.ByteSha256,
                    videoBinding, ConsumeBytes),
            ],
            [Seg(0, output, ProductionArtifactRoles.VideoTake, video.ArtifactId, video.ByteSha256, 0, output)],
            [new ProductionMixSegment(0, output, Silence, Guid.Empty, "", 0, output)],
            (timingHash ?? "").Trim(),
            "");
        var error = Validate(contract, video, null);
        if (error is not null)
            throw new InvalidOperationException(error);
        return contract with { ContractHash = Hash(contract) };
    }

    /// <summary>
    /// Approved keyframe held for the approved voice. Picture is not a VIDEO_TAKE and audio is not a LIPSYNC.
    /// </summary>
    public static ProductionMixContractV1 BuildVoiceOverStill(
        ProductionArtifact keyframe,
        ProductionArtifact voice,
        string keyframeBinding,
        string voiceBinding)
    {
        if (keyframe.Role != ProductionArtifactRoles.Keyframe
            || voice.Role != ProductionArtifactRoles.Voice)
            throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
        if (voice.DurationSec <= 0)
            throw new InvalidOperationException(ProductionFailureReasons.OutputClockUnresolved);
        var output = voice.DurationSec;
        var contract = new ProductionMixContractV1(
            Version,
            VoiceOverStill,
            OutputClockVoice,
            output,
            DurationToleranceSec,
            [
                new ProductionMixParent(
                    ProductionArtifactRoles.Keyframe, keyframe.ArtifactId, keyframe.ByteSha256,
                    keyframeBinding, ConsumeBytes),
                new ProductionMixParent(
                    ProductionArtifactRoles.Voice, voice.ArtifactId, voice.ByteSha256,
                    voiceBinding, ConsumeBytes),
            ],
            [Seg(0, output, ProductionArtifactRoles.Keyframe, keyframe.ArtifactId, keyframe.ByteSha256, 0, output)],
            [Seg(0, output, ProductionArtifactRoles.Voice, voice.ArtifactId, voice.ByteSha256, 0, output)],
            "",
            "");
        var error = Validate(contract, keyframe, voice);
        if (error is not null)
            throw new InvalidOperationException(error);
        return contract with { ContractHash = Hash(contract) };
    }

    public static string? Validate(
        ProductionMixContractV1 contract,
        ProductionArtifact? video = null,
        ProductionArtifact? lipsync = null)
    {
        if (string.Equals(contract.CompositionMode, VoiceOverStill, StringComparison.Ordinal))
            return ValidateVoiceOverStill(contract, video, lipsync);
        if (!string.Equals(contract.ContractVersion, Version, StringComparison.Ordinal))
            return ProductionFailureReasons.ContractVersionMismatch;
        if (!string.Equals(contract.CompositionMode, ComposeSegments, StringComparison.Ordinal))
            return ProductionFailureReasons.UnsupportedCompositionMode;
        if (!string.Equals(contract.OutputClock, OutputClockVideoTake, StringComparison.Ordinal))
            return ProductionFailureReasons.OutputClockUnresolved;
        if (contract.OutputDurationSec <= 0)
            return ProductionFailureReasons.OutputClockUnresolved;
        if (contract.PictureSegments.Count == 0 || contract.AudioSegments.Count == 0)
            return ProductionFailureReasons.MissingTimeline;
        if (video is not null)
        {
            if (video.DurationSec <= 0)
                return ProductionFailureReasons.OutputClockUnresolved;
            if (Math.Abs(contract.OutputDurationSec - video.DurationSec) > contract.DurationToleranceSec)
                return ProductionFailureReasons.DurationMismatch;
        }
        if (lipsync is not null && lipsync.DurationSec <= 0)
            return ProductionFailureReasons.MissingTimeline;

        var pic = ValidateLane(contract.PictureSegments, contract.OutputDurationSec, video, lipsync, picture: true);
        if (pic is not null) return pic;
        var aud = ValidateLane(contract.AudioSegments, contract.OutputDurationSec, video, lipsync, picture: false);
        if (aud is not null) return aud;

        if (contract.Parents.Any(p => p.Role == ProductionArtifactRoles.Voice))
            return ProductionFailureReasons.FailedIdentity;
        foreach (var seg in contract.PictureSegments.Concat(contract.AudioSegments))
        {
            if (IsSilence(seg.SourceRole)) continue;
            if (!contract.Parents.Any(p =>
                    p.ArtifactId == seg.SourceArtifactId
                    && string.Equals(p.Sha256, NormSha(seg.SourceSha256), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(p.Consume, ConsumeBytes, StringComparison.Ordinal)))
                return ProductionFailureReasons.FailedIdentity;
        }
        return null;
    }

    static string? ValidateVoiceOverStill(
        ProductionMixContractV1 contract,
        ProductionArtifact? picture,
        ProductionArtifact? voice)
    {
        if (!string.Equals(contract.ContractVersion, Version, StringComparison.Ordinal))
            return ProductionFailureReasons.ContractVersionMismatch;
        if (!string.Equals(contract.OutputClock, OutputClockVoice, StringComparison.Ordinal))
            return ProductionFailureReasons.OutputClockUnresolved;
        if (contract.OutputDurationSec <= 0)
            return ProductionFailureReasons.OutputClockUnresolved;
        if (contract.Parents.Count != 2
            || contract.PictureSegments.Count != 1
            || contract.AudioSegments.Count != 1)
            return ProductionFailureReasons.MissingTimeline;
        var picParent = contract.Parents[0];
        var audParent = contract.Parents[1];
        if (picParent.Role != ProductionArtifactRoles.Keyframe
            || audParent.Role != ProductionArtifactRoles.Voice
            || picParent.Consume != ConsumeBytes
            || audParent.Consume != ConsumeBytes
            || NormSha(picParent.Sha256).Length != 64
            || NormSha(audParent.Sha256).Length != 64)
            return ProductionFailureReasons.FailedIdentity;
        var pic = contract.PictureSegments[0];
        var aud = contract.AudioSegments[0];
        if (pic.SourceRole != ProductionArtifactRoles.Keyframe
            || aud.SourceRole != ProductionArtifactRoles.Voice
            || pic.DestStartSec != 0
            || aud.DestStartSec != 0
            || pic.DestEndSec != contract.OutputDurationSec
            || aud.DestEndSec != contract.OutputDurationSec
            || pic.SourceStartSec != 0
            || aud.SourceStartSec != 0
            || pic.SourceEndSec != pic.DestEndSec
            || aud.SourceEndSec != aud.DestEndSec
            || pic.SourceArtifactId != picParent.ArtifactId
            || aud.SourceArtifactId != audParent.ArtifactId
            || !string.Equals(NormSha(pic.SourceSha256), NormSha(picParent.Sha256), StringComparison.OrdinalIgnoreCase)
            || !string.Equals(NormSha(aud.SourceSha256), NormSha(audParent.Sha256), StringComparison.OrdinalIgnoreCase))
            return ProductionFailureReasons.FailedIdentity;
        if (contract.Parents.Any(p =>
                p.Role == ProductionArtifactRoles.VideoTake || p.Role == ProductionArtifactRoles.Lipsync))
            return ProductionFailureReasons.FailedIdentity;
        if (picture is not null && (
                picture.Role != ProductionArtifactRoles.Keyframe
                || picture.ArtifactId != picParent.ArtifactId
                || !string.Equals(NormSha(picture.ByteSha256), NormSha(picParent.Sha256), StringComparison.OrdinalIgnoreCase)))
            return picture.Role == ProductionArtifactRoles.Keyframe
                && picture.ArtifactId == picParent.ArtifactId
                ? ProductionFailureReasons.HashChanged
                : ProductionFailureReasons.FailedIdentity;
        if (voice is not null)
        {
            if (voice.Role != ProductionArtifactRoles.Voice || voice.ArtifactId != audParent.ArtifactId)
                return ProductionFailureReasons.FailedIdentity;
            if (voice.DurationSec <= 0 || voice.DurationSec != contract.OutputDurationSec)
                return ProductionFailureReasons.DurationMismatch;
            if (!string.Equals(NormSha(voice.ByteSha256), NormSha(audParent.Sha256), StringComparison.OrdinalIgnoreCase))
                return ProductionFailureReasons.HashChanged;
        }
        return null;
    }

    public static string? MatchesFrozen(
        ProductionMixContractV1 frozen,
        ProductionArtifact video,
        ProductionArtifact lipsync)
    {
        var err = Validate(frozen, video, lipsync);
        if (err is not null) return err;
        foreach (var parent in frozen.Parents)
        {
            var live = parent.Role == ProductionArtifactRoles.VideoTake ? video : lipsync;
            if (live.ArtifactId != parent.ArtifactId)
                return ProductionFailureReasons.BindingChanged;
            if (!string.Equals(NormSha(live.ByteSha256), NormSha(parent.Sha256), StringComparison.OrdinalIgnoreCase))
                return ProductionFailureReasons.HashChanged;
        }
        return null;
    }

    /// <summary>
    /// Speech spans are the placed audio segments. Silence does not carry speech.
    /// </summary>
    public static IReadOnlyList<MixSpeechSpan> SpeechSpans(ProductionMixContractV1 contract) =>
        contract.AudioSegments
            .Select(s => new MixSpeechSpan(s.DestStartSec, s.DestEndSec, !IsSilence(s.SourceRole)))
            .ToArray();

    public static string Identity(
        ProductionScope scope,
        ProductionMixContractV1 contract) =>
        string.Join('|',
            scope.BuildId.ToString("N"),
            (scope.ShotCode ?? "").Trim(),
            ProductionActions.MixVideo,
            contract.ContractVersion,
            contract.CompositionMode,
            contract.OutputClock,
            Dec(contract.OutputDurationSec),
            contract.ContractHash,
            contract.TimingHash);

    static string? ValidateLane(
        IReadOnlyList<ProductionMixSegment> segments,
        decimal output,
        ProductionArtifact? video,
        ProductionArtifact? lipsync,
        bool picture)
    {
        var ordered = segments.OrderBy(s => s.DestStartSec).ToArray();
        if (ordered[0].DestStartSec != 0)
            return picture ? ProductionFailureReasons.InvalidRange : ProductionFailureReasons.InvalidRange;
        if (ordered[^1].DestEndSec != output)
            return ProductionFailureReasons.DurationMismatch;
        for (var i = 0; i < ordered.Length; i++)
        {
            var s = ordered[i];
            if (s.DestStartSec < 0 || s.SourceStartSec < 0)
                return ProductionFailureReasons.InvalidRange;
            if (s.DestEndSec <= s.DestStartSec || s.SourceEndSec <= s.SourceStartSec)
                return ProductionFailureReasons.InvalidRange;
            if (s.DestEndSec - s.DestStartSec != s.SourceEndSec - s.SourceStartSec)
                return ProductionFailureReasons.InvalidRange;
            if (i > 0)
            {
                if (s.DestStartSec < ordered[i - 1].DestEndSec)
                    return picture ? ProductionFailureReasons.PictureOverlap : ProductionFailureReasons.AudioOverlap;
                if (s.DestStartSec > ordered[i - 1].DestEndSec)
                    return picture ? ProductionFailureReasons.PictureGap : ProductionFailureReasons.AudioGap;
            }
            if (IsSilence(s.SourceRole))
            {
                if (picture) return ProductionFailureReasons.InvalidRange;
                if (s.SourceArtifactId != Guid.Empty || NormSha(s.SourceSha256).Length > 0)
                    return ProductionFailureReasons.FailedIdentity;
                continue;
            }
            var dur = s.SourceRole == ProductionArtifactRoles.VideoTake ? video?.DurationSec
                : s.SourceRole == ProductionArtifactRoles.Lipsync ? lipsync?.DurationSec
                : null;
            if (dur is { } max && s.SourceEndSec > max)
                return ProductionFailureReasons.InvalidRange;
        }
        return null;
    }

    static ProductionMixSegment Seg(
        decimal d0, decimal d1, string role, Guid id, string sha, decimal s0, decimal s1) =>
        new(d0, d1, role, id, NormSha(sha), s0, s1);

    static bool IsSilence(string? role) =>
        string.Equals((role ?? "").Trim(), Silence, StringComparison.Ordinal);

    public static string Dec(decimal n) =>
        n.ToString("0.###", CultureInfo.InvariantCulture);

    public static bool TryDec(string? raw, out decimal value) =>
        decimal.TryParse((raw ?? "").Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    public static string NormSha(string? sha) => (sha ?? "").Trim().ToLowerInvariant();

    public static string Hash(ProductionMixContractV1 contract)
    {
        var canonical = string.Join('|',
            contract.ContractVersion,
            contract.CompositionMode,
            contract.OutputClock,
            Dec(contract.OutputDurationSec),
            Dec(contract.DurationToleranceSec),
            contract.TimingHash,
            string.Join(';', contract.Parents.Select(p =>
                p.Role + "," + p.ArtifactId.ToString("N") + "," + NormSha(p.Sha256) + "," + p.Binding + "," + p.Consume)),
            string.Join(';', contract.PictureSegments.Select(SegCanon)),
            string.Join(';', contract.AudioSegments.Select(SegCanon)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    static string SegCanon(ProductionMixSegment s) =>
        string.Join(',',
            Dec(s.DestStartSec), Dec(s.DestEndSec), s.SourceRole,
            s.SourceArtifactId.ToString("N"), NormSha(s.SourceSha256),
            Dec(s.SourceStartSec), Dec(s.SourceEndSec));
}

public sealed record ProductionMixParent(
    string Role,
    Guid ArtifactId,
    string Sha256,
    string Binding,
    string Consume);

public sealed record ProductionMixSegment(
    decimal DestStartSec,
    decimal DestEndSec,
    string SourceRole,
    Guid SourceArtifactId,
    string SourceSha256,
    decimal SourceStartSec,
    decimal SourceEndSec);

public sealed record ProductionMixContractV1(
    string ContractVersion,
    string CompositionMode,
    string OutputClock,
    decimal OutputDurationSec,
    decimal DurationToleranceSec,
    IReadOnlyList<ProductionMixParent> Parents,
    IReadOnlyList<ProductionMixSegment> PictureSegments,
    IReadOnlyList<ProductionMixSegment> AudioSegments,
    string TimingHash,
    string ContractHash);
