namespace KitPlatform.Packs.Content;

public static class FamixaFalLipsyncPlaybackV1Regression
{
    public const string SuiteId = "FAMIXA_FAL_LIPSYNC_PLAYBACK_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var task = "lipsync_v3_01a09617-d14e-77d3-abd2-31400caa566c";
        var id = FamixaFalLipsyncPlaybackV1.FileId(task);
        Ok(id.Length == 32 && id == FamixaFalLipsyncPlaybackV1.FileId(task), "T1 stable 32-hex file id");
        Ok(!id.Contains("lipsync_", StringComparison.Ordinal), "T2 file id is not the raw task");

        var path = FamixaFalLipsyncPlaybackV1.PublicPath(task);
        Ok(path.StartsWith(FamixaFalLipsyncPlaybackV1.Route, StringComparison.Ordinal)
           && path.Contains("taskId=", StringComparison.Ordinal)
           && !path.Contains("fal.media", StringComparison.Ordinal),
            "T3 public path is short HTTP");

        Ok(FamixaFalLipsyncPlaybackV1.FileIdFromUrl(path) == id, "T4 parse taskId query");
        Ok(FamixaFalLipsyncPlaybackV1.FileIdFromUrl("https://api.novixa.vn" + path) == id, "T5 parse absolute URL");
        Ok(FamixaFalLipsyncPlaybackV1.FileIdFromUrl(task) == id, "T6 parse raw lipsync_ task");
        Ok(FamixaFalLipsyncPlaybackV1.FileIdFromUrl("https://v3b.fal.media/files/x.mp4") is null, "T7 CDN is not a file id");
        Ok(FamixaFalLipsyncPlaybackV1.IsStoredTake(path) && FamixaFalLipsyncPlaybackV1.IsStoredTake(task), "T8 local Fal path is stored");
        Ok(!FamixaFalLipsyncPlaybackV1.IsStoredTake("https://v3b.fal.media/files/x.mp4"), "T9 CDN is not stored");
        Ok(!FamixaVeoTakePlaybackV1.IsStoredTake(path), "T10 Fal path is not a Veo take");

        const string cdn = "https://v3b.fal.media/files/b/x/output.mp4";
        Ok(FamixaFalLipsyncPlaybackV1.CanonicalPlayableUrl(cdn, localReadable: true, task) == path, "T11 persist SUCCESS → local path");
        Ok(FamixaFalLipsyncPlaybackV1.CanonicalPlayableUrl(cdn, localReadable: false, task) == cdn, "T12 persist FAIL → keep Fal CDN");
        Ok(!FamixaFalLipsyncPlaybackV1.CanonicalPlayableUrl(cdn, localReadable: false, task).Contains("/fal-video", StringComparison.Ordinal), "T13 FAIL never mints local pointer");
        Ok(FamixaFalLipsyncPlaybackV1.IsLocalPointer(path) && !FamixaFalLipsyncPlaybackV1.IsLocalPointer(cdn), "T14 local pointer vs CDN");
        Ok(FamixaFalLipsyncPlaybackV1.CanonicalPlayableUrl(path, localReadable: false, task) == "", "T15 broken pointer is not playable");
        Ok(FamixaVeoTakePlaybackV1.PublicPath("veo_xxxxxxxx") != path, "T16 Veo path unchanged");

        return fail;
    }
}
