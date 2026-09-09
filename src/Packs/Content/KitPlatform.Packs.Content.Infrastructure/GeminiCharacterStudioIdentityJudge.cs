using Microsoft.Extensions.Logging;
using KitPlatform.Packs.Content;

namespace KitPlatform.Packs.Content.Infrastructure;

internal sealed class GeminiCharacterStudioIdentityJudge : ICharacterStudioIdentityJudge
{
    private readonly ContentGeminiClient _gemini;
    private readonly ILogger<GeminiCharacterStudioIdentityJudge> _logger;

    public GeminiCharacterStudioIdentityJudge(
        ContentGeminiClient gemini,
        ILogger<GeminiCharacterStudioIdentityJudge> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, int>?> ScoreViewAsync(
        string view,
        byte[] candidate,
        byte[] master,
        int ageMin,
        int ageMax,
        CancellationToken cancellationToken)
    {
        if (candidate is not { Length: > 32 } || master is not { Length: > 32 })
            return null;
        var masterJpeg = ToVisionJpeg(master);
        var candidateJpeg = ToVisionJpeg(candidate);
        var images = new List<(string Mime, string Base64, string Label)>
        {
            ("image/jpeg", Convert.ToBase64String(masterJpeg), "MASTER — identity lock. Compare the candidate to this face, age, hair, and wardrobe."),
            ("image/jpeg", Convert.ToBase64String(candidateJpeg), $"CANDIDATE {view} — score only this image."),
        };
        try
        {
            var raw = await _gemini.GenerateJsonWithImagesAsync(
                "You are a character-reference identity judge. Return ONLY JSON scores 0-100. Do not approve. Do not invent a name.",
                CharacterStudioIdentityLockV1Rules.VisionUserPrompt(view, ageMin, ageMax),
                images, cancellationToken);
            var parsed = CharacterStudioIdentityLockV1Rules.ParseScores(ExtractJson(raw));
            if (parsed is null)
                _logger.LogWarning("Identity judge {View} returned unparseable JSON", view);
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Identity judge {View} failed", view);
            return null;
        }
    }

    private static byte[] ToVisionJpeg(byte[] source)
    {
        try { return KitVideoArtifactStore.NormalizeJpeg(source); }
        catch { return source; }
    }

    private static string ExtractJson(string raw)
    {
        var t = (raw ?? "").Trim();
        var start = t.IndexOf('{');
        var end = t.LastIndexOf('}');
        return start >= 0 && end > start ? t[start..(end + 1)] : t;
    }
}
