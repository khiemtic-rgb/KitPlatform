using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Production;

/// <summary>
/// The 984-character SH03 prompt plus the 186-character continuity clause used to be
/// joined and then cut inside the character JSON. No provider is called.
/// </summary>
public static class ProductionRunwayPromptCapV1Regression
{
    const string Approved =
        "Người mẹ đón lấy, liếc nhanh con số đỏ chót rồi nhìn thẳng vào mắt con. Continuity: this is the same folded test from the previous shot. Prop: the score on the paper is red. Exactly two people. No third person, no ghost, no duplicate body. Both of Linh's eyes stay visible for the whole shot. She does not turn into profile. Her mouth stays a visible shape in three-quarter view toward Minh, not a closed line. Linh's face stays the large face. She looks at Minh, not the lens. She holds the folded test with both hands. Minh stays beside her, head lowered. Exactly two intact people. The camera stays still. Linh looks toward Minh. Linh looks toward Minh. {\"gaze\":\"Linh looks at Minh. Her mouth is a visible shape in three-quarter view.\",\"lens\":\"35mm\",\"focus\":\"Linh\",\"camera\":\"Eye level\",\"shotId\":\"EP01-SC01-SH03\",\"framing\":\"MEDIUM\",\"primary\":{\"id\":\"CHAR-003\",\"name\":\"Linh\",\"role\":\"primary\"},\"secondary\":[{\"id\":\"CHAR-001\",\"name\":\"Minh\",\"role\":\"secondary\"}],\"visualUniverse\":\"FAMIXA\"}";

    public static IReadOnlyList<string> Run()
    {
        var fail = new List<string>();
        void Ok(bool cond, string name)
        {
            if (!cond) fail.Add(name);
        }

        var visualAt = Approved.IndexOf('{');
        var visual = Approved[visualAt..];
        var head = Approved[..(visualAt - 1)];
        var actingAt = head.IndexOf(" Exactly two people.", StringComparison.Ordinal);
        var story = head[..actingAt];
        var acting = head[(actingAt + 1)..];
        var clause = ProductionVisualContinuity.Format(new ShotExecutionInput(
            story, acting, visual, "", "kf", "5s", "", ""));
        var joined = Approved + " " + clause;
        Ok(story.Length == 173 && acting.Length == 482 && visual.Length == 327
            && Approved.Length == 984 && clause.Length == 186 && joined.Length == 1171,
            "T01 the approved prompt is 984 and continuity is 186");

        var over = RunwayPromptBudget.Decide(story, acting, visual, clause);
        Ok(!over.Fits && !over.ContinuityIncluded
            && over.ApprovalPrompt == Approved
            && over.Reason!.Contains(ProductionFailureReasons.RunwayPromptOverCap, StringComparison.Ordinal)
            && over.Reason.Contains("joined=1171", StringComparison.Ordinal)
            && over.Reason.Contains("cap=1000", StringComparison.Ordinal)
            && over.Reason.Contains("continuity=186", StringComparison.Ordinal),
            "T02 an over-cap join is reported and the approval prompt is the intact 984");

        var wire = VideoVisualIngressV1Rules.RunwayI2vPrompt(joined);
        Ok(wire == Approved
            && wire.Length == 984
            && wire.Contains("\"id\":\"CHAR-003\"", StringComparison.Ordinal)
            && wire.Contains("\"id\":\"CHAR-001\"", StringComparison.Ordinal)
            && wire.Contains("\"visualUniverse\":\"FAMIXA\"", StringComparison.Ordinal)
            && !wire.Contains(ProductionVisualContinuity.Marker, StringComparison.Ordinal)
            && RunwayPromptBudget.Intact(wire)
            && JsonPrimary(wire) == "CHAR-003",
            "T03 the wire prompt keeps the character JSON and does not cut a field");

        var transport = new FakeRunwayTransport();
        var adapter = new RunwayMotionExecutionAdapter(transport);
        var refused = false;
        try
        {
            adapter.StartFrozenAsync(Snap(story, acting, visual, clause), new MotionStartContext(null, null), default)
                .GetAwaiter().GetResult();
        }
        catch (InvalidOperationException ex)
        {
            refused = ex.Message.Contains(ProductionFailureReasons.RunwayPromptOverCap, StringComparison.Ordinal)
                && ex.Message.Contains(Approved, StringComparison.Ordinal);
        }
        Ok(refused && transport.CreateCalls == 0 && transport.LastCreateRequest is null,
            "T04 the over-cap prompt is not sent");

        var shortStory = "Linh looks at Minh.";
        var shortActing = "The camera stays still.";
        var fit = RunwayPromptBudget.Decide(shortStory, shortActing, visual, clause);
        adapter.StartFrozenAsync(Snap(shortStory, shortActing, visual, clause), new MotionStartContext(null, null), default)
            .GetAwaiter().GetResult();
        var sent = transport.LastCreateRequest?.PromptText ?? "";
        Ok(fit.Fits && fit.ContinuityIncluded
            && sent == fit.PromptText
            && sent.Length <= VideoVisualIngressV1Rules.RunwayPromptMax
            && sent.Contains(ProductionVisualContinuity.Marker, StringComparison.Ordinal)
            && sent.Contains("\"id\":\"CHAR-003\"", StringComparison.Ordinal)
            && VideoVisualIngressV1Rules.RunwayI2vPrompt(sent) == sent
            && transport.CreateCalls == 1,
            "T05 a join that fits is sent whole, after the cap");

        var heavy = RunwayPromptBudget.Decide(new string('A', 800), new string('B', 800), visual, clause);
        Ok(!heavy.Fits && heavy.ApprovalPrompt == visual && RunwayPromptBudget.Intact(heavy.ApprovalPrompt),
            "T06 a frozen prompt over the cap keeps the visual field whole");

        var sh04 = RunwayPromptBudget.Decide("SH04 stays silent.", acting, visual, clause);
        var veo = VeoMotionPrompt.FromFrozen(Snap(story, acting, visual, clause));
        Ok(!sh04.Fits && sh04.ApprovalPrompt.Contains("\"id\":\"CHAR-001\"", StringComparison.Ordinal)
            && !veo.Contains(ProductionVisualContinuity.Marker, StringComparison.Ordinal),
            "T07 every Runway shot uses this cap and Veo does not append continuity");

        return fail;
    }

    static string JsonPrimary(string prompt)
    {
        var start = prompt.IndexOf('{');
        var end = prompt.LastIndexOf('}');
        using var doc = JsonDocument.Parse(prompt[start..(end + 1)]);
        return doc.RootElement.GetProperty("primary").GetProperty("id").GetString() ?? "";
    }

    static ProductionTaskInputSnapshot Snap(string story, string acting, string visual, string shotContract) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            story,
            acting,
            visual,
            shotContract,
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            "",
            ProductionMotionCatalog.RunwayProviderId,
            ProductionMotionCatalog.RunwayGen4Turbo,
            "",
            DateTimeOffset.UnixEpoch);
}
