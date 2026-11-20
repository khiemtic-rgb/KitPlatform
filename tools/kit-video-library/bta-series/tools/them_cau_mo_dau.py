# Gắn câu mở đầu lớn (0–2 giây) đè lên dải hình giữa của Short dọc 1080x1920.
# Dùng: python hook.py vao.mp4 ra.mp4 "DONG 1" "DONG 2" [cat_dau_giay]
import sys, subprocess
from PIL import Image, ImageDraw, ImageFont
F='/home/claude/kp/tools/kit-video-library/bta-series/fonts/'
src,out,cut=sys.argv[1],sys.argv[2],float(sys.argv[3]); lines=sys.argv[4:]
W,H=1080,1920; INK=(28,36,64,255); CREAM=(255,250,240); VERM=(236,96,70)
img=Image.new('RGBA',(W,H),(0,0,0,0)); d=ImageDraw.Draw(img)
def fit(t,mx):
    s=190
    while ImageFont.truetype(F+'BTA-Display.ttf',s).getlength(t)>mx: s-=4
    return s
s=min(fit(l,W*0.88) for l in lines)
f=ImageFont.truetype(F+'BTA-Display.ttf',s)
y0,y1=300,1200
d.rectangle((0,y0,W,y1),fill=INK)
lh=s*1.12; top=(y0+y1)/2-lh*(len(lines)-1)/2
for i,l in enumerate(lines):
    d.text((W/2,top+i*lh),l,font=f,fill=VERM if i==len(lines)-1 else CREAM,anchor='mm')
img.save('/tmp/hook_ov.png')
fc=f"[1:v]format=rgba,fade=t=out:st=2.0:d=0.15:alpha=1[o];[0:v][o]overlay=0:0:enable='lte(t,2.2)'[v]"
subprocess.run(['ffmpeg','-v','error','-y','-ss',str(cut),'-i',src,'-loop','1','-t','2.3','-i','/tmp/hook_ov.png',
  '-filter_complex',fc,'-map','[v]','-map','0:a','-c:v','libx264','-preset','veryfast','-crf','18','-pix_fmt','yuv420p',
  '-c:a','aac','-b:a','192k','-movflags','+faststart',out],check=True)
print(out, s)
