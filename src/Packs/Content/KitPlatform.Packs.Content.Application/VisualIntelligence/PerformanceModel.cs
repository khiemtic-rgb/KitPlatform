namespace KitPlatform.Packs.Content.VisualIntelligence;

/// <summary>
/// Production data for a rigged performance. The renderer does not own these records.
/// </summary>
public sealed record CharacterRig(
    string Id,
    float UpperArm,
    float ForeArm,
    float Thigh,
    float Shin,
    float Torso,
    float Head);

public sealed record JointPose(
    float RightShoulder,
    float RightElbow,
    float LeftShoulder,
    float LeftElbow,
    float Spine,
    float Head = 0)
{
    public static JointPose Lerp(JointPose a, JointPose b, float t) => new(
        a.RightShoulder + (b.RightShoulder - a.RightShoulder) * t,
        a.RightElbow + (b.RightElbow - a.RightElbow) * t,
        a.LeftShoulder + (b.LeftShoulder - a.LeftShoulder) * t,
        a.LeftElbow + (b.LeftElbow - a.LeftElbow) * t,
        a.Spine + (b.Spine - a.Spine) * t,
        a.Head + (b.Head - a.Head) * t);
}

public sealed record PoseKey(float Time, JointPose Pose, string Hand = "rest");

public sealed record ActionClip(string Id, string RigId, IReadOnlyList<PoseKey> Keys);

public readonly record struct TimelineCue(string ClipId, float Start, float Duration);

public sealed record FilmBeat(string ClipId, float Start, float Duration, string Line);

public sealed record ShotTimeline(
    string Id,
    string BrandCode,
    string Setting,
    string PlateFile,
    float AnchorX,
    float FootY,
    TimelineCue Cue);

public sealed record CastLook(uint Skin, uint Hair, uint Cloth, uint Ink);

/// <summary>
/// Which painting a character is cut from. The engine refuses a master it cannot cut
/// instead of drawing another character in its place.
/// </summary>
public sealed record CharacterCut(string Id, string RigId, string MasterFile);

public sealed record PerformanceRequest(
    CharacterRig Rig,
    ActionClip Clip,
    ShotTimeline Timeline,
    CastLook Look,
    float TimeSeconds,
    CharacterCut? Character = null);

public static class CharacterCatalog
{
    public static CharacterCut Lan { get; } = new("CHAR-VIS-LAN", "rig-human", "lan-stand.jpg");
}

public static class PerformanceSampling
{
    public static JointPose At(ActionClip clip, TimelineCue cue, float timeSeconds)
    {
        var keys = clip.Keys;
        if (keys.Count == 0)
            throw new InvalidOperationException("Clip " + clip.Id + " has no keys.");
        var span = keys[^1].Time - keys[0].Time;
        var local = Math.Max(0, timeSeconds - cue.Start);
        var scaled = span <= 0 ? 0 : local / Math.Max(0.05f, cue.Duration) * span;
        var clock = keys[0].Time + scaled;
        if (clock <= keys[0].Time)
            return keys[0].Pose;
        if (clock >= keys[^1].Time)
            return keys[^1].Pose;
        for (var i = 1; i < keys.Count; i++)
        {
            if (clock > keys[i].Time)
                continue;
            var gap = keys[i].Time - keys[i - 1].Time;
            var t = gap <= 0 ? 1 : Ease((clock - keys[i - 1].Time) / gap);
            return JointPose.Lerp(keys[i - 1].Pose, keys[i].Pose, t);
        }

        return keys[^1].Pose;
    }

    public static string HandAt(ActionClip clip, TimelineCue cue, float timeSeconds)
    {
        var keys = clip.Keys;
        if (keys.Count == 0)
            return "rest";
        var span = keys[^1].Time - keys[0].Time;
        var local = Math.Max(0, timeSeconds - cue.Start);
        var scaled = span <= 0 ? 0 : local / Math.Max(0.05f, cue.Duration) * span;
        var clock = keys[0].Time + scaled;
        var hand = keys[0].Hand;
        for (var i = 1; i < keys.Count; i++)
        {
            if (clock + 0.0001f < keys[i - 1].Time)
                break;
            hand = keys[i].Hand;
            if (clock <= keys[i].Time)
                break;
        }

        return string.IsNullOrWhiteSpace(hand) ? "rest" : hand;
    }

    private static float Ease(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
