using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Official EP01 SH02 CREATE_LIPSYNC parents. Do not invent.
/// VIDEO_TAKE is ACCEPTED from CK21A. VOICE is ACCEPTED/CURRENT from CK22.
/// Speaker is CHAR-001 / line-SC01-CHAR-001-2.
/// CK23 T3 pin (0.28, 0.38) → [278,184,438,364] is historical evidence only — it missed the face.
/// CK23B box is measured on VIDEO_TAKE frame 0 / 2.0s (XYXY). Global TryResolve stays CHAR-003-only.
/// </summary>
public static class ProductionEp01Sh02LipsyncCanon
{
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_CREATE_LIPSYNC_V1";
    public const string FaceBoxSuiteId = "FAMIXA_PRODUCTION_OS_OFFICIAL_SH02_LIPSYNC_FACE_BOX_V1";
    public static readonly Guid VideoArtifactId = Guid.Parse("b44e530b-5b59-422f-9b80-e058a3a639b5");
    public const string VideoSha256 = ProductionEp01Sh02VoiceIngressCanon.VideoParentSha;
    public const long VideoBytes = ProductionEp01Sh02VoiceIngressCanon.VideoParentBytes;
    public static readonly Guid VoiceArtifactId = Guid.Parse("0fde86c2-250f-4303-bda0-4913a6dbd1e4");
    public const string VoiceSha256 = "bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298";
    public const long VoiceBytes = 40586;
    public static readonly Guid HistoricalCk23LipsyncArtifactId = Guid.Parse("32bec770-9706-4aad-bc21-8db3d4f11d4c");
    public const string HistoricalCk23LipsyncSha256 = "9d8e6949eb43480a60cee4e6058cff6239beb24191ed3fe06595e30d12b724db";
    public const string LineId = ProductionEp01Sh02VoiceIngressCanon.LineId;
    public const string LineText = ProductionEp01Sh02VoiceIngressCanon.LineText;
    public const string SpeakerCharacterId = ProductionEp01Sh02VoiceIngressCanon.SpeakerCharacterId;
    public const string VoiceId = ProductionEp01Sh02VoiceIngressCanon.VoiceId;
    public const string ProviderId = ProductionLipsyncCatalog.FalProviderId;
    public const string ModelId = ProductionLipsyncCatalog.SyncLipsyncV3;
    public const string SyncMode = "cut_off";
    public const double LegacyT3PinX01 = 0.28;
    public const double LegacyT3PinY01 = 0.38;
    public const int LegacyT3BoxX1 = 278;
    public const int LegacyT3BoxY1 = 184;
    public const int LegacyT3BoxX2 = 438;
    public const int LegacyT3BoxY2 = 364;
    public const double PinX01 = 0.46;
    public const double PinY01 = 0.31;
    public const int PinWidth = 1280;
    public const int PinHeight = 720;
    public const int PinFrame = 0;
    public const int BoxX1 = 450;
    public const int BoxY1 = 50;
    public const int BoxX2 = 720;
    public const int BoxY2 = 400;
    public static readonly int[] FaceBoxXyxy = [BoxX1, BoxY1, BoxX2, BoxY2];
    public const string FaceBoxIdemToken = "450|50|720|400|cut_off";

    public static ProductionScope Scope() => ProductionEp01Sh02IngressCanon.Scope();

    public static bool MatchesScope(ProductionScope scope) =>
        ProductionEp01Sh02VoiceIngressCanon.MatchesScope(scope);

    /// <summary>
    /// Tenant EP01 SH02 file whose bytes are the official Veo take. A new id does not make it a new take.
    /// </summary>
    public static bool IsTenantByteCopy(ProductionScope scope, string? byteSha256) =>
        ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode)
        && string.Equals(scope.ShotCode, "EP01-SC01-SH02", StringComparison.Ordinal)
        && string.Equals(byteSha256, VideoSha256, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesVideo(AuthoritativeVideoTake? take) =>
        take is not null
        && take.ArtifactId == VideoArtifactId
        && take.ArtifactRole == ProductionArtifactRoles.VideoTake
        && take.AcceptanceStatus == OfficialVeoVideoTakeAcceptanceV1.AcceptedStatus
        && string.Equals(take.ByteSha256, VideoSha256, StringComparison.OrdinalIgnoreCase)
        && take.ByteLength == VideoBytes;

    public static bool MatchesVoice(ProductionArtifact? voice) =>
        voice is not null
        && voice.ArtifactId == VoiceArtifactId
        && voice.Role == ProductionArtifactRoles.Voice
        && voice.VerificationStatus == ProductionArtifactStatuses.Verified
        && string.Equals(voice.ByteSha256, VoiceSha256, StringComparison.OrdinalIgnoreCase)
        && voice.SizeBytes == VoiceBytes;

    /// <summary>
    /// SH02-scoped CHAR-001 pin. Requires the canonical line so CHAR-001 is not a silent Fal auto target.
    /// </summary>
    public static bool TryResolveSpeaker(string? lineId, string? speakerCharacterId, out ProductionLipsyncSpeakerPin pin)
    {
        pin = default;
        if (!string.Equals((speakerCharacterId ?? "").Trim(), SpeakerCharacterId, StringComparison.Ordinal))
            return false;
        if (!string.Equals((lineId ?? "").Trim(), LineId, StringComparison.Ordinal))
            return false;
        pin = new ProductionLipsyncSpeakerPin(SpeakerCharacterId, PinX01, PinY01, PinWidth, PinHeight, PinFrame);
        return FamixaFalLipsyncSpeakerPinV1.TryActiveSpeakerDetection(
            pin.X01, pin.Y01, pin.Width, pin.Height, pin.FrameNumber, out _, out _);
    }

    public static bool MatchesFaceBox(int[]? box) =>
        box is { Length: 4 }
        && box[0] == BoxX1 && box[1] == BoxY1 && box[2] == BoxX2 && box[3] == BoxY2
        && box[2] > box[0] && box[3] > box[1]
        && box[0] >= 0 && box[1] >= 0 && box[2] <= PinWidth && box[3] <= PinHeight;

    public static string ExecutionAuthority(
        Guid snapshotId,
        string inputHash,
        Guid videoParentArtifactId,
        string videoParentSha,
        Guid voiceParentArtifactId,
        string voiceParentSha) =>
        string.Join('\n',
            "LIPSYNC_EXECUTION_AUTHORITY",
            "BuildId=" + ProductionEp01Sh02IngressCanon.BuildId.ToString("D"),
            "ShotCode=" + ProductionEp01Sh02IngressCanon.Shot,
            "LipSync Provider=" + ProviderId + " / " + ModelId,
            "VideoParentArtifactId=" + videoParentArtifactId.ToString("D"),
            "VideoParentSHA=" + videoParentSha,
            "VoiceParentArtifactId=" + voiceParentArtifactId.ToString("D"),
            "VoiceParentSHA=" + voiceParentSha,
            "SnapshotId=" + snapshotId.ToString("D"),
            "InputHash=" + inputHash);
}
