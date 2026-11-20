namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Phase C single evaluator: CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC consume the same
/// PerformanceSnapshot identity. Official film stays out of TEST cutover (legacy CREATE_*).
/// </summary>
public static class ProductionPerformanceConsumption
{
    public const string RequiredInput = "PerformanceSnapshot";

    public static bool ActionRequiresSnapshot(string action, ProductionScope scope) =>
        ActionRequiresSnapshot(action, scope.BuildId)
        && !ProductionScopeAnchorRules.IsTenantOwnedEp01Build(scope.BuildId, scope.EpisodeCode);

    public static bool ActionRequiresSnapshot(string action, Guid buildId) =>
        !ProductionScopeAnchorRules.IsOfficialFilm(buildId)
        && action is ProductionActions.CreateVideo
            or ProductionActions.CreateVoice
            or ProductionActions.CreateLipsync;

    public static string ReasonForMissing(CreateVideoReadinessClaim? claim)
    {
        var hash = (claim?.PerformanceHash ?? "").Trim();
        if (hash.Length > 0 && !PerformanceContractCanon.IsValidPerformanceHash(hash))
            return PerformanceContractIds.InvalidPerformanceHash;
        return ProductionFailureReasons.MissingPerformanceSnapshot;
    }

    public static bool HasSnapshotClaim(CreateVideoReadinessClaim? claim) =>
        claim?.PerformanceSnapshotId is { } id && id != Guid.Empty;

    public static bool HasSnapshotClaim(ShotExecutionInput input) =>
        PerformanceSnapshotBinding.HasClaim(input);

    public static void CollectMissing(ProductionScope scope, string action, CreateVideoReadinessClaim? claim, List<string> missing)
    {
        if (!ActionRequiresSnapshot(action, scope)) return;
        if (!HasSnapshotClaim(claim))
        {
            missing.Add(RequiredInput);
            return;
        }
        var hash = (claim?.PerformanceHash ?? "").Trim();
        if (hash.Length > 0 && !PerformanceContractCanon.IsValidPerformanceHash(hash))
            missing.Add(RequiredInput);
    }

    public static async Task<PerformanceSnapshotBindResult> RequireBoundAsync(
        IPerformanceSnapshotStore? store,
        ShotExecutionInput input,
        ProductionScope scope,
        CancellationToken ct = default)
    {
        if (ProductionScopeAnchorRules.IsOfficialFilm(scope.BuildId)
            && !HasSnapshotClaim(input))
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (!HasSnapshotClaim(input))
            throw new InvalidOperationException(ProductionFailureReasons.MissingPerformanceSnapshot);
        if (store is null)
            throw new InvalidOperationException(PerformanceContractIds.SnapshotNotFound);
        return await PerformanceSnapshotBinding.BindAsync(store, input, scope, ct);
    }

    public static async Task<PerformanceSnapshotBindResult?> TryBindClaimAsync(
        IPerformanceSnapshotStore? store,
        CreateVideoReadinessClaim? claim,
        ProductionScope scope,
        CancellationToken ct = default)
    {
        if (!HasSnapshotClaim(claim)) return null;
        var input = new ShotExecutionInput(
            claim?.LineText ?? claim?.Story ?? "",
            claim?.ActingBeat ?? "",
            claim?.VisualContract ?? "",
            "",
            "",
            claim?.Timing ?? "",
            "",
            "",
            PerformanceSnapshotId: claim!.PerformanceSnapshotId,
            PerformanceHash: claim.PerformanceHash);
        return await RequireBoundAsync(store, input, scope, ct);
    }

    public static void RequireVoiceLine(PerformanceDraft draft, string lineId, string characterId, string lineText)
    {
        var requestedLine = (lineId ?? "").Trim();
        var requestedChar = (characterId ?? "").Trim();
        var requestedText = PerformanceContractRules.SpokenText(lineText);
        if (requestedLine.Length == 0 || requestedChar.Length == 0 || requestedText.Length == 0)
            throw new InvalidOperationException(ProductionFailureReasons.MissingDependency);

        var line = draft.Lines.FirstOrDefault(l => string.Equals(l.LineId, requestedLine, StringComparison.Ordinal));
        if (line is null)
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        if (!string.Equals(line.CharacterId, requestedChar, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        if (!string.Equals(line.TextIdentity.Text, requestedText, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        var textHash = PerformanceContractCanon.LineTextHashOf(requestedText);
        if (!string.Equals(line.TextIdentity.TextHash, textHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceLineMismatch);
        if (!line.Delivery.Locked)
            throw new InvalidOperationException(PerformanceContractIds.SpokenLineUnlocked);
    }

    public static void RequireSharedParentPerformance(
        ProductionTaskInputSnapshot? motion,
        ProductionTaskInputSnapshot? voice,
        string? claimedHash)
    {
        var motionHash = (motion?.PerformanceHash ?? "").Trim();
        var voiceHash = (voice?.PerformanceHash ?? "").Trim();
        if (!PerformanceContractCanon.IsValidPerformanceHash(motionHash)
            || !PerformanceContractCanon.IsValidPerformanceHash(voiceHash)
            || !string.Equals(motionHash, voiceHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceHashMismatch);

        if (!string.IsNullOrWhiteSpace(claimedHash)
            && !string.Equals(claimedHash.Trim(), motionHash, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PerformanceHashMismatch);
    }

    public static ShotExecutionInput ApplyBound(ShotExecutionInput input, PerformanceSnapshotBindResult bound)
    {
        var derived = PerformanceActingProjection.FromDraft(bound.Draft);
        return input with
        {
            PerformanceSnapshotId = bound.PerformanceSnapshotId,
            PerformanceHash = bound.PerformanceHash,
            PerformancePayloadJson = bound.PerformancePayloadJson,
            ActingBeat = derived.Length > 0 ? derived : input.ActingBeat,
        };
    }
}

public static class PerformanceActingProjection
{
    public static string FromDraft(PerformanceDraft? draft)
    {
        if (draft is null) return "";
        var before = draft.ShotPerformance.Before;
        var during = draft.ShotPerformance.During;
        var after = draft.ShotPerformance.After;
        var parts = new[]
        {
            before?.Action,
            before?.Body,
            before?.Prop,
            before?.Gaze,
            before?.Room,
            during?.Emotion,
            during?.Intensity is { } n ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : null,
            after?.Action,
            after?.Gaze,
            draft.ShotPerformance.GazeTarget,
        };
        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
    }

    public static string FromPayloadDraft(PerformanceDraft? draft) => FromDraft(draft);
}

public sealed record ProductionActionInputVerdict(
    bool Available,
    string? BlockReason,
    IReadOnlyList<string> MissingInputs,
    IReadOnlyList<string> MissingReasons,
    Guid? PerformanceSnapshotId = null,
    string? PerformanceHash = null,
    string? PerformancePayloadJson = null,
    string? DerivedActingBeat = null,
    Guid? VideoArtifactId = null,
    Guid? VoiceArtifactId = null,
    Guid? LipsyncArtifactId = null,
    string? LineId = null,
    string? LineText = null,
    string? CharacterId = null,
    string? VoiceId = null,
    string? ProviderId = null,
    string? ModelId = null);
