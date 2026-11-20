"""Nhân vật 2D dược sĩ Xuân Hòa — bản 004. Vẽ hoàn toàn bằng code (Pillow), thiết kế gốc.

So với 003: tóc buộc thấp vắt qua vai + mái lệch, nét viền, đổ bóng, áo có nếp và túi,
tay có khớp và ngón, nền nhà thuốc có chiều sâu (làm mờ), quầy kính.
Toạ độ khung 1080x1920 trước khi compose phóng 1.25 lần quanh mép quầy.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

from PIL import Image, ImageDraw, ImageFilter

SS = 2

SKIN = (247, 211, 186)
SKIN_SHADE = (232, 184, 158)
LINE_SKIN = (186, 122, 98)
HAIR = (38, 30, 34)
HAIR_LIGHT = (92, 76, 82)
LINE_HAIR = (18, 12, 16)
COAT = (250, 250, 255)
COAT_SHADE = (220, 220, 236)
LINE_COAT = (150, 150, 178)
GREEN = (38, 150, 64)
MOUTH = (140, 42, 58)
TONGUE = (226, 112, 122)
GOLD = (220, 176, 76)
IRIS = (88, 56, 44)


@dataclass
class Face:
    mouth: int = 0
    blink: float = 0.0
    brow: float = 0.0
    smile: float = 1.0


HEAD_W, HEAD_H = 520, 600
PIVOT = (260, 560)


def _sc(points, s):
    return [(x * s, y * s) for x, y in points]


def _curve(p0, p1, p2, n=14):
    """Bezier bậc hai, trả về danh sách điểm."""
    out = []
    for i in range(n + 1):
        t = i / n
        x = (1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0]
        y = (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1]
        out.append((x, y))
    return out


def head(face: Face) -> Image.Image:
    s = SS
    img = Image.new("RGBA", (HEAD_W * s, HEAD_H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    W = 4 * s

    # đuôi tóc buộc thấp, vắt qua vai trái nhân vật (bên phải khung)
    tail = _curve((360, 360), (450, 470), (400, 598)) + list(reversed(_curve((330, 380), (400, 480), (350, 598))))
    d.polygon(_sc(tail, s), fill=HAIR, outline=LINE_HAIR, width=W)
    d.line(_sc(_curve((365, 400), (420, 480), (385, 590)), s), fill=HAIR_LIGHT, width=4 * s)
    # tóc sau
    d.ellipse(_sc([(86, 72), (434, 432)], s), fill=HAIR, outline=LINE_HAIR, width=W)
    # tai + bông tai
    for x0 in (90, 390):
        d.ellipse(_sc([(x0, 296), (x0 + 40, 372)], s), fill=SKIN_SHADE, outline=LINE_SKIN, width=3 * s)
    d.ellipse(_sc([(100, 370), (120, 392)], s), fill=GOLD, outline=(160, 120, 40), width=2 * s)
    d.ellipse(_sc([(400, 370), (420, 392)], s), fill=GOLD, outline=(160, 120, 40), width=2 * s)
    # mặt + bóng hàm
    d.ellipse(_sc([(110, 138), (410, 522)], s), fill=SKIN, outline=LINE_SKIN, width=W)
    d = ImageDraw.Draw(img)
    # mái lệch
    cap = (_curve((112, 310), (100, 120), (262, 92)) + _curve((262, 92), (410, 110), (412, 300))[1:]
           + _curve((412, 300), (380, 196), (282, 186))[1:] + _curve((282, 186), (190, 200), (138, 300))[1:])
    d.polygon(_sc(cap, s), fill=HAIR, outline=LINE_HAIR, width=W)
    d.line(_sc(_curve((180, 128), (270, 100), (360, 150)), s), fill=HAIR_LIGHT, width=6 * s)
    d.line(_sc(_curve((300, 120), (380, 150), (398, 230)), s), fill=HAIR_LIGHT, width=4 * s)
    # má hồng
    blush = Image.new("RGBA", img.size, (0, 0, 0, 0))
    bd = ImageDraw.Draw(blush)
    bd.ellipse(_sc([(146, 398), (206, 434)], s), fill=(244, 140, 140, 95))
    bd.ellipse(_sc([(314, 398), (374, 434)], s), fill=(244, 140, 140, 95))
    img.alpha_composite(blush.filter(ImageFilter.GaussianBlur(6 * s)))
    d = ImageDraw.Draw(img)
    # lông mày
    by = 286 - 12 * face.brow
    d.line(_sc(_curve((172, by + 12), (204, by - 4), (238, by + 8)), s), fill=LINE_HAIR, width=7 * s, joint="curve")
    d.line(_sc(_curve((282, by + 8), (316, by - 4), (348, by + 12)), s), fill=LINE_HAIR, width=7 * s, joint="curve")
    # mắt
    for cx in (205, 315):
        if face.blink > 0.6:
            d.line(_sc(_curve((cx - 24, 350), (cx, 364), (cx + 24, 350)), s), fill=LINE_HAIR, width=5 * s)
            continue
        h = 48 * (1 - face.blink)
        d.ellipse(_sc([(cx - 21, 350 - h / 2), (cx + 21, 350 + h / 2)], s), fill=(255, 255, 255))
        d.ellipse(_sc([(cx - 17, 350 - h / 2 + 2), (cx + 17, 350 + h / 2)], s), fill=IRIS)
        d.ellipse(_sc([(cx - 9, 350 - h / 4), (cx + 9, 350 + h / 4)], s), fill=(24, 16, 18))
        if h > 20:
            d.ellipse(_sc([(cx - 10, 350 - h / 2 + 6), (cx + 1, 350 - h / 2 + 17)], s), fill=(255, 255, 255))
            d.ellipse(_sc([(cx + 6, 352), (cx + 11, 357)], s), fill=(255, 255, 255))
        d.line(_sc(_curve((cx - 26, 346 - h / 4), (cx, 350 - h / 2 - 8), (cx + 26, 346 - h / 4)), s), fill=LINE_HAIR, width=5 * s)
        d.line(_sc([(cx + 22, 336 - h / 4), (cx + 30, 328 - h / 4)], s), fill=LINE_HAIR, width=4 * s)
    # mũi
    d.line(_sc(_curve((254, 384), (262, 400), (252, 404)), s), fill=LINE_SKIN, width=3 * s)
    # miệng
    mx, my = 260, 452
    if face.mouth == 0:
        k = 8 + 8 * face.smile
        d.line(_sc(_curve((mx - 30, my - 2), (mx, my + k), (mx + 30, my - 2)), s), fill=MOUTH, width=5 * s)
    else:
        w, h = {1: (22, 12), 2: (27, 20), 3: (31, 28)}[face.mouth]
        d.ellipse(_sc([(mx - w, my - h * 0.5), (mx + w, my + h)], s), fill=MOUTH, outline=LINE_SKIN, width=2 * s)
        d.ellipse(_sc([(mx - w * 0.55, my + h * 0.3), (mx + w * 0.55, my + h * 0.95)], s), fill=TONGUE)
        if face.mouth >= 2:
            d.chord(_sc([(mx - w + 4, my - h * 0.5), (mx + w - 4, my + h * 0.25)], s), 180, 360, fill=(255, 255, 255))
    return img.resize((HEAD_W, HEAD_H), Image.Resampling.LANCZOS)


def body() -> Image.Image:
    s = SS
    W, H = 1080, 1920
    img = Image.new("RGBA", (W * s, H * s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    L = 4 * s
    # cổ + bóng dưới cằm
    d.rounded_rectangle(_sc([(500, 790), (580, 900)], s), radius=20 * s, fill=SKIN, outline=LINE_SKIN, width=3 * s)
    d.rectangle(_sc([(502, 820), (578, 846)], s), fill=SKIN_SHADE)
    # tay phải nhân vật (bên phải khung): khuỷu gập, bàn tay trên quầy
    _limb(d, (735, 960), (770, 1080), (690, 1168), s)
    _hand_rest(d, (668, 1166), s, mirror=True)
    for i in range(6):
        x = 700 + i * 13
        d.ellipse(_sc([(x, 1112 + i * 3), (x + 13, 1125 + i * 3)], s), fill=(240, 236, 228), outline=(190, 184, 176), width=s)
    # áo blouse
    coat = (_curve((330, 1240), (318, 1000), (360, 900)) + _curve((360, 900), (440, 868), (500, 872))[1:]
            + [(580, 872)] + _curve((580, 872), (640, 868), (720, 900))[1:] + _curve((720, 900), (762, 1000), (750, 1240))[1:])
    d.polygon(_sc(coat, s), fill=COAT, outline=LINE_COAT, width=L)
    sh = Image.new("RGBA", img.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(sh)
    sd.polygon(_sc([(330, 1240), (322, 1010), (360, 930), (392, 1240)], s), fill=(*COAT_SHADE, 170))
    sd.polygon(_sc([(750, 1240), (758, 1010), (724, 930), (690, 1240)], s), fill=(*COAT_SHADE, 120))
    img.alpha_composite(sh)
    d = ImageDraw.Draw(img)
    # tay áo ngắn
    for pts in ([(362, 896), (300, 1000), (352, 1040), (404, 950)], [(718, 896), (780, 1000), (728, 1040), (676, 950)]):
        d.polygon(_sc(pts, s), fill=COAT, outline=LINE_COAT, width=L)
    # cổ áo chữ V + ve áo
    d.polygon(_sc([(494, 876), (540, 980), (586, 876)], s), fill=SKIN_SHADE)
    d.polygon(_sc([(472, 868), (540, 986), (498, 1004), (434, 902)], s), fill=(255, 255, 255), outline=LINE_COAT, width=3 * s)
    d.polygon(_sc([(608, 868), (540, 986), (582, 1004), (646, 902)], s), fill=(255, 255, 255), outline=LINE_COAT, width=3 * s)
    d.line(_sc([(540, 990), (540, 1240)], s), fill=LINE_COAT, width=3 * s)
    for y in (1040, 1110, 1180):
        d.ellipse(_sc([(531, y - 9), (549, y + 9)], s), fill=(236, 236, 246), outline=LINE_COAT, width=2 * s)
    # túi áo dưới
    d.rounded_rectangle(_sc([(380, 1150), (480, 1230)], s), radius=10 * s, outline=LINE_COAT, width=3 * s)
    d.rounded_rectangle(_sc([(600, 1150), (700, 1230)], s), radius=10 * s, outline=LINE_COAT, width=3 * s)
    # huy hiệu chữ thập xanh
    cx, cy = 652, 1010
    d.ellipse(_sc([(cx - 30, cy - 30), (cx + 30, cy + 30)], s), fill=(255, 255, 255), outline=GREEN, width=3 * s)
    d.rounded_rectangle(_sc([(cx - 8, cy - 21), (cx + 8, cy + 21)], s), radius=3 * s, fill=GREEN)
    d.rounded_rectangle(_sc([(cx - 21, cy - 8), (cx + 21, cy + 8)], s), radius=3 * s, fill=GREEN)
    return img.resize((W, H), Image.Resampling.LANCZOS)


def _limb(d: ImageDraw.ImageDraw, a, b, c, s: int) -> None:
    for p, q in ((a, b), (b, c)):
        d.line(_sc([p, q], s), fill=LINE_SKIN, width=58 * s)
    for p in (b,):
        d.ellipse(_sc([(p[0] - 29, p[1] - 29), (p[0] + 29, p[1] + 29)], s), fill=LINE_SKIN)
    for p, q in ((a, b), (b, c)):
        d.line(_sc([p, q], s), fill=SKIN, width=50 * s)
    d.ellipse(_sc([(b[0] - 25, b[1] - 25), (b[0] + 25, b[1] + 25)], s), fill=SKIN)


def _hand_rest(d: ImageDraw.ImageDraw, c, s: int, mirror: bool = False) -> None:
    x, y = c
    d.rounded_rectangle(_sc([(x - 50, y - 26), (x + 50, y + 24)], s), radius=24 * s, fill=SKIN, outline=LINE_SKIN, width=3 * s)
    for k in (-24, -6, 12, 30):
        d.line(_sc([(x + k, y - 6), (x + k, y + 20)], s), fill=SKIN_SHADE, width=2 * s)
    tx = x - 46 if mirror else x + 46
    d.ellipse(_sc([(tx - 14, y - 22), (tx + 14, y + 4)], s), fill=SKIN, outline=LINE_SKIN, width=3 * s)


def arm(layer: Image.Image, p: float, fingers: int) -> None:
    """Tay trái khung. p=0 đặt trên quầy, p=1 giơ lên ngang vai, xòe số ngón."""
    s = SS
    p = max(0.0, min(1.0, p))
    W, H = layer.size
    d = ImageDraw.Draw(layer)
    shoulder = (352, 950)
    elbow = (316 + 34 * p, 1078 - 18 * p)
    rest, up = (412, 1166), (438, 884)
    hand = (rest[0] + (up[0] - rest[0]) * p, rest[1] + (up[1] - rest[1]) * p)
    for a_, b_ in ((shoulder, elbow), (elbow, hand)):
        d.line([a_, b_], fill=LINE_SKIN, width=58)
    d.ellipse((elbow[0] - 29, elbow[1] - 29, elbow[0] + 29, elbow[1] + 29), fill=LINE_SKIN)
    for a_, b_ in ((shoulder, elbow), (elbow, hand)):
        d.line([a_, b_], fill=SKIN, width=50)
    d.ellipse((elbow[0] - 25, elbow[1] - 25, elbow[0] + 25, elbow[1] + 25), fill=SKIN)
    # tay áo phủ vai
    d.polygon([(362, 896), (300, 1000), (352, 1040), (404, 950)], fill=COAT, outline=LINE_COAT, width=4)
    hx, hy = hand
    if p < 0.5:
        d.rounded_rectangle((hx - 50, hy - 26, hx + 50, hy + 24), radius=24, fill=SKIN, outline=LINE_SKIN, width=3)
        d.ellipse((hx + 32, hy - 22, hx + 60, hy + 4), fill=SKIN, outline=LINE_SKIN, width=3)
        return
    tips = [(-22, -80), (-4, -94), (14, -90), (30, -74)]
    for i, (fx, fy) in enumerate(tips):
        if i < fingers:
            d.line([(hx + fx * 0.4, hy - 12), (hx + fx, hy + fy)], fill=LINE_SKIN, width=24)
            d.ellipse((hx + fx - 12, hy + fy - 12, hx + fx + 12, hy + fy + 12), fill=LINE_SKIN)
            d.line([(hx + fx * 0.4, hy - 12), (hx + fx, hy + fy)], fill=SKIN, width=18)
            d.ellipse((hx + fx - 9, hy + fy - 9, hx + fx + 9, hy + fy + 9), fill=SKIN)
    d.ellipse((hx - 38, hy - 34, hx + 38, hy + 36), fill=SKIN, outline=LINE_SKIN, width=3)
    for i in range(fingers, 4):
        fx = tips[i][0] * 0.5
        d.ellipse((hx + fx - 12, hy - 40, hx + fx + 12, hy - 14), fill=SKIN, outline=LINE_SKIN, width=3)
    d.ellipse((hx + 24, hy - 8, hx + 58, hy + 18), fill=SKIN, outline=LINE_SKIN, width=3)


def backdrop() -> Image.Image:
    """Nhà thuốc: tường kem chuyển màu, đèn trần, kệ gỗ có hộp không chữ, làm mờ cho chiều sâu."""
    W, H = 1080, 1920
    img = Image.new("RGB", (W, H))
    d = ImageDraw.Draw(img)
    for y in range(H):
        t = y / H
        d.line((0, y, W, y), fill=(round(250 - 10 * t), round(245 - 14 * t), round(234 - 18 * t)))
    # đèn trần
    glow = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((140, -160, 940, 260), fill=(255, 255, 240, 120))
    img = Image.alpha_composite(img.convert("RGBA"), glow.filter(ImageFilter.GaussianBlur(60))).convert("RGB")
    d = ImageDraw.Draw(img)
    palette = [(150, 196, 168), (236, 196, 150), (160, 186, 226), (244, 214, 170), (214, 170, 196),
               (170, 214, 206), (250, 236, 200), (196, 210, 160)]
    for row, y in enumerate((330, 560, 790, 1020)):
        x, k = 40, row * 3
        while x < 1030:
            w = 54 + (k * 37) % 46
            h = 96 + (k * 23) % 80
            col = palette[k % len(palette)]
            d.rounded_rectangle((x, y - h, x + w, y), radius=8, fill=col)
            d.rectangle((x + 8, y - h + 16, x + w - 8, y - h + 34), fill=tuple(min(255, c + 30) for c in col))
            d.rectangle((x, y - h, x + 6, y), fill=tuple(max(0, c - 25) for c in col))
            x += w + 12
            k += 1
        d.rectangle((20, y, 1060, y + 22), fill=(196, 160, 120))
        d.rectangle((20, y + 22, 1060, y + 30), fill=(160, 124, 88))
    # chữ thập xanh trên tường
    d.rounded_rectangle((60, 120, 180, 240), radius=22, fill=GREEN)
    d.rectangle((106, 138, 134, 222), fill=(255, 255, 255))
    d.rectangle((78, 166, 162, 194), fill=(255, 255, 255))
    img = img.filter(ImageFilter.GaussianBlur(5))
    veil = Image.new("RGB", (W, H), (250, 244, 232))
    return Image.blend(img, veil, 0.22)


def counter() -> Image.Image:
    """Quầy kính: mặt trên trắng, kính phía trước thấy hộp mờ, dải xanh thương hiệu."""
    W, H = 1080, 1920
    img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rectangle((0, 1196, W, H), fill=(214, 232, 236))
    glass = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glass)
    palette = [(150, 196, 168), (236, 196, 150), (160, 186, 226), (244, 214, 170), (214, 170, 196)]
    for row, y in enumerate((1420, 1600)):
        x, k = 30, row * 2
        while x < 1050:
            w = 70 + (k * 31) % 50
            gd.rounded_rectangle((x, y - 110, x + w, y), radius=8, fill=(*palette[k % 5], 255))
            x += w + 18
            k += 1
        gd.rectangle((0, y, W, y + 10), fill=(180, 200, 206, 255))
    glass = glass.filter(ImageFilter.GaussianBlur(4))
    img.alpha_composite(glass)
    tint = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    td = ImageDraw.Draw(tint)
    td.rectangle((0, 1196, W, H), fill=(230, 244, 248, 90))
    for x in range(-200, W, 260):
        td.polygon([(x, H), (x + 120, 1210), (x + 170, 1210), (x + 50, H)], fill=(255, 255, 255, 50))
    img.alpha_composite(tint)
    d = ImageDraw.Draw(img)
    d.rectangle((0, 1176, W, 1206), fill=(252, 252, 250))
    d.rectangle((0, 1206, W, 1212), fill=(190, 196, 200))
    # chậu cây nhỏ và hộp giấy trên quầy (góc phải)
    d.rounded_rectangle((930, 1120, 1000, 1180), radius=10, fill=(214, 120, 80), outline=(160, 84, 56), width=3)
    for ang in (-50, -20, 10, 40):
        r = math.radians(ang - 90)
        ex, ey = 965 + 60 * math.cos(r), 1120 + 60 * math.sin(r)
        d.ellipse((ex - 18, ey - 30, ex + 18, ey + 6), fill=(70, 150, 80), outline=(40, 110, 56), width=2)
    return img


def customer_hand(layer: Image.Image, p: float, t: float) -> None:
    """Tay khách thò vào từ mép trái, đập nhẹ xuống quầy. p 0..1 là mức thò vào."""
    if p <= 0:
        return
    d = ImageDraw.Draw(layer)
    dx = -320 * (1 - p)
    bob = 10 * math.sin(t * 14) * p
    sleeve = (255, 150, 90)
    cx, cy = 250 + dx, 1150 + bob
    d.polygon([(-40 + dx, 1290), (-40 + dx, 1170), (cx - 40, cy - 34), (cx - 20, cy + 40)], fill=sleeve, outline=(196, 100, 56))
    d.rounded_rectangle((cx - 60, cy - 30, cx + 40, cy + 34), radius=26, fill=SKIN, outline=LINE_SKIN, width=3)
    for k in (-34, -14, 6, 24):
        d.ellipse((cx + k - 2, cy - 44, cx + k + 18, cy - 14), fill=SKIN, outline=LINE_SKIN, width=3)
