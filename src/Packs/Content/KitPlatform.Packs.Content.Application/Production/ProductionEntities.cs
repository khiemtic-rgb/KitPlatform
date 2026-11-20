namespace KitPlatform.Packs.Content.Production;

public sealed record ProductionScope(
    Guid BuildId,
    string EpisodeCode,
    string SceneCode,
    string ShotCode);

public sealed record ProductionRequest(
    Guid RequestId,
    ProductionScope Scope,
    string UserAction,
    string RequiredOutputType,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    string Status,
    Guid? ParentRequestId,
    string IdempotencyKey,
    IReadOnlyList<string> ForbiddenFallbacks);

public sealed record ProductionTask(
    Guid TaskId,
    Guid RequestId,
    Guid? ParentTaskId,
    string Action,
    string TargetType,
    string TargetId,
    ProductionScope Scope,
    Guid? InputSnapshotId,
    string? ProviderId,
    string? ModelId,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    string? FailureReason);

public sealed record ProductionTaskInputSnapshot(
    Guid SnapshotId,
    Guid TaskId,
    string Story,
    string ActingBeat,
    string VisualContract,
    string ShotContract,
    string Prompt,
    string CompilerOutput,
    string StoryHash,
    string ActingHash,
    string VisualHash,
    string CompilerHash,
    string KeyframeHash,
    string VoiceHash,
    string MotionHash,
    string LipsyncSourceHash,
    string TimingHash,
    string ProviderId,
    string ModelId,
    string SnapshotHash,
    DateTimeOffset FrozenAt,
    Guid? KeyframeArtifactId = null,
    string AspectRatio = "16:9",
    decimal DurationSec = 0,
    string CompilerVersion = ShotExecutionCompiler.Version,
    IReadOnlyList<Guid>? ParentArtifactIds = null,
    Guid? PerformanceSnapshotId = null,
    string? PerformanceHash = null,
    string? PerformancePayloadJson = null,
    Guid? CredentialId = null,
    Guid? CredentialVersionId = null);

public sealed record ProductionAttempt(
    Guid AttemptId,
    Guid TaskId,
    int AttemptNumber,
    string ProviderId,
    string ModelId,
    string? ProviderJobId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string RequestPayloadHash,
    string? ResponsePayloadHash,
    string? ErrorCode);

public sealed record ProductionArtifact(
    Guid ArtifactId,
    Guid TaskId,
    Guid AttemptId,
    string Role,
    string MimeType,
    long SizeBytes,
    decimal DurationSec,
    string ByteSha256,
    string StorageKey,
    string? ProviderJobId,
    DateTimeOffset CreatedAt,
    string VerificationStatus,
    string InputSnapshotHash,
    IReadOnlyList<Guid>? ParentArtifactIds = null);

public sealed record ProductionBinding(
    Guid BindingId,
    Guid BuildId,
    string ShotCode,
    string Role,
    Guid ArtifactId,
    Guid BoundByTaskId,
    DateTimeOffset BoundAt,
    DateTimeOffset? UnboundAt);

public sealed record ProductionTaskEvent(
    Guid EventId,
    string EventType,
    DateTimeOffset Timestamp,
    Guid? RequestId,
    Guid? TaskId,
    Guid? AttemptId,
    Guid? ArtifactId,
    string ShotCode,
    string Action,
    string? PayloadJson);

public sealed record FilmAssembly(
    Guid AssemblyId,
    Guid BuildId,
    string Title,
    DateTimeOffset CreatedAt)
{
    /// <summary>Dapper/Npgsql TIMESTAMPTZ arrives as DateTime. Keep DateTimeOffset on the domain model.</summary>
    public FilmAssembly(Guid assemblyid, Guid buildid, string title, DateTime createdat)
        : this(
            assemblyid,
            buildid,
            title,
            createdat.Kind == DateTimeKind.Unspecified
                ? new DateTimeOffset(DateTime.SpecifyKind(createdat, DateTimeKind.Utc))
                : new DateTimeOffset(createdat))
    {
    }
}

public sealed record FilmAssemblyShot(
    Guid AssemblyShotId,
    Guid AssemblyId,
    Guid ShotBuildId,
    string ShotCode,
    Guid ArtifactId,
    int Order,
    decimal DurationSec,
    string Transition,
    string AudioState);

public sealed record ProductionActionAvailability(
    string Action,
    string UiCta,
    bool Available,
    string? BlockReason,
    IReadOnlyList<string>? RequiredInputs = null,
    IReadOnlyList<string>? MissingInputs = null);

public sealed record ShotNodeState(
    string Node,
    string Status,
    Guid? ArtifactId,
    string? ByteSha256,
    string? Reason);

public sealed record ShotProductionState(
    ProductionScope Scope,
    bool StoryPresent,
    Guid? VisibleArtifactId,
    Guid? CurrentArtifactId,
    Guid? AcceptedArtifactId,
    IReadOnlyList<ShotNodeState> Nodes,
    IReadOnlyList<ProductionActionAvailability> Actions,
    Guid? VideoArtifactId = null,
    Guid? VoiceArtifactId = null,
    Guid? LipsyncArtifactId = null,
    string? PaidStageLedger = null);

public static class ShotNodeIds
{
    public const string Story = "STORY";
    public const string Acting = "ACTING";
    public const string Keyframe = "KEYFRAME";
    public const string Motion = "MOTION";
    public const string Voice = "VOICE";
    public const string Lipsync = "LIPSYNC";
    public const string Mix = "MIX";
    public const string Accepted = "ACCEPTED";
    public const string Assembly = "ASSEMBLY";
}

public static class ShotNodeStatuses
{
    public const string Missing = "MISSING";
    public const string Available = "AVAILABLE";
    public const string Current = "CURRENT";
    public const string Visible = "VISIBLE";
    public const string Accepted = "ACCEPTED";
    public const string Stale = "STALE";
}
