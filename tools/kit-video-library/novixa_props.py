"""NOVIXA illustrative product props, drawn with Pillow. No network, no paid API.

These are illustrations drawn by code for a fictional product line. They are not
photographs of a real product and NOVIXA is not presented as a drug maker. Every
asset is written as `approval: proposed`; only a person may change that.

Run:
    python novixa_props.py --repo E:/KitPlatform --out proof/kit-video-library/<run>
Light comes from the upper left in every object, so the set reads as one family.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, __version__ as PIL_VERSION

SS = 2                      # supersampling: draw at 2x, then reduce with LANCZOS
CW = CH = 1800              # final working canvas before the crop
TEAL, TEAL_D, TEAL_L = (31, 138, 125), (20, 98, 89), (126, 205, 190)
PEACH, CREAM, INK = (240, 138, 93), (250, 246, 238), (35, 64, 58)
BRAND = "NOVIXA"
PRODUCT = "MẪU 01"
PRODUCT_SUB = "Sản phẩm giả lập"
DISCLAIMER = ("SẢN PHẨM MINH HỌA —", "KHÔNG PHẢI THUỐC THẬT")
DISCLAIMER_FULL = "SẢN PHẨM MINH HỌA — KHÔNG PHẢI THUỐC THẬT"
ILLUSTRATION_LABEL = "Dữ liệu minh họa"
LIGHT = np.array([-0.50, 0.50, 0.70])
LIGHT = LIGHT / np.linalg.norm(LIGHT)   # x (left is negative), up, toward the viewer

FONT_PATHS = {"regular": None, "bold": None}


def U(v: float) -> int:
    return int(round(v * SS))


def font(size: float, bold: bool = False) -> ImageFont.FreeTypeFont:
    path = FONT_PATHS["bold" if bold else "regular"]
    return ImageFont.truetype(str(path), U(size))


def canvas(w: int = CW, h: int = CH) -> Image.Image:
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def lerp(a, b, t):
    a = np.asarray(a, float)
    b = np.asarray(b, float)
    return a + (b - a) * np.asarray(t)[..., None]


def to_img(arr: np.ndarray, alpha: np.ndarray | None = None) -> Image.Image:
    rgb = np.clip(arr, 0, 255).astype(np.uint8)
    if alpha is None:
        alpha = np.full(rgb.shape[:2], 255, np.uint8)
    return Image.fromarray(np.dstack([rgb, np.clip(alpha, 0, 255).astype(np.uint8)]), "RGBA")


def width_of(fnt, text: str, spacing: float) -> float:
    return sum(fnt.getlength(c) for c in text) + U(spacing) * max(0, len(text) - 1)


def spaced_text(draw, x, y, text, fnt, fill, spacing=0.0, anchor="l"):
    """Baseline text with letter spacing. x and y are working pixels."""
    w = width_of(fnt, text, spacing)
    if anchor == "m":
        x -= w / 2
    elif anchor == "r":
        x -= w
    for ch in text:
        draw.text((x, y), ch, font=fnt, fill=fill, anchor="ls")
        x += fnt.getlength(ch) + U(spacing)


def shadow_under(img: Image.Image, mask: Image.Image, blur, opacity, dx=0, dy=0, color=(18, 28, 26)):
    sh = Image.new("RGBA", img.size, color + (0,))
    alpha = mask.filter(ImageFilter.GaussianBlur(U(blur))).point(lambda v: int(v * opacity))
    sh.putalpha(alpha)
    sh = ImageChops.offset(sh, U(dx), U(dy))
    return Image.alpha_composite(sh, img)


def noise(shape, amp, seed, smooth=0.0):
    rng = np.random.default_rng(seed)
    n = rng.normal(0, amp, shape)
    if smooth:
        r = int(smooth * 3) + 1
        k = np.exp(-0.5 * (np.arange(-r, r + 1) / smooth) ** 2)
        k /= k.sum()
        n = np.apply_along_axis(lambda v: np.convolve(v, k, mode="same"), 0, n)
    return n


def rounded(size, radius, fill=255):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=fill)
    return m


def disclaimer_block(draw, cx, top, width, height, size, color, border=3, fill=None):
    draw.rectangle((cx - width // 2, top, cx + width // 2, top + height), outline=color, width=U(border), fill=fill)
    f = font(size, True)
    spaced_text(draw, cx, top + int(height * 0.43), DISCLAIMER[0], f, color, 0.6, "m")
    spaced_text(draw, cx, top + int(height * 0.84), DISCLAIMER[1], f, color, 0.6, "m")


# ---------------------------------------------------------------------------
# Box
# ---------------------------------------------------------------------------

def _box_front(w, h, v):
    k = min(w / U(620), h / U(800) * 1.1, 1.1)
    layout, name = v["layout"], v["name"]
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    base = lerp((255, 252, 246), (232, 226, 215), (x / w * 0.45 + y / h * 0.55))
    arr = base.copy()
    sweep = np.clip(1 - np.abs((x + y * 0.6) - w * 0.62) / (w * 0.22), 0, 1) * 9   # soft diagonal gloss
    if layout == "split":
        head_h = int(h * 0.30)
        head = lerp(TEAL_L, TEAL_D, np.clip(x / w * 0.7 + y / head_h * 0.3, 0, 1))
        head = lerp(head, (255, 255, 255), np.clip(0.10 - (y / head_h) * 0.10, 0, 1))
        arr[:head_h] = head[:head_h]
        arr[head_h: head_h + U(7)] = PEACH
    elif layout == "full":
        arr = lerp(TEAL, TEAL_D, np.clip(x / w * 0.6 + y / h * 0.5, 0, 1))
        arr = lerp(arr, (255, 255, 255), np.clip(0.14 - (y / h) * 0.20, 0, 1))
    elif layout == "low":
        band = int(h * 0.70)
        bnd = lerp(TEAL_L, TEAL_D, np.clip(x / w * 0.7 + (y - band) / (h - band) * 0.3, 0, 1))
        arr[band:] = bnd[band:]
        arr[band - U(7): band] = PEACH
    arr += sweep[..., None]
    arr += noise((h, w, 1), 2.0, 11)
    im = to_img(arr)
    d = ImageDraw.Draw(im)
    if layout == "split":
        spaced_text(d, w // 2, int(h * 0.185), BRAND, font(120 * k, True), (255, 255, 255), 16 * k, "m")
        spaced_text(d, w // 2, int(h * 0.485), name, font(120 * k, True), INK, 8 * k, "m")
        spaced_text(d, w // 2, int(h * 0.55), PRODUCT_SUB, font(40 * k), (70, 96, 90), 2, "m")
        d.rectangle((w // 2 - U(60 * k), int(h * 0.60), w // 2 + U(60 * k), int(h * 0.60) + U(5)), fill=PEACH)
        disclaimer_block(d, w // 2, int(h * 0.80), w - U(90 * k), int(h * 0.125), 27 * k, INK)
    elif layout == "full":
        spaced_text(d, w // 2, int(h * 0.17), BRAND, font(120 * k, True), (255, 255, 255), 16 * k, "m")
        d.rectangle((w // 2 - U(60 * k), int(h * 0.215), w // 2 + U(60 * k), int(h * 0.215) + U(5)), fill=PEACH)
        px0, px1, py0, py1 = int(w * 0.09), int(w * 0.91), int(h * 0.32), int(h * 0.66)
        d.rounded_rectangle((px0, py0, px1, py1), radius=U(26 * k), fill=(252, 248, 240, 255))
        spaced_text(d, w // 2, int(h * 0.48), name, font(110 * k, True), INK, 8 * k, "m")
        spaced_text(d, w // 2, int(h * 0.56), PRODUCT_SUB, font(38 * k), (70, 96, 90), 2, "m")
        disclaimer_block(d, w // 2, int(h * 0.76), w - U(90 * k), int(h * 0.125), 27 * k, (255, 255, 255))
    else:  # low
        spaced_text(d, w // 2, int(h * 0.22), name, font(120 * k, True), INK, 8 * k, "m")
        spaced_text(d, w // 2, int(h * 0.29), PRODUCT_SUB, font(40 * k), (70, 96, 90), 2, "m")
        d.rectangle((w // 2 - U(60 * k), int(h * 0.33), w // 2 + U(60 * k), int(h * 0.33) + U(5)), fill=PEACH)
        disclaimer_block(d, w // 2, int(h * 0.43), w - U(90 * k), int(h * 0.125), 27 * k, INK)
        spaced_text(d, w // 2, int(h * 0.865), BRAND, font(120 * k, True), (255, 255, 255), 16 * k, "m")
    d.rectangle((0, 0, U(4), h), fill=(255, 255, 255, 110))            # lit left edge
    d.rectangle((0, 0, w, U(4)), fill=(255, 255, 255, 120))            # lit top edge
    d.rectangle((w - U(3), 0, w, h), fill=(30, 50, 46, 50))
    d.rectangle((0, h - U(3), w, h), fill=(30, 50, 46, 60))
    return im


def _box_side(w, h, name=PRODUCT, k=1.0):
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    arr = lerp(TEAL, TEAL_D, np.clip(x / w * 0.85 + 0.10, 0, 1))
    arr = lerp(arr, (0, 0, 0), np.clip(x / w * 0.18, 0, 1))
    arr += noise((h, w, 1), 2.0, 12)
    im = to_img(arr)
    layer = Image.new("RGBA", (h, w), (0, 0, 0, 0))              # vertical text drawn rotated
    d = ImageDraw.Draw(layer)
    spaced_text(d, h // 2, int(w * 0.43), BRAND, font(78 * k, True), (255, 255, 255), 14 * k, "m")
    spaced_text(d, h // 2, int(w * 0.68), name, font(40 * k, True), (205, 235, 228), 6 * k, "m")
    im = Image.alpha_composite(im, layer.rotate(90, expand=True))
    d = ImageDraw.Draw(im)
    d.rectangle((0, 0, U(4), h), fill=(255, 255, 255, 70))
    return im


def _box_top(w, d_):
    y, x = np.mgrid[0:d_, 0:w].astype(np.float32)
    arr = lerp((255, 254, 250), (240, 234, 224), np.clip(x / w * 0.6 + y / d_ * 0.4, 0, 1))
    arr += noise((d_, w, 1), 2.0, 13)
    im = to_img(arr)
    dr = ImageDraw.Draw(im)
    dr.line((0, int(d_ * 0.62), w, int(d_ * 0.62)), fill=(205, 198, 186), width=U(3))   # tuck flap fold
    dr.line((0, int(d_ * 0.62) + U(3), w, int(d_ * 0.62) + U(3)), fill=(255, 255, 255), width=U(2))
    dr.arc((w // 2 - U(48), int(d_ * 0.62) - U(24), w // 2 + U(48), int(d_ * 0.62) + U(24)), 0, 180, fill=(205, 198, 186), width=U(3))
    return im


def _shear(tex: Image.Image, origin, dx, dy, out_size, mode):
    """Place a flat texture on an oblique face. mode: side or top. All in working pixels."""
    sw, sh = tex.size
    ox, oy = origin
    if mode == "side":
        k = dy / dx
        coef = (sw / dx, 0, 0, k, 1, 0)
        out = tex.transform(out_size, Image.AFFINE, coef, resample=Image.BICUBIC)
    else:
        k = dx / dy
        coef = (1, k, 0, 0, sh / dy, 0)
        out = tex.transform(out_size, Image.AFFINE, coef, resample=Image.BICUBIC)
    return out


BOX_DEFAULT = dict(layout="split", name=PRODUCT, W=620, H=800, DX=230, DY=95, X0=470, Y0=640)


def make_box(v=None) -> Image.Image:
    v = {**BOX_DEFAULT, **(v or {})}
    X0, Y0, Wf, Hf, DX, DY = U(v["X0"]), U(v["Y0"]), U(v["W"]), U(v["H"]), U(v["DX"]), U(v["DY"])
    X1, Y1 = X0 + Wf, Y0 + Hf
    im = canvas()
    front, side, top = _box_front(Wf, Hf, v), _box_side(U(260), Hf, v['name'], min(1.0, v['DX'] / 230)), _box_top(Wf, U(190))
    # side face: dest (x, y) -> src (u, v); rectangle sheared up by DY toward the back
    side_out = Image.new("RGBA", (DX, Hf + DY), (0, 0, 0, 0))
    k = DY / DX
    sd = side.transform((DX, Hf + DY), Image.AFFINE, (side.size[0] / DX, 0, 0, k, 1, -DY), resample=Image.BICUBIC)
    # top face: parallelogram from the front-top edge back to (x+DX, y-DY)
    k2 = DX / DY
    tp = top.transform((Wf + DX, DY), Image.AFFINE, (1, k2, -DX, 0, top.size[1] / DY, 0), resample=Image.BICUBIC)
    sil = Image.new("L", im.size, 0)
    dm = ImageDraw.Draw(sil)
    dm.polygon([(X0, Y0), (X0 + DX, Y0 - DY), (X1 + DX, Y0 - DY), (X1 + DX, Y1 - DY), (X1, Y1), (X0, Y1)], fill=255)
    # floor shadow: cast to the right and back, plus a tight contact line
    cast = Image.new("L", im.size, 0)
    ImageDraw.Draw(cast).polygon([(X0 + U(30), Y1), (X1, Y1), (X1 + DX, Y1 - DY), (X1 + DX + U(210), Y1 - DY + U(26)),
                                  (X1 + U(150), Y1 + U(38)), (X0 + U(60), Y1 + U(30))], fill=255)
    im = shadow_under(im, cast, 46, 0.34)
    cont = Image.new("L", im.size, 0)
    ImageDraw.Draw(cont).polygon([(X0, Y1 - U(4)), (X1, Y1 - U(4)), (X1 + DX, Y1 - DY - U(4)), (X1 + DX, Y1 - DY + U(14)),
                                  (X1, Y1 + U(14)), (X0, Y1 + U(14))], fill=255)
    im = shadow_under(im, cont, 12, 0.55)
    im.alpha_composite(sd, (X1, Y0 - DY))
    im.alpha_composite(front, (X0, Y0))
    im.alpha_composite(tp, (X0, Y0 - DY))
    d = ImageDraw.Draw(im)
    d.line((X1, Y0, X1, Y1), fill=(255, 255, 255, 140), width=U(2))           # front/side crease
    d.line((X0, Y0, X1, Y0), fill=(255, 255, 255, 200), width=U(3))           # front/top crease
    return im


# ---------------------------------------------------------------------------
# Bottle
# ---------------------------------------------------------------------------

def _profile(y, ground, R, rn, shoulder_top, shoulder_h):
    """Half width of an amber bottle at row y (final pixel units)."""
    rb = 44.0
    y = np.asarray(y, float)
    hw = np.full_like(y, R)
    bot = y > ground - rb
    dyb = np.clip(y - (ground - rb), 0, rb)
    hw = np.where(bot, R - rb + np.sqrt(np.maximum(rb * rb - dyb * dyb, 0)), hw)
    sh = (y < shoulder_top) & (y >= shoulder_top - shoulder_h)
    t = np.clip((shoulder_top - y) / shoulder_h, 0, 1)
    hw = np.where(sh, rn + (R - rn) * np.sqrt(np.maximum(1 - t * t, 0)), hw)
    hw = np.where(y < shoulder_top - shoulder_h, rn, hw)
    return hw


def _cylinder_shading(nx, up=0.0, spec_pow=26):
    nz = np.sqrt(np.clip(1 - nx * nx, 0, 1))
    cos_phi = np.sqrt(np.clip(1 - up * up, 0, 1))
    n = np.stack([nx * cos_phi, np.full_like(nx, 0) + up, nz * cos_phi], -1)
    dif = np.clip(n @ LIGHT, 0, 1)
    h = LIGHT + np.array([0, 0, 1.0])
    h = h / np.linalg.norm(h)
    spec = np.clip(n @ h, 0, 1) ** spec_pow
    return dif, spec, nz


def _label_texture(w, h, name=PRODUCT, k=1.0, dark=False):
    im = Image.new("RGBA", (w, h), CREAM + (255,))
    arr = np.asarray(im).astype(np.float32)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    arr[..., :3] = lerp((255, 252, 246), (238, 232, 222), x / w)
    head = int(h * 0.28)
    arr[:head, :, :3] = lerp(TEAL_L, TEAL_D, np.clip(x[:head] / w * 0.7 + y[:head] / head * 0.3, 0, 1))
    arr[head: head + U(6), :, :3] = PEACH
    if dark:
        arr[head + U(6):, :, :3] = lerp(TEAL, TEAL_D, np.clip(x[head + U(6):] / w * 0.8, 0, 1))
    arr[..., :3] += noise((h, w, 1), 1.6, 21)
    im = to_img(arr[..., :3])
    ink, sub = ((255, 255, 255), (205, 235, 228)) if dark else (INK, (70, 96, 90))
    d = ImageDraw.Draw(im)
    spaced_text(d, w // 2, int(h * 0.185), BRAND, font(62 * k, True), (255, 255, 255), 9 * k, "m")
    spaced_text(d, w // 2, int(h * 0.55), name, font(66 * k, True), ink, 5 * k, "m")
    spaced_text(d, w // 2, int(h * 0.65), PRODUCT_SUB, font(25 * k), sub, 1.5, "m")
    disclaimer_block(d, w // 2, int(h * 0.755), int(w * 0.80), int(h * 0.19), 17.5 * k, ink, 2.5)
    return im


BOTTLE_DEFAULT = dict(
    name=PRODUCT, R=190.0, rn=128.0, rc=148.0, shoulder_top=840.0, shoulder_h=118.0, cap_top=520.0, cap_bot=725.0,
    lab_top=960, lab_bot=1335, glass=False,
    pal=((92, 44, 12), (170, 92, 24), (232, 158, 64)), glow=(255, 188, 92),
    cap=((16, 80, 72), (96, 190, 174)), cap_rim=(8, 40, 36), cap_top_col=(110, 205, 188))


def make_bottle(v=None) -> Image.Image:
    v = {**BOTTLE_DEFAULT, **(v or {})}
    cx, ground = 900, 1470
    R, rn, rc = v["R"], v["rn"], v["rc"]
    shoulder_top, shoulder_h = v["shoulder_top"], v["shoulder_h"]
    neck_top = shoulder_top - shoulder_h
    cap_top, cap_bot = v["cap_top"], v["cap_bot"]
    e = 17.0                                    # slight view from above: smile curves
    im = canvas()
    y0, y1 = U(cap_top) - U(40), U(ground) + U(40)
    rows = np.arange(y0, y1)
    yf = rows / SS
    hw = _profile(yf, ground, R, rn, shoulder_top, shoulder_h) * SS
    xs = np.arange(-U(R) - 3, U(R) + 4)
    X, HW = np.meshgrid(xs, hw)
    inside = np.clip(HW - np.abs(X) + 0.5, 0, 1)
    in_body = (yf >= neck_top - 10)[:, None]
    nx = np.clip(X / np.maximum(HW, 1), -1, 1)
    slope = np.gradient(hw / SS, yf)
    up = np.clip(np.where((yf < shoulder_top) & (yf > neck_top), np.abs(slope) / np.sqrt(1 + slope ** 2), 0), 0, 0.92)
    dif, spec, nz = _cylinder_shading(nx, up[:, None] * np.ones_like(nx), 30)
    dark, mid, lite = (np.array(c, float) for c in v['pal'])
    b = np.clip(0.18 + 0.95 * dif, 0, 1)
    col = np.where(b[..., None] < 0.55, lerp(dark, mid, b / 0.55), lerp(mid, lite, (b - 0.55) / 0.45))
    glow = np.exp(-((nx - 0.52) / 0.22) ** 2) * 0.22                       # light passing through the glass
    col = lerp(col, v['glow'], glow)
    if v['glass']:                                                         # glass: darker edges, thin rim light on the far side
        col = lerp(col, dark * 0.5, np.clip(np.abs(nx) ** 3 * 0.6, 0, 0.6))
        col = lerp(col, (255, 236, 205), np.exp(-((nx - 0.90) / 0.035) ** 2) * 0.30)
    thick = np.clip((yf[:, None] - (ground - 70)) / 70, 0, 1) * 0.35        # thicker glass at the base
    col = lerp(col, (60, 28, 6), thick)
    streak = np.exp(-((nx + 0.60) / 0.075) ** 2) * np.clip(1 - np.abs(yf[:, None] - 1090) / 330, 0, 1) ** 0.6
    col = lerp(col, (255, 244, 220), np.clip(streak * 0.85, 0, 0.9))
    streak2 = np.exp(-((nx - 0.78) / 0.04) ** 2) * np.clip(1 - np.abs(yf[:, None] - 1100) / 300, 0, 1)
    col = lerp(col, (255, 214, 150), np.clip(streak2 * 0.35, 0, 0.5))
    shoulder_hl = np.exp(-((yf[:, None] - (shoulder_top - 72)) / 20) ** 2) * np.exp(-((nx + 0.25) / 0.45) ** 2)
    col = lerp(col, (255, 232, 190), np.clip(shoulder_hl * 0.35, 0, 0.45))
    alpha = (inside * 255) * in_body * (yf <= ground)[:, None]
    body = to_img(col, alpha)
    # label, wrapped around the cylinder
    th = 1.12
    lab_top, lab_bot = v['lab_top'], v['lab_bot']
    kl = min(R / 190.0, (lab_bot - lab_top) / 375.0)
    arc_w = int(2 * th * R * SS)
    tex = np.asarray(_label_texture(arc_w, U(lab_bot - lab_top), v['name'], kl, v.get('label_dark', False))).astype(np.float32)
    lx = xs[np.abs(xs) < U(R) * math.sin(th)]
    theta = np.arcsin(lx / (R * SS))
    src_col = np.clip(((theta / th + 1) / 2 * (arc_w - 1)).astype(int), 0, arc_w - 1)
    lab = tex[:, src_col, :]                                                # (rows, cols, 4)
    nxl = lx / (R * SS)
    dl, sl, _ = _cylinder_shading(nxl[None, :] * np.ones((1, 1)), 0.0, 40)
    shade = (0.50 + 0.62 * dl)[0]
    lab[..., :3] = lab[..., :3] * shade[None, :, None] + 255 * (sl[0] * 0.14)[None, :, None]
    lab[..., 3] = 255 * np.clip((1 - np.abs(nxl) * 0.0), 0, 1)
    label_img = to_img(lab[..., :3], lab[..., 3])
    body.alpha_composite(label_img, (int(lx[0] - xs[0]), U(lab_top) - y0))
    ld = ImageDraw.Draw(body)
    lx0, lx1 = int(lx[0] - xs[0]), int(lx[-1] - xs[0])
    for yy in (U(lab_top) - y0, U(lab_bot) - y0):                          # label edges
        ld.line((lx0, yy, lx1, yy), fill=(0, 0, 0, 40), width=U(1.2))
    # smile: shift each column down by e * nz
    arr = np.asarray(body)
    out = np.zeros_like(arr)
    shift = (U(e) * np.sqrt(np.clip(1 - (xs / (R * SS)) ** 2, 0, 1))).astype(int)
    for i, s in enumerate(shift):
        col_ = arr[:, i]
        out[s:, i] = col_[: len(col_) - s] if s else col_
    body = Image.fromarray(out, "RGBA")
    # cap
    ch = int((cap_bot - cap_top) * SS)
    ccx = np.arange(-U(rc) - 3, U(rc) + 4)
    cy = np.arange(ch)
    CX, CY = np.meshgrid(ccx, cy)
    nxc = np.clip(CX / (rc * SS), -1, 1)
    difc, specc, nzc = _cylinder_shading(nxc, 0.0, 22)
    base_c = lerp(v['cap'][0], v['cap'][1], np.clip(0.12 + 1.0 * difc, 0, 1))
    ribs = 1 + 0.085 * np.cos(np.arcsin(nxc) * 46) * (CY > ch * 0.18)
    base_c = base_c * ribs[..., None] + 255 * (specc * 0.22)[..., None]
    base_c = lerp(base_c, (255, 255, 255), np.exp(-((nxc + 0.58) / 0.07) ** 2) * 0.28)
    rim = np.clip((CY - (ch - U(16))) / U(16), 0, 1) * 0.30                    # shaded lower skirt
    base_c = lerp(base_c, v['cap_rim'], rim)
    calpha = np.clip((rc * SS - np.abs(CX) + 0.5), 0, 1) * 255
    cap = to_img(base_c, calpha)
    carr = np.asarray(cap)
    cout = np.zeros_like(carr)
    cshift = (U(e * 0.8) * np.sqrt(np.clip(1 - (ccx / (rc * SS)) ** 2, 0, 1))).astype(int)
    for i, s in enumerate(cshift):
        cc = carr[:, i]
        cout[s:, i] = cc[: len(cc) - s] if s else cc
    cap = Image.fromarray(cout, "RGBA")
    # compose with ground shadow
    cast = Image.new("L", im.size, 0)
    ImageDraw.Draw(cast).ellipse((U(cx - 160), U(ground - 12), U(cx + 330), U(ground + 52)), fill=255)
    im = shadow_under(im, cast, 30, 0.38)
    cont = Image.new("L", im.size, 0)
    ImageDraw.Draw(cont).ellipse((U(cx - R + 8), U(ground - 10), U(cx + R - 8), U(ground + 34)), fill=255)
    im = shadow_under(im, cont, 9, 0.60)
    im.alpha_composite(body, (U(cx) + int(xs[0]), y0))
    im.alpha_composite(cap, (U(cx) + int(ccx[0]), U(cap_top)))
    d = ImageDraw.Draw(im)
    top_y = U(cap_top)
    d.ellipse((U(cx - rc), top_y - U(e * 0.8), U(cx + rc), top_y + U(e * 0.8)), fill=v['cap_top_col'] + (255,))
    d.ellipse((U(cx - rc + 20), top_y - U(e * 0.8) + U(5), U(cx + rc - 20), top_y + U(e * 0.8) - U(5)), outline=(255, 255, 255, 70), width=U(2))
    d.arc((U(cx - rc), top_y - U(e * 0.8), U(cx + rc), top_y + U(e * 0.8)), 180, 360, fill=(255, 255, 255, 160), width=U(3))
    return im


# ---------------------------------------------------------------------------
# Blister
# ---------------------------------------------------------------------------

def _foil(w, h, radius):
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    base = lerp((196, 202, 204), (236, 240, 241), np.clip(x / w * 0.5 + (1 - y / h) * 0.5, 0, 1))
    streak = noise((h, 1, 1), 5.0, 31, smooth=1.5)
    base += streak
    base += np.clip(1 - np.abs((x - y * 0.5) - w * 0.45) / (w * 0.16), 0, 1)[..., None] * 16
    base += noise((h, w, 1), 2.2, 32)
    return to_img(base, np.asarray(rounded((w, h), radius)))


def _capsule(draw_img: Image.Image, cx, cy, length, thick, angle, ca, cb):
    layer = Image.new("RGBA", (int(length * 1.3), int(length * 1.3)), (0, 0, 0, 0))
    lw, lh = layer.size
    h = int(thick)
    L = int(length)
    arr = np.zeros((h, L, 4), np.float32)
    yy = np.linspace(-1, 1, h)[:, None] * np.ones((1, L))
    xx = np.arange(L)[None, :] * np.ones((h, 1))
    shade = 0.30 + 0.78 * np.clip((-yy * 0.62 + np.sqrt(np.clip(1 - yy * yy, 0, 1)) * 0.78), 0, 1)
    colr = np.where((xx < L / 2)[..., None], np.array(ca, float), np.array(cb, float))
    arr[..., :3] = colr * shade[..., None] + 255 * (np.exp(-((yy + 0.50) / 0.13) ** 2) * 0.34)[..., None]
    arr[..., :3] = lerp(arr[..., :3], (0, 0, 0), np.clip((np.abs(xx - L / 2) < U(1.2)) * 0.18, 0, 1))
    r = h / 2
    cap_mask = rounded((L, h), int(r))
    arr[..., 3] = np.asarray(cap_mask)
    cap_img = to_img(arr[..., :3], arr[..., 3])
    layer.alpha_composite(cap_img, ((lw - L) // 2, (lh - h) // 2))
    layer = layer.rotate(angle, resample=Image.BICUBIC)
    draw_img.alpha_composite(layer, (int(cx - lw / 2), int(cy - lh / 2)))


def _tablet(img: Image.Image, cx, cy, dia, angle, col):
    r = int(dia / 2)
    size = 2 * r + U(8)
    yy, xx = np.mgrid[0:size, 0:size].astype(np.float32)
    nx, ny = (xx - size / 2) / r, (yy - size / 2) / r
    d2 = nx * nx + ny * ny
    nz = np.sqrt(np.clip(1 - d2, 0, 1))
    n = np.stack([nx * 0.9, -ny * 0.9, nz * 0.8 + 0.25], -1)
    n = n / np.linalg.norm(n, axis=-1, keepdims=True)
    b = np.clip(0.30 + 0.80 * np.clip(n @ LIGHT, 0, 1), 0, 1.15)
    rgb = np.asarray(col, np.float32) * b[..., None] + 255 * (np.clip(n @ (LIGHT + [0, 0, 1.0]) / np.linalg.norm(LIGHT + [0, 0, 1.0]), 0, 1) ** 30 * 0.30)[..., None]
    alpha = np.clip((r - np.sqrt(d2) * r) + 0.5, 0, 1) * 255
    im = to_img(rgb, alpha)
    ln = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(ln).line((U(6), size / 2, size - U(6), size / 2), fill=(60, 70, 66, 120), width=U(3))
    ImageDraw.Draw(ln).line((U(6), size / 2 + U(3), size - U(6), size / 2 + U(3)), fill=(255, 255, 255, 110), width=U(2))
    im = Image.alpha_composite(im, ln.rotate(angle, resample=Image.BICUBIC))
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).ellipse((U(4), U(4), size - U(4), size - U(4)), fill=255)
    im.putalpha(ImageChops.multiply(im.getchannel("A"), mask))
    img.alpha_composite(im, (int(cx - size / 2), int(cy - size / 2)))


def _pocket(sheet: Image.Image, cx, cy, pw, ph, angle, style="capsule", cols=(TEAL, (246, 241, 230))):
    box = (int(cx - pw / 2 - U(40)), int(cy - ph / 2 - U(40)), int(cx + pw / 2 + U(40)), int(cy + ph / 2 + U(40)))
    w, h = box[2] - box[0], box[3] - box[1]
    local = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    pm = Image.new("L", (w, h), 0)
    pr = (w / 2 - pw / 2, h / 2 - ph / 2, w / 2 + pw / 2, h / 2 + ph / 2)
    ImageDraw.Draw(pm).rounded_rectangle(pr, radius=int(ph * 0.5), fill=255)
    sh = Image.new("RGBA", (w, h), (30, 40, 40, 0))
    sh.putalpha(ImageChops.offset(pm, U(11), U(15)).filter(ImageFilter.GaussianBlur(U(9))).point(lambda v: int(v * 0.42)))
    local = Image.alpha_composite(local, sh)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx - w / 2) / (pw / 2)) ** 2 + ((yy - h / 2) / (ph / 2)) ** 2)
    inner = lerp((158, 166, 168), (210, 216, 218), np.clip((yy - h / 2) / ph + 0.5, 0, 1))   # foil seen through the plastic
    inner = inner * (1 - 0.18 * np.clip(d, 0, 1.2)[..., None])
    local = Image.alpha_composite(local, to_img(inner, np.asarray(pm)))
    pill_sh = Image.new("RGBA", (w, h), (20, 30, 30, 0))
    pmask = Image.new("L", (w, h), 0)
    if style == "tablet":
        ImageDraw.Draw(pmask).ellipse((w / 2 - U(50), h / 2 - U(50), w / 2 + U(50), h / 2 + U(50)), fill=255)
    else:
        ImageDraw.Draw(pmask).rounded_rectangle((w / 2 - U(76), h / 2 - U(28), w / 2 + U(76), h / 2 + U(28)), radius=U(28), fill=255)
    pmask = pmask.rotate(angle, resample=Image.BICUBIC)
    pill_sh.putalpha(ImageChops.offset(pmask, U(8), U(11)).filter(ImageFilter.GaussianBlur(U(6))).point(lambda v: int(v * 0.40)))
    local = Image.alpha_composite(local, pill_sh)
    if style == "tablet":
        _tablet(local, w / 2, h / 2, U(100), angle, cols[0])
    else:
        _capsule(local, w / 2, h / 2, U(152), U(56), angle, cols[0], cols[1])
    # clear dome: soft white veil, rim light on the upper left, rim shade on the lower right
    veil = np.clip(1 - d, 0, 1) ** 0.8
    veil_rgb = np.dstack([np.full_like(d, 255)] * 3)
    local = Image.alpha_composite(local, to_img(veil_rgb, veil * 38 * (np.asarray(pm) > 0)))
    ring = Image.new("L", (w, h), 0)
    ImageDraw.Draw(ring).rounded_rectangle(pr, radius=int(ph * 0.5), outline=255, width=U(6))
    ring_a = np.asarray(ring.filter(ImageFilter.GaussianBlur(U(1.2)))).astype(np.float32) / 255
    tilt = np.clip(0.5 + 0.5 * (-(xx - w / 2) / (pw / 2) - (yy - h / 2) / (ph / 2)) * 0.8, 0, 1)
    ring_col = lerp((52, 66, 68), (255, 255, 255), tilt)
    local = Image.alpha_composite(local, to_img(ring_col, ring_a * (80 + 130 * tilt)))
    hi = Image.new("L", (w, h), 0)
    ImageDraw.Draw(hi).ellipse((w / 2 - pw * 0.36, h / 2 - ph * 0.40, w / 2 + pw * 0.06, h / 2 - ph * 0.22), fill=255)
    hi = hi.rotate(14, resample=Image.BICUBIC).filter(ImageFilter.GaussianBlur(U(2.2)))
    white = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    white.putalpha(hi.point(lambda v: int(v * 0.58)))
    local = Image.alpha_composite(local, white)
    spot = Image.new("L", (w, h), 0)
    ImageDraw.Draw(spot).ellipse((w / 2 + pw * 0.18, h / 2 + ph * 0.10, w / 2 + pw * 0.27, h / 2 + ph * 0.22), fill=255)
    white2 = Image.new("RGBA", (w, h), (255, 255, 255, 0))
    white2.putalpha(spot.filter(ImageFilter.GaussianBlur(U(2))).point(lambda v: int(v * 0.35)))
    local = Image.alpha_composite(local, white2)
    sheet.alpha_composite(local, (box[0], box[1]))


BLISTER_DEFAULT = dict(cols=5, rows=2, x0=215, y0=110, pw=196, ph=152, sx=216, sy=262, style="capsule",
                       pill=(TEAL, (246, 241, 230)), rot=6, name=PRODUCT, center=False)


def make_blister(v=None) -> Image.Image:
    v = {**BLISTER_DEFAULT, **(v or {})}
    cols, rows = v["cols"], v["rows"]
    x0, y0, pw, ph, sx, sy = (U(v[k]) for k in ("x0", "y0", "pw", "ph", "sx", "sy"))
    W = x0 + (cols - 1) * sx + pw + U(105)
    H = y0 + (rows - 1) * sy + ph + U(126)
    R = U(46)
    sheet = Image.new("RGBA", (W, H + U(26)), (0, 0, 0, 0))
    slab = Image.new("RGBA", (W, H + U(26)), (0, 0, 0, 0))
    slab_mask = Image.new("L", slab.size, 0)
    ImageDraw.Draw(slab_mask).rounded_rectangle((0, U(18), W - 1, H + U(25)), radius=R, fill=255)
    slab.paste(Image.new("RGBA", slab.size, (134, 142, 144, 255)), (0, 0), slab_mask)
    sheet = Image.alpha_composite(sheet, slab)
    foil = _foil(W, H, R)
    sheet.alpha_composite(foil, (0, 0))
    PERF = x0 + pw // 2 + ((cols - 1) // 2) * sx + sx // 2
    d = ImageDraw.Draw(sheet)
    d.rounded_rectangle((U(2), U(2), W - U(3), H - U(3)), radius=R, outline=(255, 255, 255, 150), width=U(3))
    # printed foil: wordmark down the left margin, disclaimer along the bottom margin
    tl = Image.new("RGBA", (H, U(90)), (0, 0, 0, 0))
    wm, wsz = f"{BRAND}  ·  {v['name']}", 44.0
    while width_of(font(wsz, True), wm, 10) > H - U(40):
        wsz -= 1
    spaced_text(ImageDraw.Draw(tl), H // 2, U(62), wm, font(wsz, True), TEAL_D + (210,), 10, "m")
    sheet.alpha_composite(tl.rotate(90, expand=True), (U(18), 0))
    dsz = 25.0
    while width_of(font(dsz, True), DISCLAIMER_FULL, 1.5) > W - U(215) - U(70):
        dsz -= 1
    half = width_of(font(dsz, True), DISCLAIMER_FULL, 1.5) / 2
    dcx = min(max(PERF, U(215) + half), W - U(70) - half)
    spaced_text(d, dcx, H - U(36), DISCLAIMER_FULL, font(dsz, True), TEAL_D + (225,), 1.5, "m")
    for r in range(rows):
        for c in range(cols):
            ang = ((r * cols + c) * 37 % 21) - 10
            _pocket(sheet, x0 + pw // 2 + c * sx, y0 + ph // 2 + r * sy, pw, ph, ang, v["style"], v["pill"])
    for yy in range(U(40), H - U(70), U(26)):                                  # perforation slits
        d.line((PERF, yy, PERF, yy + U(13)), fill=(120, 130, 132, 200), width=U(4))
        d.line((PERF + U(3), yy, PERF + U(3), yy + U(13)), fill=(255, 255, 255, 120), width=U(2))
    big = canvas()
    if v["center"]:
        if sheet.width > U(1500):
            sc = U(1500) / sheet.width
            sheet = sheet.resize((int(sheet.width * sc), int(sheet.height * sc)), Image.LANCZOS)
        ox, oy = (big.width - sheet.width) // 2, (big.height - sheet.height) // 2
        big.alpha_composite(sheet, (ox, oy))
        big = big.rotate(v["rot"], resample=Image.BICUBIC, center=(big.width // 2, big.height // 2))
    else:
        big.alpha_composite(sheet, (U(210), U(470)))
        big = big.rotate(v["rot"], resample=Image.BICUBIC, center=(U(900), U(870)))
    cast = big.getchannel("A")
    big = shadow_under(big, cast, 36, 0.40, 26, 46)
    contact = shadow_under(canvas(), big.getchannel("A").filter(ImageFilter.MinFilter(U(3) | 1)), 8, 0.30, 4, 10)
    return Image.alpha_composite(contact, big)


# ---------------------------------------------------------------------------

def finish(im: Image.Image, pad=70, square=0) -> Image.Image:
    """Reduce the supersampled canvas, crop to the object, zero hidden RGB in clear pixels."""
    im = im.resize((CW, CH), Image.LANCZOS)
    bbox = im.getchannel("A").point(lambda v: 255 if v > 3 else 0).getbbox()
    l, t, r, b = bbox
    im = im.crop((max(0, l - pad), max(0, t - pad), min(CW, r + pad), min(CH, b + pad)))
    if square:                         # one frame size per family: scale and silhouette stay comparable between variants
        side = max(square, max(im.size) + 40)
        frame = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        frame.alpha_composite(im, ((side - im.width) // 2, (side - im.height) // 2))
        im = frame
    arr = np.asarray(im).copy()
    arr[arr[..., 3] == 0] = 0
    return Image.fromarray(arr, "RGBA")


FRAME = {"make_box": 1300, "make_bottle": 1250, "make_blister": 1800}
W1, CR = (246, 241, 230), (250, 246, 238)
WHITE_PAL = ((120, 126, 124), (226, 229, 227), (252, 252, 250))
VARIANTS = [
    # id, maker, params, label, keywords. The 001 rows are the approved originals: params None.
    ("novixa-ill-box-001", make_box, None, "Hộp thuốc minh họa NOVIXA", "hộp thuốc"),
    ("novixa-ill-box-002", make_box, dict(layout="full", name="MẪU 02", W=560, H=820, DX=210, DY=100), "Hộp thuốc minh họa NOVIXA, nền xanh", "hộp thuốc"),
    ("novixa-ill-box-003", make_box, dict(layout="low", name="MẪU 03", W=760, H=560, DX=230, DY=90), "Hộp thuốc minh họa NOVIXA, hộp dẹt", "hộp thuốc"),
    ("novixa-ill-box-004", make_box, dict(layout="split", name="MẪU 04", W=700, H=620, DX=200, DY=95), "Hộp thuốc minh họa NOVIXA, hộp thon", "hộp thuốc"),
    ("novixa-ill-box-005", make_box, dict(layout="full", name="MẪU 05", W=700, H=700, DX=250, DY=110), "Hộp thuốc minh họa NOVIXA, hộp vuông", "hộp thuốc"),
    ("novixa-ill-box-006", make_box, dict(layout="low", name="MẪU 06", W=560, H=880, DX=200, DY=85), "Hộp thuốc minh họa NOVIXA, hộp cao", "hộp thuốc"),
    ("novixa-ill-bottle-001", make_bottle, None, "Lọ thuốc minh họa NOVIXA", "lọ thuốc"),
    ("novixa-ill-bottle-002", make_bottle, dict(name="MẪU 02", R=150.0, rn=105.0, rc=118.0, cap_top=380.0, cap_bot=560.0, shoulder_top=700.0,
                                                 shoulder_h=100.0, lab_top=830, lab_bot=1330, glass=True), "Lọ thuốc minh họa NOVIXA, lọ cao màu hổ phách", "lọ thuốc"),
    ("novixa-ill-bottle-003", make_bottle, dict(name="MẪU 03", R=235.0, rn=170.0, rc=190.0, cap_top=860.0, cap_bot=1010.0, shoulder_top=1130.0,
                                                 shoulder_h=120.0, lab_top=1150, lab_bot=1400, glass=True), "Lọ thuốc minh họa NOVIXA, lọ thấp màu hổ phách", "lọ thuốc"),
    ("novixa-ill-bottle-004", make_bottle, dict(name="MẪU 04", R=200.0, rn=135.0, rc=155.0, cap_top=440.0, cap_bot=690.0, shoulder_top=830.0,
                                                 lab_top=900, lab_bot=1360, pal=WHITE_PAL, glow=(255, 255, 255)), "Lọ thuốc minh họa NOVIXA, lọ nhựa trắng", "lọ thuốc"),
    ("novixa-ill-bottle-005", make_bottle, dict(name="MẪU 05", R=140.0, rn=100.0, rc=135.0, cap_top=560.0, cap_bot=760.0, shoulder_top=920.0,
                                                 shoulder_h=90.0, lab_top=960, lab_bot=1400, pal=WHITE_PAL, glow=(255, 255, 255)), "Lọ thuốc minh họa NOVIXA, lọ nhựa trắng nắp lớn", "lọ thuốc"),
    ("novixa-ill-bottle-006", make_bottle, dict(name="MẪU 06", R=115.0, rn=90.0, rc=102.0, cap_top=880.0, cap_bot=1000.0, shoulder_top=1090.0,
                                                 shoulder_h=60.0, lab_top=1110, lab_bot=1400, glass=True, pal=((10, 58, 40), (34, 128, 84), (130, 214, 156)),
                                                 glow=(170, 240, 190), label_dark=True, cap=((150, 156, 154), (250, 250, 248)), cap_rim=(96, 102, 100),
                                                 cap_top_col=(240, 242, 240)), "Lọ thuốc minh họa NOVIXA, lọ nhỏ thủy tinh xanh lá", "lọ thuốc"),
    ("novixa-ill-blister-001", make_blister, None, "Vỉ thuốc minh họa NOVIXA", "vỉ thuốc"),
    ("novixa-ill-blister-002", make_blister, dict(name="MẪU 02", cols=4, rows=2, y0=120, pw=170, ph=170, sx=230, sy=235, style="tablet",
                                                   pill=((246, 244, 238),), rot=-8, center=True), "Vỉ thuốc minh họa NOVIXA, viên tròn trắng", "vỉ thuốc"),
    ("novixa-ill-blister-003", make_blister, dict(name="MẪU 03", pill=(PEACH, W1), rot=-4, center=True), "Vỉ thuốc minh họa NOVIXA, viên nang cam", "vỉ thuốc"),
    ("novixa-ill-blister-004", make_blister, dict(name="MẪU 04", cols=6, rows=1, pill=((52, 120, 176), W1), rot=9, center=True), "Vỉ thuốc minh họa NOVIXA, vỉ một hàng", "vỉ thuốc"),
    ("novixa-ill-blister-005", make_blister, dict(name="MẪU 05", cols=3, rows=2, pw=190, ph=190, sx=250, sy=250, style="tablet",
                                                   pill=(TEAL_L,), rot=12, center=True), "Vỉ thuốc minh họa NOVIXA, viên tròn xanh", "vỉ thuốc"),
    ("novixa-ill-blister-006", make_blister, dict(name="MẪU 06", cols=4, rows=3, pw=150, ph=150, sx=196, sy=196, style="tablet",
                                                   pill=(PEACH,), rot=-3, center=True), "Vỉ thuốc minh họa NOVIXA, vỉ ba hàng", "vỉ thuốc"),
]


def keywords_for(noun):
    return [noun, noun.split()[0], "bao bì", "minh họa", "novixa"]


def build(repo: Path, out: Path, font_regular: Path, font_bold: Path, include_originals: bool = False) -> dict:
    sys.path.insert(0, str(repo / "tools" / "kit-video-production"))
    from kit_video.library import average_hash, file_sha256, hamming     # the catalog's own functions

    FONT_PATHS["regular"], FONT_PATHS["bold"] = font_regular, font_bold
    out.mkdir(parents=True, exist_ok=True)
    rows, hashes = [], {}
    try:
        rel_out = out.resolve().relative_to(repo.resolve()).as_posix()
    except ValueError:
        rel_out = out.as_posix()
    from kit_video.library import load_catalog
    catalog = load_catalog(repo / "proof" / "kit-video-production-v1" / "library" / "catalog.json")
    for asset_id, fn, params, label, noun in VARIANTS:
        if asset_id.endswith("-001") and not include_originals:
            continue                                   # approved originals stay as they are
        keywords = keywords_for(noun)
        img = finish(fn(params), square=FRAME[fn.__name__] if params is not None else 0)
        path = out / f"{asset_id}.png"
        img.save(path, optimize=True)
        digest, dh = file_sha256(path), average_hash(path)
        hashes[asset_id] = dh
        rows.append({
            "assetId": asset_id,
            "brand": "NOVIXA",
            "kind": "image",
            "category": "medicine.packaging",
            "keywords": keywords,
            "style": "code-illustration",
            "width": img.width,
            "height": img.height,
            "aspect": round(img.width / img.height, 4),
            "source": "first-party",
            "license": "Dự án tự vẽ bằng code (Pillow). Không phải ảnh chụp.",
            "approval": "proposed",
            "path": f"{rel_out}/{asset_id}.png",
            "pathBase": "repo",
            "sha256": digest,
            "averageHash": dh,
            "similarityGroup": asset_id.rsplit("-", 1)[0],
            "parentId": None,
            "derived": False,
            "usage": [],
            "allow": ["explainer"],
            "limits": [
                "Hình minh họa do code vẽ, sản phẩm hư cấu. Không phải ảnh chụp sản phẩm thật.",
                "NOVIXA không phải nhà sản xuất thuốc. Không ghi hoạt chất, liều dùng hay công dụng.",
                "Phải giữ dòng " + DISCLAIMER_FULL + " trên bao bì khi dùng trong phim.",
                f"Chữ vẽ bằng font {font_bold.name} / {font_regular.name}.",
                "Không dùng làm asset role product và không dùng để thay ảnh sản phẩm thật.",
            ],
            "label": label,
            "role": "illustration",
            "visualSource": "",
            "usageRights": (
                f"{label}: hình vẽ bằng code do dự án NOVIXA tự tạo, sản phẩm hư cấu, "
                "chỉ dùng minh họa trong video NOVIXA. Không phải ảnh sản phẩm thật. Chưa được duyệt."
            ),
            "demoData": True,
            "demoLabel": ILLUSTRATION_LABEL,
        })
    ids = list(hashes)
    near_catalog = {a: min(hamming(hashes[a], int(c["averageHash"])) for c in catalog if c.get("averageHash")) for a in ids}
    bad = [a for a, d in near_catalog.items() if d < 4]
    if bad:
        raise SystemExit(f"Trùng hash với catalog: {bad}")
    pair = {f"{a} / {b}": hamming(hashes[a], hashes[b]) for i, a in enumerate(ids) for b in ids[i + 1:]}
    (out / "candidates.json").write_text(json.dumps({"assets": rows}, ensure_ascii=False, indent=2), encoding="utf-8")
    run = {
        "generator": "novixa_props.py",
        "createdUtc": datetime.now(timezone.utc).isoformat(timespec="seconds"),
        "pillow": PIL_VERSION,
        "fontRegular": str(font_regular),
        "fontBold": str(font_bold),
        "fontBoldSha256": hashlib.sha256(Path(font_bold).read_bytes()).hexdigest(),
        "supersample": SS,
        "pairwiseHamming": pair,
        "minPairwise": min(pair.values()) if pair else None,
        "minToCatalog": near_catalog,
    }
    (out / "generator-run.json").write_text(json.dumps(run, ensure_ascii=False, indent=2), encoding="utf-8")
    return {"rows": rows, "run": run}


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--repo", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--font-regular", default="C:/Windows/Fonts/segoeui.ttf")
    ap.add_argument("--font-bold", default="C:/Windows/Fonts/segoeuib.ttf")
    ap.add_argument("--include-originals", action="store_true")
    a = ap.parse_args()
    repo = Path(a.repo)
    out = Path(a.out) if Path(a.out).is_absolute() else repo / a.out
    result = build(repo, out, Path(a.font_regular), Path(a.font_bold), a.include_originals)
    print(json.dumps({k: result["run"][k] for k in ("minPairwise", "minToCatalog")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
