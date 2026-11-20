"""Scene 4 motion-style proof. One continuous layout. Does not replace V1 or V1r."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent
SHOTS = Path(r"E:\KitPlatform\docs\novixa\launch\DOC-008\assets\screenshots")
VOICE = Path(r"E:\KitPlatform\proof\novixa-intro-v1\voice\s4.mp3")
FONT = Path(r"C:\Windows\Fonts\segoeuib.ttf")
FINAL = ROOT / "final" / "scene4-motion-proof.mp4"
QA = ROOT / "qa"
W, H, FPS = 1080, 1920, 30
DURATION = 12.0
PAPER = (244, 247, 251, 255)
INK = (14, 28, 51, 255)
TEAL = (20, 122, 104, 255)
WHITE = (255, 255, 255, 255)
SHADOW = 22


def ease(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3 - 2 * value)


def span(t: float, start: float, end: float) -> float:
    if end <= start:
        return 1.0 if t >= end else 0.0
    return ease((t - start) / (end - start))


def lerp(a: float, b: float, amount: float) -> float:
    return a + (b - a) * amount


def font(size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONT), size)


def text_sprite(text: str, size: int, fill: tuple[int, int, int, int]) -> Image.Image:
    face = font(size)
    probe = ImageDraw.Draw(Image.new("RGBA", (4, 4)))
    box = probe.textbbox((0, 0), text, font=face)
    image = Image.new("RGBA", (box[2] - box[0] + 8, box[3] - box[1] + 8), (0, 0, 0, 0))
    ImageDraw.Draw(image).text((4 - box[0], 4 - box[1]), text, font=face, fill=fill)
    return image


def round_clip(image: Image.Image, radius: int) -> Image.Image:
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, image.width - 1, image.height - 1), radius=radius, fill=255)
    clipped = image.copy()
    clipped.putalpha(mask)
    return clipped


def demo_chip() -> Image.Image:
    label = text_sprite("Dữ liệu minh họa", 22, TEAL)
    chip = Image.new("RGBA", (label.width + 24, label.height + 12), (0, 0, 0, 0))
    ImageDraw.Draw(chip).rounded_rectangle((0, 0, chip.width - 1, chip.height - 1), radius=chip.height // 2, fill=(226, 242, 237, 255))
    chip.alpha_composite(label, (12, 6))
    return chip


def shadowed(card: Image.Image, radius: int) -> Image.Image:
    canvas = Image.new("RGBA", (card.width + SHADOW * 2, card.height + SHADOW * 2), (0, 0, 0, 0))
    blur = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(blur).rounded_rectangle(
        (SHADOW + 4, SHADOW + 12, SHADOW + card.width - 4, SHADOW + card.height + 6),
        radius=radius,
        fill=(14, 28, 51, 50),
    )
    canvas.alpha_composite(blur.filter(ImageFilter.GaussianBlur(10)))
    canvas.alpha_composite(card, (SHADOW, SHADOW))
    return canvas


def ui_card(image: Image.Image, inner_w: int) -> Image.Image:
    fitted = round_clip(
        image.resize((inner_w, max(1, round(image.height * inner_w / image.width))), Image.Resampling.LANCZOS),
        16,
    )
    chip = demo_chip()
    width = max(fitted.width, chip.width + 8) + 28
    height = 16 + chip.height + 12 + fitted.height + 16
    card = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    ImageDraw.Draw(card).rounded_rectangle((0, 0, width - 1, height - 1), radius=24, fill=WHITE)
    card.alpha_composite(chip, (width - chip.width - 14, 14))
    card.alpha_composite(fitted, ((width - fitted.width) // 2, 16 + chip.height + 12))
    return shadowed(card, 24)


def phone_shot(image: Image.Image, screen_w: int) -> Image.Image:
    screen = round_clip(
        image.resize((screen_w, max(1, round(image.height * screen_w / image.width))), Image.Resampling.LANCZOS),
        28,
    )
    bezel = 14
    body_w, body_h = screen.width + bezel * 2, screen.height + bezel * 2
    body = Image.new("RGBA", (body_w, body_h), (0, 0, 0, 0))
    ImageDraw.Draw(body).rounded_rectangle((0, 0, body_w - 1, body_h - 1), radius=36, fill=(17, 28, 48, 255))
    body.alpha_composite(screen, (bezel, bezel))
    chip = demo_chip()
    width = max(body_w, chip.width)
    height = chip.height + 12 + body_h
    card = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    card.alpha_composite(chip, ((width - chip.width) // 2, 0))
    card.alpha_composite(body, ((width - body_w) // 2, chip.height + 12))
    return shadowed(card, 36)


def blit(base: Image.Image, sprite: Image.Image, xy: tuple[float, float], opacity: float = 1, reveal: float = 1) -> None:
    if sprite is None or opacity <= 0.02 or reveal <= 0.01:
        return
    layer = sprite
    if reveal < 0.995:
        layer = sprite.crop((0, 0, max(1, int(sprite.width * reveal)), sprite.height))
    if opacity < 0.985:
        layer = layer.copy()
        layer.putalpha(layer.getchannel("A").point(lambda p: int(p * opacity)))
    x, y = int(xy[0]), int(xy[1])
    src_x, src_y = max(0, -x), max(0, -y)
    dst_x, dst_y = max(0, x), max(0, y)
    width = min(layer.width - src_x, base.width - dst_x)
    height = min(layer.height - src_y, base.height - dst_y)
    if width <= 0 or height <= 0:
        return
    base.alpha_composite(layer.crop((src_x, src_y, src_x + width, src_y + height)), (dst_x, dst_y))


def scaled(image: Image.Image, factor: float) -> Image.Image | None:
    if factor <= 0.04:
        return None
    size = (max(1, int(image.width * factor)), max(1, int(image.height * factor)))
    return image.resize(size, Image.Resampling.LANCZOS)


def load_art() -> dict:
    pos = Image.open(SHOTS / "10-staff-pos.png").convert("RGBA")
    inv = Image.open(SHOTS / "04-inventory-stock.png").convert("RGBA").crop((540, 580, 1500, 1320))
    page = Image.open(SHOTS / "08-reports-home.png").convert("RGBA")
    left = page.crop((548, 888, 1292, 1088))
    right = page.crop((1296, 888, 2048, 1088))
    return {
        "brand": text_sprite("NOVIXA", 92, INK),
        "phone": phone_shot(pos, 430),
        "inv": ui_card(inv, 470),
        "rep_a": ui_card(left, 430),
        "rep_b": ui_card(right, 430),
    }


def board_geometry(art: dict) -> dict:
    phone, inv, rep_a, rep_b, brand = art["phone"], art["inv"], art["rep_a"], art["rep_b"], art["brand"]
    dock = 0.62
    phone_w, phone_h = phone.width * dock, phone.height * dock
    gap = 22
    stack_gap = 16
    col_w = max(inv.width, rep_a.width, rep_b.width)
    board_w = phone_w + gap + col_w
    stack_h = inv.height + stack_gap + rep_a.height + stack_gap + rep_b.height
    board_h = brand.height + 28 + max(phone_h, stack_h)
    x0 = (W - board_w) / 2
    y0 = 170 + max(0, ((H - 220 - 170) - board_h) / 2)
    phone_at = (x0, y0 + brand.height + 28)
    inv_at = (phone_at[0] + phone_w + gap, phone_at[1])
    rep_a_at = (inv_at[0] + (inv.width - rep_a.width) / 2, inv_at[1] + inv.height + stack_gap)
    rep_b_at = (inv_at[0] + (inv.width - rep_b.width) / 2, rep_a_at[1] + rep_a.height + stack_gap)
    return {
        "dock": dock,
        "brand": ((W - brand.width) / 2, y0),
        "phone": phone_at,
        "inv": inv_at,
        "rep_a": rep_a_at,
        "rep_b": rep_b_at,
        "phone_box": (phone_at[0], phone_at[1], phone_w, phone_h),
        "inv_box": (inv_at[0], inv_at[1], inv.width, inv.height),
        "rep_a_box": (rep_a_at[0], rep_a_at[1], rep_a.width, rep_a.height),
        "rep_b_box": (rep_b_at[0], rep_b_at[1], rep_b.width, rep_b.height),
        "center": (W / 2, H * 0.36),
    }


def brand_state(t: float, art: dict, geo: dict) -> tuple[tuple[float, float], float]:
    cx, cy = geo["center"]
    home = geo["brand"]
    appear = span(t, 0.12, 0.85)
    move = span(t, 0.85, 1.55)
    scale = lerp(0.28, 1.0, appear)
    x = lerp(cx - art["brand"].width * scale / 2, home[0], move)
    y = lerp(cy - art["brand"].height * scale / 2, home[1], move)
    return (x, y), scale


def phone_state(t: float, art: dict, geo: dict) -> tuple[tuple[float, float], float]:
    width = art["phone"].width
    dock_x, dock_y = geo["phone"]
    born = span(t, 1.40, 2.55)
    shrink = span(t, 4.70, 6.35)
    if t < 4.70:
        scale = lerp(0.12, 1.0, born)
        x = (W - width * scale) / 2
    else:
        scale = lerp(1.0, geo["dock"], shrink)
        x = lerp((W - width) / 2, dock_x, shrink)
    return (x, dock_y), scale


def placed(sprite: Image.Image, xy: tuple[float, float], factor: float) -> tuple[float, float, float, float]:
    return (xy[0], xy[1], sprite.width * factor, sprite.height * factor)


def stage_panel(frame: Image.Image, geo: dict, t: float) -> None:
    amount = span(t, 8.25, 8.80)
    if amount <= 0:
        return
    boxes = [geo["phone_box"], geo["inv_box"], geo["rep_a_box"], geo["rep_b_box"]]
    left = min(box[0] for box in boxes) - 22
    top = geo["brand"][1] - 28
    right = max(box[0] + box[2] for box in boxes) + 22
    bottom = max(box[1] + box[3] for box in boxes) + 22
    panel = Image.new("RGBA", frame.size, (0, 0, 0, 0))
    ImageDraw.Draw(panel).rounded_rectangle((left, top, right, bottom), radius=36, fill=(255, 255, 255, int(255 * amount)))
    frame.alpha_composite(panel)


def render_frame(t: float, art: dict, geo: dict) -> Image.Image:
    frame = Image.new("RGBA", (W, H), PAPER)
    stage_panel(frame, geo, t)
    phone_xy, phone_scale = phone_state(t, art, geo)
    phone_box = placed(art["phone"], phone_xy, phone_scale) if t >= 1.40 else None

    inv_box = None
    if t >= 4.85:
        settle = span(t, 4.85, 6.35)
        end = geo["inv"]
        inv_box = (lerp(end[0] + 48, end[0], settle), end[1], art["inv"].width, art["inv"].height)

    def nest_y(height: float) -> float:
        return inv_box[1] + inv_box[3] - height + 36

    rep_b = None
    if t >= 6.15 and inv_box is not None:
        drop = span(t, 6.15, 7.35)
        end = geo["rep_b"]
        rep_b = (end[0], lerp(nest_y(art["rep_b"].height), end[1], drop), art["rep_b"].width, art["rep_b"].height)

    rep_a = None
    if t >= 7.20 and inv_box is not None:
        drop = span(t, 7.20, 8.15)
        end = geo["rep_a"]
        rep_a = (end[0], lerp(nest_y(art["rep_a"].height), end[1], drop), art["rep_a"].width, art["rep_a"].height)

    if rep_b is not None:
        blit(frame, art["rep_b"], (rep_b[0], rep_b[1]))
    if rep_a is not None:
        blit(frame, art["rep_a"], (rep_a[0], rep_a[1]))
    if inv_box is not None:
        blit(frame, art["inv"], (inv_box[0], inv_box[1]), span(t, 4.85, 5.15))
    if phone_box is not None:
        phone = scaled(art["phone"], phone_scale)
        if phone is not None:
            blit(frame, phone, phone_xy, span(t, 1.40, 1.75))

    brand_xy, brand_scale = brand_state(t, art, geo)
    brand = scaled(art["brand"], brand_scale)
    if brand is not None:
        blit(frame, brand, brand_xy, span(t, 0.12, 0.4))
    return frame


def build_audio() -> Path:
    QA.mkdir(parents=True, exist_ok=True)
    wav = ROOT / "voice-scene4.wav"
    done = subprocess.run(
        ["ffmpeg", "-y", "-i", str(VOICE), "-af", f"apad=whole_dur={DURATION},atrim=0:{DURATION}", "-ar", "48000", "-ac", "2", str(wav)],
        capture_output=True,
    )
    if done.returncode != 0:
        sys.stderr.write(done.stderr.decode("utf-8", "replace")[-2000:])
        raise SystemExit(done.returncode)
    return wav


def encode(art: dict, geo: dict, audio: Path) -> None:
    FINAL.parent.mkdir(parents=True, exist_ok=True)
    QA.mkdir(parents=True, exist_ok=True)
    cmd = [
        "ffmpeg", "-y",
        "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
        "-i", str(audio),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-profile:v", "high", "-crf", "18", "-preset", "medium",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
        "-t", str(DURATION), "-movflags", "+faststart", str(FINAL),
    ]
    log = (QA / "ffmpeg.log").open("w", encoding="utf-8")
    proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stderr=log)
    assert proc.stdin is not None
    frames = int(DURATION * FPS)
    shots = {0.7, 3.6, 5.5, 6.7, 7.7, 8.4, 11.2}
    try:
        for index in range(frames):
            t = index / FPS
            frame = render_frame(t, art, geo)
            if any(abs(t - mark) < 0.02 for mark in shots):
                frame.convert("RGB").save(QA / f"frame-{t:.1f}.png")
            proc.stdin.write(frame.convert("RGB").tobytes())
    finally:
        proc.stdin.close()
        code = proc.wait()
        log.close()
    if code != 0:
        sys.stderr.write((QA / "ffmpeg.log").read_text(encoding="utf-8", errors="replace")[-3000:])
        raise SystemExit(code)


def main() -> None:
    art = load_art()
    geo = board_geometry(art)
    if len(sys.argv) > 1 and sys.argv[1] == "preview":
        QA.mkdir(parents=True, exist_ok=True)
        for t in (0.7, 3.6, 5.5, 6.7, 7.7, 8.4, 11.2):
            render_frame(t, art, geo).convert("RGB").save(QA / f"preview-{t:.1f}.png")
            print(t, flush=True)
        return
    encode(art, geo, build_audio())
    print(FINAL, flush=True)


if __name__ == "__main__":
    main()
