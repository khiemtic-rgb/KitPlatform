"""Lớp cảnh: cài đặt render, nền, máy quay theo cỡ cảnh, xuất khung hình và ghép tiếng."""
import math
import os
import shutil
import subprocess

import bpy


def setup(cfg, end):
    """cfg: phần "render" của data/cau_hinh.json."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.eevee.taa_render_samples = cfg.get("mau", 8)
    scene.view_settings.view_transform = "Standard"
    scene.render.resolution_x = cfg.get("rong", 1280)
    scene.render.resolution_y = cfg.get("cao", 720)
    scene.render.fps = 24
    scene.frame_start = 1
    scene.frame_end = end
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    return scene


def background(scene, nen):
    """nen: None -> xám; [r, g, b] -> màu phẳng; "đường/dẫn.png" -> ảnh bối cảnh đặt sau đầu nhân vật."""
    world = bpy.data.worlds.new("Nen")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    color = (0.78, 0.78, 0.80)
    if isinstance(nen, (list, tuple)):
        color = tuple(nen[:3])
    if bg:
        bg.inputs[0].default_value = (*color, 1)
        bg.inputs[1].default_value = 1
    if isinstance(nen, str) and os.path.exists(nen):
        im = bpy.data.images.load(nen)
        m = bpy.data.materials.new("NenAnh")
        m.use_nodes = True
        nt = m.node_tree
        nt.nodes.clear()
        o = nt.nodes.new("ShaderNodeOutputMaterial")
        e = nt.nodes.new("ShaderNodeEmission")
        t = nt.nodes.new("ShaderNodeTexImage")
        t.image = im
        nt.links.new(t.outputs["Color"], e.inputs["Color"])
        nt.links.new(e.outputs[0], o.inputs[0])
        aspect = im.size[0] / max(im.size[1], 1)
        h = 4.2
        bpy.ops.mesh.primitive_plane_add(size=1, location=(0.06, 2.2, 0.1), rotation=(math.radians(90), 0, 0))
        p = bpy.context.active_object
        p.name = "NenAnh"
        p.scale = (h * aspect, h, 1)
        p.data.materials.append(m)


# Cỡ cảnh: vị trí máy (x, y, z) và tiêu cự. "can_mat" là khung đã duyệt cho thoại.
FRAMING = {
    "can_mat": {"vi_tri": (0.06, -2.75, 0.14), "tieu_cu": 50},
    "dac_ta": {"vi_tri": (0.10, -2.20, 0.20), "tieu_cu": 60},
    "trung": {"vi_tri": (0.06, -3.60, 0.05), "tieu_cu": 45},
}


def camera(scene, khung="can_mat", tuy_chinh=None):
    f = dict(FRAMING.get(khung, FRAMING["can_mat"]))
    f.update(tuy_chinh or {})
    data = bpy.data.cameras.new("MayQuay")
    data.lens = f["tieu_cu"]
    cam = bpy.data.objects.new("MayQuay", data)
    scene.collection.objects.link(cam)
    cam.location = tuple(f["vi_tri"])
    cam.rotation_euler = (math.radians(90), 0, 0)
    scene.camera = cam
    return cam


def render(scene, out_dir, tag, stills=(1,), animation=True, frames_dir=None):
    os.makedirs(out_dir, exist_ok=True)
    paths = []
    for shot in stills:
        p = os.path.join(out_dir, f"{tag}_khung_{shot}.png")
        scene.frame_set(shot)
        scene.render.filepath = p
        bpy.ops.render.render(write_still=True)
        paths.append(p)
    frames = None
    if animation:
        frames = os.path.join(out_dir, frames_dir or f"frames_{tag}")
        os.makedirs(frames, exist_ok=True)
        doan = os.environ.get("SGD_DOAN")              # "a-b": chỉ render khung a..b, giữ khung đã có (máy ít RAM)
        if doan:
            a_, b_ = (int(x) for x in doan.split("-"))
            full_end = scene.frame_end
            scene.frame_start, scene.frame_end = max(1, a_), min(full_end, b_)
            scene.render.use_overwrite = False
        else:
            # Xoá khung cũ của chính cảnh này: lần render trước dài hơn thì khung dư sẽ bị ghép nhầm vào mp4.
            prefix = f"{tag}_"
            for name in os.listdir(frames):
                rest = name[len(prefix):-4] if name.startswith(prefix) and name.endswith(".png") else ""
                if rest.isdigit():
                    os.remove(os.path.join(frames, name))
        scene.render.filepath = os.path.join(frames, f"{tag}_")
        bpy.ops.render.render(animation=True)
        if doan:
            scene.frame_start, scene.frame_end = 1, full_end
            n_have = sum(1 for nm in os.listdir(frames) if nm.startswith(f"{tag}_") and nm[len(tag) + 1:-4].isdigit())
            if n_have < full_end:                       # chưa đủ khung: chưa ghép mp4
                print("SGD_DOAN_XONG", doan, f"{n_have}/{full_end} khung")
                return paths, None
    return paths, frames


def find_ffmpeg(cfg_path=None):
    for cand in (os.environ.get("SGD_FFMPEG"), cfg_path, shutil.which("ffmpeg")):
        if cand and (os.path.exists(cand) or shutil.which(cand)):
            return cand
    return None


def encode(frames_dir, tag, out_mp4, audio=None, ffmpeg=None, audio_offset=0.0, n_frames=None):
    """Ghép khung thành mp4 (H.264), có tiếng nếu có thoại. Không có ffmpeg thì bỏ qua, giữ khung PNG."""
    if not ffmpeg:
        print("SGD_WARN không tìm thấy ffmpeg, giữ nguyên khung PNG ở", frames_dir)
        return None
    cmd = [ffmpeg, "-y", "-loglevel", "error", "-framerate", "24", "-i", os.path.join(frames_dir, f"{tag}_%04d.png")]
    if audio:
        if audio_offset:
            cmd += ["-itsoffset", str(audio_offset)]
        cmd += ["-i", audio]
    if n_frames:
        cmd += ["-frames:v", str(n_frames)]      # chỉ đúng số khung của cảnh, dù thư mục còn khung lạ
        cmd += ["-t", f"{n_frames / 24:.3f}"]    # thoại dài hơn cảnh thì cắt theo cảnh
    cmd += ["-c:v", "libx264", "-preset", "veryfast", "-crf", "18", "-pix_fmt", "yuv420p"]
    if audio:
        cmd += ["-c:a", "aac", "-b:a", "128k"]
    cmd += ["-movflags", "+faststart", out_mp4]
    subprocess.run(cmd, check=True)
    return out_mp4


# ---------------- cảnh thân người (mô hình 3D / người mẫu tạm)
BODY_FRAMING = {
    # khoảng cách máy (m), cao máy (m), tiêu cự, điểm nhìn cao (m) — cho người ~1,7 m
    "toan": {"xa": 6.5, "cao": 1.15, "tieu_cu": 35, "nhin": 0.9},
    "trung_than": {"xa": 3.2, "cao": 1.30, "tieu_cu": 45, "nhin": 1.15},
    "can_than": {"xa": 2.0, "cao": 1.45, "tieu_cu": 55, "nhin": 1.40},
    "can_mat": {"xa": 1.25, "cao": 1.52, "tieu_cu": 60, "nhin": 1.50},
}


def body_set(scene, nen=None):
    """Đèn mặt trời + đèn phụ, mặt đất, nền; bật bóng đổ."""
    background(scene, nen)
    bpy.ops.object.light_add(type="SUN", location=(3, -4, 6))
    sun = bpy.context.active_object
    sun.data.energy = 3.0
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(35))
    bpy.ops.object.light_add(type="AREA", location=(-3, -3, 3))
    fill = bpy.context.active_object
    fill.data.energy = 250
    fill.data.size = 4
    fill.rotation_euler = (math.radians(60), 0, math.radians(-45))
    if scene.world and scene.world.node_tree:
        bgn = scene.world.node_tree.nodes.get("Background")
        if bgn:
            bgn.inputs[1].default_value = 0.6
    bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, 0))
    ground = bpy.context.active_object
    ground.name = "MatDat"
    m = bpy.data.materials.new("MatDat")
    m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF")
    if b:
        b.inputs["Base Color"].default_value = (0.42, 0.38, 0.33, 1)
        b.inputs["Roughness"].default_value = 1.0
    ground.data.materials.append(m)


def follow(scene, cam, points, amount=0.85, sigma_s=0.8, fps=24):
    """Máy quay bám theo một điểm (vd đầu người nói), đã làm mượt (σ ~0,8 giây) để không rung.
    points: [(khung, Vector)]; dời cả máy và điểm nhìn theo phần lệch so với khung đầu."""
    import numpy as np
    aim = next(c.target for c in cam.constraints if c.type == "TRACK_TO")
    f = np.array([p[0] for p in points], float)
    P = np.array([tuple(p[1]) for p in points], float)
    sig = sigma_s * fps
    sm = np.empty_like(P)
    for i, fi in enumerate(f):
        w = np.exp(-0.5 * ((f - fi) / sig) ** 2)
        sm[i] = (w[:, None] * P).sum(0) / w.sum()
    d = (sm - sm[0]) * amount
    c0, a0 = cam.location.copy(), aim.location.copy()
    for fi, di in zip(f, d):
        cam.location = (c0.x + di[0], c0.y + di[1], c0.z + di[2] * 0.6)
        aim.location = (a0.x + di[0], a0.y + di[1], a0.z + di[2])
        cam.keyframe_insert("location", frame=int(fi))
        aim.keyframe_insert("location", frame=int(fi))


def body_camera(scene, khung="toan", target=(0, 0, 0), goc_do=20.0, tuy_chinh=None):
    """Máy nhìn vào `target` (giữa các diễn viên), lệch `goc_do` độ sang phải so với chính diện."""
    f = dict(BODY_FRAMING.get(khung, BODY_FRAMING["toan"]))
    f.update(tuy_chinh or {})
    a = math.radians(goc_do)
    tx, ty, _ = target
    loc = (tx + f["xa"] * math.sin(a), ty - f["xa"] * math.cos(a), f["cao"])
    data = bpy.data.cameras.new("MayQuay")
    data.lens = f["tieu_cu"]
    cam = bpy.data.objects.new("MayQuay", data)
    scene.collection.objects.link(cam)
    cam.location = loc
    aim = bpy.data.objects.new("DiemNhin", None)
    scene.collection.objects.link(aim)
    aim.location = (tx, ty, f["nhin"])
    c = cam.constraints.new("TRACK_TO")
    c.target = aim
    c.track_axis = "TRACK_NEGATIVE_Z"
    c.up_axis = "UP_Y"
    scene.camera = cam
    return cam
