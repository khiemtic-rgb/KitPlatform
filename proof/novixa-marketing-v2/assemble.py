"""Local post assembly for NOVIXA marketing V2.

Cuts between distinct stills. It does not animate a character and it does not
write Production OS artifacts. V1 files are only read as copies.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
STILLS = ROOT / "assets" / "stills"
VOICE = ROOT / "assets" / "voice"
RENDERS = ROOT / "renders"
FINAL = ROOT / "final" / "novixa-marketing-v2.mp4"
FONT = Path(r"C:\Windows\Fonts\segoeui.ttf")
DASH = Path(r"E:\KitPlatform\docs\novixa\launch\DOC-008\assets\screenshots\01-dashboard.png")
INV = Path(r"E:\KitPlatform\docs\novixa\launch\DOC-008\assets\screenshots\04-inventory-stock.png")
FPS = 30
FADE = 0.20
W, H = 1080, 1920
NAVY = (14, 28, 51, 255)

CLIPS = [
    ("01", [("wide-step.jpg", 0.48), ("close-face.jpg", 0.52)]),
    ("02", [("v1-ledger.jpg", 0.50), ("hands-boxes.jpg", 0.50)]),
    ("03", [("v1-carton.jpg", 0.46), ("side-carton.jpg", 0.54)]),
    ("04", [("ui-board-a.png", 0.50), ("ui-board-b.png", 0.50)]),
    ("05", [("v1-plan.jpg", 0.50), ("close-face.jpg", 0.50)]),
    ("06", "end"),
]


def run(args: list[str]) -> None:
    done = subprocess.run(args, capture_output=True, text=True)
    if done.returncode != 0:
        raise SystemExit((done.stderr or done.stdout)[-2000:] or "ffmpeg failed")


def stack(parts: list[Image.Image], dest: Path) -> None:
    canvas = Image.new("RGB", (W, H), (244, 247, 251))
    draw = ImageDraw.Draw(canvas)
    font = ImageFont.truetype(str(FONT), 42)
    label = "Dữ liệu minh họa"
    box = draw.textbbox((0, 0), label, font=font)
    draw.rectangle((0, 0, W, 132), fill=NAVY[:3])
    draw.text(((W - (box[2] - box[0])) / 2, 42), label, font=font, fill=(244, 247, 251))
    width = 1000
    fitted = []
    for part in parts:
        height = round(part.height * (width / part.width))
        fitted.append(part.resize((width, height), Image.Resampling.LANCZOS))
    slack = H - 132 - 36 - sum(item.height for item in fitted)
    gap = max(16, slack // (len(fitted) + 1))
    y = 132 + gap
    for item in fitted:
        canvas.paste(item, ((W - item.width) // 2, y))
        y += item.height + gap
    canvas.save(dest)


def build_ui() -> None:
    STILLS.mkdir(parents=True, exist_ok=True)
    dash = Image.open(DASH).convert("RGB")
    revenue = dash.crop((500, 430, 1280, 700))
    chart = dash.crop((540, 790, 1830, 1555))
    stock = Image.open(INV).convert("RGB").crop((540, 580, 1500, 1320))
    stack([revenue, chart], STILLS / "ui-board-a.png")
    stack([stock, chart], STILLS / "ui-board-b.png")
    font = ImageFont.truetype(str(FONT), 42)
    font2 = ImageFont.truetype(str(FONT), 36)
    cap = Image.new("RGBA", (1000, 180), (0, 0, 0, 0))
    draw = ImageDraw.Draw(cap)
    lines = [
        ("Nền tảng vận hành nhà thuốc thông minh", font, (11, 31, 58, 255)),
        ("novixa.vn", font2, (31, 138, 112, 255)),
    ]
    for index, (text, face, fill) in enumerate(lines):
        box = draw.textbbox((0, 0), text, font=face)
        draw.text(((1000 - (box[2] - box[0])) / 2, 8 + index * 78), text, font=face, fill=fill)
    cap.save(STILLS / "end-caption.png")


def still_part(image: Path, dest: Path, seconds: float) -> None:
    frames = round(seconds * FPS)
    vf = (
        f"scale={W}:{H}:force_original_aspect_ratio=increase,"
        f"crop={W}:{H},fps={FPS},format=yuv420p"
    )
    run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-loop", "1", "-i", str(image), "-vf", vf, "-frames:v", str(frames),
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
        "-an", str(dest),
    ])


def voice_seconds(name: str) -> float:
    done = subprocess.run(
        ["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "default=nw=1:nk=1", str(VOICE / f"{name}.mp3")],
        capture_output=True, text=True, check=True,
    )
    return float(done.stdout.strip())


def render_clip(name: str, seconds: float, spec) -> Path:
    dest = RENDERS / f"{name}.mp4"
    audio = VOICE / f"{name}.mp3"
    if spec == "end":
        vf = (
            "scale=640:-1,format=rgba,colorkey=0xFFFFFF:0.08:0.12[logo];"
            "[2:v]format=rgba[cap];"
            f"color=c=0xF4F7FB:s={W}x{H}:d={seconds:.3f}:r={FPS}[bg];"
            "[bg][logo]overlay=(W-w)/2:500:format=auto[withlogo];"
            "[withlogo][cap]overlay=(W-w)/2:1240:format=auto,format=yuv420p[v]"
        )
        run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(STILLS / "novixa-logo.png"), "-i", str(audio),
            "-i", str(STILLS / "end-caption.png"),
            "-filter_complex", vf + ";[1:a]adelay=400|400,apad=pad_dur=3.6,aresample=48000,aformat=channel_layouts=stereo[a]",
            "-map", "[v]", "-map", "[a]", "-t", f"{seconds:.3f}",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
            str(dest),
        ])
        return dest
    parts = []
    for index, (image, part_seconds) in enumerate(spec, start=1):
        part = RENDERS / f"{name}-{index}.mp4"
        still_part(STILLS / image, part, part_seconds)
        parts.append(part)
    listing = RENDERS / f"{name}-list.txt"
    listing.write_text("".join(f"file '{p.as_posix()}'\n" for p in parts), encoding="utf-8")
    silent = RENDERS / f"{name}-silent.mp4"
    run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-f", "concat", "-safe", "0", "-i", str(listing), "-c", "copy", str(silent),
    ])
    run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-i", str(silent), "-i", str(audio),
        "-filter_complex", f"[1:a]adelay=280|280,apad=whole_dur={seconds:.3f},aresample=48000,aformat=channel_layouts=stereo[a]",
        "-map", "0:v:0", "-map", "[a]", "-t", f"{seconds:.3f}",
        "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
        str(dest),
    ])
    return dest


def stitch(paths: list[Path], durations: list[float]) -> None:
    inputs: list[str] = []
    for path in paths:
        inputs.extend(["-i", str(path)])
    video = "[0:v]"
    audio = "[0:a]"
    parts = []
    offset = 0.0
    last = len(durations) - 1
    for index, duration in enumerate(durations[:-1]):
        offset += duration - FADE
        vout = "[v]" if index == last - 1 else f"[v{index}]"
        aout = "[a]" if index == last - 1 else f"[a{index}]"
        parts.append(f"{video}[{index + 1}:v]xfade=transition=fade:duration={FADE}:offset={offset:.3f}{vout}")
        parts.append(f"{audio}[{index + 1}:a]acrossfade=d={FADE}:c1=tri:c2=tri{aout}")
        video, audio = vout, aout
    run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        *inputs, "-filter_complex", ";".join(parts),
        "-map", "[v]", "-map", "[a]",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2", "-movflags", "+faststart",
        str(FINAL),
    ])


def main() -> None:
    RENDERS.mkdir(parents=True, exist_ok=True)
    FINAL.parent.mkdir(parents=True, exist_ok=True)
    build_ui()
    paths, durations = [], []
    for name, spec in CLIPS:
        spoken = voice_seconds(name)
        seconds = round(spoken + (4.0 if spec == "end" else 1.7), 3)
        if spec != "end":
            spec = [(image, round(seconds * share, 3)) for image, share in spec]
            spec[-1] = (spec[-1][0], round(seconds - sum(item[1] for item in spec[:-1]), 3))
        print(f"render {name} {seconds}", flush=True)
        paths.append(render_clip(name, seconds, spec))
        durations.append(seconds)
    print("stitch", flush=True)
    stitch(paths, durations)
    print(FINAL)


if __name__ == "__main__":
    main()
