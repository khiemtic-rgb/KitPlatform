"""Chiếu mặt gốc lên mô hình 3D: nắn hình khối mặt + vẽ lại da mặt theo ảnh <Ma>_mat.png (ô trên-trái).

Chạy:  sgd chieu_mat Ma [Ma ...]      (bỏ trống = mọi nhân vật đã có mô hình 3D)
       thêm --chi-da: chỉ vẽ lại da, không nắn hình khối;  --muc 0.7: nắn nhẹ hơn (0..1)
Ghi:   data/mo_hinh/<Ma>_mat_hinh.npy, data/mo_hinh/<Ma>_da_mat.png, data/mo_hinh/<Ma>_chieu_mat.png (trước | sau | ảnh gốc)
Bỏ: xoá hai file .npy/.png là mô hình về như cũ.
"""
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy
import numpy as np

from sgd_core import body, chieu_mat, mat3d, shot

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
chi_da = "--chi-da" in argv
muc = float(argv[argv.index("--muc") + 1]) if "--muc" in argv else 1.0
codes = [a for a in argv if not a.startswith("--") and not a.replace(".", "").isdigit()]
cfg = shot.config()
chars = shot.load_json("nhan_vat.json")
DATA = os.path.join(HERE, "data")
codes = codes or [c for c, i in chars.items() if isinstance(i, dict) and i.get("mo_hinh_3d")]


def front(arm):
    mesh, to_u, p, no, hz, box = chieu_mat._head_setup(arm)
    img, _ = mat3d._render_front(arm, mesh, box)
    return np.asarray(img, float)


def save_rgb(path, a):
    h, w = a.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    px = np.ones((h, w, 4), np.float32)
    px[..., :3] = np.clip(a[::-1] / 255.0, 0, 1)
    im.pixels.foreach_set(px.ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()


ok = 0
for code in codes:
    info = chars[code]
    model = os.path.join(DATA, info["mo_hinh_3d"])
    ref = os.path.join(cfg["thu_muc_anh"], info.get("anh_mat", f"{code}_mat.png"))
    if not os.path.exists(ref):
        print("SGD_CHIEU_MAT_LOI", code, "không thấy", ref)
        continue
    base = os.path.splitext(model)[0]
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        arm = body.load_model(model, code, mat_goc=True)
        before = front(arm)
        moc = chieu_mat.load_moc(DATA, code)                 # 68 điểm dò sẵn (moc_mat.py) — chắc hơn bộ dò tự viết
        src_after = None
        if not chi_da:
            if moc is not None:
                src_after = chieu_mat.nan_hinh68(arm, moc, base + "_mat_hinh.npy", muc=min(muc, 0.8))
            else:
                chieu_mat.nan_hinh(arm, ref, base + "_mat_hinh.npy", muc=muc)
        elif os.path.exists(base + "_mat_hinh.npy"):
            os.remove(base + "_mat_hinh.npy")
        if moc is not None:
            chieu_mat.chieu68(arm, moc, ref, base + "_da_mat.png", src_after=src_after)
            chieu_mat.save_moc(DATA, code, moc)
        else:
            chieu_mat.chieu(arm, model, ref, base + "_da_mat.png")
        # xem lại đúng như cảnh sẽ nạp
        bpy.ops.wm.read_factory_settings(use_empty=True)
        arm = body.load_model(model, code)
        after = front(arm)
        R = before.shape[0]
        refp = chieu_mat.ref_panel(chieu_mat.load_rgb(ref))
        save_rgb(base + "_chieu_mat.png", np.concatenate([before, after, refp], 1))
        cache = os.path.join(DATA, "do_dac", f"{code}_mat3d.json")
        if os.path.exists(cache):
            os.remove(cache)
        print("SGD_CHIEU_MAT_XONG", code, base + "_chieu_mat.png")
        ok += 1
    except Exception as ex:
        import traceback
        traceback.print_exc()
        print("SGD_CHIEU_MAT_LOI", code, ex)
print("SGD_CHIEU_MAT_TONG", ok, "/", len(codes))
