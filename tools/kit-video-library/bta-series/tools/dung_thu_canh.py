"""Dựng clip thử cảnh 1–2 của tập 01 (47 giây) ngoài Studio, với giọng thu thử và phụ đề giả lập. Chỉ để soát hình, không đăng.

    python tools/kit-video-library/bta-series/tools/dung_thu_canh.py docs/marketing/bta/am-thanh/thu/01-thu-30s-canh1-2.mp3 wide 30 out.mp4

Cần Python có Pillow, numpy và ffmpeg trong PATH. Mốc câu (STARTS) đo từ file thu thử bằng ffmpeg silencedetect.
"""
import sys, time, subprocess
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[4]  # E:/KitPlatform
sys.path.insert(0, str(ROOT / "tools/kit-video-library/ngv-edu"))
import edu_studio as es

AUDIO = sys.argv[1]
ORIENT = sys.argv[2] if len(sys.argv) > 2 else "wide"
FPS = int(sys.argv[3]) if len(sys.argv) > 3 else 30
OUT = sys.argv[4] if len(sys.argv) > 4 else f"bta_thu_{ORIENT}.mp4"
TEXT = [
 "You might know the Chinese zodiac from a placemat at a Chinese restaurant.",
 "From Beijing to Seoul, from Tokyo to Singapore, the fourth animal is the rabbit.",
 "In Vietnam, it's a cat.", "Born in 1987, 1999 or 2011?", "In Vietnam, you're a cat.",
 "In the next five minutes: why the zodiac isn't really about animals, why the famous explanation may be wrong, and why Vietnam's cat was once a corrupt official.",
 "Surprise one: the zodiac isn't really about animals.", "It started as a clock and a compass.",
 "Each of the twelve signs marks a two-hour slot of the day, a month, and a direction.",
 "The fourth sign means five to seven in the morning, mid-spring, and due east."]
STARTS = [0.0, 4.35, 9.46, 11.40, 15.79, 17.94, 29.55, 33.81, 36.58, 42.29]
END = 47.23
SCENE = [0, 0, 0, 0, 0, 0, 1, 1, 1, 1]

txt = ROOT / "docs/marketing/bta/kich-ban/01-vietnam-zodiac-cat.txt"
lines = [l for l in txt.read_text(encoding="utf-8").split("\n") if l.strip() and not l.startswith("#") and not l.startswith("Cảnh ")]
rows = es.bind(lines, ORIENT)
first_row = {sc: next(r for r in rows if r and r["scene"] == sc) for sc in (0, 1)}
scene_start = {0: 0.0, 1: STARTS[6]}
scene_end = {0: STARTS[6], 1: END + 4.0}
W, H = es.SIZES[ORIENT]
cap_font = ImageFont.truetype(str(ROOT / "tools/kit-video-library/bta-series/fonts/BTA-Sans-Bold.ttf"), 46 if ORIENT == "wide" else 52)


def caption(img, text):
    d = ImageDraw.Draw(img, "RGBA")
    words, rows_, cur = text.split(), [], ""
    maxw = W * 0.8
    for w_ in words:
        t = (cur + " " + w_).strip()
        if d.textlength(t, font=cap_font) <= maxw or not cur: cur = t
        else: rows_.append(cur); cur = w_
    rows_.append(cur)
    rows_ = rows_[-2:]
    y0 = (H * 0.80) if ORIENT == "wide" else (H * 0.80)
    for i, r in enumerate(rows_):
        tw = d.textlength(r, font=cap_font)
        y = y0 + i * cap_font.size * 1.35
        d.rounded_rectangle((W / 2 - tw / 2 - 18, y - 8, W / 2 + tw / 2 + 18, y + cap_font.size * 1.2), 12, fill=(28, 36, 64, 215))
        d.text((W / 2, y), r, font=cap_font, fill=(255, 252, 246), anchor="ma")
    return img


proc = subprocess.Popen(["ffmpeg", "-y", "-loglevel", "warning", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}", "-r", str(FPS), "-i", "-",
                         "-i", AUDIO, "-c:v", "libx264", "-preset", "veryfast", "-crf", "20", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k",
                         "-shortest", OUT], stdin=subprocess.PIPE)
n = int(END * FPS) + FPS // 2
t0 = time.time()
for f in range(n):
    T = f / FPS
    idx = max(i for i, s in enumerate(STARTS) if s <= T)
    sc = SCENE[idx]
    first = SCENE.index(sc)
    pos = idx - first
    lt = T - STARTS[idx]
    row = first_row[sc]
    img, _ = es._render(row, T - scene_start[sc], scene_end[sc] - scene_start[sc], pos=pos, line_t=lt)
    img = caption(img.convert("RGB"), TEXT[idx])
    try:
        proc.stdin.write(img.tobytes())
    except BrokenPipeError:
        print("vỡ ống ở khung", f, img.size, img.mode); break
    if f == 30:
        print("tốc độ ~", round((time.time() - t0) / 31, 3), "giây/khung", flush=True)
proc.stdin.close(); proc.wait()
print(OUT, round(time.time() - t0, 1), "giây")
