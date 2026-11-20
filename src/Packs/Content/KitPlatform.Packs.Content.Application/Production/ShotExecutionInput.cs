using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace KitPlatform.Packs.Content.Production;

public sealed record ShotExecutionInput(
    string Story,
    string ActingBeat,
    string VisualContract,
    string CharacterReferences,
    string KeyframeHash,
    string Timing,
    string VoiceRequirements,
    string MotionRequirements,
    Guid? KeyframeArtifactId = null,
    string KeyframeByteSha256 = "",
    string AspectRatio = "16:9",
    decimal DurationSec = 5,
    string CompilerVersion = ShotExecutionCompiler.Version,
    IReadOnlyList<Guid>? ParentArtifactIds = null,
    string LineId = "",
    string SpeakerCharacterId = "",
    string VoiceId = "",
    Guid? MotionArtifactId = null,
    Guid? VoiceArtifactId = null,
    Guid? LipsyncArtifactId = null,
    Guid? AcceptArtifactId = null,
    Guid? PerformanceSnapshotId = null,
    string? PerformanceHash = null,
    string? PerformancePayloadJson = null,
    string PictureStrategy = "",
    string SpeechDelivery = "",
    bool StrategyConfirmed = false,
    int DirectSpeakerCount = 0);

public sealed record ShotCompilerResult(
    string Story,
    string ActingBeat,
    string VisualContract,
    string CompilerOutput,
    IReadOnlyList<string> RetainedStoryAssertions);

public static class ShotExecutionCompiler
{
    public const string Version = "SHOT_EXECUTION_COMPILER_V1";

    public static IReadOnlyList<string> StoryAssertionsOf(string story)
    {
        var text = (story ?? "").Trim();
        if (text.Length == 0) return [];
        return text
            .Split([' ', ',', '.', ';', ':', '—', '-', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => t.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ShotCompilerResult Compile(ShotExecutionInput input)
    {
        var story = (input.Story ?? "").Trim();
        var acting = (input.ActingBeat ?? "").Trim();
        var visual = (input.VisualContract ?? "").Trim();
        if (story.Length == 0)
            throw new InvalidOperationException("CREATE_VIDEO requires Story. Compiler must not invent or drop it.");

        var assertions = StoryAssertionsOf(story);
        var holdBody = IsHoldBody(acting);
        var motionLine = holdBody
            ? $"{acting} STORY_MUST_REMAIN: {story}"
            : acting;

        var output =
            $"STORY: {story}\nACTING: {acting}\nVISUAL: {visual}\nMOTION: {motionLine}\nTIMING: {input.Timing}\nCHAR: {input.CharacterReferences}";

        foreach (var token in assertions)
        {
            if (output.IndexOf(token, StringComparison.OrdinalIgnoreCase) < 0)
                throw new InvalidOperationException($"Compiler dropped required story assertion: {token}");
        }

        return new ShotCompilerResult(story, acting, visual, output, assertions);
    }

    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? ""))).ToLowerInvariant();

    public static string SnapshotHash(ProductionTaskInputSnapshot snap)
    {
        var parts = new List<string>
        {
            snap.StoryHash, snap.ActingHash, snap.VisualHash, snap.CompilerHash,
            snap.KeyframeHash, snap.VoiceHash, snap.MotionHash, snap.LipsyncSourceHash,
            snap.TimingHash, snap.ProviderId, snap.ModelId,
            snap.KeyframeArtifactId?.ToString("N") ?? "",
            snap.AspectRatio, snap.DurationSec.ToString("0.###"), snap.CompilerVersion,
        };
        if (snap.PerformanceSnapshotId is not null || !string.IsNullOrWhiteSpace(snap.PerformanceHash))
        {
            parts.Add(snap.PerformanceSnapshotId?.ToString("D") ?? "");
            parts.Add(snap.PerformanceHash ?? "");
        }
        if (snap.CredentialId is Guid credentialId && snap.CredentialVersionId is Guid credentialVersionId
            && credentialId != Guid.Empty && credentialVersionId != Guid.Empty)
        {
            parts.Add(credentialId.ToString("D"));
            parts.Add(credentialVersionId.ToString("D"));
        }
        return Hash(string.Join('|', parts));
    }

    static bool IsHoldBody(string acting) =>
        Regex.IsMatch(acting ?? "", @"stays in place|không di chuyển|hơi điều chỉnh|chỉ chuyển động nhẹ", RegexOptions.IgnoreCase);
}
