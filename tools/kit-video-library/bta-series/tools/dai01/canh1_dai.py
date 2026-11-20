"""Cảnh 1 tập 01, phong cách mới: ảnh AI làm lớp chính + máy quay chuyển động, đồ họa code làm lớp chú thích, giọng v4.

    python canh1_moi.py <audio> <out.mp4> [fps] [--doc] [--frames t1,t2,...]

    --doc     bản dọc 1080x1920 cho Shorts (mặc định: bản ngang 1920x1080)
    --frames  chỉ xuất vài khung hình JPG để soát

Thay đổi so với bản trước: vòng khoanh con thỏ đổi sang màu kem; thêm bản dọc.
"""
import sys, subprocess
sys.path.insert(0, str(__import__("pathlib").Path(__file__).parent))
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

ROOT = Path("/home/claude/kp")
IMG = ROOT / "docs/marketing/bta/hinh-ai/01"
FONTS = ROOT / "tools/kit-video-library/bta-series/fonts"
VERT = "--doc" in sys.argv
W, H = (1080, 1920) if VERT else (1920, 1080)
CREAM = (244, 236, 220); INK = (28, 36, 64); VERM = (214, 72, 48); JADE = (46, 140, 120)
F_DISP = lambda s: ImageFont.truetype(str(FONTS / "BTA-Display.ttf"), int(s))
F_BOLD = lambda s: ImageFont.truetype(str(FONTS / "BTA-Sans-Bold.ttf"), int(s))
F_REG = lambda s: ImageFont.truetype(str(FONTS / "BTA-Sans-Regular.ttf"), int(s))
END = 31.95
IW, IH = 2720, 1536  # dùng ảnh gốc, không thu nhỏ: bản dọc phóng ít hơn nên nét hơn

sys.path.insert(0, "/home/claude/dai")
from nhip_dai import PAUSES, LEAD, fwd, inv  # nhịp video dài

# ---------- bố cục chữ: ngang / dọc ----------
if not VERT:
    L = dict(px=110, py=150, py2=235, cdy=82, pill=40, city=46, sub=34,
             years=[(500, 520), (960, 520), (1420, 520)], year_size=130,
             next5=(W / 2, 140, 44), next5_hdr=(150, 130, 38),
             tx=150, ty=250, tdy=150, tnum=110, ttext=64, tnum_gap=110,
             st_rab=110, st_cat=(1440, 700, 88), st_you=(1450, 820, 96), st_wrong=(1500, 690, 80),
             cap_y=0.80, cap_w=0.80, cap_size=46)
else:  # chừa vùng trên ~250px và dưới ~400px cho giao diện Shorts
    L = dict(px=70, py=270, py2=350, cdy=80, pill=38, city=44, sub=32,
             years=[(200, 540), (540, 540), (880, 540)], year_size=96,  # hàng ngang phía trên dải ảnh, không che album
             next5=(W / 2, 270, 40), next5_hdr=(70, 400, 36),
             tx=70, ty=560, tdy=200, tnum=104, ttext=56, tnum_gap=96,
             st_rab=100, st_cat=(560, 640, 76), st_you=(540, 430, 84), st_wrong=(560, 1180, 76),
             cap_y=0.70, cap_w=0.86, cap_size=46)

# ---------- ảnh nguồn (thu nhỏ một lần để mỗi khung cắt nhanh) ----------
_cache = {}
def photo(name):
    if name not in _cache:
        _cache[name] = Image.open(IMG / f"{name}.png").convert("RGB").resize((IW, IH), Image.LANCZOS)  # ảnh đúng cỡ thì giữ nguyên
    return _cache[name]

def ease(x):
    x = min(1, max(0, x)); return x * x * (3 - 2 * x)

def fit(iw, ih, aspect, z):
    """khung cắt lớn nhất theo tỉ lệ aspect (= rộng/cao) rồi chia cho zoom z"""
    if iw / ih > aspect: ch = ih / z; cw = ch * aspect
    else: cw = iw / z; ch = cw / aspect
    return cw, ch

def crop_box(a, b, p, aspect=None):
    aspect = aspect or W / H
    e = ease(p) * 0.6 + p * 0.4
    cx = a[0] + (b[0] - a[0]) * e; cy = a[1] + (b[1] - a[1]) * e; z = a[2] + (b[2] - a[2]) * e
    cw, ch = fit(IW, IH, aspect, z)
    x0 = min(max(cx * IW - cw / 2, 0), IW - cw); y0 = min(max(cy * IH - ch / 2, 0), IH - ch)
    return x0, y0, cw, ch, z

def cam(name, a, b, p):
    """a, b = (cx, cy, zoom) đầu/cuối; zoom 1 = khung đầy ảnh. p = 0..1"""
    x0, y0, cw, ch, _ = crop_box(a, b, p)
    return photo(name).transform((W, H), Image.EXTENT, (x0, y0, x0 + cw, y0 + ch), Image.BICUBIC)

# ---------- danh sách cảnh quay: (bắt đầu, kết thúc, ảnh, cam_a, cam_b, kiểu vào) ----------
if not VERT:
    SHOTS = [
        (0.00, 4.58, "a01-placemat", (0.50, 0.50, 1.05), (0.36, 0.66, 1.55), "fade"),
        (4.58, 5.30, "a02-beijing", (0.42, 0.45, 1.25), (0.52, 0.45, 1.30), "cut"),
        (5.30, 6.28, "a03-seoul", (0.55, 0.55, 1.30), (0.45, 0.52, 1.35), "cut"),
        (6.28, 7.05, "a04-tokyo", (0.40, 0.45, 1.25), (0.50, 0.45, 1.30), "cut"),
        (7.05, 8.14, "a05-singapore", (0.66, 0.55, 1.25), (0.72, 0.52, 1.32), "cut"),  # bám con thỏ đèn lồng bên phải
        (8.14, 10.47, "GRID", None, None, "cut"),
        (10.47, 12.75, "a06-hanoi-cat", (0.45, 0.42, 1.10), (0.45, 0.38, 1.70), "flash"),
        (12.75, 17.74, "a07-album", (0.50, 0.50, 1.05), (0.42, 0.62, 1.45), "fade"),
        (17.74, 20.10, "a08-cat-portrait", (0.45, 0.45, 1.15), (0.43, 0.40, 1.75), "fade"),
        (20.10, 21.72, "a08-cat-portrait", (0.43, 0.40, 1.75), (0.45, 0.45, 1.30), "none"),
        (21.72, 24.93, "a01-placemat", (0.50, 0.50, 1.50), (0.50, 0.50, 1.05), "fade"),
        (24.93, 27.82, "a06-hanoi-cat", (0.60, 0.50, 1.30), (0.42, 0.45, 1.20), "fade"),
        (27.82, END + 0.6, "a08-cat-portrait", (0.43, 0.42, 1.25), (0.43, 0.38, 1.60), "fade"),
    ]
else:  # khung dọc chỉ lấy ~1/3 bề ngang ảnh nên tâm khung bám sát chủ thể
    SHOTS = [
        (0.00, 4.58, "a01-placemat", (0.42, 0.62, 1.15), (0.36, 0.75, 1.60), "fade"),
        (4.58, 5.30, "a02-beijing", (0.47, 0.45, 1.00), (0.50, 0.42, 1.06), "cut"),
        (5.30, 6.28, "a03-seoul", (0.46, 0.55, 1.00), (0.42, 0.55, 1.06), "cut"),
        (6.28, 7.05, "a04-tokyo", (0.22, 0.45, 1.00), (0.27, 0.45, 1.06), "cut"),
        (7.05, 8.14, "a05-singapore", (0.74, 0.55, 1.00), (0.78, 0.52, 1.08), "cut"),
        (8.14, 10.47, "GRID", None, None, "cut"),
        (10.47, 12.75, "a06-hanoi-cat", (0.43, 0.45, 1.00), (0.43, 0.38, 1.40), "flash"),
        (12.75, 17.74, "a07-album", (0.55, 0.50, 1.00), (0.45, 0.60, 1.25), "fade"),
        (17.74, 20.10, "a08-cat-portrait", (0.50, 0.45, 1.00), (0.50, 0.42, 1.45), "fade"),
        (20.10, 21.72, "a08-cat-portrait", (0.50, 0.42, 1.45), (0.50, 0.45, 1.10), "none"),
        (21.72, 24.93, "a01-placemat", (0.40, 0.50, 1.15), (0.50, 0.50, 1.00), "fade"),
        (24.93, 27.82, "a06-hanoi-cat", (0.50, 0.50, 1.10), (0.43, 0.45, 1.00), "fade"),
        (27.82, END + 0.6, "a08-cat-portrait", (0.50, 0.42, 1.05), (0.50, 0.40, 1.30), "fade"),
    ]
CITIES = [(4.58, "BEIJING · CHINA"), (5.30, "SEOUL · KOREA"), (6.28, "TOKYO · JAPAN"), (7.05, "SINGAPORE")]
COUNTRIES = ("CHINA", "KOREA", "JAPAN", "SINGAPORE")
def _ns(t): return fwd(t) - LEAD.get(t, 0.0)
NSHOTS = [(_ns(s), _ns(e), n, a, b, k) for s, e, n, a, b, k in SHOTS]  # cảnh quay trên thời gian video mới
VEND = fwd(END)

GRID = [("a02-beijing", (0.50, 0.40)), ("a03-seoul", (0.40, 0.55)), ("a04-tokyo", (0.18, 0.40)), ("a05-singapore", (0.45, 0.50))]
GRID_V = [("a02-beijing", (0.49, 0.25)), ("a03-seoul", (0.42, 0.62)), ("a04-tokyo", (0.22, 0.45)), ("a05-singapore", (0.78, 0.55))]

# bản dọc: cảnh toàn hiện NGUYÊN khung ngang trên nền mờ; cảnh cận (lót bàn, mèo) vẫn cắt dọc
FIT = {"a02-beijing", "a03-seoul", "a04-tokyo", "a05-singapore", "a06-hanoi-cat", "a07-album"} if VERT else set()
BAND_H = int(W * 9 / 16); BAND_Y = 680  # dải ảnh 1080x607, chừa trên cho nhãn, dưới cho phụ đề
_bg = {}
def blur_bg(name, cx):
    if name not in _bg:
        cw, ch = fit(IW, IH, W / H, 1.0); x0 = min(max(cx * IW - cw / 2, 0), IW - cw)
        sm = photo(name).crop((int(x0), 0, int(x0 + cw), int(ch))).resize((W // 8, H // 8), Image.BILINEAR)
        sm = sm.filter(ImageFilter.GaussianBlur(5)).resize((W, H), Image.BICUBIC)
        _bg[name] = ImageEnhance.Brightness(sm).enhance(0.5)
    return _bg[name]

_shadow = None
def fit_frame(name, a, b, p):
    global _shadow
    e = ease(p) * 0.6 + p * 0.4
    cx = a[0] + (b[0] - a[0]) * e; cy = a[1] + (b[1] - a[1]) * e; z = 1.0 + 0.07 * e  # trượt nhẹ, gần như thấy hết ảnh
    out = blur_bg(name, (a[0] + b[0]) / 2).copy()
    cw, ch = fit(IW, IH, 16 / 9, z)
    x0 = min(max(cx * IW - cw / 2, 0), IW - cw); y0 = min(max(cy * IH - ch / 2, 0), IH - ch)
    band = photo(name).transform((W, BAND_H), Image.EXTENT, (x0, y0, x0 + cw, y0 + ch), Image.BICUBIC)
    if _shadow is None:  # bóng mềm trên/dưới dải ảnh
        m = Image.new("L", (W, H), 0); ImageDraw.Draw(m).rectangle((0, BAND_Y - 6, W, BAND_Y + BAND_H + 6), fill=150)
        _shadow = m.filter(ImageFilter.GaussianBlur(22))
    out.paste((0, 0, 0), (0, 0), _shadow)
    out.paste(band, (0, BAND_Y))
    return out

def grid_cells():
    """(x, y, w, h) của 4 ô: ngang 2x2, dọc 4 hàng"""
    if not VERT:
        gw, gh = W // 2 - 6, H // 2 - 6
        return [((i % 2) * (W // 2) + 3, (i // 2) * (H // 2) + 3, gw, gh) for i in range(4)]
    gh = H // 4 - 6
    return [(3, i * (H // 4) + 3, W - 6, gh) for i in range(4)]

def grid_frame(t, d):
    out = Image.new("RGB", (W, H), CREAM)
    for i, ((n, c), (px, py, gw, gh)) in enumerate(zip(GRID_V if VERT else GRID, grid_cells())):
        k = ease((t - i * 0.08) / 0.35)
        z = (1.7 + 0.12 * (t / d)) if not VERT else (1.0 + 0.06 * (t / d))  # bản dọc: mỗi hàng thấy đủ bề ngang ảnh
        cw, ch = fit(IW, IH, gw / gh, z)
        x0 = min(max(c[0] * IW - cw / 2, 0), IW - cw); y0 = min(max(c[1] * IH - ch / 2, 0), IH - ch)
        tile = photo(n).transform((gw, gh), Image.EXTENT, (x0, y0, x0 + cw, y0 + ch), Image.BICUBIC)
        if k < 1:
            tile = Image.blend(Image.new("RGB", tile.size, CREAM), tile, k)
        out.paste(tile, (px, py))
    return out

def shot_img(i, T):
    s, e, name, a, b, _ = NSHOTS[i]
    p = min(1.0, (T - s) / (e - s))
    if name == "GRID": return grid_frame(T - s, e - s)
    return fit_frame(name, a, b, p) if name in FIT else cam(name, a, b, p)

def base_frame(T):
    i = max(k for k, s in enumerate(NSHOTS) if s[0] <= T)
    s, e, name, a, b, kind = NSHOTS[i]
    fr = shot_img(i, T)
    lt = T - s
    if kind == "fade" and lt < 0.35 and i > 0:
        fr = Image.blend(shot_img(i - 1, T), fr, ease(lt / 0.35))
    if kind == "fade" and i == 0 and lt < 0.5:
        fr = Image.blend(Image.new("RGB", (W, H), CREAM), fr, ease(lt / 0.5))
    if kind == "flash" and lt < 0.22:
        fr = Image.blend(fr, Image.new("RGB", (W, H), (255, 250, 240)), 1 - lt / 0.22)
    return fr

# ---------- hoàn màu chung: ấm nhẹ, viền tối, hạt phim ----------
yy, xx = np.mgrid[0:H, 0:W]
VIG = (1 - 0.38 * (((xx - W / 2) / (W / 2)) ** 2 + ((yy - H / 2) / (H / 2)) ** 2) ** 1.3).clip(0.55, 1)[..., None].astype(np.float32)
RNG = np.random.default_rng(7)
GRAIN = [RNG.normal(0, 5.5, (H // 2, W // 2, 1)).astype(np.float32) for _ in range(6)]
def grade(img, f):
    a = np.asarray(img, dtype=np.float32) * VIG
    a[..., 0] *= 1.03; a[..., 2] *= 0.96
    a = a + GRAIN[f % 6].repeat(2, 0).repeat(2, 1)
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))

# ---------- lớp chú thích (code) ----------
def pill(d, x, y, text, t, size=40, fg=INK, bg=CREAM, dot=VERM, anchor="l", dim=1.0):
    if t <= 0: return
    k = ease(t / 0.35) * dim; f = F_BOLD(size)
    tw = d.textlength(text, font=f); pad = size * 0.55; h = size * 1.55
    w = tw + pad * 2 + (size * 0.8 if dot else 0)
    x0 = x - (w / 2 if anchor == "c" else 0); y0 = y + (1 - k) * 24
    al = int(235 * k)
    d.rounded_rectangle((x0, y0, x0 + w, y0 + h), h / 2, fill=bg + (al,))
    tx = x0 + pad
    if dot:
        r = size * 0.22; d.ellipse((tx, y0 + h / 2 - r, tx + 2 * r, y0 + h / 2 + r), fill=dot + (al,)); tx += size * 0.8
    d.text((tx, y0 + h / 2), text, font=f, fill=fg + (int(255 * k),), anchor="lm")

def stamp(base, x, y, text, t, size=96, color=VERM, rot=-6):
    if t <= 0: return base
    k = min(1, t / 0.18); sc = 1.35 - 0.35 * ease(t / 0.22)
    f = F_DISP(size); dd = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    tw = dd.textlength(text, font=f); pad = size * 0.35
    lay = Image.new("RGBA", (int(tw + pad * 2 + 12), int(size * 1.5 + 12)), (0, 0, 0, 0)); ld = ImageDraw.Draw(lay)
    ld.rounded_rectangle((6, 6, lay.width - 6, lay.height - 6), size * 0.18, fill=color + (240,))
    ld.text((lay.width / 2, lay.height / 2 + size * 0.04), text, font=f, fill=(255, 250, 240), anchor="mm")
    lay = lay.rotate(rot, expand=True, resample=Image.BICUBIC)
    lay = lay.resize((max(1, int(lay.width * sc)), max(1, int(lay.height * sc))), Image.BICUBIC)
    if k < 1:
        lay.putalpha(lay.getchannel("A").point(lambda v: int(v * k)))
    base.alpha_composite(lay, (int(x - lay.width / 2), int(y - lay.height / 2)))
    return base

def ring(d, cx, cy, r, t, color=CREAM, width=9):
    if t <= 0: return
    k = ease(t / 0.55)
    # viền tối mảnh bên dưới để vòng kem vẫn nổi trên nền giấy đỏ
    d.arc((cx - r, cy - r, cx + r, cy + r), -90, -90 + 360 * k, fill=INK + (90,), width=width + 6)
    d.arc((cx - r, cy - r, cx + r, cy + r), -90, -90 + 360 * k, fill=color + (255,), width=width)

def big_year(d, x, y, text, t, size=150):
    if t <= 0: return
    k = ease(t / 0.3); f = F_DISP(size)
    tw = d.textlength(text, font=f)
    d.rounded_rectangle((x - tw / 2 - 30, y - size * 0.62 + (1 - k) * 30, x + tw / 2 + 30, y + size * 0.62 + (1 - k) * 30), 22, fill=CREAM + (int(230 * k),))
    d.text((x, y + (1 - k) * 30), text, font=f, fill=INK + (int(255 * k),), anchor="mm")

def wrap(text, font, room):
    d = ImageDraw.Draw(Image.new("RGBA", (10, 10))); rows, cur = [], ""
    for w_ in text.split():
        t = (cur + " " + w_).strip()
        if d.textlength(t, font=font) <= room or not cur: cur = t
        else: rows.append(cur); cur = w_
    return rows + [cur]

def teaser_font(texts):
    if VERT: return F_DISP(L["ttext"])  # bản dọc: giữ chữ to, xuống dòng thay vì thu nhỏ
    """cỡ chữ chung cho cả 3 câu, đủ vừa bề ngang khung"""
    room = W - L["tx"] - L["tnum_gap"] - 40; s = L["ttext"]
    d = ImageDraw.Draw(Image.new("RGBA", (10, 10)))
    while s > 30 and max(d.textlength(t, font=F_DISP(s)) for t in texts) > room: s -= 2
    return F_DISP(s)

TEASERS = [(21.85, "It's not really about animals"), (25.05, "The famous explanation may be wrong"), (27.95, "The cat was a corrupt official")]
T_FONT = teaser_font([t for _, t in TEASERS])

def teaser(d, n, text, t, y):
    if t <= 0: return
    k = ease(t / 0.35); fn = F_DISP(L["tnum"])
    x = L["tx"] + (1 - k) * -60
    d.text((x, y), str(n), font=fn, fill=VERM + (int(255 * k),), anchor="lm")
    rows = wrap(text, T_FONT, W - L["tx"] - L["tnum_gap"] - 40) if VERT else [text]
    for j, r in enumerate(rows):
        d.text((x + L["tnum_gap"], y + 4 + j * T_FONT.size * 1.18), r, font=T_FONT, fill=(255, 250, 240, int(255 * k)), anchor="lm", stroke_width=3, stroke_fill=INK + (int(200 * k),))

def overlay(img, Tn, T):
    """Tn: thời gian video mới; T: mốc tương ứng trên giọng gốc"""
    base = img.convert("RGBA"); lay = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    # L0: lót bàn — khoanh con thỏ (vòng màu kem)
    if Tn < NSHOTS[0][1]:  # theo cảnh lót bàn (thời gian mới)
        pill(d, L["px"], L["py"], "THE CHINESE ZODIAC · 12 SIGNS", T - 1.2, L["pill"] - 2)
        x0, y0, cw, ch, z = crop_box(SHOTS[0][3], SHOTS[0][4], min(1.0, Tn / NSHOTS[0][1]))
        rx = (0.345 * IW - x0) / cw * W; ry = (0.752 * IH - y0) / ch * H
        ring(d, rx, ry, 163.4 * W / cw, T - 2.6, width=11)  # ~163 px ảnh gốc 2720 quanh con thỏ
    # L1: bốn thành phố
    # tên thành phố + nước: mỗi tên hiện ra theo cảnh của nó rồi ở lại thành danh sách (tên đang chiếu sáng rõ)
    cs = [NSHOTS[i][0] for i in (1, 2, 3, 4)]  # tên đi theo cảnh (thời gian mới), không theo giọng
    if cs[0] <= Tn < NSHOTS[5][0]:
        pill(d, L["px"], L["py"], "4TH SIGN · RABBIT", Tn - cs[0], L["sub"], dot=None)
        cur = max(i for i in range(4) if cs[i] <= Tn)
        for i in range(cur + 1):
            on = i == cur
            pill(d, L["px"], L["py2"] + i * L["cdy"], CITIES[i][1], Tn - cs[i] + 0.15, L["city"] if on else L["city"] * 0.8,
                 dot=JADE if on else None, dim=1.0 if on else 0.72)
    if NSHOTS[5][0] <= Tn < NSHOTS[6][0]:
        for i, (c, (px, py, gw, gh)) in enumerate(zip(COUNTRIES, grid_cells())):
            pill(d, px + 37, py + 33 + (40 if VERT and i == 0 else 0), c, Tn - NSHOTS[5][0] - i * 0.08, 34, dot=JADE)
    # L3: năm sinh
    if 12.75 <= T and Tn < NSHOTS[8][0]:  # tắt khi mặt mèo vào (mèo vào sớm trong khoảng nghỉ)
        pill(d, L["px"], L["py"], "BORN IN…", T - 12.95, L["pill"], dot=None)
        for (x_, y_), s, txt in zip(L["years"], (13.9, 14.8, 16.1), ("1987", "1999", "2011")):
            big_year(d, x_, y_, txt, T - s, L["year_size"])
    # L5: ba câu hỏi nhử
    if 21.72 <= T:
        k = ease((T - 21.72) / 0.4)
        if not VERT:
            for gx in range(0, 1300, 20):
                d.rectangle((gx, 0, gx + 20, H), fill=(14, 18, 34, int(170 * k * max(0, 1 - gx / 1300))))
        else:  # dải tối ngang quanh khối chữ
            top, bot = L["ty"] - 200, L["ty"] + 2 * L["tdy"] + 280
            for gy in range(top, bot, 20):
                m = min(gy - top, bot - gy) / 160
                d.rectangle((0, gy, W, gy + 20), fill=(14, 18, 34, int(165 * k * min(1, m))))
        for n, (s, txt) in enumerate(TEASERS):
            teaser(d, n + 1, txt, T - s, L["ty"] + n * L["tdy"])
    # vẽ sau dải tối, nếu không dải tối sẽ xóa mất nhãn
    if 20.10 <= T:  # giữ đến hết cảnh, làm tiêu đề cho 3 câu hỏi nhử
        nx, ny, ns = L["next5"] if T < 21.72 else L["next5_hdr"]
        pill(d, nx, ny, "IN THE NEXT FEW MINUTES", T - 20.12, ns, anchor="l" if T >= 21.72 else "c", dot=VERM)
    base.alpha_composite(lay)
    # dấu đỏ
    if 10.47 <= T < 12.75:
        pill(ImageDraw.Draw(base), L["px"], L["py"], "HANOI", T - 10.6, L["city"], dot=JADE)
        base = stamp(base, L["st_cat"][0], L["st_cat"][1], "4TH SIGN · CAT", T - 11.62, L["st_cat"][2])
    if 8.14 <= T < 10.47:  # chốt ý "con thứ tư là thỏ" trước khi lật sang mèo
        base = stamp(base, W / 2, H / 2, "RABBIT", T - 9.35, L["st_rab"], color=INK, rot=-4)
    if 17.74 <= T < 20.10:
        base = stamp(base, L["st_you"][0], L["st_you"][1], "YOU'RE A CAT", T - 18.90, L["st_you"][2])
    if 24.93 <= T < 27.82:
        base = stamp(base, L["st_wrong"][0], L["st_wrong"][1], "WRONG?", T - 26.2, L["st_wrong"][2], rot=5)
    return base

# ---------- phụ đề giả lập (như bản cũ) ----------
# phụ đề theo cụm, mốc lấy từ các chỗ ngắt của giọng v4 (silencedetect -38 dB, 0,12 s)
TEXT = [
 (0.0, "You might know the Chinese zodiac"), (2.88, "from a placemat at a Chinese restaurant."),
 (4.58, "From Beijing to Seoul,"), (6.16, "from Tokyo to Singapore,"), (8.0, "the fourth animal is the rabbit."),
 (10.47, "In Vietnam..."), (11.48, "it's a cat."),
 (12.75, "Born in 1987,"), (14.66, "1999 or 2011?"), (17.74, "In Vietnam, you're a cat."),
 (20.09, "In the next few minutes:"), (21.62, "why the zodiac isn't really about animals,"),
 (24.77, "why the famous explanation may be wrong,"), (27.68, "and why Vietnam's cat"), (30.16, "was once a corrupt official.")]
CAP = F_BOLD(L["cap_size"])
def caption(img, T):
    text = [t for s, t in TEXT if s <= T][-1]
    d = ImageDraw.Draw(img, "RGBA"); words = text.split(); rows, cur = [], ""
    for w_ in words:
        t = (cur + " " + w_).strip()
        if d.textlength(t, font=CAP) <= W * L["cap_w"] or not cur: cur = t
        else: rows.append(cur); cur = w_
    rows.append(cur)
    if len(rows) > 2:  # câu dài: hiện theo đoạn 2 dòng, theo thời gian
        s = [s for s, t in TEXT if s <= T][-1]
        nxt = [s2 for s2, _ in TEXT if s2 > s]; e = nxt[0] if nxt else END
        chunks = [rows[i:i + 2] for i in range(0, len(rows), 2)]
        rows = chunks[min(len(chunks) - 1, int((T - s) / (e - s) * len(chunks)))]
    for i, r in enumerate(rows):
        cy = 0.205 if VERT and T < 4.58 else L["cap_y"]  # bản dọc, cảnh lót bàn: phụ đề lên trên để không đè vòng khoanh
        tw = d.textlength(r, font=CAP); y = H * cy + i * CAP.size * 1.35
        d.rounded_rectangle((W / 2 - tw / 2 - 18, y - 8, W / 2 + tw / 2 + 18, y + CAP.size * 1.2), 12, fill=INK + (215,))
        d.text((W / 2, y), r, font=CAP, fill=(255, 252, 246), anchor="ma")
    return img

def frame(Tn, f=0):
    T = inv(Tn)
    img = grade(base_frame(Tn), f)
    img = caption(overlay(img, Tn, T).convert("RGB"), T)
    if Tn > VEND - 0.4:  # mờ về nền kem sau cùng, kéo cả phụ đề theo
        img = Image.blend(img, Image.new("RGB", (W, H), CREAM), ease((Tn - (VEND - 0.4)) / 0.6))
    return img

if __name__ == "__main__":
    args = [a for a in sys.argv[1:] if a != "--doc"]
    if "--frames" in args:
        ts = [float(x) for x in args[args.index("--frames") + 1].split(",")]
        tag = "doc" if VERT else "ngang"
        for t in ts: frame(t).save(f"/home/claude/f_{tag}_{t:05.2f}.jpg", quality=85)
        sys.exit()
    AUDIO, OUT = args[0], args[1]; FPS = int(args[2]) if len(args) > 2 else 30
    p = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "warning", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                          "-i", AUDIO, "-t", f"{VEND + 0.3:.2f}", "-c:v", "libx264", "-preset", "veryfast", "-crf", "19", "-pix_fmt", "yuv420p",
                          "-c:a", "aac", "-b:a", "160k", OUT], stdin=subprocess.PIPE)
    for f in range(int((VEND + 0.3) * FPS)):
        p.stdin.write(frame(f / FPS, f).tobytes())
    p.stdin.close(); p.wait()
