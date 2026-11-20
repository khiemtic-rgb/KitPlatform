"""Chiếu ảnh mặt gốc của nhân vật lên da mặt mô hình 3D (mô hình dựng từ ảnh toàn thân thường mất nét mặt).

Cách làm (không cần thư viện ngoài, chạy trong Blender):
  1. chụp thẳng mặt mô hình (máy chiếu song song) và dò mắt / lông mày / miệng (mat3d._detect);
  2. lấy ô ảnh chính diện trong bộ mặt gốc (vd Bo_mat.png: 3 cột x 2 hàng, ô trên-trái) và dò y như vậy;
  3. uốn ảnh gốc cho các điểm đó trùng nhau (thin-plate spline: tâm + hai khoé mắt, lông mày, hai mép + giữa miệng);
  4. mỗi điểm ảnh da thuộc vùng mặt (mặt hướng ra trước, trong hình elip quanh mặt, không phải tóc) lấy màu
     tương ứng trong ảnh gốc; mép elip mờ dần, màu da chỉnh theo da mô hình để không lộ đường ghép;
  5. ghi ra data/mo_hinh/<Ma>_da_mat.png — load_model tự dùng ảnh này thay ảnh da gốc.

Chạy: blender -b -P tao_mo_hinh_3d.py -- <Ma> --chieu-mat   (hoặc sgd.cmd chieu_mat <Ma>)
"""
import math
import os

import bpy
import numpy as np

from . import body, mat3d

R = mat3d.FRONT_RES


# ---------------------------------------------------------------- ảnh
def load_rgb(path):
    """Ảnh -> mảng (H, W, 3) sRGB 0..255, hàng 0 ở trên."""
    im = bpy.data.images.load(path, check_existing=False)
    w, h = im.size
    a = np.empty(w * h * 4, np.float32)
    im.pixels.foreach_get(a)
    bpy.data.images.remove(im)
    a = a.reshape(h, w, 4)[::-1, :, :3]
    return np.clip(a * 255.0, 0, 255)


def bilinear(img, x, y):
    h, w = img.shape[:2]
    x = np.clip(x, 0, w - 1.001)
    y = np.clip(y, 0, h - 1.001)
    x0 = np.floor(x).astype(int)
    y0 = np.floor(y).astype(int)
    fx = (x - x0)[..., None]
    fy = (y - y0)[..., None]
    a = img[y0, x0] * (1 - fx) + img[y0, x0 + 1] * fx
    b = img[y0 + 1, x0] * (1 - fx) + img[y0 + 1, x0 + 1] * fx
    return a * (1 - fy) + b * fy


def ref_panel(img, cot=0, hang=0, so_cot=3, so_hang=2):
    """Ô ảnh trong bộ mặt (mặc định ô trên-trái = chính diện, bình thường), cắt vuông theo chiều cao ô,
    phóng về R x R như ảnh chụp mô hình."""
    H, W = img.shape[:2]
    pw, ph = W / so_cot, H / so_hang
    x0, y0 = cot * pw, hang * ph
    side = min(pw, ph)
    cxp = x0 + pw / 2
    xs = cxp - side / 2 + (np.arange(R) + 0.5) * side / R
    ys = y0 + (np.arange(R) + 0.5) * side / R
    X, Y = np.meshgrid(xs, ys)
    return bilinear(img, X, Y)


# ---------------------------------------------------------------- điểm mốc + uốn ảnh
def outline(img, px, bg_light):
    """Viền mặt: hai bên má ở tầm miệng và giữa miệng-cằm, đỉnh cằm (ảnh mô hình: cằm theo hình khối)."""
    e = px["eyes"]
    gap = abs(e[1]["cx"] - e[0]["cx"])
    my = px["mouth"]["y"]
    cxm = px["face_cx"]
    skin = np.asarray(px["skin"])
    d = _skin_dist(img, skin)
    lum = img @ np.array([.3, .59, .11])
    sl = float(skin @ np.array([.3, .59, .11]))
    H, W = d.shape
    if not bg_light and px.get("cam") and px["cam"] > my + 0.25 * gap:
        chin = float(px["cam"])
    else:
        # ảnh gốc: đáy cằm = ngay trên chỗ tối nhất (bóng dưới cằm) trong khoảng miệng -> cổ
        col = lum[:, int(cxm) - 4:int(cxm) + 5].mean(1)
        a_, b_ = int(my + 0.3 * gap), min(H - 2, int(my + 1.5 * gap))
        chin = float(a_ + int(np.argmin(col[a_:b_])) - 4)
    pts = []
    chroma = img[..., 0] - img[..., 2]                       # da luôn đỏ-vàng hơn xanh (kể cả chỗ tối); nền, tóc thì không
    m = px["mouth"]
    for yy in (my, (my + chin) / 2):
        yi = int(yy)
        for step, x_start in ((-1, m["x0"]), (1, m["x1"])):
            x = int(x_start) + step * 3
            run = 0
            while 2 < x < W - 3:
                bad = chroma[yi, x] < 35 or lum[yi, x] < 0.30 * sl
                run = run + 1 if bad else 0
                if run >= 3:
                    x -= step * 3
                    break
                x += step
            pts.append((float(x), float(yy)))
    pts.append((float(cxm), chin))
    return pts


def landmarks(px, edge=None):
    e = sorted(px["eyes"], key=lambda e: e["cx"])
    b = sorted(px["brows"], key=lambda b: b["x0"])
    m = px["mouth"]
    pts = []
    for ee in e:
        pts += [(ee["cx"], ee["cy"]), (ee["cx"] - ee["a"], ee["cy"]), (ee["cx"] + ee["a"], ee["cy"]),
                (ee["cx"], ee["cy"] - ee["b"]), (ee["cx"], ee["cy"] + ee["b"])]
    for bb in b:
        yb = (bb["y0"] + bb["y1"]) / 2
        pts += [((bb["x0"] + bb["x1"]) / 2, yb), (bb["x0"], yb), (bb["x1"], yb)]
    pts += [(m["x0"], m["y"]), (m["x1"], m["y"]), ((m["x0"] + m["x1"]) / 2, m["y"])]
    if edge:
        pts += list(edge)
    return np.array(pts, float)


def similarity(a, b):
    """Phép đồng dạng (xoay+co giãn+dời) đưa tập điểm a về b theo bình phương tối thiểu."""
    ma, mb = a.mean(0), b.mean(0)
    A, B = a - ma, b - mb
    za = A[:, 0] + 1j * A[:, 1]
    zb = B[:, 0] + 1j * B[:, 1]
    k = (np.conj(za) @ zb) / (np.conj(za) @ za)
    def f(p):
        z = (p[:, 0] - ma[0]) + 1j * (p[:, 1] - ma[1])
        z = z * k
        return np.stack([z.real + mb[0], z.imag + mb[1]], 1)
    return f


class TPS:
    """Thin-plate spline: điểm ảnh mô hình -> điểm ảnh gốc."""

    def __init__(self, src, dst, lam=1e-3):
        n = len(src)
        self.src = src
        K = self._U(np.linalg.norm(src[:, None] - src[None], axis=2))
        P = np.hstack([np.ones((n, 1)), src])
        A = np.zeros((n + 3, n + 3))
        A[:n, :n] = K + lam * np.eye(n) * (np.mean(K[K > 0]) if np.any(K > 0) else 1.0)
        A[:n, n:] = P
        A[n:, :n] = P.T
        B = np.zeros((n + 3, 2))
        B[:n] = dst
        self.W = np.linalg.solve(A, B)

    @staticmethod
    def _U(r):
        with np.errstate(divide="ignore", invalid="ignore"):
            u = r * r * np.log(r)
        return np.nan_to_num(u)

    def __call__(self, pts):
        n = len(self.src)
        K = self._U(np.linalg.norm(pts[:, None] - self.src[None], axis=2))
        P = np.hstack([np.ones((len(pts), 1)), pts])
        return K @ self.W[:n] + P @ self.W[n:]


def _skin_dist(img, skin):
    return np.abs(img - np.asarray(skin)[None, None]).sum(2)


# ---------------------------------------------------------------- nắn hình khối mặt
def _head_setup(arm):
    mesh = mat3d._mesh_of(arm)[0]
    to_u = mat3d._actor_frame(arm)
    p, no = mat3d._rest_verts(mesh, to_u)
    std_of = body.bone_map(arm)
    inv = {v: k for k, v in std_of.items() if v}
    head_z = (to_u @ arm.matrix_world @ arm.data.bones[inv["head"]].head_local).z
    hv = p[p[:, 2] > head_z]
    box = (float(hv[:, 0].min()), float(hv[:, 0].max()), float(head_z), float(hv[:, 2].max()), float(hv[:, 1].min()))
    return mesh, to_u, p, no, head_z, box


def _profile(p, box, cx, cz, size):
    sel = p[(np.abs(p[:, 0] - cx) < 0.006) & (p[:, 2] > box[2] - 0.03) & (p[:, 2] < box[3])]
    bins = np.arange(box[2] - 0.03, box[3], 0.003)
    prof_y = []
    for b0 in bins:
        s_ = sel[(sel[:, 2] >= b0) & (sel[:, 2] < b0 + 0.003)]
        prof_y.append(float(s_[:, 1].min()) if len(s_) else None)
    prof_v = [(0.5 - ((b0 + 0.0015) - cz) / size) * R for b0 in bins]
    return prof_v, prof_y


def nan_hinh(arm, ref_path, out_npy, o_anh=(0, 0), muc=1.0, log=print):
    """Dời đỉnh lưới vùng mặt (mặt phẳng chính diện) cho mắt, lông mày, miệng, má, cằm về tỉ lệ ảnh gốc.
    Ghi độ dời (toạ độ lưới) ra out_npy; áp ngay vào lưới đang mở."""
    mesh, to_u, p, no, head_z, box = _head_setup(arm)
    mimg, (cx, cz, size) = mat3d._render_front(arm, mesh, box)
    mimg = mimg.astype(float)
    pm = mat3d._detect(mimg, *_profile(p, box, cx, cz, size))
    ref = ref_panel(load_rgb(ref_path), *o_anh)
    pr = mat3d._detect(ref)
    em, er = outline(mimg, pm, False), outline(ref, pr, True)
    src = landmarks(pm, em)
    dst_ref = landmarks(pr, er)
    # đưa ảnh gốc về cỡ đầu mô hình: đồng dạng theo mắt + miệng (giữ cỡ đầu, đổi tỉ lệ bên trong mặt)
    nm = len(src) - len(em)                                     # mắt 10, lông mày 6, miệng 3, rồi viền mặt
    core = list(range(0, 10)) + list(range(nm - 3, nm))
    S = similarity(dst_ref[core], src[core])
    dst = S(dst_ref)
    disp = (dst - src) * muc
    log("SGD_NAN_MAT", "dời mốc lớn nhất %.1f px (~%.1f mm)" % (np.abs(disp).max(), np.abs(disp).max() * size / R * 1000))
    tps = TPS(src, src + disp, lam=5e-3)
    e = pm["eyes"]
    gap = abs(e[1]["cx"] - e[0]["cx"])
    top = min(b["y0"] for b in pm["brows"]) - 0.25 * gap
    bot = em[-1][1]
    ecx, ecy = pm["face_cx"], (top + bot) / 2
    ry, rx = (bot - top) / 2, 1.05 * gap
    u = (p[:, 0] - cx) / size * R + R / 2
    v = (0.5 - (p[:, 2] - cz) / size) * R
    r = np.hypot((u - ecx) / rx, (v - ecy) / ry)
    w = 1 - np.clip((r - 0.95) / 0.5, 0, 1)
    w = w * w * (3 - 2 * w)
    ymid = (box[4] + p[p[:, 2] > head_z][:, 1].max()) / 2
    w *= np.clip((ymid + 0.025 - p[:, 1]) / 0.05, 0, 1)        # nửa trước đầu (mép má gần mặt cắt giữa vẫn được kéo)
    w *= (p[:, 2] > head_z - 0.06)
    q = tps(np.stack([u, v], 1))
    du, dv = (q[:, 0] - u) * w, (q[:, 1] - v) * w
    d_act = np.stack([du * size / R, np.zeros_like(du), -dv * size / R], 1)
    M = np.array((to_u @ mesh.matrix_world).inverted().to_3x3())
    d_loc = d_act @ M.T
    np.save(out_npy, d_loc.astype(np.float32))
    apply_offsets(mesh, d_loc)
    log("SGD_NAN_MAT", f"đã ghi {out_npy} ({int((w > 0.01).sum())} đỉnh)")
    return d_loc


def apply_offsets(mesh, d_loc):
    co = np.empty(len(mesh.data.vertices) * 3, np.float32)
    mesh.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3) + d_loc
    mesh.data.vertices.foreach_set("co", co.ravel())
    mesh.data.update()


# ---------------------------------------------------------------- chiếu
def chieu(arm, model_path, ref_path, out_path, o_anh=(0, 0), tron=0.8, log=print):
    mesh = mat3d._mesh_of(arm)[0]
    to_u = mat3d._actor_frame(arm)
    p, no = mat3d._rest_verts(mesh, to_u)
    std_of = body.bone_map(arm)
    inv = {v: k for k, v in std_of.items() if v}
    head_z = (to_u @ arm.matrix_world @ arm.data.bones[inv["head"]].head_local).z
    hv = p[p[:, 2] > head_z]
    box = (float(hv[:, 0].min()), float(hv[:, 0].max()), float(head_z), float(hv[:, 2].max()), float(hv[:, 1].min()))
    # ảnh da gốc (không phải ảnh đã chiếu lần trước)
    tex_node = next(n for m in mesh.data.materials for n in m.node_tree.nodes
                    if n.type == "TEX_IMAGE" and n.outputs[0].links and n.outputs[0].links[0].to_socket.name == "Base Color")
    img_tex = tex_node.image
    mimg, (cx, cz, size) = mat3d._render_front(arm, mesh, box)
    sel = p[(np.abs(p[:, 0] - cx) < 0.006) & (p[:, 2] > box[2] - 0.03) & (p[:, 2] < box[3])]
    bins = np.arange(box[2] - 0.03, box[3], 0.003)
    prof_y = []
    for b0 in bins:
        s_ = sel[(sel[:, 2] >= b0) & (sel[:, 2] < b0 + 0.003)]
        prof_y.append(float(s_[:, 1].min()) if len(s_) else None)
    prof_v = [(0.5 - ((b0 + 0.0015) - cz) / size) * R for b0 in bins]
    pm = mat3d._detect(mimg.astype(float), prof_v, prof_y)
    ref = ref_panel(load_rgb(ref_path), *o_anh)
    pr = mat3d._detect(ref)
    src, dst = landmarks(pm), landmarks(pr)
    tps = TPS(src, dst)
    err = np.linalg.norm(tps(src) - dst, axis=1).max()
    log("SGD_CHIEU_MAT", f"mốc {len(src)} điểm, sai lệch uốn {err:.1f} px")

    # vùng mặt trên ảnh mô hình: elip từ trên lông mày tới cằm, ngang tới má
    e = pm["eyes"]
    gap = abs(e[1]["cx"] - e[0]["cx"])
    top = min(b["y0"] for b in pm["brows"]) - 0.22 * gap
    bot = pm["cam"] if pm.get("cam") and pm["cam"] > pm["mouth"]["y"] + 0.2 * gap else pm["mouth"]["y"] + 0.75 * gap
    ecx = pm["face_cx"]
    ecy = (top + bot) / 2
    ry = (bot - top) / 2
    rx = 1.02 * gap
    skin_m = np.asarray(pm["skin"])
    skin_r = np.asarray(pr["skin"])
    dm = _skin_dist(mimg.astype(float), skin_m)
    dr = _skin_dist(ref, skin_r)
    feats_r = np.zeros(ref.shape[:2], bool)                # vùng mắt / lông mày / miệng của ảnh gốc: luôn lấy
    for ee in pr["eyes"]:
        feats_r[int(ee["cy"] - 2.2 * ee["b"]):int(ee["cy"] + 1.6 * ee["b"]),
                int(ee["cx"] - 1.5 * ee["a"]):int(ee["cx"] + 1.5 * ee["a"])] = True
    for bb in pr["brows"]:
        feats_r[int(bb["y0"]) - 6:int(bb["y1"]) + 6, int(bb["x0"]) - 6:int(bb["x1"]) + 6] = True
    m_ = pr["mouth"]
    feats_r[int(m_["y"] - 0.18 * gap):int(m_["y"] + 0.22 * gap), int(m_["x0"]) - 8:int(m_["x1"]) + 8] = True

    # tam giác vùng mặt trên ảnh da (UV)
    me = mesh.data
    me.calc_loop_triangles()
    T = img_tex.size[0]
    uv = np.empty(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    tri_l = np.empty(len(me.loop_triangles) * 3, np.int64)
    me.loop_triangles.foreach_get("loops", tri_l)
    tri_l = tri_l.reshape(-1, 3)
    tri_v = np.empty(len(me.loop_triangles) * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", tri_v)
    tri_v = tri_v.reshape(-1, 3)

    def to_img(P3):
        u = (P3[..., 0] - cx) / size * R + R / 2
        v = (0.5 - (P3[..., 2] - cz) / size) * R
        return u, v

    cen = p[tri_v].mean(1)
    nrm = np.cross(p[tri_v[:, 1]] - p[tri_v[:, 0]], p[tri_v[:, 2]] - p[tri_v[:, 0]])
    nl = np.linalg.norm(nrm, axis=1) + 1e-12
    facing = -nrm[:, 1] / nl                                 # mặt nhìn về -Y
    cu, cv = to_img(cen)
    rr = np.hypot((cu - ecx) / rx, (cv - ecy) / ry)
    keep = (cen[:, 2] > head_z) & (facing > 0.15) & (rr < 1.08) & (cen[:, 1] < box[4] + 0.12)
    log("SGD_CHIEU_MAT", f"{int(keep.sum())} tam giác vùng mặt")

    tex = np.empty(T * T * 4, np.float32)
    img_tex.pixels.foreach_get(tex)
    tex = tex.reshape(T, T, 4)                               # hàng 0 = dưới (theo Blender)
    pos_all, rc_all, fac_all = [], [], []
    for t in np.nonzero(keep)[0]:
        U = uv[tri_l[t]] * T                                 # (3,2) điểm ảnh da
        x0, y0 = np.floor(U.min(0) - 1).astype(int)
        x1, y1 = np.ceil(U.max(0) + 1).astype(int)
        xs, ys = np.meshgrid(np.arange(max(0, x0), min(T, x1)) + 0.5, np.arange(max(0, y0), min(T, y1)) + 0.5)
        if xs.size == 0:
            continue
        a, b, c = U
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        l3 = 1 - l1 - l2
        ins = (l1 > -0.04) & (l2 > -0.04) & (l3 > -0.04)       # nới chút để không hở mép tam giác
        if not ins.any():
            continue
        L = np.stack([l1[ins], l2[ins], l3[ins]], 1)
        P3 = L @ p[tri_v[t]]
        pos_all.append(P3)
        rc_all.append(np.stack([ys[ins] - 0.5, xs[ins] - 0.5], 1).astype(int))
        fac_all.append(np.full(len(P3), facing[t]))
    if not pos_all:
        raise RuntimeError("không thấy vùng mặt trên ảnh da")
    P3 = np.concatenate(pos_all)
    RC = np.concatenate(rc_all)
    FA = np.concatenate(fac_all)
    u, v = to_img(P3)
    r = np.hypot((u - ecx) / rx, (v - ecy) / ry)
    w = 1 - np.clip((r - 0.78) / 0.24, 0, 1)
    w = w * w * (3 - 2 * w)
    w *= np.clip((FA - 0.05) / 0.25, 0, 1)
    ui, vi = np.clip(u.astype(int), 0, R - 1), np.clip(v.astype(int), 0, R - 1)
    # tóc của mô hình (tối, khác da) ở vùng trán/thái dương: giữ nguyên
    lum_m = mimg[..., :3].astype(float) @ np.array([.3, .59, .11])
    hair_m = (dm[vi, ui] > 150) & (lum_m[vi, ui] < 0.55 * float(skin_m @ np.array([.3, .59, .11])))
    q = tps(np.stack([u, v], 1))
    qx, qy = q[:, 0], q[:, 1]
    qi, qj = np.clip(qx.astype(int), 0, R - 1), np.clip(qy.astype(int), 0, R - 1)
    lum_r = ref @ np.array([.3, .59, .11])
    hair_r = (dr[qj, qi] > 150) & (lum_r[qj, qi] < 0.55 * float(skin_r @ np.array([.3, .59, .11]))) & ~feats_r[qj, qi]
    w = np.where(hair_m & ~feats_r[qj, qi], w * 0.0, w)
    w = np.where(hair_r, w * 0.0, w)
    col = bilinear(ref, qx, qy) / 255.0                      # sRGB 0..1
    # chỉnh màu da ảnh gốc theo da mô hình (giữ phần lớn sắc riêng của ảnh gốc)
    cur = tex[RC[:, 0], RC[:, 1], :3]
    srgb_tex = img_tex.colorspace_settings.name.lower().startswith("srgb")
    if not srgb_tex:
        cur = np.where(cur <= 0.0031308, cur * 12.92, 1.055 * np.power(cur, 1 / 2.4) - 0.055)
    skin_sel = (w > 0.5) & (_skin_dist(col[None] * 255, skin_r)[0] < 70)
    if skin_sel.sum() > 50:
        mu_r = col[skin_sel].mean(0)
        mu_m = cur[skin_sel].mean(0)
        gain = np.clip(mu_m / np.maximum(mu_r, 1e-3), 0.7, 1.4)
        col = col * (1 - (1 - tron)) + col * gain * (1 - tron)
    out = cur * (1 - w[:, None]) + col * w[:, None]
    if not srgb_tex:
        out = np.where(out <= 0.04045, out / 12.92, ((out + 0.055) / 1.055) ** 2.4)
    tex[RC[:, 0], RC[:, 1], :3] = out
    newimg = bpy.data.images.new(os.path.basename(out_path), T, T, alpha=True)
    newimg.colorspace_settings.name = img_tex.colorspace_settings.name
    newimg.pixels.foreach_set(tex.ravel())
    newimg.filepath_raw = out_path
    newimg.file_format = "PNG"
    newimg.save()
    log("SGD_CHIEU_MAT", f"đã ghi {out_path} ({int((w > 0.05).sum())} điểm ảnh da)")
    return out_path


# ---------------------------------------------------------------- bản 68 điểm (iBUG, dò sẵn bằng moc_mat.py)
def save_moc(data_dir, code, moc):
    import json
    p = os.path.join(data_dir, "do_dac", f"{code}_moc_mat.json")
    with open(p, encoding="utf-8") as f:
        d = json.load(f)
    if "mo_hinh_m" in moc:
        d["mo_hinh_m"] = np.round(np.asarray(moc["mo_hinh_m"]), 5).tolist()
    with open(p, "w", encoding="utf-8") as f:
        json.dump(d, f, ensure_ascii=False)


def load_moc(data_dir, code):
    import json
    p = os.path.join(data_dir, "do_dac", f"{code}_moc_mat.json")
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8") as f:
        d = json.load(f)
    return {"goc": np.array(d["goc"], float), "mo_hinh": np.array(d["mo_hinh"], float), "o_anh": tuple(d.get("o_anh", (0, 0)))}


def _gap68(P):
    return float(np.linalg.norm(P[36:42].mean(0) - P[42:48].mean(0)))


def _face_ellipse68(P, top_k=0.30, bot_k=0.10):
    gap = _gap68(P)
    top = P[17:27, 1].min() - top_k * gap
    bot = P[8, 1] + bot_k * gap
    cxe = (P[0:17, 0].min() + P[0:17, 0].max()) / 2
    rx = (P[0:17, 0].max() - P[0:17, 0].min()) / 2 * 1.08
    return cxe, (top + bot) / 2, rx, (bot - top) / 2, gap


def _sample_skin(img, P):
    pts = [(P[36] + P[48]) / 2, (P[45] + P[54]) / 2, (P[31] + P[35]) / 2 + np.array([0, -0.1 * _gap68(P)])]
    vals = []
    for x, y in pts:
        x, y = int(x), int(y)
        vals.append(img[max(0, y - 5):y + 6, max(0, x - 5):x + 6].reshape(-1, 3))
    return np.median(np.concatenate(vals), axis=0)


def nan_hinh68(arm, moc, out_npy, muc=0.8, lam_muot=0.5, so_lan_muot=12, log=print):
    mesh, to_u, p, no, head_z, box = _head_setup(arm)
    _img, (cx, cz, size) = mat3d._render_front(arm, mesh, box)
    src, ref = moc["mo_hinh"], moc["goc"]
    core = list(range(27, 68))                                     # mũi, mắt, miệng: giữ cỡ đầu mô hình
    S = similarity(ref[core], src[core])
    dst = S(ref)
    gap = _gap68(src)
    disp = (dst - src) * muc
    lim = 0.35 * gap
    n_ = np.linalg.norm(disp, axis=1, keepdims=True)
    disp = np.where(n_ > lim, disp * lim / np.maximum(n_, 1e-9), disp)
    log("SGD_NAN_MAT", "dời mốc lớn nhất %.1f px (~%.1f mm)" % (float(n_.max()), float(n_.max()) * size / R * 1000))
    tps = TPS(src, src + disp, lam=1e-2)
    ecx, ecy, rx, ry, _ = _face_ellipse68(src, 0.35, 0.15)
    u = (p[:, 0] - cx) / size * R + R / 2
    v = (0.5 - (p[:, 2] - cz) / size) * R
    r = np.hypot((u - ecx) / rx, (v - ecy) / ry)
    w = 1 - np.clip((r - 0.95) / 0.45, 0, 1)
    w = w * w * (3 - 2 * w)
    ymid = (box[4] + p[p[:, 2] > head_z][:, 1].max()) / 2
    w *= np.clip((ymid + 0.025 - p[:, 1]) / 0.05, 0, 1)
    w *= (p[:, 2] > head_z - 0.06)
    q = tps(np.stack([u, v], 1))
    du, dv = (q[:, 0] - u) * w, (q[:, 1] - v) * w
    d_act = np.stack([du * size / R, np.zeros_like(du), -dv * size / R], 1)
    # làm mịn lòng mặt (Taubin, giữ thể tích): bỏ rãnh sâu + mặt phẳng gồ ghề kiểu tự động nặn, tránh vùng mắt
    q2 = p + d_act
    ws = np.clip((1.0 - r) / 0.25, 0, 1) * (p[:, 1] < ymid)
    for i0, i1 in ((36, 42), (42, 48)):
        E = src[i0:i1]
        ex, ey = E.mean(0)
        ea = (E[:, 0].max() - E[:, 0].min()) * 0.95
        eb = max((E[:, 1].max() - E[:, 1].min()) * 1.6, 0.25 * gap)
        re = np.hypot((u - ex) / ea, (v - ey) / eb)
        ws *= np.clip((re - 1.0) / 0.5, 0, 1)
    if lam_muot > 0 and ws.max() > 0:
        me = mesh.data
        ed = np.empty(len(me.edges) * 2, np.int64)
        me.edges.foreach_get("vertices", ed)
        ed = ed.reshape(-1, 2)
        # gộp đỉnh trùng chỗ (lưới tách đỉnh dọc đường nối ảnh da) -> làm mịn trên lưới liền, không xé đường nối
        key = np.round(p / 1e-5).astype(np.int64)
        _, uid = np.unique(key, axis=0, return_inverse=True)
        uid = uid.ravel()
        nu = int(uid.max()) + 1
        eu = uid[ed]
        eu = eu[eu[:, 0] != eu[:, 1]]
        eu = np.unique(np.sort(eu, axis=1), axis=0)
        cnt = np.bincount(uid, minlength=nu).astype(float)
        Xu = np.zeros((nu, 3))
        np.add.at(Xu, uid, q2)
        Xu /= cnt[:, None]
        wu = np.zeros(nu)
        np.maximum.at(wu, uid, ws)
        deg = np.bincount(eu.ravel(), minlength=nu).astype(float)
        deg[deg == 0] = 1
        X0 = Xu.copy()
        for k in range(int(so_lan_muot)):
            for fac in (lam_muot, -lam_muot - 0.03):
                acc = np.zeros_like(Xu)
                np.add.at(acc, eu[:, 0], Xu[eu[:, 1]])
                np.add.at(acc, eu[:, 1], Xu[eu[:, 0]])
                L = acc / deg[:, None] - Xu
                Xu = Xu + fac * wu[:, None] * L
        X = q2 + (Xu - X0)[uid]
        d_act = X - p
        log("SGD_NAN_MAT", "làm mịn lòng mặt: %d đỉnh, dời tối đa %.1f mm" % (int((ws > 0.01).sum()), float(np.linalg.norm(X - q2, axis=1).max()) * 1000))
    M = np.array((to_u @ mesh.matrix_world).inverted().to_3x3())
    d_loc = d_act @ M.T
    np.save(out_npy, d_loc.astype(np.float32))
    apply_offsets(mesh, d_loc)
    log("SGD_NAN_MAT", f"đã ghi {out_npy} ({int((w > 0.01).sum())} đỉnh)")
    return src + disp                                              # vị trí mốc mô hình sau khi nắn


LBF_PATHS = [os.environ.get("SGD_LBF", ""), "/tmp/lbf/lbfmodel.yaml"]
HAAR_PATHS = [os.environ.get("SGD_HAAR", ""), "/tmp/lbf/haar.xml"]


def _lbf_detect(img):
    """68 điểm bằng OpenCV LBF nếu có (môi trường dựng tài sản); máy dựng phim không có thì trả None."""
    try:
        import cv2
    except Exception:
        return None
    lbf = next((p for p in LBF_PATHS if p and os.path.exists(p)), None)
    haar = next((p for p in HAAR_PATHS if p and os.path.exists(p)), None)
    if not lbf or not haar or not hasattr(cv2, "face"):
        return None
    fm = cv2.face.createFacemarkLBF()
    fm.loadModel(lbf)
    g = cv2.cvtColor(np.clip(img, 0, 255).astype(np.uint8), cv2.COLOR_RGB2GRAY)
    faces = cv2.CascadeClassifier(haar).detectMultiScale(g, 1.1, 3, minSize=(R // 4, R // 4))
    if len(faces) == 0:
        return None
    f = max(faces, key=lambda r: r[2] * r[3])
    ok, lm = fm.fit(g, np.array([f], dtype=np.int32))
    return np.array(lm[0]).reshape(-1, 2).astype(float) if ok else None


def chieu68(arm, moc, ref_path, out_path, src_after=None, tron=0.85, log=print):
    mesh, to_u, p, no, head_z, box = _head_setup(arm)
    tex_node = next(n for m in mesh.data.materials for n in m.node_tree.nodes
                    if n.type == "TEX_IMAGE" and n.outputs[0].links and n.outputs[0].links[0].to_socket.name == "Base Color")
    img_tex = tex_node.image
    mimg, (cx, cz, size) = mat3d._render_front(arm, mesh, box)
    mimg = np.asarray(mimg, float)[..., :3]
    src = moc["mo_hinh"] if src_after is None else src_after
    # dò lại 68 điểm trên chính hình đã nắn (nếu Python này có OpenCV + mô hình LBF) -> căn da chính xác
    redo = _lbf_detect(mimg)
    if redo is not None:
        d_ = float(np.abs(redo - src).max())
        log("SGD_CHIEU_MAT", f"dò lại mốc sau nắn: lệch so với dự tính {d_:.1f} px")
        src = redo
        moc["mo_hinh_sau"] = redo
        # mốc theo mét (khung diễn viên, x ngang, z cao) — mat3d dùng để đặt mí mắt / miệng / lông mày
        moc["mo_hinh_m"] = np.stack([cx + (redo[:, 0] / R - 0.5) * size, cz + (0.5 - redo[:, 1] / R) * size], 1)
    refpts = moc["goc"]
    ref = ref_panel(load_rgb(ref_path), *moc["o_anh"])
    tps = TPS(src, refpts, lam=1e-3)
    ecx, ecy, rx, ry, gap = _face_ellipse68(src, 0.28, 0.05)
    skin_m, skin_r = _sample_skin(mimg, src), _sample_skin(ref, refpts)
    dm, dr = _skin_dist(mimg, skin_m), _skin_dist(ref, skin_r)
    feats_r = np.zeros(ref.shape[:2], bool)
    for a_, b_, pad in ((36, 42, 0.18), (42, 48, 0.18), (17, 22, 0.08), (22, 27, 0.08), (48, 60, 0.10)):
        P = refpts[a_:b_]
        g_ = pad * gap
        feats_r[int(P[:, 1].min() - g_):int(P[:, 1].max() + g_), int(P[:, 0].min() - g_):int(P[:, 0].max() + g_)] = True
    me = mesh.data
    me.calc_loop_triangles()
    T = img_tex.size[0]
    uv = np.empty(len(me.loops) * 2, np.float32)
    me.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    tri_l = np.empty(len(me.loop_triangles) * 3, np.int64)
    me.loop_triangles.foreach_get("loops", tri_l)
    tri_l = tri_l.reshape(-1, 3)
    tri_v = np.empty(len(me.loop_triangles) * 3, np.int64)
    me.loop_triangles.foreach_get("vertices", tri_v)
    tri_v = tri_v.reshape(-1, 3)

    def to_img(P3):
        return (P3[..., 0] - cx) / size * R + R / 2, (0.5 - (P3[..., 2] - cz) / size) * R

    cen = p[tri_v].mean(1)
    nrm = np.cross(p[tri_v[:, 1]] - p[tri_v[:, 0]], p[tri_v[:, 2]] - p[tri_v[:, 0]])
    facing = -nrm[:, 1] / (np.linalg.norm(nrm, axis=1) + 1e-12)
    cu, cv = to_img(cen)
    rr = np.hypot((cu - ecx) / rx, (cv - ecy) / ry)
    keep = (cen[:, 2] > head_z - 0.03) & (facing > -0.1) & (rr < 1.12) & (cen[:, 1] < box[4] + 0.12)
    tex = np.empty(T * T * 4, np.float32)
    img_tex.pixels.foreach_get(tex)
    tex = tex.reshape(T, T, 4)
    pos_all, rc_all, fac_all = [], [], []
    for t in np.nonzero(keep)[0]:
        U = uv[tri_l[t]] * T
        x0, y0 = np.floor(U.min(0) - 1).astype(int)
        x1, y1 = np.ceil(U.max(0) + 1).astype(int)
        xs, ys = np.meshgrid(np.arange(max(0, x0), min(T, x1)) + 0.5, np.arange(max(0, y0), min(T, y1)) + 0.5)
        if xs.size == 0:
            continue
        a, b, c = U
        den = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(den) < 1e-12:
            continue
        l1 = ((b[1] - c[1]) * (xs - c[0]) + (c[0] - b[0]) * (ys - c[1])) / den
        l2 = ((c[1] - a[1]) * (xs - c[0]) + (a[0] - c[0]) * (ys - c[1])) / den
        l3 = 1 - l1 - l2
        ins = (l1 > -0.04) & (l2 > -0.04) & (l3 > -0.04)
        if not ins.any():
            continue
        L = np.stack([l1[ins], l2[ins], l3[ins]], 1)
        pos_all.append(L @ p[tri_v[t]])
        rc_all.append(np.stack([ys[ins] - 0.5, xs[ins] - 0.5], 1).astype(int))
        fac_all.append(np.full(int(ins.sum()), facing[t]))
    P3 = np.concatenate(pos_all)
    RC = np.concatenate(rc_all)
    FA = np.concatenate(fac_all)
    u, v = to_img(P3)
    r = np.hypot((u - ecx) / rx, (v - ecy) / ry)
    w = 1 - np.clip((r - 0.80) / 0.25, 0, 1)
    w = w * w * (3 - 2 * w)
    # trong lòng mặt (r < 0.8) phủ đủ cả chỗ nghiêng (cánh mũi, khoé miệng) để không lộ nếp da cũ;
    # chỉ ở viền ngoài mới giảm theo độ nghiêng
    inner = np.clip((0.95 - r) / 0.15, 0, 1)
    w *= inner + (1 - inner) * np.clip((FA + 0.05) / 0.25, 0, 1)
    ui, vi = np.clip(u.astype(int), 0, R - 1), np.clip(v.astype(int), 0, R - 1)
    sl_m = float(skin_m @ np.array([.3, .59, .11]))
    sl_r = float(skin_r @ np.array([.3, .59, .11]))
    lum_m = mimg @ np.array([.3, .59, .11])
    q = tps(np.stack([u, v], 1))
    qx, qy = q[:, 0], q[:, 1]
    qi, qj = np.clip(qx.astype(int), 0, R - 1), np.clip(qy.astype(int), 0, R - 1)
    lum_r = ref @ np.array([.3, .59, .11])
    fr = feats_r[qj, qi]
    hair_m = (dm[vi, ui] > 150) & (lum_m[vi, ui] < 0.55 * sl_m) & ~fr
    hair_r = (dr[qj, qi] > 150) & (lum_r[qj, qi] < 0.55 * sl_r) & ~fr
    bg_r = (ref[qj, qi, 0] - ref[qj, qi, 2] < 30) & ~fr          # nền xám của ảnh gốc
    w = np.where(hair_m | hair_r | bg_r, 0.0, w)
    col = bilinear(ref, qx, qy) / 255.0
    cur = tex[RC[:, 0], RC[:, 1], :3]
    srgb_tex = img_tex.colorspace_settings.name.lower().startswith("srgb")
    if not srgb_tex:
        cur = np.where(cur <= 0.0031308, cur * 12.92, 1.055 * np.power(np.maximum(cur, 0), 1 / 2.4) - 0.055)
    sel = (w > 0.6) & ~fr
    if sel.sum() > 50:
        gain = np.clip(np.median(cur[sel], 0) / np.maximum(np.median(col[sel], 0), 1e-3), 0.75, 1.3)
        col = col * tron + col * gain * (1 - tron)
    out = cur * (1 - w[:, None]) + col * w[:, None]
    if not srgb_tex:
        out = np.where(out <= 0.04045, out / 12.92, ((out + 0.055) / 1.055) ** 2.4)
    tex[RC[:, 0], RC[:, 1], :3] = out
    newimg = bpy.data.images.new(os.path.basename(out_path), T, T, alpha=True)
    newimg.colorspace_settings.name = img_tex.colorspace_settings.name
    newimg.pixels.foreach_set(tex.ravel())
    newimg.filepath_raw = out_path
    newimg.file_format = "PNG"
    newimg.save()
    log("SGD_CHIEU_MAT", f"đã ghi {out_path} ({int((w > 0.05).sum())} điểm ảnh da, 68 mốc)")
    return out_path
