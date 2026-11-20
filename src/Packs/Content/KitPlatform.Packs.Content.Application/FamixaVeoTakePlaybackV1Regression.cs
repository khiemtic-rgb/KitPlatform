namespace KitPlatform.Packs.Content;

public static class FamixaVeoTakePlaybackV1Regression
{
    public const string SuiteId = "FAMIXA_VEO_TAKE_PLAYBACK_V1";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var task = "veo_abc123XYZ";
        var id = FamixaVeoTakePlaybackV1.FileId(task);
        Ok(id.Length == 32 && id == FamixaVeoTakePlaybackV1.FileId(task), "T1 stable 32-hex file id");
        Ok(!id.Contains("veo_", StringComparison.Ordinal), "T2 file id is not the raw task");

        var path = FamixaVeoTakePlaybackV1.PublicPath(task);
        Ok(path.StartsWith(FamixaVeoTakePlaybackV1.Route, StringComparison.Ordinal)
           && path.Contains("taskId=", StringComparison.Ordinal)
           && !path.Contains("data:video", StringComparison.Ordinal),
            "T3 public path is short HTTP, not data-URI");

        Ok(FamixaVeoTakePlaybackV1.FileIdFromUrl(path) == id, "T4 parse taskId query");
        Ok(FamixaVeoTakePlaybackV1.FileIdFromUrl("https://api.novixa.vn" + path) == id, "T5 parse absolute URL");
        Ok(FamixaVeoTakePlaybackV1.FileIdFromUrl(task) == id, "T6 parse raw veo_ task");
        Ok(FamixaVeoTakePlaybackV1.FileIdFromUrl("data:video/mp4;base64,AAAA") is null, "T7 data-URI is not a file id");
        Ok(!path.Contains("private_key", StringComparison.OrdinalIgnoreCase), "T8 no secret leak");
        Ok(FamixaVeoTakePlaybackV1.IsStoredTake(path) && FamixaVeoTakePlaybackV1.IsStoredTake(task), "T9 local Veo path is a stored take");
        Ok(!FamixaVeoTakePlaybackV1.IsStoredTake("https://fal.example/take.mp4"), "T10 Fal HTTPS is not a stored Veo take");
        Ok(!FamixaVeoTakePlaybackV1.IsStoredTake("data:video/mp4;base64,AAAA"), "T11 data-URI is not a stored Veo take");

        return fail;
    }
}
