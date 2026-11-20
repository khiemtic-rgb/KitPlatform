"""Khẩu hình theo âm tiếng Việt: từ lời thoại + file thoại -> dáng môi từng khung (dùng chung mặt 2D và 3D).

Tiếng Việt mỗi chữ là một âm tiết, chữ viết gần khớp cách đọc, nên:
  1. tách mỗi chữ thành (phụ âm đầu, vần chính, âm cuối) -> chuỗi dáng môi
     (m/b/p mím kín, ph/v răng trên chạm môi dưới, a há rộng, i/ê/e kéo ngang, o/ô chu tròn, u chu nhỏ...);
  2. trong file thoại: tìm các đoạn đang nói (giữa các khoảng lặng), chia số chữ của từng cụm lời vào đoạn tương ứng,
     cắt âm tiết tại các "thung lũng" âm lượng;
  3. dựng đường cong 5 kênh (mo, ngang, tron, rang_moi, mim), miệng đi trước tiếng ~1,5 khung như phim hoạt hình,
     độ há nhân theo âm lượng thật của từng âm tiết.
Không cần bpy -> chạy/kiểm được ngoài Blender.
"""
import re
import unicodedata
import wave

import numpy as np

KENH = ("mo", "ngang", "tron", "rang_moi", "mim")

# dáng môi: (mở, kéo ngang, chu tròn, răng trên chạm môi dưới, mím)
DANG = {
    "nghi": (0.00, 0.00, 0.00, 0.0, 0.0),
    "mim": (0.00, 0.00, 0.00, 0.0, 1.0),     # m b p
    "rang_moi": (0.10, 0.10, 0.00, 1.0, 0.0),  # ph v
    "a": (1.00, 0.25, 0.00, 0.0, 0.0),       # a, ă
    "a_vua": (0.62, 0.10, 0.00, 0.0, 0.0),   # â, ơ, ư
    "e": (0.42, 1.00, 0.00, 0.0, 0.0),       # e, ê, i, y
    "o": (0.62, 0.00, 0.75, 0.0, 0.0),       # o, ô
    "u": (0.28, 0.00, 1.00, 0.0, 0.0),       # u
    "phu_am": (0.28, 0.30, 0.00, 0.0, 0.0),  # t d n l s x ch tr k g h r...
}

_INIT = ["ngh", "ng", "nh", "ch", "tr", "th", "kh", "ph", "gh", "gi", "qu",
         "b", "c", "d", "đ", "g", "h", "k", "l", "m", "n", "p", "r", "s", "t", "v", "x"]
_FINAL = ["ng", "nh", "ch", "c", "m", "n", "p", "t", "i", "y", "o", "u"]


def bo_dau(s):
    """Bỏ dấu thanh, giữ chữ cái gốc (â, ê, ô, ơ, ư, ă, đ)."""
    out = []
    for ch in unicodedata.normalize("NFD", s.lower()):
        if unicodedata.category(ch) == "Mn" and ch not in ("̂", "̆", "̛"):
            continue                       # bỏ huyền sắc hỏi ngã nặng, giữ mũ / trăng / móc
        out.append(ch)
    return unicodedata.normalize("NFC", "".join(out))


def tach_chu(word):
    """'tiền' -> ('t', 'iê', 'n'). Trả None nếu không phải chữ."""
    w = bo_dau(word)
    w = re.sub(r"[^a-zđăâêôơư]", "", w)
    if not w:
        return None
    init = ""
    for c in _INIT:
        if w.startswith(c) and len(w) > len(c):
            init = c
            break
    if init == "gi" and len(w) > 2 and w[2] not in "aăâeêoôơuưy":   # "gì" -> g + i
        init = "g"
    rest = w[len(init):]
    fin = ""
    for c in _FINAL:
        if rest.endswith(c) and len(rest) > len(c):
            fin = c
            break
    nuc = rest[:len(rest) - len(fin)] if fin else rest
    if init == "qu":                       # qu + vần: môi tròn ở đầu
        nuc = "u" + nuc
    return init, nuc, fin


def _vowel_shape(nuc):
    if not nuc:
        return "phu_am"
    v = nuc[0] if nuc[0] not in "ou" or len(nuc) == 1 else nuc[0]
    # vần đôi: lấy âm chính (âm sau nếu âm đầu là âm đệm o/u trước a/e/ê/ơ)
    if len(nuc) >= 2 and nuc[0] in "ou" and nuc[1] in "aăâeêơy":
        v = nuc[1]
    if v in "aă":
        return "a"
    if v in "âơư":
        return "a_vua"
    if v in "eêiy":
        return "e"
    if v in "oô":
        return "o"
    if v == "u":
        return "u"
    return "a_vua"


def dang_am_tiet(word):
    """Chuỗi dáng môi của một chữ: [(dáng, vị trí 0..1 trong âm tiết)]."""
    t = tach_chu(word)
    if t is None:
        return []
    init, nuc, fin = t
    seq = []
    if init in ("m", "b", "p"):
        seq.append(("mim", 0.0))
    elif init in ("ph", "v"):
        seq.append(("rang_moi", 0.0))
    elif init:
        seq.append(("phu_am", 0.0))
    # vần đệm o/u (hoa, quê, khuya...): chu môi trước
    if len(nuc) >= 2 and nuc[0] in "ou" and nuc[1] in "aăâeêơy":
        seq.append(("u" if nuc[0] == "u" else "o", 0.22))
    seq.append((_vowel_shape(nuc), 0.45))
    if fin in ("m", "p"):
        seq.append(("mim", 0.92))
    elif fin in ("o", "u"):
        seq.append(("u", 0.85))
    elif fin in ("i", "y"):
        seq.append(("e", 0.85))
    elif fin:
        seq.append(("phu_am", 0.88))
    return seq


def chu_cua(line):
    """Các chữ (âm tiết) và vị trí sau dấu câu: [(chữ, có dấu ngắt sau?)]."""
    out = []
    for tok in line.split():
        core = re.sub(r"[^\wđĐ]", "", tok, flags=re.UNICODE)
        if core:
            out.append((core, bool(re.search(r"[,.;:!?…]$", tok))))
    return out


# ---------------------------------------------------------------- âm thanh
def _rms(path, hop_s=0.01, win_s=0.03):
    with wave.open(path, "rb") as w:
        ch, sw, rate, n = w.getnchannels(), w.getsampwidth(), w.getframerate(), w.getnframes()
        raw = w.readframes(n)
    dt = {1: np.int8, 2: np.int16, 4: np.int32}[sw]
    x = np.frombuffer(raw, dtype=dt).astype(np.float32)
    if ch > 1:
        x = x.reshape(-1, ch).mean(axis=1)
    hop, win = int(rate * hop_s), int(rate * win_s)
    r = np.array([np.sqrt(np.mean(x[s:s + win] ** 2)) for s in range(0, max(1, len(x) - win), hop)])
    return r, hop_s


def _segments(r, hop, t_from, t_to, min_gap=0.12):
    """Các đoạn đang nói trong [t_from, t_to] (giây)."""
    thr = 0.12 * np.percentile(r[r > 0], 90)
    i0, i1 = int(t_from / hop), min(len(r), int(t_to / hop) + 1)
    voiced = r[i0:i1] >= thr
    segs, start, run = [], None, 0
    for k, v in enumerate(voiced):
        if v:
            if start is None:
                start = k
            run = 0
        elif start is not None:
            run += 1
            if run * hop >= min_gap:
                segs.append(((i0 + start) * hop, (i0 + k - run + 1) * hop))
                start, run = None, 0
    if start is not None:
        segs.append(((i0 + start) * hop, (i0 + len(voiced) - run) * hop))
    return [s for s in segs if s[1] - s[0] > 0.05]


def _cut(r, hop, a, b, n):
    """Chia đoạn [a,b] thành n âm tiết tại các thung lũng âm lượng sâu nhất (cách nhau >= 0,07 giây)."""
    if n <= 1:
        return [(a, b)]
    i0, i1 = int(a / hop), int(b / hop)
    sm = np.convolve(r[i0:i1 + 1], np.ones(3) / 3, mode="same") if i1 > i0 + 3 else r[i0:i1 + 1]
    cand = [k for k in range(1, len(sm) - 1) if sm[k] <= sm[k - 1] and sm[k] <= sm[k + 1]]
    cand.sort(key=lambda k: sm[k])
    gap = max(2, int(0.07 / hop))
    picked = []
    for k in cand:
        if all(abs(k - q) >= gap for q in picked) and gap <= k <= len(sm) - gap:
            picked.append(k)
        if len(picked) == n - 1:
            break
    cuts = sorted(i0 + k for k in picked)
    # thiếu thung lũng: chia đều đoạn dài nhất
    bounds = [i0] + cuts + [i1]
    while len(bounds) - 1 < n:
        lens = [bounds[i + 1] - bounds[i] for i in range(len(bounds) - 1)]
        j = int(np.argmax(lens))
        bounds.insert(j + 1, (bounds[j] + bounds[j + 1]) // 2)
    return [(bounds[i] * hop, bounds[i + 1] * hop) for i in range(n)]


def can_am_tiet(wav, lines, starts, offset=0.0, t_end=None):
    """Thời gian từng âm tiết: [(chữ, t0, t1, âm lượng 0..1)] (giây trong clip).

    starts: giây bắt đầu từng câu (tính từ đầu file thoại), như anim.sentence_starts."""
    r, hop = _rms(wav)
    total = len(r) * hop
    ref = np.percentile(r[r > 0], 90)
    out = []
    for li, line in enumerate(lines):
        s0 = starts[li]
        s1 = starts[li + 1] if li + 1 < len(starts) else total
        words = chu_cua(line)
        if not words:
            continue
        segs = _segments(r, hop, s0 - 0.05, s1)
        if not segs:
            continue
        # cụm lời theo dấu câu
        groups, cur = [], []
        for w, brk in words:
            cur.append(w)
            if brk:
                groups.append(cur)
                cur = []
        if cur:
            groups.append(cur)
        if len(groups) == len(segs):
            plan = list(zip(groups, segs))
        else:
            # phân chữ theo độ dài đoạn nói
            allw = [w for g in groups for w in g]
            durs = np.array([b - a for a, b in segs])
            cnt = np.maximum(1, np.round(durs / durs.sum() * len(allw))).astype(int)
            while cnt.sum() > len(allw):
                cnt[int(np.argmax(cnt))] -= 1
            while cnt.sum() < len(allw):
                cnt[int(np.argmax(durs / cnt))] += 1
            plan, k = [], 0
            for c, sg in zip(cnt, segs):
                plan.append((allw[k:k + c], sg))
                k += c
        for g, (a, b) in plan:
            for w, (t0, t1) in zip(g, _cut(r, hop, a, b, len(g))):
                i0, i1 = int(t0 / hop), max(int(t0 / hop) + 1, int(t1 / hop))
                loud = float(min(1.0, r[i0:i1].max() / ref)) if i1 > i0 else 0.5
                out.append((w, offset + t0, offset + t1, loud))
    if t_end is not None:
        out = [x for x in out if x[1] < t_end]
    return out


def duong_cong(syll, n_frames, fps=24, lead=1.5, mask=None):
    """Đường cong 5 kênh theo khung (mảng n_frames x 5). lead: miệng đi trước tiếng (khung)."""
    keys = []                                          # (thời điểm giây, véc tơ 5 kênh)
    prev_end = -1.0
    for w, t0, t1, loud in syll:
        if t0 - prev_end > 0.12:                      # có khoảng nghỉ trước: khép miệng
            keys.append((max(0.0, t0 - 0.10), np.array(DANG["nghi"])))
        dur = max(t1 - t0, 0.06)
        lf = 0.55 + 0.45 * loud
        for shape, pos in dang_am_tiet(w):
            v = np.array(DANG[shape], dtype=float)
            if shape not in ("mim", "nghi"):
                v[0] *= lf
            keys.append((t0 + pos * dur, v))
        prev_end = t1
    if keys:
        keys.append((prev_end + 0.10, np.array(DANG["nghi"])))
    out = np.zeros((n_frames, len(KENH)))
    if not keys:
        return out
    keys.sort(key=lambda k: k[0])
    T = np.array([k[0] for k in keys]) * fps - lead       # khung (0 = khung 1)
    V = np.array([k[1] for k in keys])
    f = np.arange(n_frames, dtype=float)
    for c in range(len(KENH)):
        out[:, c] = np.interp(f, T, V[:, c], left=0.0, right=0.0)
    # mím (m/b/p): khép kín 2 khung (đủ để mắt thấy), khung hai bên đi nửa đường
    for t, v in zip(T, V):
        if v[4] >= 0.99:
            k = int(round(t))
            for j, w in ((k - 2, 0.5), (k - 1, 1.0), (k, 1.0), (k + 1, 0.5)):
                if 0 <= j < n_frames:
                    out[j] = out[j] * (1 - w) + v * w
    if mask is not None:
        out *= mask[:, None]
    return out


def ap_vao_mat(face, curves, start_frame=1):
    """Đặt keyframe cho mặt: mặt 3D dùng đủ 5 kênh, mặt 2D (chỉ có 'mở') dùng mở x (1 - mím)."""
    for i, v in enumerate(curves):
        f = start_frame + i
        if hasattr(face, "set_viseme"):
            face.set_viseme(f, *v)
        else:
            face.set_mouth(f, float(v[0] * (1 - v[4])))
