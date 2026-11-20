"""Tạo các chuyển động mẫu (công thức) và lưu .bvh vào data/chuyen_dong/.

  blender --background --python tools/san-gach-do/tao_chuyen_dong_mau.py

Đây là bộ tạm để chạy thử toàn chuỗi. Chuyển động thật (Mixamo, quay động tác, AI) chỉ cần đặt file .bvh/.fbx
vào data/chuyen_dong/ và khai báo trong data/chuyen_dong.json — không sửa mã.

Cách viết động tác ở đây: mỗi khớp "xoay thêm W quanh trục thế giới, sau khi khớp cha đã xoay"
(D_con = W_con @ D_cha). Quy ước trục: mặt nhân vật nhìn về -Y, trái là +X, Z hướng lên.
  - khớp hướng lên (lưng, cổ, đầu): rotX(+) = cúi về trước
  - khớp hướng xuống (đùi, ống chân, tay buông): rotX(-) = đưa về trước
  - tay chữ T buông xuống: trái rotY(+75), phải rotY(-75)
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sgd_core import body  # noqa: E402

OUT = os.path.join(body.DATA, "chuyen_dong")
os.makedirs(OUT, exist_ok=True)


def rx(d):
    return Quaternion((1, 0, 0), math.radians(d))


def ry(d):
    return Quaternion((0, 1, 0), math.radians(d))


def rz(d):
    return Quaternion((0, 0, 1), math.radians(d))


I = Quaternion()


def smooth(u):
    u = min(max(u, 0.0), 1.0)
    return u * u * (3 - 2 * u)


def bell(t, a, b, c, d):
    """0 trước a, lên dần a->b, giữ b->c, xuống c->d."""
    if t <= a or t >= d:
        return 0.0
    if t < b:
        return smooth((t - a) / (b - a))
    if t <= c:
        return 1.0
    return 1.0 - smooth((t - c) / (d - c))


def arms_down(W, l_extra=I, r_extra=I, fl=None, fr=None):
    """Tay buông; cẳng tay mặc định hơi gập 12°. fl/fr: độ gập THÊM của cẳng tay (cộng vào 12°, không thay thế,
    để động tác nào cũng bắt đầu/kết thúc ở cùng một tư thế tay -> nối không giật)."""
    W["upper_arm.L"] = l_extra @ ry(75)
    W["upper_arm.R"] = r_extra @ ry(-75)
    W["forearm.L"] = (fl if fl is not None else I) @ rx(-12)
    W["forearm.R"] = (fr if fr is not None else I) @ rx(-12)


def cumulative(W):
    """D_con = W_con @ D_cha theo thứ tự cha trước con."""
    D = {}
    for n in body.STD_NAMES:
        p = body.STD_PARENT[n]
        D[n] = W.get(n, I) @ (D[p] if p else I)
    return D


# ---------- các động tác: hàm(t giây) -> (W, hip_offset mét theo người 1,70 m)
def dung_tho(t):
    T = 4.0
    ph = 2 * math.pi * t / T
    W = {}
    arms_down(W)
    W["chest"] = rx(-1.2 * math.sin(ph))          # hít vào ngực hơi ưỡn
    W["shoulder.L"] = ry(-1.5 * max(0, math.sin(ph)))
    W["shoulder.R"] = ry(1.5 * max(0, math.sin(ph)))
    W["head"] = rz(2.0 * math.sin(ph / 2)) @ rx(1.0 * math.sin(ph))
    W["hips"] = rz(1.0 * math.sin(ph / 2))
    return W, Vector((0.012 * math.sin(ph / 2), 0, 0.004 * math.sin(ph)))


# ---- bước đi: dựng theo quỹ đạo bàn chân (chân trụ đứng yên trên đất, gót chạm -> đặt phẳng -> nhón -> bước),
# gối tính bằng IK; hông hạ vừa đủ để chân không duỗi quá -> chuyển động khớp mặt đất ngay từ nguồn.
WALK_T = 25 / 24          # giây / chu kỳ (2 bước) ~ 115 bước/phút, đúng số nguyên khung để lặp khít
WALK_V = 1.22             # m/giây -> bước ~0,64 m (người lớn đi thường)
WALK_D = 0.6              # tỉ lệ thời gian chân trụ
_ANK, _HEEL, _TOEJ = Vector((0, 0, 0.10)), Vector((0, 0.05, 0.02)), Vector((0, -0.14, 0.02))
_LEG = 0.85 * 0.998       # đùi + cẳng chân (chuẩn): lúc trụ gối gần thẳng (~5°) như người thật


def _mj(u):
    u = min(max(u, 0.0), 1.0)
    return u ** 3 * (10 - 15 * u + 6 * u * u)


def _stance(s, yh, x):
    """Chân trụ ở tiến độ s (0 gót chạm .. 1 nhấc mũi): (cổ chân, góc chúc mũi chân độ)."""
    heel = Vector((x, yh, 0.02))
    toe = heel + (_TOEJ - _HEEL)
    if s < 0.12:
        p = -12 * (1 - smooth(s / 0.12))
        return heel + rx(p) @ (_ANK - _HEEL), p
    if s < 0.55:
        return heel + (_ANK - _HEEL), 0.0
    u = (s - 0.55) / 0.45
    p = 40 * u * u                             # nhón dần nhanh lên: lúc nhấc mũi chân vẫn đang đẩy (có đà)
    return toe + rx(p) @ (_ANK - _TOEJ), p


def _foot(t, phase0, y0, x):
    stride = WALK_V * WALK_T
    ph = t / WALK_T + phase0
    c = math.floor(ph)
    f = ph - c
    yh = y0 - c * stride
    if f < WALK_D:
        return _stance(f / WALK_D, yh, x)
    s = (f - WALK_D) / (1 - WALK_D)
    a0, p0 = _stance(1.0, yh, x)
    a1, p1 = _stance(0.0, yh - stride, x)
    # nối liền với lúc nhón: vận tốc đầu = vận tốc cổ chân/góc nhón ngay trước khi nhấc (đổi sang thang thời gian bước)
    e = 1e-3
    ae, pe = _stance(1.0 - e, yh, x)
    k = WALK_D / (1 - WALK_D)
    v0, w0 = (a0 - ae) / e * k, (p0 - pe) / e * k
    v1 = Vector(((a1 - a0).x * 0.2, (a1 - a0).y * 0.2, -0.05))     # đặt gót: chậm lại, hơi hạ xuống
    h00, h10, h01, h11 = 2 * s ** 3 - 3 * s * s + 1, s ** 3 - 2 * s * s + s, -2 * s ** 3 + 3 * s * s, s ** 3 - s * s
    a = a0 * h00 + v0 * h10 + a1 * h01 + v1 * h11
    a.z += 0.035 * math.sin(math.pi * s) ** 2                       # chừa khoảng hở mũi chân
    p = p0 * h00 + w0 * h10 + p1 * h01 + 0.0 * h11
    return a, p


_FEET = (("L", 0.0, -0.28, 0.09), ("R", 0.5, -0.28 + WALK_V * WALK_T / 2, -0.09))


def _walk_hip_z():
    n = int(round(WALK_T * 24))
    need = []
    for i in range(n):
        t = i / 24
        hy = -WALK_V * t
        z = 0.95
        for _, ph, y0, x in _FEET:
            f = (t / WALK_T + ph) % 1.0
            if f >= WALK_D:
                continue                      # chân đang bước trên không: tự gập gối, không giữ hông
            # chân trụ: đặt phẳng thì gối gần thẳng; lúc nhón mũi chân để bước, gối gập dần (~35° lúc nhấc)
            st = f / WALK_D
            leg = _LEG if st < 0.55 else _LEG - (_LEG - 0.85 * 0.955) * smooth((st - 0.55) / 0.45)
            a, _ = _foot(t, ph, y0, x)
            dx = math.hypot(a.x - x, a.y - hy)
            z = min(z, a.z + math.sqrt(max(leg * leg - dx * dx, 0.0)))
        need.append(z)
    # bao dưới rồi làm mượt (vòng tròn, vì lặp)
    # bao dưới (bề rộng 5 khung) rồi làm mượt rộng (σ 2,5 khung): hông đi như con lắc, không gãy khi đổi chân tựa
    low = [min(need[(i + k) % n] for k in range(-0, 1)) for i in range(n)]
    ker = [math.exp(-0.5 * (k / 2.0) ** 2) for k in range(-7, 8)]
    out = [sum(kv * low[(i + k) % n] for k, kv in zip(range(-7, 8), ker)) / sum(ker) for i in range(n)]
    return [o - 0.002 for o in out]


_HIPZ = None


def di(t):
    global _HIPZ
    if _HIPZ is None:
        _HIPZ = _walk_hip_z()
    n = len(_HIPZ)
    fi = t * 24
    i0 = int(math.floor(fi)) % n
    a = fi - math.floor(fi)
    z = _HIPZ[i0] * (1 - a) + _HIPZ[(i0 + 1) % n] * a
    ph = 2 * math.pi * t / WALK_T
    c = math.cos(ph)
    W = {}
    W["hips"] = rz(-5 * c)                         # hông xoay theo chân bước
    W["spine"] = rz(7 * c) @ rx(3)                 # vai xoay ngược
    W["chest"] = rx(1)
    W["head"] = rz(-2 * c) @ rx(-3)
    arms_down(W, rx(18 * c), rx(-18 * c), rx(-3 - 12 * max(0, c)), rx(-3 - 12 * max(0, -c)))
    feet = {side: _foot(t, ph0, y0, x) for side, ph0, y0, x in _FEET}
    sway = 0.018 * math.cos(ph - 2 * math.pi * 0.3)
    return W, Vector((sway, -WALK_V * t, z - 0.95)), feet


def ngoi_xuong(t):
    """Ngồi xuống ghế/bậc cao ~0,5 m: bàn chân ghim nguyên chỗ, hông lùi + hạ, người đổ về trước để giữ thăng bằng."""
    u = smooth(t / 1.4)
    W = {}
    arms_down(W, rx(-28 * u), rx(-28 * u), rx(-36 * u), rx(-36 * u))
    W["spine"] = rx(22 * math.sin(math.pi * u) + 4 * u)
    W["head"] = rx(-6 * math.sin(math.pi * u))
    feet = {"L": (Vector((0.09, 0, 0.10)), 0.0), "R": (Vector((-0.09, 0, 0.10)), 0.0)}
    return W, Vector((0, 0.40 * u, -0.43 * u)), feet


def dung_day(t):
    return ngoi_xuong(1.4 - t)


def dua_tien(t):
    b = bell(t, 0.1, 0.6, 1.2, 1.8)
    W = {}
    arms_down(W, I, rx(-62 * b), None, rx(-28 * b))
    W["chest"] = rx(9 * b)
    W["neck"] = rx(6 * b)
    W["head"] = rx(6 * b)
    return W, Vector((0, -0.02 * b, 0))


def vay_tay(t):
    b = bell(t, 0.0, 0.7, 1.5, 2.2)
    W = {}
    arms_down(W)
    wave = 18 * math.sin(2 * math.pi * 2.2 * (t - 0.7)) * bell(t, 0.5, 0.8, 1.4, 1.7)
    W["upper_arm.R"] = ry(-75 + 140 * b)
    W["forearm.R"] = ry(45 * b + wave) @ rx(-12 * (1 - b))
    W["head"] = rz(-6 * b)
    return W, Vector((0, 0, 0))


def gat_dau(t):
    b = bell(t, 0.0, 0.3, 0.45, 0.9)
    W = {}
    arms_down(W)
    W["neck"] = rx(9 * b)
    W["head"] = rx(12 * b)
    return W, Vector((0, 0, 0))


def cui_chao(t):
    b = bell(t, 0.0, 0.6, 1.2, 2.0)
    W = {}
    arms_down(W)
    W["spine"] = rx(22 * b)
    W["chest"] = rx(10 * b)
    W["neck"] = rx(8 * b)
    return W, Vector((0, 0.05 * b, -0.02 * b))


MOTIONS = {
    # tên: (hàm, số giây, lặp)
    "dung_tho": (dung_tho, 4.0, True),
    "di": (di, WALK_T, True),
    "ngoi_xuong": (ngoi_xuong, 1.4, False),
    "dung_day": (dung_day, 1.4, False),
    "dua_tien": (dua_tien, 1.8, False),
    "vay_tay": (vay_tay, 2.2, False),
    "gat_dau": (gat_dau, 0.9, False),
    "cui_chao": (cui_chao, 2.0, False),
}


def author(name, fn, seconds):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.fps = body.FPS
    n = int(round(seconds * body.FPS))
    scene.frame_start, scene.frame_end = 0, n
    arm = body.build_armature("Mau")
    std_of = body.bone_map(arm)
    rest = body._world_rest(arm)
    inv = arm.matrix_world.inverted()
    scl = Matrix.Identity(4)
    for f in range(0, n + 1):
        out = fn(f / body.FPS)
        W, hip = out[0], out[1]
        feet = out[2] if len(out) > 2 else None
        D = cumulative(W)
        scene.frame_set(f)
        body.apply_pose(arm, std_of, rest, D, hip / body.REF_HIP, body.REF_HIP, Vector((0, 0, 0)))
        if feet:
            for side, (ankle, pitch) in feet.items():
                pbs = [arm.pose.bones[f"{b}.{side}"] for b in ("thigh", "shin", "foot")]
                body._leg_ik(arm, pbs, ankle, Vector((0, -1, 0)), inv, scl, use_fk=False)
                pf, pt = arm.pose.bones[f"foot.{side}"], arm.pose.bones[f"toe.{side}"]
                rf = rest[f"foot.{side}"].to_quaternion()
                rt = rest[f"toe.{side}"].to_quaternion()
                body._set_world(arm, pf, body._world(arm, pf)[0], rx(pitch) @ rf, inv, scl)
                body._set_world(arm, pt, body._world(arm, pt)[0], rx(min(pitch, 0.0)) @ rt, inv, scl)
        for pb in arm.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=f)
            pb.keyframe_insert("location", frame=f)
    bpy.context.view_layer.objects.active = arm
    arm.select_set(True)
    path = os.path.join(OUT, f"{name}.bvh")
    bpy.ops.export_anim.bvh(filepath=path, frame_start=0, frame_end=n, rotate_mode="NATIVE", root_transform_only=False)
    print("SGD_MOTION", name, n + 1, "khung", path)


only = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
for name, (fn, sec, loop) in MOTIONS.items():
    if only and name not in only:
        continue
    author(name, fn, sec)
