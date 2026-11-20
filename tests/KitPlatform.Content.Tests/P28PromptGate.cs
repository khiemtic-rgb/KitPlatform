using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Production;
using Xunit;

namespace KitPlatform.Content.Tests;

public sealed class P28PromptGate
{
    public const string Story =
        "The door has just closed. Minh already stands before Linh, head lowered, backpack strap on one shoulder. Linh already stands at the table. Both remain in the room.";

    public const string Action =
        "Minh breathes, shifts weight, and the strap moves a little.";

    public const string Body =
        "Linh stops wiping, looks toward Minh, and moves one hand. Neither leaves the frame.";

    public const string Acting = Action + " " + Body;

    public const string Visual =
        """{"framing":"WIDE","lens":"35mm","camera":"Eye level","focus":"Linh","primary":{"id":"CHAR-003","name":"Linh","role":"mother"},"secondary":[{"id":"CHAR-001","name":"Minh","role":"son"}],"lock":"Exactly two people are present: Linh and Minh, each exactly once. Both are already visible in the first frame and remain in the room. Do not add a person or duplicate either character. Do not make either character enter from off-screen. Preserve faces, identity, wardrobe, positions, room and composition."}""";

    [Fact]
    public void Sh01_canonical_prompt_fits_runway_and_names_both_people()
    {
        var input = new ShotExecutionInput(Story, Acting, Visual, "", "kf", "5.0s", "", "");
        var attached = ProductionVisualContinuity.Attach(input);
        var joined = string.Join(" ", new[]
        {
            Story, Acting, Visual, ProductionVisualContinuity.Slice(attached.MotionRequirements),
        });
        var sent = VideoVisualIngressV1Rules.RunwayI2vPrompt(joined);
        var collapsed = System.Text.RegularExpressions.Regex.Replace(joined, @"\s+", " ").Trim();
        File.WriteAllText(@"E:\KitPlatform\.tmp\p28-prompt.txt", "JOIN=" + collapsed.Length + "\nLEN=" + sent.Length + "\n" + sent);
        var lower = sent.ToLowerInvariant();
        Assert.Equal(collapsed, sent);
        Assert.Contains("Linh", sent);
        Assert.Contains("Minh", sent);
        Assert.Contains("exactly two people", lower);
        Assert.Contains("composition.", sent);
        Assert.DoesNotContain("linh enters", lower);
        Assert.DoesNotContain("minh enters", lower);
        Assert.DoesNotContain("walks into frame", lower);
        Assert.Contains(ProductionVisualContinuity.Marker, sent);
        Assert.InRange(sent.Length, 1, 1000);
        Assert.DoesNotContain("bước vào", lower);
        Assert.DoesNotContain("entering", lower);
    }
}
