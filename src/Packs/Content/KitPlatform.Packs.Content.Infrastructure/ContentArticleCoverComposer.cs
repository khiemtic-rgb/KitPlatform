using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Ảnh bìa vuông vẽ tại máy từ tên thương hiệu, tiêu đề và một câu của bài.
/// Không gọi Gemini hay Pollinations.
/// </summary>
internal static class ContentArticleCoverComposer
{
    public const int Size = 1080;

    public static byte[] Render(string brandName, string title, string line)
    {
        var brand = Clean(brandName, 48);
        var heading = Clean(title, 180);
        var note = Clean(line, 160);
        if (string.Equals(note, heading, StringComparison.OrdinalIgnoreCase))
            note = "";
        if (heading.Length == 0)
            throw new InvalidOperationException("Bài không có tiêu đề để vẽ ảnh bìa.");

        var palette = Palettes[PaletteIndex(heading)];
        using var face = LoadFace();
        using var bitmap = new SKBitmap(Size, Size, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(palette.Paper);
        using var band = new SKPaint { Color = palette.Accent, IsAntialias = true };
        canvas.DrawRect(new SKRect(0, 0, 28, Size), band);

        var y = 132f;
        using (var kickerFont = new SKFont(face, 32))
        using (var kickerPaint = new SKPaint { Color = palette.Accent, IsAntialias = true })
        {
            canvas.DrawText(brand.Length == 0 ? "Nhà thuốc" : brand, 88, y, kickerFont, kickerPaint);
        }

        y = 220f;
        using (var titleFont = new SKFont(face, 64))
        using (var titlePaint = new SKPaint { Color = palette.Ink, IsAntialias = true })
        {
            foreach (var row in Wrap(titleFont, heading, Size - 176, 5))
            {
                canvas.DrawText(row, 88, y, titleFont, titlePaint);
                y += 82;
            }
        }

        if (note.Length > 0)
        {
            y += 28;
            using var rule = new SKPaint { Color = palette.Accent, StrokeWidth = 4, IsAntialias = true };
            canvas.DrawLine(88, y, 280, y, rule);
            y += 64;
            using var noteFont = new SKFont(face, 34);
            using var notePaint = new SKPaint { Color = palette.Muted, IsAntialias = true };
            foreach (var row in Wrap(noteFont, note, Size - 176, 3))
            {
                canvas.DrawText(row, 88, y, noteFont, notePaint);
                y += 48;
            }
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90)
                        ?? throw new InvalidOperationException("Không ghi được ảnh bìa.");
        return data.ToArray();
    }

    /// <summary>Một câu đọc được từ thân bài, bỏ markdown. Trùng tiêu đề thì trả chuỗi rỗng.</summary>
    public static string Excerpt(string? markdown, string? title)
    {
        var plain = Plain(markdown);
        if (plain.Length == 0) return "";
        var cut = plain;
        foreach (var mark in new[] { ". ", "? ", "! ", "\n" })
        {
            var at = plain.IndexOf(mark, StringComparison.Ordinal);
            if (at is > 24 and < 180)
            {
                cut = plain[..(at + (mark == "\n" ? 0 : 1))].Trim();
                break;
            }
        }

        if (cut.Length > 160) cut = cut[..160].TrimEnd() + "…";
        var heading = Clean(title ?? "", 180);
        return string.Equals(cut, heading, StringComparison.OrdinalIgnoreCase) ? "" : cut;
    }

    internal static int PaletteIndex(string title)
    {
        var hash = 0;
        foreach (var c in title)
            hash = unchecked(hash * 31 + c);
        return Math.Abs(hash % Palettes.Length);
    }

    static string Clean(string? value, int max)
    {
        var text = Plain(value);
        return text.Length <= max ? text : text[..max].TrimEnd() + "…";
    }

    static string Plain(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return "";
        var text = markdown.Replace("\r", " ");
        text = Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", " ");
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]*\)", "$1");
        text = Regex.Replace(text, @"[#*_`>]", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }

    static List<string> Wrap(SKFont font, string text, float maxWidth, int maxLines)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in words)
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (line.Length > 0 && font.MeasureText(next) > maxWidth)
            {
                lines.Add(line.ToString());
                line.Clear();
                if (lines.Count == maxLines) break;
                line.Append(word);
            }
            else
            {
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
        }

        if (line.Length > 0 && lines.Count < maxLines)
            lines.Add(line.ToString());
        if (lines.Count == maxLines && words.Length > 0)
        {
            var last = lines[^1];
            if (!text.EndsWith(last, StringComparison.Ordinal) && !last.EndsWith('…'))
                lines[^1] = last.Length > 3 ? last[..^1] + "…" : last;
        }

        return lines.Count == 0 ? [text] : lines;
    }

    static SKTypeface LoadFace()
    {
        var asm = typeof(ContentArticleCoverComposer).Assembly;
        using var src = asm.GetManifestResourceStream("Linesheet.Fonts.BeVietnamPro-ExtraBold.ttf")
                        ?? throw new InvalidOperationException("Thiếu font Be Vietnam Pro.");
        using var copy = new MemoryStream();
        src.CopyTo(copy);
        var data = SKData.CreateCopy(copy.ToArray());
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException("Không đọc được font Be Vietnam Pro.");
    }

    static readonly (SKColor Paper, SKColor Accent, SKColor Ink, SKColor Muted)[] Palettes =
    [
        (new SKColor(0xF3, 0xF6, 0xF4), new SKColor(0x1F, 0x6B, 0x4A), new SKColor(0x1C, 0x2B, 0x24), new SKColor(0x3E, 0x52, 0x48)),
        (new SKColor(0xF7, 0xF4, 0xEE), new SKColor(0x8A, 0x5A, 0x2B), new SKColor(0x2B, 0x24, 0x1C), new SKColor(0x5C, 0x4E, 0x3E)),
        (new SKColor(0xF2, 0xF5, 0xF8), new SKColor(0x1E, 0x4E, 0x79), new SKColor(0x1A, 0x27, 0x33), new SKColor(0x3D, 0x4E, 0x5C)),
        (new SKColor(0xF6, 0xF3, 0xF5), new SKColor(0x6E, 0x3B, 0x4A), new SKColor(0x2C, 0x1E, 0x24), new SKColor(0x5A, 0x45, 0x4C)),
    ];
}
