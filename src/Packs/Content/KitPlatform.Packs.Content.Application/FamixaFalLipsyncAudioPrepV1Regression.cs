namespace KitPlatform.Packs.Content;

public static class FamixaFalLipsyncAudioPrepV1Regression
{
    public const string SuiteId = FamixaFalLipsyncAudioPrepV1.SuiteId;

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        Ok(FamixaFalLipsyncAudioPrepV1.KeepFullVoiceBytes(1), "T1 single voice keeps full bytes");
        Ok(!FamixaFalLipsyncAudioPrepV1.KeepFullVoiceBytes(2), "T2 multi voice still merges");
        Ok(FamixaFalLipsyncAudioPrepV1.MergePadDurationSec(1, 4) is null, "T3 motion 4s does not atrim 4.24 voice");
        Ok(FamixaFalLipsyncAudioPrepV1.MergePadDurationSec(2, 4) == 4, "T4 multi may pad");

        return fail;
    }
}
