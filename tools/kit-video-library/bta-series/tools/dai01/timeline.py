"""Dựng dòng thời gian video dài tập 01 từ 3 bản giọng (1b, 2a, 3a) + mốc từng chữ (pocketsphinx).
Ra: timeline.json (mốc câu/chữ/cảnh trên video), voice_full.wav (giọng đã chèn khoảng nghỉ), scene1_anchor.json."""
import json, subprocess, numpy as np
SR=48000
P={1:'p1-b.wav',2:'p2-a.wav',3:'p3-a.wav'}
def load(f):
    raw=subprocess.run(['ffmpeg','-v','error','-i',f,'-ac','1','-ar',str(SR),'-f','f32le','-'],capture_output=True,check=True).stdout
    return np.frombuffer(raw,np.float32).copy()
AUD={k:load(v) for k,v in P.items()}
L=json.load(open('lines2.json'))
TXT=open('/home/claude/kp/docs/marketing/bta/kich-ban/01-vietnam-zodiac-cat.txt',encoding='utf-8').read().split('\n')
scene_of=[];sc=-1
for l in TXT:
    if l.startswith('Cảnh '): sc+=1; continue
    if l.strip() and not l.startswith('#'): scene_of.append(sc)
for o,s in zip(L,scene_of): o['scene']=s
pieces=[]   # (out_t, part, src_s, src_e)
# ---------- cảnh 1: chèn khoảng nghỉ đúng các chỗ ngắt của bản dựng cũ ----------
old=json.load(open('old_words.json'))
oldt=old['t']
nw=[w for o in L[:6] for w in o['words']]
assert len(nw)==len(oldt)==71
OLD_PAUSES=[(4.38,0.8),(6.17,0.6),(8.00,0.6),(10.30,0.7),(11.48,0.35),(12.52,0.9),(14.66,0.3),(16.14,0.2),(17.50,0.6),(19.87,0.9),(21.62,0.5),(24.77,0.6),(27.68,0.6),(30.16,0.2)]
ins={}
for p,e in OLD_PAUSES:
    g=min(range(70),key=lambda i: abs((oldt[i][1]+oldt[i+1][0])/2-p))
    ins[g]=ins.get(g,0)+e
cuts=sorted(ins)
src_end1=nw[-1][1]+0.18
s0=0.0; out=0.0
def o1(t):  # giây trên giọng mới (phần 1) -> giây trên video
    return t+sum(ins[g] for g in cuts if (nw[g][1]+nw[g+1][0])/2 < t)
for g in cuts:
    mid=(nw[g][1]+nw[g+1][0])/2
    pieces.append((o1(s0),1,s0,mid)); s0=mid
pieces.append((o1(s0),1,s0,src_end1))
anch=[(0.0,0.0)]
for (a,b),(c,d) in zip(oldt,nw):
    anch+= [(o1(c),a),(o1(d),b)]
OLD_END=31.95
anch.append((o1(nw[-1][1])+(OLD_END-oldt[-1][1]),OLD_END))
json.dump({'anchors':anch},open('scene1_anchor.json','w'))
VEND=anch[-1][0]
S1_END=VEND+0.3
for o in L[:6]:
    o['out_s']=o1(o['words'][0][0]); o['out_e']=o1(o['words'][-1][1]); o['wout']=[[o1(a),o1(b)] for a,b in o['words']]
# ---------- cảnh 2–11: ghép từng câu, chèn nghỉ ----------
GAP_IN=0.50; GAP_SCENE=1.05; EXTRA_DOTS=0.25
t=S1_END+0.55
scenes=[{'scene':0,'start':0.0}]
for i in range(6,len(L)):
    o=L[i]; prev=L[i-1]
    a=max(0.0,o['s2']-0.06); b=min(len(AUD[o['part']])/SR,o['e2']+0.15)
    # không lấn sang câu kề trong cùng file
    if prev['part']==o['part']: a=max(a,(prev['e2']+o['s2'])/2)
    if i+1<len(L) and L[i+1]['part']==o['part']: b=min(b,(o['e2']+L[i+1]['s2'])/2)
    if i>6:
        g=GAP_SCENE if o['scene']!=prev['scene'] else GAP_IN
        if prev['shown'].rstrip().endswith('...'): g+=EXTRA_DOTS
        t+=g
    if o['scene']!=prev['scene']:
        scenes.append({'scene':o['scene'],'start':t-0.45 if i>6 else S1_END})
    pieces.append((t,o['part'],a,b))
    sh=t-a
    o['out_s']=o['s2']+sh; o['out_e']=o['e2']+sh; o['wout']=[[x+sh,y+sh] for x,y in o['words']]
    t+=b-a
VOICE_END=t
END_CARD=12.0
TOTAL=VOICE_END+1.2+END_CARD
for k,s in enumerate(scenes): s['end']=scenes[k+1]['start'] if k+1<len(scenes) else VOICE_END+1.2
N=int(TOTAL*SR)+SR
v=np.zeros(N,np.float32)
bad=[p for p in pieces if p[3]-p[2]<0.05]; print("bad",bad)
for ot,part,a,b in pieces:
    x=AUD[part][int(a*SR):int(b*SR)].copy()
    f=int(0.012*SR); x[:f]*=np.linspace(0,1,f); x[-f:]*=np.linspace(1,0,f)
    i=int(ot*SR); v[i:i+len(x)]+=x
subprocess.run(['ffmpeg','-v','error','-y','-f','f32le','-ar',str(SR),'-ac','1','-i','-','voice_full.wav'],input=v[:int(TOTAL*SR)].tobytes(),check=True)
json.dump({'lines':L,'scenes':scenes,'s1_end':S1_END,'vend':VEND,'voice_end':VOICE_END,'end_card':[VOICE_END+1.2,TOTAL],'total':TOTAL},open('timeline.json','w'),ensure_ascii=False,indent=1)
print('S1_END',round(S1_END,2),'VOICE_END',round(VOICE_END,2),'TOTAL',round(TOTAL,2))
for s in scenes: print(s['scene']+1, round(s['start'],2), round(s['end'],2), round(s['end']-s['start'],1))
