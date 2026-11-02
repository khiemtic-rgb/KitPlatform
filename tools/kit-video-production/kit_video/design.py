"""Shared commercial layouts. Copy, color, and the board kind come from the shot plan."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

from kit_video.camera import CameraMove, render_plate
from kit_video.canvas import FrameContext, ease_out, lerp, paint_background, progress
from kit_video.composition import compose
from kit_video.errors import ProductionError, issue
from kit_video.planning import production_gate, select_method, select_scene_asset, visual_plan
from kit_video.still import confirm_file, inspect_asset, referenced_ids, visual_source
from kit_video.validate import resolve_path
from kit_video.visual_quality import assess_breathing, assess_intent, assess_scene


def designed_board(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    plan = compose(ctx.style, ctx.params, str(ctx.params.get("kind") or ""), ctx.t, ctx.duration)
    ctx.cache["scene_plan"] = plan
    ctx.cache["artDirection"] = plan["profile"]
    brief = visual_plan(ctx.scene, assets)
    plans = ctx.cache.setdefault("visual_plans", [])
    if not any(row.get("sceneId") == ctx.scene["sceneId"] for row in plans):
        plans.append({"sceneId": ctx.scene["sceneId"], "plan": brief, "gate": production_gate(brief)})
    choice = select_method(ctx.scene, assets)
    methods = ctx.cache.setdefault("method_decisions", [])
    if not any(row.get("sceneId") == ctx.scene["sceneId"] for row in methods):
        methods.append(choice)
    if not choice["paint"] and choice["pictureKind"] in {"setting", "person", "ui"}:
        raise ProductionError("BLOCKED", [issue(choice["code"] or "no_method", ctx.scene["sceneId"], choice["reason"] or "Không có phương pháp phù hợp.")])
    if not choice["paint"] and choice["code"] == "false_comparison":
        raise ProductionError("FAIL", [issue("false_comparison", ctx.scene["sceneId"], choice["reason"])])
    pictures = ctx.cache.setdefault("composition_decisions", [])
    if not any(row.get("sceneId") == ctx.scene["sceneId"] for row in pictures):
        motion = plan["motion"]
        pictures.append({
            "sceneId": ctx.scene["sceneId"],
            "family": plan["family"],
            "ground": plan["ground"],
            "idea": plan["intent"]["idea"],
            "subject": plan["intent"]["subject"],
            "focal": plan["intent"]["focal"],
            "boundAssets": plan["boundAssets"],
            "motion": {"emotion": motion["emotion"], "start": motion["start"], "end": motion["end"]},
        })
    intent = assess_intent(plan["profile"], plan["intent"])
    if intent["status"] == "blocked":
        raise ProductionError("BLOCKED", [issue(intent["code"], ctx.scene["sceneId"], intent["reason"])])
    decision = ctx.contract.fit_board(str(ctx.params.get("kind") or ""), ctx.params)
    decisions = ctx.cache.setdefault("layout_decisions", [])
    if not any(row.get("sceneId") == ctx.scene["sceneId"] for row in decisions):
        decisions.append({"sceneId": ctx.scene["sceneId"], **decision})
    if decision["status"] == "blocked":
        raise ProductionError("BLOCKED", [issue(decision["code"], ctx.scene["sceneId"], decision["reason"])])
    visual = assess_scene(ctx.contract, decision["kind"], ctx.params)
    visual_rows = ctx.cache.setdefault("visual_decisions", [])
    if not any(row.get("sceneId") == ctx.scene["sceneId"] for row in visual_rows):
        visual_rows.append({"sceneId": ctx.scene["sceneId"], **visual})
    if visual["status"] == "blocked":
        raise ProductionError("BLOCKED", [issue(visual["code"], ctx.scene["sceneId"], visual["reason"])])
    painter = BOARDS[decision["kind"]]
    painter(ctx, assets, repo_root, project_dir)
    _place_required(ctx)
    ctx.cache["spacingFindings"] = assess_breathing(
        ctx.samples,
        ctx.width,
        ctx.height,
        ctx.style.get("safeArea") or {},
        ctx.contract.space("sm"),
    )


def _wrap_line(ctx: FrameContext, text: str, size: int, width: int) -> list[str]:
    font = ctx.font(size, False)
    lines: list[str] = []
    current = ""
    for word in text.split():
        trial = word if not current else f"{current} {word}"
        if font.getlength(trial) <= width:
            current = trial
            continue
        if current:
            lines.append(current)
        current = word
    if current:
        lines.append(current)
    return lines or [text]


def _place_required(ctx: FrameContext) -> None:
    """Draw declared on-screen lines the board did not already paint. Wraps inside the safe area."""
    shown = list(ctx.samples)
    missing: list[str] = []
    for item in ctx.scene.get("onScreenText") or []:
        if not isinstance(item, str) or not item.strip():
            continue
        text = item.strip()
        if any(text == key or text in key for key in shown):
            continue
        missing.append(text)
    if ctx.cache.get("deferOnScreenText"):
        ctx.cache["deferredOnScreenText"] = missing
        return
    if not missing:
        return
    left, _top, right, bottom = ctx.safe()
    size = ctx.contract.px("caption")
    inner = right - left - ctx.contract.space("sm") * 2
    lines: list[str] = []
    for text in missing:
        lines.extend(_wrap_line(ctx, text, size, inner))
    line_h = size + ctx.contract.space("xs")
    band_h = line_h * len(lines) + ctx.contract.space("sm")
    top = max(int(bottom - band_h), int(_top))
    ImageDraw.Draw(ctx.image).rectangle((left, top, right, bottom), fill=(*ctx.color("paper"), 255))
    y = top + ctx.contract.space("xs")
    for line in lines:
        ctx.text(line, (left + ctx.contract.space("sm"), y), size, ctx.color("ink"), anchor="lt")
        y += line_h


def hero_still(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    if _family(ctx) == "photographic":
        _photographic(ctx, assets, repo_root, project_dir)
        return
    source = _asset_image(ctx, assets, repo_root, project_dir, "assetId")
    move = CameraMove.parse(ctx.params.get("preset", "hold"), ctx.params.get("amount", 0))
    plate = render_plate(source, (ctx.width, ctx.height), move, ctx.t, ctx.duration, ctx.color("background"))
    ctx.image.alpha_composite(_cover(plate, ctx.width, ctx.height))
    ctx.note_asset(str(ctx.params.get("assetId")), None)
    _lower_third(ctx, ctx.params.get("title"))


def portrait_split(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    if _family(ctx) == "portrait-object":
        _portrait_object(ctx, assets, repo_root, project_dir)
        return
    paint_background(ctx)
    source = _asset_image(ctx, assets, repo_root, project_dir, "assetId")
    panel_w = int(ctx.width * 0.46)
    panel = _portrait_panel(ctx, source, panel_w)
    ctx.image.alpha_composite(panel, (ctx.width - panel_w, 0))
    ctx.note_asset(str(ctx.params.get("assetId")), None)
    card_left = ctx.safe()[0]
    card_right = ctx.width - panel_w - 28
    lines = ctx.params.get("lines") or []
    pad = ctx.contract.space("lg")
    text_w = card_right - card_left - pad * 2
    block = _line_block_height(ctx, lines, text_w)
    card_h = block + pad * 2
    card_top = ctx.contract.place_block(ctx.safe()[1], ctx.safe()[3], card_h)
    card_bottom = card_top + card_h
    _card(ctx, (card_left, card_top, card_right, card_bottom))
    _stack(ctx, lines, card_left + pad, card_top + pad, text_w)


def brand_center(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    if _family(ctx) == "mark-field":
        _mark_field(ctx, assets, repo_root, project_dir)
        return
    paint_background(ctx)
    logo = _logo(ctx, assets, repo_root, project_dir)
    target = int(ctx.height * float(ctx.params.get("logoScale", 0.42)))
    arrive = _arrive(ctx)
    fitted = _fit(logo, int(target * lerp(0.92, 1.0, arrive)), int(target * lerp(0.92, 1.0, arrive)))
    ctx.blit(fitted, ((ctx.width - fitted.width) / 2, ctx.height * 0.16))
    _center_line(ctx, ctx.params.get("line"), 0.68, 0.04, "ink")
    _center_line(ctx, ctx.params.get("url"), 0.78, 0.046, "accent", bold=True)


def brand_split(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    if _family(ctx) == "anchored-line":
        _anchored_line(ctx, assets, repo_root, project_dir)
        return
    paint_background(ctx)
    band = int(ctx.width * 0.4)
    ImageDraw.Draw(ctx.image).rectangle((0, 0, band, ctx.height), fill=(*ctx.color("ink"), 255))
    logo = _logo(ctx, assets, repo_root, project_dir)
    arrive = _arrive(ctx)
    fitted = _fit(logo, int(band * 0.62 * lerp(0.92, 1.0, arrive)), int(ctx.height * 0.36 * lerp(0.92, 1.0, arrive)))
    plate = (int((band - fitted.width) / 2) - 28, int((ctx.height - fitted.height) / 2) - 28)
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (plate[0], plate[1], plate[0] + fitted.width + 56, plate[1] + fitted.height + 56),
        radius=18,
        fill=(*ctx.color("paper"), 255),
    )
    ctx.blit(fitted, ((band - fitted.width) / 2, (ctx.height - fitted.height) / 2))
    lines = ctx.params.get("lines") or []
    pad = ctx.contract.space("lg")
    text_w = int(ctx.width * 0.42)
    block = _line_block_height(ctx, lines, text_w)
    card_w = text_w + pad * 2
    card_h = block + pad * 2
    card_left = band + int((ctx.width - band - card_w) / 2)
    card_top = ctx.contract.place_block(ctx.safe()[1], ctx.safe()[3], card_h)
    shift = int((1 - _beat(ctx, 0.18, 0.42)) * ctx.contract.space("lg"))
    _card(ctx, (card_left + shift, card_top, card_left + card_w + shift, card_top + card_h))
    _stack(ctx, lines, card_left + pad + shift, card_top + pad, text_w)


def end_lockup(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    if _family(ctx) == "lockup":
        _lockup(ctx, assets, repo_root, project_dir)
        return
    paint_background(ctx)
    logo = _logo(ctx, assets, repo_root, project_dir)
    arrive = _arrive(ctx)
    fitted = _fit(logo, int(ctx.height * 0.4 * lerp(0.92, 1.0, arrive)), int(ctx.height * 0.4 * lerp(0.92, 1.0, arrive)))
    ctx.blit(fitted, ((ctx.width - fitted.width) / 2, ctx.height * 0.1))
    if _beat(ctx, 0.08, 0.22) > 0.02:
        _center_line(ctx, ctx.params.get("line"), 0.58, 0.038, "ink")
    url = ctx.params.get("url")
    if isinstance(url, str) and url and _beat(ctx, 0.16, 0.32) > 0.02:
        _chip(ctx, url, (ctx.width / 2, ctx.height * 0.74), "accent", "paper")


def app_screen(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    paint_background(ctx)
    bar_h = int(ctx.height * 0.08)
    ImageDraw.Draw(ctx.image).rectangle((0, 0, ctx.width, bar_h), fill=(*ctx.color("ink"), 255))
    y = place_notices(ctx, bar_h + int(ctx.height * 0.02))
    x = ctx.safe()[0]
    width = ctx.safe()[2] - x
    y = max(y, bar_h + int(ctx.height * 0.045))
    bottom = ctx.safe()[3]
    title = ctx.params.get("title")
    if isinstance(title, str):
        ctx.text(title, (x, y), _px(ctx, float(ctx.params.get("titleScale", 0.04))), ctx.color("ink"), bold=True)
        y += _px(ctx, float(ctx.params.get("titleScale", 0.04)) + 0.02)
    subtitle = ctx.params.get("subtitle")
    if isinstance(subtitle, str):
        ctx.text(subtitle, (x, y), _px(ctx, 0.026), _muted(ctx))
        y += _px(ctx, 0.045)
    blocks = [block for block in (ctx.params.get("blocks") or []) if isinstance(block, dict)]
    callout = blocks[-1] if blocks and blocks[-1].get("kind") == "callout" else None
    body = blocks[:-1] if callout else blocks
    content_bottom = bottom - (_px(ctx, 0.11) if callout else 0)
    for block in body:
        y = _block(ctx, block, x, y, width, content_bottom if len(body) == 1 else y + _px(ctx, 0.24))
    if callout:
        _callout(ctx, str(callout.get("text") or ""), x, content_bottom, width)


def manifesto(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    ImageDraw.Draw(ctx.image).rectangle((0, 0, ctx.width, ctx.height), fill=(*ctx.color("ink"), 255))
    x = ctx.safe()[0]
    lines = [line for line in (ctx.params.get("lines") or []) if isinstance(line, str)]
    block_h = _px(ctx, 0.12) * max(1, len(lines))
    y = int((ctx.height - block_h) / 2)
    for index, line in enumerate(lines):
        ctx.text(f"{index + 1:02d}", (x, y), _px(ctx, 0.028), ctx.color("accent"), bold=True, anchor="lm")
        ctx.text(line, (x + int(ctx.width * 0.08), y), _px(ctx, 0.07), ctx.color("paper"), bold=True, anchor="lm")
        y += _px(ctx, 0.12)
    note = ctx.params.get("note")
    if isinstance(note, str):
        ctx.text(note, (x, ctx.safe()[3] - _px(ctx, 0.06)), _px(ctx, 0.026), ctx.color("paper"))


def _block(ctx: FrameContext, block: dict, x: int, y: int, width: int, bottom: int) -> int:
    kind = block.get("kind")
    if kind == "timeline":
        return _timeline(ctx, block.get("rows") or [], x, y, width, bottom)
    if kind == "columns":
        return _columns(ctx, block.get("columns") or [], x, y, width, bottom)
    if kind == "table":
        return _table(ctx, block, x, y, width, bottom)
    if kind == "callout":
        return _callout(ctx, str(block.get("text") or ""), x, y, width)
    if kind == "fields":
        return _fields(ctx, block.get("rows") or [], x, y, width, bottom)
    return y


def _timeline(ctx: FrameContext, rows: list, x: int, y: int, width: int, bottom: int) -> int:
    items = [row for row in rows if isinstance(row, dict)]
    if not items:
        return y
    gap = 12
    row_h = max(_px(ctx, 0.08), int((bottom - y - gap * (len(items) - 1)) / len(items)))
    for row in items:
        _card(ctx, (x, y, x + width, y + row_h))
        ImageDraw.Draw(ctx.image).rectangle((x, y, x + 8, y + row_h), fill=(*ctx.color("accent"), 255))
        detail = row.get("detail")
        label_y = y + row_h / 2 - (_px(ctx, 0.02) if detail else 0)
        ctx.text(str(row.get("label", "")), (x + 28, label_y), _px(ctx, 0.034), ctx.color("ink"), bold=True, anchor="lm")
        if detail:
            ctx.text(str(detail), (x + 28, label_y + _px(ctx, 0.04)), _px(ctx, 0.022), _muted(ctx), anchor="lm")
        ctx.text(str(row.get("state", "")), (x + width - 24, y + row_h / 2), _px(ctx, 0.024), ctx.color("accent"), bold=True, anchor="rm")
        y += row_h + gap
    return y


def _columns(ctx: FrameContext, columns: list, x: int, y: int, width: int, bottom: int) -> int:
    items = [column for column in columns if isinstance(column, dict)]
    if not items:
        return y
    gap = _px(ctx, 0.016)
    col_w = int((width - gap * (len(items) - 1)) / len(items))
    height = max(_px(ctx, 0.24), bottom - y)
    for index, column in enumerate(items):
        left = x + index * (col_w + gap)
        _card(ctx, (left, y, left + col_w, y + height))
        ctx.text(str(column.get("label", "")), (left + 20, y + 22), _px(ctx, 0.032), ctx.color("ink"), bold=True)
        ctx.text(str(column.get("body", "")), (left + 20, y + _px(ctx, 0.09)), _px(ctx, 0.024), _muted(ctx))
        item_y = y + _px(ctx, 0.15)
        for item in column.get("items") or []:
            ctx.text(str(item), (left + 20, item_y), _px(ctx, 0.024), ctx.color("ink"))
            item_y += _px(ctx, 0.05)
        bar = float(column.get("bar", 0.4))
        bar_w = int((col_w - 40) * max(0.08, min(bar, 1)))
        ImageDraw.Draw(ctx.image).rounded_rectangle(
            (left + 20, y + height - 36, left + 20 + bar_w, y + height - 18),
            radius=6,
            fill=(*ctx.color("accent"), 255),
        )
    return y + height


def _table(ctx: FrameContext, block: dict, x: int, y: int, width: int, bottom: int) -> int:
    columns = [str(item) for item in block.get("columns") or []]
    items = [row for row in (block.get("rows") or []) if isinstance(row, list)]
    if not columns or not items:
        return y
    col_w = width / len(columns)
    header_h = _px(ctx, 0.05)
    for index, column in enumerate(columns):
        ctx.text(column, (x + int(index * col_w), y + header_h / 2), _px(ctx, 0.02), _muted(ctx), bold=True, anchor="lm")
    y += header_h
    row_h = max(_px(ctx, 0.07), int((bottom - y) / len(items)))
    for row_index, row in enumerate(items):
        if row_index % 2 == 0:
            ImageDraw.Draw(ctx.image).rectangle((x, y, x + width, y + row_h), fill=(*ctx.color("paper"), 255))
        for index, cell in enumerate(row):
            ctx.text(str(cell), (x + int(index * col_w) + 8, y + row_h / 2), _px(ctx, 0.028), ctx.color("ink"), bold=index == 0, anchor="lm")
        y += row_h
    return y


def _fields(ctx: FrameContext, rows: list, x: int, y: int, width: int, bottom: int) -> int:
    items = [row for row in rows if isinstance(row, dict)]
    if not items:
        return y
    gap = 12
    row_h = max(_px(ctx, 0.08), int((bottom - y - gap * (len(items) - 1)) / len(items)))
    for row in items:
        _card(ctx, (x, y, x + width, y + row_h))
        ctx.text(str(row.get("label", "")), (x + 24, y + row_h / 2), _px(ctx, 0.024), _muted(ctx), anchor="lm")
        ctx.text(str(row.get("value", "")), (x + int(width * 0.38), y + row_h / 2), _px(ctx, 0.032), ctx.color("ink"), bold=True, anchor="lm")
        y += row_h + gap
    return y


def _callout(ctx: FrameContext, text: str, x: int, y: int, width: int) -> int:
    if not text:
        return y
    height = _px(ctx, 0.09)
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (x, y, x + width, y + height),
        radius=12,
        fill=(*ctx.color("ink"), 255),
    )
    ctx.text(text, (x + 18, y + height / 2), _px(ctx, 0.026), ctx.color("paper"), bold=True, anchor="lm")
    return y + height + _px(ctx, 0.02)


def _muted(ctx: FrameContext) -> tuple[int, int, int]:
    if "muted" in ctx.palette:
        return ctx.color("muted")
    return ctx.color("ink")


def _lower_third(ctx: FrameContext, title: object) -> None:
    if not isinstance(title, str) or not title:
        return
    band_h = int(ctx.height * 0.16)
    band = Image.new("RGBA", (ctx.width, band_h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(band)
    for index in range(band_h):
        alpha = int(210 * (index / band_h))
        draw.line((0, index, ctx.width, index), fill=(*ctx.color("ink"), alpha))
    ctx.blit(band, (0, ctx.height - band_h))
    ctx.text(title, (ctx.safe()[0], ctx.height - band_h + int(band_h * 0.38)), _px(ctx, 0.04), ctx.color("paper"), bold=True)


def _wrap(ctx: FrameContext, text: str, size: int, bold: bool, width: int) -> list[str]:
    font = ctx.font(size, bold)
    words = text.split()
    if not words or width <= 0:
        return [text] if text else []
    lines: list[str] = []
    current = words[0]
    for word in words[1:]:
        trial = current + " " + word
        if font.getlength(trial) <= width:
            current = trial
        else:
            lines.append(current)
            current = word
    lines.append(current)
    return lines


def _line_size(ctx: FrameContext, line: dict) -> int:
    bold = bool(line.get("bold", False))
    return max(_px(ctx, float(line.get("scale", 0.04))), ctx.contract.px("display" if bold else "body"))


def _line_block_height(ctx: FrameContext, lines: list, width: int) -> int:
    total = 0
    for line in lines:
        if not isinstance(line, dict):
            continue
        size = _line_size(ctx, line)
        parts = _wrap(ctx, str(line.get("text", "")), size, bool(line.get("bold", False)), width)
        total += int(size * 1.35) * max(1, len(parts))
    return total


def _stack(ctx: FrameContext, lines: list, x: int, y: int, width: int) -> None:
    for line in lines:
        if not isinstance(line, dict):
            continue
        bold = bool(line.get("bold", False))
        size = _line_size(ctx, line)
        color = ctx.color(str(line.get("color", "ink")))
        shown = str(line.get("text", ""))
        parts = _wrap(ctx, shown, size, bold, width)
        for part in parts:
            ctx.text(part, (x, y), size, color, bold=bold)
            y += int(size * 1.35)
        if len(parts) > 1 and shown:
            ctx.texts.add(shown)


def _center_line(ctx: FrameContext, text: object, y_ratio: float, scale: float, color: str, bold: bool = False) -> None:
    if isinstance(text, str) and text:
        ctx.text(text, (ctx.width / 2, ctx.height * y_ratio), _px(ctx, scale), ctx.color(color), bold=bold, anchor="mt")


def _pill(ctx: FrameContext, text: str, center: tuple[float, float], fill: str, ink: str) -> int:
    size = ctx.contract.px("body")
    font = ctx.font(size, True)
    box = ImageDraw.Draw(ctx.image).textbbox((0, 0), text, font=font)
    width = box[2] - box[0] + 28
    height = box[3] - box[1] + 16
    left = int(center[0] - width / 2)
    top = int(center[1] - height / 2)
    ImageDraw.Draw(ctx.image).rounded_rectangle((left, top, left + width, top + height), radius=height // 2, fill=(*ctx.color(fill), 255), outline=(*ctx.color("ink"), 255), width=1)
    ctx.text(text, center, size, ctx.color(ink), bold=True, anchor="mm")
    return width


def _event_block(ctx: FrameContext, text: str, center: tuple[float, float], left_limit: float, right_limit: float) -> None:
    size = ctx.contract.px("body")
    font = ctx.font(size, True)
    box = ImageDraw.Draw(ctx.image).textbbox((0, 0), text, font=font)
    width = min(box[2] - box[0] + 32, int((right_limit - left_limit) * 0.3))
    height = box[3] - box[1] + 22
    left = int(min(right_limit - width, max(left_limit, center[0] - width / 2)))
    top = int(center[1] - height / 2)
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (left, top, left + width, top + height),
        radius=12,
        fill=(*ctx.color("accent"), 255),
    )
    ctx.text(text, (left + width / 2, center[1]), size, ctx.color("paper"), bold=True, anchor="mm")


def _chip(ctx: FrameContext, text: str, center: tuple[float, float], fill: str, ink: str) -> None:
    size = _px(ctx, 0.04)
    font = ctx.font(size, True)
    box = ImageDraw.Draw(ctx.image).textbbox((0, 0), text, font=font)
    width = box[2] - box[0] + 48
    height = box[3] - box[1] + 28
    left = int(center[0] - width / 2)
    top = int(center[1] - height / 2)
    ImageDraw.Draw(ctx.image).rounded_rectangle((left, top, left + width, top + height), radius=height // 2, fill=(*ctx.color(fill), 255))
    ctx.text(text, (center[0], center[1]), size, ctx.color(ink), bold=True, anchor="mm")


def _card(ctx: FrameContext, box: tuple[int, int, int, int], fill: str = "paper") -> None:
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        box,
        radius=16,
        fill=(*ctx.color(fill), 255),
        outline=(*ctx.color("muted"), 255),
        width=2,
    )


def _logo(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> Image.Image:
    image = _asset_image(ctx, assets, repo_root, project_dir, "logoAssetId")
    ctx.note_asset(str(ctx.params.get("logoAssetId")), None)
    cache_key = ("logo-knockout", id(image))
    knocked = ctx.cache.get(cache_key)
    if knocked is None:
        knocked = _knock_out_white(image)
        ctx.cache[cache_key] = knocked
    return knocked


def _asset_image(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path, key: str) -> Image.Image:
    asset_id = ctx.params.get(key)
    row = assets.get(asset_id) if isinstance(asset_id, str) else None
    if row is None:
        raise ProductionError("BLOCKED", [issue("asset_missing", ctx.scene["sceneId"], f"Thiếu {key}.")])
    decision = inspect_asset(row)
    if not decision["allowed"]:
        raise ProductionError("BLOCKED", [issue("asset_not_cleared", str(asset_id), decision["reason"])])
    path = resolve_path(row, project_dir, repo_root)
    if path is None or not path.is_file():
        raise ProductionError("BLOCKED", [issue("asset_missing", str(asset_id), f"Không thấy file {asset_id}.")])
    check = confirm_file(row, path)
    if check["match"] is False:
        raise ProductionError("BLOCKED", [issue("asset_hash", str(asset_id), "File không khớp sha256 đã ghi.")])
    cache_key = ("design-source", str(path))
    source = ctx.cache.get(cache_key)
    if source is None:
        source = Image.open(path).convert("RGBA")
        source.load()
        ctx.cache[cache_key] = source
    return source


def _knock_out_white(image: Image.Image) -> Image.Image:
    """Drop a near-white matte. The mark itself is left unchanged."""
    image = image.convert("RGBA")
    pixels = []
    for red, green, blue, alpha in image.getdata():
        distance = max(255 - red, 255 - green, 255 - blue)
        if distance < 12:
            pixels.append((red, green, blue, 0))
        else:
            pixels.append((red, green, blue, min(alpha, min(255, distance * 6))))
    image.putdata(pixels)
    return image


def _cover(source: Image.Image, width: int, height: int) -> Image.Image:
    scale = max(width / source.width, height / source.height)
    resized = source.resize((max(1, int(source.width * scale)), max(1, int(source.height * scale))), Image.Resampling.LANCZOS)
    left = max(0, (resized.width - width) // 2)
    top = max(0, (resized.height - height) // 2)
    return resized.crop((left, top, left + width, top + height))


def _fit(source: Image.Image, max_w: int, max_h: int) -> Image.Image:
    scale = min(max_w / source.width, max_h / source.height)
    size = (max(1, int(source.width * scale)), max(1, int(source.height * scale)))
    return source.resize(size, Image.Resampling.LANCZOS)


def _arrive(ctx: FrameContext) -> float:
    return ease_out(progress(ctx.t, ctx.duration * 0.06, ctx.duration * 0.28))


def _beat(ctx: FrameContext, start: float, end: float) -> float:
    return ease_out(progress(ctx.t, ctx.duration * start, ctx.duration * end))


def _portrait_panel(ctx: FrameContext, source: Image.Image, panel_w: int) -> Image.Image:
    """Cover-crop the approved portrait, then ease a small push. No new pose is drawn."""
    panel = _cover(source, panel_w, ctx.height)
    move = CameraMove.parse(ctx.params.get("preset", "hold"), ctx.params.get("amount", 0))
    if move.preset != "push_in" or move.amount <= 0:
        return panel
    amount = ease_out(progress(ctx.t, ctx.duration * 0.08, ctx.duration * 0.82))
    scale = 1.0 + move.amount * amount
    zoom_w = max(panel_w + 1, int(round(panel.width * scale)))
    zoom_h = max(ctx.height + 1, int(round(panel.height * scale)))
    zoomed = panel.resize((zoom_w, zoom_h), Image.Resampling.LANCZOS)
    left = (zoom_w - panel_w) // 2
    top = (zoom_h - ctx.height) // 2
    return zoomed.crop((left, top, left + panel_w, top + ctx.height))


def _px(ctx: FrameContext, ratio: float) -> int:
    return ctx.contract.px(ratio)


def workspace(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    """Dense product-screen mockup. Every string comes from params."""
    family = _family(ctx)
    if family == "foreground-row":
        _foreground(ctx)
        return
    if family == "ticket":
        _ticket(ctx, assets, repo_root, project_dir)
        return
    if family == "quantity":
        _quantity(ctx)
        return
    del assets, repo_root, project_dir
    content = _chrome(ctx)
    x, y, width, bottom = content
    nav = [item for item in (ctx.params.get("nav") or []) if isinstance(item, dict)]
    if nav:
        nav_w = int(ctx.width * 0.17)
        ImageDraw.Draw(ctx.image).rectangle((0, int(ctx.height * 0.08), nav_w, ctx.height), fill=(*ctx.color("ink"), 255))
        cursor = int(ctx.height * 0.08) + 28
        label_x = max(28, ctx.safe()[0])
        for item in nav:
            label = str(item.get("label", ""))
            if item.get("active"):
                ImageDraw.Draw(ctx.image).rounded_rectangle(
                    (16, cursor - 8, nav_w - 12, cursor + 28),
                    radius=8,
                    fill=(*ctx.color("accent"), 255),
                )
            ctx.text(label, (label_x, cursor), _px(ctx, 0.022), ctx.color("paper"), bold=bool(item.get("active")))
            cursor += 44
        x = nav_w + 24
        width = ctx.safe()[2] - x
    aside = ctx.params.get("aside") if isinstance(ctx.params.get("aside"), dict) else None
    aside_w = int(ctx.width * 0.25) if aside else 0
    main_w = width - aside_w - (18 if aside_w else 0)
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (x - 12, content[1] - 8, ctx.safe()[2], bottom),
        radius=16,
        fill=(*ctx.color("paper"), 255),
    )
    title = ctx.params.get("title")
    if isinstance(title, str):
        title_size = max(_px(ctx, float(ctx.params.get("titleScale", 0.034))), ctx.contract.px("title"))
        ctx.text(title, (x, y), title_size, ctx.color("ink"), bold=True)
        y += title_size + ctx.contract.space("sm")
    subtitle = ctx.params.get("subtitle")
    if isinstance(subtitle, str):
        ctx.text(subtitle, (x, y), _px(ctx, 0.024), _muted(ctx))
        y += _px(ctx, 0.04)
    for field in ctx.params.get("fields") or []:
        if not isinstance(field, str):
            continue
        _field(ctx, (x, y, x + main_w, y + _px(ctx, 0.055)), field)
        y += _px(ctx, 0.068)
    table = ctx.params.get("table") if isinstance(ctx.params.get("table"), dict) else None
    if table:
        y = _dense_table(ctx, table, x, y, main_w, bottom - _px(ctx, 0.08))
        foot = ctx.params.get("foot")
        if isinstance(foot, str) and foot:
            y = _callout(ctx, foot, x, y + 16, main_w)
        activity = [item for item in (ctx.params.get("activity") or []) if isinstance(item, str) and item]
        if activity and bottom - y > ctx.contract.px("body"):
            gap = ctx.contract.space("xs")
            available = bottom - y
            row_h, kept = ctx.contract.row_height(len(activity), available - gap * max(0, len(activity) - 1), "body", cap=0.07)
            for item in activity[:kept]:
                _card(ctx, (x, y, x + main_w, y + row_h), fill="background")
                ctx.text(item, (x + 18, y + row_h / 2), ctx.contract.px("body"), ctx.color("ink"), bold=True, anchor="lm")
                y += row_h + gap
    legend_x = x
    for chip in ctx.params.get("legend") or []:
        if isinstance(chip, str):
            legend_x += _pill(ctx, chip, (legend_x, bottom - 10), "paper", "ink") + 12
    if aside:
        panel_x = ctx.safe()[2] - aside_w
        panel_y = content[1]
        cursor = panel_y + 18
        line_count = len([line for line in (aside.get("lines") or []) if isinstance(line, dict)])
        panel_bottom = panel_y + 36 + ctx.contract.px("title") + line_count * (ctx.contract.px("body") + ctx.contract.px("caption") + ctx.contract.space("sm")) + (88 if aside.get("action") else 28)
        _card(ctx, (panel_x, panel_y, panel_x + aside_w, min(bottom, panel_bottom)), fill="background")
        ctx.text(str(aside.get("title", "")), (panel_x + 16, cursor), _px(ctx, 0.026), ctx.color("ink"), bold=True)
        cursor += _px(ctx, 0.05)
        for line in aside.get("lines") or []:
            if not isinstance(line, dict):
                continue
            ctx.text(str(line.get("label", "")), (panel_x + 16, cursor), _px(ctx, 0.02), _muted(ctx))
            cursor += _px(ctx, 0.03)
            ctx.text(str(line.get("value", "")), (panel_x + 16, cursor), _px(ctx, 0.028), ctx.color("ink"), bold=True)
            cursor += _px(ctx, 0.055)
        action = aside.get("action")
        if isinstance(action, str) and action:
            _chip(ctx, action, (panel_x + aside_w / 2, min(bottom, panel_bottom) - ctx.contract.space("lg")), "accent", "paper")


def lanes(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    if _family(ctx) == "spine":
        _spine(ctx)
        return
    x, y, width, bottom = _chrome(ctx)
    title = ctx.params.get("title")
    if isinstance(title, str):
        ctx.text(title, (x, y), ctx.contract.px("title"), ctx.color("ink"), bold=True)
        y += ctx.contract.px("title") + ctx.contract.space("sm")
    marks = [str(item) for item in (ctx.params.get("marks") or [])]
    rail_w = int(width * 0.18)
    track_x = x + rail_w + ctx.contract.space("md")
    track_w = width - rail_w - ctx.contract.space("lg")
    if marks:
        step = track_w / max(1, len(marks) - 1)
        for index, mark in enumerate(marks):
            mark_x = track_x + index * step
            ctx.text(mark, (mark_x, y), ctx.contract.px("subtitle"), ctx.color("ink"), bold=True, anchor="mt")
            ImageDraw.Draw(ctx.image).line(
                (mark_x, y + ctx.contract.px("subtitle"), mark_x, y + ctx.contract.space("md")),
                fill=(*ctx.color("accent"), 255),
                width=3,
            )
        y += ctx.contract.space("md")
    items = [item for item in (ctx.params.get("lanes") or []) if isinstance(item, dict)]
    if not items:
        return
    gap = ctx.contract.space("sm")
    available = bottom - y - gap * max(0, len(items) - 1)
    lane_h, kept = ctx.contract.row_height(len(items), available, "title", cap=0.2)
    for lane in items[:kept]:
        _card(ctx, (x, y, x + width, y + lane_h))
        inset = ctx.contract.space("xs")
        ImageDraw.Draw(ctx.image).rounded_rectangle(
            (x + inset, y + inset, x + rail_w, y + lane_h - inset),
            radius=12,
            fill=(*ctx.color("accent"), 255),
        )
        ctx.text(str(lane.get("label", "")), (x + inset + 16, y + lane_h / 2), ctx.contract.px("title"), ctx.color("paper"), bold=True, anchor="lm")
        mid = y + lane_h / 2
        ImageDraw.Draw(ctx.image).rounded_rectangle(
            (track_x, mid - 6, track_x + track_w, mid + 6),
            radius=6,
            fill=(*ctx.color("background"), 255),
        )
        for event in lane.get("events") or []:
            if not isinstance(event, dict):
                continue
            at = min(0.88, max(0.12, float(event.get("at", 0))))
            travel = ease_out(progress(ctx.t, ctx.duration * 0.08, ctx.duration * 0.42))
            shown = lerp(0.08, at, travel)
            _event_block(ctx, str(event.get("text", "")), (track_x + shown * track_w, mid), track_x, track_x + track_w)
        y += lane_h + gap


def catalog(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    if _family(ctx) == "mass-contrast":
        _mass(ctx, assets, repo_root, project_dir)
        return
    x, y, width, bottom = _chrome(ctx)
    title = ctx.params.get("title")
    if isinstance(title, str):
        ctx.text(title, (x, y), ctx.contract.px("title"), ctx.color("ink"), bold=True)
        y += ctx.contract.px("title") + ctx.contract.space("sm")
    tiles = [item for item in (ctx.params.get("tiles") or []) if isinstance(item, dict)]
    summary_h = _px(ctx, 0.09)
    columns = 3
    rows = max(1, (len(tiles) + columns - 1) // columns)
    gap = 14
    tile_w = int((width - gap * (columns - 1)) / columns)
    tile_h = int((bottom - y - summary_h - 12 - gap * (rows - 1)) / rows)
    for index, tile in enumerate(tiles):
        column = index % columns
        row = index // columns
        left = x + column * (tile_w + gap)
        top = y + row * (tile_h + gap)
        _card(ctx, (left, top, left + tile_w, top + tile_h))
        tone = ctx.color("accent" if tile.get("tone") == "accent" else "accentAlt")
        band_h = int(tile_h * 0.46)
        ImageDraw.Draw(ctx.image).rounded_rectangle((left, top, left + tile_w, top + band_h), radius=16, fill=(*tone, 255))
        ImageDraw.Draw(ctx.image).rectangle((left, top + band_h - 16, left + tile_w, top + band_h), fill=(*tone, 255))
        ctx.text(f"{index + 1:02d}", (left + tile_w / 2, top + band_h * 0.42), ctx.contract.px("display"), ctx.color("paper"), bold=True, anchor="mm")
        ctx.text(str(tile.get("name", "")), (left + tile_w / 2, top + band_h + ctx.contract.space("sm")), ctx.contract.px("title"), ctx.color("ink"), bold=True, anchor="mt")
        ctx.text(str(tile.get("state", "")), (left + tile_w / 2, top + tile_h - ctx.contract.px("body") - 16), ctx.contract.px("body"), tone, bold=True, anchor="mt")
    summary = ctx.params.get("summary")
    if isinstance(summary, str):
        _callout(ctx, summary, x, bottom - summary_h, width)


def dashboard(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    if _family(ctx) == "hub":
        _hub(ctx)
        return
    x, y, width, bottom = _chrome(ctx)
    title = ctx.params.get("title")
    if isinstance(title, str):
        ctx.text(title, (x, y), ctx.contract.px("title"), ctx.color("ink"), bold=True)
        y += ctx.contract.px("title") + ctx.contract.space("xs")
    subtitle = ctx.params.get("subtitle")
    if isinstance(subtitle, str):
        ctx.text(subtitle, (x, y), _px(ctx, 0.022), _muted(ctx))
        y += _px(ctx, 0.04)
    metrics = [item for item in (ctx.params.get("metrics") or []) if isinstance(item, dict)]
    metric_h = _px(ctx, 0.2)
    if metrics:
        gap = 14
        metric_w = int((width - gap * (len(metrics) - 1)) / len(metrics))
        for index, metric in enumerate(metrics):
            left = x + index * (metric_w + gap)
            _card(ctx, (left, y, left + metric_w, y + metric_h))
            ImageDraw.Draw(ctx.image).rectangle((left, y + 12, left + 6, y + metric_h - 12), fill=(*ctx.color("accent"), 255))
            ctx.text(str(metric.get("label", "")), (left + 20, y + 14), ctx.contract.px("caption"), _muted(ctx))
            ctx.text(str(metric.get("value", "")), (left + 20, y + ctx.contract.px("caption") + 22), ctx.contract.px("display"), ctx.color("ink"), bold=True)
            ctx.text(str(metric.get("note", "")), (left + 20, y + metric_h - 28), ctx.contract.px("caption"), ctx.color("accent"), bold=True)
        y += metric_h + 16
    callout_h = _px(ctx, 0.08) if ctx.params.get("callout") else 0
    panel_bottom = bottom - callout_h - 8
    chart = ctx.params.get("chart") if isinstance(ctx.params.get("chart"), dict) else None
    chart_w = int(width * 0.58)
    if chart:
        _card(ctx, (x, y, x + chart_w, panel_bottom))
        ctx.text(str(chart.get("title", "")), (x + 16, y + 12), _px(ctx, 0.022), ctx.color("ink"), bold=True)
        bars = [item for item in (chart.get("bars") or []) if isinstance(item, dict)]
        if bars:
            gap = 18
            inner_w = chart_w - 32
            bar_w = int((inner_w - gap * (len(bars) - 1)) / len(bars))
            base = panel_bottom - 36
            usable = base - (y + 48)
            for index, bar in enumerate(bars):
                amount = min(1.0, max(0.08, float(bar.get("amount", 0.4))))
                grown = lerp(0.2, 1.0, ease_out(progress(ctx.t, ctx.duration * 0.1, ctx.duration * 0.4)))
                bar_h = int(usable * amount * grown)
                left = x + 16 + index * (bar_w + gap)
                ImageDraw.Draw(ctx.image).rounded_rectangle(
                    (left, base - bar_h, left + bar_w, base),
                    radius=6,
                    fill=(*ctx.color("accent"), 255),
                )
                ctx.text(str(bar.get("label", "")), (left + bar_w / 2, base + 8), _px(ctx, 0.018), _muted(ctx), anchor="mt")
    notes = [item for item in (ctx.params.get("notes") or []) if isinstance(item, dict)]
    if notes:
        note_x = x + chart_w + 16
        _card(ctx, (note_x, y, x + width, panel_bottom))
        cursor = y + 16
        for note in notes:
            ctx.text(str(note.get("label", "")), (note_x + 16, cursor), _px(ctx, 0.022), ctx.color("ink"), bold=True)
            cursor += _px(ctx, 0.035)
            ctx.text(str(note.get("body", "")), (note_x + 16, cursor), _px(ctx, 0.02), _muted(ctx))
            cursor += _px(ctx, 0.06)
    callout = ctx.params.get("callout")
    if isinstance(callout, str):
        _callout(ctx, callout, x, bottom - callout_h, width)


def triptych(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    del assets, repo_root, project_dir
    if _family(ctx) == "sequence":
        _sequence(ctx)
        return
    ImageDraw.Draw(ctx.image).rectangle((0, 0, ctx.width, ctx.height), fill=(*ctx.color("ink"), 255))
    columns = [item for item in (ctx.params.get("columns") or []) if isinstance(item, dict)]
    if not columns:
        return
    col_w = ctx.width / len(columns)
    draw = ImageDraw.Draw(ctx.image)
    for index, column in enumerate(columns):
        left = int(index * col_w)
        if index:
            draw.line((left, int(ctx.height * 0.12), left, int(ctx.height * 0.82)), fill=(*ctx.color("accent"), 200), width=2)
        cx = left + col_w / 2
        shown = 1.0 if index == 0 else _beat(ctx, 0.08 + (index - 1) * 0.14, 0.28 + (index - 1) * 0.14)
        if shown >= 0.08:
            drop = int((1 - shown) * ctx.contract.space("md"))
            ctx.text(str(column.get("index", "")), (cx, ctx.height * 0.14 + drop), ctx.contract.px("subtitle"), ctx.color("accent"), bold=True, anchor="mt")
            ctx.text(str(column.get("title", "")), (cx, ctx.height * 0.22 + drop), ctx.contract.px("display"), ctx.color("paper"), bold=True, anchor="mt")
            ctx.text(str(column.get("body", "")), (cx, ctx.height * 0.36 + drop), ctx.contract.px("body"), ctx.color("paper"), anchor="mt")
            points = [str(item) for item in (column.get("points") or []) if isinstance(item, str) and item]
            if points:
                top = 0.48
                step = 0.28 / len(points)
                for point_index, point in enumerate(points):
                    ctx.text(point, (cx, ctx.height * (top + point_index * step) + drop), ctx.contract.px("subtitle"), ctx.color("paper"), bold=True, anchor="mt")
    note = ctx.params.get("note")
    if isinstance(note, str):
        ctx.text(note, (ctx.width / 2, ctx.height * 0.88), _px(ctx, 0.024), ctx.color("paper"), anchor="mt")


def _chrome(ctx: FrameContext) -> tuple[int, int, int, int]:
    paint_background(ctx)
    bar_h = int(ctx.height * 0.08)
    ImageDraw.Draw(ctx.image).rectangle((0, 0, ctx.width, bar_h), fill=(*ctx.color("ink"), 255))
    top = place_notices(ctx, bar_h + ctx.contract.space("xs"))
    return ctx.safe()[0], top, ctx.safe()[2] - ctx.safe()[0], ctx.safe()[3]


def _field(ctx: FrameContext, box: tuple[int, int, int, int], label: str) -> None:
    ImageDraw.Draw(ctx.image).rounded_rectangle(box, radius=10, fill=(*ctx.color("paper"), 255), outline=(*ctx.color("ink"), 255), width=2)
    ctx.text(label, (box[0] + 16, (box[1] + box[3]) / 2), _px(ctx, 0.022), _muted(ctx), anchor="lm")


def _dense_table(ctx: FrameContext, block: dict, x: int, y: int, width: int, bottom: int) -> int:
    columns = [str(item) for item in block.get("columns") or []]
    rows = [row for row in (block.get("rows") or []) if isinstance(row, list)]
    if not columns or not rows:
        return y
    col_w = width / len(columns)
    header_h = ctx.contract.px("body") + ctx.contract.space("sm")
    row_h, kept = ctx.contract.row_height(len(rows), bottom - y - header_h, "body")
    rows = rows[:kept]
    emphasis = {str(item) for item in (ctx.params.get("emphasis") or [])}
    ImageDraw.Draw(ctx.image).rounded_rectangle((x, y, x + width, y + header_h), radius=8, fill=(*ctx.color("ink"), 255))
    for index, column in enumerate(columns):
        ctx.text(column, (x + int(index * col_w) + 8, y + header_h / 2), ctx.contract.px("caption"), ctx.color("paper"), bold=True, anchor="lm")
    y += header_h
    for row_index, row in enumerate(rows):
        if row_index % 2 == 0:
            ImageDraw.Draw(ctx.image).rectangle((x, y, x + width, y + row_h), fill=(*ctx.color("background"), 255))
        for index, cell in enumerate(row):
            shown = str(cell)
            emphasized = shown in emphasis
            ctx.text(
                shown,
                (x + int(index * col_w) + 8, y + row_h / 2),
                ctx.contract.px("body"),
                ctx.color("accent") if emphasized else ctx.color("ink"),
                bold=index == 0 or emphasized,
                anchor="lm",
            )
        y += row_h
    return y


def _plan(ctx: FrameContext) -> dict:
    value = ctx.cache.get("scene_plan")
    return value if isinstance(value, dict) else {}


def _family(ctx: FrameContext) -> str:
    return str(_plan(ctx).get("family") or "")


def _amount(ctx: FrameContext) -> float:
    motion = _plan(ctx).get("motion")
    if not isinstance(motion, dict):
        return 1.0
    return float(motion.get("amount") or 0)


def _tone(ctx: FrameContext, name: str, fallback: str = "ink") -> tuple[int, int, int]:
    if name in ctx.palette:
        return ctx.color(name)
    return ctx.color(fallback)


def _stage(ctx: FrameContext, motif: bool = True) -> None:
    ground = str(_plan(ctx).get("ground") or "warm-paper")
    if ground == "ink-field":
        ImageDraw.Draw(ctx.image).rectangle((0, 0, ctx.width, ctx.height), fill=(*ctx.color("ink"), 255))
        if motif:
            _motif(ctx)
        return
    if ground == "photographic":
        return
    paint_background(ctx)
    profile = _plan(ctx).get("profile") if isinstance(_plan(ctx).get("profile"), dict) else {}
    if profile.get("depth") == "layered":
        _wash(ctx)
    if motif:
        _motif(ctx)


def _wash(ctx: FrameContext) -> None:
    color = _tone(ctx, "weight")
    layer = Image.new("RGBA", (ctx.width, ctx.height), (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse(
        (int(ctx.width * 0.68), int(ctx.height * 0.52), int(ctx.width * 1.12), int(ctx.height * 1.05)),
        fill=(*color, 28),
    )
    ctx.image.alpha_composite(layer)


def _motif(ctx: FrameContext) -> None:
    left, top, _right, bottom = ctx.safe()
    ImageDraw.Draw(ctx.image).rectangle(
        (left, top, left + max(4, int(ctx.width * 0.004)), bottom),
        fill=(*ctx.color("accent"), 255),
    )


def _notice_labels(params: dict) -> list[str]:
    """Disclosure and the demo label, once each. An empty string is not a notice."""
    found: list[str] = []
    for key in ("disclosure", "dataLabel"):
        label = params.get(key) if isinstance(params, dict) else None
        if not isinstance(label, str):
            continue
        label = label.strip()
        if label and label not in found:
            found.append(label)
    return found


def place_notices(ctx: FrameContext, y: int | None = None) -> int:
    """Draw each notice once, inside the safe area, without sharing a baseline.

    A second call on the same frame does not paint again. The fill is paper on
    ink, never the warning color that used to stack both sentences.
    """
    if getattr(ctx, "_notices_placed", False):
        return int(ctx._notices_bottom)
    left, safe_top, right, _bottom = ctx.safe()
    row_top = safe_top if y is None else int(y)
    labels = _notice_labels(ctx.params)
    gap = ctx.contract.space("sm")
    cursor = left
    row_h = 0
    boxes: list[dict] = []
    for label in labels:
        size = ctx.contract.px("caption")
        font = ctx.font(size, True)
        measure = ImageDraw.Draw(ctx.image).textbbox((0, 0), label, font=font)
        width = measure[2] - measure[0] + 22
        height = measure[3] - measure[1] + 12
        if cursor > left and cursor + width > right:
            row_top += row_h + gap
            cursor = left
            row_h = 0
        _tag(ctx, label, (cursor, row_top), "ink", "paper")
        boxes.append({"sceneId": ctx.scene.get("sceneId"), "text": label, "box": [cursor, row_top, cursor + width, row_top + height]})
        cursor += width + gap
        row_h = max(row_h, height)
    ctx._notices_placed = True
    ctx._notice_boxes = boxes
    ctx._notices_bottom = row_top + row_h + ctx.contract.space("md") if labels else (safe_top if y is None else int(y))
    ctx.cache.setdefault("notices", []).extend(boxes)
    return ctx._notices_bottom


def _tags(ctx: FrameContext) -> int:
    return place_notices(ctx)


def _tag(ctx: FrameContext, text: str, origin: tuple[float, float], fill: str, ink: str) -> int:
    size = ctx.contract.px("caption")
    font = ctx.font(size, True)
    box = ImageDraw.Draw(ctx.image).textbbox((0, 0), text, font=font)
    width = box[2] - box[0] + 22
    height = box[3] - box[1] + 12
    left = int(origin[0])
    top = int(origin[1])
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (left, top, left + width, top + height),
        radius=height // 2,
        fill=(*ctx.color(fill), 255),
    )
    ctx.text(text, (left + 11, top + height / 2), size, ctx.color(ink), bold=True, anchor="lm")
    return left + width


def _sheet(ctx: FrameContext, box: tuple[int, int, int, int], fill: str = "paper") -> None:
    x0, y0, x1, y1 = (int(box[0]), int(box[1]), int(box[2]), int(box[3]))
    if x1 - x0 < 8 or y1 - y0 < 8:
        return
    shade = _tone(ctx, "weight")
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (x0 + 8, y0 + 10, x1 + 8, y1 + 10),
        radius=16,
        fill=(*shade, 40),
    )
    _card(ctx, (x0, y0, x1, y1), fill=fill)


def _write(ctx: FrameContext, text: object, x: float, y: float, size: int, color: tuple[int, int, int], width: int, bold: bool = False) -> int:
    shown = str(text or "")
    if not shown or width <= 0:
        return int(y)
    parts = _wrap(ctx, shown, size, bold, width)
    cursor = int(y)
    for part in parts:
        ctx.text(part, (x, cursor), size, color, bold=bold)
        cursor += int(size * 1.28)
    if len(parts) > 1:
        ctx.texts.add(shown)
    return cursor


def _stagger(ctx: FrameContext, index: int) -> float:
    if index <= 0:
        return 1.0
    motion = _plan(ctx).get("motion") if isinstance(_plan(ctx).get("motion"), dict) else {}
    start = float(motion.get("start") or 0.06) + index * 0.08
    end = float(motion.get("end") or 0.3) + index * 0.08
    return ease_out(progress(ctx.t, ctx.duration * start, ctx.duration * min(0.92, end)))


def _photographic(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    choice = select_scene_asset(ctx.scene, assets, project_dir, repo_root)
    bindings = ctx.cache.setdefault("assetBindings", [])
    if not any(row.get("sceneId") == choice["sceneId"] for row in bindings):
        bindings.append({**choice, "fit": "cover"})
    if choice["status"] not in {"ready", "unverified"} or not choice.get("path"):
        raise ProductionError("BLOCKED", [issue(choice.get("code") or "asset_missing", ctx.scene["sceneId"], choice.get("reason") or "Không có ảnh nền hợp lệ.")])
    pointed = ctx.params.get("assetId")
    if isinstance(pointed, str) and pointed and pointed != choice["assetId"]:
        raise ProductionError("BLOCKED", [issue("asset_role", ctx.scene["sceneId"], "assetId không trùng asset đúng vai trò. Không đổi sang ảnh khác.")])
    source = Image.open(choice["path"]).convert("RGBA")
    source.load()
    move = CameraMove.parse(ctx.params.get("preset", "hold"), ctx.params.get("amount", 0))
    plate = render_plate(source, (ctx.width, ctx.height), move, ctx.t, ctx.duration, ctx.color("background"))
    ctx.image.alpha_composite(_cover(plate, ctx.width, ctx.height))
    ctx.cache["pictureBoxes"] = [(0, 0, ctx.width, ctx.height)]
    ctx.note_asset(str(choice["assetId"]), None)
    title = ctx.params.get("title")
    if not isinstance(title, str) or not title:
        return
    left, _top, right, bottom = ctx.safe()
    pad = ctx.contract.space("md")
    text_w = int((right - left) * 0.62)
    size = ctx.contract.px("title")
    block = max(size, _line_block_height(ctx, [{"text": title, "bold": True, "scale": 0.048}], text_w))
    card_h = block + pad
    card_w = text_w + pad * 2
    card_top = bottom - card_h
    _sheet(ctx, (left, card_top, left + card_w, bottom))
    _write(ctx, title, left + pad, card_top + pad // 2, size, ctx.color("ink"), text_w, bold=True)


def _portrait_object(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    _stage(ctx)
    source = _asset_image(ctx, assets, repo_root, project_dir, "assetId")
    left, top, right, bottom = ctx.safe()
    panel_w = int((right - left) * 0.42)
    gap = ctx.contract.space("md")
    frame = (right - panel_w, top + gap, right, bottom - gap)
    _sheet(ctx, (frame[0] - 8, frame[1] - 8, frame[2] + 8, frame[3] + 8))
    panel = _portrait_panel(ctx, source, panel_w)
    panel_h = frame[3] - frame[1]
    crop_top = max(0, (panel.height - panel_h) // 2)
    ctx.image.alpha_composite(panel.crop((0, crop_top, panel_w, crop_top + panel_h)), (frame[0], frame[1]))
    ctx.cache["pictureBoxes"] = [frame]
    ctx.note_asset(str(ctx.params.get("assetId")), None)
    text_w = frame[0] - left - gap * 2
    lines = ctx.params.get("lines") or []
    block = _line_block_height(ctx, lines, text_w)
    y = ctx.contract.place_block(top, bottom, block)
    _stack(ctx, lines, left + gap, y, text_w)


def _mark_field(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    _stage(ctx)
    logo = _logo(ctx, assets, repo_root, project_dir)
    target = int(ctx.height * float(ctx.params.get("logoScale", 0.42)))
    settle = lerp(0.96, 1.0, _amount(ctx))
    fitted = _fit(logo, int(target * settle), int(target * settle))
    ctx.blit(fitted, ((ctx.width - fitted.width) / 2, ctx.height * 0.16))
    _center_line(ctx, ctx.params.get("line"), 0.7, 0.04, "ink")
    _center_line(ctx, ctx.params.get("url"), 0.8, 0.046, "accent", bold=True)


def _anchored_line(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    _stage(ctx)
    left, top, right, bottom = ctx.safe()
    logo = _logo(ctx, assets, repo_root, project_dir)
    plate = int(ctx.height * 0.28)
    fitted = _fit(logo, plate, plate)
    _sheet(ctx, (left, top, left + fitted.width + 48, top + fitted.height + 48))
    ctx.blit(fitted, (left + 24, top + 24))
    lines = ctx.params.get("lines") or []
    text_w = int((right - left) * 0.86)
    plate_bottom = top + fitted.height + 56
    shift = int((1 - _amount(ctx)) * ctx.contract.space("sm"))
    _stack(ctx, lines, left, plate_bottom + ctx.contract.space("md") + shift, text_w)


def _lockup(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    _stage(ctx)
    logo = _logo(ctx, assets, repo_root, project_dir)
    target = int(ctx.height * 0.36)
    fitted = _fit(logo, target, target)
    ctx.blit(fitted, ((ctx.width - fitted.width) / 2, ctx.height * 0.12))
    if _beat(ctx, 0.08, 0.22) > 0.02:
        _center_line(ctx, ctx.params.get("line"), 0.62, 0.038, "ink")
    url = ctx.params.get("url")
    if isinstance(url, str) and url and _beat(ctx, 0.16, 0.32) > 0.02:
        _chip(ctx, url, (ctx.width / 2, ctx.height * 0.76), "accent", "paper")


def _spine(ctx: FrameContext) -> None:
    _stage(ctx)
    top = _tags(ctx)
    left, _tag_top, right, bottom = ctx.safe()
    split = left + int((right - left) * 0.4)
    title = ctx.params.get("title")
    if isinstance(title, str) and title:
        block = _line_block_height(ctx, [{"text": title, "bold": True, "scale": 0.062}], split - left - 24)
        y = ctx.contract.place_block(top, bottom, block)
        _write(ctx, title, left + 16, y, ctx.contract.px("display"), ctx.color("ink"), split - left - 28, bold=True)
    marks = [str(item) for item in (ctx.params.get("marks") or []) if isinstance(item, str)]
    if marks and isinstance(title, str) and title:
        _write(ctx, "  ·  ".join(marks), left + 16, bottom - ctx.contract.px("caption") - 4, ctx.contract.px("caption"), ctx.color("accent"), split - left - 28, bold=True)
    spine_x = split + 28
    ImageDraw.Draw(ctx.image).rectangle((spine_x, top, spine_x + 8, bottom), fill=(*ctx.color("ink"), 255))
    events = []
    for lane in ctx.params.get("lanes") or []:
        if not isinstance(lane, dict):
            continue
        for event in lane.get("events") or []:
            if isinstance(event, dict):
                events.append((str(lane.get("label") or ""), event))
    events.sort(key=lambda item: float(item[1].get("at", 0)))
    if not events:
        return
    slot = (bottom - top) / len(events)
    travel = _amount(ctx)
    chip_x = spine_x + int(ctx.width * 0.08)
    chip_w = right - chip_x
    for index, (label, event) in enumerate(events):
        rest = top + slot * index + max(0, (slot - ctx.contract.px("title")) // 2)
        indent = ctx.contract.space("lg") if index % 2 else 0
        nudge = int((1 - (1.0 if index == 0 else travel)) * ctx.contract.space("sm"))
        text = str(event.get("text") or "")
        ctx.text(label, (chip_x + indent + nudge, rest), ctx.contract.px("caption"), _muted(ctx))
        ctx.text(text, (chip_x + indent + nudge, rest + ctx.contract.px("caption")), ctx.contract.px("title"), ctx.color("ink"), bold=True)
        if chip_w > 0:
            ctx.texts.add(text)


def _cleared_products(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> list[tuple[dict, Image.Image]]:
    found = []
    for asset_id in referenced_ids(ctx.scene):
        row = assets.get(asset_id) if isinstance(assets, dict) else None
        if not isinstance(row, dict) or str(row.get("role") or "") != "product":
            continue
        if not inspect_asset(row)["allowed"]:
            continue
        path = resolve_path(row, project_dir, repo_root)
        if path is None or not path.is_file() or confirm_file(row, path)["match"] is False:
            continue
        image = Image.open(path).convert("RGBA")
        image.load()
        found.append((row, image))
    return found


def _place_image(ctx: FrameContext, image: Image.Image, box: tuple[int, int, int, int]) -> None:
    x0, y0, x1, y1 = (int(value) for value in box)
    width = max(1, x1 - x0)
    height = max(1, y1 - y0)
    scale = min(width / image.width, height / image.height)
    resized = image.resize((max(1, int(image.width * scale)), max(1, int(image.height * scale))), Image.Resampling.LANCZOS)
    left = x0 + (width - resized.width) // 2
    top = y0 + (height - resized.height) // 2
    ctx.image.alpha_composite(resized, (left, top))


def _scene_plates(ctx: FrameContext, plates: list[tuple[dict, Image.Image]]) -> None:
    """A full-scene photograph leads. Type supports it and does not replace it."""
    _stage(ctx)
    left, top, right, bottom = ctx.safe()
    inset = ctx.contract.space("sm")
    cursor = top
    title = ctx.params.get("title")
    if isinstance(title, str) and title:
        cursor = _write(ctx, title, left + inset, top, ctx.contract.px("title"), ctx.color("ink"), right - left - inset * 2, bold=True)
        cursor += ctx.contract.space("sm")
    summary = ctx.params.get("summary") if isinstance(ctx.params.get("summary"), str) else ""
    summary_h = ctx.contract.px("body") * 2 + ctx.contract.space("sm") if summary else 0
    states: list[str] = []
    for tile in ctx.params.get("tiles") or []:
        if isinstance(tile, dict):
            state = str(tile.get("state") or "")
            if state and state not in states:
                states.append(state)
    caption_h = ctx.contract.px("title") + ctx.contract.space("xs") if states else 0
    well_bottom = bottom - summary_h - caption_h - (ctx.contract.space("sm") if summary or states else 0)
    gap = ctx.contract.space("md")
    count = max(1, len(plates))
    slot_w = max(1, (right - left - gap * (count - 1)) // count)
    boxes = []
    for index, (row, image) in enumerate(plates):
        x0 = left + index * (slot_w + gap)
        box = (x0, cursor, x0 + slot_w, well_bottom)
        _place_image(ctx, image, box)
        boxes.append(box)
        ctx.note_asset(str(row.get("assetId") or ""), None)
    ctx.cache["pictureBoxes"] = boxes
    if states:
        cap_top = well_bottom + ctx.contract.space("xs")
        if len(states) == count:
            for index, state in enumerate(states):
                x0 = left + index * (slot_w + gap)
                _write(ctx, state, x0 + inset, cap_top, ctx.contract.px("title"), ctx.color("ink"), slot_w - inset * 2, bold=True)
        else:
            column_w = max(1, (right - left - gap * (len(states) - 1)) // len(states))
            for index, state in enumerate(states):
                x0 = left + index * (column_w + gap)
                _write(ctx, state, x0 + inset, cap_top, ctx.contract.px("title"), ctx.color("ink"), column_w - inset * 2, bold=True)
    if summary:
        _write(ctx, summary, left + inset, bottom - summary_h + ctx.contract.space("xs"), ctx.contract.px("body"), ctx.color("ink"), right - left - inset * 2)


def _mass(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    pictures = _cleared_products(ctx, assets, repo_root, project_dir)
    scene_plates = [item for item in pictures if str(item[0].get("visualRole") or "") == "scene"]
    if scene_plates:
        _scene_plates(ctx, scene_plates)
        return
    if not pictures:
        raise ProductionError("BLOCKED", [issue(
            "asset_required",
            ctx.scene["sceneId"],
            "Cảnh hàng hóa chưa có ảnh sản phẩm đã duyệt. Không vẽ thẻ chữ thay cho hàng.",
        )])
    _stage(ctx)
    top = _tags(ctx)
    left, _tag_top, right, bottom = ctx.safe()
    title = ctx.params.get("title")
    if isinstance(title, str) and title:
        top = _write(ctx, title, left + 16, top, ctx.contract.px("title"), ctx.color("ink"), right - left - 32, bold=True)
        top += ctx.contract.space("sm")
    tiles = [item for item in (ctx.params.get("tiles") or []) if isinstance(item, dict)]
    fast = [tile for tile in tiles if tile.get("tone") == "accent"]
    still = [tile for tile in tiles if tile.get("tone") != "accent"]
    if not fast:
        fast = tiles[:1]
        still = tiles[1:]
    summary = ctx.params.get("summary")
    summary_h = ctx.contract.px("body") * 2 + ctx.contract.space("md") if isinstance(summary, str) and summary else 0
    gap = ctx.contract.space("sm")
    mass_w = int((right - left) * 0.58)
    mass_box = (left + 8, top, left + mass_w, bottom - summary_h)
    _sheet(ctx, mass_box)
    state = str((still[0].get("state") if still else "") or "")
    band_h = ctx.contract.px("display") + ctx.contract.space("lg")
    ImageDraw.Draw(ctx.image).rounded_rectangle(
        (mass_box[0], mass_box[1], mass_box[2], mass_box[1] + band_h),
        radius=16,
        fill=(*_tone(ctx, "accentAlt", "accent"), 255),
    )
    ImageDraw.Draw(ctx.image).rectangle(
        (mass_box[0], mass_box[1] + band_h - 16, mass_box[2], mass_box[1] + band_h),
        fill=(*_tone(ctx, "accentAlt", "accent"), 255),
    )
    cursor = mass_box[1] + ctx.contract.space("sm")
    if state:
        ctx.text(state, (mass_box[0] + 24, cursor), ctx.contract.px("display"), ctx.color("paper"), bold=True)
        cursor = mass_box[1] + band_h + ctx.contract.space("sm")
    pictures = _cleared_products(ctx, assets, repo_root, project_dir)
    if any(row.get("visualRole") for row, _image in pictures):
        still_pics = [(row, image) for row, image in pictures if str(row.get("visualRole") or "") != "fast"]
        fast_pics = [(row, image) for row, image in pictures if str(row.get("visualRole") or "") == "fast"]
    else:
        still_pics = pictures[:1]
        fast_pics = pictures[1:]
    if still_pics:
        slot_w = max(1, (mass_box[2] - mass_box[0] - 32) // len(still_pics))
        for index, (row, image) in enumerate(still_pics):
            slot = (mass_box[0] + 16 + index * slot_w, cursor, mass_box[0] + 16 + (index + 1) * slot_w - 8, mass_box[3] - 8)
            _place_image(ctx, image, (slot[0], slot[1], slot[2], slot[3] - ctx.contract.px("caption") - 8))
            _write(ctx, row.get("label"), slot[0], slot[3] - ctx.contract.px("caption"), ctx.contract.px("caption"), ctx.color("ink"), slot_w - 16)
    else:
        for tile in still:
            cursor = _write(ctx, tile.get("name"), mass_box[0] + 24, cursor, ctx.contract.px("title"), ctx.color("ink"), mass_w - 48, bold=True)
            cursor += 4
    fast_x = mass_box[2] + gap
    fast_w = right - fast_x
    fast_count = max(len(fast), len(fast_pics), 1)
    fast_h = int((mass_box[3] - mass_box[1] - gap * (fast_count - 1)) / fast_count)
    rise = int((1 - _amount(ctx)) * ctx.contract.space("lg"))
    for index in range(max(len(fast), len(fast_pics))):
        box_top = mass_box[1] + index * (fast_h + gap) + rise
        box = (fast_x, box_top, right, box_top + fast_h)
        _sheet(ctx, box, fill="paper")
        ImageDraw.Draw(ctx.image).rectangle(
            (fast_x, box_top, fast_x + 8, box_top + fast_h),
            fill=(*ctx.color("accent"), 255),
        )
        tile = fast[index] if index < len(fast) else {}
        if index < len(fast_pics):
            _place_image(ctx, fast_pics[index][1], (fast_x + 16, box_top + 12, right - 12, box_top + fast_h - ctx.contract.px("caption") - 16))
            _write(ctx, tile.get("name") or fast_pics[index][0].get("label"), fast_x + 16, box_top + fast_h - ctx.contract.px("caption") - 8, ctx.contract.px("caption"), ctx.color("ink"), fast_w - 32)
        else:
            ctx.text(str(tile.get("state") or ""), (fast_x + 20, box_top + 16), ctx.contract.px("caption"), ctx.color("accent"), bold=True)
            ctx.text(str(tile.get("name") or ""), (fast_x + 20, box_top + ctx.contract.px("caption") + 24), ctx.contract.px("title"), ctx.color("ink"), bold=True)
    if isinstance(summary, str) and summary:
        _write(ctx, summary, left + 16, bottom - summary_h + 8, ctx.contract.px("body"), ctx.color("ink"), right - left - 32, bold=True)


def _row_text(row: list) -> str:
    return "  ·  ".join(str(cell) for cell in row)


def _foreground(ctx: FrameContext) -> None:
    _stage(ctx)
    top = _tags(ctx)
    left, _tag_top, right, bottom = ctx.safe()
    title = ctx.params.get("title")
    if isinstance(title, str) and title:
        top = _write(ctx, title, left + 16, top, ctx.contract.px("title"), ctx.color("ink"), int((right - left) * 0.7), bold=True)
        top += ctx.contract.space("xs")
    emphasis = {str(item) for item in (ctx.params.get("emphasis") or [])}
    table = ctx.params.get("table") if isinstance(ctx.params.get("table"), dict) else {}
    rows = [row for row in (table.get("rows") or []) if isinstance(row, list)]
    hero = [row for row in rows if any(str(cell) in emphasis for cell in row)] or rows[:1]
    quiet = [row for row in rows if row not in hero]
    aside = ctx.params.get("aside") if isinstance(ctx.params.get("aside"), dict) else None
    aside_w = int((right - left) * 0.28) if aside else 0
    card_w = right - left - aside_w - (ctx.contract.space("sm") if aside_w else 0)
    card_h = ctx.contract.px("display") + ctx.contract.px("title")
    for index, row in enumerate(hero):
        y = top + index * (card_h + ctx.contract.space("xs"))
        shift = int((1 - _amount(ctx)) * ctx.contract.space("md"))
        _sheet(ctx, (left, y + shift, left + card_w, y + card_h + shift))
        ImageDraw.Draw(ctx.image).rectangle((left, y + shift, left + 10, y + card_h + shift), fill=(*ctx.color("accent"), 255))
        focus = next((str(cell) for cell in row if str(cell) in emphasis), str(row[-1]))
        ctx.text(focus, (left + 24, y + shift + 12), ctx.contract.px("title"), ctx.color("accent"), bold=True)
        detail = "  ·  ".join(str(cell) for cell in row if str(cell) != focus)
        ctx.text(detail, (left + 24, y + shift + ctx.contract.px("title") + 16), ctx.contract.px("body"), ctx.color("ink"))
    body_top = top + len(hero) * (card_h + ctx.contract.space("xs")) + ctx.contract.space("sm")
    activity_h = _activity_height(ctx)
    limit = bottom - activity_h
    if quiet:
        row_h = ctx.contract.px("body") + ctx.contract.space("xs")
        ledger_h = row_h * len(quiet) + 16
        _sheet(ctx, (left, body_top, left + card_w, min(limit, body_top + ledger_h)))
        cursor = body_top + 8
        for row in quiet:
            ctx.text(_row_text(row), (left + 20, cursor), ctx.contract.px("body"), ctx.color("ink"))
            cursor += row_h
    if aside:
        _aside_note(ctx, aside, (right - aside_w, top, right, top + int(ctx.height * 0.42)))
    _activity(ctx, left, bottom)


def _ticket(ctx: FrameContext, assets: dict, repo_root: Path, project_dir: Path) -> None:
    """A verified interface image leads. Code draws the label and required lines, not a second POS."""
    choice = select_scene_asset(ctx.scene, assets, project_dir, repo_root)
    asset_id = choice.get("assetId")
    row = assets.get(asset_id) if isinstance(asset_id, str) and isinstance(assets, dict) else None
    row = row if isinstance(row, dict) else {}
    classified = visual_source(row) if row else {"status": "unspecified", "source": "", "reason": ""}
    bindings = ctx.cache.setdefault("assetBindings", [])
    if not any(item.get("sceneId") == choice.get("sceneId") for item in bindings):
        bindings.append({
            "sceneId": choice.get("sceneId"),
            "status": choice.get("status"),
            "code": choice.get("code"),
            "assetId": choice.get("assetId"),
            "role": choice.get("role"),
            "path": choice.get("path"),
            "integrity": choice.get("integrity"),
            "visualSource": classified.get("source") or "",
            "fit": "contain" if choice.get("status") == "ready" else "",
        })
    status = choice.get("status")
    if status in {"blocked", "unverified"}:
        raise ProductionError(
            "BLOCKED",
            [issue(choice.get("code") or "asset_missing", ctx.scene["sceneId"], choice.get("reason") or "Ảnh giao diện chưa được xác minh.")],
        )
    if classified.get("status") == "invalid":
        raise ProductionError("BLOCKED", [issue("visual_source", ctx.scene["sceneId"], classified.get("reason") or "Nguồn hình không hợp lệ.")])
    source_name = str(classified.get("source") or "")
    if status == "ready" and choice.get("path"):
        if source_name == "AI_VISUAL":
            raise ProductionError("BLOCKED", [issue("visual_source", ctx.scene["sceneId"], "AI_VISUAL không được đặt vào khung giao diện.")])
        if source_name == "CODE_EXACT":
            _ticket_drawn(ctx)
            return
        kind = str(row.get("kind") or "")
        if source_name in {"", "SOFTWARE_SCREENSHOT", "AI_RECONSTRUCTED_UI"} and (kind == "screenshot" or source_name == "AI_RECONSTRUCTED_UI"):
            _place_verified_screen(ctx, choice, row, source_name or ("SOFTWARE_SCREENSHOT" if kind == "screenshot" else source_name))
            return
    if status == "not_required":
        _ticket_drawn(ctx)
        return
    raise ProductionError(
        "BLOCKED",
        [issue(choice.get("code") or "official_ui_missing", ctx.scene["sceneId"], choice.get("reason") or "Không có ảnh giao diện đã xác minh.")],
    )


def _mix(base: tuple[int, int, int], other: tuple[int, int, int], amount: float) -> tuple[int, int, int]:
    return tuple(int(base[index] + (other[index] - base[index]) * amount) for index in range(3))


def software_screen_layout(frame_w: int, frame_h: int, src_w: int, src_h: int, top_reserve: int = 0) -> dict:
    """Place the interface as large as the frame allows. Prefer the original pixel size."""
    min_side = max(24, int(frame_w * 0.03))
    min_bottom = max(28, int(frame_h * 0.045))
    min_top = max(56, int(frame_h * 0.075), int(top_reserve))
    top_cap = max(min_top, int(frame_h * 0.16))
    if src_w <= frame_w - min_side * 2 and src_h <= frame_h - min_top - min_bottom:
        spare = frame_h - src_h - min_bottom
        top = min(max(min_top, spare), top_cap)
        x = (frame_w - src_w) // 2
        room = frame_h - top - min_bottom - src_h
        y = top + max(0, room // 2)
        box = (x, y, x + src_w, y + src_h)
        return {"box": box, "native": True, "top": top}
    top = min_top
    well = (min_side, top, frame_w - min_side, frame_h - min_bottom)
    well_w = max(1, well[2] - well[0])
    well_h = max(1, well[3] - well[1])
    scale = min(well_w / src_w, well_h / src_h)
    width = max(1, int(round(src_w * scale)))
    height = max(1, int(round(src_h * scale)))
    x = well[0] + (well_w - width) // 2
    y = well[1] + (well_h - height) // 2
    return {"box": (x, y, x + width, y + height), "native": False, "top": top}


def paint_software_ad(
    canvas: Image.Image,
    source: Image.Image,
    ink: tuple[int, int, int],
    accent: tuple[int, int, int],
    *,
    paste: bool = True,
    top_reserve: int = 0,
) -> dict:
    """Cinematic ground plus the source image as its own layer. The source file is not rewritten."""
    width, height = canvas.size
    top = _mix(ink, (0, 0, 0), 0.5)
    mid = _mix(ink, accent, 0.12)
    bottom = _mix(ink, (0, 0, 0), 0.68)
    draw = ImageDraw.Draw(canvas)
    for y in range(height):
        amount = y / max(1, height - 1)
        if amount < 0.58:
            color = _mix(top, mid, amount / 0.58)
        else:
            color = _mix(mid, bottom, (amount - 0.58) / 0.42)
        draw.line((0, y, width, y), fill=(*color, 255))
    layout = software_screen_layout(width, height, source.width, source.height, top_reserve)
    box = layout["box"]
    glow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    center_x = (box[0] + box[2]) // 2
    center_y = (box[1] + box[3]) // 2
    radius_x = int((box[2] - box[0]) * 0.62)
    radius_y = int((box[3] - box[1]) * 0.48)
    ImageDraw.Draw(glow).ellipse(
        (center_x - radius_x, center_y - radius_y, center_x + radius_x, center_y + radius_y),
        fill=(*accent, 42),
    )
    canvas.alpha_composite(glow.filter(ImageFilter.GaussianBlur(48)))
    if paste:
        shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rectangle((box[0] - 6, box[1] + 12, box[2] + 6, box[3] + 26), fill=(0, 0, 0, 150))
        canvas.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(18)))
        plate = source if layout["native"] else source.resize((box[2] - box[0], box[3] - box[1]), Image.Resampling.LANCZOS)
        canvas.alpha_composite(plate.convert("RGBA"), (box[0], box[1]))
    return layout


def _place_verified_screen(ctx: FrameContext, choice: dict, row: dict, source_name: str) -> None:
    path = Path(str(choice.get("path") or ""))
    source = Image.open(path).convert("RGBA")
    source.load()
    label = row.get("demoLabel") if row.get("demoData") is True else None
    shown = label.strip() if isinstance(label, str) else ""
    reserve = 0
    if shown:
        reserve = ctx.safe()[1] + max(14, int(ctx.height * 0.02)) + 36
    layout = paint_software_ad(ctx.image, source, ctx.color("ink"), ctx.color("accent"), top_reserve=reserve)
    box = layout["box"]
    label_box = None
    if shown:
        size = max(14, int(ctx.height * 0.02))
        font = ctx.font(size, True)
        measure = font.getbbox(shown)
        text_w = measure[2] - measure[0]
        text_h = measure[3] - measure[1]
        origin = (box[2] - text_w, max(ctx.safe()[1], box[1] - text_h - 18))
        label_box = ctx.text(shown, origin, size, ctx.color("paper"), bold=True, anchor="lt")
        ctx.cache.setdefault("notices", []).append({
            "sceneId": ctx.scene.get("sceneId"),
            "text": shown,
            "box": list(label_box),
        })
    headline_right = (label_box[0] - 24) if label_box else box[2]
    headline = [box[0], max(ctx.safe()[1], box[1] - 56), max(box[0] + 8, headline_right), box[1] - 12]
    ctx.cache["deferOnScreenText"] = True
    ctx.cache["pictureBoxes"] = [box]
    ctx.cache.setdefault("pictureSources", []).append({
        "sceneId": ctx.scene.get("sceneId"),
        "assetId": choice.get("assetId"),
        "visualSource": source_name,
        "sha256": (choice.get("integrity") or {}).get("sha256"),
        "path": str(path),
        "width": source.width,
        "height": source.height,
        "box": [int(box[0]), int(box[1]), int(box[2]), int(box[3])],
        "placement": "native" if layout["native"] else "scaled",
        "headline": headline,
        "demoLabel": shown,
    })
    ctx.note_asset(str(choice.get("assetId") or ""), shown or None)


def _ticket_drawn(ctx: FrameContext) -> None:
    _stage(ctx)
    top = _tags(ctx)
    left, _tag_top, right, bottom = ctx.safe()
    aside = ctx.params.get("aside") if isinstance(ctx.params.get("aside"), dict) else None
    aside_w = int((right - left) * 0.3) if aside else 0
    activity_h = _activity_height(ctx)
    ticket = (left, top, right - aside_w - ctx.contract.space("md"), bottom - activity_h - ctx.contract.space("sm"))
    _sheet(ctx, ticket)
    draw = ImageDraw.Draw(ctx.image)
    for index in range(8):
        cx = ticket[0] + 28 + index * ((ticket[2] - ticket[0] - 40) / 7)
        draw.ellipse((cx, ticket[1] + 10, cx + 10, ticket[1] + 20), fill=(*ctx.color("background"), 255))
    cursor = ticket[1] + 36
    title = ctx.params.get("title")
    if isinstance(title, str):
        ctx.text(title, (ticket[0] + 24, cursor), ctx.contract.px("display"), ctx.color("ink"), bold=True)
        cursor += ctx.contract.px("display") + 4
    subtitle = ctx.params.get("subtitle")
    if isinstance(subtitle, str):
        ctx.text(subtitle, (ticket[0] + 24, cursor), ctx.contract.px("title"), _muted(ctx))
        cursor += ctx.contract.px("title") + ctx.contract.space("sm")
    for field in ctx.params.get("fields") or []:
        if isinstance(field, str):
            ctx.text(field, (ticket[0] + 24, cursor), ctx.contract.px("body"), ctx.color("ink"))
            cursor += ctx.contract.px("body") + 6
    table = ctx.params.get("table") if isinstance(ctx.params.get("table"), dict) else {}
    draw.line((ticket[0] + 20, cursor, ticket[2] - 20, cursor), fill=(*ctx.color("muted"), 255), width=2)
    cursor += 10
    for row in table.get("rows") or []:
        if isinstance(row, list):
            ctx.text(_row_text(row), (ticket[0] + 24, cursor), ctx.contract.px("body"), ctx.color("ink"), bold=True)
            cursor += ctx.contract.px("body") + 8
    foot = ctx.params.get("foot")
    if isinstance(foot, str) and foot:
        cursor += 6
        _write(ctx, foot, ticket[0] + 24, cursor, ctx.contract.px("body"), ctx.color("accent"), ticket[2] - ticket[0] - 48, bold=True)
    if aside:
        slip_top = top + int(ctx.height * 0.08) + int((1 - _amount(ctx)) * ctx.contract.space("lg"))
        _aside_note(ctx, aside, (ticket[2] + ctx.contract.space("sm"), slip_top, right, bottom - activity_h))
    _activity(ctx, left, bottom)


def _quantity(ctx: FrameContext) -> None:
    _stage(ctx)
    top = _tags(ctx)
    left, _tag_top, right, bottom = ctx.safe()
    title = ctx.params.get("title")
    if isinstance(title, str) and title:
        ctx.text(title, (left + 12, top), ctx.contract.px("title"), _muted(ctx), bold=True)
        top += ctx.contract.px("title") + ctx.contract.space("xs")
    emphasis = {str(item) for item in (ctx.params.get("emphasis") or [])}
    table = ctx.params.get("table") if isinstance(ctx.params.get("table"), dict) else {}
    rows = [row for row in (table.get("rows") or []) if isinstance(row, list)]
    hero = next((row for row in rows if any(str(cell) in emphasis for cell in row)), rows[0] if rows else None)
    quiet = [row for row in rows if row is not hero]
    aside = ctx.params.get("aside") if isinstance(ctx.params.get("aside"), dict) else None
    hero_w = int((right - left) * (0.6 if aside else 0.92))
    hero_h = int(ctx.height * 0.34)
    if hero:
        _sheet(ctx, (left, top, left + hero_w, top + hero_h))
        focus = next((str(cell) for cell in hero if str(cell) in emphasis), "")
        ctx.text(str(hero[0]), (left + 24, top + 20), ctx.contract.px("display"), ctx.color("ink"), bold=True)
        if focus:
            ctx.text(focus, (left + 24, top + ctx.contract.px("display") + 28), ctx.contract.px("title"), ctx.color("accent"), bold=True)
        ctx.text(_row_text(hero), (left + 24, top + hero_h - ctx.contract.px("body") - 20), ctx.contract.px("body"), ctx.color("ink"))
    if aside:
        _aside_note(ctx, aside, (left + hero_w + ctx.contract.space("sm"), top, right, top + hero_h))
    cursor = top + hero_h + ctx.contract.space("md")
    for row in quiet:
        ctx.text(_row_text(row), (left + 16, cursor), ctx.contract.px("body"), ctx.color("ink"))
        cursor += ctx.contract.px("body") + ctx.contract.space("xs")
    _activity(ctx, left, bottom)


def _aside_note(ctx: FrameContext, aside: dict, box: tuple[int, int, int, int]) -> None:
    _sheet(ctx, box)
    cursor = box[1] + 16
    ctx.text(str(aside.get("title") or ""), (box[0] + 16, cursor), ctx.contract.px("title"), ctx.color("ink"), bold=True)
    cursor += ctx.contract.px("title") + 8
    for line in aside.get("lines") or []:
        if not isinstance(line, dict):
            continue
        ctx.text(str(line.get("label") or ""), (box[0] + 16, cursor), ctx.contract.px("caption"), _muted(ctx))
        cursor += ctx.contract.px("caption")
        ctx.text(str(line.get("value") or ""), (box[0] + 16, cursor), ctx.contract.px("body"), ctx.color("ink"), bold=True)
        cursor += ctx.contract.px("body") + ctx.contract.space("xs")
    action = aside.get("action")
    if isinstance(action, str) and action and action != str(aside.get("title") or ""):
        ctx.text(action, (box[0] + 16, box[3] - ctx.contract.px("title") - 8), ctx.contract.px("title"), ctx.color("accent"), bold=True)


def _activity_height(ctx: FrameContext) -> int:
    items = [item for item in (ctx.params.get("activity") or []) if isinstance(item, str) and item]
    if not items:
        return 0
    return (ctx.contract.px("caption") + 4) * len(items) + ctx.contract.space("xs")


def _activity(ctx: FrameContext, x: int, bottom: int) -> None:
    items = [item for item in (ctx.params.get("activity") or []) if isinstance(item, str) and item]
    if not items:
        return
    y = bottom - (ctx.contract.px("caption") + 4) * len(items)
    for item in items:
        ctx.text(item, (x + 12, y), ctx.contract.px("caption"), _muted(ctx))
        y += ctx.contract.px("caption") + 4


def _hub_links(ctx: FrameContext) -> None:
    """Three labels join one sheet. The links are lines, not bars, and carry no amounts."""
    _stage(ctx, motif=False)
    left, _safe_top, right, bottom = ctx.safe()
    top = _tags(ctx)
    inset = ctx.contract.space("sm")
    metrics = [item for item in (ctx.params.get("metrics") or []) if isinstance(item, dict)][:3]
    notes = [item for item in (ctx.params.get("notes") or []) if isinstance(item, dict)]
    sources = metrics or notes
    count = max(1, len(sources))
    gap = ctx.contract.space("md")
    source_w = int((right - left) * 0.32)
    row_gap = ctx.contract.space("sm")
    row_h = ctx.contract.px("body") + ctx.contract.px("caption") + ctx.contract.space("sm") * 2 + ctx.contract.space("xs")
    stack_h = row_h * count + row_gap * (count - 1)
    stack_top = top + max(0, (bottom - top - stack_h) // 2)
    sheet = (left + source_w + gap * 3, stack_top, right, stack_top + stack_h)
    draw = ImageDraw.Draw(ctx.image)
    junction_x = left + source_w + gap
    mids: list[float] = []
    for index, source in enumerate(sources):
        y0 = stack_top + index * (row_h + row_gap)
        box = (left, y0, left + source_w, y0 + row_h)
        mids.append((box[1] + box[3]) / 2)
        _sheet(ctx, box)
        ctx.text(str(source.get("label") or ""), (box[0] + inset, box[1] + inset), ctx.contract.px("body"), ctx.color("ink"), bold=True)
        detail = str(source.get("body") or source.get("note") or "")
        if detail:
            _write(
                ctx,
                detail,
                box[0] + inset,
                box[1] + inset + ctx.contract.px("body") + ctx.contract.space("xs"),
                ctx.contract.px("caption"),
                _muted(ctx),
                source_w - inset * 2,
            )
    if mids:
        accent = (*ctx.color("accent"), 255)
        bus_y = mids[len(mids) // 2]
        for mid_y in mids:
            draw.line((left + source_w, mid_y, junction_x, mid_y), fill=accent, width=3)
        draw.line((junction_x, mids[0], junction_x, mids[-1]), fill=accent, width=3)
        draw.line((junction_x, bus_y, sheet[0], bus_y), fill=accent, width=3)
        dot = 5
        draw.ellipse((junction_x - dot, bus_y - dot, junction_x + dot, bus_y + dot), fill=accent)
    _sheet(ctx, sheet)
    inner_w = sheet[2] - sheet[0] - inset * 2
    title = ctx.params.get("title") if isinstance(ctx.params.get("title"), str) else ""
    subtitle = ctx.params.get("subtitle") if isinstance(ctx.params.get("subtitle"), str) else ""
    callout = ctx.params.get("callout") if isinstance(ctx.params.get("callout"), str) else ""
    block = 0
    if title:
        block += ctx.contract.px("title") + ctx.contract.space("xs")
    if subtitle:
        block += ctx.contract.px("body") + ctx.contract.space("md")
    if callout:
        block += ctx.contract.px("body")
    cursor = sheet[1] + max(inset, (stack_h - block) // 2)
    if title:
        cursor = _write(ctx, title, sheet[0] + inset, cursor, ctx.contract.px("title"), ctx.color("ink"), inner_w, bold=True)
        cursor += ctx.contract.space("xs")
    if subtitle:
        cursor = _write(ctx, subtitle, sheet[0] + inset, cursor, ctx.contract.px("body"), _muted(ctx), inner_w)
        cursor += ctx.contract.space("md")
    if callout:
        _write(ctx, callout, sheet[0] + inset, cursor, ctx.contract.px("body"), ctx.color("ink"), inner_w, bold=True)


def _hub(ctx: FrameContext) -> None:
    """Scattered work joins one sheet. Sample bars stay labeled as structure, not as a result."""
    chart = ctx.params.get("chart") if isinstance(ctx.params.get("chart"), dict) else None
    bars = [item for item in (chart or {}).get("bars") or [] if isinstance(item, dict) and item.get("label")]
    if not bars:
        _hub_links(ctx)
        return
    _stage(ctx)
    top = _tags(ctx)
    left, _tag_top, right, bottom = ctx.safe()
    inset = ctx.contract.space("sm")
    metrics = [item for item in (ctx.params.get("metrics") or []) if isinstance(item, dict)][:3]
    notes = [item for item in (ctx.params.get("notes") or []) if isinstance(item, dict)]
    callout = ctx.params.get("callout") if isinstance(ctx.params.get("callout"), str) else ""
    callout_h = 0
    chart_h = 0
    if bars:
        chart_h = ctx.contract.px("caption") * 2 + ctx.contract.space("sm") + ctx.contract.px("body")
    gap = ctx.contract.space("md")
    well_bottom = bottom - callout_h - chart_h - (ctx.contract.space("sm") if callout or bars else 0)
    sources = metrics or notes
    count = max(1, len(sources))
    source_w = int((right - left) * 0.34)
    row_gap = ctx.contract.space("md")
    row_h = ctx.contract.px("body") + ctx.contract.px("caption") + ctx.contract.space("sm") * 2
    stack_h = row_h * count + row_gap * (count - 1)
    stack_top = top + max(0, (well_bottom - top - stack_h) // 2)
    sheet = (left + source_w + gap * 2, top, right, well_bottom)
    draw = ImageDraw.Draw(ctx.image)
    for index, source in enumerate(sources):
        y0 = stack_top + index * (row_h + row_gap)
        box = (left, y0, left + source_w, y0 + row_h)
        label = str(source.get("label") or "")
        mid_y = (box[1] + box[3]) / 2
        draw.line((box[2], mid_y, sheet[0], mid_y), fill=(*ctx.color("ink"), 255), width=3)
        _sheet(ctx, box)
        ctx.text(label, (box[0] + inset, box[1] + inset), ctx.contract.px("body"), ctx.color("ink"), bold=True)
        sample = " · ".join(part for part in (str(source.get("value") or ""), str(source.get("note") or "")) if part)
        if sample:
            ctx.text(sample, (box[0] + inset, box[3] - ctx.contract.px("caption") - inset // 2), ctx.contract.px("caption"), _muted(ctx))
    _sheet(ctx, sheet)
    cursor = sheet[1] + inset
    title = ctx.params.get("title")
    if isinstance(title, str) and title:
        cursor = _write(ctx, title, sheet[0] + inset, cursor, ctx.contract.px("title"), ctx.color("ink"), sheet[2] - sheet[0] - inset * 2, bold=True)
        cursor += ctx.contract.space("xs")
    subtitle = ctx.params.get("subtitle")
    if isinstance(subtitle, str) and subtitle:
        cursor = _write(ctx, subtitle, sheet[0] + inset, cursor, ctx.contract.px("body"), _muted(ctx), sheet[2] - sheet[0] - inset * 2)
        cursor += ctx.contract.space("sm")
    for note in notes:
        body = str(note.get("body") or "")
        if body and cursor + ctx.contract.px("body") < sheet[3] - inset:
            cursor = _write(ctx, body, sheet[0] + inset, cursor, ctx.contract.px("body"), ctx.color("ink"), sheet[2] - sheet[0] - inset * 2, bold=True)
            cursor += ctx.contract.space("xs")
    if callout:
        _write(
            ctx,
            callout,
            sheet[0] + inset,
            max(cursor + ctx.contract.space("sm"), sheet[3] - inset - ctx.contract.px("body") * 2),
            ctx.contract.px("body"),
            ctx.color("ink"),
            sheet[2] - sheet[0] - inset * 2,
            bold=True,
        )
    if bars:
        chart_top = well_bottom + ctx.contract.space("xs")
        chart_title = str((chart or {}).get("title") or "")
        if chart_title:
            ctx.text(chart_title, (left + inset, chart_top), ctx.contract.px("caption"), _muted(ctx))
            chart_top += ctx.contract.px("caption") + ctx.contract.space("xs")
        track_w = right - left
        bar_h = ctx.contract.px("body")
        segment = track_w // len(bars)
        for index, item in enumerate(bars):
            x0 = left + index * segment
            x1 = left + track_w if index == len(bars) - 1 else x0 + segment - ctx.contract.space("xs")
            draw.rounded_rectangle((x0, chart_top, x1, chart_top + bar_h), radius=8, fill=(*ctx.color("accent"), 255))
            ctx.text(str(item.get("label") or ""), (x0 + inset, chart_top + bar_h + ctx.contract.space("sm")), ctx.contract.px("caption"), ctx.color("ink"), bold=True)


def _sequence(ctx: FrameContext) -> None:
    _stage(ctx)
    columns = [item for item in (ctx.params.get("columns") or []) if isinstance(item, dict)]
    left, top, right, bottom = ctx.safe()
    x = left + ctx.contract.space("lg")
    width = right - x
    note = ctx.params.get("note")
    note_h = ctx.contract.px("body") + ctx.contract.space("sm") if isinstance(note, str) and note else 0
    span = max(1, (bottom - top - note_h) / max(1, len(columns)))
    title_x = x + int(ctx.width * 0.06)
    point_x = x + int(width * 0.42)
    point_w = right - point_x
    for index, column in enumerate(columns):
        shown = _stagger(ctx, index)
        if shown < 0.08 and index:
            continue
        shift = int((1 - shown) * ctx.contract.space("md"))
        row_y = top + span * index + shift
        size = ctx.contract.px("display" if index == 0 else "title")
        ctx.text(str(column.get("index") or ""), (x, row_y), ctx.contract.px("caption"), ctx.color("accent"), bold=True)
        ctx.text(str(column.get("title") or ""), (title_x, row_y), size, ctx.color("paper"), bold=True)
        body = column.get("body")
        cursor = row_y + size + 6
        if isinstance(body, str) and body:
            cursor = _write(ctx, body, title_x, cursor, ctx.contract.px("body"), ctx.color("paper"), int(width * 0.36))
        point_y = row_y + ctx.contract.px("caption")
        for point in column.get("points") or []:
            if isinstance(point, str) and point:
                point_y = _write(ctx, point, point_x, point_y, ctx.contract.px("body"), ctx.color("paper"), point_w)
    if isinstance(note, str) and note:
        ctx.text(note, (x, bottom - ctx.contract.px("body")), ctx.contract.px("body"), ctx.color("paper"))


BOARDS = {
    "hero_still": hero_still,
    "portrait_split": portrait_split,
    "brand_center": brand_center,
    "brand_split": brand_split,
    "end_lockup": end_lockup,
    "app_screen": app_screen,
    "manifesto": manifesto,
    "workspace": workspace,
    "lanes": lanes,
    "catalog": catalog,
    "dashboard": dashboard,
    "triptych": triptych,
}

