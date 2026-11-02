"""Studio adapter: spoken lines of a script -> knowledge scenes drawn by kit_video edu templates.

A script may ship a sidecar `<code>-<name>.hinh.json` next to its .txt (docs/marketing/<kênh>/kich-ban).
The sidecar lists scenes, each starting at a spoken line. bind() matches the lines Studio sends against
the sidecar; lines that match get a scene binding, other lines keep the older Studio drawing.

    {"palette": {...}, "scenes": [{"first": "Trước khi hỏi nên mua gì", "template": "edu_title", "params": {...}}, ...]}

No network, no paid call. Pure Pillow. Text and numbers come from the sidecar, which a person approves.
"""

from __future__ import annotations

import json
import re
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ENGINE = ROOT / "tools" / "kit-video-production"
if str(ENGINE) not in sys.path:
    sys.path.insert(0, str(ENGINE))

FONT_REGULAR = ["C:/Windows/Fonts/segoeui.ttf", "/usr/share/fonts/opentype/inter/Inter-Regular.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"]
FONT_BOLD = ["C:/Windows/Fonts/segoeuib.ttf", "/usr/share/fonts/opentype/inter/Inter-Bold.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"]
SIZES = {"tall": (1080, 1920), "wide": (1920, 1080)}
# Caption bands are drawn by the Studio (tall: y >= 1460, wide: y >= 780). Keep scenes above them.
SAFE = {
    "tall": {"left": 0.07, "right": 0.07, "top": 0.080, "bottom": 0.25},
    "wide": {"left": 0.06, "right": 0.06, "top": 0.125, "bottom": 0.30},
}
DEFAULT_PALETTE = {
    "background": [16, 30, 62], "paper": [26, 44, 84], "ink": [245, 240, 228], "muted": [170, 184, 208],
    "accent": [222, 176, 84], "good": [84, 196, 140], "warn": [236, 160, 70], "danger": [232, 100, 100],
}
_CACHE: dict = {"sidecars": None}


def _norm(text: str) -> str:
    text = unicodedata.normalize("NFC", str(text or "")).casefold()
    return re.sub(r"[^\w]+", " ", text).strip()


def _first_font(paths: list[str]) -> str:
    for path in paths:
        if Path(path).is_file():
            return path
    raise FileNotFoundError("Không thấy font cho cảnh kiến thức.")


def _sidecars() -> list[dict]:
    if _CACHE["sidecars"] is None:
        found = []
        for path in sorted((ROOT / "docs" / "marketing").glob("*/kich-ban/*.hinh.json")):
            try:
                data = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, json.JSONDecodeError):
                continue
            if isinstance(data.get("scenes"), list) and data["scenes"]:
                data["_file"] = path.name
                found.append(data)
        _CACHE["sidecars"] = found
    return _CACHE["sidecars"]


def bind(lines: list[str], orient: str = "tall") -> list[dict | None]:
    """One entry per spoken line: a scene binding, or None. All scene starts must be found, in order."""
    orient = "wide" if orient == "wide" else "tall"
    out: list[dict | None] = [None] * len(lines)
    normal = [_norm(line) for line in lines]
    for sidecar in _sidecars():
        starts, cursor, ok = [], 0, True
        for scene in sidecar["scenes"]:
            key = _norm(scene.get("first", ""))
            hit = next((i for i in range(cursor, len(lines)) if key and normal[i].startswith(key)), -1)
            if hit < 0:
                ok = False
                break
            starts.append(hit)
            cursor = hit + 1
        if not ok or starts[0] != 0:
            continue
        starts.append(len(lines))
        for number, scene in enumerate(sidecar["scenes"]):
            first, end = starts[number], starts[number + 1]
            for pos, index in enumerate(range(first, end)):
                out[index] = {
                    "sidecar": sidecar["_file"], "scene": number, "first": first, "count": end - first, "pos": pos,
                    "template": scene["template"], "params": scene.get("params") or {},
                    "palette": sidecar.get("palette") or DEFAULT_PALETTE, "orient": orient, "brand": sidecar.get("brand") or "", "logo": sidecar.get("logo") or "", "tagline": sidecar.get("tagline") or "", "footer": sidecar.get("footer") or "", "background": sidecar.get("background") or "",
                }
        return out
    return out


def _style(binding: dict) -> dict:
    orient = binding.get("orient", "tall")
    return {
        "profileId": "edu-" + str(binding.get("sidecar", "")),
        "palette": {k: tuple(v) for k, v in (binding.get("palette") or DEFAULT_PALETTE).items()},
        "typography": {"fontRegular": _first_font(FONT_REGULAR), "fontBold": _first_font(FONT_BOLD)},
        "safeArea": SAFE[orient],
    }


MIN_TEXT = {"tall": 30, "wide": 28}  # px: below this the line is not readable on a phone
MIN_FILL = 0.62  # share of the safe height the scene should occupy
_FIT: dict = {}


def _context(binding: dict, t: float, duration: float, zoom: float = 1.0, dy: int = 0, pos=None, line_t: float = 1.0):
    from kit_video.canvas import FrameContext

    class Ctx(FrameContext):
        def text(self, text, xy, size, *args, **kwargs):
            if str(text).strip():
                self.sizes.append(int(size))
            return super().text(text, xy, size, *args, **kwargs)

    w, h = SIZES[binding.get("orient", "tall")]
    scene = {"sceneId": f"{binding.get('sidecar')}#{binding.get('scene')}", "params": binding["params"]}
    ctx = Ctx(scene, _style(binding), w, h, t, duration, {})
    ctx.sizes = []
    ctx.align_baseline = True
    ctx.edu_zoom = zoom
    ctx.edu_dy = dy
    ctx.edu_pos = pos
    ctx.edu_lt = line_t
    return ctx


_LOGO: dict = {}


def _logo(path: str):
    if path not in _LOGO:
        from PIL import Image

        file = Path(path)
        file = file if file.is_absolute() else ROOT / file
        _LOGO[path] = Image.open(file).convert("RGBA") if file.is_file() else None
    return _LOGO[path]


def _brand_mark(ctx, binding: dict) -> None:
    """Channel lockup (shield, name, tagline) in the top margin of every scene, above the safe area."""
    name = str(binding.get("brand") or "").strip()
    if not name:
        return
    from PIL import ImageDraw

    orient = binding.get("orient", "tall")
    left = int(ctx.width * SAFE[orient]["left"])
    box_h = int(ctx.height * (0.036 if orient == "tall" else 0.050))
    top = int(ctx.height * (0.020 if orient == "tall" else 0.014))
    x = left
    logo = _logo(str(binding.get("logo") or "")) if binding.get("logo") else None
    if logo is not None:
        w = int(logo.width * box_h / logo.height)
        ctx.image.alpha_composite(logo.resize((w, box_h)), (x, top))
        x += w + int(box_h * 0.28)
    draw = ImageDraw.Draw(ctx.image)
    ink, accent = ctx.color("ink"), ctx.color("accent")
    name_size = int(box_h * 0.42)
    draw.text((x, top + int(box_h * 0.04)), name.upper(), font=ctx.font(name_size, True), fill=(*ink, 255))
    tagline = str(binding.get("tagline") or "").strip()
    if tagline:
        draw.text((x, top + int(box_h * 0.62)), tagline, font=ctx.font(int(box_h * 0.27), False), fill=(*accent, 235))


def overlay(image, binding: dict):
    """Small disclaimer at the bottom edge. Drawn after the caption shade so the shade cannot hide it."""
    from PIL import Image, ImageDraw, ImageFont

    text = str(binding.get("footer") or "").strip()
    if not text:
        return image
    base = image.convert("RGBA")
    size = int(base.height * (0.0125 if binding.get("orient", "tall") == "tall" else 0.021))
    font = ImageFont.truetype(_first_font(FONT_REGULAR), size)
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    muted = tuple((binding.get("palette") or DEFAULT_PALETTE).get("muted", [170, 184, 208]))
    d.text((base.width / 2, base.height - int(base.height * 0.03)), text, font=font, fill=(*muted, 210), anchor="mm")
    base.alpha_composite(layer)
    return base.convert("RGB")


_BG: dict = {}


def _ngv_background(ctx) -> None:
    """Background of the channel videos: navy glow in the middle, dark edges, faint candle bars in the middle band."""
    import random

    import numpy as np
    from PIL import Image, ImageDraw

    key = (ctx.width, ctx.height, tuple(ctx.color("background")), tuple(ctx.color("paper")))
    sprite = _BG.get(key)
    if sprite is None:
        w, h = ctx.width, ctx.height
        bg, paper = np.array(ctx.color("background"), float), np.array(ctx.color("paper"), float)
        centre = bg + (paper - bg) * 0.5
        edge = bg * 0.55
        yy, xx = np.mgrid[0:h, 0:w]
        d = np.sqrt(((xx - w * 0.5) / (w * 0.75)) ** 2 + ((yy - h * 0.52) / (h * 0.60)) ** 2)
        k = np.clip(d, 0, 1)[..., None] ** 1.3
        arr = centre * (1 - k) + edge * k
        sprite = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
        layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        dr = ImageDraw.Draw(layer)
        rnd = random.Random(11)
        tall = h > w
        # Nến xanh (tăng) và đỏ (giảm), cao hơn: đi theo một chuỗi giá bước ngẫu nhiên có xu hướng.
        band_top, band_bot = (0.40, 0.84) if tall else (0.36, 0.94)
        step = int(w * (0.045 if tall else 0.026))
        count = w // step + 2
        import math

        walk = []
        previous = 0.0
        for k in range(count):
            # Đường giá lượn sóng (không trôi hẳn một phía) cộng nhiễu, để nến trải đều khắp khung.
            level = 2.2 * math.sin(k * 0.23) + 1.2 * math.sin(k * 0.071 + 1.0) + rnd.uniform(-0.9, 0.9)
            walk.append((previous, level))
            previous = level
        lo = min(min(a, b) for a, b in walk) - 0.8
        hi = max(max(a, b) for a, b in walk) + 0.8
        top_y, bot_y = int(h * band_top), int(h * band_bot)

        def py(value: float) -> int:
            return int(bot_y - (value - lo) / (hi - lo) * (bot_y - top_y))

        green, red = tuple(ctx.color("good")), tuple(ctx.color("danger"))
        x = step // 2
        for opened, closed in walk:
            if x >= w:
                break
            up = closed >= opened
            rgb = green if up else red
            high = max(opened, closed) + rnd.uniform(0.15, 0.7)
            low = min(opened, closed) - rnd.uniform(0.15, 0.7)
            bw = max(10, int(step * 0.42))
            dr.line((x, py(high), x, py(low)), fill=(*rgb, 58), width=3)
            y0, y1 = sorted((py(opened), py(closed)))
            dr.rectangle((x - bw // 2, y0, x + bw // 2, max(y1, y0 + 4)), fill=(*rgb, 64))
            x += step
        sprite.alpha_composite(layer)
        _BG[key] = sprite
    ctx.image.alpha_composite(sprite)


def _draw(binding: dict, t: float, duration: float, zoom: float = 1.0, dy: int = 0, pos=None, line_t: float = 1.0):
    import kit_video.edu_scenes as scenes
    from kit_video.edu_scenes import TEMPLATES

    ctx = _context(binding, t, duration, zoom, dy, pos, line_t)
    original = scenes.paint_background
    if binding.get("background") == "ngv":
        scenes.paint_background = _ngv_background
    try:
        TEMPLATES[binding["template"]](ctx, {}, ROOT, ROOT)
    finally:
        scenes.paint_background = original
    _brand_mark(ctx, binding)
    return ctx


def _extent(binding: dict, ctx) -> tuple[int, int]:
    """Top and bottom row of what the scene drew (difference against the empty background)."""
    from PIL import ImageChops
    from kit_video.canvas import paint_background

    blank = _context(binding, 0.0, 8.0)
    paint_background(blank)
    box = ImageChops.difference(ctx.image.convert("RGB"), blank.image.convert("RGB")).convert("L").point(lambda v: 255 if v > 14 else 0).getbbox()
    return (box[1], box[3]) if box else (0, 0)


def _fit_scene(binding: dict) -> tuple[float, int, list[str]]:
    """Largest zoom (readable text, no overflow) so the scene fills the safe area, and the shift that centres it."""
    orient = binding.get("orient", "tall")
    key = (binding.get("sidecar"), binding.get("scene"), orient, json.dumps(binding.get("params"), sort_keys=True, ensure_ascii=False))
    if key in _FIT:
        return _FIT[key]
    top = int(SIZES[orient][1] * SAFE[orient]["top"])
    bottom = int(SIZES[orient][1] * (1 - SAFE[orient]["bottom"]))
    avail = bottom - top
    best = (1.0, 0, [])
    # chart and flow already stretch to the full safe height; the others grow with their text.
    top_zoom = {"edu_chart": 1.0, "edu_flow": 1.0, "edu_title": 1.5}.get(binding.get("template"), 1.35)
    zoom = top_zoom
    chosen = None
    while zoom >= 0.79:
        ctx = _draw(binding, 7.0, 8.0, zoom)
        if not ctx.violations and not getattr(ctx, "dropped", False):
            chosen = (zoom, ctx)
            break
        zoom -= 0.1
    notes: list[str] = []
    if chosen is None:
        ctx = _draw(binding, 7.0, 8.0, 1.0)
        chosen = (1.0, ctx)
    zoom, ctx = chosen
    first, last = _extent(binding, ctx)
    height = max(1, last - first)
    dy = 0
    if binding.get("template") != "edu_title":
        dy = max(0, (avail - height) // 2 - max(0, first - top))
    if height < MIN_FILL * avail and binding.get("template") != "edu_title":
        notes.append("Khung quá trống, nên thêm nội dung hoặc đổi mẫu cảnh.")
    low = min(ctx.sizes) if ctx.sizes else 99
    if low < MIN_TEXT[orient]:
        notes.append(f"Chữ nhỏ nhất {low}px, dưới mức đọc được {MIN_TEXT[orient]}px.")
    best = (zoom, dy, notes)
    _FIT[key] = best
    return best


def _render(binding: dict, t: float, duration: float, pos=None, line_t: float = 1.0) -> tuple:
    zoom, dy, notes = _fit_scene(binding)
    ctx = _draw(binding, t, duration, zoom, dy, pos, line_t)
    problems = list(ctx.violations) + [{"text": str(binding.get("sidecar")), "bbox": [0, 0, 0, 0], "t": 0, "reason": note} for note in notes]
    return ctx.image.convert("RGB"), problems


def warp(scene_seconds: float) -> float:
    """Reveal times in the templates assume ~5 s. Longer scenes stretch them so the last reveal lands near 55%."""
    return min(1.0, max(0.35, 8.7 / max(scene_seconds, 0.5)))


def still(binding: dict, dest: str) -> list[dict]:
    """The picture Studio shows for approval: the scene as it looks when this line has been spoken."""
    image, problems = _render(binding, 6.5, 8.0)  # the approval picture is the finished scene
    image.save(dest)
    return problems


def frame(binding: dict, scene_t: float, scene_seconds: float, line_t: float = 1.0):
    image, _ = _render(binding, max(0.0, scene_t) * warp(scene_seconds), max(scene_seconds, 1.0), binding.get("pos"), max(0.0, line_t))
    return image
