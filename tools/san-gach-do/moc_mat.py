"""Dò 68 điểm mốc mặt (OpenCV LBF) trên ảnh mặt gốc và ảnh chụp thẳng mặt mô hình 3D, lưu
data/do_dac/<Ma>_moc_mat.json cho chieu_mat dùng (máy dựng phim không cần cài OpenCV).

Chạy (Python có opencv-contrib, KHÔNG phải trong Blender):
    python moc_mat.py --lbf lbfmodel.yaml --haar haarcascade_frontalface_default.xml --anh <thư mục ảnh> --mo_hinh <thư mục ảnh chụp mô hình .npy> Ma ...
Ảnh chụp mô hình: <Ma>.npy (R x R x 3) do chieu_mat_nv.py --chup ghi ra.
Thứ tự 68 điểm theo chuẩn iBUG: 0-16 đường hàm, 17-26 lông mày, 27-35 mũi, 36-47 mắt, 48-67 miệng.
"""
import argparse
import json
import os

import cv2
import numpy as np
from PIL import Image

R = 800


def panel(path, cot=0, hang=0, so_cot=3, so_hang=2):
    im = Image.open(path).convert("RGB")
    W, H = im.size
    pw, ph = W / so_cot, H / so_hang
    side = min(pw, ph)
    x0 = cot * pw + pw / 2 - side / 2
    y0 = hang * ph
    return np.array(im.crop((int(round(x0)), int(round(y0)), int(round(x0 + side)), int(round(y0 + side)))).resize((R, R), Image.BILINEAR))


def detect(a, fm, hc):
    g = cv2.cvtColor(a.astype(np.uint8), cv2.COLOR_RGB2GRAY)
    faces = hc.detectMultiScale(g, 1.1, 3, minSize=(R // 4, R // 4))
    if len(faces) == 0:
        return None
    f = max(faces, key=lambda r: r[2] * r[3])
    ok, lm = fm.fit(g, np.array([f], dtype=np.int32))
    return np.array(lm[0]).reshape(-1, 2) if ok else None


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--lbf", required=True)
    ap.add_argument("--haar", required=True)
    ap.add_argument("--anh", required=True)
    ap.add_argument("--mo_hinh", required=True)
    ap.add_argument("--ra", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "data", "do_dac"))
    ap.add_argument("ma", nargs="+")
    a = ap.parse_args()
    fm = cv2.face.createFacemarkLBF()
    fm.loadModel(a.lbf)
    hc = cv2.CascadeClassifier(a.haar)
    for c in a.ma:
        ref = panel(os.path.join(a.anh, f"{c}_mat.png"))
        mo = np.load(os.path.join(a.mo_hinh, f"{c}.npy"))[..., :3]
        pr, pm = detect(ref, fm, hc), detect(mo, fm, hc)
        if pr is None or pm is None:
            print("SGD_MOC_LOI", c, "không thấy mặt", "ảnh gốc" if pr is None else "mô hình")
            continue
        out = os.path.join(a.ra, f"{c}_moc_mat.json")
        with open(out, "w", encoding="utf-8") as f:
            json.dump({"goc": pr.round(2).tolist(), "mo_hinh": pm.round(2).tolist(), "o_anh": [0, 0], "R": R,
                       "_ghi_chu": "68 điểm iBUG (OpenCV LBF). Ảnh gốc: ô trên-trái của <Ma>_mat.png cắt vuông; mô hình: ảnh chụp thẳng mặt."},
                      f, ensure_ascii=False)
        print("SGD_MOC_XONG", c, out)
