import sys, wave, re, json
from pocketsphinx import Decoder
def align(wavpath, text):
    d = Decoder(samprate=16000, bestpath=False)
    words = re.findall(r"[A-Za-z0-9']+", text.lower())
    d.set_align_text(" ".join(words))
    with wave.open(wavpath,'rb') as w: data=w.readframes(w.getnframes())
    d.start_utt(); d.process_raw(data, full_utt=True); d.end_utt()
    out=[]
    for seg in d.seg():
        out.append((seg.word, seg.start_frame/100, seg.end_frame/100))
    return out
if __name__=='__main__':
    import subprocess
    subprocess.run(['ffmpeg','-v','error','-y','-i',sys.argv[1],'-t',sys.argv[2],'/tmp/clip.wav'])
    for w in align('/tmp/clip.wav', sys.argv[3]): print(w)
