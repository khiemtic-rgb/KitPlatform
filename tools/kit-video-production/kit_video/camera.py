"""Camera moves on a still plate. The move only reframes pixels that are already in the file."""

from __future__ import annotations

from dataclasses import dataclass

from PIL import Image

from kit_video.canvas import ease_out, lerp, progress
from kit_video.errors import ProductionError, issue

PRESETS = ("hold", "push_in", "pull_out", "pan", "tilt", "zoom")
MAX_AMOUNT = 0.12


@dataclass(frozen=True)
class CameraMove:
    preset: str
    amount: float

    @classmethod
    def parse(cls, preset: object, amount: object) -> "CameraMove":
        name = str(preset or "hold")
        if name not in PRESETS:
            raise ProductionError("BLOCKED", [issue(
                "camera_preset",
                name,
                "Preset camera không có trong danh sách dùng chung.",
            )])
        try:
            value = float(amount or 0)
        except (TypeError, ValueError) as exc:
            raise ProductionError("BLOCKED", [issue("camera_amount", name, "amount phải là số.")]) from exc
        if value < 0 or value > MAX_AMOUNT:
            raise ProductionError("BLOCKED", [issue(
                "camera_amount",
                name,
                f"amount {value} vượt biên {MAX_AMOUNT}. Ảnh nguồn không đủ trường nhìn cho chuyển động đó.",
            )])
        return cls(name, value)


def render_plate(
    source: Image.Image,
    size: tuple[int, int],
    move: CameraMove,
    now: float,
    duration: float,
    background: tuple[int, int, int],
) -> Image.Image:
    """Fit the whole plate, then ease a small reframe. No new scenery is painted."""
    frame_w, frame_h = size
    fitted = _contain(source.convert("RGBA"), frame_w, frame_h)
    amount = ease_out(progress(now, duration * 0.08, duration * 0.82))
    scale, shift_x, shift_y = _pose(move, amount)
    scaled_w = max(1, int(round(fitted.width * scale)))
    scaled_h = max(1, int(round(fitted.height * scale)))
    scaled = fitted.resize((scaled_w, scaled_h), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", size, (*background, 255))
    left = (frame_w - scaled_w) / 2 + shift_x * frame_w
    top = (frame_h - scaled_h) / 2 + shift_y * frame_h
    canvas.alpha_composite(scaled, (int(round(left)), int(round(top))))
    return canvas


def _pose(move: CameraMove, amount: float) -> tuple[float, float, float]:
    if move.preset == "hold" or move.amount == 0:
        return 1.0, 0.0, 0.0
    if move.preset in {"push_in", "zoom"}:
        return lerp(1.0, 1.0 + move.amount, amount), 0.0, 0.0
    if move.preset == "pull_out":
        return lerp(1.0 + move.amount, 1.0, amount), 0.0, 0.0
    if move.preset == "pan":
        return 1.0 + move.amount * 0.35, lerp(-move.amount, move.amount, amount), 0.0
    return 1.0 + move.amount * 0.35, 0.0, lerp(-move.amount * 0.6, move.amount * 0.6, amount)


def _contain(source: Image.Image, frame_w: int, frame_h: int) -> Image.Image:
    scale = min(frame_w / source.width, frame_h / source.height)
    size = (max(1, int(round(source.width * scale))), max(1, int(round(source.height * scale))))
    return source.resize(size, Image.Resampling.LANCZOS)
