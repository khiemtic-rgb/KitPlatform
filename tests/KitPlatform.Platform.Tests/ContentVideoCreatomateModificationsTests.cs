using KitPlatform.Packs.Content;
using Xunit;

namespace KitPlatform.Platform.Tests;

public sealed class ContentVideoCreatomateModificationsTests
{
    private const string Config = """
        {
          "voiceKey": "Giong_doc",
          "modifications": {
            "Tieu_de.text": "{{script}}",
            "Video-1.duration": "5",
            "Video-4.time": "15",
            "Video-4.duration": "5"
          },
          "sceneTextKeys": { "HOOK": "Hook.text" },
          "sceneImageKeys": { "HOOK": "Hook.image" },
          "sceneVideoKeys": { "HOOK": "Video-1", "PROBLEM": "Video-2" }
        }
        """;

    [Fact]
    public void Script_is_written_to_the_configured_text_element()
    {
        var mods = ContentVideoCreatomateModifications.Build(
            Config,
            "Nhà thuốc đang mất tiền",
            "Doanh thu tăng không đồng nghĩa với lợi nhuận tăng.",
            [new ContentVideoCreatomateScene("HOOK", "Mất tiền ở quầy", null)],
            voicePublicUrl: null);

        Assert.Equal("Doanh thu tăng không đồng nghĩa với lợi nhuận tăng.", mods["Tieu_de.text"]);
        Assert.Equal("5", mods["Video-1.duration"]);
        Assert.Equal("15", mods["Video-4.time"]);
        Assert.Equal("Mất tiền ở quầy", mods["Hook.text"]);
        Assert.Equal("0%", mods["Giong_doc.volume"]);
    }

    [Fact]
    public void Scene_video_slot_takes_a_video_file_and_leaves_a_still_alone()
    {
        var mods = ContentVideoCreatomateModifications.Build(
            Config,
            "title",
            "script",
            [
                new ContentVideoCreatomateScene("HOOK", "hook", "https://cdn.example/hook.mp4"),
                new ContentVideoCreatomateScene("PROBLEM", "problem", "https://cdn.example/problem.jpg"),
            ],
            voicePublicUrl: null);

        Assert.Equal("https://cdn.example/hook.mp4", mods["Video-1.source"]);
        Assert.False(mods.ContainsKey("Video-2.source"));
        Assert.Equal("https://cdn.example/problem.jpg", mods["PROBLEM.image"]);
    }

    [Fact]
    public void Public_voice_replaces_the_placeholder_and_is_not_muted()
    {
        var mods = ContentVideoCreatomateModifications.Build(
            Config,
            "title",
            "script",
            [],
            "https://media.example/voice.mp3");

        Assert.Equal("https://media.example/voice.mp3", mods["Giong_doc.source"]);
        Assert.Equal("https://media.example/voice.mp3", mods["Giong_doc"]);
        Assert.False(mods.ContainsKey("Giong_doc.volume"));
    }
}
