using System.Text;
using SkiaSharp;
using KitPlatform.Packs.Content.Production;

namespace KitPlatform.Packs.Content.Infrastructure;

/// <summary>
/// Ghép lớp ảnh cho một shot và vẽ thẻ chữ bằng SkiaSharp. Chuyển từ <c>build_layers</c>, <c>draw_characters</c>,
/// <c>render_card</c> của bản dựng mẫu Python (PIL). Font nạp từ file (SkiaSharp.NativeAssets.Linux.NoDependencies không có fontconfig).
/// </summary>
internal sealed class SkiaLinesheetLayerComposer : ILinesheetLayerComposer
{
    static readonly SKSamplingOptions Sampling = new(SKCubicResampler.Mitchell);

    readonly IFamixaLinesheetFileStore _files;

    public SkiaLinesheetLayerComposer(IFamixaLinesheetFileStore files) => _files = files;

    // ---------- ghép lớp ----------

    public LinesheetRenderPlan.ShotImages ComposeShot(LinesheetComposeRequest r)
    {
        const double over = LinesheetRenderPlan.Over;
        const double q = LinesheetRenderPlan.Quality;
        var shot = r.Shot;
        var cw = (int)(r.Width * over * q);
        var ch = (int)(r.Height * over * q);
        using var bg = LoadImage(r.BackgroundPath);
        using var mid = LoadImage(r.MidPath);
        var name = FamixaLinesheetService.SafeName(shot.ShotId);

        if (shot.CameraPreset != LinesheetCameraPresets.Parallax)
        {
            using var flat = new SKBitmap(cw, ch, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(flat))
            {
                canvas.Clear(SKColors.Black);
                DrawCover(canvas, bg, cw, ch);
                var view = ((cw - r.Width * q) / 2, (ch - r.Height * q) / 2, r.Width * q, r.Height * q);
                DrawCharacters(canvas, r, view);
                DrawCover(canvas, mid, cw, ch); // tiền cảnh phủ cả khung, phần lớn trong suốt
            }

            var p = Path.Combine(r.OutputDirectory, name + "_flat.png");
            SavePng(flat, p);
            return new LinesheetRenderPlan.ShotImages(p, null, null);
        }

        // parallax: nền và tiền cảnh tách rời; tiền cảnh rộng 1,3 lần khung, cao bằng khung
        var pBg = Path.Combine(r.OutputDirectory, name + "_bg.png");
        using (var back = new SKBitmap(cw, ch, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var canvas = new SKCanvas(back))
            {
                canvas.Clear(SKColors.Black);
                DrawCover(canvas, bg, cw, ch);
            }

            SavePng(back, pBg);
        }

        var fw = (int)(r.Width * 1.3 * q);
        var fh = (int)(r.Height * q);
        var pFront = Path.Combine(r.OutputDirectory, name + "_front.png");
        using (var front = new SKBitmap(fw, fh, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var canvas = new SKCanvas(front))
            {
                canvas.Clear(SKColors.Transparent);
                DrawCharacters(canvas, r, ((fw - r.Width * q) / 2, 0, r.Width * q, r.Height * q));
                var mh = (int)(fw * (double)mid.Height / mid.Width);
                canvas.DrawImage(mid, new SKRect(0, fh - mh, fw, fh), Sampling);
            }

            SavePng(front, pFront);
        }

        return new LinesheetRenderPlan.ShotImages(null, pBg, pFront);
    }

    /// <summary>Vị trí ngang (0–1) của từng nhân vật, giữ thứ tự chèn. "ID@x" trong bảng shot ghi đè vị trí.</summary>
    internal static List<(string Id, double X)> CharacterPositions(IReadOnlyList<string> cast, string focus, bool vertical)
    {
        var ids = new List<string>();
        var fixedX = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var c in cast)
        {
            var parts = c.Split('@');
            ids.Add(parts[0]);
            if (parts.Length > 1 && double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var x))
                fixedX[parts[0]] = x;
        }

        var pos = new List<(string Id, double X)>();
        if (vertical)
        {
            // 9:16: nhân vật chính giữa, người khác lấp ló hai mép
            var order = ids.Contains(focus) ? new[] { focus }.Concat(ids.Where(c => c != focus)).ToList() : ids;
            double[] slots = [0.5, 0.06, 0.94, -0.2];
            for (var i = 0; i < Math.Min(order.Count, slots.Length); i++) pos.Add((order[i], slots[i]));
        }
        else
        {
            double[] slots = ids.Count switch
            {
                1 => [0.5],
                2 => [0.33, 0.67],
                3 => [0.22, 0.5, 0.78],
                _ => Enumerable.Range(1, ids.Count).Select(i => i / (double)(ids.Count + 1)).ToArray(),
            };
            for (var i = 0; i < ids.Count; i++) pos.Add((ids[i], slots[i]));
        }

        foreach (var (id, x) in fixedX)
        {
            var i = pos.FindIndex(p => p.Id == id);
            if (i >= 0) pos[i] = (id, x);
            else pos.Add((id, x));
        }

        return pos;
    }

    void DrawCharacters(SKCanvas canvas, LinesheetComposeRequest r, (double X, double Y, double W, double H) view)
    {
        var focus = r.Shot.Focus;
        var pos = CharacterPositions(r.Shot.Characters, focus, r.Vertical);
        var order = pos.Where(p => p.Id != focus).Concat(pos.Where(p => p.Id == focus)).ToList();
        var (vx, vy, vw, vh) = view;
        foreach (var (cid, px) in order)
        {
            if (!r.CharacterPaths.TryGetValue(cid, out var path) || !r.Settings.Characters.TryGetValue(cid, out var info))
                continue;
            using var img = LoadImage(path);
            var isFocus = cid == focus;
            var h = r.Vertical
                ? (int)(Math.Min(vh * 0.52, vw * 1.2) * info.Height / 0.62 * (isFocus ? 1.06 : 1.0))
                : (int)(vh * info.Height * (isFocus ? 1.06 : 1.0));
            var w = (int)(img.Width * (double)h / img.Height);
            var x = (int)(vx + vw * px - w / 2.0);
            var y = (int)(vy + vh - h + vh * (r.Vertical ? 0.14 : 0.04)); // 9:16: hạ nhân vật để chừa chỗ phụ đề
            using var paint = new SKPaint { IsAntialias = true };
            // người không nói tối đi; cả cảnh ám màu ánh sáng của bối cảnh (bếp đêm xanh than, sân khấu tím…)
            r.Settings.Settings.TryGetValue(r.Shot.Setting, out var settingDef);
            var (tr, tg, tb) = LinesheetRenderPlan.CharacterTint(settingDef, focus.Length > 0 && !isFocus);
            if (tr != 1 || tg != 1 || tb != 1)
            {
                paint.ColorFilter = SKColorFilter.CreateColorMatrix(
                [
                    (float)tr, 0, 0, 0, 0,
                    0, (float)tg, 0, 0, 0,
                    0, 0, (float)tb, 0, 0,
                    0, 0, 0, 1, 0,
                ]);
            }

            var mirror = LinesheetRenderPlan.ShouldMirror(info.Faces, px);
            canvas.Save();
            if (mirror)
            {
                canvas.Translate(x + x + w, 0);
                canvas.Scale(-1, 1);
            }

            canvas.DrawImage(img, new SKRect(x, y, x + w, y + h), Sampling, paint);
            canvas.Restore();
        }
    }

    static void DrawCover(SKCanvas canvas, SKImage img, int w, int h)
    {
        var s = Math.Max(w / (double)img.Width, h / (double)img.Height);
        var iw = (float)Math.Round(img.Width * s);
        var ih = (float)Math.Round(img.Height * s);
        var x = (float)Math.Floor((iw - w) / 2);
        var y = (float)Math.Floor((ih - h) / 2);
        canvas.DrawImage(img, new SKRect(-x, -y, iw - x, ih - y), Sampling);
    }

    static SKImage LoadImage(string path) =>
        SKImage.FromEncodedData(path) ?? throw new InvalidOperationException(LinesheetErrorCodes.MissingLayer + ": không đọc được " + Path.GetFileName(path));

    static void SavePng(SKBitmap bmp, string path)
    {
        using var image = SKImage.FromBitmap(bmp);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
                         ?? throw new InvalidOperationException("LINESHEET_PNG_ENCODE: " + Path.GetFileName(path));
        var tmp = path + ".partial";
        using (var stream = File.Create(tmp)) data.SaveTo(stream);
        File.Move(tmp, path, overwrite: true);
    }

    // ---------- đoạn kết ----------

    public void RenderOutroCards(LinesheetOutro outro, int width, int height, bool vertical, string? logoPng, string titlePng, string cardPng)
    {
        using var regular = SKTypeface.FromFile(_files.FontRegularPath) ?? throw new InvalidOperationException("LINESHEET_FONT_MISSING");
        using var bold = SKTypeface.FromFile(_files.FontBoldPath) ?? regular;
        var s = vertical ? width / 1080f : width / 1920f;
        var cream = new SKColor(246, 239, 227);
        var lilac = new SKColor(217, 208, 236);
        var orange = new SKColor(255, 154, 74);
        SKImage? logo = logoPng is not null && File.Exists(logoPng) ? SKImage.FromEncodedData(logoPng) : null;
        try
        {
            void Background(SKCanvas cv)
            {
                cv.Clear(new SKColor(26, 12, 52));
                using var glow = new SKPaint { IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 170 * s) };
                glow.Color = new SKColor(168, 52, 190, 90);
                cv.DrawCircle(width * 0.31f, height * 0.5f, 400 * s, glow);
                glow.Color = new SKColor(255, 140, 60, 55);
                cv.DrawCircle(width * 0.73f, height * 0.58f, 380 * s, glow);
            }

            void Center(SKCanvas cv, string text, float cx, float top, SKTypeface tf, float size, SKColor color)
            {
                if (text.Length == 0) return;
                using var f = new SKFont(tf, size);
                using var p = new SKPaint { IsAntialias = true, Color = color };
                var t = text.Normalize(NormalizationForm.FormC);
                DrawTop(cv, t, cx - f.MeasureText(t) / 2, top, f, p);
            }

            void Logo(SKCanvas cv, float cx, float top, float h)
            {
                if (logo is null) return;
                var w = h * logo.Width / logo.Height;
                using var lp = new SKPaint { IsAntialias = true };
                cv.DrawImage(logo, new SKRect(cx - w / 2, top, cx + w / 2, top + h), new SKSamplingOptions(SKCubicResampler.Mitchell), lp);
            }

            using (var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            {
                using (var cv = new SKCanvas(bmp))
                {
                    Background(cv);
                    var cy = height / 2f;
                    Center(cv, outro.EndTitle, width / 2f, cy - 110 * s, bold, (vertical ? 110 : 132) * s, cream);
                    Center(cv, outro.SeriesLine, width / 2f, cy + 70 * s, regular, (vertical ? 34 : 38) * s, lilac);
                }

                SavePng(bmp, titlePng);
            }

            using (var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul))
            {
                using (var cv = new SKCanvas(bmp))
                {
                    Background(cv);
                    using var line = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * s, Color = new SKColor(236, 226, 248, 110) };
                    if (!vertical)
                    {
                        // trái: logo, câu mời, vòng tròn nút Đăng ký; phải: tập sau và khung "Xem tiếp" (chỗ gắn màn hình kết thúc)
                        Logo(cv, 500 * s, 170 * s, 280 * s);
                        Center(cv, outro.FollowLine, 500 * s, 490 * s, regular, 40 * s, cream);
                        cv.DrawCircle(500 * s, 680 * s, 100 * s, line);
                        Center(cv, "Đăng ký kênh", 500 * s, 800 * s, regular, 28 * s, lilac);
                        Center(cv, outro.NextLabel, 1400 * s, 170 * s, bold, 40 * s, orange);
                        Center(cv, outro.NextTitle, 1400 * s, 228 * s, bold, 84 * s, cream);
                        Center(cv, outro.NextNote, 1400 * s, 345 * s, regular, 40 * s, lilac);
                        cv.DrawRoundRect(new SKRect(1060 * s, 440 * s, 1740 * s, 823 * s), 18 * s, 18 * s, line);
                        Center(cv, "Xem tiếp", 1400 * s, 612 * s, regular, 34 * s, lilac);
                    }
                    else
                    {
                        Logo(cv, width / 2f, 240 * s, 260 * s);
                        Center(cv, outro.FollowLine, width / 2f, 560 * s, regular, 40 * s, cream);
                        Center(cv, outro.NextLabel, width / 2f, 760 * s, bold, 40 * s, orange);
                        Center(cv, outro.NextTitle, width / 2f, 820 * s, bold, 76 * s, cream);
                        Center(cv, outro.NextNote, width / 2f, 930 * s, regular, 40 * s, lilac);
                        cv.DrawRoundRect(new SKRect(190 * s, 1040 * s, 890 * s, 1434 * s), 18 * s, 18 * s, line);
                        Center(cv, "Xem tiếp", width / 2f, 1215 * s, regular, 34 * s, lilac);
                        cv.DrawCircle(width / 2f, 1600 * s, 100 * s, line);
                        Center(cv, "Đăng ký kênh", width / 2f, 1720 * s, regular, 28 * s, lilac);
                    }
                }

                SavePng(bmp, cardPng);
            }
        }
        finally
        {
            logo?.Dispose();
        }
    }

    // ---------- thẻ chữ ----------

    public LinesheetRenderPlan.PlacedCard RenderCard(
        LinesheetTextLayer layer, int width, int height, bool vertical, IReadOnlyCollection<string> chatSelf, string outputPng,
        string? phoneInsertPng = null, string? titleLogoPng = null)
    {
        var text = layer.Text.Normalize(NormalizationForm.FormC);
        var s = vertical ? width / 540.0 : width / 1280.0;
        using var regular = SKTypeface.FromFile(_files.FontRegularPath) ?? throw new InvalidOperationException("LINESHEET_FONT_MISSING");
        using var bold = SKTypeface.FromFile(_files.FontBoldPath) ?? regular;

        if (layer.Kind == LinesheetTextKinds.Title)
        {
            // tên phim (sửa 2026-11-05): logo kênh nhỏ ở trên (settings.title_logo), tên phim to, dòng phụ màu kem,
            // dòng tập màu cam; cả khối căn giữa theo chiều dọc trên nền tối
            using var f1 = new SKFont(bold, (int)(92 * s));
            using var f2 = new SKFont(regular, (int)(38 * s));
            using var f3 = new SKFont(bold, (int)(34 * s));
            using var card = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var canvas = new SKCanvas(card))
            {
                canvas.Clear(new SKColor(10, 6, 24, 190));
                var parts = text.Split('—').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                SKFont FontOf(int i) => i == 0 ? f1 : i == 1 ? f2 : f3;
                SKImage? logo = null;
                if (titleLogoPng is not null && File.Exists(titleLogoPng)) logo = SKImage.FromEncodedData(titleLogoPng);
                try
                {
                    var logoH = logo is null ? 0 : (float)(110 * s);
                    var logoW = logo is null ? 0 : logoH * logo.Width / logo.Height;
                    var gapLogo = logo is null ? 0 : (float)(26 * s);
                    var lines = parts.Select((p, i) => (p, f: FontOf(i), h: FontOf(i).Size * (i == 0 ? 1.25f : 1.55f))).ToList();
                    var total = logoH + gapLogo + lines.Sum(l => l.h);
                    var y = (height - total) / 2f;
                    if (logo is not null)
                    {
                        using var sp = new SKPaint { IsAntialias = true };
                        canvas.DrawImage(logo, new SKRect((width - logoW) / 2f, y, (width + logoW) / 2f, y + logoH),
                            new SKSamplingOptions(SKCubicResampler.Mitchell), sp);
                        y += logoH + gapLogo;
                    }

                    for (var i = 0; i < lines.Count; i++)
                    {
                        using var paint = new SKPaint
                        {
                            IsAntialias = true,
                            Color = i == 0 ? new SKColor(246, 239, 227) : i == 1 ? new SKColor(236, 226, 248) : new SKColor(255, 154, 74),
                        };
                        var tw = lines[i].f.MeasureText(lines[i].p);
                        DrawTop(canvas, lines[i].p, (width - tw) / 2, y, lines[i].f, paint);
                        y += lines[i].h;
                    }
                }
                finally
                {
                    logo?.Dispose();
                }
            }

            SavePng(card, outputPng);
            return new LinesheetRenderPlan.PlacedCard(outputPng, 0, 0, layer.FromSec, layer.ToSec);
        }

        if (layer.Kind == LinesheetTextKinds.Note)
            return RenderNote(layer, text, width, height, vertical, bold, outputPng);

        if (layer.Kind == LinesheetTextKinds.Chat)
            return (phoneInsertPng is not null && File.Exists(phoneInsertPng)
                       ? RenderPhoneInsert(layer, text, width, height, chatSelf, regular, bold, phoneInsertPng, outputPng)
                       : null)
                   ?? RenderPhone(layer, text, width, height, chatSelf, regular, bold, outputPng);

        return RenderCaption(layer, text, width, height, vertical, regular, bold, outputPng);
    }

    /// <summary>
    /// Chữ viết tay trên mảnh giấy (2026-11-07): giấy màu kem nghiêng nhẹ, có bóng, chữ đậm màu mực xanh đen cỡ 84
    /// (theo khung 1280), giữa khung hơi trên tâm. Dùng cho mẩu giấy, lời nhắn mà nhân vật đang cầm đọc.
    /// </summary>
    LinesheetRenderPlan.PlacedCard RenderNote(
        LinesheetTextLayer layer, string text, int width, int height, bool vertical, SKTypeface bold, string outputPng)
    {
        var s = vertical ? width / 540.0 : width / 1280.0;
        using var font = new SKFont(bold, (int)(84 * s));
        var padX = (int)(46 * s);
        var padY = (int)(30 * s);
        var lines = Wrap(text, font, (vertical ? width * 0.8 : width * 0.6) - 2 * padX);
        var lh = (int)(font.Size * 1.3);
        var tw = (int)lines.Select(l => font.MeasureText(l)).DefaultIfEmpty(0).Max();
        var cw = tw + 2 * padX;
        var ch = lh * lines.Count + 2 * padY;
        var margin = (int)(30 * s);
        var bw = cw + 2 * margin;
        var bh = ch + 2 * margin;
        using (var bmp = new SKBitmap(bw, bh, SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.Transparent);
                canvas.RotateDegrees(-2.5f, bw / 2f, bh / 2f);
                var rect = new SKRect(margin, margin, margin + cw, margin + ch);
                using var shadow = new SKPaint
                {
                    IsAntialias = true, Color = new SKColor(0, 0, 0, 110),
                    MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, (float)(10 * s)),
                };
                canvas.DrawRect(new SKRect(rect.Left + (float)(6 * s), rect.Top + (float)(8 * s), rect.Right + (float)(6 * s), rect.Bottom + (float)(8 * s)), shadow);
                using var paper = new SKPaint { IsAntialias = true, Color = new SKColor(250, 244, 226) };
                canvas.DrawRect(rect, paper);
                using var ink = new SKPaint { IsAntialias = true, Color = new SKColor(28, 40, 86) };
                for (var i = 0; i < lines.Count; i++)
                    DrawTop(canvas, lines[i], margin + (cw - font.MeasureText(lines[i])) / 2, margin + padY + i * lh, font, ink);
            }

            SavePng(bmp, outputPng);
        }

        var px = (width - bw) / 2;
        // dưới tâm khung để không che mặt nhân vật (cận mặt thường nằm ở 40% trên) — sửa 2026-11-07
        var py = (int)(height * (vertical ? 0.66 : 0.70)) - bh / 2;
        return new LinesheetRenderPlan.PlacedCard(outputPng, px, py, layer.FromSec, layer.ToSec);
    }

    static readonly System.Text.RegularExpressions.Regex PlaceTime = new(
        @"(\d{1,2}[:h]\d{2})|^(Sáng|Trưa|Chiều|Tối|Đêm|Hôm|Morning|Noon|Afternoon|Evening|Night|That|The same|Later)\b",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Thẻ chữ là địa điểm hay thời gian ("Bệnh viện, 22:10", "Sáng cùng ngày") thì đặt góc dưới trái.</summary>
    internal static bool IsPlaceTime(string text) => PlaceTime.IsMatch(text.Trim());

    /// <summary>
    /// Thẻ chữ (sửa 2026-11-05 sau soát EP01 bản 9: chữ 24 điểm trong bong bóng sáng ở góc trên phải, khó đọc).
    /// Địa điểm, thời gian: dải tối góc dưới trái, chữ 34. Chữ khác (tên đồ vật, thông báo): giữa khung, hơi dưới
    /// tâm, chữ đậm 46, nền tối bo góc. Cỡ tính theo khung 1280 điểm ảnh.
    /// </summary>
    LinesheetRenderPlan.PlacedCard RenderCaption(
        LinesheetTextLayer layer, string text, int width, int height, bool vertical,
        SKTypeface regular, SKTypeface bold, string outputPng)
    {
        var s = vertical ? width / 540.0 : width / 1280.0;
        var place = IsPlaceTime(text);
        using var font = place ? new SKFont(regular, (int)(34 * s)) : new SKFont(bold, (int)(46 * s));
        var padX = (int)((place ? 20 : 28) * s);
        var padY = (int)((place ? 12 : 16) * s);
        var maxText = (vertical ? width * 0.86 : width * (place ? 0.5 : 0.7)) - 2 * padX;
        var lines = Wrap(text, font, maxText);
        var lh = (int)(font.Size * 1.3);
        var tw = (int)lines.Select(l => font.MeasureText(l)).DefaultIfEmpty(0).Max();
        var cw = tw + 2 * padX;
        var ch = lh * lines.Count + 2 * padY;
        using (var bmp = new SKBitmap(Math.Max(cw, 1), Math.Max(ch, 1), SKColorType.Rgba8888, SKAlphaType.Premul))
        {
            using (var canvas = new SKCanvas(bmp))
            {
                canvas.Clear(SKColors.Transparent);
                using var fill = new SKPaint { IsAntialias = true, Color = new SKColor(10, 10, 14, place ? (byte)175 : (byte)190) };
                var radius = (float)((place ? 10 : 16) * s);
                canvas.DrawRoundRect(new SKRect(0, 0, cw - 1, ch - 1), radius, radius, fill);
                using var ink = new SKPaint { IsAntialias = true, Color = SKColors.White };
                for (var i = 0; i < lines.Count; i++)
                {
                    var x = place ? padX : (cw - font.MeasureText(lines[i])) / 2;
                    DrawTop(canvas, lines[i], x, padY + i * lh, font, ink);
                }
            }

            SavePng(bmp, outputPng);
        }

        int px, py;
        if (place)
        {
            px = (int)(width * 0.05);
            py = (int)(height * (vertical ? 0.70 : 0.80)) - ch;
        }
        else
        {
            px = (width - cw) / 2;
            py = (int)(height * (vertical ? 0.62 : 0.68)) - ch / 2;
        }

        return new LinesheetRenderPlan.PlacedCard(outputPng, px, py, layer.FromSec, layer.ToSec);
    }

    /// <summary>
    /// Cảnh chèn điện thoại (thêm 2026-11-05): tin nhắn, cuộc gọi, thông báo vẽ trên màn hình một chiếc điện thoại chiếm
    /// gần hết khung hình; phía sau là chính shot đó làm mờ (FinishShotArgs). Chữ do phần mềm vẽ nên luôn đúng dấu, đọc được
    /// trên màn hình nhỏ. Giao diện chung chung, không giống ứng dụng có thương hiệu nào.
    /// Kiểu theo người gửi đầu tiên: "Đang gọi" (màn hình gọi), "Cuộc gọi nhỡ" / "Tin nhắn thoại" (màn hình khóa có thông báo),
    /// "Đang ghi âm" (đang ghi tin nhắn thoại), còn lại là khung trò chuyện. Chủ máy: tiền tố "@Mẹ|", không có thì chat_self đầu tiên.
    /// </summary>
    LinesheetRenderPlan.PlacedCard RenderPhone(
        LinesheetTextLayer layer, string text, int width, int height, IReadOnlyCollection<string> chatSelf,
        SKTypeface regular, SKTypeface bold, string outputPng)
    {
        var (owner, msgs) = ParsePhone(text, chatSelf);

        var ph = height * 0.96f;
        var pw = ph * 0.49f;
        var px = (width - pw) / 2f;
        var py = (height - ph) / 2f;
        var u = pw / 400f; // đơn vị theo bề ngang điện thoại
        using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);
        using (var shadow = new SKPaint { Color = new SKColor(0, 0, 0, 120), IsAntialias = true, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 18 * u) })
            canvas.DrawRoundRect(new SKRect(px + 6 * u, py + 10 * u, px + pw + 6 * u, py + ph + 10 * u), 46 * u, 46 * u, shadow);
        using (var body = new SKPaint { Color = new SKColor(24, 24, 26), IsAntialias = true })
            canvas.DrawRoundRect(new SKRect(px, py, px + pw, py + ph), 46 * u, 46 * u, body);
        var sr = new SKRect(px + 12 * u, py + 12 * u, px + pw - 12 * u, py + ph - 12 * u);
        DrawScreen(canvas, sr, u, text, owner, msgs, regular, bold);
        SavePng(bmp, outputPng);
        return new LinesheetRenderPlan.PlacedCard(outputPng, 0, 0, layer.FromSec, layer.ToSec, Blur: true);
    }

    /// <summary>Nội dung màn hình điện thoại trong khung sr (u = đơn vị theo bề ngang màn hình, 376u = cả bề ngang).</summary>
    static void DrawScreen(SKCanvas canvas, SKRect sr, float u, string text, string owner, List<(string Who, string Msg)> msgs, SKTypeface regular, SKTypeface bold)
    {
        var kind = msgs[0].Who;
        var dark = kind is "Đang gọi" or "Cuộc gọi nhỡ" or "Tin nhắn thoại" or "Đang ghi âm";
        canvas.Save();
        using (var clip = new SKPath()) { clip.AddRoundRect(sr, 36 * u, 36 * u); canvas.ClipPath(clip, antialias: true); }
        using (var bg = new SKPaint { IsAntialias = true })
        {
            bg.Shader = dark
                ? SKShader.CreateLinearGradient(new SKPoint(sr.Left, sr.Top), new SKPoint(sr.Left, sr.Bottom),
                    [new SKColor(32, 44, 70), new SKColor(14, 16, 24)], SKShaderTileMode.Clamp)
                : SKShader.CreateColor(new SKColor(242, 242, 247));
            canvas.DrawRect(sr, bg);
        }

        // chữ to cho người xem trên điện thoại (bài học 2026-11-05: cỡ 22 quá nhỏ ở 720p)
        using var f = new SKFont(regular, 31 * u);
        using var fb = new SKFont(bold, 31 * u);
        using var fSmall = new SKFont(regular, 22 * u);
        using var fBig = new SKFont(regular, 88 * u);
        using var fName = new SKFont(bold, 42 * u);
        var ink = dark ? SKColors.White : new SKColor(20, 20, 22);
        using var inkP = new SKPaint { Color = ink, IsAntialias = true };
        using var greyP = new SKPaint { Color = dark ? new SKColor(255, 255, 255, 170) : new SKColor(120, 120, 128), IsAntialias = true };
        var cx = (sr.Left + sr.Right) / 2;
        float Center(string t, SKFont font, float y, SKPaint paint) { DrawTop(canvas, t, cx - font.MeasureText(t) / 2, y, font, paint); return y + font.Size * 1.3f; }
        var clock = System.Text.RegularExpressions.Regex.Match(text, @"\b\d{1,2}:\d{2}\b").Value;

        if (kind == "Đang gọi")
        {
            var name = msgs[0].Msg;
            using (var av = new SKPaint { Color = new SKColor(90, 104, 130), IsAntialias = true })
                canvas.DrawCircle(cx, sr.Top + 200 * u, 62 * u, av);
            Center(name.Length > 0 ? name[..1].ToUpperInvariant() : "?", fBig, sr.Top + 150 * u, inkP);
            var y = Center(name, fName, sr.Top + 290 * u, inkP);
            Center("Đang gọi…", f, y + 6 * u, greyP);
            using (var red = new SKPaint { Color = new SKColor(235, 64, 52), IsAntialias = true })
                canvas.DrawCircle(cx, sr.Bottom - 120 * u, 36 * u, red);
        }
        else if (kind is "Cuộc gọi nhỡ" or "Tin nhắn thoại")
        {
            if (clock.Length > 0) Center(clock, fBig, sr.Top + 90 * u, inkP);
            var cardTop = sr.Top + (clock.Length > 0 ? 220 : 120) * u;
            var title = kind == "Cuộc gọi nhỡ" ? "Cuộc gọi nhỡ" : "Tin nhắn thoại mới"; // "…chưa nghe" tràn thẻ ở màn hẹp 320u (2026-11-05)
            var body = System.Text.RegularExpressions.Regex.Replace(msgs[0].Msg, @"\s*·?\s*\b\d{1,2}:\d{2}\b", "").Trim();
            using (var card = new SKPaint { Color = new SKColor(255, 255, 255, 46), IsAntialias = true })
                canvas.DrawRoundRect(new SKRect(sr.Left + 18 * u, cardTop, sr.Right - 18 * u, cardTop + 118 * u), 22 * u, 22 * u, card);
            DrawTop(canvas, title, sr.Left + 36 * u, cardTop + 18 * u, fSmall, greyP);
            DrawTop(canvas, body, sr.Left + 36 * u, cardTop + 56 * u, fb, inkP);
        }
        else if (kind == "Đang ghi âm")
        {
            var to = System.Text.RegularExpressions.Regex.Replace(msgs[0].Msg, @"\s*·?\s*\b\d{1,2}:\d{2}\b", "").Trim();
            var y = Center(to, fName, sr.Top + 120 * u, inkP);
            Center("Đang ghi âm…", f, y, greyP);
            var mid = sr.Top + sr.Height * 0.48f;
            var rnd = new Random(7);
            using (var wave = new SKPaint { Color = new SKColor(255, 255, 255, 220), IsAntialias = true })
                for (var b = 0; b < 22; b++)
                {
                    var hgt = (8 + rnd.Next(0, 46)) * u;
                    // canh giữa theo bề ngang (bài học 2026-11-05: bắt đầu từ mép trái thì màn hẹp 320u bị tràn vạch cuối)
                    var x = cx - 22 * 11.5f * u / 2 + b * 11.5f * u;
                    canvas.DrawRoundRect(new SKRect(x, mid - hgt / 2, x + 6 * u, mid + hgt / 2), 3 * u, 3 * u, wave);
                }
            if (clock.Length > 0) Center(clock, f, mid + 60 * u, greyP);
            using (var red = new SKPaint { Color = new SKColor(235, 64, 52), IsAntialias = true })
                canvas.DrawCircle(cx, sr.Bottom - 120 * u, 34 * u, red);
        }
        else
        {
            // khung trò chuyện: tiêu đề là người bên kia, tin của chủ máy bên phải màu xanh
            var others = msgs.Select(m => m.Who).Where(w => w.Length > 0 && w != owner).Distinct().ToList();
            var group = others.Count > 1;
            using (var head = new SKPaint { Color = new SKColor(250, 250, 252), IsAntialias = true })
                canvas.DrawRect(new SKRect(sr.Left, sr.Top, sr.Right, sr.Top + 130 * u), head);
            DrawTop(canvas, "‹", sr.Left + 20 * u, sr.Top + 62 * u, fName, new SKPaint { Color = new SKColor(10, 132, 255), IsAntialias = true });
            Center(others.Count > 0 ? (group ? string.Join(", ", others) : others[0]) : owner, fb, sr.Top + 68 * u, inkP);
            var y = sr.Top + 160 * u;
            var maxw = (sr.Width - 40 * u) * 0.84f;
            foreach (var (who, msg) in msgs)
            {
                var self = who == owner;
                var lines = Wrap(msg, f, maxw - 32 * u);
                var lh = f.Size * 1.32f;
                var label = group && !self && who.Length > 0 ? fSmall.Size * 1.4f : 0;
                var bw = Math.Min(maxw, lines.Max(l => f.MeasureText(l)) + 32 * u);
                var bh = lines.Count * lh + 26 * u;
                if (label > 0) DrawTop(canvas, who, sr.Left + 24 * u, y, fSmall, greyP);
                y += label;
                var bx = self ? sr.Right - 20 * u - bw : sr.Left + 20 * u;
                using (var bub = new SKPaint { Color = self ? new SKColor(10, 132, 255) : new SKColor(229, 229, 234), IsAntialias = true })
                    canvas.DrawRoundRect(new SKRect(bx, y, bx + bw, y + bh), 20 * u, 20 * u, bub);
                using var t = new SKPaint { Color = self ? SKColors.White : new SKColor(20, 20, 22), IsAntialias = true };
                for (var i = 0; i < lines.Count; i++) DrawTop(canvas, lines[i], bx + 16 * u, y + 13 * u + i * lh, f, t);
                y += bh + 16 * u;
            }

            using (var bar = new SKPaint { Color = SKColors.White, IsAntialias = true })
                canvas.DrawRoundRect(new SKRect(sr.Left + 18 * u, sr.Bottom - 74 * u, sr.Right - 18 * u, sr.Bottom - 30 * u), 22 * u, 22 * u, bar);
        }

        // thanh trạng thái: sóng và pin (không có giờ để khỏi lệch với giờ trong truyện)
        for (var b = 0; b < 4; b++)
            canvas.DrawRoundRect(new SKRect(sr.Right - 92 * u + b * 9 * u, sr.Top + 30 * u - b * 3 * u, sr.Right - 86 * u + b * 9 * u, sr.Top + 36 * u), 1.5f * u, 1.5f * u, inkP);
        using (var bat = new SKPaint { Color = ink, IsAntialias = true, IsStroke = true, StrokeWidth = 1.6f * u })
            canvas.DrawRoundRect(new SKRect(sr.Right - 46 * u, sr.Top + 24 * u, sr.Right - 20 * u, sr.Top + 36 * u), 3 * u, 3 * u, bat);
        canvas.DrawRect(new SKRect(sr.Right - 44 * u, sr.Top + 26 * u, sr.Right - 28 * u, sr.Top + 34 * u), inkP);
        canvas.Restore();
    }

    static (string Owner, List<(string Who, string Msg)> Msgs) ParsePhone(string text, IReadOnlyCollection<string> chatSelf)
    {
        var (ownerPrefix, rest) = LinesheetRenderPlan.PhoneOwner(text);
        var owner = ownerPrefix.Length > 0 ? ownerPrefix : chatSelf.FirstOrDefault() ?? "";
        var msgs = rest.Split(" / ").Select(b =>
        {
            var i = b.IndexOf(": ", StringComparison.Ordinal);
            return i > 0 && i < 25 ? (Who: b[..i].Trim(), Msg: b[(i + 2)..].Trim()) : (Who: "", Msg: b.Trim());
        }).ToList();
        return (owner, msgs);
    }

    /// <summary>
    /// Cảnh cận tay cầm điện thoại (thêm 2026-11-05): ảnh cảnh có màn hình trắng phát sáng; tìm màn hình (vùng trắng lớn nhất),
    /// vẽ tin nhắn vào một ảnh phẳng rồi bẻ theo bốn góc màn hình (phép chiếu phối cảnh). Ảnh ra kín khung, không làm mờ.
    /// Không tìm được màn hình thì trả null để dùng điện thoại kín khung.
    /// </summary>
    LinesheetRenderPlan.PlacedCard? RenderPhoneInsert(
        LinesheetTextLayer layer, string text, int width, int height, IReadOnlyCollection<string> chatSelf,
        SKTypeface regular, SKTypeface bold, string insertPng, string outputPng)
    {
        using var src = SKBitmap.Decode(insertPng);
        if (src is null) return null;
        using var frame = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(frame);
        var scale = Math.Max(width / (float)src.Width, height / (float)src.Height);
        var dw = src.Width * scale;
        var dh = src.Height * scale;
        var dest = new SKRect((width - dw) / 2, (height - dh) / 2, (width + dw) / 2, (height + dh) / 2);
        using var srcImg = SKImage.FromBitmap(src);
        canvas.DrawImage(srcImg, dest, Sampling);
        canvas.Flush();

        var quad = FindScreen(frame);
        if (quad is null) return null;
        var (tl, tr, br, bl) = quad.Value;

        // phóng to quanh màn hình (bài học 2026-11-05: điện thoại chỉ rộng ~10% khung, chữ ~11 điểm ảnh, xem trên điện thoại không đọc được).
        // Màn hình cao chưa tới 60% khung thì phóng tối đa InsertZoom lần, dời cho ảnh vẫn phủ kín khung.
        var qh = Math.Max(bl.Y, br.Y) - Math.Min(tl.Y, tr.Y);
        var z = Math.Clamp(height * 0.6f / Math.Max(1, qh), 1f, InsertZoom);
        if (z > 1.01f)
        {
            var cx = (tl.X + tr.X + br.X + bl.X) / 4;
            var cy = (tl.Y + tr.Y + br.Y + bl.Y) / 4;
            float Ox(float x) => (x - cx) * z + width / 2f;
            float Oy(float y) => (y - cy) * z + height / 2f;
            var r = new SKRect(Ox(dest.Left), Oy(dest.Top), Ox(dest.Right), Oy(dest.Bottom));
            var sx = r.Left > 0 ? -r.Left : r.Right < width ? width - r.Right : 0;
            var sy = r.Top > 0 ? -r.Top : r.Bottom < height ? height - r.Bottom : 0;
            r.Offset(sx, sy);
            SKPoint M(SKPoint a) => new(Ox(a.X) + sx, Oy(a.Y) + sy);
            (tl, tr, br, bl) = (M(tl), M(tr), M(br), M(bl));
            canvas.Clear(SKColors.Black);
            canvas.DrawImage(srcImg, r, Sampling);
        }
        float Dist(SKPoint a, SKPoint b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
        var sw = Math.Max(40, (int)((Dist(tl, tr) + Dist(bl, br)) / 2));
        var sh = Math.Max(60, (int)((Dist(tl, bl) + Dist(tr, br)) / 2));
        using var screen = new SKBitmap(sw, sh, SKColorType.Rgba8888, SKAlphaType.Premul);
        var (owner, msgs) = ParsePhone(text, chatSelf);
        using (var sc = new SKCanvas(screen))
        {
            sc.Clear(SKColors.Transparent);
            // chữ to hơn bản kín khung một bậc (376 → 320 đơn vị): cảnh chèn chỉ lướt qua vài giây
            DrawScreen(sc, new SKRect(0, 0, sw, sh), sw / 320f, text, owner, msgs, regular, bold);
        }

        var dark = msgs[0].Who is "Đang gọi" or "Cuộc gọi nhỡ" or "Tin nhắn thoại" or "Đang ghi âm";
        // tô viền trắng hai lần: trước khi vẽ để mép khử răng cưa của nội dung trộn với màu nền chứ không với màu trắng
        // (bài học 2026-11-05: chỉ tô sau thì còn một đường kẻ xám mảnh quanh màn hình), sau khi vẽ để lấp chỗ còn sót
        canvas.Flush();
        FillScreenRim(frame, tl, tr, br, bl, dark);
        canvas.Save();
        var warp = Homography(sw, sh, tl, tr, br, bl);
        canvas.SetMatrix(in warp);
        using (var screenImg = SKImage.FromBitmap(screen))
        using (var paint = new SKPaint { IsAntialias = true })
            canvas.DrawImage(screenImg, 0, 0, Sampling, paint);
        canvas.Restore();
        canvas.Flush();
        FillScreenRim(frame, tl, tr, br, bl, dark);
        SavePng(frame, outputPng);
        return new LinesheetRenderPlan.PlacedCard(outputPng, 0, 0, layer.FromSec, layer.ToSec);
    }

    /// <summary>
    /// Màn hình điện thoại trên ảnh: vùng liền lớn nhất gồm điểm gần trắng (sáng, ít màu), tìm trên lưới thu nhỏ 4 lần.
    /// Bốn góc lấy theo cực trị của x+y và x−y, đẩy ra 6 điểm ảnh cho kín viền. Nhỏ hơn 1,5% khung hình thì coi như không có.
    /// </summary>
    /// <summary>
    /// Viền trắng còn sót giữa nội dung và khung máy (bài học 2026-11-05: máy nghiêng thì bốn góc lấy theo đường chéo không ôm hết màn hình,
    /// lộ viền trắng 5–8 điểm ảnh ở cạnh). Điểm còn sáng trắng nằm ngoài tứ giác hoặc cách cạnh dưới 14 điểm ảnh thì tô bằng màu nền màn hình
    /// (nền tối theo dải màu từ trên xuống, nền sáng của khung trò chuyện). Chữ cách cạnh từ 18 đơn vị trở lên nên không bị tô.
    /// </summary>
    static void FillScreenRim(SKBitmap img, SKPoint tl, SKPoint tr, SKPoint br, SKPoint bl, bool dark)
    {
        SKPoint[] q = [tl, tr, br, bl];
        const int pad = 18;
        int x0 = Math.Max(0, (int)q.Min(p => p.X) - pad), x1 = Math.Min(img.Width - 1, (int)q.Max(p => p.X) + pad);
        int y0 = Math.Max(0, (int)q.Min(p => p.Y) - pad), y1 = Math.Min(img.Height - 1, (int)q.Max(p => p.Y) + pad);
        var top = Math.Min(tl.Y, tr.Y);
        var span = Math.Max(1f, Math.Max(bl.Y, br.Y) - top);
        // khoảng cách có dấu tới cạnh i (dương là phía trong, tứ giác theo chiều kim đồng hồ trên ảnh)
        float Edge(int i, float x, float y)
        {
            var a = q[i]; var b = q[(i + 1) % 4];
            var dx = b.X - a.X; var dy = b.Y - a.Y;
            var len = (float)Math.Sqrt(dx * dx + dy * dy);
            return len < 1 ? 0 : ((x - a.X) * dy - (y - a.Y) * dx) / -len;
        }

        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
            {
                var c = img.GetPixel(x, y);
                int mx = Math.Max(c.Red, Math.Max(c.Green, c.Blue)), mn = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                if (mn < 150 || mx - mn > 45) continue;
                var inner = Math.Min(Math.Min(Edge(0, x, y), Edge(1, x, y)), Math.Min(Edge(2, x, y), Edge(3, x, y)));
                if (inner > 14 || inner < -pad) continue;
                SKColor fill;
                if (dark)
                {
                    var t = Math.Clamp((y - top) / span, 0f, 1f);
                    fill = new SKColor((byte)(32 + (14 - 32) * t), (byte)(44 + (16 - 44) * t), (byte)(70 + (24 - 70) * t));
                }
                else fill = new SKColor(242, 242, 247);
                // điểm xám ở mép (khử răng cưa) trộn theo độ sáng cho khỏi gắt
                var w = Math.Clamp((mn - 150) / 70f, 0f, 1f);
                img.SetPixel(x, y, new SKColor(
                    (byte)(c.Red + (fill.Red - c.Red) * w), (byte)(c.Green + (fill.Green - c.Green) * w), (byte)(c.Blue + (fill.Blue - c.Blue) * w)));
            }
    }

    /// <summary>Phóng tối đa của cảnh chèn tay cầm điện thoại.</summary>
    const float InsertZoom = 2.2f;

    internal static (SKPoint Tl, SKPoint Tr, SKPoint Br, SKPoint Bl)? FindScreen(SKBitmap img)
    {
        const int k = 4;
        int gw = img.Width / k, gh = img.Height / k;
        var white = new bool[gw * gh];
        for (var y = 0; y < gh; y++)
            for (var x = 0; x < gw; x++)
            {
                var c = img.GetPixel(x * k + k / 2, y * k + k / 2);
                int mx = Math.Max(c.Red, Math.Max(c.Green, c.Blue)), mn = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                white[y * gw + x] = mn >= 215 && mx - mn <= 28;
            }

        var seen = new bool[white.Length];
        List<int>? best = null;
        var stack = new Stack<int>();
        for (var i = 0; i < white.Length; i++)
        {
            if (!white[i] || seen[i]) continue;
            var comp = new List<int>();
            seen[i] = true;
            stack.Push(i);
            while (stack.Count > 0)
            {
                var p = stack.Pop();
                comp.Add(p);
                int px = p % gw, py = p / gw;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = px + dx, ny = py + dy;
                    if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                    var n = ny * gw + nx;
                    if (white[n] && !seen[n]) { seen[n] = true; stack.Push(n); }
                }
            }

            if (best is null || comp.Count > best.Count) best = comp;
        }

        if (best is null || best.Count < white.Length * 0.015) return null;
        SKPoint P(int p) => new((p % gw) * k + k / 2f, (p / gw) * k + k / 2f);
        var tl = P(best.MinBy(p => p % gw + p / gw));
        var br = P(best.MaxBy(p => p % gw + p / gw));
        var tr = P(best.MaxBy(p => p % gw - p / gw));
        var bl = P(best.MinBy(p => p % gw - p / gw));
        var cx = (tl.X + tr.X + br.X + bl.X) / 4;
        var cy = (tl.Y + tr.Y + br.Y + bl.Y) / 4;
        // góc lấy theo đường chéo nằm lọt trong góc bo của màn hình, cạnh lệch nửa ô lưới: đẩy góc ra 6 điểm ảnh cho khỏi hở viền trắng
        // (bài học 2026-11-05: thu vào 3 điểm ảnh để lộ viền trắng quanh chữ)
        SKPoint Out(SKPoint a) { var dx = a.X - cx; var dy = a.Y - cy; var d = (float)Math.Sqrt(dx * dx + dy * dy); return d < 1 ? a : new SKPoint(a.X + dx / d * 6, a.Y + dy / d * 6); }
        return (Out(tl), Out(tr), Out(br), Out(bl));
    }

    /// <summary>Ma trận chiếu đưa hình chữ nhật (0,0)–(w,h) về bốn góc cho trước.</summary>
    internal static SKMatrix Homography(float w, float h, SKPoint tl, SKPoint tr, SKPoint br, SKPoint bl)
    {
        var srcPts = new[] { (0f, 0f), (w, 0f), (w, h), (0f, h) };
        var dst = new[] { tl, tr, br, bl };
        var a = new double[8, 9];
        for (var i = 0; i < 4; i++)
        {
            double x = srcPts[i].Item1, y = srcPts[i].Item2, u = dst[i].X, v = dst[i].Y;
            double[] r1 = [x, y, 1, 0, 0, 0, -u * x, -u * y, u];
            double[] r2 = [0, 0, 0, x, y, 1, -v * x, -v * y, v];
            for (var c = 0; c < 9; c++) { a[2 * i, c] = r1[c]; a[2 * i + 1, c] = r2[c]; }
        }

        for (var col = 0; col < 8; col++)
        {
            var piv = col;
            for (var r = col + 1; r < 8; r++) if (Math.Abs(a[r, col]) > Math.Abs(a[piv, col])) piv = r;
            for (var c = 0; c < 9; c++) (a[col, c], a[piv, c]) = (a[piv, c], a[col, c]);
            for (var r = 0; r < 8; r++)
            {
                if (r == col) continue;
                var f = a[r, col] / a[col, col];
                for (var c = col; c < 9; c++) a[r, c] -= f * a[col, c];
            }
        }

        var hv = new double[8];
        for (var i = 0; i < 8; i++) hv[i] = a[i, 8] / a[i, i];
        return new SKMatrix((float)hv[0], (float)hv[1], (float)hv[2], (float)hv[3], (float)hv[4], (float)hv[5], (float)hv[6], (float)hv[7], 1f);
    }

    /// <summary>Vẽ chữ với (x, y) là mép trên (giống PIL), không phải đường chân chữ.</summary>
    static void DrawTop(SKCanvas canvas, string text, double x, double y, SKFont font, SKPaint paint) =>
        canvas.DrawText(text, (float)x, (float)(y - font.Metrics.Ascent), font, paint);

    internal static List<string> Wrap(string text, SKFont font, double maxWidth)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = cur.Length == 0 ? word : cur + " " + word;
            if (font.MeasureText(t) > maxWidth && cur.Length > 0)
            {
                lines.Add(cur);
                cur = word;
            }
            else
            {
                cur = t;
            }
        }

        lines.Add(cur);
        return lines;
    }
}
