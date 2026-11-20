import sys,json; sys.path.insert(0,'/home/claude/shortlab')
from shortlab import *
import shortlab
I1='/home/claude/kp/docs/marketing/bta/hinh-ai/01/'; I2='/home/claude/kp/docs/marketing/bta/hinh-ai/02/'
SUNO='/mnt/user-data/uploads/Between_Two_Asias/Nhac_Suno/'
T=json.load(open('/home/claude/v02/timeline.json'))['lines'][:7]
A=0.83
vd,tot=mix_audio('/home/claude/v02/voice_full.wav',[(A,30.72,0)],SUNO+'BTA Theme - River Morning.wav','/tmp/sC.wav',music_db=-25)
print(vd,tot)
caps=[]; WS={}
for li,L in enumerate(T):
    ws=L['shown'].split(); wt=L['wout']; n=min(len(ws),len(wt))
    if len(ws)!=len(wt): print('mm',li)
    for i in range(0,n,3):
        j=min(i+3,n)
        caps.append((wt[i][0]-A,(wt[j][0] if j<n else wt[n-1][1]+0.2)-A,' '.join(ws[i:j] if j<n else ws[i:])))
    for i,w in enumerate(ws[:n]): WS.setdefault(w.strip('.,'),wt[i][0]-A)
ts=lambda li: T[li]['out_s']-A
jp,cn,kr=WS['Japan'],[T[4]['wout'][i][0]-A for i,w in enumerate(T[4]['shown'].split()) if w=='China'][0],WS["Korea's"]
print(jp,cn,kr)
def kb(img,fx,fy,z0,z1,dark=0):
    im0=load(img)
    def f(t,u):
        im=cover(im0,fx,fy,z0+(z1-z0)*ease(u))
        if dark: im=Image.blend(im,Image.new('RGB',(W,H),(10,12,24)),dark)
        return im
    return f
def calendar(im,month,day,year,t_in,t,col=VERM,y=760,sc=1.0):
    u=min(1,max(0,(t-t_in)/0.35)); s=(0.6+0.4*(1-(1-u)**3))*sc
    cw,ch=int(560*s),int(620*s); x0,y0=W//2-cw//2,y-ch//2
    d=ImageDraw.Draw(im)
    d.rounded_rectangle((x0+12,y0+16,x0+cw+12,y0+ch+16),36,fill=(0,0,0))
    d.rounded_rectangle((x0,y0,x0+cw,y0+ch),36,fill=(250,246,236))
    d.rounded_rectangle((x0,y0,x0+cw,y0+int(170*s)),36,fill=col); d.rectangle((x0,y0+int(120*s),x0+cw,y0+int(170*s)),fill=col)
    d.text((W/2,y0+int(88*s)),month,font=DISP(int(100*s)),fill=(255,255,255),anchor='mm')
    d.text((W/2,y0+int(370*s)),day,font=DISP(int(300*s)),fill=INK,anchor='mm')
    d.text((W/2,y0+int(560*s)),year,font=BOLD(int(64*s)),fill=INK,anchor='mm')
def label(d,txt,col=GOLD,y=300,size=104):
    text_outline(d,(W/2,y),txt,fit_font(DISP,txt,W*0.86,size),col,stroke=7)
hanoi=kb(I2+'t01-hanoi-tet-street.png',0.5,0.5,1.05,1.2,0.25)
bj=kb(I1+'a02-beijing.png',0.5,0.4,1.2,1.05,0.25)
def s1(t,u):
    im=hanoi(t,u); calendar(im,'JAN','21','1985',0.0,t); label(ImageDraw.Draw(im),'VIETNAM: TẾT',VERM); return im
def s2(t,u):
    im=bj(t,u); t0=ts(1)
    # lật nhanh các ngày 22 Jan -> 20 Feb
    k=min(1,max(0,(t-t0)/1.6)); days=[('JAN',str(d)) for d in range(21,32)]+[('FEB',str(d)) for d in range(1,21)]
    m,dd=days[int(k*(len(days)-1))]
    calendar(im,m,dd,'1985',t0-0.4,t,col=(200,40,40)); label(ImageDraw.Draw(im),'CHINA: +1 MONTH' if k>=1 else 'CHINA ...'); return im
hn2=kb(I2+'t01-hanoi-tet-street.png',0.5,0.5,1.0,1.1); bj2=kb(I1+'a02-beijing.png',0.5,0.3,1.0,1.1)
def s3(t,u):
    a=hanoi(t,u).resize((W,H//2),box=(0,H//4,W,3*H//4)); b=bj(t,u).resize((W,H//2),box=(0,H//4,W,3*H//4))
    im=Image.new('RGB',(W,H)); im.paste(a,(0,0)); im.paste(b,(0,H//2)); d=ImageDraw.Draw(im)
    d.rectangle((0,H//2-5,W,H//2+5),fill=CREAM)
    calendar(im,'JAN','21','VIETNAM',ts(2)-0.2,t,y=470,sc=0.75); calendar(im,'FEB','20','CHINA',ts(2)-0.2,t,col=(200,40,40),y=1190,sc=0.75)
    return im
dark=kb(I2+'t05-ancestor-altar.sach.png',0.5,0.5,1.0,1.25,0.55)
def s4(t,u):
    im=dark(t,u); label(ImageDraw.Draw(im),'NOT THE STRANGEST PART',(255,255,255),y=760,size=96); return im
tk=kb(I1+'a04-tokyo.png',0.6,0.4,1.0,1.12,0.15); sl=kb(I1+'a03-seoul.png',0.42,0.5,1.0,1.12,0.15); bj3=kb(I1+'a02-beijing.png',0.5,0.4,1.0,1.12,0.15)
def s5(t,u):
    if t<cn: im=tk(t,u); txt=('JAPAN','MOVED TO JAN 1')
    elif t<kr: im=bj3(t,u); txt=('CHINA','RENAMED IT')
    else: im=sl(t,u); txt=('KOREA','BANNED FOR DECADES')
    d=ImageDraw.Draw(im); label(d,txt[0],GOLD,y=280); label(d,txt[1],(255,255,255),y=400,size=80); return im
mai=kb(I2+'t01-hanoi-tet-street.png',0.5,0.5,1.0,1.25,0.15)
def s6(t,u):
    im=mai(t,u); d=ImageDraw.Draw(im)
    if t<ts(6): label(d,'VIETNAM: STILL HERE',VERM,y=300)
    else: text_outline(d,(W/2,760),'TẾT',DISP(300),VERM,stroke=10)
    return im
scenes=[dict(t0=0,t1=ts(1)-0.3,draw=s1),dict(t0=ts(1)-0.3,t1=ts(2)-0.2,draw=s2),dict(t0=ts(2)-0.2,t1=ts(3)-0.2,draw=s3),
        dict(t0=ts(3)-0.2,t1=jp-0.1,draw=s4),dict(t0=jp-0.1,t1=ts(5)-0.2,draw=s5),dict(t0=ts(5)-0.2,t1=99,draw=s6)]
shortlab.chunks_from_line=lambda text,t0,t1,n=3:[(t0,t1,text)]
render(dict(dur=tot,audio='/tmp/sC.wav',scenes=scenes,lines=caps,cap_y=1500),'/home/claude/shortlab/sC_1985.mp4')
