"""Trộn âm video dài tập 02: giọng (đã chèn nghỉ) + nhạc nền vietnam-lofi nối vòng (nhỏ đi khi có giọng) + tiếng động. Ra -14 LUFS.
CẤM asian-lofi.mp3 (bài 'Bamboo Rain', bị Content ID nhận ở tập 01)."""
import sys, json, subprocess, numpy as np
sys.path.insert(0, '/home/claude/bta')
import mix_canh1 as M
SR = 48000; FX = '/home/claude/bta/sfx'
TL = json.load(open('timeline.json')); TOTAL = TL['total']; L = TL['lines']; SC = TL['scenes']; EC0 = TL['end_card'][0]
N = int(TOTAL * SR)
db = M.db; load = M.load; fades = M.fades; add = M.add; rms_db = M.rms_db
v = load('voice_full.wav')[:N]; v = np.pad(v, ((0, N - len(v)), (0, 0)))
fx = np.zeros((N, 2), np.float32)
def sfx(t, name, seg, hit, peak, fi=0, fo=0.2):
    x = fades(load(f'{FX}/{name}.mp3', *seg), fi, fo); x *= db(peak) / (np.abs(x).max() + 1e-9); add(fx, x, t - hit)
def line(scene, pos):
    return [o for o in L if o['scene'] == scene][pos]
for s in SC[1:]:
    sfx(s['start'] + 0.05, 'whoosh-nhe', (0.0, 1.4), 0.55, -18)
for sc in (1, 3, 5):                                   # "Surprise one/two/three"
    sfx(line(sc, 0)['out_s'] - 0.05, 'hit-tram', (0.0, 2.5), 0.05, -15, 0, 1.6)
sfx(line(0, 6)['out_s'] + 0.2, 'stamp', (0.0, 0.9), 0.10, -12, 0, 0.3)        # TẾT
sfx(line(2, 8)['out_s'] + 0.2, 'stamp', (0.0, 0.9), 0.10, -12, 0, 0.3)        # same date, same name
sfx(line(5, 8)['out_s'] + 0.2, 'stamp', (0.0, 0.9), 0.10, -11, 0, 0.3)        # Tet hasn't moved
sfx(line(4, 11)['out_s'] + 0.2, 'stamp', (0.0, 0.9), 0.10, -13, 0, 0.3)       # same week
sfx(line(6, 1)['out_s'] - 0.1, 'page-flip', (0.15, 0.80), 0.12, -18, 0.02, 0.15)  # tranh Táo quân
sfx(line(7, 1)['out_s'] + 0.3, 'pen-draw', (0.0, 1.6), 0.05, -22, 0.05, 0.3)     # ông đồ viết chữ
sfx(EC0 - 0.1, 'whoosh-nhe', (0.0, 1.4), 0.55, -18)
parts = []; total = 0.0
while total < TOTAL + 4:
    x = load(f'{FX}/vietnam-lofi.mp3', 0.75); x *= db(M.MUSIC_RMS) / db(rms_db(x[:10 * SR])); parts.append(x); total += len(x) / SR - 3
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
# giọng ElevenLabs nhỏ hơn tập 01 khoảng 4 dB: nâng giọng trước khi trộn để tỉ lệ giọng/nhạc giống tập 01
vg = db(-16.0 - rms_db(v[np.abs(v).max(1) > 0.01]) - 3.0)
mix = v * vg + m + fx
mix /= max(1.0, np.abs(mix).max() / 0.95)
tmp = 'mix_raw.wav'
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-f', 'f32le', '-ar', str(SR), '-ac', '2', '-i', '-', tmp], input=mix.astype(np.float32).tobytes(), check=True)
meas = subprocess.run(['ffmpeg', '-nostats', '-i', tmp, '-af', 'ebur128', '-f', 'null', '-'], capture_output=True, text=True).stderr
I = float([l for l in meas.splitlines() if l.strip().startswith('I:')][-1].split()[1])
subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', tmp, '-af', f'volume={-14.0 - I:.2f}dB,alimiter=limit=0.79:attack=2:release=60:level=disabled', 'mix02.wav'], check=True)
print('ok', I, 'voice gain dB', round(20 * np.log10(vg), 1))
