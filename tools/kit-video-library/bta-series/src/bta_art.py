"""Hình vẽ bằng code của kênh Between Two Asias (BTA) cho mẫu cảnh `nv_board`.

novixa_board.py nạp file này (bọc try/except): `ART` = tên -> (hàm(w, h, args), tỉ lệ rộng/cao[, sống]).
Hình "sống" (cờ thứ ba True) nhận thêm trong args:
    _t   giây kể từ đầu cảnh          _dur  độ dài cảnh
    _pos câu đang đọc (0 = câu mở cảnh; None = ảnh duyệt, vẽ trạng thái cuối)
    _lt  giây kể từ đầu câu đang đọc   _line câu mà hình này xuất hiện   _a  mức đã hiện 0..1
`BACKGROUNDS["bta_paper"]`: nền giấy kem có vân (bật bằng `series.background` trong kenh.json).

Phong cách: 2D phẳng kiểu minh hoạ báo chí, nền giấy kem, mực chàm, nhấn đỏ son (hướng dẫn kênh mục 6).
Không gọi mạng, không tốn phí. Chữ trên hình chỉ là nhãn ngắn ghi trong file hình đã duyệt.
Không vẽ bản đồ có biên giới: `map_ea` chỉ có chấm và tên thành phố trên nền trừu tượng.
"""

from __future__ import annotations

import math
import random
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont, ImageOps

HERE = Path(__file__).resolve().parent
ROOT = Path(__file__).resolve().parents[4]  # E:/KitPlatform
FONTS = HERE.parent / "fonts"

# ---------------------------------------------------------------- màu, chữ
CREAM = (244, 236, 220)
PAPER = (250, 245, 234)
INK = (28, 36, 64)
VERM = (214, 72, 48)
JADE = (46, 140, 120)
OCHRE = (214, 160, 60)
MUTED = (120, 116, 108)
WHITE = (255, 252, 246)
SS = 2  # vẽ gấp đôi rồi thu nhỏ cho nét mịn

_FONT_FILES = {
    "display": ["BTA-Display.ttf"],
    "sans": ["BTA-Sans-Regular.ttf"],
    "bold": ["BTA-Sans-Bold.ttf"],
    "hanzi": ["BTA-Hanzi.otf"],
    "cjk": ["BTA-CJK.otf"],  # tập 03: chữ Hán/Hàn ngoài bộ BTA-Hanzi (cắt từ Noto Serif CJK KR Bold, giấy phép OFL)
}
_FALLBACK = {
    "display": ["C:/Windows/Fonts/georgiab.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf"],
    "sans": ["C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"],
    "bold": ["C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"],
    "hanzi": ["C:/Windows/Fonts/msyhbd.ttc", "C:/Windows/Fonts/simsun.ttc", "/usr/share/fonts/opentype/noto/NotoSerifCJK-Bold.ttc"],
}


@lru_cache(maxsize=128)
def font(kind: str, size: int) -> ImageFont.FreeTypeFont:
    size = max(6, int(size))
    for name in _FONT_FILES.get(kind, []):
        path = FONTS / name
        if path.is_file():
            return ImageFont.truetype(str(path), size)
    for path in _FALLBACK.get(kind, []):
        if Path(path).is_file():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def _col(value, fallback):
    if isinstance(value, (list, tuple)) and len(value) >= 3:
        return tuple(int(v) for v in value[:3])
    return {"ink": INK, "verm": VERM, "red": VERM, "jade": JADE, "green": JADE, "ochre": OCHRE, "yellow": OCHRE,
            "muted": MUTED, "cream": CREAM, "white": WHITE}.get(str(value or "").lower(), fallback)


def mix(a, b, k):
    return tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))


def clamp(v, lo=0.0, hi=1.0):
    return lo if v < lo else hi if v > hi else v


def ease_out(p):
    p = clamp(p)
    return 1 - (1 - p) ** 3


def ease_io(p):
    p = clamp(p)
    return 0.5 - 0.5 * math.cos(math.pi * p)


def back(p):
    p = clamp(p)
    c1 = 1.70158
    return 1 + (c1 + 1) * (p - 1) ** 3 + c1 * (p - 1) ** 2


def since(a: dict, extra_line: int | None = None) -> float:
    """Giây kể từ lúc câu `_line` (hoặc extra_line) bắt đầu. Ảnh duyệt (_pos None): coi như đã lâu."""
    pos = a.get("_pos")
    line = a.get("_line") if extra_line is None else extra_line
    if pos is None or line is None:
        return 99.0 if pos is None else float(a.get("_t", 99.0))
    line = int(line)
    if pos > line:
        return 99.0
    if pos < line:
        return -1.0
    return float(a.get("_lt", 0.0))


def _has_cjk(s):
    return any(0x3000 <= ord(c) <= 0x9FFF or 0xAC00 <= ord(c) <= 0xD7AF or ord(c) >= 0x20000 for c in str(s))


class Cv:
    """Khung vẽ gấp SS lần; .out() trả ảnh RGBA đúng cỡ."""

    def __init__(self, w, h):
        self.w, self.h = max(2, int(w)), max(2, int(h))
        self.im = Image.new("RGBA", (self.w * SS, self.h * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.im)

    def _b(self, box):
        return [v * SS for v in box]

    def rect(self, box, fill=None, outline=None, width=0, r=0):
        if r:
            self.d.rounded_rectangle(self._b(box), radius=r * SS, fill=fill, outline=outline, width=int(width * SS))
        else:
            self.d.rectangle(self._b(box), fill=fill, outline=outline, width=int(width * SS))

    def ell(self, box, fill=None, outline=None, width=0):
        self.d.ellipse(self._b(box), fill=fill, outline=outline, width=int(width * SS))

    def poly(self, pts, fill=None, outline=None, width=0):
        self.d.polygon([(x * SS, y * SS) for x, y in pts], fill=fill, outline=outline, width=int(width * SS) if width else 1)

    def line(self, pts, fill, width=4):
        self.d.line([(x * SS, y * SS) for x, y in pts], fill=fill, width=max(1, int(width * SS)), joint="curve")

    def arc(self, box, a0, a1, fill, width=4):
        self.d.arc(self._b(box), a0, a1, fill=fill, width=max(1, int(width * SS)))

    def pie(self, box, a0, a1, fill=None, outline=None, width=0):
        self.d.pieslice(self._b(box), a0, a1, fill=fill, outline=outline, width=int(width * SS))

    def text(self, xy, txt, kind, size, fill, anchor="mm"):
        if kind != "hanzi" and _has_cjk(txt):
            kind = "cjk"
        self.d.text((xy[0] * SS, xy[1] * SS), txt, font=font(kind, size * SS), fill=fill, anchor=anchor)

    def textlen(self, txt, kind, size):
        if kind != "hanzi" and _has_cjk(txt):
            kind = "cjk"
        return self.d.textlength(txt, font=font(kind, size * SS)) / SS

    def paste(self, img, xy):
        big = img.resize((img.width * SS, img.height * SS), Image.LANCZOS)
        self.im.alpha_composite(big, (int(xy[0] * SS), int(xy[1] * SS)))

    def out(self):
        return self.im.resize((self.w, self.h), Image.LANCZOS)


def _fit_text(txt, kind, size, maxw):
    probe = ImageDraw.Draw(Image.new("RGB", (4, 4)))
    while size > 8 and probe.textlength(txt, font=font(kind, size)) > maxw:
        size -= 1
    return size


def _pill(cv, cx, cy, txt, size, fill, ink, kind="bold", pad=0.7):
    tw = cv.textlen(txt, kind, size)
    h = size * 1.7
    w = tw + size * pad * 2
    cv.rect((cx - w / 2, cy - h / 2, cx + w / 2, cy + h / 2), fill=fill, r=h / 2)
    cv.text((cx, cy + size * 0.04), txt, kind, size, ink)
    return w, h


# ---------------------------------------------------------------- vân giấy dùng chung
@lru_cache(maxsize=8)
def _grain(w: int, h: int, seed: int = 7) -> Image.Image:
    """Lớp vân giấy mờ (alpha thấp) phủ lên ảnh AI và nền."""
    small = Image.effect_noise((max(2, w // 3), max(2, h // 3)), 28).resize((w, h), Image.BICUBIC)
    alpha = small.point(lambda v: int(abs(v - 128) * 0.28))
    layer = Image.new("RGBA", (w, h), (*INK, 0))
    layer.putalpha(alpha)
    return layer


_PAPER: dict = {}


def bta_paper(ctx) -> None:
    """Nền kênh: giấy kem, sáng ở giữa, tối nhẹ về mép, có vân."""
    key = ("bta_paper", ctx.width, ctx.height)
    sprite = _PAPER.get(key)
    if sprite is None:
        w, h = ctx.width, ctx.height
        base = Image.new("RGBA", (w, h), (*CREAM, 255))
        glow = Image.new("L", (w, h), 0)
        ImageDraw.Draw(glow).ellipse((-w * 0.1, -h * 0.15, w * 1.1, h * 1.05), fill=255)
        glow = glow.filter(ImageFilter.GaussianBlur(w * 0.09))
        light = Image.new("RGBA", (w, h), (*PAPER, 255))
        light.putalpha(glow)
        base.alpha_composite(light)
        edge = Image.new("RGBA", (w, h), (*mix(CREAM, INK, 0.18), 255))
        edge.putalpha(Image.eval(glow, lambda v: int((255 - v) * 0.35)))
        base.alpha_composite(edge)
        base.alpha_composite(_grain(w, h))
        sprite = base
        _PAPER[key] = sprite
    ctx.image.alpha_composite(sprite)


BACKGROUNDS = {"bta_paper": bta_paper}


# ---------------------------------------------------------------- con giáp
ANIMALS = {
    "cn": ["RAT", "OX", "TIGER", "RABBIT", "DRAGON", "SNAKE", "HORSE", "GOAT", "MONKEY", "ROOSTER", "DOG", "PIG"],
    "kr": ["RAT", "OX", "TIGER", "RABBIT", "DRAGON", "SNAKE", "HORSE", "SHEEP", "MONKEY", "ROOSTER", "DOG", "PIG"],
    "jp": ["RAT", "OX", "TIGER", "RABBIT", "DRAGON", "SNAKE", "HORSE", "SHEEP", "MONKEY", "ROOSTER", "DOG", "BOAR"],
    "vn": ["RAT", "BUFFALO", "TIGER", "CAT", "DRAGON", "SNAKE", "HORSE", "GOAT", "MONKEY", "ROOSTER", "DOG", "PIG"],
    # Thái Lan: rồng thành naga, vẫn giữ thỏ (nguồn F23). Một số dân tộc Thái: lợn thành voi (F24).
    "th": ["RAT", "OX", "TIGER", "RABBIT", "NAGA", "SNAKE", "HORSE", "GOAT", "MONKEY", "ROOSTER", "DOG", "PIG"],
    "tai": ["RAT", "OX", "TIGER", "RABBIT", "NAGA", "SNAKE", "HORSE", "GOAT", "MONKEY", "ROOSTER", "DOG", "ELEPHANT"],
}
BRANCHES = "子丑寅卯辰巳午未申酉戌亥"
DIFF = {"cn": set(), "kr": {7}, "jp": {7, 11}, "vn": {1, 3, 7}, "th": {4}, "tai": {4, 11}}  # ô khác bảng gốc của Trung Quốc


def _wheel(w, h, a):
    """Tấm lót bàn 12 con giáp: vòng ngoài 12 ô tên con vật, vòng trong 12 chữ Địa chi. Ô `highlight` tô đỏ son."""
    s = min(w, h)
    cv = Cv(s, s)
    c = s / 2
    R = s * 0.48
    set_ = str(a.get("set", "cn"))
    names = ANIMALS.get(set_, ANIMALS["cn"])
    hl = a.get("highlight")
    hl = set(hl if isinstance(hl, list) else ([] if hl is None else [hl]))
    if a.get("diff"):
        hl |= DIFF.get(set_, set())
    rot = min(float(a.get("spin", 0.0)), 0.02) * 360 * float(a.get("_t", 0.0))  # quay chậm: tối đa 7°/giây
    style = str(a.get("style", "placemat"))
    ring = VERM if style == "placemat" else INK
    # đĩa nền và viền
    cv.ell((c - R * 1.02, c - R * 1.02 + s * 0.012, c + R * 1.02, c + R * 1.02 + s * 0.012), fill=(*mix(CREAM, INK, 0.25), 90))
    cv.ell((c - R, c - R, c + R, c + R), fill=ring)
    cv.ell((c - R * 0.965, c - R * 0.965, c + R * 0.965, c + R * 0.965), fill=PAPER)
    r_out, r_mid, r_in = R * 0.94, R * 0.62, R * 0.36
    for i in range(12):
        a0 = -90 - 15 + i * 30 + rot
        fill = mix(VERM, PAPER, 0.12) if i in hl else (mix(CREAM, PAPER, 0.4) if i % 2 else PAPER)
        cv.pie((c - r_out, c - r_out, c + r_out, c + r_out), a0, a0 + 30, fill=fill)
    cv.ell((c - r_mid, c - r_mid, c + r_mid, c + r_mid), fill=PAPER, outline=ring, width=max(2, s * 0.006))
    for i in range(12):
        ang = math.radians(-90 - 15 + i * 30 + rot)
        cv.line([(c + math.cos(ang) * r_mid, c + math.sin(ang) * r_mid), (c + math.cos(ang) * r_out, c + math.sin(ang) * r_out)],
                (*mix(ring, PAPER, 0.35), 255), max(1.5, s * 0.004))
    for i in range(12):
        ang = math.radians(-90 + i * 30 + rot)
        rx = (r_out + r_mid) / 2
        x, y = c + math.cos(ang) * rx, c + math.sin(ang) * rx
        name = names[i]
        size = _fit_text(name, "bold", int(s * 0.034), (r_out - r_mid) * 1.25)
        ink = WHITE if i in hl else INK
        # chữ đứng thẳng (không xoay theo vành) để bánh xe quay chậm mà chữ không lật
        cv.text((x, y), name, "bold", size, ink)
        ry = (r_mid + r_in) / 2
        hx, hy = c + math.cos(ang) * ry, c + math.sin(ang) * ry
        cv.text((hx, hy), BRANCHES[i], "hanzi", int(s * 0.05), VERM if i in hl else INK)
    cv.ell((c - r_in, c - r_in, c + r_in, c + r_in), fill=mix(PAPER, CREAM, 0.6), outline=ring, width=max(2, s * 0.006))
    center = str(a.get("center", "生肖" if set_ != "vn" else "VIỆT NAM"))
    if center:
        kind = "hanzi" if any(ord(ch) > 0x3000 for ch in center) else "display"
        size = _fit_text(center, kind, int(s * (0.09 if kind == "hanzi" else 0.06)), r_in * 1.5)
        cv.text((c, c), center, kind, size, ring)
    return cv.out()


# ---------------------------------------------------------------- con vật (huy hiệu đầu con vật)
def _animal_head(cv, cx, cy, r, kind, ink=INK):
    """Đầu con vật đơn giản, cùng nét: hình học phẳng màu mực trên đĩa giấy."""
    fur = {"cat": OCHRE, "rabbit": WHITE, "ox": mix(OCHRE, INK, 0.35), "buffalo": mix(INK, MUTED, 0.45),
           "pig": (236, 170, 160), "boar": mix(INK, OCHRE, 0.35), "sheep": WHITE, "goat": mix(CREAM, MUTED, 0.35),
           "rat": mix(MUTED, CREAM, 0.2)}.get(kind, OCHRE)
    lw = max(2, r * 0.07)
    if kind == "cat":
        for sx in (-1, 1):
            cv.poly([(cx + sx * r * .78, cy - r * .2), (cx + sx * r * .62, cy - r * 1.05), (cx + sx * r * .18, cy - r * .62)], fill=fur, outline=ink, width=lw)
            cv.poly([(cx + sx * r * .64, cy - r * .35), (cx + sx * r * .58, cy - r * .82), (cx + sx * r * .3, cy - r * .6)], fill=(236, 170, 160))
        cv.ell((cx - r * .86, cy - r * .7, cx + r * .86, cy + r * .78), fill=fur, outline=ink, width=lw)
        for sx in (-1, 1):  # vằn
            cv.line([(cx + sx * r * .1, cy - r * .68), (cx + sx * r * .16, cy - r * .42)], ink, lw * .8)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .34 - r * .11, cy - r * .1 - r * .15, cx + sx * r * .34 + r * .11, cy - r * .1 + r * .15), fill=ink)
            cv.ell((cx + sx * r * .34 - r * .035, cy - r * .1 - r * .1, cx + sx * r * .34 + r * .035, cy - r * .1 - r * .03), fill=WHITE)
            for k in (-1, 1):
                cv.line([(cx + sx * r * .5, cy + r * .24), (cx + sx * r * 1.05, cy + r * (.18 + .12 * k))], ink, lw * .55)
        cv.poly([(cx - r * .1, cy + r * .14), (cx + r * .1, cy + r * .14), (cx, cy + r * .26)], fill=VERM)
        cv.arc((cx - r * .18, cy + r * .2, cx, cy + r * .42), 0, 160, ink, lw * .6)
        cv.arc((cx, cy + r * .2, cx + r * .18, cy + r * .42), 20, 180, ink, lw * .6)
    elif kind == "rabbit":
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .36 - r * .2, cy - r * 1.5, cx + sx * r * .36 + r * .2, cy - r * .3), fill=fur, outline=ink, width=lw)
            cv.ell((cx + sx * r * .36 - r * .08, cy - r * 1.32, cx + sx * r * .36 + r * .08, cy - r * .5), fill=(236, 170, 160))
        cv.ell((cx - r * .78, cy - r * .62, cx + r * .78, cy + r * .78), fill=fur, outline=ink, width=lw)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .3 - r * .09, cy - r * .02 - r * .11, cx + sx * r * .3 + r * .09, cy - r * .02 + r * .11), fill=ink)
        cv.ell((cx - r * .09, cy + r * .2, cx + r * .09, cy + r * .32), fill=(220, 120, 120))
        cv.line([(cx, cy + r * .32), (cx, cy + r * .44)], ink, lw * .6)
    elif kind in ("ox", "buffalo"):
        horn = mix(CREAM, OCHRE, 0.3)
        if kind == "buffalo":  # sừng trâu cong rộng ra sau
            for sx in (-1, 1):
                cv.arc((cx + sx * r * .1 - r * 1.15, cy - r * 1.25, cx + sx * r * .1 + r * 1.15, cy + r * .35), 200 if sx < 0 else 270, 270 if sx < 0 else 340, ink, r * .26)
                cv.arc((cx + sx * r * .1 - r * 1.15, cy - r * 1.25, cx + sx * r * .1 + r * 1.15, cy + r * .35), 202 if sx < 0 else 272, 268 if sx < 0 else 338, horn, r * .16)
        else:
            for sx in (-1, 1):
                cv.poly([(cx + sx * r * .5, cy - r * .5), (cx + sx * r * 1.0, cy - r * 1.05), (cx + sx * r * .78, cy - r * .42)], fill=horn, outline=ink, width=lw)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .95 - r * .3, cy - r * .35, cx + sx * r * .95 + r * .3, cy - r * .05), fill=fur, outline=ink, width=lw)
        cv.rect((cx - r * .62, cy - r * .62, cx + r * .62, cy + r * .5), fill=fur, outline=ink, width=lw, r=r * .45)
        cv.ell((cx - r * .5, cy + r * .1, cx + r * .5, cy + r * .78), fill=mix(fur, WHITE, 0.35), outline=ink, width=lw)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .3 - r * .08, cy - r * .2 - r * .1, cx + sx * r * .3 + r * .08, cy - r * .2 + r * .1), fill=ink)
            cv.ell((cx + sx * r * .2 - r * .06, cy + r * .42 - r * .06, cx + sx * r * .2 + r * .06, cy + r * .42 + r * .06), fill=ink)
    elif kind in ("pig", "boar"):
        for sx in (-1, 1):
            cv.poly([(cx + sx * r * .35, cy - r * .55), (cx + sx * r * .8, cy - r * .95), (cx + sx * r * .75, cy - r * .35)], fill=fur, outline=ink, width=lw)
        cv.ell((cx - r * .82, cy - r * .7, cx + r * .82, cy + r * .72), fill=fur, outline=ink, width=lw)
        if kind == "boar":
            for k in range(5):
                x = cx - r * .3 + k * r * .15
                cv.line([(x, cy - r * .72), (x + r * .05, cy - r * .95)], ink, lw * .7)
        cv.ell((cx - r * .38, cy + r * .02, cx + r * .38, cy + r * .44), fill=mix(fur, WHITE, 0.3), outline=ink, width=lw)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .13 - r * .05, cy + r * .18, cx + sx * r * .13 + r * .05, cy + r * .28), fill=ink)
            cv.ell((cx + sx * r * .32 - r * .07, cy - r * .3, cx + sx * r * .32 + r * .07, cy - r * .14), fill=ink)
        if kind == "boar":
            for sx in (-1, 1):
                cv.poly([(cx + sx * r * .36, cy + r * .3), (cx + sx * r * .6, cy + r * .02), (cx + sx * r * .46, cy + r * .4)], fill=WHITE, outline=ink, width=lw * .6)
    elif kind in ("sheep", "goat"):
        if kind == "sheep":
            for k in range(9):
                ang = math.radians(200 + k * 17.5)
                x, y = cx + math.cos(ang) * r * .75, cy - r * .1 + math.sin(ang) * r * .7
                cv.ell((x - r * .28, y - r * .28, x + r * .28, y + r * .28), fill=WHITE, outline=ink, width=lw * .7)
            cv.ell((cx - r * .5, cy - r * .45, cx + r * .5, cy + r * .75), fill=mix(MUTED, CREAM, 0.15), outline=ink, width=lw)
        else:
            for sx in (-1, 1):
                cv.arc((cx + sx * r * .2 - r * .5, cy - r * 1.25, cx + sx * r * .2 + r * .5, cy - r * .15), 180 if sx < 0 else 270, 270 if sx < 0 else 360, ink, r * .14)
            cv.ell((cx - r * .55, cy - r * .55, cx + r * .55, cy + r * .7), fill=fur, outline=ink, width=lw)
            cv.poly([(cx - r * .14, cy + r * .62), (cx + r * .14, cy + r * .62), (cx, cy + r * 1.1)], fill=fur, outline=ink, width=lw * .7)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .9 - r * .3, cy - r * .2, cx + sx * r * .9 + r * .3, cy + r * .02), fill=mix(MUTED, CREAM, 0.3), outline=ink, width=lw * .7)
            cv.ell((cx + sx * r * .22 - r * .07, cy - r * .05 - r * .08, cx + sx * r * .22 + r * .07, cy - r * .05 + r * .08), fill=ink)
    else:  # chuột
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .62 - r * .38, cy - r * .9, cx + sx * r * .62 + r * .38, cy - r * .14), fill=fur, outline=ink, width=lw)
        cv.ell((cx - r * .7, cy - r * .5, cx + r * .7, cy + r * .7), fill=fur, outline=ink, width=lw)
        cv.ell((cx - r * .08, cy + r * .45, cx + r * .08, cy + r * .58), fill=ink)
        for sx in (-1, 1):
            cv.ell((cx + sx * r * .26 - r * .07, cy - r * .05 - r * .08, cx + sx * r * .26 + r * .07, cy - r * .05 + r * .08), fill=ink)


def animal(w, h, a):
    """Huy hiệu con vật. args: kind, pin (nhãn thành phố dưới huy hiệu), stamp (dấu đỏ tên con vật), label."""
    kind = str(a.get("kind", "cat"))
    pin = str(a.get("pin", "") or "")
    cv = Cv(w, h)
    s = min(w, h * 0.78)
    cx, cy = w / 2, s * 0.52
    r = s * 0.42
    cv.ell((cx - r * 1.08, cy - r * 1.08 + s * 0.02, cx + r * 1.08, cy + r * 1.08 + s * 0.02), fill=(*mix(CREAM, INK, 0.25), 110))
    ring = VERM if kind == "cat" else INK
    cv.ell((cx - r * 1.08, cy - r * 1.08, cx + r * 1.08, cy + r * 1.08), fill=ring)
    cv.ell((cx - r, cy - r, cx + r, cy + r), fill=PAPER)
    _animal_head(cv, cx, cy + r * 0.1, r * 0.62, kind)
    label = str(a.get("label") or (kind.upper() if a.get("stamp") else ""))
    if label:
        k = ease_out(since(a) / 0.35) if a.get("_pos") is not None else 1.0
        size = int(s * 0.12)
        st = Cv(int(cv.textlen(label, "display", size) + size * 1.2), int(size * 1.7))
        st.rect((2, 2, st.w - 2, st.h - 2), outline=VERM, width=max(3, size * 0.12), r=size * 0.2)
        st.text((st.w / 2, st.h / 2 + size * 0.05), label, "display", size, VERM)
        img = st.out().rotate(-12, expand=True, resample=Image.BICUBIC)
        if k < 1:
            sc = 1.6 - 0.6 * k
            img = img.resize((max(2, int(img.width * sc)), max(2, int(img.height * sc))), Image.LANCZOS)
            img.putalpha(img.getchannel("A").point(lambda v: int(v * k)))
        cv.paste(img, (cx + r * 0.25, cy + r * 0.35))
    if pin:
        _pill(cv, cx, min(h - s * 0.08, cy + r * 1.08 + s * 0.11), pin.upper(), int(s * 0.085), INK, WHITE)
    return cv.out()


# ---------------------------------------------------------------- bản đồ trừu tượng (chỉ chấm thành phố)
CITIES = {  # kinh độ, vĩ độ: chỉ dùng để đặt chấm, không vẽ biên giới hay đường bờ biển
    "Beijing": (116.4, 39.9), "Seoul": (127.0, 37.6), "Tokyo": (139.7, 35.7), "Singapore": (103.8, 1.35),
    "Hanoi": (105.8, 21.0), "Bangkok": (100.5, 13.75), "Jakarta": (106.8, -6.2), "Kuala Lumpur": (101.7, 3.1),
    "Shanghai": (121.5, 31.2), "Hong Kong": (114.2, 22.3), "Manila": (121.0, 14.6), "Taipei": (121.5, 25.0),
    "Vientiane": (102.6, 17.97), "Phnom Penh": (104.9, 11.55), "Yangon": (96.2, 16.8),  # tập 02: Tết tháng Tư Đông Nam Á
}
MAP_BOX = (95.0, 145.0, -9.0, 46.0)  # lon0, lon1, lat0, lat1


def map_point(city: str, w: float, h: float) -> tuple[float, float]:
    lon, lat = CITIES[city]
    lon0, lon1, lat0, lat1 = MAP_BOX
    pad = 0.1
    return (w * (pad + (1 - 2 * pad) * (lon - lon0) / (lon1 - lon0)), h * (pad + (1 - 2 * pad) * (lat1 - lat) / (lat1 - lat0)))


def map_ea(w, h, a):
    """Nền trừu tượng (lưới chấm) + chấm thành phố, mỗi chấm có biểu tượng con vật. pins hiện so le theo `stagger` giây."""
    cv = Cv(w, h)
    rnd = random.Random(5)
    step = min(w, h) * 0.045
    y = step / 2
    while y < h:
        x = step / 2 + (step / 2 if int(y / step) % 2 else 0)
        while x < w:
            d = math.hypot((x - w * .55) / w, (y - h * .45) / h)
            if d < 0.48 and rnd.random() > 0.08:
                cv.ell((x - step * .09, y - step * .09, x + step * .09, y + step * .09), fill=(*mix(CREAM, INK, 0.22), 160))
            x += step
        y += step
    pins = a.get("pins") or []
    gap = float(a.get("stagger", 0.35))
    t0 = since(a)
    show_hanoi = bool(a.get("hanoi", True))
    s = min(w, h)
    for i, pin in enumerate(pins + ([{"city": "Hanoi", "icon": "", "ghost": True}] if show_hanoi else [])):
        city = str(pin.get("city", ""))
        if city not in CITIES:
            continue
        k = 1.0 if t0 >= 90 else ease_out((t0 - i * gap) / 0.4)
        if k <= 0.01 and not pin.get("ghost"):
            continue
        x, y = map_point(city, w, h)
        if pin.get("ghost"):
            r = s * 0.018
            cv.ell((x - r, y - r, x + r, y + r), outline=(*VERM, 220), width=max(2, s * 0.005))
            cv.text((x + r * 1.8, y), "?", "display", int(s * 0.05), VERM, anchor="lm")
            continue
        r = s * 0.02
        cv.ell((x - r, y - r, x + r, y + r), fill=INK)
        size = int(s * 0.048)
        cv.text((x + r * 1.8, y + size * 0.1), city.upper(), "bold", size, (*INK, int(255 * k)), anchor="lm")
        icon = str(pin.get("icon", ""))
        if icon:
            ir = s * 0.08 * (0.6 + 0.4 * back(k))
            ix, iy = x, y - s * 0.11
            cv.ell((ix - ir, iy - ir, ix + ir, iy + ir), fill=(*PAPER, int(255 * k)), outline=(*INK, int(255 * k)), width=max(2, s * 0.004))
            if k > 0.3:
                _animal_head(cv, ix, iy + ir * 0.12, ir * 0.55, icon)
    return cv.out()


# ---------------------------------------------------------------- chữ động
def kinetic(w, h, a):
    """Chữ ngắn có chuyển động. style: big (mặc định) | tag | stamp | strike | question | footnote."""
    txt = str(a.get("text", ""))
    style = str(a.get("style", "big"))
    t = since(a)
    k = 1.0 if t >= 90 else ease_out(t / 0.45)
    cv = Cv(w, h)
    if style == "tag":
        size = int(h * 0.42)
        size = _fit_text(txt, "bold", size, w * 0.85)
        tw = cv.textlen(txt, "bold", size)
        bw = tw + size * 1.6
        x0 = w / 2 - bw / 2
        reveal = clamp(k * 1.15)
        cv.rect((x0, h * .18, x0 + bw * reveal, h * .82), fill=VERM, r=h * .12)
        if reveal > 0.6:
            cv.text((w / 2, h / 2 + size * 0.05), txt, "bold", size, (*WHITE, int(255 * clamp((reveal - .6) / .4))))
    elif style == "stamp":
        size = _fit_text(txt, "display", int(h * 0.55), w * 0.8)
        tw = cv.textlen(txt, "display", size)
        st = Cv(int(tw + size * 1.0), int(size * 1.6))
        st.rect((3, 3, st.w - 3, st.h - 3), outline=VERM, width=max(3, size * 0.09), r=size * 0.18)
        st.text((st.w / 2, st.h / 2 + size * 0.04), txt, "display", size, VERM)
        img = st.out().rotate(-4, expand=True, resample=Image.BICUBIC)
        sc = 1.0 + 0.5 * (1 - back(k)) if t < 90 else 1.0
        img = img.resize((max(2, int(img.width * sc)), max(2, int(img.height * sc))), Image.LANCZOS)
        img.putalpha(img.getchannel("A").point(lambda v: int(v * clamp(k * 1.5))))
        cv.paste(img, ((w - img.width) / 2, (h - img.height) / 2))
    elif style == "footnote":
        size = _fit_text(txt, "sans", int(h * 0.36), w * 0.95)
        cv.text((w / 2, h / 2), txt, "sans", size, (*MUTED, int(255 * k)))
    else:
        size = _fit_text(txt, "display", int(h * 0.62), w * 0.92)
        dy = (1 - k) * h * 0.25
        cv.text((w / 2, h / 2 + dy), txt, "display", size, (*INK, int(255 * k)))
        if style == "strike" and t > 0.5:
            p = 1.0 if t >= 90 else ease_out((t - 0.5) / 0.4)
            tw = cv.textlen(txt, "display", size)
            cv.line([(w / 2 - tw / 2 - size * .1, h / 2), (w / 2 - tw / 2 - size * .1 + (tw + size * .2) * p, h / 2)], VERM, size * 0.1)
        if style == "question":
            tw = cv.textlen(txt, "display", size)
            cv.text((w / 2 + tw / 2 + size * 0.35, h / 2 + dy), "?", "display", size, (*VERM, int(255 * k)))
    return cv.out()


# ---------------------------------------------------------------- người dẫn chuyện v3 (14/11/2026, tông Đông Hồ)
def _smooth(pts, n=10, closed=True):
    """Đường cong Catmull-Rom qua các điểm điều khiển."""
    P = list(pts)
    m = len(P)
    out = []
    for i in (range(m) if closed else range(m - 1)):
        p0 = P[(i - 1) % m] if closed else P[max(0, i - 1)]
        p1, p2 = P[i], P[(i + 1) % m]
        p3 = P[(i + 2) % m] if closed else P[min(m - 1, i + 2)]
        for k in range(n):
            t = k / n
            t2, t3 = t * t, t * t * t
            out.append(tuple(0.5 * ((2 * p1[j]) + (-p0[j] + p2[j]) * t + (2 * p0[j] - 5 * p1[j] + 4 * p2[j] - p3[j]) * t2 + (-p0[j] + 3 * p1[j] - 3 * p2[j] + p3[j]) * t3) for j in (0, 1)))
    if not closed:
        out.append(P[-1])
    return out


def _tube(cv, pts, w0, w1, color, lw):
    """Ống tay thon dần từ w0 tới w1, viền mực."""
    for width, col in ((None, INK), (0, color)):
        for i in range(len(pts) - 1):
            k0, k1 = i / (len(pts) - 1), (i + 1) / (len(pts) - 1)
            ww = w0 + (w1 - w0) * (k0 + k1) / 2
            cv.line([pts[i], pts[i + 1]], col, ww + (lw * 2 if width is None else 0))
        for p, ww in ((pts[0], w0), (pts[-1], w1)):
            r = ww / 2 + (lw if width is None else 0)
            cv.ell((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=col)


def _palm(cv, x, y, r, skin, lw, kind="fist", ang=0.0):
    """Bàn tay: fist (nắm nhẹ), open (xòe, vẫy), up (ngửa lòng), point (ngón trỏ theo góc `ang`)."""
    if kind == "point":
        a = math.radians(ang)
        fx, fy = x + math.cos(a) * r * 2.1, y + math.sin(a) * r * 2.1
        _tube(cv, [(x, y), (fx, fy)], r * .62, r * .55, skin, lw)
    if kind == "open":
        for k in range(4):
            a = math.radians(-125 + k * 22)
            fx, fy = x + math.cos(a) * r * 1.35, y + math.sin(a) * r * 1.35
            _tube(cv, [(x, y), (fx, fy)], r * .42, r * .38, skin, lw * .8)
    if kind == "up":
        cv.pie((x - r * 1.15, y - r * .9, x + r * 1.15, y + r * .9), 0, 180, fill=skin, outline=INK, width=lw)
        cv.line([(x - r * 1.05, y), (x + r * 1.05, y)], INK, lw)
        return
    cv.ell((x - r, y - r * .92, x + r, y + r * .92), fill=skin, outline=INK, width=lw)
    if kind in ("fist", "point"):
        cv.arc((x - r * .55, y - r * .3, x + r * .35, y + r * .5), 180, 330, INK, lw * .7)


def host(w, h, a):
    """Người dẫn chuyện 2D v3, tông tranh Đông Hồ: mặt tròn, má đỏ, tóc ngang vai mái lệch, búi nhỏ cài kẹp son;
    áo cổ đứng xanh ngọc kiểu áo dài, viền son, chấm hoa vàng đất. Thở, chớp mắt, nghiêng đầu theo biểu cảm, miệng nhép khi `talk`.
    mood: smile|wink|curious|surprise|think|serious. pose: point|hold|shrug|wave|think. prop: alarm_clock (pose hold).
    Mọi tư thế nằm gọn trong khung."""
    t = float(a.get("_t", 2.0))
    mood = str(a.get("mood", "smile"))
    pose = str(a.get("pose", "hold"))
    talk = bool(a.get("talk", False)) and a.get("_pos") is not None
    cv = Cv(w, h)
    lw = max(2.0, w * 0.011)
    bob = h * 0.004 * math.sin(t * math.tau / 4)
    skin, hair, shirt, trim = (242, 204, 170), INK, JADE, VERM
    cx = w * 0.55
    top = h * 0.50 + bob            # chân cổ
    sw = w * 0.235                  # nửa vai
    hr = w * 0.205                  # bán kính đầu
    hx, hy = cx, top - hr * 1.22
    hand_r = w * .052

    # ---------- tay (vẽ sau lưng thân với tư thế giơ ra ngoài)
    def arm(side, elbow, hand, kind, ang=0.0, fore_only=False):
        sh = (cx + side * sw * .8, top + h * .085)
        if fore_only:
            _tube(cv, [elbow, hand], w * .08, w * .07, shirt, lw)
        else:
            _tube(cv, [sh, elbow, hand], w * .088, w * .07, shirt, lw)
        # cổ tay áo viền son
        dx, dy = hand[0] - elbow[0], hand[1] - elbow[1]
        d = math.hypot(dx, dy) or 1
        c0 = (hand[0] - dx / d * hand_r * 1.6, hand[1] - dy / d * hand_r * 1.6)
        c1 = (hand[0] - dx / d * hand_r * .9, hand[1] - dy / d * hand_r * .9)
        cv.line([c0, c1], INK, w * .075 + lw * 2)
        cv.line([c0, c1], trim, w * .075)
        _palm(cv, hand[0], hand[1], hand_r, skin, lw, kind, ang)

    def arms():
        if pose == "point":
            arm(-1, (cx - sw * 1.32, top + h * .15), (cx - sw * 1.6, top - h * .01), "point", 208)
        elif pose == "wave":
            wv = math.sin(t * 6) * w * .02
            arm(1, (cx + sw * 1.36, top + h * .13), (cx + sw * 1.42 + wv, top - h * .07), "open")
        elif pose == "shrug":
            for s in (-1, 1):
                arm(s, (cx + s * sw * 1.22, top + h * .25), (cx + s * sw * 1.55, top + h * .16), "up")
        elif pose == "think":
            arm(-1, (cx - sw * .96, top + h * .28), (cx + sw * .45, top + h * .29), "fist", fore_only=True)
            arm(1, (cx + sw * .62, top + h * .3), (hx + hr * .32, hy + hr * 1.08), "fist", fore_only=True)
        else:  # hold
            if str(a.get("prop", "")) == "alarm_clock":
                pr = w * .12
                px, py = cx, top + h * .19
                for s in (-1, 1):
                    cv.ell((px + s * pr * .62 - pr * .32, py - pr * 1.14, px + s * pr * .62 + pr * .32, py - pr * .64), fill=VERM, outline=INK, width=lw)
                cv.ell((px - pr, py - pr, px + pr, py + pr), fill=VERM, outline=INK, width=lw)
                cv.ell((px - pr * .78, py - pr * .78, px + pr * .78, py + pr * .78), fill=PAPER, outline=INK, width=lw * .7)
                cv.line([(px, py), (px, py - pr * .55)], INK, pr * .1)
                cv.line([(px, py), (px + pr * .4, py + pr * .1)], INK, pr * .1)
            for s in (-1, 1):
                arm(s, (cx + s * sw * .96, top + h * .27), (cx + s * w * .085, top + h * .24), "fist", fore_only=True)

    behind = pose in ("point", "wave", "shrug")
    if behind:
        arms()
    else:  # bắp tay sau thân, chỉ cẳng tay ở phía trước
        for s in (-1, 1):
            _tube(cv, [(cx + s * sw * .86, top + h * .075), (cx + s * sw * .96, top + h * .27)], w * .088, w * .08, shirt, lw)

    # ---------- thân áo: vai tròn, eo nhẹ, vạt xòe
    def side(s):
        return _smooth([(cx + s * w * .05, top), (cx + s * sw * .72, top + h * .02), (cx + s * sw * .98, top + h * .075), (cx + s * sw * .98, top + h * .2),
                        (cx + s * sw * .86, top + h * .32), (cx + s * sw * 1.02, top + h * .44)], n=8, closed=False) + [(cx + s * sw * 1.12, h * 1.02)]
    body = side(-1) + list(reversed(side(1)))
    cv.poly(body, fill=shirt, outline=INK, width=lw)
    # chấm hoa vàng đất (4 cánh) gần vạt
    for fx, fy in ((cx - sw * .55, top + h * .41), (cx + sw * .35, top + h * .45), (cx - sw * .05, top + h * .38)):
        r = w * .016
        for k in range(4):
            aa = k * math.pi / 2
            cv.ell((fx + math.cos(aa) * r - r * .7, fy + math.sin(aa) * r - r * .7, fx + math.cos(aa) * r + r * .7, fy + math.sin(aa) * r + r * .7), fill=OCHRE)
        cv.ell((fx - r * .5, fy - r * .5, fx + r * .5, fy + r * .5), fill=trim)
    # cổ + cổ áo đứng + đường cài chéo viền son
    cv.rect((cx - w * .048, top - h * .055, cx + w * .048, top + h * .012), fill=skin, outline=INK, width=lw)
    seam = _smooth([(cx + w * .02, top + h * .028), (cx + w * .09, top + h * .065), (cx + sw * .9, top + h * .12)], n=8, closed=False)
    sl = w * .006  # nét đường cài theo cỡ khung, không theo lw (ở cỡ nhỏ lw bị chặn 2px làm đường cài thành dải đeo chéo)
    cv.line(seam, INK, sl * 2.4)
    cv.line(seam, trim, sl * 1.1)
    for k in (0.3, 0.62):
        px, py = seam[int(len(seam) * k)]
        cv.ell((px - sl * 1.3, py - sl * 1.3, px + sl * 1.3, py + sl * 1.3), fill=OCHRE)
    cv.rect((cx - w * .072, top - h * .014, cx + w * .072, top + h * .028), fill=trim, outline=INK, width=lw, r=w * .018)

    if not behind and pose != "think":
        arms()

    # ---------- đầu (lớp riêng để nghiêng)
    hv = Cv(w, h)
    tilt = {"curious": 7, "think": -6, "surprise": 0, "wink": 4, "serious": 0}.get(mood, 2)
    # tóc sau: ngang vai, phồng, đuôi hơi vểnh
    back = _smooth([(hx - hr * 1.16, hy - hr * .05), (hx - hr * 1.02, hy - hr * .98), (hx - hr * .1, hy - hr * 1.3), (hx + hr * .98, hy - hr * 1.02),
                    (hx + hr * 1.2, hy - hr * .05), (hx + hr * 1.22, hy + hr * .8), (hx + hr * 1.32, hy + hr * 1.12), (hx + hr * .95, hy + hr * 1.08),
                    (hx - hr * .95, hy + hr * 1.08), (hx - hr * 1.32, hy + hr * 1.12), (hx - hr * 1.22, hy + hr * .8)])
    hv.poly(back, fill=hair, outline=INK, width=lw)
    # búi nhỏ + kẹp son
    bx, by, br = hx + hr * .62, hy - hr * 1.18, hr * .36
    hv.ell((bx - br, by - br, bx + br, by + br), fill=hair, outline=INK, width=lw)
    hv.line([(bx - br * 1.1, by + br * .55), (bx + br * .3, by - br * .2)], INK, lw * 3.4)
    hv.line([(bx - br * 1.1, by + br * .55), (bx + br * .3, by - br * .2)], trim, lw * 1.8)
    # tai
    for s in (-1, 1):
        hv.ell((hx + s * hr * .9 - hr * .16, hy + hr * .02, hx + s * hr * .9 + hr * .16, hy + hr * .34), fill=skin, outline=INK, width=lw * .8)
    # mặt tròn bầu
    face = _smooth([(hx, hy - hr * .98), (hx + hr * .8, hy - hr * .62), (hx + hr * .93, hy + hr * .12), (hx + hr * .72, hy + hr * .72),
                    (hx, hy + hr * .98), (hx - hr * .72, hy + hr * .72), (hx - hr * .93, hy + hr * .12), (hx - hr * .8, hy - hr * .62)])
    hv.poly(face, fill=skin, outline=INK, width=lw)
    # mái lệch mềm (rẽ ngôi bên trái)
    fringe = _smooth([(hx - hr * 1.0, hy + hr * .02), (hx - hr * .96, hy - hr * .7), (hx - hr * .35, hy - hr * 1.12), (hx + hr * .5, hy - hr * 1.06),
                      (hx + hr * 1.0, hy - hr * .5), (hx + hr * 1.02, hy + hr * .1), (hx + hr * .78, hy - hr * .32), (hx + hr * .35, hy - hr * .5),
                      (hx - hr * .18, hy - hr * .58), (hx - hr * .48, hy - hr * .74), (hx - hr * .72, hy - hr * .42)], n=8)
    hv.poly(fringe, fill=hair, outline=INK, width=lw)
    strand = (70, 82, 120)
    for pts in (((hx - hr * .3, hy - hr * 1.0), (hx + hr * .15, hy - hr * .8), (hx + hr * .55, hy - hr * .6)),
                ((hx + hr * .2, hy - hr * 1.0), (hx + hr * .62, hy - hr * .78), (hx + hr * .85, hy - hr * .35))):
        hv.line(_smooth(pts, n=6, closed=False), strand, lw * .7)
    # má đỏ kiểu tranh dân gian
    for s in (-1, 1):
        cx2 = hx + s * hr * .52
        hv.ell((cx2 - hr * .17, hy + hr * .4, cx2 + hr * .17, hy + hr * .62), fill=(*VERM, 115))
    # mắt
    blink = (t % 4.2) < 0.14
    ey = hy + hr * .14
    look = {"think": (hr * .05, -hr * .05), "curious": (hr * .03, 0)}.get(mood, (0, 0))
    for s in (-1, 1):
        ex = hx + s * hr * .38
        if blink or (mood == "wink" and s > 0):
            hv.arc((ex - hr * .17, ey - hr * .14, ex + hr * .17, ey + hr * .1), 20, 160, INK, lw * 1.2)
            continue
        er = hr * (0.17 if mood == "surprise" else 0.135)
        hv.ell((ex - er * .92, ey - er * 1.12, ex + er * .92, ey + er * 1.12), fill=INK)
        lx, ly = ex + look[0], ey + look[1]
        hv.ell((lx - er * .05, ly - er * .78, lx + er * .45, ly - er * .28), fill=WHITE)
        hv.ell((lx - er * .45, ly + er * .25, lx - er * .2, ly + er * .5), fill=WHITE)
        hv.line([(ex + s * er * .7, ey - er * .8), (ex + s * er * 1.15, ey - er * 1.05)], INK, lw * .8)  # mi
    # lông mày theo biểu cảm
    for s in (-1, 1):
        bx2 = hx + s * hr * .38
        y0 = ey - hr * .36
        if mood == "surprise":
            y0 -= hr * .1
            pts = [(bx2 - hr * .16, y0 + hr * .04), (bx2, y0 - hr * .04), (bx2 + hr * .16, y0 + hr * .04)]
        elif mood == "curious" and s > 0:
            pts = [(bx2 - hr * .16, y0 - hr * .02), (bx2, y0 - hr * .12), (bx2 + hr * .17, y0 - hr * .06)]
        elif mood == "think":
            pts = [(bx2 - s * hr * .16, y0 - hr * .04), (bx2 + s * hr * .16, y0 + hr * .05)] if s < 0 else [(bx2 - hr * .16, y0 - hr * .06), (bx2 + hr * .16, y0 - hr * .02)]
        elif mood == "serious":  # đầu mày phía trong thấp xuống: nghiêm, không buồn
            pts = [(bx2 + s * hr * .17, y0 - hr * .04), (bx2 - s * hr * .15, y0 + hr * .06)]
        else:
            pts = [(bx2 - hr * .16, y0 + hr * .02), (bx2, y0 - hr * .03), (bx2 + hr * .16, y0 + hr * .02)]
        hv.line(pts, INK, lw * 1.05)
    # mũi
    hv.arc((hx - hr * .07, hy + hr * .32, hx + hr * .07, hy + hr * .46), 300, 80, INK, lw * .75)
    # miệng
    my = hy + hr * .68
    shape = 0
    if talk:
        shape = int((math.sin(t * 13.0) + math.sin(t * 7.3 + 1)) * 1.2 + 1.5) % 3
    if shape:
        op = hr * (.1 if shape == 1 else .2)
        hv.ell((hx - hr * .16, my - op * .5, hx + hr * .16, my + op), fill=(150, 50, 46), outline=INK, width=lw * .8)
    elif mood == "surprise":
        hv.ell((hx - hr * .1, my - hr * .08, hx + hr * .1, my + hr * .14), fill=(150, 50, 46), outline=INK, width=lw * .8)
    elif mood == "wink":
        hv.pie((hx - hr * .2, my - hr * .14, hx + hr * .2, my + hr * .18), 0, 180, fill=(150, 50, 46), outline=INK, width=lw * .8)
    elif mood == "smile":
        hv.arc((hx - hr * .22, my - hr * .18, hx + hr * .22, my + hr * .12), 15, 165, INK, lw * 1.1)
    elif mood == "think":
        hv.line([(hx - hr * .04, my + hr * .02), (hx + hr * .16, my - hr * .03)], INK, lw * 1.1)
    elif mood == "curious":
        hv.arc((hx - hr * .14, my - hr * .1, hx + hr * .18, my + hr * .08), 20, 160, INK, lw * 1.05)
    else:
        hv.line([(hx - hr * .13, my), (hx + hr * .13, my)], INK, lw * 1.1)
    pivot = (hx * SS, (hy + hr) * SS)
    head = hv.im.rotate(tilt + 1.2 * math.sin(t * math.tau / 5), resample=Image.BICUBIC, center=pivot)
    cv.im.alpha_composite(head)

    if pose == "think":
        arms()  # tay chống cằm đè lên mặt
    return cv.out()


# ---------------------------------------------------------------- ảnh AI có máy quay
_PHOTOS: dict = {}


def _load_photo(rel: str, grade: bool = True):
    key = rel if grade else rel + "#raw"
    if key not in _PHOTOS:
        path = Path(rel)
        path = path if path.is_absolute() else ROOT / rel
        img = None
        if path.is_file():
            img = Image.open(path).convert("RGBA")
            graded = _grade(img) if grade else img
            fg_path = path.with_name(path.stem + ".fg.png")
            fg = Image.open(fg_path).convert("RGBA") if fg_path.is_file() else None
            img = (graded, fg)
        if len(_PHOTOS) > 24:
            _PHOTOS.clear()
        _PHOTOS[key] = img
    return _PHOTOS[key]


def _grade(img: Image.Image) -> Image.Image:
    """Kéo ảnh AI về bảng màu kênh: vùng sáng ngả kem, vùng tối ngả chàm, phủ vân giấy 6%."""
    rgb = img.convert("RGB")
    gray = ImageOps.grayscale(rgb)
    toned = ImageOps.colorize(gray, black=mix(INK, (0, 0, 0), 0.2), white=PAPER, mid=mix(OCHRE, CREAM, 0.55))
    out = Image.blend(rgb, toned, 0.35).convert("RGBA")
    out.alpha_composite(_grain(out.width, out.height, 3))
    return out


def _cover(img: Image.Image, w: int, h: int, zoom: float, fx: float, fy: float) -> Image.Image:
    """Cắt ảnh phủ kín khung w×h, phóng `zoom`, tâm lệch fx, fy (-1..1)."""
    scale = max(w / img.width, h / img.height) * zoom
    cw, ch = w / scale, h / scale
    cx = img.width / 2 + fx * (img.width - cw) / 2
    cy = img.height / 2 + fy * (img.height - ch) / 2
    box = (cx - cw / 2, cy - ch / 2, cx + cw / 2, cy + ch / 2)
    return img.resize((w, h), Image.BICUBIC, box=box)


def _camera(a: dict) -> tuple[float, float, float]:
    cam = str(a.get("cam", "hold"))
    amount = clamp(float(a.get("amount", 0.05)), 0.0, 0.12)  # luật kênh: không quá 12%
    t, dur = float(a.get("_t", 0.0)), max(1.0, float(a.get("_dur", 6.0)))
    p = 1.0 if a.get("_pos") is None else ease_io(t / dur)
    zoom, fx, fy = 1.0 + amount, 0.0, 0.0
    if cam == "push_in":
        zoom = 1.0 + amount * p
    elif cam == "pull_out":
        zoom = 1.0 + amount * (1 - p)
    elif cam == "pan_left":
        fx = 0.9 - 1.8 * p
    elif cam == "pan_right":
        fx = -0.9 + 1.8 * p
    elif cam == "tilt_up":
        fy = 0.9 - 1.8 * p
    return zoom, fx, fy


def _placeholder(w, h, rel):
    cv = Cv(w, h)
    cv.rect((0, 0, w, h), fill=mix(CREAM, MUTED, 0.18))
    step = max(12, min(w, h) * 0.05)
    x = -h
    while x < w:
        cv.line([(x, h), (x + h, 0)], (*mix(CREAM, MUTED, 0.3), 255), max(1, step * .08))
        x += step
    size = int(min(w, h) * 0.045)
    cv.text((w / 2, h / 2 - size), "ẢNH AI CHƯA CÓ", "bold", size, INK)
    cv.text((w / 2, h / 2 + size * 0.7), Path(rel).name, "sans", int(size * 0.7), MUTED)
    return cv.out()


def photo(w, h, a):
    """Ảnh AI đã duyệt (args.file, đường dẫn trong repo) với máy quay. Có `<file>.fg.png` thì tiền cảnh đi nhanh hơn nền (parallax).
    Thiếu file: khung giữ chỗ ghi tên file, không văng lỗi."""
    rel = str(a.get("file", ""))
    loaded = _load_photo(rel, bool(a.get("grade", True))) if rel else None
    w, h = int(w), int(h)
    if not loaded:
        return _placeholder(w, h, rel or "(chưa ghi file)")
    bg, fg = loaded
    zoom, fx, fy = _camera(a)
    if str(a.get("fit", "cover")) == "contain":
        # tranh thật (vd. tranh Đông Hồ): hiện trọn tranh trên nền giấy, không cắt mép
        out = Image.new("RGBA", (w, h), (*PAPER, 255))
        out.alpha_composite(_grain(w, h, 3))
        room = float(a.get("margin", 0.9))
        scale = min(w * room / bg.width, h * room / bg.height) * zoom
        pw, ph = int(bg.width * scale), int(bg.height * scale)
        pic = bg.resize((pw, ph), Image.LANCZOS)
        x0, y0 = (w - pw) // 2, (h - ph) // 2
        shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rectangle((x0 + 10, y0 + 14, x0 + pw + 10, y0 + ph + 14), fill=(*INK, 70))
        out.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(12)))
        out.alpha_composite(pic, (x0, y0))
        return out
    if fg is not None and a.get("parallax"):
        back_ = _cover(bg, w, h, 1.0 + (zoom - 1.0) * 0.4 + 0.02, fx * 0.4, fy * 0.4)
        back_.alpha_composite(_cover(fg, w, h, zoom + 0.02, fx, fy))
        out = back_
    else:
        out = _cover(bg, w, h, zoom, fx, fy)
    return out


def photo_flash(w, h, a):
    """Chớp ảnh trong `hold` giây, bắt đầu ở tỉ lệ `at` của câu (ước theo 4 giây/câu nếu chưa biết độ dài câu)."""
    t = since(a)
    if t >= 90:
        return None  # ảnh duyệt: không chớp
    start = float(a.get("at", 0.8)) * float(a.get("lineSeconds", 6.0))
    hold = float(a.get("hold", 0.9))
    if not (start <= t <= start + hold):
        return Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))
    k = min(1.0, (t - start) / 0.08, (start + hold - t) / 0.12)
    img = photo(w, h, {**a, "cam": "push_in", "amount": 0.06})
    img.putalpha(img.getchannel("A").point(lambda v: int(v * clamp(k))))
    return img


def focus_pan(w, h, a):
    """Phóng tới một vùng của ảnh (`target` ghi trong `<file>.vung.json`, toạ độ 0..1). Chưa có vùng: phóng vào giữa."""
    rel = str(a.get("file", ""))
    loaded = _load_photo(rel, bool(a.get("grade", True))) if rel else None
    w, h = int(w), int(h)
    if not loaded:
        return _placeholder(w, h, rel or "focus_pan")
    bg, _ = loaded
    zone = (0.5, 0.5)
    try:
        import json

        path = (ROOT / rel).with_name(Path(rel).stem + ".vung.json")
        if path.is_file():
            zone = tuple(json.loads(path.read_text(encoding="utf-8")).get(str(a.get("target")), zone))
    except Exception:
        pass
    t = since(a)
    p = 1.0 if t >= 90 else ease_io(t / 1.6)
    zoom = 1.0 + (float(a.get("zoom", 1.3)) - 1.0) * p
    return _cover(bg, w, h, zoom, (zone[0] - .5) * 2 * p, (zone[1] - .5) * 2 * p)


# ---------------------------------------------------------------- đồng hồ 12 chi
def _clock_geo(s):
    c = s / 2
    return c, s * 0.46, s * 0.30, s * 0.16


def clock12(w, h, a):
    """Mặt đồng hồ 24 giờ chia 12 ô Địa chi (子 ở đỉnh = 23–1 giờ), kim quét chậm."""
    s = int(min(w, h))
    cv = Cv(s, s)
    c, R, r_mid, r_in = _clock_geo(s)
    cv.ell((c - R * 1.04, c - R * 1.04 + s * .012, c + R * 1.04, c + R * 1.04 + s * .012), fill=(*mix(CREAM, INK, 0.25), 90))
    cv.ell((c - R * 1.04, c - R * 1.04, c + R * 1.04, c + R * 1.04), fill=INK)
    cv.ell((c - R, c - R, c + R, c + R), fill=PAPER)
    for i in range(12):
        a0 = -90 - 15 + i * 30
        cv.pie((c - R, c - R, c + R, c + R), a0, a0 + 30, fill=PAPER if i % 2 else mix(PAPER, CREAM, 0.7))
        ang = math.radians(a0)
        cv.line([(c + math.cos(ang) * r_in, c + math.sin(ang) * r_in), (c + math.cos(ang) * R, c + math.sin(ang) * R)], (*mix(INK, PAPER, 0.6), 255), max(1.5, s * .004))
    for i in range(12):
        ang = math.radians(-90 + i * 30)
        x, y = c + math.cos(ang) * (R + r_mid) / 2, c + math.sin(ang) * (R + r_mid) / 2
        cv.text((x, y), BRANCHES[i], "hanzi", int(s * .075), INK)
        hx, hy = c + math.cos(ang) * (r_mid * .86), c + math.sin(ang) * (r_mid * .86)
        hour = (i * 2 + 23) % 24
        cv.text((hx, hy), f"{hour:02d}–{(hour + 2) % 24:02d}", "sans", int(s * .026), MUTED)
    cv.ell((c - r_in, c - r_in, c + r_in, c + r_in), fill=PAPER, outline=INK, width=max(2, s * .006))
    for k, lab in enumerate(("N", "E", "S", "W")):
        ang = math.radians(-90 + k * 90)
        cv.text((c + math.cos(ang) * r_in * .7, c + math.sin(ang) * r_in * .7), lab, "bold", int(s * .03), VERM if lab == "E" else MUTED)
    if a.get("hands", True):
        t = float(a.get("_t", 0.0))
        ang = math.radians(-90 + (t * 18) % 360)
        cv.line([(c, c), (c + math.cos(ang) * R * .9, c + math.sin(ang) * R * .9)], VERM, s * .012)
        cv.ell((c - s * .018, c - s * .018, c + s * .018, c + s * .018), fill=VERM)
    return cv.out()


def clock12_focus(w, h, a):
    """Lớp phủ đúng khớp clock12 (cùng x, y, s): tô đỏ ô `branch`, các nhãn `callouts` hiện lần lượt.
    Khung ngang: nhãn bên phải mặt đồng hồ. Khung dọc (ghi `layout: "below"`, `dial`, `cy` theo tỉ lệ chiều cao khung):
    mặt đồng hồ cỡ `dial`·h, tâm ở `cy`·h, nhãn xếp một hàng bên dưới."""
    full_w, full_h = int(w), int(h)
    below = str(a.get("layout", "side")) == "below"
    s = int(full_h * float(a.get("dial", 1.0)))
    cv = Cv(full_w, full_h)
    ox = (full_w - s) / 2
    oy = full_h * float(a.get("cy", 0.5)) - s / 2
    c, R, r_mid, r_in = _clock_geo(s)
    br = str(a.get("branch", "卯"))
    i = BRANCHES.index(br) if br in BRANCHES else 3
    t = since(a)
    k = 1.0 if t >= 90 else ease_out(t / 0.45)
    a0 = -90 - 15 + i * 30
    cv.pie((ox + c - R, oy + c - R, ox + c + R, oy + c + R), a0, a0 + 30, fill=(*VERM, int(215 * k)))
    ang = math.radians(-90 + i * 30)
    x, y = ox + c + math.cos(ang) * (R + r_mid) / 2, oy + c + math.sin(ang) * (R + r_mid) / 2
    cv.text((x, y), br, "hanzi", int(s * .075 * (1 + .25 * (1 - k))), (*WHITE, int(255 * k)))
    calls = [str(v) for v in a.get("callouts") or []]
    gap = float(a.get("stagger", 0.5))
    size = int(s * (.05 if not below else .06))
    widths = [cv.textlen(txt, "bold", size) + size * 1.2 for txt in calls]
    row_w = sum(widths) + size * 0.6 * max(0, len(calls) - 1)
    ex, ey = ox + c + math.cos(ang) * R, oy + c + math.sin(ang) * R
    cx_row = full_w / 2 - row_w / 2
    for j, txt in enumerate(calls):
        kj = 1.0 if t >= 90 else ease_out((t - 0.35 - j * gap) / 0.4)
        if kj <= 0.01:
            continue
        if below:
            px = cx_row + sum(widths[:j]) + size * 0.6 * j
            py = oy + s + size * 1.4 + (1 - kj) * size
        else:
            px = ex + s * .1 + (1 - kj) * s * .05
            py = ey - s * .14 + j * size * 2.2
            cv.line([(ex + s * .01, ey), (px - s * .02, py)], (*VERM, int(200 * kj)), s * .005)
        cv.rect((px, py - size * .85, px + widths[j], py + size * .85), fill=(*VERM, int(255 * kj)), r=size * .85)
        cv.text((px + size * .6, py + size * .05), txt, "bold", size, (*WHITE, int(255 * kj)), anchor="lm")
    return cv.out()


def sticker_peel(w, h, a):
    """Lớp phủ trên clock12: 12 nhãn tên con vật dán lên vành, rồi nhãn số `peel` (tính từ 1) bong ra bay đi."""
    s = int(min(w, h))
    cv = Cv(s, s)
    c, R, r_mid, r_in = _clock_geo(s)
    names = ANIMALS.get(str(a.get("set", "cn")), ANIMALS["cn"])
    peel = {int(v) - 1 for v in (a.get("peel") or [])}
    t = since(a)
    for i in range(12):
        kin = 1.0 if t >= 90 else ease_out((t - i * 0.05) / 0.3)
        if kin <= 0.01:
            continue
        ang = math.radians(-90 + i * 30)
        x, y = c + math.cos(ang) * (R + r_mid) / 2, c + math.sin(ang) * (R + r_mid) / 2
        size = int(s * .032)
        st = Cv(int(cv.textlen(names[i], "bold", size) + size * 1.2), int(size * 1.9))
        st.rect((1, 1, st.w - 1, st.h - 1), fill=OCHRE if i not in peel else WHITE, outline=INK, width=max(1.5, size * .08), r=size * .4)
        st.text((st.w / 2, st.h / 2 + size * .05), names[i], "bold", size, INK)
        img = st.out()
        dx = dy = rot = 0.0
        alpha = kin
        if i in peel:
            kp = 1.0 if t >= 90 else ease_out((t - 1.2) / 1.0)
            if t >= 90:
                continue
            dx, dy, rot = kp * s * .25, -kp * s * .2, kp * 35
            alpha *= 1 - kp * 0.85
            if kp > 0:
                img = img.rotate(rot, expand=True, resample=Image.BICUBIC)
        sc = 0.7 + 0.3 * back(kin)
        img = img.resize((max(2, int(img.width * sc)), max(2, int(img.height * sc))), Image.LANCZOS)
        img.putalpha(img.getchannel("A").point(lambda v, g=alpha: int(v * g)))
        cv.paste(img, (x + dx - img.width / 2, y + dy - img.height / 2))
    return cv.out()


# ---------------------------------------------------------------- chữ Hán
def hanzi(w, h, a):
    """Thẻ chữ Hán: char (to) có thể kèm neq (≠ chữ khác) và chú thích hai bên. branches: dải 12 chi nhỏ."""
    cv = Cv(w, h)
    ch = str(a.get("char", "卯"))
    neq = str(a.get("neq", "") or "")
    gl, gr = str(a.get("gloss_left", "") or ""), str(a.get("gloss_right", "") or "")
    cv.rect((w * .02, h * .04, w * .98, h * .96), fill=PAPER, outline=INK, width=max(2, min(w, h) * .012), r=min(w, h) * .05)
    t = since(a)
    if neq:
        size = int(min(h * .5, w * .3))
        cv.text((w * .27, h * .45), ch, "hanzi", size, INK)
        cv.text((w * .73, h * .45), neq, "hanzi", size, mix(INK, PAPER, 0.35))
        k = 1.0 if t >= 90 else ease_out((t - 0.6) / 0.4)
        cv.text((w * .5, h * .45), "≠", "display", int(size * .6 * (0.6 + 0.4 * k)), (*VERM, int(255 * k)))
        gs = int(min(h * .085, w * .06))
        if gl:
            cv.text((w * .27, h * .82), _fit(gl, gs, w * .44), "bold", _fit_text(gl, "bold", gs, w * .44), MUTED)
        if gr:
            cv.text((w * .73, h * .82), gr, "bold", _fit_text(gr, "bold", gs, w * .44), MUTED)
    else:
        size = int(min(h * .62, w * .62))
        cv.text((w / 2, h * .46), ch, "hanzi", size, INK)
        if gl or gr:
            g = (gl + ("  ·  " if gl and gr else "") + gr)
            cv.text((w / 2, h * .85), g, "bold", _fit_text(g, "bold", int(h * .08), w * .9), MUTED)
    return cv.out()


def _fit(txt, size, maxw):
    return txt


# ---------------------------------------------------------------- các hình còn lại của tập 01
def wheel_row(w, h, a):
    """Ba (hoặc hơn) vòng con giáp nhỏ đặt cạnh nhau, ô khác Trung Quốc được tô; nhãn nước bên dưới."""
    sets = [str(v) for v in a.get("sets") or ["cn", "kr", "jp"]]
    labels = [str(v) for v in a.get("labels") or [v.upper() for v in sets]]
    n = max(1, len(sets))
    cv = Cv(w, h)
    cell = w / n
    s = min(cell * .92, h * .82)
    for i, st in enumerate(sets):
        img = _wheel(s, s, {"set": st, "diff": True, "style": "line", "center": ""})
        cv.paste(img, (cell * i + (cell - s) / 2, 0))
        size = _fit_text(labels[i], "bold", int(h * .07), cell * .8)
        cv.text((cell * i + cell / 2, s + (h - s) / 2), labels[i].upper(), "bold", size, INK)
    return cv.out()


def split_vs(w, h, a):
    """Hai ô đặt cạnh nhau: con vật (hoặc chữ lớn `text`, tập 02) + nhãn mỗi bên, giữa là chữ VS (`vs: false` để ẩn)."""
    cv = Cv(w, h)
    for k, side in enumerate(("left", "right")):
        d = a.get(side) or {}
        x0 = w * (0.02 if k == 0 else 0.53)
        cv.rect((x0, h * .04, x0 + w * .45, h * .96), fill=PAPER, outline=INK, width=max(2, min(w, h) * .01), r=min(w, h) * .05)
        big = str(d.get("text", ""))
        if big:
            cv.text((x0 + w * .225, h * .42), big, "display", _fit_text(big, "display", int(h * .3), w * .38), VERM if k else INK)
        else:
            img = animal(int(w * .34), int(h * .74), {"kind": d.get("animal", "cat")})
            cv.paste(img, (x0 + w * .225 - img.width / 2, h * .08))
        lab = str(d.get("label", ""))
        cv.text((x0 + w * .225, h * .86), lab, "bold", _fit_text(lab, "bold", int(h * .08), w * .4), VERM if k else INK)
    if a.get("vs", True):
        cv.ell((w / 2 - h * .09, h / 2 - h * .09, w / 2 + h * .09, h / 2 + h * .09), fill=INK)
        cv.text((w / 2, h / 2), "VS", "bold", int(h * .07), WHITE)
    return cv.out()


def quote_card(w, h, a):
    """Trích dẫn ngắn trong khung giấy, nguồn nhỏ bên dưới. `strike: true` gạch đỏ (lời giải thích bị nghi ngờ)."""
    txt, src = str(a.get("text", "")), str(a.get("source", ""))
    cv = Cv(w, h)
    cv.rect((w * .02, h * .06, w * .98, h * .94), fill=PAPER, outline=INK, width=max(2, min(w, h) * .01), r=min(w, h) * .05)
    cv.text((w * .08, h * .22), "“", "display", int(h * .3), VERM)
    size = _fit_text(txt, "display", int(h * .2), w * .82)
    cv.text((w / 2, h * .48), txt, "display", size, INK)
    if src:
        cv.text((w / 2, h * .78), "— " + src, "sans", int(size * .5), MUTED)
    return cv.out()


def reading_stack(w, h, a):
    """Các dòng cách đọc xếp chồng (Mão / Mẹo / mèo), mỗi dòng hiện theo câu `line` riêng, có nhãn nhỏ."""
    rows = a.get("rows") or []
    cv = Cv(w, h)
    n = max(1, len(rows))
    rh = h / n
    pos = a.get("_pos")
    for i, row in enumerate(rows):
        line = row.get("line")
        visible = pos is None or line is None or (line is not None and pos >= int(line))
        if not visible:
            continue
        y = rh * i + rh / 2
        txt, tag = str(row.get("text", "")), str(row.get("tag", ""))
        size = int(rh * .52)
        color = VERM if i == n - 1 else INK
        cv.text((w * .42, y), txt, "display", _fit_text(txt, "display", size, w * .40), color, anchor="rm")  # căn phải ở 42%: chữ rộng quá 40% sẽ bị cắt mép trái
        if tag:
            _pill(cv, w * .72, y, tag.upper(), int(rh * .17), mix(CREAM, INK, 0.12), INK)
    if a.get("wave_link") and (pos is None or pos >= 2) and n >= 3:
        y0, y1 = rh * 1.5, rh * 2.5
        pts = [(w * .46 + math.sin(k / 4) * w * .02, y0 + (y1 - y0) * k / 20) for k in range(21)]
        cv.line(pts, VERM, max(2, h * .01))
    return cv.out()


def verse_card(w, h, a):
    """Câu thơ/ca dao tiếng Việt (đủ dấu) trên thẻ giấy; `typing` hiện dần từng chữ theo thời gian."""
    lines = [str(v) for v in a.get("vi") or []]
    cv = Cv(w, h)
    cv.rect((w * .02, h * .04, w * .98, h * .96), fill=PAPER, outline=VERM, width=max(2, min(w, h) * .01), r=min(w, h) * .04)
    n = max(1, len(lines))
    size = int(min(h * .7 / n / 1.5, w * .06))
    longest = max(lines, key=len) if lines else ""
    size = _fit_text(longest, "display", size, w * .86)
    total = sum(len(l) for l in lines)
    t = since(a)
    shown = total if (t >= 90 or not a.get("typing")) else int(max(0.0, t) * 28)
    y = h / 2 - (n - 1) * size * 1.5 / 2
    for ln in lines:
        part = ln[:max(0, min(len(ln), shown))]
        shown -= len(ln)
        if part:
            cv.text((w / 2, y), part, "display", size, INK)
        y += size * 1.5
    return cv.out()


def mask_flip(w, h, a):
    """Mặt nạ mèo cười lật sang mặt mèo săn mồi ở câu `flip_line` (lật quanh trục dọc)."""
    pos = a.get("_pos")
    fl = int(a.get("flip_line", 1))
    k = 1.0 if pos is None or pos > fl else (0.0 if pos < fl else ease_io(float(a.get("_lt", 0)) / 0.6))
    s = int(min(w, h))
    face = Cv(s, s)
    c = s / 2
    front = k < 0.5
    face.ell((c - s * .46, c - s * .42, c + s * .46, c + s * .46), fill=OCHRE if front else INK, outline=INK, width=s * .02)
    for sx in (-1, 1):
        face.poly([(c + sx * s * .4, c - s * .15), (c + sx * s * .34, c - s * .48), (c + sx * s * .12, c - s * .34)], fill=OCHRE if front else INK, outline=INK, width=s * .02)
        ex = c + sx * s * .17
        if front:
            face.arc((ex - s * .08, c - s * .06, ex + s * .08, c + s * .06), 200, 340, INK, s * .025)
        else:
            face.poly([(ex - s * .09, c - s * .02), (ex + s * .09, c - s * .06 * sx * -1 - s * .02), (ex, c + s * .04)], fill=OCHRE)
    if front:
        face.arc((c - s * .14, c + s * .08, c + s * .14, c + s * .26), 20, 160, INK, s * .025)
    else:
        face.poly([(c - s * .16, c + s * .16), (c + s * .16, c + s * .16), (c + s * .1, c + s * .26), (c - s * .1, c + s * .26)], fill=VERM)
        for sx in (-1, 1):
            face.poly([(c + sx * s * .1, c + s * .16), (c + sx * s * .06, c + s * .16), (c + sx * s * .08, c + s * .23)], fill=WHITE)
    img = face.out()
    sx = abs(math.cos(k * math.pi))
    img = img.resize((max(2, int(s * max(0.04, sx))), s), Image.LANCZOS)
    out = Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))
    out.alpha_composite(img, ((int(w) - img.width) // 2, (int(h) - s) // 2))
    labels = (str(a.get("front", "")), str(a.get("back", "")))
    lab = labels[0] if front else labels[1]
    if lab:
        cv = Cv(int(w), int(h))
        cv.paste(out, (0, 0))
        _pill(cv, w / 2, h * .92, lab.upper(), int(h * .05), JADE if front else VERM, WHITE)
        return cv.out()
    return out


def duality(w, h, a):
    """Hai nhãn đối nhau (guardian / omen) nối bằng một vạch, nhãn phải hiện sau."""
    cv = Cv(w, h)
    l, r = str(a.get("left", "")).upper(), str(a.get("right", "")).upper()
    size = int(h * .22)
    t = since(a)
    k = 1.0 if t >= 90 else ease_out((t - 0.5) / 0.4)
    _pill(cv, w * .25, h / 2, l, size, JADE, WHITE)
    cv.line([(w * .42, h / 2), (w * .42 + w * .16 * k, h / 2)], INK, h * .03)
    if k > 0.05:
        _pill(cv, w * .75, h / 2, r, size, (*VERM, int(255 * k)), (*WHITE, int(255 * k)))
    return cv.out()


def word_stack(w, h, a):
    """Các từ hiện lần lượt, xếp dọc, từ cuối màu đỏ son."""
    words = [str(v) for v in a.get("words") or []]
    gap = float(a.get("stagger", 0.3))
    t = since(a)
    cv = Cv(w, h)
    n = max(1, len(words))
    size = int(h / n * .55)
    for i, wd in enumerate(words):
        k = 1.0 if t >= 90 else ease_out((t - i * gap) / 0.35)
        if k <= 0.01:
            continue
        y = h * (i + .5) / n + (1 - k) * size * .4
        cv.text((w / 2, y), wd, "display", _fit_text(wd, "display", size, w * .95), (*(VERM if i == n - 1 else INK), int(255 * k)))
    return cv.out()


def two_asias(w, h, a):
    """Hình kết của kênh: hai vòng tròn giao nhau (Đông Á / Đông Nam Á), phần giao là Việt Nam."""
    cv = Cv(w, h)
    t = since(a)
    k = 1.0 if t >= 90 else ease_out(t / 0.8)
    r = min(w * .27, h * .44)
    off = r * (0.95 - 0.35 * (1 - k))
    cx, cy = w / 2, h * .46
    L = Image.new("L", (cv.w * SS, cv.h * SS), 0)
    ImageDraw.Draw(L).ellipse([(cx - off / 2 - r) * SS, (cy - r) * SS, (cx - off / 2 + r) * SS, (cy + r) * SS], fill=255)
    R = Image.new("L", L.size, 0)
    ImageDraw.Draw(R).ellipse([(cx + off / 2 - r) * SS, (cy - r) * SS, (cx + off / 2 + r) * SS, (cy + r) * SS], fill=255)
    lens = ImageChops.multiply(L, R)
    cv.im.paste((*INK, 255), (0, 0), L)
    cv.im.paste((*JADE, 255), (0, 0), R)
    cv.im.paste((*VERM, 255), (0, 0), lens)
    size = int(h * .06)
    cv.text((cx - off / 2 - r * .45, cy), str(a.get("left", "")), "bold", _fit_text(str(a.get("left", "")), "bold", size, r * .9), WHITE)
    cv.text((cx + off / 2 + r * .45, cy), str(a.get("right", "")), "bold", _fit_text(str(a.get("right", "")), "bold", size, r * .9), WHITE)
    cv.text((cx, cy + r + size * 1.3), str(a.get("center", "")), "display", int(size * 1.3), VERM)
    return cv.out()


def date_card(w, h, a):
    """Các dòng mốc ngày, mỗi dòng hiện theo `line` riêng."""
    rows = a.get("rows") or []
    pos = a.get("_pos")
    cv = Cv(w, h)
    n = max(1, len(rows))
    rh = h / n
    for i, row in enumerate(rows):
        line = row.get("line")
        if pos is not None and line is not None and pos < int(line):
            continue
        y = rh * (i + .5)
        txt = str(row.get("text", ""))
        size = _fit_text(txt, "bold", int(rh * .42), w * .86)
        cv.rect((w * .02, y - rh * .4, w * .98, y + rh * .4), fill=PAPER if i else INK, outline=INK, width=max(2, h * .008), r=rh * .2)
        cv.text((w / 2, y), txt, "bold", size, INK if i else WHITE)
    return cv.out()


def lang_tree(w, h, a):
    """Nhánh ngữ hệ đơn giản: root → branch → leaf, mũi tên nét đứt nối leaf với chữ `link` (giả thuyết)."""
    cv = Cv(w, h)
    nodes = [str(a.get("root", "")), str(a.get("branch", "")), str(a.get("leaf", ""))]
    size = int(h * .07)
    ys = [h * .16, h * .46, h * .78]
    for i, txt in enumerate(nodes):
        if not txt:
            continue
        x = w * (.3 + .1 * i)
        _pill(cv, x, ys[i], txt, _fit_text(txt, "bold", size, w * .5), INK if i < 2 else JADE, WHITE)
        if i:
            cv.line([(w * (.3 + .1 * (i - 1)), ys[i - 1] + size), (x, ys[i] - size)], INK, h * .008)
    link = str(a.get("link", ""))
    if link:
        x0, y0, x1, y1 = w * .55, ys[2], w * .82, ys[1]
        for k in range(10):
            p0, p1 = k / 10, (k + .5) / 10
            cv.line([(x0 + (x1 - x0) * p0, y0 + (y1 - y0) * p0), (x0 + (x1 - x0) * p1, y0 + (y1 - y0) * p1)], VERM, h * .008)
        cv.text((x1 + w * .06, y1), link, "hanzi", int(h * .2), VERM)
        cv.text((x1 + w * .06, y1 + h * .17), "THEORY", "bold", int(h * .045), MUTED)
    return cv.out()


# ---------- Tập 03: chữ quốc ngữ ----------
def viet_marks(w, h, a):
    """Chữ to (mặc định "Viet") rồi lần lượt hiện hai dấu trên một chữ cái: mũ (^) ở trên, chấm ở dưới, màu son, có nhãn nhỏ.
    args: word, letter (vị trí chữ mang dấu, mặc định 2), hat_at, dot_at (giây kể từ câu), labels (true/false)."""
    word = str(a.get("word", "Viet"))
    li = int(a.get("letter", 2))
    t = since(a)
    cv = Cv(w, h)
    size = _fit_text(word, "display", int(h * .62), w * .8)
    cx, cy = w / 2, h * .52
    tw = cv.textlen(word, "display", size)
    x0 = cx - tw / 2
    cv.text((cx, cy), word, "display", size, INK)
    xa = x0 + cv.textlen(word[:li], "display", size)
    xb = x0 + cv.textlen(word[:li + 1], "display", size)
    xm = (xa + xb) / 2
    top = cy - size * .32
    k1 = 1.0 if t >= 90 else ease_out((t - float(a.get("hat_at", 0.6))) / 0.35)
    k2 = 1.0 if t >= 90 else ease_out((t - float(a.get("dot_at", 1.4))) / 0.35)
    lw = max(3, size * .07)
    if k1 > 0.01:
        hw = (xb - xa) * .36; hy = top - size * .1 - (1 - k1) * size * .3
        cv.line([(xm - hw, hy + size * .13), (xm, hy - size * .02), (xm + hw, hy + size * .13)], (*VERM, int(255 * k1)), lw)
        if a.get("labels", True):
            _pill(cv, xm + size * .95, hy - size * .05, str(a.get("hat_label", "CHANGES THE VOWEL")), int(h * .075), (*VERM, int(255 * k1)), WHITE)
    if k2 > 0.01:
        r = size * .07 * (0.6 + 0.4 * k2); dy = cy + size * .44
        cv.ell((xm - r, dy - r, xm + r, dy + r), fill=(*VERM, int(255 * k2)))
        if a.get("labels", True):
            _pill(cv, xm + size * .95, dy + size * .02, str(a.get("dot_label", "SETS THE TONE")), int(h * .075), (*INK, int(255 * k2)), WHITE)
    return cv.out()


_ICONS = {}
def _icon(rel):
    if rel not in _ICONS:
        path = ROOT / rel
        _ICONS[rel] = Image.open(path).convert("RGBA") if path.is_file() else None
    return _ICONS[rel]


def tone_cards(w, h, a):
    """Sáu thẻ chữ "ma" (3 cột x 2 hàng). reveal=false: chỉ hiện chữ. reveal=true: từng thẻ lật ra hình + nghĩa ở mốc `at` (giây kể từ câu).
    args: words[6], meanings[6], icons[6] (đường dẫn hoặc ""), at[6], reveal, mark (chỉ số thẻ cần khoanh)."""
    words = list(a.get("words") or [])
    means = list(a.get("meanings") or [""] * len(words))
    icons = list(a.get("icons") or [""] * len(words))
    ats = list(a.get("at") or [0] * len(words))
    reveal = bool(a.get("reveal", True))
    t = since(a)
    cv = Cv(w, h)
    cols, rows = 3, 2
    gw, gh = w / cols, h / rows
    for i, wd in enumerate(words):
        c, r = i % cols, i // cols
        x0, y0 = c * gw + gw * .05, r * gh + gh * .05
        x1, y1 = x0 + gw * .9, y0 + gh * .9
        k = 0.0
        if reveal:
            k = 1.0 if t >= 90 else ease_out((t - float(ats[i])) / 0.3)
        hot = reveal and (t < 90) and 0 <= t - float(ats[i]) < (float(ats[i + 1]) - float(ats[i]) if i + 1 < len(ats) else 9)
        cv.rect((x0, y0, x1, y1), fill=PAPER, outline=VERM if hot else INK, width=max(3, gh * (.03 if hot else .015)), r=gh * .08)
        cw, ch = x1 - x0, y1 - y0
        size = int(ch * (.36 if k < .5 else .2))
        wy = y0 + ch * (.5 if k < .01 else .5 - .36 * k)
        cv.text(((x0 + x1) / 2, wy), str(wd), "display", size, VERM if hot else INK)
        if k > 0.01:
            ic = _icon(icons[i]) if i < len(icons) and icons[i] else None
            if ic is not None:
                ih = ch * .5; iw = ic.width * ih / ic.height
                if iw > cw * .8: iw = cw * .8; ih = ic.height * iw / ic.width
                im = ic.resize((max(2, int(iw * SS)), max(2, int(ih * SS))), Image.LANCZOS)
                if k < 1: im.putalpha(im.getchannel("A").point(lambda v: int(v * k)))
                cv.im.alpha_composite(im, (int(((x0 + x1) / 2 - iw / 2) * SS), int((y0 + ch * .27) * SS)))
                my = y0 + ch * .88
            else:
                my = y0 + ch * .62
            m = str(means[i]) if i < len(means) else ""
            if m:
                cv.text(((x0 + x1) / 2, my), m, "bold", _fit_text(m, "bold", int(ch * (.1 if ic is not None else .2)), cw * .9), (*INK, int(255 * k)))
    mk = a.get("mark")
    if mk is not None and not reveal:
        i = int(mk); c, r = i % cols, i // cols
    return cv.out()


def square_word(w, h, a):
    """Thư pháp chữ quốc ngữ xếp thành khối vuông như một chữ Hán: giấy đỏ, chữ mực đen, viết dần.
    args: top, bottom (hai nửa chữ, vd. "Ph" / "úc"), gloss (nghĩa nhỏ bên dưới)."""
    t = since(a)
    k = 1.0 if t >= 90 else clamp(t / 1.6)
    cv = Cv(w, h)
    side = min(w, h) * .82
    x0, y0 = w / 2 - side / 2, h * .04
    cv.rect((x0, y0, x0 + side, y0 + side), fill=VERM, outline=mix(VERM, INK, .4), width=max(2, side * .01), r=side * .03)
    ink = (20, 18, 16)
    top, bot = str(a.get("top", "Ph")), str(a.get("bottom", "úc"))
    s1 = _fit_text(top, "display", int(side * .5), side * .78)
    s2 = _fit_text(bot, "display", int(side * .5), side * .78)
    if k > 0.05:
        cv.text((w / 2, y0 + side * .3), top, "display", s1, (*ink, int(255 * clamp(k * 2))))
    if k > 0.5:
        cv.text((w / 2, y0 + side * .7), bot, "display", s2, (*ink, int(255 * clamp((k - .5) * 2))))
    g = str(a.get("gloss", ""))
    if g:
        cv.text((w / 2, y0 + side + h * .06), g, "bold", int(h * .05), INK)
    return cv.out()


ART = {
    # tên: (hàm, tỉ lệ rộng/cao, sống)
    "zodiac_wheel": (_wheel, 1.0, True),
    "map_ea": (map_ea, 1.6, True),
    "animal": (animal, 0.82, True),
    "kinetic": (kinetic, 5.0, True),
    "host": (host, 0.62, True),
    "photo": (photo, 16 / 9, True),
    "photo_flash": (photo_flash, 16 / 9, True),
    "focus_pan": (focus_pan, 16 / 9, True),
    "clock12": (clock12, 1.0, True),
    "clock12_focus": (clock12_focus, 2.3, True),
    "sticker_peel": (sticker_peel, 1.0, True),
    "hanzi": (hanzi, 1.5, True),
    "wheel_row": (wheel_row, 2.6, False),
    "split_vs": (split_vs, 2.0, False),
    "quote_card": (quote_card, 3.0, False),
    "reading_stack": (reading_stack, 1.6, True),
    "verse_card": (verse_card, 1.8, True),
    "mask_flip": (mask_flip, 1.0, True),
    "duality": (duality, 3.2, True),
    "word_stack": (word_stack, 1.2, True),
    "two_asias": (two_asias, 1.8, True),
    "date_card": (date_card, 2.4, True),
    "lang_tree": (lang_tree, 1.5, False),
    "viet_marks": (viet_marks, 2.2, True),
    "tone_cards": (tone_cards, 1.9, True),
    "square_word": (square_word, 0.9, True),
}
