"""Đo độ mượt chuyển động trên diễn viên thật (mô hình 3D) và người mẫu tạm, bằng số.

  blender --background --python tools/san-gach-do/kiem_tra_muot.py -- [Ma ...]   (mặc định: Bo Minh Bin)

Đo trên 3 chuỗi động tác (A: mọi động tác lần lượt, B: thứ tự khác, C: đi-dừng-đi rất ngắn):
 - giat: độ giật (sai phân bậc 3 vị trí khớp) vượt ngưỡng: 10 cm/khung³ (người 1,70 m), hoặc 1,3 lần độ giật
   vốn có của động tác đang chạy (đo riêng động tác đó) -> số chỗ giật. Một cú nảy/lật gối lúc đứng 15–80
 - truot_chan: lúc đi, bàn chân đang chạm đất mà vẫn trượt ngang (cm/giây, người thật ~0, chấp nhận < 15)
 - xuyen_dat / lo_lung: điểm thấp nhất của lưới (hoặc bàn chân) dưới mặt đất / lơ lửng khi đứng (cm)
 - noi_dong_tac: tốc độ xoay lớn nhất quanh chỗ nối hai động tác so với trong động tác (lần)
Báo cáo -> kiem_tra/muot.json
"""
import json
import math
import os
import sys

import bpy
import numpy as np
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
from sgd_core import anim, body, shot  # noqa: E402

FPS = anim.FPS
SEQS = {
    # A: mọi động tác lần lượt
    "A": [{"ten": "dung_tho", "giay": 1.0}, {"ten": "di", "giay": 3.0}, {"ten": "dung_tho", "giay": 1.0},
       {"ten": "dua_tien"}, {"ten": "gat_dau"}, {"ten": "di", "giay": 1.6}, {"ten": "ngoi_xuong"},
       {"ten": "dung_day"}, {"ten": "vay_tay"}, {"ten": "cui_chao"}, {"ten": "dung_tho", "giay": 1.0}],
    # B: thứ tự khác, đi xong cúi chào / gật đầu ngay
    "B": [{"ten": "vay_tay"}, {"ten": "di", "giay": 2.3}, {"ten": "cui_chao"}, {"ten": "di", "giay": 0.8},
          {"ten": "gat_dau"}, {"ten": "ngoi_xuong"}, {"ten": "dung_day"}, {"ten": "dua_tien"}, {"ten": "di", "giay": 1.2}],
    # C: đi - dừng - đi rất ngắn (khó nhất)
    "C": [{"ten": "di", "giay": 0.5}, {"ten": "dung_tho", "giay": 0.4}, {"ten": "di", "giay": 0.7}, {"ten": "dua_tien"},
          {"ten": "di", "giay": 4.0}, {"ten": "ngoi_xuong"}, {"ten": "dung_day"}, {"ten": "di", "giay": 0.9}],
    # D: các động tác khác trong thư viện (đi buồn, chạy, nhảy, đi chậm, ngồi yên, nói chuyện)
    "D": [{"ten": "dung_tho", "giay": 0.8}, {"ten": "di_buon", "giay": 2.5}, {"ten": "chay", "giay": 2.0},
          {"ten": "dung_tho", "giay": 0.8}, {"ten": "nhay_tai_cho"}, {"ten": "di_cham_gia", "giay": 2.0},
          {"ten": "ngoi_xuong"}, {"ten": "ngoi_tho", "giay": 1.5}, {"ten": "dung_day"}, {"ten": "noi_chuyen", "giay": 2.5}],
}


_INTRINSIC = {}


def intrinsic(code, info, model, name):
    """Độ giật vốn có của một động tác (chạy riêng, không lớp chạm đất, bỏ đầu/cuối): mức chuẩn để so."""
    key = (code, name)
    if key in _INTRINSIC:
        return _INTRINSIC[key]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    arm = body.actor(code, info, (0, 0, 0), 0.0, model)
    clip = body.get_clip(name)
    dur = 2.5 if clip.loop else clip.duration()
    end = int(math.ceil(dur * FPS))
    body.bake_timeline(arm, [{"ten": name, "bat_dau": 0.0, "giay": dur}], end, cham_dat=False)
    std_of = body.bone_map(arm)
    names = [b for b, s in std_of.items() if s]
    P = []
    for f in range(1, end + 1):
        bpy.context.scene.frame_set(f)
        P.append([(arm.matrix_world @ arm.pose.bones[b].head)[:] for b in names])
    P = np.array(P)
    j = np.linalg.norm(P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3], axis=2).max(1)
    v = float(j[3:-3].max()) if len(j) > 8 else 0.0
    _INTRINSIC[key] = v
    return v


def measure(code, info, model, SEQ, loc=(0, 0, 0), heading=0.0):
    INTR = {it["ten"]: intrinsic(code, info, model, it["ten"]) for it in SEQ}
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.render.fps = FPS
    arm = body.actor(code, info, loc, heading, model)
    dg0 = bpy.context.evaluated_depsgraph_get()
    rest_floor = min((min((o.matrix_world @ v.co).z for v in o.evaluated_get(dg0).data.vertices)
                      for o in bpy.data.objects if o.type == "MESH"), default=0.0)
    items, t_end = shot._timeline(SEQ, lambda k: 0.0)
    end = int(math.ceil(t_end * FPS))
    body.bake_timeline(arm, items, end)
    std_of = body.bone_map(arm)
    names = [b for b, s in std_of.items() if s]
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and (o.parent == arm or any(
        m.type == "ARMATURE" and m.object == arm for m in o.modifiers))]
    h = float(info.get("chieu_cao", 1.7))
    pos, rots, low = [], [], []
    feet = [b for b in names if std_of[b].split(".")[0] in ("foot", "toe")]
    dg = bpy.context.evaluated_depsgraph_get()
    legs, floor = body._ground_rig(arm, std_of, body._world_rest(arm))
    lifts, ank, lifts_tip = [], [], []
    for f in range(1, end + 1):
        bpy.context.scene.frame_set(f)
        lifts.append({s: body._lift(arm, legs[s], floor) for s in legs})                 # điểm tì đất (đo trượt)
        lifts_tip.append(min(body._lift(arm, legs[s], floor, tip=True) for s in legs))  # cả đầu mũi (xuyên đất)
        # điểm lòng bàn chân đang tì đất (cổ chân/gót hay mũi chân, điểm thấp nhất)
        ank.append({s: body.sole_points(arm, legs[s]) for s in legs})
        pos.append([(arm.matrix_world @ arm.pose.bones[b].head)[:] for b in names])
        rots.append([(arm.matrix_world @ arm.pose.bones[b].matrix).to_3x3().normalized().to_quaternion()[:] for b in names])
        if meshes and f % 2 == 1:
            z = 1e9
            for o in meshes:
                ev = o.evaluated_get(dg)
                me = ev.to_mesh()
                co = np.empty(len(me.vertices) * 3)
                me.vertices.foreach_get("co", co)
                co = co.reshape(-1, 3)
                mw = np.array(o.matrix_world)
                zz = co @ mw[2, :3] + mw[2, 3]
                z = min(z, float(zz.min()))
                ev.to_mesh_clear()
            low.append((f, z))
        elif not meshes:
            low.append((f, lifts_tip[-1]))
    P = np.array(pos)                              # khung x khớp x 3 (m)
    Q = np.array(rots)
    k = h / 1.7
    # 1. giật: độ giật (sai phân bậc 3 của vị trí khớp). Chuyển động liền mạch < ~4 cm/khung³; một cú nảy 5 cm cho ~15.
    jerk = np.linalg.norm(P[3:] - 3 * P[2:-1] + 3 * P[1:-2] - P[:-3], axis=2)
    per_frame = jerk.max(1)
    # ngưỡng mỗi khung: 10 cm/khung³ (người 1,70 m), hoặc 1,3 lần độ giật vốn có của động tác đang chạy (đi nhanh
    # thì tự nhiên giật hơn đứng) -> lớp chạm đất / chỗ nối không được làm giật hơn bản thân động tác quá 30%
    allow = np.full(len(per_frame), 0.10 * k)
    for idx, it in enumerate(items):
        lim = 1.3 * INTR[it["ten"]]
        a = int(it["bat_dau"] * FPS)
        b = int((it["bat_dau"] + it["giay"]) * FPS)
        allow[max(0, a):max(0, b - 2)] = np.maximum(allow[max(0, a):max(0, b - 2)], lim)
        if idx > 0:
            # chỗ nối (~0,6 giây đầu): không đòi êm hơn động tác mạnh hơn trong hai động tác được nối
            prev = 1.3 * INTR[items[idx - 1]["ten"]]
            allow[max(0, a - 2):a + int(0.6 * FPS)] = np.maximum(allow[max(0, a - 2):a + int(0.6 * FPS)], prev)
    spikes = [int(i + 2) for i in np.nonzero(per_frame > allow)[0]]
    acc = np.linalg.norm(P[2:] - 2 * P[1:-1] + P[:-2], axis=2).max(1)
    # 2. chỗ nối động tác: độ giật lớn nhất quanh chỗ nối so với trong động tác
    starts = sorted(int(round(it["bat_dau"] * FPS)) + 1 for it in items)[1:]
    near = set()
    for s0 in starts:
        near.update(range(max(0, s0 - 4), min(len(per_frame), s0 + int(0.3 * FPS) + 2)))
    inside = [i for i in range(len(per_frame)) if i not in near]
    joint_ratio = max(per_frame[i] for i in near) / max(np.percentile(per_frame[inside], 99), 1e-6)
    # 3. trượt chân: điểm đang tì đất (gót/cổ chân lúc đặt phẳng, mũi chân lúc nhón) di chuyển ngang khi sát đất
    slide = []
    for f in range(len(P) - 1):
        for s in ("L", "R"):
            if lifts[f][s] < 0.015 * k and lifts[f + 1][s] < 0.015 * k:
                q = min(range(len(ank[f][s])), key=lambda i: ank[f][s][i].z)    # điểm thấp nhất ở khung này
                d = ank[f + 1][s][q] - ank[f][s][q]                            # cùng điểm đó ở khung sau
                slide.append(math.hypot(d.x, d.y) * FPS)
    lows = np.array([z for _, z in low])
    stand = [min(lifts[f - 1].values()) for f in range(1, end + 1) if any(it["ten"] == "dung_tho" and it["bat_dau"] * FPS + 8 < f < (it["bat_dau"] + it["giay"]) * FPS
                                      for it in items)]
    return {
        "ma": code,
        "mo_hinh": "3D" if meshes and model else "nguoi_mau_tam",
        "khung": end,
        "giat": {"so_cho": len(spikes), "khung": spikes[:20],
                 "do_giat_lon_nhat_cm": round(float(per_frame.max()) * 100, 2), "khung_lon_nhat": int(per_frame.argmax() + 2),
                 "ty_le_vuot_nguong_lon_nhat": round(float((per_frame / allow).max()), 2),
                 "do_giat_von_co_cm": {kk: round(v * 100, 1) for kk, v in INTR.items()},
                 "gia_toc_lon_nhat_cm": round(float(acc.max()) * 100, 2)},
        "noi_dong_tac_lan": round(float(joint_ratio), 2),
        "truot_chan_cm_giay": {"trung_binh": round(float(np.mean(slide)) * 100, 1) if slide else None,
                               "lon_nhat": round(float(np.max(slide)) * 100, 1) if slide else None},
        "xuyen_dat_cm": round(float(-min(lows.min() - (rest_floor if meshes else 0.0), 0)) * 100, 1),
        "xuyen_dat_khung": int(low[int(lows.argmin())][0]),
        "lo_lung_khi_dung_cm": round(float(np.median(stand)) * 100, 1) if stand else None,
    }


argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
codes = argv or ["Bo", "Minh", "Bin"]
chars = shot.load_json("nhan_vat.json")
rows = []
for code in codes:
    info = chars[code]
    model = info.get("mo_hinh_3d")
    model = model if not model or os.path.isabs(model) else os.path.join(HERE, "data", model)
    for sq, SEQ in list(SEQS.items()) + [("A_xoay", SEQS["A"])]:
        # A_xoay: chuỗi A nhưng đứng ở chỗ khác và quay hướng khác (như trong cảnh thật)
        r = measure(code, info, model, SEQ, *(((2.3, -1.2, 0), -90.0) if sq == "A_xoay" else ((0, 0, 0), 0.0)))
        r["chuoi"] = sq
        tr = r["truot_chan_cm_giay"]["trung_binh"]
        r["dat"] = (r["giat"]["so_cho"] == 0 and r["noi_dong_tac_lan"] < 3 and (tr is None or tr < 15)
                    and r["xuyen_dat_cm"] < 2 and (r["lo_lung_khi_dung_cm"] is None or abs(r["lo_lung_khi_dung_cm"]) < 2))
        print("SGD_MUOT", json.dumps(r, ensure_ascii=False))
        rows.append(r)
os.makedirs(os.path.join(HERE, "kiem_tra"), exist_ok=True)
with open(os.path.join(HERE, "kiem_tra", "muot.json"), "w", encoding="utf-8") as f:
    json.dump(rows, f, ensure_ascii=False, indent=1)
print("SGD_MUOT_TONG", sum(r["dat"] for r in rows), "/", len(rows), "đạt")
