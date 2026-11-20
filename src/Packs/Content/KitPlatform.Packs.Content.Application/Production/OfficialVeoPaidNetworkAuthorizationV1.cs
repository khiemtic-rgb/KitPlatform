using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

public sealed record OfficialVeoPaidNetworkAuthorizationRequest(
    ProductionScope Scope,
    string Action,
    ProductionAiSelection? Selection,
    ProductionTaskInputSnapshot? Frozen,
    ShotExecutionInput? Live,
    ProductionExecutionPermit? Permit,
    CostPolicyDecision? Cost = null,
    FamixaVeoCostVariant? Variant = null);

public sealed record OfficialVeoPaidNetworkAuthorizationResult(
    bool Authorized,
    string State,
    string? BlockReason,
    string? BindingIdentity);

/// <summary>
/// Official google/veo-3.1 CREATE_VIDEO paid-network policy only.
/// Reuses CK13B issuance gates + existing permit rows. Does not issue,
/// consume, Start, or flip a global AuthorizePaidNetwork default.
/// </summary>
public static class OfficialVeoPaidNetworkAuthorizationV1
{
    public static OfficialVeoPaidNetworkAuthorizationResult Evaluate(
        OfficialVeoPaidNetworkAuthorizationRequest request)
    {
        try
        {
            var identity = RequireAuthorized(request);
            return new OfficialVeoPaidNetworkAuthorizationResult(
                true,
                ProductionFailureReasons.PaidNetworkAuthorized,
                null,
                identity);
        }
        catch (InvalidOperationException ex)
        {
            var code = CodeOf(ex.Message);
            return new OfficialVeoPaidNetworkAuthorizationResult(
                false,
                ProductionFailureReasons.PaidNetworkForbidden,
                code,
                null);
        }
    }

    public static string RequireAuthorized(OfficialVeoPaidNetworkAuthorizationRequest request)
    {
        if (!string.Equals((request.Action ?? "").Trim(), ProductionActions.CreateVideo, StringComparison.Ordinal))
            throw new InvalidOperationException(PerformanceContractIds.OfficialFilmForbidden);
        if (request.Selection is null)
            throw new InvalidOperationException(ProductionFailureReasons.MissingProductionAiSelection);

        var quote = OfficialCreateVideoPermitAuthorizationV1.RequireIssuanceGates(
            new OfficialCreateVideoPermitIssueRequest(
                request.Scope,
                request.Selection,
                request.Frozen,
                request.Live,
                request.Cost,
                request.Variant,
                OfficialCreateVideoPermitAuthorizationV1.IssuedBy));

        var permitBlock = ProductionOfficialPermitGate.BlockReason(
            request.Permit,
            RealVertexExecutionV1Lock.BuildId,
            RealVertexExecutionV1Lock.ShotCode,
            ProductionActions.CreateVideo,
            RealVertexExecutionV1Lock.ProviderId,
            RealVertexExecutionV1Lock.ModelId);
        if (permitBlock is not null)
            throw new InvalidOperationException(permitBlock);

        var expected = OfficialCreateVideoPermitAuthorizationV1.BindingReason(
            request.Selection, request.Frozen!, quote);
        if (!string.Equals(request.Permit!.Reason, expected, StringComparison.Ordinal))
            throw new InvalidOperationException(ProductionFailureReasons.PermitConflict);
        return expected;
    }

    static string CodeOf(string message)
    {
        var raw = (message ?? "").Trim();
        var sep = raw.IndexOf(" · ", StringComparison.Ordinal);
        return sep < 0 ? raw : raw[..sep];
    }
}
