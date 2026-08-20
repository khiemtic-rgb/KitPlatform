using System.Text.Json;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class ContentSeriesScriptDraftService : IContentSeriesScriptDraftService
{
    private readonly ContentGeminiClient _gemini;
    private readonly ContentRepository _repo;

    public ContentSeriesScriptDraftService(ContentGeminiClient gemini, ContentRepository repo)
    {
        _gemini = gemini;
        _repo = repo;
    }

    public async Task<ContentSeriesScriptDraftDto> DraftAsync(
        ContentSeriesScriptDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var seed = (request.Seed ?? string.Empty).Trim();
        if (seed.Length < 12)
            throw new InvalidOperationException("Nhập hạt giống ít nhất một câu (12 ký tự) — KIT không bịa tập trống.");
        if (seed.Length > 2000)
            throw new InvalidOperationException("Hạt giống quá dài (tối đa 2000 ký tự).");

        var ai = await _gemini.ResolveConfigAsync(cancellationToken);
        if (!ai.ApiKeyConfigured)
        {
            throw new InvalidOperationException(
                "Chưa có key Gemini — Nội dung → Model AI, hoặc env GEMINI_API_KEY. Không trừ credit Runway.");
        }

        var org = await _repo.GetOrgSettingsAsync(cancellationToken);
        var estimate = org.TextPackEstimateUsd < 0 ? 0 : org.TextPackEstimateUsd;
        var hint = Clip(request.CharactersHint, 800);
        var episode = Clip(request.EpisodeHint, 80);
        var brand = await ResolveBrandAsync(request.BrandId, cancellationToken);
        var knowledge = brand is null
            ? ContentBrandKnowledge.Empty
            : ContentBrandKnowledge.Parse(brand.ToneJson, brand.VisualKitJson);
        var brain = brand is null
            ? ""
            : ContentBrandKnowledge.FormatForSeriesDraft(knowledge, brand.OperationalBrief);
        var usedBrain = !string.IsNullOrWhiteSpace(brain);

        var raw = await _gemini.GenerateJsonAsync(
            SystemPrompt,
            UserPrompt(seed, hint, episode, brain),
            cancellationToken,
            4096);
        var pack = ExtractPack(raw);
        if (!LooksLikePack(pack))
            throw new InvalidOperationException("Gemini không trả FAMIXA PACK đọc được. Sửa hạt giống rồi đề xuất lại.");

        var note = usedBrain
            ? $"Gemini text — ước ${estimate:0.##}. Dùng Brand Brain {brand!.Code} (không dán 48 tài liệu). 0 cr Runway."
            : $"Gemini text — ước ${estimate:0.##} (trần pack). Chưa có Brand Brain Famixa. 0 cr Runway.";

        return new ContentSeriesScriptDraftDto(
            pack,
            string.IsNullOrWhiteSpace(ai.TextModel) ? "gemini" : ai.TextModel,
            estimate,
            note,
            usedBrain,
            brand?.Code);
    }

    private static string Clip(string? raw, int max)
    {
        var t = (raw ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length <= max ? t : t[..max];
    }

    private static string ExtractPack(string raw)
    {
        var text = (raw ?? string.Empty).Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = text.IndexOf('\n');
            if (nl > 0) text = text[(nl + 1)..];
            var fence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (fence >= 0) text = text[..fence];
            text = text.Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.Equals("pack", StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        return (prop.Value.GetString() ?? string.Empty).Trim();
                    }
                }
            }
        }
        catch (JsonException)
        {
            /* raw pack */
        }

        return text;
    }

    private static bool LooksLikePack(string pack)
    {
        if (string.IsNullOrWhiteSpace(pack) || pack.Length < 40 || pack.Length > 16_000)
            return false;
        return pack.Contains("FAMIXA PACK", StringComparison.OrdinalIgnoreCase)
               || pack.Contains("--- SHOT ---", StringComparison.OrdinalIgnoreCase)
               || pack.Contains("--- SHORT ---", StringComparison.OrdinalIgnoreCase)
               || pack.Contains("VIDEO ID", StringComparison.OrdinalIgnoreCase);
    }

    private const string SystemPrompt =
        """
        You write a draft FAMIXA series pack for KIT operators. Output JSON only: {"pack":"<text>"}.
        The pack MUST use this shape (fill every field; Vietnamese story; English MOTION):

        FAMIXA PACK
        SERIES: FAMIXA
        EP: EP01
        TITLE:
        PREMISE:
        MORAL:
        CTA: no-app
        ROLE: <role> | <name>
        ROLE: <role> | <name>

        --- SHORT ---
        ID: S01
        SCENE: SC01
        CHAR: CHAR-001, CHAR-002
        HOOK:
        VISUAL:
        SECONDS: 7
        MOTION: <English I2V, natural, no looking at camera>
        MOTION_VI:

        --- SHOT ---
        ID: SC01-SH01
        SCENE: SC01
        SHOT: SH01
        STORY:
        VISUAL:
        SECONDS: 5
        CHAR: CHAR-001, CHAR-002, CHAR-003
        LOC:
        MOTION:
        MOTION_VI:

        Rules:
        - Apply locked FAMIXA KNOWLEDGE if provided. Do not reinvent Famixa DNA, philosophy, or characters.
        - 2 ROLE lines minimum when the seed has a family; keep CHAR-001/002/003 stable if the user listed them.
        - 1–2 SHORT blocks (9:16) and 3–6 SHOT blocks (16:9) for ONE scene. No extra episodes.
        - CTA always no-app. No Novixa/Famixa app pitch. No violence, no nude.
        - MOTION is English only. MOTION_VI is Vietnamese. Do not paste VIDEO PRODUCTION MASTER essays.
        - Do not invent wardrobe that conflicts with the character hint.
        - Do not add Next production target / SC02 unless the seed asks for a second scene.
        - Do not auto-resolve conflict, apologize, forgive, hug, teach a lesson, or reset the family unless the seed explicitly requires it.
        - An episode may end unresolved. Do not close a story thread the operator did not close.
        """;

    private async Task<ContentRepository.BrandRow?> ResolveBrandAsync(Guid? brandId, CancellationToken cancellationToken)
    {
        if (brandId is { } id && id != Guid.Empty)
            return await _repo.GetBrandAsync(id, cancellationToken);

        var all = await _repo.ListBrandsAsync(true, cancellationToken);
        return all.FirstOrDefault(b =>
                   b.Code.Contains("FAMIXA", StringComparison.OrdinalIgnoreCase)
                   || b.Name.Contains("Famixa", StringComparison.OrdinalIgnoreCase));
    }

    private static string UserPrompt(string seed, string charactersHint, string episodeHint, string brain) =>
        "Hạt giống (operator viết — giữ tinh thần này, đừng đổi thành chuyện khác):\n"
        + seed
        + (string.IsNullOrEmpty(episodeHint) ? "" : $"\n\nMã tập gợi ý: {episodeHint}")
        + (string.IsNullOrEmpty(charactersHint)
            ? "\n\nChưa có Canon. Dùng CHAR-001 Minh (con), CHAR-002 Nam (bố), CHAR-003 Linh (mẹ) nếu hạt giống là gia đình Famixa."
            : $"\n\nCanon phải giữ đúng id/tên:\n{charactersHint}")
        + (string.IsNullOrEmpty(brain)
            ? ""
            : "\n\nFAMIXA KNOWLEDGE (locked — apply, do not rewrite):\n" + brain);
}
