"""Lớp đo đạc: tự dò khe mắt, đường môi, lông mày trên ô mặt chính của ảnh mẫu.

Chỉ dùng numpy, chạy được cả trong Blender lẫn Python thường.
Mọi toạ độ trả về là toạ độ local của lưới mặt (-1..1), y hướng lên.
Kết quả được lưu ở data/do_dac/<ma>.json; đặt "khoa": true trong file đó để giữ số đo đã sửa tay.
"""
import json
import os

import numpy as np

EYE_KNOTS = (-1.0, -0.7, -0.5, -0.3, -0.1, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0)

def panel(a):
    H, W = a.shape[:2]
    x0, x1 = 0.006 * W, (1 / 3 - 0.006) * W
    yt, yb = (1 - 0.994) * H, (1 - 0.506) * H
    return x0, x1, yt, yb

def is_skin(a):
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    mx = a.max(-1); mn = a.min(-1)
    # Da: cam rõ (đỏ hơn lục hẳn). Bóng hồng nhạt trên lòng trắng có r-g nhỏ nên không bị tính là da.
    return (r > 130) & (g > 70) & (r - b > 65) & (r - g > 32) & (g >= b)

def measure_eyes(a):
    """a: mảng RGB 0..255, hàng 0 là đỉnh ảnh."""
    a = a.astype(int)
    x0, x1, yt, yb = panel(a)
    def lx(X): return (X - x0) / (x1 - x0) * 2 - 1
    def ly(Y): return (1 - (Y - yt) / (yb - yt)) * 2 - 1
    def X(lx_): return x0 + (lx_ + 1) / 2 * (x1 - x0)
    def Y(ly_): return yt + (1 - (ly_ + 1) / 2) * (yb - yt)
    sub = a[int(Y(0.6)):int(Y(-0.1)), int(X(-0.6)):int(X(0.6))]
    oy, ox = int(Y(0.6)), int(X(-0.6))
    skin = is_skin(sub)
    mn = sub.min(-1); mx = sub.max(-1)
    white = (mn > 165) & (mx - mn < 50)
    # chỉ giữ lòng trắng nằm giữa da: có da ở cả trái lẫn phải trong cùng hàng, cách không xa
    rows, cols = np.nonzero(white)
    keep = []
    for r_, c_ in zip(rows, cols):
        left = skin[r_, max(0, c_ - 60):c_].any()
        right = skin[r_, c_ + 1:c_ + 61].any()
        up = skin[max(0, r_ - 40):r_, c_].any()
        if left and right and up:
            keep.append((r_, c_))
    keep = np.array(keep)
    # chia hai mắt theo khe lớn nhất trên trục x
    xs = np.sort(np.unique(keep[:, 1]))
    gap = np.argmax(np.diff(xs))
    split = (xs[gap] + xs[gap + 1]) / 2
    eyes = []
    for side in (keep[keep[:, 1] < split], keep[keep[:, 1] >= split]):
        cy_px = int(np.median(side[:, 0]))
        cx0, cx1 = side[:, 1].min(), side[:, 1].max()
        # mở rộng biên để bắt cả tròng và khoé
        w = cx1 - cx0
        lo, hi = cx0 - int(0.25 * w), cx1 + int(0.25 * w)
        tops, bots, cols_ = {}, {}, []
        for c_ in range(max(lo, 0), min(hi, sub.shape[1] - 1) + 1):
            if skin[cy_px, c_]:
                # có thể khoé lệch hàng: tìm hàng không-da gần nhất trong ±6px
                near = [d for d in range(-6, 7) if 0 <= cy_px + d < sub.shape[0] and not skin[cy_px + d, c_]]
                if not near:
                    continue
                start = cy_px + min(near, key=abs)
            else:
                start = cy_px
            def walk(step):
                r_, gapn = start, 0
                last = start
                while 0 <= r_ + step < sub.shape[0]:
                    r_ += step
                    if skin[r_, c_]:
                        gapn += 1
                        if gapn > 4:
                            break
                    else:
                        gapn = 0
                        last = r_
                return last
            tops[c_] = walk(-1); bots[c_] = walk(1); cols_.append(c_)
        cols_ = np.array(cols_)
        # chỉ giữ dải cột liền nhau quanh tâm mắt
        cl, cr = cols_.min(), cols_.max()
        center = (cx0 + cx1) // 2
        run = [c_ for c_ in range(cl, cr + 1) if c_ in tops]
        # cắt ở chỗ đứt
        segs, curr = [], [run[0]]
        for c_ in run[1:]:
            if c_ == curr[-1] + 1: curr.append(c_)
            else: segs.append(curr); curr = [c_]
        segs.append(curr)
        seg = min(segs, key=lambda s: 0 if s[0] <= center <= s[-1] else min(abs(s[0]-center), abs(s[-1]-center)))
        c_in, c_out = seg[0], seg[-1]
        top_l = [ly(tops[c_] + oy) for c_ in seg]
        bot_l = [ly(bots[c_] + oy) for c_ in seg]
        xl = [lx(c_ + ox) for c_ in seg]
        cx = (xl[0] + xl[-1]) / 2
        half = (xl[-1] - xl[0]) / 2 * 1.12
        def sample(vals, s):
            xx = cx + s * half
            return float(np.interp(xx, xl, vals))
        corner_l = 0.5 * (sample(top_l, -1) + sample(bot_l, -1))
        corner_r = 0.5 * (sample(top_l, 1) + sample(bot_l, 1))
        top = [corner_l] + [sample(top_l, s) for s in EYE_KNOTS[1:-1]] + [corner_r]
        bot = [corner_l] + [sample(bot_l, s) for s in EYE_KNOTS[1:-1]] + [corner_r]
        bot = [min(t, b) for t, b in zip(top, bot)]
        # Khoảng da từ đỉnh hàng mi lên tới lông mày (điểm tối đầu tiên phía trên), lấy ở 1/3 giữa mắt.
        gaps = []
        n = len(seg)
        for c_ in seg[n // 3: max(n // 3 + 1, 2 * n // 3)]:
            r_ = tops[c_] - 1
            while r_ > 0 and skin[r_, c_]:
                r_ -= 1
            if r_ > 0:
                gaps.append(ly(r_ + oy) - ly(tops[c_] + oy))
        brow_gap = float(np.median(gaps)) if gaps else 0.08
        height = max(t - b for t, b in zip(top, bot))
        eyes.append(dict(cx=cx, half_w=half, top=top, bottom=bot, brow_gap=brow_gap, height=height))
    return eyes


# ---- Dò đường khép môi ----
def measure_mouth(a, eyes):
    a = a.astype(float)
    H, W = a.shape[:2]
    x0, x1 = 0.006 * W, (1 / 3 - 0.006) * W
    yt, yb = (1 - 0.994) * H, (1 - 0.506) * H
    def X(l): return x0 + (l + 1) / 2 * (x1 - x0)
    def Y(l): return yt + (1 - (l + 1) / 2) * (yb - yt)
    def lx(px): return (px - x0) / (x1 - x0) * 2 - 1
    def ly(py): return (1 - (py - yt) / (yb - yt)) * 2 - 1
    mid = 0.5 * (eyes[0]["cx"] + eyes[1]["cx"])
    eye_y = 0.5 * (np.mean(eyes[0]["top"]) + np.mean(eyes[1]["top"]))
    span = eyes[1]["cx"] - eyes[0]["cx"]
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    lum = 0.3 * r + 0.59 * g + 0.11 * b
    warm = (r - b > 40) & (r > 90)
    # vùng tìm: dưới mắt từ 0.55 đến 1.35 lần khoảng cách hai mắt, rộng ±0.5 khoảng cách mắt
    ya, yb_ = int(Y(eye_y - 0.6 * span)), int(Y(eye_y - 1.75 * span))
    xa, xb = int(X(mid - 0.55 * span)), int(X(mid + 0.55 * span))
    # 1) hàng của đường môi: tối nhất khi lấy trung bình trên một dải ngang rộng (lỗ mũi chỉ là hai chấm nhỏ)
    band = lum[ya:yb_, int(X(mid - 0.22 * span)):int(X(mid + 0.22 * span)) + 1]
    okb = warm[ya:yb_, int(X(mid - 0.22 * span)):int(X(mid + 0.22 * span)) + 1]
    band = np.where(okb, band, np.nan)
    # độ tối so với trung bình hai dải trên/dưới 10px
    rowm = np.nanmean(band, axis=1)
    score = np.full_like(rowm, -1e9)
    for k in range(10, len(rowm) - 10):
        score[k] = 0.5 * (np.nanmean(rowm[k - 10:k - 4]) + np.nanmean(rowm[k + 4:k + 10])) - rowm[k]
    # các đỉnh tối đủ mạnh; chọn đỉnh đỏ nhất (môi đỏ hơn bóng dưới mũi)
    xa2, xb2 = int(X(mid - 0.22 * span)), int(X(mid + 0.22 * span)) + 1
    red = (r - g)[ya:yb_, xa2:xb2]
    smax = np.nanmax(score)
    cands = [k for k in range(1, len(score) - 1)
             if score[k] >= 0.45 * smax and score[k] >= score[k - 1] and score[k] >= score[k + 1]]
    def redness(k):
        d = band[k]
        sel = d <= np.nanpercentile(d, 30)
        return float(np.nanmean(np.where(sel, red[k], np.nan)))
    row0 = ya + max(cands, key=redness)
    win = max(4, int(0.035 / 2 * (yb - yt)))
    best = {}
    for c in range(xa, xb + 1):
        r0, r1 = row0 - win, row0 + win
        col = lum[r0:r1, c].copy()
        col[~warm[r0:r1, c]] = 1e9
        k = int(np.argmin(col))
        if col[k] > 1e8:
            continue
        up = lum[r0 + k - 8:r0 + k - 3, c]
        dn = lum[r0 + k + 3:r0 + k + 8, c]
        if len(up) == 0 or len(dn) == 0:
            continue
        best[c] = (r0 + k, min(up.mean(), dn.mean()) - col[k])
    # chuỗi cột liền quanh giữa mặt có tương phản đủ
    cm = int(X(mid))
    thr = 0.35 * np.median([best[c][1] for c in best if abs(c - cm) < (xb - xa) * 0.12])
    cols = [c for c in sorted(best) if best[c][1] > thr]
    cols_set = set(cols)
    start = min(cols, key=lambda c: abs(c - cm))
    lo = hi = start
    while lo - 1 in cols_set or lo - 2 in cols_set:
        lo -= 1 if lo - 1 in cols_set else 2
    while hi + 1 in cols_set or hi + 2 in cols_set:
        hi += 1 if hi + 1 in cols_set else 2
    pts = [(lx(c), ly(best[c][0])) for c in range(lo, hi + 1) if c in best]
    xs = np.array([p[0] for p in pts]); ys = np.array([p[1] for p in pts])
    # làm trơn đường môi
    k = 7
    ys_s = np.convolve(np.pad(ys, k // 2, mode="edge"), np.ones(k) / k, mode="valid")
    return dict(x=xs.tolist(), y=ys_s.tolist(), left=float(xs[0]), right=float(xs[-1]),
                cx=float(0.5 * (xs[0] + xs[-1])), cy=float(np.interp(0.5 * (xs[0] + xs[-1]), xs, ys_s)),
                span=float(span))


# ---- Dò lông mày ----
def measure_brows(a, eyes):
    a = a.astype(float)
    H, W = a.shape[:2]
    x0, x1 = 0.006 * W, (1 / 3 - 0.006) * W
    yt, yb = (1 - 0.994) * H, (1 - 0.506) * H
    def X(l): return x0 + (l + 1) / 2 * (x1 - x0)
    def Y(l): return yt + (1 - (l + 1) / 2) * (yb - yt)
    def lx(px): return (px - x0) / (x1 - x0) * 2 - 1
    def ly(py): return (1 - (py - yt) / (yb - yt)) * 2 - 1
    mx = a.max(-1)
    dark = mx < 115
    brows = []
    for e in eyes:
        top_eye = max(e["top"])
        xs, lo, hi = [], [], []
        for k in range(-14, 15):
            x = e["cx"] + k / 14 * e["half_w"] * 1.35
            c = int(round(X(x)))
            r = int(Y(top_eye + 0.015))
            rmax = int(Y(top_eye + 0.30))
            # đi lên tới điểm tối đầu tiên = mép dưới lông mày
            while r > rmax and not dark[r, c]:
                r -= 1
            if r <= rmax:
                continue
            bot = r
            gap = 0
            while r > rmax:
                r -= 1
                if dark[r, c]:
                    gap = 0
                else:
                    gap += 1
                    if gap > 3:
                        break
            topb = r + gap
            if ly(topb) - ly(bot) < 0.008:
                continue
            xs.append(x); lo.append(ly(bot)); hi.append(ly(topb))
        if len(xs) < 6:
            brows.append(None)
            continue
        xs = np.array(xs); lo = np.array(lo); hi = np.array(hi)
        # bỏ cột lạc (tóc mái rủ xuống): giữ cột có mép dưới gần trung vị
        keep = np.abs(lo - np.median(lo)) < 0.05
        th = hi - lo
        med = np.median(th[keep])
        # chỗ lông mày chạm tóc: mép trên vọt lên, giới hạn độ dày quanh trung vị
        hi = np.minimum(hi, lo + 1.4 * med)
        brows.append(dict(x=xs[keep].tolist(), lo=lo[keep].tolist(), hi=hi[keep].tolist()))
    return brows



def measure_face(rgb):
    """rgb: mảng H×W×3 (0..255, hàng 0 là đỉnh ảnh). Trả dict eyes / mouth / brows."""
    eyes = measure_eyes(rgb)
    mouth = measure_mouth(rgb, eyes)
    brows = measure_brows(rgb, eyes)
    return {"eyes": eyes, "mouth": mouth, "brows": brows}


def _plain(o):
    if isinstance(o, dict):
        return {k: _plain(v) for k, v in o.items()}
    if isinstance(o, (list, tuple)):
        return [_plain(v) for v in o]
    if isinstance(o, (np.floating, np.integer)):
        return o.item()
    return o


def load_or_measure(code, sheet_path, rgb_loader, cache_dir):
    """Đọc số đo đã lưu nếu còn khớp ảnh mẫu (hoặc đã khoá); không thì đo lại và lưu.

    rgb_loader: hàm không đối số trả mảng RGB của ảnh mẫu (chỉ gọi khi cần đo).
    """
    os.makedirs(cache_dir, exist_ok=True)
    path = os.path.join(cache_dir, f"{code}.json")
    stamp = os.path.getmtime(sheet_path) if os.path.exists(sheet_path) else 0
    if os.path.exists(path):
        with open(path, encoding="utf-8") as f:
            saved = json.load(f)
        if saved.get("khoa") or abs(saved.get("anh_mtime", -1) - stamp) < 1e-3:
            return saved
    face = _plain(measure_face(rgb_loader()))
    face.update({"ma": code, "anh": sheet_path, "anh_mtime": stamp, "khoa": False})
    with open(path, "w", encoding="utf-8") as f:
        json.dump(face, f, ensure_ascii=False, indent=1)
    return face
