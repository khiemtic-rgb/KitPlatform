namespace KitPlatform.Packs.Content.Production;

public static class ProductionWorkIds
{
    public const string ArchitectureId = "FAMIXA_PRODUCTION_OS_WORK_EXECUTION_V1";
    public const string SuiteId = "FAMIXA_PRODUCTION_OS_WORK_EXECUTION_V1_REGRESSION";
    public const string SchemaVersion = "372";
}

public static class ProductionActions
{
    public const string CreateImage = "CREATE_IMAGE";
    public const string CreateVideo = "CREATE_VIDEO";
    public const string CreateVoice = "CREATE_VOICE";
    public const string CreateLipsync = "CREATE_LIPSYNC";
    public const string MixVideo = "MIX_VIDEO";
    public const string Accept = "ACCEPT";
    public const string Bind = "BIND";
    public const string Regenerate = "REGENERATE";
    public const string Assemble = "ASSEMBLE";
    public const string AssembleEpisode = "ASSEMBLE_EPISODE";

    public static readonly IReadOnlyList<string> All =
    [
        CreateImage, CreateVideo, CreateVoice, CreateLipsync,
        MixVideo, Accept, Bind, Regenerate, Assemble, AssembleEpisode,
    ];

    public static bool IsKnown(string? action) =>
        All.Contains((action ?? "").Trim(), StringComparer.Ordinal);
}

public static class ProductionOutputTypes
{
    public const string Image = "IMAGE";
    public const string VideoTake = "VIDEO_TAKE";
    public const string Voice = "VOICE";
    public const string Lipsync = "LIPSYNC";
    public const string Mix = "MIX";
    public const string Binding = "BINDING";
}

public static class ProductionBindingRoles
{
    public const string Current = "CURRENT";
    public const string Accepted = "ACCEPTED";
    public const string Visible = "VISIBLE";
    public const string EditorialSource = "EDITORIAL_SOURCE";
    public const string Final = "FINAL";

    public static readonly IReadOnlyList<string> All =
        [Current, Accepted, Visible, EditorialSource, Final];

    public static string CurrentFor(string artifactRole) => $"{Current}:{artifactRole}";
    public static string VisibleFor(string artifactRole) => $"{Visible}:{artifactRole}";
    public static string AcceptedFor(string artifactRole) => $"{Accepted}:{artifactRole}";

    public static bool IsKnown(string? role)
    {
        var r = (role ?? "").Trim();
        if (All.Contains(r)) return true;
        var i = r.IndexOf(':');
        if (i <= 0) return false;
        return All.Contains(r[..i]) && r[(i + 1)..].Length > 0;
    }
}

public static class ProductionArtifactRoles
{
    public const string Keyframe = "KEYFRAME";
    public const string VideoTake = "VIDEO_TAKE";
    public const string Voice = "VOICE";
    public const string Lipsync = "LIPSYNC";
    public const string Mix = "MIX";
    public const string Assembly = "ASSEMBLY";
}

public static class ProductionRequestStatuses
{
    public const string Draft = "DRAFT";
    public const string Submitted = "SUBMITTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public static class ProductionTaskStatuses
{
    public const string Created = "CREATED";
    public const string InputFrozen = "INPUT_FROZEN";
    public const string ProviderFrozen = "PROVIDER_FROZEN";
    public const string Running = "RUNNING";
    public const string Succeeded = "SUCCEEDED";
    public const string FailedIdentity = "FAILED_IDENTITY";
    public const string Failed = "FAILED";
    public const string Superseded = "SUPERSEDED";
}

public static class ProductionAttemptStatuses
{
    public const string Started = "STARTED";
    public const string ProviderSubmitted = "PROVIDER_SUBMITTED";
    public const string ProviderSucceeded = "PROVIDER_SUCCEEDED";
    public const string ProviderFailed = "PROVIDER_FAILED";
    public const string Late = "LATE";
}

public static class ProductionArtifactStatuses
{
    public const string Unverified = "UNVERIFIED";
    public const string Verified = "VERIFIED";
    public const string FailedVerification = "FAILED_VERIFICATION";
}

public static class ProductionEventTypes
{
    public const string RequestCreated = "REQUEST_CREATED";
    public const string TaskCreated = "TASK_CREATED";
    public const string InputFrozen = "INPUT_FROZEN";
    public const string AttemptStarted = "ATTEMPT_STARTED";
    public const string ProviderSubmitted = "PROVIDER_SUBMITTED";
    public const string ProviderSucceeded = "PROVIDER_SUCCEEDED";
    public const string PollStarted = "POLL_STARTED";
    public const string PollCompleted = "POLL_COMPLETED";
    public const string VideoTakeAcceptanceStarted = "VIDEO_TAKE_ACCEPTANCE_STARTED";
    public const string VideoTakeAccepted = "VIDEO_TAKE_ACCEPTED";
    public const string VideoTakeAcceptanceCompleted = "VIDEO_TAKE_ACCEPTANCE_COMPLETED";
    public const string ArtifactVerified = "ARTIFACT_VERIFIED";
    public const string ArtifactFailedVerification = "ARTIFACT_FAILED_VERIFICATION";
    public const string CurrentBound = "CURRENT_BOUND";
    public const string VisibleBound = "VISIBLE_BOUND";
    public const string AcceptedBound = "ACCEPTED_BOUND";
    public const string TaskFailed = "TASK_FAILED";
    public const string TaskSuperseded = "TASK_SUPERSEDED";
    public const string LateResultIgnored = "LATE_RESULT_IGNORED";
    public const string TaskStale = "TASK_STALE";
    public const string StageGate = "STAGE_GATE";
    public const string TestFixtureRegistered = "TEST_FIXTURE_REGISTERED";
    public const string ProviderFailed = "PROVIDER_FAILED";
    public const string ArtifactFailed = "ARTIFACT_FAILED";
    public const string AssemblyCreated = "ASSEMBLY_CREATED";
    public const string AssemblyRendered = "ASSEMBLY_RENDERED";
}

public static class ProductionFailureReasons
{
    public const string InputSnapshotMismatch = "INPUT_SNAPSHOT_MISMATCH";
    public const string StaleTask = "STALE_TASK";
    public const string StaleAttempt = "STALE_ATTEMPT";
    public const string ProviderChanged = "PROVIDER_CHANGED";
    public const string ModelChanged = "MODEL_CHANGED";
    public const string KeyframeChanged = "KEYFRAME_CHANGED";
    public const string StoryChanged = "STORY_CHANGED";
    public const string ActingChanged = "ACTING_CHANGED";
    public const string VoiceChanged = "VOICE_CHANGED";
    public const string MotionChanged = "MOTION_CHANGED";
    public const string BindingChanged = "BINDING_CHANGED";
    public const string ActionRemapForbidden = "ACTION_REMAP_FORBIDDEN";
    public const string FailedIdentity = "FAILED_IDENTITY";
    public const string CrossShotForbidden = "CROSS_SHOT_FORBIDDEN";
    public const string CrossScopeForbidden = "CROSS_SCOPE_FORBIDDEN";
    public const string UnknownScope = "UNKNOWN_SCOPE";
    public const string TestScopeRequired = "TEST_SCOPE_REQUIRED";
    public const string TestShotMissing = "TEST_SHOT_MISSING";
    public const string TestRunMismatch = "TEST_RUN_MISMATCH";
    public const string FixtureNotFound = "FIXTURE_NOT_FOUND";
    public const string FixtureHashMismatch = "FIXTURE_HASH_MISMATCH";
    public const string FixtureMimeInvalid = "FIXTURE_MIME_INVALID";
    public const string FixtureDimensionsInvalid = "FIXTURE_DIMENSIONS_INVALID";
    public const string FixtureBytesChanged = "FIXTURE_BYTES_CHANGED";
    public const string VisibleMissing = "VISIBLE_MISSING";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
    /// <summary>
    /// Selected local execution target was invoked and the process failed before a valid official output.
    /// Not an AI-provider outage and not a byte-identity failure.
    /// </summary>
    public const string ExecutionProcessFailed = "EXECUTION_PROCESS_FAILED";
    public const string HiddenFallbackForbidden = "HIDDEN_FALLBACK_FORBIDDEN";
    public const string UrlAuthorityForbidden = "URL_AUTHORITY_FORBIDDEN";
    public const string LateResultNotCurrent = "LATE_RESULT_NOT_CURRENT";
    public const string MissingDependency = "MISSING_DEPENDENCY";
    public const string MissingVideoArtifact = "MISSING_VIDEO_ARTIFACT";
    public const string MissingVoiceArtifact = "MISSING_VOICE_ARTIFACT";
    public const string MissingLipsyncArtifact = "MISSING_LIPSYNC_ARTIFACT";
    public const string OutputClockUnresolved = "OUTPUT_CLOCK_UNRESOLVED";
    public const string InvalidRange = "INVALID_RANGE";
    public const string PictureGap = "PICTURE_GAP";
    public const string PictureOverlap = "PICTURE_OVERLAP";
    public const string AudioGap = "AUDIO_GAP";
    public const string AudioOverlap = "AUDIO_OVERLAP";
    public const string DurationMismatch = "DURATION_MISMATCH";
    public const string UnsupportedCompositionMode = "UNSUPPORTED_COMPOSITION_MODE";
    public const string ContractVersionMismatch = "CONTRACT_VERSION_MISMATCH";
    public const string MissingTimeline = "MISSING_TIMELINE";
    public const string HashChanged = "HASH_CHANGED";
    public const string MissingKeyframe = "MISSING_KEYFRAME";
    public const string MissingStory = "MISSING_STORY";
    public const string MissingActingBeat = "MISSING_ACTING_BEAT";
    public const string MissingPerformanceSnapshot = "MISSING_PERFORMANCE_SNAPSHOT";
    public const string PerformanceLineMismatch = "PERFORMANCE_LINE_MISMATCH";
    public const string PerformanceHashMismatch = "PERFORMANCE_HASH_MISMATCH";
    public const string MissingVisualContract = "MISSING_VISUAL_CONTRACT";
    public const string MissingTiming = "MISSING_TIMING";
    public const string TimingProductionDecisionRequired = "TIMING_PRODUCTION_DECISION_REQUIRED";
    public const string VoiceProductionDecisionRequired = "VOICE_PRODUCTION_DECISION_REQUIRED";
    public const string VoiceIdProductionDecisionRequired = "VOICE_ID_PRODUCTION_DECISION_REQUIRED";
    public const string CanonicalVoiceInputNotBound = "CANONICAL_VOICE_INPUT_NOT_BOUND";
    public const string VoiceContractMissing = "VOICE_CONTRACT_MISSING";
    public const string MissingProviderModel = "MISSING_PROVIDER_MODEL";
    public const string MissingProductionAiSelection = "MISSING_PRODUCTION_AI_SELECTION";
    public const string SelectionConflict = "SELECTION_CONFLICT";
    public const string PermitConflict = "PERMIT_CONFLICT";
    public const string InputMutated = "INPUT_MUTATED";
    public const string SpeakerTargetAmbiguous = "SPEAKER_TARGET_AMBIGUOUS";
    public const string LipsyncTargetUnresolved = "LIPSYNC_TARGET_UNRESOLVED";
    public const string UnknownProvider = "UNKNOWN_PROVIDER";
    public const string ProviderModelMismatch = "PROVIDER_MODEL_MISMATCH";
    public const string CapabilityUnsupported = "CAPABILITY_UNSUPPORTED";
    public const string ProviderDisabled = "PROVIDER_DISABLED";
    public const string UnknownCost = "UNKNOWN_COST";
    public const string CostPolicySourceMissing = "COST_POLICY_SOURCE_MISSING";
    public const string OverBudget = "OVER_BUDGET";
    public const string PaidStageExhausted = "PAID_STAGE_ATTEMPTS_EXHAUSTED";
    public const string PaidStageUnchanged = "PAID_STAGE_INPUT_UNCHANGED";
    public const string PaidNetworkForbidden = "PAID_NETWORK_FORBIDDEN";
    public const string PaidNetworkAuthorized = "PAID_NETWORK_AUTHORIZED";
    public const string FailedVerification = "FAILED_VERIFICATION";
    public const string OutputDownloadFailed = "OUTPUT_DOWNLOAD_FAILED";
    public const string ProviderOutputMissing = "PROVIDER_OUTPUT_MISSING";
    public const string ProviderOutputUnsupported = "PROVIDER_OUTPUT_UNSUPPORTED";
    public const string InvalidProviderResponse = "INVALID_PROVIDER_RESPONSE";
    public const string InFlightReplay = "IN_FLIGHT_REPLAY";
    public const string RecoveryFailed = "RECOVERY_FAILED";
    public const string VertexExecutionClosed = "VERTEX_EXECUTION_CLOSED";
    public const string AuthenticatorNotReady = "AUTHENTICATOR_NOT_READY";
    public const string AuthenticatorReady = "AUTHENTICATOR_READY";
    public const string HttpCompositionUnavailable = "HTTP_COMPOSITION_UNAVAILABLE";
    public const string HttpCompositionReady = "HTTP_COMPOSITION_READY";
    public const string ExecutionAuthorizationRequired = "EXECUTION_AUTHORIZATION_REQUIRED";
    public const string ExecutionAuthorized = "EXECUTION_AUTHORIZED";
    public const string ProviderResolverReady = "PROVIDER_RESOLVER_READY";
    public const string PermitExpired = "PERMIT_EXPIRED";
    public const string InputNotFrozen = "INPUT_NOT_FROZEN";
    public const string KeyframeBytesMissing = "KEYFRAME_BYTES_MISSING";
    public const string StartOutcomeUncertain = "START_OUTCOME_UNCERTAIN";
    public const string StartOutcomeUnknown = "START_OUTCOME_UNKNOWN";
    public const string StartNotSent = "START_NOT_SENT";
    public const string ExecutionPermitConsumptionUnsafe = "EXECUTION_PERMIT_CONSUMPTION_UNSAFE";
    public const string StartReplay = "START_REPLAY";
    public const string PollTimeout = "POLL_TIMEOUT";
    public const string FreezeMismatch = "FREEZE_MISMATCH";
    public const string LiveOfficialContractBound = "LIVE_OFFICIAL_CONTRACT_BOUND";
    public const string LiveOfficialSnapshotNotBound = "LIVE_OFFICIAL_SNAPSHOT_NOT_BOUND";
    public const string LiveOfficialPermitNotBound = "LIVE_OFFICIAL_PERMIT_NOT_BOUND";
    public const string LiveOfficialPermitAmbiguous = "LIVE_OFFICIAL_PERMIT_AMBIGUOUS";
    public const string LiveOfficialPermitSnapshotMismatch = "LIVE_OFFICIAL_PERMIT_SNAPSHOT_MISMATCH";
    public const string LiveOfficialContractMaterialized = "LIVE_OFFICIAL_CONTRACT_MATERIALIZED";
    public const string SnapshotMaterializationFailed = "SNAPSHOT_MATERIALIZATION_FAILED";
    public const string SnapshotVerificationFailed = "SNAPSHOT_VERIFICATION_FAILED";
    public const string PermitMaterializationFailed = "PERMIT_MATERIALIZATION_FAILED";
    public const string PermitVerificationFailed = "PERMIT_VERIFICATION_FAILED";
    public const string AuthorizationContractMismatch = "AUTHORIZATION_CONTRACT_MISMATCH";
    public const string AmbiguousLiveState = "AMBIGUOUS_LIVE_STATE";
    public const string DuplicateProviderStart = "DUPLICATE_PROVIDER_START";
    public const string UnauthorizedVertexTarget = "UNAUTHORIZED_VERTEX_TARGET";
    public const string OutputArtifactNotFound = "OUTPUT_ARTIFACT_NOT_FOUND";
    public const string OutputArtifactAmbiguous = "OUTPUT_ARTIFACT_AMBIGUOUS";
    public const string ArtifactHashMismatch = "ARTIFACT_HASH_MISMATCH";
    public const string ArtifactLengthMismatch = "ARTIFACT_LENGTH_MISMATCH";
    public const string ArtifactMimeMismatch = "ARTIFACT_MIME_MISMATCH";
    public const string ArtifactRoleMismatch = "ARTIFACT_ROLE_MISMATCH";
    public const string ArtifactVerificationFailed = "ARTIFACT_VERIFICATION_FAILED";
    public const string OutputTaskMismatch = "OUTPUT_TASK_MISMATCH";
    public const string OutputAttemptMismatch = "OUTPUT_ATTEMPT_MISMATCH";
    public const string ShotOutputBindingInvalid = "SHOT_OUTPUT_BINDING_INVALID";
    public const string DurationBindingMismatch = "DURATION_BINDING_MISMATCH";
    public const string OperationProvenanceMissing = "OPERATION_PROVENANCE_MISSING";
    public const string OperationProvenanceMismatch = "OPERATION_PROVENANCE_MISMATCH";
    public const string StartForbidden = "START_FORBIDDEN";
    public const string PollForbidden = "POLL_FORBIDDEN";
    public const string PermitMutationForbidden = "PERMIT_MUTATION_FORBIDDEN";
    public const string SnapshotMutationForbidden = "SNAPSHOT_MUTATION_FORBIDDEN";
    public const string SelectionMutationForbidden = "SELECTION_MUTATION_FORBIDDEN";

    /// <summary>Retired. Never persist this as a production state.</summary>
    public const string LegacyInputChanged = "INPUT_CHANGED";

    /// <summary>The joined Runway prompt exceeds 1000 characters. Nothing was sent.</summary>
    public const string RunwayPromptOverCap = "RUNWAY_PROMPT_OVER_CAP";
}
