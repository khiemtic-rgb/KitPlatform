using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// SH01 duplicate-character gate. No provider. The historical still is one Minh and one Linh;
/// the frozen acting line asked Linh to enter anyway, and CHAR was empty.
/// </summary>
public static class ProductionVisualContinuityV1Regression
{
    const string Sh01Visual =
        """{"gaze":"Linh looks toward Minh.","lens":"35mm","focus":"Linh","camera":"Eye level","framing":"WIDE","primary":{"id":"CHAR-003","name":"Linh","role":"primary"},"secondary":[{"id":"CHAR-001","name":"Minh","role":"secondary"}]}""";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var input = new ShotExecutionInput(
            "Minh bước vào. Người mẹ đang lau dọn.",
            "Audience must see Linh entering the room.",
            Sh01Visual,
            "",
            "kf",
            "5.0s",
            "",
            "");
        var clause = ProductionVisualContinuity.Format(input);
        Ok(clause.StartsWith(ProductionVisualContinuity.Marker, StringComparison.Ordinal), "T01 marker");
        Ok(clause.Contains("Exactly 2 people in frame: Linh, Minh", StringComparison.Ordinal), "T02 named count");
        Ok(clause.Contains("No second copy when someone enters", StringComparison.Ordinal), "T03 entrance does not spawn a copy");
        Ok(!clause.Contains("Motion:", StringComparison.Ordinal)
            && !clause.Contains("Timing:", StringComparison.Ordinal)
            && !clause.Contains("Duration:", StringComparison.Ordinal)
            && !clause.Contains("CameraMovement:", StringComparison.Ordinal)
            && !clause.Contains("Blocking:", StringComparison.Ordinal), "T04 clause avoids Runway slice markers");

        var attached = ProductionVisualContinuity.Attach(input);
        var again = ProductionVisualContinuity.Attach(attached);
        Ok(attached.MotionRequirements.Contains(ProductionVisualContinuity.Marker, StringComparison.Ordinal)
            && again.MotionRequirements == attached.MotionRequirements
            && attached.VisualContract == input.VisualContract
            && attached.Story == input.Story, "T05 attach once, visual and story unchanged");

        var padded = new string('x', 1800) + " Motion: blink. " + clause;
        var sent = VideoVisualIngressV1Rules.RunwayI2vPrompt(padded);
        Ok(sent.Length <= VideoVisualIngressV1Rules.RunwayPromptMax, "T06 capped");
        Ok(sent.Contains("Exactly 2 people in frame: Linh, Minh", StringComparison.Ordinal), "T07 continuity survives the cut");

        var legacy = VideoVisualIngressV1Rules.RunwayI2vPrompt(
            "[FAMIXA VISUAL UNIVERSE AUTHORITY V1] style lock Motion: Subtle blink. Camera remains steady.");
        Ok(legacy.StartsWith("Motion:", StringComparison.Ordinal)
            && !legacy.Contains("[FAMIXA VISUAL UNIVERSE AUTHORITY V1]", StringComparison.Ordinal), "T08 legacy motion slice unchanged");

        return fail;
    }
}
