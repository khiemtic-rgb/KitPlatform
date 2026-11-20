"""Trộn âm cảnh 1 tập 01: giọng v4 + nhạc nền (tự nhỏ đi khi có giọng) + nền phố + tiếng động.

    python mix_canh1.py <giong.wav> <thu_muc_sfx> <nhac.mp3> <ra.wav>

Mốc thời gian khớp với canh1_moi.py (dùng chung cho bản ngang và bản dọc).
Mỗi tiếng động: (lúc cú đánh rơi vào, file, đoạn cắt trong file, vị trí cú đánh trong đoạn, đỉnh dBFS, fade vào, fade ra).
"""
import sys, subprocess
from pathlib import Path
import numpy as np
sys.path.insert(0, str(Path(__file__).parent))
from nhip_canh1 import fwd, LEAD  # mốc dưới đây viết theo giọng gốc, đổi sang video mới bằng fwd()

SR = 48000
DUR = fwd(32.25)

SFX = [
    # mở đầu: nhãn 12 con giáp, bút khoanh con thỏ
    (1.20, "pop", (0.0, 0.68), 0.13, -17, 0, 0.05),
    (2.60, "pen-draw", (0.40, 1.20), 0.0, -22, 0.02, 0.25),
    # bốn thành phố: whoosh nhẹ ở mỗi lần cắt, tick khi lưới hiện từng ô
    *[(t, "whoosh-nhe", (0.0, 1.4), 0.55, -16, 0, 0.2) for t in (4.58, 5.30, 6.28, 7.05, 8.14)],
    *[(8.14 + i * 0.08, "tick", (0.0, 0.5), 0.02, -21, 0, 0.1) for i in range(4)],
    (9.47, "stamp", (0.0, 0.9), 0.10, -10, 0, 0.3),                  # RABBIT
    # lật sang Hà Nội: whoosh mạnh trùng khung lóe trắng
    (10.47, "whoosh-manh", (5.40, 7.40), 0.60, -13, 0.25, 0.5),
    (11.74, "stamp", (0.0, 0.9), 0.10, -9, 0, 0.3),                  # 4TH SIGN · CAT
    # album, năm sinh
    (12.80, "page-flip", (0.15, 0.80), 0.12, -19, 0.02, 0.15),
    *[(t, "pop", (0.0, 0.68), 0.13, -17, 0, 0.05) for t in (12.97, 13.92, 14.82, 16.12)],
    # mèo
    ("MEOW", "meow", (0.30, 0.95), 0.05, -15, 0.01, 0.2),  # rơi vào khoảng nghỉ, lúc mặt mèo hiện ra
    (19.02, "stamp", (0.0, 0.9), 0.10, -9, 0, 0.3),                  # YOU'RE A CAT
    (20.14, "pop", (0.0, 0.68), 0.13, -17, 0, 0.05),                 # IN THE NEXT 5 MINUTES
    (21.85, "riser", (4.0, 6.0), 2.0, -19, 1.2, 0.05),               # dồn lên câu hỏi nhử 1
    *[(t, "hit-tram", (0.0, 2.5), 0.05, -13, 0, 1.8) for t in (21.85, 25.05, 27.95)],
    (26.32, "stamp", (0.0, 0.9), 0.10, -10, 0, 0.3),                 # WRONG?
]
# nền phố đêm: (bắt đầu, kết thúc, vị trí lấy trong file)
AMB = [(4.58, 8.14, 3.0), (10.47, 12.75, 20.0), (24.93, 27.82, 40.0)]
AMB_RMS = -37        # dBFS
MUSIC_RMS = -27      # dBFS lúc không có giọng
DUCK_DB = 8          # nhạc nhỏ đi bấy nhiêu dB khi có giọng

def load(path, start=0.0, end=None):
    cmd = ["ffmpeg", "-v", "error", "-ss", f"{start}", "-i", path]
    if end is not None: cmd += ["-t", f"{end - start}"]
    raw = subprocess.run(cmd + ["-ac", "2", "-ar", str(SR), "-f", "f32le", "-"], capture_output=True, check=True).stdout
    return np.frombuffer(raw, np.float32).reshape(-1, 2).copy()

def fades(x, fi, fo):
    n = len(x); a = np.ones(n, np.float32)
    if fi > 0: k = min(n, int(fi * SR)); a[:k] = np.linspace(0, 1, k)
    if fo > 0: k = min(n, int(fo * SR)); a[n - k:] *= np.linspace(1, 0, k)
    return x * a[:, None]

def db(x): return 10 ** (x / 20)
def rms_db(x): return 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)

def add(bus, x, t0):
    i = int(round(t0 * SR))
    if i < 0: x = x[-i:]; i = 0
    n = min(len(x), len(bus) - i)
    if n > 0: bus[i:i + n] += x[:n]

def main(voice, sfx_dir, music, out):
    N = int(DUR * SR)
    v = load(voice)[:N]; v = np.pad(v, ((0, N - len(v)), (0, 0)))
    fx = np.zeros((N, 2), np.float32); amb = np.zeros_like(fx)
    for t, name, (s, e), hit, peak, fi, fo in SFX:
        x = fades(load(f"{sfx_dir}/{name}.mp3", s, e), fi, fo)
        x *= db(peak) / (np.abs(x).max() + 1e-9)
        tt = fwd(17.50) + 0.35 if t == "MEOW" else fwd(t)
        add(fx, x, tt - hit)
    for s, e, off in AMB:
        s2, e2 = fwd(s) - LEAD.get(s, 0), fwd(e) - LEAD.get(e, 0)
        x = fades(load(f"{sfx_dir}/pho-dem.mp3", off, off + (e2 - s2) + 0.3), 0.3, 0.4)
        add(amb, x * db(AMB_RMS) / db(rms_db(x)), s2 - 0.15)
    # nhạc: bỏ đoạn lặng đầu file, chỉnh độ to, nhỏ đi theo giọng (bám giọng nhanh, nhả chậm)
    m = load(music, 0.75, 0.75 + DUR)[:N]; m = np.pad(m, ((0, N - len(m)), (0, 0)))
    m *= db(MUSIC_RMS) / db(rms_db(m[: 10 * SR]))
    hop = SR // 100
    e = np.sqrt(np.convolve((v ** 2).mean(1), np.ones(hop) / hop, "same"))[::hop]
    act = (20 * np.log10(e + 1e-9) > -40).astype(np.float32)
    g = np.zeros_like(act); s_ = 0.0
    for i, a in enumerate(act):
        s_ += (a - s_) * (0.35 if a > s_ else 0.025)  # ~30 ms vào, ~400 ms nhả
        g[i] = s_
    gain = db(-DUCK_DB * np.interp(np.arange(N) / hop, np.arange(len(g)), g))
    m = fades(m * gain[:, None], 0.8, 0.9)
    mix = v + m + amb + fx
    mix /= max(1.0, np.abs(mix).max() / 0.95)
    tmp = out + ".raw.wav"
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(SR), "-ac", "2", "-i", "-", tmp],
                   input=mix.astype(np.float32).tobytes(), check=True)
    # đưa về -14 LUFS, đỉnh dưới -1 dBTP
    meas = subprocess.run(["ffmpeg", "-nostats", "-i", tmp, "-af", "ebur128", "-f", "null", "-"], capture_output=True, text=True).stderr
    I = float([l for l in meas.splitlines() if l.strip().startswith("I:")][-1].split()[1])
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-af",
                    f"volume={-14.0 - I:.2f}dB,alimiter=limit=0.84:attack=2:release=60:level=disabled", out], check=True)

if __name__ == "__main__":
    main(*sys.argv[1:5])
