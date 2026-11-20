"""Compose the 9:16 frame sequence for NOVIXA video POC 001.

Reads timeline.json from speak.py. Copies UI screenshots; does not modify originals.
No paid provider is called.
"""

from __future__ import annotations

import json
import math
import shutil
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
JOB = ROOT / "job"
FRAMES = JOB / "frames"
ASSETS = ROOT / "assets"
SRC_UI = Path(r"E:\KitPlatform\docs\novixa\launch\DOC-008\assets\screenshots")
SRC_LOGO = Path(r"E:\KitPlatform\docs\novixa\assets\brand\novixa-logo.png")

W, H = 1080, 1920
FPS = 12
DURATION = 60
FRAME_COUNT = FPS * DURATION

NAVY = (8, 22, 42, 255)
BLUE = (37, 112, 214, 255)
GREEN = (18, 156, 112, 255)
WHITE = (248, 250, 252, 255)
MUTED = (176, 198, 216, 255)
AMBER = (232, 176, 84, 255)
INK = (232, 240, 248, 255)

FONT_BOLD = ImageFont.truetype(r"C:\Windows\Fonts\segoeuib.ttf", 10)
FONT_REG = ImageFont.truetype(r"C:\Windows\Fonts\segoeui.ttf", 10)


def font(kind: str, size: int) -> ImageFont.FreeTypeFont:
    path = r"C:\Windows\Fonts\segoeuib.ttf" if kind == "bold" else r"C:\Windows\Fonts\segoeui.ttf"
    return ImageFont.truetype(path, size)


def ease(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def alpha_of(t: float, start: float, span: float = 0.45) -> float:
    return ease((t - start) / span)


def copy_assets() -> None:
    ASSETS.mkdir(parents=True, exist_ok=True)
    for name in ("01-dashboard.png", "04-inventory-stock.png", "08-reports-home.png"):
        dest = ASSETS / name
        if not dest.exists():
            shutil.copy2(SRC_UI / name, dest)
    logo = ASSETS / "novixa-logo.png"
    if not logo.exists():
        shutil.copy2(SRC_LOGO, logo)


def trim_white(path: Path) -> Image.Image:
    image = Image.open(path).convert("RGBA")
    pixels = []
    for red, green, blue, alpha in image.getdata():
        if red > 242 and green > 242 and blue > 242:
            pixels.append((red, green, blue, 0))
        else:
            pixels.append((red, green, blue, alpha))
    image.putdata(pixels)
    box = image.getchannel("A").getbbox()
    return image.crop(box) if box else image


def load_crops() -> dict[str, Image.Image]:
    dash = Image.open(ASSETS / "01-dashboard.png").convert("RGB")
    reports = Image.open(ASSETS / "08-reports-home.png").convert("RGB")
    return {
        "revenue": dash.crop((500, 430, 1240, 760)),
        "expiry": dash.crop((520, 1500, 1180, 1760)),
        "inventory": reports.crop((500, 860, 1280, 1140)),
        "shift": reports.crop((2060, 470, 2780, 650)),
    }


def make_background() -> Image.Image:
    strip = Image.new("RGB", (1, 512))
    pixels = strip.load()
    for y in range(512):
        blend = y / 511
        pixels[0, y] = (
            int(8 + 12 * blend),
            int(18 + 30 * blend),
            int(36 + 42 * blend),
        )
    return strip.resize((W, H), Image.Resampling.BICUBIC).convert("RGBA")


def wrap(text: str, face: ImageFont.FreeTypeFont, width: int) -> list[str]:
    lines: list[str] = []
    current = ""
    for word in text.split():
        trial = word if not current else current + " " + word
        if face.getlength(trial) <= width:
            current = trial
        else:
            if current:
                lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines[:3]


def draw_text(
    base: Image.Image,
    text: str,
    xy: tuple[int, int],
    face: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int, int],
    anchor: str = "lt",
) -> None:
    if fill[3] <= 0:
        return
    overlay = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(overlay).text(xy, text, font=face, fill=fill, anchor=anchor)
    base.alpha_composite(overlay)


def rounded(base: Image.Image, box: tuple[int, int, int, int], radius: int, fill: tuple[int, int, int, int]) -> None:
    overlay = Image.new("RGBA", base.size, (0, 0, 0, 0))
    ImageDraw.Draw(overlay).rounded_rectangle(box, radius=radius, fill=fill)
    base.alpha_composite(overlay)


def caption(base: Image.Image, text: str, opacity: float) -> None:
    if opacity <= 0.02 or not text:
        return
    face = font("regular", 42)
    lines = wrap(text, face, 900)
    top = 1628
    rounded(base, (48, top - 28, W - 48, top + 36 + 58 * len(lines)), 28, (4, 12, 24, int(190 * opacity)))
    for index, line in enumerate(lines):
        draw_text(base, line, (W // 2, top + index * 58), face, (255, 255, 255, int(255 * opacity)), "mt")


def progress(base: Image.Image, t: float) -> None:
    width = int((W - 144) * min(1.0, t / DURATION))
    rounded(base, (72, 1848, W - 72, 1858), 4, (255, 255, 255, 36))
    if width > 8:
        rounded(base, (72, 1848, 72 + width, 1858), 4, (18, 156, 112, 220))


def brand_mark(base: Image.Image, opacity: float = 1.0) -> None:
    draw_text(base, "NOVIXA", (W - 72, 118), font("bold", 28), (186, 214, 236, int(180 * opacity)), "rt")


def at(lines: list[dict], key: str) -> float:
    for line in lines:
        if line["id"] == key:
            return float(line["start"])
    return 0.0


def hook(base: Image.Image, t: float, active: str, lines: list[dict]) -> None:
    brand_mark(base)
    draw_text(base, "TÌNH HUỐNG", (72, 210), font("bold", 28), (18, 156, 112, 255))
    count_from = at(lines, "hook_a") + 0.1
    shown = 300 if t >= count_from + 1.2 else int(300 * ease(max(0.0, t - count_from) / 1.15))
    number_fill = WHITE if active != "hook_c" else (186, 204, 220, 255)
    draw_text(base, str(shown), (72, 430), font("bold", 210), number_fill)
    draw_text(base, "triệu mỗi tháng", (78, 680), font("regular", 54), MUTED)
    hook_b = at(lines, "hook_b")
    if t >= hook_b:
        pills = [("Đơn ra đều", hook_b), ("Tiền vào đều", hook_b + 1.15)]
        for label, start in pills:
            reveal = alpha_of(t, start, 0.4)
            y = 860 + pills.index((label, start)) * 150
            rounded(base, (72, y, 1008, y + 120), 28, (16, 40, 68, int(230 * reveal)))
            draw_text(base, label, (112, y + 60), font("bold", 48), (248, 250, 252, int(255 * reveal)), "lm")
    hook_c = at(lines, "hook_c")
    if t >= hook_c:
        reveal = alpha_of(t, hook_c, 0.5)
        draw_text(base, "Còn lại bao nhiêu?", (72, 1280), font("bold", 64), (232, 176, 84, int(255 * reveal)))


def problem(base: Image.Image, t: float, active: str, lines: list[dict]) -> None:
    brand_mark(base)
    draw_text(base, "DOANH THU", (72, 180), font("bold", 28), BLUE)
    draw_text(base, "Khách đã trả.", (72, 250), font("bold", 64), WHITE)
    rows = [
        ("01", "Trong kho", "Tiền đang bị giữ", "problem_b1", "problem_b1b"),
        ("02", "Sắp hết hạn", "Có thể không bán được", "problem_b2", "problem_b2b"),
        ("03", "Lệch quỹ", "Không khớp với sổ", "problem_b3", "problem_b3b"),
    ]
    for index, (num, label, note, start_key, note_key) in enumerate(rows):
        start = at(lines, start_key)
        reveal = alpha_of(t, start, 0.35)
        if reveal <= 0:
            continue
        y = 420 + index * 280
        hot = active.startswith(start_key)
        fill = (22, 58, 96, int(240 * reveal)) if hot else (14, 36, 62, int(210 * reveal))
        rounded(base, (72, y, 1008, y + 250), 32, fill)
        draw_text(base, num, (112, y + 78), font("bold", 40), (18, 156, 112, int(255 * reveal)), "lm")
        draw_text(base, label, (210, y + 78), font("bold", 54), (248, 250, 252, int(255 * reveal)), "lm")
        note_reveal = alpha_of(t, at(lines, note_key), 0.3)
        if note_reveal > 0:
            draw_text(base, note, (210, y + 168), font("regular", 36), (232, 176, 84, int(255 * note_reveal)), "lm")


def insight(base: Image.Image, t: float, lines: list[dict]) -> None:
    brand_mark(base)
    draw_text(base, "NHÌN CÙNG MỘT LÚC", (72, 210), font("bold", 28), AMBER)
    anchor = at(lines, "insight_b")
    lead = at(lines, "insight_a")
    columns = [
        ("Tiền bán ra", "Đã thu", anchor, BLUE),
        ("Giá trị hàng", "Đang giữ", anchor + 1.15, GREEN),
        ("Rủi ro quầy", "Cần xem", anchor + 2.3, AMBER),
    ]
    for index, (label, note, start, color) in enumerate(columns):
        reveal = alpha_of(t, start, 0.45) if t >= anchor else alpha_of(t, lead, 0.4) * 0.35
        x = 72 + index * 330
        y = 460 + int((1 - reveal) * 36)
        rounded(base, (x, y, x + 300, y + 460), 32, (14, 36, 62, int(230 * max(reveal, 0))))
        bar = Image.new("RGBA", base.size, (0, 0, 0, 0))
        ImageDraw.Draw(bar).rounded_rectangle((x, y, x + 300, y + 12), radius=6, fill=(*color[:3], int(255 * reveal)))
        base.alpha_composite(bar)
        face = font("bold", 46)
        wrapped = wrap(label, face, 240)
        for line_index, line in enumerate(wrapped):
            draw_text(base, line, (x + 150, y + 180 + line_index * 60), face, (248, 250, 252, int(255 * reveal)), "mm")
        draw_text(base, note, (x + 150, y + 340), font("regular", 32), (*color[:3], int(255 * reveal)), "mm")
    if t >= at(lines, "insight_c"):
        reveal = alpha_of(t, at(lines, "insight_c"), 0.35)
        draw_text(base, "Không hứa một số lãi.", (W // 2, 1040), font("bold", 42), (248, 250, 252, int(255 * reveal)), "mm")


def panel(base: Image.Image, crop: Image.Image, kicker: str, title: str, reveal: float) -> None:
    brand_mark(base, reveal)
    draw_text(base, kicker, (72, 190), font("bold", 28), (18, 156, 112, int(255 * reveal)))
    draw_text(base, title, (72, 250), font("bold", 52), (248, 250, 252, int(255 * reveal)))
    draw_text(base, "Giao diện thật  ·  dữ liệu demo", (72, 340), font("regular", 32), (232, 176, 84, int(255 * reveal)))
    max_w, max_h = 936, 980
    scale = min(max_w / crop.width, max_h / crop.height)
    resized = crop.resize((int(crop.width * scale), int(crop.height * scale)), Image.Resampling.LANCZOS)
    x = (W - resized.width) // 2
    y = 560
    pad = 28
    rounded(
        base,
        (x - pad, y - pad, x + resized.width + pad, y + resized.height + pad),
        36,
        (255, 255, 255, int(245 * reveal)),
    )
    if reveal > 0.02:
        pasted = resized.convert("RGBA")
        if reveal < 0.999:
            pasted.putalpha(int(255 * reveal))
        base.alpha_composite(pasted, (x, y))


def solution(base: Image.Image, t: float, active: str, lines: list[dict], crops: dict[str, Image.Image]) -> None:
    if active == "solution_d":
        brand_mark(base)
        draw_text(base, "ĐANG CÓ TRÊN SẢN PHẨM", (72, 240), font("bold", 28), GREEN)
        labels = ["Tổng quan", "Tồn kho và giá trị", "Báo cáo ca làm việc"]
        for index, label in enumerate(labels):
            y = 420 + index * 180
            rounded(base, (72, y, 1008, y + 150), 28, (14, 36, 62, 230))
            draw_text(base, label, (112, y + 75), font("bold", 46), WHITE, "lm")
        draw_text(base, "Không phải lời hứa lợi nhuận.", (72, 1020), font("regular", 36), AMBER)
        return
    if active == "solution_a":
        start = at(lines, "solution_a")
        end = next(float(line["end"]) for line in lines if line["id"] == "solution_a")
        crop = crops["revenue"] if t < start + (end - start) / 2 else crops["expiry"]
        title = "Doanh thu hôm nay" if crop is crops["revenue"] else "Hàng sắp hết hạn"
        panel(base, crop, "TỔNG QUAN", title, alpha_of(t, start, 0.3))
    elif active == "solution_b":
        panel(base, crops["inventory"], "BÁO CÁO TỒN KHO", "Giá trị theo giá vốn lô", alpha_of(t, at(lines, "solution_b"), 0.3))
    else:
        panel(base, crops["shift"], "BÁO CÁO CA", "Quỹ tiền mặt trong ca", alpha_of(t, at(lines, "solution_c"), 0.3))


def closing(base: Image.Image, t: float, lines: list[dict], logo: Image.Image) -> None:
    logo_at = at(lines, "cta_b")
    number_at = at(lines, "cta_a")
    reveal = alpha_of(t, logo_at, 0.4)
    if t < logo_at:
        earlier = alpha_of(t, number_at, 0.3)
        draw_text(base, "300 triệu", (W // 2, 700), font("bold", 120), (248, 250, 252, int(255 * earlier)), "mm")
        draw_text(base, "là doanh thu", (W // 2, 840), font("regular", 48), (176, 198, 216, int(255 * earlier)), "mm")
        return
    fitted = logo.copy()
    fitted.thumbnail((760, 760), Image.Resampling.LANCZOS)
    x = (W - fitted.width) // 2
    pasted = fitted.convert("RGBA")
    if reveal < 0.999:
        pasted.putalpha(int(255 * reveal))
    base.alpha_composite(pasted, (x, 340))
    draw_text(base, "Mở số liệu ra mà xem.", (W // 2, 1240), font("bold", 48), (248, 250, 252, int(255 * reveal)), "mm")


def active_line(lines: list[dict], t: float) -> dict | None:
    chosen = None
    for line in lines:
        if t + 0.02 >= float(line["start"]):
            chosen = line
    return chosen


def paint(t: float, lines: list[dict], crops: dict[str, Image.Image], logo: Image.Image, background: Image.Image) -> Image.Image:
    frame = background.copy()
    current = active_line(lines, t)
    name = current["id"] if current else "hook_a"
    spoken = str(current["text"]) if current and t >= float(current["start"]) - 0.05 else ""
    if name.startswith("hook"):
        hook(frame, t, name, lines)
    elif name.startswith("problem"):
        problem(frame, t, name, lines)
    elif name.startswith("insight"):
        insight(frame, t, lines)
    elif name.startswith("solution"):
        solution(frame, t, name, lines, crops)
    else:
        closing(frame, t, lines, logo)
    if spoken and not name.startswith("cta"):
        caption(frame, spoken, 1)
    progress(frame, t)
    return frame.convert("RGB")


def pad_voice(target: float) -> None:
    wav = JOB / "voice.wav"
    probe = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(wav)],
        check=True,
        capture_output=True,
        text=True,
    )
    current = float(probe.stdout.strip())
    pad = max(0.0, target - current)
    subprocess.run(
        [
            "ffmpeg",
            "-y",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(wav),
            "-af",
            f"volume=3dB,apad=pad_dur={pad:.3f}",
            "-c:a",
            "libmp3lame",
            "-b:a",
            "128k",
            str(JOB / "voice.mp3"),
        ],
        check=True,
    )


def video_seconds(lines: list[dict]) -> int:
    natural = math.ceil(max(float(line["end"]) for line in lines) + 0.4)
    if 54 <= natural <= 60:
        return 60
    return max(1, natural)


def write_storyboard(lines: list[dict], seconds: int) -> None:
    order = [("HOOK", "hook"), ("PROBLEM", "problem"), ("INSIGHT", "insight"), ("SOLUTION", "solution"), ("CTA", "cta")]
    bounds = [0]
    for _beat, prefix in order[1:]:
        start = min(float(line["start"]) for line in lines if str(line["id"]).startswith(prefix))
        bounds.append(max(bounds[-1] + 1, int(round(start))))
    bounds.append(seconds)
    scenes = []
    for index, (beat, prefix) in enumerate(order):
        text = " ".join(str(line["text"]) for line in lines if str(line["id"]).startswith(prefix))
        scenes.append({"beat": beat, "start": bounds[index], "end": bounds[index + 1], "text": text})
    (JOB / "storyboard.json").write_text(
        json.dumps({"fps": FPS, "durationSec": seconds, "scenes": scenes}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def main() -> None:
    global DURATION
    preview = "--preview" in sys.argv
    copy_assets()
    timeline = json.loads((JOB / "timeline.json").read_text(encoding="utf-8"))
    lines = timeline["lines"]
    DURATION = video_seconds(lines)
    crops = load_crops()
    logo = trim_white(ASSETS / "novixa-logo.png")
    background = make_background()
    write_storyboard(lines, DURATION)
    if not preview:
        pad_voice(DURATION)
        if FRAMES.exists():
            shutil.rmtree(FRAMES)
        FRAMES.mkdir(parents=True)
    else:
        (JOB / "preview").mkdir(parents=True, exist_ok=True)
    if preview:
        for line in lines:
            mark = (float(line["start"]) + float(line["end"])) / 2
            paint(mark, lines, crops, logo, background).save(JOB / "preview" / f"{line['id']}.jpg", quality=90)
        print("preview", len(lines), "duration", DURATION)
        return
    total = FPS * DURATION
    for index in range(total):
        paint(index / FPS, lines, crops, logo, background).save(FRAMES / f"f{index:05d}.png")
        if index % 60 == 0:
            print(f"frame {index}/{total}", flush=True)
    print("frames", total, "duration", DURATION)


if __name__ == "__main__":
    main()
