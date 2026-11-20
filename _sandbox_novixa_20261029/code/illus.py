"""Vector-illustration engine for NOVIXA explainer (sandbox). Supersampled PIL drawing."""
import math, os
from functools import lru_cache
from PIL import Image, ImageDraw, ImageFont, ImageFilter

SS = 2
W, H = 1080, 1920
WHITE = (246, 249, 253); INK = (14, 28, 56); NAVY = (6, 18, 44)
CYAN = (64, 206, 250); GREEN = (64, 196, 136); ORANGE = (255, 164, 56); RED = (240, 84, 100)
PURPLE = (146, 104, 236); YEL = (255, 212, 58); GREY = (150, 166, 190); SKIN = (255, 214, 178)

FB = [r"C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/opentype/inter/Inter-Bold.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
FR = [r"C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/opentype/inter/Inter-Medium.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]
_fc = {}
_dummy = ImageDraw.Draw(Image.new("RGB", (4, 4)))


def F(size, bold=True):
    size = max(6, int(size)); k = (size, bold)
    if k not in _fc:
        for p in (FB if bold else FR):
            if os.path.exists(p):
                _fc[k] = ImageFont.truetype(p, size); break
    return _fc[k]


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def shade(c, k):
    return mix(c, (255, 255, 255), k) if k >= 0 else mix(c, (0, 0, 0), -k)


def desat(c, k):
    g = int(sum(c) / 3); return mix(c, (g, g, g), k)


def wrap(txt, f, maxw):
    out, cur = [], ""
    for w in txt.split():
        t = (cur + " " + w).strip()
        if _dummy.textlength(t, font=f) <= maxw or not cur: cur = t
        else: out.append(cur); cur = w
    if cur: out.append(cur)
    return out


class Cv:
    def __init__(s, w, h):
        s.w, s.h = int(w), int(h)
        s.im = Image.new("RGBA", (s.w * SS, s.h * SS), (0, 0, 0, 0)); s.d = ImageDraw.Draw(s.im)

    def rect(s, box, fill=None, outline=None, width=0, r=0):
        b = [v * SS for v in box]
        if r: s.d.rounded_rectangle(b, radius=r * SS, fill=fill, outline=outline, width=int(width * SS))
        else: s.d.rectangle(b, fill=fill, outline=outline, width=int(width * SS))

    def ell(s, box, fill=None, outline=None, width=0):
        s.d.ellipse([v * SS for v in box], fill=fill, outline=outline, width=int(width * SS))

    def poly(s, pts, fill=None, outline=None):
        s.d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill, outline=outline)

    def line(s, pts, fill, width=4):
        s.d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=int(width * SS), joint="curve")

    def arc(s, box, a0, a1, fill, width=4):
        s.d.arc([v * SS for v in box], a0, a1, fill=fill, width=int(width * SS))

    def grad(s, box, c1, c2, r=0, horiz=False):
        x0, y0, x1, y1 = [int(v * SS) for v in box]; w, h = x1 - x0, y1 - y0
        if w < 2 or h < 2: return
        g = Image.new("RGBA", (w, h)); gd = ImageDraw.Draw(g); n = w if horiz else h
        for i in range(n):
            c = mix(c1, c2, i / max(1, n - 1)) + (255,)
            gd.line((i, 0, i, h) if horiz else (0, i, w, i), fill=c)
        m = Image.new("L", (w, h), 0); ImageDraw.Draw(m).rounded_rectangle((0, 0, w - 1, h - 1), radius=int(r * SS), fill=255)
        s.im.paste(g, (x0, y0), m)

    def shadow(s, box, blur=12, alpha=120):
        t = Image.new("RGBA", s.im.size, (0, 0, 0, 0)); ImageDraw.Draw(t).ellipse([v * SS for v in box], fill=(0, 0, 0, alpha))
        s.im.alpha_composite(t.filter(ImageFilter.GaussianBlur(blur * SS)))
        s.d = ImageDraw.Draw(s.im)

    def text(s, x, y, txt, size, fill=WHITE, bold=True, anchor="l", maxw=None, gap=1.2, stroke=0, sfill=None):
        f = F(size * SS, bold); lines = wrap(txt, f, maxw * SS) if maxw else [txt]; yy = y * SS
        for ln in lines:
            wd = s.d.textlength(ln, font=f)
            xx = x * SS if anchor == "l" else (x * SS - wd / 2 if anchor == "m" else x * SS - wd)
            s.d.text((xx, yy), ln, font=f, fill=fill, stroke_width=int(stroke * SS), stroke_fill=sfill)
            yy += size * gap * SS
        return len(lines) * size * gap

    def paste(s, img, x, y):
        s.im.alpha_composite(img.resize((img.width * SS, img.height * SS), Image.LANCZOS) if img.width * SS != s.im.width or True else img, (int(x * SS), int(y * SS)))
        s.d = ImageDraw.Draw(s.im)

    def out(s):
        return s.im.resize((s.w, s.h), Image.LANCZOS)


def textsize(txt, size, bold=True):
    return _dummy.textlength(txt, font=F(size, bold))


# ---------------- illustrations (each returns RGBA) ----------------
@lru_cache(None)
def box_img(w, h, color=CYAN, name="Thuốc A", hsd="HSD 12/2027", red=False, dim=False, qm=False):
    cv = Cv(w, h); color = desat(color, .8) if dim else color
    face = (172, 182, 198) if dim else (246, 249, 253)
    d = w * .2; t = h * .1; fw = w - d; bh = (h - t) * .36
    cv.shadow((w * .05, h * .93, w * .98, h * 1.08), 8, 110)
    cv.poly([(0, t), (d, 0), (w, 0), (fw, t)], fill=shade(face, -.04))
    cv.poly([(fw, t), (w, 0), (w, h - t), (fw, h)], fill=shade(face, -.24))
    cv.poly([(fw, t), (w, 0), (w, bh * .9), (fw, t + bh)], fill=shade(color, -.3))
    cv.grad((0, t, fw, h), face, shade(face, -.07), r=3)
    cv.grad((0, t, fw, t + bh), shade(color, .1), shade(color, -.18), r=3)
    r = bh * .3; cx, cy = fw * .15, t + bh * .5
    cv.ell((cx - r, cy - r, cx + r, cy + r), fill=WHITE)
    cv.rect((cx - r * .55, cy - r * .16, cx + r * .55, cy + r * .16), fill=color); cv.rect((cx - r * .16, cy - r * .55, cx + r * .16, cy + r * .55), fill=color)
    fs = max(11, w * .15); cv.text(fw * .30, cy - fs * .62, name, fs, WHITE)
    by = t + bh + (h - t - bh) * .12
    cv.rect((fw * .1, by, fw * .78, by + max(3, h * .022)), fill=(160, 170, 188), r=2)
    cv.rect((fw * .1, by + h * .06, fw * .56, by + h * .06 + max(3, h * .022)), fill=(180, 188, 204), r=2)
    px = fw * .1; py = by + h * .14
    for i in range(3):
        cv.rect((px + i * fw * .13, py, px + i * fw * .13 + fw * .1, py + h * .05), fill=shade(color, .15), r=h * .025)
    if hsd:
        sy = h - (h - t) * .17; cv.rect((fw * .06, sy, fw * .94, h - (h - t) * .04), fill=(255, 226, 230) if red else (226, 234, 246), r=3)
        cv.text(fw * .5, sy + (h - t) * .02, hsd, max(9, w * .09), RED if red else (70, 90, 130), anchor="m")
    if qm:
        cv.ell((w * .3, h * .22, w * .9, h * .22 + w * .6), fill=YEL)
        cv.text(w * .6, h * .22 + w * .02, "?", w * .5, INK, anchor="m")
    return cv.out()


@lru_cache(None)
def bottle_img(w, h, color=ORANGE, dim=False):
    cv = Cv(w, h); color = desat(color, .8) if dim else color
    cv.shadow((w * .05, h * .94, w * .95, h * 1.06), 7, 110)
    cv.rect((w * .3, 0, w * .7, h * .16), fill=shade(color, -.1), r=4)
    cv.rect((w * .34, h * .16, w * .66, h * .24), fill=(210, 224, 240, 230))
    cv.grad((w * .08, h * .22, w * .92, h), (214, 128, 40) if not dim else (130, 120, 110), (150, 84, 20) if not dim else (90, 84, 78), r=w * .1)
    cv.rect((w * .12, h * .42, w * .88, h * .84), fill=WHITE, r=w * .05)
    cv.rect((w * .2, h * .5, w * .8, h * .56), fill=color, r=3); cv.rect((w * .2, h * .62, w * .66, h * .66), fill=(170, 180, 198), r=3); cv.rect((w * .2, h * .7, w * .54, h * .74), fill=(190, 198, 212), r=3)
    return cv.out()


@lru_cache(None)
def blister_img(w, h, color=GREEN):
    cv = Cv(w, h)
    cv.shadow((w * .05, h * .9, w * .95, h * 1.08), 6, 100)
    cv.grad((0, 0, w, h), (214, 222, 236), (170, 182, 204), r=w * .06)
    cols, rows = 4, 2
    for i in range(cols):
        for j in range(rows):
            cx = w * (.16 + i * .22); cy = h * (.3 + j * .4); r = w * .08
            cv.ell((cx - r, cy - r, cx + r, cy + r), fill=color); cv.ell((cx - r * .5, cy - r * .7, cx - r * .1, cy - r * .3), fill=(255, 255, 255, 150))
    return cv.out()


@lru_cache(None)
def coin_img(r=40):
    cv = Cv(r * 2 + 8, r * 2 + 14)
    cv.ell((4, 10, r * 2 + 4, r * 2 + 10), fill=(176, 120, 18)); cv.ell((4, 4, r * 2 + 4, r * 2 + 4), fill=(255, 208, 64), outline=(214, 150, 22), width=max(2, r * .1))
    cv.ell((r * .35 + 4, r * .35 + 4, r * 1.65 + 4, r * 1.65 + 4), outline=(214, 150, 22), width=max(2, r * .07))
    cv.text(r + 4, r * .38, "đ", r * 1.15, (160, 104, 12), anchor="m")
    return cv.out()


@lru_cache(None)
def coins_img(n=5, r=36):
    cw = r * 2 + 8; cv = Cv(cw + (min(n, 3) - 1) * r * 1.2 + 20, r * 2 + 14 + ((n - 1) // 3) * r * .7 + 10)
    k = 0
    for row in range((n + 2) // 3):
        for c in range(3):
            if k >= n: break
            cv.paste(coin_img(r), 10 + c * r * 1.2, cv.h - (r * 2 + 14) - 6 - row * r * .7); k += 1
    return cv.out()


@lru_cache(None)
def bill_img(w=260, h=130, color=GREEN):
    cv = Cv(w, h); cv.shadow((w * .05, h * .88, w * .95, h * 1.1), 6, 100)
    cv.grad((0, 0, w, h * .94), shade(color, .25), shade(color, -.2), r=10)
    cv.rect((w * .04, h * .08, w * .96, h * .86), outline=shade(color, .5), width=3, r=8)
    cv.ell((w * .36, h * .18, w * .64, h * .74), fill=shade(color, .45))
    cv.text(w * .5, h * .22, "đ", h * .42, shade(color, -.45), anchor="m"); cv.text(w * .12, h * .12, "500K", h * .2, WHITE); cv.text(w * .88, h * .62, "500K", h * .2, WHITE, anchor="r")
    return cv.out()


@lru_cache(None)
def wallet_img(w=300, h=220, empty=False):
    cv = Cv(w, h); cv.shadow((w * .05, h * .9, w * .95, h * 1.08), 8, 120)
    cv.grad((0, h * .15, w, h * .95), (150, 96, 60), (104, 64, 38), r=22)
    cv.grad((w * .62, h * .4, w, h * .72), (190, 130, 80), (140, 90, 52), r=14)
    cv.ell((w * .72, h * .5, w * .84, h * .62), fill=YEL)
    if not empty:
        cv.paste(bill_img(int(w * .6), int(h * .4)), w * .1, h * -.02)
    else:
        cv.text(w * .3, h * .5, "0 đ", h * .22, (255, 230, 220), anchor="m")
    return cv.out()


@lru_cache(None)
def person_img(w=300, h=420, mood="ok"):
    cv = Cv(w, h); cx = w / 2
    cv.shadow((w * .1, h * .93, w * .9, h * 1.06), 10, 120)
    cv.poly([(w * .14, h), (w * .2, h * .56), (w * .8, h * .56), (w * .86, h)], fill=(240, 244, 250))
    cv.poly([(w * .14, h), (w * .2, h * .56), (w * .32, h * .58), (w * .27, h)], fill=(214, 224, 238))
    cv.poly([(cx - w * .14, h * .56), (cx, h * .72), (cx + w * .14, h * .56)], fill=(70, 160, 220))
    cv.poly([(cx - w * .08, h * .56), (cx, h * .66), (cx + w * .08, h * .56)], fill=SKIN)
    bx, by = cx + w * .2, h * .74
    cv.ell((bx - w * .09, by - w * .09, bx + w * .09, by + w * .09), fill=GREEN)
    cv.rect((bx - w * .05, by - w * .013, bx + w * .05, by + w * .013), fill=WHITE); cv.rect((bx - w * .013, by - w * .05, bx + w * .013, by + w * .05), fill=WHITE)
    cv.rect((cx - w * .06, h * .5, cx + w * .06, h * .58), fill=SKIN)
    cv.ell((cx - w * .22, h * .12, cx + w * .22, h * .54), fill=SKIN)
    cv.d.pieslice([(cx - w * .24) * SS, (h * .08) * SS, (cx + w * .24) * SS, (h * .42) * SS], 180, 360, fill=(48, 36, 40))
    cv.rect((cx - w * .24, h * .22, cx - w * .17, h * .34), fill=(48, 36, 40), r=4); cv.rect((cx + w * .17, h * .22, cx + w * .24, h * .34), fill=(48, 36, 40), r=4)
    ey = h * .32
    for sx in (-1, 1):
        ex = cx + sx * w * .09
        cv.ell((ex - w * .022, ey - w * .026, ex + w * .022, ey + w * .026), fill=(30, 30, 40))
        if mood == "worry": cv.line([(ex - sx * w * .06, ey - h * .045 - sx * h * .0), (ex + sx * w * .04, ey - h * .06 + (h * .02))], (60, 44, 44), 3)
        else: cv.line([(ex - w * .05, ey - h * .05), (ex + w * .05, ey - h * .05)], (60, 44, 44), 3)
    my = h * .43
    if mood == "worry": cv.arc((cx - w * .07, my, cx + w * .07, my + h * .05), 200, 340, (150, 60, 60), 4)
    elif mood == "think": cv.line([(cx - w * .05, my + h * .01), (cx + w * .05, my)], (150, 60, 60), 4)
    else: cv.arc((cx - w * .08, my - h * .03, cx + w * .08, my + h * .04), 20, 160, (150, 60, 60), 4)
    return cv.out()


@lru_cache(None)
def store_img(w=900, h=700):
    cv = Cv(w, h)
    cv.shadow((w * .02, h * .93, w * .98, h * 1.08), 14, 130)
    cv.grad((w * .06, h * .26, w * .94, h * .96), (236, 242, 250), (196, 210, 230), r=10)
    cv.rect((w * .04, h * .06, w * .96, h * .26), fill=(30, 96, 160), r=10)
    cv.text(w * .5, h * .105, "NHÀ THUỐC", h * .1, WHITE, anchor="m")
    cx = w * .12; cv.ell((cx - h * .06, h * .1, cx + h * .06, h * .22), fill=GREEN); cv.rect((cx - h * .04, h * .15, cx + h * .04, h * .17), fill=WHITE); cv.rect((cx - h * .01, h * .12, cx + h * .01, h * .2), fill=WHITE)
    n = 10; aw = (w * .96 - w * .04) / n
    for i in range(n):
        cv.poly([(w * .04 + i * aw, h * .26), (w * .04 + (i + 1) * aw, h * .26), (w * .04 + (i + 1) * aw - 6, h * .38), (w * .04 + i * aw + 6, h * .38)], fill=GREEN if i % 2 == 0 else WHITE)
    cv.rect((w * .1, h * .46, w * .56, h * .9), fill=(90, 150, 210), r=8)
    for r_ in range(3):
        cv.rect((w * .12, h * .52 + r_ * h * .12, w * .54, h * .53 + r_ * h * .12 + 6), fill=(210, 226, 246))
        for c in range(6): cv.rect((w * (.14 + c * .065), h * (.43 + r_ * .12) + h * .04, w * (.14 + c * .065) + w * .04, h * (.52 + r_ * .12)), fill=[CYAN, ORANGE, GREEN][(c + r_) % 3], r=2)
    cv.rect((w * .64, h * .46, w * .88, h * .96), fill=(60, 80, 120), r=8); cv.rect((w * .66, h * .49, w * .86, h * .94), fill=(130, 184, 230), r=6); cv.ell((w * .83, h * .7, w * .85, h * .73), fill=YEL)
    return cv.out()


@lru_cache(None)
def shelf_img(w=900, h=900, rows=3):
    cv = Cv(w, h); cv.shadow((w * .02, h * .96, w * .98, h * 1.07), 12, 120)
    cv.rect((0, 0, w, h), fill=(40, 64, 104), r=18); cv.rect((w * .025, h * .02, w * .975, h * .98), fill=(24, 42, 76), r=12)
    for i in range(rows):
        y = h * (.02 + (i + 1) * .96 / rows) - 16
        cv.grad((w * .0, y, w, y + 20), (150, 176, 210), (96, 120, 156), r=4)
    return cv.out()


@lru_cache(None)
def cabinet_img(w=300, h=620):
    cv = Cv(w, h); cv.shadow((w * .05, h * .96, w * .95, h * 1.06), 10, 120)
    cv.grad((0, 0, w, h), (232, 238, 246), (186, 200, 220), r=14)
    for i in range(4):
        y = h * (.05 + i * .235); cv.rect((w * .08, y, w * .92, y + h * .2), fill=(250, 252, 255), outline=(150, 166, 194), width=3, r=8); cv.rect((w * .38, y + h * .085, w * .62, y + h * .105), fill=(120, 140, 170), r=5)
    return cv.out()


@lru_cache(None)
def carton_img(w=240, h=170, color=(214, 164, 100)):
    cv = Cv(w, h); d = w * .16; t = h * .16; fw = w - d
    cv.shadow((w * .04, h * .9, w * .96, h * 1.08), 7, 110)
    cv.poly([(0, t), (d, 0), (w, 0), (fw, t)], fill=shade(color, .12)); cv.poly([(fw, t), (w, 0), (w, h - t), (fw, h)], fill=shade(color, -.22))
    cv.grad((0, t, fw, h), color, shade(color, -.1), r=2); cv.rect((fw * .35, t, fw * .65, h), fill=shade(color, .25)); cv.rect((fw * .12, t + (h - t) * .5, fw * .3, t + (h - t) * .66), fill=WHITE, r=2)
    return cv.out()


@lru_cache(None)
def calendar_img(w=320, h=340, day="30", stamp="HSD", month="Tháng 11", color=RED):
    cv = Cv(w, h); cv.shadow((w * .05, h * .93, w * .95, h * 1.07), 9, 120)
    cv.grad((0, 0, w, h * .95), WHITE, (226, 234, 246), r=20); cv.grad((0, 0, w, h * .26), shade(color, .05), shade(color, -.2), r=20)
    cv.text(w / 2, h * .075, month, h * .12, WHITE, anchor="m")
    for x in (w * .12, w * .88): cv.rect((x - 7, -4, x + 7, h * .06), fill=(60, 70, 94), r=6)
    cv.text(w / 2, h * .3, day, h * .42, INK, anchor="m")
    if stamp:
        cv.rect((w * .12, h * .72, w * .88, h * .9), outline=color, width=6, r=10); cv.text(w / 2, h * .735, stamp, h * .13, color, anchor="m")
    return cv.out()


@lru_cache(None)
def clock_img(r=120, hh=9, mm=0, color=CYAN):
    cv = Cv(r * 2 + 16, r * 2 + 24); c = r + 8
    cv.shadow((10, r * 2 - 4, r * 2 + 6, r * 2 + 18), 8, 110)
    cv.ell((c - r, c - r, c + r, c + r), fill=(238, 244, 252), outline=color, width=r * .09)
    for k in range(12):
        a = k * math.pi / 6; cv.line([(c + r * .8 * math.sin(a), c - r * .8 * math.cos(a)), (c + r * .92 * math.sin(a), c - r * .92 * math.cos(a))], (90, 108, 140), 4)
    ah = ((hh % 12) + mm / 60) * math.pi / 6; am = mm * math.pi / 30
    cv.line([(c, c), (c + r * .5 * math.sin(ah), c - r * .5 * math.cos(ah))], INK, r * .09); cv.line([(c, c), (c + r * .78 * math.sin(am), c - r * .78 * math.cos(am))], color, r * .06)
    cv.ell((c - 8, c - 8, c + 8, c + 8), fill=INK)
    return cv.out()


@lru_cache(None)
def lock_img(s=140, color=RED):
    cv = Cv(s, s * 1.2)
    cv.shadow((s * .1, s * 1.1, s * .9, s * 1.2), 6, 100)
    cv.arc((s * .22, s * .02, s * .78, s * .7), 180, 360, shade(color, .3), s * .1)
    cv.line([(s * .24, s * .36), (s * .24, s * .52)], shade(color, .3), s * .1); cv.line([(s * .76, s * .36), (s * .76, s * .52)], shade(color, .3), s * .1)
    cv.grad((s * .1, s * .48, s * .9, s * 1.12), shade(color, .1), shade(color, -.25), r=s * .1)
    cv.ell((s * .44, s * .68, s * .56, s * .8), fill=(70, 20, 30)); cv.rect((s * .475, s * .76, s * .525, s * .94), fill=(70, 20, 30))
    return cv.out()


@lru_cache(None)
def mark_img(kind="check", s=100, color=GREEN):
    cv = Cv(s, s)
    cv.ell((2, 2, s - 2, s - 2), fill=color)
    if kind == "check": cv.line([(s * .26, s * .52), (s * .43, s * .68), (s * .74, s * .32)], WHITE, s * .12)
    elif kind == "cross": cv.line([(s * .3, s * .3), (s * .7, s * .7)], WHITE, s * .12); cv.line([(s * .3, s * .7), (s * .7, s * .3)], WHITE, s * .12)
    else: cv.text(s / 2, s * .12, "?", s * .7, INK if color == YEL else WHITE, anchor="m")
    return cv.out()


@lru_cache(None)
def arrow_img(length=200, thick=26, color=CYAN, vertical=False):
    L = int(length); T = int(thick * 2.2)
    cv = Cv(L if not vertical else T, T if not vertical else L)
    if not vertical:
        cv.rect((0, T / 2 - thick / 2, L - T * .5, T / 2 + thick / 2), fill=color, r=thick / 2); cv.poly([(L - T * .7, 0), (L, T / 2), (L - T * .7, T)], fill=color)
    else:
        cv.rect((T / 2 - thick / 2, 0, T / 2 + thick / 2, L - T * .5), fill=color, r=thick / 2); cv.poly([(0, L - T * .7), (T / 2, L), (T, L - T * .7)], fill=color)
    return cv.out()


@lru_cache(None)
def card_img(w, h, color=CYAN, fill=(18, 40, 82)):
    cv = Cv(w, h); cv.shadow((w * .04, h * .88, w * .96, h * 1.08), 12, 120)
    cv.grad((0, 0, w, h * .96), shade(fill, .1), fill, r=26); cv.rect((0, 0, w, h * .96), outline=color, width=4, r=26)
    return cv.out()


@lru_cache(None)
def phone_img(w=520, h=900):
    cv = Cv(w, h); cv.shadow((w * .05, h * .95, w * .95, h * 1.05), 14, 130)
    cv.grad((0, 0, w, h * .97), (60, 72, 100), (24, 32, 56), r=46); cv.rect((w * .04, h * .025, w * .96, h * .94), fill=(12, 26, 54), r=36)
    cv.rect((w * .38, h * .03, w * .62, h * .045), fill=(60, 72, 100), r=8)
    return cv.out()


@lru_cache(None)
def receipt_img(w=420, h=640):
    cv = Cv(w, h); cv.shadow((w * .05, h * .95, w * .95, h * 1.05), 10, 110)
    n = 14; pts = [(0, 0), (w, 0)]
    for i in range(n + 1): pts.append((w - i * w / n, h * .96 + (h * .035 if i % 2 else 0)))
    cv.poly(pts, fill=(248, 250, 254))
    cv.text(w / 2, h * .04, "HÓA ĐƠN", h * .07, INK, anchor="m"); cv.line([(w * .08, h * .14), (w * .92, h * .14)], (170, 180, 200), 3)
    for i, (a, b) in enumerate((("Thuốc A  x1", "45.000"), ("Thuốc B  x2", "78.000"), ("Vitamin C", "32.000"))):
        y = h * (.2 + i * .12); cv.text(w * .08, y, a, h * .055, INK, bold=False); cv.text(w * .92, y, b, h * .055, INK, bold=False, anchor="r")
    cv.line([(w * .08, h * .6), (w * .92, h * .6)], (170, 180, 200), 3); cv.text(w * .08, h * .66, "Tổng", h * .07, INK); cv.text(w * .92, h * .66, "155.000", h * .07, GREEN, anchor="r")
    return cv.out()


@lru_cache(None)
def scale_img(w=900, h=560, tilt=0.0):
    cv = Cv(w, h); cx = w / 2
    cv.grad((cx - 16, h * .2, cx + 16, h * .92), (150, 170, 200), (96, 116, 150), r=8); cv.grad((cx - w * .16, h * .9, cx + w * .16, h * .98), (150, 170, 200), (96, 116, 150), r=10)
    cv.ell((cx - 26, h * .14, cx + 26, h * .14 + 52), fill=YEL)
    a = math.radians(tilt); dx = w * .4; dy = math.sin(a) * dx
    L = (cx - math.cos(a) * dx, h * .17 - dy); R = (cx + math.cos(a) * dx, h * .17 + dy)
    cv.line([L, R], (190, 206, 230), 14)
    for (x, y) in (L, R):
        cv.line([(x, y), (x - w * .14, y + h * .26)], (150, 170, 200), 5); cv.line([(x, y), (x + w * .14, y + h * .26)], (150, 170, 200), 5)
        cv.grad((x - w * .17, y + h * .25, x + w * .17, y + h * .29), (190, 206, 230), (130, 150, 184), r=10)
    return cv.out(), L, R


@lru_cache(None)
def mag_img(s=260):
    cv = Cv(s, s)
    cv.line([(s * .62, s * .62), (s * .94, s * .94)], (190, 150, 90), s * .12); cv.ell((s * .04, s * .04, s * .7, s * .7), fill=(160, 224, 255, 90), outline=(220, 232, 248), width=s * .06)
    cv.arc((s * .14, s * .14, s * .58, s * .58), 200, 280, (255, 255, 255, 200), s * .03)
    return cv.out()


@lru_cache(None)
def bubble_img(w=860, h=360, tail="bl"):
    cv = Cv(w, h); cv.shadow((w * .06, h * .84, w * .94, h * 1.06), 12, 110)
    cv.rect((0, 0, w, h * .88), fill=(250, 252, 255), r=40)
    cv.poly([(w * .12, h * .86), (w * .06, h), (w * .26, h * .86)], fill=(250, 252, 255))
    return cv.out()


@lru_cache(None)
def report_img(w=300, h=380, color=CYAN):
    cv = Cv(w, h); cv.shadow((w * .06, h * .93, w * .94, h * 1.06), 9, 110)
    cv.grad((0, 0, w, h * .95), WHITE, (222, 232, 246), r=14); cv.rect((w * .1, h * .08, w * .6, h * .14), fill=color, r=4)
    for i in range(3): cv.rect((w * .1, h * (.22 + i * .08), w * .9, h * (.22 + i * .08) + 6), fill=(190, 200, 216), r=3)
    for i, v in enumerate((.3, .55, .4, .7)): cv.rect((w * (.14 + i * .2), h * (.8 - v * .38), w * (.14 + i * .2) + w * .14, h * .84), fill=shade(color, -.1 * i % .4), r=3)
    return cv.out()


@lru_cache(None)
def book_img(w=300, h=380):
    cv = Cv(w, h); cv.shadow((w * .06, h * .93, w * .94, h * 1.06), 9, 110)
    cv.grad((0, 0, w, h * .95), (196, 140, 84), (140, 92, 52), r=14); cv.rect((w * .1, h * .08, w * .92, h * .9), fill=(250, 244, 230), r=8)
    for i in range(6): cv.rect((w * .2, h * (.2 + i * .1), w * .82, h * (.2 + i * .1) + 5), fill=(160, 150, 134), r=3)
    cv.rect((0, 0, w * .1, h * .95), fill=(120, 76, 42), r=10)
    return cv.out()


@lru_cache(None)
def brain_img(w=300, h=300):
    cv = Cv(w, h); cv.shadow((w * .1, h * .9, w * .9, h * 1.06), 9, 100)
    cv.ell((w * .12, h * .22, w * .88, h * .86), fill=(250, 252, 255))
    for (a, b, c, d) in ((.05, .34, .44, .7), (.5, .14, .92, .6), (.3, .06, .7, .44)): cv.ell((w * a, h * b, w * c, h * d), fill=(250, 252, 255))
    cv.text(w / 2, h * .24, "?", h * .5, PURPLE, anchor="m")
    return cv.out()


@lru_cache(None)
def megaphone_img(s=240):
    cv = Cv(s, s); cv.poly([(s * .1, s * .38), (s * .62, s * .1), (s * .62, s * .9), (s * .1, s * .62)], fill=ORANGE)
    cv.rect((s * .02, s * .38, s * .22, s * .62), fill=shade(ORANGE, -.2), r=6)
    for k in (-1, 0, 1): cv.line([(s * .74, s * .5 + k * s * .14), (s * .94, s * .5 + k * s * .24)], YEL, 8)
    return cv.out()


@lru_cache(None)
def tag_img(s=240, txt="-30%", color=ORANGE):
    cv = Cv(s, s * .7); cv.poly([(s * .02, s * .35), (s * .28, s * .02), (s * .98, s * .02), (s * .98, s * .68), (s * .28, s * .68)], fill=color)
    cv.ell((s * .18, s * .3, s * .28, s * .4), fill=NAVY); cv.text(s * .62, s * (.14 if len(txt) <= 4 else .2), txt, s * (.24 if len(txt) <= 4 else .15), WHITE, anchor="m")
    return cv.out()


@lru_cache(None)
def cracked_coin_img(r=70):
    cv = Cv(r * 2 + 20, r * 2 + 24); cv.paste(coin_img(r), 4, 4)
    cv.line([(r * .9 + 10, 8), (r * 1.1 + 10, r * .7), (r * .8 + 10, r * 1.1), (r * 1.1 + 10, r * 1.6), (r * 1.0 + 10, r * 2)], NAVY, 7)
    return cv.out()
