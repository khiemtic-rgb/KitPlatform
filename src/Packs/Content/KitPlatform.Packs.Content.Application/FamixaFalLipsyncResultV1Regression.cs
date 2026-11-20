namespace KitPlatform.Packs.Content;

public static class FamixaFalLipsyncResultV1Regression
{
    public const string SuiteId = FamixaFalLipsyncResultV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(
            FamixaFalLipsyncResultV1.ReadVideoUrl("""{"video":{"url":"https://v3b.fal.media/files/b/x/output.mp4"}}""")
            == "https://v3b.fal.media/files/b/x/output.mp4",
            "root video.url");
        Ok(
            FamixaFalLipsyncResultV1.ReadVideoUrl("""{"payload":{"video":{"url":"https://fal.media/files/a/out.mp4"}}}""")
            == "https://fal.media/files/a/out.mp4",
            "queue payload.video.url");
        Ok(
            FamixaFalLipsyncResultV1.ReadVideoUrl("""{"data":{"video_url":"https://v3.fal.media/files/z/clip.mp4"}}""")
            == "https://v3.fal.media/files/z/clip.mp4",
            "data.video_url");
        Ok(
            FamixaFalLipsyncResultV1.ReadVideoUrl("""{"status":"COMPLETED","response_url":"https://queue.fal.run/fal-ai/sync-lipsync/v3/requests/x/response"}""")
            is null,
            "queue status URL is not the take");
        Ok(
            FamixaFalLipsyncResultV1.LooksLikeFalOutputVideo("https://v3b.fal.media/files/b/x/output.mp4"),
            "fal.media output");
        Ok(
            !FamixaFalLipsyncResultV1.LooksLikeFalOutputVideo("https://queue.fal.run/fal-ai/sync-lipsync/v3/requests/x"),
            "reject queue URL");
        Ok(
            FamixaFalLipsyncResultV1.ReadVideoUrl(
                """<html>https://v3b.fal.media/files/b/x/output.mp4</html>""")
            == "https://v3b.fal.media/files/b/x/output.mp4",
            "scan raw HTML for fal.media");
        Ok(
            FamixaFalLipsyncResultV1.ReadVideoUrl(
                """{"items":[{"request_id":"x","json_output":{"video":{"url":"https://v3b.fal.media/files/b/x/output.mp4"}}}]}""")
            == "https://v3b.fal.media/files/b/x/output.mp4",
            "platform json_output.video.url");
        Ok(
            FamixaFalLipsyncResultV1.ReadPlatformNoVideoReason(
                """{"items":[{"request_id":"01a09617-d14e-77d3-abd2-3140caa566c4","json_output":{"detail":[{"type":"int_type","loc":["body"],"msg":"Input should be a valid integer"}]}}]}""",
                "01a09617-d14e-77d3-abd2-3140caa566c4")
            ?.Contains("số nguyên pixel", StringComparison.Ordinal) == true,
            "T14 validation json_output is a dead job");
        Ok(
            FamixaFalLipsyncResultV1.ReadPlatformNoVideoReason(
                """{"items":[{"request_id":"01a0a0b6-7b51-7700-b8a4-afbfb15eec56","json_output":{"detail":[{"type":"value_error","loc":["body","options","active_speaker_detection"],"msg":"v3 cannot mix with manual coordinates"}]}}]}""",
                "01a0a0b6-7b51-7700-b8a4-afbfb15eec56")
            ?.Contains("active_speaker_detection", StringComparison.Ordinal) == true,
            "T16 value_error surfaces loc+msg");
        Ok(
            FamixaFalLipsyncResultV1.ReadPlatformNoVideoReason(
                """{"items":[{"request_id":"other","json_output":{"detail":[]}}]}""",
                "01a09617-d14e-77d3-abd2-3140caa566c4")
            is null,
            "T15 other request is not this job's dead end");

        return fail;
    }
}
