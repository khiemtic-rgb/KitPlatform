"""Nhịp cảnh 1 cho video dài: đổi mốc giọng cũ (bản dựng cảnh 1 v6) sang mốc video dài (giọng 1b đã chèn nghỉ).
canh1 viết mọi mốc theo giọng cũ; inv(): giây video -> giây giọng cũ, fwd(): ngược lại. Nội suy theo từng chữ."""
import json, numpy as np
_A = sorted(json.load(open('/home/claude/dai/scene1_anchor.json'))['anchors'])
OUT = np.array([a for a, b in _A]); OLD = np.maximum.accumulate(np.array([b for a, b in _A]))
PAUSES = []
LEAD = {4.58: 0.45, 6.28: 0.25, 17.74: 0.55}
def inv(T): return float(np.interp(T, OUT, OLD))
def fwd(t):
    i = np.where(np.diff(OLD) > 1e-6)[0]
    return float(np.interp(t, np.r_[OLD[i], OLD[-1]], np.r_[OUT[i], OUT[-1]]))
