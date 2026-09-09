namespace KitPlatform.Packs.Content;

/// <summary>
/// Execution provenance is evidence, not lifecycle authority.
/// Reuses existing attemptId / vendor ProviderRequestId. No invented IDs.
/// </summary>
public enum FamixaProvenanceClass
{
    Verified,
    Partial,
    LegacyUnverified,
}

public sealed record FamixaSelectionSnapshot(
    string DecisionId,
    string ProviderId,
    string? ModelId,
    FamixaProviderSelectionMode SelectionMode,
    string? Reason = null,
    decimal? EstimatedCost = null,
    FamixaCostKind? CostKind = null);

public sealed record FamixaExecutionProvenance(
    string AttemptId,
    FamixaProviderCapability Capability,
    string? DecisionId = null,
    string? ShotId = null,
    string? ProviderId = null,
    string? ModelId = null,
    FamixaProviderSelectionMode? SelectionMode = null,
    string? SelectionReason = null,
    string? PictureRevisionId = null,
    string? PicturePixelHash = null,
    string? MotionContentFingerprint = null,
    string? ActingBeatFingerprint = null,
    string? TimingHash = null,
    string? ProviderRequestId = null,
    string? ProviderTaskId = null,
    string? ArtifactId = null,
    DateTimeOffset? CreatedAt = null,
    FamixaProvenanceClass Class = FamixaProvenanceClass.Partial);

public sealed record FamixaExecutionLineage(
    FamixaExecutionProvenance Provenance,
    FamixaSelectionSnapshot? Decision,
    string? WhatFingerprint,
    string? HowProviderId,
    string? HowModelId);

public static class FamixaExecutionProvenanceRules
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_EXECUTION_PROVENANCE_V1";

    public static FamixaSelectionSnapshot Snapshot(FamixaProviderSelectionDecision decision) =>
        new(
            decision.DecisionId,
            decision.ProviderId,
            decision.ModelId,
            decision.SelectionMode,
            decision.Reason,
            decision.EstimatedCost,
            decision.CostKind);

    public static FamixaProvenanceClass Classify(
        string? attemptId,
        string? decisionId,
        string? providerId,
        string? providerRequestId)
    {
        var attempt = !string.IsNullOrWhiteSpace(attemptId);
        var decision = !string.IsNullOrWhiteSpace(decisionId);
        var provider = !string.IsNullOrWhiteSpace(providerId);
        if (attempt && decision && provider) return FamixaProvenanceClass.Verified;
        if (attempt && provider) return FamixaProvenanceClass.Partial;
        if (!string.IsNullOrWhiteSpace(providerRequestId) && provider) return FamixaProvenanceClass.Partial;
        return FamixaProvenanceClass.LegacyUnverified;
    }

    public static string? VendorRequestId(string? providerRequestId)
    {
        var t = (providerRequestId ?? "").Trim();
        return t.Length == 0 ? null : t;
    }

    public static FamixaExecutionProvenance Bind(
        string attemptId,
        FamixaProviderCapability capability,
        FamixaProviderSelectionDecision? decision = null,
        string? shotId = null,
        string? pictureRevisionId = null,
        string? picturePixelHash = null,
        string? motionContentFingerprint = null,
        string? actingBeatFingerprint = null,
        string? timingHash = null,
        string? providerRequestId = null,
        string? providerTaskId = null,
        string? artifactId = null,
        DateTimeOffset? createdAt = null)
    {
        var requestId = VendorRequestId(providerRequestId);
        var taskId = VendorRequestId(providerTaskId) ?? requestId;
        var cls = Classify(attemptId, decision?.DecisionId, decision?.ProviderId ?? "", requestId);
        return new(
            attemptId,
            capability,
            decision?.DecisionId,
            shotId,
            decision?.ProviderId,
            decision?.ModelId,
            decision?.SelectionMode,
            decision?.Reason,
            pictureRevisionId,
            picturePixelHash,
            motionContentFingerprint,
            actingBeatFingerprint,
            timingHash,
            requestId,
            taskId,
            artifactId,
            createdAt ?? DateTimeOffset.UtcNow,
            cls);
    }

    public static FamixaExecutionLineage Reconstruct(FamixaExecutionProvenance provenance)
    {
        FamixaSelectionSnapshot? decision = string.IsNullOrWhiteSpace(provenance.DecisionId)
            || string.IsNullOrWhiteSpace(provenance.ProviderId)
            ? null
            : new(
                provenance.DecisionId,
                provenance.ProviderId,
                provenance.ModelId,
                provenance.SelectionMode ?? FamixaProviderSelectionMode.Legacy,
                provenance.SelectionReason);
        return new(
            provenance,
            decision,
            provenance.MotionContentFingerprint,
            provenance.ProviderId,
            provenance.ModelId);
    }

    public static bool SameWhat(FamixaExecutionProvenance a, FamixaExecutionProvenance b) =>
        !string.IsNullOrWhiteSpace(a.MotionContentFingerprint)
        && string.Equals(a.MotionContentFingerprint, b.MotionContentFingerprint, StringComparison.Ordinal)
        && string.Equals(a.PictureRevisionId ?? "", b.PictureRevisionId ?? "", StringComparison.Ordinal);

    public static bool SameHow(FamixaExecutionProvenance a, FamixaExecutionProvenance b) =>
        string.Equals(a.ProviderId ?? "", b.ProviderId ?? "", StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.ModelId ?? "", b.ModelId ?? "", StringComparison.OrdinalIgnoreCase);

    public static ContentSeriesTurboTaskDto StampTurbo(
        ContentSeriesTurboTaskDto dto,
        FamixaProviderSelectionDecision decision) =>
        dto with
        {
            DecisionId = decision.DecisionId,
            ProviderId = decision.ProviderId,
            SelectionMode = decision.SelectionMode.ToString().ToUpperInvariant(),
            SelectionReason = decision.Reason,
            EstimatedCost = decision.EstimatedCost,
            CostKind = decision.CostKind switch
            {
                FamixaCostKind.Estimate => "ESTIMATE",
                FamixaCostKind.FalBilledEstimate => "FAL_BILLED_ESTIMATE",
                _ => "UNKNOWN",
            },
        };
}
