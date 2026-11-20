from PIL import Image; import numpy as np, sys
src,dst=sys.argv[1:3]
t=np.asarray(Image.open(src).convert('RGB')).astype(np.float32)/255
def ss(a,b,x): k=np.clip((x-a)/(b-a),0,1); return k*k*(3-2*k)
mx=t.max(2); mn=t.min(2); sat=(mx-mn)/np.maximum(mx,1e-3); v=mx
lum=t@np.array([0.3,0.59,0.11],np.float32)
w_h=ss(0.30,0.14,sat)*ss(0.80,0.64,v)
import cv2
# đốm sáng lọt giữa tóc (ít màu, xung quanh toàn tóc): cũng tô tối
w_blur=cv2.GaussianBlur(w_h.astype(np.float32),(0,0),5)
w_h=np.maximum(w_h, ss(0.45,0.7,w_blur)*ss(0.32,0.2,sat))
ref_hair=np.array([70,57,58],np.float32)/255
# toi: luminance map 0.5 -> ref hair lum, keep strand contrast
L_ref=float(ref_hair@np.array([0.3,0.59,0.11]))
newL=L_ref*(lum/0.5)**1.2
hair_col=np.clip(ref_hair[None,None,:]/L_ref*newL[...,None],0,1)
# skin
r,g,b=t[...,0],t[...,1],t[...,2]
w_s=ss(0.25,0.38,sat)*ss(0.45,0.6,v)*(r>g)*(g>b)
skin=np.clip(t*np.array([0.95,0.88,0.80],np.float32),0,1)
o=t*(1-w_h[...,None])+hair_col*w_h[...,None]
o=o*(1-w_s[...,None])+np.clip(o*np.array([0.95,0.88,0.80]),0,1)*w_s[...,None]
import os
if os.environ.get('SGD_DA_MIN'):
    # da trẻ: lấp nét sậm mảnh (vết xước / nếp của bản dựng) trên vùng da, giữ mảng màu
    import cv2
    bl=cv2.GaussianBlur(o,(0,0),6)
    lum_o=o@np.array([0.3,0.59,0.11],np.float32); lum_b=bl@np.array([0.3,0.59,0.11],np.float32)
    k=w_s*ss(-0.006,-0.03,lum_o-lum_b)
    o=o*(1-k[...,None])+bl*k[...,None]
    print('da min texel',int((k>0.5).sum()))
Image.fromarray((o*255+0.5).astype(np.uint8)).save(dst)
print('hair w mean',w_h.mean(),'skin w mean',w_s.mean())
