"""Assemble the 15s POC 002 proof from stills. Camera moves are crop animation, not character animation."""

from __future__ import annotations

import json
import subprocess
import wave
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
JOB = ROOT / "job"
FRAMES = JOB / "frames"
W, H = 1080, 1920
FPS = 30
TARGET = 15.0

ASSETS = ROOT / "assets"
SCENE1 = ASSETS / "scene1-closed.jpg"
SCENE2 = ASSETS / "scene2-ledger.jpg"
SCENE3 = ASSETS / "scene3-shelf.jpg"
DASH = Path(r"E:\KitPlatform\docs\novixa\launch\DOC-008\assets\screenshots\01-dashboard.png")
LOGO = Path(r"E:\KitPlatform\docs\novixa\assets\brand\novixa-logo.png")
FONT = Path(r"C:\Windows\Fonts\segoeuib.ttf")
FONT_R = Path(r"C:\Windows\Fonts\segoeui.ttf")

LINES = [
    ("close_a", 0.70),
    ("close_b", 0.90),
    ("gap_a", 0.45),
    ("gap_b", 0.55),
    ("see", 0.20),
]
LEAD = 0.40


def run(args: list[str]) -> None:
    completed = subprocess.run(args, check=False, capture_output=True, text=True)
    if completed.returncode != 0:
        raise SystemExit((completed.stderr or completed.stdout or "ffmpeg failed")[-2000:])


def wav_duration(path: Path) -> float:
    with wave.open(str(path), "rb") as handle:
        return handle.getnframes() / float(handle.getframerate())


def marks() -> dict[str, tuple[float, float]]:
    cursor = LEAD
    found: dict[str, tuple[float, float]] = {}
    for line_id, pause in LINES:
        duration = wav_duration(JOB / "voice-parts" / f"{line_id}.wav")
        found[line_id] = (cursor, cursor + duration)
        cursor += duration + pause
    return found


def cover(im: Image.Image, box: tuple[float, float, float, float]) -> Image.Image:
    x, y, cw, ch = box
    x = max(0, min(x, im.width - cw))
    y = max(0, min(y, im.height - ch))
    cropped = im.crop((int(x), int(y), int(x + cw), int(y + ch)))
    return cropped.resize((W, H), Image.Resampling.LANCZOS)


def window(im: Image.Image, t: float, kind: str) -> tuple[float, float, float, float]:
    w, h = im.size
    t = max(0.0, min(1.0, t))
    if kind == "push":
        z = 1.0 + 0.05 * t
        cw, ch = w / z, h / z
        x = (w - cw) * 0.50
        y = (h - ch) * 0.36
    elif kind == "drop":
        z = 1.34
        cw, ch = w / z, h / z
        x = (w - cw) * 0.46
        y = (h - ch) * t
    elif kind == "shelf":
        z = 1.30
        cw, ch = w / z, h / z
        x = (w - cw) * (0.02 + 0.55 * t)
        y = (h - ch) * (0.78 - 0.62 * t)
    else:
        cw, ch, x, y = w, h, 0, 0
    return x, y, cw, ch


def dashboard_plate() -> Image.Image:
    src = Image.open(DASH).convert("RGB")
    plate = Image.new("RGB", (W, H), (244, 241, 234))
    revenue = src.crop((560, 424, 1200, 688))
    chips = src.crop((560, 1508, 1490, 1728))
    width = 960
    blocks = []
    for crop in (revenue, chips):
        height = int(crop.height * (width / crop.width))
        blocks.append(crop.resize((width, height), Image.Resampling.LANCZOS))
    font = ImageFont.truetype(str(FONT), 40)
    small = ImageFont.truetype(str(FONT_R), 28)
    label = "Giao diện thật · dữ liệu demo"
    gap = 36
    block_h = 150 + blocks[0].height + gap + blocks[1].height + 48 + 108
    top = max(80, (H - block_h) // 2)
    draw = ImageDraw.Draw(plate)
    draw.text((70, top), "Tổng quan", font=font, fill=(22, 36, 58))
    draw.text((70, top + 52), "DEMO_PHARMACY", font=small, fill=(47, 111, 214))
    y = top + 130
    x = (W - width) // 2
    plate.paste(blocks[0], (x, y))
    y += blocks[0].height + gap
    plate.paste(blocks[1], (x, y))
    y += blocks[1].height + 48
    tw = draw.textlength(label, font=font)
    draw.rounded_rectangle((70, y, 1010, y + 108), 18, fill=(28, 42, 64))
    draw.text(((W - tw) / 2, y + 28), label, font=font, fill=(255, 248, 236))
    return plate


def logo_plate() -> Image.Image:
    plate = Image.new("RGB", (W, H), (12, 28, 48))
    logo = Image.open(LOGO).convert("RGBA")
    pixels = logo.getdata()
    cleaned = []
    for r, g, b, a in pixels:
        if r > 242 and g > 242 and b > 242:
            cleaned.append((r, g, b, 0))
        else:
            cleaned.append((r, g, b, a))
    logo.putdata(cleaned)
    bbox = logo.getbbox()
    if bbox:
        logo = logo.crop(bbox)
    logo.thumbnail((760, 760), Image.Resampling.LANCZOS)
    x = (W - logo.width) // 2
    y = (H - logo.height) // 2 - 40
    plate.paste(logo, (x, y), logo)
    return plate


def build() -> None:
    if FRAMES.exists():
        for old in FRAMES.glob("f*.png"):
            old.unlink()
    FRAMES.mkdir(parents=True, exist_ok=True)
    timing = marks()
    voice_len = wav_duration(JOB / "voice.wav")
    duration = TARGET if voice_len <= TARGET else voice_len
    nframes = int(round(duration * FPS))
    s1 = Image.open(SCENE1).convert("RGB")
    s2 = Image.open(SCENE2).convert("RGB")
    s3 = Image.open(SCENE3).convert("RGB")
    dash = dashboard_plate()
    logo = logo_plate()
    see_start = timing["see"][0]
    logo_at = 13.70

    def shot_at(second: float) -> Image.Image:
        if second < timing["gap_a"][0]:
            span = timing["gap_a"][0]
            t = second / span if span else 0
            return cover(s1, window(s1, t, "push"))
        if second < timing["gap_b"][0]:
            a, b = timing["gap_a"]
            t = (second - a) / (b - a) if b > a else 0
            # Ledger shot starts at the stock clause and continues through expiry words.
            return cover(s2, window(s2, t, "drop"))
        if second < see_start:
            a = timing["gap_b"][0]
            t = (second - a) / (see_start - a) if see_start > a else 0
            return cover(s3, window(s3, t, "shelf"))
        if second < logo_at:
            return dash
        return logo

    for index in range(nframes):
        second = index / FPS
        shot_at(second).save(FRAMES / f"f{index:05d}.png")
        if index % 60 == 0:
            print(f"frame {index}/{nframes}", flush=True)

    voice = JOB / "voice-padded.wav"
    pad = max(0.0, duration - voice_len)
    run(
        [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(JOB / "voice.wav"),
            "-af", f"apad=pad_dur={pad:.3f},volume=2dB",
            "-c:a", "pcm_s16le", str(voice),
        ]
    )
    out = JOB / "output.mp4"
    if out.exists():
        out.unlink()
    run(
        [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-framerate", str(FPS), "-i", str(FRAMES / "f%05d.png"),
            "-i", str(voice),
            "-t", f"{duration:.3f}",
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-preset", "veryfast", "-crf", "18",
            "-c:a", "aac", "-b:a", "160k", "-ar", "48000", "-ac", "2",
            "-movflags", "+faststart",
            str(out),
        ]
    )
    (JOB / "timeline.json").write_text(
        json.dumps(
            {
                "fps": FPS,
                "durationSec": duration,
                "voiceSec": voice_len,
                "logoAtSec": logo_at,
                "lines": {key: {"start": a, "end": b} for key, (a, b) in timing.items()},
                "motion": {
                    "scene1": "simulated slow push-in on a still",
                    "scene2": "simulated downward pan on a still",
                    "scene3": "simulated rise from the box toward the face on a still",
                    "scene4": "hard cut, real dashboard hold, then hard cut to logo",
                    "characterAnimation": False,
                },
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )
    print(f"wrote {out} duration {duration:.3f}")


if __name__ == "__main__":
    build()
