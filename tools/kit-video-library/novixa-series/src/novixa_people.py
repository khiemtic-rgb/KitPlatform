"""Nhân vật Novixa (cập nhật 09/11/2026): dược sĩ và khách, có tay, cử chỉ và nét mặt.

Dùng chung nét vẽ với nhân vật kênh Người Giữ Vốn (`ngv-edu/ngv_scenes.py`): đầu, tóc, tay, nét mặt.
Đổi trang phục cho Novixa: dược sĩ áo blouse trắng có huy hiệu chữ thập xanh, khách áo thun màu.

Trong file hình (`nv_board`): `{"art": "person", "args": {"mood": "worry", "pose": "hold"}}`.
- mood: ok, smile, happy, worry, think, angry, shock, sad, focus.
- pose: rest (hai tay buông), hold (cầm hộp thuốc trước ngực), point (chỉ sang bên), chin (tay chống cằm),
  head (ôm đầu), wave (giơ tay chào), shrug (dang tay). Không ghi pose thì máy chọn theo mood.
- Khách: thêm `color` (màu áo), `hair: "long"` (tóc dài) hoặc `hair: "grey"` (khách lớn tuổi, tóc bạc).
Hình là ảnh bán thân (đứng sau quầy), nền trong suốt.
"""

from __future__ import annotations

import importlib.util
import math
import sys
from pathlib import Path

from PIL import Image

HERE = Path(__file__).resolve().parent
_NGV = None


def _ngv():
    global _NGV
    if _NGV is None:
        path = HERE.parents[1] / "ngv-edu" / "ngv_scenes.py"
        spec = importlib.util.spec_from_file_location("ngv_scenes_people", path)
        mod = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(mod)
        _NGV = mod
    return _NGV


MOODS = {
    "ok": "smile", "smile": "smile", "happy": "grin", "worry": "worried", "worried": "worried",
    "think": "focus", "focus": "focus", "angry": "angry", "shock": "shock", "sad": "sad", "neutral": "neutral",
}
DEFAULT_POSE = {"think": "chin", "focus": "chin", "shock": "shrug", "angry": "rest", "sad": "rest"}
LOGICAL = (760, 700)  # khung vẽ (trước khi thu nhỏ)
CX, SY = 380, 470
ASPECT = LOGICAL[0] / LOGICAL[1]


def _arms_nv(base):
    def arms(pose, t, cx, sy, hy):
        extra = {
            "hold": (((cx - 230, sy + 230), (cx - 70, sy + 170)), ((cx + 230, sy + 230), (cx + 70, sy + 170))),
            "point": (((cx - 262, sy + 205), (cx - 215, sy + 280)), ((cx + 250, sy + 130), (cx + 350, sy + 40))),
            "wave": (((cx - 262, sy + 205), (cx - 215, sy + 280)), ((cx + 270, sy + 10), (cx + 250, sy - 150))),
            "shrug": (((cx - 262, sy + 200), (cx - 340, sy + 120)), ((cx + 262, sy + 200), (cx + 340, sy + 120))),
            "head": base("head_hands", t, cx, sy, hy),
        }
        if pose in extra:
            return extra[pose]
        return base(pose, t, cx, sy, hy)
    return arms


def _darker(c, k=0.8):
    return tuple(int(v * k) for v in c[:3])


def figure(kind: str = "person", mood: str = "ok", pose: str | None = None, color=None, hair: str = "short") -> Image.Image:
    ng = _ngv()
    ss = ng.SS
    lw, lh = LOGICAL
    # Nét vẽ gốc có lớp bóng bán trong suốt: vẽ hai lần trên nền đen và nền trắng rồi tách nền (giữ đúng độ trong).
    layers = []
    for base in ((0, 0, 0), (255, 255, 255)):
        img = Image.new("RGB", (lw * ss, lh * ss), base)
        _draw(ng, ng.Pen(img), kind, mood, pose, color, hair)
        layers.append(img)
    return _matte(*layers)


def _matte(black, white):
    import numpy as np

    b = np.asarray(black, dtype=np.float32)
    w = np.asarray(white, dtype=np.float32)
    alpha = 1.0 - (w - b).mean(axis=2) / 255.0
    alpha = np.clip(alpha, 0.0, 1.0)
    rgb = np.where(alpha[..., None] > 0.004, b / np.maximum(alpha[..., None], 0.004), 0)
    out = np.dstack([np.clip(rgb, 0, 255), alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def _draw(ng, p, kind, mood, pose, color, hair):
    lw, lh = LOGICAL
    expr = MOODS.get(str(mood or "ok").lower(), "smile")
    pose = str(pose or DEFAULT_POSE.get(str(mood or "").lower(), "rest"))
    ngv_pose = {"head": "head_hands"}.get(pose, pose)
    saved = {k: getattr(ng, k) for k in ("H", "TEE", "TEE_D", "SHIRT", "SHIRT_D", "_arms", "EXPR", "HAIR")}
    try:
        ng.H = lh
        ng._arms = _arms_nv(saved["_arms"])
        ng.EXPR = {**saved["EXPR"], "angry": ((12, -8), "open", "frown")}
        if hair == "grey":
            ng.HAIR = (176, 180, 188)
        if kind == "person":
            ng.SHIRT, ng.SHIRT_D = (242, 246, 252), (206, 216, 232)
            outfit = "shirt"
        else:
            c = tuple(color) if color else (236, 120, 96)
            ng.TEE, ng.TEE_D = c, _darker(c)
            outfit = "tee"
        hy = SY - 186 + {"head_hands": 26}.get(ngv_pose, 0)
        if hair == "long":
            p.rect(CX - 118, hy - 60, CX + 118, hy + 190, ng.HAIR, 60)
        ng.character_front(p, CX, SY, pose=ngv_pose, expr=expr, t=1.0, outfit=outfit)
        if kind == "person":
            # Huy hiệu chữ thập xanh trên ngực áo blouse.
            bx, by = CX + 120, SY + 150
            if pose not in ("hold",):
                p.ell(bx, by, 30, 30, (64, 196, 136))
                p.rect(bx - 18, by - 6, bx + 18, by + 6, (255, 255, 255), 2)
                p.rect(bx - 6, by - 18, bx + 6, by + 18, (255, 255, 255), 2)
        if pose == "hold":
            # Hộp thuốc giữa hai bàn tay.
            x0, y0 = CX - 70, SY + 70
            p.rect(x0, y0, x0 + 140, y0 + 120, (246, 249, 253), 8)
            p.rect(x0, y0, x0 + 140, y0 + 40, (64, 206, 250), 8)
            p.rect(x0 + 12, y0 + 12, x0 + 30, y0 + 28, (255, 255, 255), 3)
            p.rect(x0 + 16, y0 + 64, x0 + 110, y0 + 72, (170, 182, 200), 3)
            p.rect(x0 + 16, y0 + 84, x0 + 80, y0 + 92, (190, 200, 214), 3)
            for s in (-1, 1):
                p.ell(CX + s * 74, SY + 150, 26, 26, ng.SKIN)
    finally:
        for k, v in saved.items():
            setattr(ng, k, v)


_CACHE: dict = {}


def sprite(kind: str, w: int, h: int, args: dict) -> Image.Image:
    """Hình nhân vật vừa khung w x h, giữ tỉ lệ, đặt sát đáy (thân bị mặt quầy che)."""
    key = (kind, int(w), int(h), repr(sorted((args or {}).items())))
    if key in _CACHE:
        return _CACHE[key]
    a = args or {}
    big = figure(kind, a.get("mood", "ok"), a.get("pose"), a.get("color"), str(a.get("hair", "short")))
    k = min(w / big.width, h / big.height)
    small = big.resize((max(2, int(big.width * k)), max(2, int(big.height * k))), Image.LANCZOS)
    out = Image.new("RGBA", (int(w), int(h)), (0, 0, 0, 0))
    out.alpha_composite(small, ((int(w) - small.width) // 2, int(h) - small.height))
    if len(_CACHE) > 200:
        _CACHE.clear()
    _CACHE[key] = out
    return out
