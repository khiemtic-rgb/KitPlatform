"""Cutout stage. Motion, labels, and brand color come from the stage file and the shot plan.

The painter does not draw a scene by hand. It loads registered images, eases them,
and sets Vietnamese type with the project fonts so the letters are not baked into the art.
"""

from __future__ import annotations

import json
import math
from pathlib import Path

from PIL import Image, ImageDraw

from kit_video.canvas import FrameContext, ease_out, lerp, progress, smoothstep
from kit_video.errors import ProductionError, issue


def paint_reference_stage(ctx: FrameContext, project_dir: Path) -> None:
    spec = _spec(ctx, project_dir)
    plate = _scaled_plate(ctx, project_dir / str(spec["plate"]))
    ctx.image.alpha_composite(plate)
    occluded = False
    occlude_after = spec.get("occludeAfter")
    for layer in spec.get("layers") or []:
        if not isinstance(layer, dict):
            continue
        _paint_layer(ctx, project_dir, plate, layer)
        if occlude_after and layer.get("id") == occlude_after and not occluded:
            _paint_occluders(ctx, plate, spec.get("occluders") or [])
            occluded = True
    if not occluded:
        _paint_occluders(ctx, plate, spec.get("occluders") or [])
    for card in spec.get("cards") or []:
        if isinstance(card, dict):
            _paint_card(ctx, card)


def render_stage_frame(
    style: dict,
    params: dict,
    size: tuple[int, int],
    now: float,
    duration: float,
    project_dir: Path,
) -> tuple[Image.Image, set[str]]:
    scene = {"sceneId": "stage", "params": params, "templateId": "reference_stage"}
    ctx = FrameContext(scene, style, size[0], size[1], now, duration, {})
    paint_reference_stage(ctx, project_dir)
    return ctx.image, set(ctx.texts)


def pose_at(layer: dict, now: float) -> dict:
    """Anchor pose. Positions are fractions of the frame. The arc starts and ends at rest."""
    x = float(layer["x"])
    y = float(layer["y"])
    scale_x = 1.0
    scale_y = 1.0
    spin = 0.0
    lifted = 0.0
    ground_x = x
    ground_y = y
    motion = layer.get("motion")
    if isinstance(motion, dict):
        start = float(motion["start"])
        end = float(motion["end"])
        dest_x = float(motion["to"][0])
        dest_y = float(motion["to"][1])
        lift = float(motion.get("lift") or 0)
        spin_max = float(motion.get("spin") or 0)
        wind = 0.42
        if start - wind <= now < start:
            dip = math.sin(progress(now, start - wind, start) * math.pi)
            y += 0.012 * dip
            scale_y -= 0.08 * dip
            scale_x += 0.05 * dip
        elif start <= now <= end:
            amount = smoothstep(progress(now, start, end))
            ground_x = lerp(x, dest_x, amount)
            ground_y = lerp(y, dest_y, amount)
            lifted = math.sin(amount * math.pi)
            x = ground_x
            y = ground_y - lifted * lift
            spin = spin_max * lifted
            scale_y += 0.035 * lifted
            scale_x -= 0.025 * lifted
        elif now > end:
            ground_x = dest_x
            ground_y = dest_y
            x = dest_x
            y = dest_y
            settle = progress(now, end, end + 0.48)
            bounce = math.sin(settle * math.pi) * (1 - settle)
            y -= bounce * float(motion.get("settle") or 0.016)
            scale_y -= bounce * 0.07
            scale_x += bounce * 0.04
    if layer.get("breath"):
        wave = math.sin(now * 1.6)
        scale_y *= 1 + 0.01 * wave
    return {
        "x": x,
        "y": y,
        "groundX": ground_x,
        "groundY": ground_y,
        "sx": scale_x,
        "sy": scale_y,
        "spin": spin,
        "lifted": lifted,
    }


def _spec(ctx: FrameContext, project_dir: Path) -> dict:
    name = ctx.params.get("stageFile")
    if not isinstance(name, str) or not name:
        raise ProductionError("BLOCKED", [issue("stage_missing", ctx.scene.get("sceneId", "?"), "params.stageFile là bắt buộc.")])
    path = project_dir / name
    if not path.is_file():
        raise ProductionError("BLOCKED", [issue("stage_missing", name, f"Không thấy file sân khấu {name}.")])
    spec = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(spec, dict) or not spec.get("plate"):
        raise ProductionError("BLOCKED", [issue("stage_invalid", name, "File sân khấu cần plate và layers.")])
    return spec


def _fill(template: str, params: dict) -> str:
    text = str(template)
    for key, value in params.items():
        if isinstance(value, str):
            text = text.replace("{" + key + "}", value)
    return text


def _load_rgba(ctx: FrameContext, path: Path) -> Image.Image:
    key = ("stage-src", str(path))
    image = ctx.cache.get(key)
    if image is None:
        if not path.is_file():
            raise ProductionError("BLOCKED", [issue("asset_missing", path.name, f"Không thấy ảnh {path.name}.")])
        image = Image.open(path).convert("RGBA")
        ctx.cache[key] = image
    return image


def _scaled_plate(ctx: FrameContext, path: Path) -> Image.Image:
    key = ("stage-plate", str(path), ctx.width, ctx.height)
    image = ctx.cache.get(key)
    if image is None:
        source = _load_rgba(ctx, path)
        image = source.resize((ctx.width, ctx.height), Image.Resampling.LANCZOS)
        ctx.cache[key] = image
    return image


def _paint_occluders(ctx: FrameContext, plate: Image.Image, rows: list) -> None:
    for row in rows:
        if not isinstance(row, dict):
            continue
        rect = row.get("rect")
        if not isinstance(rect, list) or len(rect) != 4:
            continue
        box = _px(ctx, rect)
        if box[2] <= box[0] or box[3] <= box[1]:
            continue
        ctx.blit(plate.crop(box), (box[0], box[1]))


def _px(ctx: FrameContext, rect: list) -> tuple[int, int, int, int]:
    return (
        int(float(rect[0]) * ctx.width),
        int(float(rect[1]) * ctx.height),
        int(float(rect[2]) * ctx.width),
        int(float(rect[3]) * ctx.height),
    )


def _paint_layer(ctx: FrameContext, project_dir: Path, plate: Image.Image, layer: dict) -> None:
    del plate
    pose = pose_at(layer, ctx.t)
    height = max(2, int(float(layer.get("h") or 0.2) * ctx.height * pose["sy"]))
    source = _load_rgba(ctx, project_dir / str(layer["asset"]))
    if layer.get("flip"):
        source = source.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    width = max(2, int(source.width * (height / source.height) * pose["sx"] / max(pose["sy"], 0.01)))
    sprite = source.resize((width, height), Image.Resampling.LANCZOS)
    if abs(pose["spin"]) > 0.2:
        sprite = sprite.rotate(pose["spin"], expand=True, resample=Image.Resampling.BICUBIC)
    anchor = layer.get("anchor") or "center"
    left = pose["x"] * ctx.width - sprite.width / 2
    top = pose["y"] * ctx.height - (sprite.height if anchor == "bottom" else sprite.height / 2)
    if layer.get("shadow"):
        anchor_drop = float(layer.get("h") or 0.2) * 0.5 if anchor != "bottom" else 0.0
        _shadow(ctx, pose, float(layer.get("h") or 0.2), anchor_drop)
    ctx.blit(sprite, (left, top))
    ctx.note_asset(str(layer.get("id") or layer["asset"]), None)
    callout = layer.get("callout")
    if isinstance(callout, str) and callout:
        _callout(ctx, _fill(callout, ctx.params), left + sprite.width / 2, top)


def _shadow(ctx: FrameContext, pose: dict, height: float, anchor_drop: float) -> None:
    spread = (1 - 0.55 * pose["lifted"]) * height * ctx.height * 0.42
    if spread < 2:
        return
    ground_x = pose["groundX"] * ctx.width
    ground_y = (pose["groundY"] + anchor_drop) * ctx.height
    blob = Image.new("RGBA", (int(spread * 2) + 4, int(spread * 0.55) + 4), (0, 0, 0, 0))
    alpha = int(70 * (1 - 0.45 * pose["lifted"]))
    ImageDraw.Draw(blob).ellipse((2, 2, blob.width - 3, blob.height - 3), fill=(40, 28, 16, alpha))
    ctx.blit(blob, (ground_x - blob.width / 2, ground_y - blob.height / 2))


def _callout(ctx: FrameContext, content: str, center_x: float, top: float) -> None:
    if not content:
        return
    size = max(16, int(ctx.height * 0.034))
    font = ctx.font(size, True)
    probe = ImageDraw.Draw(ctx.image)
    box = probe.textbbox((0, 0), content, font=font)
    text_w = box[2] - box[0]
    text_h = box[3] - box[1]
    pad_x = int(size * 0.55)
    pad_y = int(size * 0.28)
    chip_w = text_w + pad_x * 2
    chip_h = text_h + pad_y * 2 + int(size * 0.28)
    chip = Image.new("RGBA", (chip_w, chip_h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(chip)
    paper = ctx.color("paper")
    accent = ctx.color("accent")
    draw.rounded_rectangle((0, 0, chip_w - 1, chip_h - 1), radius=max(4, size // 4), fill=(*paper, 235))
    draw.rectangle((0, 0, chip_w, max(4, size // 5)), fill=(*accent, 255))
    left = int(center_x - chip_w / 2)
    placed_top = int(top - chip_h - size * 0.35)
    ctx.blit(chip, (left, placed_top))
    ctx.text(content, (center_x, placed_top + pad_y + int(size * 0.22)), size, ctx.color("ink"), bold=True, anchor="mt")


def _paint_card(ctx: FrameContext, card: dict) -> None:
    amount = ease_out(progress(ctx.t, float(card.get("at") or 0), float(card.get("at") or 0) + float(card.get("in") or 0.55)))
    if amount <= 0.02:
        return
    title = _fill(str(card.get("text") or ""), ctx.params)
    sub = _fill(str(card.get("sub") or ""), ctx.params)
    if not title:
        return
    title_size = max(18, int(ctx.height * float(card.get("scale") or 0.05)))
    sub_size = max(14, int(title_size * 0.62))
    probe = ImageDraw.Draw(ctx.image)
    title_box = probe.textbbox((0, 0), title, font=ctx.font(title_size, True))
    sub_box = probe.textbbox((0, 0), sub, font=ctx.font(sub_size, True)) if sub else (0, 0, 0, 0)
    text_w = max(title_box[2] - title_box[0], sub_box[2] - sub_box[0])
    pad = int(title_size * 0.7)
    width = text_w + pad * 2
    height = (title_box[3] - title_box[1]) + (sub_box[3] - sub_box[1] if sub else 0) + pad * 2
    card_image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    draw = ImageDraw.Draw(card_image)
    paper = ctx.color("paper")
    accent = ctx.color("accent")
    draw.rounded_rectangle((0, 0, width - 1, height - 1), radius=max(8, title_size // 3), fill=(*paper, int(242 * amount)))
    draw.rectangle((0, 0, max(8, title_size // 4), height), fill=(*accent, int(255 * amount)))
    origin_x = float(card.get("x") or 0.5) * ctx.width - width / 2
    origin_y = float(card.get("y") or 0.16) * ctx.height - height / 2 + (1 - amount) * ctx.height * 0.02
    ctx.blit(card_image, (origin_x, origin_y), 1)
    ctx.text(title, (origin_x + width / 2, origin_y + pad * 0.7), title_size, ctx.color("ink"), bold=True, anchor="mt", opacity=amount)
    if sub:
        ctx.text(
            sub,
            (origin_x + width / 2, origin_y + pad * 0.7 + (title_box[3] - title_box[1]) + title_size * 0.2),
            sub_size,
            ctx.color("accent"),
            bold=True,
            anchor="mt",
            opacity=amount,
        )
