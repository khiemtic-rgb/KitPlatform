using System.Collections.Concurrent;
using System.Text.Json;

namespace KitPlatform.Packs.Content.Production;

public static class OfficialVeoStartOutcomes
{
    public const string InFlight = "IN_FLIGHT";
    public const string Started = "STARTED";
    public const string Running = "RUNNING";
    public const string Polling = "POLLING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string NotSent = "NOT_SENT";
    public const string Unknown = "UNKNOWN";
    public const string Replay = "REPLAY";
    public const string Blocked = "BLOCKED";
}

public sealed record OfficialVeoStartClaim(
    string Key,
    string AuthorizationHash,
    Guid PermitId,
    Guid AttemptId,
    string RequestPayloadHash,
    string Outcome,
    string? ProviderOperationName,
    string? EncodedOperationId,
    int? HttpStatus,
    string? ErrorCode,
    DateTimeOffset ClaimedAt,
    DateTimeOffset? CompletedAt);

public sealed record OfficialVeoStartClaimResult(
    bool Won,
    bool Replay,
    OfficialVeoStartClaim Claim,
    string? BlockReason);

/// <summary>
/// Durable one-shot Start claim. Not an in-memory boolean.
/// Keyed by authorizationHash + permitId. Second claim cannot Start.
/// </summary>
public sealed class OfficialVeoStartLedger
{
    readonly object _gate = new();
    readonly ConcurrentDictionary<string, OfficialVeoStartClaim> _claims = new(StringComparer.Ordinal);
    readonly string? _path;

    public OfficialVeoStartLedger(string? persistPath = null)
    {
        _path = string.IsNullOrWhiteSpace(persistPath) ? null : persistPath.Trim();
        Load();
    }

    public int Count
    {
        get { lock (_gate) return _claims.Count; }
    }

    public OfficialVeoStartClaim? Get(string authorizationHash, Guid permitId)
    {
        lock (_gate)
            return _claims.TryGetValue(KeyOf(authorizationHash, permitId), out var claim) ? claim : null;
    }

    public OfficialVeoStartClaimResult TryClaim(string authorizationHash, Guid permitId, string requestPayloadHash)
    {
        var key = KeyOf(authorizationHash, permitId);
        lock (_gate)
        {
            if (_claims.TryGetValue(key, out var existing))
            {
                if (string.Equals(existing.Outcome, OfficialVeoStartOutcomes.Started, StringComparison.Ordinal)
                    || string.Equals(existing.Outcome, OfficialVeoStartOutcomes.Running, StringComparison.Ordinal)
                    || !string.IsNullOrWhiteSpace(existing.ProviderOperationName))
                {
                    return new OfficialVeoStartClaimResult(false, true, existing, ProductionFailureReasons.StartReplay);
                }

                if (existing.Outcome == OfficialVeoStartOutcomes.Unknown)
                {
                    return new OfficialVeoStartClaimResult(
                        false, false, existing, ProductionFailureReasons.StartOutcomeUnknown);
                }

                // Proven pre-HTTP only: NOT_SENT, or IN_FLIGHT before Attempt bind.
                // IN_FLIGHT + AttemptId set is treated as possibly transmitted — no rematch.
                if (!CanReclaimUnsent(existing))
                {
                    return new OfficialVeoStartClaimResult(
                        false, false, existing, ProductionFailureReasons.DuplicateProviderStart);
                }
            }

            var claim = new OfficialVeoStartClaim(
                key,
                authorizationHash,
                permitId,
                Guid.Empty,
                requestPayloadHash,
                OfficialVeoStartOutcomes.InFlight,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow,
                null);
            _claims[key] = claim;
            Persist();
            return new OfficialVeoStartClaimResult(true, false, claim, null);
        }
    }

    public OfficialVeoStartClaim BindAttempt(string authorizationHash, Guid permitId, Guid attemptId)
    {
        lock (_gate)
        {
            var key = KeyOf(authorizationHash, permitId);
            if (!_claims.TryGetValue(key, out var claim))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            claim = claim with { AttemptId = attemptId };
            _claims[key] = claim;
            Persist();
            return claim;
        }
    }

    public OfficialVeoStartClaim Complete(
        string authorizationHash,
        Guid permitId,
        string outcome,
        string? operationName,
        string? encodedId,
        int? httpStatus,
        string? errorCode)
    {
        lock (_gate)
        {
            var key = KeyOf(authorizationHash, permitId);
            if (!_claims.TryGetValue(key, out var claim))
                throw new InvalidOperationException(ProductionFailureReasons.FailedIdentity);
            claim = claim with
            {
                Outcome = outcome,
                ProviderOperationName = operationName,
                EncodedOperationId = encodedId,
                HttpStatus = httpStatus,
                ErrorCode = errorCode,
                CompletedAt = DateTimeOffset.UtcNow,
            };
            _claims[key] = claim;
            Persist();
            return claim;
        }
    }

    static bool CanReclaimUnsent(OfficialVeoStartClaim existing) =>
        existing.Outcome == OfficialVeoStartOutcomes.NotSent
        || (existing.Outcome == OfficialVeoStartOutcomes.InFlight
            && existing.AttemptId == Guid.Empty
            && string.IsNullOrWhiteSpace(existing.ProviderOperationName));

    static string KeyOf(string authorizationHash, Guid permitId) =>
        RealVertexExecutionV1Lock.BuildId.ToString("D")
        + "|" + RealVertexExecutionV1Lock.ShotCode
        + "|" + (authorizationHash ?? "").Trim()
        + "|" + permitId.ToString("D");

    void Persist()
    {
        if (_path is null) return;
        var json = JsonSerializer.Serialize(_claims.Values.ToArray());
        File.WriteAllText(_path, json);
    }

    void Load()
    {
        if (_path is null || !File.Exists(_path)) return;
        var json = File.ReadAllText(_path);
        var rows = JsonSerializer.Deserialize<OfficialVeoStartClaim[]>(json) ?? [];
        foreach (var row in rows)
            _claims[row.Key] = row;
    }
}
