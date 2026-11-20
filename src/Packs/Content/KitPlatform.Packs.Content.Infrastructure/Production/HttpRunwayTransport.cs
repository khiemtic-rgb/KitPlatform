using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Real Runway HTTP. Official film and tenant-owned EP01 require a consumed execution permit.
/// Every other scope still requires the process-local TEST paid-network permit.
/// </summary>
internal sealed class HttpRunwayTransport : IRunwayTransport
{
    readonly ContentRunwayClient _runway;
    readonly IProductionPaidNetworkPermit _permit;
    readonly IProductionExecutionPermitStore _officialPermits;
    readonly ProductionRunwayHttpAudit _audit;
    readonly IProductionCredentialResolver _credentials;

    public HttpRunwayTransport(
        ContentRunwayClient runway,
        IProductionPaidNetworkPermit permit,
        IProductionExecutionPermitStore officialPermits,
        ProductionRunwayHttpAudit audit,
        IProductionCredentialResolver credentials)
    {
        _runway = runway;
        _permit = permit;
        _officialPermits = officialPermits;
        _audit = audit;
        _credentials = credentials;
    }

    public bool RequiresNetwork => true;
    public int HttpCallCount => StartCount + PollCount + DownloadCount;
    public int StartCount { get; private set; }
    public int PollCount { get; private set; }
    public int DownloadCount { get; private set; }
    public string? LastProviderJobId { get; private set; }
    public string? LastOutputUrl { get; private set; }

    public async Task<RunwayTransportCreateResult> CreateImageToVideoAsync(
        RunwayTransportCreateRequest request, CancellationToken ct = default)
    {
        var scope = request.Scope ?? throw new InvalidOperationException(ProductionFailureReasons.UnknownScope);
        var shot = request.ShotCode ?? scope.ShotCode;
        if (ProductionPaidNetworkGate.TransportUsesExecutionPermit(scope.BuildId, scope.EpisodeCode))
        {
            var latest = await _officialPermits.GetLatestAsync(
                scope.BuildId, shot, ProductionActions.CreateVideo, FamixaProviderIds.Runway, request.ModelId, ct);
            if (latest is null
                || !ProductionExecutionPermitCanon.MatchesConsumed(
                    latest, scope.BuildId, shot, ProductionActions.CreateVideo, FamixaProviderIds.Runway, request.ModelId))
                throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        }
        else if (!_permit.TryConsumeStart(scope, shot, FamixaProviderIds.Runway, request.ModelId))
            throw new InvalidOperationException(ProductionFailureReasons.PaidNetworkForbidden);
        if (request.KeyframeBytes is null || request.KeyframeBytes.Length < 32)
            throw new InvalidOperationException(ProductionFailureReasons.MissingKeyframe);
        if (!string.Equals(request.ModelId, ProductionMotionCatalog.RunwayGen4Turbo, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);

        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, request.Credential, FamixaProviderIds.Runway, request.ModelId, ct);

        var mime = string.IsNullOrWhiteSpace(request.KeyframeMime) ? "image/png" : request.KeyframeMime;
        var dataUri = $"data:{mime};base64,{Convert.ToBase64String(request.KeyframeBytes)}";
        var duration = request.DurationSec >= 8 ? 10 : 5;
        var ratio = MapRatio(request.AspectRatio);
        StartCount++;
        _audit.StartCount++;
        var jobId = await _runway.CreateProductionImageToVideoAsync(
            dataUri, request.PromptText ?? "", (int)duration, ratio, ct, material.Secret);
        if (string.IsNullOrWhiteSpace(jobId)
            || jobId.StartsWith(DryRunRunwayTransport.JobPrefix, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderUnavailable);
        LastProviderJobId = jobId;
        _audit.LastProviderJobId = jobId;
        return new RunwayTransportCreateResult(jobId);
    }

    public Task<RunwayTransportTaskResult> GetTaskAsync(string providerJobId, CancellationToken ct = default)
    {
        _ = providerJobId;
        _ = ct;
        throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
    }

    public async Task<RunwayTransportTaskResult> GetTaskAsync(
        string providerJobId, ProductionCredentialReference credential, string modelId, CancellationToken ct = default)
    {
        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, credential, FamixaProviderIds.Runway, modelId, ct);
        PollCount++;
        _audit.PollCount++;
        var task = await _runway.GetProductionTaskAsync(providerJobId, ct, material.Secret);
        LastOutputUrl = task.VideoUrl;
        return new RunwayTransportTaskResult(providerJobId, task.Status, task.Error, task.FailureCode);
    }

    public Task<byte[]> DownloadOutputAsync(string providerJobId, CancellationToken ct = default)
    {
        _ = providerJobId;
        _ = ct;
        throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
    }

    public async Task<byte[]> DownloadOutputAsync(
        string providerJobId, ProductionCredentialReference credential, string modelId, CancellationToken ct = default)
    {
        var material = await ProductionCredentialResolver.RequireAsync(
            _credentials, credential, FamixaProviderIds.Runway, modelId, ct);
        if (string.IsNullOrWhiteSpace(LastOutputUrl))
        {
            var task = await GetTaskAsync(providerJobId, credential, modelId, ct);
            if (!string.Equals(task.Status, MotionPollStatuses.Succeeded, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(LastOutputUrl))
                throw new InvalidOperationException(task.FailureReason ?? ProductionFailureReasons.FailedIdentity);
        }
        if (material.CredentialId != credential.CredentialId
            || material.CredentialVersionId != credential.CredentialVersionId)
            throw new InvalidOperationException(ProductionCredentialResolver.Unresolved);
        DownloadCount++;
        _audit.DownloadCount++;
        return await _runway.DownloadOutputAsync(LastOutputUrl!, ct);
    }

    static string MapRatio(string? aspect) =>
        (aspect ?? "").Contains("16:9", StringComparison.OrdinalIgnoreCase) ? "1280:720" : "1280:720";
}
