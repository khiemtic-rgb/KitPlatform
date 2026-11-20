"""15-second NOVIXA motion pilot. Local Pillow frames plus the existing FFmpeg.

Does not write Production OS artifacts and does not modify marketing V1 or V2.
"""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
VOICE = ROOT / "assets" / "voice"
FINAL = ROOT / "final" / "novixa-motion-pilot-001.mp4"
LOGO_SRC = Path(r"E:\KitPlatform\docs\novixa\assets\brand\novixa-logo.png")
STOCK_SRC = ROOT / "assets" / "stock-crop.png"
FONT_B = Path(r"C:\Windows\Fonts\segoeuib.ttf")
FONT_R = Path(r"C:\Windows\Fonts\segoeui.ttf")
W, H, FPS = 1080, 1920, 30
FRAMES = 15 * FPS
PAPER = (244, 247, 251, 255)
INK = (14, 28, 51, 255)
TEAL = (31, 138, 112, 255)
CORAL = (186, 72, 54, 255)
WHITE = (255, 255, 255, 255)


def ease(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3 - 2 * value)


def span(t: float, start: float, end: float) -> float:
    return ease((t - start) / (end - start))


def text_sprite(text: str, size: int, fill: tuple[int, int, int, int], bold: bool = False) -> Image.Image:
    font = ImageFont.truetype(str(FONT_B if bold else FONT_R), size)
    probe = ImageDraw.Draw(Image.new("RGBA", (4, 4)))
    box = probe.textbbox((0, 0), text, font=font)
    image = Image.new("RGBA", (box[2] - box[0] + 8, box[3] - box[1] + 8), (0, 0, 0, 0))
    ImageDraw.Draw(image).text((4 - box[0], 4 - box[1]), text, font=font, fill=fill)
    return image


def blit(base: Image.Image, sprite: Image.Image, xy: tuple[float, float], opacity: float = 1) -> None:
    if opacity <= 0.01:
        return
    x, y = int(xy[0]), int(xy[1])
    if x >= W or y >= H or x + sprite.width <= 0 or y + sprite.height <= 0:
        return
    layer = sprite
    if opacity < 0.99:
        layer = sprite.copy()
        alpha = layer.getchannel("A").point(lambda p: int(p * opacity))
        layer.putalpha(alpha)
    base.alpha_composite(layer, (x, y))


def dewhite(path: Path, width: int) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            if red > 242 and green > 242 and blue > 242:
                pixels[x, y] = (red, green, blue, 0)
    height = round(image.height * (width / image.width))
    return image.resize((width, height), Image.Resampling.LANCZOS)


def card(title: str) -> Image.Image:
    image = Image.new("RGBA", (860, 200), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 10, 860, 200), radius=28, fill=(14, 28, 51, 28))
    draw.rounded_rectangle((0, 0, 860, 190), radius=28, fill=WHITE)
    draw.rounded_rectangle((0, 0, 18, 190), radius=8, fill=TEAL)
    label = text_sprite(title, 64, INK, bold=True)
    image.alpha_composite(label, (56, (190 - label.height) // 2))
    return image


def ui_board() -> Image.Image:
    crop = Image.open(STOCK_SRC).convert("RGBA")
    fitted = crop.resize((960, round(crop.height * (960 / crop.width))), Image.Resampling.LANCZOS)
    board = Image.new("RGBA", (1000, fitted.height + 132), WHITE)
    draw = ImageDraw.Draw(board)
    draw.rectangle((0, 0, 1000, 108), fill=INK)
    label = text_sprite("Dữ liệu minh họa", 40, WHITE, bold=True)
    board.alpha_composite(label, ((1000 - label.width) // 2, (108 - label.height) // 2))
    board.alpha_composite(fitted, (20, 120))
    return board


def build_sprites() -> dict:
    return {
        "thu": text_sprite("Doanh thu", 96, INK, bold=True),
        "cao": text_sprite("cao", 168, CORAL, bold=True),
        "line": text_sprite("chưa chắc đã kinh doanh hiệu quả", 46, TEAL, bold=True),
        "cards": [card("HÀNG TỒN"), card("GIÁ VỐN"), card("CHI PHÍ")],
        "ui": ui_board(),
        "logo": dewhite(LOGO_SRC, 560),
        "tag": text_sprite("Nền tảng vận hành nhà thuốc thông minh", 40, INK),
        "web": text_sprite("novixa.vn", 40, TEAL, bold=True),
    }


def render_frame(t: float, art: dict) -> Image.Image:
    frame = Image.new("RGBA", (W, H), PAPER)
    hook_opacity = 1 - span(t, 2.72, 3.25)
    hook_dy = -70 * span(t, 2.72, 3.25)
    if hook_opacity > 0.02 and t < 3.4:
        thu = art["thu"]
        arrived = span(t, 0.12, 0.85)
        blit(frame, thu, ((W - thu.width) / 2 + (1 - arrived) * -90, 560 + hook_dy), hook_opacity * arrived)
        cao = art["cao"]
        grown = span(t, 0.62, 1.45)
        scale = 0.74 + 0.26 * grown
        cao_now = cao.resize((max(1, int(cao.width * scale)), max(1, int(cao.height * scale))), Image.Resampling.LANCZOS)
        blit(frame, cao_now, ((W - cao_now.width) / 2, 700 + hook_dy), hook_opacity * grown)
        rule = span(t, 1.25, 2.05)
        if rule > 0:
            width = int(520 * rule)
            overlay = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            ImageDraw.Draw(overlay).rectangle(((W - width) / 2, 940 + hook_dy, (W + width) / 2, 952 + hook_dy), fill=CORAL)
            blit(frame, overlay, (0, 0), hook_opacity)
        line = art["line"]
        shown = span(t, 1.7, 2.45)
        blit(frame, line, ((W - line.width) / 2, 990 + hook_dy), hook_opacity * shown)

    finals = [520, 810, 1100]
    enters = [(3.05, 4.05, -940, 110), (3.55, 4.55, 1160, 110), (4.15, 5.2, 110, 110)]
    settle = 36 * span(t, 5.35, 6.15)
    leave = span(t, 6.75, 7.4)
    for index, item in enumerate(art["cards"]):
        start, end, x0, x1 = enters[index]
        if t < start or t > 7.55:
            continue
        x = x0 + (x1 - x0) * span(t, start, end)
        y = finals[index] - settle - 360 * leave
        if index == 2:
            y = 1680 + (finals[2] - 1680) * span(t, start, end) - settle - 360 * leave
        blit(frame, item, (x, y), 1 - leave)

    board = art["ui"]
    if 7.0 <= t <= 11.35:
        y0, y1 = 1980, (H - board.height) // 2
        y = y0 + (y1 - y0) * span(t, 7.05, 8.15)
        y -= 520 * span(t, 10.7, 11.35)
        blit(frame, board, ((W - board.width) / 2, y), 1 - span(t, 10.85, 11.3))
        if 8.15 < t < 10.85:
            stroke = Image.new("RGBA", board.size, (0, 0, 0, 0))
            ImageDraw.Draw(stroke).rounded_rectangle(
                (14, 120, board.width - 14, board.height - 16),
                radius=18, outline=TEAL, width=6,
            )
            blit(frame, stroke, ((W - board.width) / 2, y), span(t, 8.2, 8.7) * (1 - span(t, 10.85, 11.3)))

    if t >= 11.0:
        arrive = span(t, 11.05, 12.05)
        logo = art["logo"]
        scaled = logo.resize((max(1, int(logo.width * (0.86 + 0.14 * arrive))), max(1, int(logo.height * (0.86 + 0.14 * arrive)))), Image.Resampling.LANCZOS)
        blit(frame, scaled, ((W - scaled.width) / 2, 500 + (1 - arrive) * 40), arrive)
        blit(frame, art["tag"], ((W - art["tag"].width) / 2, 1180), span(t, 11.55, 12.25))
        blit(frame, art["web"], ((W - art["web"].width) / 2, 1280), span(t, 11.9, 12.5))
    return frame.convert("RGB")


def build_audio(dest: Path) -> None:
    source = VOICE / "full-48.mp3"
    subprocess.run(
        ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-i", str(source),
         "-filter_complex",
         "[0:a]atrim=start=0.30:end=15.45,asetpts=PTS-STARTPTS,atempo=1.055,apad=whole_dur=15,atrim=0:15,aresample=48000,aformat=channel_layouts=stereo[a]",
         "-map", "[a]", "-c:a", "pcm_s16le", str(dest)],
        check=True,
    )


def encode(art: dict) -> None:
    FINAL.parent.mkdir(parents=True, exist_ok=True)
    wav = ROOT / "renders" / "voice.wav"
    wav.parent.mkdir(parents=True, exist_ok=True)
    build_audio(wav)
    proc = subprocess.Popen(
        ["ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
         "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
         "-i", str(wav), "-t", "15",
         "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", "-movflags", "+faststart",
         str(FINAL)],
        stdin=subprocess.PIPE,
    )
    assert proc.stdin is not None
    for index in range(FRAMES):
        proc.stdin.write(render_frame(index / FPS, art).tobytes())
        if index % 90 == 0:
            print(f"frame {index}", flush=True)
    proc.stdin.close()
    if proc.wait() != 0:
        raise SystemExit("ffmpeg encode failed")
    print(FINAL)


def preview(art: dict) -> None:
    dest = ROOT / "renders" / "preview"
    dest.mkdir(parents=True, exist_ok=True)
    for t in (0.8, 1.8, 2.4, 4.4, 6.3, 8.8, 12.6, 14.4):
        render_frame(t, art).save(dest / f"t-{t:.1f}.png")
        print(t, flush=True)


def main() -> None:
    art = build_sprites()
    if len(sys.argv) > 1 and sys.argv[1] == "preview":
        preview(art)
    else:
        encode(art)


if __name__ == "__main__":
    main()
