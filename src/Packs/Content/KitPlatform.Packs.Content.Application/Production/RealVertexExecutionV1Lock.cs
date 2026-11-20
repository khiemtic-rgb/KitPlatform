using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Checkpoint 12 one-job lock. Only EP01-SC01-SH02 / google / veo-3.1.
/// StartCount is process-wide and fail-closed at 2.
/// </summary>
public static class RealVertexExecutionV1Lock
{
    public static readonly Guid BuildId = ProductionScopeAnchorRules.OfficialEp01;
    public const string EpisodeCode = "EP01";
    public const string SceneCode = "SC01";
    public const string ShotCode = "EP01-SC01-SH02";
    public const string ProviderId = FamixaProviderIds.Google;
    public const string ModelId = ProductionMotionCatalog.Veo31ModelId;
    public const string VertexModel = VeoVertexEndpoints.VertexModel;
    public const int MaxStarts = 1;

    static int _starts;

    public static int StartCount => Volatile.Read(ref _starts);

    public static ProductionScope OfficialScope =>
        new(BuildId, EpisodeCode, SceneCode, ShotCode);

    public static bool IsAuthorizedTarget(ProductionScope scope, string? providerId, string? modelId) =>
        scope.BuildId == BuildId
        && string.Equals(scope.ShotCode, ShotCode, StringComparison.Ordinal)
        && string.Equals((providerId ?? "").Trim(), ProviderId, StringComparison.OrdinalIgnoreCase)
        && string.Equals((modelId ?? "").Trim(), ModelId, StringComparison.OrdinalIgnoreCase);

    public static void RequireAuthorizedTarget(ProductionScope scope, string? providerId, string? modelId)
    {
        if (!IsAuthorizedTarget(scope, providerId, modelId))
            throw new InvalidOperationException(ProductionFailureReasons.UnauthorizedVertexTarget);
    }

    public static int RecordStart()
    {
        var next = Interlocked.Increment(ref _starts);
        if (next > MaxStarts)
        {
            Interlocked.Decrement(ref _starts);
            throw new InvalidOperationException(ProductionFailureReasons.DuplicateProviderStart);
        }
        return next;
    }

    public static void ResetForTests() => Interlocked.Exchange(ref _starts, 0);
}

public sealed record RealVertexExecutionV1Result(
    bool Executed,
    RealVertexExecutionPreflightResult Preflight,
    int StartCount,
    int HttpCallCount,
    int PermitConsumeCalls,
    string? Outcome);
