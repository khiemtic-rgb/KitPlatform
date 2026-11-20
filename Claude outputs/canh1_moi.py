"""Cảnh 1 tập 01, phong cách mới: ảnh AI làm lớp chính + máy quay chuyển động, đồ họa code làm lớp chú thích, giọng v4.

    python canh1_moi.py <audio.mp3> <out.mp4> [fps] [--frames t1,t2,...]
"""
import sys, math, subprocess
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

ROOT = Path("/home/claude/kp")
IMG = ROOT / "docs/marketing/bta/hinh-ai/01"
FONTS = ROOT / "tools/kit-video-library/bta-series/fonts"
W, H = 1920, 1080
CREAM = (244, 236, 220); INK = (28, 36, 64); VERM = (214, 72, 48); JADE = (46, 140, 120)
F_DISP = lambda s: ImageFont.truetype(str(FONTS / "BTA-Display.ttf"), s)
F_BOLD = lambda s: ImageFont.truetype(str(FONTS / "BTA-Sans-Bold.ttf"), s)
F_REG = lambda s: ImageFont.truetype(str(FONTS / "BTA-Sans-Regular.ttf"), s)
END = 31.95

# ---------- ảnh nguồn (thu nhỏ một lần để mỗi khung cắt nhanh) ----------
_cache = {}
def photo(name):
    if name not in _cache:
        im = Image.open(IMG / f"{name}.png").convert("RGB").resize((2400, 1355), Image.LANCZOS)
        _cache[name] = im
    return _cache[name]

def ease(x):
    x = min(1, max(0, x)); return x * x * (3 - 2 * x)

def cam(name, a, b, p):
    """a, b = (cx, cy, zoom) đầu/cuối; zoom 1 = khung đầy ảnh. p = 0..1"""
    im = photo(name); iw, ih = im.size
    e = ease(p) * 0.6 + p * 0.4
    cx = a[0] + (b[0] - a[0]) * e; cy = a[1] + (b[1] - a[1]) * e; z = a[2] + (b[2] - a[2]) * e
    cw = iw / z; ch = cw * H / W
    x0 = min(max(cx * iw - cw / 2, 0), iw - cw); y0 = min(max(cy * ih - ch / 2, 0), ih - ch)
    return im.transform((W, H), Image.EXTENT, (x0, y0, x0 + cw, y0 + ch), Image.BICUBIC)

# ---------- danh sách cảnh quay: (bắt đầu, kết thúc, ảnh, cam_a, cam_b, kiểu vào) ----------
SHOTS = [
    (0.00, 4.58, "a01-placemat", (0.50, 0.50, 1.05), (0.36, 0.66, 1.55), "fade"),
    (4.58, 5.30, "a02-beijing", (0.42, 0.45, 1.25), (0.52, 0.45, 1.30), "cut"),
    (5.30, 6.28, "a03-seoul", (0.55, 0.55, 1.30), (0.45, 0.52, 1.35), "cut"),
    (6.28, 7.05, "a04-tokyo", (0.40, 0.45, 1.25), (0.50, 0.45, 1.30), "cut"),
    (7.05, 8.14, "a05-singapore", (0.40, 0.55, 1.30), (0.38, 0.52, 1.40), "cut"),
    (8.14, 10.47, "GRID", None, None, "cut"),
    (10.47, 12.75, "a06-hanoi-cat", (0.45, 0.42, 1.10), (0.45, 0.38, 1.70), "flash"),
    (12.75, 17.74, "a07-album", (0.50, 0.50, 1.05), (0.42, 0.62, 1.45), "fade"),
    (17.74, 20.10, "a08-cat-portrait", (0.45, 0.45, 1.15), (0.43, 0.40, 1.75), "fade"),
    (20.10, 21.72, "a08-cat-portrait", (0.43, 0.40, 1.75), (0.45, 0.45, 1.30), "none"),
    (21.72, 24.93, "a01-placemat", (0.50, 0.50, 1.50), (0.50, 0.50, 1.05), "fade"),
    (24.93, 27.82, "a06-hanoi-cat", (0.60, 0.50, 1.30), (0.42, 0.45, 1.20), "fade"),
    (27.82, END + 0.6, "a08-cat-portrait", (0.43, 0.42, 1.25), (0.43, 0.38, 1.60), "fade"),
]
GRID = [("a02-beijing", (0.50, 0.40)), ("a03-seoul", (0.40, 0.55)), ("a04-tokyo", (0.18, 0.40)), ("a05-singapore", (0.45, 0.50))]

def grid_frame(t, d):
    out = Image.new("RGB", (W, H), CREAM)
    gw, gh = W // 2 - 6, H // 2 - 6
    for i, (n, c) in enumerate(GRID):
        k = ease((t - i * 0.08) / 0.35)
        z = 1.7 + 0.12 * (t / d)
        im = photo(n); iw, ih = im.size
        cw = iw / z; ch = cw * gh / gw
        x0 = min(max(c[0] * iw - cw / 2, 0), iw - cw); y0 = min(max(c[1] * ih - ch / 2, 0), ih - ch)
        tile = im.transform((gw, gh), Image.EXTENT, (x0, y0, x0 + cw, y0 + ch), Image.BICUBIC)
        px = (i % 2) * (W // 2) + 3; py = (i // 2) * (H // 2) + 3
        if k < 1:
            tile = Image.blend(Image.new("RGB", tile.size, CREAM), tile, k)
        out.paste(tile, (px, py))
    return out

def base_frame(T):
    i = max(k for k, s in enumerate(SHOTS) if s[0] <= T)
    s, e, name, a, b, kind = SHOTS[i]
    p = (T - s) / (e - s)
    fr = grid_frame(T - s, e - s) if name == "GRID" else cam(name, a, b, p)
    lt = T - s
    if kind == "fade" and lt < 0.35 and i > 0:
        ps, pe, pn, pa, pb, _ = SHOTS[i - 1]
        prev = grid_frame(T - ps, pe - ps) if pn == "GRID" else cam(pn, pa, pb, min(1.0, (T - ps) / (pe - ps)))
        fr = Image.blend(prev, fr, ease(lt / 0.35))
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
    a = np.asarray(img, dtype=np.float32)
    a = a * VIG
    a[..., 0] *= 1.03; a[..., 2] *= 0.96
    g = GRAIN[f % 6].repeat(2, 0).repeat(2, 1)
    a = a + g
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))

# ---------- lớp chú thích (code) ----------
def pill(d, x, y, text, t, size=40, fg=INK, bg=CREAM, dot=VERM, anchor="l"):
    if t <= 0: return
    k = ease(t / 0.35); f = F_BOLD(size)
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
    f = F_DISP(size); tmp = Image.new("RGBA", (10, 10)); dd = ImageDraw.Draw(tmp)
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

def ring(d, cx, cy, r, t, color=VERM, width=9):
    if t <= 0: return
    k = ease(t / 0.55)
    d.arc((cx - r, cy - r, cx + r, cy + r), -90, -90 + 360 * k, fill=color + (255,), width=width)

def big_year(d, x, y, text, t, size=150):
    if t <= 0: return
    k = ease(t / 0.3); f = F_DISP(size)
    tw = d.textlength(text, font=f)
    d.rounded_rectangle((x - tw / 2 - 30, y - size * 0.62 + (1 - k) * 30, x + tw / 2 + 30, y + size * 0.62 + (1 - k) * 30), 22, fill=CREAM + (int(230 * k),))
    d.text((x, y + (1 - k) * 30), text, font=f, fill=INK + (int(255 * k),), anchor="mm")

def teaser(d, n, text, t, y):
    if t <= 0: return
    k = ease(t / 0.35); f = F_DISP(64); fn = F_DISP(110)
    x = 150 + (1 - k) * -60
    d.text((x, y), str(n), font=fn, fill=VERM + (int(255 * k),), anchor="lm")
    d.text((x + 110, y + 4), text, font=f, fill=(255, 250, 240, int(255 * k)), anchor="lm", stroke_width=3, stroke_fill=INK + (int(200 * k),))

def overlay(img, T):
    base = img.convert("RGBA"); lay = Image.new("RGBA", (W, H), (0, 0, 0, 0)); d = ImageDraw.Draw(lay)
    # L0: lót bàn — khoanh con thỏ
    if T < 4.58:
        pill(d, 110, 150, "THE CHINESE ZODIAC · 12 SIGNS", T - 1.2, 38)
        a, b = SHOTS[0][3], SHOTS[0][4]; p = T / 4.58; e = ease(p) * 0.6 + p * 0.4
        cx = a[0] + (b[0] - a[0]) * e; cy = a[1] + (b[1] - a[1]) * e; z = a[2] + (b[2] - a[2]) * e
        iw, ih = 2400, 1355; cw = iw / z; ch = cw * H / W
        x0 = min(max(cx * iw - cw / 2, 0), iw - cw); y0 = min(max(cy * ih - ch / 2, 0), ih - ch)
        rx = (0.345 * iw - x0) / cw * W; ry = (0.752 * ih - y0) / ch * H
        ring(d, rx, ry, 150 * z / 1.3, T - 2.6, color=CREAM, width=11)
    # L1: bốn thành phố
    for s, e, city in ((4.58, 5.30, "BEIJING"), (5.30, 6.28, "SEOUL"), (6.28, 7.05, "TOKYO"), (7.05, 8.14, "SINGAPORE")):
        if s <= T < e:
            pill(d, 110, 150, city, T - s + 0.25, 46, dot=JADE)
            pill(d, 110, 240, "4TH SIGN · RABBIT", T - s - 0.05, 34, dot=None)
    if 8.14 <= T < 10.47:
        for i, c in enumerate(("BEIJING", "SEOUL", "TOKYO", "SINGAPORE")):
            pill(d, (i % 2) * W // 2 + 40, (i // 2) * H // 2 + 36, c, T - 8.14 - i * 0.08, 30, dot=JADE)
    # L3: năm sinh
    if 12.75 <= T < 17.74:
        pill(d, 110, 150, "BORN IN…", T - 12.95, 40, dot=None)
        for xx_, s, txt in ((500, 13.9, "1987"), (960, 14.8, "1999"), (1420, 16.1, "2011")):
            big_year(d, xx_, 520, txt, T - s, 130)
    if 20.10 <= T < 21.72:
        pill(d, W / 2, 140, "IN THE NEXT 5 MINUTES", T - 20.2, 44, anchor="c", dot=VERM)
    # L5: ba câu hỏi nhử
    if 21.72 <= T:
        y0 = 250
        k = ease((T - 21.72) / 0.4)
        for gx in range(0, 1300, 20):
            d.rectangle((gx, 0, gx + 20, H), fill=(14, 18, 34, int(170 * k * max(0, 1 - gx / 1300))))
        teaser(d, 1, "It's not really about animals", T - 21.85, y0)
        teaser(d, 2, "The famous explanation may be wrong", T - 25.05, y0 + 150)
        teaser(d, 3, "The cat was a corrupt official", T - 27.95, y0 + 300)
    base.alpha_composite(lay)
    # dấu đỏ
    if 10.47 <= T < 12.75:
        pill(ImageDraw.Draw(base), 110, 150, "HANOI", T - 10.6, 46, dot=JADE)
        base = stamp(base, 1440, 700, "4TH SIGN · CAT", T - 11.35, 88)
    if 17.74 <= T < 20.10:
        base = stamp(base, 1450, 820, "YOU'RE A CAT", T - 18.85, 96)
    if 24.93 <= T < 27.82:
        base = stamp(base, 1500, 860, "WRONG?", T - 26.2, 80, rot=5)
    return base

# ---------- phụ đề giả lập (như bản cũ) ----------
TEXT = [
 (0.0, "You might know the Chinese zodiac from a placemat at a Chinese restaurant."),
 (4.58, "From Beijing to Seoul, from Tokyo to Singapore, the fourth animal is the rabbit."),
 (10.47, "In Vietnam... it's a cat."), (12.75, "Born in 1987, 1999 or 2011?"), (17.74, "In Vietnam, you're a cat."),
 (20.10, "In the next five minutes: why the zodiac isn't really about animals, why the famous explanation may be wrong, and why Vietnam's cat was once a corrupt official.")]
CAP = F_BOLD(46)
def caption(img, T):
    text = [t for s, t in TEXT if s <= T][-1]
    d = ImageDraw.Draw(img, "RGBA"); words = text.split(); rows, cur = [], ""
    for w_ in words:
        t = (cur + " " + w_).strip()
        if d.textlength(t, font=CAP) <= W * 0.8 or not cur: cur = t
        else: rows.append(cur); cur = w_
    rows.append(cur)
    if len(rows) > 2:  # câu dài: hiện theo đoạn 2 dòng, theo thời gian
        s = [s for s, t in TEXT if s <= T][-1]; e = END
        chunks = [rows[i:i + 2] for i in range(0, len(rows), 2)]
        rows = chunks[min(len(chunks) - 1, int((T - s) / (e - s) * len(chunks)))]
    for i, r in enumerate(rows):
        tw = d.textlength(r, font=CAP); y = H * 0.80 + i * CAP.size * 1.35
        d.rounded_rectangle((W / 2 - tw / 2 - 18, y - 8, W / 2 + tw / 2 + 18, y + CAP.size * 1.2), 12, fill=INK + (215,))
        d.text((W / 2, y), r, font=CAP, fill=(255, 252, 246), anchor="ma")
    return img

def frame(T, f=0):
    img = grade(base_frame(T), f)
    img = overlay(img, T).convert("RGB")
    if T > END - 0.4:
        img = Image.blend(img, Image.new("RGB", (W, H), CREAM), ease((T - (END - 0.4)) / 0.6))
    return caption(img, T)

if __name__ == "__main__":
    if "--frames" in sys.argv:
        ts = [float(x) for x in sys.argv[sys.argv.index("--frames") + 1].split(",")]
        for t in ts: frame(t).save(f"/home/claude/f_{t:05.2f}.jpg", quality=85)
        sys.exit()
    AUDIO, OUT = sys.argv[1], sys.argv[2]; FPS = int(sys.argv[3]) if len(sys.argv) > 3 else 30
    p = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "warning", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                          "-i", AUDIO, "-t", f"{END + 0.3:.2f}", "-c:v", "libx264", "-preset", "veryfast", "-crf", "19", "-pix_fmt", "yuv420p",
                          "-c:a", "aac", "-b:a", "160k", OUT], stdin=subprocess.PIPE)
    for f in range(int((END + 0.3) * FPS)):
        p.stdin.write(frame(f / FPS, f).tobytes())
    p.stdin.close(); p.wait()
