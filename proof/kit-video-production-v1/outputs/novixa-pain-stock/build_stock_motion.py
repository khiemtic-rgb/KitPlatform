"""Vertical stock short. Code draws the stage; the two reports stay original pixels."""

from __future__ import annotations

import base64
import importlib.util
import json
import os
import re
import shutil
import subprocess
import sys
import urllib.error
import urllib.request
import wave
from datetime import datetime, timezone
from pathlib import Path

import psycopg2
from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(r"E:\KitPlatform")
SCREENS = ROOT / "proof/kit-video-production-v1/outputs/novixa-pain-stock/screens"
SELLERS = SCREENS / "sellers-five.png"
EXPIRY = SCREENS / "expiry-five.png"
SETTINGS = ROOT / "src/KitPlatform.Api/appsettings.Development.json"
FONT_B = Path(r"C:/Windows/Fonts/segoeuib.ttf")
FONT_R = Path(r"C:/Windows/Fonts/segoeui.ttf")
VOICE_ID = "nbv4fVbfyLxvuHzyIeDo"
MODEL = "eleven_v3"
# Voice settings for this job. The brand sets them (Brand -> voice); defaults keep the proven Novixa take.
VOICE_SETTINGS = {"stability": 0.5, "similarity_boost": 0.85, "style": 0.0, "speed": 1.0}


def set_voice_settings(job: dict | None) -> None:
    raw = (job or {}).get("voiceSettings") or {}
    def num(key: str, low: float, high: float, default: float) -> float:
        try:
            value = float(raw.get(key))
        except (TypeError, ValueError):
            return default
        return min(high, max(low, value))
    VOICE_SETTINGS.update({
        "stability": num("stability", 0.0, 1.0, 0.5),
        "similarity_boost": num("similarityBoost", 0.0, 1.0, 0.85),
        "style": num("style", 0.0, 1.0, 0.0),
        "speed": num("speed", 0.7, 1.2, 1.0),
    })


def voice_key() -> str:
    return "|".join(f"{VOICE_SETTINGS[k]:.2f}" for k in ("stability", "similarity_boost", "style", "speed"))
W, H = 1080, 1920
FPS = 30
TAIL = 1.1
FRAME = (920, 1000)
NAVY = (5, 16, 40)
CYAN = (70, 214, 255)
YELLOW = (255, 220, 48)
PURPLE = (124, 48, 214)
CARD = (10, 26, 54)
LINE = (36, 140, 196)
MUTED = (150, 186, 214)
PAPER = (248, 250, 252)
BANDS = [
    (42, 168, 198),
    (214, 146, 48),
    (58, 158, 112),
    (78, 112, 196),
    (186, 84, 108),
    (96, 128, 150),
]
LEFT_ITEMS = [
    ("box", 52, 136, 300, BANDS[0]),
    ("bottle", 206, 108, 246, BANDS[1]),
    ("box", 328, 96, 176, BANDS[2]),
]
RIGHT_COUNTS = (4, 5, 4, 5)
RIGHT_TOTAL = sum(RIGHT_COUNTS)

LINES = [
    "Nhập ít thì sợ không đủ hàng để bán.",
    "Nhập nhiều thì sợ hàng nằm lại, bán không hết.",
    "Hàng bán chạy chỉ có ít. Hàng nằm im thì chất đầy kệ.",
    "Hàng vẫn còn trên kệ, nhưng tiền thì đã chi ra rồi.",
    "Novixa cho thấy hàng nào đang bán chạy, và hàng nào đang cận hạn.",
]


def ease(t: float) -> float:
    t = max(0.0, min(1.0, t))
    return 1 - (1 - t) ** 3


def tts_text(line: str) -> str:
    return line.replace("Novixa", "Nôvixa")


def run(command: list[str]) -> None:
    done = subprocess.run(command, capture_output=True)
    if done.returncode != 0:
        raise RuntimeError(done.stderr.decode("utf-8", "replace")[-600:])


def eleven_key() -> str:
    settings = json.loads(SETTINGS.read_text(encoding="utf-8"))
    material = base64.b64decode(settings["ProductionCredentials"]["DataEncryptionKey"])
    conn = settings["ConnectionStrings"]["Default"]
    parts = dict(item.split("=", 1) for item in conn.split(";") if "=" in item)
    with psycopg2.connect(
        host=parts["Host"],
        port=parts.get("Port", "5432"),
        dbname=parts["Database"],
        user=parts["Username"],
        password=parts["Password"],
    ) as db:
        with db.cursor() as cur:
            cur.execute(
                """
                SELECT secret_nonce, secret_ciphertext, secret_tag
                FROM pack_content.production_credential_versions
                WHERE lower(provider_id) = 'elevenlabs'
                  AND lower(model_id) = 'eleven_v3'
                  AND status = 'ACTIVE'
                  AND secret_ciphertext IS NOT NULL
                ORDER BY created_at
                LIMIT 1
                """
            )
            row = cur.fetchone()
    if row is None:
        raise SystemExit("Chưa có khóa ElevenLabs đang dùng.")
    nonce, cipher, tag = row
    plain = AESGCM(material).decrypt(bytes(nonce), bytes(cipher) + bytes(tag), None)
    return plain.decode("utf-8").strip()


def chosen_voice(job: dict | None) -> str:
    raw = str((job or {}).get("voiceId") or VOICE_ID).strip()
    return raw if re.fullmatch(r"[A-Za-z0-9_-]{8,64}", raw) else VOICE_ID


def speak_eleven(mp3: Path, alignment_path: Path, lines: list[str], voice_id: str | None = None) -> None:
    key = eleven_key()
    voice = voice_id or VOICE_ID
    spoken = "\n".join(tts_text(line) for line in lines)
    body = {
        "text": spoken,
        "model_id": MODEL,
        "apply_text_normalization": "off",
        "voice_settings": {
            "stability": VOICE_SETTINGS["stability"],
            "similarity_boost": VOICE_SETTINGS["similarity_boost"],
            "style": VOICE_SETTINGS["style"],
            "speed": VOICE_SETTINGS["speed"],
            "use_speaker_boost": True,
        },
    }
    request = urllib.request.Request(
        f"https://api.elevenlabs.io/v1/text-to-speech/{voice}/with-timestamps?output_format=mp3_44100_128",
        data=json.dumps(body).encode("utf-8"),
        headers={"xi-api-key": key, "Content-Type": "application/json", "Accept": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", "replace")[:300].replace(key, "")
        raise SystemExit(f"ElevenLabs {exc.code}: {detail}") from None
    audio = base64.b64decode(payload["audio_base64"])
    if len(audio) < 1000:
        raise SystemExit("ElevenLabs trả audio rỗng.")
    mp3.write_bytes(audio)
    alignment = payload.get("alignment") or payload.get("normalized_alignment")
    if not alignment or not alignment.get("characters"):
        raise SystemExit("ElevenLabs không trả mốc từ.")
    alignment_path.write_text(json.dumps(alignment, ensure_ascii=False), encoding="utf-8")


def word_tokens(alignment: dict) -> list[dict]:
    tokens: list[dict] = []
    buf = ""
    start = end = 0.0
    chars = alignment["characters"]
    starts = alignment["character_start_times_seconds"]
    ends = alignment["character_end_times_seconds"]

    def flush() -> None:
        nonlocal buf
        if buf:
            tokens.append({"text": buf, "start": start, "end": end})
            buf = ""

    for char, char_start, char_end in zip(chars, starts, ends):
        if char.isspace():
            flush()
            continue
        if not buf:
            start = char_start
        buf += char
        end = char_end
    flush()
    merged: list[dict] = []
    for token in tokens:
        if merged and not token["text"].strip(".,;:!?…"):
            merged[-1]["text"] += token["text"]
            merged[-1]["end"] = token["end"]
        elif token["text"].strip("[]").lower() in {"pause", "short", "short pause"}:
            continue
        else:
            merged.append(token)
    return merged


def tighten(lines: list[list[dict]], raw: bytes, rate: int, seconds: float) -> tuple[bytes, list[list[dict]], float]:
    breath = 0.48
    kept: list[tuple[float, float]] = [(0.0, lines[0][-1]["end"])]
    for index in range(1, len(lines)):
        previous = lines[index - 1][-1]["end"]
        nxt = lines[index][0]["start"]
        kept.append((max(previous, nxt - breath), lines[index][-1]["end"]))
    tail_from = lines[-1][-1]["end"]
    kept.append((tail_from, min(seconds, tail_from + TAIL)))

    def mapped(moment: float) -> float:
        clock = 0.0
        for start, end in kept:
            if moment < start:
                return clock
            if moment <= end:
                return clock + (moment - start)
            clock += end - start
        return clock

    pieces = []
    for start, end in kept:
        a = int(start * rate) * 2
        b = int(end * rate) * 2
        pieces.append(raw[a:b])
    audio = b"".join(pieces)
    shifted = []
    for marks in lines:
        shifted.append([
            {"text": mark["text"], "start": mapped(mark["start"]), "end": mapped(mark["end"])}
            for mark in marks
        ])
    duration = len(audio) / 2 / rate
    missing = TAIL - (duration - shifted[-1][-1]["end"])
    if missing > 0.05:
        audio += silence(rate, missing)
        duration = len(audio) / 2 / rate
    return audio, shifted, duration


def assign_lines(tokens: list[dict], lines: list[str]) -> list[list[dict]]:
    cursor = 0
    assigned = []
    for line in lines:
        words = tts_text(line).split()
        taken = tokens[cursor: cursor + len(words)]
        if len(taken) != len(words):
            raise SystemExit(f"Lệch mốc từ: cần {len(words)}, còn {len(tokens) - cursor}.")
        cursor += len(words)
        assigned.append(taken)
    return assigned


def wav_info(path: Path) -> tuple[bytes, int, float]:
    with wave.open(str(path), "rb") as handle:
        rate = handle.getframerate()
        frames = handle.readframes(handle.getnframes())
        seconds = handle.getnframes() / rate
    return frames, rate, seconds


def silence(rate: int, seconds: float) -> bytes:
    return b"\x00\x00" * int(rate * seconds)


def background() -> Image.Image:
    image = Image.new("RGB", (W, H), NAVY)
    glow = Image.new("RGB", (W, H), NAVY)
    draw = ImageDraw.Draw(glow)
    draw.ellipse((80, 220, 1000, 1400), fill=(14, 52, 102))
    glow = glow.filter(ImageFilter.GaussianBlur(80))
    return Image.blend(image, glow, 0.8)


def rounded_mask(size: tuple[int, int], radius: int) -> Image.Image:
    mask = Image.new("L", size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius, fill=255)
    return mask


def paste_card(base: Image.Image, card: Image.Image, xy: tuple[int, int], alpha: float, dy: int) -> None:
    if alpha <= 0:
        return
    layer = Image.new("RGBA", base.size, (0, 0, 0, 0))
    layer.paste(card, (xy[0], xy[1] + int((1 - alpha) * dy)))
    if alpha < 1:
        faded = layer.copy()
        faded.putalpha(layer.getchannel("A").point(lambda value: int(value * alpha)))
        layer = faded
    base.alpha_composite(layer)


def draw_box(draw: ImageDraw.ImageDraw, x: int, y: int, w: int, h: int, band: tuple[int, int, int]) -> None:
    side = tuple(max(0, channel - 42) for channel in band)
    draw.ellipse((x + 8, y + h - 4, x + w - 2, y + h + 14), fill=(2, 8, 18))
    draw.polygon(
        [(x + w - 1, y + 8), (x + w + 10, y), (x + w + 10, y + h - 10), (x + w - 1, y + h - 2)],
        fill=(168, 178, 188),
    )
    draw.rounded_rectangle((x, y, x + w, y + h), 7, fill=PAPER)
    face_h = max(28, int(h * 0.42))
    draw.rectangle((x, y, x + w, y + face_h), fill=band)
    draw.rectangle((x, y + face_h - 5, x + w, y + face_h), fill=side)
    draw.rectangle((x + 14, y + face_h + 16, x + w - 16, y + face_h + 22), fill=(176, 186, 196))
    if h > 90:
        draw.rectangle((x + 14, y + face_h + 30, x + int(w * 0.58), y + face_h + 36), fill=(206, 214, 220))


def draw_bottle(draw: ImageDraw.ImageDraw, x: int, y: int, w: int, h: int, band: tuple[int, int, int]) -> None:
    body_w = max(26, int(w * 0.7))
    left = x + (w - body_w) // 2
    cap_h = max(12, int(h * 0.13))
    neck_h = max(8, int(h * 0.09))
    cap_w = max(16, int(body_w * 0.56))
    cap_x = left + (body_w - cap_w) // 2
    body_top = y + cap_h + neck_h
    draw.ellipse((left + 4, y + h - 4, left + body_w - 2, y + h + 12), fill=(2, 8, 18))
    draw.rounded_rectangle((left, body_top, left + body_w, y + h), body_w // 2, fill=(236, 242, 246))
    draw.ellipse((left, body_top - 8, left + body_w, body_top + 18), fill=(236, 242, 246))
    label_top = body_top + max(10, int((y + h - body_top) * 0.18))
    label_bottom = y + h - max(12, int(h * 0.12))
    if label_bottom > label_top + 16:
        draw.rounded_rectangle((left + 5, label_top, left + body_w - 5, label_bottom), 5, fill=band)
        mid = (label_top + label_bottom) // 2
        draw.rectangle((left + 12, mid - 3, left + body_w - 12, mid + 2), fill=(255, 255, 255))
    draw.rectangle((cap_x + 3, y + cap_h - 2, cap_x + cap_w - 3, body_top + 6), fill=(214, 222, 228))
    draw.rounded_rectangle((cap_x, y, cap_x + cap_w, y + cap_h + 2), 4, fill=band)
    draw.rectangle((left + 6, body_top + 8, left + 10, y + h - 10), fill=(255, 255, 255))


def plank(draw: ImageDraw.ImageDraw, x: int, y: int, w: int) -> None:
    draw.rectangle((x, y, x + w, y + 8), fill=(78, 176, 214))
    draw.rectangle((x, y + 8, x + w, y + 20), fill=(12, 36, 64))


def pain_card(shown_left: int, shown_right: int) -> Image.Image:
    card = Image.new("RGBA", FRAME, (0, 0, 0, 0))
    draw = ImageDraw.Draw(card)
    draw.rounded_rectangle((0, 0, FRAME[0] - 1, FRAME[1] - 1), 36, fill=CARD, outline=LINE, width=3)
    font = ImageFont.truetype(str(FONT_B), 36)
    draw.rounded_rectangle((28, 28, 448, 972), 24, fill=(7, 20, 42))
    draw.rounded_rectangle((472, 28, 892, 972), 24, fill=(7, 20, 42))
    draw.text((52, 52), "Bán được ít", font=font, fill=CYAN)
    draw.text((504, 52), "Hàng tồn nhiều", font=font, fill=CYAN)
    plank(draw, 48, 900, 380)
    for index in range(min(shown_left, len(LEFT_ITEMS))):
        kind, x, w, h, band = LEFT_ITEMS[index]
        if kind == "bottle":
            draw_bottle(draw, x, 900 - h, w, h, band)
        else:
            draw_box(draw, x, 900 - h, w, h, band)
    shelves = (300, 500, 700, 900)
    shown = 0
    band_index = 0
    for row, count in enumerate(RIGHT_COUNTS):
        plank(draw, 496, shelves[row], 372)
        gap = 8
        width = (372 - gap * (count - 1)) // count
        for col in range(count):
            if shown >= shown_right:
                break
            height = 124 + ((row + col) % 3) * 14
            slot_x = 496 + col * (width + gap)
            slot_w = width - 8
            band = BANDS[band_index % len(BANDS)]
            if (row + col) % 3 == 1:
                draw_bottle(draw, slot_x, shelves[row] - height, slot_w, height, band)
            else:
                draw_box(draw, slot_x, shelves[row] - height, slot_w - 6, height, band)
            band_index += 1
            shown += 1
    return card


def shelf_counts(index: int, total: int) -> tuple[int, int]:
    span = max(total, 1)
    if index < max(1, span // 3):
        return min(len(LEFT_ITEMS), index + 1), 0
    done = index - span // 3
    room = max(span - span // 3, 1)
    right = min(RIGHT_TOTAL, int(done / room * RIGHT_TOTAL) + 1)
    return len(LEFT_ITEMS), right


def picture_for(line: str, index: int, total: int) -> tuple[str, Image.Image]:
    low = line.lower()
    if any(token in low for token in ("cận hạn", "hết hạn", "sát hạn", "hạn dùng")) and EXPIRY.is_file():
        return "Cận hạn", shot_card(EXPIRY, "Cận hạn")
    if any(token in low for token in ("bán chạy", "novixa")) and SELLERS.is_file():
        return "Bán chạy", shot_card(SELLERS, "Bán chạy")
    left, right = shelf_counts(index, total)
    return "Hàng tồn", pain_card(left, right)


def _scene_board():
    name = "novixa_scene_board"
    if name in sys.modules:
        return sys.modules[name]
    path = Path(__file__).resolve().parent / "novixa_scene_board.py"
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def story_frame(line: str, index: int, total: int, used: set[str] | None = None) -> Image.Image:
    return _scene_board().story_frame(line, index, total, used)


def scene_plan(lines: list[str]) -> list[dict]:
    return _scene_board().board_plan(lines)


def scene_assess(lines: list[str]) -> dict:
    return _scene_board().assess(lines)


def line_card(text: str) -> Image.Image:
    card = Image.new("RGBA", FRAME, (0, 0, 0, 0))
    draw = ImageDraw.Draw(card)
    draw.rounded_rectangle((0, 0, FRAME[0] - 1, FRAME[1] - 1), 36, fill=CARD, outline=LINE, width=3)
    font = ImageFont.truetype(str(FONT_B), 54)
    rows = wrap_words(draw, text.split(), font, FRAME[0] - 120)
    line_h = 78
    y = max(80, (FRAME[1] - line_h * max(1, min(len(rows), 10))) // 2)
    for row in rows[:10]:
        shown = " ".join(row)
        draw.text(((FRAME[0] - draw.textlength(shown, font=font)) / 2, y), shown, font=font, fill=PAPER)
        y += line_h
    return card


def shot_card(path: Path, label: str) -> Image.Image:
    source = Image.open(path).convert("RGB")
    max_w, max_h = 840, 800
    scale = min(max_w / source.width, max_h / source.height)
    size = (max(1, int(source.width * scale)), max(1, int(source.height * scale)))
    fitted = source.resize(size, Image.Resampling.LANCZOS)
    card = Image.new("RGBA", FRAME, (0, 0, 0, 0))
    draw = ImageDraw.Draw(card)
    draw.rounded_rectangle((0, 0, FRAME[0] - 1, FRAME[1] - 1), 36, fill=CARD, outline=LINE, width=3)
    font = ImageFont.truetype(str(FONT_B), 40)
    draw.text((40, 36), label, font=font, fill=CYAN)
    x = (FRAME[0] - size[0]) // 2
    y = 112 + (800 - size[1]) // 2
    card.paste(fitted, (x, y), rounded_mask(size, 18))
    return card


def wrap_words(draw: ImageDraw.ImageDraw, words: list[str], font: ImageFont.FreeTypeFont, width: int) -> list[list[str]]:
    rows: list[list[str]] = [[]]
    for word in words:
        trial = " ".join(rows[-1] + [word])
        if draw.textlength(trial, font=font) <= width or not rows[-1]:
            rows[-1].append(word)
        else:
            rows.append([word])
    return rows


def caption(base: Image.Image, words: list[str], active: int, top: int) -> None:
    if not words:
        return
    overlay = Image.new("RGBA", base.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    font = ImageFont.truetype(str(FONT_B), 46)
    rows = wrap_words(draw, words, font, 940)
    line_h = 78
    y = top
    cursor = 0
    space = draw.textlength(" ", font=font)
    for row in rows:
        text = " ".join(row)
        x = (W - draw.textlength(text, font=font)) / 2
        for word in row:
            width = draw.textlength(word, font=font)
            if cursor == active:
                box = draw.textbbox((x, y), word, font=font)
                draw.rounded_rectangle((box[0] - 12, box[1] - 10, box[2] + 12, box[3] + 10), 12, fill=PURPLE)
            draw.text((x, y), word, font=font, fill=YELLOW)
            x += width + space
            cursor += 1
        y += line_h
    base.alpha_composite(overlay)


def caption_top(card_y: int) -> int:
    return card_y + FRAME[1] + 40


def card_origin() -> int:
    return 210


def title(base: Image.Image, text: str) -> None:
    draw = ImageDraw.Draw(base)
    font = ImageFont.truetype(str(FONT_B), 72)
    tw = draw.textlength(text, font=font)
    draw.text(((W - tw) / 2, 96), text, font=font, fill=CYAN)
    mark = ImageFont.truetype(str(FONT_B), 28)
    draw.text((W - 210, 48), "NOVIXA", font=mark, fill=CYAN)


def disclaimer(base: Image.Image) -> None:
    draw = ImageDraw.Draw(base)
    font = ImageFont.truetype(str(FONT_R), 28)
    draw.text((64, 48), "Dữ liệu minh họa", font=font, fill=MUTED)


def active_word(words: list[str], marks: list[dict], local_t: float) -> int:
    if not words:
        return -1
    if not marks:
        return min(len(words) - 1, int(local_t / 0.28))
    for index, mark in enumerate(marks):
        if local_t < mark["end"]:
            return min(index, len(words) - 1)
    return len(words) - 1


def _result(status: str, message: str, folder: Path | None = None, video: Path | None = None) -> dict:
    return {
        "status": status,
        "message": message,
        "folder": str(folder) if folder else None,
        "videoPath": str(video) if video else None,
    }


def _screens(job: dict) -> list[tuple[Path, str]] | dict:
    rows = job.get("screens") or []
    if not isinstance(rows, list):
        return _result("images_missing", "Ảnh màn hình không đúng dạng đường dẫn và nhãn.")
    supplied = [
        row for row in rows
        if isinstance(row, dict) and (str(row.get("label") or "").strip() or str(row.get("path") or "").strip())
    ]
    if not supplied:
        return []
    if len(supplied) < 2:
        return _result("images_missing", "Đã khai ảnh thì cần đủ hai ảnh. Để trống thì máy vẽ từ kịch bản.")
    found: list[tuple[Path, str]] = []
    for row in supplied[:2]:
        if not isinstance(row, dict):
            return _result("images_missing", "Ảnh màn hình không đúng dạng đường dẫn và nhãn.")
        label = str(row.get("label") or "").strip()
        raw = str(row.get("path") or "").strip()
        if not label or not raw:
            return _result("images_missing", "Mỗi ảnh cần một đường dẫn và một nhãn.")
        path = Path(raw).expanduser().resolve()
        if path.suffix.lower() not in {".png", ".jpg", ".jpeg", ".webp"} or not path.is_file():
            return _result("images_missing", f"Không thấy ảnh đã chuẩn bị: {raw}")
        if path.name.lower() in {"sellers-five.png", "expiry-five.png"} and not job.get("allowProofScreens"):
            return _result(
                "images_missing",
                "Không gắn sellers-five.png hoặc expiry-five.png cho kịch bản này. Hãy đưa ảnh đã chuẩn bị của phim.",
            )
        found.append((path, label))
    return found


def _build_dir(job: dict) -> Path:
    build_id = re.sub(r"[^0-9A-Za-z_-]", "", str(job.get("buildId") or "film"))[:80] or "film"
    return ROOT / "proof/kit-video-production-v1/outputs/novixa-studio" / build_id


def _ready_video(job: dict, lines: list[str]) -> dict | None:
    root = _build_dir(job)
    if not root.is_dir():
        return None
    found = None
    for episode in sorted(root.glob("*-motion/episode.json")):
        try:
            data = json.loads(episode.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        video = str(data.get("videoPath") or "")
        if data.get("status") == "ready" and _same_take(data, job, lines) and video and Path(video).is_file():
            found = data
    return found


def _same_take(data: dict, job: dict, lines: list[str]) -> bool:
    if data.get("lines") != lines:
        return False
    wanted_voice = str(job.get("voiceId") or "").strip()
    if wanted_voice and str(data.get("voiceId") or "") != wanted_voice:
        return False
    if job.get("voiceSettings") and str(data.get("voiceKey") or "") != voice_key():
        return False
    wanted = [str(row.get("path") or "") for row in (job.get("stills") or []) if isinstance(row, dict)]
    if not wanted:
        return True
    stored = data.get("stills") if isinstance(data.get("stills"), list) else []
    stored_paths = [str(row.get("path") or "") for row in stored if isinstance(row, dict)]
    return stored_paths == wanted


def _voice_source(job: dict, lines: list[str]) -> Path | None:
    raw = str(job.get("reuseFolder") or "").strip()
    if not raw:
        return None
    folder = Path(raw)
    note = folder / "lines.json"
    if not (folder / "voice.mp3").is_file() or not (folder / "alignment.json").is_file() or not note.is_file():
        return None
    try:
        saved = json.loads(note.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    return folder if saved == lines else None


def _stamp_dir(job: dict, kind: str) -> Path:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    out = _build_dir(job) / f"{stamp}-{kind}"
    out.mkdir(parents=True)
    return out


def _graphics():
    name = "novixa_motion_graphics"
    if name in sys.modules:
        return sys.modules[name]
    path = Path(__file__).resolve().parent / "novixa_motion_graphics.py"
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    assert spec and spec.loader
    spec.loader.exec_module(module)
    return module


def _still_meta(path: Path) -> dict:
    note = path.parent / "stills.json"
    if not note.is_file():
        return {}
    try:
        data = json.loads(note.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return {}
    for item in data.get("stills") or []:
        if isinstance(item, dict) and Path(str(item.get("path") or "")).name == path.name:
            return item
    return {}


def _kept_source(row: dict, path: Path) -> str:
    if str(row.get("source") or "") == "gemini":
        return "gemini"
    if "-g" in path.stem:
        return "gemini"
    if str(_still_meta(path).get("source") or "") == "gemini":
        return "gemini"
    if str(row.get("source") or "") == "library" or str(_still_meta(path).get("source") or "") == "library":
        return "library"
    return ""


def _still_record(index: int, line: str, dest: Path, plan: dict, kept: dict | None) -> dict:
    row = kept or {}
    record = {
        "index": index,
        "id": f"U{index + 1:02d}",
        "text": line,
        "path": str(dest),
        "layout": str(row.get("layout") or plan["layout"]),
        "family": str(row.get("family") or plan["family"]),
        "mirrored": bool(row.get("mirrored")) if row.get("layout") else bool(plan.get("mirrored")),
    }
    for key in ("kind", "subject", "signature", "arrangement", "distance", "light", "shift", "brandId", "paletteKey"):
        value = row.get(key)
        if value in (None, "") and not (kept and key == "brandId"):
            value = plan.get(key)
        if value not in (None, ""):
            record[key] = value
    source = _kept_source(row, Path(str(row.get("path") or dest)))
    if source:
        record["source"] = source
    return record


def _merge_still(row: dict, path: Path) -> dict:
    meta = _still_meta(path)
    merged = {**meta, **{key: value for key, value in row.items() if value not in (None, "")}}
    merged["path"] = str(path)
    if str(merged.get("source") or "") != "gemini" and "-g" in path.stem:
        merged["source"] = "gemini"
    return merged


def _existing_stills(job: dict) -> dict[str, dict]:
    found: dict[str, dict] = {}
    for item in list(job.get("keep") or []) + list(job.get("stills") or []):
        if not isinstance(item, dict) or not str(item.get("path") or "").strip():
            continue
        text = spoken_line(str(item.get("text") or ""))
        path = Path(str(item["path"]))
        if text and path.is_file():
            found[text] = item
    return found


def _copy_still(index: int, line: str, source: Path, out: Path, plan: dict, kept: dict) -> dict:
    suffix = ".jpg" if source.suffix.lower() in {".jpg", ".jpeg"} else ".png"
    dest = out / f"u{index:02d}{suffix}"
    shutil.copy(source, dest)
    return _still_record(index, line, dest, plan, kept)


def _recent_signatures(job: dict) -> list[str]:
    root = _build_dir(job)
    found: list[str] = []
    if not root.exists():
        return found
    for folder in sorted(path for path in root.glob("*-stills") if path.is_dir()):
        note = folder / "stills.json"
        if not note.is_file():
            continue
        try:
            data = json.loads(note.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            continue
        for item in data.get("stills") or []:
            if isinstance(item, dict) and item.get("signature"):
                found.append(str(item["signature"]))
    return found[-100:]


def _library_still(plan: dict, line: str, dest: Path, job: dict) -> bool:
    """Vẽ ảnh bằng bộ minh họa mới (tools/kit-video-library/novixa-series). Lỗi thì trả False để dùng cách vẽ cũ."""
    if job.get("useLibrary") is False or os.environ.get("NOVIXA_STUDIO_LIBRARY", "1") == "0":
        return False
    src = Path(__file__).resolve().parents[4] / "tools" / "kit-video-library" / "novixa-series" / "src"
    if not (src / "studio_plates.py").is_file():
        return False
    try:
        name = "novixa_studio_plates"
        module = sys.modules.get(name)
        if module is None:
            os.environ["NOVIXA_SERIES_SRC"] = str(src)
            spec = importlib.util.spec_from_file_location(name, src / "studio_plates.py")
            module = importlib.util.module_from_spec(spec)
            sys.modules[name] = module
            sys.modules["studio_plates"] = module  # bản ngang import studio_plates: dùng chung một bản
            assert spec and spec.loader
            spec.loader.exec_module(module)
        if job.get("redrawIndex") is not None and not getattr(module, "_primed", False):
            module._primed = True  # vẽ lại một ảnh lẻ: nạp lịch sử các ảnh còn lại để không chọn trùng
            others = [str(item.get("path")) for item in list(job.get("keep") or []) if isinstance(item, dict) and item.get("path")]
            module.prime([path for path in others if Path(path).is_file()])
        orient = "wide" if str(job.get("orientation") or os.environ.get("NOVIXA_STUDIO_ORIENT") or "").lower() in ("wide", "16:9", "landscape") else "tall"
        module.plate(str(plan.get("layout") or ""), line, str(dest), pick=int(plan.get("arrangement") or 0) + int(plan.get("index") or 0), orient=orient, brand=job.get("brand") if isinstance(job.get("brand"), dict) else None)
        return dest.is_file()
    except Exception as error:  # noqa: BLE001
        print(f"[library_still] bỏ qua: {error}", file=sys.stderr)
        if str(job.get("orientation") or os.environ.get("NOVIXA_STUDIO_ORIENT") or "").lower() in ("wide", "16:9", "landscape"):
            raise RuntimeError(f"Không vẽ được ảnh ngang 16:9 ({error}). Không dùng cách vẽ cũ vì nó chỉ ra ảnh dọc.") from error
        return False


def _stills_job(job: dict, lines: list[str]) -> dict:
    out = _stamp_dir(job, "stills")
    redraw = job.get("redrawIndex")
    redraw_index = int(redraw) if isinstance(redraw, int) or (isinstance(redraw, str) and str(redraw).isdigit()) else None
    variant = int(job.get("variant") or 0)
    brand = job.get("brand") if isinstance(job.get("brand"), dict) else {}
    brand_id = str(brand.get("id") or "")
    history = _recent_signatures(job)
    plans = _graphics().plans_for(lines, brand, variant if redraw_index is None else 0, history)
    keep = {}
    for item in job.get("keep") or []:
        if isinstance(item, dict) and str(item.get("path") or "").strip():
            keep[int(item.get("index") or 0)] = item
    existing = _existing_stills(job)
    fresh = variant > 0 and redraw_index is None
    images = []
    kept_count = 0
    for index, line in enumerate(lines):
        plan = plans[index]
        if redraw_index is not None and index == redraw_index:
            plan = _graphics().plans_for([line], brand, variant or 1, history)[0]
            plan["index"] = index
        kept_row = None
        kept_path = None
        if redraw_index is not None and index != redraw_index:
            kept_row = keep.get(index)
        elif redraw_index is None and not fresh:
            kept_row = existing.get(line)
        if isinstance(kept_row, dict) and kept_row.get("path"):
            kept_path = Path(str(kept_row.get("path")))
        if kept_path is not None and kept_path.is_file() and isinstance(kept_row, dict):
            record = _copy_still(index, line, kept_path, out, plan, kept_row)
            kept_brand = str(kept_row.get("brandId") or "")
            if brand_id and kept_brand and kept_brand != brand_id:
                record["brandStale"] = True
                record["brandId"] = kept_brand
            kept_count += 1
        else:
            dest = out / f"u{index:02d}.png"
            library_ok = _library_still(plan, line, dest, job)
            if not library_ok:
                _graphics().still_frame(plan).convert("RGB").save(dest)
            record = _still_record(index, line, dest, plan, None)
            if library_ok:
                record["source"] = "library"
        images.append(record)
    drawn = len(images) - kept_count
    message = f"Đã vẽ {drawn} ảnh mới." if kept_count else f"Đã vẽ {len(images)} ảnh từ kịch bản."
    if kept_count:
        message = f"Giữ {kept_count} ảnh đã có. {message}"
    payload = _result("ready", message, out)
    payload["phase"] = "stills"
    payload["stills"] = images
    (out / "stills.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return payload


def _approved_stills(job: dict, lines: list[str]) -> list[dict] | dict:
    rows = job.get("stills") or []
    if not isinstance(rows, list) or len(rows) != len(lines):
        return _result("images_missing", "Chưa có đủ ảnh đã duyệt cho từng câu.")
    found: list[dict] = []
    for index, row in enumerate(rows):
        if not isinstance(row, dict):
            return _result("images_missing", "Ảnh đã duyệt không đúng dạng.")
        raw = str(row.get("path") or "").strip()
        path = Path(raw) if raw else None
        if path is None or not path.is_file() or path.suffix.lower() not in {".png", ".jpg", ".jpeg"}:
            return _result("images_missing", f"Không thấy ảnh đã duyệt cho câu {index + 1}.")
        if path.name.lower() in {"sellers-five.png", "expiry-five.png"} and not job.get("allowProofScreens"):
            return _result("images_missing", "Không dùng ảnh mẫu sellers-five.png hoặc expiry-five.png.")
        try:
            with Image.open(path) as image:
                image.load()
        except (OSError, ValueError):
            return _result("images_missing", f"Không mở được ảnh đã duyệt cho câu {index + 1}.")
        found.append(_merge_still(row, path))
    return found


_DIRECTION_LABEL = re.compile(
    r"^(chủ đề|định dạng|phong cách|thời lượng|hình ảnh|lời dẫn|ghi chú|nhạc|âm thanh|chú thích|camera|bối cảnh|mục tiêu|tone|style)\b",
    re.IGNORECASE,
)
_SCENE_HEADING = re.compile(r"^(cảnh|canh|sc)\s*\d+", re.IGNORECASE)


def spoken_line(raw: str) -> str:
    line = str(raw or "").strip()
    if not line or re.match(r"^#{1,6}\s", line) or re.fullmatch(r"([-*_])\1{2,}", line.replace(" ", "")) or _SCENE_HEADING.match(line) or re.fullmatch(r"\[[^\]]*\]", line):
        return ""
    plain = line.replace("**", "").strip()
    if not plain or _SCENE_HEADING.match(plain) or re.fullmatch(r"([-*_])\1{2,}", plain):
        return ""
    colon = plain.find(":")
    label = plain[:colon].strip() if colon > 0 else ""
    if label and _DIRECTION_LABEL.match(label):
        return ""
    speaker = colon > 0 and colon <= 40 and bool(label) and len(label.split()) <= 3 and not re.search(r"[,.!?…]", label)
    return plain[colon + 1:].strip() if speaker else plain


def _spoken_job(job: dict, lines: list[str]) -> tuple[list[str], dict]:
    spoken = [text for line in lines if (text := spoken_line(line))]
    rows = []
    for row in job.get("stills") or []:
        if not isinstance(row, dict):
            continue
        raw_text = str(row.get("text") or "")
        if raw_text.strip() and not spoken_line(raw_text):
            continue
        rows.append({**row, "text": spoken_line(raw_text) or raw_text})
    return spoken, {**job, "stills": rows}


def run_job(job: dict) -> dict:
    set_voice_settings(job)
    raw = [str(line).strip() for line in (job.get("lines") or []) if str(line).strip()]
    lines, job = _spoken_job(job, raw)
    if not lines:
        return _result("script_missing", "Phim chưa có lời để đọc.")
    phase = str(job.get("phase") or "video")
    if phase == "stills":
        return _stills_job(job, lines)
    if phase == "shorts":
        return _shorts_job(job, lines, raw)
    if phase == "preview":
        if not job.get("allowVoice"):
            return _result("voice_not_run", "Nghe thử chưa chạy. Không gọi ElevenLabs.")
        return _preview_job(job, lines)
    ready = None if job.get("force") else _ready_video(job, lines)
    if ready:
        ready["message"] = "Video đã có. Không tạo lượt giọng mới."
        return ready
    stills = _approved_stills(job, lines)
    if isinstance(stills, dict):
        return stills
    reused = _voice_source(job, lines)
    if not job.get("allowVoice") and reused is None:
        return _result("voice_not_run", "Đã có ảnh đã duyệt. Lần chạy này không gọi ElevenLabs.")
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        return _result("failed", "Không thấy ffmpeg.")
    out = _stamp_dir(job, "motion")
    (out / "task.json").write_text(
        json.dumps({"status": "running", "phase": "video", "message": "Đang dựng video.", "folder": str(out), "videoPath": None}, ensure_ascii=False),
        encoding="utf-8",
    )
    return _render_job(lines, [], out, ffmpeg, stills, reused, chosen_voice(job))


def _spoken_index(raw: list[str]) -> list[int]:
    """Vị trí câu đọc được (đã bỏ tiêu đề/chỉ dẫn) ứng với từng câu gốc; -1 nếu câu gốc bị bỏ."""
    out, count = [], 0
    for line in raw:
        if spoken_line(line):
            out.append(count)
            count += 1
        else:
            out.append(-1)
    return out


def _shorts_job(job: dict, lines: list[str], raw: list[str] | None = None) -> dict:
    """Cắt video dài đã dựng thành nhiều video ngắn theo ranh giới câu. Không gọi giọng, không dựng lại hình."""
    ready = _ready_video(job, lines)
    if not ready:
        return _result("video_missing", "Chưa có video dài khớp kịch bản này. Hãy tạo video trước rồi mới cắt.")
    src = Path(str(ready.get("videoPath") or ""))
    folder = src.parent
    mp3, alignment_path = folder / "voice.mp3", folder / "alignment.json"
    if not (mp3.is_file() and alignment_path.is_file()):
        return _result("failed", "Thiếu giọng hoặc mốc thời gian của video dài, không cắt được.")
    cuts = []
    for row in job.get("cuts") or []:
        try:
            a, b = int(row.get("from")), int(row.get("to"))
        except (TypeError, ValueError, AttributeError):
            continue
        if raw is not None and len(raw) != len(lines):  # đổi chỉ số câu gốc sang chỉ số câu đọc
            index = _spoken_index(raw)
            inside = [index[i] for i in range(max(0, a), min(b, len(raw) - 1) + 1) if index[i] >= 0]
            if not inside:
                continue
            a, b = inside[0], inside[-1]
        if 0 <= a <= b < len(lines):
            cuts.append({"from": a, "to": b, "title": str(row.get("title") or "")[:120]})
    if not cuts:
        return _result("cuts_missing", "Chưa có đoạn nào để cắt.")
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        return _result("failed", "Không thấy ffmpeg.")
    out = _stamp_dir(job, "shorts")
    shutil.copy(mp3, out / "voice.mp3")
    shutil.copy(alignment_path, out / "alignment.json")
    _wav, timeline, total = _voice_timeline(out / "voice.mp3", out / "alignment.json", lines, out, ffmpeg)
    rows = []
    for number, cut in enumerate(cuts, 1):
        start, end = timeline[cut["from"]]["start"], timeline[cut["to"]]["end"]
        target = out / f"short_{number:02d}.mp4"
        run([ffmpeg, "-y", "-loglevel", "error", "-ss", f"{start:.3f}", "-i", str(src), "-t", f"{max(0.5, end - start):.3f}",
             "-c:v", "libx264", "-preset", "veryfast", "-crf", "22", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart", str(target)])
        rows.append({"index": number, "title": cut["title"], "from": cut["from"], "to": cut["to"], "seconds": round(end - start, 1), "path": str(target)})
    payload = _result("ready", f"Đã cắt {len(rows)} video ngắn từ video dài.", out)
    payload["phase"] = "shorts"
    payload["shorts"] = rows
    (out / "shorts.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return payload


def _preview_job(job: dict, lines: list[str]) -> dict:
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        return _result("failed", "Không thấy ffmpeg.")
    out = _stamp_dir(job, "preview")
    sample = [lines[0][:180]]
    speak_eleven(out / "preview.mp3", out / "alignment.json", sample, chosen_voice(job))
    payload = _result("ready", "Đã tạo bản nghe thử.", out, out / "preview.mp3")
    payload["phase"] = "preview"
    return payload


def _encode_full_stills(out: Path, ffmpeg: str, wav_path: Path, still_paths: list[Path], timeline: list[dict], video: Path) -> None:
    listing = out / "slides.txt"
    rows: list[str] = []
    for beat in timeline:
        path = still_paths[beat["order"]].resolve()
        duration = max(float(beat["end"]) - float(beat["start"]), 1 / FPS)
        rows.append(f"file '{path.as_posix()}'")
        rows.append(f"duration {duration:.3f}")
    rows.append(f"file '{still_paths[timeline[-1]['order']].resolve().as_posix()}'")
    listing.write_text("\n".join(rows) + "\n", encoding="utf-8")
    done = subprocess.run(
        [
            ffmpeg, "-y", "-loglevel", "error",
            "-f", "concat", "-safe", "0", "-i", str(listing),
            "-i", str(wav_path),
            "-vf", f"scale={W}:{H},format=yuv420p",
            "-r", str(FPS),
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-b:v", "6M",
            "-c:a", "aac", "-shortest",
            str(video),
        ],
        capture_output=True,
    )
    if done.returncode != 0:
        detail = (done.stderr or b"").decode("utf-8", "replace")[-500:]
        raise RuntimeError(detail or "ffmpeg failed")


def _voice_timeline(mp3: Path, alignment_path: Path, lines: list[str], out: Path, ffmpeg: str):
    wav_path = out / "voice.wav"
    run([ffmpeg, "-y", "-i", str(mp3), "-ar", "24000", "-ac", "1", str(wav_path)])
    alignment = json.loads(alignment_path.read_text(encoding="utf-8"))
    marks = assign_lines(word_tokens(alignment), lines)
    raw, sample_rate, seconds = wav_info(wav_path)
    raw, marks, seconds = tighten(marks, raw, sample_rate, seconds)
    with wave.open(str(wav_path), "wb") as handle:
        handle.setnchannels(1)
        handle.setsampwidth(2)
        handle.setframerate(sample_rate)
        handle.writeframes(raw)
    timeline = []
    for index, row in enumerate(marks):
        start = 0.0 if index == 0 else row[0]["start"]
        end = marks[index + 1][0]["start"] if index + 1 < len(marks) else seconds
        local_marks = [
            {"text": mark["text"], "start": mark["start"] - start, "end": mark["end"] - start}
            for mark in row
        ]
        timeline.append({
            "text": lines[index],
            "words": lines[index].split(),
            "marks": local_marks,
            "start": start,
            "end": end,
            "speech": local_marks[-1]["end"],
            "order": index,
        })
    return wav_path, timeline, seconds


def render_motion_preview(source: Path, out: Path, seconds: float | None = 8.0) -> Path:
    """Draw motion from an existing voice. Does not call ElevenLabs."""
    import novixa_motion_graphics as graphics

    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise RuntimeError("Không thấy ffmpeg.")
    lines = json.loads((source / "lines.json").read_text(encoding="utf-8"))
    out.mkdir(parents=True, exist_ok=True)
    shutil.copy(source / "voice.mp3", out / "voice.mp3")
    shutil.copy(source / "alignment.json", out / "alignment.json")
    wav_path, timeline, _duration = _voice_timeline(out / "voice.mp3", out / "alignment.json", lines, out, ffmpeg)
    video = out / ("preview.mp4" if seconds else "video.mp4")
    graphics.render_video(timeline, wav_path, video, ffmpeg, max_seconds=seconds)
    if seconds is None:
        previous = json.loads((source / "episode.json").read_text(encoding="utf-8")) if (source / "episode.json").is_file() else {}
        payload = _result("ready", "Đã xuất video mới.", out, video)
        payload.update({
            "approval": "proposed",
            "format": "1080x1920",
            "fps": FPS,
            "voiceProvider": "elevenlabs",
            "voiceModel": MODEL,
            "voiceId": previous.get("voiceId") or VOICE_ID,
            "screens": [],
            "lines": lines,
            "stills": previous.get("stills") or [],
        })
        (out / "episode.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        (out / "lines.json").write_text(json.dumps(lines, ensure_ascii=False), encoding="utf-8")
    return video


def _render_job(
    lines: list[str],
    screens: list[tuple[Path, str]],
    out: Path,
    ffmpeg: str,
    stills: list[dict] | None = None,
    reused: Path | None = None,
    voice_id: str | None = None,
) -> dict:
    mp3 = out / "voice.mp3"
    alignment_path = out / "alignment.json"
    reuse = reused or (Path(os.environ["STOCK_REUSE"]) if os.environ.get("STOCK_REUSE") else None)
    if reuse and (reuse / "voice.mp3").is_file() and (reuse / "alignment.json").is_file():
        shutil.copy(reuse / "voice.mp3", mp3)
        shutil.copy(reuse / "alignment.json", alignment_path)
    else:
        speak_eleven(mp3, alignment_path, lines, voice_id)
    (out / "lines.json").write_text(json.dumps(lines, ensure_ascii=False), encoding="utf-8")
    wav_path, timeline, seconds = _voice_timeline(mp3, alignment_path, lines, out, ffmpeg)

    rows = []
    for item in stills or []:
        row = item if isinstance(item, dict) else {"path": str(item)}
        path = Path(str(row.get("path") or ""))
        rows.append(_merge_still(row, path) if str(row.get("path") or "").strip() else row)
    still_paths = [Path(str(row.get("path") or "")) for row in rows]
    stage = background().convert("RGBA")
    use_stills = bool(still_paths) and all(path.is_file() for path in still_paths)
    use_screens = len(screens) >= 2 and not use_stills
    sellers = shot_card(screens[0][0], screens[0][1]) if use_screens else None
    expiry = shot_card(screens[1][0], screens[1][1]) if use_screens else None
    drawn = {}
    pain_cache = {
        (left, right): pain_card(left, right)
        for left in range(len(LEFT_ITEMS) + 1)
        for right in range(RIGHT_TOTAL + 1)
    } if use_screens else {}
    origin_y = card_origin()
    words_y = caption_top(origin_y)
    total_frames = int(seconds * FPS)
    video = out / "video.mp4"
    if use_stills:
        import novixa_motion_graphics as graphics
        take_plans = graphics.plans_for_take(lines, rows)
        graphics.render_video(timeline, wav_path, video, ffmpeg, plans=take_plans)
        frame_w, frame_h = graphics.frame_size(take_plans)
        payload = _result("ready", "Đã xuất video mới.", out, video)
        payload.update({
            "approval": "proposed",
            "format": f"{frame_w}x{frame_h}",
            "fps": FPS,
            "voiceProvider": "elevenlabs",
            "voiceModel": MODEL,
            "voiceId": voice_id or VOICE_ID,
        "voiceKey": voice_key(),
            "voiceKey": voice_key(),
            "screens": [str(item[0]) for item in screens],
            "lines": lines,
            "stills": rows,
        })
        (out / "episode.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
        return payload
    drawn = {index: Image.open(path).convert("RGBA") for index, path in enumerate(still_paths)} if still_paths else {}
    proc = subprocess.Popen(
        [
            ffmpeg, "-y", "-loglevel", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS),
            "-i", "pipe:0", "-i", str(wav_path),
            "-c:v", "libx264", "-pix_fmt", "yuv420p", "-b:v", "6M", "-c:a", "aac", "-shortest", str(video),
        ],
        stdin=subprocess.PIPE,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
    )
    assert proc.stdin is not None
    slot = ((W - FRAME[0]) // 2, origin_y)
    for frame_index in range(total_frames):
        t = frame_index / FPS
        image = stage.copy()
        beat = next(item for item in timeline if item["start"] <= t < item["end"])
        local = t - beat["start"]
        words = beat["words"]
        active = active_word(words, beat["marks"], local)
        order = beat["order"]
        full_still = use_stills and drawn[order].size == (W, H)
        if full_still:
            image = drawn[order].copy()
        elif use_screens and order == len(lines) - 1:
            switch = next(
                (mark["start"] for mark in beat["marks"] if "cận" in mark["text"].lower()),
                beat["speech"] * 0.58,
            )
            title(image, screens[0][1][:24])
            intro = ease(min(local, 0.3) / 0.3)
            card = expiry if local >= switch else sellers
            paste_card(image, card, slot, intro, 40)
        elif not use_screens:
            title(image, "Novixa")
            opened = ease(min(local, 0.3) / 0.3)
            paste_card(image, drawn[order], slot, opened, 40)
        else:
            title(image, "Hàng tồn")
            progress = ease(min(local, beat["speech"] * 0.9) / max(beat["speech"] * 0.9, 0.1))
            if order == 0:
                left = min(len(LEFT_ITEMS), int(progress * len(LEFT_ITEMS)) + 1)
                right = 0
                opened = ease(min(local, 0.3) / 0.3)
            elif order == 1:
                left = len(LEFT_ITEMS)
                right = min(RIGHT_TOTAL, int(progress * RIGHT_TOTAL) + 1)
                opened = 1.0
            else:
                left, right, opened = len(LEFT_ITEMS), RIGHT_TOTAL, 1.0
            paste_card(image, pain_cache[(left, right)], slot, opened, 40)
        if not full_still:
            if order == 0:
                disclaimer(image)
            caption(image, words, active, words_y)
        proc.stdin.write(image.convert("RGB").tobytes())
    proc.stdin.close()
    if proc.wait() != 0:
        raise RuntimeError("ffmpeg failed")
    payload = _result("ready", "Đã xuất video mới.", out, video)
    payload.update({
        "approval": "proposed",
        "format": "1080x1920",
        "fps": FPS,
        "voiceProvider": "elevenlabs",
        "voiceModel": MODEL,
        "voiceId": voice_id or VOICE_ID,
        "voiceKey": voice_key(),
        "screens": [str(item[0]) for item in screens],
        "lines": lines,
        "stills": [{"index": index, "path": str(path)} for index, path in enumerate(still_paths or [])],
    })
    (out / "episode.json").write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    return payload


def main() -> int:
    for stream in (sys.stdout, sys.stderr):  # tiếng Việt an toàn dù API được khởi động với bảng mã mặc định cp1252
        try:
            stream.reconfigure(encoding="utf-8", errors="replace")
        except Exception:  # noqa: BLE001
            pass
    if len(sys.argv) != 3 or sys.argv[1] != "--job":
        print(json.dumps(_result("job_missing", "Cần --job. Không chạy năm câu cố định và không gắn stock.mp4."), ensure_ascii=False))
        return 2
    job = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
    payload = run_job(job)
    print(json.dumps(payload, ensure_ascii=False))
    return 0 if payload["status"] != "failed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
