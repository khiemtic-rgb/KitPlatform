import sys; sys.path.insert(0,'/home/claude/shortlab')
from shortlab import *
ICON='/home/claude/kp/docs/marketing/bta/hinh-ai/03/'
SUNO='/mnt/user-data/uploads/Between_Two_Asias/Nhac_Suno/'
vd,tot=mix_audio('/home/claude/v03/voice_full.wav',[(224.85,247.97,0.35),(248.55,254.40,0)],SUNO+'BTA Light - Lantern Market.wav','/tmp/sA.wav',music_db=-24)
print('dur',vd,tot)
words=['ma','mà','má','mả','mã','mạ']
mean=['GHOST','BUT','CHEEK','TOMB','HORSE','RICE SEEDLING']
icons=[ICON+'t05-icon-ghost.png',None,ICON+'t05-icon-mother.png',ICON+'t05-icon-mound.png',ICON+'t05-icon-horse.png',ICON+'t05-icon-rice.png']
icons=[Image.open(p).convert('RGBA') if p else None for p in icons]
REV=[9.53,11.68,13.67,17.63,19.67,21.63]
paper=Image.new('RGB',(W,H),CREAM)
n=np.asarray(paper).astype(np.int16)+np.random.default_rng(2).normal(0,6,(H,W,1)).astype(np.int16)
paper=Image.fromarray(np.clip(n,0,255).astype(np.uint8))
def draw(t,u):
    im=paper.copy(); d=ImageDraw.Draw(im)
    # tiêu đề trên
    if t<6.1: hdr=('WHICH ONE','MEANS HORSE?')
    elif t<9.3: hdr=None
    elif t<REV[4]: hdr=('SIX WORDS.','ALL "MA".')
    elif t<23.4: hdr=('MÃ','= HORSE')
    else: hdr=('THE MARKS ARE','THE MEANING')
    if hdr:
        f=fit_font(DISP,hdr[0],W*0.86,120); d.text((W/2,250),hdr[0],font=f,fill=INK,anchor='mm')
        f=fit_font(DISP,hdr[1],W*0.86,120); d.text((W/2,375),hdr[1],font=f,fill=VERM,anchor='mm')
    else:
        k=3-int(t-6.1); k=max(1,min(3,k)); fr=(t-6.1)%1
        s=int(260-80*fr); d.text((W/2,310),str(k),font=DISP(s),fill=VERM,anchor='mm')
    # 6 thẻ 2x3
    cw,chh=460,330; xs=[W/2-cw/2-20, W/2+cw/2+20]; ys=[650,1010,1370]
    for i in range(6):
        cx=xs[i%2]; cy=ys[i//2]
        rv=t>=REV[i]
        hot=(i==4 and t>=REV[4])
        pop=1.0
        if rv: pop=1+0.08*math.exp(-6*(t-REV[i]))
        w2,h2=cw*pop/2,chh*pop/2
        fill=(255,250,240) if not hot else (255,236,224)
        d.rounded_rectangle((cx-w2,cy-h2,cx+w2,cy+h2),28,fill=fill,outline=VERM if hot else INK,width=8 if hot else 5)
        wy=cy-75 if rv else cy
        d.text((cx,wy),words[i],font=DISP(int((130 if rv else 150)*pop)),fill=INK,anchor='mm')
        if rv:
            ic=icons[i]; mtxt=mean[i]
            if ic is not None:
                icn=ic.copy(); icn.thumbnail((90,90)); im.paste(icn,(int(cx-icn.width/2),int(cy+10)),icn)
            f=fit_font(BOLD,mtxt,cw*0.86,50)
            d.text((cx,cy+128 if ic is not None else cy+70),mtxt,font=f,fill=VERM if hot else INK,anchor='mm')
    return im
spec=dict(dur=tot,audio='/tmp/sA.wav',scenes=[dict(t0=0,t1=99,draw=draw)],
          lines=[(0.03,6.11,'Six words. Every one is "ma". Which one means horse?'),(23.47,29.4,'In Vietnamese, the little marks are the meaning.')],
          cap_y=1660,words_per_cap=3)
render(spec,'/home/claude/shortlab/sA_ma.mp4')
