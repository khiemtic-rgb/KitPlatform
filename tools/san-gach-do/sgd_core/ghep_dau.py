"""Ghép đầu 3D riêng (dựng từ ảnh chân dung, đã sửa theo ảnh gốc: <mô hình>_dau.glb) lên thân đã gắn xương.

  - cằm (đường nghiêng giữa mặt) của đầu mới khớp cằm cũ; cỡ theo chiều cao cằm -> đỉnh đầu cũ;
  - cắt phẳng ngay dưới cằm: đầu mới giữ phần trên (không lấy cổ áo của tượng bán thân),
    thân giữ phần dưới (cổ thân sạch, nối vào cổ áo thân) + bỏ tóc / cằm cũ thấp dưới mặt cắt;
  - kéo cổ thân (chỉ đỉnh da cổ) theo hướng bán kính cho mép trên trùng mép cắt đầu mới, nhạt dần xuống;
  - trọng số đầu mới: sát mép chép từ đỉnh cổ thân gần nhất, lên dần thành 100% xương đầu;
  - nhập vào lưới thân (một lưới: mat3d vẫn chạy); vật liệu đầu đi theo các mặt của đầu.
"""
import os

import bmesh
import bpy
import numpy as np


def head_path(model_path):
    return os.path.splitext(model_path)[0] + "_dau.glb"


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _world(o):
    n = len(o.data.vertices)
    co = np.empty(n * 3)
    o.data.vertices.foreach_get("co", co)
    M = np.array(o.matrix_world)
    return co.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3]


def _set_world(o, P):
    Mi = np.linalg.inv(np.array(o.matrix_world))
    o.data.vertices.foreach_set("co", (P @ Mi[:3, :3].T + Mi[:3, 3]).astype(np.float32).ravel())
    o.data.update()


def _slice_shape(P, z, dz, axis_xy, rmax):
    s = P[(np.abs(P[:, 2] - z) < dz) & (np.linalg.norm(P[:, :2] - axis_xy, axis=1) < rmax)]
    if len(s) < 8:
        return None
    lo, hi = np.percentile(s[:, :2], 3, axis=0), np.percentile(s[:, :2], 97, axis=0)
    return (lo + hi) / 2, (hi[0] - lo[0]) / 2, (hi[1] - lo[1]) / 2


def _chin_z(P, cx, z_lo, z_face, z_hi):
    """Cằm theo đường nghiêng giữa mặt (-Y là phía mặt). Độ nhô mặt = trung vị trong [z_face, z_hi], độ nhô cổ =
    nhỏ nhất trong [z_lo, z_face]; dò từ z_face đi xuống, chỗ đầu tiên tụt dưới mức giữa hai cái là đáy cằm."""
    band = P[(P[:, 2] > z_face) & (P[:, 2] < z_hi)]
    if len(band) < 10:
        return None
    w = band[:, 0].max() - band[:, 0].min()
    sel = P[np.abs(P[:, 0] - cx) < 0.12 * w]
    step = (z_hi - z_lo) / 90
    zs = np.arange(z_lo, z_hi, step)
    f = np.full(len(zs), np.nan)
    for i, z in enumerate(zs):
        s_ = sel[np.abs(sel[:, 2] - z) < step]
        if len(s_):
            f[i] = -s_[:, 1].min()
    face = np.nanmedian(f[zs >= z_face])
    neck = np.nanmin(f[zs < z_face])
    mid = (face + neck) / 2
    for i in range(int(np.searchsorted(zs, z_face)), 0, -1):
        if not np.isnan(f[i]) and f[i] < mid:
            return float(zs[i] + step / 2)
    return None


def _bisect(obj, plane_z, keep_above, extra_del=None, only=None):
    """Cắt phẳng ngang z = plane_z (thế giới); only: mặt nạ đỉnh được cắt (mặt có đủ đỉnh trong đó).
    Trả toạ độ thế giới các đỉnh mép cắt."""
    M = obj.matrix_world.copy()
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.transform(M)
    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        v.tag = bool(only[v.index]) if only is not None else True
    if extra_del is not None and extra_del.any():
        bmesh.ops.delete(bm, geom=[bm.verts[i] for i in np.nonzero(extra_del)[0]], context="VERTS")
    faces = [f for f in bm.faces if all(v.tag for v in f.verts)]
    verts = list({v for f in faces for v in f.verts})
    edges = list({e for f in faces for e in f.edges})
    r = bmesh.ops.bisect_plane(bm, geom=verts + edges + faces, dist=1e-7,
                               plane_co=(0, 0, plane_z), plane_no=(0, 0, 1),
                               clear_inner=keep_above, clear_outer=not keep_above)
    edge = np.array([v.co[:] for v in r["geom_cut"] if isinstance(v, bmesh.types.BMVert)])
    bm.transform(M.inverted())
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return edge if len(edge) else np.zeros((0, 3))


def _smooth_neck(body, z_p, zn, cxy, rx, ry, it=8):
    """Làm mượt lưới da cổ thân (Meshy gãy góc), giữ mép cắt và cổ áo; bật bóng mịn."""
    M = body.matrix_world.copy()
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.transform(M)
    sel = []
    for v in bm.verts:
        x, y, z = v.co
        r = ((x - cxy[0]) / rx) ** 2 + ((y - cxy[1]) / ry) ** 2
        if zn - 0.03 < z < z_p - 0.002 and r < 1.12 ** 2:
            sel.append(v)
    bmesh.ops.remove_doubles(bm, verts=sel, dist=1e-5)    # đỉnh trùng ở đường nối UV: hàn lại kẻo bị kéo rách
    sel = [v for v in sel if v.is_valid]
    sset = set(sel)
    for f in bm.faces:
        if any(v in sset for v in f.verts):
            f.smooth = True
    for _ in range(it):
        bmesh.ops.smooth_vert(bm, verts=sel, factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm.transform(M.inverted())
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()


def _polar(E, c):
    d = E[:, :2] - c
    th = np.arctan2(d[:, 1], d[:, 0])
    o = np.argsort(th)
    th, rr = th[o], np.linalg.norm(d, axis=1)[o]
    return np.concatenate([th - 2 * np.pi, th, th + 2 * np.pi]), np.tile(rr, 3)


def _vertex_is_skin(o):
    """Đỉnh mang màu da (ảnh màu tại UV): đỏ > lục > lam, đủ bão hoà. None nếu không đọc được ảnh."""
    img = _tex_of(o)
    if img is None or not o.data.uv_layers:
        return None
    W, H = img.size
    px = np.array(img.pixels[:]).reshape(H, W, img.channels)[:, :, :3]
    n, nl = len(o.data.vertices), len(o.data.loops)
    uv = np.empty(nl * 2)
    o.data.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    vi = np.empty(nl, np.int64)
    o.data.loops.foreach_get("vertex_index", vi)
    c = px[np.clip((uv[:, 1] * H).astype(int), 0, H - 1), np.clip((uv[:, 0] * W).astype(int), 0, W - 1)]
    mx, mn = c.max(1), c.min(1)
    ok = (c[:, 0] > c[:, 1]) & (c[:, 1] > c[:, 2]) & ((mx - mn) / np.maximum(mx, 1e-3) > 0.38) & (mx > 0.3)
    cnt = np.bincount(vi, minlength=n)
    good = np.bincount(vi, weights=ok.astype(float), minlength=n)
    return (good >= 0.5 * np.maximum(cnt, 1)).astype(float)


def _tex_of(o):
    for m in o.data.materials:
        if m and m.use_nodes:
            b_ = m.node_tree.nodes.get("Principled BSDF")
            if b_ is None:
                continue
            for sock in ("Base Color", "Emission Color"):
                if b_.inputs[sock].links and b_.inputs[sock].links[0].from_node.type == "TEX_IMAGE":
                    return b_.inputs[sock].links[0].from_node.image
    return None


def _band_colour(o, img, z0, z1):
    """Màu trung vị (ảnh, 0..1) của các đỉnh trong dải độ cao [z0, z1]."""
    P = _world(o)
    sel = (P[:, 2] >= z0) & (P[:, 2] <= z1)
    if sel.sum() < 10 or img is None or not o.data.uv_layers:
        return None
    W, H = img.size
    px = np.array(img.pixels[:]).reshape(H, W, img.channels)[:, :, :3]
    nl = len(o.data.loops)
    uv = np.empty(nl * 2)
    o.data.uv_layers.active.data.foreach_get("uv", uv)
    uv = uv.reshape(-1, 2)
    vi = np.empty(nl, np.int64)
    o.data.loops.foreach_get("vertex_index", vi)
    m = sel[vi]
    X = np.clip((uv[m, 0] * W).astype(int), 0, W - 1)
    Y = np.clip((uv[m, 1] * H).astype(int), 0, H - 1)
    c = px[Y, X]
    mx, mn = c.max(1), c.min(1)
    sat = (mx - mn) / np.maximum(mx, 1e-3)
    skin = (c[:, 0] > c[:, 1]) & (c[:, 1] > c[:, 2]) & (mx > 0.3) & (sat > 0.38)
    return np.median(c[skin], axis=0) if skin.sum() > 10 else None


def _neck_chain(nt, src, bsdf, ch_lin, keep=0.15):
    """Base Color = trộn(src, màu da cổ mịn ch_lin giữ `keep` sáng tối của src, theo thuộc tính sgd_co)."""
    a_co = nt.nodes.new("ShaderNodeAttribute")
    a_co.attribute_name = "sgd_co"
    a_co.name = a_co.label = "GHEP_CoMin"
    bw = nt.nodes.new("ShaderNodeRGBToBW")
    bw.name = bw.label = "GHEP_CoSang"
    nt.links.new(src, bw.inputs[0])
    lum0 = float(np.dot(ch_lin, [0.2126, 0.7152, 0.0722]))
    sc_ = nt.nodes.new("ShaderNodeMath")
    sc_.operation = "MULTIPLY_ADD"
    sc_.name = sc_.label = "GHEP_CoTiLe"
    nt.links.new(bw.outputs[0], sc_.inputs[0])
    sc_.inputs[1].default_value = keep / max(lum0, 1e-3)
    sc_.inputs[2].default_value = 1 - keep
    flat = nt.nodes.new("ShaderNodeMix")
    flat.data_type = "RGBA"
    flat.blend_type = "MULTIPLY"
    flat.name = flat.label = "GHEP_CoMau"
    flat.inputs["Factor"].default_value = 1.0
    flat.inputs[6].default_value = (*ch_lin.tolist(), 1.0)
    nt.links.new(sc_.outputs[0], flat.inputs[7])
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.name = mix.label = "GHEP_CoTron"
    nt.links.new(a_co.outputs["Fac"], mix.inputs["Factor"])
    nt.links.new(src, mix.inputs[6])
    nt.links.new(flat.outputs[2], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])


def _match_skin(body, head, z_p, masks):
    """Đưa màu da thân về màu da đầu mới (đo hai bên mép cắt) bằng hệ số nhân trong vật liệu, chỉ ở các đỉnh
    da (thuộc tính sgd_da: cổ + bàn tay) -- ảnh màu thân không đổi, áo không bị nhuộm."""
    ib, ih = _tex_of(body), _tex_of(head)
    if ib is None or ih is None:
        return
    cb = _band_colour(body, ib, z_p - 0.03, z_p)
    ch = _band_colour(head, ih, z_p, z_p + 0.02)
    if cb is None or ch is None:
        return
    ratio = np.clip(ch / np.maximum(cb, 1e-3), 0.6, 1.6)
    skin_mask, neck_mask = masks
    for nm, mk in (("sgd_da", skin_mask), ("sgd_co", neck_mask)):
        at = body.data.attributes.get(nm) or body.data.attributes.new(nm, "FLOAT", "POINT")
        at.data.foreach_set("value", mk.astype(np.float32))
    ch_lin = np.where(ch <= 0.04045, ch / 12.92, ((ch + 0.055) / 1.055) ** 2.4)
    for m in body.data.materials:
        if not (m and m.use_nodes):
            continue
        nt = m.node_tree
        b_ = nt.nodes.get("Principled BSDF")
        if b_ is None or not b_.inputs["Base Color"].links:
            continue
        src = b_.inputs["Base Color"].links[0].from_socket
        attr = nt.nodes.new("ShaderNodeAttribute")
        attr.attribute_name = "sgd_da"
        attr.name = attr.label = "GHEP_DaKhop"
        mul = nt.nodes.new("ShaderNodeMix")
        mul.data_type = "RGBA"
        mul.blend_type = "MULTIPLY"
        mul.name = mul.label = "GHEP_DaKhopNhan"
        nt.links.new(attr.outputs["Fac"], mul.inputs["Factor"])
        nt.links.new(src, mul.inputs[6])
        mul.inputs[7].default_value = (*ratio.tolist(), 1.0)
        _neck_chain(nt, mul.outputs[2], b_, ch_lin)
    # mép dưới cổ đầu mới hoà cùng màu đó (không còn đường ranh)
    Ph_ = _world(head)
    at = head.data.attributes.get("sgd_co") or head.data.attributes.new("sgd_co", "FLOAT", "POINT")
    at.data.foreach_set("value", _smooth(z_p + 0.018, z_p + 0.002, Ph_[:, 2]).astype(np.float32))
    for m in head.data.materials:
        if m and m.use_nodes:
            b_ = m.node_tree.nodes.get("Principled BSDF")
            if b_ is not None and b_.inputs["Base Color"].links:
                _neck_chain(m.node_tree, b_.inputs["Base Color"].links[0].from_socket, b_, ch_lin)
    print("SGD_GHEP_DAU_DA", dict(than=(cb * 255).round().tolist(), dau=(ch * 255).round().tolist()))


def _skin_vertices(body, arm, std, z_p, zn, cxy, rx, ry):
    """Đỉnh da của thân sau khi cắt: cổ (gần trục, trên gốc cổ một chút) + bàn tay (theo xương tay)."""
    P = _world(body)
    rad = np.sqrt(((P[:, 0] - cxy[0]) / rx) ** 2 + ((P[:, 1] - cxy[1]) / ry) ** 2)
    neck = (rad < 1.12) & (P[:, 2] > zn - 0.06) & (P[:, 2] <= z_p + 1e-4)
    gname = {g.index: g.name for g in body.vertex_groups}
    hands = set()
    for side in ("L", "R"):
        for part in ("hand", "foot", "toe"):          # bàn tay + bàn chân trần
            hb = std.get(f"{part}.{side}")
            if hb and hb in arm.data.bones:
                hands |= {hb} | {b.name for b in arm.data.bones[hb].children_recursive}
    w = np.zeros(len(P))
    for v in body.data.vertices:
        for ge in v.groups:
            if gname.get(ge.group) in hands:
                w[v.index] += ge.weight
    m_neck = neck * _smooth(zn - 0.06, zn - 0.02, P[:, 2]) * _smooth(1.12, 1.0, rad)
    sk = _vertex_is_skin(body)                     # chỉ đỉnh mang màu da (áo vàng nhạt / giày không bị nhuộm)
    if sk is not None:
        m_neck = m_neck * sk
        w = w * sk
    return np.maximum(m_neck, np.clip(w, 0, 1)), m_neck


def apply(arm, model_path, bone_map):
    """Có <mô hình>_dau.glb thì ghép. Trả True nếu đã ghép."""
    hp = head_path(model_path)
    if not os.path.exists(hp):
        return False
    std = {v: k for k, v in bone_map(arm).items() if v}
    if "head" not in std or "neck" not in std:
        return False
    meshes = [o for o in bpy.data.objects if o.type == "MESH" and
              (o.parent == arm or any(m.type == "ARMATURE" and m.object == arm for m in o.modifiers))]
    if not meshes:
        return False
    body = max(meshes, key=lambda o: len(o.data.vertices))
    hb, nb = arm.data.bones[std["head"]], arm.data.bones[std["neck"]]
    zh = (arm.matrix_world @ hb.head_local).z
    zn = (arm.matrix_world @ nb.head_local).z
    axis = (np.array((arm.matrix_world @ nb.head_local)[:2]) + np.array((arm.matrix_world @ hb.head_local)[:2])) / 2

    # --- thân
    Pb = _world(body)
    top_old = float(Pb[:, 2].max())
    gname = {g.index: g.name for g in body.vertex_groups}
    head_set = {hb.name} | {b.name for b in hb.children_recursive}
    w_head = np.zeros(len(Pb))
    for v in body.data.vertices:
        for ge in v.groups:
            if gname.get(ge.group) in head_set:
                w_head[v.index] += ge.weight
    near_axis = np.linalg.norm(Pb[:, :2] - axis, axis=1) < 0.15
    z_chin_b = _chin_z(Pb[(Pb[:, 2] > zn) & near_axis], axis[0], zn, zh + 0.25 * (top_old - zh), zh + 0.5 * (top_old - zh))
    if z_chin_b is None:
        return False
    z_p = z_chin_b - 0.12 * (z_chin_b - zn)                # mặt cắt: ngay dưới cằm
    sh = _slice_shape(Pb, z_p - 0.004, 0.004, axis, 0.12)
    if sh is None:
        return False
    cxy_b, rx_b, ry_b = sh

    # --- đầu mới: khớp cằm + cỡ
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=hp)
    new = [o for o in bpy.data.objects if o not in before]
    hm = [o for o in new if o.type == "MESH" and len(o.data.vertices) > 100]
    for o in new:
        if o not in hm:
            bpy.data.objects.remove(o, do_unlink=True)
    if not hm:
        return False
    head = max(hm, key=lambda o: len(o.data.vertices))
    for o in hm:
        if o is not head:
            bpy.data.objects.remove(o, do_unlink=True)
    head.parent = None
    Ph = _world(head)
    zmin, zmax = Ph[:, 2].min(), Ph[:, 2].max()
    Hh = zmax - zmin
    cxh = np.median(Ph[:, :2], axis=0)
    z_chin_h = _chin_z(Ph, cxh[0], zmin + 0.15 * Hh, zmin + 0.42 * Hh, zmin + 0.6 * Hh)
    if z_chin_h is None:
        return False
    k = (top_old - z_chin_b) / (zmax - z_chin_h)
    zh_cut = z_chin_h - (z_chin_b - z_p) / k
    sh_h = _slice_shape(Ph, zh_cut - 0.004 / k, 0.004 / k, cxh, 0.3 * Hh)
    c_h = sh_h[0] if sh_h else cxh                        # tâm cổ đầu mới ngay dưới cằm
    Q = (Ph - np.array([c_h[0], c_h[1], z_chin_h])) * k + np.array([cxy_b[0], cxy_b[1], z_chin_b])
    head.matrix_world = body.matrix_world.copy()
    _set_world(head, Q)
    print("SGD_GHEP_DAU", dict(cam_than=round(float(z_chin_b), 3), mat_cat=round(float(z_p), 3),
                               cam_dau=round(float((z_chin_h - zmin) / Hh), 3), ti_le=round(float(k), 4)))

    # --- trọng số cổ thân gần mặt cắt (đọc trước khi cắt: chỉ số đỉnh còn nguyên)
    rad_b = np.sqrt(((Pb[:, 0] - cxy_b[0]) / rx_b) ** 2 + ((Pb[:, 1] - cxy_b[1]) / ry_b) ** 2)
    old_low = (w_head > 0.6) & (rad_b > 1.05) & (Pb[:, 2] > zn) & (Pb[:, 2] <= z_p)   # tóc / cằm cũ thấp
    near_idx = np.nonzero(~old_low & (Pb[:, 2] <= z_p) & (Pb[:, 2] > z_p - 0.03) & (rad_b < 1.3))[0]
    vw = {i: [(gname[ge.group], ge.weight) for ge in body.data.vertices[i].groups if ge.weight > 0.01]
          for i in near_idx}
    P_near = Pb[near_idx]

    # --- cắt
    neckhead = (rad_b < 1.25) | (w_head > 0.5)               # cổ áo / áo (xa trục, không theo đầu) giữ nguyên
    edge_b = _bisect(body, z_p, keep_above=False, extra_del=old_low, only=neckhead)
    edge_b = edge_b[np.sqrt(((edge_b[:, 0] - cxy_b[0]) / rx_b) ** 2 + ((edge_b[:, 1] - cxy_b[1]) / ry_b) ** 2) < 1.25] \
        if len(edge_b) else edge_b
    edge_h = _bisect(head, z_p, keep_above=True)
    Qk = _world(head)

    # --- kéo cổ thân cho mép trên trùng mép cắt đầu mới
    if len(edge_b) >= 8 and len(edge_h) >= 8:
        tb, rb = _polar(edge_b, cxy_b)
        th_, rh = _polar(edge_h, cxy_b)
        P2 = _world(body)
        d = P2[:, :2] - cxy_b
        ang = np.arctan2(d[:, 1], d[:, 0])
        rr = np.linalg.norm(d, axis=1) + 1e-9
        r_edge = np.interp(ang, tb, rb)
        dr = np.interp(ang, th_, rh) - r_edge
        D = 0.55 * (z_p - zn)
        fade = _smooth(z_p - D, z_p, P2[:, 2]) * (rr < 1.15 * r_edge)       # chỉ da cổ, không đụng cổ áo
        kk = (rr + dr * fade) / rr
        P2[:, 0] = cxy_b[0] + d[:, 0] * kk
        P2[:, 1] = cxy_b[1] + d[:, 1] * kk
        _set_world(body, P2)
    _smooth_neck(body, z_p, zn, cxy_b, rx_b, ry_b)

    # --- trọng số đầu mới
    z_full = z_p + 0.35 * (top_old - z_p)
    groups = {}
    for v in head.data.vertices:
        p = Qk[v.index]
        t_ = float(_smooth(z_p, z_full, p[2]))
        acc = {}
        if t_ < 1 and len(near_idx):
            j = near_idx[int(np.argmin(np.linalg.norm(P_near - p, axis=1)))]
            tot = sum(w for _, w in vw[j]) or 1
            for g, w in vw[j]:
                acc[g] = acc.get(g, 0) + (1 - t_) * w / tot
        acc[hb.name] = acc.get(hb.name, 0) + t_
        for g, w in acc.items():
            groups.setdefault(g, []).append((v.index, w))
    for g, lst in groups.items():
        vg = head.vertex_groups.get(g) or head.vertex_groups.new(name=g)
        for i, w in lst:
            vg.add([i], w, "REPLACE")

    _match_skin(body, head, z_p, _skin_vertices(body, arm, std, z_p, zn, cxy_b, rx_b, ry_b))

    # --- nhập vào lưới thân
    if body.data.uv_layers and head.data.uv_layers:
        head.data.uv_layers[0].name = body.data.uv_layers.active.name
    for m in head.data.materials:
        if m and m.use_nodes:
            b_ = m.node_tree.nodes.get("Principled BSDF")
            if b_:
                b_.inputs["Specular IOR Level"].default_value = min(b_.inputs["Specular IOR Level"].default_value, 0.2)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    with bpy.context.temp_override(active_object=body, object=body, selected_objects=[body, head],
                                   selected_editable_objects=[body, head]):
        bpy.ops.object.join()
    body["sgd_dau_rieng"] = os.path.basename(hp)
    return True
