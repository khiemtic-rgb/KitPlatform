namespace KitPlatform.Packs.Content.Production;

public sealed record ShotPerformanceBefore(
    string? Action = null,
    string? Body = null,
    string? Prop = null,
    string? Gaze = null,
    string? Room = null);

public sealed record ShotPerformanceDuring(
    string? Emotion = null,
    int? Intensity = null);

public sealed record ShotPerformanceAfter(
    string? Action = null,
    string? Gaze = null);

public sealed record ShotPerformanceSlice(
    ShotPerformanceBefore? Before = null,
    ShotPerformanceDuring? During = null,
    ShotPerformanceAfter? After = null,
    string? GazeTarget = null);

public sealed record CharacterInShot(
    string CharacterId,
    string Participation,
    string VisualRole,
    string SpeechRole,
    string? EmotionalState = null,
    int? Intensity = null,
    IReadOnlyList<string>? LineIds = null);

public sealed record PerformanceTextIdentity(
    string Text,
    string TextHash);

public sealed record PerformanceDelivery(
    string Emotion,
    int Intensity,
    string Pace,
    string Volume,
    double PauseSec,
    string Label,
    string? Emphasis = null,
    bool Locked = false);

public sealed record PerformanceLine(
    string LineId,
    string CharacterId,
    PerformanceTextIdentity TextIdentity,
    PerformanceDelivery Delivery);

public sealed record PerformanceTimingRef(
    string TimingDocumentId,
    string? TimingHash = null);

public sealed record PerformanceVisualRef(
    IReadOnlyList<string> CharacterIdentityIds,
    string? VisualAuthorityId = null,
    string? VisualContractHash = null);

public sealed record PerformanceStoryRef(
    IReadOnlyList<string> DialogueSegmentIds,
    string? StoryTextHash = null,
    string? BeatId = null);

public sealed record PerformanceSourceRef(
    bool ActingBeatPresent,
    IReadOnlyList<bool> LinePerformanceLocked,
    bool DerivedFromInfer,
    string? DraftFingerprint = null,
    decimal? ProductionDurationSec = null,
    string? TimingSource = null,
    string? DecisionType = null,
    Guid? KeyframeArtifactId = null,
    decimal? SpeechStartSec = null,
    decimal? SpeechEndSec = null);

public sealed record PerformanceDraft(
    string DocumentId,
    int SchemaVersion,
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    ShotPerformanceSlice ShotPerformance,
    IReadOnlyList<CharacterInShot> Characters,
    IReadOnlyList<PerformanceLine> Lines,
    PerformanceTimingRef TimingRef,
    PerformanceVisualRef VisualRef,
    PerformanceStoryRef StoryRef);

public sealed record PerformanceSnapshot(
    Guid PerformanceSnapshotId,
    string PerformanceHash,
    DateTimeOffset FrozenAt,
    string CreatedBy,
    PerformanceSourceRef Source,
    PerformanceDraft Draft,
    string PayloadJson);

public sealed record PerformanceIssue(
    string Code,
    string Path,
    string Message);

public sealed record PerformanceValidation(
    bool Ok,
    bool VoiceReady,
    IReadOnlyList<PerformanceIssue> Issues);

public sealed record ActingBeatSource(
    string? BeforeAction = null,
    string? BeforeBody = null,
    string? BeforeProp = null,
    string? BeforeGaze = null,
    string? BeforeRoom = null,
    string? DuringEmotion = null,
    int? DuringIntensity = null,
    string? AfterAction = null,
    string? AfterGaze = null);

public sealed record LineProjectionSource(
    string LineId,
    string CharacterId,
    string Text,
    string? Emotion = null,
    int? Intensity = null,
    string? Pace = null,
    string? Volume = null,
    double? PauseSec = null,
    string? Label = null,
    string? Emphasis = null,
    bool Locked = false);

public sealed record PerformanceProjectionSource(
    Guid BuildId,
    string ShotCode,
    string EpisodeCode,
    string SceneCode,
    IReadOnlyList<string> CharacterIds,
    IReadOnlyList<string> DialogueSegmentIds,
    IReadOnlyList<LineProjectionSource> Lines,
    ActingBeatSource? ActingBeat = null,
    string? GazeTarget = null,
    IReadOnlyList<string>? OffFrameCharacterIds = null,
    IReadOnlyList<string>? VoiceOnlyCharacterIds = null,
    IReadOnlyList<string>? PrimaryCharacterIds = null,
    IReadOnlyList<string>? SecondaryCharacterIds = null,
    IReadOnlyDictionary<string, string>? CharacterEmotionalState = null,
    string? TimingHash = null,
    string? VisualAuthorityId = null,
    string? VisualContractHash = null,
    string? StoryTextHash = null,
    string? BeatId = null);
