"""Lớp cơ thể: bộ xương chuẩn, thư viện chuyển động, chuyển động sang nhân vật bất kỳ.

Ý chính: mọi nhân vật dùng chung MỘT bộ khớp chuẩn (STANDARD), đứng tư thế chữ T khi nghỉ.
Một chuyển động được lưu dưới dạng "mỗi khớp xoay bao nhiêu so với tư thế nghỉ, tính theo trục thế giới"
cộng vị trí hông. Vì vậy một file "đi" áp được cho Bố, Minh, Bin… dù cao thấp, tên khớp, hướng khớp khác nhau.

Nguồn chuyển động: file .bvh / .fbx (Mixamo, quay động tác, AI tạo động tác) đặt trong data/chuyen_dong/,
khai báo trong data/chuyen_dong.json. Tên khớp lạ được đổi về tên chuẩn bằng data/xuong_chuan.json.
"""
import json
import math
import os

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

FPS = 24

# Khớp chuẩn: tên, cha, đầu khớp, đuôi khớp (mét, người cao 1,70 m, mặt nhìn về -Y, trái nhân vật là +X).
STANDARD = [
    ("hips", None, (0, 0, 0.95), (0, 0, 1.05)),
    ("spine", "hips", (0, 0, 1.05), (0, 0, 1.25)),
    ("chest", "spine", (0, 0, 1.25), (0, 0, 1.42)),
    ("neck", "chest", (0, 0, 1.42), (0, 0, 1.52)),
    ("head", "neck", (0, 0, 1.52), (0, 0, 1.72)),
    ("shoulder.L", "chest", (0.02, 0, 1.40), (0.17, 0, 1.40)),
    ("upper_arm.L", "shoulder.L", (0.17, 0, 1.40), (0.45, 0, 1.40)),
    ("forearm.L", "upper_arm.L", (0.45, 0, 1.40), (0.70, 0, 1.40)),
    ("hand.L", "forearm.L", (0.70, 0, 1.40), (0.80, 0, 1.40)),
    ("shoulder.R", "chest", (-0.02, 0, 1.40), (-0.17, 0, 1.40)),
    ("upper_arm.R", "shoulder.R", (-0.17, 0, 1.40), (-0.45, 0, 1.40)),
    ("forearm.R", "upper_arm.R", (-0.45, 0, 1.40), (-0.70, 0, 1.40)),
    ("hand.R", "forearm.R", (-0.70, 0, 1.40), (-0.80, 0, 1.40)),
    ("thigh.L", "hips", (0.09, 0, 0.95), (0.09, 0, 0.52)),
    ("shin.L", "thigh.L", (0.09, 0, 0.52), (0.09, 0, 0.10)),
    ("foot.L", "shin.L", (0.09, 0, 0.10), (0.09, -0.14, 0.02)),
    ("toe.L", "foot.L", (0.09, -0.14, 0.02), (0.09, -0.20, 0.02)),
    ("thigh.R", "hips", (-0.09, 0, 0.95), (-0.09, 0, 0.52)),
    ("shin.R", "thigh.R", (-0.09, 0, 0.52), (-0.09, 0, 0.10)),
    ("foot.R", "shin.R", (-0.09, 0, 0.10), (-0.09, -0.14, 0.02)),
    ("toe.R", "foot.R", (-0.09, -0.14, 0.02), (-0.09, -0.20, 0.02)),
]
STD_NAMES = [b[0] for b in STANDARD]
STD_PARENT = {b[0]: b[1] for b in STANDARD}
REF_HEIGHT = 1.70
REF_HIP = 0.95

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DATA = os.path.join(HERE, "data")


def _load(name):
    with open(os.path.join(DATA, name), encoding="utf-8") as f:
        return {k: v for k, v in json.load(f).items() if not k.startswith("_")}


def name_map():
    """{tên khớp lạ (chữ thường) -> tên chuẩn} gộp từ mọi bộ trong data/xuong_chuan.json."""
    out = {n.lower(): n for n in STD_NAMES}
    for table in _load("xuong_chuan.json").values():
        for k, v in table.items():
            out[k.lower()] = v
    return out


def to_standard(bone_name, nmap):
    key = bone_name.lower()
    if key in nmap:
        return nmap[key]
    if ":" in key and key.split(":", 1)[1] in nmap:      # mixamorig:Hips, mixamorig1:Hips …
        return nmap[key.split(":", 1)[1]]
    return None


def bone_map(arm):
    """{tên khớp của xương này -> tên chuẩn}. Hai khớp cùng về một tên chuẩn (vd Mixamo Spine1, Spine2 -> chest)
    thì giữ khớp nằm sâu hơn (gần đầu/ngọn hơn), khớp kia để tự theo cha."""
    nmap = name_map()

    def depth(b):
        d = 0
        while b.parent:
            b, d = b.parent, d + 1
        return d
    best = {}
    for b in arm.data.bones:
        s = to_standard(b.name, nmap)
        if s and (s not in best or depth(b) > depth(arm.data.bones[best[s]])):
            best[s] = b.name
    keep = {v: k for k, v in best.items()}
    return {b.name: keep.get(b.name) for b in arm.data.bones}


# ---------------------------------------------------------------- dựng xương
def build_armature(name, height=REF_HEIGHT, rename=None, roll_deg=0.0, location=(0, 0, 0), arms_down_deg=0.0):
    """Dựng bộ xương chuẩn cao `height` mét (tư thế chữ T). rename: đổi tên khớp (thử xương kiểu Mixamo).
    arms_down_deg: hạ tay xuống bấy nhiêu độ lúc nghỉ (giả lập mô hình tư thế chữ A)."""
    k = height / REF_HEIGHT
    down = math.radians(arms_down_deg)
    data = bpy.data.armatures.new(name)
    arm = bpy.data.objects.new(name, data)
    bpy.context.scene.collection.objects.link(arm)
    arm.location = location
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    arm_chain = ("upper_arm", "forearm", "hand")
    for n, parent, head, tail in STANDARD:
        b = data.edit_bones.new((rename or {}).get(n, n))
        h, t = Vector(head) * k, Vector(tail) * k
        if down and n.split(".")[0] in arm_chain:
            side = 1 if n.endswith(".L") else -1
            pivot = Vector(STANDARD[STD_NAMES.index("upper_arm" + n[-2:])][2]) * k
            q = Quaternion((0, 1, 0), side * down)
            h, t = pivot + q @ (h - pivot), pivot + q @ (t - pivot)
        b.head = h
        b.tail = t
        b.roll = math.radians(roll_deg)
        if parent:
            b.parent = eb[parent]
            b.use_connect = (Vector(STANDARD[STD_NAMES.index(parent)][3]) - Vector(head)).length < 1e-6
        eb[n] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    return arm


def build_mannequin(arm, colors=None):
    """Người mẫu tạm: khối trụ/cầu gắn cứng vào từng khớp (để thử chuyển động khi chưa có mô hình 3D)."""
    colors = colors or {}
    nmap = name_map()

    def material(name, rgb):
        m = bpy.data.materials.new(name)
        m.use_nodes = True
        bsdf = m.node_tree.nodes.get("Principled BSDF")
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (*rgb, 1)
            bsdf.inputs["Roughness"].default_value = 0.85
        return m

    mats = {"da": material(arm.name + "_da", colors.get("da", (0.80, 0.55, 0.40))),
            "ao": material(arm.name + "_ao", colors.get("ao", (0.25, 0.32, 0.40))),
            "quan": material(arm.name + "_quan", colors.get("quan", (0.20, 0.18, 0.16)))}
    # (vật liệu, bán kính) theo người cao 1,70 m
    part = {"hips": ("quan", 0.15), "spine": ("ao", 0.14), "chest": ("ao", 0.16), "neck": ("da", 0.05),
            "head": ("da", 0.105), "shoulder": ("ao", 0.06), "upper_arm": ("ao", 0.055), "forearm": ("da", 0.045),
            "hand": ("da", 0.045), "thigh": ("quan", 0.075), "shin": ("quan", 0.06), "foot": ("da", 0.05),
            "toe": ("da", 0.045)}
    hips = next(b for b in arm.data.bones if to_standard(b.name, nmap) == "hips")
    k = hips.length / 0.10
    for bone in arm.data.bones:
        std = to_standard(bone.name, nmap)
        if not std:
            continue
        kind = std.split(".")[0]
        mat_key, radius = part[kind]
        length = bone.length
        if kind == "head":
            bpy.ops.mesh.primitive_uv_sphere_add(radius=radius * k, location=(0, 0, 0))
            obj = bpy.context.active_object
            obj.scale = (0.9, 1.0, 1.15)
        else:
            bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius * k, depth=length * 1.08,
                                                location=(0, 0, 0))
            obj = bpy.context.active_object
        obj.name = f"{arm.name}_{std}"
        obj.data.materials.append(mats[mat_key])
        bpy.ops.object.shade_smooth()
        # vật gắn khớp nằm ở ĐUÔI khớp, trục Y dọc khớp; hình trụ mặc định dọc Z -> xoay -90° quanh X
        obj.parent = arm
        obj.parent_type = "BONE"
        obj.parent_bone = bone.name
        obj.matrix_parent_inverse = Matrix.Identity(4)
        obj.rotation_euler = (math.radians(-90), 0, 0)
        obj.location = (0, -length / 2, 0)
    return arm


# ---------------------------------------------------------------- đọc chuyển động
class Clip:
    """Một chuyển động đã quy về khớp chuẩn.

    rot[name][f]: quaternion "độ xoay so với tư thế nghỉ, trục thế giới" của khớp ở khung f.
    hip[f]: vị trí hông (đơn vị: chiều cao hông), tính từ hông lúc nghỉ, theo trục thế giới.
    """

    def __init__(self, name, n_frames, rot, hip, loop=False):
        self.name, self.n, self.rot, self.hip, self.loop = name, n_frames, rot, hip, loop

    def sample(self, t_frames):
        """Lấy tư thế tại khung (số thực); clip lặp thì vòng lại và cộng dồn quãng đường hông."""
        if self.loop:
            cycles = math.floor(t_frames / (self.n - 1)) if self.n > 1 else 0
            local = t_frames - cycles * (self.n - 1)
            step = self.hip[-1] - self.hip[0]
            step = Vector((step.x, step.y, 0.0))
            if step.length < 0.05:                 # động tác tại chỗ (đứng thở, nói chuyện): không cộng dồn độ trôi
                step = Vector((0, 0, 0))
            base = step * cycles
        else:
            local = min(max(t_frames, 0.0), self.n - 1)
            base = Vector((0, 0, 0))
        i = int(math.floor(local))
        j = min(i + 1, self.n - 1)
        a = local - i
        rot = {k: v[i].slerp(v[j], a) for k, v in self.rot.items()}
        hip = self.hip[i].lerp(self.hip[j], a) + base
        return rot, hip

    def duration(self):
        return (self.n - 1) / FPS


def _world_rest(arm):
    return {b.name: (arm.matrix_world @ b.matrix_local) for b in arm.data.bones}


def hip_height(arm, std_of, rest):
    """Chiều cao hông lúc nghỉ = từ đầu khớp hông xuống điểm thấp nhất của bàn chân (không phụ thuộc gốc toạ độ)."""
    hip_bone = next(b for b, s in std_of.items() if s == "hips")
    hz = rest[hip_bone].to_translation().z
    heads, tails = [], []
    for b, s in std_of.items():
        if s and s.split(".")[0] in ("foot", "toe"):
            bone = arm.data.bones[b]
            h = arm.matrix_world @ bone.head_local
            t = arm.matrix_world @ bone.tail_local
            heads.append(h.z)
            tails.append((t.z, (t - h).length))
    if not heads:
        raise ValueError(f"{arm.name}: không thấy khớp bàn chân để đo chiều cao hông")
    span = hz - min(heads)
    low = heads + [z for z, ln in tails if ln < 0.4 * span]   # bỏ đuôi xương dài bất thường
    return hip_bone, max(hz - min(low), 1e-6)


def stand_upright(arm):
    """Xoay cả bộ xương nguồn cho đứng thẳng (đầu trên hông, trục Z) và quay mặt về -Y, bất kể nguồn dùng trục nào."""
    std_of = bone_map(arm)
    inv = {v: k for k, v in std_of.items() if v}
    need = ("hips", "head", "foot.L", "toe.L", "foot.R", "toe.R")
    if not all(n in inv for n in need):
        return
    bones = arm.data.bones
    m3 = arm.matrix_world.to_3x3()
    up = m3 @ (bones[inv["head"]].head_local - bones[inv["hips"]].head_local)
    fwd = m3 @ ((bones[inv["toe.L"]].head_local - bones[inv["foot.L"]].head_local) +
                (bones[inv["toe.R"]].head_local - bones[inv["foot.R"]].head_local))
    up.normalize()
    # khung xương mẫu hay hơi nghiêng (vd Mixamo cúi đầu ~3°): lệch dưới 15° so với một trục chính thì bám trục đó,
    # nếu không, bước đi sẽ "leo dốc/xuống dốc" (hông tụt dần mỗi chu kỳ)
    axes = [Vector(v) for v in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1))]
    best = max(axes, key=lambda v: v.dot(up))
    if best.dot(up) > math.cos(math.radians(15)):
        up = best
    fwd = fwd - up * fwd.dot(up)
    if fwd.length < 1e-6:
        return
    fwd.normalize()
    side = fwd.cross(up)
    src_basis = Matrix((side, fwd, up)).transposed()               # cột: phải, trước, lên (trong toạ độ xương)
    dst_basis = Matrix((Vector((0, -1, 0)).cross(Vector((0, 0, 1))), Vector((0, -1, 0)), Vector((0, 0, 1)))).transposed()
    rot = dst_basis @ src_basis.inverted()
    loc, q, sc = arm.matrix_world.decompose()
    arm.matrix_world = Matrix.Translation(loc) @ (rot @ q.to_matrix()).to_4x4() @ Matrix.Diagonal(sc).to_4x4()
    bpy.context.view_layer.update()


def clip_from_armature(src, name, frame_start, frame_end, loop=False):
    """Đọc chuyển động từ một xương bất kỳ (BVH/FBX vừa nhập) và quy về khớp chuẩn."""
    std_of = bone_map(src)
    rest = _world_rest(src)
    if "hips" not in std_of.values():
        raise ValueError(f"Chuyển động '{name}': không tìm thấy khớp hông (xem data/xuong_chuan.json)")
    hip_bone, hip_h = hip_height(src, std_of, rest)
    hip_rest = rest[hip_bone].to_translation()
    rot = {s: [] for s in std_of.values() if s}
    hip = []
    scene = bpy.context.scene
    for f in range(frame_start, frame_end + 1):
        scene.frame_set(f)
        for b, s in std_of.items():
            if not s:
                continue
            pb = src.pose.bones[b]
            world = src.matrix_world @ pb.matrix
            d = world.to_3x3().normalized().to_quaternion() @ rest[b].to_3x3().normalized().to_quaternion().inverted()
            d.normalize()
            rot[s].append(d)
        hw = (src.matrix_world @ src.pose.bones[hip_bone].matrix).to_translation()
        hip.append((hw - hip_rest) / hip_h)
    # giữ quaternion cùng bán cầu để nội suy không quay vòng ngược
    for s, seq in rot.items():
        for i in range(1, len(seq)):
            if seq[i].dot(seq[i - 1]) < 0:
                seq[i].negate()
    return Clip(name, len(hip), rot, hip, loop)


def load_clip(path, name, loop=False):
    before = set(bpy.data.objects)
    ext = os.path.splitext(path)[1].lower()
    if ext == ".bvh":
        bpy.ops.import_anim.bvh(filepath=path, axis_forward="-Z", axis_up="Y", rotate_mode="NATIVE",
                                update_scene_fps=False, update_scene_duration=False)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    else:
        raise ValueError(f"Định dạng chuyển động chưa hỗ trợ: {path}")
    new = [o for o in bpy.data.objects if o not in before]
    src = next(o for o in new if o.type == "ARMATURE")
    stand_upright(src)
    act = src.animation_data.action if src.animation_data else None
    f0, f1 = (int(act.frame_range[0]), int(act.frame_range[1])) if act else (1, 1)
    clip = clip_from_armature(src, name, f0, f1, loop)
    for o in new:
        bpy.data.objects.remove(o, do_unlink=True)
    return clip


_CACHE = {}


def library():
    return _load("chuyen_dong.json")


def get_clip(name):
    if name in _CACHE:
        return _CACHE[name]
    lib = library()
    if name not in lib:
        raise KeyError(f"Chuyển động '{name}' chưa có trong data/chuyen_dong.json")
    info = lib[name]
    path = info["file"] if os.path.isabs(info["file"]) else os.path.join(DATA, "chuyen_dong", info["file"])
    clip = load_clip(path, name, info.get("lap", False))
    # "chinh_dau": độ (dương = cúi xuống) — sửa dáng đầu của bản quay (vd bước đi Mixamo hếch cằm ~10° so với lúc đứng);
    # chia 1/3 cho cổ, 2/3 cho đầu để cổ không gãy
    # "he_so_dau": nhân độ lệch của đầu/cổ so với khung đầu (âm = đảo chiều; vd bản gật đầu Mixamo hất cằm lên
    # thay vì cúi xuống -> -0.8 thành gật cúi xuống kiểu "dạ")
    ks = float(info.get("he_so_dau", 1.0))
    if ks != 1.0:
        ident = Quaternion()
        for part in ("neck", "head"):
            if part in clip.rot and clip.rot[part]:
                r0 = clip.rot[part][0]
                out = []
                for r in clip.rot[part]:
                    d = r @ r0.inverted()
                    if ks < 0:
                        d = d.inverted()
                    d = ident.slerp(d, abs(ks))
                    out.append(d @ r0)
                clip.rot[part] = out
    deg = float(info.get("chinh_dau", 0.0))
    if deg:
        for part, share in (("neck", 1 / 3), ("head", 1.0)):
            if part in clip.rot:
                q = Quaternion((1, 0, 0), math.radians(deg * share))
                clip.rot[part] = [q @ r for r in clip.rot[part]]
    _CACHE[name] = clip
    return clip


# ---------------------------------------------------------------- áp chuyển động lên nhân vật
def _order(arm, std_of):
    """Khớp theo thứ tự cha trước con."""
    out = []

    def walk(b):
        out.append(b)
        for c in b.children:
            walk(c)
    for b in arm.data.bones:
        if b.parent is None:
            walk(b)
    return [b.name for b in out]


_STD_DIR = {n: (Vector(t) - Vector(h)).normalized() for n, _, h, t in STANDARD}


_STD = {n: (p, Vector(h), Vector(t)) for n, p, h, t in STANDARD}


def bone_dir(arm, std_of, b):
    """Hướng khớp lúc nghỉ (thế giới): tới đầu khớp con chuẩn nối tiếp; không có thì theo đuôi xương."""
    s = std_of[b]
    bone = arm.data.bones[b]
    tail_std = _STD[s][2]
    best, bd = None, 1e9
    stack = list(bone.children)
    while stack:
        c = stack.pop()
        cs = std_of.get(c.name)
        if cs and _STD[cs][0] == s:
            dist = (_STD[cs][1] - tail_std).length
            if dist < bd:
                best, bd = c, dist
        elif not cs:
            stack.extend(c.children)
    end = best.head_local if best is not None and bd < 0.02 else bone.tail_local
    v = arm.matrix_world.to_3x3() @ (end - bone.head_local)
    return v.normalized() if v.length > 1e-9 else Vector(_STD_DIR[s])


def rest_fix(arm, std_of, rest):
    """Mỗi khớp: phép xoay đưa hướng khớp lúc nghỉ của mô hình về hướng khớp chuẩn (tư thế chữ T).
    Nhờ vậy mô hình dựng ở tư thế chữ A (tay buông) vẫn nhận đúng chuyển động."""
    turn = Quaternion((0, 0, 1), math.radians(arm.get("sgd_huong_do", 0.0)))
    fix = {}
    for b, s in std_of.items():
        if not s:
            continue
        d = bone_dir(arm, std_of, b)
        want = turn @ _STD_DIR[s]
        fix[b] = d.rotation_difference(want) if d.angle(want) > math.radians(3) else Quaternion()
    return fix


def apply_pose(arm, std_of, rest, rot, hip, hip_h, origin, fix=None):
    """Đặt tư thế: mỗi khớp xoay đúng bằng độ xoay so với tư thế nghỉ của chuyển động (trục thế giới)."""
    inv = arm.matrix_world.inverted()
    scl = Matrix.Diagonal(arm.matrix_world.to_scale()).to_4x4()
    # chỉ xoay theo hướng đứng của diễn viên trong cảnh (không gồm phần dựng thẳng mô hình nguồn)
    turn = Quaternion((0, 0, 1), math.radians(arm.get("sgd_huong_do", 0.0)))
    for bname in _order(arm, std_of):
        s = std_of.get(bname)
        pb = arm.pose.bones[bname]
        if not s or s not in rot:
            continue
        rest_w = rest[bname]
        world_rot = (turn @ rot[s] @ turn.inverted()) @ (fix or {}).get(bname, Quaternion()) @ rest_w.to_3x3().normalized().to_quaternion()
        if s == "hips":
            head_w = rest_w.to_translation() + turn @ (hip * hip_h + origin)
        else:
            parent = pb.parent
            # đầu khớp con đi theo cha: lấy từ ma trận cha vừa đặt
            pw = arm.matrix_world @ parent.matrix
            prest = rest[parent.name]
            head_w = pw @ (prest.inverted() @ rest_w.to_translation())
        m = Matrix.Translation(head_w) @ world_rot.to_matrix().to_4x4() @ scl
        pb.matrix = inv @ m
        bpy.context.view_layer.update()


def _set_world(arm, pb, head, q, inv, scl):
    pb.matrix = inv @ (Matrix.Translation(head) @ q.to_matrix().to_4x4() @ scl)
    bpy.context.view_layer.update()


def _world(arm, pb):
    m = arm.matrix_world @ pb.matrix
    return m.translation.copy(), m.to_3x3().normalized().to_quaternion()


def _leg_ik(arm, legs, target, knee_fwd, inv, scl, use_fk=True):
    """Đưa đầu khớp cổ chân tới `target` bằng cách xoay đùi + cẳng chân (IK hai đoạn), giữ hướng gối như cũ
    và giữ nguyên hướng bàn chân trong không gian thế giới."""
    thigh, shin, foot = legs
    H, qt = _world(arm, thigh)
    K, _ = _world(arm, shin)
    A, qf = _world(arm, foot)
    l1, l2 = (K - H).length, (A - K).length
    v = target - H
    d = max(v.length, abs(l1 - l2) + 1e-4)
    # IK mềm: gần duỗi thẳng thì tiến chậm dần tới giới hạn -> gối không "bật" (chân gần thẳng rất nhạy)
    # (chỉ làm mềm phần duỗi THÊM so với tư thế gốc, nên tư thế gốc đã thẳng vẫn giữ nguyên)
    reach = l1 + l2
    start = min(max((A - H).length, reach * 0.96), reach - 1e-3)
    band = reach - start
    if d > start:
        d = start + band * (1 - math.exp(-(d - start) / band))
    u = v.normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    r = math.sqrt(max(l1 * l1 - a * a, 0.0))
    # hướng gối: theo độ gập gối của tư thế gốc (đo so với trục hông -> cổ chân gốc), cộng một chút "về trước"
    # để chân gần thẳng không lật; gối người không gập ra sau
    if use_fk:
        ax = (A - H).normalized()
        bend = (K - H) - ax * (K - H).dot(ax)
    else:
        bend = Vector((0, 0, 0))
    p = bend + knee_fwd * (0.08 * l1)
    if p.dot(knee_fwd) < 0:
        p -= knee_fwd * p.dot(knee_fwd)
    p = p - u * p.dot(u)
    if p.length < 1e-9:
        p = Vector((0, 0, 1)).cross(u)
    p.normalize()
    knee = H + u * a + p * r
    _set_world(arm, thigh, H, (K - H).rotation_difference(knee - H) @ qt, inv, scl)
    K2, qs = _world(arm, shin)
    A2, _ = _world(arm, foot)
    _set_world(arm, shin, K2, (A2 - K2).rotation_difference(H + u * d - K2) @ qs, inv, scl)
    A3, _ = _world(arm, foot)
    _set_world(arm, foot, A3, qf, inv, scl)


def reach(arm, side, target_fn, weight_fn, frames):
    """Với tay tới một điểm (vd bàn tay người kia lúc trao đồ): IK hai đoạn vai-khuỷu-cổ tay, trộn theo trọng số
    weight_fn(f) (0 = giữ động tác gốc, 1 = chạm điểm). Khuỷu tay theo độ gập gốc, không bao giờ chỉa lên.
    target_fn(f): điểm lòng bàn tay cần tới (thế giới) hoặc None. Ghi đè khoá xương tay ở các khung đó."""
    std_of = bone_map(arm)
    inv_map = {v: k for k, v in std_of.items() if v}
    bones = [arm.pose.bones[inv_map[f"{p}.{side}"]] for p in ("upper_arm", "forearm", "hand")]
    sh_b = arm.pose.bones[inv_map.get(f"shoulder.{side}", inv_map[f"upper_arm.{side}"])]
    inv = arm.matrix_world.inverted()
    scl = Matrix.Diagonal(arm.matrix_world.to_scale()).to_4x4()
    down = Vector((0, 0, -1))
    for f in frames:
        w = float(weight_fn(f))
        tgt = target_fn(f)
        if w <= 1e-4 or tgt is None:
            continue
        bpy.context.scene.frame_set(f)
        A, _ = _world(arm, bones[2])
        S, _ = _world(arm, sh_b)
        # cổ tay dừng trước điểm cần tới một đoạn (lòng bàn tay chạm, không xuyên)
        T = tgt - (tgt - S).normalized() * 0.11
        _leg_ik(arm, bones, A.lerp(T, w), down, inv, scl)
        for pb in bones:
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)


def _ground_rig(arm, std_of, rest):
    """Mỗi bên chân: (đùi, cẳng, bàn, ngón) + độ cao đầu khớp bàn/ngón so với mặt đất lúc nghỉ."""
    inv_map = {v: k for k, v in std_of.items() if v}
    pts = []
    for side in ("L", "R"):
        for part in ("foot", "toe"):
            b = inv_map.get(f"{part}.{side}")
            if b:
                pts.append(rest[b].to_translation().z)
    tails = []
    for side in ("L", "R"):
        for part in ("foot", "toe"):
            b = inv_map.get(f"{part}.{side}")
            if b:
                bone = arm.data.bones[b]
                h, t = arm.matrix_world @ bone.head_local, arm.matrix_world @ bone.tail_local
                if (t - h).length < 0.25 * (rest[inv_map["hips"]].to_translation().z - min(pts)):
                    tails.append(t.z)
    floor = min(pts + tails)
    span = rest[inv_map["hips"]].to_translation().z - min(pts)
    legs = {}
    for side in ("L", "R"):
        names = [inv_map.get(f"{n}.{side}") for n in ("thigh", "shin", "foot", "toe")]
        if not all(names[:3]):
            return None, floor
        sole = [(arm.pose.bones[n], rest[n].to_translation().z - floor, None) for n in names[2:] if n]
        # đầu mũi chân — khi nhón/gập mũi chân, đây là điểm chạm đất. Lấy từ lưới mô hình (điểm xa nhất phía trước
        # của bàn chân, sát đất), không có lưới thì lấy đuôi xương ngón (nếu hợp lý)
        tip = names[3] or names[2]
        bone = arm.data.bones[tip]
        h, t = arm.matrix_world @ bone.head_local, arm.matrix_world @ bone.tail_local
        tip_w = _mesh_toe_tip(arm, h, rest[names[2]].to_translation(), span)
        if tip_w is None and (t - h).length < 0.25 * span:
            tip_w = t
        if tip_w is not None:
            local = (arm.matrix_world @ bone.matrix_local).inverted() @ tip_w   # toạ độ trong xương ngón
            sole.append((arm.pose.bones[tip], tip_w.z - floor, local))
        # gót giày theo lưới (sau mắt cá, sát đất): đi cứng theo bàn chân, là điểm chạm đất khi tiếp gót
        fb = arm.data.bones[names[2]]
        heel_w = _mesh_heel(arm, arm.matrix_world @ fb.head_local, h, floor, span)
        if heel_w is not None:
            local = (arm.matrix_world @ fb.matrix_local).inverted() @ heel_w
            sole.append((arm.pose.bones[names[2]], heel_w.z - floor, local))
        legs[side] = {"bones": [arm.pose.bones[n] for n in names[:3]], "sole": sole}
    return legs, floor


def _mesh_toe_tip(arm, toe_head, foot_head, span):
    """Đầu mũi chân theo lưới đã gắn xương: đỉnh phía trước nhất trong vùng bàn chân, sát đất (tư thế nghỉ)."""
    fwd = toe_head - foot_head
    fwd.z = 0
    if fwd.length < 1e-6:
        return None
    fwd.normalize()
    side = Vector((-fwd.y, fwd.x, 0))
    best = None
    for o in bpy.data.objects:
        if o.type != "MESH" or not (o.parent == arm or any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)):
            continue
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            r = w - toe_head
            if w.z > toe_head.z + 0.03 * span or abs(r.dot(side)) > 0.08 * span or r.dot(fwd) < 0:
                continue
            if r.dot(fwd) > 0.25 * span:
                continue
            if best is None or r.dot(fwd) > (best - toe_head).dot(fwd):
                best = w.copy()
    if best is None:
        return None
    best.z = min(best.z, toe_head.z)
    return best


def _mesh_heel(arm, ankle, toe_head, floor, span):
    """Gót theo lưới: đỉnh lùi về sau nhất của vùng bàn chân, sát đất (tư thế nghỉ). None nếu không thấy."""
    fwd = toe_head - ankle
    fwd.z = 0
    if fwd.length < 1e-6:
        return None
    fwd.normalize()
    side = Vector((-fwd.y, fwd.x, 0))
    hz = ankle.z - floor
    best = None
    for o in bpy.data.objects:
        if o.type != "MESH" or not (o.parent == arm or any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)):
            continue
        mw = o.matrix_world
        for v in o.data.vertices:
            w = mw @ v.co
            if w.z > floor + 0.35 * hz:
                continue
            r = w - ankle
            r.z = 0
            if abs(r.dot(side)) > 0.05 * span or r.dot(fwd) > 0 or r.dot(fwd) < -0.15 * span:
                continue
            if best is None or r.dot(fwd) < (best - ankle).dot(fwd):
                best = w.copy()
    if best is None:
        return None
    best.z = floor
    return best


def sole_points(arm, leg, tip=False):
    """Các điểm lòng bàn chân (gót/cổ chân, gốc ngón; thêm đầu mũi nếu tip), đã trừ độ cao lúc nghỉ: z = 0 là chạm đất.
    Đầu mũi (gắn xương ngón) chỉ dùng để chặn xuyên đất, không dùng để ghim chân (mũi chân gập/xoè làm điểm đó trượt giả);
    gót theo lưới gắn cứng vào bàn chân nên luôn được tính."""
    foot = leg["bones"][2]
    return [(arm.matrix_world @ (pb.matrix @ local if local is not None else pb.head)) - Vector((0, 0, h))
            for pb, h, local in leg["sole"] if tip or local is None or pb == foot]


def _lift(arm, leg, floor, tip=False):
    """Độ cao thấp nhất của lòng bàn chân bên này so với mặt đất (âm = xuyên đất)."""
    return min(p.z for p in sole_points(arm, leg, tip)) - floor


def bake_timeline(arm, items, end, fade=0.3, start_frame=1, cham_dat=True):
    """items: [{"ten": "di", "giay": 3.0, "bat_dau": 0.0}, ...] (bat_dau tính sẵn, giây).

    Chuyển mềm `fade` giây giữa hai động tác; bước đi cộng dồn để động tác sau nối từ chỗ động tác trước dừng.
    cham_dat: lớp chạm đất dùng chung cho mọi nhân vật —
      * động tác "cham_dat": "ca_nguoi" (mặc định): hạ/nâng cả người để bàn chân thấp nhất luôn chạm đất
        (hết lơ lửng, hết xuyên đất khi đứng/đi), làm mượt qua các khung;
      * mọi động tác: bàn chân nào xuyên đất thì gập gối (IK) nhấc lên đúng mặt đất;
      * bàn chân đang chạm đất được khoá tại chỗ (hết trượt chân), nhả mềm khi nhấc lên.
    """
    std_of = bone_map(arm)
    rest = _world_rest(arm)
    hip_bone, hip_h = hip_height(arm, std_of, rest)
    fix = rest_fix(arm, std_of, rest)
    lib = library()
    seq = []
    for it in items:
        clip = get_clip(it["ten"])
        dur = float(it.get("giay") or (lib.get(it["ten"]) or {}).get("giay") or clip.duration())
        mode = (lib.get(it["ten"]) or {}).get("cham_dat", "ca_nguoi")
        seq.append((it["bat_dau"], dur, clip, mode))
    seq.sort(key=lambda x: x[0])
    fade_f = fade * FPS

    # điểm gốc của từng động tác: nơi động tác trước kết thúc (chỉ cộng phần di chuyển ngang)
    # bắt đầu đi từ chỗ đứng yên: chạy bước đi từ nhịp có một chân đang trụ ngay dưới hông (chân đó làm trụ)
    inv_map = {v: kk for kk, v in std_of.items() if v}
    feet_b = [inv_map.get("foot.L"), inv_map.get("foot.R")]

    def _moving(c):
        return c.loop and (c.hip[-1] - c.hip[0]).xy.length > 0.05

    phase = [0.0] * len(seq)
    start_plant = {}
    if all(feet_b):
        for k in range(len(seq)):
            clip = seq[k][2]
            if not _moving(clip) or (k > 0 and _moving(seq[k - 1][2])):
                continue
            _, h0 = clip.sample(0)
            samples = []
            for lf in range(clip.n - 1):
                rot, hip = clip.sample(lf)
                hip = hip - Vector((h0.x, h0.y, 0))
                apply_pose(arm, std_of, rest, rot, hip, hip_h, Vector((0, 0, 0)), fix)
                hp = arm.matrix_world @ arm.pose.bones[hip_bone].head
                for sd, b in zip("LR", feet_b):
                    fp = arm.matrix_world @ arm.pose.bones[b].head
                    # đo theo chiều cao hông -> cùng một kết quả cho mọi cỡ người, mọi kiểu xương
                    samples.append((lf, sd, fp.z / hip_h, (fp.xy - hp.xy).length / hip_h))
            zmin = min(z for _, _, z, _ in samples)
            stance = [x for x in samples if x[2] < zmin + 0.012]
            lf, sd, _, _ = min(stance, key=lambda x: (round(x[3], 2), x[0], x[1]))
            phase[k] = float(lf)
            start_plant[k] = sd

    origins = []
    acc = Vector((0, 0, 0))
    for k, (t0, dur, clip, _) in enumerate(seq):
        origins.append(acc.copy())
        _, hip_end = clip.sample(phase[k] + dur * FPS)
        _, hip_start = clip.sample(phase[k])
        moved = (hip_end - hip_start) * hip_h
        acc += Vector((moved.x, moved.y, 0))

    def pose_at(k, frame, cont=False):
        t0, dur, clip, _ = seq[k]
        lt = max((frame - start_frame) / FPS - t0, 0.0)
        # động tác lặp (đi, đứng thở) vẫn chạy tiếp trong lúc chuyển sang động tác sau -> không khựng
        lt = (lt if (cont and clip.loop) else min(lt, dur)) * FPS + phase[k]
        rot, hip = clip.sample(lt)
        _, hip0 = clip.sample(phase[k])
        hip = hip - Vector((hip0.x, hip0.y, 0))
        return rot, hip, origins[k]

    # dừng bước: động tác đứng yên sau khi đi thì đặt người vào giữa hai bàn chân lúc dừng (chân trước giữ chỗ,
    # chân sau bước lên khép lại) thay vì kéo cả hai chân về dưới hông
    stops = []
    starts = []
    if all(feet_b):
        for k in range(1, len(seq)):
            prev, cur = seq[k - 1][2], seq[k][2]
            prev_moving = prev.loop and (prev.hip[-1] - prev.hip[0]).xy.length > 0.05
            cur_moving = cur.loop and (cur.hip[-1] - cur.hip[0]).xy.length > 0.05
            if cur_moving and not prev_moving:
                # bắt đầu bước: chân nào lúc vào nhịp đi ở gần chỗ đang đứng nhất thì giữ trụ, chân kia bước đi
                f0 = start_frame + seq[k][0] * FPS
                f1 = f0 + fade_f * 1.7
                plant = start_plant.get(k, "L")
                trail = "R" if plant == "L" else "L"
                starts.append((int(round(f0)), plant, trail))
                continue
            if not prev_moving or cur_moving:
                continue
            f_end = start_frame + seq[k][0] * FPS
            rot, hip, org = pose_at(k - 1, f_end)
            apply_pose(arm, std_of, rest, rot, hip, hip_h, org, fix)
            pos = {sd: arm.matrix_world @ arm.pose.bones[b].head for sd, b in zip("LR", feet_b)}
            heading = Quaternion((0, 0, 1), math.radians(arm.get("sgd_huong_do", 0.0)))
            fwd = heading @ Vector((0, -1, 0))
            # chân trụ: chân thấp hơn (đang tì đất); ngang nhau thì chân phía trước
            plant = min("LR", key=lambda sd: (round(pos[sd].z, 2), -pos[sd].dot(fwd)))
            trail = "R" if plant == "L" else "L"
            rp = {sd: rest[b].to_translation() for sd, b in zip("LR", feet_b)}
            E = pos[plant] + (rp[trail] - rp[plant])          # tư thế nghỉ đã theo hướng đứng của diễn viên
            E.z = rp[trail].z
            hip_xy = (arm.matrix_world @ arm.pose.bones[hip_bone].head).xy
            mid = (pos[plant].xy + E.xy) / 2
            rest_mid = (rp["L"].xy + rp["R"].xy) / 2
            off = (mid - hip_xy) - (rest_mid - rest[hip_bone].to_translation().xy)
            off = heading.inverted() @ Vector((off.x, off.y, 0))   # gốc động tác tính theo hướng diễn viên
            for m in range(k, len(origins)):
                origins[m] = origins[m] + Vector((off.x, off.y, 0))
            stops.append((int(round(f_end)), plant, trail, pos[plant].copy(), pos[trail].copy(), E))

    def blended(k, f, cont=False):
        """Tư thế của động tác k tại khung f, gồm cả phần đang chuyển từ động tác trước. Đệ quy: nếu động tác
        trước cũng còn đang chuyển dở (động tác ngắn hơn thời gian chuyển) thì trộn từ chính tư thế đã trộn đó
        -> không có bước nhảy dù các động tác nối sát nhau."""
        t = (f - start_frame) / FPS
        rot, hip, org = pose_at(k, f, cont)
        if k == 0:
            return rot, hip, org
        since = (t - seq[k][0]) * FPS
        prev, cur = seq[k - 1][2], seq[k][2]
        moving = prev.loop and (prev.hip[-1] - prev.hip[0]).xy.length > 0.05
        starting = cur.loop and (cur.hip[-1] - cur.hip[0]).xy.length > 0.05 and not moving
        ff = fade_f * (1.7 if (moving or starting) else 1.0)   # dừng bước / bắt đầu bước: chuyển chậm hơn cho kịp chân
        if since >= ff:
            return rot, hip, org
        a = since / ff
        a = a * a * (3 - 2 * a)
        rot0, hip0, org0 = blended(k - 1, f, cont=True)
        rot = {n: (rot0[n].slerp(rot[n], a) if n in rot0 and n in rot else rot.get(n, rot0.get(n)))
               for n in set(rot) | set(rot0)}
        hip = (hip0 * hip_h + org0).lerp(hip * hip_h + org, a)
        return rot, hip / hip_h, Vector((0, 0, 0))

    frames = []
    for f in range(start_frame, end + 1):
        t = (f - start_frame) / FPS
        k = 0
        for i, (t0, _, _, _) in enumerate(seq):
            if t >= t0:
                k = i
        rot, hip, org = blended(k, f)
        frames.append((f, rot, hip, org, seq[k][3]))

    legs, floor = _ground_rig(arm, std_of, rest) if cham_dat else (None, 0.0)
    inv = arm.matrix_world.inverted()
    scl = Matrix.Diagonal(arm.matrix_world.to_scale()).to_4x4()
    turn = Quaternion((0, 0, 1), math.radians(arm.get("sgd_huong_do", 0.0)))
    knee_fwd = turn @ Vector((0, -1, 0))
    drop = [0.0] * len(frames)
    if legs:
        # lượt 1: tư thế gốc -> đo độ lơ lửng / xuyên đất, vị trí cổ chân
        lifts, ankles, soles = [], [], []
        for i, (f, rot, hip, org, mode) in enumerate(frames):
            bpy.context.scene.frame_set(f)
            apply_pose(arm, std_of, rest, rot, hip, hip_h, org, fix)
            lf = {s: _lift(arm, legs[s], floor) for s in legs}
            lifts.append(lf)
            ankles.append({s: _world(arm, legs[s]["bones"][2])[0] for s in legs})
            soles.append({s: sole_points(arm, legs[s]) for s in legs})
            drop[i] = -min(lf.values()) if mode == "ca_nguoi" else None
        # động tác chỉ ghim chân (ngồi...): độ hạ người nhả dần về 0 trong 0,4 giây, và đón dần trước khi sang
        # động tác cả người -> không giật ở chỗ chuyển
        ramp = int(0.4 * FPS)
        raw = drop[:]
        for i in range(len(raw)):
            if raw[i] is None:
                p_i = next((j for j in range(i - 1, -1, -1) if raw[j] is not None), None)
                n_i = next((j for j in range(i + 1, len(raw)) if raw[j] is not None), None)
                v = 0.0
                if p_i is not None and i - p_i < ramp:
                    u = (i - p_i) / ramp
                    v += raw[p_i] * (1 - u * u * (3 - 2 * u))
                if n_i is not None and n_i - i < ramp:
                    u = (n_i - i) / ramp
                    v += raw[n_i] * (1 - u * u * (3 - 2 * u))
                drop[i] = v
        # làm mượt độ hạ người (Gauss, σ = 1,5 khung)
        ker = [math.exp(-0.5 * (x / 1.5) ** 2) for x in range(-4, 5)]
        sm = []
        for i in range(len(drop)):
            w = s_ = 0.0
            for j, kv in zip(range(i - 4, i + 5), ker):
                if 0 <= j < len(drop):
                    w += kv
                    s_ += kv * drop[j]
            sm.append(s_ / w)
        drop = sm
        # quỹ đạo bàn chân (bù trượt): lúc chạm đất, điểm lòng bàn chân đang tì đất (gót/cổ chân hay mũi) được giữ
        # đứng yên bằng cách cộng dồn độ bù ngược với độ trượt của tư thế gốc; lúc nhấc chân, độ bù về 0 theo đường
        # mượt (chân đặt xuống đúng chỗ động tác muốn). Bù quá xa (tư thế gốc trượt nhiều, vd lúc chuyển động tác)
        # thì chèn một bước nhỏ nhấc chân lên.
        k = hip_h / REF_HIP
        eps, max_slide, step_n = 0.012 * k, 0.10 * k, 9
        n = len(frames)
        plan = {}
        busy = [False] * n                     # khung đang có chân khác bước
        reset_at = 0.035 * k                  # đứng yên mà lệch quá mức này -> bước chỉnh lại
        quiet = [False] * n
        for i in range(2, n - 2):
            quiet[i] = all((ankles[m][sd] - ankles[m - 1][sd]).length < 0.004 * k
                           for m in range(i - 2, i + 3) for sd in legs)
        # bước dừng: khung -> đích cổ chân (tuyệt đối) cho từng chân
        override = {sd: {} for sd in legs}
        win = int(round(fade_f * 1.7))
        pinned = {}
        for f0, plant, trail, P, S, E in stops:
            i0 = f0 - start_frame
            low = S.z < E.z + 0.02 * k                  # chân sau đang tì đất -> nhấc lên khi bước
            for q in range(0, win + 1):
                u = q / win
                w = u ** 3 * (10 - 15 * u + 6 * u * u)
                bump = 0.05 * k * math.sin(math.pi * u) ** 2 if low else 0.0
                wp = min(1.0, q / 3.0)
                if 0 <= i0 + q < n:
                    # đích = trộn dần từ quỹ đạo gốc sang chỗ đặt chân -> giữ đà, không khựng
                    override[trail][i0 + q] = (E, w, bump)
                    override[plant][i0 + q] = ("ghim", 1.0, 0.0)
        for f0, plant, trail in starts:
            i0 = f0 - start_frame
            # bước dừng trước đó còn dở (đứng quá ngắn): đợi bước dừng xong mới khởi bước
            while i0 < n and (i0 in override[plant] or i0 in override[trail]):
                i0 += 1
            for q in range(0, win + 1):
                u = q / win
                w = u ** 3 * (10 - 15 * u + 6 * u * u)
                bump = 0.05 * k * math.sin(math.pi * u) ** 2
                if 0 <= i0 + q < n:
                    override[trail][i0 + q] = ("theo_goc", w, bump)     # rời chỗ đứng, hoà dần vào nhịp đi
                    override[plant][i0 + q] = ("ghim_dau", 1.0, 0.0)
        pins = {}                                           # (chân, khung bắt đầu đoạn) -> chỗ ghim thật

        def plan_side(s, override_all, busy):
            override = {s: dict(override_all[s])}         # bản riêng: có thể bỏ bớt đoạn ghim khi chân trụ nhấc
            # chạm đất = sát đất VÀ điểm tì gần như đứng yên (chân lê sát đất lúc bước, vd đi buồn, không bị ghim nhầm)
            vmax = 0.6 * k / FPS                       # 0,6 m/giây (người 1,70 m)

            def still(i):
                sp = soles[i][s]
                q = min(range(len(sp)), key=lambda m: sp[m].z)
                j = i + 1 if i + 1 < n else i - 1
                d = soles[j][s][q] - sp[q]
                return math.hypot(d.x, d.y) < vmax
            contact = [lifts[i][s] + drop[i] < eps and still(i) for i in range(n)]
            corr = [Vector((0, 0))] * n
            arc = [0.0] * n
            run = 0
            pin = None
            pin_q = None
            c_start = None
            last_ov = -1
            i = 0
            while i < n - 1:
                if (i + 1) not in override[s]:
                    pin = None
                    pin_q = None
                    c_start = None
                ov = override[s].get(i + 1)
                if ov and ov[0] == "ghim_dau" and not contact[i + 1] and pin is not None:
                    # chân trụ đã nhấc theo nhịp đi -> bỏ phần ghim còn lại, để lớp thường đưa chân theo nhịp
                    q = i + 1
                    while q in override[s] and override[s][q][0] == "ghim_dau":
                        del override[s][q]
                        q += 1
                    ov = None
                if ov:
                    if last_ov != i:                         # vừa vào một đoạn mới: bỏ trạng thái của đoạn cũ
                        pin = None
                        pin_q = None
                        c_start = None
                        seg0 = i + 1
                    goal, w, bump = ov
                    a3 = ankles[i + 1][s]
                    fkz = a3.z + drop[i + 1]
                    if goal in ("ghim", "ghim_dau"):      # chân trụ: giữ đúng chỗ đang đứng
                        if pin is None and not contact[i + 1] and goal == "ghim":
                            # chân trụ lúc dừng còn đang hạ xuống: cứ theo đà tới khi chạm đất rồi mới ghim
                            tg = Vector((a3.x + corr[i].x, a3.y + corr[i].y, fkz))
                        else:
                            sp = soles[i + 1][s]
                            if pin is None:
                                # ghim từng điểm lòng bàn chân (gót/cổ chân, mũi) tại chỗ đang đứng
                                pin = [Vector((p.x + corr[i].x, p.y + corr[i].y)) for p in sp]
                                # chỉ lần ghim đầu của đoạn: đoạn dừng nối liền đoạn khởi bước thì lần ghim sau (bước kế)
                                # không được đè lên chỗ chân trụ lúc dừng
                                pins.setdefault((s, seg0), Vector((a3.x + corr[i].x, a3.y + corr[i].y, fkz)))
                            # điểm đang tì đất giữ yên, cổ chân theo dáng bàn chân (gót nhấc lăn qua mũi chân vẫn đúng)
                            q = min(range(len(sp)), key=lambda m: sp[m].z)
                            if pin_q is not None and q != pin_q:
                                # đổi điểm tì (gót -> mũi): ghim điểm mới tại đúng chỗ nó đang ở khung trước -> không nhảy
                                pp_ = soles[i][s][q]
                                pin[q] = Vector((pp_.x + corr[i].x, pp_.y + corr[i].y))
                            pin_q = q
                            tg = Vector((pin[q].x + a3.x - sp[q].x, pin[q].y + a3.y - sp[q].y, fkz))
                    else:
                        pin = None
                        pin_q = None
                        if c_start is None:            # bắt đầu bước dừng: đi tiếp từ chỗ chân đang thật sự ở
                            c_start = (corr[i].copy(), max(arc[i], 0.0))
                    if goal not in ("ghim", "ghim_dau"):
                        c0, z0 = c_start
                        fk3 = Vector((a3.x, a3.y, fkz))
                        tg = (fk3 + Vector((c0.x, c0.y, z0))).lerp(fk3 if goal == "theo_goc" else goal, w)
                        tg.z += bump
                    corr[i + 1] = Vector((tg.x - a3.x, tg.y - a3.y))
                    arc[i + 1] = tg.z - (a3.z + drop[i + 1])
                    last_ov = i + 1
                    run = 0
                    i += 1
                    if (i + 1) not in override[s]:
                        # hết bước dừng: phần lệch độ cao nhả dần trong 6 khung
                        last = arc[i]
                        for q in range(1, 7):
                            if i + q < n:
                                u = q / 6
                                arc[i + q] += last * (1 - u * u * (3 - 2 * u))
                    continue
                if contact[i] and contact[i + 1]:
                    pid = min(range(len(soles[i][s])), key=lambda q: soles[i][s][q].z)
                    v = soles[i + 1][s][pid] - soles[i][s][pid]
                    run += 1
                    g = min(1.0, run / 4.0)          # vừa chạm đất: cho bàn chân "lăn" nốt rồi mới ghim hẳn
                    c = corr[i] - Vector((v.x, v.y)) * (g * g * (3 - 2 * g))
                    need = c.length > max_slide or (c.length > reset_at and quiet[i])
                    free = not any((i + q) in override[s] for q in range(1, step_n + 1))
                    if need and free and i + step_n < n and not any(busy[i:i + step_n + 1]):
                        # chèn bước: trong step_n khung độ bù về 0, bàn chân nhấc lên rồi đặt xuống
                        for q in range(1, step_n + 1):
                            u = q / step_n
                            w = u ** 3 * (10 - 15 * u + 6 * u * u)
                            corr[i + q] = c.lerp(Vector((0, 0)), w) if q > 1 else c
                            arc[i + q] = 0.07 * k * math.sin(math.pi * u) ** 4   # lên/xuống êm (gối gần thẳng rất nhạy)
                            busy[i + q] = True
                        i += step_n
                        run = 0
                        continue
                    corr[i + 1] = c
                    i += 1
                elif not contact[i + 1]:
                    run = 0
                    b = i + 1
                    while b < n and not contact[b] and b not in override[s]:
                        b += 1
                    if b >= n:
                        for q in range(i + 1, n):          # nhấc chân tới hết cảnh: nhả dần
                            u = min(1.0, (q - i) / 6.0)
                            corr[q] = corr[i] * (1 - u * u * (3 - 2 * u))
                        break
                    for q in range(i + 1, b + 1):
                        u = (q - i) / (b - i)
                        w = u ** 3 * (10 - 15 * u + 6 * u * u)
                        corr[q] = corr[i].lerp(Vector((0, 0)), w)
                    i = b
                else:
                    i += 1
            return corr, arc
        # lượt A: chân trụ ghim đúng chỗ thật -> lượt B: dời chỗ đặt chân sau theo chỗ ghim thật
        busy1 = [False] * n
        first = {s: plan_side(s, override, busy1) for s in legs}
        shift_ok = False
        for f0, plant, trail, P, S, E in stops:
            i0 = f0 - start_frame
            if (plant, i0) in pins:
                pp = pins[(plant, i0)]
                d = Vector((pp.x - P.x, pp.y - P.y, 0))
                for q, (goal, w, bump) in list(override[trail].items()):
                    if i0 <= q <= i0 + win and not isinstance(goal, str):
                        override[trail][q] = (goal + d, w, bump)
                        shift_ok = True
        busy2 = [False] * n
        plan = {s: (plan_side(s, override, busy2) if shift_ok else first[s]) for s in legs}
        # tiếp đất êm: phần nâng chân để khỏi xuyên đất được biết trước cả đoạn -> bắt đầu nâng từ 3 khung trước
        # (cực đại nhìn tới, mềm cos²) thay vì chặn cứng đúng khung chạm (gối khựng một khung, rõ nhất khi nhảy xuống)
        pre = {}
        R = 3
        wts = [math.cos(math.pi * d / (2 * (R + 1))) ** 2 for d in range(R + 1)]
        # + chân còn lao ngang nhanh (gót dài chạm đất sớm lúc chân chưa dừng) thì giữ cách đất theo tốc độ:
        #   0 khi chậm hơn 0,6 m/giây, ~1,2 cm ở 1,2 m/giây, ~5 cm ở 3 m/giây -> gót đặt xuống đúng lúc chân dừng
        need = {s: [0.0] * n for s in legs}
        low_pt = {s: [None] * n for s in legs}
        lift_now = {s: [0.0] * n for s in legs}
        for i, (f, rot, hip, org, mode) in enumerate(frames):    # đo đúng tư thế lượt 2 sẽ dựng (trước IK)
            apply_pose(arm, std_of, rest, rot, hip, hip_h, org + Vector((0, 0, drop[i])), fix)
            for s in legs:
                sp = sole_points(arm, legs[s])
                lp = min(sp, key=lambda p: p.z)
                c = plan[s][0][i]
                low_pt[s][i] = Vector((lp.x + c.x, lp.y + c.y))
                lift_now[s][i] = _lift(arm, legs[s], floor) + plan[s][1][i]
        v0, kc, wv = 0.6 * k, 0.02, 0.4 * k
        sw = 0.03 * k                                       # đang vung chân: cách đất hơn 3 cm
        gk = [math.exp(-0.5 * (x / 1.5) ** 2) for x in range(-4, 5)]
        for s in legs:
            lf = lift_now[s]
            clear = [0.0] * n
            i = 0
            while i < n:
                if lf[i] <= sw:
                    i += 1
                    continue
                j = i
                while j < n and lf[j] > sw:
                    j += 1
                apex = max(range(i, j), key=lambda q: lf[q])
                # pha tiếp đất: từ đỉnh vung chân tới khi điểm thấp nhất gần dừng (tối đa 10 khung sau khi hạ thấp)
                q = apex
                while q < min(n, j + 10):
                    a_, b_ = max(0, q - 1), min(n - 1, q + 1)
                    v = (low_pt[s][b_] - low_pt[s][a_]).length * FPS / max(1, b_ - a_)
                    if q >= j and v < v0:
                        break
                    x = v - v0                              # tăng mềm từ 0 (không gãy góc ở 0,6 m/giây)
                    x = 0.0 if x <= -wv else (x if x >= wv else (x + wv) ** 2 / (4 * wv))
                    clear[q] = kc * x * k
                    q += 1
                i = j
            # mềm theo thời gian, rồi gộp mềm với độ cao sẵn có (không gãy góc khi hai đường cắt nhau)
            cs = []
            for q in range(n):
                w_ = s_ = 0.0
                for d_, kv in zip(range(q - 4, q + 5), gk):
                    if 0 <= d_ < n:
                        w_ += kv
                        s_ += kv * clear[d_]
                cs.append(s_ / w_)
            dl = 0.006 * k
            for q in range(n):
                a_, b_ = lf[q], cs[q]
                top = 0.5 * (a_ + b_ + math.sqrt((a_ - b_) ** 2 + dl * dl)) if b_ > 1e-5 else max(a_, 0.0)
                need[s][q] = max(0.0, max(top, 0.0) - a_)
        for s in legs:
            nd = need[s]
            # chỉ nhìn tới trước: đón cú chạm sắp tới; lúc chân rời đất thì không kéo dài phần chặn cũ
            sm_ = [max(nd[j] * wts[j - i] for j in range(i, min(n, i + R + 1))) for i in range(n)]
            pre[s] = [a_ - max(0.0, -lf) for a_, lf in zip(sm_, lift_now[s])]     # phần chặn sàn vẫn do bước chặn cứng bên dưới

    for i, (f, rot, hip, org, mode) in enumerate(frames):
        bpy.context.scene.frame_set(f)
        apply_pose(arm, std_of, rest, rot, hip, hip_h, org + Vector((0, 0, drop[i])), fix)
        if legs:
            for s, leg in legs.items():
                A, _ = _world(arm, leg["bones"][2])
                T = A.copy()
                corr, arc = plan[s]
                T.x += corr[i].x
                T.y += corr[i].y
                T.z += arc[i] + pre[s][i]
                lift = _lift(arm, leg, floor) + (T.z - A.z)
                if lift < 0:
                    T.z -= lift
                if (T - A).length > 5e-4:
                    _leg_ik(arm, leg["bones"], T, knee_fwd, inv, scl)
                # đầu mũi chân chạm/xuyên đất mà gót, gốc ngón còn trên đất: gập khớp ngón lên (như người nhón chân)
                for pb_t, h_t, local in leg["sole"]:
                    if local is None or pb_t == leg["bones"][2]:       # gót / điểm cứng theo bàn chân: đã nâng ở trên
                        continue
                    tipw = arm.matrix_world @ (pb_t.matrix @ local)
                    depth = floor + h_t - tipw.z
                    if depth > 1e-4:
                        head, q = _world(arm, pb_t)
                        r = tipw - head
                        axis = r.cross(Vector((0, 0, 1)))
                        if axis.length > 1e-6 and r.length > 1e-6:
                            ang = math.asin(min(1.0, depth / r.length))
                            _set_world(arm, pb_t, head, Quaternion(axis.normalized(), ang) @ q, inv, scl)
        for bname, sname in std_of.items():
            if not sname:
                continue
            pb = arm.pose.bones[bname]
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)
    return arm


# ---------------------------------------------------------------- diễn viên trong cảnh
def load_model(path, name, mat_goc=False):
    """Nạp mô hình 3D đã gắn xương (.glb/.gltf/.fbx, xương tư thế chữ T như Mixamo). Trả bộ xương.
    mat_goc=True: bỏ qua file nắn mặt / ảnh da chiếu mặt (dùng khi tạo lại chính các file đó)."""
    before = set(bpy.data.objects)
    ext = os.path.splitext(path)[1].lower()
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path, automatic_bone_orientation=False)
    else:
        raise ValueError(f"Mô hình chưa hỗ trợ định dạng: {path}")
    new = [o for o in bpy.data.objects if o not in before]
    arm = next((o for o in new if o.type == "ARMATURE"), None)
    if arm is None:
        raise ValueError(f"{path}: không có bộ xương (cần gắn xương, vd Mixamo auto-rig)")
    arm.name = name
    # bỏ vật thừa đi kèm file (vd khối cầu mẫu) không gắn vào bộ xương
    for o in new:
        if o.type == "MESH" and o.parent != arm and not any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers):
            bpy.data.objects.remove(o, do_unlink=True)
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.rotation_mode = "QUATERNION"
    stand_upright(arm)
    _fix_foot_weights(arm)
    _fix_hand_size(arm)
    from . import ghep_dau
    if ghep_dau.apply(arm, path, bone_map):
        arm["sgd_dau_rieng"] = True       # đầu riêng đã theo ảnh gốc: bỏ file nắn / chiếu mặt cũ
    elif not mat_goc:
        _apply_face_files(arm, path)
    return arm


TAY_TI_LE = 0.105      # bàn tay (cổ tay -> đầu ngón) / chiều cao, theo bộ ảnh gốc nhân vật


def _fix_hand_size(arm, target=TAY_TI_LE):
    """Mô hình dựng tự động hay làm bàn tay to (Meshy: ~12% chiều cao, ảnh gốc ~10%): thu lưới bàn tay về
    quanh cổ tay, mềm dần lên cẳng tay. Trả hệ số đã áp (1 = không đổi)."""
    std_of = bone_map(arm)
    inv = {v: k for k, v in std_of.items() if v}
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and
              (o.parent == arm or any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers))]
    if not meshes:
        return 1.0
    me = max(meshes, key=lambda o: len(o.data.vertices))
    n = len(me.data.vertices)
    co = np.empty(n * 3, np.float32)
    me.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    M = np.array(me.matrix_world)
    P = co @ M[:3, :3].T + M[:3, 3]
    H = float(P[:, 2].max() - P[:, 2].min())
    names = {g.index: g.name for g in me.vertex_groups}
    k_all = []
    for side in ("L", "R"):
        hb = inv.get(f"hand.{side}")
        if not hb or hb not in me.vertex_groups:
            continue
        sub = {hb} | {b.name for b in arm.data.bones[hb].children_recursive}
        wrist = np.array((arm.matrix_world @ arm.data.bones[hb].head_local)[:])
        wsum = np.zeros(n)
        for v in me.data.vertices:
            for ge in v.groups:
                if names.get(ge.group) in sub:
                    wsum[v.index] += ge.weight
        idx = np.nonzero(wsum > 0.5)[0]
        if len(idx) < 20:
            continue
        L = float(np.linalg.norm(P[idx] - wrist, axis=1).max())
        k = target * H / L
        if k > 0.97:
            continue
        k = max(k, 0.75)
        w = np.clip(wsum, 0, 1)[:, None]
        Pn = P + (wrist + (P - wrist) * k - P) * w
        k_all.append(k)
        P = Pn
    if not k_all:
        return 1.0
    Minv = np.linalg.inv(M)
    co = P @ Minv[:3, :3].T + Minv[:3, 3]
    me.data.vertices.foreach_set("co", co.astype(np.float32).ravel())
    me.data.update()
    return min(k_all)


def _apply_face_files(arm, model_path):
    """Nếu có (do chieu_mat tạo): <mô hình>_mat_hinh.npy = độ nắn hình khối mặt, <mô hình>_da_mat.png = ảnh da
    đã chiếu mặt gốc. Áp sau khi dựng thẳng (toạ độ lưới)."""
    base = os.path.splitext(model_path)[0]
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and
              (o.parent == arm or any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers))]
    if not meshes:
        return
    me = max(meshes, key=lambda o: len(o.data.vertices))
    npy = base + "_mat_hinh.npy"
    if os.path.exists(npy):
        d = np.load(npy)
        if d.shape == (len(me.data.vertices), 3):
            co = np.empty(d.size, np.float32)
            me.data.vertices.foreach_get("co", co)
            me.data.vertices.foreach_set("co", (co.reshape(-1, 3) + d).ravel())
            me.data.update()
    png = base + "_da_mat.png"
    if os.path.exists(png):
        img = bpy.data.images.load(png, check_existing=True)
        for m in me.data.materials:
            if not m or not m.use_nodes:
                continue
            for nd in m.node_tree.nodes:
                if nd.type == "TEX_IMAGE" and nd.outputs[0].links and \
                        nd.outputs[0].links[0].to_socket.name == "Base Color":
                    img.colorspace_settings.name = nd.image.colorspace_settings.name
                    nd.image = img


def _fix_foot_weights(arm):
    """Đế + gót giày phải đi cứng theo bàn chân. Gắn xương tự động (Meshy...) hay chia gót cho ống chân,
    nên khi gập cổ chân gót giày chọc xuống sàn. Chuyển phần trọng số ống chân/đùi ở vùng dưới mắt cá
    sang xương bàn chân (mềm dần lên tới 75% chiều cao mắt cá)."""
    std = bone_map(arm)
    inv = {v: k for k, v in std.items() if v}
    meshes = [o for o in arm.children if o.type == "MESH"] or [
        o for o in bpy.data.objects if o.type == "MESH" and any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers)]
    if not meshes:
        return 0
    moved = 0
    for o in meshes:
        n = len(o.data.vertices)
        co = np.empty(n * 3)
        o.data.vertices.foreach_get("co", co)
        M = np.array(o.matrix_world)
        P = co.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3]
        floor = float(P[:, 2].min())
        names = {g.index: g.name for g in o.vertex_groups}
        for side in ("L", "R"):
            foot, toe = inv.get("foot." + side), inv.get("toe." + side)
            if not foot or foot not in o.vertex_groups:
                continue
            ank = arm.matrix_world @ arm.data.bones[foot].head_local
            tip = arm.matrix_world @ (arm.data.bones[toe].tail_local if toe else arm.data.bones[foot].tail_local)
            hz = ank.z - floor
            if hz <= 1e-4:
                continue
            keep = {foot, toe}
            gfoot = o.vertex_groups[foot]
            # chỉ vùng bàn chân này: gần đoạn mắt cá -> mũi chân (mặt phẳng ngang)
            a2 = np.array([ank.x, ank.y]); b2 = np.array([tip.x, tip.y]); ab = b2 - a2
            L2 = max(float(ab @ ab), 1e-9)
            for i in np.nonzero(P[:, 2] < floor + 0.75 * hz)[0]:
                t = np.clip(((P[i, :2] - a2) @ ab) / L2, -0.6, 1.0)
                if np.linalg.norm(P[i, :2] - (a2 + t * ab)) > 0.9 * hz + 0.03:
                    continue
                v = o.data.vertices[i]
                gs = [(ge, names[ge.group]) for ge in v.groups]
                if not any(nm in keep for _, nm in gs):
                    continue                                   # đỉnh không thuộc bàn chân này
                k = 1.0 - _smooth01((P[i, 2] - floor) / hz, 0.45, 0.75)
                if k <= 0:
                    continue
                take = 0.0
                for ge, nm in gs:
                    if nm not in keep and ge.weight > 0:
                        d = ge.weight * k
                        ge.weight -= d
                        take += d
                if take > 0:
                    cur = next((ge.weight for ge, nm in gs if nm == foot), 0.0)
                    gfoot.add([int(i)], cur + take, "REPLACE")
                    moved += 1
    return moved


def _smooth01(x, e0, e1):
    t = min(max((x - e0) / (e1 - e0), 0.0), 1.0)
    return t * t * (3 - 2 * t)


def actor(name, info, location=(0, 0, 0), heading_deg=0.0, model_path=None):
    """Một diễn viên: mô hình 3D nếu có (info['mo_hinh_3d']), không thì người mẫu tạm theo chiều cao và màu áo."""
    if model_path and os.path.exists(model_path):
        arm = load_model(model_path, name)
    else:
        arm = build_armature(name, float(info.get("chieu_cao", REF_HEIGHT)))
        build_mannequin(arm, info.get("mau_tam"))
    # đặt chỗ đứng và hướng mặt (0° = nhìn về -Y)
    base = arm.matrix_world.copy()
    place = Matrix.Translation(Vector(location)) @ Matrix.Rotation(math.radians(heading_deg), 4, "Z")
    arm.matrix_world = place @ base
    arm["sgd_huong_do"] = float(heading_deg)
    bpy.context.view_layer.update()
    return arm
