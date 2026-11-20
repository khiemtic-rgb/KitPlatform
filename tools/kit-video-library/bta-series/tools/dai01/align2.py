import json,re,wave,subprocess
from pocketsphinx import Decoder
REP={'1985':'nineteen eighty five','1987':'nineteen eighty seven','1999':'nineteen ninety nine','2011':'twenty eleven',
     '2023':'twenty twenty three','2027':'twenty twenty seven','2035':'twenty thirty five','6':'sixth','areca':'eric a',
     'asias':'asia','meh':'may','naga':'nah ga','woodblock':'wood block'}
REP['asias']='asias'
L=json.load(open('lines.json'))
D='/usr/local/lib/python3.13/site-packages/pocketsphinx/model/en-us/cmudict-en-us.dict'
dic=set(l.split()[0] for l in open(D,encoding='utf-8',errors='ignore'))
def toks(t):
    out=[]
    for w in re.findall(r"[A-Za-z0-9']+", t.lower()):
        r=REP.get(w,w)
        if r=='asias': r='asia'
        for x in r.split():
            out.append(x if x in dic else 'uh')
    return out
res={}
for p,tag in [(1,'1-b'),(2,'2-a'),(3,'3-a')]:
    lines=[o for o in L if o['part']==p]
    seq=[];owner=[]
    for o in lines:
        tk=toks(o['text']); seq+=tk; owner+=[o['line']]*len(tk)
    d=Decoder(samprate=16000,bestpath=False)
    d.set_align_text(' '.join(seq))
    with wave.open(f'p16_{tag}.wav','rb') as w: data=w.readframes(w.getnframes())
    d.start_utt(); d.process_raw(data,full_utt=True); d.end_utt()
    segs=[(s.word,s.start_frame/100,s.end_frame/100) for s in d.seg()]
    ws=[s for s in segs if s[0] not in ('<sil>','<s>','</s>') and not s[0].startswith('+')]
    print(p,len(seq),len(ws))
    k=0
    for (wd,a,b) in ws:
        if k>=len(owner): break
        li=owner[k]; r=res.setdefault(li,[a,b]); r[0]=min(r[0],a); r[1]=max(r[1],b); k+=1
for o in L:
    r=res.get(o['line'])
    o['s2'],o['e2']=(r if r else (None,None))
json.dump(L,open('lines2.json','w'),ensure_ascii=False,indent=1)
for o in L: print(o['line'],o['s'],o['e'],'|',o['s2'],o['e2'],o['text'][:40])
