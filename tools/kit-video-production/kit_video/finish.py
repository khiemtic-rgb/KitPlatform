"""Post-production graphics. Title cards are drawn here. Character scenes are not."""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

from kit_video.audio import _run
from kit_video.errors import ProductionError, issue


def write_logo_plate(
    logo: Path,
    lines: list[str],
    dest: Path,
    font_regular: str,
    font_bold: str,
    size: tuple[int, int] = (1280, 720),
) -> Path:
    """Logo file plus controlled lines. Does not time a scene and does not redraw the mark."""
    if not logo.is_file() or not lines:
        raise ProductionError("FAIL", [issue("logo_plate", str(dest), "Cần logo và ít nhất một dòng.")])
    plate = Image.new("RGB", size, (247, 249, 247))
    mark = _logo_on_paper(logo)
    mark.thumbnail((480, 260))
    plate.paste(mark, ((size[0] - mark.width) // 2, 120), mark)
    draw = ImageDraw.Draw(plate)
    fonts = [ImageFont.truetype(font_bold, 36)] + [ImageFont.truetype(font_regular, 28)] * (len(lines) - 1)
    y = 430
    for line, font in zip(lines, fonts):
        box = draw.textbbox((0, 0), line, font=font)
        width = box[2] - box[0]
        if width > size[0] - 120:
            raise ProductionError("FAIL", [issue("logo_plate", line, "Dòng chữ vượt khung.")])
        draw.text(((size[0] - width) / 2, y), line, font=font, fill=(0, 32, 96))
        y += (box[3] - box[1]) + 28
    dest.parent.mkdir(parents=True, exist_ok=True)
    plate.save(dest)
    return dest


def write_caption_plate(
    line: str,
    dest: Path,
    font_bold: str,
    size: tuple[int, int] = (1280, 720),
) -> Path:
    """One reviewed line on paper. Not a timed scene and not a product screen."""
    plate = Image.new("RGB", size, (247, 249, 247))
    draw = ImageDraw.Draw(plate)
    font = ImageFont.truetype(font_bold, 40)
    box = draw.textbbox((0, 0), line, font=font)
    width = box[2] - box[0]
    height = box[3] - box[1]
    if width > size[0] - 120:
        raise ProductionError("FAIL", [issue("caption_plate", line, "Dòng chữ vượt khung.")])
    draw.text(((size[0] - width) / 2, (size[1] - height) / 2), line, font=font, fill=(0, 32, 96))
    dest.parent.mkdir(parents=True, exist_ok=True)
    plate.save(dest)
    return dest


def write_flow_plate(
    labels: list[str],
    caption: str,
    dest: Path,
    font_regular: str,
    font_bold: str,
    size: tuple[int, int] = (1280, 720),
) -> Path:
    """Three named steps. This is a diagram, not a verified product report."""
    if len(labels) != 3:
        raise ProductionError("FAIL", [issue("flow_plate", str(dest), "Sơ đồ này có đúng ba nhãn.")])
    plate = Image.new("RGB", size, (247, 249, 247))
    draw = ImageDraw.Draw(plate)
    label_font = ImageFont.truetype(font_bold, 28)
    caption_font = ImageFont.truetype(font_regular, 28)
    centers = (240, 640, 1040)
    for label, center in zip(labels, centers):
        box = draw.textbbox((0, 0), label, font=label_font)
        width = box[2] - box[0]
        left = center - width / 2 - 28
        top = 250
        draw.rounded_rectangle((left, top, left + width + 56, top + 88), 16, outline=(22, 140, 136), width=3)
        draw.text((center - width / 2, top + 26), label, font=label_font, fill=(0, 32, 96))
    draw.line((400, 294, 500, 294), fill=(22, 140, 136), width=4)
    draw.line((780, 294, 880, 294), fill=(22, 140, 136), width=4)
    box = draw.textbbox((0, 0), caption, font=caption_font)
    width = box[2] - box[0]
    if width > size[0] - 120:
        raise ProductionError("FAIL", [issue("flow_plate", caption, "Dòng chữ vượt khung.")])
    draw.text(((size[0] - width) / 2, 430), caption, font=caption_font, fill=(0, 32, 96))
    dest.parent.mkdir(parents=True, exist_ok=True)
    plate.save(dest)
    return dest


def write_still_hold(image: Path, dest: Path, seconds: float, frame_rate: int = 30) -> Path:
    """Hold one finished graphic for the scene length. This is not character motion."""
    if not image.is_file():
        raise ProductionError("FAIL", [issue("still", str(image), "Không thấy ảnh để giữ.")])
    dest.parent.mkdir(parents=True, exist_ok=True)
    _encode_hold(image, dest, seconds, frame_rate)
    return dest


def write_brand_hold(
    logo: Path,
    line: str,
    dest: Path,
    font_regular: str,
    size: tuple[int, int] = (1280, 720),
    seconds: float = 3,
    frame_rate: int = 30,
    ink: tuple[int, int, int] = (0, 32, 96),
    paper: tuple[int, int, int] = (247, 249, 247),
) -> Path:
    if not logo.is_file() or not line.strip():
        raise ProductionError("FAIL", [issue("logo", str(dest), "Thẻ kết cần file logo và một câu.")])
    plate = Image.new("RGB", size, paper)
    mark = _logo_on_paper(logo)
    mark.thumbnail((520, 280))
    plate.paste(mark, ((size[0] - mark.width) // 2, 150), mark)
    draw = ImageDraw.Draw(plate)
    font = ImageFont.truetype(font_regular, 36)
    box = draw.textbbox((0, 0), line, font=font)
    draw.text(((size[0] - (box[2] - box[0])) / 2, 470), line, font=font, fill=ink)
    png = dest.with_suffix(".png")
    png.parent.mkdir(parents=True, exist_ok=True)
    plate.save(png)
    _encode_hold(png, dest, seconds, frame_rate)
    return dest


def _logo_on_paper(logo: Path) -> Image.Image:
    """Drop the flat white matte around the brand file. The file itself is not edited."""
    mark = Image.open(logo).convert("RGBA")
    pixels = list(mark.get_flattened_data())
    cleared = []
    for red, green, blue, alpha in pixels:
        if red > 245 and green > 245 and blue > 245:
            cleared.append((red, green, blue, 0))
        else:
            cleared.append((red, green, blue, alpha))
    mark.putdata(cleared)
    return mark


def _encode_hold(image: Path, dest: Path, seconds: float, frame_rate: int) -> None:
    _run([
        "ffmpeg", "-y",
        "-loop", "1", "-i", str(image),
        "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
        "-t", f"{seconds:.3f}",
        "-r", str(frame_rate),
        "-vf", "scale=in_range=pc:out_range=tv,format=yuv420p",
        "-color_range", "tv",
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-profile:v", "high", "-crf", "18", "-preset", "veryfast",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
        "-shortest", "-movflags", "+faststart",
        str(dest),
    ])


def write_title_card(
    dest: Path,
    lines: list[str],
    font_regular: str,
    font_bold: str,
    size: tuple[int, int] = (1280, 720),
    seconds: float = 3,
    frame_rate: int = 30,
    ink: tuple[int, int, int] = (0, 32, 96),
    accent: tuple[int, int, int] = (0, 144, 224),
    paper: tuple[int, int, int] = (255, 250, 244),
) -> Path:
    if not lines or any(not str(line).strip() for line in lines):
        raise ProductionError("FAIL", [issue("title", str(dest), "Thẻ kết cần ít nhất một dòng chữ.")])
    dest.parent.mkdir(parents=True, exist_ok=True)
    plate = _plate(lines, font_regular, font_bold, size, ink, accent, paper)
    png = dest.with_suffix(".png")
    plate.save(png)
    _run([
        "ffmpeg", "-y",
        "-loop", "1", "-i", str(png),
        "-f", "lavfi", "-i", "anullsrc=r=48000:cl=stereo",
        "-t", f"{seconds:.3f}",
        "-r", str(frame_rate),
        "-c:v", "libx264", "-pix_fmt", "yuv420p", "-profile:v", "high", "-crf", "18", "-preset", "veryfast",
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-ac", "2",
        "-shortest", "-movflags", "+faststart",
        str(dest),
    ])
    return dest


def _plate(
    lines: list[str],
    font_regular: str,
    font_bold: str,
    size: tuple[int, int],
    ink: tuple[int, int, int],
    accent: tuple[int, int, int],
    paper: tuple[int, int, int],
) -> Image.Image:
    image = Image.new("RGB", size, paper)
    draw = ImageDraw.Draw(image)
    draw.rectangle((0, 0, int(size[0] * 0.012), size[1]), fill=accent)
    title = ImageFont.truetype(font_bold, int(size[1] * 0.09))
    body = ImageFont.truetype(font_regular, int(size[1] * 0.045))
    fonts = [title] + [body] * (len(lines) - 1)
    heights = []
    for line, font in zip(lines, fonts):
        box = draw.textbbox((0, 0), line, font=font)
        heights.append(box[3] - box[1])
    gap = int(size[1] * 0.03)
    block = sum(heights) + gap * (len(lines) - 1)
    y = (size[1] - block) / 2
    for line, font, height in zip(lines, fonts, heights):
        box = draw.textbbox((0, 0), line, font=font)
        width = box[2] - box[0]
        draw.text(((size[0] - width) / 2, y), line, font=font, fill=ink)
        y += height + gap
    return image
