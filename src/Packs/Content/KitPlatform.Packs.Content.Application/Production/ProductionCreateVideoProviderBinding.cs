namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// Authoritative CREATE_VIDEO ProviderId/ModelId. ACTIVE VIDEO selection wins.
/// Never remaps a non-Fixture selection onto Fixture.
/// </summary>
public static class ProductionCreateVideoProviderBinding
{
    public static (string ProviderId, string ModelId) Authoritative(
        bool officialFilm,
        ProductionAiSelection? activeVideoSelection,
        string? claimProviderId,
        string? claimModelId,
        IMotionExecutionAdapter? testFallback)
    {
        if (activeVideoSelection is not null
            && CreateVideoActionContractGate.IsPresent(activeVideoSelection.ProviderId)
            && CreateVideoActionContractGate.IsPresent(activeVideoSelection.ModelId))
            return (activeVideoSelection.ProviderId.Trim(), activeVideoSelection.ModelId.Trim());

        var claimProvider = CreateVideoActionContractGate.IsPresent(claimProviderId) ? claimProviderId!.Trim() : "";
        var claimModel = CreateVideoActionContractGate.IsPresent(claimModelId) ? claimModelId!.Trim() : "";
        if (officialFilm)
            return (claimProvider, claimModel);

        return (
            claimProvider.Length > 0 ? claimProvider : (testFallback?.ProviderId ?? "").Trim(),
            claimModel.Length > 0 ? claimModel : (testFallback?.ModelId ?? "").Trim());
    }

    public static bool HasProviderModel(string? providerId, string? modelId) =>
        CreateVideoActionContractGate.IsPresent(providerId)
        && CreateVideoActionContractGate.IsPresent(modelId);
}
