"""Visual stage for story scenes.

The production core still owns time, assets, and encode. This module only
composes a picture: a room with depth, jointed acting, and object motion.
Colors, copy, counts, and beat times come from the shot plan and palette.
"""

from __future__ import annotations

import math
from functools import lru_cache
from pathlib import Path

from PIL import Image, ImageDraw

from kit_video.canvas import (
    FrameContext,
    ease_out,
    lerp,
    load_font,
    progress,
    rotate,
    scalar,
    smoothstep,
)


def mix(a: tuple[int, int, int], b: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
    amount = max(0.0, min(1.0, amount))
    return tuple(int(a[i] + (b[i] - a[i]) * amount) for i in range(3))


def shade(color: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
    if amount < 0:
        return mix(color, (0, 0, 0), -amount)
    return mix(color, (255, 255, 255), amount)


def story_colors(palette: dict) -> dict:
    def get(name: str, fallback: tuple[int, int, int]) -> tuple[int, int, int]:
        value = palette.get(name)
        if isinstance(value, (list, tuple)) and len(value) >= 3:
            return (int(value[0]), int(value[1]), int(value[2]))
        return fallback

    ink = get("ink", (20, 24, 32))
    paper = get("paper", (250, 247, 240))
    accent = get("accent", (0, 122, 176))
    background = get("background", (230, 224, 214))
    alt = get("accentAlt", (166, 102, 72))
    return {
        "ink": ink,
        "paper": paper,
        "accent": accent,
        "background": background,
        "wall": get("wall", mix(paper, background, 0.45)),
        "floor": get("floor", mix(background, ink, 0.14)),
        "wood": get("wood", mix(alt, paper, 0.28)),
        "skin": get("skin", (222, 186, 156)),
        "cloth": get("cloth", accent),
        "shirt": get("shirt", mix(paper, accent, 0.14)),
        "pants": get("pants", mix(ink, background, 0.42)),
        "hair": get("hair", shade(ink, -0.12)),
        "weight": get("weight", shade(ink, -0.05)),
        "alt": alt,
        "muted": get("muted", mix(ink, background, 0.5)),
        "coin": get("coin", mix((206, 164, 72), paper, 0.12)),
    }


def stage_layout(mode: str, width: int, height: int, box_count: int, fast_count: int) -> dict:
    portrait = height > width * 1.05
    box_count = max(1, int(box_count))
    fast_count = min(max(0, int(fast_count)), box_count - 1) if box_count > 1 else 0
    floor_y = height * (0.9 if portrait else 0.86)
    if mode == "metaphor":
        person = None
        surface = height * (0.42 if portrait else 0.5)
        counter_x = width * 0.07
        counter_w = width * 0.86
        preferred_w = width * (0.2 if portrait else 0.13)
        preferred_h = height * (0.15 if portrait else 0.22)
        safe_w = width * (0.78 if portrait else 0.62)
        safe_h = height * 0.15
        calendar = (width * 0.22, height * 0.045, width * (0.44 if portrait else 0.2), height * 0.13)
        window = (width * 0.05, height * 0.05, width * (0.32 if portrait else 0.2), height * 0.14)
    elif mode == "acting":
        person_h = height * (0.46 if portrait else 0.62)
        person = (width * (0.42 if portrait else 0.34), floor_y, person_h)
        surface = height * (0.4 if portrait else 0.56)
        counter_x = width * (0.08 if portrait else 0.5)
        counter_w = width * (0.84 if portrait else 0.44)
        preferred_w = width * (0.16 if portrait else 0.075)
        preferred_h = height * (0.12 if portrait else 0.15)
        safe_w = width * (0.7 if portrait else 0.3)
        safe_h = height * 0.13
        calendar = (width * 0.2, height * 0.05, width * 0.18, height * 0.11)
        window = (width * 0.04, height * 0.06, width * 0.18, height * 0.13)
    else:
        person_h = height * (0.38 if portrait else 0.52)
        person = (width * (0.28 if portrait else 0.3), floor_y, person_h)
        surface = height * (0.4 if portrait else 0.52)
        counter_x = width * (0.06 if portrait else 0.36)
        counter_w = width * (0.88 if portrait else 0.58)
        preferred_w = width * (0.18 if portrait else 0.09)
        preferred_h = height * (0.12 if portrait else 0.18)
        safe_w = width * (0.72 if portrait else 0.3)
        safe_h = height * (0.16 if portrait else 0.2)
        calendar = (width * (0.46 if portrait else 0.24), height * 0.045, width * (0.42 if portrait else 0.18), height * (0.1 if portrait else 0.13))
        window = (width * 0.035, height * 0.05, width * (0.3 if portrait else 0.17), height * 0.15)
    box_w, box_h = _fit_boxes(counter_w, box_count, preferred_w, preferred_h)
    safe_x = counter_x + (counter_w - safe_w) * (0.5 if mode == "metaphor" else 0.62)
    if mode == "acting" and not portrait:
        safe_x = width * 0.58
    safe = (safe_x, floor_y - safe_h, safe_w, safe_h)
    if not portrait:
        calendar = (width * 0.04, height * 0.22, width * 0.15, height * 0.13)
    box_top = surface - box_h
    cal_h = min(calendar[3], max(height * 0.07, box_top - calendar[1] - height * 0.02))
    calendar = (calendar[0], calendar[1], calendar[2], cal_h)
    return {
        "width": width,
        "height": height,
        "mode": mode,
        "person": person,
        "counter": (counter_x, surface, counter_w, max(8.0, floor_y - surface)),
        "box_w": box_w,
        "box_h": box_h,
        "safe": safe,
        "calendar": calendar,
        "window": window,
        "floor_y": floor_y,
        "box_count": box_count,
        "fast_count": fast_count,
        "portrait": portrait,
    }


def _fit_boxes(counter_w: float, count: int, preferred_w: float, preferred_h: float) -> tuple[float, float]:
    fitted = counter_w * 0.86 / (count * 1.16)
    width = max(8.0, min(preferred_w, fitted))
    height = max(8.0, min(preferred_h, width * 1.22))
    return width, height


def _slots(layout: dict) -> list[tuple[float, float]]:
    x, y, width, _front = layout["counter"]
    count = layout["box_count"]
    box_w = layout["box_w"]
    gap = box_w * 1.16
    total = (count - 1) * gap + box_w
    left = x + (width - total) / 2
    top = y - layout["box_h"]
    return [(left + index * gap, top) for index in range(count)]


def _box_states(t: float, layout: dict, beats: dict, colors: dict) -> list[dict]:
    slots = _slots(layout)
    fast_count = layout["fast_count"]
    hero = fast_count
    intro = beats["intro"]
    fast = beats["fast"]
    transform = beats["transform"]
    drop = beats["drop"]
    states = []
    for index, (home_x, home_top) in enumerate(slots):
        width = layout["box_w"]
        height = layout["box_h"]
        x = home_x
        y = home_top
        rot = 0.0
        opacity = 1.0
        squash = 1.0
        heavy = 0.0
        rise = 0.0
        layer = "shelf"
        anchor = "bottom"
        intro_p = progress(t, intro[0], intro[1])
        if index == hero:
            scale = lerp(2.5, 1.0, ease_out(intro_p))
            width *= scale
            height *= scale
            center_x = lerp(layout["width"] * 0.52, home_x + layout["box_w"] / 2, ease_out(intro_p))
            center_y = lerp(layout["height"] * 0.44, home_top + layout["box_h"] / 2, ease_out(intro_p))
            x = center_x - width / 2
            y = center_y - height / 2
            anchor = "center"
            if intro_p < 0.92:
                layer = "air"
            if 0.8 < intro_p < 1:
                land = (intro_p - 0.8) / 0.2
                squash *= lerp(1, 0.9, land) if land < 0.5 else lerp(0.9, 1, (land - 0.5) / 0.5)
        elif intro_p < 1:
            appear = smoothstep(progress(t, intro[0] + (intro[1] - intro[0]) * 0.35, intro[1]))
            opacity = appear
            y = home_top + (1 - appear) * layout["box_h"] * 0.28
        if index < fast_count and t >= fast[0]:
            stagger = index * 0.14
            start = fast[0] + stagger
            end = fast[1] + stagger * 0.35
            span = max(0.2, end - start)
            wind_end = start + span * 0.3
            if t < wind_end:
                wind = smoothstep(progress(t, start, wind_end))
                squash *= lerp(1, 0.82, wind)
                x -= layout["box_w"] * 0.08 * wind
                y += layout["box_h"] * 0.045 * wind
            else:
                flight = smoothstep(progress(t, wind_end, end))
                arc = math.sin(min(1.0, flight) * math.pi)
                x = lerp(home_x, home_x + layout["width"] * (0.58 + index * 0.05), ease_out(flight))
                rise = arc * layout["box_h"] * (1.65 if index == 0 else 1.4)
                y = home_top - rise
                rot = lerp(0, -28 if index % 2 == 0 else 22, flight)
                squash *= lerp(0.88, 1, min(1, flight * 3))
                anchor = "center"
                layer = "air"
                if flight > 0.88:
                    opacity *= 1 - (flight - 0.88) / 0.12
        elif index >= fast_count:
            if fast[0] <= t <= fast[1] + 0.25:
                wobble = math.sin(progress(t, fast[0], fast[1]) * math.pi)
                neighbor = 1.0 if index == fast_count else 0.35
                rot += wobble * 5 * neighbor
            if t >= transform[0]:
                heavy = smoothstep(progress(t, transform[0], transform[1]))
                squash *= lerp(1, 0.9, heavy)
                squash *= lerp(1, 0.9, math.sin(heavy * math.pi))
            if t >= drop[0]:
                span = max(0.2, drop[1] - drop[0])
                wind_end = drop[0] + span * 0.16
                if t < wind_end:
                    wind = smoothstep(progress(t, drop[0], wind_end))
                    y -= layout["box_h"] * 0.08 * wind
                    squash *= lerp(1, 0.86, wind)
                else:
                    fall = smoothstep(progress(t, wind_end, drop[1]))
                    order = index - fast_count
                    land_x, land_w = _landing(layout, order)
                    land_h = layout["box_h"] * lerp(1, 0.76, max(heavy, 0.65))
                    land_y = layout["safe"][1] - land_h
                    x = lerp(home_x, land_x, fall)
                    y = lerp(home_top, land_y, fall)
                    width = lerp(layout["box_w"], land_w, fall)
                    height = lerp(layout["box_h"], land_h, fall)
                    rot += lerp(0, 4 if index % 2 == 0 else -3, fall)
                    layer = "front"
                    anchor = "bottom"
                    if fall > 0.84:
                        hit = (fall - 0.84) / 0.16
                        squash *= lerp(1, 0.72, hit) if hit < 0.5 else lerp(0.72, 0.94, (hit - 0.5) / 0.5)
        x, y, width, height = _squash_box(x, y, width, height, squash, anchor)
        front = colors["accent"] if index < fast_count else colors["paper"]
        tape = colors["accent"] if index < fast_count else colors["alt"]
        if heavy > 0:
            front = mix(front, colors["weight"], heavy)
            tape = mix(tape, colors["weight"], heavy)
        states.append({
            "x": x,
            "y": y,
            "w": max(4.0, width),
            "h": max(4.0, height),
            "rot": rot,
            "opacity": max(0.0, min(1.0, opacity)),
            "front": front,
            "tape": tape,
            "rise": rise,
            "mark": heavy < 0.62,
            "heavy": heavy,
            "layer": layer,
            "index": index,
        })
    return states


def _squash_box(x: float, y: float, width: float, height: float, squash: float, anchor: str) -> tuple[float, float, float, float]:
    squash = max(0.68, min(1.15, squash))
    drawn_w = width / squash
    drawn_h = height * squash
    drawn_x = x + (width - drawn_w) / 2
    if anchor == "center":
        drawn_y = y + (height - drawn_h) / 2
    else:
        drawn_y = y + height - drawn_h
    return drawn_x, drawn_y, drawn_w, drawn_h


def _landing(layout: dict, order: int) -> tuple[float, float]:
    stayers = layout["box_count"] - layout["fast_count"]
    safe_x, _y, safe_w, _h = layout["safe"]
    block = min(layout["box_w"] * 0.92, safe_w * 0.84 / max(1, stayers))
    gap = block * 1.12
    total = (stayers - 1) * gap + block
    left = safe_x + (safe_w - total) / 2
    return left + order * gap, block


def _pose(t: float, layout: dict, beats: dict, hand_target: tuple[float, float] | None) -> dict:
    fast = beats["fast"]
    intro = beats["intro"]
    calendar = beats["calendar"]
    transform = beats["transform"]
    drop = beats["drop"]
    lock = beats["lock"]
    pose = {"lean": 0.0, "reach": 0.08, "look": (0.35, -0.15), "brow": 0.2, "mouth": 0.0, "weight": 0.0, "hand_target": hand_target}
    if t < intro[1]:
        amount = ease_out(progress(t, intro[0], intro[1]))
        pose.update(lean=lerp(-0.35, 0.18, amount), reach=lerp(0.0, 0.28, amount), look=(0.55, -0.45), brow=0.1, mouth=0.0)
    span = max(0.2, fast[1] - fast[0])
    launch = fast[0] + span * 0.3
    if fast[0] <= t < calendar[0]:
        if t < launch:
            wind = smoothstep(progress(t, fast[0], launch))
            pose.update(lean=lerp(0.18, -0.32, wind), reach=lerp(0.28, 0.12, wind), look=(0.7, -0.1), brow=0.25, mouth=0.0)
        elif t < launch + 0.42:
            pose.update(lean=0.78, reach=1.0, look=(0.95, -0.15), brow=0.05, mouth=0.12, weight=0.45)
        else:
            recoil = smoothstep(progress(t, launch + 0.42, launch + 1.05))
            pose.update(
                lean=lerp(0.72, -0.48, recoil) if recoil < 0.65 else lerp(-0.48, -0.12, (recoil - 0.65) / 0.35),
                reach=lerp(1.0, 0.12, recoil),
                look=(0.45, -0.2),
                brow=lerp(0.15, 0.75, recoil),
                mouth=lerp(0.45, 0.05, recoil),
                weight=-0.35,
            )
    elif calendar[0] <= t < transform[0]:
        pose.update(lean=0.05, reach=0.12, look=(0.15, -0.9), brow=0.55, mouth=-0.05)
    elif transform[0] <= t < drop[0]:
        pose.update(lean=0.28, reach=0.34, look=(0.75, 0.15), brow=0.8, mouth=-0.25)
    elif t >= drop[0]:
        fall = progress(t, drop[0], drop[1])
        if fall < 0.84:
            pose.update(lean=lerp(0.1, -0.2, fall), reach=0.05, look=(0.55, 0.35), brow=0.7, mouth=0.0)
        else:
            flinch = smoothstep((fall - 0.84) / 0.16)
            settle = smoothstep(progress(t, lock[0], lock[1]))
            pose.update(
                lean=lerp(-0.62, -0.08, settle) if settle > 0 else lerp(-0.1, -0.62, flinch),
                reach=0.04,
                look=(0.8, lerp(0.75, 0.35, settle)),
                brow=lerp(0.9, 0.5, settle),
                mouth=lerp(0.35, 0.0, settle),
                weight=-0.2,
            )
    if layout["mode"] == "acting":
        pose["lean"] *= 1.15
        pose["reach"] = min(1.0, pose["reach"] * 1.05)
    return pose


def _impact(t: float, beats: dict) -> float:
    drop = beats["drop"]
    span = max(0.2, drop[1] - drop[0])
    wind_end = drop[0] + span * 0.16
    fall = smoothstep(progress(t, wind_end, drop[1]))
    if fall <= 0.84:
        return 0.0
    return min(1.0, (fall - 0.84) / 0.16)


def paint_stock_story(ctx: FrameContext) -> None:
    frame = compose_story(
        ctx.width,
        ctx.height,
        ctx.t,
        ctx.duration,
        ctx.params,
        ctx.palette,
        ctx.style,
        ctx.cache,
    )
    ctx.image.alpha_composite(frame)
    _headline(ctx)
    label = ctx.params.get("pipelineTestLabel")
    if isinstance(label, str) and label:
        ctx.text(
            label,
            (ctx.safe()[0], ctx.safe()[3] - max(18, int(ctx.height * 0.04))),
            max(14, int(ctx.height * 0.028)),
            ctx.color("muted") if "muted" in ctx.palette else ctx.color("ink"),
            bold=True,
        )


def compose_story(width, height, t, duration, params, palette, style, cache) -> Image.Image:
    mode = params.get("mode") if params.get("mode") in {"acting", "metaphor", "hybrid"} else "hybrid"
    box_count = int(params.get("boxCount") or 5)
    fast_count = int(params.get("fastCount") or 2)
    layout = stage_layout(mode, width, height, box_count, fast_count)
    colors = story_colors(palette)
    beats = _beats_from(params, duration)
    aa = 2
    plate = _background(layout, colors, aa, cache)
    draw = ImageDraw.Draw(plate)
    line = max(2, int(height * aa * 0.0032))
    _draw_calendar(draw, layout, beats, t, colors, line, style, aa)
    states = _box_states(t, layout, beats, colors)
    _draw_shadows(draw, states, layout, colors, aa)
    for state in states:
        if state["layer"] == "shelf":
            _draw_box(plate, state, colors, line, aa)
    if layout["person"] is not None:
        _draw_actor(draw, layout, beats, t, colors, line, aa)
    for state in states:
        if state["layer"] == "air":
            _draw_box(plate, state, colors, line, aa)
    _draw_safe(plate, draw, layout, beats, t, colors, line, aa)
    for state in states:
        if state["layer"] == "front":
            _draw_box(plate, state, colors, line, aa)
    zoom = lerp(1.015, 1.045, smoothstep(progress(t, 0, max(duration, 0.01))))
    if t >= beats["drop"][0]:
        zoom = min(1.06, zoom + 0.012 * smoothstep(progress(t, beats["drop"][0], beats["drop"][1])))
    drift = lerp(0.006, -0.004, smoothstep(progress(t, 0, max(duration, 0.01))))
    pan = 0.0
    if t >= beats["drop"][0]:
        pan = 0.015 * smoothstep(progress(t, beats["drop"][0], beats["drop"][1]))
    return _present(plate, width, height, zoom, drift, pan)


def _beats_from(params: dict, duration: float) -> dict[str, tuple[float, float]]:
    defaults = {
        "intro": (0.04, 0.18),
        "fast": (0.22, 0.36),
        "calendar": (0.4, 0.54),
        "transform": (0.56, 0.68),
        "drop": (0.7, 0.82),
        "lock": (0.84, 0.96),
    }
    custom = params.get("beats") if isinstance(params.get("beats"), dict) else {}
    found = {}
    for name, window in defaults.items():
        value = custom.get(name)
        if isinstance(value, list) and len(value) == 2:
            found[name] = (float(value[0]), float(value[1]))
        else:
            found[name] = (window[0] * duration, window[1] * duration)
    return found


def _background(layout: dict, colors: dict, aa: int, cache: dict) -> Image.Image:
    key = (
        "stock-bg",
        layout["mode"],
        layout["width"],
        layout["height"],
        layout["box_count"],
        colors["wall"],
        colors["wood"],
        colors["floor"],
        aa,
    )
    cached = cache.get(key)
    if cached is None:
        cached = _paint_room(layout, colors, aa)
        cache[key] = cached
    return cached.copy()


def _paint_room(layout: dict, colors: dict, aa: int) -> Image.Image:
    frame_w = int(layout["width"])
    frame_h = int(layout["height"])
    plate = Image.new("RGBA", (frame_w * aa, frame_h * aa), (*colors["wall"], 255))
    draw = ImageDraw.Draw(plate)
    line = max(2, int(frame_h * aa * 0.003))
    floor_y = layout["floor_y"] * aa
    draw.rectangle((0, floor_y, plate.width, plate.height), fill=(*colors["floor"], 255))
    draw.rectangle((0, floor_y - frame_h * aa * 0.012, plate.width, floor_y), fill=(*shade(colors["wood"], -0.05), 255))
    _draw_window(draw, layout["window"], colors, line, aa)
    _draw_shelf(draw, layout, colors, line, aa)
    light = layout["window"]
    pool = (
        light[0] * aa,
        floor_y,
        (light[0] + light[2] * 1.8) * aa,
        plate.height,
    )
    overlay = Image.new("RGBA", plate.size, (0, 0, 0, 0))
    ImageDraw.Draw(overlay).ellipse(pool, fill=(*mix(colors["paper"], colors["accent"], 0.35), 28))
    plate.alpha_composite(overlay)
    return plate


def _draw_window(draw: ImageDraw.ImageDraw, window: tuple, colors: dict, line: int, aa: int) -> None:
    x, y, w, h = [value * aa for value in window]
    glass = mix(colors["paper"], colors["accent"], 0.22)
    radius = max(4, int(min(w, h) * 0.08))
    draw.rounded_rectangle((x, y, x + w, y + h), radius=radius, fill=(*glass, 255), outline=(*colors["ink"], 255), width=line)
    draw.line((x + w / 2, y + 4, x + w / 2, y + h - 4), fill=(*colors["ink"], 255), width=max(1, line - 1))
    draw.line((x + 4, y + h / 2, x + w - 4, y + h / 2), fill=(*colors["ink"], 255), width=max(1, line - 1))
    draw.rectangle((x - line, y + h, x + w + line, y + h + max(4, h * 0.06)), fill=(*colors["wood"], 255))


def _draw_shelf(draw: ImageDraw.ImageDraw, layout: dict, colors: dict, line: int, aa: int) -> None:
    x, y, w, front = [value * aa for value in layout["counter"]]
    depth = max(6, layout["box_h"] * aa * 0.18)
    wood = colors["wood"]
    inset = depth * 0.45
    back_top = y - depth - layout["box_h"] * aa * 0.38
    draw.polygon(
        [(x + inset, y - depth), (x + w - inset, y - depth), (x + w - inset, back_top), (x + inset, back_top)],
        fill=(*shade(wood, -0.16), 255),
    )
    top = [(x, y), (x + w, y), (x + w - inset, y - depth), (x + inset, y - depth)]
    draw.polygon(top, fill=(*shade(wood, 0.14), 255))
    draw.line((*top, top[0]), fill=(*colors["ink"], 255), width=line)
    draw.rectangle((x, y, x + w, y + front), fill=(*shade(wood, -0.06), 255), outline=(*colors["ink"], 255), width=line)
    kick = y + front - max(6, front * 0.08)
    draw.rectangle((x, kick, x + w, y + front), fill=(*shade(wood, -0.22), 255))
    draw.line((x + 8, y + front * 0.22, x + w - 8, y + front * 0.22), fill=(*shade(wood, 0.2), 255), width=max(1, line // 2))


def _draw_lip(draw: ImageDraw.ImageDraw, layout: dict, colors: dict, line: int, aa: int) -> None:
    x, y, w, _front = [value * aa for value in layout["counter"]]
    lip = max(3, int(layout["box_h"] * aa * 0.035))
    draw.rectangle((x, y - lip, x + w, y + lip), fill=(*shade(colors["wood"], -0.02), 255), outline=(*colors["ink"], 255), width=max(1, line // 2))


def _draw_calendar(draw, layout, beats, t, colors, line, style, aa) -> None:
    x, y, w, h = [value * aa for value in layout["calendar"]]
    if w < 8 or h < 8:
        return
    radius = max(3, int(min(w, h) * 0.08))
    draw.rounded_rectangle((x, y, x + w, y + h), radius=radius, fill=(*colors["paper"], 255), outline=(*colors["ink"], 255), width=line)
    bind = h * 0.16
    draw.rectangle((x, y, x + w, y + bind), fill=(*colors["accent"], 255))
    rings = 3 if w > 40 else 2
    for index in range(rings):
        cx = x + w * (index + 1) / (rings + 1)
        draw.ellipse((cx - w * 0.03, y + bind * 0.2, cx + w * 0.03, y + bind * 0.95), outline=(*colors["ink"], 255), width=max(1, line // 2))
    window = beats["calendar"]
    local = progress(t, window[0], window[1])
    page = min(2, int(local * 3)) if local > 0 else 0
    flip_slot = (local * 3) % 1 if 0 < local < 1 else 0
    shear = math.sin(flip_slot * math.pi) * w * 0.28 if 0 < local < 1 else 0
    page_top = y + bind + 2
    draw.polygon(
        [(x + 6 + shear, page_top), (x + w - 6, page_top), (x + w - 8 - shear * 0.3, y + h - 6), (x + 8, y + h - 6)],
        fill=(*shade(colors["paper"], -0.03), 255),
    )
    labels = ("02", "05", "09")
    font_path = (style.get("typography") or {}).get("fontBold")
    if isinstance(font_path, str) and Path(font_path).is_file() and shear < w * 0.12:
        font = load_font(font_path, max(10, int(h * 0.38)))
        draw.text((x + w * 0.5, y + h * 0.58), labels[page], font=font, fill=(*colors["ink"], 255), anchor="mm")


def _draw_shadows(draw, states, layout, colors, aa) -> None:
    surface = layout["counter"][1] * aa
    for state in states:
        if state["opacity"] < 0.05:
            continue
        lift = state["rise"] / max(1.0, layout["box_h"] * 1.35)
        alpha = int(70 * state["opacity"] * (1 - min(1, lift)))
        if alpha < 4:
            continue
        cx = (state["x"] + state["w"] / 2) * aa
        wide = state["w"] * aa * lerp(0.42, 0.22, min(1, lift))
        draw.ellipse((cx - wide, surface - 3, cx + wide, surface + max(4, state["h"] * aa * 0.06)), fill=(*colors["ink"], alpha))


def _draw_box(plate: Image.Image, state: dict, colors: dict, line: int, aa: int) -> None:
    if state["opacity"] <= 0.03:
        return
    sprite = _carton(
        int(state["w"] * aa),
        int(state["h"] * aa),
        max(4, int(state["w"] * aa * 0.22)),
        state["front"],
        state["tape"],
        colors["ink"],
        line,
        state["mark"],
    )
    if abs(state["rot"]) > 0.4:
        sprite = rotate(sprite, state["rot"])
    center = ((state["x"] + state["w"] / 2) * aa, (state["y"] + state["h"] / 2) * aa)
    _paste(plate, sprite, (center[0] - sprite.width / 2, center[1] - sprite.height / 2), state["opacity"])


@lru_cache(maxsize=256)
def _carton(width: int, height: int, depth: int, front: tuple[int, int, int], tape: tuple[int, int, int], ink: tuple[int, int, int], line: int, mark: bool) -> Image.Image:
    width = max(4, width)
    height = max(4, height)
    depth = max(3, depth)
    pad = depth + line * 3
    image = Image.new("RGBA", (width + pad * 2, height + pad * 2), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    x = pad
    y = pad
    side = shade(front, -0.2)
    top = shade(front, 0.16)
    top_poly = [(x, y), (x + width, y), (x + width + depth * 0.62, y - depth), (x + depth * 0.62, y - depth)]
    side_poly = [(x + width, y), (x + width + depth * 0.62, y - depth), (x + width + depth * 0.62, y + height - depth), (x + width, y + height)]
    draw.polygon(top_poly, fill=(*top, 255))
    draw.polygon(side_poly, fill=(*side, 255))
    radius = max(2, min(width, height) // 10)
    draw.rounded_rectangle((x, y, x + width, y + height), radius=radius, fill=(*front, 255))
    if mark:
        strip = max(3, int(width * 0.14))
        draw.rectangle((x + width * 0.16, y + 3, x + width * 0.16 + strip, y + height - 3), fill=(*tape, 255))
        draw.rounded_rectangle(
            (x + width * 0.42, y + height * 0.3, x + width * 0.86, y + height * 0.62),
            radius=max(2, width // 18),
            fill=(*shade(front, 0.08), 255),
            outline=(*shade(front, -0.25), 255),
            width=max(1, line // 2),
        )
    else:
        draw.line((x + width * 0.18, y + height * 0.22, x + width * 0.82, y + height * 0.22), fill=(*shade(front, 0.2), 255), width=max(1, line // 2))
        draw.line((x + width * 0.18, y + height * 0.55, x + width * 0.82, y + height * 0.55), fill=(*shade(front, -0.15), 255), width=max(1, line // 2))
    draw.line((*top_poly, top_poly[0]), fill=(*ink, 255), width=line)
    draw.line((side_poly[1], side_poly[2], side_poly[3]), fill=(*ink, 255), width=line)
    draw.rounded_rectangle((x, y, x + width, y + height), radius=radius, outline=(*ink, 255), width=line)
    return image


def _draw_actor(draw, layout, beats, t, colors, line, aa) -> None:
    foot_x, foot_y, height = layout["person"]
    slots = _slots(layout)
    target = None
    if slots and layout["fast_count"] > 0:
        home_x, home_top = slots[0]
        target = (home_x + layout["box_w"] * 0.12, home_top + layout["box_h"] * 0.42)
    pose = _pose(t, layout, beats, target)
    _actor(
        draw,
        (foot_x * aa, foot_y * aa),
        height * aa,
        pose,
        colors,
        line,
        aa,
    )


def _actor(draw, foot, height, pose, colors, line, aa) -> None:
    fx, fy = foot
    unit = height / 100
    lean = pose["lean"]
    reach = max(0.0, min(1.0, pose["reach"]))
    look_x, look_y = pose["look"]
    brow = pose["brow"]
    mouth = pose["mouth"]
    cx = fx + lean * 5 * unit
    skin = colors["skin"]
    ink = colors["ink"]

    def at(dx: float, up: float) -> tuple[float, float]:
        return (cx + dx * unit, fy - up * unit)

    draw.ellipse((fx - 16 * unit, fy - 2 * unit, fx + 16 * unit, fy + 4.5 * unit), fill=(*ink, 40))
    limb = max(line, int(4.6 * unit))
    for side in (-1, 1):
        hip = at(side * 6.5, 40)
        knee = at(side * 8, 22)
        shoe = at(side * 9, 2)
        _limb(draw, hip, knee, limb, colors["pants"])
        _limb(draw, knee, shoe, limb, colors["pants"])
        draw.rounded_rectangle(
            (shoe[0] - 5 * unit, fy - 4.2 * unit, shoe[0] + 7 * unit, fy),
            radius=max(1, int(unit)),
            fill=(*ink, 255),
        )
    draw.rounded_rectangle((*at(-15, 66), *at(15, 54)), radius=max(2, int(3 * unit)), fill=(*colors["shirt"], 255), outline=(*ink, 255), width=line)
    apron = [at(-10, 59), at(10, 59), at(13.5, 39), at(-13.5, 39)]
    draw.polygon(apron, fill=(*colors["cloth"], 255))
    draw.line((*apron, apron[0]), fill=(*ink, 255), width=line)
    strap_l, strap_r = at(-2.2, 64), at(2.2, 64)
    draw.polygon([strap_l, strap_r, at(1.2, 52), at(-1.2, 52)], fill=(*shade(colors["cloth"], -0.2), 255))
    draw.rounded_rectangle((*at(-3.4, 72), *at(3.4, 64)), radius=max(1, int(unit)), fill=(*skin, 255))
    shoulder_l = at(-14, 62)
    elbow_l = at(-18, 50)
    hand_l = at(-16 - lean * 2, 42)
    sleeve = max(line, int(3.8 * unit))
    _limb(draw, shoulder_l, elbow_l, sleeve, colors["shirt"])
    _limb(draw, elbow_l, hand_l, max(line, int(3.2 * unit)), skin)
    _hand(draw, hand_l, unit * 0.85, skin, ink, line)
    hx, hy = at(lean * 1.2, 82)
    rx, ry = 11.2 * unit, 12.4 * unit
    draw.ellipse((hx - rx * 0.95, hy - ry * 0.15, hx - rx * 0.45, hy + ry * 0.45), fill=(*shade(skin, -0.06), 255), outline=(*ink, 255), width=max(1, line // 2))
    draw.ellipse((hx - rx, hy - ry, hx + rx, hy + ry), fill=(*skin, 255), outline=(*ink, 255), width=line)
    draw.pieslice((hx - rx * 0.98, hy - ry * 1.05, hx + rx * 1.05, hy + ry * 0.15), 200, 340, fill=(*colors["hair"], 255))
    draw.polygon([
        (hx - rx * 0.2, hy - ry * 0.95),
        (hx + rx * 0.72, hy - ry * 0.72),
        (hx + rx * 0.15, hy - ry * 0.28),
    ], fill=(*colors["hair"], 255))
    for side in (-1, 1):
        ex = hx + side * 4.2 * unit + look_x * 1.3 * unit
        ey = hy + 1.4 * unit + look_y * 1.1 * unit
        draw.ellipse((ex - 2.15 * unit, ey - 2.45 * unit, ex + 2.15 * unit, ey + 2.45 * unit), fill=(255, 255, 255, 255), outline=(*ink, 255), width=max(1, line // 2))
        pupil = 1.15 * unit
        px = ex + look_x * 0.7 * unit
        py = ey + look_y * 0.55 * unit
        draw.ellipse((px - pupil, py - pupil, px + pupil, py + pupil), fill=(*ink, 255))
        brow_y = ey - 4.3 * unit
        inner = brow_y + brow * 1.7 * unit
        outer = brow_y - 0.6 * unit
        if side < 0:
            draw.line((ex - 2.4 * unit, outer, ex + 2.4 * unit, inner), fill=(*ink, 255), width=max(2, line - 1))
        else:
            draw.line((ex - 2.4 * unit, inner, ex + 2.4 * unit, outer), fill=(*ink, 255), width=max(2, line - 1))
    draw.line((hx + 0.2 * unit, hy + 2.4 * unit, hx + 1.1 * unit, hy + 5.4 * unit), fill=(*shade(skin, -0.3), 255), width=max(1, line // 2))
    my = hy + 7.6 * unit
    if mouth > 0.22:
        draw.ellipse((hx - 1.8 * unit, my, hx + 2.2 * unit, my + 2.1 * unit * mouth), fill=(*shade(skin, -0.35), 255))
    elif mouth < -0.12:
        draw.arc((hx - 2.8 * unit, my - 1.6 * unit, hx + 2.8 * unit, my + 2.2 * unit), 200, 340, fill=(*ink, 255), width=max(2, line - 1))
    else:
        draw.line((hx - 2.2 * unit, my, hx + 2.3 * unit, my + 0.4 * unit), fill=(*ink, 255), width=max(2, line - 1))
    shoulder_r = at(14, 62)
    rest = at(16, 46)
    target = pose.get("hand_target")
    if target is None:
        target = rest
    else:
        target = (target[0] * aa, target[1] * aa)
        dx = target[0] - shoulder_r[0]
        dy = target[1] - shoulder_r[1]
        dist = math.hypot(dx, dy) or 1
        limit = height * 0.46
        if dist > limit:
            target = (shoulder_r[0] + dx / dist * limit, shoulder_r[1] + dy / dist * limit)
    hand_r = (lerp(rest[0], target[0], reach), lerp(rest[1], target[1], reach))
    mx = (shoulder_r[0] + hand_r[0]) / 2
    my_arm = (shoulder_r[1] + hand_r[1]) / 2
    dx = hand_r[0] - shoulder_r[0]
    dy = hand_r[1] - shoulder_r[1]
    dist = math.hypot(dx, dy) or 1
    bend = lerp(8 * unit, 2.5 * unit, reach)
    elbow_r = (mx - dy / dist * bend, my_arm + dx / dist * bend - 3 * unit * (1 - reach))
    _limb(draw, shoulder_r, elbow_r, sleeve, colors["shirt"])
    _limb(draw, elbow_r, hand_r, max(line, int(3.2 * unit)), skin)
    _hand(draw, hand_r, unit * 0.9, skin, ink, line)


def _limb(draw, start, end, width, color) -> None:
    draw.line((*start, *end), fill=(*color, 255), width=width)
    radius = width / 2
    for point in (start, end):
        draw.ellipse((point[0] - radius, point[1] - radius, point[0] + radius, point[1] + radius), fill=(*color, 255))


def _hand(draw, center, unit, skin, ink, line) -> None:
    draw.ellipse(
        (center[0] - 3.3 * unit, center[1] - 2.6 * unit, center[0] + 3.3 * unit, center[1] + 2.6 * unit),
        fill=(*skin, 255),
        outline=(*ink, 255),
        width=max(1, line // 2),
    )


def _lock_dial(size: int, ink: tuple[int, int, int], paper: tuple[int, int, int], turns: float, line: int) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((1, 1, size - 2, size - 2), fill=(*paper, 255), outline=(*ink, 255), width=max(2, line))
    draw.ellipse((size * 0.22, size * 0.22, size * 0.78, size * 0.78), outline=(*ink, 255), width=max(1, line // 2))
    for tick in range(8):
        angle = tick * math.tau / 8 - math.pi / 2
        inner = size * 0.34
        outer = size * 0.44
        draw.line(
            (
                size / 2 + math.cos(angle) * inner,
                size / 2 + math.sin(angle) * inner,
                size / 2 + math.cos(angle) * outer,
                size / 2 + math.sin(angle) * outer,
            ),
            fill=(*ink, 255),
            width=max(1, line // 2),
        )
    pointer = 0.9 + turns * math.tau
    knob = size * 0.16
    px = size / 2 + math.cos(pointer) * size * 0.18
    py = size / 2 + math.sin(pointer) * size * 0.18
    draw.ellipse((px - knob, py - knob, px + knob, py + knob), fill=(*ink, 255))
    return image


def _draw_safe(plate, draw, layout, beats, t, colors, line, aa) -> None:
    impact = _impact(t, beats)
    dip = math.sin(min(1.0, impact) * math.pi) * layout["floor_y"] * 0.012 * aa
    x, y, w, h = [value * aa for value in layout["safe"]]
    y += dip
    depth = max(6, h * 0.16)
    body = shade(colors["weight"], 0.08)
    top = [(x, y), (x + w, y), (x + w - depth * 0.4, y - depth), (x + depth * 0.25, y - depth)]
    side = [(x + w, y), (x + w - depth * 0.4, y - depth), (x + w - depth * 0.4, y + h - depth), (x + w, y + h)]
    draw.polygon(top, fill=(*shade(body, 0.18), 255))
    draw.polygon(side, fill=(*shade(body, -0.18), 255))
    radius = max(3, int(min(w, h) * 0.06))
    draw.rounded_rectangle((x, y, x + w, y + h), radius=radius, fill=(*body, 255), outline=(*colors["ink"], 255), width=line)
    draw.line((*top, top[0]), fill=(*colors["ink"], 255), width=line)
    draw.line((x + w * 0.46, y + 6, x + w * 0.46, y + h - 6), fill=(*shade(body, 0.22), 255), width=max(2, line))
    handle = (x + w * 0.18, y + h * 0.42, x + w * 0.28, y + h * 0.58)
    draw.rounded_rectangle(handle, radius=max(2, int(h * 0.06)), fill=(*shade(body, 0.28), 255), outline=(*colors["ink"], 255), width=max(1, line // 2))
    dial_size = max(16, int(min(w, h) * 0.34))
    turns = smoothstep(progress(t, beats["lock"][0], beats["lock"][1])) * 0.7
    face = _lock_dial(dial_size, colors["ink"], colors["paper"], turns, line)
    _paste(plate, face, (x + w * 0.67 - dial_size / 2, y + h * 0.52 - dial_size / 2), 1)
    foot = max(3, int(h * 0.04))
    draw.rectangle((x + w * 0.12, y + h - foot, x + w * 0.22, y + h + foot), fill=(*shade(body, -0.2), 255))
    draw.rectangle((x + w * 0.78, y + h - foot, x + w * 0.88, y + h + foot), fill=(*shade(body, -0.2), 255))
    _draw_coin(plate, layout, beats, t, colors, line, aa, dip)


def _draw_coin(plate, layout, beats, t, colors, line, aa, dip) -> None:
    start = beats["transform"][0]
    end = beats["drop"][0] + (beats["drop"][1] - beats["drop"][0]) * 0.55
    if t < start:
        return
    amount = smoothstep(progress(t, start, end))
    safe_x, safe_y, safe_w, safe_h = layout["safe"]
    diameter = max(22.0, min(safe_h * 0.5, layout["width"] * 0.052))
    origin_x = safe_x + safe_w - diameter * 0.15
    origin_y = safe_y - diameter * 0.35
    dest_x = min(layout["width"] * 0.92, origin_x + layout["width"] * 0.1)
    dest_y = origin_y - safe_h * 0.02
    if amount < 0.18:
        rock = math.sin(amount / 0.18 * math.pi) * 14
        x = origin_x
        y = origin_y
        rot = rock
    else:
        travel = ease_out((amount - 0.18) / 0.82)
        x = lerp(origin_x, dest_x, travel)
        y = lerp(origin_y, dest_y, travel)
        rot = travel * 420
    sprite = _coin(int(diameter * aa), colors["coin"], colors["ink"], max(2, line))
    sprite = rotate(sprite, rot)
    _paste(plate, sprite, ((x * aa) - sprite.width / 2, (y * aa) + dip - sprite.height / 2), 1)


@lru_cache(maxsize=32)
def _coin(diameter: int, fill: tuple[int, int, int], ink: tuple[int, int, int], line: int) -> Image.Image:
    diameter = max(8, diameter)
    image = Image.new("RGBA", (diameter, diameter), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((1, 1, diameter - 2, diameter - 2), fill=(*fill, 255), outline=(*ink, 255), width=line)
    pad = diameter * 0.28
    draw.ellipse((pad, pad, diameter - pad, diameter - pad), outline=(*ink, 255), width=max(1, line - 1))
    draw.rounded_rectangle((diameter * 0.44, diameter * 0.24, diameter * 0.56, diameter * 0.76), radius=max(1, diameter // 10), fill=(*ink, 255))
    return image


def _paste(base: Image.Image, sprite: Image.Image, xy: tuple[float, float], opacity: float) -> None:
    if opacity <= 0.02 or sprite.width < 1 or sprite.height < 1:
        return
    x = int(xy[0])
    y = int(xy[1])
    if x >= base.width or y >= base.height or x + sprite.width <= 0 or y + sprite.height <= 0:
        return
    layer = sprite
    if opacity < 0.99:
        layer = sprite.copy()
        alpha = layer.getchannel("A").point(lambda pixel, gain=opacity: int(pixel * gain))
        layer.putalpha(alpha)
    left = max(x, 0)
    top = max(y, 0)
    right = min(x + layer.width, base.width)
    bottom = min(y + layer.height, base.height)
    crop = layer.crop((left - x, top - y, right - x, bottom - y))
    base.alpha_composite(crop, (left, top))


def _present(plate: Image.Image, width: int, height: int, zoom: float, drift: float, pan: float) -> Image.Image:
    zoom = max(1.0, zoom)
    cw = max(1, int(plate.width / zoom))
    ch = max(1, int(plate.height / zoom))
    left = int((plate.width - cw) / 2 + pan * plate.width)
    top = int((plate.height - ch) / 2 + drift * plate.height)
    left = max(0, min(left, plate.width - cw))
    top = max(0, min(top, plate.height - ch))
    crop = plate.crop((left, top, left + cw, top + ch))
    return crop.resize((width, height), Image.Resampling.LANCZOS)


def _headline(ctx: FrameContext) -> None:
    content = ctx.params.get("headline")
    if not isinstance(content, str) or not content:
        return
    start = scalar(ctx, "headlineAt", ctx.duration * 0.82)
    amount = 1.0 if ctx.t >= start + 0.45 else ease_out(progress(ctx.t, start, start + 0.45))
    if amount <= 0.02:
        return
    left, top, right, _bottom = ctx.safe()
    max_width = (right - left) * 0.94
    size = max(16, int(ctx.height * (0.042 if ctx.height > ctx.width else 0.05)))
    while size > 14:
        font = ctx.font(size, bold=True)
        box = font.getbbox(content)
        if box[2] - box[0] <= max_width:
            break
        size -= 2
    font = ctx.font(size, bold=True)
    box = font.getbbox(content)
    text_w = box[2] - box[0]
    text_h = box[3] - box[1]
    y = top + lerp(ctx.height * 0.028, ctx.height * 0.008, amount)
    card = Image.new("RGBA", ctx.image.size, (0, 0, 0, 0))
    pad_x = int(ctx.width * 0.012)
    pad_y = int(text_h * 0.32)
    rect = (
        int(ctx.width / 2 - text_w / 2 - pad_x),
        int(y - pad_y),
        int(ctx.width / 2 + text_w / 2 + pad_x),
        int(y + text_h + pad_y),
    )
    ImageDraw.Draw(card).rounded_rectangle(rect, radius=max(6, text_h // 3), fill=(*ctx.color("paper"), int(235 * amount)))
    ctx.image.alpha_composite(card)
    ctx.text(content, (ctx.width / 2, y), size, ctx.color("ink"), bold=True, anchor="mt", opacity=amount)


def render_story_frame(style: dict, params: dict, size: tuple[int, int], t: float, duration: float = 16) -> tuple[Image.Image, set]:
    scene = {"sceneId": "frame", "templateId": "stock_story", "params": params}
    cache: dict = {}
    ctx = FrameContext(scene, style, size[0], size[1], t, duration, cache)
    paint_stock_story(ctx)
    return ctx.image, set(cache.get("texts") or ())


def write_storyboard(dest: Path, style: dict, beats: dict, duration: float = 16.65) -> Path:
    dest.mkdir(parents=True, exist_ok=True)
    moments = (
        ("01-open", 0.45),
        ("02-sell", 4.15),
        ("03-weight", 10.1),
        ("04-money", 14.2),
    )
    labels = {
        "acting": "1. Hành động nhân vật",
        "metaphor": "2. Ẩn dụ đạo cụ",
        "hybrid": "3. Kết hợp",
    }
    thumbs = []
    for mode in ("acting", "metaphor", "hybrid"):
        row = []
        for name, t in moments:
            params = {
                "mode": mode,
                "headline": "Hàng tồn kho cũng là vốn",
                "boxCount": 5,
                "fastCount": 2,
                "beats": beats,
                "headlineAt": 13.15,
            }
            frame, _texts = render_story_frame(style, params, (960, 540), t, duration)
            path = dest / f"{mode}-{name}.png"
            frame.convert("RGB").save(path, quality=95)
            row.append(frame.resize((480, 270), Image.Resampling.LANCZOS))
        thumbs.append((labels[mode], row))
    sheet = Image.new("RGB", (1920, 90 + 3 * 300), (244, 241, 234))
    draw = ImageDraw.Draw(sheet)
    font_path = style["typography"]["fontBold"]
    font = load_font(font_path, 28)
    small = load_font(style["typography"]["fontRegular"], 18)
    draw.text((24, 16), "NOVIXA style proof V2 — ba hướng, cùng lời, cùng nhịp", font=font, fill=(0, 32, 96))
    captions = ("Mở: hộp vào kệ", "Bán vèo: hộp bay", "Hàng nằm: khối nặng", "Tiền đi trước, hàng khóa")
    for index, caption in enumerate(captions):
        draw.text((24 + index * 480, 52), caption, font=small, fill=(40, 40, 40))
    for row_index, (label, row) in enumerate(thumbs):
        y = 90 + row_index * 300
        draw.text((8, y + 4), label, font=small, fill=(0, 32, 96))
        for col, thumb in enumerate(row):
            sheet.paste(thumb, (col * 480, y + 28))
    contact = dest / "contact.png"
    sheet.save(contact, quality=95)
    reuse_style = {
        "profileId": "sample-east",
        "palette": {
            "background": [226, 232, 224],
            "ink": [24, 48, 36],
            "accent": [36, 122, 92],
            "paper": [248, 246, 238],
            "muted": [90, 104, 94],
            "accentAlt": [184, 112, 48],
            "weight": [28, 48, 40],
            "skin": [206, 164, 132],
            "wood": [132, 96, 64],
            "wall": [236, 240, 232],
            "floor": [206, 214, 204],
            "cloth": [36, 122, 92],
            "coin": [196, 154, 64],
        },
        "typography": style["typography"],
        "safeArea": style["safeArea"],
    }
    reuse, _texts = render_story_frame(
        reuse_style,
        {
            "mode": "hybrid",
            "headline": "Hàng nằm lại",
            "boxCount": 3,
            "fastCount": 1,
            "beats": beats,
            "headlineAt": 13.15,
            "pipelineTestLabel": "DỰ ÁN KIỂM THỬ",
        },
        (540, 960),
        10.1,
        duration,
    )
    reuse.convert("RGB").save(dest / "reuse-portrait.png", quality=95)
    return contact
