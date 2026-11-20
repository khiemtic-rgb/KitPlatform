"""NOVIXA intro v1. Two layouts, one voice, local frames. Does not touch older films."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
VOICE = ROOT / "voice"
FINAL = ROOT / "final"
QA = ROOT / "qa"
SHOTS = Path(r"E:\KitPlatform\docs\novixa\launch\DOC-008\assets\screenshots")
LOGO_SRC = Path(r"E:\KitPlatform\docs\novixa\assets\brand\novixa-logo.png")
FONT_B = Path(r"C:\Windows\Fonts\segoeuib.ttf")
FONT_R = Path(r"C:\Windows\Fonts\segoeui.ttf")
FPS = 30
TOTAL = 53.0
SCENES = (0.0, 5.0, 12.0, 20.0, 31.0, 41.0, 53.0)

SHADOW = 22
NIGHT = (8, 14, 28, 255)
PAPER = (244, 247, 251, 255)
WHITE = (255, 255, 255, 255)
INK = (14, 28, 51, 255)
TEAL = (20, 122, 104, 255)
CORAL = (196, 78, 62, 255)
MUTED = (90, 104, 122, 255)

PROFILES = {
    "9x16": {"w": 1080, "h": 1920, "x": 72, "top": 156, "bottom": 236, "kind": "tall"},
    "4x5": {"w": 1080, "h": 1350, "x": 64, "top": 76, "bottom": 100, "kind": "feed"},
}


def ease(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3 - 2 * value)


def span(t: float, start: float, end: float) -> float:
    if end <= start:
        return 1.0 if t >= end else 0.0
    return ease((t - start) / (end - start))


def mix(a: tuple[int, int, int, int], b: tuple[int, int, int, int], amount: float) -> tuple[int, int, int, int]:
    return tuple(int(a[i] + (b[i] - a[i]) * amount) for i in range(4))


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT_B if bold else FONT_R), size)


def text_sprite(text: str, size: int, fill: tuple[int, int, int, int], bold: bool = False) -> Image.Image:
    face = font(size, bold)
    probe = ImageDraw.Draw(Image.new("RGBA", (4, 4)))
    box = probe.textbbox((0, 0), text, font=face)
    image = Image.new("RGBA", (box[2] - box[0] + 8, box[3] - box[1] + 8), (0, 0, 0, 0))
    ImageDraw.Draw(image).text((4 - box[0], 4 - box[1]), text, font=face, fill=fill)
    return image


def block(lines: list[str], size: int, fill: tuple[int, int, int, int], bold: bool = False, gap: int = 6, align: str = "center") -> Image.Image:
    rows = [text_sprite(line, size, fill, bold) for line in lines]
    width = max(row.width for row in rows)
    height = sum(row.height for row in rows) + gap * (len(rows) - 1)
    image = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    y = 0
    for row in rows:
        x = 0 if align == "left" else (width - row.width) // 2
        image.alpha_composite(row, (x, y))
        y += row.height + gap
    return image


def fit_lines(text: str, max_width: int, size: int, bold: bool = False) -> list[str]:
    face = font(size, bold)
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        trial = word if not current else f"{current} {word}"
        if face.getlength(trial) <= max_width:
            current = trial
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines or [text]


def blit(base: Image.Image, sprite: Image.Image, xy: tuple[float, float], opacity: float = 1) -> None:
    if sprite is None or opacity <= 0.02:
        return
    x, y = int(xy[0]), int(xy[1])
    layer = sprite
    if opacity < 0.985:
        layer = sprite.copy()
        layer.putalpha(layer.getchannel("A").point(lambda p: int(p * opacity)))
    src_x, src_y = max(0, -x), max(0, -y)
    dst_x, dst_y = max(0, x), max(0, y)
    width = min(layer.width - src_x, base.width - dst_x)
    height = min(layer.height - src_y, base.height - dst_y)
    if width <= 0 or height <= 0:
        return
    piece = layer.crop((src_x, src_y, src_x + width, src_y + height))
    base.alpha_composite(piece, (dst_x, dst_y))


def contain(image: Image.Image, width: int, height: int) -> Image.Image:
    scale = min(width / image.width, height / image.height)
    size = (max(1, int(image.width * scale)), max(1, int(image.height * scale)))
    return image.resize(size, Image.Resampling.LANCZOS)


def plate(width: int, height: int, radius: int, fill: tuple[int, int, int, int]) -> tuple[Image.Image, int]:
    canvas = Image.new("RGBA", (width + SHADOW * 2, height + SHADOW * 2), (0, 0, 0, 0))
    blur = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(blur).rounded_rectangle(
        (SHADOW + 6, SHADOW + 14, SHADOW + width - 6, SHADOW + height + 8),
        radius=radius,
        fill=(14, 28, 51, 58),
    )
    canvas.alpha_composite(blur.filter(ImageFilter.GaussianBlur(12)))
    ImageDraw.Draw(canvas).rounded_rectangle((SHADOW, SHADOW, SHADOW + width - 1, SHADOW + height - 1), radius=radius, fill=fill)
    return canvas, SHADOW


def round_clip(image: Image.Image, radius: int) -> Image.Image:
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, image.width - 1, image.height - 1), radius=radius, fill=255)
    clipped = image.copy()
    clipped.putalpha(mask)
    return clipped


def demo_chip(size: int = 22) -> Image.Image:
    label = text_sprite("Dữ liệu minh họa", size, TEAL, True)
    chip = Image.new("RGBA", (label.width + 24, label.height + 12), (0, 0, 0, 0))
    ImageDraw.Draw(chip).rounded_rectangle((0, 0, chip.width - 1, chip.height - 1), radius=chip.height // 2, fill=(226, 242, 237, 255))
    chip.alpha_composite(label, (12, 6))
    return chip


def board(image: Image.Image, inner_w: int, label_size: int = 22) -> Image.Image:
    fitted = round_clip(
        image.resize((inner_w, max(1, round(image.height * (inner_w / image.width)))), Image.Resampling.LANCZOS),
        16,
    )
    chip = demo_chip(label_size if inner_w >= 380 else 18)
    card_w = max(fitted.width, chip.width + 8) + 28
    card_h = 16 + chip.height + 12 + fitted.height + 16
    canvas, pad = plate(card_w, card_h, 24, WHITE)
    canvas.alpha_composite(chip, (pad + 14, pad + 14))
    canvas.alpha_composite(fitted, (pad + (card_w - fitted.width) // 2, pad + 14 + chip.height + 12))
    return canvas


def phone_shot(image: Image.Image, screen_w: int) -> Image.Image:
    screen = round_clip(
        image.resize((screen_w, max(1, round(image.height * (screen_w / image.width)))), Image.Resampling.LANCZOS),
        30,
    )
    bezel = 14
    body_w, body_h = screen.width + bezel * 2, screen.height + bezel * 2
    body = Image.new("RGBA", (body_w, body_h), (0, 0, 0, 0))
    ImageDraw.Draw(body).rounded_rectangle((0, 0, body_w - 1, body_h - 1), radius=38, fill=(17, 28, 48, 255))
    body.alpha_composite(screen, (bezel, bezel))
    chip = demo_chip(22)
    content_w = max(body_w, chip.width)
    content_h = chip.height + 14 + body_h
    canvas = Image.new("RGBA", (content_w + SHADOW * 2, content_h + SHADOW * 2), (0, 0, 0, 0))
    blur = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    bx = SHADOW + (content_w - body_w) // 2
    by = SHADOW + chip.height + 14
    ImageDraw.Draw(blur).rounded_rectangle((bx + 8, by + 16, bx + body_w - 8, by + body_h + 10), radius=40, fill=(14, 28, 51, 78))
    canvas.alpha_composite(blur.filter(ImageFilter.GaussianBlur(14)))
    canvas.alpha_composite(chip, (SHADOW + (content_w - chip.width) // 2, SHADOW))
    canvas.alpha_composite(body, (bx, by))
    return canvas


def report_cards(page: Image.Image) -> tuple[Image.Image, Image.Image]:
    # Stop above the next section title. Card copy ends well before that row.
    return page.crop((548, 888, 1292, 1088)), page.crop((1296, 888, 2048, 1088))


def stacked(top: Image.Image, bottom: Image.Image, gap: int = 16) -> Image.Image:
    width = max(top.width, bottom.width)
    image = Image.new("RGBA", (width, top.height + gap + bottom.height), (0, 0, 0, 0))
    image.alpha_composite(top, ((width - top.width) // 2, 0))
    image.alpha_composite(bottom, ((width - bottom.width) // 2, top.height + gap))
    return image


def title_card(index: int, title: str, width: int, height: int) -> Image.Image:
    canvas, pad = plate(width, height, 22, WHITE)
    index_label = text_sprite(f"{index:02d}", 28 if width < 420 else 34, TEAL, True)
    name_size = 30 if width < 420 else 48
    room = width - 72 - index_label.width
    while name_size > 22 and font(name_size, True).getlength(title) > room:
        name_size -= 2
    name = text_sprite(title, name_size, INK, True)
    row = max(index_label.height, name.height)
    y = pad + (height - row) // 2
    ImageDraw.Draw(canvas).rounded_rectangle((pad, pad + 22, pad + 7, pad + height - 22), radius=3, fill=TEAL)
    canvas.alpha_composite(index_label, (pad + 28, y + (row - index_label.height) // 2))
    canvas.alpha_composite(name, (pad + 28 + index_label.width + 16, y + (row - name.height) // 2))
    return canvas


def fit_size(text: str, max_width: int, start: int, minimum: int = 36) -> int:
    size = start
    while size > minimum and font(size, True).getlength(text) > max_width:
        size -= 2
    return size


def web_pill(max_width: int) -> Image.Image:
    label = text_sprite("novixa.vn", 42 if max_width > 800 else 36, TEAL, True)
    chip = Image.new("RGBA", (label.width + 48, label.height + 22), (0, 0, 0, 0))
    ImageDraw.Draw(chip).rounded_rectangle((0, 0, chip.width - 1, chip.height - 1), radius=chip.height // 2, fill=(226, 242, 237, 255))
    chip.alpha_composite(label, (24, 11))
    return chip


def word_at(words: list[dict], text: str, occurrence: int = 0) -> float:
    found = [word["offset"] for word in words if word["text"].lower() == text.lower()]
    if len(found) <= occurrence:
        raise SystemExit(f"missing word {text!r} occurrence {occurrence}")
    return found[occurrence]


def load_hits() -> dict[str, float]:
    rows = json.loads((VOICE / "boundaries.json").read_text(encoding="utf-8"))
    scenes = [row["words"] for row in rows]
    return {
        "chua": word_at(scenes[0], "chưa"),
        "hang": word_at(scenes[1], "hàng"),
        "gia": word_at(scenes[1], "giá"),
        "chi": word_at(scenes[1], "chi"),
        "dieu": word_at(scenes[2], "Điều"),
        "ro": word_at(scenes[2], "rõ"),
        "brand4": word_at(scenes[3], "Nôvixa"),
        "ban": word_at(scenes[4 - 1], "bán"),
        "ton": word_at(scenes[3], "tồn"),
        "theo": word_at(scenes[3], "theo"),
        "ro5": word_at(scenes[4], "rõ"),
        "quyet": word_at(scenes[4], "quyết"),
        "brand6": word_at(scenes[5], "Nôvixa", 0),
        "nen": word_at(scenes[5], "nền"),
        "cham": word_at(scenes[5], "chấm"),
        "end6": scenes[5][-1]["offset"] + scenes[5][-1]["duration"],
    }


def build_art(profile: dict, hits: dict[str, float]) -> dict:
    pos = Image.open(SHOTS / "10-staff-pos.png").convert("RGBA")
    inv = Image.open(SHOTS / "04-inventory-stock.png").convert("RGBA").crop((540, 580, 1500, 1320))
    left_card, right_card = report_cards(Image.open(SHOTS / "08-reports-home.png").convert("RGBA"))
    logo = Image.open(LOGO_SRC).convert("RGBA")
    kind = profile["kind"]
    box_w = profile["w"] - profile["x"] * 2
    names = ("HÀNG TỒN", "GIÁ VỐN", "CHI PHÍ")
    text_w = box_w - 36
    if kind == "tall":
        cards = [title_card(index, name, box_w, 168) for index, name in enumerate(names, start=1)]
        logo_w = 520
        heroes = {
            "pos": phone_shot(pos, 520),
            "inv": board(inv, 900),
            "rep": stacked(board(left_card, 860, 22), board(right_card, 860, 22)),
        }
        cluster = {
            "pos": phone_shot(pos, 300),
            "inv": board(inv, 470, 20),
            "rep": stacked(board(left_card, 450, 18), board(right_card, 450, 18), 12),
        }
        hook_size, hook_b, problem_size, solution_size = fit_size("DOANH THU CAO…", text_w, 108), fit_size("LỢI NHUẬN TỐT", text_w, 92), fit_size("ĐỪNG QUẢN LÝ", text_w, 84), fit_size("DỮ LIỆU VẬN HÀNH", text_w, 72)
    else:
        card_w = (box_w - 36) // 3
        cards = [title_card(index, name, card_w, 188) for index, name in enumerate(names, start=1)]
        logo_w = 400
        heroes = {
            "pos": phone_shot(pos, 300),
            "inv": board(inv, 900),
            "rep": stacked(board(left_card, 860, 22), board(right_card, 860, 22)),
        }
        cluster = {
            "pos": phone_shot(pos, 168),
            "inv": board(inv, 560, 18),
            "rep_l": board(left_card, 420, 18),
            "rep_r": board(right_card, 420, 18),
        }
        hook_size, hook_b, problem_size, solution_size = fit_size("DOANH THU CAO…", text_w, 78), fit_size("LỢI NHUẬN TỐT", text_w, 64), fit_size("ĐỪNG QUẢN LÝ", text_w, 60), fit_size("DỮ LIỆU VẬN HÀNH", text_w, 52)
    logo_h = round(logo.height * (logo_w / logo.width))
    return {
        "profile": profile,
        "hits": hits,
        "cards": cards,
        "heroes": heroes,
        "cluster": cluster,
        "logo": logo.resize((logo_w, logo_h), Image.Resampling.LANCZOS),
        "hook_a": block([ "DOANH THU CAO…" ], hook_size, WHITE, True),
        "hook_b": block(["CHƯA CHẮC", "LỢI NHUẬN TỐT"], hook_b, CORAL, True, 6, "left"),
        "problem": block(["ĐỪNG QUẢN LÝ", "BẰNG CẢM GIÁC"], problem_size, WHITE, True, 6, "left"),
        "solution": block(["NHÌN RÕ", "DỮ LIỆU VẬN HÀNH"], solution_size, INK, True, 6, "left"),
        "proof_titles": {
            "pos": block(["BÁN HÀNG"], 72 if kind == "tall" else 54, INK, True, align="left"),
            "inv": block(fit_lines("QUẢN LÝ TỒN KHO", text_w, 60 if kind == "tall" else 46, True), 60 if kind == "tall" else 46, INK, True, align="left"),
            "rep": block(fit_lines("THEO DÕI DỮ LIỆU VẬN HÀNH", text_w, 52 if kind == "tall" else 40, True), 52 if kind == "tall" else 40, INK, True, align="left"),
        },
        "brand": text_sprite("NOVIXA", 156 if kind == "tall" else 116, INK, True),
        "see": text_sprite("NHÌN RÕ HƠN", 78 if kind == "tall" else 52, TEAL, True),
        "act": text_sprite("CHỦ ĐỘNG HƠN", 78 if kind == "tall" else 52, INK, True),
        "tag": block(fit_lines("Nền tảng vận hành nhà thuốc thông minh", box_w - 80, 42, True), 42, INK, True, 4),
        "web": web_pill(box_w),
        "tabs": [
            text_sprite("BÁN HÀNG", 32 if kind == "tall" else 28, INK, True),
            text_sprite("QUẢN LÝ TỒN KHO", 32 if kind == "tall" else 28, INK, True),
            text_sprite("THEO DÕI DỮ LIỆU", 32 if kind == "tall" else 28, INK, True),
        ],
        "pill": text_sprite("Dữ liệu minh họa", 26, WHITE, True),
    }


def background(t: float, profile: dict, paper: float) -> Image.Image:
    color = mix(NIGHT, PAPER, paper)
    image = Image.new("RGBA", (profile["w"], profile["h"]), color)
    if paper < 0.85:
        shade = Image.new("L", (1, profile["h"]))
        for y in range(profile["h"]):
            shade.putpixel((0, y), int(36 * (1 - paper) * (1 - y / profile["h"])))
        tint = Image.new("RGBA", image.size, (86, 140, 176, 255))
        tint.putalpha(shade.resize(image.size))
        image.alpha_composite(tint)
    return image


def safe_box(profile: dict) -> tuple[int, int, int, int]:
    return (profile["x"], profile["top"], profile["w"] - profile["x"], profile["h"] - profile["bottom"])


def mark(qa: list, name: str, profile: dict, xy: tuple[float, float], sprite: Image.Image, opacity: float) -> None:
    if opacity < 0.9:
        return
    left, top, right, bottom = safe_box(profile)
    x, y = int(xy[0]), int(xy[1])
    if x < left - 2 or y < top - 2 or x + sprite.width > right + 2 or y + sprite.height > bottom + 2:
        qa.append(f"{profile['kind']} {name} {x},{y} {sprite.width}x{sprite.height}")


def paint_cards(frame: Image.Image, art: dict, places: list[tuple[float, float, float]], qa: list | None, names: list[str]) -> None:
    for card, (x, y, opacity), name in zip(art["cards"], places, names):
        blit(frame, card, (x, y), opacity)
        if qa is not None:
            mark(qa, name, art["profile"], (x, y), card, opacity)


def card_places(art: dict, local: float, collapse: float) -> list[tuple[float, float, float]]:
    profile = art["profile"]
    cards = art["cards"]
    hits = art["hits"]
    arrivals = (hits["hang"], hits["gia"], hits["chi"])
    box_l, box_t, box_r, box_b = safe_box(profile)
    box_w, box_h = box_r - box_l, box_b - box_t
    spread = span(local, arrivals[2] + 0.35, arrivals[2] + 0.95) if profile["kind"] == "tall" else 0.0
    whites = [(card.width - SHADOW * 2, card.height - SHADOW * 2) for card in cards]
    places = []
    if profile["kind"] == "tall":
        gap = 16 + int(18 * spread)
        stack = sum(item[1] for item in whites) + gap * 2
        y = box_t + (box_h - stack) / 2 - SHADOW
        for index, card in enumerate(cards):
            arrived = span(local, 0.25 + index * 0.12, arrivals[index] + 0.18)
            direction = -1 if index % 2 == 0 else 1
            x = box_l - SHADOW + (1 - arrived) * direction * 42
            places.append((x, y, arrived))
            y += whites[index][1] + gap
    else:
        gap = 18
        row = sum(item[0] for item in whites) + gap * 2
        x = box_l + (box_w - row) / 2 - SHADOW
        y = box_t + (box_h - whites[0][1]) / 2 - SHADOW
        for index, card in enumerate(cards):
            arrived = span(local, 0.25 + index * 0.12, arrivals[index] + 0.18)
            places.append((x, y + (1 - arrived) * 28, arrived))
            x += whites[index][0] + gap
    if collapse <= 0:
        return places
    cx, cy = profile["w"] / 2, profile["h"] / 2
    pulled = []
    for x, y, opacity in places:
        pulled.append((x + (cx - x) * collapse, y + (cy - 40 - y) * collapse, opacity * (1 - collapse)))
    return pulled


def demo_pill(frame: Image.Image, art: dict, opacity: float, qa: list | None) -> None:
    if opacity <= 0.02:
        return
    profile = art["profile"]
    pill = art["pill"]
    width, height = pill.width + 36, pill.height + 18
    chip = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    ImageDraw.Draw(chip).rounded_rectangle((0, 0, width - 1, height - 1), radius=18, fill=INK)
    chip.alpha_composite(pill, (18, 9))
    x = (profile["w"] - width) / 2
    y = profile["top"]
    blit(frame, chip, (x, y), opacity)
    if qa is not None:
        mark(qa, "demo-pill", profile, (x, y), chip, opacity)


def ruled(frame: Image.Image, sprite: Image.Image, profile: dict, y: float, opacity: float, accent: tuple[int, int, int, int]) -> tuple[float, float]:
    x = profile["x"]
    if opacity > 0.04:
        ImageDraw.Draw(frame).rounded_rectangle(
            (x, int(y) + 8, x + 8, int(y + max(8, sprite.height) - 8)),
            radius=4,
            fill=(accent[0], accent[1], accent[2], int(255 * min(1, opacity))),
        )
    pos = (x + 28, y)
    blit(frame, sprite, pos, opacity)
    return pos


def scene_hook(frame: Image.Image, local: float, art: dict, qa: list | None) -> None:
    profile = art["profile"]
    switch = art["hits"]["chua"]
    first = art["hook_a"]
    second = art["hook_b"]
    a_opacity = 1 - span(local, switch - 0.08, switch + 0.18)
    b_opacity = span(local, switch + 0.02, switch + 0.4)
    ay = profile["h"] * (0.30 if profile["kind"] == "tall" else 0.24)
    by = ay + first.height + (40 if profile["kind"] == "tall" else 28)
    a_pos = ruled(frame, first, profile, ay - (1 - a_opacity) * 24, a_opacity, TEAL)
    b_pos = ruled(frame, second, profile, by + (1 - b_opacity) * 20, b_opacity, CORAL)
    if qa is not None:
        mark(qa, "hook-a", profile, a_pos, first, a_opacity)
        mark(qa, "hook-b", profile, b_pos, second, b_opacity)


def scene_bridge(frame: Image.Image, local: float, art: dict, qa: list | None) -> float:
    collapse = span(local, 2.6, 4.5)
    paint_cards(frame, art, card_places(art, 6.5, collapse), qa, ["card-a", "card-b", "card-c"])
    problem = art["problem"]
    profile = art["profile"]
    p_opacity = span(local, 0.15, 0.55) * (1 - span(local, 3.55, 4.25))
    py = profile["top"] + (56 if profile["kind"] == "tall" else 28)
    p_pos = ruled(frame, problem, profile, py, p_opacity, CORAL)
    solution = art["solution"]
    s_opacity = span(local, 4.22, 4.62) * (1 - span(local, 7.4, 7.9))
    sy = profile["h"] * (0.36 if profile["kind"] == "tall" else 0.32)
    s_pos = ruled(frame, solution, profile, sy, s_opacity, TEAL)
    if qa is not None:
        mark(qa, "problem", profile, p_pos, problem, p_opacity)
        mark(qa, "solution", profile, s_pos, solution, s_opacity)
    return span(local, 3.45, 5.05)


def proof_key(local: float) -> str:
    if local < 6.55:
        return "pos"
    if local < 8.75:
        return "inv"
    return "rep"


def scene_proof(frame: Image.Image, local: float, art: dict, qa: list | None) -> None:
    profile = art["profile"]
    brand = art["brand"]
    intro = 1 - span(local, 4.05, 4.5)
    hero = span(local, 4.15, 4.55)
    by = profile["h"] * (0.28 if profile["kind"] == "tall" else 0.22)
    arrived = span(local, art["hits"]["brand4"] - 0.2, art["hits"]["brand4"] + 0.45)
    b_pos = ruled(frame, brand, profile, by, intro * arrived, TEAL)
    if qa is not None:
        mark(qa, "brand", profile, b_pos, brand, intro * arrived)
    if hero <= 0.02:
        return
    key = proof_key(local)
    title = art["proof_titles"][key]
    sprite = art["heroes"][key]
    box_l, box_t, box_r, box_b = safe_box(profile)
    if profile["kind"] == "feed" and key == "pos":
        px = box_r - (sprite.width - SHADOW)
        py = box_t - 8
        title_y = py + (sprite.height - title.height) / 2
        t_pos = ruled(frame, title, profile, title_y, hero, TEAL)
        blit(frame, sprite, (px + (1 - hero) * 28, py), hero)
        if qa is not None:
            mark(qa, "proof-title", profile, t_pos, title, hero)
            mark(qa, "hero-pos", profile, (px, py), sprite, hero)
        return
    t_pos = ruled(frame, title, profile, box_t + 4, hero, TEAL)
    hy = t_pos[1] + title.height + 12 - SHADOW
    hx = box_l + max(0, (box_r - box_l - (sprite.width - SHADOW * 2)) / 2) - SHADOW
    blit(frame, sprite, (hx, hy + (1 - hero) * 24), hero)
    if qa is not None:
        mark(qa, "proof-title", profile, t_pos, title, hero)
        mark(qa, f"hero-{key}", profile, (hx, hy), sprite, hero)


def scene_value(frame: Image.Image, local: float, art: dict, qa: list | None) -> None:
    profile = art["profile"]
    see_on = span(local, art["hits"]["ro5"] - 0.1, art["hits"]["ro5"] + 0.35)
    act_on = span(local, art["hits"]["quyet"] - 0.05, art["hits"]["quyet"] + 0.4)
    settle = span(local, 0.05, 0.75)
    box_l, box_t, box_r, box_b = safe_box(profile)
    see, act = art["see"], art["act"]
    if profile["kind"] == "tall":
        blit(frame, see, ((profile["w"] - see.width) / 2, box_t + 62), see_on)
        act_y = box_t + 62 + see.height + 6
        blit(frame, act, ((profile["w"] - act.width) / 2, act_y), act_on)
        if qa is not None:
            mark(qa, "see", profile, ((profile["w"] - see.width) / 2, box_t + 62), see, see_on)
            mark(qa, "act", profile, ((profile["w"] - act.width) / 2, act_y), act, act_on)
        pos, inv, rep = art["cluster"]["pos"], art["cluster"]["inv"], art["cluster"]["rep"]
        top = act_y + act.height + 22
        gap = 14
        group_w = pos.width + gap + max(inv.width, rep.width)
        x = box_l + max(0, ((box_r - box_l) - group_w) / 2)
        blit(frame, pos, (x + (1 - settle) * -30, top), settle)
        right = x + pos.width + gap
        blit(frame, inv, (right, top), settle)
        blit(frame, rep, (right, top + inv.height + 12), settle)
        if qa is not None:
            mark(qa, "cluster-pos", profile, (x, top), pos, settle)
            mark(qa, "cluster-inv", profile, (right, top), inv, settle)
            mark(qa, "cluster-rep", profile, (right, top + inv.height + 12), rep, settle)
        return
    see_x = box_l
    act_x = box_l + see.width + 28
    head_y = box_t + 58
    if act_x + act.width > box_r:
        act_x = box_l
        blit(frame, see, (see_x, head_y), see_on)
        blit(frame, act, (act_x, head_y + see.height + 6), act_on)
        row_y = head_y + see.height + act.height + 24
    else:
        blit(frame, see, (see_x, head_y), see_on)
        blit(frame, act, (act_x, head_y), act_on)
        row_y = head_y + max(see.height, act.height) + 18
    if qa is not None:
        mark(qa, "see", profile, (see_x, head_y), see, see_on)
        mark(qa, "act", profile, (act_x, head_y if act_x != box_l or see_x == box_l else head_y), act, act_on)
    pos, inv = art["cluster"]["pos"], art["cluster"]["inv"]
    row_w = pos.width + 16 + inv.width
    x = box_l + max(0, ((box_r - box_l) - row_w) / 2)
    blit(frame, pos, (x, row_y), settle)
    blit(frame, inv, (x + pos.width + 16, row_y), settle)
    left, right = art["cluster"]["rep_l"], art["cluster"]["rep_r"]
    pair_w = left.width + 16 + right.width
    rx = box_l + max(0, ((box_r - box_l) - pair_w) / 2)
    ry = row_y + max(pos.height, inv.height) + 14
    blit(frame, left, (rx, ry), settle)
    blit(frame, right, (rx + left.width + 16, ry), settle)
    if qa is not None:
        mark(qa, "cluster-pos", profile, (x, row_y), pos, settle)
        mark(qa, "cluster-inv", profile, (x + pos.width + 16, row_y), inv, settle)
        mark(qa, "cluster-rep-l", profile, (rx, ry), left, settle)
        mark(qa, "cluster-rep-r", profile, (rx + left.width + 16, ry), right, settle)


def scene_close(frame: Image.Image, local: float, art: dict, qa: list | None) -> None:
    profile = art["profile"]
    logo, tag, web = art["logo"], art["tag"], art["web"]
    logo_on = span(local, art["hits"]["brand6"], art["hits"]["brand6"] + 0.45)
    tag_on = span(local, art["hits"]["nen"] - 0.05, art["hits"]["nen"] + 0.4)
    web_on = span(local, art["hits"]["cham"] - 0.05, art["hits"]["cham"] + 0.35)
    group = logo.height + 18 + 4 + 22 + tag.height + 16 + web.height
    box_l, box_t, box_r, box_b = safe_box(profile)
    y = box_t + max(0, ((box_b - box_t) - group) / 2)
    blit(frame, logo, ((profile["w"] - logo.width) / 2, y + (1 - logo_on) * 12), logo_on)
    rule_y = y + logo.height + 18
    if tag_on > 0.05:
        rule_w = int(88 * tag_on)
        ImageDraw.Draw(frame).rectangle(((profile["w"] - rule_w) / 2, rule_y, (profile["w"] + rule_w) / 2, rule_y + 4), fill=TEAL)
    tag_y = rule_y + 22
    web_y = tag_y + tag.height + 16
    blit(frame, tag, ((profile["w"] - tag.width) / 2, tag_y), tag_on)
    blit(frame, web, ((profile["w"] - web.width) / 2, web_y), web_on)
    if qa is not None:
        mark(qa, "logo", profile, ((profile["w"] - logo.width) / 2, y), logo, logo_on)
        mark(qa, "tag", profile, ((profile["w"] - tag.width) / 2, tag_y), tag, tag_on)
        mark(qa, "web", profile, ((profile["w"] - web.width) / 2, web_y), web, web_on)


def render_frame(t: float, art: dict, qa: list | None = None) -> Image.Image:
    profile = art["profile"]
    if t < SCENES[2]:
        paper = 0.0
    elif t < SCENES[3]:
        paper = 0.0
    else:
        paper = 1.0
    local_bridge = t - SCENES[2]
    bridge_paper = scene_bridge_paper(local_bridge) if SCENES[2] <= t < SCENES[3] else 0.0
    if t >= SCENES[5]:
        paper = 1.0
        frame = Image.new("RGBA", (profile["w"], profile["h"]), WHITE)
    else:
        frame = background(t, profile, max(paper, bridge_paper) if t < SCENES[3] else paper)
        if SCENES[2] <= t < SCENES[3]:
            frame = background(t, profile, bridge_paper)
    if t < SCENES[1]:
        scene_hook(frame, t - SCENES[0], art, qa)
    elif t < SCENES[2]:
        paint_cards(frame, art, card_places(art, t - SCENES[1], 0.0), qa, ["stock", "cost", "spend"])
    elif t < SCENES[3]:
        scene_bridge(frame, t - SCENES[2], art, qa)
    elif t < SCENES[4]:
        scene_proof(frame, t - SCENES[3], art, qa)
    elif t < SCENES[5]:
        scene_value(frame, t - SCENES[4], art, qa)
    else:
        scene_close(frame, t - SCENES[5], art, qa)
    return frame


def scene_bridge_paper(local: float) -> float:
    return span(local, 3.45, 5.05)


def run_ffmpeg(cmd: list[str]) -> None:
    done = subprocess.run(cmd, capture_output=True)
    if done.returncode != 0:
        sys.stderr.write(done.stderr.decode("utf-8", "replace")[-4000:])
        raise SystemExit(done.returncode)


def build_audio() -> Path:
    slots = [5, 7, 8, 11, 10, 12]
    wavs = []
    for index, slot in enumerate(slots, start=1):
        src = VOICE / f"s{index}.mp3"
        probe = subprocess.run(
            ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(src)],
            capture_output=True, text=True, check=True,
        )
        duration = float(probe.stdout.strip())
        if duration > slot + 0.05:
            raise SystemExit(f"s{index}.mp3 is {duration:.3f}s and does not fit slot {slot}s")
        wav = VOICE / f"s{index}-slot.wav"
        run_ffmpeg([
            "ffmpeg", "-y", "-i", str(src),
            "-af", f"apad=whole_dur={slot},atrim=0:{slot}",
            "-ar", "48000", "-ac", "2", str(wav),
        ])
        wavs.append(wav)
    out = VOICE / "master.wav"
    cmd = ["ffmpeg", "-y"]
    for wav in wavs:
        cmd.extend(["-i", str(wav)])
    filt = "".join(f"[{i}:a]" for i in range(len(wavs))) + f"concat=n={len(wavs)}:v=0:a=1[a]"
    cmd.extend(["-filter_complex", filt, "-map", "[a]", "-c:a", "pcm_s16le", str(out)])
    run_ffmpeg(cmd)
    return out


def encode(profile_name: str, art: dict, audio: Path) -> Path:
    profile = art["profile"]
    FINAL.mkdir(parents=True, exist_ok=True)
    out = FINAL / f"novixa-intro-v1r-{profile_name}.mp4"
    cmd = [
        "ffmpeg", "-y",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{profile['w']}x{profile['h']}", "-r", str(FPS), "-i", "-",
        "-i", str(audio),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-profile:v", "high", "-crf", "18", "-preset", "medium",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
        "-t", str(TOTAL), "-movflags", "+faststart", str(out),
    ]
    log_path = QA / f"ffmpeg-{profile_name}.log"
    log_file = log_path.open("w", encoding="utf-8")
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stderr=log_file)
    assert proc.stdin is not None
    frames = int(TOTAL * FPS)
    qa: list[str] = []
    try:
        for index in range(frames):
            frame = render_frame(index / FPS, art, qa if index % 15 == 0 else None)
            proc.stdin.write(frame.convert("RGB").tobytes())
            if index % 150 == 0:
                print(f"{profile_name} {index}/{frames}", flush=True)
    finally:
        proc.stdin.close()
        code = proc.wait()
        log_file.close()
    if code != 0:
        sys.stderr.write(log_path.read_text(encoding="utf-8", errors="replace")[-4000:])
        raise SystemExit(code)
    (QA / f"safe-{profile_name}.txt").write_text("\n".join(qa) or "none", encoding="utf-8")
    return out


def preview(profile_name: str, art: dict) -> None:
    QA.mkdir(parents=True, exist_ok=True)
    times = [0.45, 1.1, 2.4, 6.4, 8.6, 11.4, 13.2, 16.6, 18.4, 21.2, 25.0, 27.2, 29.8, 33.2, 38.2, 42.2, 46.5, 52.4]
    for t in times:
        frame = render_frame(t, art)
        path = QA / f"preview-{profile_name}-{t:.1f}.png"
        frame.convert("RGB").save(path)
        print(path.name, flush=True)


def main() -> None:
    mode = sys.argv[1] if len(sys.argv) > 1 else "preview"
    hits = load_hits()
    QA.mkdir(parents=True, exist_ok=True)
    if mode == "audio":
        print(build_audio())
        return
    if mode == "preview":
        name = sys.argv[2] if len(sys.argv) > 2 else "9x16"
        preview(name, build_art(PROFILES[name], hits))
        return
    if mode == "encode":
        audio = VOICE / "master.wav"
        if not audio.exists():
            audio = build_audio()
        name = sys.argv[2]
        encode(name, build_art(PROFILES[name], hits), audio)
        return
    raise SystemExit("use preview, audio, or encode")


if __name__ == "__main__":
    main()
