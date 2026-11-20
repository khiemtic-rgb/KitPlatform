"""Cảnh Blender của Bố: mặt mẫu Bo_mat, máy quay khóa, miệng ngậm, chớp mắt, thở, nghiêng đầu.

Chạy: blender --background --python tools/san-gach-do/bo_motion.py
"""
import math
import os

import bpy

ROOT = os.environ.get("SGD_OUT", r"E:\KitPlatform\.dev\san-gach-do\bo")
SHEET = os.environ.get("SGD_SHEET", r"F:\Noi_Com_Chung\Nhan_Vat\Bo_Chuan\Bo_mat.png")
os.makedirs(ROOT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.eevee.taa_render_samples = 8
scene.view_settings.view_transform = "Standard"
scene.render.resolution_x = 1280
scene.render.resolution_y = 720
scene.render.fps = 24
scene.frame_start = 1
scene.frame_end = 96
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False

world = bpy.data.worlds.new("Nen")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
if bg:
    bg.inputs[0].default_value = (0.78, 0.78, 0.80, 1)
    bg.inputs[1].default_value = 1

img = bpy.data.images.load(SHEET)
mat = bpy.data.materials.new("BoMat")
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
bust.name = "Bo"
bust.data.materials.append(mat)
me = bust.data
uv_layer = me.uv_layers.active
for loop in me.loops:
    uv = uv_layer.data[loop.index].uv
    uv.x = 0.006 + uv.x * (1.0 / 3.0 - 0.012)
    uv.y = 0.506 + uv.y * (0.5 - 0.012)
for v in me.vertices:
    v.co.z -= (v.co.x * v.co.x) * 0.28
bpy.ops.object.shade_smooth()

def sheet_uv(local_x, local_y):
    u = (local_x + 1.0) * 0.5
    v = (local_y + 1.0) * 0.5
    return (
        0.006 + u * (1.0 / 3.0 - 0.012),
        0.506 + v * (0.5 - 0.012),
    )

# Mí là một dải da cong ôm khe mắt: hàng dưới là viền mi, hàng trên là nếp mí.
# UV của mỗi đỉnh lấy đúng chỗ nó nằm trên ảnh mẫu khi mở, nên mở là trùng da, không lộ mép.
# Khép thì chỉ viền mi trượt xuống mép dưới mắt, nếp mí đứng yên: da mí giãn ra che tròng.
LID_SEG_S = 32
LID_SEG_T = 10
LID_KNOTS = (-1.0, -0.7, -0.5, -0.3, -0.1, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0)

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
mix = lnt.nodes.new("ShaderNodeMix")
mix.data_type = "RGBA"
mix.blend_type = "MIX"
mix.inputs[7].default_value = (0.07, 0.04, 0.03, 1.0)
lnt.links.new(fac.outputs[0], mix.inputs[0])
lnt.links.new(shade.outputs[2], mix.inputs[6])
lnt.links.new(mix.outputs[2], lid_em.inputs["Color"])

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
        need = (shut_line(s, 0.0) - (knot_y(eye_bottom, s) - 0.012)) / math.sqrt(1.0 - u * u)
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

# Khe mắt đo trên ảnh mẫu Bo_mat (toạ độ local của lưới mặt), trái/phải theo ảnh.
lids = [
    make_lid(
        "MiTrai", -0.135, 0.088,
        (0.222, 0.245, 0.262, 0.271, 0.275, 0.280, 0.280, 0.275, 0.258, 0.219, 0.212),
        (0.222, 0.210, 0.197, 0.188, 0.184, 0.184, 0.184, 0.197, 0.206, 0.210, 0.212),
        0.060,
    ),
    make_lid(
        "MiPhai", 0.175, 0.086,
        (0.252, 0.284, 0.302, 0.310, 0.310, 0.310, 0.306, 0.297, 0.267, 0.265, 0.265),
        (0.252, 0.228, 0.219, 0.214, 0.214, 0.223, 0.232, 0.249, 0.262, 0.265, 0.265),
        0.055,
    ),
]

# Đứng đối diện máy quay, xoay sẵn ba phần tư. Nghiêng đầu animate trên empty cha.
bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, 0, 0))
rig = bpy.context.active_object
rig.name = "Dau"
bust.parent = rig
bust.location = (0, 0, 0)
bust.scale = (1.45, 1.45, 1.45)
bust.location = (0.22, 0, 0)
bust.rotation_euler = (math.radians(90), 0, 0)
rig.rotation_euler = (0, math.radians(-3), math.radians(10))

# Thở: xương ngực giả bằng empty dưới, nhích rất nhỏ.
bpy.ops.object.empty_add(type="PLAIN_AXES", location=(0, -0.55, 0))
chest = bpy.context.active_object
chest.name = "Nguc"
chest.parent = rig

cam_data = bpy.data.cameras.new("MayQuay")
cam_data.lens = 70
cam = bpy.data.objects.new("MayQuay", cam_data)
scene.collection.objects.link(cam)
cam.location = (0.06, -2.35, 0.02)
cam.data.lens = 50
cam.rotation_euler = (math.radians(90), 0, 0)
scene.camera = cam

def key(obj, frame, data_path, index, value):
    obj.keyframe_insert(data_path=data_path, index=index, frame=frame)
    # ghi giá trị trước khi insert
    # caller sets the value first

def set_tilt(frame, degrees):
    rig.rotation_euler.y = math.radians(degrees)
    rig.keyframe_insert(data_path="rotation_euler", index=1, frame=frame)

def set_breath(frame, amount):
    chest.location.z = amount
    chest.keyframe_insert(data_path="location", index=2, frame=frame)
    rig.scale.z = 1 + amount
    rig.keyframe_insert(data_path="scale", index=2, frame=frame)

def set_blink(frame, closed):
    for lid in lids:
        kb = lid.data.shape_keys.key_blocks["Khep"]
        kb.value = closed
        kb.keyframe_insert(data_path="value", frame=frame)
    khep_val.outputs[0].default_value = closed
    khep_val.outputs[0].keyframe_insert(data_path="default_value", frame=frame)

# Máy quay không có keyframe. Đầu nghiêng chậm, thở một nhịp thấy được.
set_tilt(1, -2)
set_tilt(48, 7)
set_tilt(96, -2)
set_breath(1, 0.0)
set_breath(24, 0.045)
set_breath(48, 0.0)
set_breath(72, 0.045)
set_breath(96, 0.0)
blinks = (
    (1, 0), (28, 0), (32, 0.45), (36, 1), (39, 1), (44, 0),
    (70, 0), (74, 0.45), (78, 1), (81, 1), (86, 0),
)
fixed = os.environ.get("SGD_CLOSED")
for frame, closed in blinks:
    set_blink(frame, float(fixed) if fixed else closed)

def smooth(obj):
    ad = obj.animation_data
    if not ad or not ad.action:
        return
    curves = []
    if hasattr(ad.action, "layers"):
        for layer in ad.action.layers:
            for strip in layer.strips:
                bag = strip.channelbag(ad.action_slot) if hasattr(strip, "channelbag") else None
                if bag:
                    curves.extend(bag.fcurves)
    if not curves and hasattr(ad.action, "fcurves"):
        curves.extend(ad.action.fcurves)
    for fcu in curves:
        for kp in fcu.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = "AUTO_CLAMPED"
            kp.handle_right_type = "AUTO_CLAMPED"

for action_owner in (rig, chest, lnt, *(lid.data.shape_keys for lid in lids)):
    smooth(action_owner)

shots = [int(os.environ["SGD_FRAME"])] if os.environ.get("SGD_FRAME") else [1, 32, 36]
still = ""
for shot in shots:
    still = os.path.join(ROOT, f"bo_khung_{shot}.png")
    scene.frame_set(shot)
    scene.render.filepath = still
    bpy.ops.render.render(write_still=True)

if os.environ.get("SGD_STILL_ONLY") != "1":
    frames = os.path.join(ROOT, "frames")
    os.makedirs(frames, exist_ok=True)
    scene.render.filepath = os.path.join(frames, "bo_")
    bpy.ops.render.render(animation=True)
print("SGD_DONE", still)
