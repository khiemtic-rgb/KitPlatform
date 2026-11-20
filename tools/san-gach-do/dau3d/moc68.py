import cv2, numpy as np, json, sys, os
def lbf(path):
    img=cv2.imread(path); g=cv2.cvtColor(img,cv2.COLOR_BGR2GRAY)
    det=cv2.CascadeClassifier(os.environ.get('SGD_HAAR','/tmp/lbf/haar.xml')); f=det.detectMultiScale(g,1.1,4,minSize=(80,80))
    f=sorted(f,key=lambda r:-r[2]*r[3])[:1]
    fm=cv2.face.createFacemarkLBF(); fm.loadModel(os.environ.get('SGD_LBF','/tmp/lbf/lbfmodel.yaml'))
    ok,lm=fm.fit(g,np.array(f)); return img,np.array(lm[0]).reshape(-1,2)
def iris(img,lm,idx):
    pts=lm[idx].astype(np.int32); x0,y0=pts.min(0); x1,y1=pts.max(0)
    mask=np.zeros(img.shape[:2],np.uint8); cv2.fillPoly(mask,[pts],255)
    lum=img.astype(float)@[0.11,0.59,0.3]
    thr=np.percentile(lum[mask>0],45)
    ys,xs=np.where((mask>0)&(lum<thr)); c=(xs.mean(),ys.mean())
    col=img[ys,xs][:, ::-1].mean(0)
    return c,(x1-x0)/2,col
for p in sys.argv[1:]:
    img,lm=lbf(p)
    L=iris(img,lm,list(range(36,42))); R=iris(img,lm,list(range(42,48)))
    gap=R[0][0]-L[0][0]
    print('RES',p.split('/')[-1],'Lc',[round(v,1) for v in L[0]],'half',round(L[1],1),'col',L[2].round(),'Rc',[round(v,1) for v in R[0]],R[2].round(),'gap/facew',round(gap/(lm[16,0]-lm[0,0]),3),'nose-chin/gap',round((lm[8,1]-lm[33,1])/gap,3),'eye-mouth/gap',round((lm[62,1]-(L[0][1]+R[0][1])/2)/gap,3),'eyeW/gap',round(2*L[1]/gap,3))
    json.dump(lm.tolist(),open('lm_'+p.split('/')[-1]+'.json','w'))
