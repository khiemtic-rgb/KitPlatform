"""Reusable scene templates. Content, timing, and color come from the shot plan."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw

from kit_video.canvas import (
    FrameContext,
    beat,
    dial,
    ease_in,
    ease_out,
    lerp,
    paint_background,
    progress,
    rotate,
    rounded,
    scalar,
    shadow_under,
    smoothstep,
)
from kit_video.errors import ProductionError, issue


def optional_color(ctx: FrameContext, name: str, fallback: str) -> tuple[int, int, int]:
    if name in ctx.palette:
        return ctx.color(name)
    return ctx.color(fallback)


def stock_story(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    from kit_video.picture import paint_stock_story

    paint_stock_story(ctx)


def reference_stage(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root
    from kit_video.stage import paint_reference_stage

    paint_reference_stage(ctx, project_dir)


def designed_board(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    from kit_video.design import designed_board as paint_board

    paint_board(ctx, assets, repo_root, project_dir)


def plate_camera(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    """Reframe one registered still. The preset does not invent a gesture or a second person."""
    from kit_video.camera import CameraMove, render_plate
    from kit_video.validate import resolve_path

    asset_id = ctx.params.get("assetId")
    row = assets.get(asset_id) if isinstance(asset_id, str) else None
    if row is None:
        raise ProductionError("BLOCKED", [issue("asset_missing", ctx.scene["sceneId"], "plate_camera cần assetId đã khai.")])
    path = resolve_path(row, project_dir, repo_root)
    if path is None or not path.is_file():
        raise ProductionError("BLOCKED", [issue("asset_missing", str(asset_id), f"Không thấy ảnh {asset_id}.")])
    move = CameraMove.parse(ctx.params.get("preset"), ctx.params.get("amount", 0))
    cache_key = ("plate-source", str(path))
    source = ctx.cache.get(cache_key)
    if source is None:
        source = Image.open(path).convert("RGBA")
        source.load()
        ctx.cache[cache_key] = source
    plate = render_plate(source, (ctx.width, ctx.height), move, ctx.t, ctx.duration, ctx.color("background"))
    ctx.image.alpha_composite(plate)
    ctx.note_asset(str(asset_id), None)
    caption = ctx.params.get("caption")
    if isinstance(caption, str) and caption:
        appear = ease_out(progress(ctx.t, ctx.duration * 0.12, ctx.duration * 0.28))
        bar_w = int(ctx.width * 0.72)
        bar_h = int(ctx.height * 0.09)
        chip = Image.new("RGBA", (bar_w, bar_h), (*ctx.color("paper"), 232))
        ctx.blit(chip, ((ctx.width - bar_w) / 2, ctx.height * 0.07), appear)
        ctx.text(caption, (ctx.width / 2, ctx.height * 0.085), int(ctx.height * 0.042), ctx.color("ink"), bold=True, anchor="mt", opacity=appear)


def draw_scene(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> Image.Image:
    template = ctx.scene["templateId"]
    painter = TEMPLATES.get(template)
    if painter is None:
        raise ProductionError("BLOCKED", [issue(
            "template_missing",
            ctx.scene["sceneId"],
            f"Chưa có template '{template}'. Cảnh đặc biệt cần thiết kế riêng hoặc một template đã đăng ký.",
        )])
    painter(ctx, assets, repo_root, project_dir)
    return ctx.image


def kinetic_type(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    lines = ctx.params.get("lines") or []
    for index, line in enumerate(lines):
        if not isinstance(line, dict):
            continue
        start = float(line.get("at", index * 0.8))
        amount = ease_out(progress(ctx.t, start, start + float(line.get("in", 0.45))))
        y = ctx.height * (0.34 + index * 0.16) + (1 - amount) * ctx.height * 0.03
        size = int(ctx.height * float(line.get("scale", 0.07)))
        ctx.text(str(line.get("text", "")), (ctx.width / 2, y), size, ctx.color("ink"), bold=bool(line.get("bold", True)), anchor="mt", opacity=amount)


def object_move(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    draw = ImageDraw.Draw(ctx.image)
    ground = int(ctx.height * 0.72)
    draw.rounded_rectangle((int(ctx.width * 0.12), ground, int(ctx.width * 0.88), ground + int(ctx.height * 0.02)), radius=8, fill=(*ctx.color("ink"), 255))
    for item in ctx.params.get("objects") or []:
        if not isinstance(item, dict):
            continue
        start = float(item.get("start", 0.2))
        end = float(item.get("end", start + 1))
        amount = ease_in(progress(ctx.t, start, end)) if item.get("ease") == "in" else ease_out(progress(ctx.t, start, end))
        x = lerp(float(item["from"][0]), float(item["to"][0]), amount) * ctx.width
        y = lerp(float(item["from"][1]), float(item["to"][1]), amount) * ctx.height
        box_w = int(ctx.width * float(item.get("w", 0.1)))
        box_h = int(ctx.height * float(item.get("h", 0.14)))
        sprite = rounded((box_w, box_h), 16, ctx.color(item.get("color", "accent")), ctx.color("ink"))
        shadow_under(ctx, (x, y + box_h * 0.55), box_w * 0.28, 0.7)
        ctx.blit(sprite, (x - box_w / 2, y - box_h))
        if item.get("label"):
            ctx.text(str(item["label"]), (x, y - box_h * 0.55), int(box_h * 0.28), ctx.color("paper"), bold=True, anchor="mm")
    caption = ctx.params.get("caption")
    if isinstance(caption, dict):
        amount = ease_out(progress(ctx.t, float(caption.get("at", 0.5)), float(caption.get("at", 0.5)) + 0.4))
        ctx.text(str(caption.get("text", "")), (ctx.width / 2, ctx.height * 0.84), int(ctx.height * 0.055), ctx.color("ink"), bold=True, anchor="mt", opacity=amount)


def object_transform(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    start, end = beat(ctx, "transform", (0.2, 0.75))
    amount = smoothstep(progress(ctx.t, start, end))
    width = int(ctx.width * lerp(0.16, 0.2, amount))
    height = int(ctx.height * lerp(0.22, 0.16, amount))
    face = [int(lerp(ctx.color("paper")[channel], optional_color(ctx, "weight", "ink")[channel], amount)) for channel in range(3)]
    sprite = rounded((width, height), int(lerp(18, 8, amount)), tuple(face), ctx.color("ink"))
    x = ctx.width * 0.5
    y = ctx.height * lerp(0.48, 0.62, ease_in(amount))
    shadow_under(ctx, (x, y + height * 0.2), width * lerp(0.3, 0.55, amount), 0.5 + amount * 0.5)
    ctx.blit(sprite, (x - width / 2, y - height / 2))
    caption = ctx.params.get("caption")
    if isinstance(caption, str):
        ctx.text(caption, (ctx.width / 2, ctx.height * 0.82), int(ctx.height * 0.05), ctx.color("ink"), bold=True, anchor="mt", opacity=ease_out(progress(ctx.t, end, end + 0.35)))


def character_entrance(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    enter = ease_out(progress(ctx.t, 0.1, scalar(ctx, "enterEnd", ctx.duration * 0.45)))
    x = lerp(-0.08, float(ctx.params.get("toX", 0.32)), enter) * ctx.width
    y = ctx.height * 0.62
    look = "down" if ctx.t > ctx.duration * 0.62 else "shelf"
    _person(ctx, (x, y), look, 1)
    prop = rounded((int(ctx.width * 0.1), int(ctx.height * 0.12)), 12, ctx.color("accent"), ctx.color("ink"))
    prop_x = lerp(ctx.width * 0.7, ctx.width * float(ctx.params.get("propX", 0.55)), enter)
    ctx.blit(prop, (prop_x, y - prop.height * 0.2))
    reach = smoothstep(progress(ctx.t, ctx.duration * 0.4, ctx.duration * 0.7))
    shoulder = (x + ctx.width * 0.03, y - ctx.height * 0.12)
    hand = (lerp(shoulder[0], prop_x, reach), lerp(shoulder[1], y - prop.height * 0.1, reach))
    ImageDraw.Draw(ctx.image).line((*shoulder, *hand), fill=(*ctx.color("ink"), 255), width=max(4, int(ctx.width * 0.006)))
    caption = ctx.params.get("caption")
    if isinstance(caption, str):
        ctx.text(caption, (ctx.width / 2, ctx.safe()[1]), int(ctx.height * 0.05), ctx.color("ink"), bold=True, anchor="mt", opacity=ease_out(progress(ctx.t, ctx.duration * 0.2, ctx.duration * 0.35)))


def calendar_flip(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    flips = ctx.params.get("flips") or [ctx.duration * 0.2, ctx.duration * 0.45, ctx.duration * 0.7]
    scale = float(ctx.params.get("scale", 1))
    at = float(ctx.params.get("at", 0.46))
    _calendar(ctx, (ctx.width * 0.5, ctx.height * at), int(ctx.width * 0.28 * scale), flips, 1)
    caption = ctx.params.get("caption")
    if isinstance(caption, str):
        ctx.text(caption, (ctx.width / 2, ctx.height * 0.8), int(ctx.height * 0.048), ctx.color("ink"), bold=True, anchor="mt")


def data_connection(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    nodes = [node for node in (ctx.params.get("nodes") or []) if isinstance(node, dict)]
    positions = {}
    for node in nodes:
        positions[node["id"]] = (float(node["at"][0]) * ctx.width, float(node["at"][1]) * ctx.height)
        appear = ease_out(progress(ctx.t, float(node.get("start", 0.2)), float(node.get("start", 0.2)) + 0.45))
        if appear <= 0.04:
            continue
        radius = max(4, int(ctx.width * 0.055 * appear))
        sprite = Image.new("RGBA", (radius * 2, radius * 2), (0, 0, 0, 0))
        ImageDraw.Draw(sprite).ellipse((1, 1, radius * 2 - 2, radius * 2 - 2), fill=(*ctx.color("paper"), 255), outline=(*ctx.color("accent"), 255), width=max(2, radius // 8))
        center = positions[node["id"]]
        ctx.blit(sprite, (center[0] - radius, center[1] - radius), appear)
        ctx.text(str(node.get("label", "")), (center[0], center[1] + radius + ctx.height * 0.02), int(ctx.height * 0.04), ctx.color("ink"), bold=True, anchor="mt", opacity=appear)
    draw = ImageDraw.Draw(ctx.image)
    for link in ctx.params.get("links") or []:
        if not isinstance(link, dict) or link.get("from") not in positions or link.get("to") not in positions:
            continue
        amount = ease_in(progress(ctx.t, float(link.get("start", 1)), float(link.get("end", 2))))
        start = positions[link["from"]]
        end = positions[link["to"]]
        point = (lerp(start[0], end[0], amount), lerp(start[1], end[1], amount))
        if amount > 0.02:
            draw.line((*start, *point), fill=(*ctx.color("accent"), 255), width=max(4, ctx.width // 240))
    caption = ctx.params.get("caption")
    if isinstance(caption, str):
        ctx.text(caption, (ctx.width / 2, ctx.height * 0.84), int(ctx.height * 0.042), ctx.color("ink"), bold=True, anchor="mt", opacity=ease_out(progress(ctx.t, ctx.duration * 0.55, ctx.duration * 0.7)))


def ui_showcase(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    paint_background(ctx)
    asset_id = ctx.params.get("assetId")
    row = assets.get(asset_id) if isinstance(asset_id, str) else None
    if row is None or row.get("role") != "product_ui":
        raise ProductionError("BLOCKED", [issue("product_ui", ctx.scene["sceneId"], "ui_showcase cần assetId có role product_ui.")])
    from kit_video.validate import resolve_path

    path = resolve_path(row, project_dir, repo_root)
    if path is None or not path.is_file():
        raise ProductionError("BLOCKED", [issue("asset_missing", str(asset_id), f"Không thấy file UI của {asset_id}.")])
    slide = ease_out(progress(ctx.t, 0.15, 0.9))
    frame = _device_frame(ctx, path, slide)
    ctx.blit(frame, ((ctx.width - frame.width) / 2, ctx.height * 0.16 + (1 - slide) * ctx.height * 0.04))
    label = row.get("demoLabel") if row.get("demoData") else None
    if row.get("demoData"):
        if label != "Dữ liệu minh họa":
            raise ProductionError("BLOCKED", [issue("demo_label", str(asset_id), "Thiếu nhãn Dữ liệu minh họa.")])
        ctx.text(label, (ctx.width / 2, ctx.safe()[3] - ctx.height * 0.08), int(ctx.height * 0.036), optional_color(ctx, "accentAlt", "ink"), bold=True, anchor="mt")
        ctx.note_asset(str(asset_id), label)
    else:
        ctx.note_asset(str(asset_id), None)


def brand_reveal(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    paint_background(ctx)
    amount = ease_out(progress(ctx.t, 0.2, 0.9))
    logo_id = ctx.params.get("logoAssetId")
    drawn = False
    if isinstance(logo_id, str) and logo_id in assets:
        from kit_video.validate import resolve_path

        path = resolve_path(assets[logo_id], project_dir, repo_root)
        if path and path.is_file():
            logo = Image.open(path).convert("RGBA")
            target = int(ctx.height * 0.28 * (0.92 + 0.08 * amount))
            logo = logo.resize((target, target), Image.Resampling.LANCZOS)
            ctx.blit(logo, ((ctx.width - logo.width) / 2, ctx.height * 0.22), amount)
            ctx.note_asset(logo_id, None)
            drawn = True
    if not drawn:
        word = str(ctx.params.get("wordmark", ""))
        ctx.text(word, (ctx.width / 2, ctx.height * 0.38), int(ctx.height * 0.09), ctx.color("ink"), bold=True, anchor="mm", opacity=amount)
    line = ctx.params.get("line")
    if isinstance(line, str):
        ctx.text(line, (ctx.width / 2, ctx.height * 0.62), int(ctx.height * 0.04), ctx.color("ink"), anchor="mt", opacity=ease_out(progress(ctx.t, 0.7, 1.1)))


def end_card(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    brand_reveal(ctx, assets, repo_root, project_dir)
    url = ctx.params.get("url")
    if isinstance(url, str) and url:
        ctx.text(url, (ctx.width / 2, ctx.height * 0.74), int(ctx.height * 0.046), ctx.color("accent"), bold=True, anchor="mt", opacity=ease_out(progress(ctx.t, ctx.duration * 0.45, ctx.duration * 0.62)))


def metaphor_locked_capital(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    intro = beat(ctx, "intro", (0.00, 0.16))
    fast = beat(ctx, "fast", (0.24, 0.40))
    calendar = beat(ctx, "calendar", (0.38, 0.62))
    transform = beat(ctx, "transform", (0.58, 0.74))
    drop = beat(ctx, "drop", (0.72, 0.86))
    lock = beat(ctx, "lock", (0.84, 0.97))
    headline_at = scalar(ctx, "headlineAt", ctx.duration * 0.78)
    slots = _shelf_slots(ctx)
    intro_amount = ease_out(progress(ctx.t, intro[0], intro[1]))
    _shelf(ctx, ease_out(progress(ctx.t, intro[0] + 0.05, intro[1])))
    _person(ctx, (lerp(-0.12 * ctx.width, 0.16 * ctx.width, intro_amount), ctx.height * 0.72), "down" if ctx.t >= drop[0] else "shelf", intro_amount)
    calendar_opacity = smoothstep(progress(ctx.t, calendar[0], calendar[0] + 0.35))
    if calendar_opacity > 0.02:
        flips = [calendar[0] + (calendar[1] - calendar[0]) * part for part in (0.2, 0.5, 0.8)]
        _calendar(ctx, (ctx.width * 0.28, ctx.height * 0.18), int(ctx.width * 0.09), flips, calendar_opacity)
    drop_amount = ease_in(progress(ctx.t, drop[0], drop[1]))
    if ctx.t >= drop[0]:
        _safe(ctx, max(drop_amount, 0.02), lock)

    stay = [slot for slot in slots if not slot["fast"]]
    for slot in slots:
        _draw_stock_box(ctx, slot, intro_amount, fast, transform, drop, stay)

    headline = " ".join(ctx.scene.get("onScreenText") or []) or str(ctx.params.get("headline", ""))
    if headline:
        ctx.text(
            headline,
            (ctx.width / 2, ctx.height * 0.075),
            int(ctx.height * 0.052),
            ctx.color("ink"),
            bold=True,
            anchor="mt",
            opacity=ease_out(progress(ctx.t, headline_at, headline_at + 0.45)),
        )


def _shelf_slots(ctx: FrameContext) -> list[dict]:
    count = int(ctx.params.get("boxCount", 5))
    fast_count = int(ctx.params.get("fastCount", 2))
    left = 0.38 * ctx.width
    right = 0.9 * ctx.width
    slots = []
    for index in range(count):
        x = left + (right - left) * ((index + 0.5) / count)
        slots.append({"index": index, "x": x, "fast": index >= count - fast_count})
    return slots


def _shelf(ctx: FrameContext, opacity: float) -> None:
    if opacity <= 0.02:
        return
    plank_h = max(10, int(ctx.height * 0.018))
    y = int(ctx.height * 0.5)
    sprite = rounded((int(ctx.width * 0.58), plank_h), 6, ctx.color("ink"))
    ctx.blit(sprite, (ctx.width * 0.35, y), opacity)


def _stock_sprite(ctx: FrameContext, heavy: float, tab_name: str) -> Image.Image:
    box_w = max(28, int(ctx.width * 0.07))
    box_h = max(36, int(ctx.height * 0.15))
    key = ("stock", box_w, box_h, round(heavy, 2), tab_name, tuple(ctx.color("paper")), tuple(ctx.color("ink")), tuple(optional_color(ctx, "weight", "ink")))
    cached = ctx.cache.get(key)
    if cached is not None:
        return cached
    face = tuple(int(lerp(ctx.color("paper")[channel], optional_color(ctx, "weight", "ink")[channel], heavy)) for channel in range(3))
    image = Image.new("RGBA", (box_w, box_h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((2, 4, box_w - 2, box_h - 2), radius=int(lerp(12, 6, heavy)), fill=(*face, 255), outline=(*ctx.color("ink"), 255), width=3)
    if heavy < 0.55:
        tab = ctx.color(tab_name) if tab_name in ctx.palette else ctx.color("accent")
        draw.rectangle((6, 8, box_w - 6, int(box_h * 0.22)), fill=(*tab, 255))
        cx, cy = box_w // 2, int(box_h * 0.58)
        arm = int(box_w * 0.16)
        draw.rounded_rectangle((cx - 3, cy - arm, cx + 3, cy + arm), 2, fill=(*ctx.color("ink"), 255))
        draw.rounded_rectangle((cx - arm, cy - 3, cx + arm, cy + 3), 2, fill=(*ctx.color("ink"), 255))
    ctx.cache[key] = image
    return image


def _draw_stock_box(ctx: FrameContext, slot: dict, intro_amount: float, fast: tuple[float, float], transform: tuple[float, float], drop: tuple[float, float], stay: list[dict]) -> None:
    shelf_y = ctx.height * 0.5
    heavy = 0.0 if slot["fast"] else smoothstep(progress(ctx.t, transform[0], transform[1]))
    sprite = _stock_sprite(ctx, heavy, "accent" if slot["fast"] else "accentAlt")
    home_x = slot["x"] - sprite.width / 2
    home_y = shelf_y - sprite.height
    if slot["index"] == 2:
        x = lerp(ctx.width * 0.5 - sprite.width * lerp(2.4, 0.5, intro_amount), home_x, intro_amount)
        y = lerp(ctx.height * 0.34, home_y, intro_amount)
        scale_hint = lerp(7.2, 1.0, intro_amount)
        if scale_hint > 1.04:
            grown = sprite.resize((max(1, int(sprite.width * scale_hint)), max(1, int(sprite.height * scale_hint))), Image.Resampling.BICUBIC)
            x = lerp(ctx.width * 0.5 - grown.width / 2, home_x, intro_amount)
            y = lerp(ctx.height * 0.28, home_y, intro_amount)
            sprite = grown
    else:
        x, y = home_x, home_y
    opacity = intro_amount if slot["index"] != 2 else 1
    if slot["fast"]:
        leave = ease_in(progress(ctx.t, fast[0] + slot["index"] * 0.12, fast[1] + slot["index"] * 0.08))
        x = lerp(x, ctx.width + sprite.width, leave)
        if leave > 0.05:
            draw = ImageDraw.Draw(ctx.image)
            for streak in range(3):
                y_line = y + sprite.height * (0.3 + streak * 0.18)
                draw.line((x - 30 - streak * 16, y_line, x - 6, y_line), fill=(*ctx.color("accent"), int(160 * (1 - leave))), width=3)
    elif ctx.t >= drop[0]:
        target = _safe_top(ctx, stay, slot)
        fall = ease_in(progress(ctx.t, drop[0] + 0.04 * stay.index(slot), drop[1]))
        y = lerp(home_y, target, fall)
        x = lerp(home_x, ctx.width * 0.5 + (stay.index(slot) - 1) * sprite.width * 1.15 - sprite.width / 2, fall)
        shadow_under(ctx, (x + sprite.width / 2, y + sprite.height + 8), sprite.width * (0.25 + 0.25 * heavy), 0.35 + 0.6 * fall)
    else:
        shadow_under(ctx, (x + sprite.width / 2, shelf_y + 8), sprite.width * (0.22 + 0.2 * heavy), 0.35 + 0.4 * heavy)
    ctx.blit(sprite, (x, y), opacity)


def _safe_top(ctx: FrameContext, stay: list[dict], slot: dict) -> float:
    del stay, slot
    return ctx.height * 0.63


def _safe(ctx: FrameContext, appear: float, lock: tuple[float, float]) -> None:
    width = int(ctx.width * 0.36)
    height = int(ctx.height * 0.2)
    y = lerp(ctx.height * 0.98, ctx.height * 0.78, ease_out(min(1, appear * 1.3)))
    x = ctx.width * 0.5 - width / 2
    body = rounded((width, height), 18, optional_color(ctx, "weight", "ink"), ctx.color("ink"))
    shadow_under(ctx, (x + width / 2, y + height), width * 0.4, appear)
    ctx.blit(body, (x, y), appear)
    turns = smoothstep(progress(ctx.t, lock[0], lock[1])) * 0.5
    face = dial(max(24, int(height * 0.58)), ctx.color("ink"), ctx.color("paper"), turns)
    ctx.blit(face, (x + width / 2 - face.width / 2, y + (height - face.height) / 2), appear)
    # A token leaves ahead of the stock. No amount is written on it.
    token = smoothstep(progress(ctx.t, lock[0], lock[1]))
    coin_size = max(18, int(ctx.width * 0.028))
    coin = Image.new("RGBA", (coin_size, coin_size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(coin)
    draw.ellipse((1, 1, coin_size - 2, coin_size - 2), fill=(*ctx.color("accent"), 255))
    inset = int(coin_size * 0.28)
    draw.ellipse((inset, inset, coin_size - inset, coin_size - inset), outline=(*ctx.color("paper"), 255), width=max(2, coin_size // 12))
    ctx.blit(coin, (lerp(x + width * 0.72, ctx.width * 0.86, token), y + height * 0.42), token)


def _calendar(ctx: FrameContext, center: tuple[float, float], width: int, flips: list[float], opacity: float) -> None:
    height = int(width * 0.78)
    page = rounded((width, height), 12, ctx.color("paper"), ctx.color("ink"))
    draw = ImageDraw.Draw(page)
    draw.rectangle((0, 0, width, int(height * 0.18)), fill=(*ctx.color("accent"), 255))
    shown = 0
    turning = 0.0
    for index, moment in enumerate(flips):
        if ctx.t >= moment:
            shown = index + 1
        local = progress(ctx.t, moment, moment + 0.28)
        if 0 < local < 1:
            turning = ease_in(local)
            shown = index
    ctx.blit(page, (center[0] - width / 2, center[1]), opacity)
    ctx.text(str(shown + 1), (center[0], center[1] + height * 0.58), int(height * 0.34), ctx.color("ink"), bold=True, anchor="mm", opacity=opacity)
    if turning > 0.02:
        leaf = page.resize((width, max(1, int(height * (1 - turning)))), Image.Resampling.BICUBIC)
        ctx.blit(rotate(leaf, -70 * turning), (center[0] - leaf.width / 2, center[1] - leaf.height * turning * 0.2), opacity * (1 - turning))


def _person(ctx: FrameContext, foot: tuple[float, float], look: str, opacity: float) -> None:
    if opacity <= 0.02:
        return
    scale = ctx.height / 1080
    width = int(150 * scale)
    height = int(280 * scale)
    key = ("person", width, height, look, tuple(ctx.color("ink")), tuple(ctx.color("paper")), tuple(ctx.color("accent")))
    sprite = ctx.cache.get(key)
    if sprite is None:
        sprite = Image.new("RGBA", (width, height), (0, 0, 0, 0))
        draw = ImageDraw.Draw(sprite)
        ink = (*ctx.color("ink"), 255)
        paper = (*ctx.color("paper"), 255)
        accent = (*ctx.color("accent"), 255)
        draw.rounded_rectangle((width * 0.32, height * 0.36, width * 0.68, height * 0.78), radius=int(width * 0.12), fill=accent)
        draw.rounded_rectangle((width * 0.38, height * 0.74, width * 0.48, height * 0.98), radius=6, fill=ink)
        draw.rounded_rectangle((width * 0.52, height * 0.74, width * 0.62, height * 0.98), radius=6, fill=ink)
        draw.ellipse((width * 0.28, height * 0.05, width * 0.72, height * 0.38), fill=paper, outline=ink, width=max(2, width // 40))
        eye_y = height * (0.27 if look == "down" else 0.16)
        eye_dx = width * (0.02 if look == "shelf" else 0)
        for side in (-1, 1):
            cx = width * 0.5 + side * width * 0.1 + eye_dx
            draw.ellipse((cx - 4 * scale, eye_y, cx + 4 * scale, eye_y + 8 * scale), fill=ink)
        ctx.cache[key] = sprite
    shadow_under(ctx, (foot[0], foot[1]), width * 0.28, 0.45 * opacity)
    ctx.blit(sprite, (foot[0] - sprite.width / 2, foot[1] - sprite.height), opacity)


def _device_frame(ctx: FrameContext, path: Path, slide: float) -> Image.Image:
    del slide
    source = Image.open(path).convert("RGBA")
    max_w = int(ctx.width * 0.72)
    max_h = int(ctx.height * 0.62)
    scale = min(max_w / source.width, max_h / source.height)
    fitted = source.resize((max(1, int(source.width * scale)), max(1, int(source.height * scale))), Image.Resampling.LANCZOS)
    pad = 18
    frame = Image.new("RGBA", (fitted.width + pad * 2, fitted.height + pad * 2), (0, 0, 0, 0))
    ImageDraw.Draw(frame).rounded_rectangle((0, 0, frame.width - 1, frame.height - 1), radius=24, fill=(*ctx.color("ink"), 255))
    frame.alpha_composite(fitted, (pad, pad))
    return frame


def annotated_plate(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    """A registered still, a small camera move, and a code caption that stays inside the safe area."""
    from kit_video.camera import CameraMove, render_plate
    from kit_video.validate import resolve_path

    asset_id = ctx.params.get("assetId")
    row = assets.get(asset_id) if isinstance(asset_id, str) else None
    if row is None:
        raise ProductionError("BLOCKED", [issue("asset_missing", ctx.scene["sceneId"], "annotated_plate cần assetId đã khai.")])
    path = resolve_path(row, project_dir, repo_root)
    if path is None or not path.is_file():
        raise ProductionError("BLOCKED", [issue("asset_missing", str(asset_id), f"Không thấy ảnh {asset_id}.")])
    move = CameraMove.parse(ctx.params.get("preset"), ctx.params.get("amount", 0))
    cache_key = ("plate-source", str(path))
    source = ctx.cache.get(cache_key)
    if source is None:
        source = Image.open(path).convert("RGBA")
        source.load()
        ctx.cache[cache_key] = source
    caption = str(ctx.params.get("caption") or "")
    note = str(ctx.params.get("note") or "")
    left, _top, right, bottom = ctx.safe()
    bar_h = int(ctx.height * (0.1 if note else 0.06)) if caption or note else 0
    plate = render_plate(source, (ctx.width, max(1, bottom - bar_h)), move, ctx.t, ctx.duration, ctx.color("background"))
    ctx.image.alpha_composite(plate, (0, 0))
    ctx.note_asset(str(asset_id), None)
    if not caption and not note:
        return
    appear = ease_out(progress(ctx.t, ctx.duration * 0.08, ctx.duration * 0.22))
    size = _fit_caption(ctx, caption, right - left - 24, int(ctx.height * 0.03))
    ctx.text(caption, ((left + right) / 2, bottom - bar_h + 6), size, ctx.color("ink"), bold=True, anchor="mt", opacity=appear)
    if note:
        ctx.text(note, ((left + right) / 2, bottom - 4), int(ctx.height * 0.02), ctx.color("muted"), anchor="mb", opacity=appear)


def _fit_caption(ctx: FrameContext, text: str, max_width: int, size: int) -> int:
    while size > 14 and text:
        if ctx.font(size, True).getlength(text) <= max_width:
            return size
        size -= 1
    return size


def layered_plate(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    """A real plate plus a second photo. The caption says they are two sources."""
    from kit_video.camera import CameraMove, render_plate
    from kit_video.validate import resolve_path

    background = _plate_source(ctx, assets, repo_root, project_dir, "assetId")
    inset = _plate_source(ctx, assets, repo_root, project_dir, "insetId")
    caption = str(ctx.params.get("caption") or "")
    note = str(ctx.params.get("note") or "")
    left, _top, right, bottom = ctx.safe()
    bar_h = int(ctx.height * (0.1 if note else 0.06)) if caption or note else 0
    move = CameraMove.parse(ctx.params.get("preset"), ctx.params.get("amount", 0))
    plate = render_plate(background, (ctx.width, max(1, bottom - bar_h)), move, ctx.t, ctx.duration, ctx.color("background"))
    ctx.image.alpha_composite(plate, (0, 0))
    card_w = int(ctx.width * 0.32)
    card_h = int((bottom - bar_h) * 0.46)
    card_x = right - card_w - 16
    card_y = bottom - bar_h - card_h - 16
    draw = ImageDraw.Draw(ctx.image)
    draw.rounded_rectangle((card_x - 8, card_y - 28, card_x + card_w + 8, card_y + card_h + 8), radius=12, fill=(*ctx.color("paper"), 235))
    fitted = render_plate(inset, (card_w, card_h), CameraMove.parse("hold", 0), ctx.t, ctx.duration, ctx.color("paper"))
    ctx.image.alpha_composite(fitted, (card_x, card_y))
    ctx.text("Ảnh tham chiếu", (card_x, card_y - 22), int(ctx.height * 0.02), ctx.color("ink"), bold=True, anchor="lt")
    ctx.note_asset(str(ctx.params.get("assetId")), None)
    ctx.note_asset(str(ctx.params.get("insetId")), None)
    if not caption and not note:
        return
    size = _fit_caption(ctx, caption, right - left - 24, int(ctx.height * 0.028))
    ctx.text(caption, ((left + right) / 2, bottom - bar_h + 6), size, ctx.color("ink"), bold=True, anchor="mt")
    if note:
        ctx.text(note, ((left + right) / 2, bottom - 4), int(ctx.height * 0.02), ctx.color("muted"), anchor="mb")


def _plate_source(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path, key: str):
    from kit_video.validate import resolve_path

    asset_id = ctx.params.get(key)
    row = assets.get(asset_id) if isinstance(asset_id, str) else None
    if row is None:
        raise ProductionError("BLOCKED", [issue("asset_missing", ctx.scene["sceneId"], f"layered_plate cần {key}.")])
    path = resolve_path(row, project_dir, repo_root)
    if path is None or not path.is_file():
        raise ProductionError("BLOCKED", [issue("asset_missing", str(asset_id), f"Không thấy ảnh {asset_id}.")])
    cache_key = ("plate-source", str(path))
    source = ctx.cache.get(cache_key)
    if source is None:
        source = Image.open(path).convert("RGBA")
        source.load()
        ctx.cache[cache_key] = source
    return source


def market_chart(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    """Candles from a stored OHLCV file. The file is the source; the frame does not invent prints."""
    del assets
    from kit_video.chart import load_candles, paint_market

    raw = ctx.params.get("dataFile")
    if not isinstance(raw, str) or not raw:
        raise ProductionError("BLOCKED", [issue("ohlcv", ctx.scene["sceneId"], "market_chart cần dataFile.")])
    base = repo_root if ctx.params.get("dataBase") == "repo" else project_dir
    path = (base / raw).resolve()
    cache_key = ("ohlcv", str(path))
    series = ctx.cache.get(cache_key)
    if series is None:
        if not path.is_file():
            raise ProductionError("BLOCKED", [issue("ohlcv", ctx.scene["sceneId"], f"Không thấy file nến {raw}.")])
        series = load_candles(path)
        ctx.cache[cache_key] = series
    paint_background(ctx)
    paint_market(ctx, series)


TEMPLATES = {
    "kinetic_type": kinetic_type,
    "object_move": object_move,
    "object_transform": object_transform,
    "character_entrance": character_entrance,
    "calendar_flip": calendar_flip,
    "data_connection": data_connection,
    "ui_showcase": ui_showcase,
    "brand_reveal": brand_reveal,
    "end_card": end_card,
    "metaphor_locked_capital": metaphor_locked_capital,
    "stock_story": stock_story,
    "reference_stage": reference_stage,
    "plate_camera": plate_camera,
    "annotated_plate": annotated_plate,
    "layered_plate": layered_plate,
    "market_chart": market_chart,
    "designed_board": designed_board,
}


# Knowledge-education scenes (typography, numbers, diagrams, code-drawn props). See tools/kit-video-library/STATUS.md.
from kit_video.edu_scenes import TEMPLATES as _EDU_TEMPLATES  # noqa: E402

TEMPLATES.update(_EDU_TEMPLATES)
