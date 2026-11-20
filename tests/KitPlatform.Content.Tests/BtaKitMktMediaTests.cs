using System.Text.Json;
using KitPlatform.Packs.Content;
using KitPlatform.Packs.Content.Infrastructure;
using Xunit;

namespace KitPlatform.Content.Tests;

public class BtaKitMktMediaTests
{
    [Fact]
    public void Text_to_image_keeps_model_ratio_and_cuts_prompt_at_1000()
    {
        var built = BtaRunwayTextToImage.Build(new string('x', 1200), "1920:1080", "gen4_image", 7);
        Assert.Equal("gen4_image", built.Model);
        Assert.Equal("1920:1080", built.Ratio);
        Assert.Equal(1000, built.PromptText.Length);
        Assert.Equal(8, built.CreditUocTinh);
        Assert.Equal(7, built.Seed);

        var small = BtaRunwayTextToImage.Build("a cat", "1280:720", "gen4_image", null);
        Assert.Equal("1280:720", small.Ratio);
        Assert.Equal(5, small.CreditUocTinh);
        Assert.Null(small.Seed);
    }

    [Fact]
    public void Text_to_image_rejects_unknown_model_or_ratio()
    {
        var model = Assert.Throws<InvalidOperationException>(() =>
            BtaRunwayTextToImage.Build("a cat", "1920:1080", "gen4_turbo", null));
        Assert.Contains("gen4_image", model.Message, StringComparison.Ordinal);

        var ratio = Assert.Throws<InvalidOperationException>(() =>
            BtaRunwayTextToImage.Build("a cat", "1024:1024", "gen4_image", null));
        Assert.Contains("1920:1080", ratio.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_file_credits_are_32_and_40()
    {
        var json = File.ReadAllText(RepoFile("docs/marketing/bta/hinh-ai/01/lenh-tao-anh.json"));
        var a = BtaImageCommandPlan.Read(json, "A");
        var b = BtaImageCommandPlan.Read(json, "B");
        Assert.Equal(4, a.Anh.Count);
        Assert.Equal(32, a.TongCredit);
        Assert.Equal(5, b.Anh.Count);
        Assert.Equal(40, b.TongCredit);
        Assert.All(a.Anh.Concat(b.Anh), item =>
        {
            Assert.Equal("gen4_image", item.Model);
            Assert.Equal("1920:1080", item.Ratio);
            Assert.Equal(8, item.CreditUocTinh);
            Assert.InRange(item.PromptText.Length, 1, 1000);
        });
        var second = Assert.Single(b.Anh, item => item.File == "c07-rats-wedding.b.png");
        Assert.Equal(702, second.Seed);
        Assert.True(BtaImageFiles.IsProtected("a01-placemat.png"));
        Assert.False(BtaImageFiles.IsProtected("c03-japan-forest-boar.png"));
    }

    [Fact]
    public void Voice_request_uses_model_voice_and_settings()
    {
        var speech = BtaElevenLabsSpeech.Build("Hello.", "model-from-api", null);
        Assert.Equal("model-from-api", speech.ModelId);
        Assert.Equal("wJGJJlqiL7g3nHR8c3SQ", speech.VoiceId);
        Assert.Equal(0.5, speech.Stability);
        Assert.Equal(0.75, speech.SimilarityBoost);
        Assert.Equal(0.0, speech.Style);
        Assert.True(speech.UseSpeakerBoost);
        Assert.EndsWith("/wJGJJlqiL7g3nHR8c3SQ/with-timestamps", speech.Path, StringComparison.Ordinal);

        var json = JsonSerializer.Serialize(speech.Body());
        Assert.Contains("\"model_id\":\"model-from-api\"", json, StringComparison.Ordinal);
        Assert.Contains("\"stability\":0.5", json, StringComparison.Ordinal);
        Assert.Contains("\"similarity_boost\":0.75", json, StringComparison.Ordinal);
        Assert.Contains("\"use_speaker_boost\":true", json, StringComparison.Ordinal);
        Assert.Equal("eleven_v3", ContentElevenLabsClient.ResolveTtsModelId(null));
        Assert.Equal("model-from-api", ContentElevenLabsClient.ResolveTtsModelId(" model-from-api "));
    }

    [Fact]
    public void Voice_script_drops_hash_lines_and_splits_three_parts()
    {
        var sample = """
            # không gửi
            # ===== PHẦN 1: a =====
            hello
            # bỏ
            world
            # ===== PHẦN 2: b =====
            # chỉ chú thích
            second
            # ===== PHẦN 3: c =====
            third
            """;
        var parts = BtaVoiceScript.Split(sample);
        Assert.Equal(3, parts.Count);
        Assert.Equal("hello\nworld", parts[0].Text);
        Assert.Equal("second", parts[1].Text);
        Assert.Equal("third", parts[2].Text);
        Assert.DoesNotContain("#", parts[0].Text, StringComparison.Ordinal);

        var script = File.ReadAllText(RepoFile("docs/marketing/bta/kich-ban/01-vietnam-zodiac-cat.giong-v4.txt"));
        var real = BtaVoiceScript.Split(script);
        Assert.Equal(3, real.Count);
        Assert.StartsWith("[warm, curious] You might know", real[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("[playful] Now, the cat.", real[1].Text, StringComparison.Ordinal);
        Assert.StartsWith("[lowers voice, a little eerie]", real[2].Text, StringComparison.Ordinal);
        Assert.All(real, part => Assert.DoesNotContain("\n#", "\n" + part.Text, StringComparison.Ordinal));
        Assert.True(BtaVoiceScript.ProbeSentence.Length < BtaVoiceScript.ProbeMaxChars);
    }

    [Fact]
    public void Timing_json_exposes_character_marks()
    {
        const string json = """
            {
              "alignment": {
                "characters": ["Y", "o", "u"],
                "character_start_times_seconds": [0.0, 0.1, 0.2],
                "character_end_times_seconds": [0.1, 0.2, 0.3]
              },
              "normalized_alignment": {
                "characters": ["Y", "o", "u"],
                "character_start_times_seconds": [0.0, 0.12, 0.22],
                "character_end_times_seconds": [0.12, 0.22, 0.34]
              }
            }
            """;
        var read = BtaVoiceTimingReader.Read(json);
        Assert.Equal(3, read.CharacterCount);
        Assert.Equal(0.0, read.FirstStartSeconds);
        Assert.Equal(0.3, read.LastEndSeconds);
        Assert.Equal(3, read.NormalizedCharacterCount);
    }

    [Fact]
    public void Lot_c_redraws_three_dong_ho_images_inside_the_raised_cap()
    {
        var json = File.ReadAllText(RepoFile("docs/marketing/bta/hinh-ai/01/lenh-tao-anh.json"));
        Assert.Equal(120, BtaImageCommandPlan.Cap(json));
        Assert.Equal(72, BtaImageCommandPlan.Cap("{\"anh\":[]}"));
        var c = BtaImageCommandPlan.Read(json, "C");
        Assert.Equal(6, c.Anh.Count);
        Assert.Equal(48, c.TongCredit);
        Assert.All(c.Anh, item => Assert.InRange(item.PromptText.Length, 1, 999));
        Assert.Contains(c.Anh, item => item.File == "c09-village-cat-v2.b.png" && item.Seed == 902);
        Assert.NotNull(BtaImageCommandPlan.FindItem(json, "c08-cat-areca-v2.b.png"));
        Assert.NotNull(BtaImageCommandPlan.FindItem(json, "c03-japan-forest-boar.png"));
        Assert.Null(BtaImageCommandPlan.FindItem(json, "khong-co.png"));
    }

    [Fact]
    public void Keys_follow_famixa_credential_models()
    {
        Assert.Equal(KitPlatform.Packs.Content.Production.ProductionMotionCatalog.RunwayGen4Turbo,
            BtaKitMktKeys.RunwayCredentialModel);
        Assert.Equal(KitPlatform.Packs.Content.Production.ProductionVoiceCatalog.ElevenV3,
            BtaKitMktKeys.ElevenLabsCredentialModel);
        Assert.True(BtaKitMktKeys.LooksLikeRunwayKey(" key_abc "));
        Assert.False(BtaKitMktKeys.LooksLikeRunwayKey("sk-abc"));
        Assert.False(BtaKitMktKeys.LooksLikeRunwayKey(null));
        Assert.True(BtaKitMktKeys.IsMissingCredential("PRODUCTION_CREDENTIAL_UNRESOLVED"));
        Assert.True(BtaKitMktKeys.IsMissingCredential("PRODUCTION_CREDENTIAL_AMBIGUOUS"));
        Assert.False(BtaKitMktKeys.IsMissingCredential("PRODUCTION_CREDENTIAL_REVOKED"));
    }

    [Fact]
    public void Model_list_denied_falls_back_to_eleven_v4()
    {
        Assert.Equal("eleven_v4", BtaElevenLabsSpeech.ModelIdFallback);
        Assert.True(BtaElevenLabsSpeech.ModelsListDenied("ElevenLabs models 401: missing_permissions models_read"));
        Assert.True(BtaElevenLabsSpeech.ModelsListDenied("ElevenLabs models 403: forbidden"));
        Assert.False(BtaElevenLabsSpeech.ModelsListDenied("ElevenLabs models 500: oops"));
        Assert.False(BtaElevenLabsSpeech.ModelsListDenied("ElevenLabs TTS failed (401): bad key"));
        Assert.False(BtaElevenLabsSpeech.ModelsListDenied(null));
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
