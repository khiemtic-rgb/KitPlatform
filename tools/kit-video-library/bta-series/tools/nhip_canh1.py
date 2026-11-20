"""Nhịp nghỉ cảnh 1 tập 01: dùng chung cho canh1_moi.py (hình) và mix_canh1.py (âm thanh)."""
# ---------- nhịp nghỉ: chèn khoảng lặng vào giọng đọc ở các chỗ ngắt tự nhiên ----------
# (mốc trên giọng gốc, số giây thêm). Mọi mốc khác trong file vẫn viết theo giọng gốc; fwd()/inv() đổi qua lại.
PAUSES = [(4.38, 0.8), (6.17, 0.6), (8.00, 0.6), (10.30, 0.7), (11.48, 0.35), (12.52, 0.9), (14.66, 0.3),
          (16.14, 0.2), (17.50, 0.6), (19.87, 0.9), (21.62, 0.5), (24.77, 0.6), (27.68, 0.6), (30.16, 0.2)]
def fwd(t):  # mốc giọng gốc -> mốc video mới
    return t + sum(e for p, e in PAUSES if p < t)
def inv(T):  # mốc video mới -> mốc giọng gốc (trong khoảng nghỉ thì đứng ở chỗ ngắt)
    acc = 0.0
    for p, e in PAUSES:
        if T < p + acc: return T - acc
        if T < p + acc + e: return p
        acc += e
    return T - acc
# ảnh vào trước giọng một chút (cắt sớm trong khoảng nghỉ) để mắt kịp nhìn
LEAD = {4.58: 0.45, 6.28: 0.25, 17.74: 0.55}

