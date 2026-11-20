"""Code-drawn icons and small charts for knowledge scenes. License-clean: drawn here, no files.

Every prop takes a palette (dict of RGB tuples) so one brand token set recolors all of them.
Drawing uses a 100x100 design grid, supersampled, and returns a square RGBA sprite.
Charts are synthetic and always carry a "minh họa" label; real charts use chart.py + an OHLCV file.
"""

from __future__ import annotations

import math
import random

from PIL import Image, ImageDraw

SS = 4


def _mix(a, b, k):
    return tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))


def _rgba(c, a=255):
    return (*c, a)


class _G:
    """100x100 grid canvas."""

    def __init__(self, size: int):
        self.size = size
        self.k = size * SS / 100.0
        self.im = Image.new("RGBA", (size * SS, size * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.im)

    def p(self, *xs):
        return [v * self.k for v in xs]

    def rect(self, x0, y0, x1, y1, fill=None, outline=None, w=0, r=0):
        box = self.p(x0, y0, x1, y1)
        if r:
            self.d.rounded_rectangle(box, radius=r * self.k, fill=fill and _rgba(fill), outline=outline and _rgba(outline), width=int(w * self.k))
        else:
            self.d.rectangle(box, fill=fill and _rgba(fill), outline=outline and _rgba(outline), width=int(w * self.k))

    def ell(self, x0, y0, x1, y1, fill=None, outline=None, w=0):
        self.d.ellipse(self.p(x0, y0, x1, y1), fill=fill and _rgba(fill), outline=outline and _rgba(outline), width=int(w * self.k))

    def poly(self, pts, fill):
        self.d.polygon([(x * self.k, y * self.k) for x, y in pts], fill=_rgba(fill))

    def line(self, pts, fill, w=3):
        pts = [(x * self.k, y * self.k) for x, y in pts]
        self.d.line(pts, fill=_rgba(fill), width=int(w * self.k), joint="curve")
        r = w * self.k / 2
        for x, y in (pts[0], pts[-1]):
            self.d.ellipse((x - r, y - r, x + r, y + r), fill=_rgba(fill))

    def arc(self, box, a0, a1, fill, w):
        self.d.arc(self.p(*box), a0, a1, fill=_rgba(fill), width=int(w * self.k))

    def done(self) -> Image.Image:
        return self.im.resize((self.size, self.size), Image.Resampling.LANCZOS)


def _tone(pal, name, fb="accent"):
    return pal.get(name) or pal[fb]


def coin(size, pal):
    g = _G(size)
    c = _tone(pal, "warn")
    g.ell(10, 14, 90, 94, fill=_mix(c, (0, 0, 0), 0.35))
    g.ell(10, 8, 90, 88, fill=c, outline=_mix(c, (255, 255, 255), 0.35), w=4)
    g.ell(24, 22, 76, 74, outline=_mix(c, (0, 0, 0), 0.25), w=3)
    g.line([(38, 48), (62, 48)], _mix(c, (0, 0, 0), 0.45), 4)
    g.line([(50, 34), (50, 64)], _mix(c, (0, 0, 0), 0.45), 4)
    return g.done()


def coins(size, pal):
    g = _G(size)
    c = _tone(pal, "warn")
    for i, y in enumerate((74, 60, 46, 32)):
        g.ell(16, y, 84, y + 22, fill=_mix(c, (0, 0, 0), 0.30))
        g.ell(16, y - 6, 84, y + 16, fill=c, outline=_mix(c, (255, 255, 255), 0.3), w=2)
    return g.done()


def wallet(size, pal, empty=False):
    g = _G(size)
    body = _mix(_tone(pal, "warn"), (0, 0, 0), 0.45)
    if not empty:
        g.rect(22, 18, 74, 52, fill=_tone(pal, "good"), r=4)
        g.rect(28, 12, 80, 46, fill=_mix(_tone(pal, "good"), (255, 255, 255), 0.25), r=4)
    g.rect(8, 34, 92, 86, fill=body, r=9)
    g.rect(8, 34, 92, 46, fill=_mix(body, (255, 255, 255), 0.15), r=9)
    g.rect(60, 52, 94, 72, fill=_mix(body, (255, 255, 255), 0.2), r=6)
    g.ell(70, 58, 80, 68, fill=_tone(pal, "warn"))
    return g.done()


def wallet_empty(size, pal):
    return wallet(size, pal, True)


def lock(size, pal):
    g = _G(size)
    c = _tone(pal, "accent")
    g.arc((28, 10, 72, 62), 180, 360, _mix(c, (255, 255, 255), 0.2), 9)
    g.line([(28.5, 36), (28.5, 46)], _mix(c, (255, 255, 255), 0.2), 9)
    g.line([(71.5, 36), (71.5, 46)], _mix(c, (255, 255, 255), 0.2), 9)
    g.rect(18, 42, 82, 92, fill=c, r=9)
    g.ell(43, 58, 57, 72, fill=pal["paper"])
    g.rect(47, 66, 53, 82, fill=pal["paper"], r=2)
    return g.done()


def shield(size, pal):
    g = _G(size)
    c = _tone(pal, "good")
    pts = [(50, 6), (86, 18), (84, 52), (70, 78), (50, 94), (30, 78), (16, 52), (14, 18)]
    g.poly(pts, _mix(c, (0, 0, 0), 0.25))
    g.poly([(50 + (x - 50) * 0.88, 52 + (y - 52) * 0.9) for x, y in pts], c)
    g.line([(34, 50), (46, 62), (68, 38)], pal["paper"], 8)
    return g.done()


def check(size, pal):
    g = _G(size)
    g.ell(6, 6, 94, 94, fill=_tone(pal, "good"))
    g.line([(28, 52), (44, 68), (74, 34)], pal["paper"], 10)
    return g.done()


def cross(size, pal):
    g = _G(size)
    g.ell(6, 6, 94, 94, fill=_tone(pal, "danger"))
    g.line([(32, 32), (68, 68)], pal["paper"], 10)
    g.line([(68, 32), (32, 68)], pal["paper"], 10)
    return g.done()


def warn(size, pal):
    g = _G(size)
    c = _tone(pal, "warn")
    g.poly([(50, 8), (95, 86), (5, 86)], c)
    g.line([(50, 36), (50, 62)], _mix(c, (0, 0, 0), 0.7), 9)
    g.ell(44.5, 70, 55.5, 81, fill=_mix(c, (0, 0, 0), 0.7))
    return g.done()


def clock(size, pal):
    g = _G(size)
    c = _tone(pal, "accent")
    g.ell(6, 6, 94, 94, fill=pal["paper"], outline=c, w=7)
    for i in range(12):
        a = i * math.pi / 6
        g.line([(50 + 36 * math.sin(a), 50 - 36 * math.cos(a)), (50 + 41 * math.sin(a), 50 - 41 * math.cos(a))], pal["muted"], 2.5)
    g.line([(50, 50), (50, 22)], pal["ink"], 5)
    g.line([(50, 50), (68, 58)], pal["ink"], 5)
    g.ell(45, 45, 55, 55, fill=c)
    return g.done()


def scale(size, pal):
    g = _G(size)
    c = pal["ink"]
    g.line([(50, 14), (50, 84)], c, 5)
    g.rect(28, 84, 72, 92, fill=c, r=3)
    g.line([(16, 30), (84, 22)], c, 5)
    for cx, cy, tone in ((16, 30, "good"), (84, 22, "danger")):
        g.line([(cx, cy), (cx - 12, cy + 24)], pal["muted"], 2)
        g.line([(cx, cy), (cx + 12, cy + 24)], pal["muted"], 2)
        g.rect(cx - 16, cy + 24, cx + 16, cy + 30, fill=_tone(pal, tone), r=3)
    return g.done()


def flame(size, pal):
    g = _G(size)
    c = _tone(pal, "danger")
    o = _tone(pal, "warn")
    g.poly([(50, 4), (74, 34), (86, 62), (76, 86), (50, 96), (24, 86), (14, 62), (28, 38), (36, 52), (42, 30)], c)
    g.poly([(50, 44), (64, 64), (60, 82), (50, 90), (40, 82), (36, 64)], o)
    return g.done()


def _series(seed, n, drift):
    rnd = random.Random(seed)
    v, out = 50.0, []
    for _ in range(n):
        v += rnd.uniform(-5, 5) + drift
        out.append(v)
    lo, hi = min(out), max(out)
    return [(x - lo) / (hi - lo + 1e-9) for x in out]


def line_down(size, pal):
    g = _G(size)
    ys = _series(3, 9, -2.2)
    pts = [(8 + i * 84 / 8, 12 + (1 - y) * 70) for i, y in enumerate(ys)]
    g.poly(pts + [(92, 90), (8, 90)], _mix(pal["paper"], _tone(pal, "danger"), 0.22))
    g.line(pts, _tone(pal, "danger"), 5)
    return g.done()


def line_up(size, pal):
    g = _G(size)
    ys = _series(5, 9, 2.2)
    pts = [(8 + i * 84 / 8, 12 + (1 - y) * 70) for i, y in enumerate(ys)]
    g.poly(pts + [(92, 90), (8, 90)], _mix(pal["paper"], _tone(pal, "good"), 0.22))
    g.line(pts, _tone(pal, "good"), 5)
    return g.done()


def candles(size, pal, up=True, seed=7, n=9):
    g = _G(size)
    rnd = random.Random(seed)
    v, rows = 50.0, []
    for _ in range(n):
        o = v
        v += rnd.uniform(-7, 7) + (4.5 if up else -4.5)
        rows.append((o, v, max(o, v) + rnd.uniform(1, 6), min(o, v) - rnd.uniform(1, 6)))
    lo = min(r[3] for r in rows)
    hi = max(r[2] for r in rows)
    sc = lambda x: 88 - (x - lo) / (hi - lo + 1e-9) * 72
    w = 84 / n
    for i, (o, c, h, l) in enumerate(rows):
        x = 8 + i * w + w / 2
        col = _tone(pal, "good" if c >= o else "danger")
        g.line([(x, sc(h)), (x, sc(l))], col, 2)
        g.rect(x - w * 0.3, min(sc(o), sc(c)), x + w * 0.3, max(sc(o), sc(c)) + 1, fill=col)
    return g.done()


def gauge(size, pal):
    g = _G(size)
    for a0, a1, tone in ((180, 240, "good"), (240, 300, "warn"), (300, 360, "danger")):
        g.arc((10, 22, 90, 102), a0, a1, _tone(pal, tone), 12)
    g.line([(50, 62), (28, 36)], pal["ink"], 5)
    g.ell(44, 56, 56, 68, fill=pal["ink"])
    return g.done()


def phone(size, pal):
    g = _G(size)
    g.rect(24, 4, 76, 96, fill=pal["ink"], r=9)
    g.rect(29, 12, 71, 86, fill=_mix(pal["paper"], pal["background"], 0.4), r=4)
    ys = _series(11, 7, 1.6)
    pts = [(33 + i * 34 / 6, 30 + (1 - y) * 36) for i, y in enumerate(ys)]
    g.line(pts, _tone(pal, "accent"), 3)
    g.rect(33, 72, 67, 78, fill=_mix(pal["paper"], pal["muted"], 0.4), r=2)
    return g.done()


def hop(size, pal):
    """Hộp thuốc: một hộp có nắp, nhãn và dấu cộng."""
    g = _G(size)
    c = _tone(pal, "accent")
    g.poly([(14, 34), (34, 16), (92, 16), (72, 34)], _mix(c, pal["ink"], 0.35))
    g.poly([(72, 34), (92, 16), (92, 70), (72, 88)], _mix(c, pal["background"], 0.4))
    g.rect(14, 34, 72, 88, fill=c, r=3)
    g.rect(24, 46, 62, 76, fill=pal["ink"], r=3)
    g.line([(43, 52), (43, 70)], _tone(pal, "good"), 5)
    g.line([(34, 61), (52, 61)], _tone(pal, "good"), 5)
    return g.done()


def ke(size, pal):
    """Kệ: hai tầng ván, trên mỗi tầng vài hộp."""
    g = _G(size)
    wood = _mix(pal["muted"], pal["background"], 0.25)
    g.rect(8, 8, 14, 94, fill=wood, r=2)
    g.rect(86, 8, 92, 94, fill=wood, r=2)
    for y, tones in ((44, ("accent", "good", "warn")), (84, ("warn", "accent", "good"))):
        g.rect(8, y, 92, y + 7, fill=pal["muted"], r=2)
        x = 18
        for i, tone in enumerate(tones):
            h = 26 - i * 4
            g.rect(x, y - h, x + 18, y, fill=_tone(pal, tone), r=2)
            g.rect(x + 3, y - h + 5, x + 15, y - h + 10, fill=pal["ink"], r=1)
            x += 22
    return g.done()


def so_tay(size, pal):
    """Sổ tay: bìa có gáy lò xo, các dòng kẻ, một dòng đang viết."""
    g = _G(size)
    c = _tone(pal, "accent")
    g.rect(18, 8, 86, 94, fill=c, r=6)
    g.rect(26, 8, 86, 94, fill=pal["ink"], r=5)
    for y in (24, 40, 56, 72):
        g.line([(36, y), (76, y)], pal["muted"], 3)
    g.line([(36, 40), (58, 40)], _tone(pal, "danger"), 3)
    for y in (18, 34, 50, 66, 82):
        g.ell(10, y - 5, 28, y + 5, fill=pal["muted"])
        g.ell(14, y - 2.5, 24, y + 2.5, fill=pal["background"])
    return g.done()


def ket(size, pal):
    """Két tiền: thân két, cửa, núm xoay và tay nắm."""
    g = _G(size)
    body = _mix(pal["muted"], pal["background"], 0.35)
    g.rect(8, 10, 92, 86, fill=body, r=7)
    g.rect(16, 18, 84, 78, fill=_mix(pal["muted"], pal["ink"], 0.15), r=4)
    g.ell(28, 28, 62, 62, fill=_tone(pal, "accent"), outline=pal["ink"], w=3)
    g.ell(40, 40, 50, 50, fill=pal["ink"])
    g.line([(45, 30), (45, 36)], pal["ink"], 2.5)
    g.line([(45, 54), (45, 60)], pal["ink"], 2.5)
    g.line([(30, 45), (36, 45)], pal["ink"], 2.5)
    g.line([(54, 45), (60, 45)], pal["ink"], 2.5)
    g.rect(70, 36, 78, 60, fill=pal["ink"], r=3)
    g.rect(16, 86, 28, 94, fill=body, r=2)
    g.rect(72, 86, 84, 94, fill=body, r=2)
    return g.done()


def khach(size, pal):
    """Khách: đầu và vai."""
    g = _G(size)
    g.ell(16, 58, 84, 132, fill=_tone(pal, "accent"))
    g.ell(32, 12, 68, 48, fill=pal["ink"])
    return g.done()


def nhan_vien(size, pal):
    """Nhân viên quầy: áo choàng sáng, bảng tên xanh."""
    g = _G(size)
    coat = _mix(pal["ink"], pal["muted"], 0.15)
    g.ell(14, 58, 86, 134, fill=coat)
    g.poly([(40, 58), (50, 76), (60, 58)], _tone(pal, "accent"))
    g.line([(50, 76), (50, 98)], pal["muted"], 2.5)
    g.rect(58, 70, 76, 82, fill=_tone(pal, "good"), r=2)
    g.line([(62, 76), (72, 76)], pal["ink"], 2)
    g.ell(32, 12, 68, 48, fill=_mix(pal["ink"], pal["muted"], 0.35))
    return g.done()


def danh_sach(size, pal):
    """Danh sách việc: bảng kẹp giấy, hai mục đã xong, một mục còn mở."""
    g = _G(size)
    g.rect(16, 10, 84, 94, fill=_tone(pal, "accent"), r=7)
    g.rect(22, 20, 78, 88, fill=pal["ink"], r=4)
    g.rect(36, 5, 64, 18, fill=pal["muted"], r=4)
    for y, done in ((34, True), (52, True), (70, False)):
        g.rect(28, y - 6, 40, y + 6, outline=_tone(pal, "good" if done else "warn"), w=3, r=2)
        if done:
            g.line([(30, y), (33.5, y + 3.5), (38.5, y - 4)], _tone(pal, "good"), 3)
        g.line([(46, y), (72, y)], pal["muted"], 3.5)
    return g.done()


def lo_thuoc(size, pal):
    """Lô thuốc: ba hộp xếp chồng, hộp phía trước có nhãn hạn dùng."""
    g = _G(size)
    c = _tone(pal, "accent")
    g.rect(28, 12, 72, 44, fill=_mix(c, pal["ink"], 0.25), r=3)
    g.rect(8, 46, 50, 88, fill=c, r=3)
    g.rect(52, 46, 94, 88, fill=_mix(c, pal["background"], 0.25), r=3)
    g.rect(14, 54, 44, 74, fill=pal["ink"], r=2)
    g.rect(18, 60, 34, 66, fill=_tone(pal, "warn"), r=1)
    g.line([(18, 70), (38, 70)], pal["muted"], 2)
    g.rect(58, 54, 88, 66, fill=pal["ink"], r=2)
    g.rect(34, 18, 66, 30, fill=pal["ink"], r=2)
    return g.done()


PROPS = {
    "coin": coin, "coins": coins, "wallet": wallet, "wallet_empty": wallet_empty, "lock": lock,
    "shield": shield, "check": check, "cross": cross, "warn": warn, "clock": clock, "scale": scale,
    "flame": flame, "line_down": line_down, "line_up": line_up, "candles": candles,
    "candles_down": lambda s, p: candles(s, p, up=False, seed=4), "gauge": gauge, "phone": phone,
    "hop": hop, "ke": ke, "so_tay": so_tay, "ket": ket, "khach": khach, "nhan_vien": nhan_vien,
    "danh_sach": danh_sach, "lo_thuoc": lo_thuoc,
}


def prop(name: str, size: int, pal: dict) -> Image.Image | None:
    fn = PROPS.get(name)
    return fn(max(24, int(size)), pal) if fn else None
