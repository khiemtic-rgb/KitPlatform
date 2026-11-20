import sys,json; sys.path.insert(0,'/home/claude/shortlab')
from shortlab import *
import shortlab
IMG='/home/claude/kp/docs/marketing/bta/hinh-ai/01/'
SUNO='/mnt/user-data/uploads/Between_Two_Asias/Nhac_Suno/'
L=json.load(open('/home/claude/dai/lines2.json'))
pick=[1,2,3,4,15,18,20]; gaps=[0.3,0.3,0.35,0.45,0.35,0.3,0]
segs=[];caps=[];t=0;starts={}
for k,g in zip(pick,gaps):
    x=L[k]; a,b=x['s2']-0.03,x['e2']+0.05
    segs.append((a,b,g)); off=t-a; starts[k]=(t,t+b-a)
    ws=x['shown'].split(); wt=x['words']
    if len(ws)!=len(wt): print('mismatch',k,len(ws),len(wt)); 
    n=min(len(ws),len(wt))
    for i in range(0,n,3):
        j=min(i+3,n)-1
        caps.append((wt[i][0]+off, (wt[j+1][0] if j+1<n else wt[j][1]+0.15)+off, ' '.join(ws[i:j+1]) if j+1<n else ' '.join(ws[i:])))
    if k==1: cw=[w[0]+off for w in wt]
    t+=b-a+g
vd,tot=mix_audio('/home/claude/dai/p1-b.wav',segs,SUNO+'BTA Light - Lantern Market.wav','/tmp/sB.wav',music_db=-24)
print(vd,tot,starts,cw)
def kb(path,fx,fy,z0,z1,dx=0):
    img=load(IMG+path)
    def f(t,u): return cover(img,fx+dx*ease(u),fy,z0+(z1-z0)*ease(u))
    return f
def lab(fn,txt,col=GOLD):
    def f(t,u):
        im=fn(t,u); d=ImageDraw.Draw(im)
        ft=fit_font(DISP,txt,W*0.85,110)
        text_outline(d,(W/2,300),txt,ft,col,stroke=7)
        return im
    return f
c=cw  # word starts of line 1: From Beijing to Seoul, from Tokyo to Singapore, the fourth animal is the rabbit.
s=starts
scenes=[
 dict(t0=0,t1=c[2],draw=lab(kb('a02-beijing.png',0.5,0.35,1.15,1.05),'BEIJING')),
 dict(t0=c[2],t1=c[4],draw=lab(kb('a03-seoul.png',0.45,0.5,1.1,1.0),'SEOUL')),
 dict(t0=c[4],t1=c[6],draw=lab(kb('a04-tokyo.png',0.25,0.4,1.0,1.1),'TOKYO')),
 dict(t0=c[6],t1=s[2][0],draw=lab(kb('a05-singapore.png',0.72,0.5,1.0,1.15),'4TH SIGN: RABBIT')),
 dict(t0=s[2][0],t1=s[3][0],draw=lab(kb('a06-hanoi-cat.png',0.42,0.4,1.3,1.05),'HANOI: CAT',VERM)),
 dict(t0=s[3][0],t1=s[15][0],draw=lab(kb('a08-cat-portrait.png',0.33,0.55,1.0,1.15),'1987 · 1999 · 2011')),
 dict(t0=s[15][0],t1=s[18][0],draw=lab(kb('c03-japan-forest-boar.png',0.45,0.55,1.0,1.12,0.03),'JAPAN: PIG TO BOAR')),
 dict(t0=s[18][0],t1=s[20][0],draw=lab(kb('c05-ricefield-night-cat.png',0.42,0.6,1.25,1.4),'OWN ANIMALS, SAME CLOCK')),
 dict(t0=s[20][0],t1=99,draw=lab(kb('a06-hanoi-cat.png',0.42,0.4,1.05,1.3),'ONLY VIETNAM: CAT',VERM)),
]
# render tự gom caps theo lines; truyền caps trực tiếp
shortlab.chunks_from_line=lambda text,t0,t1,n=3:[(t0,t1,text)]
spec=dict(dur=tot,audio='/tmp/sB.wav',scenes=scenes,lines=caps,cap_y=1450)
render(spec,'/home/claude/shortlab/sB_cat.mp4')
