"""Lớp chuyển động: nghỉ (thở, nghiêng đầu, chớp mắt), miệng theo thoại, biểu cảm theo thời gian.

Mọi hàm nhận FaceRig (rig.build) và đặt keyframe; không tự dựng gì.
"""
import wave

import numpy as np

FPS = 24


def audio_seconds(path):
    with wave.open(path, "rb") as w:
        return w.getnframes() / w.getframerate()


def idle(face, end, blink_off=False, fixed_blink=None, phase=0):
    """Đầu nghiêng chậm, thở đều (nhịp 4 giây, lặp đến hết clip), chớp mắt lệch nhịp 2,5–4 giây."""
    for base in range(0, end, 96):
        face.set_tilt(base + 1, -2)
        face.set_tilt(base + 48, 7)
        face.set_breath(base + 1, 0.0)
        face.set_breath(base + 24, 0.045)
        face.set_breath(base + 48, 0.0)
        face.set_breath(base + 72, 0.045)
    last = ((end - 1) // 96 + 1) * 96
    face.set_tilt(last, -2)
    face.set_breath(last, 0.0)

    face.set_blink(1, fixed_blink if fixed_blink is not None else 0.0)
    if blink_off or fixed_blink is not None:
        return
    shape = ((0, 0), (2, 0.55), (3, 1), (5, 1), (9, 0))   # ~0,37 giây: khép nhanh, mở chậm hơn (như chớp thật)
    starts = [28 + phase, 70 + phase]          # phase: lệch nhịp chớp giữa các diễn viên
    gaps = (84, 66, 92, 74, 60, 88)
    k = 0
    while starts[-1] + gaps[k % len(gaps)] + 9 < end:
        starts.append(starts[-1] + gaps[k % len(gaps)])
        k += 1
    for st in starts:
        for d, v in shape:
            if st + d <= end:
                face.set_blink(st + d, v)


def audio_envelope(path, fps, n_frames, offset_frames=0):
    """Âm lượng từng khung, chuẩn hoá 0..1 (mức 90% = mở hết), có ngưỡng im lặng và làm trơn 3 khung."""
    with wave.open(path, "rb") as w:
        ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    dt = {1: np.int8, 2: np.int16, 4: np.int32}[sw]
    data = np.frombuffer(raw, dtype=dt).astype(np.float32)
    if ch > 1:
        data = data.reshape(-1, ch).mean(axis=1)
    hop = rate / fps
    env = np.zeros(n_frames)
    for f in range(n_frames):
        a = f - offset_frames
        if a < 0:
            continue
        seg = data[int(a * hop):int((a + 1) * hop)]
        env[f] = float(np.sqrt(np.mean(seg * seg))) if len(seg) else 0.0
    ref = np.percentile(env[env > 0], 90) if np.any(env > 0) else 1.0
    gate = 0.12 * ref
    out = np.clip((env - gate) / max(ref - gate, 1e-6), 0.0, 1.0) ** 0.7
    return np.convolve(np.pad(out, 1, mode="edge"), np.ones(3) / 3, mode="valid")


def mouth_from_audio(face, path, end, offset_frames=0):
    env = audio_envelope(path, FPS, end, offset_frames)
    for f, v in enumerate(env, start=1):
        face.set_mouth(f, float(v))


SAMPLE_TALK = [(1, 0), (10, 0), (13, 0.8), (16, 0.25), (19, 0.9), (22, 0.3), (25, 0.6), (28, 0.1), (31, 1.0),
               (34, 0.35), (37, 0.7), (40, 0.0), (52, 0), (55, 0.6), (58, 0.2), (61, 0.95), (64, 0.4),
               (67, 0.75), (70, 0.15), (73, 0.85), (76, 0.3), (79, 0.5), (82, 0.0), (96, 0)]


def mouth_sample(face):
    for f, v in SAMPLE_TALK:
        face.set_mouth(f, v)


def expressions(face, timeline, library, default_ramp=0.35):
    """timeline: [{"giay": 0, "bieu_cam": "lo", "chuyen": 0.3}, ...]; library: data/bieu_cam.json.

    Mỗi biểu cảm là trọng số các dáng lông mày ({"Lo": 1} ...). Chuyển dần trong "chuyen" giây.
    """
    def weights(name):
        if name not in library:
            raise KeyError(f"Biểu cảm '{name}' chưa có trong data/bieu_cam.json")
        return library[name].get("may", {})

    current = weights("thuong")
    face.set_brow(1, **current)
    for item in sorted(timeline, key=lambda x: x["giay"]):
        start = max(1, int(round(item["giay"] * FPS)) + 1)
        ramp = max(1, int(round(item.get("chuyen", default_ramp) * FPS)))
        face.set_brow(start, **current)
        current = weights(item["bieu_cam"])
        face.set_brow(start + ramp, **current)


def smooth(owners):
    """Mọi keyframe nội suy Bezier tự động kẹp, cho chuyển động mềm."""
    for obj in owners:
        ad = obj.animation_data
        if not ad or not ad.action:
            continue
        curves = []
        if hasattr(ad.action, "layers"):
            for layer in ad.action.layers:
                for strip in layer.strips:
                    bag = strip.channelbag(ad.action_slot) if hasattr(strip, "channelbag") else None
                    if bag:
                        curves.extend(bag.fcurves)
        if not curves and hasattr(ad.action, "fcurves"):
            curves.extend(ad.action.fcurves)
        for fcu in curves:
            for kp in fcu.keyframe_points:
                kp.interpolation = "BEZIER"
                kp.handle_left_type = "AUTO_CLAMPED"
                kp.handle_right_type = "AUTO_CLAMPED"


# ---------------- mốc theo câu thoại
_BREAK = set(",.;:!?…")


def speech_gaps(path, min_gap=0.25):
    """Các khoảng lặng trong thoại: [(bắt đầu, độ dài)], cùng thời điểm bắt đầu và kết thúc lời nói (giây)."""
    with wave.open(path, "rb") as w:
        ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    dt = {1: np.int8, 2: np.int16, 4: np.int32}[sw]
    x = np.frombuffer(raw, dtype=dt).astype(np.float32)
    if ch > 1:
        x = x.reshape(-1, ch).mean(axis=1)
    hop, win = int(rate * 0.01), int(rate * 0.04)
    rms = np.array([np.sqrt(np.mean(x[s:s + win] ** 2)) for s in range(0, max(1, len(x) - win), hop)])
    thr = 0.1 * np.percentile(rms, 90)
    voiced = rms >= thr
    idx = np.nonzero(voiced)[0]
    if len(idx) == 0:
        return [], 0.0, 0.0
    t0, t1 = idx[0] * 0.01, idx[-1] * 0.01
    gaps, run = [], 0
    for k in range(idx[0], idx[-1] + 1):
        if not voiced[k]:
            run += 1
        else:
            if run * 0.01 >= min_gap:
                gaps.append(((k - run) * 0.01, run * 0.01))
            run = 0
    return gaps, t0, t1


def sentence_starts(path, lines):
    """Thời điểm bắt đầu từng câu (theo danh sách lời `lines`) trong file thoại.

    Ghép theo thứ tự các chỗ ngắt có dấu câu trong lời với các khoảng lặng đo được (quy hoạch động,
    được phép bỏ dấu phẩy không ngừng hoặc khoảng lặng không có dấu). Trả (danh sách giây, ghi chú).
    """
    gaps, t0, t1 = speech_gaps(path)
    text = " ".join(lines)
    total = max(1, len(text))
    # các chỗ ngắt: (vị trí ký tự, là ranh giới câu?)
    breaks, pos = [], 0
    for li, ln in enumerate(lines):
        for ci, chx in enumerate(ln):
            if chx in _BREAK and ci < len(ln) - 1 and ln[ci + 1] == " ":
                breaks.append((pos + ci + 1, False))
        pos += len(ln) + 1
        if li < len(lines) - 1:
            breaks.append((pos - 1, True))
    span = max(t1 - t0, 1e-3)
    exp = [t0 + p / total * span for p, _ in breaks]
    G = [g0 + gl / 2 for g0, gl in gaps]
    nb, ng = len(breaks), len(G)
    INF = 1e9
    skip_b, skip_g = 0.9, 0.6
    cost = [[INF] * (ng + 1) for _ in range(nb + 1)]
    back = [[None] * (ng + 1) for _ in range(nb + 1)]
    cost[0][0] = 0.0
    for i in range(nb + 1):
        for j in range(ng + 1):
            c = cost[i][j]
            if c >= INF:
                continue
            if i < nb and j < ng:
                v = c + abs(exp[i] - G[j]) - 0.5 * gaps[j][1]
                if v < cost[i + 1][j + 1]:
                    cost[i + 1][j + 1], back[i + 1][j + 1] = v, (i, j, "m")
            if i < nb:
                v = c + (skip_b * 2 if breaks[i][1] else skip_b)
                if v < cost[i + 1][j]:
                    cost[i + 1][j], back[i + 1][j] = v, (i, j, "b")
            if j < ng:
                v = c + skip_g
                if v < cost[i][j + 1]:
                    cost[i][j + 1], back[i][j + 1] = v, (i, j, "g")
    match = {}
    i, j = nb, ng
    while back[i][j]:
        pi, pj, kind = back[i][j]
        if kind == "m":
            match[pi] = pj
        i, j = pi, pj
    starts, note = [t0], []
    for bi, (p, is_line) in enumerate(breaks):
        if not is_line:
            continue
        if bi in match:
            g0, gl = gaps[match[bi]]
            starts.append(round(g0 + gl, 2))
        else:
            starts.append(round(exp[bi], 2))
            note.append(f"câu {len(starts)} ước theo độ dài chữ")
    return starts, note
