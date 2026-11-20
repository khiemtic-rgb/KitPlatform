"""Kiểm tra thư viện chuyển động dùng lại được cho mọi bộ xương.

  blender --background --python tools/san-gach-do/kiem_tra_chuyen_dong.py [-- di ngoi_xuong ...]

Với mỗi chuyển động: áp lên 3 bộ xương khác hẳn nhau
  - chuẩn cao 1,70 m
  - trẻ con cao 1,10 m (như Bin)
  - kiểu Mixamo cao 1,65 m: tên khớp 'mixamorig:LeftArm'…, khớp xoay lệch 37°
  - tư thế chữ A cao 1,60 m: lúc nghỉ tay buông 55° (như mô hình dựng từ ảnh đứng thẳng)
rồi so vị trí đầu, hai bàn tay, hai bàn chân với xương chuẩn (đã quy về cùng chiều cao).
Sai lệch tính theo % chiều cao người; đạt khi ≤ 1%.
"""
import json
import os
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sgd_core import body  # noqa: E402

MIXAMO = {"hips": "mixamorig:Hips", "spine": "mixamorig:Spine", "chest": "mixamorig:Spine2", "neck": "mixamorig:Neck",
          "head": "mixamorig:Head", "shoulder.L": "mixamorig:LeftShoulder", "upper_arm.L": "mixamorig:LeftArm",
          "forearm.L": "mixamorig:LeftForeArm", "hand.L": "mixamorig:LeftHand", "shoulder.R": "mixamorig:RightShoulder",
          "upper_arm.R": "mixamorig:RightArm", "forearm.R": "mixamorig:RightForeArm", "hand.R": "mixamorig:RightHand",
          "thigh.L": "mixamorig:LeftUpLeg", "shin.L": "mixamorig:LeftLeg", "foot.L": "mixamorig:LeftFoot",
          "toe.L": "mixamorig:LeftToeBase", "thigh.R": "mixamorig:RightUpLeg", "shin.R": "mixamorig:RightLeg",
          "foot.R": "mixamorig:RightFoot", "toe.R": "mixamorig:RightToeBase"}
ENDS = ["head", "hand.L", "hand.R", "toe.L", "toe.R"]


def tips(arm, height):
    std_of = body.bone_map(arm)
    inv = {v: k for k, v in std_of.items() if v}
    out = {}
    for e in ENDS:
        pb = arm.pose.bones[inv[e]]
        p = arm.matrix_world @ pb.tail - arm.matrix_world.to_translation()
        out[e] = p / height
    return out


def check(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    clip = body.get_clip(name)
    dur = clip.duration() if not clip.loop else 2.0
    end = int(round(dur * body.FPS)) + 1
    rigs = [("chuan_170", 1.70, None, 0.0, (0, 0, 0), 0.0), ("tre_110", 1.10, None, 0.0, (3, 0, 0), 0.0),
            ("mixamo_165", 1.65, MIXAMO, 37.0, (6, 0, 0), 0.0), ("chu_A_160", 1.60, None, 0.0, (9, 0, 0), 55.0)]
    arms = []
    for rname, h, ren, roll, loc, down in rigs:
        arm = body.build_armature(rname, h, ren, roll, loc, down)
        body.bake_timeline(arm, [{"ten": name, "bat_dau": 0.0, "giay": dur}], end, cham_dat=False)
        arms.append((rname, h, arm))
    worst = {r[0]: 0.0 for r in rigs[1:]}
    for f in range(1, end + 1, 3):
        scene.frame_set(f)
        ref = tips(arms[0][2], arms[0][1])
        for rname, h, arm in arms[1:]:
            got = tips(arm, h)
            err = max((got[e] - ref[e]).length for e in ENDS) * 100
            worst[rname] = max(worst[rname], err)
    return {"chuyen_dong": name, "khung": end, "sai_lech_pct": {k: round(v, 3) for k, v in worst.items()},
            "dat": all(v <= 1.0 for v in worst.values())}


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
names = argv or list(body.library())
report = [check(n) for n in names]
for r in report:
    print("SGD_CD", json.dumps(r, ensure_ascii=False))
print("SGD_CD_TONG", sum(r["dat"] for r in report), "/", len(report), "đạt")
