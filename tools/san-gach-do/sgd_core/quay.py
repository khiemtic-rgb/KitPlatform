"""Máy quay nhiều góc (cắt cảnh theo câu thoại), đạo cụ gắn tay/lưng, hiện/ẩn diễn viên theo thời gian.

"canh_quay": danh sách cảnh, mỗi cảnh chạy tới khi cảnh sau bắt đầu:
  {"tu": giây | {"cau": k, "lech": giây}, "kieu": ..., ...}
  kieu:
    "toan"      — toàn cảnh, nhìn giữa các diễn viên trong "nhin" (mặc định tất cả đang hiện)
    "hai_nguoi" — cỡ trung hai người
    "qua_vai"   — "sau": diễn viên tiền cảnh (vai), "nhin": diễn viên được quay
    "can"       — cận mặt "nhin" (lệch "goc_do" về phía máy)
    "tay"       — cận đạo cụ "do_vat" (vd tiền lúc trao tay)
  tuỳ chỉnh: "xa" (m), "cao" (m, so với điểm nhìn), "tieu_cu" (mm), "goc_do", "day_vao" (m, máy tiến dần),
             "xoa_phong" (f-stop, mặc định 2.8 với cận/qua vai), "theo" (0..1: máy bám theo người, mặc định 0.6)
  Luật 180°: mọi máy ở cùng một phía đường nối hai diễn viên đầu tiên ("phia": -1 = phía -Y như máy mặc định).

"do_vat": [{"loai": "tien", "theo": [{"tu": 0, "dien_vien": 0}, {"tu": {"cau": 3, "lech": 2.1}, "dien_vien": 1}]},
           {"loai": "ba_lo", "dien_vien": 1}]
  "tien" theo bàn tay (tay gần người kia nhất, hoặc "tay": "L"/"R"); đổi người cầm đúng mốc "tu";
  "tu": {"gan_nhat": [t0, t1], "giua": [i, j]} = lúc tay i và tay j gần nhau nhất trong khoảng (trao tay);
  "chuyen": n = tiền đi mượt từ tay người đưa sang tay người nhận trong n khung trước/sau mốc.
"""
import math

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector

from . import body


def _when(t, anchors):
    if isinstance(t, dict):
        return anchors(int(t["cau"])) + float(t.get("lech", 0.0))
    return float(t)


def _std_inv(arm):
    return {v: k for k, v in body.bone_map(arm).items() if v}


class Tracks:
    """Vị trí đầu, ngực, hai bàn tay của mọi diễn viên ở từng khung (sau khi đã đặt chuyển động)."""

    def __init__(self, scene, arms, end):
        self.n = end
        self.head, self.chest, self.hand, self.handm = [], [], [], []
        invs = [_std_inv(a) for a in arms]
        # đuôi xương trong file glTF không tin được -> dùng khớp đầu + độ xoay so với tư thế nghỉ
        rest = []
        for a, inv in zip(arms, invs):
            std_of = body.bone_map(a)
            H = a.get("sgd_cao", None) or 1.7
            r = {}
            for key in ["head"] + [f"hand.{s}" for s in "LR"]:
                bn = inv[key]
                Rw = (a.matrix_world @ a.data.bones[bn].matrix_local).to_quaternion()
                if key == "head":
                    off = Vector((0, 0, 0.075))                     # tầm mắt trên khớp cổ-đầu
                else:
                    off = body.bone_dir(a, std_of, bn).normalized() * 0.075   # giữa lòng bàn tay
                r[key] = (bn, Rw, off)
            rest.append(r)
        for a in arms:
            self.head.append([None] * (end + 1))
            self.chest.append([None] * (end + 1))
            self.hand.append({"L": [None] * (end + 1), "R": [None] * (end + 1)})
            self.handm.append({"L": [None] * (end + 1), "R": [None] * (end + 1)})
        for f in range(1, end + 1):
            scene.frame_set(f)
            for i, (a, inv) in enumerate(zip(arms, invs)):
                W = a.matrix_world
                pb = a.pose.bones
                for key in ["head", "hand.L", "hand.R"]:
                    bn, Rw, off = rest[i][key]
                    M = W @ pb[bn].matrix
                    dq = M.to_quaternion() @ Rw.inverted()
                    p = M.translation + dq @ off
                    if key == "head":
                        self.head[i][f] = p
                    else:
                        s = key[-1]
                        self.hand[i][s][f] = p
                        self.handm[i][s][f] = Matrix.Translation(p) @ dq.to_matrix().to_4x4()
                self.chest[i][f] = W @ pb[inv.get("chest", inv.get("spine"))].head


def _smooth(points, sigma):
    n = len(points)
    out = []
    for i in range(n):
        acc, ws = Vector((0, 0, 0)), 0.0
        for j in range(max(0, i - int(3 * sigma)), min(n, i + int(3 * sigma) + 1)):
            w = math.exp(-0.5 * ((j - i) / max(sigma, 1e-6)) ** 2)
            acc += points[j] * w
            ws += w
        out.append(acc / ws)
    return out


def _new_cam(scene, name, lens):
    data = bpy.data.cameras.new(name)
    data.lens = lens
    data.clip_start = 0.05
    data.clip_end = 600
    cam = bpy.data.objects.new(name, data)
    scene.collection.objects.link(cam)
    aim = bpy.data.objects.new(name + "_Nhin", None)
    scene.collection.objects.link(aim)
    c = cam.constraints.new("TRACK_TO")
    c.target = aim
    c.track_axis = "TRACK_NEGATIVE_Z"
    c.up_axis = "UP_Y"
    return cam, aim


def build_cameras(scene, shots, tracks, anchors, end, visible, props=None, fps=24, phia=-1):
    """Tạo một máy cho mỗi cảnh, gắn vào mốc thời gian (Blender tự đổi máy khi render)."""
    props = props or {}
    starts = [max(1, int(round(_when(s.get("tu", 0), anchors) * fps)) + 1) for s in shots]
    starts[0] = 1
    cams = []
    for k, sh in enumerate(shots):
        f0 = starts[k]
        f1 = (starts[k + 1] - 1) if k + 1 < len(shots) else end
        f1 = max(f0, min(f1, end))
        vis = [i for i in range(len(tracks.head)) if visible(i, f0)]
        kieu = sh.get("kieu", "toan")
        # đường nối hai diễn viên chính (lúc đầu cảnh) và phía đặt máy
        a_, b_ = (vis + [0, 1])[:2] if len(vis) >= 2 else (0, 1 if len(tracks.head) > 1 else 0)
        pa, pb = tracks.head[a_][f0], tracks.head[b_][f0]
        u = Vector((pb.x - pa.x, pb.y - pa.y, 0))
        u = u.normalized() if u.length > 1e-4 else Vector((1, 0, 0))
        nrm = Vector((-u.y, u.x, 0))
        if nrm.y * phia < 0:
            nrm = -nrm
        lens = float(sh.get("tieu_cu", {"toan": 32, "hai_nguoi": 40, "qua_vai": 50, "can": 65, "tay": 55}.get(kieu, 40)))
        cam, aim = _new_cam(scene, f"May_{k + 1:02d}_{kieu}", lens)
        theo = float(sh.get("theo", 0.6 if kieu in ("can", "qua_vai", "tay") else 0.35))
        rng = list(range(f0, f1 + 1))

        def subject(f):
            if kieu == "tay":
                ob = props.get(sh.get("do_vat", "tien"))
                if ob is not None:
                    return ob["pos"][f]
            ids = sh.get("nhin")
            ids = [ids] if isinstance(ids, int) else (ids or vis or [0])
            if kieu in ("toan", "hai_nguoi"):
                p = sum((tracks.head[i][f] for i in ids), Vector()) / len(ids)
                p.z = p.z - float(sh.get("ha", 0.45 if kieu == "toan" else 0.25))
                return p
            p = tracks.head[ids[0]][f].copy()
            p.z -= float(sh.get("ha", 0.0))
            return p

        raw = [subject(f) for f in rng]
        sm = _smooth(raw, 0.6 * fps)
        base = sm[0].copy()
        aim_pts = [base + (p - base) * theo for p in sm]
        # vị trí máy lúc đầu cảnh
        T = aim_pts[0]
        g = math.radians(float(sh.get("goc_do", 0.0)))
        rot = Quaternion((0, 0, 1), g)
        if kieu in ("toan", "hai_nguoi"):
            xa = float(sh.get("xa", 9.0 if kieu == "toan" else 3.4))
            loc = T + (rot @ nrm) * xa + Vector((0, 0, float(sh.get("cao", 0.25))))
        elif kieu == "qua_vai":
            i = int(sh["sau"])
            hi = tracks.head[i][f0]
            d = Vector((hi.x - T.x, hi.y - T.y, 0)).normalized()
            loc = hi + d * float(sh.get("xa", 0.55)) + nrm * float(sh.get("lech_ngang", 0.42)) \
                + Vector((0, 0, float(sh.get("cao", 0.06))))
        elif kieu == "can":
            j = sh.get("nhin", 0)
            j = j if isinstance(j, int) else j[0]
            others = [i for i in vis if i != j] or [i for i in range(len(tracks.head)) if i != j] or [j]
            ho = tracks.head[others[0]][f0]
            fwd = Vector((ho.x - T.x, ho.y - T.y, 0))
            fwd = fwd.normalized() if fwd.length > 1e-4 else nrm
            side = math.radians(float(sh.get("goc_do", 35.0)))
            # quay hướng nhìn của người được quay về phía máy một góc
            sgn = 1 if (fwd.cross(nrm)).z > 0 else -1
            dirc = Quaternion((0, 0, 1), sgn * side) @ fwd
            loc = T + dirc * float(sh.get("xa", 1.15)) + Vector((0, 0, float(sh.get("cao", -0.02))))
        elif kieu == "tay":
            loc = T + (rot @ nrm) * float(sh.get("xa", 0.85)) + Vector((0, 0, float(sh.get("cao", 0.22))))
        else:
            raise ValueError(f"Kiểu cảnh quay chưa có: {kieu}")
        push = float(sh.get("day_vao", 0.0))
        for idx, f in enumerate(rng):
            t = idx / max(1, len(rng) - 1)
            p = aim_pts[idx]
            # máy đi cùng phần bám theo (song song), cộng tiến dần về điểm nhìn
            cl = loc + (p - aim_pts[0]) * 0.7
            if push:
                cl = cl + (p - cl).normalized() * push * (t * t * (3 - 2 * t))
            cam.location = cl
            aim.location = p
            if idx % 2 == 0 or f == f1:
                cam.keyframe_insert("location", frame=f)
                aim.keyframe_insert("location", frame=f)
        fstop = sh.get("xoa_phong", 2.8 if kieu in ("can", "qua_vai", "tay") else None)
        if fstop:
            cam.data.dof.use_dof = True
            cam.data.dof.focus_object = aim
            cam.data.dof.aperture_fstop = float(fstop)
        m = scene.timeline_markers.new(f"C{k + 1:02d}_{kieu}", frame=f0)
        m.camera = cam
        cams.append((f0, f1, cam))
    scene.camera = cams[0][2]
    return cams


# ---------------------------------------------------------------- đạo cụ
def _money_mesh():
    bm = bmesh.new()
    # xấp tiền gấp đôi: hai lớp lệch nhau chút
    for k, (dx, dz) in enumerate(((0, 0), (0.004, 0.003), (-0.003, 0.006))):
        m = Matrix.Translation((dx, 0, dz)) @ Matrix.Diagonal((0.12, 0.062, 0.0025, 1))
        bmesh.ops.create_cube(bm, size=1.0, matrix=m)
    me = bpy.data.meshes.new("DV_Tien")
    bm.to_mesh(me)
    bm.free()
    mat = bpy.data.materials.new("DV_Tien")
    mat.use_nodes = True
    b = mat.node_tree.nodes.get("Principled BSDF")
    # tờ 100 nghìn: xanh lục đậm ngả ô liu, hoa văn loang, viền sáng hai đầu
    nt = mat.node_tree
    tc = nt.nodes.new("ShaderNodeTexCoord")
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 60.0
    noise.inputs["Detail"].default_value = 6.0
    nt.links.new(tc.outputs["Object"], noise.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color = (0.035, 0.10, 0.05, 1)
    ramp.color_ramp.elements[1].color = (0.12, 0.22, 0.10, 1)
    nt.links.new(noise.outputs["Fac"], ramp.inputs[0])
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Object"], sep.inputs[0])
    ab = nt.nodes.new("ShaderNodeMath")
    ab.operation = "ABSOLUTE"
    nt.links.new(sep.outputs[0], ab.inputs[0])
    edge = nt.nodes.new("ShaderNodeMapRange")
    edge.inputs[1].default_value = 0.046
    edge.inputs[2].default_value = 0.052
    nt.links.new(ab.outputs[0], edge.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    nt.links.new(edge.outputs[0], mix.inputs["Factor"])
    nt.links.new(ramp.outputs[0], mix.inputs[6])
    mix.inputs[7].default_value = (0.55, 0.62, 0.45, 1)
    nt.links.new(mix.outputs[2], b.inputs["Base Color"])
    b.inputs["Roughness"].default_value = 0.55
    me.materials.append(mat)
    return me


def _backpack_mesh():
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Diagonal((0.30, 0.14, 0.38, 1)))
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.035, segments=3, affect="EDGES")
    # túi nhỏ mặt ngoài
    bmesh.ops.create_cube(bm, size=1.0, matrix=Matrix.Translation((0, 0.08, -0.07)) @ Matrix.Diagonal((0.22, 0.05, 0.16, 1)))
    me = bpy.data.meshes.new("DV_BaLo")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    mat = bpy.data.materials.new("DV_BaLo")
    mat.use_nodes = True
    b = mat.node_tree.nodes.get("Principled BSDF")
    b.inputs["Base Color"].default_value = (0.12, 0.20, 0.30, 1)     # vải xanh than bạc màu
    b.inputs["Roughness"].default_value = 0.9
    me.materials.append(mat)
    return me


def build_props(scene, items, tracks, arms, anchors, end, fps=24):
    out = {}
    for n_, it in enumerate(items or []):
        loai = it.get("loai")
        if loai == "tien":
            ob = bpy.data.objects.new("DV_Tien", _money_mesh())
            scene.collection.objects.link(ob)
            def when_f(s):
                t = s.get("tu", 0)
                if isinstance(t, dict) and "gan_nhat" in t:
                    # khoảnh khắc hai bàn tay (người đưa / người nhận) gần nhau nhất trong khoảng [t0, t1]
                    t0, t1 = t["gan_nhat"]
                    i, j = t.get("giua", [0, int(s["dien_vien"])])
                    fr = range(max(1, int(_when(t0, anchors) * fps) + 1), min(end, int(_when(t1, anchors) * fps) + 1) + 1)
                    best = min(((min((tracks.hand[i][a][f] - tracks.hand[j][b][f]).length for a in "LR" for b in "LR"), f)
                                for f in fr), default=(0, fr.start))
                    print("SGD_TRAO_TAY", f"khung {best[1]}", f"{best[0] * 100:.1f} cm")
                    return best[1]
                return max(1, int(round(_when(t, anchors) * fps)) + 1)
            seg = sorted(((when_f(s), s) for s in it.get("theo", [])), key=lambda t: t[0])
            pos = [None] * (end + 1)

            def holder(f):
                cur = seg[0][1]
                for f0, s in seg:
                    if f >= f0:
                        cur = s
                return cur

            def pick_hand(s, f):
                i = int(s["dien_vien"])
                if s.get("tay") in ("L", "R"):
                    return s["tay"]
                others = [j for j in range(len(arms)) if j != i]
                if not others:
                    return "R"
                # gần người kia nhất (tay của người kia, hoặc ngực nếu chưa có)
                o_hands = [tracks.hand[j][h2][f] for j in others for h2 in "LR"]
                return min("LR", key=lambda h: min((tracks.hand[i][h][f] - q).length for q in o_hands))

            # tay cầm của mỗi đoạn: chọn ở giữa đoạn (lúc tay đưa ra nhất)
            hands = []
            for k, (f0, s) in enumerate(seg):
                # người đưa: tay đang chìa ra ngay trước lúc trao; người nhận cuối: tay lúc nhận
                fm = max(1, seg[k + 1][0] - 1) if k + 1 < len(seg) else max(1, min(end, f0))
                hands.append(pick_hand(s, fm))
            def at(k, f):
                i = int(seg[k][1]["dien_vien"])
                h = hands[k]
                return tracks.hand[i][h][f], tracks.handm[i][h][f].to_quaternion()
            for f in range(1, end + 1):
                k = max(i for i, (f0, _) in enumerate(seg) if f >= f0 or i == 0)
                p, q = at(k, f)
                # trao tay: "chuyen" khung quanh mốc, tiền đi mượt từ tay người đưa sang tay người nhận
                for kk in range(1, len(seg)):
                    w = int(seg[kk][1].get("chuyen", 0))
                    if w and abs(f - seg[kk][0]) < w:
                        t = (f - (seg[kk][0] - w)) / (2.0 * w)
                        t = t * t * (3 - 2 * t)
                        p0, q0 = at(kk - 1, f)
                        p1, q1 = at(kk, f)
                        p, q = p0.lerp(p1, t), q0.slerp(q1, t)
                ob.matrix_world = Matrix.Translation(p) @ q.to_matrix().to_4x4()
                ob.keyframe_insert("location", frame=f)
                ob.keyframe_insert("rotation_euler", frame=f)
                pos[f] = p.copy()
            if ob.animation_data and ob.animation_data.action:
                for fc in ob.animation_data.action.fcurves:
                    for kp in fc.keyframe_points:
                        kp.interpolation = "LINEAR"
            out[it.get("ten", "tien")] = {"obj": ob, "pos": pos}
        elif loai == "ba_lo":
            i = int(it["dien_vien"])
            arm = arms[i]
            ob = bpy.data.objects.new("DV_BaLo", _backpack_mesh())
            scene.collection.objects.link(ob)
            inv = _std_inv(arm)
            cb = inv.get("chest", inv.get("spine"))
            heading = Quaternion((0, 0, 1), math.radians(arm.get("sgd_huong_do", 0.0)))
            back = float(it.get("lui", 0.17))
            for f in range(1, end + 1):
                scene.frame_set(f)
                pbm = arm.matrix_world @ arm.pose.bones[cb].matrix
                pbm_rest = arm.matrix_world @ arm.data.bones[cb].matrix_local
                # xoay theo ngực (so với tư thế nghỉ), đặt sau lưng theo hướng mặt
                dq = pbm.to_quaternion() @ pbm_rest.to_quaternion().inverted()
                off = dq @ (heading @ Vector((0, back, -0.08)))
                ob.location = pbm.translation + off
                ob.rotation_mode = "QUATERNION"
                ob.rotation_quaternion = dq @ heading
                ob.keyframe_insert("location", frame=f)
                ob.keyframe_insert("rotation_quaternion", frame=f)
            out[it.get("ten", f"ba_lo_{n_}")] = {"obj": ob, "dien_vien": i}
    return out


def set_visibility(objs, t0, t1, end, fps=24):
    """Ẩn khi render ngoài khoảng [t0, t1] giây (đổi tức thì: cắt cảnh)."""
    f0 = int(round(t0 * fps)) + 1
    f1 = int(round(t1 * fps)) + 1 if t1 is not None else end + 1
    for ob in objs:
        for f, hide in ((1, f0 > 1), (f0, False), (f1, True)):
            if 1 <= f <= end + 1:
                ob.hide_render = hide
                ob.keyframe_insert("hide_render", frame=f)
        if ob.animation_data and ob.animation_data.action:
            for fc in ob.animation_data.action.fcurves:
                if fc.data_path == "hide_render":
                    for kp in fc.keyframe_points:
                        kp.interpolation = "CONSTANT"
