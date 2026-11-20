namespace KitPlatform.Packs.Content;

public static class FamixaFalLipsyncPollV1Regression
{
    public const string SuiteId = FamixaFalLipsyncPollV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var id = "01a09617-d14e-77d3-abd2-3140caa566c4";
        var paths = FamixaFalLipsyncPollV1.PeekPaths(
            "fal-ai/sync-lipsync/v3",
            id,
            $"https://queue.fal.run/fal-ai/sync-lipsync/v3/requests/{id}/status",
            $"https://queue.fal.run/fal-ai/sync-lipsync/v3/requests/{id}/response");

        Ok(FamixaFalLipsyncPollV1.QueueApp("fal-ai/sync-lipsync/v3") == "fal-ai/sync-lipsync", "T0 GET drops /v3");
        Ok(paths.Count >= 3, "T1 response + result + status");
        Ok(paths[0] == $"fal-ai/sync-lipsync/requests/{id}/response", "T2 payload /response first");
        Ok(paths[1] == $"fal-ai/sync-lipsync/requests/{id}", "T3 result second");
        Ok(paths[2].EndsWith("/status?logs=1", StringComparison.Ordinal), "T4 status?logs=1");
        Ok(paths.All(p => !p.Contains("/v3/requests/", StringComparison.OrdinalIgnoreCase)), "T5 never GET /v3/requests");
        Ok(paths.All(p => !p.Contains("%2F", StringComparison.OrdinalIgnoreCase)), "T6 never encoded slash");
        Ok(
            FamixaFalLipsyncPollV1.ToQueueRelative(
                $"https://queue.fal.run/fal-ai/sync-lipsync/v3/requests/{id}/response",
                keepResponse: true)
            == $"fal-ai/sync-lipsync/requests/{id}/response",
            "T7 follow keeps /response, rewrites v3");
        var follow = FamixaFalLipsyncPollV1.FollowPaths(
            "fal-ai/sync-lipsync/v3",
            id,
            $"https://queue.fal.run/fal-ai/sync-lipsync/v3/requests/{id}/response");
        Ok(follow.Contains($"fal-ai/sync-lipsync/requests/{id}/response"), "T8 follow list has payload");

        return fail;
    }
}
