"""Kiểm tra nhân vật sau khi thêm hoặc đổi ảnh mẫu: mí khép có che kín mắt không.

  blender --background --python tools/san-gach-do/kiem_tra.py -- Minh Lan     (bỏ trống = mọi nhân vật)

Cách đo: tô xanh lá nhãn cầu (trong khe mắt đã đo, phần không phải da) trên bản sao ảnh mẫu dùng cho da mặt,
mí vẫn dùng ảnh gốc; render lúc mắt khép hẳn và đếm điểm xanh còn lộ. Đạt khi ≤ 20 điểm.
Kết quả: <thu_muc_ra>/kiem_tra/<ma>_khep.png và kiem_tra/bao_cao.json.
"""
import json
import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sgd_core import anim, measure, rig as rigmod, shot, stage  # noqa: E402

LIMIT = 20


def eye_mask_image(rgb, eyes, name):
    H, W = rgb.shape[:2]
    x0, x1 = rigmod.PANEL_U0 * W, rigmod.PANEL_U1 * W
    yt, yb = (1 - rigmod.PANEL_V1) * H, (1 - rigmod.PANEL_V0) * H
    yy, xx = np.mgrid[0:H, 0:W]
    lx = (xx - x0) / (x1 - x0) * 2 - 1
    ly = (1 - (yy - yt) / (yb - yt)) * 2 - 1
    skin = measure.is_skin(rgb)
    mask = np.zeros((H, W), bool)
    for e in eyes:
        s = (lx - e["cx"]) / e["half_w"]
        inside = np.abs(s) < 1
        top = np.interp(s, measure.EYE_KNOTS, e["top"])
        bot = np.interp(s, measure.EYE_KNOTS, e["bottom"])
        mask |= inside & (ly < top + 0.004) & (ly > bot - 0.004) & ~skin
    out = rgb.copy() / 255.0
    out[mask] = (0.0, 1.0, 0.0)
    im = bpy.data.images.new(name, W, H, alpha=True)
    rgba = np.concatenate([np.flipud(out), np.ones((H, W, 1))], axis=2).astype(np.float32)
    im.pixels.foreach_set(rgba.ravel())
    return im, int(mask.sum())


def count_green(path):
    im = bpy.data.images.load(path)
    w, h = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)
    g = (px[..., 1] > 0.78) & (px[..., 0] < 0.35) & (px[..., 2] < 0.35)
    return int(g.sum())


def check(code, cfg, chars):
    info = chars[code]
    scene = stage.setup(cfg.get("render", {}), 48)
    stage.background(scene, None)
    face_info = shot.face_data(cfg, code, info)
    path = shot.sheet_path(cfg, code, info)
    face = rigmod.build(scene, path, face_info, name=code, pose=info.get("dang"))
    stage.camera(scene, "can_mat", info.get("khung"))
    anim.idle(face, 48, fixed_blink=1.0)
    face.set_mouth(1, 0.0)
    face.set_brow(1)
    rgb = rigmod.sheet_rgb(rigmod.img)
    mask_img, n_mask = eye_mask_image(rgb, face_info["eyes"], f"{code}_mask")
    tex = next(n for n in rigmod.mat.node_tree.nodes if n.type == "TEX_IMAGE")
    tex.image = mask_img          # da mặt dùng ảnh tô xanh; mí giữ ảnh gốc (vật liệu riêng)
    out_dir = os.path.join(cfg["thu_muc_ra"], "kiem_tra")
    paths, _ = stage.render(scene, out_dir, f"{code.lower()}_khep", stills=(24,), animation=False)
    green = count_green(paths[0])
    m = face_info["mouth"]
    return {
        "ma": code,
        "mat_lo_khi_khep": green,
        "dat": green <= LIMIT and n_mask > 200,
        "diem_to_xanh": n_mask,
        "mat": [{"tam_x": round(e["cx"], 3), "rong": round(2 * e["half_w"], 3), "cao": round(e["height"], 3)}
                for e in face_info["eyes"]],
        "mieng": {"trai": round(m["left"], 3), "phai": round(m["right"], 3), "y": round(m["cy"], 3)},
        "long_may": sum(1 for b in face_info["brows"] if b),
        "anh": paths[0],
    }


cfg = shot.config()
chars = shot.load_json("nhan_vat.json")
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
codes = argv or list(chars)
report = []
for c in codes:
    try:
        r = check(c, cfg, chars)
    except Exception as exc:  # một nhân vật lỗi không chặn các nhân vật khác
        r = {"ma": c, "dat": False, "loi": repr(exc)}
    report.append(r)
    print("SGD_KIEM_TRA", json.dumps(r, ensure_ascii=False))
out = os.path.join(cfg["thu_muc_ra"], "kiem_tra", "bao_cao.json")
os.makedirs(os.path.dirname(out), exist_ok=True)
with open(out, "w", encoding="utf-8") as f:
    json.dump(report, f, ensure_ascii=False, indent=1)
print("SGD_BAO_CAO", out, "đạt", sum(1 for r in report if r.get("dat")), "/", len(report))
