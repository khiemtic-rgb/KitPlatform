namespace KitPlatform.Packs.Content;

public static class FamixaFalLipsyncUsageV1Regression
{
    public const string SuiteId = FamixaFalLipsyncUsageV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var id = "01a09617-d14e-77d3-abd2-3140caa566c4";
        var ids = FamixaFalLipsyncUsageV1.EndpointIds("fal-ai/sync-lipsync/v3");
        Ok(ids.Count == 2, "T0 submit + alias");
        Ok(ids[0] == "fal-ai/sync-lipsync/v3", "T1 keep /v3 for Usage");
        Ok(ids[1] == "fal-ai/sync-lipsync", "T2 alias without /v3");

        var urls = FamixaFalLipsyncUsageV1.PeekUrls(id, "fal-ai/sync-lipsync/v3");
        Ok(urls.Count >= 4, "T3 Usage + recent + Assets");
        Ok(urls[0].StartsWith("https://api.fal.ai/v1/models/requests/by-endpoint?", StringComparison.Ordinal), "T4 platform host");
        Ok(urls[0].Contains("endpoint_id=fal-ai%2Fsync-lipsync%2Fv3", StringComparison.Ordinal), "T5 v3 endpoint encoded");
        Ok(urls[0].Contains($"request_id={id}", StringComparison.Ordinal), "T6 request id");
        Ok(urls[0].Contains("expand=payloads", StringComparison.Ordinal), "T7 payloads");
        Ok(!urls[0].Contains("status=success", StringComparison.Ordinal), "T7b no status filter");
        Ok(urls.Any(u => u.Contains("/assets?", StringComparison.Ordinal)), "T7c Assets browse");
        Ok(urls.All(u => !u.Contains("queue.fal.run", StringComparison.OrdinalIgnoreCase)), "T8 not queue");

        const string platform = """
            {"items":[{"request_id":"01a09617-d14e-77d3-abd2-3140caa566c4","json_output":{"video":{"url":"https://v3b.fal.media/files/b/x/output.mp4"}}},{"request_id":"other","json_output":{"video":{"url":"https://v3b.fal.media/files/b/y/other.mp4"}}}]}
            """;
        Ok(
            FamixaFalLipsyncResultV1.ReadPlatformVideoUrl(platform, id)
            == "https://v3b.fal.media/files/b/x/output.mp4",
            "T9 match request json_output");
        Ok(FamixaFalLipsyncResultV1.ReadPlatformVideoUrl(platform, "missing") is null, "T10 ignore other jobs");
        const string assets = """
            {"assets":[{"request_id":"01a09617-d14e-77d3-abd2-3140caa566c4","url":"https://v3b.fal.media/files/b/x/output.mp4","type":"video"}]}
            """;
        Ok(
            FamixaFalLipsyncResultV1.ReadPlatformVideoUrl(assets, id)
            == "https://v3b.fal.media/files/b/x/output.mp4",
            "T11 Assets url by request");
        Ok(
            FamixaFalLipsyncResultV1.DescribePlatformPeek(platform, id).Contains("match=yes", StringComparison.Ordinal),
            "T12 peek match");
        const string mixed = """
            {"items":[{"request_id":"01a09617-d14e-77d3-abd2-3140caa566c4","json_input":{"video_url":"https://v3b.fal.media/files/b/in/mute-take.mp4"},"json_output":{"video":{"url":"https://v3b.fal.media/files/b/out/lipsync.mp4"}}}]}
            """;
        Ok(
            FamixaFalLipsyncResultV1.ReadPlatformVideoUrl(mixed, id)
            == "https://v3b.fal.media/files/b/out/lipsync.mp4",
            "T13 json_output wins over mute json_input");
        const string inputOnly = """
            {"items":[{"request_id":"01a09617-d14e-77d3-abd2-3140caa566c4","json_input":{"video_url":"https://v3b.fal.media/files/b/in/mute-take.mp4"}}]}
            """;
        Ok(FamixaFalLipsyncResultV1.ReadPlatformVideoUrl(inputOnly, id) is null, "T14 never persist mute input");

        return fail;
    }
}
