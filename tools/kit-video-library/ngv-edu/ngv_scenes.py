# -*- coding: utf-8 -*-
"""NGV – bộ cảnh 2D cho kênh Người Giữ Vốn.

Nhân vật đại diện (không vẽ theo ảnh thật), có mặt và biểu cảm; 4 bối cảnh
(nhà ban đêm, văn phòng ban ngày, phòng ngủ, cận màn hình/điện thoại).
Giao diện giao dịch tự thiết kế: không tên sàn, không logo; số liệu minh hoạ
theo đúng hình dạng thị trường, chỉ các nhãn đọc ra trong lời thoại là số chính xác.

API chính:
    shot(kind, t, dur, cfg) -> PIL.Image RGB 1920x1080
"""
import functools
import math
import os
import random
from datetime import date, timedelta

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

SS = 2
W, H = 1920, 1080
STEP = 1 / 12  # nhân vật chuyển động kiểu cắt giấy

# ---------------------------------------------------------------- phông chữ
_FONT_DIRS = ["/usr/share/fonts/opentype/inter/", os.path.expanduser("~/.fonts/"), "C:/Windows/Fonts/"]


@functools.lru_cache(None)
def font(size, w="SemiBold", disp=False):
    names = [("InterDisplay-" if disp else "Inter-") + w + ".otf", "Inter-" + w + ".otf",
             "segoeuib.ttf" if w in ("Bold", "SemiBold", "ExtraBold") else "segoeui.ttf", "arial.ttf"]
    for d in _FONT_DIRS:
        for n in names:
            p = os.path.join(d, n)
            if os.path.exists(p):
                return ImageFont.truetype(p, max(6, int(size * SS)))
    return ImageFont.load_default()


# ---------------------------------------------------------------- màu
SKIN = (228, 182, 148)
SKIN_S = (204, 156, 124)
SKIN_D = (178, 130, 102)
HAIR = (36, 30, 32)
EYE = (34, 28, 30)
LIP = (150, 84, 80)
TEE = (62, 84, 120)
TEE_D = (48, 66, 96)
SHIRT = (236, 240, 247)
SHIRT_D = (204, 212, 226)
LEATHER = (118, 70, 40)
STEEL = (214, 220, 228)
UP = (46, 204, 138)
DN = (240, 84, 100)
GOLD = (232, 184, 92)


def clamp(v, a=0.0, b=1.0):
    return a if v < a else b if v > b else v


def ease(p):
    p = clamp(p)
    return p * p * (3 - 2 * p)


def seg(t, a, b):
    return ease((t - a) / max(1e-6, b - a))


def lerp(a, b, p):
    return a + (b - a) * p


def mix(c0, c1, p):
    return tuple(int(lerp(c0[i], c1[i], p)) for i in range(3))


# ---------------------------------------------------------------- bút vẽ (toạ độ ảo 1920x1080, vẽ ở SS)
class Pen:
    def __init__(self, img):
        self.im = img
        self.d = ImageDraw.Draw(img, "RGBA")

    def rect(self, x0, y0, x1, y1, fill, r=0, outline=None, width=1):
        x0, x1 = min(x0, x1), max(x0, x1)
        y0, y1 = min(y0, y1), max(y0, y1)
        r = min(r, (x1 - x0) / 2, (y1 - y0) / 2)
        self.d.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS, y1 * SS], int(r * SS), fill=fill,
                                 outline=outline, width=max(1, int(width * SS)))

    def ell(self, cx, cy, rx, ry, fill, outline=None, width=1):
        self.d.ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS], fill=fill,
                       outline=outline, width=max(1, int(width * SS)))

    def poly(self, pts, fill):
        self.d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill)

    def line(self, pts, fill, w=2):
        self.d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=max(1, int(w * SS)), joint="curve")

    def cap(self, a, b, r, fill):
        self.line([a, b], fill, 2 * r)
        self.ell(a[0], a[1], r, r, fill)
        self.ell(b[0], b[1], r, r, fill)

    def chord(self, box, a0, a1, fill):
        self.d.chord([v * SS for v in box], a0, a1, fill=fill)

    def text(self, x, y, s, size, fill, w="SemiBold", anchor="la", disp=False):
        f = font(size, w, disp)
        if len(fill) == 4 and fill[3] < 255:
            if fill[3] <= 0 or not s:
                return
            bb = self.d.textbbox((x * SS, y * SS), s, font=f, anchor=anchor)
            bw, bh = int(bb[2] - bb[0]) + 2, int(bb[3] - bb[1]) + 2
            if bw <= 0 or bh <= 0:
                return
            m = Image.new("L", (bw, bh), 0)
            ImageDraw.Draw(m).text((x * SS - bb[0], y * SS - bb[1]), s, font=f, fill=int(fill[3]), anchor=anchor)
            self.im.paste(tuple(fill[:3]), (int(bb[0]), int(bb[1]), int(bb[0]) + bw, int(bb[1]) + bh), m)
            return
        self.d.text((x * SS, y * SS), s, font=f, fill=fill, anchor=anchor)

    def tw(self, s, size, w="SemiBold", disp=False):
        return self.d.textlength(s, font=font(size, w, disp)) / SS


def canvas(color=(0, 0, 0)):
    return Image.new("RGB", (W * SS, H * SS), tuple(color))


def down(img):
    return img.convert("RGB").resize((W, H), Image.LANCZOS)


@functools.lru_cache(None)
def _vgrad(w, h, c0, c1):
    a = np.linspace(0, 1, h)[:, None, None]
    arr = np.array(c0, float)[None, None, :] * (1 - a) + np.array(c1, float)[None, None, :] * a
    arr = np.repeat(arr, w, axis=1)
    return Image.fromarray(arr.astype(np.uint8), "RGB")


def vgrad(pen, x0, y0, x1, y1, c0, c1):
    g = _vgrad(int((x1 - x0) * SS), int((y1 - y0) * SS), tuple(c0), tuple(c1))
    pen.im.paste(g, (int(x0 * SS), int(y0 * SS)))


@functools.lru_cache(64)
def _glow(cx, cy, r, color, strength):
    """Ánh sáng màu (RGB đã nhân alpha) ở độ phân giải 1x."""
    sw, sh = W // 4, H // 4
    y, x = np.ogrid[0:sh, 0:sw]
    d = np.sqrt((x - cx / 4) ** 2 + (y - cy / 4) ** 2) / (r / 4)
    a = np.clip(1 - d, 0, 1) ** 1.8 * strength
    arr = (np.array(color, float)[None, None, :] * a[..., None]).astype(np.uint8)
    return Image.fromarray(arr, "RGB").resize((W, H), Image.BILINEAR)


def light(img, cx, cy, r, color, strength):
    if strength <= 0.01:
        return img
    return ImageChops.screen(img, _glow(int(cx), int(cy), int(r), tuple(color), round(strength, 2)))


@functools.lru_cache(4)
def _vignette(strength):
    y, x = np.ogrid[0:H // 4, 0:W // 4]
    d = np.sqrt(((x - W / 8) / (W / 8)) ** 2 + ((y - H / 8) / (H / 8)) ** 2)
    v = 1 - np.clip(d - 0.55, 0, 1) * strength
    arr = (np.clip(v, 0, 1) * 255).astype(np.uint8)
    return Image.fromarray(arr, "L").resize((W, H), Image.BILINEAR).convert("RGB")


def vignette(img, strength=0.55):
    return ImageChops.multiply(img, _vignette(strength))


# ---------------------------------------------------------------- nhân vật (chính diện)
EXPR = {
    #          lông mày (trong, ngoài)  mắt     miệng
    "neutral": ((0, 0), "open", "flat"),
    "focus": ((5, -3), "open", "tight"),
    "smile": ((-5, -4), "open", "smile"),
    "grin": ((-7, -6), "happy", "grin"),
    "smug": ((-2, -7), "half", "smile"),
    "worried": ((-10, 5), "open", "frown"),
    "shock": ((-14, -9), "wide", "open"),
    "sad": ((-9, 6), "half", "frown"),
    "pained": ((-9, 7), "closed", "frown"),
    "blank": ((-3, 3), "open", "flat"),
}


def _blink(t, seed=0):
    ph = (t + seed * 0.7) % 3.7
    return ph < 0.12


def head(p, hx, hy, expr="neutral", t=0.0, gaze=(0, 0), blink=True, sleeve=None):
    (bi, bo), eyes, mouth = EXPR.get(expr, EXPR["neutral"])
    if blink and eyes in ("open", "wide") and _blink(t):
        eyes = "closed"
    # tai
    for s in (-1, 1):
        p.ell(hx + s * 97, hy + 16, 15, 24, SKIN_S)
        p.ell(hx + s * 99, hy + 16, 7, 13, SKIN_D)
    # tóc sau
    p.ell(hx, hy - 34, 110, 96, HAIR)
    # mặt
    p.ell(hx, hy + 8, 97, 112, SKIN)
    p.chord((hx - 97, hy - 104, hx + 97, hy + 120), 20, 160, SKIN)
    # đổ bóng má phải
    p.chord((hx + 30, hy - 60, hx + 100, hy + 112), 280, 80, SKIN_S + (150,))
    # tóc mái
    p.poly([(hx - 108, hy - 6), (hx - 108, hy - 70), (hx - 70, hy - 118), (hx - 10, hy - 134), (hx + 56, hy - 124),
            (hx + 100, hy - 92), (hx + 110, hy - 40), (hx + 104, hy - 2), (hx + 92, hy - 46), (hx + 60, hy - 62),
            (hx + 10, hy - 64), (hx - 34, hy - 56), (hx - 76, hy - 44), (hx - 96, hy - 22)], HAIR)
    p.poly([(hx - 20, hy - 66), (hx + 40, hy - 120), (hx + 62, hy - 64)], HAIR)
    gx, gy = gaze
    for s in (-1, 1):
        ex, ey = hx + s * 38 + gx, hy + 10 + gy
        if eyes == "closed":
            p.line([(ex - 12, ey - 1), (ex, ey + 4), (ex + 12, ey - 1)], EYE, 3.5)
        elif eyes == "happy":
            p.line([(ex - 12, ey + 3), (ex, ey - 5), (ex + 12, ey + 3)], EYE, 4)
        else:
            rx, ry = (11, 15) if eyes == "wide" else (9, 12)
            if eyes == "wide":
                p.ell(ex, ey, rx + 5, ry + 5, (250, 246, 240))
            p.ell(ex, ey, rx, ry, EYE)
            p.ell(ex - 3, ey - 4, 3, 3, (255, 255, 255))
            if eyes == "half":
                p.rect(ex - 14, ey - 18, ex + 14, ey - 1, SKIN)
                p.line([(ex - 12, ey - 1), (ex + 12, ey - 1)], SKIN_D, 2.5)
        # lông mày
        inner = (hx + s * 20, hy - 26 + bi)
        outer = (hx + s * 60, hy - 26 + bo)
        p.line([inner, outer], HAIR, 7)
    # mũi
    p.line([(hx + 4, hy + 30), (hx + 10, hy + 47), (hx - 2, hy + 51)], SKIN_D, 3)
    # miệng
    my = hy + 74
    if mouth == "flat":
        p.line([(hx - 15, my), (hx + 15, my)], LIP, 4)
    elif mouth == "tight":
        p.line([(hx - 9, my), (hx + 9, my)], LIP, 4)
    elif mouth == "smile":
        p.line([(hx - 22, my - 6), (hx, my + 5), (hx + 22, my - 6)], LIP, 4)
    elif mouth == "grin":
        p.chord((hx - 26, my - 18, hx + 26, my + 20), 0, 180, (96, 38, 40))
        p.rect(hx - 18, my - 1, hx + 18, my + 6, (250, 246, 240), 3)
    elif mouth == "frown":
        p.line([(hx - 18, my + 5), (hx, my - 4), (hx + 18, my + 5)], LIP, 4)
    elif mouth == "open":
        p.ell(hx, my + 4, 11, 14, (96, 38, 40))


def limb(p, a, b, ra, rb, col):
    dx, dy = b[0] - a[0], b[1] - a[1]
    L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L, dx / L
    p.poly([(a[0] + nx * ra, a[1] + ny * ra), (b[0] + nx * rb, b[1] + ny * rb), (b[0] - nx * rb, b[1] - ny * rb),
            (a[0] - nx * ra, a[1] - ny * ra)], col)
    p.ell(a[0], a[1], ra, ra, col)
    p.ell(b[0], b[1], rb, rb, col)


def hand(p, hd, el, side, grip=False):
    dx, dy = hd[0] - el[0], hd[1] - el[1]
    L = math.hypot(dx, dy) or 1
    ux, uy = dx / L, dy / L
    nx, ny = -uy * side, ux * side
    cx_, cy_ = hd[0] + ux * 8, hd[1] + uy * 8
    p.ell(cx_, cy_, 25, 25, SKIN)
    if not grip:
        for k in (-1, 0, 1):
            fx, fy = cx_ + ux * 22 + nx * k * 12, cy_ + uy * 22 + ny * k * 12
            p.ell(fx, fy, 9, 9, SKIN)
    p.cap((cx_ + nx * 16, cy_ + ny * 16), (cx_ + nx * 26 + ux * 16, cy_ + ny * 26 + uy * 16), 9, SKIN_S)
    p.line([(cx_ - ux * 22 + nx * 22, cy_ - uy * 22 + ny * 22), (cx_ - ux * 22 - nx * 22, cy_ - uy * 22 - ny * 22)],
           SKIN_S + (120,), 2)


# tư thế tay chính diện: (khuỷu, bàn tay) cho tay trái màn hình (L) và phải màn hình (R), toạ độ tương đối (cx, sy)
def _arms(pose, t, cx, sy, hy):
    jit = math.sin(t * 31) * 6 if pose == "type" else 0
    jit2 = math.sin(t * 27 + 1.3) * 6 if pose == "type" else 0
    A = {
        "type": (((cx - 262, sy + 200), (cx - 170, sy + 262 + jit)), ((cx + 262, sy + 200), (cx + 170, sy + 262 + jit2))),
        "rest": (((cx - 262, sy + 205), (cx - 215, sy + 280)), ((cx + 262, sy + 205), (cx + 215, sy + 280))),
        "phone": (((cx - 196, sy + 236), (cx - 58, sy + 150)), ((cx + 196, sy + 236), (cx + 58, sy + 160))),
        "phone_low": (((cx - 210, sy + 230), (cx - 60, sy + 215)), ((cx + 230, sy + 230), (cx + 60, sy + 225))),
        "head_hands": (((cx - 228, sy + 168), (cx - 98, hy + 40)), ((cx + 228, sy + 168), (cx + 98, hy + 40))),
        "chin": (((cx - 70, sy + 250), (cx - 8, hy + 132)), ((cx + 262, sy + 205), (cx + 215, sy + 280))),
    }
    return A.get(pose, A["rest"])


def character_front(p, cx, sy, pose="rest", expr="neutral", t=0.0, outfit="tee", gaze=(0, 0), desk=None,
                    lid=False, phone_glow=(120, 170, 255)):
    tq = math.floor(t / STEP) * STEP
    breathe = math.sin(tq * 1.7) * 3
    hdy = {"head_hands": 26, "phone_low": 12, "phone": 6}.get(pose, 0)
    hx = cx + (math.sin(tq * 0.9) * 3 if pose != "head_hands" else 0)
    hy = sy - 186 + hdy + breathe * 0.5
    shirt, shirt_d = (TEE, TEE_D) if outfit == "tee" else (SHIRT, SHIRT_D)
    # cổ
    p.rect(cx - 34, hy + 70, cx + 34, sy + 30, SKIN_S, 10)
    # thân
    top = sy + breathe
    p.poly([(cx - 212, top + 34), (cx - 150, top - 6), (cx + 150, top - 6), (cx + 212, top + 34),
            (cx + 236, H + 20), (cx - 236, H + 20)], shirt)
    for s in (-1, 1):
        p.ell(cx + s * 182, top + 44, 56, 54, shirt)
    p.poly([(cx + 60, top + 60), (cx + 236, top + 60), (cx + 236, H + 20), (cx + 90, H + 20)], shirt_d + (90,))
    if outfit == "tee":
        p.chord((cx - 52, top - 40, cx + 52, top + 40), 0, 180, SKIN_S)
        p.line([(cx - 52, top), (cx, top + 40), (cx + 52, top)], shirt_d, 5)
    else:
        p.poly([(cx - 40, top - 10), (cx, top + 34), (cx + 40, top - 10), (cx + 30, top - 20), (cx - 30, top - 20)], SKIN_S)
        p.poly([(cx - 44, top - 14), (cx - 6, top + 40), (cx - 30, top + 52), (cx - 62, top + 4)], SHIRT_D)
        p.poly([(cx + 44, top - 14), (cx + 6, top + 40), (cx + 30, top + 52), (cx + 62, top + 4)], SHIRT_D)
        for k in range(3):
            p.ell(cx, top + 80 + k * 70, 5, 5, SHIRT_D)
    head(p, hx, hy, expr, tq, gaze)
    if desk:
        desk(p)
    def _lid():
        p.rect(cx - 270, sy + 66, cx + 270, sy + 292, (44, 48, 58), 16)
        p.rect(cx - 260, sy + 76, cx + 260, sy + 282, (56, 60, 72), 12)
        p.rect(cx - 290, sy + 284, cx + 290, sy + 302, (70, 74, 86), 6)
    if lid and pose in ("phone", "head_hands", "chin"):
        _lid()
    (le, lh), (re, rh) = _arms(pose, tq, cx, top, hy)
    if pose == "phone":
        p.rect(cx - 62, top + 50, cx + 62, top + 236, (26, 28, 36), 16)
        p.rect(cx - 54, top + 58, cx + 54, top + 228, (44, 48, 60), 12)
        p.ell(cx - 30, top + 84, 9, 9, (20, 22, 28))
    sleeve_c = shirt
    arm_c = SKIN if outfit == "tee" else shirt
    phone_hand = None
    for side, (el, hd) in ((-1, (le, lh)), (1, (re, rh))):
        sh = (cx + side * 182, top + 52)
        if outfit == "tee":
            mid = (lerp(sh[0], el[0], .5), lerp(sh[1], el[1], .5))
            limb(p, sh, el, 36, 28, SKIN)
            limb(p, sh, mid, 44, 40, sleeve_c)
            p.line([(mid[0] - 30, mid[1] + 4), (mid[0] + 30, mid[1] + 4)], shirt_d, 4)
            limb(p, el, hd, 28, 22, SKIN)
        else:
            limb(p, sh, el, 42, 34, shirt)
            limb(p, el, hd, 34, 26, shirt)
            p.ell(lerp(el[0], hd[0], .86), lerp(el[1], hd[1], .86), 27, 27, shirt_d)
        if side == -1 and pose != "head_hands":
            # đồng hồ dây da, không logo
            wx, wy = lerp(el[0], hd[0], 0.78), lerp(el[1], hd[1], 0.78)
            p.ell(wx, wy, 17, 17, LEATHER)
            p.ell(wx, wy, 12, 12, STEEL)
            p.ell(wx, wy, 9, 9, (246, 244, 236))
        if pose != "head_hands":
            hand(p, hd, el, side, grip=pose in ("phone", "phone_low"))
        if pose in ("phone_low",) and side == -1:
            phone_hand = hd
    if pose == "head_hands":
        for s in (-1, 1):
            p.ell(cx + s * 96, hy + 18, 32, 40, SKIN)
            for k in range(4):
                fx = cx + s * (100 - k * 13)
                p.cap((fx, hy + 6), (fx - s * 4, hy - 46 - k * 6), 10, SKIN if k % 2 == 0 else (220, 174, 140))
            p.ell(cx + s * 70, hy + 40, 12, 18, SKIN_S)
    if phone_hand:
        x, y = phone_hand
        if True:
            p.rect(x - 70, y - 26, x + 140, y + 16, (28, 30, 38), 12)
            p.ell(x - 4, y, 24, 20, SKIN)
            p.ell(x + 120, y + 4, 24, 20, SKIN)
    if lid and pose not in ("phone", "head_hands", "chin"):
        _lid()
    return hx, hy


# ---------------------------------------------------------------- bối cảnh tĩnh (cache)
def _rng(seed):
    return random.Random(seed)


@functools.lru_cache(None)
def bg_home_night():
    img = canvas()
    p = Pen(img)
    vgrad(p, 0, 0, W, H, (26, 32, 54), (18, 22, 38))
    # cửa sổ thành phố
    p.rect(110, 110, 640, 640, (40, 46, 66), 10)
    vgrad(p, 126, 126, 624, 624, (8, 14, 34), (24, 30, 62))
    p.ell(530, 210, 34, 34, (240, 236, 214))
    p.ell(544, 202, 30, 30, (12, 18, 38))
    r = _rng(3)
    x = 130
    while x < 620:
        bw = r.randint(50, 90)
        bh = r.randint(160, 360)
        p.rect(x, 624 - bh, x + bw, 624, (16, 22, 40))
        for wy in range(int(624 - bh + 14), 610, 26):
            for wx in range(x + 8, x + bw - 12, 18):
                if r.random() < 0.32:
                    p.rect(wx, wy, wx + 8, wy + 12, (250, 210, 120) if r.random() < .7 else (180, 210, 250))
        x += bw + r.randint(4, 12)
    p.rect(110, 360, 640, 372, (40, 46, 66))
    p.rect(368, 110, 380, 640, (40, 46, 66))
    p.rect(60, 90, 160, 700, (44, 40, 70))  # rèm
    p.rect(590, 90, 690, 700, (44, 40, 70))
    for xx in (80, 112, 140, 612, 644, 672):
        p.line([(xx, 96), (xx, 696)], (36, 32, 58), 4)
    # kệ sách
    p.rect(1290, 250, 1780, 266, (70, 54, 46))
    r = _rng(7)
    x = 1310
    cols = [(150, 80, 70), (70, 110, 140), (190, 160, 90), (90, 120, 90), (120, 90, 140), (200, 200, 210)]
    while x < 1600:
        bw, bh = r.randint(20, 34), r.randint(80, 130)
        p.rect(x, 250 - bh, x + bw, 250, mix(r.choice(cols), (26, 32, 54), .45), 3)
        x += bw + 3
    p.rect(1640, 186, 1700, 250, (90, 70, 60), 8)
    for a in range(5):
        p.ell(1670 + math.cos(a * 1.3) * 34, 160 + math.sin(a * 1.3) * 18, 26, 14, (60, 104, 80))
    # khung tranh trừu tượng
    p.rect(1330, 330, 1620, 530, (52, 46, 44), 6)
    vgrad(p, 1346, 346, 1604, 514, (60, 74, 110), (110, 90, 100))
    p.poly([(1346, 514), (1430, 420), (1500, 480), (1560, 410), (1604, 470), (1604, 514)], (40, 52, 80))
    # cửa hé có ánh vàng (vợ ở phòng bên)
    p.rect(1790, 60, 1920, 800, (34, 30, 40))
    vgrad(p, 1820, 70, 1850, 800, (120, 90, 60), (70, 52, 40))
    return down(img)


@functools.lru_cache(None)
def bg_office_day():
    img = canvas()
    p = Pen(img)
    vgrad(p, 0, 0, W, H, (230, 228, 222), (214, 212, 206))
    # cửa kính lớn
    p.rect(0, 70, W, 600, (196, 200, 204))
    vgrad(p, 0, 80, W, 590, (176, 210, 238), (220, 232, 240))
    r = _rng(11)
    x = -20
    while x < W:
        bw, bh = r.randint(90, 180), r.randint(160, 380)
        p.rect(x, 590 - bh, x + bw, 590, (176, 190, 206))
        for wy in range(590 - bh + 18, 580, 30):
            p.rect(x + 10, wy, x + bw - 10, wy + 10, (196, 210, 224))
        x += bw + r.randint(10, 40)
    for xx in range(0, W, 320):
        p.rect(xx, 70, xx + 10, 600, (186, 190, 196))
    # đèn trần
    for xx in (300, 960, 1620):
        p.rect(xx - 160, 18, xx + 160, 34, (250, 250, 246), 6)
    # đồng nghiệp phía sau (mờ)
    back = canvas((0, 0, 0))
    back.putalpha(0)
    bp = Pen(back)
    for i, xx in enumerate((300, 720, 1300, 1660)):
        c = [(96, 120, 150), (150, 110, 100), (90, 130, 110), (130, 120, 150)][i]
        bp.rect(xx - 120, 500, xx + 120, 640, (60, 66, 80), 8)  # màn hình
        bp.rect(xx - 110, 510, xx + 110, 630, (120, 150, 190), 6)
        bp.ell(xx + 160, 470, 42, 48, (60, 50, 50))
        bp.ell(xx + 160, 480, 36, 40, (220, 180, 150))
        bp.rect(xx + 100, 520, xx + 220, 700, c, 40)
        bp.rect(xx - 220, 640, xx + 260, 660, (200, 190, 176))
    back = back.filter(ImageFilter.GaussianBlur(10 * SS))
    img.paste(back, (0, 0), back)
    # vách ngăn
    p.rect(0, 660, W, 700, (180, 186, 196))
    # cây
    p.rect(110, 560, 210, 700, (210, 200, 186), 10)
    for a in range(9):
        p.ell(160 + math.cos(a * .8) * 70, 470 + math.sin(a * 1.1) * 60, 44, 22, (80, 140, 100))
    return down(img)


@functools.lru_cache(None)
def bg_bedroom():
    img = canvas()
    p = Pen(img)
    vgrad(p, 0, 0, W, H, (24, 30, 52), (16, 20, 36))
    p.rect(300, 0, 1620, 130, (54, 40, 40), 12)  # đầu giường
    p.rect(300, 120, 1620, H, (34, 42, 66))
    p.rect(450, 150, 1020, 430, (64, 74, 104), 60)  # gối
    p.rect(1060, 150, 1600, 430, (64, 74, 104), 60)
    # ánh trăng chéo
    return down(img)


def home_desk(p):
    # bàn
    vgrad(p, 0, 806, W, H, (74, 54, 42), (52, 38, 30))
    p.rect(0, 796, W, 812, (96, 72, 56))
    # cốc và sổ
    p.rect(1380, 720, 1460, 806, (210, 206, 196), 12)
    p.rect(1452, 740, 1490, 780, (210, 206, 196), 14)
    p.rect(1462, 750, 1480, 770, (74, 54, 42), 8)
    p.poly([(330, 830), (620, 818), (650, 900), (350, 916)], (200, 190, 168))
    p.line([(352, 900), (640, 886)], (170, 160, 140), 3)


def office_desk(p):
    # bàn sáng
    vgrad(p, 0, 806, W, H, (206, 182, 150), (186, 160, 128))
    p.rect(0, 796, W, 812, (222, 200, 170))
    # màn hình (mặt sau) bên phải
    p.rect(1300, 410, 1760, 740, (62, 66, 78), 14)
    p.rect(1500, 740, 1560, 800, (80, 84, 96))
    p.rect(1440, 792, 1620, 808, (80, 84, 96), 4)
    # bàn phím, cốc, giấy
    p.rect(700, 834, 1180, 880, (236, 236, 236), 8)
    for ky in range(842, 876, 11):
        for kx in range(712, 1170, 26):
            p.rect(kx, ky, kx + 20, ky + 7, (210, 212, 216), 2)
    p.rect(1240, 730, 1310, 806, (70, 110, 140), 12)
    p.poly([(240, 840), (520, 826), (540, 900), (250, 914)], (250, 250, 246))


# ---------------------------------------------------------------- dữ liệu giá minh hoạ (hình dạng thị trường)
_ANCH = [((2013, 1, 1), 14), ((2013, 4, 10), 190), ((2013, 4, 20), 110), ((2013, 7, 5), 70), ((2013, 10, 1), 125),
         ((2013, 11, 10), 330), ((2013, 12, 4), 1130), ((2013, 12, 18), 600), ((2014, 1, 6), 950), ((2014, 2, 25), 520),
         ((2014, 4, 10), 380), ((2014, 6, 3), 660), ((2014, 8, 18), 460), ((2014, 10, 5), 320), ((2014, 11, 12), 420),
         ((2014, 12, 31), 310), ((2015, 1, 14), 172), ((2015, 3, 10), 280), ((2015, 7, 10), 290), ((2015, 11, 4), 400),
         ((2016, 1, 10), 440), ((2016, 6, 17), 740), ((2016, 8, 2), 560), ((2016, 12, 31), 960), ((2017, 3, 24), 950),
         ((2017, 5, 25), 2400), ((2017, 7, 16), 1900), ((2017, 9, 1), 4800), ((2017, 9, 15), 3200),
         ((2017, 11, 12), 6000), ((2017, 12, 17), 19800), ((2018, 2, 6), 6900), ((2018, 3, 5), 11400),
         ((2018, 4, 6), 6600), ((2018, 5, 5), 9800), ((2018, 6, 29), 5900), ((2018, 7, 24), 8300),
         ((2018, 9, 10), 6300), ((2018, 11, 14), 5600), ((2018, 11, 25), 3800), ((2018, 12, 15), 3240),
         ((2018, 12, 31), 3700), ((2019, 6, 26), 12900), ((2019, 12, 17), 6600), ((2020, 3, 12), 4900),
         ((2020, 7, 1), 9100), ((2020, 10, 20), 11900), ((2020, 12, 31), 29000), ((2021, 4, 14), 63500), ((2021, 7, 20), 29800),
         ((2021, 11, 10), 68800), ((2022, 1, 24), 34000), ((2022, 3, 28), 47500), ((2022, 6, 18), 18900), ((2022, 9, 10), 21500),
         ((2022, 11, 21), 15600), ((2022, 12, 31), 16500)]


@functools.lru_cache(None)
def series():
    """Nến 3 ngày (ngày, mở, cao, thấp, đóng) nội suy log + nhiễu."""
    pts = [(date(*d).toordinal(), math.log(v)) for d, v in _ANCH]
    r = random.Random(42)
    out = []
    d0, d1 = pts[0][0], pts[-1][0]
    prev = math.exp(pts[0][1])
    k = 0
    noise = 0.0
    for day in range(d0, d1 + 1, 3):
        while k + 1 < len(pts) and pts[k + 1][0] < day:
            k += 1
        a, b = pts[k], pts[min(k + 1, len(pts) - 1)]
        f = 0 if b[0] == a[0] else (day - a[0]) / (b[0] - a[0])
        noise = noise * 0.6 + r.gauss(0, 0.035)
        lv = lerp(a[1], b[1], f) + noise
        close = math.exp(lv)
        hi = max(prev, close) * math.exp(abs(r.gauss(0, 0.025)))
        lo = min(prev, close) * math.exp(-abs(r.gauss(0, 0.025)))
        out.append((date.fromordinal(day), prev, hi, lo, close))
        prev = close
    return out


def window(d0, d1):
    a, b = date(*d0), date(*d1)
    return [c for c in series() if a <= c[0] <= b]


def vnd_num(v, dec=0):
    s = f"{v:,.{dec}f}"
    return s.replace(",", "#").replace(".", ",").replace("#", ".")


def pct(v):
    s = f"{abs(v):.0f}%"
    return ("+" if v >= 0 else "−") + s


# ---------------------------------------------------------------- giao diện giao dịch (tự thiết kế)
UBG = (12, 16, 26)
UPAN = (19, 25, 39)
ULINE = (34, 42, 60)
UTXT = (226, 232, 242)
UMUT = (128, 140, 164)
BUY_BTN = (1385, 524, 1625, 578)
SELL_BTN = (1645, 524, 1880, 578)
LEV_KNOB_X = (1420, 1840)


def cursor(p, x, y, click=0.0):
    if click > 0:
        rr = 10 + 40 * click
        p.ell(x, y, rr, rr, None, outline=(255, 255, 255, int(200 * (1 - click))), width=3)
    p.poly([(x, y), (x, y + 34), (x + 9, y + 26), (x + 16, y + 40), (x + 22, y + 37), (x + 15, y + 23), (x + 26, y + 22)],
           (255, 255, 255))
    p.line([(x, y), (x, y + 34), (x + 9, y + 26), (x + 16, y + 40), (x + 22, y + 37), (x + 15, y + 23), (x + 26, y + 22),
            (x, y)], (20, 20, 20), 1.5)


def ui_trading(t, c):
    img = canvas(UBG)
    p = Pen(img)
    cs = window(*c["win"])
    n = len(cs)
    prog = c.get("prog", 1.0)
    shown = max(2, int(round(n * prog)))
    last = cs[shown - 1]
    price = last[4]
    first = cs[0][1]
    ref = cs[max(0, shown - 9)][4]
    chg = (price / ref - 1) * 100
    # thanh trên
    p.rect(0, 0, W, 92, UPAN)
    p.line([(0, 92), (W, 92)], ULINE, 2)
    p.ell(66, 46, 22, 22, GOLD)
    p.text(66, 46, "B", 24, (40, 30, 10), "Bold", "mm")
    p.text(104, 30, "Bitcoin / USD", 30, UTXT, "Bold")
    col = UP if chg >= 0 else DN
    p.text(400, 26, "$" + vnd_num(price, 0 if price > 100 else 2), 36, col, "Bold")
    p.text(400 + p.tw("$" + vnd_num(price, 0 if price > 100 else 2), 36, "Bold") + 20, 36, pct(chg), 26, col, "SemiBold")
    mode = c.get("mode", "spot")
    tabs = [("Giao ngay", "spot"), ("Hợp đồng tương lai", "fut")]
    tx = 860
    tab_sw = c.get("tab_sw", 1.0)
    for label, key in tabs:
        tw_ = p.tw(label, 26, "SemiBold")
        active = (key == mode)
        p.text(tx, 32, label, 26, UTXT if active else UMUT, "SemiBold")
        if active:
            p.rect(tx, 80, tx + tw_ * tab_sw, 86, GOLD, 3)
        tx += tw_ + 50
    dl = c.get("date_label") or f"Tháng {last[0].month:02d}/{last[0].year}"
    dw = p.tw(dl, 24, "SemiBold")
    p.rect(W - 60 - dw - 36, 24, W - 60, 70, (30, 38, 56), 23)
    p.text(W - 60 - dw - 18, 33, dl, 24, UTXT, "SemiBold")
    # biểu đồ
    x0, y0, x1, y1 = 40, 116, 1330, 600
    p.rect(x0, y0, x1, y1, UPAN, 14)
    px0, px1, py0, py1 = x0 + 24, x1 - 110, y0 + 60, y1 - 50
    p.text(x0 + 24, y0 + 18, "Biểu đồ giá · nến 3 ngày", 22, UMUT, "Medium")
    hi = max(cc[2] for cc in cs)
    lo = min(cc[3] for cc in cs)
    pad = (hi - lo) * 0.08
    hi, lo = hi + pad, max(0, lo - pad)

    def Y(v):
        return py1 - (v - lo) / (hi - lo) * (py1 - py0)

    for k in range(6):
        v = lo + (hi - lo) * k / 5
        yy = Y(v)
        p.line([(px0, yy), (px1, yy)], ULINE, 1.2)
        p.text(px1 + 14, yy - 12, vnd_num(v, 0 if hi > 100 else 1), 19, UMUT, "Medium")
    step = (px1 - px0) / n
    seen = set()
    for i, cc in enumerate(cs):
        key = (cc[0].year, cc[0].month)
        if cc[0].month in (1, 4, 7, 10) and key not in seen:
            seen.add(key)
            xx = px0 + i * step
            p.line([(xx, py1), (xx, py1 + 8)], ULINE, 1.5)
            p.text(xx, py1 + 16, f"{cc[0].month:02d}/{cc[0].year}", 18, UMUT, "Medium", "ma")
    cw = max(2.0, step * 0.62)
    for i in range(shown):
        d, o, h_, l_, cl = cs[i]
        xx = px0 + (i + 0.5) * step
        colc = UP if cl >= o else DN
        p.line([(xx, Y(h_)), (xx, Y(l_))], colc, max(1.2, cw * 0.18))
        top, bot = Y(max(o, cl)), Y(min(o, cl))
        p.rect(xx - cw / 2, top, xx + cw / 2, max(bot, top + 2), colc)
    # đường giá hiện tại
    yy = Y(price)
    xx = px0 + (shown - 0.5) * step
    for k in range(int(xx), int(px1), 16):
        p.line([(k, yy), (k + 8, yy)], col + (180,), 1.5)
    lab = vnd_num(price, 0 if price > 100 else 2)
    p.rect(px1 + 6, yy - 16, px1 + 18 + p.tw(lab, 19, "Bold"), yy + 16, col, 6)
    p.text(px1 + 12, yy - 12, lab, 19, (10, 14, 22), "Bold")
    # chú thích sụt giảm
    dd = c.get("dd")
    if dd and c.get("dd_a", 0) > 0:
        a = c["dd_a"]
        ip = max(range(shown), key=lambda i: cs[i][2])
        it = min(range(ip, shown), key=lambda i: cs[i][3]) if ip < shown - 1 else shown - 1
        xp, xt = px0 + (ip + .5) * step, px0 + (it + .5) * step
        yp, yt = Y(cs[ip][2]), Y(cs[it][3])
        xb = xt + 40
        p.line([(xp, yp), (xb + 10, yp)], (255, 255, 255, int(160 * a)), 2)
        p.line([(xt, yt), (xb + 10, yt)], (255, 255, 255, int(160 * a)), 2)
        p.line([(xb, yp), (xb, yp + (yt - yp) * a)], DN + (int(255 * a),), 5)
        p.poly([(xb - 12, yp + (yt - yp) * a - 14), (xb + 12, yp + (yt - yp) * a - 14), (xb, yp + (yt - yp) * a + 4)],
               DN + (int(255 * a),))
        bw_ = p.tw(dd, 54, "Bold", True) + 40
        bx, by = min(xb + 24, px1 - bw_), (yp + yt) / 2 - 40
        p.rect(bx, by, bx + bw_, by + 80, DN + (int(235 * a),), 16)
        p.text(bx + 20, by + 8, dd, 54, (255, 255, 255, int(255 * a)), "Bold", disp=True)
        if c.get("dd_note"):
            p.text(bx, by + 92, c["dd_note"], 20, (230, 230, 240, int(255 * a)), "Medium")
    # sổ lệnh
    rx0 = 1356
    p.rect(rx0, y0, 1890, y1, UPAN, 14)
    p.text(rx0 + 24, y0 + 18, "Sổ lệnh", 22, UMUT, "Medium")
    p.text(1866, y0 + 18, "Khối lượng", 20, UMUT, "Medium", "ra")
    r = random.Random(int(t * 7))
    tick = max(0.01, price * 0.0006)
    for k in range(4):
        pr = price + tick * (6 - k) * (1 + r.random() * .2)
        yy = y0 + 56 + k * 28
        w_ = r.random() * 220
        p.rect(1866 - w_, yy, 1866, yy + 24, DN + (34,))
        p.text(rx0 + 24, yy, vnd_num(pr, 0 if price > 100 else 2), 21, DN, "Medium")
        p.text(1856, yy, f"{r.random() * 3:.3f}".replace(".", ","), 21, UTXT, "Regular", "ra")
    yy = y0 + 56 + 4 * 28 + 2
    p.text(rx0 + 24, yy, vnd_num(price, 0 if price > 100 else 2), 30, col, "Bold")
    for k in range(4):
        pr = price - tick * (k + 1) * (1 + r.random() * .2)
        yy = y0 + 56 + 5 * 28 + 14 + k * 28
        w_ = r.random() * 220
        p.rect(1866 - w_, yy, 1866, yy + 24, UP + (34,))
        p.text(rx0 + 24, yy, vnd_num(pr, 0 if price > 100 else 2), 21, UP, "Medium")
        p.text(1856, yy, f"{r.random() * 3:.3f}".replace(".", ","), 21, UTXT, "Regular", "ra")
    # đòn bẩy
    if mode == "fut":
        lev = c.get("lev", 1)
        p.text(rx0 + 24, 452, "Đòn bẩy", 22, UMUT, "Medium")
        p.text(1866, 446, f"x{lev:.0f}", 30, GOLD, "Bold", "ra")
        a, b = LEV_KNOB_X
        p.rect(a, 492, b, 498, ULINE, 3)
        kx = a + (b - a) * (lev - 1) / 49
        p.rect(a, 492, kx, 498, GOLD, 3)
        p.ell(kx, 495, 11, 11, (255, 255, 255))
    p.rect(*BUY_BTN, UP, 10)
    p.text((BUY_BTN[0] + BUY_BTN[2]) / 2, (BUY_BTN[1] + BUY_BTN[3]) / 2, "Mua" if mode == "spot" else "Mua / Tăng", 24,
           (8, 30, 20), "Bold", "mm")
    p.rect(*SELL_BTN, DN, 10)
    p.text((SELL_BTN[0] + SELL_BTN[2]) / 2, (SELL_BTN[1] + SELL_BTN[3]) / 2, "Bán" if mode == "spot" else "Bán / Giảm", 24,
           (40, 10, 14), "Bold", "mm")
    # tài khoản
    by0 = 620
    p.rect(40, by0, 1890, 846, UPAN, 14)
    p.text(64, by0 + 20, "Tài khoản của tôi", 24, UTXT, "SemiBold")
    p.text(1866, by0 + 24, "Số liệu minh hoạ", 18, UMUT, "Medium", "ra")
    pnl = c.get("pnl", 0.0)
    if c.get("pnl_map"):  # lãi/lỗ đi cùng chiều giá đang hiển thị
        lo_c, hi_c = min(x[4] for x in cs), max(x[4] for x in cs)
        f = (price - lo_c) / max(1e-9, hi_c - lo_c)
        pnl = lerp(c["pnl_map"][0], c["pnl_map"][1], f)
    cols = [("Lãi / lỗ", pct(pnl), UP if pnl >= 0 else DN, 58),
            ("Số lần nạp vốn", str(c.get("deposits", 1)), UTXT, 58),
            ("Đòn bẩy", "Không dùng" if mode == "spot" else f"x{c.get('lev', 1):.0f}", GOLD if mode == "fut" else UTXT, 44),
            ("Trạng thái", c.get("status", "Đang giữ"), c.get("status_c", UTXT), 44)]
    for k, (lab, val, cc, sz) in enumerate(cols):
        xx = 64 + k * 460
        p.text(xx, by0 + 66, lab, 22, UMUT, "Medium")
        p.text(xx, by0 + 96, val, sz, cc, "Bold", disp=True)
    if mode == "fut":
        m = c.get("margin", 0.1)
        p.text(64, by0 + 180, "Mức an toàn ký quỹ", 20, UMUT, "Medium")
        p.rect(300, by0 + 186, 1866, by0 + 200, ULINE, 8)
        mc = mix(UP, DN, clamp(m * 1.3))
        p.rect(300, by0 + 186, 300 + 1566 * clamp(m), by0 + 200, mc, 8)
        p.line([(300 + 1566 * .9, by0 + 178), (300 + 1566 * .9, by0 + 208)], DN, 3)
        p.text(300 + 1566 * .9, by0 + 150, "Ngưỡng thanh lý", 18, DN, "SemiBold", "ma")
    # dải tin nhanh dưới cùng
    p.rect(40, 866, 1890, 1060, UPAN, 14)
    p.text(64, 884, "Giao dịch gần đây trên thị trường", 20, UMUT, "Medium")
    rr = random.Random(int(t * 4))
    for k in range(5):
        yy = 920 + k * 27
        sidec = UP if rr.random() > .45 else DN
        p.text(64, yy, f"{last[0].day:02d}/{last[0].month:02d}/{last[0].year}", 19, UMUT, "Regular")
        p.text(300, yy, vnd_num(price * (1 + rr.uniform(-.002, .002)), 0 if price > 100 else 2), 19, sidec, "Medium")
        p.text(560, yy, f"{rr.random() * 2:.3f}".replace(".", ","), 19, UTXT, "Regular")
    # toast
    ta = c.get("toast_a", 0)
    if ta > 0 and c.get("toast"):
        s = c["toast"]
        tw_ = p.tw(s, 26, "SemiBold") + 100
        x0t = 1890 - tw_
        y0t = 112 - (1 - ta) * 30
        p.rect(x0t, y0t, 1890, y0t + 70, (24, 120, 84, int(245 * ta)), 14)
        p.ell(x0t + 40, y0t + 35, 16, 16, (255, 255, 255, int(255 * ta)))
        p.line([(x0t + 32, y0t + 35), (x0t + 38, y0t + 42), (x0t + 49, y0t + 28)], (24, 120, 84), 4)
        p.text(x0t + 70, y0t + 20, s, 26, (255, 255, 255, int(255 * ta)), "SemiBold")
    # hộp thoại thanh lý
    ma = c.get("modal_a", 0)
    if ma > 0:
        ov = Image.new("RGBA", img.size, (0, 0, 0, int(150 * ma)))
        img.paste(ov, (0, 0), ov)
        sc = 0.9 + 0.1 * ease(ma)
        mw, mh = 820 * sc, 380 * sc
        mx, my = W / 2 - mw / 2, H / 2 - mh / 2
        p.rect(mx, my, mx + mw, my + mh, (26, 30, 44), 22)
        p.rect(mx, my, mx + mw, my + 96 * sc, DN, 22)
        p.rect(mx, my + 60 * sc, mx + mw, my + 96 * sc, DN)
        p.text(W / 2, my + 48 * sc, "Vị thế đã bị thanh lý", 40 * sc, (255, 255, 255), "Bold", "mm")
        p.text(W / 2, my + 150 * sc, "Ký quỹ không còn đủ. Hệ thống đã tự động", 26 * sc, UTXT, "Medium", "mm")
        p.text(W / 2, my + 190 * sc, "đóng toàn bộ lệnh của bạn.", 26 * sc, UTXT, "Medium", "mm")
        p.text(W / 2, my + 262 * sc, "Số dư còn lại: gần như bằng 0", 30 * sc, DN, "Bold", "mm")
        p.rect(W / 2 - 110 * sc, my + 306 * sc, W / 2 + 110 * sc, my + 356 * sc, (48, 54, 74), 12)
        p.text(W / 2, my + 331 * sc, "Đóng", 24 * sc, UTXT, "SemiBold", "mm")
    if c.get("cursor"):
        cx_, cy_, ck = c["cursor"]
        cursor(p, cx_, cy_, ck)
    out = down(img)
    # ánh phản chiếu nhẹ của màn hình
    out = ImageChops.screen(out, _glow(1500, -200, 1300, (40, 50, 70), 0.6))
    return vignette(out, 0.35)


# ---------------------------------------------------------------- điện thoại cận cảnh
def phone_shot(t, c, bg):
    base = bg.filter(ImageFilter.GaussianBlur(14)).point(lambda v: int(v * 0.55))
    img = base.convert("RGB").resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    tq = math.floor(t / STEP) * STEP
    dy = math.sin(tq * 1.3) * 4
    x0, y0, x1, y1 = 690, 60 + dy, 1230, 1180 + dy
    # bàn tay phía sau
    # lòng bàn tay và đầu ngón phía sau máy
    p.ell(x0 + 40, 1090 + dy, 230, 210, SKIN_S)
    for k in range(4):
        fy = 540 + k * 86 + dy
        p.rect(x1 - 50, fy, x1 + 28, fy + 64, SKIN if k % 2 else SKIN_S, 30)
    p.rect(x0, y0, x1, y1, (18, 18, 22), 64)
    p.rect(x0 + 14, y0 + 14, x1 - 14, y1 - 14, (14, 16, 26), 52)
    sx0, sy0, sx1 = x0 + 14, y0 + 14, x1 - 14
    p.rect(W / 2 - 60, y0 + 30, W / 2 + 60, y0 + 62, (0, 0, 0), 16)
    kind = c["kind"]
    if kind == "lock":
        vgrad(p, sx0 + 30, sy0 + 40, sx1 - 30, sy0 + 41, (30, 30, 60), (30, 30, 60))
        p.text(W / 2, y0 + 190, c.get("clock", "02:14"), 120, (240, 240, 250), "Light", "mm", True)
        p.text(W / 2, y0 + 280, c.get("day", ""), 26, (200, 205, 220), "Medium", "mm")
        for k, nt in enumerate(c["notes"]):
            when, a = nt[0], nt[1]
            n_title = nt[2] if len(nt) > 2 else "Vị thế đã bị thanh lý"
            n_body = nt[3] if len(nt) > 3 else "Toàn bộ lệnh đã tự động đóng."
            n_app = nt[4] if len(nt) > 4 else "Ứng dụng giao dịch"
            if a <= 0:
                continue
            ny = y0 + 360 + k * 170 - (1 - a) * 40
            p.rect(sx0 + 22, ny, sx1 - 22, ny + 150, (46, 50, 66, int(235 * a)), 28)
            p.ell(sx0 + 70, ny + 44, 22, 22, DN + (int(255 * a),))
            p.text(sx0 + 70, ny + 44, "!", 26, (255, 255, 255, int(255 * a)), "Bold", "mm")
            p.text(sx0 + 106, ny + 30, n_app, 20, (190, 196, 214, int(255 * a)), "SemiBold")
            p.text(sx1 - 44, ny + 30, when, 20, (190, 196, 214, int(255 * a)), "Medium", "ra")
            p.text(sx0 + 46, ny + 76, n_title, 28, (255, 255, 255, int(255 * a)), "Bold")
            p.text(sx0 + 46, ny + 112, n_body, 22, (210, 214, 228, int(255 * a)), "Medium")
    elif kind == "chat":
        p.rect(sx0, sy0, sx1, sy0 + 150, (24, 28, 42), 52)
        p.rect(sx0, sy0 + 100, sx1, sy0 + 150, (24, 28, 42))
        p.text(sx0 + 40, sy0 + 100, "‹", 44, (120, 170, 255), "Medium", "lm")
        p.ell(sx0 + 110, sy0 + 100, 30, 30, (206, 140, 150))
        p.text(sx0 + 110, sy0 + 100, c.get("who", "Vợ")[0], 28, (255, 255, 255), "Bold", "mm")
        p.text(sx0 + 154, sy0 + 82, c.get("who", "Vợ"), 28, (240, 240, 250), "Bold")
        p.text(sx0 + 154, sy0 + 116, "đang hoạt động", 18, (130, 200, 150), "Medium")
        yy = sy0 + 200
        for who, msg, a in c["msgs"]:
            if a <= 0:
                continue
            lines = _wrap(p, msg, 30, 380)
            hh = 24 + 42 * len(lines)
            wmax = max(p.tw(s, 30, "Medium") for s in lines) + 48
            if who == "in":
                bx = sx0 + 30
                colb = (52, 56, 74, int(255 * a))
            else:
                bx = sx1 - 30 - wmax
                colb = (52, 110, 230, int(255 * a))
            yo = yy + (1 - a) * 20
            p.rect(bx, yo, bx + wmax, yo + hh, colb, 26)
            for k, s in enumerate(lines):
                p.text(bx + 24, yo + 12 + k * 42, s, 30, (245, 245, 250, int(255 * a)), "Medium")
            yy += hh + 22
        # ô nhập
        typed = c.get("typing", "")
        p.rect(sx0 + 24, y0 + 740, sx1 - 24, y0 + 810, (34, 38, 54), 35)
        if typed:
            p.text(sx0 + 56, y0 + 756, typed[-24:] + ("|" if int(t * 3) % 2 else ""), 24, (240, 240, 250), "Medium")
        else:
            p.text(sx0 + 56, y0 + 756, "Nhắn tin", 24, (120, 126, 146), "Medium")
    if kind == "feed":
        off = c.get("scroll", 0)
        p.rect(sx0, sy0, sx1, sy0 + 130, (24, 28, 42), 52)
        p.text(sx0 + 40, sy0 + 92, "Nhóm đầu tư", 30, (240, 240, 250), "Bold", "lm")
        posts = c.get("posts", [])
        snap = img.copy()
        yy = sy0 + 150 - off
        for k, (name, txt, big, col) in enumerate(posts):
            if yy > y1:
                break
            if yy + 290 > sy0 + 130:
                p.rect(sx0 + 18, yy, sx1 - 18, yy + 290, (30, 34, 50), 22)
                p.ell(sx0 + 62, yy + 44, 22, 22, ((120 + k * 37) % 255, 140, 200))
                p.text(sx0 + 98, yy + 44, name, 22, (230, 232, 240), "SemiBold", "lm")
                p.text(sx0 + 40, yy + 92, txt, 22, (200, 204, 216), "Medium")
                p.rect(sx0 + 40, yy + 130, sx1 - 40, yy + 270, (16, 20, 32), 14)
                p.text(W / 2, yy + 200, big, 54, col, "Bold", "mm", True)
            yy += 310
        cut = int((sy0 + 130) * SS)
        img.paste(snap.crop((0, 0, W * SS, cut)), (0, 0))
    # ngón cái vắt qua mép trái
    p.cap((x0 - 90, 980 + dy), (x0 + 26, 760 + dy), 32, SKIN)
    p.ell(x0 + 22, 768 + dy, 16, 20, (238, 200, 176))
    out = down(img)
    return vignette(light(out, 960, 400, 900, (60, 80, 140), 0.35), 0.6)


def _wrap(p, s, size, maxw):
    out, cur = [], ""
    for w_ in s.split():
        tr = (cur + " " + w_).strip()
        if p.tw(tr, size, "Medium") > maxw and cur:
            out.append(cur)
            cur = w_
        else:
            cur = tr
    out.append(cur)
    return out


# ---------------------------------------------------------------- các cảnh có nhân vật
GLOW_C = {"red": (255, 70, 90), "green": (60, 230, 150), "blue": (90, 140, 255), "off": (0, 0, 0)}


def home_front(t, c):
    img = bg_home_night().convert("RGB").resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    character_front(p, 960, 600, c.get("pose", "type"), c.get("expr", "focus"), t, "tee", c.get("gaze", (0, 0)),
                    desk=home_desk, lid=True)
    out = down(img)
    g = c.get("glow", "blue")
    flick = 0.85 + 0.15 * math.sin(t * 9) * math.sin(t * 2.3)
    out = light(out, 960, 640, 760, GLOW_C[g], 0.55 * flick if g != "off" else 0)
    out = light(out, 1840, 420, 420, (255, 190, 120), 0.18)
    return vignette(out, 0.7)


def office_front(t, c):
    img = bg_office_day().convert("RGB").resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    character_front(p, 900, 600, c.get("pose", "phone_low"), c.get("expr", "smug"), t, "shirt",
                    c.get("gaze", (0, 6)), desk=office_desk)
    out = down(img)
    if c.get("pose", "phone_low") == "phone_low":
        out = light(out, 900, 780, 260, GLOW_C[c.get("glow", "green")], 0.35)
    return vignette(out, 0.3)


def bedroom(t, c):
    img = bg_bedroom().convert("RGB").resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    tq = math.floor(t / STEP) * STEP
    br = math.sin(tq * 1.2) * 4
    # vợ quay lưng
    p.ell(1330, 290, 116, 100, (58, 40, 36))
    p.ell(1290, 250, 60, 40, (74, 52, 46))
    # anh: mặt hướng lên
    head(p, 735, 290, c.get("expr", "blank"), tq, gaze=(10, -4), blink=True)
    # chăn
    p.poly([(330, 470 + br), (700, 440 + br), (1000, 470 + br), (1250, 430 - br), (1620, 470 - br), (1620, H), (330, H)],
           (60, 72, 108))
    for k in range(5):
        y = 560 + k * 100
        p.line([(380, y + br), (900, y - 20 + br), (1580, y + 10 - br)], (50, 60, 92), 5)
    # tay cầm điện thoại
    limb(p, (1010, 560 + br), (960, 450), 34, 28, (52, 66, 100))
    p.poly([(870, 270), (980, 300), (960, 470), (850, 440)], (24, 26, 34))
    p.line([(870, 272), (850, 440)], (150, 190, 255), 5)
    hand(p, (950, 440), (1010, 560), 1, grip=True)
    out = down(img)
    out = light(out, 800, 300, 420, (110, 150, 255), 0.55)
    return vignette(out, 0.9)


# ---------------------------------------------------------------- nhân vật nghiêng (đi bộ)
PANTS = (64, 70, 88)
PANTS_D = (48, 52, 68)
SHOE = (40, 32, 30)


def head_side(p, hx, hy, s, expr, t, look_down=0):
    (bi, bo), eyes, mouth = EXPR.get(expr, EXPR["neutral"])
    if eyes in ("open", "wide") and _blink(t, 1):
        eyes = "closed"
    p.ell(hx - s * 16, hy - 26, 84, 78, HAIR)
    p.ell(hx + s * 4, hy + 8, 74, 86, SKIN)
    p.poly([(hx + s * 64, hy + 10), (hx + s * 92, hy + 36), (hx + s * 66, hy + 44)], SKIN)  # mũi
    p.ell(hx - s * 18, hy + 12, 13, 20, SKIN_S)
    p.ell(hx - s * 18, hy + 12, 6, 11, SKIN_D)
    p.poly([(hx - s * 90, hy - 4), (hx - s * 80, hy - 70), (hx - s * 30, hy - 104), (hx + s * 40, hy - 100),
            (hx + s * 80, hy - 60), (hx + s * 82, hy - 26), (hx + s * 50, hy - 46), (hx + s * 10, hy - 50),
            (hx - s * 40, hy - 40), (hx - s * 60, hy - 10)], HAIR)
    ex, ey = hx + s * 44, hy + 4 + look_down * 6
    if eyes == "closed":
        p.line([(ex - 9, ey), (ex + 9, ey + 2)], EYE, 3)
    elif eyes == "half":
        p.ell(ex, ey + 3, 7, 7, EYE)
        p.line([(ex - 10, ey - 2), (ex + 10, ey - 2)], SKIN_D, 3)
    else:
        p.ell(ex, ey, 7, 10, EYE)
        p.ell(ex - 2, ey - 3, 2.5, 2.5, (255, 255, 255))
    p.line([(ex - s * 14, hy - 22 + bo), (ex + s * 12, hy - 22 + bi)], HAIR, 6)
    my = hy + 58
    if mouth in ("smile", "grin"):
        p.line([(hx + s * 40, my - 2), (hx + s * 56, my + 4), (hx + s * 68, my - 4)], LIP, 4)
    elif mouth == "frown":
        p.line([(hx + s * 42, my + 4), (hx + s * 56, my - 2), (hx + s * 68, my + 4)], LIP, 4)
    else:
        p.line([(hx + s * 44, my), (hx + s * 66, my)], LIP, 4)


def character_side(p, x, gy, t, expr="smug", walk=True, phone=True, s=1, outfit="shirt"):
    tq = math.floor(t / STEP) * STEP
    ph = tq * 2 * math.pi * 1.4 if walk else 0
    bob = -abs(math.sin(ph)) * 8 if walk else 0
    hip = (x, gy - 330 + bob)
    shirt, shirt_d = (SHIRT, SHIRT_D) if outfit == "shirt" else (TEE, TEE_D)
    legs = []
    for k, ofs in ((0, math.pi), (1, 0.0)):
        a = math.sin(ph + ofs) * 0.42 if walk else 0
        bend = max(0.0, math.sin(ph + ofs + 1.6)) * 0.7 if walk else 0
        knee = (hip[0] + s * math.sin(a) * 170, hip[1] + math.cos(a) * 170)
        foot = (knee[0] + s * math.sin(a - bend) * 160, knee[1] + math.cos(a - bend) * 160)
        legs.append((k, knee, foot))
    sh = (x + s * 6, hip[1] - 250)
    # tay sau
    arm_a = math.sin(ph) * 0.35 if walk else 0
    el_b = (sh[0] - s * math.sin(arm_a) * 130, sh[1] + math.cos(arm_a) * 130)
    hd_b = (el_b[0] - s * math.sin(arm_a + 0.2) * 120, el_b[1] + math.cos(arm_a + 0.2) * 120)
    limb(p, sh, el_b, 30, 26, shirt_d)
    limb(p, el_b, hd_b, 26, 20, shirt_d)
    p.ell(hd_b[0], hd_b[1], 20, 20, SKIN_S)
    for k, knee, foot in legs:
        col = PANTS_D if k == 0 else PANTS
        limb(p, hip, knee, 38, 30, col)
        limb(p, knee, foot, 30, 24, col)
        p.rect(foot[0] - 26 + s * 14, foot[1] - 14, foot[0] + 30 + s * 14, foot[1] + 16, SHOE, 12)
    # thân
    p.poly([(hip[0] - 62, hip[1] + 20), (hip[0] + 62, hip[1] + 20), (sh[0] + 66, sh[1] + 10), (sh[0] - 70, sh[1] + 10)],
           shirt)
    p.ell(sh[0], sh[1] + 20, 72, 44, shirt)
    p.rect(hip[0] - 64, hip[1] - 10, hip[0] + 64, hip[1] + 24, (60, 50, 44), 6)  # thắt lưng
    p.rect(sh[0] - 22 + s * 6, sh[1] - 70, sh[0] + 22 + s * 6, sh[1] + 10, SKIN_S, 8)
    if outfit == "shirt":
        p.poly([(sh[0] + s * 10, sh[1] - 6), (sh[0] + s * 46, sh[1] + 30), (sh[0] + s * 30, sh[1] - 10)], SHIRT_D)
    head_side(p, sh[0] + s * 14, sh[1] - 120 + (8 if phone else 0), s, expr, tq, look_down=1 if phone else 0)
    # tay trước
    if phone:
        el = (sh[0] + s * 10, sh[1] + 130)
        hd = (sh[0] + s * 110, sh[1] + 70)
    else:
        el = (sh[0] + s * math.sin(arm_a) * 130, sh[1] + math.cos(arm_a) * 130)
        hd = (el[0] + s * math.sin(arm_a - 0.2) * 120, el[1] + math.cos(arm_a - 0.2) * 120)
    limb(p, sh, el, 32, 28, shirt)
    limb(p, el, hd, 28, 22, shirt)
    if phone:
        p.poly([(hd[0] + s * 4, hd[1] - 70), (hd[0] + s * 20, hd[1] - 68), (hd[0] + s * 12, hd[1] + 30),
                (hd[0] - s * 4, hd[1] + 28)], (26, 28, 36))
        p.line([(hd[0] - s * 2, hd[1] - 60), (hd[0] - s * 10, hd[1] + 22)], (120, 200, 160), 3)
    p.ell(hd[0], hd[1], 22, 22, SKIN)
    p.ell(lerp(el[0], hd[0], .8), lerp(el[1], hd[1], .8), 14, 14, LEATHER)


@functools.lru_cache(None)
def bg_corridor():
    base = bg_office_day().filter(ImageFilter.GaussianBlur(5))
    img = base.convert("RGB").resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    vgrad(p, 0, 870, W, H, (200, 198, 194), (176, 174, 170))
    p.rect(0, 862, W, 874, (160, 160, 158))
    for xx in range(-200, W + 200, 240):
        p.line([(xx, 874), (xx - 160, H)], (186, 184, 180), 2)
    return down(img)


def corridor(t, c):
    img = bg_corridor().convert("RGB").resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    p.ell(c.get("x", 900), 1010, 150, 18, (0, 0, 0, 40))
    character_side(p, c.get("x", 900), 1010, t, c.get("expr", "smug"), c.get("walk", True), c.get("phone", True))
    out = down(img)
    return vignette(out, 0.3)


# ---------------------------------------------------------------- hũ tích cóp (cùng nét vẽ)
def jars(t, c):
    img = bg_home_night().filter(ImageFilter.GaussianBlur(8)).point(lambda v: int(v * .55)).convert("RGB")
    img = img.resize((W * SS, H * SS), Image.BILINEAR)
    p = Pen(img)
    vgrad(p, 0, 760, W, H, (70, 52, 42), (46, 34, 28))
    p.rect(0, 752, W, 768, (96, 72, 56))
    n = c.get("n", 5)
    drain = c.get("drain", [0] * n)
    show = c.get("show", 1.0)
    for i in range(n):
        a = clamp(show * n - i)
        if a <= 0:
            continue
        gap_ = 280 if n <= 5 else 1240 / (n - 1)
        cx = 960 - gap_ * (n - 1) / 2 + i * gap_
        top, bot = 400 + (1 - ease(a)) * 40, 750
        f = 1.0 if n <= 5 else gap_ * 0.42 / 110
        lvl = 1 - drain[i]
        p.ell(cx, bot + 4, 120, 14, (0, 0, 0, 70))
        p.rect(cx - 110 * f, top, cx + 110 * f, bot, (120, 160, 190, int(28 * a)), 34, outline=(200, 224, 244, int(150 * a)), width=4)
        if lvl > 0.01:
            ty = bot - 14 - (bot - top - 60) * lvl * .92
            p.rect(cx - 98 * f, ty, cx + 98 * f, bot - 12, GOLD + (int(235 * a),), 26)
            r = random.Random(i)
            for k in range(int(18 * lvl)):
                yy = ty + 14 + r.random() * max(1, bot - 30 - ty - 14)
                xx = cx - 80 * f + r.random() * 160 * f
                p.ell(xx, yy, 14 * f, 6, (250, 214, 130, int(255 * a)))
        if 0 < drain[i] < 1:
            r = random.Random(int(t * 25) + i)
            for k in range(6):
                xx = cx - 60 * f + r.random() * 120 * f
                yy = bot + 10 + r.random() * 140
                p.ell(xx, yy, 12, 5, GOLD + (200,))
        p.rect(cx - 86 * f, top - 34, cx + 86 * f, top + 6, (150, 156, 168, int(255 * a)), 10)
        p.rect(cx - 70 * f, top + 110, cx + 70 * f, top + 160, (240, 232, 214, int(255 * a)), 6)
        p.text(cx, top + 135, f"Năm {i + 1}", 26 * max(f, .75), (60, 50, 40, int(255 * a)), "Bold", "mm")
        p.line([(cx - 80 * f, top + 30), (cx - 80 * f, bot - 40)], (255, 255, 255, int(60 * a)), 6)
    ta = c.get("title_a", 1.0)
    p.text(W / 2, 230, c.get("title", "5 năm tích cóp"), 72, GOLD + (int(255 * ta),), "Bold", "mm", True)
    ea = c.get("end_a", 0)
    if ea > 0:
        p.text(W / 2, 320, "gần như về 0", 52, DN + (int(255 * ea),), "Bold", "mm", True)
    out = down(img)
    if ea > 0:
        out = light(out, 960, 600, 900, (255, 60, 80), .35 * ea)
    return vignette(out, 0.6)
