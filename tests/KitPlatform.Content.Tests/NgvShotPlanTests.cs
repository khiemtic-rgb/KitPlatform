using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Content.Tests;

public class NgvShotPlanTests
{
    const string Legacy = "Vertical 9:16 photoreal frame. Real materials only: wood, cardboard, plastic blister, and paper.";

    [Fact]
    public void No_plan_keeps_the_legacy_prompt_and_portrait_ratio()
    {
        var choice = NgvShotPlan.Resolve(null, 4, new[] { "Câu" }, "wide", Legacy);
        Assert.Equal(Legacy, choice.Prompt);
        Assert.Equal("9:16", choice.AspectRatio);
        Assert.Null(choice.Note);
        Assert.False(choice.UsedPlan);
    }

    [Fact]
    public void Matching_line_uses_the_shot_and_swaps_the_ratio()
    {
        var shots = new[]
        {
            new NgvShotPlan.Shot(0, 0, "Câu mở.", "Photoreal documentary photograph, 16:9, a room."),
            new NgvShotPlan.Shot(1, 3, "Câu hai.", "Photoreal documentary photograph, 16:9, a table."),
        };
        var lines = new[] { "Câu mở.", "Câu hai.", "Câu ba.", "Câu bốn." };

        var wide = NgvShotPlan.Resolve(shots, 2, lines, "wide", Legacy);
        Assert.True(wide.UsedPlan);
        Assert.Equal("16:9", wide.AspectRatio);
        Assert.Equal(1, wide.From);
        Assert.Equal(3, wide.To);
        Assert.Contains("16:9", wide.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("cardboard", wide.Prompt, StringComparison.Ordinal);

        var tall = NgvShotPlan.Resolve(shots, 2, lines, "tall", Legacy);
        Assert.Equal("9:16", tall.AspectRatio);
        Assert.Contains("9:16", tall.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("16:9", tall.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Nfc_trim_match_accepts_composed_characters()
    {
        var shots = new[] { new NgvShotPlan.Shot(0, 0, "café", "Photoreal, 16:9, cup.") };
        var choice = NgvShotPlan.Resolve(shots, 0, new[] { "  cafe\u0301  " }, "wide", Legacy);
        Assert.True(choice.UsedPlan);
        Assert.Null(choice.Note);
    }

    [Fact]
    public void Mismatched_first_line_falls_back_and_says_so()
    {
        var shots = new[] { new NgvShotPlan.Shot(4, 4, "Câu đúng.", "Photoreal, 16:9, desk.") };
        var choice = NgvShotPlan.Resolve(shots, 4, new[] { "", "", "", "", "Câu khác." }, "wide", Legacy);
        Assert.False(choice.UsedPlan);
        Assert.Equal(Legacy, choice.Prompt);
        Assert.Equal("9:16", choice.AspectRatio);
        Assert.Contains("không khớp câu 5", choice.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Da3_plan_has_44_shots_and_the_trial_indexes()
    {
        var path = RepoFile("docs/marketing/nguoigiuvon/kich-ban/DA3-luong-20-trieu-mang-bao-nhieu-di-dau-tu.anh.json");
        var shots = NgvShotPlan.Parse(File.ReadAllText(path));
        Assert.Equal(44, shots.Count);
        Assert.Equal(0, shots[0].From);
        Assert.Equal(0, shots[0].To);
        Assert.Equal(5, shots[4].From);
        Assert.Equal("Mình không nói với vợ.", shots[4].First);
        Assert.Equal(6, shots[5].From);
        Assert.StartsWith("Mấy ngày sau", shots[5].First, StringComparison.Ordinal);

        var lines = new string[103];
        foreach (var shot in shots)
            lines[shot.From] = shot.First;

        var opening = NgvShotPlan.Resolve(shots, 0, lines, "wide", Legacy);
        Assert.True(opening.UsedPlan);
        Assert.Equal("16:9", opening.AspectRatio);
        Assert.Contains("back to the camera", opening.Prompt, StringComparison.Ordinal);

        var hall = NgvShotPlan.Resolve(shots, 5, lines, "tall", Legacy);
        Assert.True(hall.UsedPlan);
        Assert.Equal(5, hall.From);
        Assert.Equal("9:16", hall.AspectRatio);
        Assert.DoesNotContain("16:9", hall.Prompt, StringComparison.Ordinal);

        var drifted = NgvShotPlan.Resolve(shots, 0, new[] { "Một câu khác." }, "wide", Legacy);
        Assert.False(drifted.UsedPlan);
        Assert.Equal(Legacy, drifted.Prompt);
    }

    [Fact]
    public void Da3_script_puts_short_lines_in_the_shot_that_contains_them()
    {
        var shots = NgvShotPlan.Parse(File.ReadAllText(RepoFile("docs/marketing/nguoigiuvon/kich-ban/DA3-luong-20-trieu-mang-bao-nhieu-di-dau-tu.anh.json")));
        var lines = SpokenLines(File.ReadAllText(RepoFile("docs/marketing/nguoigiuvon/kich-ban/DA3-luong-20-trieu-mang-bao-nhieu-di-dau-tu.txt")));
        Assert.Equal(117, lines.Count);
        var map = NgvShotPlan.Map(shots, lines);
        Assert.NotNull(map);
        Assert.Equal(0, map![0]);
        Assert.Equal(4, map[5]);
        Assert.Equal(5, map[6]);
        Assert.Equal(map[15], map[16]);
        Assert.StartsWith("Giao dịch có rủi ro", lines[16], StringComparison.Ordinal);

        var shared = NgvShotPlan.Resolve(shots, 16, lines, "wide", Legacy);
        Assert.True(shared.UsedPlan);
        Assert.True(shared.From <= 16 && shared.To >= 16);
        Assert.DoesNotContain("cardboard", shared.Prompt, StringComparison.Ordinal);

        var step = NgvShotPlan.Resolve(shots, lines.FindIndex(line => line.StartsWith("mình lấy tiền trả nợ", StringComparison.Ordinal)), lines, "wide", Legacy);
        Assert.True(step.UsedPlan);
        Assert.Contains("glass jars", step.Prompt, StringComparison.OrdinalIgnoreCase);
    }

    static List<string> SpokenLines(string script)
    {
        var lines = new List<string>();
        var header = true;
        foreach (var raw in script.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = raw.Trim();
            if (header && (trimmed.Length == 0 || trimmed.StartsWith('#'))) continue;
            header = false;
            if (trimmed.Length == 0) continue;
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^(?:cảnh|canh|sc)\s*\d+", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                continue;
            var text = NgvShotPlan.Spoken(trimmed);
            if (text.Length > 0) lines.Add(text);
        }
        return lines;
    }

    static string RepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var path = Path.Combine(dir.FullName, relative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(path)) return path;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
