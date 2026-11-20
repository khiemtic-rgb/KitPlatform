using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoHttpCompositionRequest(
    ProductionScope Scope,
    ProductionAiSelection? Selection,
    ProductionTaskInputSnapshot? Frozen,
    ShotExecutionInput? Live,
    byte[]? KeyframeBytes,
    string KeyframeMime = "image/png",
    CostPolicyDecision? Cost = null,
    FamixaVeoCostVariant? Variant = null,
    string? ProjectId = null,
    string? Location = null);

public sealed record OfficialVeoHttpCompositionResult(
    bool Ready,
    string State,
    string? BlockReason,
    VeoHttpSettings? Settings,
    string? StartUrl,
    string? PollUrl,
    VeoMotionTransportCreateRequest? TransportRequest,
    string? VertexRequestJson);

/// <summary>
/// Official google/veo-3.1 Vertex HTTP composition. Reuses
/// <see cref="HttpVeoMotionTransport"/> / <see cref="VeoVertexEndpoints"/>.
/// Does not Send, OAuth, or open the production resolver.
/// </summary>
public static class OfficialVeoHttpCompositionV1
{
    public const bool ProductionResolverOpen = false;
    public const string DefaultComposeProject = "official-veo-compose";

    public static OfficialVeoHttpCompositionResult Compose(OfficialVeoHttpCompositionRequest request)
    {
        try
        {
            OfficialVeoProductionAuthenticatorV1.RequireOfficialTarget(
                new OfficialVeoAuthenticatorCompositionRequest(request.Scope, request.Selection));
            OfficialCreateVideoPermitAuthorizationV1.RequireIssuanceGates(
                new OfficialCreateVideoPermitIssueRequest(
                    request.Scope,
                    request.Selection!,
                    request.Frozen,
                    request.Live,
                    request.Cost,
                    request.Variant));

            var frozen = request.Frozen!;
            var bytes = request.KeyframeBytes;
            if (bytes is null || bytes.Length < 32)
                throw new InvalidOperationException(ProductionFailureReasons.KeyframeBytesMissing);
            var hash = KitVideoIntegrityRules.Sha256Hex(bytes);
            if (!string.Equals(frozen.KeyframeHash, hash, StringComparison.OrdinalIgnoreCase)
                || frozen.KeyframeArtifactId != OfficialCreateVideoFreezeV1.OfficialKeyframeId)
                throw new InvalidOperationException(ProductionFailureReasons.InputSnapshotMismatch);

            var project = string.IsNullOrWhiteSpace(request.ProjectId)
                ? DefaultComposeProject
                : request.ProjectId.Trim();
            var location = VeoHttpConfigPrecedence.ResolveLocation(request.Location, null);
            var settings = new VeoHttpSettings(project, location, VeoVertexEndpoints.VertexModel);
            var mime = string.IsNullOrWhiteSpace(request.KeyframeMime) ? "image/png" : request.KeyframeMime.Trim();
            var transportRequest = new VeoMotionTransportCreateRequest(
                frozen.TaskId,
                Guid.Empty,
                RealVertexExecutionV1Lock.ProviderId,
                RealVertexExecutionV1Lock.ModelId,
                VeoMotionPrompt.FromFrozen(frozen),
                VeoMotionImageDataUri.FromBytes(bytes, mime),
                mime.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : mime.ToLowerInvariant(),
                VeoMotionDurationMapping.ToProviderDurationSec(frozen.DurationSec),
                frozen.DurationSec,
                VeoMotionImageDataUri.ToProviderAspect(frozen.AspectRatio),
                SampleCount: 1,
                Resolution: "720p",
                GenerateAudio: false,
                frozen.SnapshotHash,
                frozen.KeyframeHash,
                frozen.KeyframeArtifactId,
                ProductionFrozenCredential.FromSnapshot(frozen));

            return new OfficialVeoHttpCompositionResult(
                true,
                ProductionFailureReasons.HttpCompositionReady,
                null,
                settings,
                VeoVertexEndpoints.PredictLongRunning(settings),
                VeoVertexEndpoints.FetchPredictOperation(settings),
                transportRequest,
                HttpVeoMotionTransport.PreviewStartBody(transportRequest));
        }
        catch (InvalidOperationException ex)
        {
            return new OfficialVeoHttpCompositionResult(
                false,
                ProductionFailureReasons.HttpCompositionUnavailable,
                CodeOf(ex.Message),
                null, null, null, null, null);
        }
    }

    public static bool IsBoundTo(RealVertexExecutionPreflightRequest request) =>
        OfficialVeoProductionAuthenticatorV1.IsBoundTo(request)
        && request.Frozen is not null;

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
