using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Provider abstraction → IImageGenerator. Receives a compiled prompt + optional FRONT pixel refs.
/// Does not resolve VUA / PVS / CDL / Character Profile / Age / IdentityBrief.
/// Missing identity anchor is a hard gate. No independent downstream T2I fallback.
/// </summary>
internal sealed class VisualCalibrationGenerationProvider : IVisualCalibrationGenerationProvider
{
    private readonly IImageGenerator _images;
    private readonly KitVideoArtifactStore _artifacts;

    public VisualCalibrationGenerationProvider(IImageGenerator images, KitVideoArtifactStore artifacts)
    {
        _images = images;
        _artifacts = artifacts;
    }

    public string ProviderId =>
        string.IsNullOrWhiteSpace(_images.ProviderId) ? VisualCalibrationPackV1Rules.ProviderName : _images.ProviderId;

    public async Task<VisualCalibrationSlotGenerationResult> GenerateSlotAsync(
        VisualCalibrationGenerationRequest request, CancellationToken cancellationToken)
    {
        if (!IdentityConditionedCalibrationV1Rules.ProviderMayCall(request))
            return new VisualCalibrationSlotGenerationResult(
                false, false, null, null, null, null, null, IdentityConditionedCalibrationV1Rules.GateAnchor);

        var refs = new List<KitVideoPromptRefBytes>();
        if (!IdentityConditionedCalibrationV1Rules.IsFront(request.ViewType))
        {
            var bytes = _artifacts.Read(request.IdentityAnchorPath);
            if (bytes is not { Length: > 32 })
                return new VisualCalibrationSlotGenerationResult(
                    false, false, null, null, null, null, null, IdentityConditionedCalibrationV1Rules.GateReference);
            refs.Add(new KitVideoPromptRefBytes(
                IdentityConditionedCalibrationV1Rules.ReferenceRoleAnchor,
                KitVideoArtifactRules.DetectMime(bytes) ?? "image/jpeg",
                bytes,
                IdentityConditionedCalibrationV1Rules.IdentityAnchorView));
        }

        var result = await _images.GenerateAsync(
            new KitVideoImageGenerationRequest(
                request.CompiledPrompt,
                refs,
                request.ViewType == "FULL_BODY" ? "3:4" : "3:4",
                1024,
                1024,
                request.Model),
            cancellationToken);
        if (!result.Ok || result.Bytes is not { Length: > 32 })
            return new VisualCalibrationSlotGenerationResult(
                true, false, result.Bytes, result.Mime, null, null, result.GenerationId,
                string.IsNullOrWhiteSpace(result.FailureClass)
                    ? VisualCalibrationPackV1Rules.GateIncomplete
                    : result.FailureClass);
        try
        {
            if (string.IsNullOrWhiteSpace(request.CalibrationRunId))
                return new VisualCalibrationSlotGenerationResult(
                    true, false, result.Bytes, result.Mime, null, null, result.GenerationId,
                    IdentityConditionedCalibrationLiveV1Rules.GateRun);
            var persist = _artifacts.PersistCalibration(
                request.CalibrationPackId,
                request.CalibrationRunId,
                request.SubjectId,
                request.ViewType,
                result.Bytes);
            if (!persist.Check.Ok)
                return new VisualCalibrationSlotGenerationResult(
                    true, false, persist.Jpeg, "image/jpeg", persist.Path, null, result.GenerationId,
                    persist.Check.Status);
            var sha = KitVideoIntegrityRules.Sha256Hex(persist.Jpeg);
            return new VisualCalibrationSlotGenerationResult(
                true, true, persist.Jpeg, "image/jpeg", persist.Path, sha, result.GenerationId, null);
        }
        catch (InvalidOperationException ex)
        {
            return new VisualCalibrationSlotGenerationResult(
                true, false, result.Bytes, result.Mime, null, null, result.GenerationId, ex.Message);
        }
    }
}
