"""Drawing helpers shared by scene templates. Colors and fonts come from the project style."""

from __future__ import annotations

import math
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from kit_video.contract import DesignContract
from kit_video.errors import ProductionError, issue


def clamp(value: float, low: float = 0.0, high: float = 1.0) -> float:
    if value < low:
        return low
    if value > high:
        return high
    return value


def smoothstep(value: float) -> float:
    value = clamp(value)
    return value * value * (3 - 2 * value)


def ease_out(value: float) -> float:
    value = clamp(value)
    return 1 - (1 - value) ** 3


def ease_in(value: float) -> float:
    value = clamp(value)
    return value ** 3


def progress(now: float, start: float, end: float) -> float:
    if end <= start:
        return 1.0 if now >= start else 0.0
    return clamp((now - start) / (end - start))


def lerp(start: float, end: float, amount: float) -> float:
    return start + (end - start) * amount


@lru_cache(maxsize=32)
def load_font(path: str, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(path, size)


class FrameContext:
    def __init__(self, scene: dict, style: dict, width: int, height: int, now: float, duration: float, cache: dict):
        self.scene = scene
        self.style = style
        self.params = scene.get("params") or {}
        self.width = width
        self.height = height
        self.t = now
        self.duration = duration
        self.cache = cache
        self.palette = style["palette"]
        self.contract = DesignContract.from_frame(style, width, height)
        cache.setdefault("contract", self.contract.summary())
        background = self.color("background")
        self.image = Image.new("RGBA", (width, height), (*background, 255))
        self.texts: set[str] = cache.setdefault("texts", set())
        self.violations: list[dict] = cache.setdefault("violations", [])
        self.samples: dict[str, list[int]] = cache.setdefault("samples", {})
        self.drawn_assets: list[dict] = cache.setdefault("drawn_assets", [])

    def color(self, name: str) -> tuple[int, int, int]:
        value = self.palette[name]
        return int(value[0]), int(value[1]), int(value[2])

    def safe(self) -> tuple[int, int, int, int]:
        area = self.style["safeArea"]
        return (
            int(self.width * area["left"]),
            int(self.height * area["top"]),
            int(self.width * (1 - area["right"])),
            int(self.height * (1 - area["bottom"])),
        )

    def font(self, size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
        typography = self.style["typography"]
        path = typography["fontBold" if bold else "fontRegular"]
        if not Path(path).is_file():
            raise ProductionError("BLOCKED", [issue("font_missing", path, f"Không thấy font {path}.")])
        return load_font(path, max(8, size))

    def blit(self, sprite: Image.Image, xy: tuple[float, float], opacity: float = 1) -> None:
        if opacity <= 0.02 or sprite.width < 1 or sprite.height < 1:
            return
        x = int(xy[0])
        y = int(xy[1])
        if x >= self.width or y >= self.height or x + sprite.width <= 0 or y + sprite.height <= 0:
            return
        layer = sprite
        if opacity < 0.99:
            layer = sprite.copy()
            alpha = layer.getchannel("A").point(lambda pixel, gain=opacity: int(pixel * gain))
            layer.putalpha(alpha)
        left = max(x, 0)
        top = max(y, 0)
        right = min(x + layer.width, self.width)
        bottom = min(y + layer.height, self.height)
        crop = layer.crop((left - x, top - y, right - x, bottom - y))
        self.image.alpha_composite(crop, (left, top))

    def text(
        self,
        content: str,
        xy: tuple[float, float],
        size: int,
        fill: tuple[int, int, int],
        bold: bool = False,
        anchor: str = "lt",
        opacity: float = 1,
    ) -> tuple[int, int, int, int] | None:
        if opacity <= 0.02 or not content:
            return None
        font = self.font(size, bold)
        from kit_video.visual_quality import missing_glyphs

        font_path = self.style["typography"]["fontBold" if bold else "fontRegular"]
        absent = missing_glyphs(font_path, content)
        if absent:
            self.violations.append({
                "text": content,
                "bbox": [0, 0, 0, 0],
                "t": round(self.t, 3),
                "reason": "Font thiếu glyph: " + "".join(absent),
            })
        probe = ImageDraw.Draw(self.image)
        box = probe.textbbox((0, 0), content, font=font)
        width = box[2] - box[0]
        height = box[3] - box[1]
        x, y = xy
        if anchor == "mm":
            x -= width / 2
            y -= height / 2
        elif anchor == "mt":
            x -= width / 2
        elif anchor == "mb":
            x -= width / 2
            y -= height
        elif anchor == "lm":
            y -= height / 2
        elif anchor == "rt":
            x -= width
        elif anchor == "rm":
            x -= width
            y -= height / 2
        image = Image.new("RGBA", (width + 4, height + 4), (0, 0, 0, 0))
        ImageDraw.Draw(image).text((2 - box[0], 2 - box[1]), content, font=font, fill=(*fill, 255))
        placed_x = int(x)
        placed_y = int(y)
        if getattr(self, "align_baseline", False) and anchor in ("lt", "mt", "rt"):
            # Xếp theo đường cơ sở: dòng không dấu không bị đẩy lên so với dòng có dấu cao (Ế, Ổ...).
            placed_y += box[1] - probe.textbbox((0, 0), "ẾẲỔ", font=font)[1]
        bbox = (placed_x, placed_y, placed_x + image.width, placed_y + image.height)
        bbox = self._shift_into_safe(content, bbox)
        self.blit(image, (bbox[0], bbox[1]), opacity)
        if opacity >= 0.85:
            self.texts.add(content)
            self.samples.setdefault(content, list(bbox))
        return bbox

    def _shift_into_safe(self, content: str, bbox: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
        left, top, right, bottom = self.safe()
        x0, y0, x1, y1 = bbox
        if x1 - x0 > right - left or y1 - y0 > bottom - top:
            self.violations.append({
                "text": content,
                "bbox": list(bbox),
                "t": round(self.t, 3),
                "reason": "Chữ rộng hơn vùng an toàn.",
            })
            return bbox
        dx = 0
        dy = 0
        if x0 < left:
            dx = left - x0
        if x1 + dx > right:
            dx = right - x1
        if y0 < top:
            dy = top - y0
        if y1 + dy > bottom:
            dy = bottom - y1
        return (x0 + dx, y0 + dy, x1 + dx, y1 + dy)

    def note_asset(self, asset_id: str, demo_label: str | None) -> None:
        self.drawn_assets.append({"assetId": asset_id, "t": round(self.t, 3), "demoLabel": demo_label})


def beat(ctx: FrameContext, name: str, default_fraction: tuple[float, float]) -> tuple[float, float]:
    custom = (ctx.params.get("beats") or {}).get(name)
    if isinstance(custom, list) and len(custom) == 2:
        return float(custom[0]), float(custom[1])
    return default_fraction[0] * ctx.duration, default_fraction[1] * ctx.duration


def scalar(ctx: FrameContext, name: str, default: float) -> float:
    value = ctx.params.get(name)
    if isinstance(value, (int, float)):
        return float(value)
    return default


def paint_background(ctx: FrameContext) -> None:
    key = ("bg", ctx.width, ctx.height, tuple(ctx.color("background")), tuple(ctx.color("paper")))
    sprite = ctx.cache.get(key)
    if sprite is None:
        top = ctx.color("paper")
        bottom = ctx.color("background")
        sprite = Image.new("RGBA", (ctx.width, ctx.height))
        draw = ImageDraw.Draw(sprite)
        steps = 24
        for index in range(steps):
            amount = index / (steps - 1)
            color = tuple(int(top[channel] + (bottom[channel] - top[channel]) * amount) for channel in range(3))
            y0 = int(index * ctx.height / steps)
            y1 = int((index + 1) * ctx.height / steps) + 1
            draw.rectangle((0, y0, ctx.width, y1), fill=(*color, 255))
        ctx.cache[key] = sprite
    ctx.image.alpha_composite(sprite)

    label = ctx.params.get("pipelineTestLabel")
    if isinstance(label, str) and label:
        ctx.text(label, (ctx.safe()[0], ctx.safe()[1]), max(16, int(ctx.height * 0.028)), ctx.color("muted") if "muted" in ctx.palette else ctx.color("ink"), bold=True)


def rounded(size: tuple[int, int], radius: int, fill: tuple[int, int, int], outline: tuple[int, int, int] | None = None) -> Image.Image:
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    shape = (0, 0, size[0] - 1, size[1] - 1)
    draw.rounded_rectangle(shape, radius=radius, fill=(*fill, 255), outline=(*outline, 255) if outline else None, width=3 if outline else 1)
    return image


def shadow_under(ctx: FrameContext, center: tuple[float, float], radius: float, opacity: float) -> None:
    if opacity <= 0.02:
        return
    sprite = Image.new("RGBA", (int(radius * 4) + 4, int(radius * 1.4) + 4), (0, 0, 0, 0))
    ImageDraw.Draw(sprite).ellipse((2, 2, sprite.width - 2, sprite.height - 2), fill=(20, 24, 32, int(90 * opacity)))
    ctx.blit(sprite, (center[0] - sprite.width / 2, center[1] - sprite.height / 2), 1)


def rotate(sprite: Image.Image, degrees: float) -> Image.Image:
    if abs(degrees) < 0.4:
        return sprite
    return sprite.rotate(degrees, resample=Image.Resampling.BICUBIC, expand=True)


def dial(size: int, ink: tuple[int, int, int], paper: tuple[int, int, int], turns: float) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((1, 1, size - 2, size - 2), fill=(*paper, 255), outline=(*ink, 255), width=max(3, size // 18))
    draw.ellipse((int(size * 0.28), int(size * 0.28), int(size * 0.72), int(size * 0.72)), outline=(*ink, 255), width=max(2, size // 28))
    angle = turns * math.tau - math.pi / 2
    cx = cy = size / 2
    radius = size * 0.34
    end = (cx + math.cos(angle) * radius, cy + math.sin(angle) * radius)
    draw.line((cx, cy, end[0], end[1]), fill=(*ink, 255), width=max(3, size // 16))
    return image
