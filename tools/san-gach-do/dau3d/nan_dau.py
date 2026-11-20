# blender: sua dau 3D theo anh goc: hinh (TPS 68 moc) + mau (toc, mong mat)
import bpy, bmesh, sys, json, math, numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
a=sys.argv[sys.argv.index('--')+1:]
glb, tex2, lm_r_path, lm_g_path, ref_png, out_glb = a[:6]
RES=700; MUC=float(a[6]) if len(a)>6 else 0.85
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=glb)
ms=[o for o in bpy.data.objects if o.type=='MESH']
for o in ms:
    if o.name=='Cube' or len(o.data.vertices)<10: bpy.data.objects.remove(o)
o=[x for x in bpy.data.objects if x.type=='MESH'][0]
pts=[o.matrix_world@Vector(c) for c in o.bound_box]
mn=Vector((min(q.x for q in pts),min(q.y for q in pts),min(q.z for q in pts))); mx=Vector((max(q.x for q in pts),max(q.y for q in pts),max(q.z for q in pts)))
c=(mn+mx)/2; size=max(mx-mn); ortho=size*1.05
def px2w(p): return (c.x+(p[0]/RES-0.5)*ortho, c.z-(p[1]/RES-0.5)*ortho)
lr=np.array(json.load(open(lm_r_path)),float); lg=np.array(json.load(open(lm_g_path)),float)
# similarity ref->render (core: 36..47 mat, 27..35 mui, 48,54 mieng)
core=list(range(27,48))+[48,54]
A=lg[core]; B=lr[core]; ma,mb=A.mean(0),B.mean(0); A0,B0=A-ma,B-mb
U,S,Vt=np.linalg.svd(A0.T@B0); R=(U@Vt).T
if np.linalg.det(R)<0: Vt[-1]*=-1; R=(U@Vt).T
s=S.sum()/(A0**2).sum(); tgt=(s*(R@(lg-ma).T)).T+mb
gap=np.linalg.norm(lr[45]-lr[36])
d=np.clip(tgt-lr,-0.35*gap,0.35*gap)*MUC
print('dich trung binh px',np.abs(d).mean().round(2),'max',np.abs(d).max().round(2))
# TPS tren mat phang x,z (the gioi)
P=np.array([px2w(p) for p in lr]); Q=np.array([px2w(p) for p in lr+d])
# them diem neo vien (dich 0) quanh mat
cx,cz=P.mean(0); rad=np.linalg.norm(P-P.mean(0),axis=1).max()
anc=np.array([[cx+1.6*rad*math.cos(t),cz+1.6*rad*math.sin(t)] for t in np.linspace(0,2*math.pi,24,endpoint=False)])
P2=np.vstack([P,anc]); D2=np.vstack([Q-P,np.zeros_like(anc)])
def U_(r): r2=r*r; return np.where(r2>0,r2*np.log(r2+1e-12),0)
n=len(P2); K=U_(np.linalg.norm(P2[:,None]-P2[None],axis=2))+np.eye(n)*1e-6
Pm=np.hstack([np.ones((n,1)),P2]); L=np.zeros((n+3,n+3)); L[:n,:n]=K; L[:n,n:]=Pm; L[n:,:n]=Pm.T
W=np.linalg.solve(L,np.vstack([D2,np.zeros((3,2))]))
mw=o.matrix_world; inv=mw.inverted()
V=np.array([tuple(mw@v.co) for v in o.data.vertices])
xz=V[:,[0,2]]; Kv=U_(np.linalg.norm(xz[:,None]-P2[None],axis=2))
disp=Kv@W[:n]+np.hstack([np.ones((len(xz),1)),xz])@W[n:]
# trong so: trong vung mat, phia truoc (y nho = gan camera)
r_=np.linalg.norm(xz-[cx,cz],axis=1)/rad
w_r=np.clip((1.6-r_)/0.6,0,1)
yf=V[:,1]; front=np.percentile(yf,1); back=c.y
w_y=np.clip((back-yf)/(back-front),0,1)**0.7
w=(w_r*w_y)[:,None]
V2=V.copy(); V2[:,0]+=disp[:,0]*w[:,0]; V2[:,2]+=disp[:,1]*w[:,0]
for i,v in enumerate(o.data.vertices): v.co=inv@Vector(V2[i])
o.data.update()
import os
if os.environ.get('SGD_DA_MIN'):
    # da trẻ: là phẳng rãnh nhỏ trên hai má (vd tóc mai khắc lên má), khoanh theo mốc, tránh mắt/mũi/miệng/tai
    import cv2
    msk=np.zeros((RES,RES),np.uint8)
    lq=lr+d
    for poly in ([0,1,2,3,4,5,48,31,39,40,41,36],[16,15,14,13,12,11,54,35,42,47,46,45]):
        cv2.fillPoly(msk,[lq[poly].astype(np.int32)],255)
    msk=cv2.GaussianBlur(msk.astype(np.float32)/255,(0,0),6)
    bmx=bmesh.new(); bmx.from_mesh(o.data); bmesh.ops.remove_doubles(bmx,verts=bmx.verts[:],dist=1e-6)
    bmx.verts.ensure_lookup_table(); bmx.normal_update()
    def wmask(co):
        q=mw@co; px_=int((q.x-c.x)/ortho*RES+RES/2); py_=int(RES/2-(q.z-c.z)/ortho*RES)
        if not(0<=px_<RES and 0<=py_<RES): return 0.0
        nz=(mw.to_3x3()@co.normalized())
        return float(msk[py_,px_])
    ws={v:wmask(v.co)*(1.0 if (mw.to_3x3()@v.normal).y<-0.1 else 0.0) for v in bmx.verts}
    sel=[v for v,w in ws.items() if w>0.02]
    for it in range(40):
        new={}
        for v in sel:
            nb=[e.other_vert(v).co for e in v.link_edges]
            if not nb: continue
            avg=sum(nb,Vector())/len(nb)
            lam=0.5 if it%2==0 else -0.53
            new[v]=v.co+(avg-v.co)*lam*ws[v]
        for v,co in new.items(): v.co=co
    bmx.to_mesh(o.data); bmx.free(); o.data.update()
    print('da min dinh',len(sel))
# --- mau: mong mat ---
from PIL import Image
T=np.asarray(Image.open(tex2).convert('RGB')).astype(np.float32)/255; H,Wt=T.shape[:2]
deps=bpy.context.evaluated_depsgraph_get()
bm=bmesh.new(); bm.from_mesh(o.data); bm.transform(mw); bmesh.ops.triangulate(bm,faces=bm.faces[:])
uvl=bm.loops.layers.uv.active; bm.faces.ensure_lookup_table(); bvh=BVHTree.FromBMesh(bm)
def hit_uv(p):  # p: pixel tren render goc (truoc khi nan) -> dung toa do sau nan (Q)
    x,z=p
    loc,nor,fi,dist=bvh.ray_cast(Vector((x,mn.y-1,z)),Vector((0,1,0)))
    if fi is None: return None
    f=bm.faces[fi]; vs=[l.vert.co for l in f.loops]
    from mathutils.geometry import barycentric_transform
    uvs=[l[uvl].uv for l in f.loops]
    b=barycentric_transform(loc,vs[0],vs[1],vs[2],Vector((uvs[0].x,uvs[0].y,0)),Vector((uvs[1].x,uvs[1].y,0)),Vector((uvs[2].x,uvs[2].y,0)))
    return b.x,b.y
ref=np.asarray(Image.open(ref_png).convert('RGB')).astype(np.float32)/255
def iris_info(img,lm,idx):
    import cv2
    pts=lm[idx].astype(np.int32); mask=np.zeros(img.shape[:2],np.uint8); cv2.fillPoly(mask,[pts],255)
    lum=img@np.array([0.3,0.59,0.11],np.float32); thr=np.percentile(lum[mask>0],45)
    ys,xs=np.where((mask>0)&(lum<thr)); return np.array([xs.mean(),ys.mean()]),img[ys,xs].mean(0),(pts[:,0].max()-pts[:,0].min())/2
refcols=[iris_info(ref,lg,list(range(36,42)))[1],iris_info(ref,lg,list(range(42,48)))[1]]
tgt_col=np.mean(refcols,0); print('mau mong mat goc',(tgt_col*255).round())
rimg=np.asarray(Image.open(a[7]).convert('RGB')).astype(np.float32)/255 if len(a)>7 else None
acc=np.zeros((H,Wt),np.float32)
for idx in (list(range(36,42)),list(range(42,48))):
    cc,_,half=iris_info(rimg,lr,idx); cc=cc+d[idx].mean(0)
    rr=half*0.62
    for dy in np.arange(-rr,rr+0.01,0.5):
        for dx in np.arange(-rr,rr+0.01,0.5):
            q=math.hypot(dx,dy)/rr
            if q>1: continue
            uv=hit_uv(px2w((cc[0]+dx,cc[1]+dy)))
            if uv is None: continue
            u,v=uv; X=int(u*(Wt-1)); Y=int((1-v)*(H-1))
            wv=1-max(0,(q-0.75)/0.25)
            for yy in range(Y-3,Y+4):
                for xx in range(X-3,X+4):
                    if 0<=yy<H and 0<=xx<Wt: acc[yy,xx]=max(acc[yy,xx],wv)
lum=T@np.array([0.3,0.59,0.11],np.float32); mxv=T.max(2); sat=(mxv-T.min(2))/np.maximum(mxv,1e-3)
keep=(mxv>0.88)&(sat<0.2)  # dom sang / long trang giu nguyen
L_t=float(tgt_col@np.array([0.3,0.59,0.11])); Lm=np.median(lum[acc>0.5]) if (acc>0.5).any() else 0.4
newc=np.clip(tgt_col[None,None]*(lum/Lm)[...,None],0,1)
wa=(acc*(~keep))[...,None]
T2=T*(1-wa)+newc*wa
print('texel mong mat',int((acc>0.5).sum()))
Image.fromarray((T2*255+0.5).astype(np.uint8)).save(out_glb.replace('.glb','_tex.png'))
im=bpy.data.images.load(out_glb.replace('.glb','_tex.png')); im.colorspace_settings.name='sRGB'
for m in o.data.materials:
    for nd in m.node_tree.nodes:
        if nd.type=='TEX_IMAGE': nd.image=im
    b_=m.node_tree.nodes.get('Principled BSDF'); ec=b_.inputs['Emission Color']
    if ec.is_linked:
        src=ec.links[0].from_socket; m.node_tree.links.remove(ec.links[0]); m.node_tree.links.new(src,b_.inputs['Base Color']); b_.inputs['Emission Strength'].default_value=0
    b_.inputs['Roughness'].default_value=0.6; b_.inputs['Specular IOR Level'].default_value=0.15
bm.free()
# giảm lưới (~50 nghìn tam giác, ngang thân Meshy): nhẹ bộ nhớ khi nhiều diễn viên trong cảnh
nf=sum(len(pl.vertices)-2 for pl in o.data.polygons)
if nf>60000:
    md=o.modifiers.new('giam','DECIMATE'); md.ratio=50000/nf; md.use_collapse_triangulate=True
    bpy.context.view_layer.objects.active=o
    with bpy.context.temp_override(object=o, active_object=o): bpy.ops.object.modifier_apply(modifier='giam')
    print('giam luoi',nf,'->',len(o.data.polygons))
bpy.ops.export_scene.gltf(filepath=out_glb,export_format='GLB')
print('XONG',out_glb)
