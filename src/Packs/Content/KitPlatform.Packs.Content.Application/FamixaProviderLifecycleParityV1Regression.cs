namespace KitPlatform.Packs.Content;

public static class FamixaProviderLifecycleParityV1Regression
{
    public const string SuiteId = "FAMIXA_AI_PROVIDER_LIFECYCLE_PARITY_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var runway = FamixaProviderTaskIdentity.Resolve("9ef1c551-aaaa-bbbb-cccc-ddddeeeeffff");
        Ok(runway.ProviderId == FamixaProviderIds.Runway
           && runway.Capability == FamixaProviderCapability.Motion
           && runway.Source == FamixaProviderTaskSource.LegacyUnverified, "T10 legacy UUID → runway LEGACY_UNVERIFIED");

        var wan = FamixaProviderTaskIdentity.Resolve("wan_01a08088-04a0-7e81-bc31-c0a786a4c5c2");
        Ok(wan.ProviderId == FamixaProviderIds.Wan
           && wan.Capability == FamixaProviderCapability.Motion
           && wan.Source == FamixaProviderTaskSource.LegacyPrefix, "T4 legacy wan_ prefix → Wan LEGACY");

        var lip = FamixaProviderTaskIdentity.Resolve("lipsync_v3_abc");
        Ok(lip.ProviderId == FamixaProviderIds.Fal
           && lip.Capability == FamixaProviderCapability.LipSync
           && lip.ModelId == "v3"
           && lip.Source == FamixaProviderTaskSource.LegacyPrefix, "T7 legacy lipsync_v3 → Fal");

        var known = FamixaProviderTaskIdentity.Resolve("not-a-known-prefix", "wan");
        Ok(known.ProviderId == FamixaProviderIds.Wan
           && known.Source == FamixaProviderTaskSource.Explicit, "T9 explicit provider beats prefix");

        var unknown = false;
        try { FamixaProviderTaskIdentity.Resolve("task", "kling"); }
        catch (FamixaProviderException ex) { unknown = ex.Code == FamixaProviderErrorCodes.Unsupported; }
        Ok(unknown, "T8 unknown provider");

        Ok(FamixaProviderTaskIdentity.CanHandle(FamixaProviderIds.Wan, FamixaProviderCapability.Motion, "wan_1"), "Wan can handle wan_");
        Ok(!FamixaProviderTaskIdentity.CanHandle(FamixaProviderIds.Runway, FamixaProviderCapability.Motion, "wan_1"), "Runway cannot handle wan_");
        Ok(!FamixaProviderTaskIdentity.CanHandle(FamixaProviderIds.Wan, FamixaProviderCapability.Motion, "9ef1c551"), "Wan cannot handle UUID");
        Ok(FamixaProviderTaskIdentity.CanHandle(FamixaProviderIds.Fal, FamixaProviderCapability.LipSync, "lipsync_ls_1"), "Fal can handle lipsync");
        Ok(!FamixaProviderTaskIdentity.CanHandle(FamixaProviderIds.Fal, FamixaProviderCapability.LipSync, "wan_1"), "Fal cannot handle wan_");

        var result = new FamixaProviderResult(
            FamixaProviderIds.Wan, "wan-2.1", FamixaProviderStatus.Succeeded, ProviderRequestId: "wan_1", OutputUrl: "https://fal/x.mp4");
        Ok(result.ProviderId == "wan" && result.Status != default, "T13 result has provider identity");
        var leak = typeof(FamixaProviderResult).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Ok(!leak.Contains("Current") && !leak.Contains("Approved") && !leak.Contains("Accepted")
           && !leak.Contains("Stale") && !leak.Contains("Final"), "T13 result has no Director/Execution state");

        return fail;
    }
}
