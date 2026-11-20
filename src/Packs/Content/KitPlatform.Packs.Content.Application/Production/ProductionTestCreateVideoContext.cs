namespace KitPlatform.Packs.Content.Production;

/// <summary>TEST-only CREATE_VIDEO facts. Not EP01/EP99/graph/localStorage.</summary>
public static class ProductionTestCreateVideoContext
{
    public const string Story = "TEST STORY — child walks into a room.";
    public const string ActingBeat = "TEST ACTING — walks forward and looks around.";
    public const string VisualContract = "TEST VISUAL — 16:9 interior, locked camera, no film graph.";
    public const string Timing = "5s";
    public const decimal DurationSec = 5;
    public const string AspectRatio = "16:9";

    public static string StoryHash => ShotExecutionCompiler.Hash(Story);
    public static string ActingHash => ShotExecutionCompiler.Hash(ActingBeat);
    public static string VisualHash => ShotExecutionCompiler.Hash(VisualContract);
    public static string TimingHash => ShotExecutionCompiler.Hash(Timing);

    public static ShotExecutionInput Input(Guid? keyframeArtifactId = null) =>
        new(Story, ActingBeat, VisualContract, "",
            "", Timing, "", "",
            keyframeArtifactId, "", AspectRatio, DurationSec, ShotExecutionCompiler.Version,
            keyframeArtifactId is { } id ? [id] : null);

    public static CreateVideoReadinessClaim Claim(Guid? keyframeArtifactId = null) =>
        new(Story, ActingBeat, VisualContract, Timing, DurationSec, keyframeArtifactId);
}
