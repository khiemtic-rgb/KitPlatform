"""Mặt cho mô hình 3D (Fal/Meshy...): chớp mắt, mở miệng theo thoại, lông mày — dùng chung mọi nhân vật.

Mô hình dựng từ ảnh là một khối lưới liền, mắt/miệng là hình vẽ trên bề mặt. Lớp này:
  1. chụp thẳng mặt ở tư thế nghỉ, dò mắt / miệng / lông mày trên ảnh (bù đối xứng khi một bên dò hụt);
  2. ghi lên từng đỉnh lưới toạ độ "trong mắt", "trong miệng" (thuộc tính đỉnh -> bám chặt bề mặt khi đầu cử động);
  3. mí mắt = màu da phủ dần từ trên xuống + viền mi (trong vật liệu), miệng = môi dưới/cằm hạ thật (shape key "Mo")
     + khe miệng tối và răng trên (trong vật liệu), lông mày = shape key "Nhuong", "Lo" trên lưới.
FaceRig3D có cùng hàm điều khiển với lớp mặt 2D (set_blink, set_mouth, set_brow, ...) nên anim.idle,
anim.mouth_from_audio, anim.expressions dùng được nguyên.

Kết quả dò lưu ở data/do_dac/<Ma>_mat3d.json (dò lại khi file mô hình đổi hoặc cách dò đổi, trừ khi "khoa": true để chỉnh tay).
"""
import json
import math
import os
import tempfile

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

FRONT_RES = 800
DO_PHIEN_BAN = 2          # đổi cách dò mặt thì tăng: kết quả dò cũ tự bị bỏ


# ---------------------------------------------------------------- tiện ích
def _label(mask):
    """Đánh nhãn vùng liền 4 hướng (không cần scipy)."""
    H, W = mask.shape
    lab = np.zeros((H, W), np.int32)
    n = 0
    ys, xs = np.nonzero(mask)
    for y0, x0 in zip(ys, xs):
        if lab[y0, x0]:
            continue
        n += 1
        st = [(y0, x0)]
        lab[y0, x0] = n
        while st:
            y, x = st.pop()
            for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < H and 0 <= xx < W and mask[yy, xx] and not lab[yy, xx]:
                    lab[yy, xx] = n
                    st.append((yy, xx))
    return lab, n


def _smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def _mesh_of(arm):
    return [o for o in bpy.data.objects if o.type == "MESH" and (o.parent == arm or any(
        m.type == "ARMATURE" and m.object == arm for m in o.modifiers))]


def _actor_frame(arm):
    """Khung "diễn viên": gốc ở chỗ đứng, -Y là phía mặt, Z lên (bỏ hướng đứng trong cảnh)."""
    heading = math.radians(arm.get("sgd_huong_do", 0.0))
    loc = arm.matrix_world.translation
    return (Matrix.Translation(Vector((loc.x, loc.y, 0))) @ Matrix.Rotation(heading, 4, "Z")).inverted()


def _rest_verts(mesh, to_u):
    """Toạ độ đỉnh lúc nghỉ (chưa biến dạng theo xương) trong khung diễn viên."""
    n = len(mesh.data.vertices)
    co = np.empty(n * 3)
    mesh.data.vertices.foreach_get("co", co)
    co = co.reshape(-1, 3)
    M = np.array(to_u @ mesh.matrix_world)
    p = co @ M[:3, :3].T + M[:3, 3]
    no = np.empty(n * 3)
    mesh.data.vertices.foreach_get("normal", no)
    no = no.reshape(-1, 3) @ np.array((to_u @ mesh.matrix_world).to_3x3()).T
    no /= np.linalg.norm(no, axis=1, keepdims=True) + 1e-9
    return p, no


# ---------------------------------------------------------------- 1. chụp thẳng mặt + dò
def _render_front(arm, mesh, head_box):
    """Ảnh chụp thẳng mặt (vuông FRONT_RES), máy chiếu song song. Trả (ảnh rgb uint8, (cx, cz, cỡ))."""
    x0, x1, z0, z1, yfront = head_box
    size = (z1 - z0) * 1.25
    cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    from_u = _actor_frame(arm).inverted()
    sc = bpy.data.scenes.new("SGD_ChupMat")
    sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [
        e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = FRONT_RES
    sc.render.resolution_percentage = 100
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGB"
    try:
        sc.view_settings.view_transform = "Standard"
    except TypeError:
        pass
    col = bpy.data.collections.new("SGD_ChupMat")
    sc.collection.children.link(col)
    col.objects.link(arm)
    for m in [mesh]:
        if m.name not in col.objects:
            col.objects.link(m)
    cam = bpy.data.objects.new("SGD_MayMat", bpy.data.cameras.new("SGD_MayMat"))
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = size
    cam.matrix_world = from_u @ (Matrix.Translation(Vector((cx, yfront - 2.0, cz))) @ Matrix.Rotation(math.pi / 2, 4, "X"))
    col.objects.link(cam)
    sc.camera = cam
    sun = bpy.data.objects.new("SGD_DenMat", bpy.data.lights.new("SGD_DenMat", "SUN"))
    sun.data.energy = 2.5
    sun.matrix_world = from_u @ Matrix.Rotation(math.radians(60), 4, "X")
    col.objects.link(sun)
    w = bpy.data.worlds.new("SGD_TroiMat")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    sc.world = w
    path = os.path.join(tempfile.gettempdir(), f"sgd_mat_{arm.name}.png")
    sc.render.filepath = path
    win = bpy.context.window
    if win is not None:
        old = win.scene
        win.scene = sc
    bpy.ops.render.render(write_still=True, scene=sc.name)
    if win is not None:
        win.scene = old
    for ob in (cam, sun):
        bpy.data.objects.remove(ob, do_unlink=True)
    bpy.data.collections.remove(col)
    bpy.data.worlds.remove(w)
    bpy.data.scenes.remove(sc)
    img = bpy.data.images.load(path)
    W, H = img.size
    a = np.array(img.pixels[:]).reshape(H, W, 4)[::-1, :, :3]
    bpy.data.images.remove(img)
    # ảnh lưu dạng sRGB, pixels đọc ra 0..1 theo giá trị đã lưu
    return (np.clip(a, 0, 1) * 255).astype(np.int32), (cx, cz, size)


def _nose_chin(v, y, below_v):
    pts=[(a,b) for a,b in zip(v,y) if b is not None and below_v<a<795]
    nv,ny=min(pts,key=lambda t:t[1])
    chin=nv; far=0
    for a,b in sorted([t for t in pts if t[0]>nv]):
        d=b-ny
        if d>0.15: continue            # đỉnh sau đầu / trong miệng: bỏ
        if d<0.045: chin=a; far=0
        else:
            far+=1
            if far>=2: break
    return nv, chin

def _find_eyes(a):
    H,W=a.shape[:2]
    r,g,b=a[...,0],a[...,1],a[...,2]; mx,mn=a.max(2),a.min(2); lum=r*0.3+g*0.59+b*0.11
    skin=np.median(a[int(H*0.62):int(H*0.70),int(W*0.45):int(W*0.58)].reshape(-1,3),axis=0)
    skin_lum=float(skin@np.array([.3,.59,.11])); dist=np.abs(a-skin).sum(2)
    blobs=[]
    for white in ((mn>150)&(mx-mn<45),
                  (mx-mn<50)&(lum>max(120.,0.95*skin_lum))&(mx-mn<0.45*(skin.max()-skin.min()))):
        lab,n=_label(white)
        for i in range(1,n+1):
            ys,xs=np.nonzero(lab==i)
            if 40<len(ys)<H*W*0.03 and H*0.35<ys.mean()<H*0.72:
                blobs.append((len(ys),xs.mean(),ys.mean()))
        if len(blobs)>=2 and min(t[1] for t in blobs)<W/2<max(t[1] for t in blobs): break
        blobs=[]
    if not blobs: raise RuntimeError("không thấy lòng trắng mắt trên ảnh thẳng mặt")
    eyes=[]
    for grp in ([t for t in blobs if t[1]<W/2],[t for t in blobs if t[1]>=W/2]):
        big=max(grp)
        grp=[t for t in grp if abs(t[1]-big[1])<W*0.08 and abs(t[2]-big[2])<W*0.05]
        cx0=np.average([t[1] for t in grp],weights=[t[0] for t in grp]); cy0=np.average([t[2] for t in grp],weights=[t[0] for t in grp])
        R=int(W*0.10); x0,x1,y0,y1=int(cx0-R),int(cx0+R),int(cy0-R*0.8),int(cy0+R*0.8)
        for th in (95,120,150,180):
            nonskin=(dist[y0:y1,x0:x1]>th)&(lum[y0:y1,x0:x1]>25)
            lab2,n2=_label(nonskin)
            cen=np.zeros_like(nonskin); cy_,cx_=int(cy0-y0),int(cx0-x0); cen[max(0,cy_-6):cy_+7,max(0,cx_-25):cx_+26]=True
            k=max(range(1,n2+1),key=lambda i:((lab2==i)&cen).sum())
            ys,xs=np.nonzero(lab2==k)
            best=(x0+(xs.min()+xs.max())/2,y0+(ys.min()+ys.max())/2,(xs.max()-xs.min())/2,(ys.max()-ys.min())/2)
            if not (xs.min()<=1 or xs.max()>=x1-x0-2 or ys.min()<=1 or ys.max()>=y1-y0-2): break
        eyes.append(dict(cx=best[0],cy=best[1],a=best[2],b=best[3]))
    return eyes, skin, dist, lum

def _detect(a, prof_v=None, prof_y=None):
    """Dò mắt, lông mày, miệng trên ảnh thẳng mặt (toạ độ điểm ảnh). prof_*: đường nghiêng giữa mặt (mũi, cằm)."""
    a=a.astype(float); H,W=a.shape[:2]
    eyes,skin,dist,lum=_find_eyes(a)
    r,g=a[...,0],a[...,1]
    skin_lum=float(skin@np.array([.3,.59,.11]))
    gap=abs(eyes[1]['cx']-eyes[0]['cx']); ecy=(eyes[0]['cy']+eyes[1]['cy'])/2
    cxm=int((eyes[0]['cx']+eyes[1]['cx'])/2)
    # cỡ mắt đối xứng: mắt lệch quá thì lấy theo mắt nhỏ hơn (mắt to thường là dò tràn)
    A=[e['a']*e['b'] for e in eyes]
    if max(A)>1.5*min(A):
        s,o=(0,1) if A[0]<A[1] else (1,0)
        eyes[o]=dict(cx=eyes[o]['cx'],cy=eyes[o]['cy'],a=eyes[s]['a'],b=eyes[s]['b'])
    # mũi + cằm theo hình khối (nếu có), không thì theo tỉ lệ
    if prof_v is not None:
        nv,ch=_nose_chin(prof_v,prof_y,ecy+0.12*gap)
    else:
        nv,ch=ecy+0.55*gap,ecy+1.6*gap
    # miệng: đường tối liền (cả trên lẫn dưới đều sáng hơn), trải ngang quanh trục giữa
    lo=int(nv+0.12*gap); hi=int(min(H-12,nv+0.85*(ch-nv)+0.05*gap))
    hw=int(gap*0.28)
    best=None
    for y in range(lo,hi):
        seg=slice(cxm-hw,cxm+hw)
        up=lum[y-8:y-3,seg].mean(0); dn=lum[y+3:y+8,seg].mean(0); row=lum[y-1:y+2,seg].mean(0)
        valley=np.minimum(up-row,dn-row)
        s_=float(np.median(valley))
        if best is None or s_>best[0]: best=(s_,y)
    my=best[1]
    def valley(y):
        return np.minimum(lum[y-8:y-3].mean(0)-lum[y-1:y+2].mean(0), lum[y+3:y+8].mean(0)-lum[y-1:y+2].mean(0))
    V=np.array([valley(my+dy) for dy in range(-6,7)])      # miệng cười/mếu: đường cong lệch vài điểm ảnh
    val=V.max(0); bend=V.argmax(0)-6
    thr=max(4.0,0.3*float(np.median(val[cxm-hw//2:cxm+hw//2])))
    x0=x1=cxm
    while x0>cxm-gap*0.75 and val[x0-3:x0].max()>thr: x0-=1
    while x1<cxm+gap*0.75 and val[x1+1:x1+4].max()>thr: x1+=1
    face_cx=(x0+x1)/2 if abs((x0+x1)/2-cxm)<gap*0.08 else cxm
    # lông mày: từng cột, đi từ tâm mắt lên qua mắt/mi tới da, rồi mép dưới vùng tối đầu tiên
    dark=lum<0.55*skin_lum
    soft=lum<0.80*skin_lum
    isskin=(dist<110)&~dark
    brows=[]
    for e in eyes:
        cols=[]
        for x in range(int(e['cx']-e['a']*1.45),int(e['cx']+e['a']*1.45)):
            y=int(e['cy'])
            lim=int(e['cy']-e['b']*2.2)
            run=0
            while y>lim:                      # tới khi gặp 3 hàng da liên tiếp
                run=run+1 if isskin[y,x] else 0
                if run>=3: break
                y-=1
            if run<3: continue
            yb=None
            for yy in range(y,y-int(gap*0.55),-1):
                if dark[yy,x] and dark[yy-1,x]: yb=yy; break
            if yb is None: continue
            yt=yb
            while yt>yb-int(gap*0.20) and dark[yt-1,x]: yt-=1
            cols.append((x,yb,yt))
        if not cols: brows.append(None); continue
        cen=[c[1] for c in cols if abs(c[0]-e['cx'])<e['a']*0.5] or [c[1] for c in cols]
        m=np.median(cen)
        ok=[c for c in cols if abs(c[1]-m)<0.10*gap+abs(c[0]-e['cx'])*0.25]
        # đoạn liên tục chứa phần giữa
        ok.sort(); segs=[[ok[0]]] if ok else []
        for c in ok[1:]:
            if c[0]-segs[-1][-1][0]>4: segs.append([])
            segs[-1].append(c)
        if not segs: brows.append(None); continue
        sgm=max(segs,key=lambda s:len(s)-abs(np.mean([c[0] for c in s])-e['cx'])*0.2)
        xs=[c[0] for c in sgm]; yb=[c[1] for c in sgm]; yt=[c[2] for c in sgm]
        th=max(4.0,float(np.median([p-q for p,q in zip(yb,yt)])))   # độ dày lông mày thật
        # lần theo đường lông mày ra hai bên (đuôi nhạt hơn, cong theo vòm); gặp mảng tối dày (tóc) thì dừng
        track={c[0]:c[1] for c in sgm}
        for step in (-1,1):
            x=(min(xs) if step<0 else max(xs)); y=track[x]; miss=0; y_mid=float(np.median(yb))
            while abs(x-e['cx'])<e['a']*1.9 and miss<=6 and abs(x+step-cxm)>gap*0.10:
                x+=step; found=None
                for dy in range(0,int(0.05*gap)+1):
                    for yy in (y-dy,y+dy):
                        if soft[yy,x] and soft[yy-1,x]: found=yy; break
                    if found: break
                if found is None: miss+=1; continue
                yb_=found
                while soft[yb_+1,x] and yb_<found+th: yb_+=1
                yt_=yb_
                while soft[yt_-1,x] and yt_>yb_-3*th: yt_-=1
                if yb_-yt_>2.5*th+2 or abs(yb_-y_mid)>0.12*gap: break     # tóc / lệch khỏi đường lông mày
                track[x]=yb_; y=yb_; miss=0
        xs=sorted(track); yb=[track[x] for x in xs]
        brows.append(dict(x0=min(xs),x1=max(xs),y0=int(np.percentile(yb,8)-th*1.1),y1=int(max(np.percentile(yb,90),np.median(yb)+2))))
    def mirror(B): return dict(x0=2*face_cx-B['x1'],x1=2*face_cx-B['x0'],y0=B['y0'],y1=B['y1'])
    wid=[(B['x1']-B['x0']) if B else 0 for B in brows]
    if wid[0]<0.75*wid[1]: brows[0]=mirror(brows[1])
    elif wid[1]<0.75*wid[0]: brows[1]=mirror(brows[0])
    hgt=[B['y1']-B['y0'] for B in brows]
    if hgt[0]>1.35*hgt[1]: brows[0]=dict(brows[0],y0=brows[1]['y0'],y1=brows[1]['y1'])
    elif hgt[1]>1.35*hgt[0]: brows[1]=dict(brows[1],y0=brows[0]['y0'],y1=brows[0]['y1'])
    return dict(eyes=eyes,brows=brows,mouth=dict(y=my,x0=x0,x1=x1,cong=[int(bend[x]) for x in range(x0,x1+1,8)]),face_cx=face_cx,skin=skin.tolist(),mui=nv,cam=ch)


def _override_from_moc(d, cache_path):
    """Có 68 điểm mốc đo trên mặt đã chiếu (chieu_mat, theo mét) thì lấy miệng (và trục giữa mặt) từ đó."""
    if not cache_path:
        return
    mp_ = cache_path.replace("_mat3d.json", "_moc_mat.json")
    if not os.path.exists(mp_):
        return
    if d.get("dau_rieng"):          # mốc đo trên mặt cũ (trước khi ghép đầu riêng): không dùng
        return
    with open(mp_, encoding="utf-8") as f:
        m = json.load(f).get("mo_hinh_m")
    if not m or len(m) != 68:
        return
    P = np.array(m, float)
    eyes = []
    for a_, b_ in ((36, 42), (42, 48)):
        E = P[a_:b_]
        cxe, cze = E[:, 0].mean(), E[:, 1].mean()
        aa = (E[:, 0].max() - E[:, 0].min()) / 2 * 1.05
        top = E[[1, 2], 1].mean()
        bot = E[[4, 5], 1].mean()
        bb = max(abs(top - bot) / 2 * 1.25, aa * 0.35)
        eyes.append(dict(cx=float(cxe), cz=float(cze), a=float(aa), b=float(bb)))
    eyes.sort(key=lambda e: e["cx"])
    brows = []
    for a_, b_ in ((17, 22), (22, 27)):
        B = P[a_:b_]
        th = 0.25 * (eyes[0]["b"] + eyes[1]["b"]) / 2 + 0.002
        brows.append(dict(x0=float(B[:, 0].min()), x1=float(B[:, 0].max()),
                          z0=float(B[:, 1].min() - th), z1=float(B[:, 1].max() + th)))
    brows.sort(key=lambda b: b["x0"])
    mz = float(P[60:68, 1].mean())
    # mắt / lông mày: giữ bộ dò màu (khoanh trọn lòng trắng + viền mi, mí che kín); 68 điểm khoanh mắt hoạt hình
    # hẹp hơn mắt thật. Miệng: lấy theo 68 điểm (bộ dò màu dễ nhầm râu / nếp nhân trung với khe miệng).
    d["mouth"] = dict(z=mz, x0=float(P[48, 0]), x1=float(P[54, 0]))
    d["face_cx"] = float(P[27:36, 0].mean())
    d["nguon_moc"] = "68 điểm (chieu_mat)"


def _model_mtime(model_path):
    """Mốc thời gian của mô hình + file nắn mặt / ảnh da chiếu mặt đi kèm (đổi file nào cũng dò lại mặt)."""
    if not model_path or not os.path.exists(model_path):
        return None
    base = os.path.splitext(model_path)[0]
    return max(os.path.getmtime(f) for f in (model_path, base + "_mat_hinh.npy", base + "_da_mat.png", base + "_dau.glb")
               if os.path.exists(f))


def measure(arm, cache_path=None, model_path=None):
    """Đặc điểm mặt trong khung diễn viên (mét): mắt (tâm, bán trục), lông mày (hộp), miệng (đường, hai mép)."""
    if cache_path and os.path.exists(cache_path):
        with open(cache_path, encoding="utf-8") as f:
            d = json.load(f)
        mt = _model_mtime(model_path)
        if d.get("khoa") or (d.get("mtime_mo_hinh") == mt and d.get("phien_ban_do") == DO_PHIEN_BAN):
            return d
    mesh = _mesh_of(arm)[0]
    to_u = _actor_frame(arm)
    p, no = _rest_verts(mesh, to_u)
    from . import body
    std_of = body.bone_map(arm)
    inv = {v: k for k, v in std_of.items() if v}
    head_z = (to_u @ arm.matrix_world @ arm.data.bones[inv["head"]].head_local).z
    hv = p[p[:, 2] > head_z]
    box = (float(hv[:, 0].min()), float(hv[:, 0].max()), float(head_z), float(hv[:, 2].max()), float(hv[:, 1].min()))
    img, (cx, cz, size) = _render_front(arm, mesh, box)
    # đường nghiêng giữa mặt (đỉnh trước nhất theo từng độ cao): cho mũi và cằm, khoanh vùng tìm miệng
    sel = p[(np.abs(p[:, 0] - cx) < 0.006) & (p[:, 2] > box[2] - 0.03) & (p[:, 2] < box[3])]
    bins = np.arange(box[2] - 0.03, box[3], 0.003)
    prof_y = []
    for b0 in bins:
        s_ = sel[(sel[:, 2] >= b0) & (sel[:, 2] < b0 + 0.003)]
        prof_y.append(float(s_[:, 1].min()) if len(s_) else None)
    prof_v = [(0.5 - ((b0 + 0.0015) - cz) / size) * FRONT_RES for b0 in bins]
    px = _detect(img, prof_v, prof_y)

    def X(u):
        return cx + (u / FRONT_RES - 0.5) * size

    def Z(v):
        return cz + (0.5 - v / FRONT_RES) * size
    s = size / FRONT_RES
    d = dict(
        eyes=[dict(cx=X(e["cx"]), cz=Z(e["cy"]), a=e["a"] * s, b=e["b"] * s) for e in px["eyes"]],
        brows=[dict(x0=X(b["x0"]), x1=X(b["x1"]), z0=Z(b["y1"]), z1=Z(b["y0"])) for b in px["brows"]],
        mouth=dict(z=Z(px["mouth"]["y"]), x0=X(px["mouth"]["x0"]), x1=X(px["mouth"]["x1"])),
        face_cx=X(px["face_cx"]), head=dict(z0=box[2], z1=box[3]),
        mtime_mo_hinh=_model_mtime(model_path),
        phien_ban_do=DO_PHIEN_BAN,
        dau_rieng=bool(arm.get("sgd_dau_rieng")),
        _ghi_chu="Toạ độ mét trong khung diễn viên (-Y là phía mặt). Sửa tay thì đặt \"khoa\": true.",
    )
    _override_from_moc(d, cache_path)
    if d["dau_rieng"]:                 # đầu riêng (mắt hoạt hình to, tròng sát mí): mí phủ rộng hơn chút
        for e in d["eyes"]:
            e["a"] *= 1.08
            e["b"] *= 1.12
    if cache_path:
        os.makedirs(os.path.dirname(cache_path), exist_ok=True)
        with open(cache_path, "w", encoding="utf-8") as f:
            json.dump(d, f, ensure_ascii=False, indent=1)
    return d


# ---------------------------------------------------------------- 2. gắn lên lưới
def _texture(mesh, idx=None):
    """Vật liệu (+ BSDF, ảnh màu) phủ các đỉnh idx (lưới nhiều vật liệu, vd đầu ghép riêng); không có idx: vật liệu đang chọn."""
    if idx is not None and len(idx) and len(mesh.data.materials) > 1:
        want = set(int(i) for i in idx)
        cnt = {}
        for poly in mesh.data.polygons:
            if any(v in want for v in poly.vertices):
                cnt[poly.material_index] = cnt.get(poly.material_index, 0) + 1
        if cnt:
            mesh.active_material_index = max(cnt, key=cnt.get)
    mat = mesh.active_material
    if not mat or not mat.use_nodes:
        return None, None, None
    bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None:
        return mat, None, None
    # ảnh màu: dò ngược từ Base Color qua các nút pha màu (vd khâu hoà da cổ khi ghép đầu riêng)
    tex, todo, seen = None, [bsdf.inputs["Base Color"]], set()
    while todo and tex is None:
        sock = todo.pop(0)
        for lk in sock.links:
            nd = lk.from_node
            if nd.name in seen or nd.name.startswith("SGD_"):
                continue
            seen.add(nd.name)
            if nd.type == "TEX_IMAGE":
                tex = nd
                break
            todo.extend(i for i in nd.inputs if i.is_linked)
    return mat, bsdf, tex


def _sample_texture(mesh, tex, idx, every=False):
    """Màu texture trung vị (tuyến tính) ở các đỉnh idx; every=True: trả mọi mẫu."""
    if tex is None or tex.image is None or not len(idx):
        return None
    img = tex.image
    W, H = img.size
    px = np.array(img.pixels[:]).reshape(H, W, img.channels)[:, :, :3]
    uv = mesh.data.uv_layers.active.data
    want = set(int(i) for i in idx)
    cols = []
    for lp in mesh.data.loops:
        if lp.vertex_index in want:
            u, v = uv[lp.index].uv
            cols.append(px[min(H - 1, max(0, int(v * H))), min(W - 1, max(0, int(u * W)))])
    if not cols:
        return None
    c = np.array(cols) if every else np.median(np.array(cols), axis=0)
    if img.colorspace_settings.name.lower().startswith("srgb"):
        c = np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)
    return c


def build(arm, feats):
    mesh = _mesh_of(arm)[0]
    to_u = _actor_frame(arm)
    p, no = _rest_verts(mesh, to_u)
    n = len(p)
    hz0, hz1 = feats["head"]["z0"], feats["head"]["z1"]
    hh = hz1 - hz0
    front = (no[:, 1] < -0.15)
    # chỉ lớp mặt trước: đỉnh gần mặt phẳng trước nhất tại (x,z) đó (bỏ tóc sau, mặt trong miệng...)
    yfront = p[:, 1].min()
    near_front = p[:, 1] < yfront + hh * 0.45

    # --- mắt: t = vị trí dọc trong mắt (0 mép trên .. 1 mép dưới), in = trong mắt (mềm ở rìa)
    eye_t = np.full(n, -1.0)
    eye_in = np.zeros(n)
    ring_idx = []
    cheek_idx = []
    for e in feats["eyes"]:
        ka, kb = 1.38, 1.6                                              # phủ cả viền sáng quanh mắt
        dx = (p[:, 0] - e["cx"]) / (e["a"] * ka)
        dz = (p[:, 2] - e["cz"]) / (e["b"] * kb)
        r = np.sqrt(dx * dx + dz * dz)
        inside = (r < 1.3) & front & near_front
        half = np.sqrt(np.clip(1 - np.clip(dx, -1, 1) ** 2, 0, 1))      # nửa chiều cao mắt tại cột x
        top = e["cz"] + half * e["b"] * kb
        bot = e["cz"] - half * e["b"] * kb
        t = np.where(top - bot > 1e-6, (top - p[:, 2]) / np.maximum(top - bot, 1e-6), 0.5)
        eye_t = np.where(inside, np.clip(t, -0.3, 1.3), eye_t)
        eye_in = np.where(inside, np.maximum(eye_in, 1 - _smooth(0.85, 1.25, r)), eye_in)
        ring = (r > 1.25) & (r < 1.7) & front & near_front                # da ngay quanh mắt: lấy màu mí
        ring_idx.extend(np.nonzero(ring)[0].tolist())
        cheek = (np.abs(dx) < 0.6) & (dz < -2.0) & (dz > -3.2) & front & near_front   # má ngay dưới mắt: màu da chuẩn
        cheek_idx.extend(np.nonzero(cheek)[0].tolist())
    # --- miệng: mt = độ sâu dưới đường miệng (0 ở đường, 1 ở hết dải), mx = lệch ngang chuẩn hoá
    m = feats["mouth"]
    mcx, mhw = (m["x0"] + m["x1"]) / 2, (m["x1"] - m["x0"]) / 2
    band = hh * 0.10                                   # dải môi dưới co giãn khi há miệng
    mt = (m["z"] - p[:, 2]) / band
    mx_ = np.abs(p[:, 0] - mcx) / mhw
    m_in = (front & near_front & (mx_ < 1.15) & (mt > -0.6) & (mt < 1.6)).astype(float)
    # thuộc tính đỉnh (bám bề mặt)
    for name, data in (("sgd_mat", np.stack([eye_t, eye_in, np.zeros(n)], 1)),
                       # toạ độ quanh miệng TUYẾN TÍNH theo vị trí (u ngang, v dọc) -> nội suy chính xác,
                       # hình miệng tính theo từng điểm ảnh trong vật liệu nên sắc nét dù lưới thưa
                       ("sgd_mieng", np.stack([(p[:, 0] - mcx) / mhw, mt, m_in], 1))):
        if name in mesh.data.attributes:
            mesh.data.attributes.remove(mesh.data.attributes[name])
        at = mesh.data.attributes.new(name, "FLOAT_VECTOR", "POINT")
        at.data.foreach_set("vector", data.astype(np.float32).ravel())

    # --- shape keys: hàm (môi dưới + cằm), lông mày
    if mesh.data.shape_keys is None:
        mesh.shape_key_add(name="Basis", from_mix=False)
    from_u3 = np.array((to_u @ mesh.matrix_world).inverted().to_3x3())   # khung diễn viên -> toạ độ lưới

    def add_key(name, disp_u):
        if name in mesh.data.shape_keys.key_blocks:
            mesh.shape_key_remove(mesh.data.shape_keys.key_blocks[name])
        kb = mesh.shape_key_add(name=name, from_mix=False)
        base = np.empty(n * 3)
        mesh.data.shape_keys.key_blocks["Basis"].data.foreach_get("co", base)
        kb.data.foreach_set("co", (base.reshape(-1, 3) + disp_u @ from_u3.T).ravel())
        kb.slider_max = 1.0
        return kb
    # hàm: dưới đường miệng, giảm dần ra hai má; tới cằm thì cả mảng hạ xuống
    jaw = hh * 0.03
    lat = 1 - _smooth(1.0, 2.1, mx_)
    below = _smooth(0.0, 1.0, mt)
    # cằm: chỉ mặt trước, tới ~1/3 chiều cao đầu dưới miệng; xa hơn (cổ) giảm dần về 0
    depth_ = (m["z"] - p[:, 2]) / hh
    neck = 1 - _smooth(0.22, 0.36, depth_)
    wj = below * lat * neck * near_front
    disp = np.zeros((n, 3))
    disp[:, 2] = -jaw * wj
    disp[:, 1] = jaw * 0.25 * wj                         # cằm lùi nhẹ vào trong khi há
    add_key("Mo", disp)
    # môi: chu tròn / kéo ngang / mím (vùng quanh khe miệng)
    vo = mt                                                           # dọc theo đường miệng (dương = xuống)
    wl = (1 - _smooth(0.85, 1.45, mx_)) * (1 - _smooth(0.35, 0.9, np.abs(vo + 0.05))) * near_front
    rel = p[:, 0] - mcx
    d = np.zeros((n, 3))
    d[:, 0] = -rel * 0.24 * wl                                        # thu hẹp về giữa
    d[:, 1] = -hh * 0.020 * wl                                         # nhô ra trước (phía mặt là -Y)
    add_key("Tron", d)
    d = np.zeros((n, 3))
    d[:, 0] = rel * 0.13 * wl                                         # kéo hai mép ra
    d[:, 2] = hh * 0.005 * wl * np.clip(mx_, 0, 1)                     # mép hơi nhếch lên
    add_key("Ngang", d)
    d = np.zeros((n, 3))
    d[:, 1] = hh * 0.006 * wl                                          # môi ép vào
    d[:, 2] = hh * 0.004 * wl * (vo > 0)                               # môi dưới đẩy lên
    add_key("Mim", d)
    # mép trễ (buồn / nghẹn): hai khoé miệng hạ xuống, giữa môi gần như đứng yên -> gỡ nụ cười sẵn có của mặt
    wc = _smooth(0.25, 0.9, mx_) * (1 - _smooth(1.15, 2.0, mx_)) * (1 - _smooth(0.5, 1.6, np.abs(vo))) * near_front
    d = np.zeros((n, 3))
    d[:, 2] = -hh * 0.032 * wc                                         # khoé (và nếp cười) hạ xuống
    d[:, 0] = -(p[:, 0] - mcx) * 0.06 * wc                             # khoé thu vào: bớt nụ cười
    d[:, 1] = hh * 0.004 * wc
    wlo = (1 - _smooth(0.0, 0.7, mx_)) * _smooth(0.05, 0.4, vo) * (1 - _smooth(0.6, 1.2, vo)) * near_front
    d[:, 2] += hh * 0.008 * wlo                                        # môi dưới đẩy lên nhẹ (mếu)
    add_key("MepHa", d)
    # lông mày
    wb = np.zeros(n)
    inner = np.zeros(n)
    for b in feats["brows"]:
        bcx, bcz = (b["x0"] + b["x1"]) / 2, (b["z0"] + b["z1"]) / 2
        bw, bh = (b["x1"] - b["x0"]) / 2, max((b["z1"] - b["z0"]) / 2, hh * 0.02)
        dx = (p[:, 0] - bcx) / (bw * 1.35)
        dz = (p[:, 2] - bcz) / (max(bh, hh * 0.035) * 3.2)
        rr = np.sqrt(dx * dx + dz * dz)
        w = (1 - _smooth(0.75, 1.6, rr)) * near_front                  # cả mặt dưới của gờ lông mày
        wb = np.maximum(wb, w)
        # đầu trong = phía gần giữa mặt
        toward = np.sign(feats["face_cx"] - bcx)
        inner = np.where(w > 0, np.clip(0.5 + toward * (p[:, 0] - bcx) / (2 * bw), 0, 1), inner)
    # không kéo mí/mắt theo
    wb = wb * (1 - np.clip(eye_in * 1.5, 0, 1))
    lift = hh * 0.035
    d1 = np.zeros((n, 3))
    d1[:, 2] = lift * wb
    add_key("Nhuong", d1)
    d2 = np.zeros((n, 3))
    d2[:, 2] = lift * wb * (inner * 1.1 - 0.25)
    d2[:, 0] = -lift * 0.35 * wb * inner * np.sign(p[:, 0] - feats["face_cx"])
    add_key("Lo", d2)
    # nhắm mi (hình khối): vùng mắt kéo về mặt căng mịn phủ qua nhãn cầu -> mắt nhắm không còn vành tròn
    # nổi (đầu dựng riêng khắc mắt thành khối lồi). Hàn đỉnh trùng (đường nối UV) để không xé lưới.
    mov = eye_in > 0.02
    if mov.any():
        key_p = np.round(p, 6)
        uq, inv_u = np.unique(key_p, axis=0, return_inverse=True)
        inv_u = inv_u.ravel()
        ev = np.empty(len(mesh.data.edges) * 2, np.int64)
        mesh.data.edges.foreach_get("vertices", ev)
        ev = inv_u[ev.reshape(-1, 2)]
        ev = ev[ev[:, 0] != ev[:, 1]]
        nu = len(uq)
        w_u = np.zeros(nu)
        np.maximum.at(w_u, inv_u, eye_in)
        act = np.nonzero(w_u > 0.02)[0]
        keep = np.isin(ev[:, 0], act) | np.isin(ev[:, 1], act)
        e2 = ev[keep]
        S = uq.copy()
        deg = np.bincount(e2.ravel(), minlength=nu).astype(float)
        for _ in range(60):
            acc = np.zeros((nu, 3))
            np.add.at(acc, e2[:, 0], S[e2[:, 1]])
            np.add.at(acc, e2[:, 1], S[e2[:, 0]])
            avg = np.where(deg[:, None] > 0, acc / np.maximum(deg, 1)[:, None], S)
            S = np.where((w_u > 0.02)[:, None], S + (avg - S) * 0.6, S)
        dm = (S - uq)[inv_u] * np.clip(eye_in * 1.3, 0, 1)[:, None]
        dm[:, 1] = np.minimum(dm[:, 1], 0.0) + np.maximum(dm[:, 1], 0.0) * 0.6
        add_key("NhamMi", dm)

    # --- vật liệu: mí mắt, khe miệng, răng
    mat, bsdf, tex = _texture(mesh, np.array(ring_idx + cheek_idx))
    if bsdf is None:
        raise RuntimeError(f"{mesh.name}: không thấy vật liệu Principled để gắn mí/miệng")
    # màu mí = da quanh mắt; bỏ mẫu dính viền mi / phấn mắt / bóng (khác hẳn màu da má)
    ring_c = _sample_texture(mesh, tex, np.array(ring_idx), every=True)
    cheek_c = _sample_texture(mesh, tex, np.array(cheek_idx))
    lid_col = None
    if cheek_c is not None:
        if ring_c is not None and len(ring_c):
            rel = np.abs(np.log((ring_c + 0.01) / (cheek_c + 0.01))).max(1)
            near = ring_c[rel < 0.45]
            if len(near) >= 12:
                lid_col = np.median(near, axis=0)
        if lid_col is None:
            lid_col = cheek_c * 0.92
    elif ring_c is not None and len(ring_c):
        lid_col = np.median(ring_c, axis=0)
    if lid_col is None:
        lid_col = np.array([0.55, 0.32, 0.22])
    nt = mat.node_tree
    for nd in [nd for nd in nt.nodes if nd.name.startswith("SGD_")]:
        nt.nodes.remove(nd)
    N = nt.nodes
    L = nt.links

    def node(kind, name, **kw):
        nd = N.new(kind)
        nd.name = nd.label = "SGD_" + name
        for k, v in kw.items():
            setattr(nd, k, v)
        return nd

    def math_(op, a=None, b=None, name="m", clamp=False):
        nd = node("ShaderNodeMath", name, operation=op)
        nd.use_clamp = clamp
        for i, v in enumerate((a, b)):
            if v is None:
                continue
            if isinstance(v, (int, float)):
                nd.inputs[i].default_value = v
            else:
                L.new(v, nd.inputs[i])
        return nd.outputs[0]

    def smooth_(e0, e1, x, name):
        nd = node("ShaderNodeMapRange", name)
        nd.interpolation_type = "SMOOTHSTEP"
        for sock, v in ((nd.inputs[1], e0), (nd.inputs[2], e1)):
            if isinstance(v, (int, float)):
                sock.default_value = v
            else:
                L.new(v, sock)
        nd.inputs[3].default_value = 0.0
        nd.inputs[4].default_value = 1.0
        L.new(x, nd.inputs[0])
        return nd.outputs[0]
    blink = node("ShaderNodeValue", "Chop")
    blink.outputs[0].default_value = 0.0
    mouth = node("ShaderNodeValue", "Mieng")
    mouth.outputs[0].default_value = 0.0
    k_ngang = node("ShaderNodeValue", "Ngang")
    k_tron = node("ShaderNodeValue", "Tron")
    k_fv = node("ShaderNodeValue", "RangMoi")
    k_mim = node("ShaderNodeValue", "Mim")
    for nd_ in (k_ngang, k_tron, k_fv, k_mim):
        nd_.outputs[0].default_value = 0.0
    a_eye = node("ShaderNodeAttribute", "ThuocTinhMat", attribute_name="sgd_mat")
    a_m = node("ShaderNodeAttribute", "ThuocTinhMieng", attribute_name="sgd_mieng")
    se = node("ShaderNodeSeparateXYZ", "TachMat")
    L.new(a_eye.outputs["Vector"], se.inputs[0])
    sm = node("ShaderNodeSeparateXYZ", "TachMieng")
    L.new(a_m.outputs["Vector"], sm.inputs[0])
    t, ein = se.outputs[0], se.outputs[1]
    bl = math_("MULTIPLY", blink.outputs[0], 1.08, "ChopVuot")
    # mí: phủ phần t < blink (mép mềm)
    lid = math_("MULTIPLY", ein, math_("SUBTRACT", 1.0, smooth_(math_("SUBTRACT", bl, 0.05, "c1"), bl, t, "MiMem"), "mi"), "MiMat")
    # viền mi tối ngay mép mí
    edge = math_("MINIMUM", bl, 0.5 + 0.5 / 1.6 + 0.03, "MepMi")           # mắt nhắm hẳn: đường mi ở mép dưới mắt thật
    lash_band = math_("SUBTRACT", smooth_(math_("SUBTRACT", edge, 0.13, "c2"), math_("SUBTRACT", edge, 0.06, "c3"), t, "Mi1"),
                      smooth_(math_("SUBTRACT", edge, 0.015, "c4"), edge, t, "Mi2"), "Mi3")
    lash = math_("MULTIPLY", math_("MULTIPLY", lash_band, ein, "Mi4"), math_("MULTIPLY", blink.outputs[0], 6.0, "Mi5", clamp=True), "VienMi")
    # miệng (tính theo từng điểm ảnh): khe hình bầu dục giữa môi trên (nhích lên) và môi dưới (hạ xuống),
    # răng trên, lưỡi, lòng miệng tối, viền môi dưới. u: ngang (±1 = hai mép), v: dọc (0 = đường miệng, dương = xuống)
    u_, v_, min_ = sm.outputs[0], sm.outputs[1], sm.outputs[2]
    v_ = math_("ADD", v_, 0.06, "TrungDuongMoi")                      # khe bắt đầu đúng đường môi vẽ sẵn
    mv = mouth.outputs[0]
    E = 0.018                                                     # độ mềm mép (rất nhỏ -> sắc)
    w_ = math_("ADD", 0.72, math_("MULTIPLY", mv, 0.10, "W1"), "NuaRong0")     # nửa bề ngang khe
    wmod = math_("ADD", 1.0, math_("SUBTRACT", math_("MULTIPLY", k_ngang.outputs[0], 0.18, "W5"),
                                   math_("MULTIPLY", k_tron.outputs[0], 0.45, "W6"), "W7"), "W8")
    w_ = math_("MULTIPLY", w_, wmod, "NuaRong")
    q_ = math_("SUBTRACT", 1.0, math_("POWER", math_("DIVIDE", u_, w_, "W2"), 2.0, "W3"), "W4", clamp=True)
    sq = math_("POWER", q_, 0.5, "BauDuc")
    opn = math_("MULTIPLY", mv, 0.52, "DoMo")                            # há hết cỡ ~1,4 cm (nói to)
    bot = math_("MULTIPLY", opn, sq, "MoiDuoi")                          # mép môi dưới
    top = math_("MULTIPLY", math_("MULTIPLY", math_("MULTIPLY", opn, -0.30, "T1"),
                                            math_("ADD", 1.0, math_("MULTIPLY", k_tron.outputs[0], 0.9, "T2"), "T3"), "T4"),
                sq, "MoiTren")                                                   # môi trên cong lên (tròn hơn khi chu)
    on = math_("MULTIPLY", mv, 12.0, "Bat", clamp=True)
    inside = math_("MULTIPLY", math_("MULTIPLY", smooth_(math_("SUBTRACT", top, E, "I1"), math_("ADD", top, E, "I2"), v_, "I3"),
                                     math_("SUBTRACT", 1.0, smooth_(math_("SUBTRACT", bot, E, "I4"), math_("ADD", bot, E, "I5"), v_, "I6"), "I7"), "I8"),
                   math_("MULTIPLY", min_, on, "I9"), "TrongMieng")
    gap = math_("SUBTRACT", bot, top, "Khe")
    # răng trên: dải sát môi trên, cao tối đa 0,16 (đơn vị dải), chỉ khi há đủ
    tt = math_("ADD", top, math_("MINIMUM", 0.085, math_("MULTIPLY", gap, 0.36, "R0"), "R1"), "R2")
    teeth = math_("MULTIPLY", math_("MULTIPLY", inside, math_("SUBTRACT", 1.0, smooth_(math_("SUBTRACT", tt, E, "R3"), math_("ADD", tt, E, "R4"), v_, "R5"), "R6"), "R7"),
                  math_("MULTIPLY", math_("MULTIPLY", math_("SUBTRACT", mv, 0.12, "R8"), 6.0, "R9", clamp=True),
                        math_("SUBTRACT", 1.0, math_("MULTIPLY", k_tron.outputs[0], 0.7, "R10"), "R11"), "R12"), "Rang")
    # ph/v: răng trên lộ, môi dưới chạm răng (không có khe tối)
    au = math_("ABSOLUTE", u_, None, "FV0")
    fv_band = math_("MULTIPLY", smooth_(-0.035, -0.015, v_, "FV1"), math_("SUBTRACT", 1.0, smooth_(0.045, 0.065, v_, "FV2"), "FV3"), "FV4")
    fv = math_("MULTIPLY", math_("MULTIPLY", fv_band, math_("SUBTRACT", 1.0, smooth_(0.45, 0.6, au, "FV5"), "FV6"), "FV7"),
               math_("MULTIPLY", k_fv.outputs[0], min_, "FV8"), "RangMoiMau")
    # m/b/p: môi ép lại thành đường đậm
    av = math_("ABSOLUTE", v_, None, "MM0")
    mim = math_("MULTIPLY", math_("MULTIPLY", math_("SUBTRACT", 1.0, smooth_(0.012, 0.035, av, "MM1"), "MM2"),
                                  math_("SUBTRACT", 1.0, smooth_(0.75, 0.95, au, "MM3"), "MM4"), "MM5"),
                math_("MULTIPLY", k_mim.outputs[0], min_, "MM6"), "MimMau")
    # lưỡi: phần dưới khe
    tg = math_("SUBTRACT", bot, math_("MULTIPLY", gap, 0.22, "L0"), "L1")
    tongue = math_("MULTIPLY", inside, smooth_(math_("SUBTRACT", tg, E, "L2"), math_("ADD", tg, E, "L3"), v_, "L4"), "Luoi")
    # viền môi dưới (ngay dưới khe), hồng hơn da
    lip = math_("MULTIPLY", math_("MULTIPLY", smooth_(math_("SUBTRACT", bot, E, "V1"), math_("ADD", bot, E, "V2"), v_, "V3"),
                                  math_("SUBTRACT", 1.0, smooth_(math_("ADD", bot, 0.09, "V4"), math_("ADD", bot, 0.15, "V5"), v_, "V6"), "V7"), "V8"),
                math_("MULTIPLY", math_("MULTIPLY", min_, on, "V9"), smooth_(0.0, 0.25, q_, "V10"), "V11"), "MoiDuoiMau")
    lip = math_("MULTIPLY", lip, 0.45, "MoiDuoiNhat")
    cav = inside
    # pha màu: texture -> mí (màu da) -> viền mi -> khe miệng -> răng
    base_link = bsdf.inputs["Base Color"].links[0] if bsdf.inputs["Base Color"].links else None
    src = base_link.from_socket if base_link else None

    def mix(a_sock, color, fac, name):
        nd = node("ShaderNodeMix", name)
        nd.data_type = "RGBA"
        nd.blend_type = "MIX"
        L.new(fac, nd.inputs["Factor"])
        if a_sock is not None:
            L.new(a_sock, nd.inputs[6])
        else:
            nd.inputs[6].default_value = (*bsdf.inputs["Base Color"].default_value[:3], 1)
        nd.inputs[7].default_value = (*color, 1.0)
        return nd.outputs[2]
    c = mix(src, tuple(float(v) for v in lid_col), lid, "PhuMi")
    c = mix(c, tuple(float(v) * 0.25 for v in lid_col), lash, "ToVienMi")
    c = mix(c, (0.42, 0.13, 0.12), lip, "ToMoiDuoi")
    c = mix(c, (0.045, 0.012, 0.012), cav, "ToKheMieng")
    c = mix(c, (0.22, 0.05, 0.05), tongue, "ToLuoi")
    c = mix(c, (0.80, 0.76, 0.66), teeth, "ToRang")
    c = mix(c, (0.80, 0.76, 0.66), fv, "ToRangMoi")
    c = mix(c, (0.26, 0.08, 0.07), math_("MULTIPLY", mim, 0.75, "MM7"), "ToMim")
    L.new(c, bsdf.inputs["Base Color"])
    return FaceRig3D(mesh, mat, blink, mouth, dict(ngang=k_ngang, tron=k_tron, rang_moi=k_fv, mim=k_mim))


class FaceRig3D:
    """Cùng hàm điều khiển với lớp mặt 2D (rig.FaceRig)."""
    BROW_KEYS = ("Nhuong", "Lo", "MepHa")

    def __init__(self, mesh, mat, blink_node, mouth_node, lip_nodes):
        self.mesh, self.mat = mesh, mat
        self.blink, self.mouth = blink_node, mouth_node
        self.lip = lip_nodes
        self.keys = mesh.data.shape_keys

    def set_tilt(self, frame, degrees):      # đầu do lớp thân người lo
        pass

    def set_breath(self, frame, amount):
        pass

    def set_blink(self, frame, closed):
        self.blink.outputs[0].default_value = float(closed)
        self.blink.outputs[0].keyframe_insert("default_value", frame=frame)
        if self.keys and "NhamMi" in self.keys.key_blocks:
            kb = self.keys.key_blocks["NhamMi"]
            c_ = min(max((float(closed) - 0.55) / 0.45, 0.0), 1.0)      # chỉ làm phẳng khi mắt đã khép quá nửa
            kb.value = c_ * c_ * (3 - 2 * c_)
            kb.keyframe_insert("value", frame=frame)

    def set_mouth(self, frame, amount):
        amount = float(amount)
        self.mouth.outputs[0].default_value = amount
        self.mouth.outputs[0].keyframe_insert("default_value", frame=frame)
        kb = self.keys.key_blocks["Mo"]
        kb.value = amount
        kb.keyframe_insert("value", frame=frame)

    def set_viseme(self, frame, mo, ngang, tron, rang_moi, mim):
        """Dáng môi theo âm (khau_hinh): mở, kéo ngang, chu tròn, răng trên chạm môi dưới, mím."""
        self.set_mouth(frame, mo)
        for name, val, key in (("ngang", ngang, "Ngang"), ("tron", tron, "Tron"), ("rang_moi", rang_moi, None),
                               ("mim", mim, "Mim")):
            out = self.lip[name].outputs[0]
            out.default_value = float(val)
            out.keyframe_insert("default_value", frame=frame)
            if key:
                kb = self.keys.key_blocks[key]
                kb.value = float(val)
                kb.keyframe_insert("value", frame=frame)

    def set_brow(self, frame, **weights):
        for k in self.BROW_KEYS:
            kb = self.keys.key_blocks[k]
            kb.value = float(weights.get(k, 0.0))
            kb.keyframe_insert("value", frame=frame)

    def animated_owners(self):
        return [self.keys, self.mat.node_tree]


def attach(arm, code, model_path, data_dir):
    """Đo (hoặc đọc đo đạc cũ) rồi gắn mặt lên diễn viên mô hình 3D. Trả FaceRig3D."""
    feats = measure(arm, os.path.join(data_dir, "do_dac", f"{code}_mat3d.json"), model_path)
    return build(arm, feats)
