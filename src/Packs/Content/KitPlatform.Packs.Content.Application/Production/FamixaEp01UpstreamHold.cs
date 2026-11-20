namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Inspected EP01 artifacts that must not be sent to the next paid stage.
/// New artifacts are not on this list. SH03 is not listed and is not modified here.
/// </summary>
public static class FamixaEp01UpstreamHold
{
    public const string MotionBlocked = "EP01_KEYFRAME_QUALITY_BLOCKED";
    public const string LipsyncBlocked = "EP01_VIDEO_QUALITY_BLOCKED";

    public static readonly Guid Sh01Keyframe = Guid.Parse("5f9f78c2-e310-4978-a872-f9125c3453a9");
    public static readonly Guid Sh01KeyframeEpoch1 = Guid.Parse("b940624d-cf83-4ea1-b250-d347cc83a539");
    /// <summary>Wide SH01 still. Linh is too small for a readable mouth.</summary>
    public static readonly Guid Sh01KeyframeWide = Guid.Parse("e240ff86-d773-4e0b-8aa1-179d9ccbb620");
    public static readonly Guid Sh02Keyframe = Guid.Parse("27872dda-69f0-4758-b1c9-4fd214b292a5");
    /// <summary>SH02 still whose mouth is a closed line.</summary>
    public static readonly Guid Sh02KeyframeClosedMouth = Guid.Parse("37d3e561-7500-4edb-8043-502475613b49");
    /// <summary>SH03 still whose mouth is a wide shout. Runway returned no video from it.</summary>
    public static readonly Guid Sh03KeyframeWideMouth = Guid.Parse("5c1b8a63-9b6b-40fe-9fed-c24fbafeff65");
    /// <summary>SH03 still whose mouth closed into a line and whose paper is blank.</summary>
    public static readonly Guid Sh03KeyframeClosedMouth = Guid.Parse("38668c70-2228-4791-ab59-e98a0f6c1b8a");
    /// <summary>SH03 still with a corner mark and a circled digit. Runway rejects overlaid text.</summary>
    public static readonly Guid Sh03KeyframeTextMark = Guid.Parse("803f18bf-feb9-4754-b5c5-0b238e70bb16");
    public static readonly Guid Sh04KeyframeEpoch1 = Guid.Parse("f2e27d89-e558-4e3d-914b-f0b6e449fb6f");
    public static readonly Guid Sh01Video = Guid.Parse("81b29b5c-9e5e-492f-8e40-154c43604507");
    public static readonly Guid Sh01VideoEpoch1 = Guid.Parse("e5dd0411-b493-4a89-a267-e38f9424672f");
    /// <summary>SH01 take whose head turns to the back and snaps forward.</summary>
    public static readonly Guid Sh01VideoHeadSnap = Guid.Parse("b061342e-8dee-4cf1-a4c5-4855ce228635");
    public static readonly Guid Sh02Video = Guid.Parse("e5435599-9ad0-482c-9a25-1db70271b0b3");
    public static readonly Guid Sh02VideoEpoch1 = Guid.Parse("55563890-0b45-4c42-a626-13e30469cbdc");
    /// <summary>Clean SH02 take kept as the picture fallback. Classified so one closer retry is allowed.</summary>
    public static readonly Guid Sh02VideoKeep = Guid.Parse("a34360bf-2a2b-4a34-a27c-6aa3793bda77");
    /// <summary>Mouth-open still's video. Minh turns to profile during the line, so the mouth closes.</summary>
    public static readonly Guid Sh02VideoProfile = Guid.Parse("82bf78e6-6c16-48a2-835f-7e54c39fd2f2");
    /// <summary>SH02 Fal results whose mouth stayed closed. Not video takes.</summary>
    public static readonly Guid Sh02LipsyncEpoch1 = Guid.Parse("5c2ca20f-35c0-4fba-8db6-5e3df073cc7f");
    public static readonly Guid Sh02LipsyncEpoch2 = Guid.Parse("60511c61-d6bd-401c-a106-b6b847b0ffe3");
    /// <summary>SH02 voice on the older timing. A new take needs a voice frozen on the same performance hash.</summary>
    public static readonly Guid Sh02VoiceEpoch1 = Guid.Parse("8b4e3166-3cba-4453-a90e-9576939493a3");
    /// <summary>SH01 voice frozen before the medium take. The new video uses a different performance hash.</summary>
    public static readonly Guid Sh01VoiceEpoch1 = Guid.Parse("1e981299-d5ef-4fa2-8820-eb0db95c4af0");
    /// <summary>SH01 Fal result on the wide head-snap take. The mouth did not follow the line.</summary>
    public static readonly Guid Sh01LipsyncEpoch1 = Guid.Parse("f933c79e-ff9d-4be3-8b9a-f2d0d77e50eb");

    public static bool BlocksMotion(Guid? keyframeId) =>
        keyframeId == Sh01Keyframe || keyframeId == Sh01KeyframeEpoch1 || keyframeId == Sh01KeyframeWide
        || keyframeId == Sh02Keyframe || keyframeId == Sh02KeyframeClosedMouth
        || keyframeId == Sh04KeyframeEpoch1
        || keyframeId == Sh03KeyframeWideMouth
        || keyframeId == Sh03KeyframeClosedMouth
        || keyframeId == Sh03KeyframeTextMark;

    public static bool BlocksLipsync(Guid? videoId) =>
        videoId == Sh01Video || videoId == Sh01VideoEpoch1 || videoId == Sh01VideoHeadSnap
        || videoId == Sh02Video || videoId == Sh02VideoEpoch1;

    /// <summary>A paid output that failed quality review. Counts as a classified attempt. Does not block a different parent.</summary>
    public static bool MarksPaidRetry(Guid? artifactId) =>
        BlocksMotion(artifactId) || BlocksLipsync(artifactId)
        || artifactId == Sh02LipsyncEpoch1 || artifactId == Sh02LipsyncEpoch2
        || artifactId == Sh02VideoKeep || artifactId == Sh02VideoProfile
        || artifactId == Sh02VoiceEpoch1 || artifactId == Sh01VoiceEpoch1
        || artifactId == Sh01LipsyncEpoch1;
}
