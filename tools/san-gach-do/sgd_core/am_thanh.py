"""Âm thanh cảnh: thoại + âm nền (tổng hợp) + nhạc nền (tổng hợp hoặc file), trộn ra một file wav 48 kHz stereo.

Trong file cảnh:
  "am_thanh": {
     "nen": "dong_que",             # gió nhẹ + chim xa + lá xào xạc; null = không có
     "am_luong_nen": 0.12,
     "nhac": "buon_nhe",            # nhạc tổng hợp (Am-F-C-G, đàn + đệm), hoặc đường dẫn file .wav có bản quyền
     "nhac_tu": 9.0, "nhac_den": null,   # giây trong cảnh
     "am_luong_nhac": 0.16,
     "ha_nhac_khi_thoai": 0.45      # nhạc còn bấy nhiêu khi đang có lời thoại
  }
Không dùng thư viện ngoài (chỉ numpy + wave) để chạy được cả trong Blender trên máy người dùng.
"""
import math
import os
import wave

import numpy as np

SR = 48000


def _read_wav(path):
    with wave.open(path, "rb") as w:
        ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    if sw == 2:
        a = np.frombuffer(raw, np.int16).astype(np.float32) / 32768
    elif sw == 4:
        a = np.frombuffer(raw, np.int32).astype(np.float32) / 2147483648
    elif sw == 1:
        a = (np.frombuffer(raw, np.uint8).astype(np.float32) - 128) / 128
    else:
        raise ValueError(f"{path}: độ sâu mẫu {sw * 8} bit chưa hỗ trợ")
    a = a.reshape(-1, ch)
    if ch == 1:
        a = np.repeat(a, 2, axis=1)
    else:
        a = a[:, :2]
    if rate != SR:                                    # đổi tần số mẫu (nội suy tuyến tính, đủ cho thoại)
        t_new = np.arange(int(len(a) * SR / rate)) * rate / SR
        a = np.stack([np.interp(t_new, np.arange(len(a)), a[:, c]) for c in range(2)], 1).astype(np.float32)
    return a


def _lowpass(x, fc):
    """Lọc thông thấp mềm qua miền tần số (bậc 2, không tràn số, nhanh với tín hiệu dài)."""
    X = np.fft.rfft(x)
    f = np.fft.rfftfreq(len(x), 1 / SR)
    X *= 1 / (1 + (f / fc) ** 4)
    return np.fft.irfft(X, len(x)).astype(np.float32)


def _smooth_noise(n, rate_hz, rng):
    """Đường cong ngẫu nhiên chậm (0..1) — độ lên xuống của gió."""
    pts = max(4, int(n / SR * rate_hz) + 4)
    v = rng.random(pts)
    x = np.linspace(0, pts - 3, n)
    i = np.floor(x).astype(int)
    f = x - i
    f = f * f * (3 - 2 * f)
    return v[i] * (1 - f) + v[i + 1] * f


def ambience(seconds, rng):
    n = int(seconds * SR)
    out = np.zeros((n, 2), np.float32)
    # gió: nhiễu trắng lọc thấp, hai kênh khác nhau, lên xuống chậm
    for c in range(2):
        wn = rng.standard_normal(n).astype(np.float32)
        wind = _lowpass(_lowpass(wn, 380), 380)
        wind /= np.abs(wind).max() + 1e-9
        out[:, c] += wind * (0.35 + 0.65 * _smooth_noise(n, 0.25, rng)) * 0.55
    # lá xào xạc: nhiễu cao, từng đợt
    hiss = rng.standard_normal(n).astype(np.float32)
    hiss = hiss - _lowpass(hiss, 2500)
    gust = _smooth_noise(n, 0.4, rng) ** 3
    for c in range(2):
        out[:, c] += hiss * gust * 0.06
    # chim xa: cụm 2-4 tiếng líu lo, cách nhau 2-6 giây, lệch trái phải
    t = 1.0 + rng.random() * 2
    while t < seconds - 1:
        pan = rng.random()
        f0 = 2800 + rng.random() * 1800
        for k in range(int(rng.integers(2, 5))):
            st = int((t + k * (0.11 + rng.random() * 0.08)) * SR)
            ln = int((0.05 + rng.random() * 0.06) * SR)
            if st + ln >= n:
                break
            tt = np.arange(ln) / SR
            sweep = f0 * (1 + 0.35 * np.sin(np.pi * tt / tt[-1]) * (1 if k % 2 else -1))
            ph = 2 * np.pi * np.cumsum(sweep) / SR
            env = np.sin(np.pi * np.arange(ln) / ln) ** 2
            s = (np.sin(ph) * env * 0.12).astype(np.float32)
            out[st:st + ln, 0] += s * (1 - pan)
            out[st:st + ln, 1] += s * pan
        t += 2.0 + rng.random() * 4.0
    return out


def _note(freq, dur, decay=1.6):
    """Tiếng đàn (gần guitar mộc / piano mềm): vài hoạ âm, tắt dần theo hàm mũ, đầu tiếng mềm."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    s = np.zeros(n, np.float32)
    for h, a in ((1, 1.0), (2, 0.42), (3, 0.18), (4, 0.09), (5, 0.05)):
        s += (a * np.sin(2 * np.pi * freq * h * t) * np.exp(-t * decay * (1 + 0.6 * (h - 1)))).astype(np.float32)
    att = np.minimum(1, t / 0.008)
    return s * att


def music_buon_nhe(seconds, rng):
    """Nhạc buồn nhẹ: Am - F - C - G (mỗi hợp âm 2,4 giây), đàn rải + đệm dây kéo dài."""
    n = int(seconds * SR)
    out = np.zeros((n, 2), np.float32)
    A4 = 440.0

    def hz(semi):                                     # semi: bán cung so với A4
        return A4 * 2 ** (semi / 12)
    chords = [(-12, -9, -5, 0), (-16, -12, -9, -4), (-21, -17, -14, -9), (-14, -10, -7, -2)]   # Am F C G
    bar = 2.4
    t = 0.0
    k = 0
    while t < seconds:
        ch = chords[k % 4]
        # đệm dây: hợp âm kéo dài, vào ra chậm
        ln = int(min(bar + 0.6, seconds - t) * SR)
        st = int(t * SR)
        if ln > 0:
            tt = np.arange(ln) / SR
            env = np.minimum(1, tt / 0.8) * np.minimum(1, (ln / SR - tt) / 0.8)
            pad = np.zeros(ln, np.float32)
            for semi in ch[:3]:
                f = hz(semi - 12)
                pad += (np.sin(2 * np.pi * f * tt) + 0.3 * np.sin(2 * np.pi * f * 2.003 * tt)).astype(np.float32)
            pad *= env.astype(np.float32) * 0.07
            out[st:st + ln, 0] += pad
            out[st:st + ln, 1] += pad * 0.9
        # đàn rải: 6 nốt mỗi ô nhịp
        pattern = (0, 2, 1, 3, 2, 1)
        for j, idx in enumerate(pattern):
            tn = t + j * bar / 6
            if tn >= seconds:
                break
            nt = _note(hz(ch[idx]), min(2.2, seconds - tn)) * (0.22 if j else 0.28)
            s0 = int(tn * SR)
            pan = 0.35 + 0.3 * (idx / 3)
            out[s0:s0 + len(nt), 0] += nt * (1 - pan) * 1.4
            out[s0:s0 + len(nt), 1] += nt * pan * 1.4
        t += bar
        k += 1
    return out


def _envelope(x, win=0.25):
    m = np.abs(x).max(1)
    k = max(1, int(win * SR))
    c = np.cumsum(np.concatenate([[0], m]))
    e = (c[k:] - c[:-k]) / k
    return np.concatenate([e, np.full(len(m) - len(e), e[-1] if len(e) else 0)])


def mix(spec, wav, offset, seconds, out_path, base_dir="."):
    """Trộn thoại (bắt đầu ở `offset` giây) + nền + nhạc. Trả đường dẫn wav (bắt đầu ở 0 giây)."""
    cfg = spec.get("am_thanh") or {}
    rng = np.random.default_rng(int(cfg.get("hat_giong", 11)))
    n = int(seconds * SR)
    out = np.zeros((n, 2), np.float32)
    voice = np.zeros((n, 2), np.float32)
    if wav and os.path.exists(wav):
        v = _read_wav(wav)
        v = v / (np.abs(v).max() + 1e-9) * 0.7
        s0 = int(offset * SR)
        m = min(len(v), n - s0)
        if m > 0:
            voice[s0:s0 + m] = v[:m]
    out += voice
    if cfg.get("nen", "dong_que"):
        amb = ambience(seconds, rng)
        out += amb / (np.abs(amb).max() + 1e-9) * float(cfg.get("am_luong_nen", 0.12))
    nh = cfg.get("nhac")
    if nh:
        t0 = float(cfg.get("nhac_tu", 0.0))
        t1 = float(cfg.get("nhac_den") or seconds)
        dur = max(0.0, t1 - t0)
        if dur > 0.5:
            if nh == "buon_nhe":
                mu = music_buon_nhe(dur, rng)
            else:
                p = nh if os.path.isabs(nh) else os.path.join(base_dir, nh)
                mu = _read_wav(p)[:int(dur * SR)]
            mu = mu / (np.abs(mu).max() + 1e-9)
            tt = np.arange(len(mu)) / SR
            fade = np.minimum(1, tt / 2.5) * np.minimum(1, (len(mu) / SR - tt) / 2.0)
            mu = mu * fade[:, None].astype(np.float32) * float(cfg.get("am_luong_nhac", 0.16))
            s0 = int(t0 * SR)
            m = min(len(mu), n - s0)
            seg = mu[:m]
            # hạ nhạc khi có lời thoại (mềm, theo độ lớn tiếng nói)
            ev = _envelope(voice[s0:s0 + m])
            duck = 1 - (1 - float(cfg.get("ha_nhac_khi_thoai", 0.45))) * np.clip(ev / 0.08, 0, 1)
            out[s0:s0 + m] += seg * duck[:, None].astype(np.float32)
    peak = np.abs(out).max()
    if peak > 0.95:
        out *= 0.95 / peak
    pcm = (np.clip(out, -1, 1) * 32767).astype(np.int16)
    with wave.open(out_path, "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    return out_path
