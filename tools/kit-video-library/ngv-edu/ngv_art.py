"""Hình vẽ riêng của kênh Người Giữ Vốn cho mẫu cảnh nv_board (ẩn danh: nhân vật không bao giờ lộ mặt).

Nhân vật "keeper": tóc ngắn đen, áo polo trắng, quần xám, đồng hồ dây da mặt tròn KHÔNG logo.
Chỉ vẽ từ phía sau (back), ngồi bàn nhìn từ sau lưng (desk), hoặc cận cổ tay (wrist). Không có mặt.
Đồ vật lặp lại: đồng hồ (thời gian, số năm tích cóp), hũ tích cóp (jar), tách cà phê (hiện tại, đã tĩnh tâm).

novixa_board.py nạp file này qua ART: {tên: (hàm(w, h, args) -> Image, tỉ lệ w/h)}.
Thuần Pillow, không mạng.
"""

from __future__ import annotations

import importlib.util
import math
from functools import lru_cache
from pathlib import Path

_IL = Path(__file__).resolve().parents[1] / "novixa-series" / "src" / "novixa_illus.py"
_spec = importlib.util.spec_from_file_location("novixa_illus_ngv", _IL)
il = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(il)
Cv, SS, shade, mix = il.Cv, il.SS, il.shade, il.mix

HAIR = (34, 30, 32)
SKIN = (222, 176, 140)
SKIN_D = (196, 148, 116)
POLO = (240, 242, 246)
POLO_D = (206, 212, 222)
PANTS = (176, 182, 192)
PANTS_D = (146, 152, 164)
LEATHER = (120, 72, 40)
STEEL = (206, 212, 220)
GLASS = (200, 226, 240)
RED = il.RED
GREEN = il.GREEN


def _watch(cv, cx, cy, r, strap=True, hh=10, mm=10, flip=False):
    """Đồng hồ dây da, mặt tròn, không logo, không chữ."""
    if strap:
        sw = r * 1.15
        cv.rect((cx - sw / 2, cy - r * 2.3, cx + sw / 2, cy + r * 2.3), fill=LEATHER, r=int(r * .35))
        for k in (-1.7, -1.3, 1.3, 1.7):
            cv.line([(cx - sw * .35, cy + k * r), (cx + sw * .35, cy + k * r)], shade(LEATHER, -.25), max(1, int(r * .06)))
    cv.ell((cx - r * 1.08, cy - r * 1.08, cx + r * 1.08, cy + r * 1.08), fill=shade(STEEL, -.2))
    cv.ell((cx - r, cy - r, cx + r, cy + r), fill=STEEL)
    cv.ell((cx - r * .84, cy - r * .84, cx + r * .84, cy + r * .84), fill=(250, 250, 246))
    for i in range(12):
        a = math.radians(i * 30)
        x1, y1 = cx + math.sin(a) * r * .72, cy - math.cos(a) * r * .72
        x2, y2 = cx + math.sin(a) * r * .8, cy - math.cos(a) * r * .8
        cv.line([(x1, y1), (x2, y2)], (60, 64, 74), max(1, int(r * (.07 if i % 3 == 0 else .035))))
    ah = math.radians((hh % 12) * 30 + mm * .5)
    am = math.radians(mm * 6)
    cv.line([(cx, cy), (cx + math.sin(ah) * r * .45, cy - math.cos(ah) * r * .45)], (30, 34, 44), max(2, int(r * .09)))
    cv.line([(cx, cy), (cx + math.sin(am) * r * .66, cy - math.cos(am) * r * .66)], (30, 34, 44), max(1, int(r * .06)))
    cv.ell((cx - r * .07, cy - r * .07, cx + r * .07, cy + r * .07), fill=(30, 34, 44))


@lru_cache(None)
def keeper_back(w=300, h=560, mood="still"):
    """Đứng, nhìn từ sau lưng. mood: still | slump (vai chùng) | upright."""
    cv = Cv(w, h)
    cx = w / 2
    slump = mood == "slump"
    up = -h * .015 if mood == "upright" else (h * .03 if slump else 0)
    cv.shadow((w * .15, h * .95, w * .85, h * 1.05), 10, 110)
    drop = h * .03 if slump else 0
    # chân
    cv.rect((cx - w * .19, h * .6, cx - w * .02, h * .96), fill=PANTS, r=12)
    cv.rect((cx + w * .02, h * .6, cx + w * .19, h * .96), fill=PANTS_D, r=12)
    cv.rect((cx - w * .21, h * .935, cx - w * .01, h * .985), fill=(60, 52, 48), r=10)
    cv.rect((cx + w * .01, h * .935, cx + w * .21, h * .985), fill=(60, 52, 48), r=10)
    sh = h * .27 + up
    # cánh tay (vẽ trước thân để vai che chỗ nối)
    for sx in (-1, 1):
        ax = cx + sx * w * .3
        cv.rect((ax - w * .055, sh + h * .1, ax + w * .055, h * .56 + drop), fill=SKIN if sx < 0 else SKIN_D, r=int(w * .05))
    # đồng hồ dây da ở cổ tay trái
    wx, wy = cx - w * .3, h * .5 + drop
    cv.rect((wx - w * .062, wy - h * .02, wx + w * .062, wy + h * .02), fill=LEATHER, r=5)
    cv.ell((wx - w * .034, wy - w * .034, wx + w * .034, wy + w * .034), fill=STEEL)
    # thân áo polo: vai tròn, thân thu nhỏ về eo
    cv.ell((cx - w * .37, sh, cx - w * .15, sh + h * .16), fill=POLO)
    cv.ell((cx + w * .15, sh, cx + w * .37, sh + h * .16), fill=POLO_D)
    cv.poly([(cx - w * .3, sh + h * .03), (cx + w * .3, sh + h * .03), (cx + w * .24, h * .63), (cx - w * .24, h * .63)], fill=POLO)
    cv.poly([(cx + w * .04, sh + h * .03), (cx + w * .3, sh + h * .03), (cx + w * .24, h * .63), (cx + w * .06, h * .63)], fill=POLO_D)
    cv.rect((cx - w * .245, h * .6, cx + w * .245, h * .635), fill=POLO_D, r=6)
    # cổ và cổ áo
    cv.rect((cx - w * .075, sh - h * .035, cx + w * .075, sh + h * .03), fill=SKIN_D, r=10)
    cv.poly([(cx - w * .16, sh + h * .005), (cx + w * .16, sh + h * .005), (cx + w * .12, sh + h * .05), (cx - w * .12, sh + h * .05)], fill=(228, 232, 238))
    # đầu nhìn từ sau: tóc ngắn phủ kín, tai hai bên, không có mặt
    hy = sh - h * .19 + drop
    hr = w * .17
    cv.ell((cx - hr - w * .03, hy + h * .07, cx - hr + w * .03, hy + h * .13), fill=SKIN_D)
    cv.ell((cx + hr - w * .03, hy + h * .07, cx + hr + w * .03, hy + h * .13), fill=SKIN_D)
    cv.ell((cx - hr, hy, cx + hr, hy + h * .175), fill=HAIR)
    cv.poly([(cx - hr * .7, hy + h * .14), (cx + hr * .7, hy + h * .14), (cx + hr * .45, hy + h * .185), (cx - hr * .45, hy + h * .185)], fill=HAIR)
    return cv.out()


@lru_cache(None)
def keeper_desk(w=760, h=560, screen="dark", night=True):
    """Ngồi bàn, nhìn từ sau lưng, màn hình laptop phía trước. screen: dark | red | green | chart_down | chart_up."""
    cv = Cv(w, h)
    glow = {"red": RED, "green": GREEN, "chart_down": RED, "chart_up": GREEN}.get(screen, (90, 120, 170))
    if night:
        cv.ell((w * .2, h * .02, w * .8, h * .62), fill=mix((10, 18, 36), glow, .18))
    # laptop
    lx0, lx1, ly0, ly1 = w * .3, w * .7, h * .12, h * .46
    cv.rect((lx0 - 8, ly0 - 8, lx1 + 8, ly1 + 8), fill=(40, 44, 54), r=12)
    cv.grad((lx0, ly0, lx1, ly1), shade(glow, .25), shade(glow, -.35), r=6)
    if screen in ("chart_down", "chart_up", "red", "green"):
        pts = []
        for i in range(9):
            t = i / 8
            base = (1 - t) if screen in ("chart_down", "red") else t
            y = ly1 - (ly1 - ly0) * (.15 + .7 * base) + (math.sin(i * 1.9) * (ly1 - ly0) * .06)
            pts.append((lx0 + (lx1 - lx0) * (.08 + .84 * t), y))
        cv.line(pts, (250, 250, 250), max(3, int(h * .008)))
    # bàn
    cv.rect((w * .04, h * .5, w * .96, h * .58), fill=(92, 64, 44), r=6)
    cv.rect((w * .04, h * .58, w * .96, h * .62), fill=(70, 48, 34), r=4)
    # người: lưng, vai, đầu sau, che một phần màn hình
    cx = w * .5
    cv.poly([(cx - w * .2, h * .66), (cx + w * .2, h * .66), (cx + w * .23, h), (cx - w * .23, h)], fill=POLO)
    cv.poly([(cx + w * .03, h * .66), (cx + w * .2, h * .66), (cx + w * .23, h), (cx + w * .05, h)], fill=POLO_D)
    cv.ell((cx - w * .24, h * .6, cx - w * .1, h * .76), fill=POLO)
    cv.ell((cx + w * .1, h * .6, cx + w * .24, h * .76), fill=POLO_D)
    cv.rect((cx - w * .04, h * .54, cx + w * .04, h * .64), fill=SKIN_D, r=8)
    cv.ell((cx - w * .1, h * .35, cx + w * .1, h * .6), fill=HAIR)
    cv.ell((cx - w * .115, h * .45, cx - w * .085, h * .52), fill=SKIN_D)
    cv.ell((cx + w * .085, h * .45, cx + w * .115, h * .52), fill=SKIN_D)
    # cổ tay trái với đồng hồ đặt trên bàn
    cv.rect((w * .18, h * .5, w * .3, h * .55), fill=SKIN, r=10)
    _watch(cv, w * .245, h * .525, h * .03, strap=False)
    return cv.out()


@lru_cache(None)
def wrist(w=520, h=400, screen="red"):
    """Cận cổ tay đeo đồng hồ đặt cạnh điện thoại úp/ngửa. screen: red | green | down (úp)."""
    cv = Cv(w, h)
    # điện thoại
    px0, py0, px1, py1 = w * .58, h * .1, w * .9, h * .9
    if screen == "down":
        cv.rect((px0, py0, px1, py1), fill=(36, 40, 50), r=22)
        cv.ell((px0 + (px1 - px0) * .1, py0 + (py1 - py0) * .05, px0 + (px1 - px0) * .26, py0 + (py1 - py0) * .14), fill=(20, 22, 28))
    else:
        c = RED if screen == "red" else GREEN
        cv.rect((px0, py0, px1, py1), fill=(30, 32, 40), r=22)
        cv.grad((px0 + 10, py0 + 14, px1 - 10, py1 - 14), shade(c, .2), shade(c, -.3), r=14)
        pts = [(px0 + (px1 - px0) * (.12 + .76 * t), py0 + (py1 - py0) * ((.3 + .5 * t) if screen == "red" else (.8 - .5 * t)) + math.sin(t * 9) * 8) for t in [i / 7 for i in range(8)]]
        cv.line(pts, (255, 255, 255), 5)
    # cẳng tay và bàn tay
    cv.poly([(0, h * .42), (w * .44, h * .48), (w * .44, h * .74), (0, h * .8)], fill=SKIN)
    cv.ell((w * .36, h * .44, w * .56, h * .78), fill=SKIN_D)
    _watch(cv, w * .24, h * .61, h * .13, hh=11, mm=50)
    # mép tay áo polo
    cv.poly([(0, h * .36), (w * .07, h * .4), (w * .07, h * .86), (0, h * .9)], fill=POLO)
    return cv.out()


@lru_cache(None)
def watch_only(w=300, h=420, hh=10, mm=10):
    cv = Cv(w, h)
    cv.shadow((w * .2, h * .9, w * .8, h * 1.04), 8, 90)
    _watch(cv, w / 2, h / 2, min(w, h * .43) * .42, hh=hh, mm=mm)
    return cv.out()


@lru_cache(None)
def jar(w=220, h=300, level=1.0, label="", cracked=False):
    """Hũ tích cóp bằng thuỷ tinh, mức xu 0..1, nhãn ngắn (vd 'Năm 1')."""
    cv = Cv(w, h)
    cv.shadow((w * .1, h * .92, w * .9, h * 1.05), 8, 100)
    x0, y0, x1, y1 = w * .1, h * .18, w * .9, h * .96
    cv.rect((x0, y0, x1, y1), fill=mix(GLASS, (16, 30, 62), .35), r=int(w * .14))
    lv = max(0.0, min(1.0, float(level)))
    if lv > 0:
        top = y1 - (y1 - y0 - 10) * lv
        cv.rect((x0 + 8, top, x1 - 8, y1 - 8), fill=(232, 182, 72), r=int(w * .1))
        for i in range(int(6 * lv) + 1):
            yy = top + 10 + i * ((y1 - top) / 7)
            cv.line([(x0 + 14, yy), (x1 - 14, yy)], (196, 146, 50), 3)
    cv.rect((x0, y0, x0 + (x1 - x0) * .18, y1), fill=mix(GLASS, (255, 255, 255), .5), r=int(w * .1))
    cv.rect((w * .22, h * .06, w * .78, h * .2), fill=(150, 110, 70), r=10)
    if cracked:
        cv.line([(w * .62, y0 + 10), (w * .5, h * .4), (w * .6, h * .55), (w * .46, h * .72)], (250, 250, 250), 4)
    if label:
        cv.rect((w * .18, h * .42, w * .82, h * .58), fill=(250, 248, 240), r=8)
        cv.text(w / 2, h * .435, label, h * .09, (30, 34, 44), anchor="m")
    return cv.out()


@lru_cache(None)
def cup(w=300, h=220):
    """Tách cà phê trên đĩa, có khói."""
    cv = Cv(w, h)
    cv.shadow((w * .08, h * .82, w * .92, h * 1.02), 8, 90)
    cv.ell((w * .06, h * .74, w * .94, h * .96), fill=(236, 238, 242))
    cv.rect((w * .24, h * .38, w * .7, h * .84), fill=(250, 250, 252), r=24)
    cv.ell((w * .24, h * .32, w * .7, h * .46), fill=(250, 250, 252))
    cv.ell((w * .27, h * .34, w * .67, h * .44), fill=(150, 96, 56))
    cv.arc((w * .64, h * .46, w * .84, h * .7), 270, 90, (250, 250, 252), int(w * .045))
    for k, dx in enumerate((-.08, .02, .12)):
        x = w * (.47 + dx)
        cv.arc((x - w * .05, h * .04, x + w * .05, h * .18), 90 if k % 2 else 270, 270 if k % 2 else 90, (200, 210, 224), 4)
        cv.arc((x - w * .05, h * .16, x + w * .05, h * .3), 270 if k % 2 else 90, 90 if k % 2 else 270, (200, 210, 224), 4)
    return cv.out()


@lru_cache(None)
def keys(w=260, h=200):
    """Chùm chìa khoá: tài sản phải bán."""
    cv = Cv(w, h)
    cv.ell((w * .08, h * .1, w * .38, h * .48), outline=(200, 206, 214), width=8)
    for i, (dx, col) in enumerate(((.38, (214, 180, 90)), (.5, (200, 206, 214)))):
        y = h * (.36 + i * .2)
        cv.rect((w * dx - 10, y, w * (dx + .44), y + h * .09), fill=col, r=6)
        for t in (.12, .22, .32):
            cv.rect((w * (dx + t), y + h * .09, w * (dx + t + .05), y + h * .16), fill=col)
        cv.ell((w * dx - 26, y - 12, w * dx + 14, y + h * .09 + 12), fill=col)
    return cv.out()


def _a(args, key, default):
    v = args.get(key, default)
    return default if v is None else v


ART = {
    "keeper": (lambda w, h, a: keeper_back(w, h, str(_a(a, "mood", "still"))), .54),
    "keeper_desk": (lambda w, h, a: keeper_desk(w, h, str(_a(a, "screen", "dark")), bool(_a(a, "night", True))), 1.36),
    "wrist": (lambda w, h, a: wrist(w, h, str(_a(a, "screen", "red"))), 1.3),
    "watch": (lambda w, h, a: watch_only(w, h, int(_a(a, "hh", 10)), int(_a(a, "mm", 10))), .72),
    "jar": (lambda w, h, a: jar(w, h, float(_a(a, "level", 1.0)), str(_a(a, "label", "")), bool(_a(a, "cracked", False))), .74),
    "cup": (lambda w, h, a: cup(w, h), 1.36),
    "keys": (lambda w, h, a: keys(w, h), 1.3),
}
