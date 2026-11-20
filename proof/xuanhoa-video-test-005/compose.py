"""Xuân Hòa video test 005 — bản 004, dải địa chỉ dưới quầy và thẻ cuối 2,5 giây.

Giọng: lấy job/voice-parts/<id>.mp3 đã có sẵn. Không gọi API. Chạy: python compose.py
"""

from __future__ import annotations

import json
import math
import subprocess
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from character import PIVOT, Face, arm, backdrop, body, counter, customer_hand, head

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
JOB = HERE / "job"
FONTS = REPO / "docs" / "xuanhoa" / "assets" / "fonts"
W, H, FPS = 1080, 1920, 30
GREEN = (38, 150, 64)
GREEN_DARK = (24, 110, 46)
INK = (28, 36, 32)
SCALE, ANCHOR = 1.25, (540, 1190)
HEAD_AT = (280, 310)
SHIFT = -190
TITLE = "Mua thuốc cho con"
DISCLOSURE = "Nhân vật minh họa"
STRIP = "Nhà thuốc Xuân Hòa · P. Phan Đình Phùng, Thái Nguyên"
END_HOLD = 2.5
END_LINES = (
    ("Nhà thuốc Xuân Hòa", True, 72),
    ("Phường Phan Đình Phùng - Thái Nguyên", True, 48),
    ("Mở cửa 06:30 – 22:00", False, 44),
)
TILES = [("can", "CÂN", "nặng mấy cân"), ("uong", "UỐNG", "uống gì khác"), ("ngay", "NGÀY", "sốt mấy ngày")]
STRIP_FONT: ImageFont.FreeTypeFont | None = None


def font(bold: bool, size: int) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(str(FONTS / ("BeVietnamPro-ExtraBold.ttf" if bold else "BeVietnamPro-SemiBold.ttf")), size)


def fit_font(text: str, bold: bool, start: int, max_w: int, floor: int = 22):
    """Thu nhỏ đến khi chữ nằm trong max_w. Vẫn lỗi nếu tràn 1080 px."""
    size = start
    face = font(bold, size)
    width = face.getlength(text)
    while width > max_w and size > floor:
        size -= 1
        face = font(bold, size)
        width = face.getlength(text)
    if width > 1080:
        raise SystemExit(f"Chữ tràn 1080 px ({width:.0f}): {text}")
    return face, size, width


def ease(x: float) -> float:
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


def back(x: float) -> float:
    """Nảy nhẹ khi xuất hiện."""
    x = max(0.0, min(1.0, x))
    c = 1.7
    return 1 + (c + 1) * (x - 1) ** 3 + c * (x - 1) ** 2


def probe(path: Path) -> float:
    out = subprocess.run(["ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", str(path)],
                         capture_output=True, text=True, check=True)
    return float(out.stdout.strip())


def build_audio(spec: dict) -> tuple[list[dict], str]:
    final = JOB / "voice-parts"
    use_final = all((final / f"{l['id']}.mp3").exists() for l in spec["lines"])
    src = final if use_final else JOB / "voice-parts-temp"
    wav_dir = JOB / "wav"
    wav_dir.mkdir(parents=True, exist_ok=True)
    t, rows, timeline = 0.2, [], []
    gap_short, gap = wav_dir / "gap-s.wav", wav_dir / "gap.wav"
    for path, sec in ((wav_dir / "lead.wav", 0.2), (gap_short, 0.15), (gap, 0.3)):
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "lavfi", "-i", "anullsrc=r=48000:cl=mono", "-t", str(sec), str(path)], check=True)
    rows.append(wav_dir / "lead.wav")
    lines = spec["lines"]
    for i, line in enumerate(lines):
        wav = wav_dir / f"{line['id']}.wav"
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(src / f"{line['id']}.mp3"), "-ar", "48000", "-ac", "1", str(wav)], check=True)
        d = probe(wav)
        timeline.append({**line, "start": round(t, 3), "end": round(t + d, 3)})
        rows.append(wav)
        t += d
        if i < len(lines) - 1:
            short = line["beat"] == "hook" and lines[i + 1]["beat"] == "hook"
            rows.append(gap_short if short else gap)
            t += 0.15 if short else 0.3
    concat = JOB / "voice-concat.txt"
    concat.write_text("".join(f"file '{p.as_posix()}'\n" for p in rows), encoding="utf-8")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "concat", "-safe", "0", "-i", str(concat), "-c:a", "pcm_s16le", str(JOB / "voice.wav")], check=True)
    return timeline, ("elevenlabs" if use_final else "edge-tts tạm")


def envelope(lines: list[dict], total: float) -> np.ndarray:
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(JOB / "voice.wav"), "-f", "s16le", "-ac", "1", "-ar", "48000", "-"],
                         capture_output=True, check=True).stdout
    pcm = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768
    hop, n = 48000 // FPS, int(total * FPS) + 1
    rms = np.array([float(np.sqrt(np.mean(pcm[i * hop:(i + 1) * hop] ** 2))) if i * hop < len(pcm) else 0.0 for i in range(n)])
    mask = np.zeros(n, dtype=bool)
    for l in lines:
        if l["who"] == "ds":
            mask[int(l["start"] * FPS):int(l["end"] * FPS) + 1] = True
    rms[~mask] = 0
    return np.clip(rms / (np.percentile(rms[mask], 90) if mask.any() else 1.0), 0, 1.2)


def mouth_level(v: float) -> int:
    return 0 if v < 0.10 else 1 if v < 0.35 else 2 if v < 0.70 else 3


def blink_at(t: float) -> float:
    phase = (t + 1.1) % 3.4
    if phase < 0.06:
        return phase / 0.06
    if phase < 0.14:
        return 1.0
    if phase < 0.20:
        return 1 - (phase - 0.14) / 0.06
    return 0.0


def wrap(text: str, face: ImageFont.FreeTypeFont, width: int) -> list[str]:
    out, line = [], ""
    for w in text.split():
        test = (line + " " + w).strip()
        if face.getlength(test) <= width or not line:
            line = test
        else:
            out.append(line)
            line = w
    return out + ([line] if line else [])


def balanced(text: str, face: ImageFont.FreeTypeFont, width: int) -> list[str]:
    n = len(wrap(text, face, width))
    while width > 300 and len(wrap(text, face, width - 20)) == n:
        width -= 20
    return wrap(text, face, width)


# ---------- hình minh họa vẽ bằng code ----------

def icon_scale(d: ImageDraw.ImageDraw, cx: float, cy: float, col) -> None:
    d.rounded_rectangle((cx - 46, cy - 6, cx + 46, cy + 40), radius=12, fill=col)
    d.ellipse((cx - 30, cy - 52, cx + 30, cy + 8), fill=col)
    d.ellipse((cx - 22, cy - 44, cx + 22, cy), fill=(255, 255, 255))
    d.line((cx, cy - 22, cx + 12, cy - 36), fill=col, width=5)
    d.rounded_rectangle((cx - 56, cy - 62, cx + 56, cy - 50), radius=6, fill=col)


def icon_bottle(d: ImageDraw.ImageDraw, cx: float, cy: float, col) -> None:
    d.rounded_rectangle((cx - 14, cy - 58, cx + 14, cy - 40), radius=4, fill=col)
    d.rounded_rectangle((cx - 10, cy - 42, cx + 10, cy - 26), radius=3, fill=col)
    d.rounded_rectangle((cx - 32, cy - 30, cx + 32, cy + 46), radius=16, fill=col)
    d.rounded_rectangle((cx - 22, cy - 4, cx + 22, cy + 26), radius=6, fill=(255, 255, 255))


def icon_calendar(d: ImageDraw.ImageDraw, cx: float, cy: float, col) -> None:
    d.rounded_rectangle((cx - 46, cy - 40, cx + 46, cy + 46), radius=12, fill=col)
    d.rectangle((cx - 38, cy - 14, cx + 38, cy + 38), fill=(255, 255, 255))
    for k in (-22, 22):
        d.rounded_rectangle((cx + k - 5, cy - 54, cx + k + 5, cy - 30), radius=4, fill=col)
    for r in range(2):
        for c in range(3):
            x, y = cx - 26 + c * 26, cy - 2 + r * 22
            d.rounded_rectangle((x - 8, y - 6, x + 8, y + 8), radius=3, fill=col if (r, c) == (1, 2) else (205, 225, 210))


ICONS = {"can": icon_scale, "uong": icon_bottle, "ngay": icon_calendar}


def board(frame: Image.Image, t: float, tl: dict, state: dict) -> None:
    show = state["board"]
    if show <= 0:
        return
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    x0 = 650 + round((1 - ease(show)) * 460)
    x1 = x0 + 396
    d.rounded_rectangle((x0, 246, x1, 334), radius=26, fill=(*GREEN_DARK, 255))
    d.text(((x0 + x1) / 2, 290), "NHỚ 3 CHỮ", font=font(True, 46), fill=(255, 255, 255), anchor="mm")
    for k, (beat, word, hint) in enumerate(TILES):
        pop = state["tiles"][k]
        if pop <= 0:
            continue
        y0 = 356 + k * 214
        sc = back(pop)
        active = state["active"] == beat
        done = state["done"][k]
        fill = (*GREEN, 255) if active else (255, 255, 255, 240)
        ink = (255, 255, 255) if active else INK
        hgt = 190 * sc
        cy = y0 + 95
        d.rounded_rectangle((x0, cy - hgt / 2, x1, cy + hgt / 2), radius=30, fill=fill)
        if sc > 0.6:
            ICONS[beat](d, x0 + 78, cy + 4, (255, 255, 255) if active else GREEN)
            d.text((x0 + 150, cy - 26), word, font=font(True, 66), fill=ink, anchor="lm")
            d.text((x0 + 152, cy + 38), hint, font=font(False, 30), fill=(225, 245, 230) if active else (96, 110, 100), anchor="lm")
            if done and not active:
                d.ellipse((x1 - 50, cy - hgt / 2 + 12, x1 - 14, cy - hgt / 2 + 48), fill=GREEN)
                d.line((x1 - 41, cy - hgt / 2 + 30, x1 - 34, cy - hgt / 2 + 38, x1 - 22, cy - hgt / 2 + 22), fill=(255, 255, 255), width=5, joint="curve")
    if state["save"] > 0:
        a = ease(state["save"])
        d.rounded_rectangle((x0 + 40, 1012, x1 - 40, 1080), radius=34, fill=(255, 196, 0, round(255 * a)))
        d.text(((x0 + x1) / 2, 1046), "LƯU LẠI NGAY", font=font(True, 36), fill=(*INK, round(255 * a)), anchor="mm")
    frame.alpha_composite(layer)


def bubble(frame: Image.Image, line: dict, t: float) -> None:
    pop = ease((t - line["start"] + 0.12) / 0.2)
    out = 1 - ease((t - line["end"] - 0.2) / 0.2)
    a = min(pop, out)
    if a <= 0:
        return
    ds = line["who"] == "ds"
    face = font(True, 52)
    lines = balanced(line["text"], face, 860)
    spoken = ease((t - line["start"]) / max(0.3, line["end"] - line["start"]))
    total_chars = sum(len(s) for s in lines)
    shake = 0
    if line["id"] == "l01":
        k = max(0.0, 1 - (t - line["start"]) / 0.7)
        shake = round(12 * k * math.sin(t * 70))
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    lh = 70
    bh = 80 + lh * len(lines)
    y0 = 1270 + round((1 - pop) * 30)
    x0, x1 = 50 + shake, W - 50 + shake
    fill = (255, 255, 255, round(245 * a)) if ds else (255, 236, 214, round(245 * a))
    d.rounded_rectangle((x0, y0, x1, y0 + bh), radius=36, fill=fill)
    if ds:
        d.polygon([(330, y0 + 2), (360, y0 - 34), (400, y0 + 2)], fill=fill)
    else:
        d.polygon([(x1 - 120, y0 + bh - 2), (x1 + 10, y0 + bh + 40), (x1 - 40, y0 + bh - 2)], fill=fill)
    label = "Dược sĩ" if ds else "Khách hàng"
    d.text((x0 + 36, y0 + 22), label, font=font(True, 30), fill=(*(GREEN if ds else (200, 100, 30)), round(255 * a)))
    y, shown = y0 + 62, 0
    for s in lines:
        x = x0 + 36
        for word in s.split(" "):
            done = shown / max(1, total_chars) <= spoken
            d.text((x, y), word, font=face, fill=(*(INK if done else (150, 156, 152)), round(255 * a)))
            x += face.getlength(word + " ")
            shown += len(word) + 1
        y += lh
    frame.alpha_composite(layer)


def chrome(frame: Image.Image) -> None:
    d = ImageDraw.Draw(frame)
    f = font(True, 64)
    tw = f.getlength(TITLE)
    d.rounded_rectangle(((W - tw) / 2 - 36, 52, (W + tw) / 2 + 36, 148), radius=48, fill=(*GREEN, 255))
    d.text((W / 2, 100), TITLE, font=f, fill=(255, 255, 255), anchor="mm")
    d.text((W / 2, 1610), DISCLOSURE, font=font(False, 26), fill=(120, 104, 84), anchor="mm")
    d.rectangle((0, 1640, W, 1700), fill=(*GREEN, 255))
    d.text((W / 2, 1670), STRIP, font=STRIP_FONT, fill=(255, 255, 255), anchor="mm")


def make_end_card() -> tuple[Image.Image, list[dict]]:
    """Nền xanh thương hiệu, đúng 3 dòng, sau câu l09."""
    img = Image.new("RGB", (W, H), GREEN)
    d = ImageDraw.Draw(img)
    fitted = []
    for text, bold, start in END_LINES:
        face, size, width = fit_font(text, bold, start, 1000)
        box = face.getbbox(text)
        fitted.append({"text": text, "font": size, "width": round(width, 1), "height": box[3] - box[1], "face": face})
    gap = 36
    block = sum(row["height"] for row in fitted) + gap * (len(fitted) - 1)
    y = (H - block) / 2
    for row in fitted:
        d.text((W / 2, y + row["height"] / 2), row["text"], font=row["face"], fill=(255, 255, 255), anchor="mm")
        y += row["height"] + gap
    meta = [{k: v for k, v in row.items() if k != "face"} for row in fitted]
    return img, meta


def peak_db(path: Path) -> float:
    raw = subprocess.run(
        ["ffmpeg", "-v", "error", "-i", str(path), "-f", "s16le", "-ac", "1", "-ar", "48000", "-"],
        capture_output=True, check=True).stdout
    pcm = np.frombuffer(raw, dtype=np.int16)
    if pcm.size == 0:
        return -99.0
    peak = float(np.max(np.abs(pcm))) / 32768.0
    return -99.0 if peak <= 0 else 20 * math.log10(peak)


def main() -> None:
    global STRIP_FONT
    spec = json.loads((HERE / "lines.json").read_text(encoding="utf-8"))
    lines, voice_src = build_audio(spec)
    by = {l["id"]: l for l in lines}
    end_at = lines[-1]["end"]
    total = end_at + END_HOLD
    if total > 90:
        raise SystemExit(f"Dài {total:.2f}s, vượt 90 giây")
    STRIP_FONT, strip_size, strip_w = fit_font(STRIP, True, 36, 1040)
    card, card_meta = make_end_card()
    src_peak = peak_db(JOB / "voice.wav")
    # AAC có thể nhích đỉnh. Hạ PCM về −1,0 dB nếu nguồn nóng hơn, để file không vượt −0,3 dB.
    gain = min(0.0, -1.0 - src_peak)
    af = "apad" if gain > -0.05 else f"volume={gain:.2f}dB,apad"
    env = envelope(lines, total)
    bg = backdrop().convert("RGBA")
    torso = body()
    front = counter()
    a = 1 / SCALE
    board_in = by["l05"]["start"] - 0.1
    tile_times = [by["l05"]["start"] + f * (by["l05"]["end"] - by["l05"]["start"]) for f in (0.60, 0.76, 0.90)]

    out = JOB / "output.mp4"
    proc = subprocess.Popen([
        "ffmpeg", "-v", "error", "-y", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
        "-i", str(JOB / "voice.wav"), "-map", "0:v:0", "-map", "1:a:0",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p",
        "-c:a", "aac", "-b:a", "160k", "-ar", "48000", "-af", af, "-t", f"{total:.3f}", "-movflags", "+faststart", str(out),
    ], stdin=subprocess.PIPE)
    prev_dir = JOB / "preview"
    prev_dir.mkdir(exist_ok=True)
    marks = {round(x * FPS) for x in np.arange(1.0, total, 2.5)}
    smooth = 0.0
    end_frame = round((end_at + END_HOLD / 2) * FPS)
    for i in range(int(total * FPS)):
        t = i / FPS
        if t >= end_at:
            rgb = card
            if i in marks:
                rgb.save(prev_dir / f"frame-{t:05.1f}s.jpg", quality=88)
            if i == end_frame:
                rgb.save(prev_dir / "frame-endcard.jpg", quality=90)
            proc.stdin.write(rgb.tobytes())
            continue
        smooth = max(env[min(i, len(env) - 1)], smooth * 0.55)
        current = next((l for l in lines if l["start"] - 0.05 <= t <= l["end"] + 0.4), None)
        talking = any(l["who"] == "ds" and l["start"] <= t <= l["end"] for l in lines)
        listening = any(l["who"] == "kh" and l["start"] <= t <= l["end"] for l in lines)
        asking = by["l02"]["start"] <= t <= by["l02"]["end"] + 0.4

        nod = 0.0
        for l in lines:
            if l["who"] == "kh" and 0 <= t - l["end"] < 0.5:
                nod = math.sin((t - l["end"]) / 0.5 * math.pi)
        angle = (2.2 * math.sin(t * 2 * math.pi * 0.8) if talking else 0.6 * math.sin(t * 2 * math.pi * 0.25)) + (4 if listening else 0)
        dt = t - by["l04"]["start"]
        if 0 <= dt < 1.0:
            angle += 6 * math.sin(dt * 2 * math.pi * 2.5) * (1 - dt)

        # tay: đếm 1-2-3 theo từng chữ
        beat = next((l["beat"] for l in lines if l["start"] - 0.1 <= t <= l["end"]), None)
        fingers = {"can": 1, "uong": 2, "ngay": 3}.get(beat or "", 0)
        raise_p = 0.0
        if fingers:
            l = next(l for l in lines if l["beat"] == beat)
            raise_p = ease((t - l["start"] + 0.1) / 0.35) * (1 - ease((t - l["end"] + 0.25) / 0.3))

        shift = SHIFT * ease((t - board_in) / 0.6)
        face = Face(mouth=mouth_level(smooth), blink=blink_at(t), brow=1.0 if asking else 0.0)
        hd = head(face).rotate(angle, center=PIVOT, resample=Image.Resampling.BICUBIC)
        breathe = 3 * math.sin(t * 2 * math.pi / 3.6)
        char = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        char.alpha_composite(torso, (0, round(breathe)))
        arm(char, raise_p, fingers)
        char.alpha_composite(hd, (HEAD_AT[0], HEAD_AT[1] + round(breathe + 10 * nod + (1.5 * math.sin(t * 9) if talking else 0))))
        affine = (a, 0, ANCHOR[0] - a * ANCHOR[0] - a * shift, 0, a, ANCHOR[1] - a * ANCHOR[1])
        char = char.transform((W, H), Image.Transform.AFFINE, affine, resample=Image.Resampling.BICUBIC)

        frame = bg.copy()
        frame.alpha_composite(char)
        frame.alpha_composite(front)
        # tay khách thò vào khi khách nói
        reach = 0.0
        for lid in ("l01", "l03"):
            l = by[lid]
            reach = max(reach, ease((t - l["start"] + 0.2) / 0.3) * (1 - ease((t - l["end"] - 0.2) / 0.35)))
        customer_hand(frame, reach, t)
        # đẩy ống kính vào mặt dược sĩ khi khách gọi, lùi ra khi bảng xuất hiện
        push = ease((t - by["l01"]["start"]) / 0.25) * (1 - ease((t - by["l04"]["end"]) / 0.5))
        z = 1 + 0.14 * push
        if z > 1.001:
            cx, cy = W / 2, 620
            k = 1 / z
            frame = frame.transform((W, H), Image.Transform.AFFINE, (k, 0, cx - k * cx, 0, k, cy - k * cy), resample=Image.Resampling.BICUBIC)
        chrome(frame)
        active = beat if beat in ("can", "uong", "ngay") else None
        order = ["can", "uong", "ngay"]
        state = {
            "board": ease((t - board_in) / 0.5),
            "tiles": [ease((t - tt) / 0.35) for tt in tile_times],
            "active": active,
            "done": [t > by[lid]["end"] for lid in ("l06", "l07", "l08")],
            "save": ease((t - by["l09"]["start"]) / 0.4),
        }
        if active:
            state["done"] = [order.index(active) > k or d_ for k, d_ in enumerate(state["done"])]
        board(frame, t, by, state)
        if current:
            bubble(frame, current, t)
        ft = t - by["l09"]["start"]
        if 0 <= ft < 0.3:
            frame.alpha_composite(Image.new("RGBA", (W, H), (255, 255, 255, round(200 * (1 - ft / 0.3)))))

        rgb = frame.convert("RGB")
        if i in marks:
            rgb.save(prev_dir / f"frame-{t:05.1f}s.jpg", quality=88)
        proc.stdin.write(rgb.tobytes())
    proc.stdin.close()
    if proc.wait() != 0:
        raise SystemExit("FFmpeg thất bại")
    (JOB / "timeline.json").write_text(json.dumps({
        "voice": voice_src,
        "seconds": round(total, 3),
        "endCardStart": round(end_at, 3),
        "endCardSeconds": END_HOLD,
        "strip": {"text": STRIP, "font": strip_size, "widthPx": round(strip_w, 1), "limitPx": 1080},
        "endCard": card_meta,
        "voicePeakDb": round(src_peak, 2),
        "voiceGainDb": round(gain, 2),
        "lines": lines,
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print("OK", out, round(total, 2), "s, giọng:", voice_src, "dải", round(strip_w, 1), "px")


if __name__ == "__main__":
    main()
