"""Local post assembly for NOVIXA marketing V1.

This is not the Production OS mix. The platform path proven in the first E2E
is one still plus one voice. This script only stitches this film's own stills
and voice files with FFmpeg. It does not write production artifacts.
"""

from __future__ import annotations

import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent
STILLS = ROOT / "assets" / "stills"
VOICE = ROOT / "assets" / "voice"
RENDERS = ROOT / "renders"
FINAL = ROOT / "final" / "novixa-marketing-v1.mp4"
FONT = Path(r"C:\Windows\Fonts\segoeui.ttf")
FPS = 30
FADE = 0.40
W, H = 1080, 1920

# Clip length includes a short lead and a hold after the spoken line.
CLIPS = [
    ("01", 7.2, "in"),
    ("02", 10.2, "down"),
    ("03", 10.2, "out"),
    ("04", 15.2, "ui"),
    ("05", 11.2, "in"),
    ("06", 7.5, "end"),
]


def run(args: list[str]) -> None:
    done = subprocess.run(args, capture_output=True, text=True)
    if done.returncode != 0:
        raise SystemExit(done.stderr[-2000:] or "ffmpeg failed")


def zoom(kind: str, frames: int) -> str:
    if kind == "out":
        z = "if(eq(on\\,0)\\,1.10\\,max(1.0\\,zoom-0.00035))"
    elif kind == "down":
        z = "min(1.06\\,1+0.00025*on)"
    else:
        z = "min(1.10\\,1+0.00040*on)"
    y = "ih/2-(ih/zoom/2)+on*0.35" if kind == "down" else "ih/2-(ih/zoom/2)"
    return (
        f"scale={W * 3 // 2}:{H * 3 // 2}:force_original_aspect_ratio=increase,"
        f"crop={W * 3 // 2}:{H * 3 // 2},"
        f"zoompan=z='{z}':x='iw/2-(iw/zoom/2)':y='{y}':d={frames}:s={W}x{H}:fps={FPS},"
        "format=yuv420p"
    )


def ui_part(image: Path, dest: Path, seconds: float) -> None:
    frames = round(seconds * FPS)
    vf = (
        "scale=1080:-1,"
        f"pad={W}:{H}:(ow-iw)/2:(oh-ih)/2:color=0x0E1C33,"
        f"zoompan=z='min(1.04\\,1+0.00015*on)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':"
        f"d={frames}:s={W}x{H}:fps={FPS},format=yuv420p"
    )
    run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-i", str(image), "-vf", vf, "-frames:v", str(frames),
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
        "-an", str(dest),
    ])


def render_clip(name: str, seconds: float, kind: str) -> Path:
    dest = RENDERS / f"{name}.mp4"
    frames = round(seconds * FPS)
    audio = VOICE / f"{name}.mp3"
    if kind == "ui":
        parts = []
        images = ["ui-dashboard.png", "ui-inventory.png", "ui-reports.png"]
        each = seconds / len(images)
        for index, image in enumerate(images, start=1):
            part = RENDERS / f"04-{index}.mp4"
            ui_part(STILLS / image, part, each)
            parts.append(part)
        listing = RENDERS / "04-list.txt"
        listing.write_text("".join(f"file '{p.as_posix()}'\n" for p in parts), encoding="utf-8")
        silent = RENDERS / "04-silent.mp4"
        run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-f", "concat", "-safe", "0", "-i", str(listing), "-c", "copy", str(silent),
        ])
        run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(silent), "-i", str(audio),
            "-filter_complex", "[1:a]adelay=300|300,apad,aresample=48000,aformat=channel_layouts=stereo[a]",
            "-map", "0:v:0", "-map", "[a]", "-t", f"{seconds:.3f}",
            "-c:v", "copy", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
            str(dest),
        ])
        return dest

    if kind == "end":
        vf = (
            f"scale=640:-1,format=rgba,colorkey=0xFFFFFF:0.08:0.12[logo];"
            f"[2:v]format=rgba[cap];"
            f"color=c=0xF4F7FB:s={W}x{H}:d={seconds:.3f}:r={FPS}[bg];"
            "[bg][logo]overlay=(W-w)/2:500:format=auto[withlogo];"
            "[withlogo][cap]overlay=(W-w)/2:1240:format=auto,format=yuv420p[v]"
        )
        run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
            "-i", str(STILLS / "novixa-logo.png"), "-i", str(audio),
            "-i", str(STILLS / "end-caption.png"),
            "-filter_complex", vf + ";[1:a]adelay=350|350,apad,aresample=48000,aformat=channel_layouts=stereo[a]",
            "-map", "[v]", "-map", "[a]", "-t", f"{seconds:.3f}",
            "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
            "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
            str(dest),
        ])
        return dest

    image = STILLS / f"shot{int(name)}.jpg"
    run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error",
        "-i", str(image), "-i", str(audio),
        "-filter_complex",
        f"[0:v]{zoom(kind, frames)}[v];"
        "[1:a]adelay=280|280,apad,aresample=48000,aformat=channel_layouts=stereo[a]",
        "-map", "[v]", "-map", "[a]", "-t", f"{seconds:.3f}",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart",
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
    for index, duration in enumerate(durations[:-1]):
        offset += duration - FADE
        vout = f"[v{index}]" if index < len(durations) - 2 else "[v]"
        aout = f"[a{index}]" if index < len(durations) - 2 else "[a]"
        parts.append(
            f"{video}[{index + 1}:v]xfade=transition=fade:duration={FADE}:offset={offset:.3f}{vout}"
        )
        parts.append(
            f"{audio}[{index + 1}:a]acrossfade=d={FADE}:c1=tri:c2=tri{aout}"
        )
        video = vout
        audio = aout
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
    if not FONT.exists():
        raise SystemExit(f"missing font {FONT}")
    paths = []
    durations = []
    for name, seconds, kind in CLIPS:
        print(f"render {name} {seconds}s", flush=True)
        paths.append(render_clip(name, seconds, kind))
        durations.append(seconds)
    print("stitch", flush=True)
    stitch(paths, durations)
    print(FINAL)


if __name__ == "__main__":
    main()
