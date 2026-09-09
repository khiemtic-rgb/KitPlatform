using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace KitPlatform.Packs.Content;

public static class KitVideoMasterCreationRules
{
    public const string DocumentId = "CHAR-001_MINH_MASTER_REFERENCE_CREATION_V1";
    public const int MaxBatch = 4;
    public const int MaxAutoAttempts = 3;

    public static string CompilePrompt(string view, string expression, string? diagnosis = null, string? parentCode = null)
    {
        var v = string.IsNullOrWhiteSpace(view) ? "FRONT" : view.Trim().ToUpperInvariant();
        var e = string.IsNullOrWhiteSpace(expression) ? "NEUTRAL" : expression.Trim().ToUpperInvariant();
        var repair = SanitizeRepair(diagnosis);
        var lines = new List<string>
        {
            "[STYLE] Famixa stylized cinematic human. Not photoreal. Not cartoon. Not anime.",
            "[CHARACTER DNA] Minh 11. Soft slightly oval child face, wide-ish forehead, small chin. Relatively large natural eyes, not anime, not doll-glass. Hair: black or dark-brown modern child cut; silhouette is Canon. Child proportion, never adult body with child face.",
            "[BRIEF] Everyday boy who holds emotion before showing it. Not hot boy, not ad-child.",
            "[ERA] ERA-01 age 11.",
            "[REFERENCE] Visual Style + Visual DNA only. No Golden Shot. No SH01-01. No random still.",
            $"[VIEW] {v} upper body unless FULL_BODY. Single boy. Simple neutral background. Soft face-clear light.",
            $"[EXPRESSION] {e}. Subtle. Identity must not change.",
            "[WARDROBE] HOME baseline T-shirt. Wardrobe is not identity.",
            "[IMAGE CONTRACT] single_character single_image single_subject single_composition no_panels no_collage no_storyboard no_watermark no_logo no_unrequested_text",
            "[FORBIDDEN] character sheet, collage, storyboard, multi-panel, watermark, logo, photoreal child, anime, cartoon, adult body",
        };
        if (!string.IsNullOrWhiteSpace(parentCode))
            lines.Insert(7, $"[FAMILY] Same boy as {SanitizeRepair(parentCode)}. Variation only. Do not invent a new Minh.");
        if (!string.IsNullOrWhiteSpace(repair))
            lines.Insert(7, $"[REPAIR] Revise only the failed look. Keep the same boy identity. {repair}");
        return string.Join('\n', lines);
    }

    public static string Fingerprint(string prompt, string view, string expression, string? diagnosis = null, string? parentCode = null)
    {
        var raw = $"{prompt}|CHAR-001|ERA-01|{view}|{expression}|V1|{SanitizeRepair(diagnosis)}|{SanitizeRepair(parentCode)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    public static string SanitizeRepair(string? diagnosis)
    {
        if (string.IsNullOrWhiteSpace(diagnosis)) return "";
        var t = diagnosis.Trim().Replace('{', ' ').Replace('}', ' ');
        return t.Length > 400 ? t[..400] : t;
    }

    public static void EnsureCanGenerate(bool confirmed, int batchCount, int attemptsOnFingerprint, bool sameFingerprint, string? diagnosis, bool locked)
    {
        if (locked) throw new InvalidOperationException("MASTER_LOCKED");
        if (!confirmed) throw new InvalidOperationException("CREDIT_GATE: chưa xác nhận — không gọi generator.");
        if (batchCount >= MaxBatch) throw new InvalidOperationException("BATCH_FULL: tối đa 4 ảnh thử / đợt.");
        if (sameFingerprint && string.IsNullOrWhiteSpace(diagnosis))
            throw new InvalidOperationException("DO_NOT_BLIND_RETRY");
        if (attemptsOnFingerprint >= MaxAutoAttempts)
            throw new InvalidOperationException("DIRECTOR_REVIEW_REQUIRED: MAX_AUTO_ATTEMPTS=3");
    }

    public static bool IsSheet(string? imageType) =>
        imageType is "CHARACTER_SHEET" or "COLLAGE" or "MULTI_PANEL" or "REFERENCE_BOARD" or "STORYBOARD" or "TEXT_HEAVY";

    public static bool ProductionAccepts(string? status) =>
        string.Equals(status, "LOCKED", StringComparison.OrdinalIgnoreCase);

    public static JsonElement VisionContract()
    {
        using var doc = JsonDocument.Parse("""
            {
              "purpose": "MASTER_REFERENCE",
              "characters": [{"id":"CHAR-001","age":11,"era":"ERA-01"}],
              "imageContract": "single_character single_image",
              "forbidden": ["CHARACTER_SHEET","COLLAGE","MULTI_PANEL","WATERMARK"]
            }
            """);
        return doc.RootElement.Clone();
    }
}
