"""Bối cảnh dựng bằng hình khối + vật liệu thủ tục (không cần file ngoài, không vướng bản quyền).

"bo_de": bờ đê làng quê buổi sáng. Mặt đê chạy dọc trục X, giữa ở y = 0, mặt đê cao z = 0 (diễn viên đứng trên đó).
  - phía +Y: mái đê cỏ -> ruộng lúa nước -> lũy tre làng xa;
  - phía -Y: mái đê cỏ -> bãi cỏ -> sông -> bờ bên kia có hàng cây;
  - cột điện gỗ dọc mép đê, vài bụi tre / cây gần để làm tiền cảnh;
  - trời sáng: nắng sớm xiên ấm, trời xanh nhạt về chân trời; xa thì mờ dần như hơi sương (theo khoảng cách máy).
Tuỳ chọn (dict): {"kieu": "bo_de", "gio": "sang"|"chieu", "hat_giong": 7, "mat_de_rong": 4.5}
"""
import math
import random

import bpy
import bmesh
from mathutils import Euler, Matrix, Vector

HAZE = (0.70, 0.78, 0.87)


# ---------------------------------------------------------------- vật liệu
def _mat(name, build):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    col = build(nt, bsdf)
    # hơi sương theo khoảng cách máy: trộn về màu trời ở xa
    cam = nt.nodes.new("ShaderNodeCameraData")
    k = nt.nodes.new("ShaderNodeMath")
    k.operation = "MULTIPLY"
    k.inputs[1].default_value = -1.0 / 260.0
    nt.links.new(cam.outputs["View Distance"], k.inputs[0])
    e = nt.nodes.new("ShaderNodeMath")
    e.operation = "EXPONENT"
    nt.links.new(k.outputs[0], e.inputs[0])
    inv = nt.nodes.new("ShaderNodeMath")
    inv.operation = "SUBTRACT"
    inv.inputs[0].default_value = 1.0
    nt.links.new(e.outputs[0], inv.inputs[1])
    cl = nt.nodes.new("ShaderNodeMath")
    cl.operation = "MULTIPLY"
    cl.inputs[1].default_value = 0.70
    nt.links.new(inv.outputs[0], cl.inputs[0])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    nt.links.new(cl.outputs[0], mix.inputs["Factor"])
    if isinstance(col, tuple):
        mix.inputs[6].default_value = (*col[:3], 1)
    else:
        nt.links.new(col, mix.inputs[6])
    mix.inputs[7].default_value = (*HAZE, 1)
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    return m


def _noise_col(nt, c1, c2, scale, detail=4.0, coord="Object", lo=0.35, hi=0.65):
    tc = nt.nodes.new("ShaderNodeTexCoord")
    nz = nt.nodes.new("ShaderNodeTexNoise")
    nz.inputs["Scale"].default_value = scale
    nz.inputs["Detail"].default_value = detail
    nt.links.new(tc.outputs[coord], nz.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position = lo
    ramp.color_ramp.elements[0].color = (*c1, 1)
    ramp.color_ramp.elements[1].position = hi
    ramp.color_ramp.elements[1].color = (*c2, 1)
    nt.links.new(nz.outputs["Fac"], ramp.inputs[0])
    return ramp.outputs[0], tc, nz


def _bump(nt, bsdf, height_socket, strength=0.3, distance=0.05):
    b = nt.nodes.new("ShaderNodeBump")
    b.inputs["Strength"].default_value = strength
    b.inputs["Distance"].default_value = distance
    nt.links.new(height_socket, b.inputs["Height"])
    nt.links.new(b.outputs[0], bsdf.inputs["Normal"])


def mat_road():
    """Mặt đê đất nện: nâu đất, hai vệt bánh xe sáng/mịn hơn, mép cỏ lấn vào."""
    def build(nt, bsdf):
        dirt, tc, nz = _noise_col(nt, (0.36, 0.22, 0.12), (0.52, 0.36, 0.22), 6.0, 8.0)
        bsdf.inputs["Roughness"].default_value = 0.95
        _bump(nt, bsdf, nz.outputs["Fac"], 0.35, 0.03)
        sep = nt.nodes.new("ShaderNodeSeparateXYZ")
        nt.links.new(tc.outputs["Object"], sep.inputs[0])
        ay = nt.nodes.new("ShaderNodeMath")
        ay.operation = "ABSOLUTE"
        nt.links.new(sep.outputs[1], ay.inputs[0])
        # vệt bánh xe quanh |y| = 0.75
        d = nt.nodes.new("ShaderNodeMath")
        d.operation = "SUBTRACT"
        d.inputs[1].default_value = 0.75
        nt.links.new(ay.outputs[0], d.inputs[0])
        ad = nt.nodes.new("ShaderNodeMath")
        ad.operation = "ABSOLUTE"
        nt.links.new(d.outputs[0], ad.inputs[0])
        rut = nt.nodes.new("ShaderNodeMapRange")
        rut.inputs[1].default_value = 0.08
        rut.inputs[2].default_value = 0.22
        rut.inputs[3].default_value = 0.55
        rut.inputs[4].default_value = 0.0
        nt.links.new(ad.outputs[0], rut.inputs[0])
        m1 = nt.nodes.new("ShaderNodeMix")
        m1.data_type = "RGBA"
        nt.links.new(rut.outputs[0], m1.inputs["Factor"])
        nt.links.new(dirt, m1.inputs[6])
        m1.inputs[7].default_value = (0.60, 0.46, 0.32, 1)
        # mép cỏ: |y| > 1.7 (lởm chởm theo noise)
        nz2 = nt.nodes.new("ShaderNodeTexNoise")
        nz2.inputs["Scale"].default_value = 3.0
        nt.links.new(tc.outputs["Object"], nz2.inputs["Vector"])
        ed = nt.nodes.new("ShaderNodeMath")
        ed.operation = "MULTIPLY_ADD"
        ed.inputs[1].default_value = 0.6
        nt.links.new(nz2.outputs["Fac"], ed.inputs[0])
        nt.links.new(ay.outputs[0], ed.inputs[2])
        gm = nt.nodes.new("ShaderNodeMapRange")
        gm.inputs[1].default_value = 1.95
        gm.inputs[2].default_value = 2.15
        nt.links.new(ed.outputs[0], gm.inputs[0])
        grass, _, _ = _noise_col(nt, (0.16, 0.26, 0.07), (0.30, 0.38, 0.12), 14.0)
        m2 = nt.nodes.new("ShaderNodeMix")
        m2.data_type = "RGBA"
        nt.links.new(gm.outputs[0], m2.inputs["Factor"])
        nt.links.new(m1.outputs[2], m2.inputs[6])
        nt.links.new(grass, m2.inputs[7])
        return m2.outputs[2]
    return _mat("BC_MatDe", build)


def mat_grass(name="BC_Co", c1=(0.14, 0.24, 0.06), c2=(0.30, 0.40, 0.12), scale=9.0):
    def build(nt, bsdf):
        col, tc, nz = _noise_col(nt, c1, c2, scale, 6.0)
        bsdf.inputs["Roughness"].default_value = 0.9
        _bump(nt, bsdf, nz.outputs["Fac"], 0.5, 0.05)
        return col
    return _mat(name, build)


def mat_paddy():
    """Ruộng lúa nước: hàng mạ xanh xen mặt nước phản trời."""
    def build(nt, bsdf):
        tc = nt.nodes.new("ShaderNodeTexCoord")
        wv = nt.nodes.new("ShaderNodeTexWave")
        wv.wave_type = "BANDS"
        wv.bands_direction = "X"
        wv.inputs["Scale"].default_value = 3.5
        wv.inputs["Distortion"].default_value = 0.6
        nt.links.new(tc.outputs["Object"], wv.inputs["Vector"])
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 0.25
        nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
        mx = nt.nodes.new("ShaderNodeMath")
        mx.operation = "MULTIPLY_ADD"
        mx.inputs[1].default_value = 0.5
        nt.links.new(wv.outputs["Fac"], mx.inputs[0])
        nt.links.new(nz.outputs["Fac"], mx.inputs[2])
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.45
        ramp.color_ramp.elements[0].color = (0.30, 0.38, 0.30, 1)     # nước phản trời
        ramp.color_ramp.elements[1].position = 0.62
        ramp.color_ramp.elements[1].color = (0.22, 0.42, 0.08, 1)     # mạ
        nt.links.new(mx.outputs[0], ramp.inputs[0])
        rr = nt.nodes.new("ShaderNodeMapRange")
        rr.inputs[3].default_value = 0.15
        rr.inputs[4].default_value = 0.85
        nt.links.new(mx.outputs[0], rr.inputs[0])
        nt.links.new(rr.outputs[0], bsdf.inputs["Roughness"])
        return ramp.outputs[0]
    return _mat("BC_Ruong", build)


def mat_water():
    def build(nt, bsdf):
        bsdf.inputs["Roughness"].default_value = 0.08
        tc = nt.nodes.new("ShaderNodeTexCoord")
        nz = nt.nodes.new("ShaderNodeTexNoise")
        nz.inputs["Scale"].default_value = 1.5
        nz.inputs["Detail"].default_value = 3
        nt.links.new(tc.outputs["Object"], nz.inputs["Vector"])
        _bump(nt, bsdf, nz.outputs["Fac"], 0.15, 0.2)
        return (0.20, 0.27, 0.27)
    return _mat("BC_Song", build)


def mat_plain(name, col, rough=0.85):
    def build(nt, bsdf):
        bsdf.inputs["Roughness"].default_value = rough
        return col
    return _mat(name, build)


def mat_leaf(name, c1, c2):
    def build(nt, bsdf):
        col, tc, nz = _noise_col(nt, c1, c2, 3.0, 5.0)
        bsdf.inputs["Roughness"].default_value = 0.8
        _bump(nt, bsdf, nz.outputs["Fac"], 0.8, 0.3)
        return col
    return _mat(name, build)


# ---------------------------------------------------------------- hình khối
def _obj_from_bm(name, bm, mat):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    me.materials.append(mat)
    for p in me.polygons:
        p.use_smooth = True
    return ob


def _strip(name, ys, zs, x0, x1, nx, mat, jitter=0.0, rnd=None):
    """Dải mặt cắt ngang (ys, zs) kéo dọc X từ x0 tới x1 (nx đoạn)."""
    bm = bmesh.new()
    rows = []
    for i in range(nx + 1):
        x = x0 + (x1 - x0) * i / nx
        row = []
        for y, z in zip(ys, zs):
            dz = (rnd.uniform(-jitter, jitter) if (rnd and jitter) else 0.0)
            row.append(bm.verts.new((x, y, z + dz)))
        rows.append(row)
    for i in range(nx):
        for j in range(len(ys) - 1):
            bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
    return _obj_from_bm(name, bm, mat)


class _Batch:
    """Gộp nhiều hình khối vào một lưới cho mỗi vật liệu (dựng nhanh, render nhẹ)."""
    def __init__(self):
        self.bms = {}

    def _bm(self, mat):
        if mat.name not in self.bms:
            self.bms[mat.name] = (bmesh.new(), mat)
        return self.bms[mat.name][0]

    def ico(self, mat, loc, radius, scale=(1, 1, 1), rot=(0, 0, 0), sub=2):
        m = Matrix.Translation(Vector(loc)) @ Euler(rot).to_matrix().to_4x4() @ Matrix.Diagonal((*scale, 1))
        bmesh.ops.create_icosphere(self._bm(mat), subdivisions=sub, radius=radius, matrix=m)

    def cyl(self, mat, a, b, r1, r2=None, seg=6):
        a, b = Vector(a), Vector(b)
        d = b - a
        q = d.to_track_quat("Z", "Y")
        m = Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4()
        bmesh.ops.create_cone(self._bm(mat), cap_ends=True, segments=seg, radius1=r1,
                              radius2=r1 if r2 is None else r2, depth=d.length, matrix=m)

    def box(self, mat, loc, size):
        m = Matrix.Translation(Vector(loc)) @ Matrix.Diagonal((*size, 1))
        bmesh.ops.create_cube(self._bm(mat), size=1.0, matrix=m)

    def finish(self, prefix):
        out = []
        for name, (bm, mat) in self.bms.items():
            ob = _obj_from_bm(f"{prefix}_{name}", bm, mat)
            out.append(ob)
        self.bms = {}
        return out


def _tree(B, loc, h, rnd, leaf_mats, trunk_mat, kind="tron"):
    x, y, z = loc
    if kind == "tre":                                   # bụi tre: thân mảnh vươn xoè, lá thành chùm nhỏ rủ ra ngoài
        for _ in range(rnd.randint(8, 12)):
            a = rnd.uniform(0, 2 * math.pi)
            r = rnd.uniform(0, 0.5)
            hh = h * rnd.uniform(0.8, 1.05)
            tilt = rnd.uniform(0.08, 0.30)
            base = Vector((x + r * math.cos(a), y + r * math.sin(a), z))
            out_ = Vector((math.cos(a), math.sin(a), 0))
            axis = (out_ * math.sin(tilt) + Vector((0, 0, math.cos(tilt)))).normalized()
            B.cyl(trunk_mat, base, base + axis * hh, 0.04, 0.025)
            for _k in range(rnd.randint(6, 9)):
                t = rnd.uniform(0.45, 1.0)
                p_ = base + axis * hh * t + out_ * rnd.uniform(0.2, 0.9) * t - Vector((0, 0, 0.25 * t))
                p_ += Vector((rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 0))
                B.ico(rnd.choice(leaf_mats), p_, rnd.uniform(0.32, 0.5), (1.7, 0.8, 0.38),
                      (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.4, 0.4), a + rnd.uniform(-0.5, 0.5)), sub=1)
        return
    # cây tán tròn
    B.cyl(trunk_mat, (x, y, z), (x, y, z + h * 0.6), 0.10 * h / 6, 0.06 * h / 6, seg=8)
    for _ in range(rnd.randint(4, 7)):
        r = h * rnd.uniform(0.18, 0.28)
        off = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.4, 0.6))) * h * 0.16
        B.ico(rnd.choice(leaf_mats), (x + off.x, y + off.y, z + h * 0.68 + off.z), r, (1, 1, 0.85))


def _pole(B, loc, mat):
    x, y, z = loc
    B.cyl(mat, (x, y, z), (x, y, z + 7.5), 0.10, 0.08, seg=8)
    B.box(mat, (x, y, z + 7.0), (0.06, 1.1, 0.06))


def _wires(xs, y, z_top, mat):
    """Dây điện võng giữa các cột."""
    out = []
    for dy in (-0.45, 0.45):
        for x0, x1 in zip(xs[:-1], xs[1:]):
            cu = bpy.data.curves.new("BC_Day", "CURVE")
            cu.dimensions = "3D"
            cu.bevel_depth = 0.008
            sp = cu.splines.new("POLY")
            n = 12
            sp.points.add(n)
            for i in range(n + 1):
                t = i / n
                sag = 0.55 * 4 * t * (1 - t)
                sp.points[i].co = (x0 + (x1 - x0) * t, y + dy, z_top - sag, 1)
            ob = bpy.data.objects.new("BC_Day", cu)
            bpy.context.scene.collection.objects.link(ob)
            cu.materials.append(mat)
            out.append(ob)
    return out


# ---------------------------------------------------------------- trời + nắng
def sky_and_sun(scene, gio="sang"):
    w = bpy.data.worlds.new("BC_Troi")
    w.use_nodes = True
    nt = w.node_tree
    for n in list(nt.nodes):
        nt.nodes.remove(n)
    out = nt.nodes.new("ShaderNodeOutputWorld")
    bg = nt.nodes.new("ShaderNodeBackground")
    tc = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(tc.outputs["Generated"], sep.inputs[0])
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    els = ramp.color_ramp.elements
    if gio == "chieu":
        els[0].color = (0.95, 0.78, 0.55, 1)
        els[1].color = (0.42, 0.55, 0.78, 1)
    else:
        els[0].color = (0.88, 0.84, 0.76, 1)          # chân trời sớm hơi ấm
        els[1].color = (0.28, 0.48, 0.78, 1)
    els[0].position = 0.0
    els[1].position = 0.45
    nt.links.new(sep.outputs[2], ramp.inputs[0])
    nt.links.new(ramp.outputs[0], bg.inputs[0])
    bg.inputs[1].default_value = 0.75                    # bớt sáng tràn -> mặt có khối, có chiều sâu
    nt.links.new(bg.outputs[0], out.inputs[0])
    scene.world = w
    sun_d = bpy.data.lights.new("BC_Nang", "SUN")
    sun_d.energy = 4.6 if gio != "chieu" else 3.6
    sun_d.color = (1.0, 0.86, 0.68) if gio != "chieu" else (1.0, 0.76, 0.52)
    sun_d.angle = math.radians(2.5)
    sun = bpy.data.objects.new("BC_Nang", sun_d)
    scene.collection.objects.link(sun)
    # nắng sớm thấp, chiếu từ phía trước-trái máy (phía -Y, -X) để mặt diễn viên có sáng tối
    sun.rotation_euler = (math.radians(62), 0, math.radians(-38))
    # đèn hắt nhẹ phía sau (viền tóc, vai) — tách người khỏi nền
    rim_d = bpy.data.lights.new("BC_Vien", "SUN")
    rim_d.energy = 1.2
    rim_d.color = (1.0, 0.95, 0.88)
    rim = bpy.data.objects.new("BC_Vien", rim_d)
    scene.collection.objects.link(rim)
    rim.rotation_euler = (math.radians(70), 0, math.radians(160))
    return sun


# ---------------------------------------------------------------- dựng
def build(scene, opts=None):
    opts = dict(opts or {})
    kieu = opts.get("kieu", "bo_de")
    if kieu != "bo_de":
        raise ValueError(f"Chưa có bối cảnh '{kieu}' (hiện có: bo_de)")
    rnd = random.Random(int(opts.get("hat_giong", 7)))
    half = float(opts.get("mat_de_rong", 4.5)) / 2
    L = 160.0
    sky_and_sun(scene, opts.get("gio", "sang"))
    # màu: Standard (giữ màu tươi như ảnh gốc) + tương phản vừa-cao (bớt phẳng); AgX làm xám trời -> không dùng
    try:
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "Medium High Contrast"
    except TypeError:
        pass

    road = mat_road()
    grass = mat_grass()
    grass_far = mat_grass("BC_CoXa", (0.20, 0.30, 0.09), (0.33, 0.42, 0.15), 2.0)
    paddy = mat_paddy()
    water = mat_water()
    trunk = mat_plain("BC_Than", (0.20, 0.15, 0.10))
    bamboo = mat_plain("BC_Tre", (0.38, 0.42, 0.18), 0.6)
    pole = mat_plain("BC_Cot", (0.30, 0.25, 0.20))
    wire = mat_plain("BC_DayDien", (0.05, 0.05, 0.05), 0.5)
    leaves = [mat_leaf("BC_La1", (0.10, 0.20, 0.05), (0.20, 0.32, 0.08)),
              mat_leaf("BC_La2", (0.14, 0.24, 0.06), (0.26, 0.36, 0.10)),
              mat_leaf("BC_La3", (0.18, 0.26, 0.07), (0.32, 0.40, 0.12))]

    # mặt đê (hơi khum giữa) + hai mái đê + chân đê
    ys = [-half - 0.15, -half * 0.6, 0.0, half * 0.6, half + 0.15]
    zs = [-0.03, 0.0, 0.02, 0.0, -0.03]
    _strip("BC_MatDe", ys, zs, -L, L, 160, road, 0.01, rnd)
    for sgn in (1, -1):
        yy = [sgn * (half + 0.15), sgn * (half + 1.6), sgn * (half + 3.2), sgn * (half + 4.5)]
        zz = [-0.03, -1.05, -2.05, -2.25]
        if sgn < 0:
            yy, zz = yy[::-1], zz[::-1]
        _strip("BC_MaiDe", yy, zz, -L, L, 120, grass, 0.04, rnd)
    # +Y: bờ ruộng rồi ruộng lúa
    _strip("BC_BoRuong", [half + 4.5, half + 6.0], [-2.25, -2.3], -L, L, 40, grass, 0.03, rnd)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, half + 6.0 + 60, -2.32))
    pd = bpy.context.active_object
    pd.name = "BC_Ruong"
    pd.scale = (2 * L, 120, 1)
    pd.data.materials.append(paddy)
    # bờ ruộng ngang chia ô
    for yy in (half + 22, half + 45, half + 75):
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, yy, -2.25))
        b = bpy.context.active_object
        b.scale = (2 * L, 0.5, 0.18)
        b.data.materials.append(grass_far)
    # -Y: bãi cỏ, sông, bờ bên kia
    _strip("BC_Bai", [-(half + 14), -(half + 4.5)], [-2.45, -2.25], -L, L, 40, grass, 0.05, rnd)
    bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -(half + 14 + 35), -2.6))
    rv = bpy.context.active_object
    rv.name = "BC_Song"
    rv.scale = (2 * L, 70, 1)
    rv.data.materials.append(water)
    _strip("BC_BoXa", [-(half + 200), -(half + 84)], [-2.3, -2.4], -L, L, 10, grass_far)

    # cây: lũy tre làng xa phía ruộng, hàng cây bờ sông bên kia, vài bụi gần làm tiền cảnh
    B = _Batch()
    for _ in range(60):
        x = rnd.uniform(-L, L)
        y = rnd.uniform(half + 95, half + 120)
        k_ = "tre" if rnd.random() < 0.6 else "tron"
        _tree(B, (x, y, -2.3), rnd.uniform(7, 12), rnd, leaves, bamboo if k_ == "tre" else trunk, k_)
    for _ in range(40):
        x = rnd.uniform(-L, L)
        y = -(half + rnd.uniform(86, 100))
        _tree(B, (x, y, -2.4), rnd.uniform(6, 10), rnd, leaves, trunk, "tron")
    near = opts.get("cay_gan", [(-9.0, half + 5.2, "tre"), (7.5, half + 5.4, "tron"), (-17.0, half + 5.0, "tron"),
                                 (15.0, -(half + 6.0), "tre")])
    for x, y, k_ in near:
        _tree(B, (x, y, -2.25), 7.5 if k_ == "tre" else 6.0, rnd, leaves, bamboo if k_ == "tre" else trunk, k_)
    # cột điện dọc mép đê phía ruộng
    xs = [x for x in range(-150, 151, 30)]
    for x in xs:
        _pole(B, (x + 4.0, half + 0.6, -0.4), pole)
    B.finish("BC")
    _wires([x + 4.0 for x in xs], half + 0.6, -0.4 + 7.0, wire)
    return {"mat_de_z": 0.0, "mat_de_nua_rong": half}
