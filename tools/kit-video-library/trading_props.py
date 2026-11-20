"""Agent Trading illustrative props, drawn with Pillow. No network, no paid API.

Agent Trading is an agent of the Second Brain park. This park only makes videos about it.
The objects follow what the agent really does: public candles -> SMA signal -> risk gate ->
paper/demo order -> ledger -> journal. Nothing here is market data: every chart is a seeded
synthetic series, tagged "Dữ liệu minh họa", and no object suggests a real order or a profit.

Run:
    python trading_props.py --repo E:/KitPlatform --out proof/kit-video-library/<run>
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, __version__ as PIL_VERSION

import novixa_props as base
from novixa_props import SS, U, canvas, font, lerp, noise, shadow_under, to_img, finish

PAPER, INK, MUTED = (255, 252, 246), (16, 28, 48), (84, 94, 110)
UP, DOWN = (18, 122, 82), (164, 72, 64)
SMA_FAST, SMA_SLOW = (47, 110, 214), (214, 140, 40)
BG_SOFT = (232, 236, 242)
TAG = "Dữ liệu minh họa"
NOTE = "Paper / Demo — không phải khuyến nghị"
SYMBOL = "BTCUSDT · 1H"


def _t(d, x, y, text, size, fill, bold=False, anchor="ls", spacing=0.0):
    """Text at design units. Letter spacing handled by base.spaced_text when asked."""
    f = font(size, bold)
    if spacing:
        base.spaced_text(d, U(x), U(y), text, f, fill, spacing, {"ls": "l", "ms": "m", "rs": "r"}[anchor])
    else:
        d.text((U(x), U(y)), text, font=f, fill=fill, anchor=anchor)


def _pill(d, x0, y0, x1, y1, fill=None, outline=None, width=2, r=None):
    r = (y1 - y0) / 2 if r is None else r
    d.rounded_rectangle((U(x0), U(y0), U(x1), U(y1)), radius=U(r), fill=fill, outline=outline, width=U(width))


# ---------------------------------------------------------------------------
# Synthetic series
# ---------------------------------------------------------------------------

def series(seed: int, shown: int = 48, warm: int = 24):
    rng = np.random.default_rng(seed)
    n = shown + warm
    drift = np.zeros(n)
    cuts = sorted(rng.choice(np.arange(8, n - 8), 3, replace=False))
    sign = rng.choice([-1, 1])
    last = 0
    for c in list(cuts) + [n]:
        drift[last:c] = sign * rng.uniform(0.25, 0.55)
        sign, last = -sign, c
    close = 100 + np.cumsum(drift + rng.normal(0, 0.55, n))
    open_ = np.r_[close[0] - drift[0], close[:-1]]
    hi = np.maximum(open_, close) + np.abs(rng.normal(0, 0.35, n))
    lo = np.minimum(open_, close) - np.abs(rng.normal(0, 0.35, n))
    vol = 40 + np.abs(close - open_) * 55 + rng.uniform(0, 30, n)
    sma = lambda k: np.array([close[max(0, i - k + 1): i + 1].mean() if i >= k - 1 else np.nan for i in range(n)])
    f5, s20 = sma(5), sma(20)
    crosses = []
    for i in range(max(21, warm), n):
        if f5[i - 1] <= s20[i - 1] and f5[i] > s20[i]:
            crosses.append((i - warm, "up"))
        if f5[i - 1] >= s20[i - 1] and f5[i] < s20[i]:
            crosses.append((i - warm, "down"))
    sl = slice(warm, n)
    return dict(o=open_[sl], h=hi[sl], l=lo[sl], c=close[sl], v=vol[sl], f=f5[sl], s=s20[sl], crosses=crosses)


def good_seed(start=1):
    s = start
    while True:
        d = series(s)
        kinds = {k for _, k in d["crosses"]}
        if kinds == {"up", "down"} and 2 <= len(d["crosses"]) <= 4 and not np.isnan(d["s"]).any():
            return s
        s += 1


def chart_panel(w, h, seed, geom=None) -> Image.Image:
    """Light chart card at design size (w x h units). Returns an RGBA image in working pixels."""
    data = series(seed)
    im = Image.new("RGBA", (U(w), U(h)), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    d.rounded_rectangle((0, 0, U(w) - 1, U(h) - 1), radius=U(26), fill=PAPER + (255,), outline=(214, 218, 226, 255), width=U(2))
    _t(d, 40, 62, SYMBOL, 30, INK, True)
    _pill(d, w - 290, 30, w - 40, 72, outline=(214, 140, 40, 255), width=2)
    _t(d, w - 165, 59, TAG, 21, (150, 92, 16), True, "ms")
    # legend
    lx = 330
    for name, col in (("SMA 5", SMA_FAST), ("SMA 20", SMA_SLOW)):
        d.line((U(lx), U(52), U(lx + 34), U(52)), fill=col + (255,), width=U(5))
        _t(d, lx + 44, 60, name, 21, MUTED, True)
        lx += 150
    px0, px1, py0, py1 = 120, w - 40, 110, h - 160
    vy0, vy1 = h - 140, h - 56
    lo, hi = data["l"].min() - 1.5, data["h"].max() + 1.5
    Y = lambda p: py1 - (p - lo) / (hi - lo) * (py1 - py0)
    n = len(data["c"])
    step = (px1 - px0) / n
    X = lambda i: px0 + step * (i + 0.5)
    for k in range(5):                                           # grid + axis labels (index 100 = start of series)
        gy = py0 + (py1 - py0) * k / 4
        d.line((U(px0), U(gy), U(px1), U(gy)), fill=(226, 229, 235, 255), width=U(1.5))
        val = hi - (hi - lo) * k / 4
        _t(d, px0 - 14, gy + 7, f"{val:.0f}", 20, MUTED, False, "rs")
    for i in range(n):                                           # volume
        vh = (vy1 - vy0) * data["v"][i] / data["v"].max()
        col = UP if data["c"][i] >= data["o"][i] else DOWN
        d.rectangle((U(X(i) - step * 0.32), U(vy1 - vh), U(X(i) + step * 0.32), U(vy1)), fill=col + (110,))
    for i in range(n):                                           # candles
        col = UP if data["c"][i] >= data["o"][i] else DOWN
        d.line((U(X(i)), U(Y(data["h"][i])), U(X(i)), U(Y(data["l"][i]))), fill=col + (255,), width=U(2.4))
        y0, y1 = sorted((Y(data["o"][i]), Y(data["c"][i])))
        d.rounded_rectangle((U(X(i) - step * 0.34), U(y0), U(X(i) + step * 0.34), U(max(y1, y0 + 2))), radius=U(2), fill=col + (255,))
    for key, col in (("s", SMA_SLOW), ("f", SMA_FAST)):          # moving averages
        pts = [(U(X(i)), U(Y(v))) for i, v in enumerate(data[key])]
        d.line(pts, fill=col + (255,), width=U(4.5), joint="curve")
    for i, kind in data["crosses"]:                              # signal markers
        cx, cy = X(i), Y(data["c"][i])
        col = UP if kind == "up" else DOWN
        d.ellipse((U(cx - 15), U(cy - 15), U(cx + 15), U(cy + 15)), fill=(255, 255, 255, 255), outline=col + (255,), width=U(4))
        tri = [(cx, cy - 7), (cx - 7, cy + 5), (cx + 7, cy + 5)] if kind == "up" else [(cx, cy + 7), (cx - 7, cy - 5), (cx + 7, cy - 5)]
        d.polygon([(U(a), U(b)) for a, b in tri], fill=col + (255,))
    _t(d, 40, h - 20, NOTE, 21, MUTED, False)
    if geom is not None:
        geom.update(data=data, X=X, Y=Y, step=step, vy1=vy1, vy0=vy0, py0=py0, py1=py1, px0=px0, px1=px1)
    return im


# ---------------------------------------------------------------------------
# Objects
# ---------------------------------------------------------------------------

def _place(card: Image.Image, angle: float, blur=40, opacity=0.34, dy=34, dx=18) -> Image.Image:
    im = canvas()
    im.alpha_composite(card, ((im.width - card.width) // 2, (im.height - card.height) // 2))
    if angle:
        im = im.rotate(angle, resample=Image.BICUBIC, center=(im.width // 2, im.height // 2))
    return shadow_under(im, im.getchannel("A"), blur, opacity, dx, dy)


def make_chart_card(p=None) -> Image.Image:
    p = {"seed": good_seed(), "angle": 3.0, **(p or {})}
    return _place(chart_panel(1200, 780, p["seed"]), p["angle"])


def make_monitor(p=None) -> Image.Image:
    p = {"seed": good_seed(), **(p or {})}
    W, H, bez = 1380, 860, 34
    m = Image.new("RGBA", (U(W), U(H + 190)), (0, 0, 0, 0))
    d = ImageDraw.Draw(m)
    # stand
    d.polygon([(U(W / 2 - 70), U(H - 6)), (U(W / 2 + 70), U(H - 6)), (U(W / 2 + 120), U(H + 150)), (U(W / 2 - 120), U(H + 150))],
              fill=(120, 128, 142, 255))
    sx = np.linspace(0, 1, U(240))
    neck = np.zeros((U(160), U(240), 3), np.float32)
    neck[:] = lerp((86, 94, 108), (176, 184, 198), np.clip(np.abs(sx - 0.35) * 1.6, 0, 1))[None]
    nm = Image.new("L", (U(240), U(160)), 0)
    ImageDraw.Draw(nm).polygon([(U(50), 0), (U(190), 0), (U(240), U(160)), (0, U(160))], fill=255)
    m.alpha_composite(to_img(neck, np.asarray(nm)), (U(W / 2 - 120), U(H - 10)))
    d.rounded_rectangle((U(W / 2 - 250), U(H + 136), U(W / 2 + 250), U(H + 176)), radius=U(20), fill=(104, 112, 126, 255))
    d.rounded_rectangle((U(W / 2 - 250), U(H + 136), U(W / 2 + 250), U(H + 152)), radius=U(8), fill=(190, 198, 210, 255))
    # bezel
    y, x = np.mgrid[0:U(H), 0:U(W)].astype(np.float32)
    bz = lerp((46, 54, 70), (18, 26, 42), np.clip(x / U(W) * 0.5 + y / U(H) * 0.6, 0, 1))
    bm = Image.new("L", (U(W), U(H)), 0)
    ImageDraw.Draw(bm).rounded_rectangle((0, 0, U(W) - 1, U(H) - 1), radius=U(38), fill=255)
    m.alpha_composite(to_img(bz, np.asarray(bm)), (0, 0))
    d.rounded_rectangle((U(3), U(3), U(W) - U(4), U(H) - U(4)), radius=U(36), outline=(255, 255, 255, 60), width=U(2))
    # screen
    sw, sh = W - 2 * bez, H - 2 * bez - 10
    scr = Image.new("RGBA", (U(sw), U(sh)), BG_SOFT + (255,))
    sd = ImageDraw.Draw(scr)
    chart = chart_panel(930, 680, p["seed"])
    scr.alpha_composite(chart, (U(28), U(28)))
    tiles = [("Nến công khai", "Đã nạp", UP), ("Tín hiệu SMA", "Chờ nến đóng", SMA_SLOW), ("Cổng rủi ro", "Đạt", UP), ("Sổ cái", "Đã ghi", SMA_FAST)]
    ty = 28
    for name, state, col in tiles:
        sd.rounded_rectangle((U(990), U(ty), U(sw - 28), U(ty + 150)), radius=U(20), fill=PAPER + (255,), outline=(214, 218, 226, 255), width=U(2))
        _t(sd, 1014, ty + 52, name, 24, MUTED, True)
        _pill(sd, 1014, ty + 78, 1014 + 24 + 14 * len(state), ty + 120, fill=tuple(int(c * 0.14 + 255 * 0.86) for c in col) + (255,), outline=col + (255,), width=2)
        _t(sd, 1014 + (24 + 14 * len(state)) / 2, ty + 108, state, 21, col, True, "ms")
        ty += 166
    _t(sd, 1014, sh / 1 - 60, "Paper / Demo", 22, MUTED, True)
    gloss = Image.new("RGBA", scr.size, (0, 0, 0, 0))
    ImageDraw.Draw(gloss).polygon([(0, 0), (U(sw * 0.55), 0), (U(sw * 0.20), U(sh)), (0, U(sh))], fill=(255, 255, 255, 16))
    scr = Image.alpha_composite(scr, gloss)
    sm = rounded_mask = Image.new("L", scr.size, 0)
    ImageDraw.Draw(sm).rounded_rectangle((0, 0, scr.width - 1, scr.height - 1), radius=U(14), fill=255)
    scr.putalpha(sm)
    m.alpha_composite(scr, (U(bez), U(bez)))
    return _place(m, 0, 36, 0.36, 40, 24)


def _shield_pts(cx, cy, w, h, n=60):
    pts = []
    top = cy - h / 2
    for i in range(n + 1):                                       # top edge with a slight notch
        t = i / n
        pts.append((cx - w / 2 + w * t, top + 22 * math.sin(math.pi * t) * 0.0 + 20 * (1 - math.sin(math.pi * t)) * 0.9))
    for i in range(1, n + 1):                                    # right side curving to the point
        t = i / n
        pts.append((cx + w / 2 * (1 - t ** 2.2) + 0 * t, top + 20 + (h - 20) * t))
    for i in range(1, n):
        t = 1 - i / n
        pts.append((cx - w / 2 * (1 - t ** 2.2), top + 20 + (h - 20) * t))
    return pts


def make_shield(p=None) -> Image.Image:
    p = p or {}
    im = canvas()
    cx, cy, w, h = 900, 840, 760, 900
    outer = _shield_pts(cx, cy, w, h)
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).polygon([(U(a), U(b)) for a, b in outer], fill=255)
    y, x = np.mgrid[0:im.height, 0:im.width].astype(np.float32)
    g = lerp((44, 62, 96), (12, 20, 38), np.clip((x / im.width - 0.3) * 1.2 + (y / im.height - 0.3) * 0.6, 0, 1))
    body = to_img(g, np.asarray(mask))
    inner_pts = _shield_pts(cx, cy + 6, w - 90, h - 100)
    imask = Image.new("L", im.size, 0)
    ImageDraw.Draw(imask).polygon([(U(a), U(b)) for a, b in inner_pts], fill=255)
    g2 = lerp((30, 150, 104), (10, 84, 62), np.clip((x / im.width - 0.25) * 1.1 + (y / im.height - 0.25) * 0.9, 0, 1))
    inner = to_img(g2 + noise(g2.shape[:2] + (1,), 1.5, 5), np.asarray(imask))
    cast = Image.new("L", im.size, 0)
    ImageDraw.Draw(cast).ellipse((U(cx - 330), U(cy + h / 2 - 20), U(cx + 380), U(cy + h / 2 + 60)), fill=255)
    out = shadow_under(canvas(), cast, 34, 0.40)
    out.alpha_composite(shadow_under(canvas(), mask, 26, 0.30, 12, 22))
    out.alpha_composite(body)
    out.alpha_composite(inner)
    d = ImageDraw.Draw(out)
    d.polygon([(U(a), U(b)) for a, b in outer], outline=(190, 205, 230, 120), width=U(4))
    d.polygon([(U(a), U(b)) for a, b in inner_pts], outline=(255, 255, 255, 80), width=U(3))
    # lock glyph
    lx, ly, lw, lh = cx, cy + 40, 250, 200
    sy = ly - lh / 2 - 38
    d.arc((U(lx - 93), U(sy - 93), U(lx + 93), U(sy + 93)), 180, 360, fill=(246, 248, 252, 255), width=U(26))
    for sx_ in (-80, 80):
        d.rectangle((U(lx + sx_ - 13), U(sy), U(lx + sx_ + 13), U(sy + 50)), fill=(246, 248, 252, 255))
    lock = Image.new("L", out.size, 0)
    ImageDraw.Draw(lock).rounded_rectangle((U(lx - lw / 2), U(ly - lh / 2), U(lx + lw / 2), U(ly + lh / 2)), radius=U(34), fill=255)
    lg = lerp((255, 255, 255), (206, 214, 228), np.clip((y - U(ly - lh / 2)) / U(lh), 0, 1)[..., None][..., 0])
    out.alpha_composite(shadow_under(canvas(), lock, 10, 0.35, 4, 8))
    out.alpha_composite(to_img(lg, np.asarray(lock)))
    d.ellipse((U(lx - 24), U(ly - 36), U(lx + 24), U(ly + 12)), fill=(16, 90, 66, 255))
    d.polygon([(U(lx - 12), U(ly + 6)), (U(lx + 12), U(ly + 6)), (U(lx + 20), U(ly + 66)), (U(lx - 20), U(ly + 66))], fill=(16, 90, 66, 255))
    # ribbon
    rb = (U(cx - 300), U(cy + h / 2 - 60), U(cx + 300), U(cy + h / 2 + 40))
    d.rounded_rectangle(rb, radius=U(50), fill=PAPER + (255,), outline=(214, 218, 226, 255), width=U(3))
    base.spaced_text(d, U(cx), U(cy + h / 2 + 8 + 14), "CỔNG RỦI RO", font(46, True), INK, 6, "m")
    if p.get("explain"):
        for k, ln in enumerate(_wrap("Trước mỗi lệnh, agent kiểm tra giới hạn vị thế và đòn bẩy. Chưa đạt thì không đi tiếp.", 28, 640)):
            _t(d, cx, cy + h / 2 + 100 + k * 36, ln, 28, INK, False, "ms")
        _t(d, cx, cy + h / 2 + 100 + 2 * 36 + 20, NOTE, 24, MUTED, False, "ms")
    else:
        _t(d, cx, cy + h / 2 + 100, NOTE, 26, MUTED, False, "ms")
    return out


def _glyph(d, kind, cx, cy, s):
    col = (255, 255, 255, 255)
    if kind == "candle":
        for dx, h, c in ((-s * 0.5, s * 0.8, UP), (0, s * 1.1, DOWN), (s * 0.5, s * 0.9, UP)):
            d.line((U(cx + dx), U(cy - h / 2 - 8), U(cx + dx), U(cy + h / 2 + 8)), fill=col, width=U(3))
            d.rectangle((U(cx + dx - 9), U(cy - h / 2), U(cx + dx + 9), U(cy + h / 2)), fill=col)
    elif kind == "cross":
        d.line([(U(cx - s * 0.9), U(cy + s * 0.5)), (U(cx), U(cy - s * 0.1)), (U(cx + s * 0.9), U(cy - s * 0.6))], fill=col, width=U(7), joint="curve")
        d.line([(U(cx - s * 0.9), U(cy - s * 0.5)), (U(cx), U(cy + s * 0.1)), (U(cx + s * 0.9), U(cy + s * 0.6))], fill=(255, 220, 150, 255), width=U(7), joint="curve")
    elif kind == "shield":
        pts = _shield_pts(cx, cy, s * 1.5, s * 1.8, 20)
        d.polygon([(U(a), U(b)) for a, b in pts], outline=col, width=U(6))
        d.line((U(cx - s * 0.3), U(cy), U(cx - s * 0.05), U(cy + s * 0.3), U(cx + s * 0.4), U(cy - s * 0.3)), fill=col, width=U(7))
    elif kind == "order":
        d.rounded_rectangle((U(cx - s * 0.7), U(cy - s * 0.9), U(cx + s * 0.7), U(cy + s * 0.9)), radius=U(10), outline=col, width=U(6))
        for k in (-0.4, 0.0, 0.4):
            d.line((U(cx - s * 0.4), U(cy + s * k), U(cx + s * 0.4), U(cy + s * k)), fill=col, width=U(5))
    else:  # ledger / book
        d.rounded_rectangle((U(cx - s * 0.9), U(cy - s * 0.7), U(cx + s * 0.9), U(cy + s * 0.7)), radius=U(8), outline=col, width=U(6))
        d.line((U(cx), U(cy - s * 0.7), U(cx), U(cy + s * 0.7)), fill=col, width=U(5))
        for k in (-0.25, 0.1):
            d.line((U(cx - s * 0.7), U(cy + s * k), U(cx - s * 0.2), U(cy + s * k)), fill=col, width=U(4))
            d.line((U(cx + s * 0.2), U(cy + s * k), U(cx + s * 0.7), U(cy + s * k)), fill=col, width=U(4))


def make_pipeline(p=None) -> Image.Image:
    W, H = 1640, 620
    card = Image.new("RGBA", (U(W), U(H)), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    d.rounded_rectangle((0, 0, U(W) - 1, U(H) - 1), radius=U(30), fill=PAPER + (255,), outline=(214, 218, 226, 255), width=U(2))
    _t(d, 44, 72, "Agent Trading · từ dữ liệu đến sổ cái", 34, INK, True)
    steps = [("Nến công khai", "candle", (47, 110, 214)), ("Tín hiệu SMA", "cross", (214, 140, 40)), ("Cổng rủi ro", "shield", (18, 122, 82)),
             ("Lệnh thử", "order", (96, 82, 170)), ("Sổ cái", "ledger", (16, 28, 48))]
    cw, gap = 252, 70
    x0 = (W - (5 * cw + 4 * gap)) / 2
    for i, (name, kind, col) in enumerate(steps):
        x = x0 + i * (cw + gap)
        shadow = Image.new("L", card.size, 0)
        ImageDraw.Draw(shadow).rounded_rectangle((U(x), U(130), U(x + cw), U(130 + 330)), radius=U(26), fill=255)
        card = shadow_under(card, shadow, 10, 0.22, 3, 8)
        d = ImageDraw.Draw(card)
        gy = np.mgrid[0:U(330), 0:U(cw)][0].astype(np.float32) / U(330)
        g = lerp(tuple(min(255, c + 40) for c in col), tuple(int(c * 0.78) for c in col), gy)
        cm = Image.new("L", (U(cw), U(330)), 0)
        ImageDraw.Draw(cm).rounded_rectangle((0, 0, U(cw) - 1, U(330) - 1), radius=U(26), fill=255)
        card.alpha_composite(to_img(g, np.asarray(cm)), (U(x), U(130)))
        d = ImageDraw.Draw(card)
        d.ellipse((U(x + cw / 2 - 66), U(160), U(x + cw / 2 + 66), U(292)), fill=tuple(int(c * 0.80 + 255 * 0.20) for c in col) + (255,), outline=(255, 255, 255, 170), width=U(3))
        _glyph(d, kind, x + cw / 2, 226, 58)
        _t(d, x + cw / 2, 380, name, 30, (255, 255, 255), True, "ms")
        _t(d, x + cw / 2, 428, f"{i + 1} / 5", 22, (255, 255, 255, 200), False, "ms")
        if i < 4:
            ax = x + cw + 10
            d.line((U(ax), U(295), U(ax + gap - 34), U(295)), fill=INK + (255,), width=U(6))
            d.polygon([(U(ax + gap - 34), U(278)), (U(ax + gap - 10), U(295)), (U(ax + gap - 34), U(312))], fill=INK + (255,))
    _pill(d, W / 2 - 330, 500, W / 2 + 330, 560, fill=(244, 238, 226, 255), outline=(214, 140, 40, 255), width=2)
    _t(d, W / 2, 540, NOTE, 26, (120, 76, 14), True, "ms")
    return _place(card, 0, 34, 0.30, 30, 14)


# ---------------------------------------------------------------------------
# Explainer versions: every part is named in plain Vietnamese for a first-time viewer
# ---------------------------------------------------------------------------

NAVY = INK


def _wrap(text, size, maxw, bold=False):
    f = font(size, bold)
    words, lines, cur = text.split(), [], ""
    for w_ in words:
        t = (cur + " " + w_).strip()
        if f.getlength(t) <= U(maxw):
            cur = t
        else:
            lines.append(cur)
            cur = w_
    return lines + [cur]


def _badge(d, n, x, y, r=21):
    d.ellipse((U(x - r), U(y - r), U(x + r), U(y + r)), fill=NAVY + (255,), outline=(255, 255, 255, 255), width=U(3))
    _t(d, x, y + 9, str(n), 26, (255, 255, 255), True, "ms")


def _pointer(d, n, bx, by, tx, ty, col=NAVY):
    d.line((U(bx), U(by), U(tx), U(ty)), fill=col + (255,), width=U(3))
    d.ellipse((U(tx - 6), U(ty - 6), U(tx + 6), U(ty + 6)), fill=col + (255,), outline=(255, 255, 255, 255), width=U(2))
    _badge(d, n, bx, by)


def _legend(d, x0, y0, items, width=470):
    y = y0
    for n, title, body in items:
        _badge(d, n, x0 + 21, y + 14)
        _t(d, x0 + 58, y + 24, title, 28, INK, True)
        yy = y + 24
        for ln in _wrap(body, 24, width - 58):
            yy += 32
            _t(d, x0 + 58, yy, ln, 24, MUTED, False)
        y = yy + 36
    return y


def _explain_card(title, sub, W=1580, H=840):
    card = Image.new("RGBA", (U(W), U(H)), (0, 0, 0, 0))
    d = ImageDraw.Draw(card)
    d.rounded_rectangle((0, 0, U(W) - 1, U(H) - 1), radius=U(30), fill=PAPER + (255,), outline=(214, 218, 226, 255), width=U(2))
    _t(d, 44, 66, title, 40, INK, True)
    _t(d, 44, 104, sub, 24, MUTED, False)
    return card, d


def _annotated_chart(mode, p=None):
    p = {"seed": good_seed(), **(p or {})}
    W, H = 1580, 860
    title = "Cách đọc biểu đồ nến" if mode == "reading" else "Tín hiệu vào và thoát của Agent Trading"
    sub = ("Mỗi hình dưới đây là giá của BTCUSDT trong 1 giờ." if mode == "reading"
           else "Agent so hai đường trung bình để biết khi nào vào và khi nào thoát.")
    card, d = _explain_card(title, sub, W, H)
    geom = {}
    ox, oy, cw, ch = 28, 134, 960, 690
    chart = chart_panel(cw, ch, p["seed"], geom)
    card.alpha_composite(chart, (U(ox), U(oy)))
    d = ImageDraw.Draw(card)
    data, X, Y = geom["data"], geom["X"], geom["Y"]
    AX = lambda v: ox + v
    AY = lambda v: oy + v
    c, o = data["c"], data["o"]
    body = np.abs(c - o)
    ups = [i for i in range(6, 20) if c[i] > o[i]]
    gi = max(ups, key=lambda i: body[i])
    downs = [i for i in range(34, len(c) - 3) if c[i] < o[i]]
    ri = max(downs, key=lambda i: body[i])
    if mode == "reading":
        _pointer(d, 2, AX(X(gi)) - 70, AY(Y(data["h"][gi])) - 70, AX(X(gi)), AY(Y(max(o[gi], c[gi]))))
        _pointer(d, 3, AX(X(ri)) + 80, AY(Y(data["l"][ri])) + 60, AX(X(ri)), AY(Y(min(o[ri], c[ri]))))
        _pointer(d, 1, AX(X(gi + 8)) - 8, AY(py_ := geom["py0"]) + 14, AX(X(gi + 8)), AY(Y((data["h"][gi + 8] + data["l"][gi + 8]) / 2)))
        k5 = 24
        _pointer(d, 4, AX(X(k5)) - 36, AY(Y(data["f"][k5])) + 74, AX(X(k5)), AY(Y(data["f"][k5])), SMA_FAST)
        k20 = 30
        _pointer(d, 5, AX(X(k20)) + 30, AY(Y(data["s"][k20])) + 80, AX(X(k20)), AY(Y(data["s"][k20])), SMA_SLOW)
        vi = 12
        vh = (geom["vy1"] - geom["vy0"]) * data["v"][vi] / data["v"].max()
        _pointer(d, 6, AX(X(vi)) + 70, AY(geom["vy1"]) - 6, AX(X(vi)), AY(geom["vy1"] - vh / 2))
        _legend(d, 1030, 150, [
            (1, "Một cây nến", "Cho biết giá đã thay đổi thế nào trong đúng 1 giờ."),
            (2, "Nến xanh = giá tăng", "Giá lúc đóng cao hơn giá lúc mở."),
            (3, "Nến đỏ = giá giảm", "Giá lúc đóng thấp hơn giá lúc mở."),
            (4, "Đường SMA 5 (xanh dương)", "Trung bình giá đóng của 5 nến gần nhất. Bám sát giá."),
            (5, "Đường SMA 20 (cam)", "Trung bình của 20 nến gần nhất. Chậm và mượt hơn."),
            (6, "Các cột phía dưới", "Khối lượng giao dịch trong mỗi giờ."),
        ])
    else:
        up = next(i for i, k in data["crosses"] if k == "up")
        dn = next(i for i, k in data["crosses"] if k == "down" and i > up)
        sh = Image.new("RGBA", card.size, (0, 0, 0, 0))
        ImageDraw.Draw(sh).rectangle((U(AX(X(up))), U(AY(geom["py0"])), U(AX(X(dn))), U(AY(geom["py1"]))), fill=(18, 122, 82, 34))
        card = Image.alpha_composite(card, sh)
        d = ImageDraw.Draw(card)
        _pointer(d, 1, AX(X(up)) + 70, AY(Y(data["c"][up])) + 80, AX(X(up)), AY(Y(data["c"][up])), UP)
        _pointer(d, 2, AX(X(dn)) + 70, AY(Y(data["c"][dn])) - 90, AX(X(dn)), AY(Y(data["c"][dn])), DOWN)
        mid = (up + dn) // 2
        _badge(d, 3, AX(X(mid)) - 150, AY(geom["py0"]) + 34)
        _legend(d, 1030, 150, [
            (1, "SMA 5 cắt lên trên SMA 20", "Giá gần đây mạnh hơn mức trung bình dài hơn. Agent coi đây là tín hiệu vào."),
            (2, "SMA 5 cắt xuống dưới SMA 20", "Đà tăng yếu đi. Agent coi đây là tín hiệu thoát."),
            (3, "Vùng nền xanh", "Khoảng thời gian agent giữ vị thế (mô phỏng). Agent chỉ mua rồi thoát, không bán khống."),
        ])
        d.rounded_rectangle((U(1030), U(630), U(1540), U(790)), radius=U(20), fill=(244, 238, 226, 255), outline=(214, 140, 40, 255), width=U(2))
        for k, ln in enumerate(_wrap("Chỉ là mô phỏng. Agent hiện chạy thử (paper / demo), không phải khuyến nghị.", 24, 450, True)):
            _t(d, 1052, 676 + k * 32, ln, 24, (120, 76, 14), True)
    return _place(card, 0, 34, 0.30, 30, 14)


def make_chart_reading(p=None):
    return _annotated_chart("reading", p)


def make_chart_signals(p=None):
    return _annotated_chart("signals", p)


def make_candle_anatomy(p=None) -> Image.Image:
    W, H = 1720, 900
    card, d = _explain_card("Một cây nến cho biết điều gì?", "Mỗi cây nến là giá trong 1 giờ, với 4 mức giá: mở, đóng, cao nhất, thấp nhất.", W, H)
    def candle(cx, top, bt, bb, bot, col, labels, side):
        d.line((U(cx), U(top), U(cx), U(bot)), fill=col + (255,), width=U(9))
        d.rounded_rectangle((U(cx - 70), U(bt), U(cx + 70), U(bb)), radius=U(8), fill=col + (255,))
        for y, txt, sub in labels:
            if side == "l":
                d.line((U(cx - 90), U(y), U(cx - 230), U(y)), fill=INK + (255,), width=U(3))
                d.ellipse((U(cx - 96), U(y - 7), U(cx - 82), U(y + 7)), fill=INK + (255,))
                _t(d, cx - 246, y + 4, txt, 30, INK, True, "rs")
                _t(d, cx - 246, y + 34, sub, 21, MUTED, False, "rs")
            else:
                d.line((U(cx + 90), U(y), U(cx + 230), U(y)), fill=INK + (255,), width=U(3))
                d.ellipse((U(cx + 82), U(y - 7), U(cx + 96), U(y + 7)), fill=INK + (255,))
                _t(d, cx + 246, y + 4, txt, 30, INK, True, "ls")
                _t(d, cx + 246, y + 34, sub, 21, MUTED, False, "ls")
    candle(580, 200, 330, 590, 700, UP, [(200, "Giá cao nhất", "mức cao nhất trong giờ"), (330, "Giá đóng", "giá lúc hết giờ"),
                                         (590, "Giá mở", "giá lúc bắt đầu giờ"), (700, "Giá thấp nhất", "mức thấp nhất trong giờ")], "l")
    candle(1140, 200, 330, 590, 700, DOWN, [(200, "Giá cao nhất", "mức cao nhất trong giờ"), (330, "Giá mở", "giá lúc bắt đầu giờ"),
                                            (590, "Giá đóng", "giá lúc hết giờ"), (700, "Giá thấp nhất", "mức thấp nhất trong giờ")], "r")
    # shared parts, named once on the green candle (right side, in the gap)
    d.line((U(655), U(460), U(750), U(460)), fill=INK + (255,), width=U(3))
    _t(d, 762, 456, "Thân nến", 28, INK, True)
    _t(d, 762, 488, "từ giá mở đến giá đóng", 21, MUTED)
    d.line((U(590), U(265), U(750), U(265)), fill=INK + (255,), width=U(3))
    _t(d, 762, 262, "Bóng nến", 28, INK, True)
    _t(d, 762, 294, "mức giá đã chạm tới", 21, MUTED)
    _pill(d, 440, 750, 720, 810, fill=tuple(int(c * 0.14 + 255 * 0.86) for c in UP) + (255,), outline=UP + (255,), width=3)
    _t(d, 580, 790, "NẾN TĂNG", 30, UP, True, "ms")
    _pill(d, 1000, 750, 1280, 810, fill=tuple(int(c * 0.14 + 255 * 0.86) for c in DOWN) + (255,), outline=DOWN + (255,), width=3)
    _t(d, 1140, 790, "NẾN GIẢM", 30, DOWN, True, "ms")
    _t(d, 580, 846, "giá đóng cao hơn giá mở", 24, MUTED, False, "ms")
    _t(d, 1140, 846, "giá đóng thấp hơn giá mở", 24, MUTED, False, "ms")
    return _place(card, 0, 34, 0.30, 30, 14)


def make_pipeline_explained(p=None) -> Image.Image:
    W, H = 1700, 720
    card, d = _explain_card("Agent Trading làm việc thế nào?", "Năm bước, lặp lại mỗi giờ khi có nến mới đóng.", W, H)
    steps = [("Nến công khai", "candle", (47, 110, 214), "Lấy giá theo giờ từ sàn. Ai cũng xem được."),
             ("Tín hiệu SMA", "cross", (214, 140, 40), "So hai đường trung bình để tìm điểm vào và thoát."),
             ("Cổng rủi ro", "shield", (18, 122, 82), "Kiểm tra giới hạn vị thế trước khi làm gì tiếp."),
             ("Lệnh thử", "order", (96, 82, 170), "Đặt lệnh giả lập (paper / demo), không dùng tiền thật."),
             ("Sổ cái", "ledger", (16, 28, 48), "Ghi lại mọi bước để xem lại sau.")]
    cw, gap, top, chh = 262, 56, 150, 400
    x0 = (W - (5 * cw + 4 * gap)) / 2
    for i, (name, kind, col, desc) in enumerate(steps):
        x = x0 + i * (cw + gap)
        shadow = Image.new("L", card.size, 0)
        ImageDraw.Draw(shadow).rounded_rectangle((U(x), U(top), U(x + cw), U(top + chh)), radius=U(26), fill=255)
        card = shadow_under(card, shadow, 10, 0.22, 3, 8)
        gy = np.mgrid[0:U(chh), 0:U(cw)][0].astype(np.float32) / U(chh)
        g = lerp(tuple(min(255, c + 40) for c in col), tuple(int(c * 0.78) for c in col), gy)
        cm = Image.new("L", (U(cw), U(chh)), 0)
        ImageDraw.Draw(cm).rounded_rectangle((0, 0, U(cw) - 1, U(chh) - 1), radius=U(26), fill=255)
        card.alpha_composite(to_img(g, np.asarray(cm)), (U(x), U(top)))
        d = ImageDraw.Draw(card)
        d.ellipse((U(x + cw / 2 - 62), U(top + 26), U(x + cw / 2 + 62), U(top + 150)), fill=tuple(int(c * 0.80 + 255 * 0.20) for c in col) + (255,), outline=(255, 255, 255, 170), width=U(3))
        _glyph(d, kind, x + cw / 2, top + 88, 55)
        _t(d, x + cw / 2, top + 206, f"{i + 1}. {name}", 29, (255, 255, 255), True, "ms")
        for k, ln in enumerate(_wrap(desc, 24, cw - 36)):
            _t(d, x + cw / 2, top + 258 + k * 34, ln, 24, (255, 255, 255), False, "ms")
        if i < 4:
            ax = x + cw + 6
            d.line((U(ax), U(top + 270), U(ax + gap - 24), U(top + 270)), fill=INK + (255,), width=U(6))
            d.polygon([(U(ax + gap - 24), U(top + 254)), (U(ax + gap - 4), U(top + 270)), (U(ax + gap - 24), U(top + 286))], fill=INK + (255,))
    _pill(d, W / 2 - 330, 590, W / 2 + 330, 650, fill=(244, 238, 226, 255), outline=(214, 140, 40, 255), width=2)
    _t(d, W / 2, 630, NOTE, 26, (120, 76, 14), True, "ms")
    return _place(card, 0, 34, 0.30, 30, 14)


ASSETS = [
    ("agtr-ill-chart-001", make_chart_card, "Biểu đồ nến minh họa, đường SMA 5 và SMA 20", "biểu đồ nến", "chart.candles"),
    ("agtr-ill-monitor-001", make_monitor, "Màn hình theo dõi Agent Trading minh họa", "màn hình", "device.monitor"),
    ("agtr-ill-shield-001", make_shield, "Huy hiệu cổng rủi ro minh họa", "cổng rủi ro", "badge.risk"),
    ("agtr-ill-pipeline-001", make_pipeline, "Chuỗi xử lý của Agent Trading minh họa", "chuỗi xử lý", "diagram.pipeline"),
    ("agtr-exp-chart-001", make_chart_reading, "Giải thích cách đọc biểu đồ nến (đánh số từng phần)", "giải thích biểu đồ nến", "explainer.chart"),
    ("agtr-exp-signal-001", make_chart_signals, "Giải thích tín hiệu vào và thoát của SMA", "giải thích tín hiệu sma", "explainer.signal"),
    ("agtr-exp-candle-001", make_candle_anatomy, "Giải thích một cây nến: giá mở, đóng, cao nhất, thấp nhất", "giải thích cây nến", "explainer.candle"),
    ("agtr-exp-pipeline-001", make_pipeline_explained, "Giải thích năm bước làm việc của Agent Trading", "giải thích chuỗi xử lý", "explainer.pipeline"),
    ("agtr-exp-shield-001", lambda: make_shield({"explain": True}), "Giải thích cổng rủi ro", "giải thích cổng rủi ro", "explainer.risk"),
]


def build(repo: Path, out: Path, font_regular: Path, font_bold: Path, only: str = "") -> dict:
    sys.path.insert(0, str(repo / "tools" / "kit-video-production"))
    from kit_video.library import average_hash, file_sha256, hamming, load_catalog

    base.FONT_PATHS["regular"], base.FONT_PATHS["bold"] = font_regular, font_bold
    out.mkdir(parents=True, exist_ok=True)
    catalog = load_catalog(repo / "proof" / "kit-video-production-v1" / "library" / "catalog.json")
    try:
        rel = out.resolve().relative_to(repo.resolve()).as_posix()
    except ValueError:
        rel = out.as_posix()
    rows, hashes = [], {}
    for asset_id, fn, label, noun, category in ASSETS:
        if only and not asset_id.startswith(only):
            continue
        img = finish(fn(), pad=60)
        path = out / f"{asset_id}.png"
        img.save(path, optimize=True)
        digest, dh = file_sha256(path), average_hash(path)
        hashes[asset_id] = dh
        rows.append({
            "assetId": asset_id, "brand": "AgentTrading", "kind": "image", "category": category,
            "keywords": [noun, noun.split()[0], "agent trading", "minh họa", "second brain"],
            "style": "code-illustration", "width": img.width, "height": img.height, "aspect": round(img.width / img.height, 4),
            "source": "first-party", "license": "Dự án tự vẽ bằng code (Pillow). Không phải ảnh chụp, không phải dữ liệu thị trường.",
            "approval": "proposed", "path": f"{rel}/{asset_id}.png", "pathBase": "repo", "sha256": digest, "averageHash": dh,
            "similarityGroup": asset_id.rsplit("-", 1)[0], "parentId": None, "derived": False, "usage": [], "allow": ["explainer"],
            "limits": [
                "Hình minh họa do code vẽ. Số liệu và đường giá là chuỗi giả lập có hạt giống cố định, không phải dữ liệu thị trường.",
                "Chỉ dùng trong video kiến thức về Agent Trading. Video giao dịch thật phải dùng nến đọc từ file OHLCV đã kiểm.",
                "Agent hiện chỉ chạy paper/demo. Không dùng hình này để gợi ý có lệnh thật, lợi nhuận hay khuyến nghị.",
                "Giữ nhãn Dữ liệu minh họa và dòng Paper / Demo — không phải khuyến nghị khi đưa vào phim.",
                f"Chữ vẽ bằng font {font_bold.name} / {font_regular.name}.",
            ],
            "label": label, "role": "illustration", "visualSource": "",
            "usageRights": f"{label}: hình vẽ bằng code do dự án tự tạo, dữ liệu giả lập, chỉ dùng minh họa. Chưa được duyệt.",
            "demoData": True, "demoLabel": TAG,
        })
    ids = list(hashes)
    pair = {f"{a} / {b}": hamming(hashes[a], hashes[b]) for i, a in enumerate(ids) for b in ids[i + 1:]}
    near = {a: min(hamming(hashes[a], int(c["averageHash"])) for c in catalog if c.get("averageHash")) for a in ids}
    (out / "candidates.json").write_text(json.dumps({"assets": rows}, ensure_ascii=False, indent=2), encoding="utf-8")
    run = {"generator": "trading_props.py", "createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"), "pillow": PIL_VERSION,
           "chartSeed": good_seed(), "only": only, "pairwiseHamming": pair, "minToCatalog": near}
    (out / "generator-run.json").write_text(json.dumps(run, ensure_ascii=False, indent=2), encoding="utf-8")
    return run


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--font-regular", default="C:/Windows/Fonts/segoeui.ttf")
    ap.add_argument("--font-bold", default="C:/Windows/Fonts/segoeuib.ttf")
    ap.add_argument("--only", default="", help="build only assets whose id starts with this prefix")
    a = ap.parse_args()
    repo = Path(a.repo)
    out = Path(a.out) if Path(a.out).is_absolute() else repo / a.out
    run = build(repo, out, Path(a.font_regular), Path(a.font_bold), a.only)
    print(json.dumps({"pair": run["pairwiseHamming"], "catalog": run["minToCatalog"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
