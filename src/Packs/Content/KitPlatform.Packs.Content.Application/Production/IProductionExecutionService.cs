namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// The only production orchestrator. Provider adapters execute HOW; they cannot bind or remap.
/// </summary>
public interface IProductionExecutionService
{
    Task<ProductionRequest> CreateRequestAsync(ProductionScope scope, string userAction, string createdBy, string? idempotencyKey = null, Guid? parentRequestId = null, CancellationToken ct = default);
    Task<ProductionTask> CreateTaskAsync(Guid requestId, CancellationToken ct = default);
    Task<ProductionTaskInputSnapshot> FreezeInputAsync(Guid taskId, ShotExecutionInput input, string providerId, string modelId, CancellationToken ct = default, ProductionCredentialReference? credential = null, bool requireCreateVideoDependencies = true);
    Task<ProductionAttempt> StartAttemptAsync(Guid taskId, string requestPayloadHash, CancellationToken ct = default);
    Task<ProductionAttempt> BeginOfficialProviderStartAsync(Guid taskId, string requestPayloadHash, Guid? reuseAttemptId = null, CancellationToken ct = default);
    Task<ProductionAttempt> SubmitProviderJobAsync(Guid attemptId, string providerJobId, CancellationToken ct = default);
    Task<ProductionAttempt> CompleteAttemptAsync(Guid attemptId, string responsePayloadHash, CancellationToken ct = default);
    Task<ProductionAttempt> FailAttemptAsync(Guid attemptId, string errorCode, CancellationToken ct = default);
    Task<ProductionAttempt> RecordExecutionProcessFailedAsync(Guid attemptId, string detail, CancellationToken ct = default);
    Task<ProductionArtifact> VerifyArtifactAsync(Guid attemptId, byte[] bytes, string mime, decimal durationSec, string storageKey, CancellationToken ct = default);
    Task<ProductionBinding> BindCurrentAsync(Guid artifactId, CancellationToken ct = default);
    Task<ProductionBinding> BindVisibleAsync(Guid artifactId, CancellationToken ct = default);
    Task<ProductionBinding> AcceptArtifactAsync(Guid artifactId, CancellationToken ct = default);
    Task<ProductionBinding> BindNamedAsync(Guid artifactId, string role, CancellationToken ct = default);
    Task<ProductionBinding> BindCurrentProtectedAsync(Guid artifactId, CancellationToken ct = default);
    Task<ProductionBinding> BindVisibleProtectedAsync(Guid artifactId, CancellationToken ct = default);
    Task<ProductionBinding> BindNamedProtectedAsync(Guid artifactId, string role, CancellationToken ct = default);
    Task CompleteDecisionAsync(Guid attemptId, CancellationToken ct = default);
    Task<ProductionTask> CreateChildTaskAsync(Guid parentRequestId, string childAction, string createdBy, CancellationToken ct = default);
    Task InvalidateDownstreamAsync(ProductionScope scope, string changedNode, CancellationToken ct = default);
    Task<FilmAssembly> CreateAssemblyAsync(Guid buildId, string title, IReadOnlyList<(string ShotCode, Guid ArtifactId, decimal Duration)> shots, CancellationToken ct = default, int sortOrigin = 0);
    Task<CreateVideoContractVerdict> EvaluateCreateVideoAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<ProductionAiSelection?> GetActiveVideoSelectionAsync(ProductionScope scope, CancellationToken ct = default);
    Task<ProductionActionInputVerdict> EvaluateCreateVoiceAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default, bool requireIssuedPermit = true);
    Task<ProductionActionInputVerdict> EvaluateCreateLipsyncAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<ProductionActionInputVerdict> EvaluateCreateMixAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<ShotProductionState> GetShotStateAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProductionActionAvailability>> GetAvailableActionsAsync(ProductionScope scope, CreateVideoReadinessClaim? claim = null, CancellationToken ct = default);
    Task<ProductionPreview> GetPreviewAsync(ProductionScope scope, CancellationToken ct = default);
    Task<AuthoritativeVideoTake?> GetAuthoritativeVideoTakeAsync(ProductionScope scope, CancellationToken ct = default);
    Task<ProductionVoiceResolution> GetCurrentVoiceAsync(ProductionScope scope, string lineId, CancellationToken ct = default);
    Task<CreateVideoSliceResult> ExecuteCreateVideoFixtureAsync(CreateVideoSliceCommand command, CancellationToken ct = default);
    IReadOnlyList<string> RunRegression();
}

public sealed record CreateVideoSliceCommand(
    ProductionScope Scope,
    ShotExecutionInput Input,
    string CreatedBy,
    string IdempotencyKey,
    bool BindCurrent,
    bool BindVisible);

public sealed record CreateVideoSliceResult(
    ProductionRequest Request,
    ProductionTask Task,
    ProductionAttempt Attempt,
    ProductionArtifact Artifact,
    ProductionBinding? Current,
    ProductionBinding? Visible);
