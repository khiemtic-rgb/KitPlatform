using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoAuthenticatorCompositionRequest(
    ProductionScope Scope,
    ProductionAiSelection? Selection,
    string? ExplicitServiceAccountJson = null);

public sealed record OfficialVeoAuthenticatorCompositionResult(
    bool Ready,
    string State,
    string? BlockReason,
    IVeoHttpAuthenticator Authenticator,
    VeoHttpCredentialContract? Contract);

/// <summary>
/// Official google/veo-3.1 authenticator composition. Reuses
/// <see cref="ProductionVeoHttpAuthenticator"/> and
/// <see cref="VeoHttpAuthContract"/>. GetAccessToken stays CLOSED.
/// Never reads env / Join / IConfiguration.
/// </summary>
public static class OfficialVeoProductionAuthenticatorV1
{
    public const bool ProductionDiRegistered = false;

    public static OfficialVeoAuthenticatorCompositionResult Compose(
        OfficialVeoAuthenticatorCompositionRequest request)
    {
        try
        {
            RequireOfficialTarget(request);
            VeoHttpCredentialContract? contract = null;
            if (request.ExplicitServiceAccountJson is not null)
            {
                var parsed = VeoHttpServiceAccountValidator.ValidateExplicit(request.ExplicitServiceAccountJson);
                if (!parsed.Ok)
                    throw new InvalidOperationException(parsed.Code ?? ProductionFailureReasons.InvalidProviderResponse);
                contract = parsed.Contract;
            }

            var authenticator = new ProductionVeoHttpAuthenticator(contract);
            return new OfficialVeoAuthenticatorCompositionResult(
                true,
                ProductionFailureReasons.AuthenticatorReady,
                null,
                authenticator,
                contract);
        }
        catch (InvalidOperationException ex)
        {
            return new OfficialVeoAuthenticatorCompositionResult(
                false,
                ProductionFailureReasons.AuthenticatorNotReady,
                CodeOf(ex.Message),
                new ProductionVeoHttpAuthenticator(),
                null);
        }
    }

    public static void RequireOfficialTarget(OfficialVeoAuthenticatorCompositionRequest request)
    {
        if (request.Scope.BuildId != RealVertexExecutionV1Lock.BuildId)
            throw new InvalidOperationException(ProductionFailureReasons.UnauthorizedVertexTarget);
        if (!string.Equals(request.Scope.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.UnauthorizedVertexTarget);
        if (request.Selection is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);
        if (request.Selection.SelectionId != OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId
            || !string.Equals(request.Selection.Stage, ProductionAiSelectionStages.Video, StringComparison.Ordinal)
            || !string.Equals(request.Selection.Status, ProductionAiSelectionStatuses.Active, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.SelectionConflict);
        if (!string.Equals(request.Selection.ProviderId.Trim(), RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(request.Selection.ModelId.Trim(), RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase)
            || request.Selection.BuildId != RealVertexExecutionV1Lock.BuildId
            || !string.Equals(request.Selection.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.ProviderModelMismatch);
    }

    public static bool IsBoundTo(RealVertexExecutionPreflightRequest request)
    {
        var selection = request.ActiveVideoSelection;
        return request.Scope.BuildId == RealVertexExecutionV1Lock.BuildId
            && string.Equals(request.Scope.ShotCode, RealVertexExecutionV1Lock.ShotCode, StringComparison.Ordinal)
            && selection is not null
            && selection.SelectionId == OfficialCreateVideoPermitAuthorizationV1.OfficialSelectionId
            && string.Equals(selection.ProviderId.Trim(), RealVertexExecutionV1Lock.ProviderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(selection.ModelId.Trim(), RealVertexExecutionV1Lock.ModelId, StringComparison.OrdinalIgnoreCase);
    }

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
