"""Trộn âm video dài tập 01: giọng (đã chèn nghỉ) + nhạc nền nối vòng (nhỏ đi khi có giọng) + tiếng động. Ra -14 LUFS."""
import sys, json, subprocess, numpy as np
sys.path.insert(0, '/home/claude/dai'); sys.path.insert(0, '/home/claude/bta')
from nhip_dai import fwd, LEAD
import mix_canh1 as M   # dùng lại danh sách tiếng động cảnh 1 và các hàm nạp/fade
SR = 48000; FX = '/home/claude/bta/sfx'
TL = json.load(open('timeline.json')); TOTAL = TL['total']; L = TL['lines']; SC = TL['scenes']; EC0 = TL['end_card'][0]
N = int(TOTAL * SR)
db = M.db; load = M.load; fades = M.fades; add = M.add; rms_db = M.rms_db
v = load('voice_full.wav')[:N]; v = np.pad(v, ((0, N - len(v)), (0, 0)))
fx = np.zeros((N, 2), np.float32); amb = np.zeros_like(fx)
def sfx(t, name, seg, hit, peak, fi=0, fo=0.2):
    x = fades(load(f'{FX}/{name}.mp3', *seg), fi, fo); x *= db(peak) / (np.abs(x).max() + 1e-9); add(fx, x, t - hit)
# cảnh 1: đúng danh sách của bản cảnh 1 v6, đổi mốc sang video dài
for t, name, seg, hit, peak, fi, fo in M.SFX:
    tt = fwd(17.50) + 0.35 if t == 'MEOW' else fwd(t)
    sfx(tt, name, seg, hit, peak, fi, fo)
for s, e, off in M.AMB:
    s2, e2 = fwd(s) - LEAD.get(s, 0), fwd(e) - LEAD.get(e, 0)
    x = fades(load(f'{FX}/pho-dem.mp3', off, off + (e2 - s2) + 0.3), 0.3, 0.4)
    add(amb, x * db(M.AMB_RMS) / db(rms_db(x)), s2 - 0.15)
# cảnh 2–11: whoosh nhẹ khi chuyển cảnh; nhấn ở các "Surprise"; lật giấy khi hiện tranh Đông Hồ; dấu "BRIBERY"
for s in SC[1:]:
    sfx(s['start'] + 0.05, 'whoosh-nhe', (0.0, 1.4), 0.55, -18)
for li in (6, 37, 49):
    sfx(L[li]['out_s'] - 0.05, 'hit-tram', (0.0, 2.5), 0.05, -15, 0, 1.6)
sfx(L[54]['out_s'] - 0.1, 'page-flip', (0.15, 0.80), 0.12, -18, 0.02, 0.15)   # tranh Đám cưới chuột
sfx(L[61]['out_s'] - 0.1, 'page-flip', (0.15, 0.80), 0.12, -19, 0.02, 0.15)   # bài đồng dao
sfx(L[60]['out_s'] + 0.35, 'stamp', (0.0, 0.9), 0.10, -11, 0, 0.3)            # BRIBERY
sfx(L[75]['out_s'] + 0.9, 'meow', (0.30, 0.95), 0.05, -18, 0.01, 0.2)         # "you're a cat in Hanoi"
sfx(EC0 - 0.1, 'whoosh-nhe', (0.0, 1.4), 0.55, -18)
# nhạc: nối vietnam-lofi / asian-lofi, hoà 3 giây chỗ nối
parts = []; total = 0.0; k = 0
names = ['vietnam-lofi', 'vietnam-lofi']  # asian-lofi = 'Bamboo Rain' (ZephiraMusic) bị Content ID nhận, không dùng
while total < TOTAL + 4:
    x = load(f'{FX}/{names[k % 2]}.mp3', 0.75); x *= db(M.MUSIC_RMS) / db(rms_db(x[:10 * SR])); parts.append(x); total += len(x) / SR - 3; k += 1
xf = 3 * SR; m = parts[0]
for x in parts[1:]:
    r = np.linspace(1, 0, xf)[:, None]
    m = np.concatenate([m[:-xf], m[-xf:] * r + x[:xf] * (1 - r), x[xf:]])
m = m[:N]; m = np.pad(m, ((0, N - len(m)), (0, 0)))
hop = SR // 100
e = np.sqrt(np.convolve((v ** 2).mean(1), np.ones(hop) / hop, 'same'))[::hop]
act = (20 * np.log10(e + 1e-9) > -40).astype(np.float32)
g = np.zeros_like(act); s_ = 0.0
for i, a in enumerate(act):
    s_ += (a - s_) * (0.35 if a > s_ else 0.025); g[i] = s_
gain = db(-M.DUCK_DB * np.interp(np.arange(N) / hop, np.arange(len(g)), g))
lift = np.ones(N, np.float32); i0 = int(EC0 * SR); lift[i0:] = np.minimum(db(4), 1 + (db(4) - 1) * np.arange(N - i0) / SR / 1.5)
m = fades(m * (gain * lift)[:, None], 1.0, 2.5)
mix = v + m + amb + fx
mix /= max(1.0, np.abs(mix).max() / 0.95)
tmp = 'mix_raw.wav'
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', tmp], input=mix.astype(np.float32).tobytes(), check=True)
meas = subprocess.run(['ffmpeg', '-nostats', '-i', tmp, '-af', 'ebur128', '-f', 'null', '-'], capture_output=True, text=True).stderr
I = float([l for l in meas.splitlines() if l.strip().startswith('I:')][-1].split()[1])
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', tmp, '-af', f'volume={-14.0 - I:.2f}dB,alimiter=limit=0.84:attack=2:release=60:level=disabled', 'mix_dai.wav'], check=True)
print('ok', I)
