"""Lớp dựng mặt: lưới mặt dán ảnh mẫu, mí mắt, miệng nói, lông mày — mỗi phần là dải da trùng ảnh mẫu
(lúc nghỉ không lộ mép), cử động bằng shape key.

Gọi build(sheet_path, face) trong Blender, nhận về FaceRig có các hàm đặt keyframe.
face: số đo từ measure.load_or_measure (eyes / mouth / brows).
"""
import math

import bpy
import numpy as np

from .measure import EYE_KNOTS

LID_SEG_S = 32
LID_SEG_T = 10
LID_KNOTS = EYE_KNOTS
M_SEG_S = 40
M_SEG_T = 12
B_SEG_S = 30
B_SEG_T = 14

# Ô mặt chính trên ảnh mẫu 6 ô (ô trên-trái), theo toạ độ UV của ảnh.
PANEL_U0, PANEL_U1 = 0.006, 1.0 / 3.0 - 0.006
PANEL_V0, PANEL_V1 = 0.506, 0.994

# Các biến dưới đây được build() gán; các hàm dựng phần mặt dùng chúng.
scene = mat = lid_mat = bust = img = None
MOUTH = None
FACE_MID = 0.0


def sheet_uv(local_x, local_y):
    u = (local_x + 1.0) * 0.5
    v = (local_y + 1.0) * 0.5
    return (PANEL_U0 + u * (PANEL_U1 - PANEL_U0), PANEL_V0 + v * (PANEL_V1 - PANEL_V0))


def sheet_rgb(image):
    w, h = image.size
    px = np.array(image.pixels[:], dtype=np.float32).reshape(h, w, 4)
    return (np.flipud(px[..., :3]) * 255.0).round()


# ---------------- Mí mắt ----------------
def knot_y(table, s):
    """Nội suy Hermite bậc ba qua các điểm đo, để viền mi cong trơn, không gãy khúc."""
    n = len(LID_KNOTS)

    def slope(k):
        lo, hi = max(k - 1, 0), min(k + 1, n - 1)
        return (table[hi] - table[lo]) / (LID_KNOTS[hi] - LID_KNOTS[lo])

    for k in range(n - 1):
        s0, s1 = LID_KNOTS[k], LID_KNOTS[k + 1]
        if s <= s1 or k == n - 2:
            h = s1 - s0
            f = min(max((s - s0) / h, 0.0), 1.0)
            h00 = 2 * f ** 3 - 3 * f ** 2 + 1
            h10 = f ** 3 - 2 * f ** 2 + f
            h01 = -2 * f ** 3 + 3 * f ** 2
            h11 = f ** 3 - f ** 2
            return h00 * table[k] + h10 * h * slope(k) + h01 * table[k + 1] + h11 * h * slope(k + 1)
    return table[-1]



def make_lid(name, cx, half_w, eye_top, eye_bottom, crease_h):
    """eye_top / eye_bottom: mép trên (đỉnh hàng mi) và mép dưới khe mắt đo trên Bo_mat, theo LID_KNOTS."""
    # Khoé thật: chỗ mép trên và mép dưới khe mắt gặp nhau, tìm từ giữa ra hai bên.
    def opening(s):
        return knot_y(eye_top, s) - knot_y(eye_bottom, s)

    def find_corner(step):
        s = 0.0
        while abs(s + step) <= 1.0 and opening(s + step) > 0.002:
            s += step
        return s + step if abs(s + step) <= 1.0 else s

    s_in, s_out = find_corner(-0.01), find_corner(0.01)
    y_in = 0.5 * (knot_y(eye_top, s_in) + knot_y(eye_bottom, s_in))
    y_out = 0.5 * (knot_y(eye_top, s_out) + knot_y(eye_bottom, s_out))
    mid, half = 0.5 * (s_in + s_out), 0.5 * (s_out - s_in)

    # Viền mi khi khép là một cung elip trơn nối hai khoé thật, hạ đủ sâu để phủ cả mép ướt mi dưới
    # (dải hồng nhạt dưới lòng trắng) ở mọi điểm đo.
    def shut_line(s, depth):
        u = (s - mid) / half
        if abs(u) >= 1.0:
            return None
        return y_in + (y_out - y_in) * (u + 1.0) * 0.5 - depth * math.sqrt(1.0 - u * u)

    depth = 0.0
    for k in range(-95, 96):
        u = k / 100.0
        s = mid + u * half
        need = (shut_line(s, 0.0) - (knot_y(eye_bottom, s) - 0.015)) / math.sqrt(1.0 - u * u)
        depth = max(depth, need)

    verts, uvs, closed, dark, fold = [], [], [], [], []
    for j in range(LID_SEG_T + 1):
        t = j / LID_SEG_T
        for i in range(LID_SEG_S + 1):
            s = -1.0 + 2.0 * i / LID_SEG_S
            x = cx + s * half_w
            bulge = max(0.0, math.cos(s * math.pi * 0.5)) ** 0.75
            margin_open = knot_y(eye_top, s) - 0.003
            line = shut_line(s, depth)
            margin_shut = margin_open if line is None else min(margin_open, line)
            crease = margin_open + crease_h * (0.35 + 0.65 * bulge)
            y_open = margin_open + (crease - margin_open) * t
            # Hai đầu dải giữ đúng tỉ lệ da gốc (viền mi không nhoè, nếp mí liền da), chỉ phần giữa giãn.
            span_shut = crease - margin_shut
            r0 = (crease - margin_open) / span_shut if span_shut > 1e-6 else 1.0
            g = r0 * t + (1.0 - r0) * (3.0 * t * t - 2.0 * t * t * t)
            y_shut = margin_shut + span_shut * g
            z = -(x * x) * 0.28 + 0.001
            verts.append((x, y_open, z))
            closed.append((x, y_shut, z))
            uvs.append(sheet_uv(x, y_open))
            # Hàng mi: đậm ở viền, nhạt dần trong 1/5 dải, mờ dần về hai khoé.
            edge = 1.0 - min(max((t - 0.05) / 0.17, 0.0), 1.0)
            dark.append(0.95 * edge * edge * min(1.0, bulge * 1.5))
            # Bóng nếp mí: một dải mềm ở 3/4 dải da, chỉ hiện khi khép.
            fold.append(0.55 * math.exp(-((t - 0.72) / 0.12) ** 2) * min(1.0, bulge * 1.4))
    faces = []
    row = LID_SEG_S + 1
    for j in range(LID_SEG_T):
        for i in range(LID_SEG_S):
            a = j * row + i
            faces.append((a, a + 1, a + row + 1, a + row))
    lid_me = bpy.data.meshes.new(name)
    lid_me.from_pydata(verts, [], faces)
    lid_me.update()
    vien_attr = lid_me.attributes.new(name="Vien", type="FLOAT", domain="POINT")
    for idx, w in enumerate(dark):
        vien_attr.data[idx].value = w
    nep_attr = lid_me.attributes.new(name="Nep", type="FLOAT", domain="POINT")
    for idx, w in enumerate(fold):
        nep_attr.data[idx].value = w
    uv = lid_me.uv_layers.new(name="UVMap")
    for loop in lid_me.loops:
        uv.data[loop.index].uv = uvs[loop.vertex_index]
    lid = bpy.data.objects.new(name, lid_me)
    scene.collection.objects.link(lid)
    lid.data.materials.append(lid_mat)
    lid.shape_key_add(name="Mo")
    shut = lid.shape_key_add(name="Khep")
    for idx, co in enumerate(closed):
        shut.data[idx].co = co
    lid.parent = bust
    return lid



def lid_skin_color(rgb, eyes):
    H, W = rgb.shape[:2]
    x0, x1 = 0.006 * W, (1.0 / 3.0 - 0.006) * W
    yt, yb = (1 - 0.994) * H, (1 - 0.506) * H
    vals = []
    for e in eyes:
        for k in range(-5, 6):
            x = e["cx"] + k / 6 * e["half_w"] * 0.7
            for f in (0.35, 0.5, 0.65):
                y = max(e["top"]) + f * e["brow_gap"]
                px = int(x0 + (x + 1) / 2 * (x1 - x0))
                py = int(yt + (1 - (y + 1) / 2) * (yb - yt))
                vals.append(rgb[py, px])
    c = np.median(np.array(vals), axis=0) / 255.0
    # ảnh mẫu là sRGB, nút màu trong Blender là tuyến tính
    lin = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return (float(lin[0]), float(lin[1]), float(lin[2]), 1.0)



# ---------------- Miệng ----------------
def mouth_line(x):
    return float(np.interp(x, MOUTH["x"], MOUTH["y"]))

def make_mouth():
    left, right = MOUTH["left"], MOUTH["right"]
    half_m = 0.5 * (right - left)
    cxm = 0.5 * (left + right)
    pad = 0.6 * half_m
    half_p = half_m + pad
    sc = half_m / half_p
    drop = 0.30 * half_m            # môi dưới hạ tối đa
    lift = 0.22 * drop               # môi trên nhích lên
    up_h = 0.45 * half_m             # dải môi trên, chưa chạm mũi
    low_h = 1.15 * half_m            # dải hàm dưới tới gần cằm

    def bell(s):
        u = s / sc
        return max(0.0, 1.0 - u * u) ** 0.8 if abs(u) < 1.0 else 0.0

    def build(name, sign, height, move):
        verts, uvs, opened = [], [], []
        for j in range(M_SEG_T + 1):
            t = j / M_SEG_T
            for i in range(M_SEG_S + 1):
                s = -1.0 + 2.0 * i / M_SEG_S
                x = cxm + s * half_p
                # mép ngoài dải ôm theo đường môi ở giữa, phẳng dần ra hai bên
                y = mouth_line(x) + sign * height * t
                z = -(x * x) * 0.28 + 0.0012
                fall = math.cos(t * math.pi * 0.5) ** (0.6 if sign < 0 else 1.2)
                # sát mép hai bên dải (ngoài khoé) không dịch, để liền da
                side = max(0.0, 1.0 - max(0.0, abs(s) - sc) / (1.0 - sc)) if abs(s) > sc else 1.0
                d = move * bell(s) * fall
                verts.append((x, y, z))
                opened.append((x, y + sign * d, z))
                uvs.append(sheet_uv(x, y))
        faces = []
        row = M_SEG_S + 1
        for j in range(M_SEG_T):
            for i in range(M_SEG_S):
                a0 = j * row + i
                faces.append((a0, a0 + 1, a0 + row + 1, a0 + row) if sign < 0 else (a0, a0 + row, a0 + row + 1, a0 + 1))
        me_ = bpy.data.meshes.new(name)
        me_.from_pydata(verts, [], faces)
        me_.update()
        uvl = me_.uv_layers.new(name="UVMap")
        for loop in me_.loops:
            uvl.data[loop.index].uv = uvs[loop.vertex_index]
        ob = bpy.data.objects.new(name, me_)
        scene.collection.objects.link(ob)
        ob.data.materials.append(mat)
        ob.shape_key_add(name="Ngam")
        kb = ob.shape_key_add(name="Mo")
        for idx, co in enumerate(opened):
            kb.data[idx].co = co
        ob.parent = bust
        return ob

    upper = build("MoiTren", 1, up_h, lift)
    lower = build("HamDuoi", -1, low_h, drop)

    # Lòng miệng: dải tối hình thấu kính giữa hai khoé, nằm dưới hai dải da.
    verts, cols, faces = [], [], []
    rows = 8
    for j in range(rows + 1):
        t = j / rows                     # 0 = mép dưới (môi dưới khi mở hết), 1 = mép trên
        for i in range(M_SEG_S + 1):
            s = -1.0 + 2.0 * i / M_SEG_S
            x = cxm + s * half_m * 1.02
            b = max(0.0, 1.0 - s * s) ** 0.8
            y_lo = mouth_line(x) - (drop + 0.004) * b
            y_hi = mouth_line(x) + (lift + 0.004) * b
            y = y_lo + (y_hi - y_lo) * t
            verts.append((x, y, -(x * x) * 0.28 + 0.0006))
            # trên cùng là răng trên mờ, xuống dưới tối dần thành lòng miệng
            if t > 0.86:
                col = (0.70, 0.63, 0.57)
            elif t > 0.74:
                col = (0.34, 0.21, 0.19)
            else:
                k = 0.55 + 0.45 * t
                col = (0.20 * k, 0.05 * k, 0.045 * k)
            cols.append(col)
    row = M_SEG_S + 1
    for j in range(rows):
        for i in range(M_SEG_S):
            a0 = j * row + i
            faces.append((a0, a0 + 1, a0 + row + 1, a0 + row))
    cme = bpy.data.meshes.new("LongMieng")
    cme.from_pydata(verts, [], faces)
    cme.update()
    attr = cme.color_attributes.new(name="Mau", type="FLOAT_COLOR", domain="POINT")
    for idx, c in enumerate(cols):
        attr.data[idx].color = (c[0], c[1], c[2], 1.0)
    cmat = bpy.data.materials.new("LongMieng")
    cmat.use_nodes = True
    cnt = cmat.node_tree
    cnt.nodes.clear()
    co_ = cnt.nodes.new("ShaderNodeOutputMaterial")
    ce = cnt.nodes.new("ShaderNodeEmission")
    ca = cnt.nodes.new("ShaderNodeAttribute")
    ca.attribute_name = "Mau"
    cnt.links.new(ca.outputs["Color"], ce.inputs["Color"])
    cnt.links.new(ce.outputs[0], co_.inputs[0])
    cob = bpy.data.objects.new("LongMieng", cme)
    scene.collection.objects.link(cob)
    cob.data.materials.append(cmat)
    cob.parent = bust
    return [upper, lower]



# ---------------- Lông mày ----------------
def make_brow(name, brow, eye):
    bx = brow["x"]; blo = brow["lo"]; bhi = brow["hi"]
    x_a, x_b = min(bx) - 0.03, max(bx) + 0.03
    half_b = 0.5 * (x_b - x_a)
    cxb = 0.5 * (x_a + x_b)
    thick = float(np.median(np.array(bhi) - np.array(blo)))
    base_gap = 0.22 * eye["brow_gap"]          # đáy dải: trên nếp mí, không đụng mí mắt
    up_room = max(0.09, 2.5 * thick)            # phía trên lông mày tới trán
    raise_amt = 0.55 * thick + 0.008
    inner_sign = 1.0 if FACE_MID > cxb else -1.0   # phía đầu trong (gần sống mũi)

    def lo_at(x):
        return float(np.interp(x, bx, blo))

    def hi_at(x):
        return float(np.interp(x, bx, bhi))

    verts, uvs, k_up, k_lo = [], [], [], []
    for j in range(B_SEG_T + 1):
        t = j / B_SEG_T
        for i in range(B_SEG_S + 1):
            s_ = -1.0 + 2.0 * i / B_SEG_S
            x = cxb + s_ * half_b
            y0 = lo_at(x) - base_gap
            y1 = hi_at(x) + up_room
            y = y0 + (y1 - y0) * t
            # trọng số dọc: 0 ở đáy, 1 trong dải lông mày, về 0 ở trán
            yb0, yb1 = lo_at(x), hi_at(x)
            if y <= yb0:
                wv = (y - y0) / max(yb0 - y0, 1e-6)
                wv = wv * wv * (3 - 2 * wv)
            elif y <= yb1:
                wv = 1.0
            else:
                wv = 1.0 - (y - yb1) / max(y1 - yb1, 1e-6)
                wv = wv * wv * (3 - 2 * wv)
            edge = min(1.0, (1.0 - abs(s_)) / 0.25)
            edge = edge * edge * (3 - 2 * edge)
            inner = 0.5 + 0.5 * s_ * inner_sign      # 0 đầu ngoài, 1 đầu trong
            z = -(x * x) * 0.28 + 0.0012
            verts.append((x, y, z))
            uvs.append(sheet_uv(x, y))
            k_up.append((x, y + raise_amt * wv * edge, z))
            lift = raise_amt * wv * edge * (0.15 + 1.1 * inner ** 1.5)
            pull = 0.25 * raise_amt * wv * edge * inner * inner_sign
            k_lo.append((x + pull, y + lift - 0.25 * raise_amt * wv * edge * (1 - inner), z))
    faces = []
    row = B_SEG_S + 1
    for j in range(B_SEG_T):
        for i in range(B_SEG_S):
            a0 = j * row + i
            faces.append((a0, a0 + 1, a0 + row + 1, a0 + row))
    me_ = bpy.data.meshes.new(name)
    me_.from_pydata(verts, [], faces)
    me_.update()
    uvl = me_.uv_layers.new(name="UVMap")
    for loop in me_.loops:
        uvl.data[loop.index].uv = uvs[loop.vertex_index]
    ob = bpy.data.objects.new(name, me_)
    scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    ob.shape_key_add(name="Thuong")
    for key_name, cos in (("Nhuong", k_up), ("Lo", k_lo)):
        kb = ob.shape_key_add(name=key_name)
        for idx, co in enumerate(cos):
            kb.data[idx].co = co
    ob.parent = bust
    return ob



class FaceRig:
    """Các phần đã dựng và hàm đặt keyframe cho từng kênh cử động."""

    def __init__(self, rig, chest, bust_obj, lids, khep_val, lnt, mouth_parts, brow_parts):
        self.rig, self.chest, self.bust = rig, chest, bust_obj
        self.lids, self.khep_val, self.lnt = lids, khep_val, lnt
        self.mouth_parts, self.brow_parts = mouth_parts, brow_parts

    # đầu, ngực
    def set_tilt(self, frame, degrees):
        self.rig.rotation_euler.y = math.radians(degrees)
        self.rig.keyframe_insert(data_path="rotation_euler", index=1, frame=frame)

    def set_breath(self, frame, amount):
        self.chest.location.z = amount
        self.chest.keyframe_insert(data_path="location", index=2, frame=frame)
        self.rig.scale.z = 1 + amount
        self.rig.keyframe_insert(data_path="scale", index=2, frame=frame)

    # mắt
    def set_blink(self, frame, closed):
        for lid in self.lids:
            kb = lid.data.shape_keys.key_blocks["Khep"]
            kb.value = closed
            kb.keyframe_insert(data_path="value", frame=frame)
        self.khep_val.outputs[0].default_value = closed
        self.khep_val.outputs[0].keyframe_insert(data_path="default_value", frame=frame)

    # miệng
    def set_mouth(self, frame, amount):
        for ob in self.mouth_parts:
            kb = ob.data.shape_keys.key_blocks["Mo"]
            kb.value = amount
            kb.keyframe_insert(data_path="value", frame=frame)

    # lông mày: các dáng có sẵn trên dải mày
    BROW_KEYS = ("Nhuong", "Lo")

    def set_brow(self, frame, **weights):
        for ob in self.brow_parts:
            for key_name in self.BROW_KEYS:
                kb = ob.data.shape_keys.key_blocks[key_name]
                kb.value = float(weights.get(key_name, 0.0))
                kb.keyframe_insert(data_path="value", frame=frame)

    def animated_owners(self):
        return [self.rig, self.chest, self.lnt,
                *(lid.data.shape_keys for lid in self.lids),
                *(ob.data.shape_keys for ob in self.mouth_parts),
                *(ob.data.shape_keys for ob in self.brow_parts)]


def build(scene_, sheet_path, face, name="Mat", pose=None):
    """Dựng mặt từ ảnh mẫu + số đo. pose: {"xoay_do": 10, "lech_x": 0.22, "co": 1.45}."""
    global scene, mat, lid_mat, bust, img, MOUTH, FACE_MID
    pose = pose or {}
    scene = scene_
    img = bpy.data.images.load(sheet_path)
    mat = bpy.data.materials.new("DaMat")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emission = nt.nodes.new("ShaderNodeEmission")
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = img
    tex.interpolation = "Linear"
    nt.links.new(tex.outputs["Color"], emission.inputs["Color"])
    nt.links.new(emission.outputs["Emission"], out.inputs["Surface"])

    bpy.ops.mesh.primitive_grid_add(x_subdivisions=48, y_subdivisions=48, size=2, location=(0, 0, 0))
    bust = bpy.context.active_object
    bust.name = name
    bust.data.materials.append(mat)
    me = bust.data
    uv_layer = me.uv_layers.active
    for loop in me.loops:
        uv = uv_layer.data[loop.index].uv
        uv.x = PANEL_U0 + uv.x * (PANEL_U1 - PANEL_U0)
        uv.y = PANEL_V0 + uv.y * (PANEL_V1 - PANEL_V0)
    for v in me.vertices:
        v.co.z -= (v.co.x * v.co.x) * 0.28
    bpy.ops.object.shade_smooth()

    # Vật liệu mí: cùng ảnh mẫu, thêm hàng mi tối ở viền, bóng nếp mí, da mí trơn khi khép.
    # Vật liệu mí: cùng ảnh mẫu, thêm hàng mi tối ở viền. Độ tối hàng mi chạy theo độ khép (nút "Khep"),
    # nên lúc mở mí y hệt da gốc.
    lid_mat = mat.copy()
    lid_mat.name = "BoMi"
    lnt = lid_mat.node_tree
    lid_tex = next(n for n in lnt.nodes if n.type == "TEX_IMAGE")
    lid_em = next(n for n in lnt.nodes if n.type == "EMISSION")
    vien = lnt.nodes.new("ShaderNodeAttribute")
    vien.attribute_name = "Vien"
    khep_val = lnt.nodes.new("ShaderNodeValue")
    khep_val.name = "Khep"
    khep_val.outputs[0].default_value = 0.0
    fac = lnt.nodes.new("ShaderNodeMath")
    fac.operation = "MULTIPLY"
    lnt.links.new(vien.outputs["Fac"], fac.inputs[0])
    lnt.links.new(khep_val.outputs[0], fac.inputs[1])
    nep = lnt.nodes.new("ShaderNodeAttribute")
    nep.attribute_name = "Nep"
    nep_fac = lnt.nodes.new("ShaderNodeMath")
    nep_fac.operation = "MULTIPLY"
    lnt.links.new(nep.outputs["Fac"], nep_fac.inputs[0])
    lnt.links.new(khep_val.outputs[0], nep_fac.inputs[1])
    shade = lnt.nodes.new("ShaderNodeMix")
    shade.data_type = "RGBA"
    shade.blend_type = "MULTIPLY"
    shade.inputs[7].default_value = (0.62, 0.48, 0.42, 1.0)
    lnt.links.new(nep_fac.outputs[0], shade.inputs[0])
    lnt.links.new(lid_tex.outputs["Color"], shade.inputs[6])
    # Da mí khi khép: pha dần về màu da trung bình của dải mí (mí khép trơn, không lộ nếp mí đôi bị giãn).
    skin_mix = lnt.nodes.new("ShaderNodeMix")
    skin_mix.data_type = "RGBA"
    skin_mix.blend_type = "MIX"
    skin_mix.inputs[7].default_value = (0.8, 0.5, 0.35, 1.0)
    skin_fac = lnt.nodes.new("ShaderNodeMath")
    skin_fac.operation = "MULTIPLY"
    skin_fac.inputs[1].default_value = 0.55
    lnt.links.new(khep_val.outputs[0], skin_fac.inputs[0])
    lnt.links.new(skin_fac.outputs[0], skin_mix.inputs[0])
    lnt.links.new(shade.outputs[2], skin_mix.inputs[6])
    mix = lnt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MIX"
    mix.inputs[7].default_value = (0.07, 0.04, 0.03, 1.0)
    lnt.links.new(fac.outputs[0], mix.inputs[0])
    lnt.links.new(skin_mix.outputs[2], mix.inputs[6])
    lnt.links.new(mix.outputs[2], lid_em.inputs["Color"])

    EYES = face["eyes"]
    skin_mix.inputs[7].default_value = lid_skin_color(sheet_rgb(img), EYES)
    lids = [
        # Dải da mí dày theo cỡ mắt (mắt to cần nhiều da để che, đỡ bị giãn), nhưng không chạm lông mày.
        make_lid(n, e["cx"], e["half_w"], e["top"], e["bottom"],
                 min(max(0.058, 0.6 * e["height"]), 0.75 * e["brow_gap"]))
        for n, e in zip(("MiTrai", "MiPhai"), EYES)
    ]

    MOUTH = face["mouth"]
    mouth_parts = make_mouth()

    FACE_MID = 0.5 * (EYES[0]["cx"] + EYES[1]["cx"])
    brow_parts = [make_brow(n, b, e) for n, b, e in zip(("MayTrai", "MayPhai"), face["brows"], EYES) if b]

    # Đứng đối diện máy quay, xoay sẵn ba phần tư. Nghiêng đầu animate trên empty cha.
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
    rig = bpy.context.active_object
    rig.name = "Dau"
    bust.parent = rig
    # Ô mặt trên ảnh mẫu không vuông: giữ đúng tỉ lệ điểm ảnh, không kéo dọc khuôn mặt.
    ratio = (PANEL_V1 - PANEL_V0) * img.size[1] / ((PANEL_U1 - PANEL_U0) * img.size[0])
    size = pose.get("co", 1.45)
    bust.scale = (size, size * ratio, size)
    bust.location = (pose.get("lech_x", 0.22), 0, 0)
    bust.rotation_euler = (math.radians(90), 0, 0)
    rig.rotation_euler = (0, math.radians(-3), math.radians(pose.get("xoay_do", 10)))

    # Thở: ngực giả bằng empty dưới, nhích rất nhỏ.
    bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, -0.55, 0))
    chest = bpy.context.active_object
    chest.name = "Nguc"
    chest.parent = rig
    return FaceRig(rig, chest, bust, lids, khep_val, lnt, mouth_parts, brow_parts)
