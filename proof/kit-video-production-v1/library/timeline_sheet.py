"""Build a timestamped contact sheet from a finished render. Not part of the engine."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def main() -> None:
    render_dir = Path(sys.argv[1])
    step_arg = sys.argv[2] if len(sys.argv) > 2 else "8"
    step = 0 if step_arg == "scenes" else int(step_arg)
    log = json.loads((render_dir / "render-log.json").read_text(encoding="utf-8"))
    video = render_dir / "video.mp4"
    folder = render_dir / "timeline-frames"
    if folder.is_dir():
        for old in folder.glob("t*.png"):
            old.unlink()
    folder.mkdir(exist_ok=True)
    if step == 0:
        cursor = 0.0
        for index, scene in enumerate(log["scenes"], start=1):
            duration = float(scene["durationSeconds"])
            at = cursor + duration / 2
            subprocess.run(
                ["ffmpeg", "-y", "-ss", f"{at:.3f}", "-i", str(video), "-frames:v", "1", str(folder / f"t{index:03d}.png")],
                check=True,
                capture_output=True,
            )
            cursor += duration
    else:
        subprocess.run(
            ["ffmpeg", "-y", "-i", str(video), "-vf", f"fps=1/{step}", str(folder / "t%03d.png")],
            check=True,
            capture_output=True,
        )
    shots = sorted(folder.glob("t*.png"))
    bounds = []
    cursor = 0.0
    for scene in log["scenes"]:
        bounds.append((cursor, cursor + float(scene["durationSeconds"]), scene["sceneId"]))
        cursor += float(scene["durationSeconds"])
    font = ImageFont.truetype("C:/Windows/Fonts/segoeui.ttf", 18)
    thumb_w, thumb_h = 320, 180
    label_h = 28
    columns = 4
    rows = (len(shots) + columns - 1) // columns
    sheet = Image.new("RGB", (columns * thumb_w, rows * (thumb_h + label_h)), (246, 246, 244))
    draw = ImageDraw.Draw(sheet)
    for index, shot in enumerate(shots):
        if step == 0 and index < len(bounds):
            second = int(round((bounds[index][0] + bounds[index][1]) / 2))
            scene_id = bounds[index][2]
        else:
            second = index * step
            scene_id = next((item[2] for item in bounds if item[0] <= second < item[1]), bounds[-1][2])
        image = Image.open(shot).convert("RGB").resize((thumb_w, thumb_h))
        column = index % columns
        row = index // columns
        x = column * thumb_w
        y = row * (thumb_h + label_h)
        draw.text((x + 8, y + 4), f"{second // 60:02d}:{second % 60:02d}  {scene_id}", fill=(16, 28, 48), font=font)
        sheet.paste(image, (x, y + label_h))
    dest = render_dir / "timeline-contact-sheet.png"
    sheet.save(dest)
    print(dest)


if __name__ == "__main__":
    main()
