"""Lối vào cũ (giữ để lệnh cũ của Cursor vẫn chạy): đọc biến môi trường, dựng file cảnh, gọi sgd_core.shot.

  set SGD_CHAR=Minh
  blender --background --python tools/san-gach-do/char_motion.py
Biến: SGD_CHAR, SGD_AUDIO (.wav), SGD_TALK=1, SGD_EXPR=<tên trong data/bieu_cam.json>, SGD_END (số khung),
SGD_SUFFIX, SGD_OUT, SGD_SHEET, SGD_FRAME, SGD_STILL_ONLY=1, SGD_CLOSED (mắt khép cố định 0..1).
Cách mới, nên dùng: viết file cảnh trong canh/ rồi chạy render_shot.py (xem README.md).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sgd_core import shot  # noqa: E402

env = os.environ.get
code = env("SGD_CHAR", "Minh")
tag = code.lower() + env("SGD_SUFFIX", "")
spec = {"nhan_vat": code, "ma": tag}
if env("SGD_SHEET"):
    spec["anh_mat"] = env("SGD_SHEET")
if env("SGD_AUDIO"):
    spec["thoai"] = {"file": env("SGD_AUDIO"), "bat_dau": 0.0}
if env("SGD_TALK") == "1":
    spec["nhep_mau"] = True
if env("SGD_END"):
    spec["do_dai"] = int(env("SGD_END")) / 24.0
if env("SGD_EXPR"):
    spec["bieu_cam"] = [{"giay": 7 / 24, "bieu_cam": env("SGD_EXPR"), "chuyen": 8 / 24}]
if env("SGD_CLOSED"):
    spec["mat_khep_co_dinh"] = float(env("SGD_CLOSED"))
out = env("SGD_OUT") or os.path.join(shot.config()["thu_muc_ra"], code.lower())
stills = [int(env("SGD_FRAME"))] if env("SGD_FRAME") else None
shot.run(spec, stills=stills, still_only=env("SGD_STILL_ONLY") == "1", out_override=out,
         frames_dir="frames" + env("SGD_SUFFIX", ""), tag=tag)
