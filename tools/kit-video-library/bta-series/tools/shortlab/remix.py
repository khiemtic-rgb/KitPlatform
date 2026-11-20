import sys,re; sys.path.insert(0,'/home/claude/shortlab')
from shortlab import mix_audio
import subprocess,json
SUNO='/mnt/user-data/uploads/Between_Two_Asias/Nhac_Suno/'
# A: River Trap, -18 dB, tăng lên -11 dB trong 3 giây đếm ngược không lời
mix_audio('/home/claude/v03/voice_full.wav',[(224.85,247.97,0.35),(248.55,254.40,0)],SUNO+'BTA Tension - River Trap.wav','/tmp/rA.wav',
  music_vol="volume='if(between(t,6.15,9.45),0.28,0.125)':eval=frame")
# B: Lantern Market -17 dB, C: River Morning -17 dB (lấy lại đúng đoạn giọng như bản trước)
L=json.load(open('/home/claude/dai/lines2.json'))
pick=[1,2,3,4,15,18,20]; gaps=[0.3,0.3,0.35,0.45,0.35,0.3,0]
segs=[(L[k]['s2']-0.03,L[k]['e2']+0.05,g) for k,g in zip(pick,gaps)]
mix_audio('/home/claude/dai/p1-b.wav',segs,SUNO+'BTA Light - Lantern Market.wav','/tmp/rB.wav',music_db=-17)
mix_audio('/home/claude/v02/voice_full.wav',[(0.83,30.72,0)],SUNO+'BTA Theme - River Morning.wav','/tmp/rC.wav',music_db=-17)
for k,f in [('A','sA_ma'),('B','sB_cat'),('C','sC_1985')]:
    subprocess.run(['ffmpeg','-v','error','-y','-i',f+'.mp4','-i',f'/tmp/r{k}.wav','-map','0:v','-map','1:a','-c:v','copy','-c:a','aac','-b:a','192k','-shortest','-movflags','+faststart',f+'-v2.mp4'],check=True)
